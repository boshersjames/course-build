"""Clubhouse-area buildings: OSM footprints in course metres, with eave and ridge heights from the LiDAR (first-return
heights inside the footprint minus the ground around it). Writes buildings/buildings.json for MoorparkBuildings.cs.
    python tools/buildings.py
"""
import json, os
import xml.etree.ElementTree as ET
import numpy as np
from pyproj import Transformer
from shapely.geometry import Polygon

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(REPO, "buildings"); os.makedirs(OUT, exist_ok=True)
c = json.load(open(os.path.join(REPO, "course.json")))
AREA = (1300, 1650, 1600, 1900)          # x0, x1, z0, z1: clubhouse, cart barn, maintenance (course metres)
tf = Transformer.from_crs("EPSG:4326", "EPSG:%d" % c["epsg"], always_xy=True)
root = ET.parse(os.path.join(REPO, "map.osm")).getroot()
nodes = {}
for nd in root.iter("node"):
    e, n = tf.transform(float(nd.get("lon")), float(nd.get("lat")))
    nodes[nd.get("id")] = (e - c["sw_e"], n - c["sw_n"])
raw = np.fromfile(os.path.join(REPO, "heightmap_2049.raw"), "<u2").reshape(2049, 2049).astype(np.float32) / 65535 * c["terrain_height"]
def ground(x, z): return float(raw[int(round(z / 3000 * 2048)), int(round(x / 3000 * 2048))])
# first returns (if the LiDAR points are here) for roof heights; else a default single storey
pts = None
pz = os.path.join(REPO, "points.npz")
if os.path.exists(pz):
    d = np.load(pz); pts = d
out = []
for w in root.findall("way"):
    tg = {t.get("k"): t.get("v") for t in w.findall("tag")}
    if "building" not in tg: continue
    P = [nodes[r.get("ref")] for r in w.findall("nd") if r.get("ref") in nodes]
    if len(P) < 4: continue
    poly = Polygon(P)
    cx, cz = poly.centroid.x, poly.centroid.y
    if not (AREA[0] <= cx <= AREA[1] and AREA[2] <= cz <= AREA[3]): continue
    ring = list(poly.exterior.coords)[:-1]
    base = min(ground(x, z) for x, z in ring)
    levels = float(tg.get("building:levels", 1))
    eave = 3.6 * levels + 0.4
    out.append({"id": w.get("id"), "name": tg.get("name", ""), "type": tg.get("building"), "area_m2": round(poly.area),
                "base_y": round(base, 2), "eave": round(eave, 2), "roof_rise": round(min(5.0, 0.18 * min(poly.minimum_rotated_rectangle.length, 60) ** 0.9), 2),
                "ring": [[round(x, 2), round(z, 2)] for x, z in ring]})
json.dump(out, open(os.path.join(OUT, "buildings.json"), "w"), indent=1)
for b in sorted(out, key=lambda b: -b["area_m2"]):
    print("%-12s %-10s %-28s %5d m2  base %.1f  eave %.1f" % (b["id"], b["type"], b["name"], b["area_m2"], b["base_y"], b["eave"]))
