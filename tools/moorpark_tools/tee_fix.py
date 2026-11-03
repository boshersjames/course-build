r"""Tees off their pad (GSPro course GKDs): after the outline smoothing, every tee that isn't on a tee mesh is either moved
onto the nearest tee pad (within 15 m, 1.5 m in from its edge) or, if no pad is that close and it sits on rough or bare
ground, gets a new pad of its own (tools/outline_smooth.py reads tee_pads_extra.json, so rebuild the meshes after).
Forward tees on fairway stay where they are. The same tee in every layout gets the same spot.

    python tools/tee_fix.py            # report only
    python tools/tee_fix.py --write    # also write the six GKDs in C:\GSProV1\Core\GSP\Courses\Moorpark_* (back them up first),
                                         build/gspro/<folder>/ and greenkeeper/<folder>/
"""
import glob, json, os, re, sys
import numpy as np
import shapely
import shapely.ops
from shapely.geometry import Polygon, Point

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
S = 3000.0
GSPRO = r"C:\GSProV1\Core\GSP\Courses"
REAL = ("Black", "Blue", "White", "Red", "Yellow", "Green")
NEAR = 15.0
INSIDE = 1.5


def layer_polys(svg, layer):
    i0 = svg.find('id="layer_%s"' % layer); blk = svg[i0:svg.find("</g>", i0)]
    out = []
    for d in re.findall(r' d="([^"]+)"', blk):
        loops = []
        for sub in re.split(r"[Zz]", d):
            nums = [float(v) for v in re.findall(r"-?\d+\.?\d*", sub)]
            if len(nums) >= 6:
                loops.append(list(zip(nums[0::2], nums[1::2])))
        if loops:
            p = shapely.make_valid(Polygon(loops[0], loops[1:]))
            out.append(p)
    return shapely.union_all(out) if out else Polygon()


def height(x, z):
    c = json.load(open(os.path.join(REPO, "course.json")))
    raw = np.fromfile(os.path.join(REPO, "heightmap_2049.raw"), "<u2").reshape(2049, 2049).astype(np.float64) / 65535 * c["terrain_height"]
    fx, fz = x / S * 2048, z / S * 2048
    i, j = int(fz), int(fx); a, b = fz - i, fx - j
    return (raw[i, j] * (1 - a) * (1 - b) + raw[i, j + 1] * (1 - a) * b + raw[i + 1, j] * a * (1 - b) + raw[i + 1, j + 1] * a * b)


def main(write):
    svg = open(os.path.join(REPO, "course_smooth.svg"), encoding="utf-8").read()
    tees = layer_polys(svg, "tee"); fair = layer_polys(svg, "fairway"); green = layer_polys(svg, "green")
    tee_list = list(getattr(tees, "geoms", [tees]))
    files = sorted(glob.glob(os.path.join(GSPRO, "Moorpark_*", "Moorpark_*.GKD")))
    decided = {}                                          # (x, z) rounded -> (action, new x, new z)
    extra = []
    log = []
    gkds = {}
    for f in files:
        g = json.load(open(f, encoding="utf-8")); gkds[f] = g
        for h in g["Holes"]:
            pin = h["Pins"][0]["Position"] if h.get("Pins") else None
            for t in h["Tees"]:
                if not t.get("Enabled") or t.get("TeeType") not in REAL or not t.get("Position"):
                    continue
                x, z = t["Position"]["x"], t["Position"]["z"]
                key = (round(x, 2), round(z, 2))
                if key not in decided:
                    p = Point(x, S - z)
                    on = [q for q in tee_list if q.buffer(-0.3).contains(p)]
                    if on:
                        decided[key] = ("ok", x, z)
                    else:
                        near = min(tee_list, key=lambda q: q.distance(p))
                        d = near.distance(p)
                        if d <= NEAR:
                            core = near.buffer(-INSIDE)
                            if core.is_empty: core = near.buffer(-0.5)
                            if core.is_empty: core = near
                            q = shapely.ops.nearest_points(core, p)[0]
                            decided[key] = ("moved %.1f m onto a tee pad" % p.distance(q), q.x, S - q.y)
                        elif fair.contains(p) or green.contains(p):
                            decided[key] = ("left on fairway (nearest pad %.0f m)" % d, x, z)
                        else:
                            ang = 0.0
                            if pin:
                                ang = float(np.degrees(np.arctan2(pin["z"] - z, pin["x"] - x)))
                            extra.append(dict(x=x, z=z, heading=ang))
                            decided[key] = ("new pad (nearest pad %.0f m)" % d, x, z)
                act, nx, nz = decided[key]
                if act != "ok":
                    log.append("%s  hole %d %s: %s" % (os.path.basename(f), h["HoleNumber"], t["TeeType"], act))
                if act.startswith("moved"):
                    t["Position"]["x"] = round(nx, 3); t["Position"]["z"] = round(nz, 3)
                    t["Position"]["y"] = round(height(nx, nz), 3)
    seen = set()
    for l in log:
        print(l)
    json.dump(extra, open(os.path.join(REPO, "tee_pads_extra.json"), "w"), indent=1)
    print(len(extra), "new pads -> tee_pads_extra.json")
    if write:
        for f, g in gkds.items():
            txt = json.dumps(g, separators=(",", ":"), ensure_ascii=False)
            folder = os.path.basename(os.path.dirname(f))
            for dst in (f, os.path.join(REPO, "build", "gspro", folder, folder + ".GKD"), os.path.join(REPO, "greenkeeper", folder, folder + ".GKD")):
                if os.path.isdir(os.path.dirname(dst)):
                    open(dst, "w", encoding="utf-8").write(txt)
        print("wrote", len(gkds), "GKDs")


if __name__ == "__main__":
    main("--write" in sys.argv)
