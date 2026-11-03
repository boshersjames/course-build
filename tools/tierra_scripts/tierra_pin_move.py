# Steep pins on the scanned greens 12-15 (BoJa, Oct 29: "use your recommendations"): any pin over 3 % moves to the nearest spot on
# the same green that is <= 3 % (1 m plane slope, as GSPro's pin check), >= 4 m from the green edge and >= 2 m from a tier break
# (cells steeper than 6 %), and stays in its day's area (moves capped at 8 m, so front/middle/back and left/right are kept).
# Surface = the applied (ripple-smoothed) scan grid; green outline = the course green mesh. Writes the repo GKD only (not C:\GSProV1),
# pin_check.csv and a sheet (old vs new pins over the slope map).
import json, math, os, shutil, sys
import numpy as np
from scipy import ndimage
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
GKD = 'C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD'; A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/scan_apply/'
OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/pins/'; os.makedirs(OUT, exist_ok=True)
DRY = 'dry' in sys.argv[1:]; GRIDS = next((x for x in sys.argv[1:] if x.endswith('.json')), 'scan_grids_1029c.json')   # e.g. scan_grids_1030.json for 17/18
g = json.load(open(GKD, encoding='utf-8-sig')); GM = json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/greens.json'))
meta = json.load(open(A + GRIDS)); rows = ['hole,day,old_x,old_z,old_slope_pct,new_x,new_z,new_y,new_slope_pct,moved_m,edge_m,note']
fig, axs = plt.subplots(1, len(meta), figsize=(7 * len(meta), 7.5)); axs = np.atleast_1d(axs)
for ax, (name, m) in zip(axs, sorted(meta.items())):
    hole = int(name.split('_')[0]); a = np.fromfile(A + 'scan_grid_%s.f32' % name, '<f4').reshape(2, m['h'], m['w']); G = np.where(a[0] < -9000, np.nan, a[0]); C = m['cell']
    H_, W_ = G.shape; XX, ZZ = np.meshgrid(m['x0'] + (np.arange(W_) + 0.5) * C, m['z0'] + (np.arange(H_) + 0.5) * C)
    gc = [t['Position'] for t in g['Holes'][hole - 1]['Tees'] if t['TeeType'] == 'GreenCenterPoint'][0]
    best = min((k for k in GM if k.endswith('_Green_-_Mesh')), key=lambda k: np.hypot(*(np.array(GM[k]['v'])[:, [0, 2]].mean(0) - [gc['x'], gc['z']])))
    vv = np.array(GM[best]['v']); mi = ((vv[:, 0] - m['x0']) / C).astype(int); mj = ((vv[:, 2] - m['z0']) / C).astype(int); ok = (mi >= 0) & (mi < W_) & (mj >= 0) & (mj < H_)
    gm = np.zeros((H_, W_), bool); gm[mj[ok], mi[ok]] = True; gm = ndimage.binary_fill_holes(ndimage.binary_closing(gm, iterations=4))
    # the course surface: the scan where it is weighted in, the LiDAR (+ earlier edits) where the scan was left out (doubled / drift)
    HM = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', '<u2').reshape(2049, 2049).astype(float) / 65535 * 207
    L = ndimage.map_coordinates(HM, [ZZ / (2000 / 2048), XX / (2000 / 2048)], order=1, mode='nearest'); Wt = a[1]
    G = np.where(np.isfinite(G), Wt * G + (1 - Wt) * L, L); Gx = G
    S1 = ndimage.uniform_filter(Gx, size=int(2 / C) + 1)   # ~2 m window = 1 m radius plane
    gz, gx = np.gradient(S1, C); slope = 100 * np.hypot(gx, gz)
    edge = ndimage.distance_transform_edt(gm) * C; tier = ndimage.distance_transform_edt(~(gm & (slope > 6))) * C
    cand = gm & np.isfinite(G) & (slope <= 3) & (edge >= 4) & (tier >= 2)
    im = ax.imshow(np.where(gm, slope, np.nan), origin='lower', extent=[m['x0'], m['x0'] + W_ * C, m['z0'], m['z0'] + H_ * C], cmap='RdYlGn_r', vmin=0, vmax=6)
    ax.contour(XX, ZZ, np.where(gm, slope, np.nan), levels=[3], colors='k', linewidths=0.6)
    for pin in g['Holes'][hole - 1]['Pins']:
        p = pin['Position']; i, j = int((p['x'] - m['x0']) / C), int((p['z'] - m['z0']) / C); s0 = float(slope[j, i]); e0 = float(edge[j, i])
        if s0 <= 3.05:   # 3.0 % rounds the same as the in-Unity pin check (2.9 %)
            rows.append('%d,%s,%.2f,%.2f,%.1f,,,,,0,%.1f,ok as is' % (hole, pin['Day'], p['x'], p['z'], s0, e0)); ax.plot(p['x'], p['z'], 'wo', mec='k', ms=7); ax.text(p['x'] + 0.4, p['z'] + 0.4, pin['Day'][:3], fontsize=8); continue
        d = np.hypot(XX - p['x'], ZZ - p['z']); c2 = cand & (d <= 8); note = ''
        if not c2.any(): c2 = gm & np.isfinite(G) & (slope <= 3) & (edge >= 4) & (d <= 8); note = 'tier rule relaxed'
        if not c2.any(): rows.append('%d,%s,%.2f,%.2f,%.1f,,,,,0,%.1f,NO spot <= 3%% within 8 m - left' % (hole, pin['Day'], p['x'], p['z'], s0, e0)); ax.plot(p['x'], p['z'], 'rx', ms=10); continue
        k = np.argmin(np.where(c2, d, np.inf)); jj, ii = np.unravel_index(k, d.shape); nx, nz, ny = float(XX[jj, ii]), float(ZZ[jj, ii]), float(G[jj, ii])
        rows.append('%d,%s,%.2f,%.2f,%.1f,%.2f,%.2f,%.3f,%.1f,%.1f,%.1f,%s' % (hole, pin['Day'], p['x'], p['z'], s0, nx, nz, ny, slope[jj, ii], d[jj, ii], edge[jj, ii], note))
        ax.annotate('', xy=(nx, nz), xytext=(p['x'], p['z']), arrowprops=dict(arrowstyle='->', color='k')); ax.plot(p['x'], p['z'], 'o', mfc='none', mec='k', ms=7); ax.plot(nx, nz, 'wo', mec='b', ms=7)
        ax.text(nx + 0.4, nz + 0.4, '%s %.1f%%->%.1f%%' % (pin['Day'][:3], s0, slope[jj, ii]), fontsize=8)
        if not DRY: pin['Position'] = {'x': round(nx, 4), 'y': round(ny, 4), 'z': round(nz, 4)}
    ax.set_title('hole %d: slope %% (black line = 3 %%), old pin (open) -> new pin (white)' % hole); ax.set_aspect('equal')
    gj, gi = np.nonzero(gm); ax.set_xlim(XX[0, gi.min()] - 2, XX[0, gi.max()] + 2); ax.set_ylim(ZZ[gj.min(), 0] - 2, ZZ[gj.max(), 0] + 2)
plt.colorbar(im, ax=axs, fraction=0.012); plt.savefig(OUT + 'pins_%s.jpg' % '_'.join(sorted(k.split('_')[0] for k in meta)), dpi=55); plt.close()
open(OUT + 'pin_check_%s.csv' % '_'.join(sorted(k.split('_')[0] for k in meta)), 'w').write('\n'.join(rows) + '\n'); print('\n'.join(rows))
if not DRY:
    shutil.copy(GKD, OUT + 'TierraRejada_before_pins_%s.GKD' % GRIDS.split('.')[0])
    open(GKD, 'w', encoding='utf-8').write(json.dumps(g, separators=(',', ':'), ensure_ascii=False))
