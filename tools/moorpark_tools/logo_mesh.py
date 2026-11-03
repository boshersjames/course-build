"""logo/logo_strokes.json (stroke outlines from tools/logo_fit.py) -> logo/logo_mesh.json: flat triangles in course
metres (x east, z north) with points every 0.3 m inside, for Moorpark > Range Logo to drape on the terrain.
    python tools/logo_mesh.py
"""
import json, os, sys
import numpy as np
import shapely
from shapely.geometry import Polygon
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from opcd_prep import triangulate, resample, ring_coords

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
src = json.load(open(os.path.join(REPO, "logo", "logo_strokes.json")))
polys = [Polygon(p).buffer(0) for p in src["polys"]]
# outlines from find_contours: an outer ring per stroke and inner rings for its holes, so even-odd (symmetric
# difference) keeps a closed stroke's inside open instead of filling it
shape = Polygon()
for p in polys:
    if p.area > 0.15: shape = shape.symmetric_difference(p)
shape = shape.buffer(0.05).buffer(-0.05)
verts, tris, n0 = [], [], 0
for p in getattr(shape, "geoms", [shape]):
    loops = [resample(ring_coords(p.exterior), 0.25)] + [resample(ring_coords(h), 0.25) for h in p.interiors]
    V, F, _ = triangulate(loops, 0.3)
    if V is None: continue
    verts += [[round(float(x), 3), round(float(z), 3)] for x, z in V]
    tris += [[int(a) + n0, int(b) + n0, int(c) + n0] for a, b, c in F]
    n0 += len(V)
json.dump({"verts": verts, "tris": tris, "area_m2": round(shape.area, 1)}, open(os.path.join(REPO, "logo", "logo_mesh.json"), "w"))
b = shape.bounds
print("logo mesh: %d verts, %d tris, %.1f m2 of white, %.1f x %.1f m" % (len(verts), len(tris), shape.area, b[2] - b[0], b[3] - b[1]))
