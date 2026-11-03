"""Where do the green-edge steps come from? Group the Unity edge survey by the collider pair each step crosses.

    python tools/edge_survey_summary.py [--survey <AutomationOutput/checks/edge_survey.csv>] [--out progress/edge_survey]

Reads edge_survey.csv (green, x, z, step, lip, stepAt, surfaces: written by Course Checks > Greens), and for every green counts the
profiles whose step exceeds rules greens.checks.edge_survey.max_step_m, grouped by the surface sequence the profile crossed
(e.g. green_17>green_17_B_Rough_Green_-_Blend>Rough_Outer). A pair that dominates every green is a modelling convention (the blend
ring sits above or below the green mesh), not eighteen separate faults; a pair that appears on one green is a local fault.
Writes edge_survey_summary.md and the per-green worst 10 profiles with positions. Nothing is modified.
"""
import argparse, csv, os, sys
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--survey"); ap.add_argument("--out"); a = ap.parse_args()
    C = course(); E = rules(C)["greens"]["checks"]["edge_survey"]; smax, lmax = E["max_step_m"], E["max_lip_m"]
    f = a.survey or os.path.join(C.path("automation_output"), "checks", "edge_survey.csv")
    if not os.path.exists(f): sys.exit("edge survey not found: %s (run Course Checks > Greens in Unity)" % f)
    out = a.out or os.path.join(C.dir, "progress", "edge_survey"); os.makedirs(out, exist_ok=True)
    rows = list(csv.DictReader(open(f, encoding="utf-8-sig")))
    if not rows or "green" not in rows[0]: sys.exit("%s is not an edge_survey.csv (green, x, z, step, lip, stepAt, surfaces); profiles.csv goes to edge_profiles.py" % f)
    byg = defaultdict(list)
    for r in rows: byg[r["green"]].append(r)
    pairs_all = defaultdict(int); md = ["# Green edge survey: where the steps are (%s)" % C["name"], "", "Rules: step <= %.0f cm, lip <= %.0f cm (greens.checks.edge_survey)." % (smax * 100, lmax * 100), ""]
    md += ["| green | profiles | step over | lip over | worst step cm | dominant crossing (share of steps over) |", "|---|---|---|---|---|---|"]
    detail = ["green,x,z,step_cm,lip_cm,step_at_m,surfaces"]
    for g, rs in sorted(byg.items()):
        over = [r for r in rs if float(r["step"]) > smax]; lover = sum(1 for r in rs if float(r["lip"]) > lmax)
        pairs = defaultdict(int)
        for r in over: pairs[r["surfaces"]] += 1; pairs_all[r["surfaces"]] += 1
        dom = max(pairs.items(), key=lambda kv: kv[1]) if pairs else ("-", 0)
        worst = max((float(r["step"]) for r in rs), default=0)
        md.append("| %s | %d | %d | %d | %.1f | %s (%d%%) |" % (g, len(rs), len(over), lover, worst * 100, dom[0], 100 * dom[1] / max(len(over), 1)))
        for r in sorted(rs, key=lambda r: -float(r["step"]))[:10]:
            detail.append(",".join([g, r["x"], r["z"], "%.1f" % (float(r["step"]) * 100), "%.1f" % (float(r["lip"]) * 100), r["stepAt"], r["surfaces"]]))
    md += ["", "## Crossings over the step limit, all greens", ""]
    for k, n in sorted(pairs_all.items(), key=lambda kv: -kv[1])[:20]: md.append("- %d: `%s`" % (n, k))
    md += ["", "Reading: one crossing on every green = a convention to fix once (blend ring height, collar mesh offset); a crossing on one green = that green's edit."]
    open(os.path.join(out, "edge_survey_summary.md"), "w").write("\n".join(md) + "\n"); open(os.path.join(out, "worst_profiles.csv"), "w").write("\n".join(detail) + "\n")
    print("\n".join(md)); print("wrote", out)


if __name__ == "__main__":
    main()
