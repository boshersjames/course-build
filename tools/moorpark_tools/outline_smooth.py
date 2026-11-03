"""Smooth course.svg's outlines into a clean vector coverage for the OPCD meshes (used by tools/opcd_prep.py).

course.svg is traced from 0.5 m pixels, and its tees, greens and bunkers were rasterised from OSM at coarser steps first,
so in GSPro the edges were crude angular polygons with stair steps. This rebuilds the outlines:
  tees, greens, bunkers  the OSM polygon where one matches the svg shape (IoU >= 0.4); tees get 0.8 m round corners,
                         greens and bunkers are Chaikin-smoothed and densified. Shapes with no OSM match (bunkers and
                         turf added in tools/svg_fix.py from the photo) are their own pixel outline, smoothed the same way.
  water                  the pixel outline, smoothed
  rough, deep, fairway   the pixels under the features above are filled from their neighbours, each class is blurred
                         (fairway 2.5 m, the rest 2 m) and the strongest wins, then the shared edges of the coverage are
                         simplified and Chaikin-smoothed between their junctions, so neighbours still meet exactly.
Then features are cut into the base by priority (water > bunker > green > tee > fairway > deep > rough), so nothing
overlaps and there are no gaps.

    python tools/outline_smooth.py --preview      # previews only: svg_fix_preview/../previews/outlines/*.jpg
"""
import os, sys, time
import xml.etree.ElementTree as ET
import numpy as np
import shapely
from shapely.geometry import shape, Polygon, MultiPolygon, LineString, Point
from shapely.strtree import STRtree
from shapely.prepared import prep
from rasterio import features
from affine import Affine
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import svg_fix

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
S, R, N = svg_fix.S, svg_fix.R, svg_fix.N
ROUGH, DEEP, FAIRWAY, TEE, GREEN, BUNKER, WATER = range(1, 8)
FEATURES = (WATER, BUNKER, GREEN, TEE)                    # priority order, highest first
OSM_TAG = {TEE: "tee", GREEN: "green", BUNKER: "bunker"}
BLUR = {0: 2.0, ROUGH: 2.0, DEEP: 2.0, FAIRWAY: 2.5}      # metres
TEE_CORNER = 0.8
T0 = time.time()
def log(*a): print("[%6.1fs]" % (time.time() - T0), *a, flush=True)


def chaikin(coords, n=3, closed=True):
    p = np.asarray(coords, float)
    if closed and np.allclose(p[0], p[-1]):
        p = p[:-1]
    for _ in range(n):
        if closed:
            q = np.roll(p, -1, axis=0)
            p = np.column_stack([0.75 * p + 0.25 * q, 0.25 * p + 0.75 * q]).reshape(-1, 2)
        else:
            if len(p) < 3:
                return p
            a, b = p[:-1], p[1:]
            mid = np.column_stack([0.75 * a + 0.25 * b, 0.25 * a + 0.75 * b]).reshape(-1, 2)
            p = np.vstack([p[:1], mid, p[-1:]])
    if closed:
        p = np.vstack([p, p[:1]])
    return p


def densify(coords, step):
    p = np.asarray(coords, float)
    out = [p[0]]
    for a, b in zip(p[:-1], p[1:]):
        L = np.hypot(*(b - a)); k = max(1, int(np.ceil(L / step)))
        out.extend(a + (b - a) * (np.arange(1, k + 1)[:, None] / k))
    return np.array(out)


def smooth_poly(poly, simplify=0.3, iters=3):
    """organic outline: simplify the stairs away, then Chaikin; holes the same"""
    poly = poly.simplify(simplify)
    if poly.is_empty or not isinstance(poly, Polygon):
        return poly
    ext = chaikin(densify(poly.exterior.coords, 3.0), iters)
    holes = [chaikin(densify(h.coords, 3.0), iters) for h in poly.interiors]
    out = shapely.make_valid(Polygon(ext, holes))
    return largest(out)


def largest(g):
    if isinstance(g, Polygon):
        return g
    ps = [q for q in getattr(g, "geoms", []) if isinstance(q, Polygon)]
    return max(ps, key=lambda q: q.area) if ps else Polygon()


def osm_polys():
    """golf=tee/green/bunker ways as polygons in svg metres (x east, y down from the north edge)"""
    import json
    from pyproj import Transformer
    c = json.load(open(os.path.join(REPO, "course.json")))
    t = Transformer.from_crs("EPSG:4326", "EPSG:%d" % c["epsg"], always_xy=True)
    root = ET.parse(os.path.join(REPO, "map.osm")).getroot()
    nodes = {}
    for nd in root.iter("node"):
        e, n = t.transform(float(nd.get("lon")), float(nd.get("lat")))
        nodes[nd.get("id")] = (e - c["sw_e"], S - (n - c["sw_n"]))
    out = {k: [] for k in OSM_TAG}
    inv = {v: k for k, v in OSM_TAG.items()}
    for w in root.findall("way"):
        tg = {q.get("k"): q.get("v") for q in w.findall("tag")}
        code = inv.get(tg.get("golf"))
        if code is None:
            continue
        p = [nodes[q.get("ref")] for q in w.findall("nd") if q.get("ref") in nodes]
        if len(p) < 4:
            continue
        g = largest(shapely.make_valid(Polygon(p)))
        if g.area > 1:
            out[code].append(g)
    return out


def pixel_polys(mask):
    tr = Affine(R, 0, 0, 0, R, 0)
    return [shape(g) for g, v in features.shapes(mask.astype(np.uint8), mask=mask, transform=tr, connectivity=4)]


def feature_vectors(lab, osm):
    """{code: [Polygon]} of the smoothed tees/greens/bunkers/water, and how many came from OSM"""
    out, stats = {}, {}
    for code in FEATURES:
        pieces = pixel_polys(lab == code)
        tree = STRtree(osm[code]) if code in osm and osm[code] else None
        used, res, n_osm = set(), [], 0
        for pc in pieces:
            if pc.area < 2.0:
                continue
            match = []
            if tree is not None:
                for k in tree.query(pc):
                    o = osm[code][k]
                    inter = o.intersection(pc).area
                    if inter / max(1e-6, o.union(pc).area) >= 0.4 or inter / max(o.area, 1e-6) >= 0.8:
                        match.append(k)
            if match:
                g = shapely.union_all([osm[code][k] for k in match])
                g = largest(shapely.make_valid(g))
                used.update(match); n_osm += 1
                if code == TEE:
                    g = g.buffer(-TEE_CORNER, join_style="round").buffer(TEE_CORNER, join_style="round")
                    g = largest(g) if not g.is_empty else largest(shapely.union_all([osm[code][k] for k in match]))
                    g = Polygon(densify(g.exterior.coords, 0.5), [densify(h.coords, 0.5) for h in g.interiors])
                else:
                    g = smooth_poly(g, simplify=0.05, iters=3)
            else:
                g = smooth_poly(pc, simplify=0.35, iters=3)
                if code == TEE:                           # rectangular pad: keep it straight-sided, just round the corners
                    g = pc.simplify(0.45).buffer(-TEE_CORNER, join_style="round").buffer(TEE_CORNER, join_style="round")
                    g = largest(g) if not g.is_empty else smooth_poly(pc)
            if not g.is_empty and g.area >= 2.0:
                res.append(g)
        out[code] = res
        stats[code] = (len(res), n_osm)
    return out, stats


def base_labels(lab):
    """rough/deep/fairway raster with the features filled from the nearest base pixel, then blurred-majority smoothed"""
    feat = lab >= TEE
    base = lab.copy()
    if feat.any():
        idx = ndimage.distance_transform_edt(feat, return_distances=False, return_indices=True)
        base = lab[idx[0], idx[1]]
    best = np.full(lab.shape, -1.0, np.float32)
    out = np.zeros(lab.shape, np.uint8)
    for code, sig in BLUR.items():
        f = ndimage.gaussian_filter((base == code).astype(np.float32), sig / R)
        f += 0.001 * code                                 # ties: higher class
        m = f > best
        out[m] = code; best[m] = f[m]
    return out


def smooth_coverage(polys, codes, simplify=0.6, iters=3):
    """shared-edge smoothing: simplify the coverage, smooth every edge between junctions, rebuild the faces"""
    simp = shapely.coverage_simplify(np.array(polys, dtype=object), simplify)
    simp = [shapely.make_valid(g) for g in simp]
    edges = shapely.union_all([shapely.boundary(g) for g in simp if not g.is_empty])
    arcs = shapely.line_merge(edges)
    arcs = list(arcs.geoms) if hasattr(arcs, "geoms") else [arcs]
    ends = {}
    for a in arcs:
        c = a.coords
        for q in (c[0], c[-1]):
            k = (round(q[0], 3), round(q[1], 3)); ends[k] = ends.get(k, 0) + 1
    sm = []
    for a in arcs:
        c = np.asarray(a.coords)
        k = (round(c[0][0], 3), round(c[0][1], 3))
        free_ring = a.is_closed and ends.get(k, 0) == 2
        if len(c) < 3:
            sm.append(a); continue
        sm.append(LineString(chaikin(c, iters, closed=free_ring)))
    noded = shapely.union_all(sm)
    faces = list(shapely.polygonize(list(noded.geoms) if hasattr(noded, "geoms") else [noded]).geoms)
    tree = STRtree(simp)
    out = []
    for f in faces:
        if f.area < 0.05:
            continue
        pt = f.representative_point()
        cand = [i for i in tree.query(pt, predicate="intersects")]
        if not cand:
            cand = list(tree.query(f))
            if not cand:
                continue
            i = max(cand, key=lambda i: simp[i].intersection(f).area)
            if simp[i].intersection(f).area < 0.5 * f.area:
                continue
        else:
            i = cand[0]
        out.append((f, codes[i]))
    return out


def build(path="course.svg"):
    """-> ({code: [Polygon]}, label raster of the result at 0.5 m, stats)"""
    lab = svg_fix.load_layers(os.path.join(REPO, path))
    log("svg rasterised")
    osm = osm_polys()
    log("osm:", {OSM_TAG[k]: len(v) for k, v in osm.items()})
    feats, stats = feature_vectors(lab, osm)
    xt = os.path.join(REPO, "tee_pads_extra.json")     # pads for drafted tees with none nearby (tools/tee_fix.py)
    if os.path.exists(xt):
        import json
        for t in json.load(open(xt)):
            a = np.radians(t["heading"]); d = np.array([np.cos(a), -np.sin(a)])     # along the hole, in svg y-down
            n = np.array([-d[1], d[0]]); c = np.array([t["x"], S - t["z"]])
            box = Polygon([c + d * u * 4.0 + n * v * 5.0 for u, v in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
            feats[TEE].append(box.buffer(-TEE_CORNER, join_style="round").buffer(TEE_CORNER, join_style="round"))
        log("extra tee pads:", len(json.load(open(xt))))
    log("features:", {svg_fix.LAYERS[k - 1][0]: "%d (%d from OSM)" % v for k, v in stats.items()})
    base = base_labels(lab)
    log("base smoothed")
    tr = Affine(R, 0, 0, 0, R, 0)
    geoms, codes = [], []
    for g, v in features.shapes(base, mask=base > 0, transform=tr, connectivity=4):
        geoms.append(shape(g)); codes.append(int(v))
    log("base pieces", len(geoms))
    faces = smooth_coverage(geoms, codes)
    log("base faces", len(faces))
    out = {c: [] for c in range(1, 8)}
    higher = Polygon()
    for code in FEATURES:                                 # cut by priority
        cur = []
        for g in feats[code]:
            d = g.difference(higher) if not higher.is_empty else g
            for p in getattr(d, "geoms", [d]):
                if isinstance(p, Polygon) and p.area >= 1.0:
                    cur.append(p)
        out[code] = cur
        if cur:
            higher = shapely.union_all([higher] + cur) if not higher.is_empty else shapely.union_all(cur)
    hp = prep(higher) if not higher.is_empty else None
    for f, code in faces:
        if hp is not None and hp.intersects(f):
            f = f.difference(higher)
        for p in getattr(f, "geoms", [f]):
            if isinstance(p, Polygon) and p.area >= 2.0:
                out[code].append(shapely.make_valid(p) if not p.is_valid else p)
    clean = {}
    for c, ps in out.items():
        cl = []
        for p in ps:
            for q in getattr(p, "geoms", [p]):
                if isinstance(q, Polygon) and q.area >= (1.0 if c >= TEE else 2.0):
                    cl.append(q)
        clean[c] = cl
        log("  %-8s %5d polygons %8.2f ha" % (svg_fix.LAYERS[c - 1][0], len(cl), sum(p.area for p in cl) / 1e4))
    shapes = [(p, c) for c in range(1, 8) for p in clean[c]]
    lab2 = features.rasterize(shapes, out_shape=(N, N), transform=tr, fill=0, dtype=np.uint8)
    return clean, lab2, lab


def write_svg(polys, path):
    out = ['<?xml version="1.0" encoding="UTF-8" standalone="no"?>',
           '<svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" '
           'xmlns:sodipodi="http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd" width="%dmm" height="%dmm" viewBox="0 0 %d %d" version="1.1">' % (S, S, S, S)]
    for code, (title, pid, col) in enumerate(svg_fix.LAYERS, start=1):
        out.append('<g inkscape:groupmode="layer" id="layer_%s" inkscape:label="%s">' % (pid, title))
        for i, p in enumerate(polys.get(code, [])):
            loops = [p.exterior] + list(p.interiors)
            d = " ".join("M " + " L ".join("%.3f,%.3f" % xy for xy in list(l.coords)[:-1]) + " Z" for l in loops)
            out.append('<path id="%s_%d" style="fill:%s;fill-opacity:1;fill-rule:evenodd;stroke:none" d="%s"/>' % (pid, i, col, d))
        out.append("</g>")
    out.append("</svg>")
    open(path, "w", encoding="utf-8").write("\n".join(out))


SPOTS = {  # name: (east, north, half-size m)
    "Ridgeline1_tees": (1335, 1600, 75), "Ridgeline1_green": (1211, 1366, 40), "Creekside1_tees": (1245, 1878, 60),
    "Creekside1_green": (1675, 2032, 40), "Ridgeline9": (1215, 1770, 110), "CanyonCrest2_bunkers": (1900, 1690, 60),
}


def preview(polys, old_lab, outdir):
    from PIL import Image, ImageDraw
    sat = Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB")
    k = sat.size[0] / S
    cols = {ROUGH: (60, 160, 70), DEEP: (30, 90, 40), FAIRWAY: (120, 255, 140), TEE: (255, 120, 255), GREEN: (255, 255, 0),
            BUNKER: (255, 255, 255), WATER: (60, 140, 255)}
    os.makedirs(outdir, exist_ok=True)
    old = {}
    for c in range(1, 8):
        old[c] = pixel_polys(old_lab == c)
    for name, (e, n, h) in SPOTS.items():
        x0, y0 = e - h, (S - n) - h
        crop = sat.crop((int(x0 * k), int(y0 * k), int((x0 + 2 * h) * k), int((y0 + 2 * h) * k)))
        Z = 1200 / crop.size[0]
        crop = crop.resize((1200, 1200), Image.BICUBIC)
        pair = []
        for src in (old, polys):
            im = crop.copy(); d = ImageDraw.Draw(im)
            for c in range(1, 8):
                for p in src[c]:
                    if p.bounds[2] < x0 or p.bounds[0] > x0 + 2 * h or p.bounds[3] < y0 or p.bounds[1] > y0 + 2 * h:
                        continue
                    for l in [p.exterior] + list(p.interiors):
                        pts = [((x - x0) * k * Z, (y - y0) * k * Z) for x, y in l.coords]
                        d.line(pts, fill=cols[c], width=2)
            pair.append(im)
        both = Image.new("RGB", (2410, 1240), (20, 20, 20))
        both.paste(pair[0], (0, 40)); both.paste(pair[1], (1210, 40))
        dd = ImageDraw.Draw(both)
        dd.text((10, 10), name + ": before (course.svg pixels)", fill=(255, 255, 255))
        dd.text((1220, 10), name + ": after (smoothed)   magenta tee, yellow green, white bunker, light green fairway", fill=(255, 255, 255))
        both.save(os.path.join(outdir, name + ".jpg"), quality=88)


if __name__ == "__main__":
    polys, lab2, lab = build()
    if "--preview" in sys.argv or "--svg" in sys.argv:
        preview(polys, lab, os.path.join(REPO, "previews", "outlines"))
        log("previews written")
    if "--svg" in sys.argv:
        write_svg(polys, os.path.join(REPO, "course_smooth.svg"))
        log("wrote course_smooth.svg")
