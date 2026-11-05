"""course.svg -> OPCD mesh plan (step 1 of 2; step 2 is tools/opcd_build_blend.py inside Blender).

Rebuilds what Clender made for Tierra Rejada (tierra-rejada/TierraRejada_course_svg/TierraRejada_course_svg_1.blend):
  frame       Blender X = -east, Y = -north of the square's SW corner, Z = 0 (flat; conform drapes it later)
  surfaces    Spline_<layer>_<i>_<Own>_-_Mesh: triangulated, the outline inset 0.12 m
  blends      Spline_<layer>_<i>_B_<Outside>_<Own>_-_Blend: one quad strip from the mesh edge out to the outline,
              vertex group "Blend" = 1 on the outline loop, Col red on the mesh side and black on the outline
              Spline_<layer>_<i>_hole<k>_B_<Own>_-_Blend: the same strip inside a hole (a green in a fairway)
  bunkers / water: rim + 3 rings 0.15 m apart (quads), then the triangulated inside, Col red / green / blue,
              so the OPCD dig (blender_scripts/conform.py) finds its lip loops
  rough, deep: no blends, tiled at 150 m like Clender's rough_<i>_<tile>
The outlines come from the SVG rasterised at 0.5 m (one class per pixel) and polygonised with shared edges, then
simplified as a coverage (shapely.coverage_simplify), so neighbouring surfaces meet without gaps or overlaps.

    python tools/opcd_prep.py            # -> build/opcd_plan.pkl, with the smoothed outlines of tools/outline_smooth.py
    python tools/opcd_prep.py --pixels   # the first build's pixel outlines (stair-stepped)
    python tools/opcd_prep.py --zones course_zoned_hole_zones.png   # rough/deep cut per hole: Spline_rough_h03L_0_<tile>
"""
import os, pickle, sys, time
import numpy as np
import shapely
from shapely.geometry import shape, Polygon, MultiPolygon, LineString, box
from shapely.ops import unary_union
from rasterio import features
from affine import Affine

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import svg_fix                                           # load_layers(): the SVG as a 0.5 m class raster

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
S, R = svg_fix.S, svg_fix.R
T0 = time.time()
def log(*a): print("[%6.1fs]" % (time.time() - T0), *a, flush=True)

# class code -> (layer id, Own name, material, boundary spacing, interior spacing, blends?, rings?)
CLS = {
    1: ("rough", "Rough", "Rough", 1.0, 1.2, False, False),
    2: ("deep", "Deep", "Deep", 1.0, 1.0, False, False),
    3: ("fairway", "Fairway", "Fairway", 0.5, 0.75, True, False),
    4: ("tee", "Tee", "Tee", 0.35, 0.85, True, False),
    5: ("green", "Green", "Green", 0.35, 0.6, True, False),
    6: ("bunker", "Bunker", "Bunker", 0.25, 0.6, True, True),
    7: ("water", "Water_Base_Lake", "Water_Base_Lake", 0.3, 1.0, True, True),
}
BLEND_MAT = {"Fairway": "Fairway - Blend", "Tee": "Tee - Blend", "Green": "Green - Blend", "Bunker": "Bunker - Blend",
             "Water_Base_Lake": "Water_Base_Lake - Blend"}
OUTSIDE = {1: "Rough", 2: "Deep", 3: "Fairway", 4: "Tee", 5: "Green"}   # what a blend fades into (unity_editor/MoorparkLook.cs); else none
INSET = 0.12
RING_STEP, RINGS = 0.15, 3
TILE = 150.0
MIN_AREA = 2.0


def to_blender(xy):
    """SVG metres (x east, y down from the north edge) -> Clender frame (X = -east, Y = -north)."""
    xy = np.asarray(xy, float)
    return np.column_stack([-xy[:, 0], xy[:, 1] - S])


def resample(ring, step):
    """closed ring (N x 2, first != last) at about `step` spacing, keeping the corners of the simplified outline"""
    pts = np.asarray(ring, float)
    out = []
    for a, b in zip(pts, np.roll(pts, -1, axis=0)):
        L = np.hypot(*(b - a))
        n = max(1, int(np.ceil(L / step)))
        t = np.arange(n)[:, None] / n
        out.append(a + (b - a) * t)
    return np.vstack(out)


def ring_coords(lr):
    c = np.asarray(lr.coords)[:-1]
    return c


def offset_ring(ring, d):
    """move every vertex d metres to the left of travel (inwards for a CCW ring), mitre-limited"""
    p = ring
    prev, nxt = np.roll(p, 1, axis=0), np.roll(p, -1, axis=0)
    t1 = p - prev; t2 = nxt - p
    t1 /= np.maximum(np.hypot(t1[:, 0], t1[:, 1]), 1e-9)[:, None]
    t2 /= np.maximum(np.hypot(t2[:, 0], t2[:, 1]), 1e-9)[:, None]
    n1 = np.column_stack([-t1[:, 1], t1[:, 0]]); n2 = np.column_stack([-t2[:, 1], t2[:, 0]])
    n = n1 + n2
    ln = np.maximum(np.hypot(n[:, 0], n[:, 1]), 1e-9)
    n /= ln[:, None]
    cos_half = np.clip((n * n1).sum(1), 0.35, 1.0)        # mitre limit ~2.9x
    return p + n * (np.asarray(d, float) / cos_half)[:, None]


def safe_inset(ring, d):
    """offset_ring, but where the inset ring crosses itself (sharp corners where a fairway meets a bunker, tight necks)
    the vertices near the crossing are inset less, so the blend strip still fits; None if it can't be fixed"""
    from shapely.validation import explain_validity
    import re as _re
    dd = np.full(len(ring), float(d))
    for _ in range(40):
        inner = offset_ring(ring, dd)
        if ok_ring(inner):
            return inner
        why = explain_validity(Polygon(inner))
        m = _re.search(r"\[(-?[\d.]+) (-?[\d.]+)\]", why)
        if not m:
            return None
        q = np.array([float(m.group(1)), float(m.group(2))])
        near = np.hypot(*(inner - q).T) < 1.5
        if not near.any() or dd[near].max() < 0.005:
            return None
        dd[near] *= 0.4
    return None


def ok_ring(r):
    return len(r) >= 4 and Polygon(r).is_valid and Polygon(r).area > 0.05


def polygonise(lab):
    """class raster -> {code: [Polygon]} with shared edges, simplified as one coverage"""
    tr = Affine(R, 0, 0, 0, R, 0)                         # pixel (col,row) -> SVG metres
    geoms, codes = [], []
    for g, v in features.shapes(lab, mask=lab > 0, transform=tr, connectivity=4):
        geoms.append(shape(g)); codes.append(int(v))
    log("polygonised", len(geoms), "pieces")
    simp = shapely.coverage_simplify(np.array(geoms, dtype=object), 0.6)   # 0.6 m takes out the 0.5 m pixel stairs
    out = {c: [] for c in CLS}
    for g, c in zip(simp, codes):
        g = shapely.make_valid(g)
        for p in (g.geoms if hasattr(g, "geoms") else [g]):
            if isinstance(p, Polygon) and p.area >= MIN_AREA:
                out[c].append(p)
    for c, ps in out.items():
        log("  %-8s %5d polygons %8.1f ha" % (CLS[c][0], len(ps), sum(p.area for p in ps) / 1e4))
    return out


def outside_name(poly, lab):
    """majority class just outside the polygon's outline (0.6 m out), as a blend prefix"""
    ring = resample(ring_coords(poly.exterior), 2.0)
    out = offset_ring(ring if Polygon(ring).exterior.is_ccw else ring[::-1], -0.6)
    ix = np.clip((out[:, 0] / R).astype(int), 0, lab.shape[1] - 1)
    iy = np.clip((out[:, 1] / R).astype(int), 0, lab.shape[0] - 1)
    v = lab[iy, ix]
    v = v[(v > 0)]
    if len(v) == 0:
        return None
    best = np.bincount(v, minlength=8).argmax()
    return OUTSIDE.get(int(best))


def interior_points(poly, s):
    """hex grid at spacing s inside poly, at least 0.5 s from its edges"""
    core = poly.buffer(-0.5 * s)
    if core.is_empty:
        return np.zeros((0, 2))
    x0, y0, x1, y1 = core.bounds
    dy = s * 0.866
    ys = np.arange(y0, y1 + dy, dy)
    pts = []
    for k, y in enumerate(ys):
        xs = np.arange(x0 + (0.5 * s if k % 2 else 0), x1 + s, s)
        pts.append(np.column_stack([xs, np.full_like(xs, y)]))
    pts = np.vstack(pts)
    inside = shapely.contains_xy(core, pts[:, 0], pts[:, 1])
    return pts[inside]


def plan_feature(code, idx, poly, lab, objs):
    lid, own, mat, bstep, istep, blends, rings = CLS[code]
    sid = "%s_%d" % (lid, idx)
    poly = shapely.geometry.polygon.orient(poly, 1.0)    # exterior CCW, holes CW (in SVG coords)
    # SVG y points down, so CCW in SVG = CW on the ground; offset_ring's "left" is inwards for SVG-CCW rings
    ext = resample(ring_coords(poly.exterior), bstep)
    if not Polygon(ext).exterior.is_ccw: ext = ext[::-1]
    holes = []
    for h in poly.interiors:
        hr = resample(ring_coords(h), bstep)
        if Polygon(hr).exterior.is_ccw: hr = hr[::-1]    # holes run CW so "left" points into the polygon too
        holes.append(hr)
    loops = []                                            # constraint loops of the triangulated mesh
    inner = safe_inset(ext, INSET) if blends else None
    if blends and inner is None:
        blends = False                                    # too thin for a 0.12 m strip: plain mesh up to the outline
    if blends:
        outer = ext
        prefix = outside_name(poly, lab)
        name = "Spline_%s_B_%s%s_-_Blend" % (sid, (prefix + "_") if prefix and prefix != own else "", own)
        objs.append(dict(name=name, kind="blend", shape_id=sid, parent_id=sid, material=BLEND_MAT[own],
                         strip=(to_blender(inner), to_blender(outer))))
        loops.append(inner)
        for k, h in enumerate(holes):
            hin = safe_inset(h, INSET)
            if hin is None:
                loops.append(h)                           # hole too tight for a strip: cut it at the outline
                continue
            objs.append(dict(name="Spline_%s_hole%d_B_%s_-_Blend" % (sid, k, own), kind="blend",
                             shape_id="%s_hole%d" % (sid, k), parent_id=sid, material=BLEND_MAT[own],
                             strip=(to_blender(hin), to_blender(h))))
            loops.append(hin)
    else:
        loops = [ext] + holes
    ring_list = []
    if rings:                                             # rim + 3 rings for the dig
        cur = loops[0]; ring_list = [cur]
        for _ in range(RINGS):
            nxt = offset_ring(cur, RING_STEP)
            if not ok_ring(nxt) or not Polygon(cur).contains(Polygon(nxt)):
                break
            ring_list.append(nxt); cur = nxt
        cons = [cur] + loops[1:]
    else:
        cons = loops
    if code == 7:
        # the OPCD water dig lowers every loop in from the rim, so a fine inside makes a 30-40 m deep hole:
        # keep Clender's ~12 loops from the rim to the middle
        istep = max(istep, np.sqrt(poly.area / np.pi) / 10.0)
    V, F, col = triangulate(cons, istep)
    if V is None:
        return False
    if len(ring_list) > 1 and not (len(V) >= len(cons[0]) and np.allclose(V[:len(cons[0])], cons[0])):
        ring_list = []                                    # the inside loop was rebuilt (invalid outline): no dig rings
    if len(ring_list) > 1:                                # quads between the rings, then the triangulated inside
        offs, RV = [], []
        base = 0
        for r in ring_list[:-1]:
            offs.append(base); RV.append(r); base += len(r)
        last = base                                       # ring_list[-1] is the first loop of V
        allV = np.vstack(RV + [V])
        quads = []
        for k in range(len(ring_list) - 1):
            a0 = offs[k]; b0 = offs[k + 1] if k + 1 < len(offs) else last
            n = len(ring_list[k])
            for j in range(n):
                j2 = (j + 1) % n
                quads.append((a0 + j, a0 + j2, b0 + j2, b0 + j))
        F = [tuple(q) for q in quads] + [tuple(int(i) + last for i in f) for f in F]
        n0 = len(ring_list[0])                            # Col: rim red, rings green, inside blue (as Clender)
        col = [0] * n0 + [1] * (last - n0) + [1] * len(ring_list[-1]) + [2] * (len(V) - len(ring_list[-1]))
        V = allV
    else:
        F = [tuple(int(i) for i in f) for f in F]
        col = [0] * len(V)
    objs.append(dict(name="Spline_%s_%s_-_Mesh" % (sid, own), kind="mesh", shape_id=sid, parent_id=sid, material=mat,
                     verts=to_blender(V), faces=F, col=np.array(col, np.uint8)))
    return True


def triangulate(loops, istep):
    """constrained triangulation of loops[0] minus the holes loops[1:], with a hex grid of points inside (triangle 'pY')"""
    import triangle
    outer = Polygon(loops[0], loops[1:])
    if not outer.is_valid:
        outer = shapely.make_valid(outer)
        if not isinstance(outer, Polygon):
            outer = max((g for g in getattr(outer, "geoms", []) if isinstance(g, Polygon)), key=lambda g: g.area, default=None)
            if outer is None:
                return None, None, None
        loops = [ring_coords(outer.exterior)] + [ring_coords(h) for h in outer.interiors]
    pts = interior_points(outer, istep)
    # triangle crashes (no exception) on repeated vertices: drop repeats within 1 mm, in and across loops
    seen = set(); clean = []
    for l in loops:
        keep = []
        for p in np.asarray(l):
            k = (round(p[0] * 1000), round(p[1] * 1000))
            if k in seen: continue
            seen.add(k); keep.append(p)
        if len(keep) >= 3: clean.append(np.array(keep))
    if not clean:
        return None, None, None
    loops = clean
    if len(pts):
        pts = np.array([p for p in pts if (round(p[0] * 1000), round(p[1] * 1000)) not in seen])
    verts, segs, n = [], [], 0
    for l in loops:
        verts.append(l)
        m = len(l)
        segs.append(np.column_stack([np.arange(m) + n, (np.arange(m) + 1) % m + n]))
        n += m
    holes = [Polygon(h).representative_point().coords[0] for h in loops[1:]]
    if len(pts): verts.append(pts)
    A = dict(vertices=np.vstack(verts), segments=np.vstack(segs))
    if holes: A["holes"] = np.array(holes)
    try:
        B = triangle.triangulate(A, "pY")
    except Exception:
        try:
            B = triangle.triangulate(A, "p")
        except Exception as e:
            log("   triangulation failed:", e)
            return None, None, None
    if "triangles" not in B or len(B["triangles"]) == 0:
        return None, None, None
    return B["vertices"], B["triangles"], None


def load_zones(path):
    """hole_zones png (uint16 ids, 1 px = res m, row 0 = SVG y 0; res from the .json beside it) -> [(id, tag, polygon)]."""
    import json
    from PIL import Image
    here = os.path.dirname(os.path.abspath(__file__))
    sys.path[:0] = [here, os.path.join(here, "..")]          # hole_zones.py beside this file or one up (course-build/tools)
    import hole_zones as HZ
    meta = os.path.splitext(path)[0] + ".json"
    res = json.load(open(meta))["res"] if os.path.exists(meta) else 1.0
    zone = np.asarray(Image.open(path)).astype(np.int32)
    parts = {}
    for g, v in features.shapes(zone, transform=Affine(res, 0, 0, 0, res, 0), connectivity=4):
        parts.setdefault(int(v), []).append(shape(g))
    zs = sorted(parts)
    simp = shapely.coverage_simplify(np.array([unary_union(parts[z]) for z in zs], dtype=object), res * 0.6)
    log("zones from", path, len(zs))
    return [(z, HZ.zone_tag(z), shapely.make_valid(g)) for z, g in zip(zs, simp)]


def split_by_zone(polys, zones):
    """Cut each polygon along the hole zones -> [(name part, polygon)]: rough_h03L_<k> instead of rough_<i>."""
    out, count = [], {}
    for poly in polys:
        left = poly
        for z, tag, zp in zones:
            if left.is_empty or not zp.intersects(left):
                continue
            piece = left.intersection(zp); left = left.difference(zp)
            for p in (piece.geoms if hasattr(piece, "geoms") else [piece]):
                if isinstance(p, Polygon) and p.area >= MIN_AREA:
                    k = count.get(tag, 0); count[tag] = k + 1
                    out.append(("%s_%d" % (tag, k), p))
        for p in (left.geoms if hasattr(left, "geoms") else [left]):
            if not isinstance(p, Polygon) or p.area < MIN_AREA:
                continue
            pb = p.buffer(0.05)                                     # a sliver outside every zone joins the piece it touches most
            best = max(range(len(out)), key=lambda j: pb.intersection(out[j][1]).area if pb.intersects(out[j][1]) else 0, default=None)
            if best is not None and pb.intersects(out[best][1]):
                g = shapely.make_valid(unary_union([out[best][1], p]))
                if isinstance(g, Polygon):
                    out[best] = (out[best][0], g); continue
            k = count.get("out", 0); count["out"] = k + 1
            out.append(("out_%d" % k, p))
    return out


def plan_tiled(code, polys, objs, zones=None):
    lid, own, mat, bstep, istep, _, _ = CLS[code]
    n = 0
    named = split_by_zone(polys, zones) if zones else list(enumerate(polys))
    for i, poly in named:
        x0, y0, x1, y1 = poly.bounds
        t = 0
        for gx in np.arange(np.floor(x0 / TILE) * TILE, x1, TILE):
            for gy in np.arange(np.floor(y0 / TILE) * TILE, y1, TILE):
                piece = poly.intersection(box(gx, gy, gx + TILE, gy + TILE))
                for p in (piece.geoms if hasattr(piece, "geoms") else [piece]):
                    if not isinstance(p, Polygon) or p.area < MIN_AREA:
                        continue
                    p = shapely.geometry.polygon.orient(p, 1.0)
                    ext = resample(ring_coords(p.exterior), bstep)
                    holes = [resample(ring_coords(h), bstep) for h in p.interiors]
                    V, F, _ = triangulate([ext] + holes, istep)
                    if V is None:
                        continue
                    sid = "%s_%s_%d" % (lid, i, t)
                    objs.append(dict(name="Spline_%s_%s_-_Mesh" % (sid, own), kind="mesh", shape_id=sid, parent_id=sid,
                                     material=mat, verts=to_blender(V), faces=[tuple(int(k) for k in f) for f in F],
                                     col=np.zeros(len(V), np.uint8)))
                    t += 1; n += 1
    return n


def main():
    os.chdir(REPO)
    if "--pixels" in sys.argv:                            # the old way: pixel outlines, coverage-simplified
        lab = svg_fix.load_layers("course.svg")
        log("course.svg rasterised", lab.shape)
        polys = polygonise(lab)
    else:                                                 # smoothed vector outlines (tools/outline_smooth.py)
        import outline_smooth
        polys, lab, _ = outline_smooth.build("course.svg")
        outline_smooth.write_svg(polys, "course_smooth.svg")
        log("wrote course_smooth.svg")
    objs = []
    zones = None
    if "--zones" in sys.argv:                             # per-hole rough: hole_zones png from svg_zone_split.py / hole_zones.py
        zones = load_zones(sys.argv[sys.argv.index("--zones") + 1])
    for code in (1, 2):
        log("tiled", CLS[code][0], plan_tiled(code, polys[code], objs, zones), "pieces")
    for code in (3, 4, 5, 6, 7):
        skipped = 0
        for i, p in enumerate(polys[code]):
            if not plan_feature(code, i, p, lab, objs):
                skipped += 1
        log(CLS[code][0], len(polys[code]) - skipped, "made", skipped, "too thin for the 0.12 m blend, skipped")
    os.makedirs("build", exist_ok=True)
    pickle.dump(dict(size=S, objects=objs), open("build/opcd_plan.pkl", "wb"), protocol=4)
    nv = sum(len(o["verts"]) if "verts" in o else 2 * len(o["strip"][0]) for o in objs)
    log("wrote build/opcd_plan.pkl:", len(objs), "objects, about", nv, "vertices")


if __name__ == "__main__":
    main()
