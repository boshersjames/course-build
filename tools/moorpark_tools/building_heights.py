"""Roof heights of the clubhouse-area buildings from the 2018 LiDAR (only the EPT nodes over a 200 m box are fetched).
For each footprint in buildings/buildings.json: ground = 5th percentile of ground points within 6 m outside the walls,
eave = 15th percentile of roof returns 1.5-3 m in from the walls, ridge = 99th percentile inside; writes them back.
    python tools/building_heights.py
"""
import json, os, sys
import numpy as np
import shapely
from shapely.geometry import Polygon
REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
os.chdir(REPO); sys.path.insert(0, REPO)
import coursekit
c = json.load(open("course.json"))
X0, Z0, W = 1370.0, 1690.0, 300.0
box = dict(c); box["sw_e"] = c["sw_e"] + X0; box["sw_n"] = c["sw_n"] + Z0; box["size"] = W
parts = coursekit.lidar_from_ept(box, c["lidar_survey"])
x = np.concatenate([p[0] for p in parts]) + X0; z = np.concatenate([p[1] for p in parts]) + Z0
y = np.concatenate([p[2] for p in parts]) - c["y_offset"]; cl = np.concatenate([p[3] for p in parts])
print(len(x), "points")
B = json.load(open("buildings/buildings.json"))
for b in B:
    poly = Polygon(b["ring"])
    near = shapely.contains_xy(poly.buffer(6).difference(poly.buffer(1)), x, z) & (cl == 2)
    roof = shapely.contains_xy(poly.buffer(-0.5), x, z) & (cl != 2)
    edge = shapely.contains_xy(poly.buffer(-1.5).difference(poly.buffer(-3.0)), x, z) & (cl != 2)
    if near.sum() < 5 or roof.sum() < 20:
        print(b["id"], "too few points", near.sum(), roof.sum()); continue
    g = float(np.percentile(y[near], 5)); ridge = float(np.percentile(y[roof], 99))
    eave = float(np.percentile(y[edge], 15)) if edge.sum() > 10 else g + 4
    b.update(base_y=round(g, 2), eave=round(eave - g, 2), roof_rise=round(ridge - eave, 2), lidar_points=int(roof.sum()))
    print("%-11s %5d m2  ground %.1f  eave %.1f m  ridge %.1f m above ground (%d roof points)" % (b["id"], b["area_m2"], g, eave - g, ridge - g, roof.sum()))
json.dump(B, open("buildings/buildings.json", "w"), indent=1)
