"""Dry sandy washes crossing the holes (the Creekside "creeks" are sand in the satellite and drone video), as candidate
red penalty areas. NOT added to the GKDs: waiting for BoJa (do they hold water / are they played as penalty areas?).
Bright, unsaturated, elongated sand pieces over 150 m2 within 45 m of a hole line, minus bunkers, cart paths and roads.
    python tools/wash_candidates.py   -> build/water/washes.json (GKD hazard format, red, no drop zone) + washes_preview.jpg
"""
import csv, json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from skimage import measure
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import svg_fix, gspro_courses

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
S = 3000
sat = np.asarray(Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB").resize((S, S), Image.BOX), dtype=np.float32)
lum = sat.mean(2); spread = sat.max(2) - sat.min(2)
sand = (lum > 150) & (spread < 55)
sand = ndimage.binary_closing(ndimage.binary_opening(sand, iterations=1), iterations=2)
os.chdir(REPO)
lab = np.asarray(Image.fromarray(svg_fix.load_layers("course.svg")).resize((S, S), Image.NEAREST))
sand &= ~ndimage.binary_dilation(np.isin(lab, (svg_fix.BUNKER, svg_fix.GREEN, svg_fix.TEE, svg_fix.FAIRWAY)), iterations=3)
paths = Image.new("L", (S, S), 0); d = ImageDraw.Draw(paths); seg = {}
for r in csv.DictReader(open("cartpaths.csv")): seg.setdefault(r["segment"], []).append((float(r["x"]), S - float(r["z"])))
for p in seg.values(): d.line(p, fill=255, width=6)
sand &= ~(np.asarray(paths) > 0)
# near a hole line (back tee to green)
holes = list(csv.DictReader(open("holes.csv"))); tees = {}
for r in csv.DictReader(open("tees.csv")): tees.setdefault(int(r["hole"]), []).append((float(r["x"]), float(r["z"])))
near = Image.new("L", (S, S), 0); d = ImageDraw.Draw(near)
for i, h in enumerate(holes, start=1):
    t = tees.get(i, [None])[0]
    if t: d.line([(t[0], S - t[1]), (float(h["green_x"]), S - float(h["green_z"]))], fill=255, width=90)
sand &= np.asarray(near) > 0
comp, n = ndimage.label(sand)
out = []
for k, sl in enumerate(ndimage.find_objects(comp), start=1):
    m = comp[sl] == k
    a = m.sum()
    if a < 150: continue
    ys, xs = np.nonzero(m)
    pts = np.column_stack([xs, ys]).astype(float); pts -= pts.mean(0)
    ev = np.linalg.eigvalsh(np.cov(pts.T))
    if ev[1] / max(ev[0], 1e-6) < 6: continue           # washes are long and thin
    cont = max(measure.find_contours(np.pad(m, 1).astype(float), 0.5), key=len)
    cont = measure.approximate_polygon(cont, 1.0)
    coords = [{"x": round(c[1] - 1 + sl[1].start, 2), "z": round(S - (c[0] - 1 + sl[0].start), 2)} for c in cont[:-1]]
    for c in coords: c["y"] = round(gspro_courses.ground(c["x"], c["z"]), 3)
    cx, cz = np.mean([c["x"] for c in coords]), np.mean([c["z"] for c in coords])
    hole = min(range(1, 28), key=lambda i: (float(holes[i - 1]["green_x"]) - cx) ** 2 + (float(holes[i - 1]["green_z"]) - cz) ** 2)
    out.append({"near": "%s %d" % (["Ridgeline", "Creekside", "Canyon Crest"][(hole - 1) // 9], (hole - 1) % 9 + 1), "area_m2": int(a),
                "hazard": {"pointCount": len(coords), "coords": [{"x": c["x"], "y": c["y"], "z": c["z"]} for c in coords],
                           "DZpos": {"x": 0.0, "y": 0.0, "z": 0.0}, "freeDrop": False, "innerOOB": False, "noAIL": False, "hasDZ": False, "islandGreen": False}})
json.dump(out, open(os.path.join(REPO, "build", "water", "washes.json"), "w"), indent=1)
img = Image.open("satellite.jpg").convert("RGB"); k = img.width / S; d = ImageDraw.Draw(img)
font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 22)
for i, w in enumerate(out):
    P = [(c["x"] * k, (S - c["z"]) * k) for c in w["hazard"]["coords"]]
    d.line(P + [P[0]], fill=(255, 40, 40), width=4); d.text(P[0], "%d %s" % (i, w["near"]), fill=(255, 255, 0), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
img.crop((int(300 * k), int(1100 * k), int(2000 * k), int(2300 * k))).save(os.path.join(REPO, "build", "water", "washes_preview.jpg"), quality=85)
for i, w in enumerate(out): print(i, w["near"], w["area_m2"], "m2")
