# Green undulation sheets for the scanned greens (12, 13, 14, 15; Oct 29 evening). Analysis only.
# Applied surface = scan grid where its weight is 1 (that is what the course now follows within mm), green outline from the
# GKD-near scan coverage. Per green: 2 cm contours, slope arrows (downhill), high / low points and total relief, and a ripple check:
# the part of the surface shorter than ~1.5 m wavelength (grid minus a 0.6 m Gaussian), which is where scan noise lives; real breaks
# on a green are metres long. Ripple amplitude (p95) is reported and a "smoothed" candidate grid is written next to the scan grid.
import json, os
import numpy as np
from scipy import ndimage
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/scan_apply/'; OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/undulation/'; os.makedirs(OUT, exist_ok=True)
meta = json.load(open(A + 'scan_grids_1029b.json')); g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig')); rep = {}
for name, m in meta.items():
    hole = int(name.split('_')[0]); a = np.fromfile(A + 'scan_grid_%s.f32' % name, '<f4').reshape(2, m['h'], m['w']); G = np.where(a[0] < -9000, np.nan, a[0]); W = a[1]; C = m['cell']
    H_, W_ = G.shape; XX, ZZ = np.meshgrid(m['x0'] + (np.arange(W_) + 0.5) * C, m['z0'] + (np.arange(H_) + 0.5) * C)
    gc = [t['Position'] for t in g['Holes'][hole - 1]['Tees'] if t['TeeType'] == 'GreenCenterPoint'][0]
    # putting surface = the course's green mesh nearest the GKD green centre (vertices rasterised, closed, holes filled), where the scan has data
    GM = json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/greens.json'))
    best = min((k for k in GM if k.endswith('_Green_-_Mesh')), key=lambda k: np.hypot(*(np.array(GM[k]['v'])[:, [0, 2]].mean(0) - [gc['x'], gc['z']])))
    vv = np.array(GM[best]['v']); mi = ((vv[:, 0] - m['x0']) / C).astype(int); mj = ((vv[:, 2] - m['z0']) / C).astype(int); ok = (mi >= 0) & (mi < W_) & (mj >= 0) & (mj < H_)
    gm = np.zeros((H_, W_), bool); gm[mj[ok], mi[ok]] = True; gm = ndimage.binary_fill_holes(ndimage.binary_closing(gm, iterations=4)); gm = ndimage.binary_erosion(gm, iterations=1)
    green = gm & np.isfinite(G)
    Gf = np.where(green, G, np.nan); fill = ndimage.distance_transform_edt(~green, return_distances=False, return_indices=True); Gx = G[fill[0], fill[1]]
    base = ndimage.gaussian_filter(Gx, 0.6 / C); ripple = np.where(green, Gx - base, np.nan); rp95 = float(np.nanpercentile(np.abs(ripple), 95))
    smooth = np.where(green, base, G)   # candidate: ripple removed on the putting surface only
    np.stack([np.nan_to_num(smooth, nan=-9999).astype('<f4'), W.astype('<f4')]).tofile(A + 'scan_grid_%s_smooth.f32' % name)
    gz, gx = np.gradient(ndimage.gaussian_filter(Gx, 1.0 / C), C); slope = 100 * np.hypot(gx, gz)
    hi = np.unravel_index(np.nanargmax(Gf), Gf.shape); lo = np.unravel_index(np.nanargmin(Gf), Gf.shape); relief = float(np.nanmax(Gf) - np.nanmin(Gf))
    pins = [(p['Position']['x'], p['Position']['z'], p['Day'][:3]) for p in g['Holes'][hole - 1]['Pins']]
    fig, ax = plt.subplots(1, 2, figsize=(16, 7.5)); ext = [m['x0'], m['x0'] + W_ * C, m['z0'], m['z0'] + H_ * C]
    im = ax[0].imshow(Gf - np.nanmin(Gf), origin='lower', extent=ext, cmap='terrain'); plt.colorbar(im, ax=ax[0], fraction=0.04, label='height above the low point (m)')
    ax[0].contour(XX, ZZ, Gf, levels=np.arange(np.nanmin(Gf), np.nanmax(Gf), 0.02), colors='k', linewidths=0.35)
    s = slice(None, None, 8); ax[0].quiver(XX[s, s], ZZ[s, s], np.where(green, -gx, np.nan)[s, s], np.where(green, -gz, np.nan)[s, s], color='navy', scale=0.6, width=0.003)
    ax[0].plot(XX[hi], ZZ[hi], 'r^', ms=11); ax[0].plot(XX[lo], ZZ[lo], 'bv', ms=11)
    for px, pz, d in pins: ax[0].plot(px, pz, 'wo', mec='k', ms=6); ax[0].text(px + 0.4, pz + 0.4, d, fontsize=8)
    ax[0].set_title('hole %d green: 2 cm contours, arrows = downhill, total relief %.0f cm (red = high, blue = low)' % (hole, relief * 100))
    im = ax[1].imshow(ripple * 100, origin='lower', extent=ext, cmap='RdBu_r', vmin=-3, vmax=3); plt.colorbar(im, ax=ax[1], fraction=0.04, label='cm')
    ax[1].set_title('short-wavelength ripple (< ~1.5 m): p95 %.1f cm' % (rp95 * 100))
    for a_ in ax: a_.set_aspect('equal'); a_.set_xlim(np.nanmin(np.where(green, XX, np.nan)) - 2, np.nanmax(np.where(green, XX, np.nan)) + 2); a_.set_ylim(np.nanmin(np.where(green, ZZ, np.nan)) - 2, np.nanmax(np.where(green, ZZ, np.nan)) + 2)
    plt.tight_layout(); plt.savefig(OUT + 'undulation_h%d.jpg' % hole, dpi=65); plt.close()
    pin_sl = []
    for px, pz, d in pins:
        i, j = int((px - m['x0']) / C), int((pz - m['z0']) / C)
        if 0 <= i < W_ and 0 <= j < H_: pin_sl.append('%s %.1f%%' % (d, slope[j, i]))
    rep[hole] = dict(area_m2=round(float(green.sum() * C * C)), relief_cm=round(relief * 100), mean_slope_pct=round(float(np.nanmean(np.where(green, slope, np.nan))), 1),
                     max_slope_pct_p95=round(float(np.nanpercentile(np.where(green, slope, np.nan), 95)), 1), ripple_p95_cm=round(rp95 * 100, 1), pins_slope_1m_smoothed=pin_sl)
    print(hole, rep[hole])
json.dump(rep, open(OUT + 'undulation.json', 'w'), indent=1)
