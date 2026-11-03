"""Frames from an online drone flyover, per hole, into the same photo index as the iPhone shots (low position confidence).

    python tools/drone_frames.py fetch   [--source 0]                   # yt-dlp the video in course.json drone_sources[i] -> course_photos/drone/<label>.mp4
    python tools/drone_frames.py segments --video <mp4> [--holes 1-9]  # write drone/segments_<label>.csv: hole,start_s,end_s split evenly; edit it while watching
    python tools/drone_frames.py cut --video <mp4> --segments <csv> [--per-hole 6] [--index]
                                                                         # frames per hole -> drone/by_hole/H<NN>/H<NN>_<k>_<tee|mid|green>_t<sec>.jpg + contact sheet;
                                                                         # --index appends rows to course_photos/index.csv (source=drone_online, gps_confidence=low)

The segments file is the only hand step: watch the video once and write the second each hole's flight starts and ends (Moorpark's
drone/holes.csv is the model). Frames are named by their position in the hole's flight: first = tee, last = green, middle = mid.
A drone pose is drafted on the hole line (tee at start_s, green at end_s, height rules photos.drone.height_m, heading = line of play)
so audit_pairs can render the same view; pose_conf = low, and the frames are never used for colour (rules photos.sources.drone_online).
Videos stay out of git (course_photos/drone/*.mp4 is ignored); frames and sheets go in.
"""
import argparse, csv, json, math, os, re, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD


def video_duration(p):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", p], capture_output=True, text=True)
    return float(out.stdout.strip() or 0)


def parse_holes(s, n):
    if not s: return list(range(1, n + 1))
    out = []
    for part in s.split(","):
        if "-" in part: a, b = part.split("-"); out += list(range(int(a), int(b) + 1))
        else: out.append(int(part))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["fetch", "segments", "cut"]); ap.add_argument("--source", type=int, default=0); ap.add_argument("--video"); ap.add_argument("--segments")
    ap.add_argument("--holes"); ap.add_argument("--per-hole", type=int, default=6); ap.add_argument("--index", action="store_true"); ap.add_argument("--label")
    a = ap.parse_args()
    C = course(); R = rules(C)["photos"]; G = GKD(C)
    root = C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, "course_photos")
    ddir = os.path.join(root, "drone"); os.makedirs(ddir, exist_ok=True)
    srcs = C.get("drone_sources", [])

    if a.cmd == "fetch":
        if not srcs or a.source >= len(srcs): sys.exit("no drone_sources[%d] in course.json" % a.source)
        s = srcs[a.source]; label = a.label or re.sub(r"[^A-Za-z0-9]+", "_", s.get("label", "drone%d" % a.source)).strip("_")
        if not s.get("url"): sys.exit("drone_sources[%d] has no url" % a.source)
        outp = os.path.join(ddir, label + ".mp4")
        cmd = ["yt-dlp", "--no-playlist", "-f", "bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[height<=1080][ext=mp4]/b", "-o", outp, s["url"]]
        print(" ".join(cmd)); r = subprocess.run(cmd)
        if r.returncode: sys.exit("yt-dlp failed (network policy may deny youtube.com / googlevideo.com)")
        print("saved", outp, "%.0f s" % video_duration(outp))
        return

    if a.cmd == "segments":
        if not a.video: sys.exit("--video")
        dur = video_duration(a.video); holes = parse_holes(a.holes, C.get("holes", 18)); label = a.label or os.path.splitext(os.path.basename(a.video))[0]
        per = dur / len(holes); p = os.path.join(ddir, "segments_%s.csv" % label)
        with open(p, "w", newline="") as fh:
            w = csv.writer(fh); w.writerow(["hole", "start_s", "end_s", "video", "note"])
            for i, h in enumerate(holes): w.writerow([h, round(i * per, 1), round((i + 1) * per - 0.5, 1), os.path.basename(a.video), "EDIT: even split of %.0f s; set the real start/end while watching" % dur])
        print("wrote", p, "(even split; edit the seconds while watching the video)")
        return

    # cut
    if not (a.video and a.segments): sys.exit("--video and --segments")
    segs = list(csv.DictReader(open(a.segments, encoding="utf-8-sig")))
    try: HM = Heightmap(C)
    except Exception: HM = None
    DR = R.get("drone", {"height_m": 35, "frames_per_hole": 6}); n = a.per_hole or DR["frames_per_hole"]
    label = a.label or os.path.splitext(os.path.basename(a.video))[0]
    rows = []
    for s in segs:
        hn = int(s["hole"]); t0, t1 = float(s["start_s"]), float(s["end_s"]); hd = os.path.join(ddir, "by_hole", "H%02d" % hn); os.makedirs(hd, exist_ok=True)
        L = G.hole(hn).line(); seglen = np.r_[0, np.cumsum(np.hypot(*np.diff(L, axis=0).T))]
        thumbs = []
        for k in range(n):
            f = k / max(n - 1, 1); t = t0 + f * (t1 - t0); tag = "tee" if k == 0 else "green" if k == n - 1 else "mid"
            fn = "H%02d_%d_%s_t%03d.jpg" % (hn, k + 1, tag, round(t)); fp = os.path.join(hd, fn)
            if not os.path.exists(fp):
                subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", "%.2f" % t, "-i", a.video, "-frames:v", "1", "-q:v", "3", fp], check=False)
            if not os.path.exists(fp): print("no frame at %.1f s for hole %d" % (t, hn)); continue
            # draft pose on the hole line at the same fraction, drone height, looking along the line of play
            d = f * seglen[-1]; i = int(np.searchsorted(seglen, d, side="right") - 1); i = min(max(i, 0), len(L) - 2); u = (d - seglen[i]) / max(seglen[i + 1] - seglen[i], 1e-6)
            x, z = L[i] + u * (L[i + 1] - L[i]); v = L[i + 1] - L[i]; yaw = math.degrees(math.atan2(v[0], v[1])) % 360
            y = (HM.sample(float(x), float(z)) if HM else 0) + DR["height_m"]
            rows.append({"hole": hn, "img": os.path.splitext(fn)[0], "file": os.path.relpath(fp, root).replace("\\", "/"), "taken_at": "", "heading": round(yaw, 1), "fov": DR.get("vfov_deg", 60), "gps_confidence": "low",
                         "spot": tag, "subject": "drone frame %s t=%.0fs" % (label, t), "pose_x": round(float(x), 1), "pose_z": round(float(z), 1), "pose_y": round(float(y), 1), "pose_yaw": round(yaw, 1), "pose_pitch": -DR.get("pitch_deg", 25),
                         "pose_conf": "low", "source": "drone_online", "batch": label, "portrait": 0, "x": round(float(x), 1), "z": round(float(z), 1), "notes": "pose from the hole line, not the video; position +/- 50 m"})
            thumbs.append((fn, fp))
        if thumbs:
            TH = 320; sheet = Image.new("RGB", (TH * len(thumbs), TH * 9 // 16 + 20), "white"); d_ = ImageDraw.Draw(sheet)
            for j, (fn, fp) in enumerate(thumbs):
                im = Image.open(fp); im.thumbnail((TH, TH * 9 // 16)); sheet.paste(im, (j * TH, 20)); d_.text((j * TH + 3, 4), fn, fill="black")
            sheet.save(os.path.join(hd, "contact_H%02d.jpg" % hn), quality=82)
        print("hole %2d: %d frames %.0f-%.0f s -> %s" % (hn, len(thumbs), t0, t1, hd))
    if a.index and rows:
        ip = os.path.join(root, "index.csv"); cols = None; old = []
        if os.path.exists(ip):
            rd = list(csv.DictReader(open(ip, encoding="utf-8-sig"))); cols = rd[0].keys() if rd else None; old = [r for r in rd if r.get("img") not in {r2["img"] for r2 in rows}]
        from photo_index import COLS
        cols = list(cols or COLS)
        with open(ip, "w", newline="", encoding="utf-8") as fh:
            w = csv.DictWriter(fh, fieldnames=cols, extrasaction="ignore"); w.writeheader()
            for r in sorted(old + rows, key=lambda r: (str(r.get("hole") or "99").zfill(2), r.get("taken_at") or "", r["img"])): w.writerow({k: r.get(k, "") for k in cols})
        print("%d drone rows merged into %s" % (len(rows), ip))


if __name__ == "__main__":
    main()
