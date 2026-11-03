"""Per-photo audit sheets and measured colour: photo | before | after from the same solved pose, one row per photo in audit/index.csv.

    python tools/audit_pairs.py --renders <dir with photo_<IMG>.png>  [--before <dir>] [--holes 10,11] [--out course_photos/audit]
    python tools/audit_pairs.py --renders latest                       # the newest AutomationOutput run's shots folder

Renders are the CourseAutomation shots named photo_<IMG> (from photo_index.py --shots). For every indexed photo with a render:
  - compose <out>/<hole>/<IMG>.jpg: photo | (before |) after at the same height, labelled with pose confidence
  - measure colour in the same regions of photo and render (automatic turf mask: green-dominant pixels below the skyline; sky and
    very dark pixels excluded), report median RGB, r/g, b/g for both and the gap after the render warm offset (rules/look.json)
  - colour_ok when |gap r/g| and |gap b/g| are within rules/photos.json audit.colour_gap_max
  - verdict is left for the reviewer (match / mismatch / fixed / n-a) unless the row already has one; notes preserved
Only photos with pose_conf in rules photos.pose.verdict_confidence (plus 'draft' when --include-draft) are composed.
Writes <out>/index.csv (hole, img, pose_conf, render, photo_rg, photo_bg, render_rg, render_bg, gap_rg, gap_bg, colour_ok, verdict, notes).
"""
import argparse, csv, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageOps
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, latest_automation_run

COLS = ["hole", "img", "pose_conf", "render", "photo_rgb", "render_rgb", "photo_rg", "photo_bg", "render_rg", "render_bg", "gap_rg", "gap_bg", "turf_px", "colour_ok", "verdict", "notes"]


def turf_mask(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]; L = a.mean(2)
    sky = (b > g + 10) & (b > r + 25) | (L > 215)
    turf = (g >= r) & (g > b) & (L > 40) & ~sky
    h = a.shape[0]; turf[: h // 4] = False   # never the top quarter (sky, far hills)
    return turf


def stats(a, m):
    px = a[m].reshape(-1, 3)
    if len(px) < 200: return None
    med = np.median(px, axis=0); return [round(float(v), 1) for v in med], round(float(med[0] / med[1]), 3), round(float(med[2] / med[1]), 3), int(len(px))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--renders", required=True); ap.add_argument("--before"); ap.add_argument("--holes"); ap.add_argument("--out")
    ap.add_argument("--include-draft", action="store_true"); ap.add_argument("--height", type=int, default=720)
    a = ap.parse_args()
    C = course(); RP = rules(C)["photos"]; RL = rules(C)["look"]; warm = RL["measure"]["render_warm_offset"]; gapmax = RP.get("audit", {}).get("colour_gap_max", 0.05)
    root = C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, "course_photos")
    rd = a.renders
    if rd == "latest":
        run = latest_automation_run(C); rd = os.path.join(run, "shots") if run else None
        if not rd: sys.exit("no AutomationOutput/latest.txt on this machine; pass --renders <dir>")
    out = a.out or os.path.join(root, "audit"); os.makedirs(out, exist_ok=True)
    idx = list(csv.DictReader(open(os.path.join(root, "index.csv"), encoding="utf-8-sig")))
    holes = set(a.holes.split(",")) if a.holes else None
    old = {}
    ip = os.path.join(out, "index.csv")
    if os.path.exists(ip):
        for r in csv.DictReader(open(ip, encoding="utf-8-sig")): old[r["img"]] = r
    ok_conf = set(RP["pose"]["verdict_confidence"]) | ({"draft"} if a.include_draft else set())
    rows = []; made = 0; missing = 0
    for r in idx:
        if holes and r.get("hole") not in holes: continue
        if r.get("pose_conf") not in ok_conf: continue
        img = r["img"]; rp = None
        for ext in (".png", ".jpg"):
            p = os.path.join(rd, "photo_%s%s" % (img, ext))
            if os.path.exists(p): rp = p; break
        row = {k: "" for k in COLS}; row.update({k: v for k, v in old.get(img, {}).items() if k in ("verdict", "notes")})
        row["hole"] = r.get("hole"); row["img"] = img; row["pose_conf"] = r.get("pose_conf")
        if not rp: missing += 1; row["render"] = "missing"; rows.append(row); continue
        row["render"] = os.path.relpath(rp, C.dir).replace("\\", "/")
        pp = os.path.join(root, "display", img + ".jpg")
        if not os.path.exists(pp): pp = os.path.join(root, r["file"])
        photo = ImageOps.exif_transpose(Image.open(pp)).convert("RGB"); ren = Image.open(rp).convert("RGB")
        bef = None
        if a.before:
            for ext in (".png", ".jpg"):
                bp = os.path.join(a.before, "photo_%s%s" % (img, ext))
                if os.path.exists(bp): bef = Image.open(bp).convert("RGB"); break
        # colour: same mask shape on both, computed from the photo, applied to the render resized to the photo
        H = a.height; pa = np.asarray(photo.resize((int(photo.width * H / photo.height), H))).astype(float)
        ra = np.asarray(ren.resize((pa.shape[1], pa.shape[0]))).astype(float)
        m = turf_mask(pa) & turf_mask(ra)
        sp, sr = stats(pa, m), stats(ra, m)
        if sp and sr:
            row.update({"photo_rgb": sp[0], "photo_rg": sp[1], "photo_bg": sp[2], "render_rgb": sr[0], "render_rg": sr[1], "render_bg": sr[2], "turf_px": sp[3]})
            grg = round(sr[1] - warm - sp[1], 3); gbg = round(sr[2] - sp[2], 3); row["gap_rg"] = grg; row["gap_bg"] = gbg
            row["colour_ok"] = "yes" if abs(grg) <= gapmax and abs(gbg) <= gapmax else "no"
        else:
            row["colour_ok"] = "n/a"; row["notes"] = (row["notes"] + " no turf region").strip()
        panels = [("photo", photo)] + ([("before", bef)] if bef else []) + [("after" if bef else "render", ren)]
        tiles = [im.resize((int(im.width * H / im.height), H)) for _, im in panels]
        sheet = Image.new("RGB", (sum(t.width for t in tiles) + 8 * (len(tiles) - 1), H + 44), "white"); d = ImageDraw.Draw(sheet); x = 0
        for (lab, _), t in zip(panels, tiles):
            sheet.paste(t, (x, 44)); d.text((x + 4, 24), lab, fill="black"); x += t.width + 8
        col = ("r/g %.3f b/g %.3f vs render %.3f %.3f (gap %+.3f %+.3f) %s" % (sp[1], sp[2], sr[1], sr[2], row["gap_rg"], row["gap_bg"], "OK" if row["colour_ok"] == "yes" else "OFF")) if sp and sr else "no turf region"
        d.text((4, 6), "%s  hole %s  pose %s  |  turf %s" % (img, row["hole"], row["pose_conf"], col), fill="black")
        hd = os.path.join(out, str(row["hole"] or "none")); os.makedirs(hd, exist_ok=True); sheet.save(os.path.join(hd, img + ".jpg"), quality=85); made += 1
        rows.append(row)
    for k, o in old.items():
        if k not in {r["img"] for r in rows}: rows.append(o)
    rows.sort(key=lambda r: (str(r.get("hole") or "99").zfill(2), r["img"]))
    with open(ip, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=COLS, extrasaction="ignore"); w.writeheader(); [w.writerow({k: r.get(k, "") for k in COLS}) for r in rows]
    n_ok = sum(1 for r in rows if r.get("colour_ok") == "yes"); n_off = sum(1 for r in rows if r.get("colour_ok") == "no")
    print("%d sheets made, %d renders missing; turf colour within %.2f on %d, off on %d -> %s" % (made, missing, gapmax, n_ok, n_off, ip))


if __name__ == "__main__":
    main()
