"""Coverage per hole: what share of the site-visit capture checklist has an indexed capture behind it (docs/BLUEPRINT.md).

    python tools/coverage.py [--out progress/coverage] [--min 80]

Items per hole (from course-build-site-visit "Capture per hole", via rules/photos.json coverage.items): one per playing tee
colour in the GKD, tee_surrounds, landing_zone, one per bunker within 60 m of the hole line (SVG), green, collar, green_surrounds,
water (only if a water outline sits within 60 m of the hole), trees_near_play, props, plus course-level once items reported
separately. A capture covers an item when an index row is assigned to the hole with pose_conf high/medium (photos), a scan with
verdict ACCEPT (scans/index.csv), or a drone frame, and its spot matches. Rows with an empty spot get an automatic spot from
geometry: the nearest GKD tee / green centre / SVG bunker to the camera position within rules coverage.spot_radius_m, else
'fairway'. Writes coverage.csv and coverage.md; holes under --min are listed as the next visit list. Nothing is modified.
"""
import argparse, csv, json, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, GKD, svg_outlines, match_outlines_to_holes

PLAY = ("AimPoint1", "AimPoint2", "GreenCenterPoint")


def near_line(P, q):
    best = 1e9
    for a, b in zip(P[:-1], P[1:]):
        ab = b - a; L2 = float(ab @ ab); u = 0 if L2 == 0 else float(np.clip((q - a) @ ab / L2, 0, 1)); best = min(best, float(np.hypot(*(q - a - u * ab))))
    return best


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out"); ap.add_argument("--min", type=float); a = ap.parse_args()
    C = course(); R = rules(C)["photos"]; CV = R.get("coverage", {}); G = GKD(C)
    minpct = a.min if a.min is not None else CV.get("min_pct", 80); rad = CV.get("spot_radius_m", 25); feat_m = CV.get("feature_near_line_m", 60)
    root = C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, "course_photos")
    out = a.out or os.path.join(C.dir, "progress", "coverage"); os.makedirs(out, exist_ok=True)
    idx_p = os.path.join(root, "index.csv"); rows = list(csv.DictReader(open(idx_p, encoding="utf-8-sig"))) if os.path.exists(idx_p) else []
    sc_p = os.path.join(root, "scans", "index.csv"); scans = list(csv.DictReader(open(sc_p, encoding="utf-8-sig"))) if os.path.exists(sc_p) else []
    try:
        bunkers = svg_outlines(C, "bunker"); water = svg_outlines(C, "water")
    except Exception: bunkers, water = {}, {}
    greens = {}
    try: greens = match_outlines_to_holes(svg_outlines(C, "green"), G)
    except Exception: pass

    # items per hole
    items = {}; feats = {}
    for h in G.holes:
        if not h.enabled or h.green_centre is None: continue
        L = h.line(); it = []; f = []
        for k, t in h.playing_tees().items(): it.append("tee_%s" % k.lower()); f.append(("tee_%s" % k.lower(), np.array([t["Position"]["x"], t["Position"]["z"]])))
        it += ["tee_surrounds", "landing_zone"]
        nb = 0
        for bid, poly in bunkers.items():
            c = poly.mean(0)
            if near_line(L, c) <= feat_m and np.hypot(*(c - h.green_centre)) < 1.5 * np.hypot(*(L[0] - h.green_centre)): nb += 1; it.append("bunker_%d" % nb); f.append(("bunker_%d" % nb, c))
        it += ["green", "collar", "green_surrounds"]; f.append(("green", h.green_centre))
        if any(near_line(L, poly.mean(0)) <= feat_m for poly in water.values()): it.append("water")
        it += ["trees_near_play", "props"]
        items[h.number] = it; feats[h.number] = f

    MANUAL = ("trees_near_play", "props")   # only a hand tag in index.csv covers these; reported separately
    fov = CV.get("aim_half_angle_deg", 30); reach = CV.get("aim_reach_m", 60)

    def spots_for(hn, x, z, hd):
        """Spots a photo covers: the feature it stands on (within spot_radius_m) plus every feature it is aimed at
        (within aim_reach_m and aim_half_angle_deg of the compass heading)."""
        q = np.array([x, z]); out = set(); best = None
        for name, p in feats.get(hn, []):
            d = float(np.hypot(*(p - q)))
            if d <= rad and (best is None or d < best[1]): best = (name, d)
            if hd not in ("", None) and 0 < d <= reach:
                bearing = math.degrees(math.atan2(p[0] - q[0], p[1] - q[1])) % 360; da = abs((bearing - float(hd) + 180) % 360 - 180)
                if da <= fov: out.add(name)
        out.add(best[0] if best else "landing_zone")
        return out

    cover = {hn: {} for hn in items}
    for r in rows:
        try: hn = int(r.get("hole") or 0)
        except ValueError: continue
        if hn not in cover: continue
        src = r.get("source") or "iphone"; conf = r.get("pose_conf") or ""
        ok = (src.startswith("drone")) or conf in R["pose"]["verdict_confidence"] or (conf == "draft" and (r.get("gps_confidence") in ("high", "medium")))
        if not ok: continue
        spot = (r.get("spot") or "").strip().lower()
        spots = {spot} if spot else (spots_for(hn, float(r["x"]), float(r["z"]), r.get("heading")) if r.get("x") not in ("", None) else set())
        norm = set()
        for sp in spots:
            if sp in ("fairway", "mid"): sp = "landing_zone"
            if sp == "tee": sp = "tee_surrounds"
            if sp.startswith("green") and sp != "green_surrounds": sp = "green"
            norm.add(sp)
            if sp.startswith("tee_"): norm.add("tee_surrounds")
            if sp == "green": norm.update(("collar", "green_surrounds"))
        for sp in norm: cover[hn].setdefault(sp, []).append(r["img"])
    for s in scans:
        try: hn = int(s.get("hole") or 0)
        except ValueError: continue
        if hn in cover and s.get("verdict") == "ACCEPT":
            k = "green" if s.get("kind") == "green" else "bunker_scan"; cover[hn].setdefault(k, []).append(s["file"])
            if k == "green": cover[hn].setdefault("collar", []).append(s["file"])
            if s.get("kind") == "bunker":
                for it in items[hn]:
                    if it.startswith("bunker_"): cover[hn].setdefault(it, []).append(s["file"])

    res = []
    for hn, it in sorted(items.items()):
        auto = [i for i in it if i not in MANUAL]; got = [i for i in auto if cover[hn].get(i)]; miss = [i for i in auto if not cover[hn].get(i)]
        man_miss = [i for i in it if i in MANUAL and not cover[hn].get(i)]
        res.append({"hole": hn, "items": len(auto), "covered": len(got), "coverage_pct": round(100.0 * len(got) / max(len(auto), 1), 0), "missing": " ".join(miss), "manual_untagged": " ".join(man_miss), "captures": len({im for v in cover[hn].values() for im in v})})
    with open(os.path.join(out, "coverage.csv"), "w", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=["hole", "items", "covered", "coverage_pct", "captures", "missing", "manual_untagged"]); w.writeheader(); [w.writerow(r) for r in res]
    low = [r for r in res if r["coverage_pct"] < minpct]
    md = ["# Coverage per hole (%s)" % C["name"], "", "Item covered = an indexed photo (pose high/medium, or draft with good GPS) standing on or aimed at the spot, an ACCEPTed scan, or a drone frame. Line: %.0f%%. trees_near_play and props need a hand tag in index.csv (column spot) and are listed, not scored." % minpct, "",
          "| hole | items | covered | % | photos | missing | untagged (manual) |", "|---|---|---|---|---|---|---|"]
    md += ["| %d | %d | %d | %.0f | %d | %s | %s |" % (r["hole"], r["items"], r["covered"], r["coverage_pct"], r["captures"], r["missing"], r["manual_untagged"]) for r in res]
    md += ["", "Next visit list (under %.0f%%): %s" % (minpct, ", ".join(str(r["hole"]) for r in low) or "none"), ""]
    open(os.path.join(out, "coverage.md"), "w").write("\n".join(md)); print("\n".join(md)); print("wrote", out)


if __name__ == "__main__":
    main()
