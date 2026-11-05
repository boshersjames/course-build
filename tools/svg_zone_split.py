"""Split the rough and deep rough of an EXISTING course SVG along hole zones, keeping every other edge exactly as drawn.

For a course whose SVG has already been hand-fixed (collars, pads, bunker lips), regenerating with
`coursekit.py svg --hole-zones` would throw those edits away. This clips the existing Rough/Deep paths with the
hole-zone polygons (shapely), so outer edges are untouched and new edges appear only where two zones meet.

    python svg_zone_split.py --svg course.svg --osm map.osm [--out course_zoned.svg]

Hole lines come from map.osm (golf=hole with ref) in the course.json frame (size, epsg, sw_e, sw_n).
Writes the new SVG (never overwrites --svg), <out>_zones.csv (path id -> hole zone) and <out>_preview.png.
"""
import argparse, csv, json, os, re, sys
import xml.etree.ElementTree as ET
import numpy as np
from PIL import Image, ImageDraw
from shapely.geometry import Polygon, box, MultiPolygon
from shapely.ops import unary_union

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hole_zones as HZ

SPLIT = ("rough", "deep")


def rings_of(d):
    out = []
    for sub in re.split(r"[Mm]", d):
        pts = [(float(x), float(y)) for x, y in re.findall(r"(-?[\d.]+)[ ,](-?[\d.]+)", sub)]
        if len(pts) > 2:
            out.append(pts)
    return out


def evenodd(rings):
    g = None
    for r in rings:
        p = Polygon(r).buffer(0)
        g = p if g is None else g.symmetric_difference(p)
    return g


def to_d(geom):
    parts = []
    polys = geom.geoms if hasattr(geom, "geoms") else [geom]
    for p in polys:
        if p.is_empty or p.geom_type != "Polygon":
            continue
        for ring in [p.exterior] + list(p.interiors):
            c = list(ring.coords)[:-1]
            parts.append("M " + " L ".join("%.3f,%.3f" % q for q in c) + " Z")
    return " ".join(parts)


def zone_polygons(zone, res):
    """Raster zone grid -> {zone id: shapely geometry} in SVG units (row runs merged)."""
    from rasterio import features
    from rasterio.transform import Affine
    from shapely.geometry import shape
    parts = {}
    for geom, z in features.shapes(zone.astype(np.int32), transform=Affine(res, 0, 0, 0, res, 0)):
        parts.setdefault(int(z), []).append(shape(geom))
    return {z: unary_union(g).simplify(res * 0.5) for z, g in parts.items()}


def hole_lines_px(osm, c, res):
    from pyproj import Transformer
    S = float(c["size"])
    t = Transformer.from_crs("EPSG:4326", "EPSG:%d" % c["epsg"], always_xy=True)
    root = ET.parse(osm).getroot()
    nodes = {}
    for nd in root.iter("node"):
        e, n = t.transform(float(nd.get("lon")), float(nd.get("lat")))
        nodes[nd.get("id")] = ((e - c["sw_e"]) / res, (S - (n - c["sw_n"])) / res)
    holes = {}
    for w in root.findall("way"):
        tg = {q.get("k"): q.get("v") for q in w.findall("tag")}
        if tg.get("golf") != "hole":
            continue
        num = int(re.sub(r"\D", "", tg.get("ref", "")) or 0)
        p = [nodes[q.get("ref")] for q in w.findall("nd") if q.get("ref") in nodes]
        if num and len(p) > 1:
            holes.setdefault(num, []).append(p)
    return holes


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--svg", default="course.svg"); ap.add_argument("--osm", default="map.osm")
    ap.add_argument("--course", default="course.json"); ap.add_argument("--out", default="course_zoned.svg")
    ap.add_argument("--res", type=float, default=1.0)
    ap.add_argument("--band", type=float, default=12.0); ap.add_argument("--max-dist", type=float, default=150.0)
    ap.add_argument("--min-piece", type=float, default=20.0, help="pieces smaller than this (m2) join a neighbour of the same path")
    ap.add_argument("--satellite", default="satellite.jpg")
    a = ap.parse_args()
    if os.path.abspath(a.out) == os.path.abspath(a.svg):
        sys.exit("--out must differ from --svg")
    c = json.load(open(a.course)); S = float(c["size"]); N = int(round(S / a.res))
    holes = hole_lines_px(a.osm, c, a.res)
    if not holes:
        sys.exit("no golf=hole lines with a ref in %s" % a.osm)
    s = open(a.svg, encoding="utf8").read()

    # course area = everything drawn (any layer), so zones only cover the course
    img = Image.new("L", (N, N), 0); drw = ImageDraw.Draw(img)
    for d in re.findall(r'\sd="([^"]+)"', s):
        for r in rings_of(d):
            drw.polygon([(x / a.res, y / a.res) for x, y in r], fill=255)
    zone = HZ.zone_grid(holes, N, a.res, np.asarray(img) > 0, a.band, a.max_dist)
    print("holes %s; %d zones" % (",".join(map(str, sorted(holes))), len(np.unique(zone))))
    zpolys = zone_polygons(zone, a.res)

    rows, counter = [], {}
    def repl(m):
        tag = m.group(0)
        pid = re.search(r'\bid="([^"]+)"', tag).group(1)
        layer = re.sub(r"_\d+$", "", pid)
        if layer not in SPLIT:
            return tag
        dm = re.search(r'\sd="([^"]+)"', tag)
        geom = evenodd(rings_of(dm.group(1)))
        if geom is None or geom.is_empty:
            return tag
        pieces = []
        left = geom                                   # zones are simplified separately: take each cell once, no overlaps
        for z, zp in zpolys.items():
            if left.is_empty or not zp.intersects(left):
                continue
            g = left.intersection(zp)
            left = left.difference(zp)
            for p in (g.geoms if hasattr(g, "geoms") else [g]):
                if p.geom_type == "Polygon" and p.area > 0:
                    pieces.append([z, p])
        # gaps between simplified zones: hand each leftover bit to a neighbour (below, as a sliver)
        for p in (left.geoms if hasattr(left, "geoms") else [left]):
            if p.geom_type == "Polygon" and p.area > 0:
                pieces.append([-1, p])
        # fold slivers into the touching piece they share the most edge with
        big = [q for q in pieces if q[0] >= 0 and q[1].area >= a.min_piece] or \
            [[max((q for q in pieces if q[0] >= 0), key=lambda q: q[1].area, default=[0])[0], geom]]
        if len(big) == 1 and big[0][1] is geom:
            pieces = big
        for q in pieces:
            if q in big:
                continue
            qb = q[1].buffer(0.05)
            best = max(big, key=lambda b: qb.intersection(b[1]).area if qb.intersects(b[1]) else 0)
            best[1] = unary_union([best[1], q[1]])
        out = []
        for z, p in sorted(big, key=lambda q: q[0]):
            k = counter.get(layer, 0); counter[layer] = k + 1
            nid = "%s_%d" % (layer, k)
            d = to_d(p)
            if not d:
                continue
            t = tag.replace('id="%s"' % pid, 'id="%s" inkscape:label="%s %s"' % (nid, layer, HZ.zone_label(z)))
            out.append(t.replace(dm.group(0), ' d="%s"' % d))
            rows.append((nid, pid, layer, z, HZ.zone_tag(z), HZ.zone_label(z), round(p.area)))
        return "\n".join(out)

    new = re.sub(r"<path\b[^>]*/>", repl, s)
    open(a.out, "w", encoding="utf8").write(new)
    base = os.path.splitext(a.out)[0]
    with open(base + "_zones.csv", "w", newline="") as f:
        w = csv.writer(f); w.writerow(["path_id", "was", "layer", "zone", "tag", "zone_label", "area_m2"]); w.writerows(rows)
    old_area = {L: sum(evenodd(rings_of(d)).area for pid, d in re.findall(r'<path\b[^>]*?id="(%s_\d+)"[^>]*?\sd="([^"]+)"' % L, s)) for L in SPLIT}
    new_area = {L: sum(r[-1] for r in rows if r[2] == L) for L in SPLIT}
    print("wrote %s: %s" % (a.out, ", ".join("%s %d -> %d paths" % (L, len(re.findall(r'id="%s_\d+"' % L, s)), counter.get(L, 0)) for L in SPLIT)))
    print("area check (m2): " + ", ".join("%s %.0f -> %.0f" % (L, old_area[L], new_area[L]) for L in SPLIT))

    if os.path.exists(a.satellite):
        bg = Image.open(a.satellite).convert("RGBA").resize((N, N))
        ov = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d2 = ImageDraw.Draw(ov)
        rng = np.random.default_rng(5); pal = {}
        for m in re.finditer(r'<path\b[^>]*?id="(\w+?)_\d+"[^>]*?\sd="([^"]+)"[^>]*/>', new):
            lay = m.group(1)
            lab = re.search(r'inkscape:label="([^"]+)"', m.group(0))
            if lay in SPLIT:
                key = lab.group(1).split(" ", 1)[1] if lab else lay
                col = (255, 60, 200, 170) if key.startswith("between") else pal.setdefault(key, tuple(int(v) for v in rng.integers(40, 255, 3)) + (140,))
            else:
                col = {"fairway": (60, 230, 90, 200), "green": (190, 240, 160, 230), "tee": (160, 230, 180, 230), "bunker": (240, 240, 170, 230)}.get(lay, (0, 0, 255, 150))
            for r in rings_of(m.group(2)):
                d2.polygon([(x / a.res, y / a.res) for x, y in r], fill=col, outline=(255, 255, 255, 255))
        im = Image.alpha_composite(bg, ov)
        al = np.asarray(ov)[..., 3] > 0; ys, xs = np.nonzero(al)
        im = im.crop((max(xs.min() - 30, 0), max(ys.min() - 30, 0), xs.max() + 30, ys.max() + 30))
        im.thumbnail((2400, 2400)); im.convert("RGB").save(base + "_preview.png")
        print("wrote", base + "_preview.png")


if __name__ == "__main__":
    main()
