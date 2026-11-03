"""LiDAR 'trees' that are really power pylons and wires: the 500 kV lines cross the course (drone: the pylons on the
Creekside 8 / Ridgeline ridges), and the tree finder took them as 20-35 m trees, which the SoCal swap then turned into
thin eucalyptus columns. Drops tall returns under the OSM power lines and towers; writes trees_clean.csv (same columns)
and trees_removed.csv (with the reason), and prints the height mix that is left.
    python tools/tree_clean.py
"""
import csv, json, os, re
import xml.etree.ElementTree as ET
import numpy as np
from pyproj import Transformer

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
c = json.load(open(os.path.join(REPO, "course.json")))
LINE_BUFFER, TOWER_BUFFER, MIN_H = 30.0, 15.0, 10.0      # metres; only returns taller than MIN_H are suspects


def seg_dist(p, a, b):
    ab = b - a; L = (ab ** 2).sum()
    t = np.clip(((p - a) @ ab) / max(L, 1e-9), 0, 1)
    return np.hypot(*(p - (a + t[:, None] * ab)).T)


root = ET.parse(os.path.join(REPO, "map.osm")).getroot()
tf = Transformer.from_crs("EPSG:4326", "EPSG:%d" % c["epsg"], always_xy=True)
nodes, towers = {}, []
for nd in root.iter("node"):
    e, n = tf.transform(float(nd.get("lon")), float(nd.get("lat")))
    xy = (e - c["sw_e"], n - c["sw_n"])
    nodes[nd.get("id")] = xy
    if any(t.get("k") == "power" and t.get("v") in ("tower", "pole") for t in nd.findall("tag")):
        towers.append(xy)
lines = []
for w in root.findall("way"):
    tg = {t.get("k"): t.get("v") for t in w.findall("tag")}
    if tg.get("power") in ("line", "minor_line", "cable"):
        lines.append(np.array([nodes[r.get("ref")] for r in w.findall("nd") if r.get("ref") in nodes]))
towers = np.array(towers) if towers else np.zeros((0, 2))
print("power: %d lines, %d towers/poles" % (len(lines), len(towers)))

rows = list(csv.DictReader(open(os.path.join(REPO, "trees.csv"))))
P = np.array([[float(r["x"]), float(r["z"])] for r in rows]); H = np.array([float(r["height_m"]) for r in rows])
dl = np.full(len(P), 1e9)
for L in lines:
    for a, b in zip(L[:-1], L[1:]):
        dl = np.minimum(dl, seg_dist(P, a, b))
dt = np.full(len(P), 1e9)
for t in towers:
    dt = np.minimum(dt, np.hypot(*(P - t).T))
drop = (H >= MIN_H) & ((dl < LINE_BUFFER) | (dt < TOWER_BUFFER))
keep = [r for r, d in zip(rows, drop) if not d]
with open(os.path.join(REPO, "trees_clean.csv"), "w", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(keep)
with open(os.path.join(REPO, "trees_removed.csv"), "w", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()) + ["reason"]); w.writeheader()
    for r, d, a, b in zip(rows, drop, dl, dt):
        if d: w.writerow(dict(r, reason="power line %.0f m / tower %.0f m" % (a, b)))
h = H[~drop]
print("kept %d, removed %d (under power lines, %d of them >= 20 m)" % (len(keep), drop.sum(), (drop & (H >= 20)).sum()))
for t in (12, 16, 20, 25):
    print("  kept >= %d m: %d" % (t, (h >= t).sum()))
