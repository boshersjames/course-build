"""Index a photo dump: EXIF -> course frame -> hole -> GPS confidence -> draft pose -> index.csv, contact sheets, render shot list.

    python tools/photo_index.py [--dump course_photos/unsorted/<date>] [--out course_photos] [--sheets] [--shots]
    python tools/photo_index.py --dump D:/phone/2026-11-05 --date 2026-11-05

Reads HEIC and JPG (iPhone originals; HEIC via pillow-heif, else the embedded TIFF block), never modifies or renames them.
Writes <out>/index.csv (one row per image, the course-build-photo-index schema), <out>/sheets/hole_NN.jpg contact sheets
(thumbnails over the satellite crop when course.json paths.satellite exists), <out>/display/<stem>.jpg JPEG copies for sheets and
audits, and <out>/shots_photo_poses.json: a CourseAutomation shot list (one render per high/medium pose) so the PC renders every
photo's view in one batch run. Existing rows in index.csv are kept (pose_*, verdict, notes preserved by img stem); new images are added.
Rules: rules/photos.json (corridor, GPS confidence windows, eye height). Frame and declination: course.json.
"""
import argparse, csv, datetime as dt, glob, json, math, os, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageOps
from PIL.ExifTags import GPSTAGS, TAGS
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD
try:
    import pillow_heif; pillow_heif.register_heif_opener(); HEIF = True
except Exception:
    HEIF = False

COLS = ["hole", "img", "file", "lat", "lon", "alt", "heading", "pitch", "fov", "taken_at", "gps_confidence", "spot", "subject",
        "dist_to_hole_line_m", "pose_x", "pose_z", "pose_y", "pose_yaw", "pose_pitch", "pose_conf", "verdict", "notes",
        "x", "z", "hole2", "portrait", "focal35", "source", "batch"]


def dms(v):
    try: return float(v[0]) + float(v[1]) / 60 + float(v[2]) / 3600
    except Exception: return None


def exif_of(path):
    """(gps dict, exif ifd dict, orientation, width, height) from a JPG or HEIC."""
    ex = None; size = (0, 0); orient = 1
    try:
        im = Image.open(path); ex = im.getexif(); size = im.size; orient = ex.get(0x0112, 1)
    except Exception:
        ex = None
    if (ex is None or not ex.get_ifd(0x8825)) and path.lower().endswith((".heic", ".heif")):
        b = open(path, "rb").read()
        for m in re.finditer(rb"(MM\x00\x2a|II\x2a\x00)", b):
            try:
                e = Image.Exif(); e.load(b[m.start():])
                if e.get_ifd(0x8825): ex = e; break
            except Exception: pass
    if ex is None: return None, {}, orient, size
    g = {GPSTAGS.get(k, k): v for k, v in ex.get_ifd(0x8825).items()}
    x = {TAGS.get(k, k): v for k, v in ex.get_ifd(0x8769).items()}
    return g, x, orient, size


def parse_time(s):
    for f in ("%Y:%m:%d %H:%M:%S", "%Y-%m-%d %H:%M:%S"):
        try: return dt.datetime.strptime(str(s)[:19], f)
        except Exception: pass
    return None


def dist_along_line(P, q):
    best = 1e9
    for a, b in zip(P[:-1], P[1:]):
        ab = b - a; L2 = float(ab @ ab)
        u = 0 if L2 == 0 else float(np.clip((q - a) @ ab / L2, 0, 1))
        best = min(best, float(np.hypot(*(q - a - u * ab))))
    return best


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dump", action="append", help="folder(s) of originals; default every course_photos/unsorted/*")
    ap.add_argument("--out"); ap.add_argument("--date", help="batch label; default the dump folder name")
    ap.add_argument("--sheets", action="store_true"); ap.add_argument("--shots", action="store_true"); ap.add_argument("--no-display", action="store_true")
    a = ap.parse_args()
    C = course(); R = rules(C)["photos"]; G = GKD(C)
    try: HM = Heightmap(C)
    except Exception as e: HM = None; print("warning: no heightmap (%s); pose_y left blank" % e)
    root = a.out or C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, R["layout"]["root"])
    os.makedirs(root, exist_ok=True)
    dumps = a.dump or sorted(glob.glob(os.path.join(root, "unsorted", "*")))
    if not dumps: sys.exit("no dump folders under %s/unsorted" % root)
    from pyproj import Transformer
    tf = Transformer.from_crs("EPSG:4326", C["frame"]["crs"], always_xy=True); E0, N0 = C["frame"]["sw_corner"]
    decl = C.get("gps", {}).get("magnetic_declination_deg", 0.0)
    lines = {h.number: h.line() for h in G.holes if h.enabled and len(h.line()) >= 2}

    idx_p = os.path.join(root, "index.csv"); old = {}
    if os.path.exists(idx_p):
        for r in csv.DictReader(open(idx_p, encoding="utf-8-sig")): old[r["img"]] = r
    disp = os.path.join(root, "display"); os.makedirs(disp, exist_ok=True)

    rows = []
    for dump in dumps:
        batch = a.date or os.path.basename(os.path.normpath(dump))
        files = sorted(p for p in glob.glob(os.path.join(dump, "**", "*"), recursive=True) if p.lower().endswith((".heic", ".heif", ".jpg", ".jpeg")))
        # group HEIC + JPG of the same shot by stem
        by = {}
        for p in files: by.setdefault(re.sub(r"\(\d+\)$", "", os.path.splitext(os.path.basename(p))[0]), []).append(p)
        for stem, ps in sorted(by.items()):
            ps = sorted(set(ps), key=lambda q: (not q.lower().endswith((".heic", ".heif")), -os.path.getsize(q)))
            g = x = None; src = ps[0]; orient = 1; size = (0, 0)
            for p in ps:
                g, x, orient, size = exif_of(p)
                if g and "GPSLatitude" in g: src = p; break
            g = g or {}; x = x or {}
            r = {k: "" for k in COLS}; r.update(old.get(stem, {}))
            r["img"] = stem; r["file"] = os.path.relpath(src, root).replace("\\", "/"); r["batch"] = batch; r["source"] = r.get("source") or "iphone"
            t = parse_time(x.get("DateTimeOriginal") or x.get("DateTimeDigitized") or ""); r["taken_at"] = t.isoformat() if t else ""
            f35 = x.get("FocalLengthIn35mmFilm"); r["focal35"] = f35 or ""
            w, h = size; portrait = orient in (5, 6, 7, 8) or (h > w and orient == 1); r["portrait"] = int(portrait)
            if f35:
                hf = 2 * math.degrees(math.atan(18.0 / float(f35))); vf = 2 * math.degrees(math.atan(12.0 / float(f35)))
                r["fov"] = round(hf if portrait else vf, 1)   # vertical FOV of the shot as displayed (Unity uses vertical FOV)
            lat = dms(g.get("GPSLatitude")); lon = dms(g.get("GPSLongitude"))
            if lat is not None and lon is not None:
                if str(g.get("GPSLatitudeRef", "N")).upper().startswith("S"): lat = -lat
                if str(g.get("GPSLongitudeRef", "W")).upper().startswith("W"): lon = -lon
                r["lat"] = round(lat, 7); r["lon"] = round(lon, 7)
                alt = g.get("GPSAltitude"); r["alt"] = round(float(alt), 1) if alt is not None else ""
                E, N = tf.transform(lon, lat); r["x"] = round(E - E0, 2); r["z"] = round(N - N0, 2)
                hd = g.get("GPSImgDirection")
                if hd is not None:
                    hdg = float(hd) + (0 if str(g.get("GPSImgDirectionRef", "M")).upper().startswith("T") else decl); r["heading"] = round(hdg % 360, 1)
                q = np.array([r["x"], r["z"]])
                ds = sorted((dist_along_line(P, q), hn) for hn, P in lines.items())
                if ds and ds[0][0] <= R["hole_assignment"]["corridor_m"]:
                    r["hole"] = ds[0][1]; r["dist_to_hole_line_m"] = round(ds[0][0], 1)
                    if len(ds) > 1 and ds[1][0] <= R["hole_assignment"]["second_hole_within_m"]: r["hole2"] = ds[1][1]
                else:
                    r["hole"] = ""; r["dist_to_hole_line_m"] = round(ds[0][0], 1) if ds else ""
                    r["notes"] = (r.get("notes") or "") + (" off-course GPS (%.0f m from any hole line)" % ds[0][0] if ds else "")
                if not r.get("pose_conf"):   # draft pose from GPS + compass; a solved pose (refined_poses.json) overrides later
                    r["pose_x"] = r["x"]; r["pose_z"] = r["z"]; r["pose_yaw"] = r["heading"]; r["pose_pitch"] = r.get("pose_pitch") or 0
                    r["pose_y"] = round(HM.sample(r["x"], r["z"]) + R["pose"]["eye_height_m"], 2) if HM else ""
                    r["pose_conf"] = "draft"
            else:
                r["gps_confidence"] = "unknown"; r["hole"] = r.get("hole") or ""
            if not a.no_display:
                dp = os.path.join(disp, stem + ".jpg")
                if not os.path.exists(dp):
                    try:
                        im = ImageOps.exif_transpose(Image.open(src)).convert("RGB"); im.thumbnail((1600, 1600)); im.save(dp, quality=85)
                    except Exception as e:
                        r["notes"] = (r.get("notes") or "") + " display failed: %s" % e
            rows.append(r)

    # GPS confidence from neighbours in time (rules gps_confidence)
    GC = R["gps_confidence"]; timed = sorted([r for r in rows if r.get("taken_at") and r.get("x") != ""], key=lambda r: r["taken_at"])
    for i, r in enumerate(timed):
        t = dt.datetime.fromisoformat(r["taken_at"]); conf = "medium"
        prev = timed[i - 1] if i > 0 else None
        gap = (t - dt.datetime.fromisoformat(prev["taken_at"])).total_seconds() if prev else 1e9
        near = [s for s in timed if s is not r and abs((dt.datetime.fromisoformat(s["taken_at"]) - t).total_seconds()) <= GC["pair_window_s"]]
        if near:
            d = min(math.hypot(float(s["x"]) - float(r["x"]), float(s["z"]) - float(r["z"])) for s in near)
            conf = "high" if d <= GC["high_pair_agree_m"] else "low"
        if gap >= GC["low_after_gap_s"] and conf != "high": conf = "low"
        r["gps_confidence"] = conf
    for r in rows:
        if not r.get("gps_confidence"): r["gps_confidence"] = "unknown" if r.get("x") == "" else "medium"

    # solved poses from the course's progress folders (refined_poses.json keyed by img) override drafts
    solved = {}
    for p in glob.glob(os.path.join(C.dir, "progress", "**", "refined_poses.json"), recursive=True):
        try:
            d = json.load(open(p, encoding="utf-8-sig"))
            for k, v in (d.items() if isinstance(d, dict) else []):
                if isinstance(v, dict) and "x" in v: solved[k] = v
        except Exception: pass
    ns = 0
    for r in rows:
        v = solved.get(r["img"])
        if v:
            r["pose_x"] = v["x"]; r["pose_z"] = v["z"]; r["pose_yaw"] = v.get("heading", r.get("heading")); r["pose_pitch"] = v.get("pitch", 0)
            r["pose_y"] = round(HM.sample(float(v["x"]), float(v["z"])) + R["pose"]["eye_height_m"], 2) if HM else r.get("pose_y")
            r["pose_conf"] = "high" if v.get("score", 1) >= 0.5 else "medium"; r["fov"] = v.get("vfov", r.get("fov")); ns += 1

    # write index: keep old rows for images not in these dumps
    seen = {r["img"] for r in rows}
    allrows = rows + [o for k, o in old.items() if k not in seen]
    allrows.sort(key=lambda r: (str(r.get("hole") or "99").zfill(2), r.get("taken_at") or "", r["img"]))
    with open(idx_p, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=COLS, extrasaction="ignore"); w.writeheader(); [w.writerow({k: r.get(k, "") for k in COLS}) for r in allrows]
    n_gps = sum(1 for r in rows if r.get("x") != ""); by_hole = {}
    for r in rows: by_hole[r.get("hole") or "none"] = by_hole.get(r.get("hole") or "none", 0) + 1
    print("%d images in %d dump(s): %d with GPS, %d solved poses carried in; confidence %s" % (len(rows), len(dumps), n_gps, ns, {c: sum(1 for r in rows if r["gps_confidence"] == c) for c in ("high", "medium", "low", "unknown")}))
    print("per hole:", dict(sorted(by_hole.items(), key=lambda kv: str(kv[0]).zfill(2))))
    print("wrote", idx_p)

    if a.sheets:
        sat = None; sq = C["frame"]["square_m"]
        try:
            sp = C.path("satellite"); sat = Image.open(sp).convert("RGB") if os.path.exists(sp) else None
        except Exception: sat = None
        sd = os.path.join(root, "sheets"); os.makedirs(sd, exist_ok=True); TH = R["contact_sheet"]["thumb_px"]; NC = R["contact_sheet"]["cols"]; M = R["contact_sheet"]["satellite_crop_margin_m"]
        for hn in sorted({r["hole"] for r in rows if r.get("hole") != ""}, key=lambda v: int(v)):
            rs = [r for r in rows if r.get("hole") == hn]; P = lines[int(hn)]
            mapw = 0
            if sat is not None:
                ppm = sat.size[0] / sq
                xs = list(P[:, 0]) + [float(r["x"]) for r in rs]; zs = list(P[:, 1]) + [float(r["z"]) for r in rs]
                x0, x1, z0, z1 = min(xs) - M, max(xs) + M, min(zs) - M, max(zs) + M
                box = (int(x0 * ppm), int((sq - z1) * ppm), int(x1 * ppm), int((sq - z0) * ppm))
                crop = sat.crop(box); scale = 900 / max(crop.size); crop = crop.resize((int(crop.size[0] * scale), int(crop.size[1] * scale))); d = ImageDraw.Draw(crop)
                def px(x, z): return ((x - x0) * ppm * scale, (z1 - z) * ppm * scale)
                d.line([px(*p) for p in P], fill=(255, 255, 0), width=3)
                for i, r in enumerate(rs):
                    cx, cy = px(float(r["x"]), float(r["z"])); col = {"high": (0, 255, 0), "medium": (255, 200, 0), "low": (255, 80, 0)}.get(r["gps_confidence"], (200, 200, 200))
                    d.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=col, outline="black")
                    if r.get("heading") != "":
                        hd = math.radians(float(r["heading"])); d.line([cx, cy, cx + 22 * math.sin(hd), cy - 22 * math.cos(hd)], fill=col, width=2)
                    d.text((cx + 8, cy - 6), str(i + 1), fill="white", stroke_width=2, stroke_fill="black")
                mapw = crop.size[0]
            nrow = math.ceil(len(rs) / NC); sheet = Image.new("RGB", (mapw + NC * TH + 10, max(crop.size[1] if sat is not None else 0, nrow * (TH + 18) + 24)), "white"); d = ImageDraw.Draw(sheet)
            if sat is not None: sheet.paste(crop, (0, 24))
            d.text((4, 4), "Hole %s: %d photos (green=high, amber=medium, orange=low GPS); arrows = compass heading" % (hn, len(rs)), fill="black")
            for i, r in enumerate(rs):
                dp = os.path.join(disp, r["img"] + ".jpg")
                try:
                    im = Image.open(dp); im.thumbnail((TH, TH))
                    ox, oy = mapw + 10 + (i % NC) * TH, 24 + (i // NC) * (TH + 18); sheet.paste(im, (ox, oy))
                    d.text((ox + 2, oy + TH + 2), "%d %s %s %s" % (i + 1, r["img"], r["gps_confidence"], ("%.0f deg" % float(r["heading"])) if r.get("heading") != "" else ""), fill="black")
                except Exception: pass
            sheet.save(os.path.join(sd, "hole_%02d.jpg" % int(hn)), quality=82)
        print("sheets ->", sd)

    if a.shots:
        VC = R["pose"]["verdict_confidence"]; shots = []
        for r in rows:
            if r.get("pose_x") == "" or r.get("pose_yaw") in ("", None): continue
            if r.get("pose_conf") not in VC + ["draft"]: continue
            x, z, y = float(r["pose_x"]), float(r["pose_z"]), float(r["pose_y"] or 0); yaw = math.radians(float(r["pose_yaw"])); pitch = math.radians(float(r.get("pose_pitch") or 0))
            la = [x + 50 * math.sin(yaw) * math.cos(pitch), y + 50 * math.sin(pitch), z + 50 * math.cos(yaw) * math.cos(pitch)]
            shots.append({"name": "photo_%s" % r["img"], "pos": [x, y, z], "lookAt": la, "aboveGround": False, "fov": float(r["fov"] or 52),
                          "portrait": bool(int(r.get("portrait") or 0)), "pose_conf": r["pose_conf"], "hole": r.get("hole")})
        sp = os.path.join(root, "shots_photo_poses.json"); json.dump({"shots": shots}, open(sp, "w"), indent=1)
        print("%d render shots -> %s (merge into the automation config 'shots', or pass as a shots-only run)" % (len(shots), sp))


if __name__ == "__main__":
    main()
