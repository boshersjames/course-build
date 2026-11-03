# Index and quality-check BoJa's phone LiDAR scans (LAS, EPSG:4326 lon/lat + Z metres as in the Oct 27 test).
# Writes scans_index.csv + top / oblique renders. Usage: python -I scan_index.py <scans_dir> <out_dir> <photos_index.csv>
import sys, os, glob, csv, json, math, datetime
import numpy as np, laspy
from pyproj import Transformer
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
from scipy import ndimage
scans, out, photos = sys.argv[1], sys.argv[2], sys.argv[3]; os.makedirs(out, exist_ok=True)
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
g = json.load(open(r'C:\Users\user\Documents\tierra-rejada\greenkeeper\TierraRejada.GKD', encoding='utf-8-sig'))
feats = []
for h in g['Holes'][:18]:
    for t in h['Tees']:
        p = t.get('Position')
        if p and (p['x'] or p['z']): feats.append((h['HoleNumber'], 'green' if t['TeeType'] == 'GreenCenterPoint' else ('aim' if t['TeeType'].startswith('Aim') else 'tee ' + t['TeeType']), p['x'], p['z']))
ph = list(csv.DictReader(open(photos)))
rows = []
for f in sorted(glob.glob(os.path.join(scans, '*', '*.las'))):
    name = os.path.splitext(os.path.basename(f))[0]; l = laspy.read(f); h = l.header
    x, y, z = np.asarray(l.x), np.asarray(l.y), np.asarray(l.z); n = len(x)
    geo = abs(x.mean()) <= 180 and abs(y.mean()) <= 90
    if geo:
        E, N = tf.transform(x, y); ux, uz = E - E0, N - N0
    else:
        ux, uz = x - x.mean(), y - y.mean()
    sx, sz, sy = ux.max() - ux.min(), uz.max() - uz.min(), z.max() - z.min()
    cx, cz = float(np.median(ux)), float(np.median(uz))
    near = min(feats, key=lambda q: math.hypot(q[2] - cx, q[3] - cz)) if geo else None
    # time from the file name (app names files <label>_<HH_MM_SS>), nearest photo by time
    parts = name.split('_'); tstr = ':'.join(parts[-3:]) if len(parts) >= 3 else ''
    nphoto = ''
    if tstr:
        try:
            t0 = datetime.datetime.strptime('2026:10:29 ' + tstr, '%Y:%m:%d %H:%M:%S')
            best = min(ph, key=lambda r: abs((datetime.datetime.strptime(r['time'][:19], '%Y:%m:%d %H:%M:%S') - t0).total_seconds())) if ph else None
            if best: nphoto = '%s (hole %s %s)' % (best['file'], best['hole'], best['feature'].split(' (')[0])
        except Exception: pass
    # quality: density, doubled surfaces (vertical spread inside 0.25 m cells), coverage holes
    cell = 0.25; ix = ((ux - ux.min()) / cell).astype(int); iz = ((uz - uz.min()) / cell).astype(int); W, H = ix.max() + 1, iz.max() + 1
    key = iz * W + ix; order = np.argsort(key); ks = key[order]; zs = z[order]
    starts = np.r_[0, np.nonzero(np.diff(ks))[0] + 1]; ends = np.r_[starts[1:], len(ks)]
    spread = np.array([np.percentile(zs[a:b], 90) - np.percentile(zs[a:b], 10) for a, b in zip(starts, ends) if b - a >= 8])
    occ = np.zeros((H, W), bool); occ.flat[np.unique(key)] = True; filled = ndimage.binary_fill_holes(ndimage.binary_closing(occ, iterations=3))
    holes_pct = 100 * (filled.sum() - occ.sum()) / max(1, filled.sum())
    dens = n / max(1e-6, filled.sum() * cell * cell)
    dbl = 100 * (spread > 0.15).mean() if len(spread) else 0; med_spread = float(np.median(spread)) if len(spread) else 0
    verdict = []
    if not geo: verdict.append('no geo-reference')
    if dbl > 15: verdict.append('doubled surfaces / drift in %.0f%% of cells' % dbl)
    if holes_pct > 10: verdict.append('%.0f%% coverage gaps' % holes_pct)
    if sx > 80 or sz > 80: verdict.append('very large extent (drift risk)')
    usable = 'yes' if dbl <= 15 and holes_pct <= 15 else ('partly' if dbl <= 35 else 'no')
    rows.append({'scan': name, 'app': (h.generating_software or '').strip() or 'unknown (phone LiDAR app, libLAS writer)', 'format': 'LAS %s pf%d' % (h.version, h.point_format.id),
                 'points': n, 'size_x_m': '%.1f' % sx, 'size_z_m': '%.1f' % sz, 'height_range_m': '%.2f' % sy, 'georef': 'EPSG:4326 lon/lat (phone GPS)' if geo else 'local',
                 'unity_x': '%.1f' % cx if geo else '', 'unity_z': '%.1f' % cz if geo else '', 'nearest_gkd': ('hole %d %s (%.0f m)' % (near[0], near[1], math.hypot(near[2] - cx, near[3] - cz))) if near else '',
                 'nearest_photo_by_time': nphoto, 'density_pts_m2': '%.0f' % dens, 'noise_p10_p90_m': '%.3f' % med_spread, 'doubled_cells_pct': '%.1f' % dbl, 'gaps_pct': '%.1f' % holes_pct, 'usable': usable, 'notes': '; '.join(verdict)})
    # renders
    k = max(1, n // 400000); col = np.c_[l.red, l.green, l.blue][::k] / 65535.0 if hasattr(l, 'red') else None
    fig = plt.figure(figsize=(14, 6.5))
    a1 = fig.add_subplot(1, 2, 1); a1.scatter(ux[::k], uz[::k], c=col if col is not None else z[::k], s=0.3, linewidths=0); a1.set_aspect('equal'); a1.set_title(name + ' top (Unity x / z, m)'); a1.grid(alpha=0.3)
    a2 = fig.add_subplot(1, 2, 2, projection='3d'); a2.scatter(ux[::k * 2], uz[::k * 2], z[::k * 2], c=col[::2] if col is not None else z[::k * 2], s=0.3, linewidths=0)
    a2.view_init(elev=25, azim=-60); a2.set_title('oblique (height x3)'); a2.set_box_aspect((sx, sz, max(0.5, sy * 3)))
    plt.tight_layout(); plt.savefig(os.path.join(out, 'scan_%s.jpg' % name), dpi=90); plt.close()
    print(name, 'noise %.3f' % med_spread, n, 'pts', '%.1f x %.1f m' % (sx, sz), rows[-1]['nearest_gkd'], '| doubled %.0f%% gaps %.0f%% ->' % (dbl, holes_pct), usable)
with open(os.path.join(out, 'scans_index.csv'), 'w', newline='') as fo:
    w = csv.DictWriter(fo, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
