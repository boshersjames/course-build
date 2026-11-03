"""Compare the Unity physics census with the SVG ground truth: where does the ball see the wrong surface?

    python tools/physics_check.py [--census <AutomationOutput/checks/physics_census.csv>] [--out progress/physics_check]

Reads physics_census.csv (x, z, y, collider, material at each grid point: written by Course Checks > Physics in Unity) and the course
SVG (green, fairway, tee, bunker, water, rough, deep polygons). For every point inside an SVG polygon, the expected GSPro material is
rules/physics.json materials[svg_classes[kind]]; a point whose top collider carries a different material is a mismatch. Writes
mismatches.csv (x, z, svg_class, expected, found, collider) clustered per mesh, and a per-class summary; the share of mismatched points
per class is compared with rules physics.census.mismatch_tolerance_pct. Points within 4 m of a green or tee edge, or under a collider
whose name carries one of physics.census.edge_name_tokens (blend strips), are reported as 'edge' (collar / first-cut rings and blends
legitimately differ there), not failed. A material listed in physics.census.overlays for the SVG class (cart paths, shrub chunks, rocks,
fences and the outer deep rough on the SVG 'rough' fallback) is reported as 'overlay', not failed. Nothing is modified.
"""
import argparse, csv, os, sys
import numpy as np
from matplotlib.path import Path as MP
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, svg_outlines, svg_paths


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--census"); ap.add_argument("--out"); a = ap.parse_args()
    C = course(); R = rules(C)["physics"]; mats = R["materials"]; cls = R["svg_classes"]; tol = R["census"]["mismatch_tolerance_pct"]
    overlays = {c: {mats[o] for o in v} for c, v in R["census"].get("overlays", {}).items()}; edge_tokens = [t.lower() for t in R["census"].get("edge_name_tokens", [])]
    cen = a.census or os.path.join(C.path("automation_output"), "checks", "physics_census.csv")
    if not os.path.exists(cen): sys.exit("census not found: %s (run Course Checks > Physics in Unity, or sync AutomationOutput/checks)" % cen)
    out = a.out or os.path.join(C.dir, "progress", "physics_check"); os.makedirs(out, exist_ok=True)
    pts = list(csv.DictReader(open(cen, encoding="utf-8-sig"))); P = np.array([[float(r["x"]), float(r["z"])] for r in pts])
    polys = []
    for kind, c in cls.items():
        try:
            for k, path in svg_paths(C, kind).items(): polys.append((kind, c, k, path, None))
        except Exception: pass
    # precedence: smaller features win (green > tee > bunker > water > fairway > rough > deep)
    order = {"green": 0, "tee": 1, "bunker": 2, "water": 3, "fairway": 4, "rough": 5, "deep": 6}
    polys.sort(key=lambda t: order.get(t[0], 9))
    assigned = np.full(len(P), -1); edge = np.zeros(len(P), bool)
    for i, (kind, c, k, path, poly) in enumerate(polys):
        inside = path.contains_points(P) & (assigned < 0); assigned[inside] = i
        if kind in ("green", "tee"):
            # 4 m ring outside the edge: collar / first cut may legitimately carry another material
            ring = path.contains_points(P, radius=4.0) & ~path.contains_points(P); edge |= ring
        if kind in ("fairway", "bunker"):
            # 2 m ring: the OPCD blend strips sit on the edge and carry the neighbour's material by rule
            ring = path.contains_points(P, radius=2.0) & ~path.contains_points(P); edge |= ring
    rows = []; summ = {}; ov = {}
    for j, r in enumerate(pts):
        if assigned[j] < 0: continue
        kind, c, k, _, _ = polys[assigned[j]]; exp = mats[c]; found = r["material"]
        s = summ.setdefault(c, {"points": 0, "mismatch": 0, "edge": 0, "overlay": 0}); s["points"] += 1
        if found != exp:
            if edge[j] or any(t in r["collider"].lower() for t in edge_tokens): s["edge"] += 1; continue
            if found in overlays.get(c, ()): s["overlay"] += 1; ov.setdefault((r["collider"], found), 0); ov[(r["collider"], found)] += 1; continue
            s["mismatch"] += 1; rows.append({"x": r["x"], "z": r["z"], "svg_class": c, "svg_id": k, "expected": exp, "found": found, "collider": r["collider"]})
    with open(os.path.join(out, "mismatches.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=["x", "z", "svg_class", "svg_id", "expected", "found", "collider"]); w.writeheader(); [w.writerow(r) for r in rows]
    bym = {}
    for r in rows: bym.setdefault((r["collider"], r["expected"], r["found"]), []).append(r)
    md = ["# Physics check: census vs SVG (%s)" % C["name"], "", "| class | points | mismatched | % | edge / blend (not failed) | overlay (not failed) | result |", "|---|---|---|---|---|---|---|"]
    bad = False
    for c, s in sorted(summ.items()):
        pct = 100.0 * s["mismatch"] / max(s["points"], 1); ok = pct <= tol; bad |= not ok
        md.append("| %s | %d | %d | %.1f | %d | %d | %s |" % (c, s["points"], s["mismatch"], pct, s["edge"], s["overlay"], "PASS" if ok else "**FAIL**"))
    md += ["", "## Mismatched meshes (by collider: expected -> found, points, first position)", ""]
    for (col, exp, found), rs in sorted(bym.items(), key=lambda kv: -len(kv[1]))[:60]:
        md.append("- `%s`: expected %s, carries %s, %d points, e.g. (%s, %s)" % (col, exp, found or "none", len(rs), rs[0]["x"], rs[0]["z"]))
    if ov:
        md += ["", "## Overlays on the rough fallback (by design: paths, shrubs, rocks, fences, outer deep rough; glance, not failed)", ""]
        for (col, found), n in sorted(ov.items(), key=lambda kv: -kv[1])[:25]: md.append("- `%s`: %s, %d points" % (col, found, n))
    open(os.path.join(out, "physics_check.md"), "w").write("\n".join(md) + "\n"); print("\n".join(md[:30])); print("wrote", out)
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
