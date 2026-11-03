# Tierra green edge format: current model (green mesh edge, blend ring, collar patches) and the clean back-nine scans (10, 14, 16).
# Profile = height relative to the green's own surface extrapolated outward (local plane fit 1-6 m inside the edge),
# as a function of signed distance from the green mesh edge (negative = on the green).
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD
_C = course(); _HM = Heightmap(_C); _R = rules(_C)
import json, math, os, csv, glob
import numpy as np, laspy
from scipy.spatial import cKDTree
from scipy import ndimage
from matplotlib.path import Path as MP
from pyproj import Transformer
from PIL import Image
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
A = os.path.join(_C.path('automation_output'), 'fairways') + '/'; OUT = os.path.join(_C.path('progress'), 'green_edges_1029') + '/'; os.makedirs(OUT, exist_ok=True)
G = json.load(open(A + 'greens.json'))
g = json.load(open(_C.path('gkd'), encoding='utf-8-sig'))
HM = _HM.H; S = _HM.S
def lidar(x, z): return ndimage.map_coordinates(HM, [np.asarray(z) / S, np.asarray(x) / S], order=1, mode='nearest')


def weld(V, T):
    _, f, inv = np.unique(np.round(V[:, [0, 2]] / 0.002).astype(np.int64), axis=0, return_index=True, return_inverse=True); return V[f], inv.ravel()[T]


def loops(T):
    E = {}
    for t in T:
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])): E[(a, b)] = 1
    bd = [(a, b) for (a, b) in E if (b, a) not in E]; nxt = {}
    for a, b in bd: nxt.setdefault(a, []).append(b)
    used = set(); out = []
    for a, b in bd:
        if (a, b) in used: continue
        L = [a]; used.add((a, b)); cur = b
        while cur != a:
            L.append(cur); nb = [q for q in nxt[cur] if (cur, q) not in used]
            if not nb: break
            used.add((cur, nb[0])); cur = nb[0]
        out.append(L)
    return out


def mesh(n):
    V = np.array(G[n]['v']); T = np.array(G[n]['t'], int).reshape(-1, 3); V, T = weld(V, T); return V, T


greens = {}
for h in g['Holes'][:18]:
    T = {t['TeeType']: t['Position'] for t in h['Tees'] if t.get('Position')}; gc = np.array([T['GreenCenterPoint']['x'], T['GreenCenterPoint']['z']])
    best = None
    for n in G:
        if not n.endswith('_Green_-_Mesh') or 'Tee default' in G[n]['mat']: continue
        V = np.array(G[n]['v']); d = np.linalg.norm(V[:, [0, 2]].mean(0) - gc)
        if best is None or d < best[0]: best = (d, n)
    k = best[1].split('_')[2]; blend = [b for b in G if b.startswith('Spline_green_%s_B_' % k)]
    pins = [(p['Day'], p['Position']['x'], p['Position']['z']) for p in h.get('Pins', []) if p.get('Position')]
    greens[h['HoleNumber']] = dict(mesh=best[1], blend=blend[0] if blend else None, centre=gc.tolist(), pins=pins)
rows = []
for hn, gr in greens.items():
    V, T = mesh(gr['mesh']); L = max(loops(T), key=len); edge = V[L]; P = MP(edge[:, [0, 2]]); area = abs(0.5 * np.sum(edge[:, 0] * np.roll(edge[:, 2], -1) - np.roll(edge[:, 0], -1) * edge[:, 2]))
    bw = np.nan
    if gr['blend']:
        BV, BT = mesh(gr['blend']); d, _ = cKDTree(edge[:, [0, 2]]).query(BV[:, [0, 2]]); ins = P.contains_points(BV[:, [0, 2]])
        bw_in = np.percentile(d[ins], 95) if ins.any() else 0; bw_out = np.percentile(d[~ins], 95) if (~ins).any() else 0; bw = bw_in + bw_out
        gr['blend_in'], gr['blend_out'] = float(bw_in), float(bw_out)
    gr.update(area=float(area), edge=edge.tolist())
    rows.append(dict(hole=hn, mesh=gr['mesh'], area_m2=round(area), blend_ring_width_m=round(float(bw), 2), blend_inside_m=round(gr.get('blend_in', 0), 2), blend_outside_m=round(gr.get('blend_out', 0), 2)))
for r in rows: print(r)
json.dump(greens, open(A + 'greens_info.json', 'w'))
