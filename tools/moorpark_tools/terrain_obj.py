"""heightmap_2049.raw -> build/moorpark_terrain_clender_frame.obj, the terrain for conforming the OPCD meshes.
Same as tierra-rejada/terrain/tierra_rejada_terrain_clender_frame.obj: 2 m grid, OBJ v = (-east, elevation - y_offset, north),
which Blender's default OBJ import turns into X = -east, Y = -north, Z = Unity y (the Clender .blend frame).
    python tools/terrain_obj.py [grid_m]
"""
import json, os, sys
import numpy as np
from scipy.ndimage import map_coordinates

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
c = json.load(open(os.path.join(REPO, "course.json")))
S, H, off = float(c["size"]), float(c["terrain_height"]), float(c["y_offset"])
step = float(sys.argv[1]) if len(sys.argv) > 1 else 2.0
raw = np.fromfile(os.path.join(REPO, "heightmap_2049.raw"), "<u2").reshape(2049, 2049).astype(np.float32) / 65535 * H   # row 0 = south
n = int(round(S / step)) + 1
g = np.linspace(0, S, n)
E, N = np.meshgrid(g, g)                                   # [row = north, col = east]
Z = map_coordinates(raw, [N / S * 2048, E / S * 2048], order=1)
out = os.path.join(REPO, "build", "moorpark_terrain_clender_frame.obj")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w") as f:
    f.write("# Moorpark terrain, %.1f m grid, Clender .blend frame: -X east, Y up (elev - %.0f m), Z north of the square's SW corner\n" % (step, off))
    v = np.column_stack([-E.ravel(), Z.ravel(), N.ravel()])
    np.savetxt(f, v, fmt="v %.2f %.3f %.2f")
    idx = np.arange(n * n).reshape(n, n) + 1
    a, b, cc, d = idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()
    np.savetxt(f, np.column_stack([a, b, cc, d]), fmt="f %d %d %d %d")
print("wrote", out, n * n, "verts")
