"""Headless twin of the Unity edge survey: read profiles.csv (every 5 cm sample of every green-edge profile) and report the real steps.

    python tools/edge_profiles.py [--profiles <AutomationOutput/checks/profiles.csv>] [--out progress/edge_profiles]

Step (rules greens.checks.edge_survey.step_definition) = one sample's rise minus the median rise of its slope_window neighbours either
side, so plain slope is not a step. Per hole: profiles, real steps over max_step_m, worst step with the surface pair and distance from the
green edge; over all greens: the steps by surface pair with mean signed size (positive = rising outward) so a convention (collar proud at
its outer rim on every green) shows as one line. Surfaces are classed by name: green mesh, green blend, collar, fairway blend, fairway,
bunker, else rough. Writes edge_profiles.md and steps.csv (hole, x, z, d_m, step_cm, from, to). Nothing is modified.
"""
import argparse, csv, os, statistics, sys
from collections import defaultdict, Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules


def cls(n):
    n = n.lower()
    if "_green_-_mesh" in n: return "green"
    if "green" in n and "blend" in n: return "green_blend"
    if "collar" in n or "apron" in n: return "collar"
    if "fairway" in n and "blend" in n: return "fairway_blend"
    if "fairway" in n: return "fairway"
    if "bunker" in n: return "bunker"
    if "_tee_-_mesh" in n or "tee_-_blend" in n: return "tee"
    return "rough"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--profiles"); ap.add_argument("--out"); a = ap.parse_args()
    C = course(); E = rules(C)["greens"]["checks"]["edge_survey"]; smax = E["max_step_m"]; W = int(E.get("slope_window", 2))
    f = a.profiles or os.path.join(C.path("automation_output"), "checks", "profiles.csv")
    if not os.path.exists(f): sys.exit("profiles not found: %s (run Course Checks > Greens in Unity; it writes checks/profiles.csv)" % f)
    out = a.out or os.path.join(C.dir, "progress", "edge_profiles"); os.makedirs(out, exist_ok=True)
    rows = list(csv.DictReader(open(f, encoding="utf-8-sig")))
    if "hole" not in rows[0] or "d_m" not in rows[0]: sys.exit("%s is not a profiles.csv (hole, edge_x, edge_z, d_m, y, surface); edge_survey.csv goes to edge_survey_summary.py" % f)
    prof = defaultdict(list)
    for r in rows: prof[(int(r["hole"]), r["edge_x"], r["edge_z"])].append((float(r["d_m"]), float(r["y"]), r["surface"]))
    per = defaultdict(list); pairs = defaultdict(list); steps = []
    for (h, x, z), P in prof.items():
        P.sort(); d = [P[i][1] - P[i - 1][1] for i in range(1, len(P))]; best = (0.0, "-", 0.0)
        for i in range(len(d)):
            nb = [d[j] for j in range(max(0, i - W), min(len(d), i + W + 1)) if j != i]
            st = d[i] - (statistics.median(nb) if nb else 0); pair = cls(P[i][2]) + ">" + cls(P[i + 1][2])
            if abs(st) > smax: pairs[pair].append(st); steps.append((h, x, z, P[i + 1][0], st, P[i][2], P[i + 1][2]))
            if abs(st) > abs(best[0]): best = (st, pair, P[i + 1][0])
        per[h].append(best)
    md = ["# Green edge profiles: real steps (%s)" % C["name"], "", "Step = rise over one sample minus the median rise of %d neighbours either side (greens.checks.edge_survey.step_definition); limit %.0f cm. Positive = rising outward from the green." % (W, smax * 100), "",
          "| hole | profiles | steps over | worst cm | at (pair, m from edge) | dominant pair | result |", "|---|---|---|---|---|---|---|"]
    allok = True
    for h in sorted(per):
        L = per[h]; over = [b for b in L if abs(b[0]) > smax]; w = max(L, key=lambda b: abs(b[0])); dom = Counter(b[1] for b in over).most_common(1)
        ok = abs(w[0]) <= smax; allok &= ok
        md.append("| %d | %d | %d | %+.1f | %s, %.2f | %s | %s |" % (h, len(L), len(over), w[0] * 100, w[1], w[2], dom[0][0] if dom else "-", "PASS" if ok else "**FAIL**"))
    md += ["", "## Steps over the limit by surface pair, all greens (count, mean signed cm, share rising outward)", ""]
    for pair, v in sorted(pairs.items(), key=lambda kv: -len(kv[1]))[:15]: md.append("- `%s`: %d, %+.1f cm, %d%% up" % (pair, len(v), 100 * sum(v) / len(v), 100 * sum(1 for q in v if q > 0) / len(v)))
    md += ["", "Reading: a pair on every green with one sign is a convention (a collar proud at its rim); a pair on one green is that green's edit."]
    open(os.path.join(out, "edge_profiles.md"), "w").write("\n".join(md) + "\n")
    with open(os.path.join(out, "steps.csv"), "w", newline="") as fh:
        wr = csv.writer(fh); wr.writerow(["hole", "x", "z", "d_m", "step_cm", "from", "to"]); [wr.writerow([h, x, z, "%.2f" % dm, "%.1f" % (st * 100), a_, b_]) for h, x, z, dm, st, a_, b_ in sorted(steps)]
    print("\n".join(md)); print("wrote", out); sys.exit(0 if allok else 1)


if __name__ == "__main__":
    main()
