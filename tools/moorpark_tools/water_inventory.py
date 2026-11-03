"""Every pond, lake and creek in or near the course: OSM water polygons and waterways, plus open water found in the
satellite (dark blue-green, smooth) and LiDAR (no ground returns / flat), per hole. Writes build/water/water.json and
build/water/water_overview.jpg (outlines on the satellite, numbered).
    python tools/water_inventory.py
"""
import csv, json, os
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from pyproj import Transformer
from scipy import ndimage

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(REPO, "build", "water"); os.makedirs(OUT, exist_ok=True)
c = json.load(open(os.path.join(REPO, "course.json")))
S = float(c["size"])
tf = Transformer.from_crs("EPSG:4326", "EPSG:%d" % c["epsg"], always_xy=True)
root = ET.parse(os.path.join(REPO, "map.osm")).getroot()
nodes = {}
for nd in root.iter("node"):
    e, n = tf.transform(float(nd.get("lon")), float(nd.get("lat")))
    nodes[nd.get("id")] = (e - c["sw_e"], n - c["sw_n"])
holes = list(csv.DictReader(open(os.path.join(REPO, "holes.csv"))))
NINES = ["Ridgeline", "Creekside", "Canyon Crest"]
def hole_name(h): h = int(h); return "%s %d" % (NINES[(h - 1) // 9], (h - 1) % 9 + 1)
G = np.array([[float(h["green_x"]), float(h["green_z"])] for h in holes])
tees = {}
for r in csv.DictReader(open(os.path.join(REPO, "tees.csv"))):
    tees.setdefault(int(r["hole"]), []).append((float(r["x"]), float(r["z"])))


def nearest_holes(pts, k=2):
    """holes whose tee-green line passes within 60 m of the feature"""
    out = []
    for i, h in enumerate(holes, start=1):
        g = G[i - 1]
        for t in tees.get(i, [])[:1]:
            a, b = np.array(t), g
            ab = b - a; L = (ab ** 2).sum()
            tt = np.clip(((pts - a) @ ab) / max(L, 1e-9), 0, 1)
            d = np.hypot(*(pts - (a + tt[:, None] * ab)).T).min()
            if d < 60: out.append((d, hole_name(i)))
    return [n for _, n in sorted(out)[:k]]


feats = []
for w in root.findall("way"):
    tg = {t.get("k"): t.get("v") for t in w.findall("tag")}
    pts = [nodes[r.get("ref")] for r in w.findall("nd") if r.get("ref") in nodes]
    if len(pts) < 2: continue
    kind = None
    if tg.get("natural") == "water" or tg.get("water") or tg.get("landuse") in ("reservoir", "basin") or tg.get("golf") in ("water_hazard", "lateral_water_hazard"):
        kind = "pond" if len(pts) > 2 and pts[0] == pts[-1] else None
    if tg.get("waterway") in ("stream", "ditch", "drain", "canal", "river"):
        kind = "creek"
    if not kind: continue
    P = np.array(pts)
    if (P[:, 0].max() < 0) or (P[:, 0].min() > S) or (P[:, 1].max() < 0) or (P[:, 1].min() > S): continue
    area = 0.5 * abs(np.dot(P[:-1, 0], P[1:, 1]) - np.dot(P[1:, 0], P[:-1, 1])) if kind == "pond" else 0
    length = float(np.hypot(*np.diff(P, axis=0).T).sum())
    feats.append(dict(source="osm", kind=kind, osm_id=w.get("id"), tags={k: v for k, v in tg.items() if k in ("name", "natural", "water", "waterway", "golf", "intermittent", "landuse")},
                      area_m2=round(float(area)), length_m=round(length), pts=[[round(x, 2), round(z, 2)] for x, z in pts], holes=nearest_holes(P)))

# satellite open water: dark, blue >= green, low texture, not already OSM
sat = np.asarray(Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB").resize((int(S), int(S)), Image.BOX), dtype=np.float32)
R_, G_, B_ = sat[..., 0], sat[..., 1], sat[..., 2]
lum = sat.mean(2)
water = (B_ >= G_ - 4) & (B_ > R_ + 6) & (lum < 120)
water = ndimage.binary_opening(water, iterations=2)
lab, n = ndimage.label(water)
osm_mask = Image.new("L", (int(S), int(S)), 0); dr = ImageDraw.Draw(osm_mask)
for f in feats:
    if f["kind"] == "pond": dr.polygon([(x, S - z) for x, z in f["pts"]], fill=255)
osm_mask = np.asarray(osm_mask) > 0
for i, sl in enumerate(ndimage.find_objects(lab), start=1):
    m = lab[sl] == i
    a = int(m.sum())
    if a < 150: continue
    ys, xs = np.nonzero(m)
    cx, cz = xs.mean() + sl[1].start, S - (ys.mean() + sl[0].start)
    if osm_mask[int(S - cz), int(cx)]: continue
    hs = nearest_holes(np.array([[cx, cz]]))
    if not hs: continue                                   # only water that is in play near a hole
    feats.append(dict(source="satellite", kind="pond?", area_m2=a, centre=[round(cx, 1), round(cz, 1)], holes=hs))

json.dump(feats, open(os.path.join(OUT, "water.json"), "w"), indent=1)
img = Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB")
k = img.width / S
d = ImageDraw.Draw(img)
font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 26)
for i, f in enumerate(feats):
    col = (0, 160, 255) if f["kind"] == "pond" else (0, 255, 255) if f["kind"] == "creek" else (255, 0, 255)
    if "pts" in f:
        P = [(x * k, (S - z) * k) for x, z in f["pts"]]
        d.line(P + ([P[0]] if f["kind"] == "pond" else []), fill=col, width=5)
        x, y = P[len(P) // 2]
    else:
        x, y = f["centre"][0] * k, (S - f["centre"][1]) * k
        d.ellipse([x - 18, y - 18, x + 18, y + 18], outline=col, width=5)
    d.text((x + 8, y + 8), str(i), fill=(255, 255, 0), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
img.thumbnail((3000, 3000)); img.save(os.path.join(OUT, "water_overview.jpg"), quality=85)
for i, f in enumerate(feats):
    print("%2d %-9s %-6s area %6s m2 len %5s m  %s  %s" % (i, f["source"], f["kind"], f.get("area_m2", ""), f.get("length_m", ""), f.get("tags", ""), ", ".join(f["holes"]) or "-"))
