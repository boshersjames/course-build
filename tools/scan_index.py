"""Index phone LiDAR scans (LAS): register each to the course LiDAR, score the fit against rules/photos.json scan limits, say
which green / bunker / tee it covers, and write course_photos/scans/index.csv plus a sheet per scan. Analysis only.

    python tools/scan_index.py [--scans course_photos/scans] [--out course_photos/scans] [--no-sheets]

For green scans MAD/p90 are measured inside the SVG green outline (the surround is reported as mad_whole_scan_m).
Fit: 0.5 m median grid of the scan, 4-parameter fit (dx, dz, dy, rotation about the vertical) to the course heightmap with a robust
loss, kept only if it beats the vertical-only fit. Reported per scan: shift, MAD and p90 of the residual after the fit, coverage of the
nearest green outline (SVG), doubled-ground share (cells whose 0.5 m window spans > 0.15 m with a bimodal height split), and the
verdict: ACCEPT when MAD <= scan.accept_mad_m and green coverage >= scan.coverage_min_pct (for green scans) and no doubled areas;
else RESCAN with the reason. Bunker scans also get rim-to-floor depth per sand blob vs the LiDAR.
Writes <out>/index.csv, <out>/<stem>_fit.png and <out>/<stem>_grid.npz (aligned 0.25 m grid + weight, for the apply step on the PC).
"""
import argparse, csv, glob, json, math, os, sys
import numpy as np
from scipy import ndimage, optimize
from matplotlib.path import Path as MP
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD, svg_outlines, match_outlines_to_holes

COLS = ["file", "hole", "kind", "points", "shift_x_m", "shift_z_m", "shift_y_m", "rot_deg", "mad_before_m", "mad_after_m", "p90_after_m", "mad_whole_scan_m",
        "coverage_pct", "doubled_pct", "area_m2", "scan_slope_mean_pct", "model_slope_mean_pct", "verdict", "reason", "extra"]


def grid_median(x, z, y, cell, min_pts=4):
    x0, z0 = x.min(), z.min(); ix = ((x - x0) / cell).astype(int); iz = ((z - z0) / cell).astype(int); W, H = ix.max() + 1, iz.max() + 1
    key = iz * W + ix; o = np.argsort(key); k = key[o]; yy = y[o]; st = np.r_[0, np.nonzero(np.diff(k))[0] + 1]; en = np.r_[st[1:], len(k)]
    G = np.full((H, W), np.nan); cnt = np.zeros((H, W)); spread = np.full((H, W), np.nan)
    for a, b in zip(st, en):
        G.flat[k[a]] = np.median(yy[a:b]); cnt.flat[k[a]] = b - a
        if b - a >= min_pts: spread.flat[k[a]] = np.percentile(yy[a:b], 90) - np.percentile(yy[a:b], 10)
    G[cnt < min_pts] = np.nan
    return G, x0, z0, spread


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--scans"); ap.add_argument("--out"); ap.add_argument("--no-sheets", action="store_true"); ap.add_argument("--only")
    a = ap.parse_args()
    import laspy
    from pyproj import Transformer
    C = course(); R = rules(C)["photos"]["scan"]; HM = Heightmap(C); G = GKD(C)
    root = C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, "course_photos")
    sdir = a.scans or os.path.join(root, "scans"); out = a.out or sdir; os.makedirs(out, exist_ok=True)
    tf = Transformer.from_crs("EPSG:4326", C["frame"]["crs"], always_xy=True); E0, N0 = C["frame"]["sw_corner"]; base = C["frame"]["base_elevation_m"]
    greens = match_outlines_to_holes(svg_outlines(C, "green"), G)
    model = lambda x, z: HM.sample(x, z, 1)
    files = sorted(glob.glob(os.path.join(sdir, "*.las")) + glob.glob(os.path.join(sdir, "*.laz")))
    if a.only: files = [f for f in files if a.only in os.path.basename(f)]
    rows = []
    for f in files:
        name = os.path.splitext(os.path.basename(f))[0]; l = laspy.read(f)
        crs = None
        try: crs = l.header.parse_crs()
        except Exception: pass
        X, Y, Z = np.asarray(l.x, float), np.asarray(l.y, float), np.asarray(l.z, float)
        if crs is None or crs.to_epsg() == 4326: E, N = tf.transform(X, Y)
        else:
            t2 = Transformer.from_crs(crs, C["frame"]["crs"], always_xy=True); E, N = t2.transform(X, Y)
        x = E - E0; z = N - N0; y = Z - base
        Gm, x0, z0, spread = grid_median(x, z, y, 0.5); jj, ii = np.nonzero(~np.isnan(Gm)); gx = x0 + (ii + 0.5) * 0.5; gz = z0 + (jj + 0.5) * 0.5; gy = Gm[jj, ii]
        cx, cz = gx.mean(), gz.mean()
        def resid(p):
            dx, dz, dy, rot = p; c, s = math.cos(rot), math.sin(rot); Xr = cx + c * (gx - cx) - s * (gz - cz) + dx; Zr = cz + s * (gx - cx) + c * (gz - cz) + dz
            return gy + dy - model(Xr, Zr)
        p0 = [0, 0, float(np.median(model(gx, gz) - gy)), 0]; r0 = resid(p0)
        sol = optimize.least_squares(resid, p0, loss="soft_l1", f_scale=0.1, x_scale=[1, 1, 0.1, 0.01], bounds=([-8, -8, -60, -0.15], [8, 8, 60, 0.15]))
        if np.median(np.abs(resid(sol.x))) > np.median(np.abs(r0)): sol.x = np.array(p0)
        dx, dz, dy, rot = sol.x; r = resid(sol.x)
        c, s = math.cos(rot), math.sin(rot); ax_ = cx + c * (x - cx) - s * (z - cz) + dx; az = cz + s * (x - cx) + c * (z - cz) + dz; ay = y + dy
        G25, a0, b0, sp25 = grid_median(ax_, az, ay, 0.25); H25, W25 = G25.shape; X25 = a0 + (np.arange(W25) + 0.5) * 0.25; Z25 = b0 + (np.arange(H25) + 0.5) * 0.25
        XX, ZZ = np.meshgrid(X25, Z25); M25 = model(XX.ravel(), ZZ.ravel()).reshape(XX.shape); D = G25 - M25
        valid = np.isfinite(G25); doubled = valid & (sp25 > 0.15); doubled_pct = 100 * doubled.sum() / max(valid.sum(), 1)
        num = ndimage.gaussian_filter(np.nan_to_num(G25), 2); den = ndimage.gaussian_filter(valid.astype(float), 2); Gs = np.where(den > 0.6, num / np.maximum(den, 1e-6), np.nan)
        gzs, gxs = np.gradient(Gs, 0.25); slope = 100 * np.hypot(gxs, gzs); mz, mx = np.gradient(ndimage.gaussian_filter(M25, 2), 0.25); mslope = 100 * np.hypot(mx, mz); mslope[np.isnan(slope)] = np.nan
        # which hole / what kind
        cen = np.array([np.nanmean(ax_), np.nanmean(az)]); hn = min(greens, key=lambda h: np.hypot(*(greens[h].mean(0) - cen))) if greens else ""
        kind = "green" if "green" in name.lower() or "geeen" in name.lower() else "bunker" if "bunker" in name.lower() else "tee" if "tee" in name.lower() else "other"
        coverage = ""
        if hn != "":
            O = greens[hn]; inside = MP(O).contains_points(np.c_[XX.ravel(), ZZ.ravel()]).reshape(XX.shape)
            coverage = 100 * (inside & valid).sum() / max(inside.sum(), 1)
            if kind == "other" and coverage > 30: kind = "green"
        mad = float(np.median(np.abs(r))); p90 = float(np.percentile(np.abs(r), 90)); mad_all = mad
        if kind == "green" and hn != "":
            # judge the putting surface: residual inside the green outline (the surround carries bunkers, banks and collar edges)
            ri = D[inside & valid]
            if ri.size > 50: mad = float(np.median(np.abs(ri))); p90 = float(np.percentile(np.abs(ri), 90))
        reasons = []
        if mad > R["accept_mad_m"]: reasons.append("MAD %.3f > %.2f" % (mad, R["accept_mad_m"]))
        if kind == "green" and coverage != "" and coverage < R["coverage_min_pct"]: reasons.append("green coverage %.0f%% < %d%%" % (coverage, R["coverage_min_pct"]))
        if doubled_pct > R.get("doubled_max_pct", 5): reasons.append("doubled ground %.1f%%" % doubled_pct)
        row = {"file": os.path.basename(f), "hole": hn, "kind": kind, "points": len(x), "shift_x_m": round(dx, 2), "shift_z_m": round(dz, 2), "shift_y_m": round(dy, 2), "rot_deg": round(math.degrees(rot), 2),
               "mad_before_m": round(float(np.median(np.abs(r0))), 3), "mad_after_m": round(mad, 3), "p90_after_m": round(p90, 3), "mad_whole_scan_m": round(mad_all, 3), "coverage_pct": round(coverage, 0) if coverage != "" else "",
               "doubled_pct": round(doubled_pct, 1), "area_m2": round(valid.sum() * 0.0625), "scan_slope_mean_pct": round(float(np.nanmean(slope)), 2), "model_slope_mean_pct": round(float(np.nanmean(mslope)), 2),
               "verdict": "ACCEPT" if not reasons else "RESCAN", "reason": "; ".join(reasons), "extra": ""}
        if kind == "bunker":
            rgb = np.c_[l.red, l.green, l.blue] / 65535.0 if "red" in l.point_format.dimension_names else None
            if rgb is not None:
                sand = (rgb.min(1) > 0.55) & ((rgb.max(1) - rgb.min(1)) / np.maximum(rgb.max(1), 1e-3) < 0.25)
                Sg = np.zeros((H25, W25), bool); si = ((ax_[sand] - a0) / 0.25).astype(int); sj = ((az[sand] - b0) / 0.25).astype(int); ok = (si >= 0) & (si < W25) & (sj >= 0) & (sj < H25); Sg[sj[ok], si[ok]] = True
                Sg = ndimage.binary_opening(ndimage.binary_closing(Sg, iterations=2)); lab, nb = ndimage.label(Sg); bl = []
                for k in range(1, nb + 1):
                    m = lab == k
                    if m.sum() < 80: continue
                    ring = ndimage.binary_dilation(m, iterations=6) & ~ndimage.binary_dilation(m, iterations=1)
                    bl.append("(%.0f,%.0f) %.0fm2 depth scan %.2f lidar %.2f" % (X25[np.nonzero(m)[1]].mean(), Z25[np.nonzero(m)[0]].mean(), m.sum() * 0.0625, np.nanpercentile(G25[ring], 75) - np.nanpercentile(G25[m], 10), np.nanpercentile(M25[ring], 75) - np.nanpercentile(M25[m], 10)))
                row["extra"] = " | ".join(bl)
        rows.append(row)
        np.savez_compressed(os.path.join(out, name + "_grid.npz"), grid=np.nan_to_num(G25, nan=-9999).astype("<f4"), weight=valid.astype("<f4"), x0=a0, z0=b0, cell=0.25, hole=hn, shift=[dx, dz, dy, math.degrees(rot)])
        print("%-36s hole %-3s %-7s %7d pts  shift (%+.2f,%+.2f,%+.2f) rot %+.2f  MAD %.3f->%.3f p90 %.3f  cover %s%%  doubled %.1f%%  %s %s" % (name, hn, kind, len(x), dx, dz, dy, math.degrees(rot), row["mad_before_m"], mad, p90, row["coverage_pct"], doubled_pct, row["verdict"], row["reason"]))
        if not a.no_sheets:
            import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
            fig, ax3 = plt.subplots(1, 3, figsize=(18, 6)); ext = [X25[0], X25[-1], Z25[0], Z25[-1]]
            lv = np.arange(np.floor(np.nanmin(G25) * 4) / 4, np.nanmax(G25) + 0.25, 0.25)
            ax3[0].imshow(slope, origin="lower", extent=ext, cmap="viridis", vmin=0, vmax=12); ax3[0].contour(X25, Z25, Gs, levels=lv, colors="w", linewidths=0.6); ax3[0].set_title(name + ": scan slope % + 0.25 m contours")
            ax3[1].imshow(mslope, origin="lower", extent=ext, cmap="viridis", vmin=0, vmax=12); ax3[1].contour(X25, Z25, np.where(np.isnan(Gs), np.nan, ndimage.gaussian_filter(M25, 2)), levels=lv, colors="w", linewidths=0.6); ax3[1].set_title("course LiDAR slope % + contours")
            im = ax3[2].imshow(D, origin="lower", extent=ext, cmap="RdBu_r", vmin=-0.4, vmax=0.4); ax3[2].set_title("scan - LiDAR (m), red = scan higher; %s" % row["verdict"]); plt.colorbar(im, ax=ax3[2], fraction=0.04)
            for axx in ax3:
                if hn != "": axx.plot(greens[hn][:, 0], greens[hn][:, 1], "r-", lw=0.8)
                axx.set_aspect("equal")
            fig.savefig(os.path.join(out, name + "_fit.png"), dpi=100); plt.close(fig)
    with open(os.path.join(out, "index.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=COLS); w.writeheader(); [w.writerow(r) for r in rows]
    print("wrote", os.path.join(out, "index.csv"))


if __name__ == "__main__":
    main()
