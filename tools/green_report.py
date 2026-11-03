"""Headless green report from the course LiDAR (or a scan grid): slope stats, relief, pin-area test, usable area, pin edge distance.

    python tools/green_report.py [--holes 1,2,..] [--out progress/green_report] [--surface lidar|<grid.f32 meta.json>] [--sheets]

Outline: the SVG green matched to the GKD green centre (or a Unity greens.json export when present in AutomationOutput).
Surface: the course heightmap by default (what the OPCD mesh drapes before any scan), or a scan grid written by the scan tools.
Rules: rules/greens.json -> pins (GreenKeeper method: probe 0.3 m N/E/S/W, worst angle over 17 points, pass <= 1.7 deg), internal_slope_pct,
usable grid/inset. Output: green_report.csv, green_report.md, optional PNG sheet per green. Analysis only; it writes no GKD.
"""
import argparse, csv, json, math, os, sys
import numpy as np
from scipy import ndimage
from matplotlib.path import Path as MP
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD, svg_outlines, match_outlines_to_holes, automation_export


def _probe_offsets(P):
    """Offsets (dx, dz) of the 17 lie points (pin, 8 at ring1, 8 at ring2) and the 4 probes per lie point, flattened."""
    pts = [(0.0, 0.0)]
    for rr in (P["ring1_m"], P["ring2_m"]):
        for k in range(P["ring_points"]):
            a = 2 * math.pi * k / P["ring_points"]; pts.append((rr * math.cos(a), rr * math.sin(a)))
    r = P["probe_radius_m"]
    probes = [(0, r), (r, 0), (0, -r), (-r, 0)]
    return np.array(pts), np.array(probes), r


def pin_area_angles(sample, xs, zs, P):
    """GreenKeeper method, vectorised: worst |asin(dz/r)| over the 17 lie points x 4 probes, per (x, z). One heightmap call."""
    xs = np.atleast_1d(np.asarray(xs, float)); zs = np.atleast_1d(np.asarray(zs, float))
    pts, probes, r = _probe_offsets(P)
    LX = xs[:, None] + pts[None, :, 0]; LZ = zs[:, None] + pts[None, :, 1]               # (n, 17)
    PX = LX[:, :, None] + probes[None, None, :, 0]; PZ = LZ[:, :, None] + probes[None, None, :, 1]   # (n, 17, 4)
    y0 = sample(LX.ravel(), LZ.ravel()).reshape(LX.shape)
    yp = sample(PX.ravel(), PZ.ravel()).reshape(PX.shape)
    ang = np.degrees(np.arcsin(np.clip((yp - y0[:, :, None]) / r, -1, 1)))
    return np.round(np.abs(ang).max(axis=(1, 2)), 1)


def pin_area_angle(sample, x, z, P):
    return float(pin_area_angles(sample, [x], [z], P)[0])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--holes"); ap.add_argument("--out"); ap.add_argument("--sheets", action="store_true")
    ap.add_argument("--cell", type=float, default=0.25, help="analysis grid, m")
    ap.add_argument("--order", type=int, default=3, help="heightmap interpolation order (3 = cubic, as the Unity drape)")
    ap.add_argument("--smooth", type=float, default=0.0, help="Gaussian sigma (m) for the LiDAR noise low-pass allowed by rules/greens.json checks.noise_lowpass (0 = off)")
    a = ap.parse_args()
    C = course(); R = rules(C)["greens"]; P = R["pins"]; SL = R["internal_slope_pct"]
    HM = Heightmap(C); G = GKD(C)
    out = a.out or os.path.join(C.dir, "progress", "green_report"); os.makedirs(out, exist_ok=True)
    holes = [int(h) for h in a.holes.split(",")] if a.holes else [h.number for h in G.holes if h.green_centre is not None]

    GM = automation_export(C, "greens.json")
    outlines = {}
    if GM:
        for h in G.holes:
            if h.green_centre is None: continue
            ks = [k for k in GM if k.endswith("_Green_-_Mesh") and GM[k]]
            if not ks: break
            k = min(ks, key=lambda k: np.hypot(*(np.array(GM[k]["v"])[:, [0, 2]].mean(0) - h.green_centre)))
            outlines[h.number] = np.array(GM[k]["v"])[:, [0, 2]]
        src = "Unity greens.json"
    if not outlines:
        outlines = match_outlines_to_holes(svg_outlines(C, "green"), G); src = "course SVG"

    sample = lambda x, z: HM.sample(x, z, a.order)
    if a.smooth > 0:
        lp = R["checks"]["noise_lowpass"]["sigma_m"]
        if not (lp[0] <= a.smooth <= lp[1]): print("warning: --smooth %.2f outside the rule band %s m" % (a.smooth, lp))
        HM.H = ndimage.gaussian_filter(HM.H, a.smooth / HM.S); HM._spl = None
    rows = []
    for hn in holes:
        h = G.hole(hn)
        if hn not in outlines:
            rows.append({"hole": hn, "note": "no outline"}); continue
        O = outlines[hn]; C_ = a.cell
        x0, x1 = O[:, 0].min() - 3, O[:, 0].max() + 3; z0, z1 = O[:, 1].min() - 3, O[:, 1].max() + 3
        XX, ZZ, H = HM.grid(x0, x1, z0, z1, C_, a.order)
        inside = MP(O).contains_points(np.c_[XX.ravel(), ZZ.ravel()]).reshape(XX.shape)
        if GM:  # mesh vertices, not a polygon: rasterise and fill
            gm = np.zeros(XX.shape, bool); ii = ((O[:, 0] - x0) / C_).astype(int); jj = ((O[:, 1] - z0) / C_).astype(int)
            ok = (ii >= 0) & (ii < XX.shape[1]) & (jj >= 0) & (jj < XX.shape[0]); gm[jj[ok], ii[ok]] = True
            inside = ndimage.binary_fill_holes(ndimage.binary_closing(gm, iterations=6))
        gz, gx = np.gradient(ndimage.gaussian_filter(H, 1.0 / C_), C_); slope = 100 * np.hypot(gx, gz)
        s_in = slope[inside]; h_in = H[inside]
        area = inside.sum() * C_ * C_; relief = float(h_in.max() - h_in.min()) if h_in.size else float("nan")
        # pin-area test on GKD pins
        edge = ndimage.distance_transform_edt(inside) * C_
        pins = []
        for p in h.pins:
            px, pz = p["Position"]["x"], p["Position"]["z"]
            i, j = int((px - x0) / C_), int((pz - z0) / C_)
            de = float(edge[j, i]) if 0 <= j < edge.shape[0] and 0 <= i < edge.shape[1] else 0.0
            ang = pin_area_angle(sample, px, pz, P)
            pins.append({"day": p.get("Day", "")[:3], "x": px, "z": pz, "angle_deg": ang, "edge_m": round(de, 1),
                         "ok": ang <= P["max_lie_angle_deg"] and de >= P["min_from_edge_m"]})
        # usable area: grid at usable_grid_m, inset usable_inset_m, pin-area pass
        step = int(round(P["usable_grid_m"] / C_)); cand = inside & (edge >= P["usable_inset_m"])
        jj, ii = np.nonzero(cand); sel = (jj % step == 0) & (ii % step == 0)
        tx, tz = XX[jj[sel], ii[sel]], ZZ[jj[sel], ii[sel]]
        angs = pin_area_angles(sample, tx, tz, P) if tx.size else np.zeros(0)
        okm = angs <= P["max_lie_angle_deg"]
        usable_pct = 100.0 * okm.mean() if tx.size else float("nan")
        # valid pin areas: greedy pick of passing points >= min spacing
        pts = list(zip(tx[okm], tz[okm]))
        areas = []
        for q in pts:
            if all(math.hypot(q[0] - r[0], q[1] - r[1]) >= P["min_area_spacing_m"] for r in areas): areas.append(q)
        row = {"hole": hn, "outline": src, "area_m2": round(area), "relief_m": round(relief, 2),
               "slope_mean_pct": round(float(s_in.mean()), 2), "slope_p90_pct": round(float(np.percentile(s_in, 90)), 2), "slope_max_pct": round(float(s_in.max()), 1),
               "pct_over_3": round(100 * float((s_in > 3).mean()), 1), "pct_over_4": round(100 * float((s_in > 4).mean()), 1),
               "pins": len(pins), "pins_failing": sum(1 for p in pins if not p["ok"]), "pin_angles": " ".join("%s:%.1f" % (p["day"], p["angle_deg"]) for p in pins),
               "usable_pct": round(usable_pct, 1), "valid_areas": len(areas),
               "flag_slope": "steep >%d%%" % SL["flag_over"] if s_in.mean() > SL["flag_over"] else ("flat <%d%%" % SL["min"] if s_in.mean() < SL["min"] else ""),
               "pass": all(p["ok"] for p in pins) and len(areas) >= P["min_valid_areas"]}
        rows.append(row)
        if a.sheets:
            import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
            fig, ax = plt.subplots(1, 2, figsize=(14, 6.5)); ext = [x0, x1, z0, z1]
            Hm = np.where(inside, H, np.nan); im = ax[0].imshow(Hm - np.nanmin(Hm), origin="lower", extent=ext, cmap="terrain"); plt.colorbar(im, ax=ax[0], fraction=0.04, label="m above low point")
            ax[0].contour(XX, ZZ, Hm, levels=np.arange(np.nanmin(Hm), np.nanmax(Hm), 0.02), colors="k", linewidths=0.3)
            im2 = ax[1].imshow(np.where(inside, slope, np.nan), origin="lower", extent=ext, cmap="RdYlGn_r", vmin=0, vmax=8); plt.colorbar(im2, ax=ax[1], fraction=0.04, label="slope %")
            for axx in ax:
                for p in pins: axx.plot(p["x"], p["z"], "o", mfc="lime" if p["ok"] else "red", mec="k", ms=7)
                axx.plot(O[:, 0], O[:, 1], "r-", lw=0.8); axx.set_aspect("equal")
            fig.suptitle("Hole %d green (%s): relief %.2f m, mean slope %.1f%%, usable %.0f%%, %d valid areas" % (hn, src, relief, s_in.mean(), usable_pct, len(areas)))
            fig.savefig(os.path.join(out, "green_%02d.png" % hn), dpi=110); plt.close(fig)

    keys = ["hole", "outline", "area_m2", "relief_m", "slope_mean_pct", "slope_p90_pct", "slope_max_pct", "pct_over_3", "pct_over_4", "pins", "pins_failing", "pin_angles", "usable_pct", "valid_areas", "flag_slope", "pass", "note"]
    with open(os.path.join(out, "green_report.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=keys); w.writeheader(); [w.writerow({k: r.get(k, "") for k in keys}) for r in rows]
    md = ["# Green report (%s, surface: course heightmap order %d%s)" % (C["name"], a.order, ", low-pass sigma %.1f m" % a.smooth if a.smooth else ""), "",
          "Rules: pin area <= %.1f deg over 17 points, pins >= %.0f m from the edge, >= %d valid areas >= %.0f m apart; internal slope %d-%d%%." % (P["max_lie_angle_deg"], P["min_from_edge_m"], P["min_valid_areas"], P["min_area_spacing_m"], SL["min"], SL["max"]), "",
          "| hole | area m2 | relief m | slope mean/p90 % | >3% | pins fail | pin angles | usable % | areas | flag | pass |", "|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        if "note" in r: md.append("| %d | %s |" % (r["hole"], r["note"])); continue
        md.append("| %d | %d | %.2f | %.1f / %.1f | %.0f | %d/%d | %s | %.0f | %d | %s | %s |" % (r["hole"], r["area_m2"], r["relief_m"], r["slope_mean_pct"], r["slope_p90_pct"], r["pct_over_3"], r["pins_failing"], r["pins"], r["pin_angles"], r["usable_pct"], r["valid_areas"], r["flag_slope"], "PASS" if r["pass"] else "FAIL"))
    open(os.path.join(out, "green_report.md"), "w").write("\n".join(md) + "\n")
    print("\n".join(md)); print("wrote", out)


if __name__ == "__main__":
    main()
