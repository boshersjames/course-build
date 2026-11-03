"""Check a course folder is ready for the toolkit, in ten seconds, before a session spends twenty minutes finding out.

    python tools/doctor.py            # from the course folder (or COURSE_JSON set)
    python tools/doctor.py --fix-lfs  # also run git lfs pull for the heightmap and GKD when they are pointers

Checks: course.json found and has frame + paths; toolkit path resolves; heightmap exists, is not an LFS pointer, has the right size;
GKD parses, hole count, tees have positions; SVG has green outlines that match GKD green centres; satellite, trees_csv, signs present
(warn); Python deps (numpy, scipy, matplotlib, PIL, pyproj, laspy, pillow_heif); external tools (git, git-lfs, ffmpeg, yt-dlp);
Unity project path exists (info on this machine); AutomationOutput latest run (info). Exit 1 on any FAIL.
"""
import argparse, importlib, json, os, shutil, subprocess, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

OUT = []
def say(level, what, detail=""):
    OUT.append((level, what, detail)); print("%-4s %-38s %s" % (level, what, detail))


def is_lfs_pointer(p):
    try:
        with open(p, "rb") as fh: return fh.read(40).startswith(b"version https://git-lfs")
    except Exception: return False


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter); ap.add_argument("--fix-lfs", action="store_true"); a = ap.parse_args()
    from rules import find_course_json
    cj = find_course_json()
    if not cj: say("FAIL", "course.json", "not found walking up from %s (set COURSE_JSON)" % os.getcwd()); return finish()
    say("ok", "course.json", cj)
    try: C = json.load(open(cj, encoding="utf-8-sig"))
    except Exception as e: say("FAIL", "course.json parses", str(e)); return finish()
    cdir = os.path.dirname(cj)
    for k in ("frame", "paths"):
        if k not in C: say("FAIL", "course.json has %s" % k, "run tools/new_course.py or add the block (see templates/course.template.json)")
    F = C.get("frame", {}); P = C.get("paths", {})
    for k in ("square_m", "sw_corner", "base_elevation_m", "relief_m", "crs"):
        if k not in F: say("FAIL", "frame.%s" % k, "missing")
    def rp(key):
        v = P.get(key)
        if not v: return None
        from rules import is_abs
        v = os.path.expandvars(os.path.expanduser(v)); return v if is_abs(v) else os.path.normpath(os.path.join(cdir, v))
    tk = rp("toolkit")
    if tk and os.path.isdir(os.path.join(tk, "rules")): say("ok", "paths.toolkit", tk)
    else: say("WARN", "paths.toolkit", "%s has no rules/ (tools still run from wherever they are; set it for the PC)" % tk)
    hm = rp("heightmap")
    if not hm or not os.path.exists(hm): say("FAIL", "heightmap", "%s missing" % hm)
    elif is_lfs_pointer(hm):
        if a.fix_lfs and shutil.which("git"):
            subprocess.run(["git", "-C", cdir, "lfs", "pull", "--include=" + os.path.relpath(hm, cdir)]); 
        if is_lfs_pointer(hm): say("FAIL", "heightmap", "LFS pointer: git lfs pull --include=%s" % os.path.relpath(hm, cdir))
        else: say("ok", "heightmap", "pulled through LFS")
    else:
        n = F.get("heightmap_px", 2049); sz = os.path.getsize(hm)
        say("ok" if sz == n * n * 2 else "FAIL", "heightmap size", "%d bytes (%s %d^2 x 2)" % (sz, "=" if sz == n * n * 2 else "expected", n))
    gk = rp("gkd")
    if not gk or not os.path.exists(gk): say("FAIL", "gkd", "%s missing" % gk)
    elif is_lfs_pointer(gk): say("FAIL", "gkd", "LFS pointer; git lfs pull --include=%s" % os.path.relpath(gk, cdir))
    else:
        try:
            g = json.load(open(gk, encoding="utf-8-sig")); holes = g.get("Holes", []); en = [h for h in holes if h.get("Enabled", True)]
            nop = [h["HoleNumber"] for h in en if not any(t.get("TeeType") == "GreenCenterPoint" and t.get("Position") for t in h.get("Tees", []))]
            say("ok" if not nop else "FAIL", "gkd parses", "%s: %d holes, %d enabled%s" % (g.get("CourseName"), len(holes), len(en), "" if not nop else ", no green centre on %s" % nop))
        except Exception as e: say("FAIL", "gkd parses", str(e))
    svg = rp("course_svg")
    if svg and os.path.exists(svg):
        try:
            from course_io import course, GKD, svg_outlines, match_outlines_to_holes
            Cc = course(); G = GKD(Cc); o = svg_outlines(Cc, "green"); m = match_outlines_to_holes(o, G); ne = len([h for h in G.holes if h.enabled and h.green_centre is not None])
            say("ok" if len(m) >= ne else "WARN", "svg greens match gkd", "%d outlines, %d of %d enabled holes matched" % (len(o), len(m), ne))
        except Exception as e: say("WARN", "svg greens match gkd", str(e)[:120])
    else: say("WARN", "course_svg", "missing (green outlines come from Unity exports only)")
    for k in ("satellite", "trees_csv", "signs", "photos_root"):
        p = rp(k); say("ok" if p and os.path.exists(p) else "WARN", "paths.%s" % k, p if p and os.path.exists(p) else "missing (%s)" % p)
    for mod in ("numpy", "scipy", "matplotlib", "PIL", "pyproj", "laspy", "pillow_heif"):
        try: importlib.import_module(mod); say("ok", "python " + mod)
        except Exception: say("WARN" if mod in ("laspy", "pillow_heif") else "FAIL", "python " + mod, "pip install " + {"PIL": "pillow", "pillow_heif": "pillow-heif"}.get(mod, mod))
    for tool in ("git", "git-lfs", "ffmpeg", "yt-dlp"):
        say("ok" if shutil.which(tool) else "WARN", "tool " + tool, "" if shutil.which(tool) else "not on PATH" + (" (needed for drone_frames fetch)" if tool == "yt-dlp" else " (needed for drone_frames cut / HEIC display)" if tool == "ffmpeg" else ""))
    up = rp("unity_project")
    say("info", "unity_project", ("%s exists" % up) if up and os.path.isdir(up) else "%s not on this machine" % up)
    ao = rp("automation_output"); lt = os.path.join(ao, "latest.txt") if ao else None
    say("info", "automation latest run", open(lt).read().strip() if lt and os.path.exists(lt) else "none here")
    return finish()


def finish():
    fails = [o for o in OUT if o[0] == "FAIL"]; warns = [o for o in OUT if o[0] == "WARN"]
    print("\n%d FAIL, %d WARN" % (len(fails), len(warns)))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
