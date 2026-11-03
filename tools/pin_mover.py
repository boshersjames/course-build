"""Move failing pins to the nearest flat spot on the live green surface, and write the GKD on request.

    python tools/pin_mover.py [--holes 1,3] [--grids <AutomationOutput/checks>] [--out progress/pin_moves] [--write]

Reads the 0.5 m collider height grids CourseGreenChecks writes (checks/grid_heights_h<NN>.csv: x, z, y, surface), keeps the cells whose
top collider is the green mesh, and runs the GreenKeeper pin-area test (rules greens.pins: 17 lie points, probe_radius_m, max_lie_angle_deg)
on every candidate cell at least greens.pins.min_from_edge_m inside the green edge. A pin over the limit moves to the nearest passing cell
that is at least greens.pins.min_area_spacing_m from the other pins of that green; a pin already passing is not moved. Contours are never
touched: the surface is the real green (scan or LiDAR), the pin goes where it is flat. Writes pin_moves.md and pin_moves.csv
(hole, day, old_x, old_z, old_deg, new_x, new_z, new_y, new_deg, moved_m, note). --write edits the GKD text in place (numbers only, BOM and
layout kept) after a backup to <gkd>.bak_<stamp>; then run the Unity pin-area test (CourseGreenChecks.RunAll) and Greenkeeper save.
"""
import argparse, csv, glob, os, re, shutil, sys, time
import numpy as np
from scipy import ndimage
from scipy.interpolate import RegularGridInterpolator
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, GKD
from green_report import pin_area_angles


def load_grid(path):
    rows = list(csv.DictReader(open(path, encoding="utf-8-sig")))
    xs = np.array([float(r["x"]) for r in rows]); zs = np.array([float(r["z"]) for r in rows]); ys = np.array([float(r["y"]) for r in rows])
    green = np.array(["_green_-_mesh" in r["surface"].lower() for r in rows])
    ux = np.unique(np.round(xs, 3)); uz = np.unique(np.round(zs, 3)); step = float(np.min(np.diff(ux))) if len(ux) > 1 else 0.5
    ix = np.round((xs - ux[0]) / step).astype(int); iz = np.round((zs - uz[0]) / step).astype(int)
    H = np.full((len(ux), len(uz)), np.nan); M = np.zeros((len(ux), len(uz)), bool)
    H[ix, iz] = ys; M[ix, iz] = green
    Hg = np.where(M, H, np.nan)
    return ux, uz, step, Hg, M


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--holes"); ap.add_argument("--grids"); ap.add_argument("--out"); ap.add_argument("--write", action="store_true"); a = ap.parse_args()
    C = course(); P = rules(C)["greens"]["pins"]; G = GKD(C)
    gdir = a.grids or os.path.join(C.path("automation_output"), "checks"); out = a.out or os.path.join(C.dir, "progress", "pin_moves"); os.makedirs(out, exist_ok=True)
    holes = [int(h) for h in a.holes.split(",")] if a.holes else [h.number for h in G.holes if h.pins]
    lim = P["max_lie_angle_deg"]; edge = P["min_from_edge_m"]; spacing = P["min_area_spacing_m"]; place = lim - P.get("placement_margin_deg", 0.0)   # place under the limit by the margin: the live test reads a little higher than the grid
    md = ["# Pin moves: %s" % C["name"], "", "Rule greens.pins: lie <= %.1f deg (17 points, %.2f m probes), placed under %.1f deg (placement_margin_deg), >= %.1f m from the edge, >= %.1f m between pins. Surface = Unity collider grid (%s). Contours untouched." % (lim, P["probe_radius_m"], place, edge, spacing, gdir), "",
          "| hole | day | old lie | new lie | moved m | note |", "|---|---|---|---|---|---|"]
    rows = []; edits = []
    for hn in holes:
        gp = os.path.join(gdir, "grid_heights_h%02d.csv" % hn)
        if not os.path.exists(gp): md.append("| %d | - | - | - | - | no grid %s |" % (hn, os.path.basename(gp))); continue
        ux, uz, step, Hg, M = load_grid(gp)
        if M.sum() < 20: md.append("| %d | - | - | - | - | %d green cells only |" % (hn, M.sum())); continue
        # linear interpolation over the green cells only; outside the green -> nan so a probe off the green fails the point
        F = RegularGridInterpolator((ux, uz), Hg, method="linear", bounds_error=False, fill_value=np.nan)
        def sample(x, z): return F(np.column_stack([np.asarray(x, float), np.asarray(z, float)]))
        dist = ndimage.distance_transform_edt(M) * step   # m to the nearest non-green cell
        cand = np.argwhere(dist >= edge); cx = ux[cand[:, 0]]; cz = uz[cand[:, 1]]
        ang = pin_area_angles(sample, cx, cz, P); ok = np.isfinite(ang) & (ang <= place)
        h = G.hole(hn); pins = h.pins; cur = []
        for p in pins:
            x, z = p["Position"]["x"], p["Position"]["z"]; a0 = pin_area_angles(sample, [x], [z], P)[0]; cur.append([p["Day"], x, z, float(a0) if np.isfinite(a0) else 99.0])
        fixed = [(x, z) for d, x, z, a0 in cur if a0 <= lim]
        for d, x, z, a0 in sorted(cur, key=lambda c: -c[3]):
            if a0 <= lim: rows.append((hn, d, x, z, a0, x, z, None, a0, 0.0, "pass, unchanged")); md.append("| %d | %s | %.1f | %.1f | 0.0 | pass, unchanged |" % (hn, d, a0, a0)); continue
            far = np.ones(len(cx), bool)
            for fx, fz in fixed: far &= np.hypot(cx - fx, cz - fz) >= spacing
            sel = ok & far
            if not sel.any(): rows.append((hn, d, x, z, a0, x, z, None, a0, 0.0, "NO passing spot >= %.0f m from the others: green needs BoJa's call" % spacing)); md.append("| %d | %s | %.1f | - | - | **no passing spot** >= %.0f m from the other pins |" % (hn, d, a0, spacing)); continue
            dd = np.hypot(cx - x, cz - z); dd[~sel] = np.inf; dmin = dd.min()
            near = np.where(dd <= dmin + 1.0)[0]; j = int(near[np.argmin(ang[near])])   # nearest passing cell, but the flattest within a metre of it, so the pin sits inside the flat area, not on its edge
            nx, nz = float(cx[j]), float(cz[j]); ny = float(sample([nx], [nz])[0])
            fixed.append((nx, nz)); rows.append((hn, d, x, z, a0, nx, nz, ny, float(ang[j]), float(dd[j]), "moved"))
            md.append("| %d | %s | %.1f | %.1f | %.1f | moved to (%.2f, %.2f), %d passing cells on this green |" % (hn, d, a0, ang[j], dd[j], nx, nz, int(ok.sum())))
            edits.append((hn, d, x, z, nx, ny, nz))
    with open(os.path.join(out, "pin_moves.csv"), "w", newline="") as fh:
        w = csv.writer(fh); w.writerow(["hole", "day", "old_x", "old_z", "old_deg", "new_x", "new_z", "new_y", "new_deg", "moved_m", "note"])
        for r in rows: w.writerow([r[0], r[1], "%.2f" % r[2], "%.2f" % r[3], "%.1f" % r[4], "%.2f" % r[5], "%.2f" % r[6], "" if r[7] is None else "%.3f" % r[7], "%.1f" % r[8], "%.1f" % r[9], r[10]])
    moved = sum(1 for r in rows if r[10] == "moved"); stuck = sum(1 for r in rows if r[10].startswith("NO"))
    md += ["", "%d pins checked, %d moved, %d with no passing spot, %d already passing." % (len(rows), moved, stuck, len(rows) - moved - stuck)]
    if a.write and edits:
        txt = G.text; bak = G.path + ".bak_" + time.strftime("%Y%m%d_%H%M%S"); shutil.copy(G.path, bak); n = 0
        for hn, d, ox, oz, nx, ny, nz in edits:
            m = re.search(r'"HoleNumber":\s*%d\b' % hn, txt); start = m.start(); nxt = re.search(r'"HoleNumber":\s*\d+', txt[m.end():]); end = m.end() + (nxt.start() if nxt else len(txt) - m.end())
            pat = re.compile(r'("Day":\s*"%s",\s*"Position":\s*\{\s*"x":\s*)([-0-9.eE]+)(,\s*"y":\s*)([-0-9.eE]+)(,\s*"z":\s*)([-0-9.eE]+)' % d)
            mm = pat.search(txt, start, end)
            if not mm or abs(float(mm.group(2)) - ox) > 0.01 or abs(float(mm.group(6)) - oz) > 0.01: md.append("WRITE SKIPPED hole %d %s: pin text not found where expected" % (hn, d)); continue
            rep = mm.group(1) + ("%.4f" % nx) + mm.group(3) + ("%.6f" % ny) + mm.group(5) + ("%.4f" % nz); txt = txt[:mm.start()] + rep + txt[mm.end():]; n += 1
        G.write_text(txt); md.append("GKD written: %d pins, backup %s. Next: Unity CourseGreenChecks.RunAll (pin-area test on the live colliders), then Greenkeeper save." % (n, bak))
    elif edits: md.append("Dry run: add --write to edit the GKD (backup first, numbers only).")
    open(os.path.join(out, "pin_moves.md"), "w").write("\n".join(md) + "\n"); print("\n".join(md)); print("wrote", out)


if __name__ == "__main__":
    main()
