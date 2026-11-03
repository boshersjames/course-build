"""GSPro course files for every Moorpark layout: the three 18-hole pairings and each nine on its own.
Writes build/gspro/<Folder>/<Folder>.GKD, coursedetails.txt and <Folder>.jpg (a drone frame) for each course;
the .gspcrse comes from GSP > Build Course on a scene called <Folder> (unity_editor/MoorparkBuild.cs).

Holes are named by nine everywhere: CourseName is "Moorpark CC - Ridgeline / Creekside", which the Unity tools
read as Ridgeline 1-9 and Creekside 1-9 (GSPro itself still numbers an 18-hole round 1-18).
    python tools/gspro_courses.py
"""
import copy, json, os
from PIL import Image

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(REPO, "build", "gspro")
NINE_FOLDER = {"Ridgeline": "H01", "Creekside": "H10", "Canyon Crest": "H19"}   # drone frame for the splash
COURSES = [  # folder, nines, source GKD (its front nine is the first name)
    ("Moorpark_RidgelineCreekside", ["Ridgeline", "Creekside"], "Moorpark_RidgelineCreekside.GKD"),
    ("Moorpark_CreeksideCanyonCrest", ["Creekside", "Canyon Crest"], "Moorpark_CreeksideCanyonCrest.GKD"),
    ("Moorpark_CanyonCrestRidgeline", ["Canyon Crest", "Ridgeline"], "Moorpark_CanyonCrestRidgeline.GKD"),
    ("Moorpark_Ridgeline", ["Ridgeline"], "Moorpark_RidgelineCreekside.GKD"),
    ("Moorpark_Creekside", ["Creekside"], "Moorpark_CreeksideCanyonCrest.GKD"),
    ("Moorpark_CanyonCrest", ["Canyon Crest"], "Moorpark_CanyonCrestRidgeline.GKD"),
]
# men's course rating / slope from SCGA's course handicap chart PDFs (tools/scga_ratings.py; scga.org .../jobs/18753/moorpark_-_<pairing>.pdf), by GKD tee slot (Gold plays from the Yellow slot;
# Blue/White has no slot in GKD 4.1). The GKD has no women's fields.
RATINGS = {
    "Moorpark_RidgelineCreekside": {"Black": "73.7/137", "Blue": "71.5/132", "White": "69.7/126", "Red": "66.8/119", "Yellow": "63.5/109"},
    "Moorpark_CreeksideCanyonCrest": {"Black": "73.0/134", "Blue": "71.0/130", "White": "69.4/125", "Red": "66.7/118", "Yellow": "64.4/110"},
    "Moorpark_CanyonCrestRidgeline": {"Black": "73.1/133", "Blue": "71.1/128", "White": "69.1/124", "Red": "66.7/118", "Yellow": "63.9/110"},
}
import csv as _csv
GREENS = [(float(r["green_x"]), float(r["green_z"]), float(r["green_y"])) for r in _csv.DictReader(open(os.path.join(REPO, "holes.csv")))]
TEES = ["Black", "White", "Green", "Blue", "Yellow", "Red", "Junior", "Par3"]


import numpy as _np
_raw = _np.fromfile(os.path.join(REPO, "heightmap_2049.raw"), "<u2").reshape(2049, 2049)
_cfg = json.load(open(os.path.join(REPO, "course.json")))


def ground(x, z):
    i = int(round(min(max(x, 0), 3000) / 3000 * 2048)); j = int(round(min(max(z, 0), 3000) / 3000 * 2048))
    return float(_raw[j, i]) / 65535 * _cfg["terrain_height"]


WASH_SKIP = {7}            # water/washes.json #7 ("Ridgeline 9") is a road edge, not a wash
WASH_LOG = []
BRUSH_LOG = []
_lab = None
def surface_code(x, z):
    """course.svg class at a point: 0 none, 1 rough, 2 deep, 3 fairway, 4 tee, 5 green, 6 bunker, 7 water"""
    global _lab
    if _lab is None:
        import sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import svg_fix
        cwd = os.getcwd(); os.chdir(REPO); _lab = svg_fix.load_layers("course.svg"); os.chdir(cwd)
    i, j = int(x / 0.5), int((3000 - z) / 0.5)
    return int(_lab[j, i]) if 0 <= i < _lab.shape[1] and 0 <= j < _lab.shape[0] else 0


def water_polys():
    """the course.svg Water layer as (x, z) rings in course metres"""
    import re
    svg = open(os.path.join(REPO, "course.svg"), encoding="utf-8").read()
    i0 = svg.find('id="layer_water"'); blk = svg[i0:svg.find("</g>", i0)]
    out = []
    for d in re.findall(r' d="([^"]+)"', blk):
        for sub in re.split(r"[Zz]", d):
            n = [float(v) for v in re.findall(r"-?\d+\.?\d*", sub)]
            pts = [(n[k], 3000 - n[k + 1]) for k in range(0, len(n) - 1, 2)]
            if len(pts) > 2: out.append(pts)
    return out


_brush_cache = None
def brush_ob(g):
    """Deep brush inside the boundary is OB too (BoJa, Oct 27: a ball in the scrub is lost). The draft only made brush
    islands of 400 m2+ interior OB, leaving ~12 ha of scrub in play (the 10 m natural band beside play and small islands).
    Every scrub patch of 60 m2+ inside the boundary becomes interior OB, kept 2 m clear of turf (rough, fairway, tee,
    green), bunkers, water and cart paths, and never over a tee, aim point or pin."""
    global _brush_cache
    from PIL import Image as _I, ImageDraw as _D
    from scipy import ndimage
    from skimage import measure
    if _brush_cache is None:
        br = _np.asarray(_I.open(os.path.join(REPO, "terrain_masks", "brush_sage_scrub.mask")).convert("L")) > 128   # 1 m, row 0 = north
        surface_code(0, 0)
        lab = _np.asarray(_I.fromarray(_lab).resize((3000, 3000), _I.NEAREST))
        play = ndimage.binary_dilation(lab > 0, iterations=2)
        paths = _I.new("L", (3000, 3000), 0); d = _D.Draw(paths); seg = {}
        for r in _csv.DictReader(open(os.path.join(REPO, "cartpaths.csv"))): seg.setdefault(r["segment"], []).append((float(r["x"]), 3000 - float(r["z"])))
        for p in seg.values(): d.line(p, fill=255, width=7)
        _brush_cache = ndimage.binary_opening(br & ~play & ~(_np.asarray(paths) > 0), iterations=1)
    m = _brush_cache.copy()
    inb = _I.new("L", (3000, 3000), 0); _D.Draw(inb).polygon([(c["x"], 3000 - c["z"]) for c in g["pOOB"]["coords"]], fill=255)
    m &= _np.asarray(inb) > 0
    for h in g["Holes"]:                                    # keep 6 m around every tee, aim point and pin in bounds
        for p in [x["Position"] for x in h["Tees"] if x["Enabled"] and x.get("Position")] + [x["Position"] for x in h["Pins"] if x.get("Position")]:
            i, j = int(p["x"]), int(3000 - p["z"])
            m[max(j - 6, 0):j + 7, max(i - 6, 0):i + 7] = False
    comp, n = ndimage.label(m)
    out = []
    for k, sl in enumerate(ndimage.find_objects(comp), start=1):
        piece = comp[sl] == k
        if piece.sum() < 60: continue
        for cont in measure.find_contours(_np.pad(piece, 1).astype(float), 0.5):
            cont = measure.approximate_polygon(cont, 0.8)
            if len(cont) < 4: continue
            coords = [{"x": round(c[1] - 1 + sl[1].start + 0.5, 2), "z": round(3000 - (c[0] - 1 + sl[0].start + 0.5), 2)} for c in cont[:-1]]
            for c in coords: c["y"] = round(ground(c["x"], c["z"]), 3)
            out.append({"pointCount": len(coords), "coords": [{"x": c["x"], "y": c["y"], "z": c["z"]} for c in coords],
                        "DZpos": {"x": 0.0, "y": 0.0, "z": 0.0}, "freeDrop": False, "innerOOB": True, "noAIL": False, "hasDZ": False, "islandGreen": False})
            break                                           # outer ring only
    BRUSH_LOG.append((g["SceneFolderName"], len(out), int(sum(1 for _ in out) and m.sum())))
    return out


MAX_HAZARDS = 120   # ... and at most 128 hazards (aHazLinesGO), with some room
MAX_POINTS = 900   # Greenkeeper 4.1 copies pOOB / hazard outlines into a 1024-point buffer (MainLogic.LoadGKD): more = stuck at 90 %


def cap_poob(g):
    """simplify the perimeter OB line under MAX_POINTS, growing the tolerance; never let a tee, aim point or pin fall outside"""
    from shapely.geometry import Polygon, Point
    ring = [(c["x"], c["z"]) for c in g["pOOB"]["coords"]]
    if len(ring) <= MAX_POINTS: return
    P = Polygon(ring).buffer(0)
    if P.geom_type != "Polygon": P = max(P.geoms, key=lambda q: q.area)
    pts = []
    for h in g["Holes"]:
        pts += [x["Position"] for x in h["Tees"] if x["Enabled"] and x.get("Position")] + [x["Position"] for x in h["Pins"] if x.get("Position")]
    tol = 0.3
    while True:
        Q = P.simplify(tol, preserve_topology=True)
        if Q.geom_type != "Polygon": Q = max(Q.geoms, key=lambda q: q.area)
        n = len(Q.exterior.coords) - 1
        if n <= MAX_POINTS and all(Q.buffer(-1.0).contains(Point(p["x"], p["z"])) for p in pts if P.buffer(-1.0).contains(Point(p["x"], p["z"]))):
            break
        tol *= 1.25
        if tol > 6: break
    coords = [{"x": round(x, 3), "y": round(ground(x, z), 3), "z": round(z, 3)} for x, z in list(Q.exterior.coords)[:-1]]
    g["pOOB"] = {"pointCount": len(coords), "coords": coords}
    print("   pOOB %d -> %d points (tolerance %.2f m)" % (len(ring), len(coords), tol))


def INB(g):
    """the in-bounds area (inside the perimeter OB line), shrunk 2 m so drop zones aren't on the line"""
    from shapely.geometry import Polygon
    return Polygon([(c["x"], c["z"]) for c in g["pOOB"]["coords"]]).buffer(0).buffer(-2.0)


def protect(g, hz):
    """GKD hazards have no holes, so a brush patch that wraps a tee or pin is cut open: the point plus 6 m, and a
    3 m channel from it to the patch edge, are taken out of the patch"""
    from shapely.geometry import Polygon, Point, LineString
    from shapely.ops import nearest_points
    pts = []
    for h in g["Holes"]:
        pts += [x["Position"] for x in h["Tees"] if x["Enabled"] and x.get("Position")] + [x["Position"] for x in h["Pins"] if x.get("Position")]
    out = []
    for z in hz:
        P = Polygon([(c["x"], c["z"]) for c in z["coords"]]).buffer(0)
        for p in pts:
            pt = Point(p["x"], p["z"])
            if P.buffer(6).contains(pt):
                edge = nearest_points(P.exterior, pt)[0]
                far = Point(edge.x + (edge.x - pt.x) * 0.5, edge.y + (edge.y - pt.y) * 0.5)
                P = P.difference(pt.buffer(6)).difference(LineString([pt, far]).buffer(1.5))
        for q in getattr(P, "geoms", [P]):
            if q.is_empty or q.area < 60: continue
            ring = list(q.exterior.simplify(0.4).coords)[:-1]
            coords = [{"x": round(x, 2), "y": round(ground(x, zz), 3), "z": round(zz, 2)} for x, zz in ring]
            out.append(dict(z, pointCount=len(coords), coords=coords))
    return out


def water_hazards(g):
    """Water hazards from course.svg (the lake, the pond, the Ridgeline 9 stream) replace the drafted OSM ones, each with a
    drop zone: on dry ground 8 m outside the water, on the side facing the tee of the nearest hole (GSPro: hasDZ)."""
    from shapely.geometry import Polygon, Point, LineString
    keep = [h for h in g["Hazards"] if h.get("innerOOB")]
    tees = []
    for h in g["Holes"]:
        t = next((x["Position"] for x in h["Tees"] if x["Enabled"] and x.get("Position") and x["TeeType"] in ("Blue", "White", "Black")), None)
        if t and h["Pins"]: tees.append(((t["x"], t["z"]), (h["Pins"][0]["Position"]["x"], h["Pins"][0]["Position"]["z"])))
    for ring in water_polys():
        P = Polygon(ring).buffer(0)
        if P.area < 50: continue
        # nearest hole line, its tee side
        best = min(tees, key=lambda tp: LineString(tp).distance(P))
        if LineString(best).distance(P) > 60: continue      # not in play for this layout
        tee = Point(best[0])
        edge = P.exterior.interpolate(P.exterior.project(tee))
        dx, dz = tee.x - edge.x, tee.y - edge.y; L = max((dx * dx + dz * dz) ** 0.5, 1e-6)
        # 8 m out, further if that is still in (or within 6 m of) any water, or off the course surfaces
        allw = [Polygon(r).buffer(6.0) for r in water_polys()]   # the water planes reach 2 m past the outline
        dz_pt = None
        for s in range(8, 60, 2):
            q = (edge.x + dx / L * s, edge.y + dz / L * s)
            if any(w.contains(Point(q)) for w in allw) or not INB(g).contains(Point(q)): continue
            if surface_code(*q) in (0, 6, 7): continue          # off the course, in a bunker or water
            dz_pt = q; break
        if dz_pt is None:      # that line crosses other water (the stream runs into the lake): nearest dry course point around it
            import math
            best_d = None
            for s in range(8, 40, 2):
                for a in range(0, 360, 10):
                    for v in P.exterior.coords[::4]:
                        q = (v[0] + s * math.cos(math.radians(a)), v[1] + s * math.sin(math.radians(a)))
                        if any(w.contains(Point(q)) for w in allw) or surface_code(*q) in (0, 6, 7) or not INB(g).contains(Point(q)): continue
                        dd = Point(q).distance(tee)
                        if best_d is None or dd < best_d: best_d, dz_pt = dd, q
                if dz_pt: break
        if dz_pt is None: continue
        simp = P.exterior.simplify(0.5).coords[:-1]
        coords = [{"x": round(x, 3), "y": round(ground(x, z), 3), "z": round(z, 3)} for x, z in simp]
        keep.append({"pointCount": len(coords), "coords": coords, "DZpos": {"x": round(dz_pt[0], 3), "y": round(ground(*dz_pt), 3), "z": round(dz_pt[1], 3)},
                     "freeDrop": False, "innerOOB": False, "noAIL": False, "hasDZ": True, "islandGreen": False})
    # seasonal dry creeks (the sandy washes) are interior OB (BoJa, Oct 27), like Tierra's brush and ravine OB; a drop
    # zone where one crosses the line of play, on the tee side (Tierra hole 3 ravine / hole 6 canyon)
    for k, w in enumerate(json.load(open(os.path.join(REPO, "water", "washes.json")))):
        if k in WASH_SKIP: continue
        ring = [(c["x"], c["z"]) for c in w["hazard"]["coords"]]
        W = Polygon(ring).buffer(0)
        hz = dict(w["hazard"]); hz["innerOOB"] = True; hz["hasDZ"] = False
        crossing = [tp for tp in tees if LineString(tp).intersects(W.buffer(2.0))]
        if crossing:
            t, pin = crossing[0]
            line = LineString([t, pin])
            cut = line.project(line.intersection(W.buffer(2.0)).representative_point())
            for back in range(8, 60, 2):
                q = line.interpolate(max(cut - back, 0.0)); q = (q.x, q.y)
                if W.buffer(3.0).contains(Point(q)) or surface_code(*q) in (0, 6, 7) or not INB(g).contains(Point(q)): continue
                hz["hasDZ"] = True; hz["DZpos"] = {"x": round(q[0], 3), "y": round(ground(*q), 3), "z": round(q[1], 3)}
                break
        keep.append(hz)
        WASH_LOG.append((w["near"], hz["hasDZ"]))
    keep += protect(g, brush_ob(g))
    # Greenkeeper 4.1 holds at most 128 hazards (aHazLinesGO / aHazPointCount in MainLogic): drop interior OB away from
    # this layout's holes, then the smallest brush patches, to stay at MAX_HAZARDS
    from shapely.geometry import Polygon as _Pg, LineString as _Ls
    lines = [_Ls(tp).buffer(120) for tp in tees]
    def _poly(h): return _Pg([(c["x"], c["z"]) for c in h["coords"]]).buffer(0)
    keep = [h for h in keep if not h.get("innerOOB") or any(l.intersects(_poly(h)) for l in lines)]
    if len(keep) > MAX_HAZARDS:
        ob = sorted([h for h in keep if h.get("innerOOB") and not h.get("hasDZ")], key=lambda h: _poly(h).area)
        drop = {id(h) for h in ob[:len(keep) - MAX_HAZARDS]}
        keep = [h for h in keep if id(h) not in drop]
    g["Hazards"], g["hazardCount"] = keep, len(keep)
    # in bounds: no tee, aim point or pin inside interior OB
    from shapely.geometry import Polygon as _P
    ob = [_P([(c["x"], c["z"]) for c in h["coords"]]).buffer(0) for h in keep if h.get("innerOOB")]
    for h in g["Holes"]:
        for p in [x["Position"] for x in h["Tees"] if x["Enabled"] and x.get("Position")] + [x["Position"] for x in h["Pins"] if x.get("Position")]:
            if any(o.contains(Point(p["x"], p["z"])) for o in ob):
                print("   OUT OF BOUNDS: hole %d point (%.1f, %.1f)" % (h["HoleNumber"], p["x"], p["z"]))


def frame_for(nine):
    import glob
    d = os.path.join(REPO, "drone", "by_hole")
    hits = sorted(glob.glob(os.path.join(d, nine.replace(" ", "") + "_1", "*_tee_*.jpg")) +
                  glob.glob(os.path.join(d, NINE_FOLDER[nine], "*_tee_*.jpg")))
    return hits[0] if hits else None


def main():
    for folder, nines, src in COURSES:
        g = json.load(open(os.path.join(REPO, src), encoding="utf-8-sig"))
        holes = sorted(g["Holes"], key=lambda h: h["HoleNumber"])
        if len(nines) == 1:
            # Greenkeeper wants all 18 holes with handicap indexes 1-18 ("Indexes Set"): keep the pairing's 18 holes
            # (front nine = this loop, odd indexes; back nine even) and switch the back nine off
            holes = copy.deepcopy(holes)
            for h in holes:
                if h["HoleNumber"] > 9: h["Enabled"] = False
        # Ridgeline 3: the drafted Saturday pin (5 m out) sat off the narrow side of the green: 2 m out instead; any pin over 9 m
        # from the green centre goes halfway back (checked on the green mesh in Unity: MoorparkMeshes.CheckGkds)
        for h in holes:
            for p in h.get("Pins", []):
                q = p.get("Position")
                if not q: continue
                gx, gz, gy = min(GREENS, key=lambda c: (c[0] - q["x"]) ** 2 + (c[1] - q["z"]) ** 2)
                d = ((gx - q["x"]) ** 2 + (gz - q["z"]) ** 2) ** 0.5
                k = 0.4 if (GREENS.index((gx, gz, gy)) == 2 and p.get("Day") == "Saturday") else (0.5 if d > 9.0 else 1.0)
                if k < 1.0:
                    q["x"] = round(gx + (q["x"] - gx) * k, 3); q["z"] = round(gz + (q["z"] - gz) * k, 3); q["y"] = round(gy + (q["y"] - gy) * k, 3)
                    print("   moved %s pin %.1f m -> %.1f m from the green centre" % (p.get("Day"), d, d * k))
        g["Holes"] = holes
        for slot, sr in RATINGS.get(folder, {}).items():      # 18-hole pairings only; the nines stay unrated
            g[slot + "SR"] = sr
        cap_poob(g)
        water_hazards(g)
        g["SceneFolderName"] = folder
        g["CourseName"] = "Moorpark CC - " + " / ".join(nines)
        g["CourseImageFileName"] = folder + ".jpg"          # the three-horse club logo (BoJa, Oct 27)
        g["CoursePar"] = sum(h["Par"] for h in holes if h["Enabled"])
        for t in g["TeeTypeTotalDistance"]:
            t["Distance"] = round(sum(next((x["Distance"] for x in h["Tees"] if x["TeeType"] == t["TeeType"] and x["Enabled"]), 0.0)
                                      for h in holes if h["Enabled"]), 3)
        d = os.path.join(OUT, folder)
        os.makedirs(d, exist_ok=True)
        json.dump(g, open(os.path.join(d, folder + ".GKD"), "w", encoding="utf-8"), separators=(",", ":"))
        # coursedetails.txt in the layout Greenkeeper writes (see TierraRejada); Greenkeeper rewrites it on Save
        pars = [h["Par"] if h["Enabled"] else 0 for h in holes] + [0] * (18 - len(holes))
        hcps = [h["Index"] for h in holes] + [0] * (18 - len(holes))
        yards = [round(next((t["Distance"] for t in g["TeeTypeTotalDistance"] if t["TeeType"] == tt), 0) / 0.9144) for tt in TEES]
        f = [g["CourseName"], g.get("Designer", ""), str(round(g.get("altitudeV2", 0))), g.get("CourseInfo", ""), g.get("Location", ""), "1", "0",
             str(g["CoursePar"])] + [str(p) for p in pars] + [str(x) for x in hcps] + [v for tt in TEES for v in g.get(tt + "SR", "0.0/0").split("/")] + ["0"] * 14 + \
            [g.get("CityTown", ""), g.get("StateCountyProvince", ""), g.get("Country", ""), "%.6f" % g["CourseLat"], "%.6f" % g["CourseLong"],
             str(g.get("DifficultyRating", 0)), "0"] + [str(y) for y in yards] + ["0", "0", "#$%^&*"]
        open(os.path.join(d, "coursedetails.txt"), "w", encoding="utf-8").write("|".join(f) + "\n")
        fr = os.path.join(REPO, "logo", "moorpark_logo.png")
        if os.path.exists(fr):
            lg = Image.open(fr).convert("RGB"); sp = Image.new("RGB", (1280, 720), (255, 255, 255))
            lg.thumbnail((680, 680)); sp.paste(lg, ((1280 - lg.width) // 2, (720 - lg.height) // 2))
            sp.save(os.path.join(d, folder + ".jpg"), quality=92)
        print("%-30s %-40s %2d holes, par %d, splash %s" % (folder, g["CourseName"], len(holes), g["CoursePar"], os.path.basename(fr) if fr else "none"))


if __name__ == "__main__":
    main()
    for name, n, px in BRUSH_LOG: print("   %s: %d brush OB patches, %.1f ha" % (name, n, px / 1e4))
