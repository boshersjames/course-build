"""GKD checks and text-preserving fixes, all read from course.json and rules.

    python tools/gkd_tools.py check                 # limits, stimp, firmness, designer, tee slots, pins per hole, distances off card
    python tools/gkd_tools.py distances [--tol 2] [--write]   # recompute tee Distance (tee->aims->green, 2D m); rewrite only those off by > tol
    python tools/gkd_tools.py totals [--write]      # TeeTypeTotalDistance per enabled tee type
    python tools/gkd_tools.py pins                  # pins vs LiDAR: lie angle (GreenKeeper method) and height offset

Writes edit the GKD text in place (numbers only, BOM and formatting kept) and back it up first to <gkd>.bak_<timestamp>.
"""
import argparse, json, math, os, re, shutil, sys, time
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, GKD, Heightmap

PLAY = ("AimPoint1", "AimPoint2", "GreenCenterPoint")


def backup(path):
    b = path + ".bak_" + time.strftime("%Y%m%d_%H%M%S"); shutil.copy2(path, b); return b


def cmd_check(C, G):
    R = rules(C); out = []
    L = G.limits(); out.append(("hazards <= %d" % L["hazards_max"], L["hazards"], L["hazards_ok"]))
    out.append(("pOOB points <= %d" % L["pOOB_max"], L["pOOB_points"], L["pOOB_ok"]))
    out.append(("hazardCount field = len(Hazards)", L["hazardCount_field"], L["hazardCount_field"] == L["hazards"]))
    d = G.data; g = R["greens"]["gkd"]
    out.append(("DefaultStimp = %d" % g["DefaultStimp"], d.get("DefaultStimp"), d.get("DefaultStimp") == g["DefaultStimp"]))
    out.append(("MaxStimp = %d" % g["MaxStimp"], d.get("MaxStimp"), d.get("MaxStimp") == g["MaxStimp"]))
    out.append(("FairwayFirmnessDefault = %d" % R["rough"]["gkd"]["FairwayFirmnessDefault"], d.get("FairwayFirmnessDefault"), d.get("FairwayFirmnessDefault") == R["rough"]["gkd"]["FairwayFirmnessDefault"]))
    out.append(("Designer = %s" % R["ob"]["gkd"]["Designer"], d.get("Designer"), d.get("Designer") == R["ob"]["gkd"]["Designer"]))
    for hz in d.get("Hazards", []):
        if hz.get("hasDZ") and hz.get("innerOOB"):
            out.append(("DZ hazard with innerOOB (re-hit only)", "hazard %d pts" % hz.get("pointCount", 0), False))
    P = R["greens"]["pins"]
    for h in G.holes:
        if not h.enabled: continue
        n = len(h.pins)
        out.append(("hole %d pins >= %d" % (h.number, P["min_valid_areas"]), n, n >= P["min_valid_areas"]))
        for k, t in h.tees.items():
            if k in PLAY: continue
            if t.get("Enabled") and not t.get("Position"): out.append(("hole %d tee %s enabled without position" % (h.number, k), "", False))
    for name, val, ok in out:
        if not ok or "--all" in sys.argv: print("%-5s %-48s %s" % ("PASS" if ok else "FAIL", name, val))
    bad = [o for o in out if not o[2]]
    print("%d checks, %d failing" % (len(out), len(bad)))
    return 1 if bad else 0


def wanted_distances(G):
    flat = []
    for h in G.holes:
        gc = h.green_centre; aims = h.aims
        for k, t in h.tees.items():
            want = None
            if t.get("Enabled") and t.get("Position") and gc is not None and k not in PLAY:
                pts = [np.array([t["Position"]["x"], t["Position"]["z"]])] + aims + [gc]
                want = sum(float(np.hypot(*(pts[i + 1] - pts[i]))) for i in range(len(pts) - 1))
            flat.append((h.number, k, t.get("Distance"), want))
    return flat


def cmd_distances(C, G, tol, write):
    flat = wanted_distances(G); txt = G.text
    pat = re.compile(r'\{[^{}]*"TeeType"\s*:\s*"([^"]+)"[^{}]*(?:\{[^{}]*\}[^{}]*)?\}')
    ms = list(pat.finditer(txt)); pairs = []; k = 0
    for hole, tt, old, want in flat:
        while k < len(ms):
            b = ms[k]; k += 1
            try: o = json.loads(b.group(0))
            except Exception: continue
            if o.get("TeeType") == tt and o.get("Distance") == old: pairs.append((b, (hole, tt, old, want))); break
        else: sys.exit("could not align hole %d %s" % (hole, tt))
    out = []; pos = 0; changes = []
    for m, (hole, tt, old, want) in pairs:
        blk = m.group(0)
        if want is not None and old is not None and abs(old - want) > tol:
            blk = re.sub(r'("Distance"\s*:\s*)-?[\d.eE+-]+', lambda q: q.group(1) + ("%.5f" % want), blk, count=1); changes.append((hole, tt, old, want))
        out.append(txt[pos:m.start()]); out.append(blk); pos = m.end()
    out.append(txt[pos:]); new = "".join(out)
    for hole, tt, old, want in changes: print("hole %2d %-7s %9.2f -> %9.2f (%+.1f m, %+.0f yd)" % (hole, tt, old, want, want - old, (want - old) * 1.09361))
    print("%d tees off by > %.1f m" % (len(changes), tol))
    if write and changes: print("backup", backup(G.path)); G.write_text(new); print("wrote", G.path)
    return new


def cmd_totals(C, G, write):
    t = G.text; sums = {}
    for h in G.holes:
        for k, tt in h.tees.items():
            if tt.get("Enabled") and tt.get("Position") and k not in PLAY: sums[k] = sums.get(k, 0) + tt["Distance"]
    i0 = t.index('"TeeTypeTotalDistance"'); j0 = t.index("]", i0); seg = t[i0:j0]; out = seg
    for e in G.data["TeeTypeTotalDistance"]:
        name = e.get("TeeType") or e.get("teeType") or e.get("Type")
        if name not in sums: continue
        pat = re.compile(r'(\{[^{}]*"[Tt]ee[Tt]ype"\s*:\s*"%s"[^{}]*"Distance"\s*:\s*)(-?[\d.eE+-]+)' % re.escape(name)); m = pat.search(out)
        if not m: pat = re.compile(r'(\{[^{}]*"Distance"\s*:\s*)(-?[\d.eE+-]+)([^{}]*"[Tt]ee[Tt]ype"\s*:\s*"%s")' % re.escape(name)); m = pat.search(out)
        if not m: sys.exit("no TeeTypeTotalDistance entry for " + name)
        out = out[:m.start(2)] + ("%.5f" % sums[name]) + out[m.end(2):]
        print("%-7s %9.2f -> %9.2f m (%.0f yd)" % (name, e["Distance"], sums[name], sums[name] * 1.09361))
    new = t[:i0] + out + t[j0:]
    if write: print("backup", backup(G.path)); G.write_text(new); print("wrote", G.path)


def cmd_pins(C, G):
    from green_report import pin_area_angle
    HM = Heightmap(C); P = rules(C)["greens"]["pins"]; tol = rules(C)["terrain"]["checks"]["pin_height_tol_m"]
    print("hole day  angle  dy_vs_lidar  verdict")
    for h in G.holes:
        for p in h.pins:
            x, z, y = p["Position"]["x"], p["Position"]["z"], p["Position"]["y"]
            ang = pin_area_angle(lambda a, b: HM.sample(a, b, 3), x, z, P); dy = y - HM.sample(x, z, 3)
            v = ("" if ang <= P["max_lie_angle_deg"] else "STEEP ") + ("" if abs(dy) <= tol else "HEIGHT")
            print("%4d %-4s %5.1f  %+6.2f       %s" % (h.number, p.get("Day", "")[:3], ang, dy, v or "ok"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["check", "distances", "totals", "pins"]); ap.add_argument("--tol", type=float, default=2.0)
    ap.add_argument("--write", action="store_true"); ap.add_argument("--all", action="store_true"); ap.add_argument("--gkd")
    a = ap.parse_args(); C = course(); G = GKD(C, a.gkd)
    if a.cmd == "check": sys.exit(cmd_check(C, G))
    if a.cmd == "distances": cmd_distances(C, G, a.tol, a.write)
    if a.cmd == "totals": cmd_totals(C, G, a.write)
    if a.cmd == "pins": cmd_pins(C, G)


if __name__ == "__main__":
    main()
