"""Per hole: satellite crop with course.svg outlines, beside the drone contact sheet. Also a per-hole feature count."""
import csv, re, sys, os
from PIL import Image, ImageDraw, ImageFont

repo = sys.argv[1]
out = sys.argv[2]
os.makedirs(out, exist_ok=True)
S = 3000.0
sat = Image.open(os.path.join(repo, "satellite.jpg")).convert("RGB")
px = sat.width / S

svg = open(os.path.join(repo, "course.svg"), encoding="utf-8").read()
shapes = []   # (layer, id, [(x, z)])
for m in re.finditer(r'<path id="([a-z]+)_(\d+)"[^>]*\sd="([^"]+)"', svg):
    kind, idx, d = m.group(1), m.group(2), m.group(3)
    for sub in re.split(r"[Zz]", d):
        nums = re.findall(r"-?\d+\.?\d*", sub)
        pts = [(float(nums[i]), S - float(nums[i + 1])) for i in range(0, len(nums) - 1, 2)]
        if len(pts) >= 3:
            shapes.append((kind, kind + "_" + idx, pts))

col = {"green": (0, 255, 0), "fairway": (255, 255, 0), "tee": (0, 200, 255), "bunker": (255, 255, 255),
       "water": (0, 80, 255), "rough": None, "deep": None}

holes = {int(r["hole"]): r for r in csv.DictReader(open(os.path.join(repo, "holes.csv")))}
tees = {}
for r in csv.DictReader(open(os.path.join(repo, "tees.csv"))):
    tees.setdefault(int(r["hole"]), []).append((float(r["x"]), float(r["z"])))

font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 22)
def seg_dist(p, a, b):
    ax, az = a; bx, bz = b; px_, pz = p
    dx, dz = bx - ax, bz - az
    L = dx * dx + dz * dz
    t = 0 if L == 0 else max(0, min(1, ((px_ - ax) * dx + (pz - az) * dz) / L))
    return ((px_ - ax - t * dx) ** 2 + (pz - az - t * dz) ** 2) ** 0.5


summary = []
for h in range(1, 28):
    g = (float(holes[h]["green_x"]), float(holes[h]["green_z"]))
    pts = tees.get(h, []) + [g]
    x0, x1 = min(p[0] for p in pts) - 70, max(p[0] for p in pts) + 70
    z0, z1 = min(p[1] for p in pts) - 70, max(p[1] for p in pts) + 70
    w = max(x1 - x0, z1 - z0)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    x0, x1, z0, z1 = cx - w / 2, cx + w / 2, cz - w / 2, cz + w / 2
    crop = sat.crop((int(x0 * px), int((S - z1) * px), int(x1 * px), int((S - z0) * px))).resize((900, 900))
    k = 900 / w
    dr = ImageDraw.Draw(crop)
    counts = {}
    for kind, sid, poly in shapes:
        c = col.get(kind)
        if c is None:
            continue
        if not any(x0 <= x <= x1 and z0 <= z <= z1 for x, z in poly):
            continue
        # count only shapes near the hole line (within 45 m of tee-to-green segments)
        cxp = sum(p[0] for p in poly) / len(poly); czp = sum(p[1] for p in poly) / len(poly)
        near = min(seg_dist((cxp, czp), t, g) for t in tees.get(h, [g])) < 45
        if near:
            counts[kind] = counts.get(kind, 0) + 1
        dr.line([((x - x0) * k, (z1 - z) * k) for x, z in poly] + [((poly[0][0] - x0) * k, (z1 - poly[0][1]) * k)],
                fill=c, width=3 if near else 1)
    for t in tees.get(h, []):
        dr.ellipse([(t[0] - x0) * k - 6, (z1 - t[1]) * k - 6, (t[0] - x0) * k + 6, (z1 - t[1]) * k + 6], outline=(255, 0, 255), width=3)
    dr.ellipse([(g[0] - x0) * k - 8, (z1 - g[1]) * k - 8, (g[0] - x0) * k + 8, (z1 - g[1]) * k + 8], outline=(255, 0, 0), width=3)
    dr.text((8, 8), "%s  %s" % (tag.replace("_", " "), " ".join("%s:%d" % kv for kv in sorted(counts.items()))), fill=(255, 255, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
    tag = "%s_%d" % (["Ridgeline", "Creekside", "CanyonCrest"][(h - 1) // 9], (h - 1) % 9 + 1)
    sheet = Image.open(os.path.join(repo, "drone", "by_hole", tag, "contact_%s.jpg" % tag)).convert("RGB")
    sheet = sheet.resize((int(sheet.width * 900 / sheet.height), 900))
    combo = Image.new("RGB", (900 + sheet.width, 900))
    combo.paste(crop, (0, 0)); combo.paste(sheet, (900, 0))
    combo.thumbnail((2400, 2400))
    combo.save(os.path.join(out, tag + ".jpg"), quality=85)
    summary.append(tag + " par %s tees %d %s" % (holes[h]["par"], len(tees.get(h, [])), counts))


print('\n'.join(summary))
