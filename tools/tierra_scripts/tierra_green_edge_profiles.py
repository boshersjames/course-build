# Edge profiles from the clean back-nine green scans (10 LAS with colour; 14, 16 height grids), relative to each green's mesh edge.
import json, math, os
import numpy as np, laspy
from scipy.spatial import cKDTree
from scipy import ndimage
from matplotlib.path import Path as MP
from pyproj import Transformer
from PIL import Image
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/'; OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/green_edges_1029/'
GI = json.load(open(A + 'greens_info.json'))
fit = json.load(open('C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_compare/scan_compare.json'))
BINS = np.arange(-6, 8.01, 0.25)


def profile(hole, x, z, y, rgb=None):
    gr = GI[str(hole)]; edge = np.array(gr['edge']); P = MP(edge[:, [0, 2]]); kd = cKDTree(edge[:, [0, 2]])
    d, idx = kd.query(np.c_[x, z]); ins = P.contains_points(np.c_[x, z]); sd = np.where(ins, -d, d)
    # local green plane per edge vertex: fit y = a + b x + c z to scan points on the green 1-6 m inside within 8 m of that edge point
    res = np.full(len(x), np.nan); kd_pts = cKDTree(np.c_[x, z])
    for k in range(0, len(edge), 3):
        e = edge[k, [0, 2]]; near = kd_pts.query_ball_point(e, 8.0)
        if len(near) < 50: continue
        near = np.array(near); gsel = near[(sd[near] < -1.0) & (sd[near] > -6.0)]
        if len(gsel) < 30: continue
        M = np.c_[np.ones(len(gsel)), x[gsel] - e[0], z[gsel] - e[1]]; coef, *_ = np.linalg.lstsq(M, y[gsel], rcond=None)
        own = near[(idx[near] >= k - 1) & (idx[near] <= k + 1)]
        res[own] = y[own] - (coef[0] + coef[1] * (x[own] - e[0]) + coef[2] * (z[own] - e[1]))
    ok = np.isfinite(res); prof = []; col = []
    for a, b in zip(BINS[:-1], BINS[1:]):
        m = ok & (sd >= a) & (sd < b); prof.append(np.median(res[m]) if m.sum() > 20 else np.nan)
        if rgb is not None:
            m2 = (sd >= a) & (sd < b); col.append(np.median(rgb[m2], 0) if m2.sum() > 20 else [np.nan] * 3)
    return np.array(prof), (np.array(col) if rgb is not None else None), sd, res


out = {}
# 10 green LAS (aligned as in scan_apply)
f = 'C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/scans/10_geeen_11_20_37/10_geeen_11_20_34.las'; p = fit['10_geeen_11_20_34']; l = laspy.read(f)
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
E, N = tf.transform(np.asarray(l.x), np.asarray(l.y)); x = E - E0; z = N - N0; y = np.asarray(l.z, float) + p['dy']
cx, cz = x.mean(), z.mean(); r = math.radians(p['rot_deg']); c, s = math.cos(r), math.sin(r)
X = cx + c * (x - cx) - s * (z - cz) + p['dx']; Z = cz + s * (x - cx) + c * (z - cz) + p['dz']
sel = np.arange(0, len(X), 3); rgb = np.c_[l.red, l.green, l.blue][sel] / 65535.0
pr10, col10, sd10, res10 = profile(10, X[sel], Z[sel], y[sel], rgb)
out[10] = pr10
# 14 / 16 grids
for hole, nm in ((14, 'H14'), (16, 'H16')):
    J = json.load(open('C:/Users/user/Documents/tierra-rejada/course_edits/visit_2026-10-29/green_scans/%s_green_scan.json' % nm))
    Hh = np.array(Image.open('C:/Users/user/Documents/tierra-rejada/course_edits/visit_2026-10-29/green_scans/%s_green_scan_height16.png' % nm)).astype(float)
    Mk = np.array(Image.open('C:/Users/user/Documents/tierra-rejada/course_edits/visit_2026-10-29/green_scans/%s_green_scan_mask.png' % nm)) > 127
    jj, ii = np.nonzero(Mk); xx = J['origin_x'] + (ii + 0.5) * J['cell']; zz = J['origin_z'] + (J['nz'] - jj - 0.5) * J['cell']; yy = J['unity_y_min'] + Hh[jj, ii] / 65535 * (J['unity_y_max'] - J['unity_y_min'])
    out[hole] = profile(hole, xx, zz, yy)[0]
# collar from colour on 10: greenness and brightness vs distance
gness = col10[:, 1] - (col10[:, 0] + col10[:, 2]) / 2; bright = col10.mean(1)
mid = (BINS[:-1] + BINS[1:]) / 2
fig, ax = plt.subplots(1, 2, figsize=(16, 6))
for hole, pr in out.items(): ax[0].plot(mid, pr * 100, label='green %d scan' % hole, lw=2)
med = np.nanmedian(np.array(list(out.values())), 0); ax[0].plot(mid, med * 100, 'k--', lw=3, label='median (Tierra edge profile)')
ax[0].axvline(0, color='grey'); ax[0].set_xlabel('distance from green edge (m, negative = on the green)'); ax[0].set_ylabel('height vs the green surface extended (cm)'); ax[0].legend(); ax[0].grid(alpha=0.3); ax[0].set_ylim(-60, 60)
ax[1].plot(mid, gness, 'g', label='greenness (G - (R+B)/2)'); ax[1].plot(mid, bright, 'k', label='brightness'); ax[1].axvline(0, color='grey'); ax[1].legend(); ax[1].grid(alpha=0.3); ax[1].set_xlabel('distance from green edge (m)'); ax[1].set_title('green 10 scan colour across the edge (collar band)')
plt.tight_layout(); plt.savefig(OUT + 'scan_edge_profiles.jpg', dpi=70); plt.close()
np.set_printoptions(precision=3, suppress=True)
print('dist', mid[::2]); [print(h, (pr * 100)[::2].round(1)) for h, pr in out.items()]; print('median', (med * 100)[::2].round(1))
print('greenness', gness[::2].round(3)); print('bright', bright[::2].round(3))
json.dump(dict(dist=mid.tolist(), profiles={str(h): pr.tolist() for h, pr in out.items()}, median=med.tolist(), greenness10=gness.tolist(), bright10=bright.tolist()), open(A + 'edge_profiles.json', 'w'))
