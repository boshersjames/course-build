# Align BoJa's phone scans to the Tierra terrain (USGS LiDAR heightmap) and compare. Analysis only.
import os, glob, json, math
import numpy as np, laspy
from pyproj import Transformer
from scipy import ndimage, optimize
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_compare/'; os.makedirs(OUT, exist_ok=True)
HM = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', '<u2').reshape(2049, 2049).astype(float) / 65535 * 207 + 182   # elevation m
S = 2000 / 2048
def model(x, z): return ndimage.map_coordinates(HM, [z / S, x / S], order=1, mode='nearest')
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
def gk(h, t): p = [q for q in g['Holes'][h - 1]['Tees'] if q['TeeType'] == t][0]['Position']; return p['x'], p['z'], p['y'] + 182
def grid(x, z, y, cell):
    x0, z0 = x.min(), z.min(); ix = ((x - x0) / cell).astype(int); iz = ((z - z0) / cell).astype(int); W, H = ix.max() + 1, iz.max() + 1
    key = iz * W + ix; o = np.argsort(key); k = key[o]; yy = y[o]; st = np.r_[0, np.nonzero(np.diff(k))[0] + 1]; en = np.r_[st[1:], len(k)]
    G = np.full((H, W), np.nan); cnt = np.zeros((H, W))
    for a, b in zip(st, en): G.flat[k[a]] = np.median(yy[a:b]); cnt.flat[k[a]] = b - a
    G[cnt < 4] = np.nan
    return G, x0, z0
res = {}
for f in sorted(glob.glob('C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/scans/*/*.las')):
    name = os.path.splitext(os.path.basename(f))[0]; l = laspy.read(f)
    E, N = tf.transform(np.asarray(l.x), np.asarray(l.y)); x = E - E0; z = N - N0; y = np.asarray(l.z, float)
    rgb = np.c_[l.red, l.green, l.blue] / 65535.0
    G, x0, z0 = grid(x, z, y, 0.5); jj, ii = np.nonzero(~np.isnan(G)); gx = x0 + (ii + 0.5) * 0.5; gz = z0 + (jj + 0.5) * 0.5; gy = G[jj, ii]
    cx, cz = gx.mean(), gz.mean()
    def resid(p):
        dx, dz, dy, rot = p; c, s = math.cos(rot), math.sin(rot); X = cx + c * (gx - cx) - s * (gz - cz) + dx; Z = cz + s * (gx - cx) + c * (gz - cz) + dz
        return gy + dy - model(X, Z)
    p0 = [0, 0, float(np.median(model(gx, gz) - gy)), 0]
    r0 = resid(p0)
    sol = optimize.least_squares(resid, p0, loss='soft_l1', f_scale=0.1, x_scale=[1, 1, 0.1, 0.01], bounds=([-8, -8, -60, -0.15], [8, 8, 60, 0.15]))
    if np.median(np.abs(resid(sol.x))) > np.median(np.abs(r0)): sol.x = np.array(p0)   # keep the vertical-only fit when ICP does not help
    dx, dz, dy, rot = sol.x; r = resid(sol.x)
    res[name] = dict(dx=dx, dz=dz, dy=dy, rot_deg=math.degrees(rot), before_mad=float(np.median(np.abs(r0))), after_mad=float(np.median(np.abs(r))), after_p90=float(np.percentile(np.abs(r), 90)), n=len(r))
    print(name, 'shift x %+.2f z %+.2f m, vertical %+.2f m, rot %+.2f deg; |res| median %.3f -> %.3f m (p90 %.3f)' % (dx, dz, dy, math.degrees(rot), res[name]['before_mad'], res[name]['after_mad'], res[name]['after_p90']))
    # aligned full-res points
    c, s = math.cos(rot), math.sin(rot); ax = cx + c * (x - cx) - s * (z - cz) + dx; az = cz + s * (x - cx) + c * (z - cz) + dz; ay = y + dy
    G25, a0, b0 = grid(ax, az, ay, 0.25); H25, W25 = G25.shape; X25 = a0 + (np.arange(W25) + 0.5) * 0.25; Z25 = b0 + (np.arange(H25) + 0.5) * 0.25
    XX, ZZ = np.meshgrid(X25, Z25); M25 = model(XX, ZZ); D = G25 - M25
    valid = np.isfinite(G25).astype(float); num = ndimage.gaussian_filter(np.nan_to_num(G25), 2); den = ndimage.gaussian_filter(valid, 2)
    Gs = np.where(den > 0.6, num / np.maximum(den, 1e-6), np.nan); Ms = ndimage.gaussian_filter(M25, 2)
    gzs, gxs = np.gradient(Gs, 0.25); slope = 100 * np.hypot(gxs, gzs)
    mz, mx = np.gradient(Ms, 0.25); mslope = 100 * np.hypot(mx, mz); mslope[np.isnan(slope)] = np.nan
    res[name].update(scan_slope_mean=float(np.nanmean(slope)), scan_slope_p95=float(np.nanpercentile(slope, 95)), model_slope_mean=float(np.nanmean(mslope)), model_slope_p95=float(np.nanpercentile(mslope, 95)),
                     diff_mean=float(np.nanmean(D)), diff_p5=float(np.nanpercentile(D, 5)), diff_p95=float(np.nanpercentile(D, 95)))
    fig, ax3 = plt.subplots(1, 3, figsize=(18, 6)); ext = [X25[0], X25[-1], Z25[0], Z25[-1]]
    lv = np.arange(np.floor(np.nanmin(G25) * 4) / 4, np.nanmax(G25) + 0.25, 0.25)
    ax3[0].imshow(slope, origin='lower', extent=ext, cmap='viridis', vmin=0, vmax=12); cs = ax3[0].contour(X25, Z25, Gs, levels=lv, colors='w', linewidths=0.6); ax3[0].set_title(name + ': scan slope % + 0.25 m contours')
    ax3[1].imshow(mslope, origin='lower', extent=ext, cmap='viridis', vmin=0, vmax=12); ax3[1].contour(X25, Z25, np.where(np.isnan(Gs), np.nan, Ms), levels=lv, colors='w', linewidths=0.6); ax3[1].set_title('model (course LiDAR) slope % + 0.25 m contours')
    im = ax3[2].imshow(D, origin='lower', extent=ext, cmap='RdBu_r', vmin=-0.4, vmax=0.4); ax3[2].set_title('scan - model (m): red = scan higher'); plt.colorbar(im, ax=ax3[2], fraction=0.04)
    for a in ax3: a.set_xlabel('Unity x'); a.set_ylabel('Unity z'); a.set_aspect('equal')
    if 'geeen' in name or 'green' in name:
        gx_, gz_, _ = gk(10, 'GreenCenterPoint'); [a.plot(gx_, gz_, 'r+', ms=14) for a in ax3]
    if 'bunker' in name:
        sand = (rgb.min(1) > 0.55) & ((rgb.max(1) - rgb.min(1)) / np.maximum(rgb.max(1), 1e-3) < 0.25)
        for a in ax3: a.scatter(ax[sand][::40], az[sand][::40], s=0.2, c='orange')
        # depth: sand floor vs rim (points within 1.5 m outside the sand) per bunker blob
        Sg = np.zeros((H25, W25), bool); si = ((ax[sand] - a0) / 0.25).astype(int); sj = ((az[sand] - b0) / 0.25).astype(int); ok = (si >= 0) & (si < W25) & (sj >= 0) & (sj < H25); Sg[sj[ok], si[ok]] = True
        Sg = ndimage.binary_opening(ndimage.binary_closing(Sg, iterations=2)); lab, nb = ndimage.label(Sg); out = []
        for k in range(1, nb + 1):
            m = lab == k
            if m.sum() < 80: continue
            ring = ndimage.binary_dilation(m, iterations=6) & ~ndimage.binary_dilation(m, iterations=1)
            floor = np.nanpercentile(G25[m], 10); rim = np.nanpercentile(G25[ring], 75); mfloor = np.nanpercentile(M25[m], 10); mrim = np.nanpercentile(M25[ring], 75)
            jjb, iib = np.nonzero(m); out.append(dict(x=float(X25[iib].mean()), z=float(Z25[jjb].mean()), area=float(m.sum() * 0.0625), scan_depth=float(rim - floor), model_depth=float(mrim - mfloor)))
        res[name]['bunkers'] = out
        for b in out: print('   bunker at (%.0f, %.0f): sand seen %.0f m2, scan rim-to-floor %.2f m, LiDAR terrain %.2f m' % (b['x'], b['z'], b['area'], b['scan_depth'], b['model_depth']))
    if 'tee' in name:
        out = []
        for t in ('Black', 'Green', 'Blue', 'White'):
            tx, tz, ty = gk(11, t); i = int((tx - a0) / 0.25); j = int((tz - b0) / 0.25)
            if 0 <= i < W25 and 0 <= j < H25:
                win = G25[max(0, j - 6):j + 7, max(0, i - 6):i + 7]; mw = M25[max(0, j - 6):j + 7, max(0, i - 6):i + 7]
                if np.isfinite(win).sum() > 20: out.append(dict(tee=t, scan=float(np.nanmedian(win)), gkd=ty, model=float(np.nanmedian(mw)), flat=float(np.nanpercentile(win, 90) - np.nanpercentile(win, 10))))
            for a in ax3: a.plot(tx, tz, 'k^', ms=8)
        res[name]['tees'] = out
        for t in out: print('   tee %s: scan pad %.2f m, GKD %.2f, LiDAR %.2f (scan - GKD %+.2f), pad flatness p10-p90 %.2f m' % (t['tee'], t['scan'], t['gkd'], t['model'], t['scan'] - t['gkd'], t['flat']))
    plt.tight_layout(); plt.savefig(OUT + 'compare_%s.jpg' % name, dpi=80); plt.close()
    print('   slope scan mean %.1f%% p95 %.1f%% | model mean %.1f%% p95 %.1f%% | scan-model mean %+.2f (p5 %+.2f p95 %+.2f) m' % (res[name]['scan_slope_mean'], res[name]['scan_slope_p95'], res[name]['model_slope_mean'], res[name]['model_slope_p95'], res[name]['diff_mean'], res[name]['diff_p5'], res[name]['diff_p95']))
json.dump(res, open(OUT + 'scan_compare.json', 'w'), indent=1)
