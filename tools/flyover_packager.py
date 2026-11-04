"""Package rendered 4K flyover frames into client-ready MP4s: verify, overlay, encode, verify, purge, log.

    python tools/flyover_packager.py [--holes 1-18|10] [--renders <AutomationOutput/renders_4k>] [--out <dir>] [--codec hevc|prores]
                                     [--keep-frames] [--no-overlays] [--dry-run] [--logo <png>] [--font <ttf>]

1. Verify: AutomationOutput/renders_4k/Hole_XX/frame_%05d.png must be contiguous from 0, match the expected count (from the cinematic
   trajectory JSON in AutomationOutput/flyovers, else --expect), all the same size, and every PNG must decode (CRC check via PIL verify()).
2. Overlays, one ffmpeg filtergraph: intro card (course name, hole, par, tee yardages from the GKD) for package.intro.seconds with fades;
   lower-left banner over the intro hold (hole, card yardage, par: rules package.banner); optional logo watermark (course.json paths.logo); lower-third distance-to-pin HUD (text + progress bar) driven by the trajectory
   JSON through sendcmd, so it changes every package.hud.interval_s without re-encoding per frame.
3. Encode: libx265 -crf 18 -preset slow, 3840x2160 @ 60, yuv420p, hvc1 tag, faststart (or ProRes 422 HQ .mov with --codec prores).
4. Verify the MP4 (ffprobe frame count == source frames), then purge the PNG folder unless --keep-frames.
5. Log AutomationOutput/logs/packager_<stamp>.json: per hole frames, verification, output path, size, encode seconds, purged.
Rules: rules/flyovers.json package. Paths: course.json (automation_output, gkd, logo). Exit 1 if any hole failed.
"""
import argparse, glob, json, os, re, shutil, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, GKD


def sh(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


def find_font(explicit):
    if explicit and os.path.exists(explicit): return explicit
    for p in ("C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/System/Library/Fonts/Helvetica.ttc"):
        if os.path.exists(p): return p
    return None


def esc(s):
    """escape for ffmpeg drawtext text= values"""
    return str(s).replace("\\", "\\\\").replace(":", "\\:").replace("'", "\\'").replace(",", "\\,").replace("%", "\\%")


def verify_frames(folder, expect, check_png, dims_must_match):
    files = sorted(glob.glob(os.path.join(folder, "frame_*.png")))
    idx = [int(re.search(r"frame_(\d+)\.png$", f).group(1)) for f in files]
    res = {"found": len(files), "expected": expect, "contiguous": idx == list(range(len(idx))), "corrupt": [], "size_mismatch": [], "dims": None}
    if files and check_png:
        from PIL import Image
        dims = None
        for f in files:
            try:
                im = Image.open(f); im.verify()
                im2 = Image.open(f); d = im2.size
                if dims is None: dims = d
                elif d != dims and dims_must_match: res["size_mismatch"].append(os.path.basename(f))
            except Exception as e:
                res["corrupt"].append(os.path.basename(f))
        res["dims"] = list(dims) if dims else None
    res["ok"] = bool(files) and res["contiguous"] and (expect is None or len(files) == expect) and not res["corrupt"] and not res["size_mismatch"]
    return res, files


def trajectory_for(hole, flyover_dir):
    cands = sorted(glob.glob(os.path.join(flyover_dir, "H%02d_cinematic_*.json" % hole)))
    cands = [c for c in cands if not c.endswith("_shots.json")]
    return json.load(open(cands[-1], encoding="utf-8-sig")) if cands else None


def hud_cmds(traj, fps, interval, label, pin_xyz, path):
    """sendcmd file: every interval, update the HUD text (distance to pin) from the trajectory."""
    frames = traj.get("frames_data", [])
    import math
    lines = []
    step = max(1, int(round(interval * fps)))
    for f in frames[::step]:
        t = f["t"]; p = f["pos"]; d = math.hypot(p[0] - pin_xyz[0], p[2] - pin_xyz[2]); yd = d * 1.09361
        lines.append("%.3f drawtext@hud reinit 'text=%s';" % (t, esc(label.format(yd=int(round(yd))))))
    open(path, "w").write("\n".join(lines) + "\n")
    return len(lines)


def bar_expr(traj, bar_w, segments=16):
    """drawbox w='...' as a piecewise-linear function of t: fraction of the arc flown, from the trajectory (ffmpeg drawbox has no live commands)."""
    fr = traj.get("frames_data", []); total = traj.get("arc_m", 1.0) or 1.0
    if len(fr) < 2: return "2"
    idx = [int(round(i * (len(fr) - 1) / segments)) for i in range(segments + 1)]
    pts = [(fr[i]["t"], min(1.0, fr[i]["d"] / total)) for i in idx]
    expr = "%.4f" % (pts[-1][1] * bar_w)
    for (t0, f0), (t1, f1) in reversed(list(zip(pts[:-1], pts[1:]))):
        slope = (f1 - f0) / max(t1 - t0, 1e-6)
        expr = "if(lt(t\\,%.3f)\\,%.4f+%.5f*(t-%.3f)\\,%s)" % (t1, f0 * bar_w, slope * bar_w, t0, expr)
    return "max(2\\,%s)" % expr


def build_filter(R, W, H, font, intro, logo, cmds_path, hud_on, bar_w_expr="2"):
    fg = []; vin = "[0:v]"; k = H / float(R.get("height", 2160))   # rule sizes are for 2160 px; scale to the frame
    B = dict(R["banner"]); T = B["seconds"]; fade = B["fade_s"]
    bw, bh, mx, my = int(B["width_px"] * k), int(B["height_px"] * k), int(B["margin_x_px"] * k), int(B["margin_y_px"] * k); tpx, spx = max(12, int(B["title_px"] * k)), max(10, int(B["sub_px"] * k)); pad = int(B["pad_px"] * k)
    alpha = "if(lt(t\\,%.2f)\\,t/%.2f\\,if(lt(t\\,%.2f)\\,1\\,max(0\\,(%.2f-t)/%.2f)))" % (fade, fade, T - fade, T, fade)
    y_box = H - my - bh
    box_f = "drawbox=x=%d:y=%d:w=%d:h=%d:color=%s:t=fill:enable='lt(t\\,%.2f)'" % (mx, y_box, bw, bh, B["bg"], T)
    intro_f = ("drawtext=fontfile='%s':text='%s':fontsize=%d:fontcolor=%s:x=%d:y=%d:alpha='%s':enable='lt(t\\,%.2f)'"
               % (font, esc(intro["title"]), tpx, B["color"], mx + pad, y_box + pad, alpha, T))
    sub_f = ("drawtext=fontfile='%s':text='%s':fontsize=%d:fontcolor=%s:x=%d:y=%d:alpha='%s':enable='lt(t\\,%.2f)'"
             % (font, esc(intro["sub"]), spx, B["color"], mx + pad, y_box + pad + tpx + int(8 * k), alpha, T))
    chain = vin + box_f + "," + intro_f + "," + sub_f
    if hud_on:
        Hh = dict(R["hud"]); m = int(Hh["margin_px"] * k); bw, bh = int(Hh["bar_px"][0] * k), max(2, int(Hh["bar_px"][1] * k)); Hh["font_px"] = max(10, int(Hh["font_px"] * k))
        chain += (",sendcmd=f='%s',drawbox@barbg=x=%d:y=%d:w=%d:h=%d:color=white@0.25:t=fill,drawbox@bar=x=%d:y=%d:w='%s':h=%d:color=white@0.85:t=fill,"
                  "drawtext@hud=fontfile='%s':text='':fontsize=%d:fontcolor=white:shadowcolor=black@0.6:shadowx=2:shadowy=2:x=%d:y=%d:enable='gte(t\\,%.2f)'"
                  % (cmds_path.replace("\\", "/").replace(":", "\\:"), m, H - m - bh, bw, bh, m, H - m - bh, bar_w_expr, bh, font, Hh["font_px"], m, H - m - bh - Hh["font_px"] - 14, Hh["show_after_s"]))
    chain += "[v1]"; fg.append(chain)
    if logo:
        Wm = R["watermark"]; mp = int(Wm["margin_px"] * k)
        fg.append("[1:v]scale=%d:-1,format=rgba,colorchannelmixer=aa=%.2f[lg]" % (max(16, int(Wm["width_px"] * k)), Wm["opacity"]))
        pos = {"top-right": "W-w-%d:%d" % (mp, mp), "top-left": "%d:%d" % (mp, mp), "bottom-right": "W-w-%d:H-h-%d" % (mp, mp), "bottom-left": "%d:H-h-%d" % (mp, mp)}[Wm["corner"]]
        fg.append("[v1][lg]overlay=%s[vout]" % pos)
    else:
        fg.append("[v1]null[vout]")
    return ";".join(fg)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--holes", default="1-18"); ap.add_argument("--renders"); ap.add_argument("--out"); ap.add_argument("--codec", choices=["hevc", "prores"])
    ap.add_argument("--keep-frames", action="store_true"); ap.add_argument("--no-overlays", action="store_true"); ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--logo"); ap.add_argument("--font"); ap.add_argument("--expect", type=int, help="expected frame count when no trajectory JSON exists"); ap.add_argument("--fps", type=int)
    a = ap.parse_args()
    C = course(); R = rules(C)["flyovers"]["package"]; G = GKD(C)
    AO = C.path("automation_output"); renders = a.renders or os.path.join(AO, "renders_4k"); flyover_dir = os.path.join(AO, "flyovers")
    out_dir = a.out or os.path.join(AO, "deliverables"); logs = os.path.join(AO, "logs"); os.makedirs(out_dir, exist_ok=True); os.makedirs(logs, exist_ok=True)
    codec = a.codec or R["codec"]; fps = a.fps or R["fps"]; font = find_font(a.font)
    logo = a.logo or (C.path("logo") if "logo" in C.get("paths", {}) else None); logo = logo if logo and os.path.exists(logo) else None
    if shutil.which("ffmpeg") is None: sys.exit("ffmpeg not on PATH")
    holes = []
    for part in a.holes.split(","):
        if "-" in part: x, y = part.split("-"); holes += list(range(int(x), int(y) + 1))
        else: holes.append(int(part))
    stamp = time.strftime("%Y%m%d_%H%M%S"); items = []; any_fail = False
    for hn in holes:
        folder = os.path.join(renders, "Hole_%02d" % hn); item = {"hole": hn, "folder": folder}
        if not os.path.isdir(folder): item.update({"status": "no frames folder"}); items.append(item); print("hole %d: no folder %s" % (hn, folder)); continue
        traj = trajectory_for(hn, flyover_dir); expect = a.expect or (int(round(traj["seconds"] * fps)) if traj and "seconds" in traj else None)
        ver, files = verify_frames(folder, expect, R["verify"]["check_png_integrity"], R["verify"]["dimensions_must_match"]); item["verify"] = ver
        if not ver["ok"]:
            item["status"] = "FRAMES_INVALID"; any_fail = True; items.append(item)
            print("hole %d: frames invalid: found %d expected %s contiguous %s corrupt %d size-mismatch %d" % (hn, ver["found"], expect, ver["contiguous"], len(ver["corrupt"]), len(ver["size_mismatch"]))); continue
        n = ver["found"]; W, H = ver["dims"] or (R["width"], R["height"])
        h = G.hole(hn); tees = {k: t for k, t in h.playing_tees().items()}
        yard = ", ".join("%s %d" % (k, round(t.get("Distance", 0) * 1.09361)) for k, t in sorted(tees.items(), key=lambda kv: -kv[1].get("Distance", 0)))
        tee_name = (traj or {}).get("tee"); tee_m = (tees.get(tee_name) or (max(tees.values(), key=lambda t: t.get("Distance", 0)) if tees else {})).get("Distance", 0)
        intro = {"title": R["banner"]["title"].format(hole=hn), "sub": R["banner"]["sub"].format(yd=int(round(tee_m * 1.09361)), par=h.par, tee=tee_name or "")}   # the lower-left banner over the intro hold: hole, card yardage of the flyover tee, par
        ext = "mp4" if codec == "hevc" else "mov"; out_path = os.path.join(out_dir, "%s_H%02d_4K.%s" % (C["name"], hn, ext))
        cmds = os.path.join(logs, "hud_H%02d.cmd" % hn); hud_on = bool(traj and traj.get("frames_data")) and not a.no_overlays
        bar_w_expr = "2"
        if hud_on:
            pin = traj["frames_data"][-1]["lookAt"]; hud_cmds(traj, fps, R["hud"]["interval_s"], R["hud"]["label"], pin, cmds)
            bar_w_expr = bar_expr(traj, int(R["hud"]["bar_px"][0] * (H / float(R.get("height", 2160)))))
        inputs = ["-framerate", str(fps), "-i", os.path.join(folder, R["verify"]["frame_pattern"])]
        if logo and not a.no_overlays: inputs += ["-i", logo]
        vf = ["-filter_complex", build_filter(R, W, H, font, intro, logo if not a.no_overlays else None, cmds, hud_on, bar_w_expr), "-map", "[vout]"] if (font and not a.no_overlays) else ["-vf", "format=%s" % R["pix_fmt"]]
        if not font and not a.no_overlays: item["warning"] = "no font found; overlays skipped"
        enc = (["-c:v", "libx265", "-crf", str(R["crf"]), "-preset", R["preset"], "-pix_fmt", R["pix_fmt"], "-tag:v", R["tag"], "-x265-params", "log-level=error", "-movflags", "+faststart"] if codec == "hevc"
               else ["-c:v", "prores_ks", "-profile:v", str(R["prores_profile"]), "-pix_fmt", "yuv422p10le", "-vendor", "apl0"])
        cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"] + inputs + vf + ["-r", str(fps)] + enc + [out_path]
        item["ffmpeg"] = " ".join(cmd)
        if a.dry_run: item["status"] = "dry-run"; items.append(item); print("hole %d: %d frames verified; would encode -> %s" % (hn, n, out_path)); continue
        t0 = time.time(); r = sh(cmd); item["encode_s"] = round(time.time() - t0, 1)
        if r.returncode != 0 or not os.path.exists(out_path): item["status"] = "ENCODE_FAILED"; item["stderr"] = r.stderr[-800:]; any_fail = True; items.append(item); print("hole %d: encode failed: %s" % (hn, r.stderr[-300:])); continue
        pr = sh(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames,width,height,r_frame_rate", "-of", "json", out_path])
        try:
            st = json.loads(pr.stdout)["streams"][0]; got = int(st.get("nb_read_frames", 0)); item["output_frames"] = got; item["output_dims"] = [st.get("width"), st.get("height")]
        except Exception: got = -1
        item["output"] = out_path; item["size_bytes"] = os.path.getsize(out_path); item["size_mb"] = round(item["size_bytes"] / 1e6, 1)
        if got != n: item["status"] = "OUTPUT_FRAME_MISMATCH (%d vs %d)" % (got, n); any_fail = True; items.append(item); print("hole %d: output has %d frames, source %d; frames kept" % (hn, got, n)); continue
        purged = False
        if R["cleanup"]["purge_frames_after_verified_encode"] and not a.keep_frames:
            shutil.rmtree(folder); purged = True
        item.update({"status": "ok", "purged_frames": purged, "frames": n}); items.append(item)
        print("hole %d: %d frames -> %s (%.1f MB, %.0f s)%s" % (hn, n, out_path, item["size_mb"], item["encode_s"], ", frames purged" if purged else ""))
    log = {"stamp": stamp, "course": C["name"], "codec": codec, "fps": fps, "overlays": not a.no_overlays, "font": font, "logo": logo, "items": items,
           "total_frames": sum(i.get("frames", 0) for i in items), "total_mb": round(sum(i.get("size_mb", 0) for i in items), 1), "total_encode_s": round(sum(i.get("encode_s", 0) for i in items), 1)}
    lp = os.path.join(logs, "packager_%s.json" % stamp); json.dump(log, open(lp, "w"), indent=1); print("log ->", lp)
    sys.exit(1 if any_fail else 0)


if __name__ == "__main__":
    main()
