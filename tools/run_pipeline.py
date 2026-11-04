"""Master orchestrator: one command from fixes to packaged flyover videos for a course, through the Unity live bridge.

    python tools/run_pipeline.py --course tierra-rejada [--hole 0|10] [--tee Blue] [--audit <audit.json>] [--apply-fixes]
                                 [--skip fixer,calibrate,osm,path,render,package] [--continue-on-error] [--step-timeout 7200] [--dry-run]

Steps, in order (each logged with status, seconds, outputs; the run ABORTS on the first failure unless --continue-on-error):
  0 doctor      tools/doctor.py on the course folder (python)
  1 fixer       Unity BatchCourseFixer.RunDry (RunApply with --apply-fixes) on --audit (skipped when no audit JSON is given)
  2 calibrate   Unity GSProPostProfileCalibrator.RunCalibration (sun lock + measured post profile; needs the editor open)
  3 osm         Unity SpeedTreeOSMImporter.Run IF that class exists in the project (asked with 'describe'); otherwise logged as skipped.
                No such tool exists in course-build today (trees come from PlantLidarTrees / the tree tools); this slot is for it when it does.
  4 path-check  tools/flyover_path.py: the headless twin builds and checks every hole's flight first (LiDAR ground, same limits)
  4 path        Unity CommercialFlyoverPath.RunPathGeneration per hole (hole, tee)
  5 render      Unity RenderQueue4K.RunRenderQueue, ONE hole at a time (4K/60 PNG, ~13 GB per hole; the disk guard can halt it)
  6 package     tools/flyover_packager.py --holes <hole> right after that hole's render (verify, banner, HEVC, verify, purge), so the disk
                holds one hole's frames at a time; then the next hole

Why the live bridge and not Unity batch mode: calibration and the 4K renders need post-processing, which Unity does not run in batch
mode (the Uber shader is unsupported there). So the editor must be open with Course Tools > Automation > Live bridge on; this script
writes command files to <project>/Automation/inbox and waits for <project>/Automation/outbox/<id>.json (the same protocol as
automation/Send-UnityCommand.ps1). A 'ping' first; if Unity does not answer within --ping-timeout the run stops with that message.
Master log: <automation_output>/logs/pipeline_master_<stamp>.json.
"""
import argparse, glob, json, os, shutil, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, GKD

TOOLS = os.path.dirname(os.path.abspath(__file__))


def resolve_course(arg):
    """--course is a folder, or a sibling folder name next to course-build, or a course.json name."""
    if arg and os.path.isdir(arg) and os.path.exists(os.path.join(arg, "course.json")): return os.path.abspath(arg)
    root = os.path.dirname(os.path.dirname(TOOLS))
    for cand in (os.path.join(root, arg or ""), os.path.join(os.getcwd(), arg or "")):
        if arg and os.path.exists(os.path.join(cand, "course.json")): return os.path.abspath(cand)
    for cj in glob.glob(os.path.join(root, "*", "course.json")):
        try:
            if json.load(open(cj, encoding="utf-8-sig")).get("name", "").lower() == (arg or "").lower(): return os.path.dirname(cj)
        except Exception: pass
    if not arg and os.path.exists(os.path.join(os.getcwd(), "course.json")): return os.getcwd()
    sys.exit("course '%s' not found: pass a folder with course.json" % arg)


class Bridge:
    def __init__(self, project, timeout, poll=1.0):
        self.inbox = os.path.join(project, "Automation", "inbox"); self.outbox = os.path.join(project, "Automation", "outbox"); self.timeout = timeout; self.poll = poll
        os.makedirs(self.inbox, exist_ok=True); os.makedirs(self.outbox, exist_ok=True)

    def send_watch(self, cmd, tool, method, set_, timeout, watch_dirs, stall_s):
        """send, then wait for the answer while watching the frame folders: a long step whose frames stop arriving for stall_s means the editor is gone."""
        cid = time.strftime("%Y%m%d_%H%M%S_") + ("%03d" % int((time.time() % 1) * 1000))
        body = {"id": cid, "cmd": cmd, "config": "", "steps": "", "shots": True, "tool": tool, "method": method, "path": "", "scene": "", "set": [{"field": k, "value": str(v)} for k, v in (set_ or {}).items()]}
        tmp = os.path.join(self.inbox, cid + ".tmp"); json.dump(body, open(tmp, "w"), indent=1); os.replace(tmp, os.path.join(self.inbox, cid + ".json"))
        ans = os.path.join(self.outbox, cid + ".json"); t0 = time.time(); last_change = t0; last_count = -1
        while time.time() - t0 < timeout:
            if os.path.exists(ans):
                try: return json.load(open(ans, encoding="utf-8-sig"))
                except Exception: time.sleep(0.2); continue
            n = sum(len(glob.glob(os.path.join(d, "frame_*.png"))) for d in watch_dirs)
            if n != last_count: last_count = n; last_change = time.time()
            elif time.time() - last_change > stall_s:
                return {"id": cid, "status": "stalled", "message": "no new frame for %.0f s (%d on disk): the editor has stopped or crashed; check %%LOCALAPPDATA%%\\Unity\\Editor\\Editor.log, reopen Unity with the live bridge, and re-run: render_4k.resume keeps the frames already written" % (stall_s, n)}
            time.sleep(5)
        return {"id": cid, "status": "timeout", "message": "no answer from Unity in %.0f s" % timeout}

    def send(self, cmd, tool="", method="", set_=None, path="", config="", timeout=None):
        cid = time.strftime("%Y%m%d_%H%M%S_") + ("%03d" % int((time.time() % 1) * 1000))
        body = {"id": cid, "cmd": cmd, "config": config, "steps": "", "shots": True, "tool": tool, "method": method, "path": path, "scene": "", "set": [{"field": k, "value": str(v)} for k, v in (set_ or {}).items()]}
        tmp = os.path.join(self.inbox, cid + ".tmp"); json.dump(body, open(tmp, "w"), indent=1); os.replace(tmp, os.path.join(self.inbox, cid + ".json"))
        ans = os.path.join(self.outbox, cid + ".json"); t0 = time.time(); tmo = timeout or self.timeout
        while time.time() - t0 < tmo:
            if os.path.exists(ans):
                try: return json.load(open(ans, encoding="utf-8-sig"))
                except Exception: time.sleep(0.2)
            time.sleep(self.poll)
        return {"id": cid, "status": "timeout", "message": "no answer from Unity in %.0f s (is the editor open with Course Tools > Automation > Live bridge on?)" % tmo}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--course", required=True); ap.add_argument("--hole", type=int, default=0, help="0 = all enabled holes"); ap.add_argument("--tee", default="Blue")
    ap.add_argument("--audit", help="audit JSON for BatchCourseFixer"); ap.add_argument("--apply-fixes", action="store_true"); ap.add_argument("--skip", default="")
    ap.add_argument("--continue-on-error", action="store_true"); ap.add_argument("--step-timeout", type=float, default=7200); ap.add_argument("--ping-timeout", type=float, default=30); ap.add_argument("--render-stall", type=float, default=300, help="render step: seconds without a new frame before the editor is declared gone")
    ap.add_argument("--project", help="Unity project folder (default course.json paths.unity_project)"); ap.add_argument("--dry-run", action="store_true"); ap.add_argument("--codec", default=None)
    a = ap.parse_args()
    cdir = resolve_course(a.course); os.environ["COURSE_JSON"] = os.path.join(cdir, "course.json"); C = course(cdir); G = GKD(C)
    holes = [a.hole] if a.hole else [h.number for h in G.holes if h.enabled and h.green_centre is not None]
    skip = {s.strip() for s in a.skip.split(",") if s.strip()}
    project = a.project or C.path("unity_project"); AO = C.path("automation_output"); logs = os.path.join(AO, "logs"); os.makedirs(logs, exist_ok=True)
    stamp = time.strftime("%Y%m%d_%H%M%S"); master = {"stamp": stamp, "course": C["name"], "course_dir": cdir, "holes": holes, "tee": a.tee, "project": project, "dry_run": a.dry_run, "steps": []}
    t_all = time.time(); aborted = False

    def record(name, status, t0, message="", outputs=None, extra=None):
        step = {"step": name, "status": status, "seconds": round(time.time() - t0, 1), "message": (message or "")[:2000], "outputs": outputs or []}
        if extra: step.update(extra)
        master["steps"].append(step); print("[%-9s] %-8s %6.1fs  %s" % (name, status, step["seconds"], (message or "").splitlines()[0][:110] if message else ""))
        return status in ("ok", "skipped")

    def finish(code):
        master["aborted"] = aborted; master["total_seconds"] = round(time.time() - t_all, 1)
        lp = os.path.join(logs, "pipeline_master_%s.json" % stamp); json.dump(master, open(lp, "w"), indent=1); print("master log ->", lp); sys.exit(code)

    def fail():
        nonlocal aborted
        if a.continue_on_error: return
        aborted = True; finish(1)

    # 0 doctor
    t0 = time.time()
    if a.dry_run: record("doctor", "skipped", t0, "dry run")
    else:
        r = subprocess.run([sys.executable, os.path.join(TOOLS, "doctor.py")], cwd=cdir, capture_output=True, text=True)
        if not record("doctor", "ok" if r.returncode == 0 else "failed", t0, r.stdout[-1500:] + r.stderr[-500:]): fail()

    # bridge
    if a.dry_run:
        plan = ["ping"] + (["BatchCourseFixer.%s jsonPath=%s" % ("RunApply" if a.apply_fixes else "RunDry", a.audit)] if a.audit and "fixer" not in skip else []) + (["GSProPostProfileCalibrator.RunCalibration"] if "calibrate" not in skip else []) + (["describe SpeedTreeOSMImporter -> Run if present"] if "osm" not in skip else []) + (["CommercialFlyoverPath.RunPathGeneration hole=%d tee=%s" % (h, a.tee) for h in holes] if "path" not in skip else []) + (["RenderQueue4K.RunRenderQueue holes=%s" % ",".join(map(str, holes))] if "render" not in skip else []) + (["flyover_packager.py --holes %d" % h for h in holes] if "package" not in skip else [])
        record("plan", "ok", time.time(), "\n".join(plan), extra={"plan": plan}); finish(0)
    br = Bridge(project, a.step_timeout)
    t0 = time.time(); ans = br.send("ping", timeout=a.ping_timeout)
    if not record("ping", "ok" if ans.get("status") == "ok" else "failed", t0, ans.get("message")): fail()

    # 1 fixer
    t0 = time.time()
    if "fixer" in skip or not a.audit: record("fixer", "skipped", t0, "no --audit" if not a.audit else "skipped by flag")
    else:
        ans = br.send("invoke", "BatchCourseFixer", "RunApply" if a.apply_fixes else "RunDry", {"jsonPath": os.path.abspath(a.audit)})
        if not record("fixer", "ok" if ans.get("status") == "ok" else "failed", t0, ans.get("message"), sorted(glob.glob(os.path.join(AO, "checks", "batch_fixer_*.md")))[-1:]): fail()

    # 2 calibrate
    t0 = time.time()
    if "calibrate" in skip: record("calibrate", "skipped", t0, "skipped by flag")
    else:
        ans = br.send("invoke", "GSProPostProfileCalibrator", "RunCalibration")
        if not record("calibrate", "ok" if ans.get("status") == "ok" else "failed", t0, ans.get("message"), sorted(glob.glob(os.path.join(AO, "checks", "post_profile_*.json")))[-1:]): fail()

    # 3 osm importer (optional: only if the class exists)
    t0 = time.time()
    if "osm" in skip: record("osm", "skipped", t0, "skipped by flag")
    else:
        d = br.send("describe", "SpeedTreeOSMImporter", timeout=60)
        if d.get("status") != "ok": record("osm", "skipped", t0, "SpeedTreeOSMImporter is not in the project (no such tool in course-build yet): " + (d.get("message") or "")[:200])
        else:
            ans = br.send("invoke", "SpeedTreeOSMImporter", "Run", {"hole": a.hole, "tee": a.tee})
            if ans.get("status") != "ok" and "isn't compiled" in (ans.get("message") or ""): record("osm", "skipped", t0, "SpeedTreeOSMImporter is not compiled in this project (no such tool in course-build yet)")
            elif not record("osm", "ok" if ans.get("status") == "ok" else "failed", t0, ans.get("message")): fail()

    # 4 path per hole: the headless twin first (tools/flyover_path.py, same spline and limits, LiDAR ground), so Unity only builds a path that already passes
    if "path" in skip: record("path", "skipped", time.time(), "skipped by flag")
    else:
        t0 = time.time(); tw = subprocess.run([sys.executable, os.path.join(TOOLS, "flyover_path.py"), "--holes", ",".join(map(str, holes)), "--tee", a.tee, "--out", os.path.join(C.dir, "progress", "flyover_path")], cwd=cdir, capture_output=True, text=True)
        tw_rows = [l for l in tw.stdout.splitlines() if l.startswith("| ") and l.split("|")[1].strip().isdigit()]
        if not record("path-check", "ok" if tw.returncode == 0 else "failed", t0, "headless flight check (flyover_path.py):\n" + "\n".join(tw_rows) + ("" if tw.returncode == 0 else "\n" + tw.stderr[-800:] + "\nfix the flight in rules/flyovers.json cinematic (or this course's rule_overrides) and re-run; Unity is not asked for a failing path")): fail()
        for h in holes:
            t0 = time.time(); ans = br.send("invoke", "CommercialFlyoverPath", "RunPathGeneration", {"hole": h, "tee": a.tee})
            outs = sorted(glob.glob(os.path.join(AO, "flyovers", "H%02d_cinematic_*.json" % h)))[-1:]
            msg = ans.get("message") or ""; smooth_fail = "-> FAIL" in msg   # the path tool's own yaw/pitch/bank check: a failing path is not rendered
            no_report = " frames, " not in msg   # a path that was built always reports its frames; anything else is the tool refusing (GKD not found, hole missing)
            if not record("path", "ok" if (ans.get("status") == "ok" and not smooth_fail and not no_report) else "failed", t0, msg + ("\npath smoothness check FAILED: not rendering this path" if smooth_fail else "") + ("\npath tool returned no path report: step failed" if no_report else ""), outs, {"hole": h}): fail()

    # 5 + 6: per hole, render then package (the packager purges the frames after a verified encode), so 18 holes need one hole's frames of
    # disk at a time, ~13 GB, not 18 x that (Tierra 11/4: 118 GB free, 230 GB of frames if every hole rendered first)
    def render_hole(h):
        t0 = time.time()
        ans = br.send_watch("invoke", "RenderQueue4K", "RunRenderQueue", {"holes": str(h), "dryRun": "false"}, timeout=max(a.step_timeout, 3600),
                            watch_dirs=[os.path.join(AO, "renders_4k", "Hole_%02d" % h)], stall_s=a.render_stall)
        halted = "DISK_SPACE_EXCEEDED" in (ans.get("message") or "")
        qlog = sorted(glob.glob(os.path.join(AO, "logs", "render_queue_*.json")))[-1:]; status = "no entry"
        try:
            if qlog:
                for it in json.load(open(qlog[0], encoding="utf-8-sig")).get("items", []):
                    if int(it["hole"]) == h: status = str(it.get("status", ""))
        except Exception as e: status = "log unreadable: %s" % e
        n = len(glob.glob(os.path.join(AO, "renders_4k", "Hole_%02d" % h, "frame_*.png")))
        bad = n == 0 or not status.startswith("rendered")
        return record("render", "failed" if (ans.get("status") != "ok" or halted or bad) else "ok", t0, (ans.get("message") or "") + "\nhole %d: %d frames on disk; queue status: %s" % (h, n, status), qlog, {"hole": h, "halted": halted, "frames": n})
    def package_hole(h):
        t0 = time.time(); cmd = [sys.executable, os.path.join(TOOLS, "flyover_packager.py"), "--holes", str(h)] + (["--codec", a.codec] if a.codec else [])
        r = subprocess.run(cmd, cwd=cdir, capture_output=True, text=True)
        outs = sorted(glob.glob(os.path.join(AO, "deliverables", "%s_H%02d_4K.*" % (C["name"], h))))
        return record("package", "ok" if r.returncode == 0 else "failed", t0, r.stdout[-1200:] + r.stderr[-400:], outs, {"hole": h})
    for h in holes:
        if "render" in skip: record("render", "skipped", time.time(), "skipped by flag", extra={"hole": h})
        elif not render_hole(h): fail(); continue   # fail() exits unless --continue-on-error; then this hole is not packaged and the next is rendered
        if "package" in skip: record("package", "skipped", time.time(), "skipped by flag", extra={"hole": h})
        elif not package_hole(h): fail()

    finish(0 if all(s["status"] in ("ok", "skipped") for s in master["steps"]) else 1)


if __name__ == "__main__":
    main()
