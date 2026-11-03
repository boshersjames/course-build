# Fairway surface (Unity OPCD meshes) vs the 2018 LiDAR heightmap (Unity y = raw/65535*207): per hole fairway and landing zone.
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD
_C = course(); _HM = Heightmap(_C); _R = rules(_C)
import json, math, sys, os, csv
import numpy as np
from scipy import ndimage
A = os.path.join(_C.path('automation_output'), 'fairways') + '/'; tag = sys.argv[1] if len(sys.argv) > 1 else 'before'
HM = _HM.H
S = _HM.S
def lidar(x, z): return ndimage.map_coordinates(HM, [z / S, x / S], order=1, mode='nearest')
d = np.fromfile(A + 'fairway_samples.bin', '<f4').reshape(-1, 5); names = open(A + 'fairway_names.txt').read().split()
x, z, yf, yt, k = d.T; k = k.astype(int); yl = lidar(x, z)
g = json.load(open(_C.path('gkd'), encoding='utf-8-sig'))
lines = {}
for h in g['Holes'][:18]:
    T = {t['TeeType']: t['Position'] for t in h['Tees'] if t.get('Position')}
    pts = [T[t] for t in ('Black', 'AimPoint1', 'AimPoint2', 'GreenCenterPoint') if t in T]
    lines[h['HoleNumber']] = (np.array([[p['x'], p['z']] for p in pts]), h['Par'])
def along(P, px, pz):
    best = np.full(len(px), 1e9); s_out = np.zeros(len(px)); acc = 0
    for a, b in zip(P[:-1], P[1:]):
        ab = b - a; L = np.hypot(*ab); u = np.clip(((px - a[0]) * ab[0] + (pz - a[1]) * ab[1]) / L ** 2, 0, 1)
        dd = np.hypot(px - a[0] - u * ab[0], pz - a[1] - u * ab[1]); m = dd < best; best[m] = dd[m]; s_out[m] = acc + u[m] * L; acc += L
    return best, s_out
D = np.full((19, len(x)), 1e9); Sx = np.zeros((19, len(x)))
for hn, (P, par) in lines.items(): D[hn], Sx[hn] = along(P, x, z)
hole = D.argmin(0); dist = D.min(0); s_al = Sx[hole, np.arange(len(x))]
# grids per hole to high-pass (undulation = height minus 15 m smooth) using both surfaces on the fairway cells only
rows = []; heat = {}
for hn, (P, par) in lines.items():
    m = (hole == hn) & (dist < 45)
    if m.sum() < 50: continue
    xs, zs = x[m], z[m]; x0, z0 = xs.min(), zs.min(); W, H = int(xs.max() - x0) + 1, int(zs.max() - z0) + 1
    Gf = np.full((H, W), np.nan); Gl = np.full((H, W), np.nan); Gs = np.full((H, W), np.nan); ii = (xs - x0).astype(int); jj = (zs - z0).astype(int)
    Gf[jj, ii] = yf[m]; Gl[jj, ii] = yl[m]; Gs[jj, ii] = s_al[m]
    def hp(G):
        v = np.isfinite(G).astype(float); num = ndimage.gaussian_filter(np.nan_to_num(G), 7.5); den = ndimage.gaussian_filter(v, 7.5)
        return G - num / np.maximum(den, 1e-6)
    Hf, Hl = hp(Gf), hp(Gl)
    zones = [('fairway', np.isfinite(Gf))]
    zones.append(('landing 220-290 yd', np.isfinite(Gf) & (Gs >= 201) & (Gs <= 265)))
    if par == 5: zones.append(('lay-up 430-500 yd', np.isfinite(Gf) & (Gs >= 393) & (Gs <= 457)))
    for zn, zm in zones:
        if zm.sum() < 30: continue
        dif = (Gf - Gl)[zm]; off = np.median(dif)
        rows.append(dict(hole=hn, par=par, zone=zn, cells=int(zm.sum()), offset_m=round(float(off), 3), rms_diff_m=round(float(np.sqrt(np.mean((dif - off) ** 2))), 3),
                         p95_abs_m=round(float(np.percentile(np.abs(dif - off), 95)), 3), undul_lidar_rms_m=round(float(np.sqrt(np.nanmean(Hl[zm] ** 2))), 3), undul_course_rms_m=round(float(np.sqrt(np.nanmean(Hf[zm] ** 2))), 3)))
        rows[-1]['undul_kept_pct'] = round(100 * rows[-1]['undul_course_rms_m'] / max(rows[-1]['undul_lidar_rms_m'], 1e-6), 0)
    heat[hn] = dict(x0=float(x0), z0=float(z0), diff=(Gf - Gl), lz=np.isfinite(Gf) & (Gs >= 201) & (Gs <= 265), Gl=Gl)
os.makedirs(A, exist_ok=True)
with open(A + 'fairway_compare_%s.csv' % tag, 'w', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
for r in rows:
    if r['zone'] != 'fairway': print(r)
np.save(A + 'heat_%s.npy' % tag, np.array([heat], dtype=object), allow_pickle=True)
