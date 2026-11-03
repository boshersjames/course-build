# Review sheets for the 3D Scanner LAS greens (13, 14, 15, 16; Oct 29 evening). Analysis only, nothing applied.
# Per green: scan height + 10 cm contours + GKD pins | scan - 2018 LiDAR | scan - current game surface (LiDAR, or the earlier
# H14 / H16 scan where that is applied) | slope % + pins | doubled cells (vertical spread of the points > 12 cm in a 0.25 m cell).
# Doubled cells get weight 0 in a cleaned copy of the grid (scan_grid_<name>.f32 in scan_apply_new/, the apply candidate).
import json, math, os, glob
import numpy as np, laspy
from pyproj import Transformer
from scipy import ndimage
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
NEW = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/scan_apply_new/'; OLD = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/scan_apply/'
FIG = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_review_las/'; os.makedirs(FIG, exist_ok=True)
fit = json.load(open('C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_compare/scan_compare.json'))
HM = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', '<u2').reshape(2049, 2049).astype(float) / 65535 * 207; S = 2000 / 2048
def lidar(x, z): return ndimage.map_coordinates(HM, [np.asarray(z) / S, np.asarray(x) / S], order=1, mode='nearest')
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
meta = json.load(open(NEW + 'scan_grids.json')); oldmeta = json.load(open(OLD + 'scan_grids.json')); summary = {}
def load(d, m, k): a = np.fromfile(d + 'scan_grid_%s.f32' % k, '<f4').reshape(2, m['h'], m['w']); return np.where(a[0] < -9000, np.nan, a[0]), a[1]
for name, m in meta.items():
    hole = int(name.split('_')[0]); G, W = load(NEW, m, name); H_, W_ = G.shape; C = m['cell']
    XX, ZZ = np.meshgrid(m['x0'] + (np.arange(W_) + 0.5) * C, m['z0'] + (np.arange(H_) + 0.5) * C); L = lidar(XX, ZZ)
    # current game surface: the earlier scan grid where it is applied (weight > 0.5), else the LiDAR
    cur = L.copy(); oldk = {11: None, 12: None, 13: None, 14: 'H14_green', 15: None, 16: 'H16_green'}[hole]
    if oldk:
        om = oldmeta[oldk]; OG, OW = load(OLD, om, oldk); oi = ((XX - om['x0']) / om['cell']).astype(int); oj = ((ZZ - om['z0']) / om['cell']).astype(int)
        ok = (oi >= 0) & (oi < om['w']) & (oj >= 0) & (oj < om['h']); v = np.full_like(G, np.nan); v[ok] = OG[oj[ok], oi[ok]]; wv = np.zeros_like(G); wv[ok] = OW[oj[ok], oi[ok]]
        cur = np.where(np.isfinite(v) & (wv > 0.5), v, L)
    # doubled cells from the raw points (same transform as scan_prep)
    f = glob.glob('C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/scans/%s*/*.las' % name.rsplit('_', 3)[0] + '_' + name.split('_')[1])[0] if False else glob.glob('C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/scans/*/%s.las' % name)[0]
    l = laspy.read(f); p = fit[name]; E, N = tf.transform(np.asarray(l.x), np.asarray(l.y)); x = E - E0; z = N - N0; y = np.asarray(l.z, float) + p['dy'] - 182
    cx, cz = x.mean(), z.mean(); r = math.radians(p['rot_deg']); c, s = math.cos(r), math.sin(r); X = cx + c * (x - cx) - s * (z - cz) + p['dx']; Z = cz + s * (x - cx) + c * (z - cz) + p['dz']
    ix = ((X - m['x0']) / C).astype(int); iz = ((Z - m['z0']) / C).astype(int); ok = (ix >= 0) & (ix < W_) & (iz >= 0) & (iz < H_)
    key = iz[ok] * W_ + ix[ok]; yy = y[ok]; o = np.argsort(key); k_ = key[o]; yy = yy[o]; st = np.r_[0, np.nonzero(np.diff(k_))[0] + 1]; en = np.r_[st[1:], len(k_)]
    spread = np.full(H_ * W_, np.nan)
    for a, b in zip(st, en):
        if b - a >= 8: q = yy[a:b]; spread[k_[a]] = np.percentile(q, 90) - np.percentile(q, 10)
    spread = spread.reshape(H_, W_); dbl = ndimage.binary_dilation(ndimage.binary_opening(spread > 0.12, iterations=1), iterations=2)
    drift = ndimage.binary_dilation(np.abs(np.nan_to_num(G - L)) > 0.30, iterations=4)   # metres off the LiDAR = scanner drift (12's walk down the hill), not a regrade
    Wc = W.copy(); Wc[dbl | drift] = 0; Wc = np.minimum(Wc, np.clip(ndimage.distance_transform_edt(Wc > 0) * C / 1.0, 0, 1))   # 1 m feather round removed patches
    np.stack([np.nan_to_num(G, nan=-9999).astype('<f4'), Wc.astype('<f4')]).tofile(NEW + 'scan_grid_%s_clean.f32' % name)
    pins = [(q['Position']['x'], q['Position']['z']) for q in g['Holes'][hole - 1]['Pins'] if q.get('Position')] if g['Holes'][hole - 1]['Pins'] and isinstance(g['Holes'][hole - 1]['Pins'][0], dict) and 'Position' in g['Holes'][hole - 1]['Pins'][0] else []
    D1 = G - L; D2 = G - cur; gz, gx = np.gradient(G, C); slope = 100 * np.hypot(gx, gz); ext = [m['x0'], m['x0'] + W_ * C, m['z0'], m['z0'] + H_ * C]
    fig, ax = plt.subplots(1, 5, figsize=(30, 6.5))
    im = ax[0].imshow(G, origin='lower', extent=ext, cmap='terrain'); ax[0].contour(XX, ZZ, G, levels=np.arange(np.floor(np.nanmin(G)), np.nanmax(G), 0.1), colors='k', linewidths=0.3); ax[0].set_title('%s scan height + 10 cm contours' % name); plt.colorbar(im, ax=ax[0], fraction=0.04)
    im = ax[1].imshow(D1, origin='lower', extent=ext, cmap='RdBu_r', vmin=-0.25, vmax=0.25); ax[1].set_title('scan - 2018 LiDAR (m)'); plt.colorbar(im, ax=ax[1], fraction=0.04)
    im = ax[2].imshow(D2, origin='lower', extent=ext, cmap='RdBu_r', vmin=-0.25, vmax=0.25); ax[2].set_title('scan - current game (%s)' % ('LiDAR + ' + oldk if oldk else 'LiDAR')); plt.colorbar(im, ax=ax[2], fraction=0.04)
    im = ax[3].imshow(slope, origin='lower', extent=ext, cmap='magma_r', vmin=0, vmax=12); ax[3].set_title('slope % (pins = white)'); plt.colorbar(im, ax=ax[3], fraction=0.04)
    ax[4].imshow(np.where(np.isfinite(G), 1, 0), origin='lower', extent=ext, cmap='Greys', alpha=0.25); rm = (dbl | drift) & np.isfinite(G); ax[4].imshow(np.ma.masked_where(~rm, rm), origin='lower', extent=ext, cmap='autumn'); ax[4].set_title('removed from the apply (doubled / drift > 0.3 m): %.0f%%' % (100 * rm.sum() / max(np.isfinite(G).sum(), 1)))
    for a_ in ax:
        a_.set_aspect('equal')
        for px, pz in pins: a_.plot(px, pz, 'wo', ms=5, mec='k')
    plt.tight_layout(); plt.savefig(FIG + 'review_%s.jpg' % name, dpi=60); plt.close()
    pin_d = []
    for px, pz in pins:
        i, j = int((px - m['x0']) / C), int((pz - m['z0']) / C)
        if 0 <= i < W_ and 0 <= j < H_ and np.isfinite(G[j, i]): pin_d.append(dict(scan_minus_game_m=round(float(D2[j, i]), 3), slope_pct=round(float(slope[j, i]), 1)))
    summary[name] = dict(hole=hole, fit_mad_m=round(p['after_mad'], 3), covered_m2=round(float(np.isfinite(G).sum() * C * C), 1), removed_pct=round(float(100 * ((dbl | drift) & np.isfinite(G)).sum() / max(np.isfinite(G).sum(), 1)), 1),
                         vs_lidar_mad=round(float(np.nanmedian(np.abs(D1 - np.nanmedian(D1)))), 3), vs_game_p95_abs=round(float(np.nanpercentile(np.abs(D2), 95)), 3), pins=pin_d)
    print(name, summary[name])
json.dump(summary, open(FIG + 'summary.json', 'w'), indent=1)
