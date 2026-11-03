"""Check the OPCD mesh plan: coverage (gaps / overlaps) at 5 cm in a few windows, and a wireframe picture of each.
    python tools/opcd_check.py   -> build/opcd_check_*.png, prints coverage stats
"""
import os, pickle
import numpy as np
from PIL import Image, ImageDraw

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
plan = pickle.load(open(os.path.join(REPO, "build", "opcd_plan.pkl"), "rb"))
S = plan["size"]
COL = {"Rough": (40, 130, 56), "Deep": (22, 75, 32), "Fairway": (67, 229, 97), "Tee": (160, 229, 184), "Green": (188, 229, 164),
       "Bunker": (229, 229, 170), "Water_Base_Lake": (0, 0, 192)}
# windows in course metres (east, north, size): H09 pond green (Ridgeline 9), H05 green, H20 fairway bunkers
WINDOWS = {"H09_green": (1309.9, 1776.1, 70), "H05_green": (463.9, 1019.6, 60), "H20_bunkers": (1890, 1700, 90)}


def faces_of(o):
    if o["kind"] == "blend":
        a, b = np.asarray(o["strip"][0]), np.asarray(o["strip"][1]); n = len(a)
        V = np.vstack([a, b]); F = [(j, (j + 1) % n, n + (j + 1) % n, n + j) for j in range(n)]
    else:
        V = np.asarray(o["verts"]); F = o["faces"]
    return V, F


for wname, (e, nn, size) in WINDOWS.items():
    res = 0.05
    N = int(size / res)
    X0, Y0 = -(e + size / 2), -(nn + size / 2)          # Blender frame: X = -east, Y = -north
    cover = np.zeros((N, N), np.uint8)
    pic = Image.new("RGB", (N, N), (90, 70, 50))
    wire = ImageDraw.Draw(pic)
    for o in plan["objects"]:
        V, F = faces_of(o)
        if not len(V): continue
        if (V[:, 0].max() < X0 or V[:, 0].min() > X0 + size or V[:, 1].max() < Y0 or V[:, 1].min() > Y0 + size):
            continue
        mat = o["material"].replace(" - Blend", "")
        c = COL.get(mat, (255, 0, 255))
        if o["kind"] == "blend": c = (255, 60, 60)
        for f in F:
            p = [((V[i][0] - X0) / res, (V[i][1] - Y0) / res) for i in f]
            m = Image.new("L", (N, N), 0); ImageDraw.Draw(m).polygon(p, fill=1)
            # cheap: only test the face bbox
            xs = [q[0] for q in p]; ys = [q[1] for q in p]
            a0, a1 = max(int(min(xs)) - 1, 0), min(int(max(xs)) + 2, N); b0, b1 = max(int(min(ys)) - 1, 0), min(int(max(ys)) + 2, N)
            if a0 >= a1 or b0 >= b1: continue
            cover[b0:b1, a0:a1] += np.asarray(m)[b0:b1, a0:a1]
            wire.polygon(p, fill=c, outline=(0, 0, 0) if o["kind"] == "mesh" else (120, 0, 0))
    inside = cover[20:-20, 20:-20]
    print("%-12s gaps %.3f %%  overlaps %.3f %%  (of %d px at 5 cm)" % (wname, 100 * (inside == 0).mean(), 100 * (inside > 1).mean(), inside.size))
    pic.transpose(Image.FLIP_LEFT_RIGHT).transpose(Image.FLIP_TOP_BOTTOM).resize((900, 900)).save(os.path.join(REPO, "build", "opcd_check_%s.png" % wname))
