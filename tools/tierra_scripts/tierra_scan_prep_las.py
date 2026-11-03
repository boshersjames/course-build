# Prepare scan-apply inputs for Tierra (Unity frame, y = elev - 182):
#  scan_grid_<name>.json/.f32 : aligned scan height grid at 0.25 m (NaN = no data) + weight grid (1 inside coverage, 0 beyond, 1 m feather)
#  bunker_polys.json : scanned sand outline per bunker (near model bunkers 24 / 26)
import os, glob, json, math
import numpy as np, laspy
from pyproj import Transformer
from scipy import ndimage
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
OUT = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/scan_apply_new/'; os.makedirs(OUT, exist_ok=True)
FIG = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_apply/'; os.makedirs(FIG, exist_ok=True)
fit = json.load(open('C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_compare/scan_compare.json'))
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
C = 0.25; meta = {}; polys = {}
for f in sorted(glob.glob('C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/scans/1[1-6]_green_*/*.las')):
    name = os.path.splitext(os.path.basename(f))[0]; p = fit[name]; l = laspy.read(f)
    E, N = tf.transform(np.asarray(l.x), np.asarray(l.y)); x = E - E0; z = N - N0; y = np.asarray(l.z, float) + p['dy'] - 182
    cx, cz = x.mean(), z.mean()
    # same transform as the fit: grid centre of the 0.5 m fit is close to the point mean; rotation about it then shift
    r = math.radians(p['rot_deg']); c, s = math.cos(r), math.sin(r); X = cx + c * (x - cx) - s * (z - cz) + p['dx']; Z = cz + s * (x - cx) + c * (z - cz) + p['dz']
    x0, z0 = math.floor(X.min()) - 2, math.floor(Z.min()) - 2; W = int((X.max() - x0) / C) + 9; H = int((Z.max() - z0) / C) + 9
    ix = ((X - x0) / C).astype(int); iz = ((Z - z0) / C).astype(int); s_ = np.zeros((H, W)); n_ = np.zeros((H, W)); np.add.at(s_, (iz, ix), y); np.add.at(n_, (iz, ix), 1)
    G = np.where(n_ >= 3, s_ / np.maximum(n_, 1), np.nan)
    # light smoothing (normalized), fill tiny gaps, coverage weight with a 1 m feather
    v = np.isfinite(G).astype(float); num = ndimage.gaussian_filter(np.nan_to_num(G), 1.0); den = ndimage.gaussian_filter(v, 1.0)
    Gs = np.where(den > 0.35, num / np.maximum(den, 1e-6), np.nan)
    cov = np.isfinite(Gs); cov = ndimage.binary_opening(cov, iterations=2)
    dist = ndimage.distance_transform_edt(cov) * C; w = np.clip(dist / 1.0, 0, 1)
    Gs[~cov] = np.nan
    meta[name] = dict(x0=x0, z0=z0, cell=C, w=W, h=H)
    if 'bunker' in name:
        rgb = np.c_[l.red, l.green, l.blue] / 65535.0; sand = (rgb.min(1) > 0.62) & ((rgb.max(1) - rgb.min(1)) / np.maximum(rgb.max(1), 1e-3) < 0.18)
        Sg = np.zeros((H, W), float); np.add.at(Sg, (iz[sand], ix[sand]), 1); Sg = (Sg >= 3) & (n_ >= 3) & (Sg >= 0.6 * n_)
        # the walk only covered the sand near the rims: bunker = scanned sand plus the unscanned interior that the scan encloses, minus scanned grass
        covered = n_ >= 3; grass = covered & ~ndimage.binary_dilation(Sg, iterations=1); interior = ndimage.binary_fill_holes(ndimage.binary_closing(covered, iterations=3)) & ~covered
        # per sand blob: its convex hull minus scanned grass (the unscanned middle of a bunker is sand)
        from matplotlib.path import Path as MPth
        from scipy.spatial import ConvexHull as CH
        Sg = ndimage.binary_opening(ndimage.binary_closing(Sg, iterations=3), iterations=1); lb, nl = ndimage.label(ndimage.binary_dilation(Sg, iterations=8)); full = np.zeros_like(Sg)
        jj_, ii_ = np.mgrid[0:H, 0:W]
        for k in range(1, nl + 1):
            m = (lb == k) & Sg
            if m.sum() * C * C < 15: continue
            pj, pi = np.nonzero(m); hv = CH(np.c_[pi, pj]).vertices; hull = MPth(np.c_[pi[hv], pj[hv]]).contains_points(np.c_[ii_.ravel(), jj_.ravel()]).reshape(H, W)
            full |= (hull & ~grass) | m
        Sg = ndimage.binary_opening(ndimage.binary_fill_holes(ndimage.binary_closing(full, iterations=2)), iterations=2)
        lab, nb = ndimage.label(Sg); sizes = ndimage.sum(Sg, lab, range(1, nb + 1))
        fig, ax = plt.subplots(figsize=(9, 9)); ax.imshow(np.where(np.isfinite(G), 1, 0), origin='lower', extent=[x0, x0 + W * C, z0, z0 + H * C], cmap='Greys', alpha=0.3)
        for k in np.argsort(sizes)[::-1][:2]:
            m = lab == k + 1
            if m.sum() * C * C < 20: continue
            cs = plt.contour(np.arange(W) * C + x0 + C / 2, np.arange(H) * C + z0 + C / 2, m.astype(float), levels=[0.5])
            seg = max(cs.allsegs[0], key=len); jj, ii = np.nonzero(m); cxz = (x0 + (ii.mean() + 0.5) * C, z0 + (jj.mean() + 0.5) * C)
            key = 'b24' if cxz[1] < 1110 else 'b26'; polys[key] = dict(centre=cxz, area=float(m.sum() * C * C), poly=[[float(a), float(b)] for a, b in seg[::2]])
            from scipy.interpolate import griddata
            known = m & np.isfinite(Gs); hole = m & ~np.isfinite(Gs)
            ring = ndimage.binary_dilation(m, iterations=3) & np.isfinite(Gs)
            if hole.any() and ring.sum() > 20:
                kj, ki = np.nonzero(ring); hj, hi = np.nonzero(hole); Gs[hj, hi] = griddata(np.c_[ki, kj], Gs[kj, ki], np.c_[hi, hj], method='linear')
            w[m & np.isfinite(Gs)] = 1.0; polys[key]['interior_filled_m2'] = float(hole.sum() * C * C)
            ax.plot(seg[:, 0], seg[:, 1], 'orange', lw=2); ax.text(cxz[0], cxz[1], '%s %.0f m2' % (key, m.sum() * C * C))
        B = json.load(open('C:/Users/user/AppData/Local/Temp/claude/C--Users-user-Documents-tierra-rejada/3efaf824-bf28-4c71-a80a-e8bab2dd9e22/scratchpad/tr_bunk10.json'))
        from scipy.spatial import ConvexHull
        for k, b in B.items():
            rim = np.array(b['rim'])[:, :2]; hh = rim[ConvexHull(rim).vertices]; ax.plot(np.r_[hh[:, 0], hh[0, 0]], np.r_[hh[:, 1], hh[0, 1]], 'r--', lw=1)
        ax.set_aspect('equal'); ax.set_title('hole 10 fairway bunkers: scanned sand (orange) vs model outline (red dashed)'); plt.savefig(FIG + 'bunkers_scan_vs_model_outline.jpg', dpi=80); plt.close()
    np.stack([np.nan_to_num(Gs, nan=-9999).astype('<f4'), w.astype('<f4')]).tofile(OUT + 'scan_grid_%s.f32' % name)
json.dump(meta, open(OUT + 'scan_grids.json', 'w'), indent=1); json.dump(polys, open(OUT + 'bunker_polys.json', 'w'))
print({k: (round(v['area']), [round(c, 1) for c in v['centre']]) for k, v in polys.items()}); print(meta)
