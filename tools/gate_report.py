"""One per-hole pass/fail table from every check output that exists, so the install gate is a table, not screenshots.

    python tools/gate_report.py [--out progress/gate_report] [--holes 1,2,..]

Reads what is present and says what is missing:
  <automation_output>/checks/greens_all18.csv   CourseGreenChecks (Unity): edge step/lip, collider dy, tiers, crowns, pins, areas
  <automation_output>/checks/grid_heights_h*.csv -> fall direction vs LiDAR (rules greens.checks.fall_direction), computed here
  <automation_output>/pin_area.csv or checks/pin_area.csv
  <out>/../green_report/green_report.csv         tools/green_report.py (LiDAR proxy, used when Unity results are absent)
  <out>/../flyover_gate/flyover_gate.json        tools/flyover_gate.py
  GKD checks via tools/gkd_tools.py check (always run)
Writes gate_report.md / .json. Exit code 1 if any hole fails. Rules from rules/greens.json; nothing hard-coded.
"""
import argparse, csv, glob, json, os, subprocess, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD


def read_csv(p):
    return list(csv.DictReader(open(p, encoding="utf-8-sig"))) if os.path.exists(p) else None


def fall_direction(grid_csv, HM, FR):
    """Compare collider heights grid with the LiDAR: aspect (downhill bearing) per point and slope correlation."""
    rows = read_csv(grid_csv)
    if not rows: return None
    pts = np.array([[float(r["x"]), float(r["z"]), float(r["y"])] for r in rows if r.get("surface", "").lower().find("green") >= 0])
    if len(pts) < 16: return None
    xs = np.unique(np.round(pts[:, 0], 2)); zs = np.unique(np.round(pts[:, 1], 2))
    if len(xs) < 4 or len(zs) < 4: return None
    gx = {v: i for i, v in enumerate(xs)}; gz = {v: i for i, v in enumerate(zs)}
    H = np.full((len(zs), len(xs)), np.nan)
    for x, z, y in pts: H[gz[round(z, 2)], gx[round(x, 2)]] = y
    cell = float(np.median(np.diff(xs)))
    L = HM.sample(*np.meshgrid(xs, zs)[::-1][::-1], 3).reshape(H.shape) if False else HM.sample(np.meshgrid(xs, zs)[0].ravel(), np.meshgrid(xs, zs)[1].ravel(), 3).reshape(H.shape)
    def grad(A):
        gzz, gxx = np.gradient(A, cell); return gxx, gzz
    hx, hz = grad(H); lx, lz = grad(L)
    ok = np.isfinite(hx) & np.isfinite(hz)
    if ok.sum() < 10: return None
    def aspect(ax, az): return np.degrees(np.arctan2(-ax, -az)) % 360
    da = np.abs(aspect(hx[ok], hz[ok]) - aspect(lx[ok], lz[ok])); da = np.minimum(da, 360 - da)
    corr_x = float(np.corrcoef(hx[ok], lx[ok])[0, 1]); corr_z = float(np.corrcoef(hz[ok], lz[ok])[0, 1])
    res = {"median_aspect_diff_deg": round(float(np.median(da)), 1), "within_45_pct": round(100 * float((da <= 45).mean()), 1),
           "reversed_pct": round(100 * float((da > 135).mean()), 1), "slope_corr": round(min(corr_x, corr_z), 2), "n": int(ok.sum())}
    res["pass"] = (res["median_aspect_diff_deg"] <= FR["median_aspect_diff_max_deg"] and res["within_45_pct"] >= FR["within_45_min_pct"]
                   and res["reversed_pct"] <= FR["reversed_max_pct"] and res["slope_corr"] >= FR["slope_corr_min"])
    return res


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out"); ap.add_argument("--holes"); a = ap.parse_args()
    C = course(); R = rules(C); GR = R["greens"]; FR = GR["checks"]["fall_direction"]
    out = a.out or os.path.join(C.dir, "progress", "gate_report"); os.makedirs(out, exist_ok=True)
    prog = os.path.dirname(out)
    try: AO = C.path("automation_output")
    except KeyError: AO = ""
    G = GKD(C); holes = [int(h) for h in a.holes.split(",")] if a.holes else [h.number for h in G.holes if h.enabled]
    missing = []

    unity = read_csv(os.path.join(AO, "checks", "greens_all18.csv")) if AO else None
    if unity is None: missing.append("Unity CourseGreenChecks (checks/greens_all18.csv): run Course Checks > Greens on the PC")
    lidar = read_csv(os.path.join(prog, "green_report", "green_report.csv"))
    if lidar is None: missing.append("tools/green_report.py output (progress/green_report)")
    fg_p = os.path.join(prog, "flyover_gate", "flyover_gate.json"); fg = json.load(open(fg_p)) if os.path.exists(fg_p) else None
    if fg is None: missing.append("tools/flyover_gate.py output (progress/flyover_gate)")
    try:
        gk = subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__), "gkd_tools.py"), "check"], capture_output=True, text=True, cwd=C.dir)
        gkd_fail = [l for l in gk.stdout.splitlines() if l.startswith("FAIL")]
    except Exception as e: gkd_fail = ["could not run gkd_tools: %s" % e]
    HM = None
    try: HM = Heightmap(C)
    except Exception as e: missing.append("heightmap (%s): fall direction skipped" % e)

    U = {int(r["hole"]): r for r in unity} if unity else {}; Lr = {int(r["hole"]): r for r in lidar} if lidar else {}
    F = {int(h["hole"]): h for h in (fg or {}).get("holes", [])} if fg else {}
    rows = []
    for hn in holes:
        r = {"hole": hn, "source": [], "fails": []}
        u = U.get(hn)
        if u:
            r["source"].append("unity")
            r["step_cm"] = float(u["max_step_cm"]); r["lip_cm"] = float(u["max_lip_cm"]); r["col_dy_cm"] = float(u["collider_dy_cm"]); r["tiers"] = u["tiers_ok"] == "1"
            r["crown_m"] = float(u["crown_overhang_m"]); r["pins_fail"] = int(u["pins_failing"]); r["areas"] = int(u["valid_areas"]); r["usable"] = float(u["usable_pct"])
            if r["step_cm"] > GR["checks"]["edge_survey"]["max_step_m"] * 100: r["fails"].append("step")
            if r["lip_cm"] > GR["checks"]["edge_survey"]["max_lip_m"] * 100: r["fails"].append("lip")
            if r["col_dy_cm"] > GR["checks"]["collider_vs_render_max_dy_m"] * 100: r["fails"].append("collider")
            if not r["tiers"]: r["fails"].append("tiers")
            if r["crown_m"] >= GR["checks"]["crown_overhang"]["block_at_m"]: r["fails"].append("crown")
            if r["pins_fail"]: r["fails"].append("pins")
            if r["areas"] < GR["pins"]["min_valid_areas"]: r["fails"].append("areas")
            if HM and AO:
                fd = fall_direction(os.path.join(AO, "checks", "grid_heights_h%02d.csv" % hn), HM, FR)
                if fd: r["fall"] = fd; r["fall"]["pass"] or r["fails"].append("fall-direction")
        l = Lr.get(hn)
        if l and not u:
            r["source"].append("lidar-proxy"); r["pins_fail"] = int(l["pins_failing"] or 0); r["areas"] = int(l["valid_areas"] or 0); r["usable"] = float(l["usable_pct"] or 0); r["slope_mean"] = float(l["slope_mean_pct"] or 0)
            # the LiDAR proxy is advisory: the live greens carry scans and edits the 2018 LiDAR does not; it warns, it does not fail
            r["warn"] = []
            if r["pins_fail"]: r["warn"].append("pins(lidar)")
            if r["areas"] < GR["pins"]["min_valid_areas"]: r["warn"].append("areas(lidar)")
        f = F.get(hn)
        if f:
            r["source"].append("flyover"); r["flyover"] = "PASS" if f.get("ok") else "FAIL"
            if not f.get("ok"): r["fails"].append("flyover")
        r["pass"] = not r["fails"] and bool(r["source"]) and ("unity" in r["source"] or "flyover" in r["source"])
        rows.append(r)

    md = ["# Gate report: %s (%d holes)" % (C["name"], len(rows)), "",
          "Sources: " + ", ".join(sorted({s for r in rows for s in r["source"]})) + ". Missing: " + ("; ".join(missing) if missing else "none") + ".", "",
          "GKD checks: " + ("all pass" if not gkd_fail else "; ".join(gkd_fail)), "",
          "| hole | source | step cm | lip cm | col dy | tiers | crown m | pins fail | usable % | areas | fall dir | flyover | result |", "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in sorted(rows, key=lambda r: (r["pass"], r["hole"])):
        g = lambda k, f="%s": (f % r[k]) if k in r else "-"
        fd = r.get("fall"); fds = ("%.0f deg %s" % (fd["median_aspect_diff_deg"], "ok" if fd["pass"] else "FAIL")) if fd else "-"
        md.append("| %d | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (r["hole"], "+".join(r["source"]) or "none", g("step_cm", "%.1f"), g("lip_cm", "%.1f"), g("col_dy_cm", "%.1f"),
                  ("ok" if r.get("tiers") else "FAIL") if "tiers" in r else "-", g("crown_m", "%.2f"), g("pins_fail"), g("usable", "%.0f"), g("areas"), fds, g("flyover"), ("PASS" if r["pass"] else "**FAIL** " + ",".join(r["fails"])) + (" warn: " + ",".join(r["warn"]) if r.get("warn") else "")))
    open(os.path.join(out, "gate_report.md"), "w").write("\n".join(md) + "\n")
    json.dump({"holes": rows, "missing": missing, "gkd_fail": gkd_fail}, open(os.path.join(out, "gate_report.json"), "w"), indent=1)
    print("\n".join(md)); print("wrote", out)
    sys.exit(0 if all(r["pass"] for r in rows) and not gkd_fail else 1)


if __name__ == "__main__":
    main()
