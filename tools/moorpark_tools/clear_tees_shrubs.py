"""Take shrubs off and around every tee pad and out of the corridor behind (and just in front of) every tee, all 27 holes.
The shrubs are combined into tile meshes in Unity, so this filters shrubs_clean.csv; replant with Plant shrubs.
Same zones as unity_editor/MoorparkClearTees.cs (which removes the trees): within 4 m of a pad (+ the shrub's crown),
25 m behind the tee in a corridor 6 m each side widening 0.15 m per metre, 12 m ahead and 4 m each side.
    python tools/clear_tees_shrubs.py      # rewrites shrubs_clean.csv, adds the removed ones to shrubs_removed.csv
"""
import csv, glob, json, os, sys
import numpy as np
import shapely
from shapely.geometry import Point
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tee_fix import layer_polys, GSPRO, S

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PAD, BEHIND, HALF, AHEAD, AHEAD_HALF = 4.0, 25.0, 6.0, 12.0, 4.0


def tees():
    out, seen = [], set()
    for f in glob.glob(os.path.join(GSPRO, "Moorpark_*", "Moorpark_*.GKD")):
        g = json.load(open(f, encoding="utf-8"))
        for h in g["Holes"]:
            if not h.get("Pins") or not h["Pins"][0].get("Position"):
                continue
            to = np.array([h["Pins"][0]["Position"]["x"], h["Pins"][0]["Position"]["z"]])
            for a in h["Tees"]:
                if a.get("TeeType") == "AimPoint1" and a.get("Enabled") and a.get("Position"):
                    to = np.array([a["Position"]["x"], a["Position"]["z"]])
            for t in h["Tees"]:
                if not t.get("Enabled") or not t.get("Position") or t["TeeType"].startswith("AimPoint"):
                    continue
                p = np.array([t["Position"]["x"], t["Position"]["z"]])
                k = (round(p[0], 1), round(p[1], 1))
                if k in seen: continue
                seen.add(k); d = to - p; out.append((p, d / np.linalg.norm(d)))
    return out


def main():
    svg = open(os.path.join(REPO, "course_smooth.svg"), encoding="utf-8").read()
    pads = layer_polys(svg, "tee")
    T = tees()
    rows = list(csv.DictReader(open(os.path.join(REPO, "shrubs_clean.csv"))))
    keep, gone = [], []
    P = np.array([[float(r["x"]), float(r["z"])] for r in rows]); cr = np.array([float(r["crown_radius_m"]) for r in rows])
    near_pad = shapely.dwithin(pads, shapely.points(P[:, 0], S - P[:, 1]), PAD + cr)
    corridor = np.zeros(len(rows), bool)
    for p, d in T:
        r = P - p; along = r @ d; across = np.abs(r[:, 0] * d[1] - r[:, 1] * d[0])
        half = np.where(along < 0, HALF + 0.15 * -along, AHEAD_HALF)
        corridor |= (along >= -BEHIND - cr) & (along <= AHEAD + cr) & (across <= half + cr)
    for r, a, b in zip(rows, near_pad, corridor):
        if a or b: gone.append(dict(id=r["id"], x=r["x"], z=r["z"], reason="tee pad" if a else "behind/in front of tee"))
        else: keep.append(r)
    with open(os.path.join(REPO, "shrubs_clean.csv"), "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(keep)
    with open(os.path.join(REPO, "shrubs_removed.csv"), "a", newline="") as f:
        csv.DictWriter(f, fieldnames=["id", "x", "z", "reason"]).writerows(gone)
    print(len(T), "tee spots;", len(gone), "shrubs removed,", len(keep), "kept")


if __name__ == "__main__":
    main()
