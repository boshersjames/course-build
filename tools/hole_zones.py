"""Hole-zone map: give every metre of the course to a hole, a side of that hole, or a band between two holes.

OPCD cuts the rough and deep rough into a square grid, so mesh names (Spline_Rough_Outer_2_3_piece245, Spline_rough_0_7)
say nothing about holes. Planters and edits should target a zone instead: "hole 3 left", "between 3 and 4".

Inputs (paths relative to the course folder, or flags):
  --positions  greenkeeper_positions.csv  (hole,par,what,yards,x,y,z in the Unity frame: tees, AimPoint1/2, GreenCenterPoint)
  --svg        the OPCD course SVG (M/L/Z paths; x east, y = size - z)
Outputs in --out:
  hole_zones.png        zone id per metre, north up (row 0 = z max), 16-bit greyscale; id table in hole_zones.json
  hole_zones.json       {"size", "res", "zones": {id: {"hole", "side", "other", "label", "area_m2"}}}
  hole_zones_preview.png  zones over the satellite (if --satellite is given)

Zone ids: 0 = outside every hole (> --max-dist), h*10 + 1 = hole h left of the line of play, h*10 + 2 = right,
1000 + a*100 + b = the band between holes a < b (where the two distances differ by less than --band metres).
Only cells on the course (any SVG layer) get a zone unless --all-ground is set.
"""
import argparse, csv, json, os, re, sys
import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw

SVG_COURSE_LAYERS = ("rough", "deep", "fairway", "tee", "green", "bunker", "water")


def read_positions(path):
    holes = {}
    for r in csv.DictReader(open(path, newline="")):
        try:
            h = int(r["hole"]); p = (float(r["x"]), float(r["z"]))
        except (ValueError, KeyError):
            continue
        holes.setdefault(h, {"tees": [], "aims": {}, "green": None})
        w = r["what"]
        if w.startswith("AimPoint"):
            holes[h]["aims"][w] = p
        elif w == "GreenCenterPoint":
            holes[h]["green"] = p
        elif not w.startswith("Pin"):
            holes[h]["tees"].append(p)
    out = {}
    for h, d in holes.items():
        if d["green"] is None or not d["tees"]:
            print("hole %d: no tees or green centre, skipped" % h); continue
        aims = [d["aims"][k] for k in sorted(d["aims"])]
        tees = list(dict.fromkeys(d["tees"]))
        first = aims[0] if aims else d["green"]
        back = max(tees, key=lambda t: np.hypot(t[0] - first[0], t[1] - first[1]))
        lines = [[back] + aims + [d["green"]]]
        lines += [[t, first] for t in tees if t != back]          # forward boxes join the line of play at the first aim
        out[h] = lines
    return out


def read_svg(path, size, res):
    """Rasterise the course layers. Returns a bool grid, row 0 = svg y 0 (= north edge)."""
    s = open(path, encoding="utf8", errors="ignore").read()
    N = int(round(size / res))
    img = Image.new("L", (N, N), 0); drw = ImageDraw.Draw(img)
    n = 0
    for m in re.finditer(r'<path\b[^>]*>', s):
        tag = m.group(0)
        pid = re.search(r'\bid="([^"]+)"', tag); d = re.search(r'\sd="([^"]+)"', tag)
        if not pid or not d or re.sub(r"[_\d].*", "", pid.group(1)).lower() not in SVG_COURSE_LAYERS:
            continue
        for sub in re.split(r"[Mm]", d.group(1)):
            pts = re.findall(r"(-?[\d.]+)[ ,](-?[\d.]+)", sub)
            if len(pts) > 2:
                drw.polygon([(float(x) / res, float(y) / res) for x, y in pts], fill=255); n += 1
    print("course area from %d SVG rings" % n)
    # evenodd holes are filled too: fine, a hole in the rough is still course ground
    return np.asarray(img) > 0


def draw_lines(lines_px, N):
    img = Image.new("L", (N, N), 0); drw = ImageDraw.Draw(img)
    for L in lines_px:
        drw.line([tuple(q) for q in L], fill=255, width=1)
    return np.asarray(img) > 0


def side_of(main_px, idx):
    """+1 left / -1 right of the nearest segment of the main line (pixel coords, rows grow south)."""
    L = np.array(main_px, float)
    px = idx[1] + 0.5; pz = -(idx[0] + 0.5)                      # flip rows so the cross product reads north-up
    best = np.full(px.shape, np.inf); sgn = np.zeros(px.shape)
    for a, b in zip(L[:-1], L[1:]):
        a = np.array([a[0], -a[1]]); b = np.array([b[0], -b[1]])
        ab = b - a; l2 = max(ab @ ab, 1e-9)
        t = np.clip(((px - a[0]) * ab[0] + (pz - a[1]) * ab[1]) / l2, 0, 1)
        d = np.hypot(px - (a[0] + t * ab[0]), pz - (a[1] + t * ab[1]))
        cross = ab[0] * (pz - a[1]) - ab[1] * (px - a[0])          # > 0: left when looking down the hole
        upd = d < best
        best[upd] = d[upd]; sgn[upd] = np.where(cross[upd] >= 0, 1, -1)
    return sgn


def zone_grid(holes_px, N, res, course, band=12.0, max_dist=150.0):
    """holes_px: {hole: [main polyline, extra polylines...]} in pixel coords (x east, row south).
    Returns the uint16 zone grid (ids as in the module docstring)."""
    hs = sorted(holes_px)
    D = np.stack([ndimage.distance_transform_edt(~draw_lines(holes_px[h], N)) * res for h in hs])
    order = np.argsort(D, axis=0)
    i1 = order[0]; i2 = order[1] if len(hs) > 1 else order[0]
    d1 = np.take_along_axis(D, order[:1], 0)[0]
    d2 = np.take_along_axis(D, order[1:2], 0)[0] if len(hs) > 1 else np.full(d1.shape, np.inf)
    zone = np.zeros(d1.shape, np.uint16)
    near = course & (d1 <= max_dist)
    between = near & (d2 - d1 < band)
    own = near & ~between
    H = np.array(hs)
    for k, h in enumerate(hs):
        idx = np.nonzero(own & (i1 == k))
        zone[idx] = np.where(side_of(holes_px[h][0], idx) > 0, h * 10 + 1, h * 10 + 2)
    ha, hb = np.minimum(H[i1], H[i2]), np.maximum(H[i1], H[i2])
    zone[between] = (1000 + ha * 100 + hb)[between]
    return zone


def zone_label(z):
    if z == 0:
        return "outside"
    if z >= 1000:
        return "between %d and %d" % ((z - 1000) // 100, (z - 1000) % 100)
    return "hole %d %s" % (z // 10, "left" if z % 10 == 1 else "right")


def zone_tag(z):
    """Short id part for SVG path ids: h03L, h03R, h03x04, out."""
    if z == 0:
        return "out"
    if z >= 1000:
        return "h%02dx%02d" % ((z - 1000) // 100, (z - 1000) % 100)
    return "h%02d%s" % (z // 10, "L" if z % 10 == 1 else "R")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--positions", default="greenkeeper/greenkeeper_positions.csv")
    p.add_argument("--svg", required=True)
    p.add_argument("--size", type=float, default=None, help="frame size in m (default: course.json frame.square_m)")
    p.add_argument("--res", type=float, default=1.0)
    p.add_argument("--band", type=float, default=12.0, help="width of the between-holes band (m of distance difference)")
    p.add_argument("--max-dist", type=float, default=150.0, help="cells further than this from every hole are 'outside'")
    p.add_argument("--all-ground", action="store_true", help="zone the whole frame, not just the SVG course area")
    p.add_argument("--satellite", default=None)
    p.add_argument("--out", default="hole_zones")
    a = p.parse_args()
    size = a.size
    if size is None:
        c = json.load(open("course.json"))
        size = float(c.get("frame", {}).get("square_m") or c.get("size"))
    holes = read_positions(a.positions)
    if not holes:
        sys.exit("no holes in %s" % a.positions)
    hs = sorted(holes)
    print("holes:", ", ".join(map(str, hs)))
    N = int(round(size / a.res))
    course = np.ones((N, N), bool) if a.all_ground else read_svg(a.svg, size, a.res)
    holes_px = {h: [[(x / a.res, (size - z) / a.res) for x, z in L] for L in Ls] for h, Ls in holes.items()}
    zone = zone_grid(holes_px, N, a.res, course, a.band, a.max_dist)
    os.makedirs(a.out, exist_ok=True)
    Image.fromarray(zone.astype(np.uint16)).save(os.path.join(a.out, "hole_zones.png"))
    ids, counts = np.unique(zone, return_counts=True)
    zones = {}
    for z, n in zip(ids.tolist(), counts.tolist()):
        if z == 0:
            info = {"hole": None, "side": None, "other": None}
        elif z >= 1000:
            info = {"hole": (z - 1000) // 100, "side": "between", "other": (z - 1000) % 100}
        else:
            info = {"hole": z // 10, "side": "left" if z % 10 == 1 else "right", "other": None}
        info["label"] = zone_label(z); info["tag"] = zone_tag(z)
        info["area_m2"] = round(n * a.res * a.res)
        zones[str(z)] = info
    json.dump({"size": size, "res": a.res, "frame": "Unity x east, z north; png row 0 = z max",
               "band_m": a.band, "max_dist_m": a.max_dist, "zones": zones},
              open(os.path.join(a.out, "hole_zones.json"), "w"), indent=1)
    print("%d zones; between-hole bands: %s" % (len(zones), ", ".join(v["label"][8:] for v in zones.values() if v["side"] == "between")))

    if a.satellite:
        rng = np.random.default_rng(7)
        pal = {z: rng.integers(40, 255, 3) for z in ids.tolist()}
        rgba = np.zeros(zone.shape + (4,), np.uint8)
        for z in ids.tolist():
            if z == 0:
                continue
            m = zone == z
            col = np.array([255, 60, 200]) if z >= 1000 else pal[z]
            rgba[m, :3] = col; rgba[m, 3] = 120 if z < 1000 else 170
        edge = (ndimage.grey_dilation(zone, 3) != ndimage.grey_erosion(zone, 3)) & (zone > 0)
        rgba[edge] = (255, 255, 255, 230)
        base = Image.open(a.satellite).convert("RGBA").resize(zone.shape[::-1])
        im = Image.alpha_composite(base, Image.fromarray(rgba, "RGBA"))
        drw = ImageDraw.Draw(im)
        for h in hs:
            for L in holes[h]:
                drw.line([(x / a.res, (size - z) / a.res) for x, z in L], fill=(255, 255, 0, 255), width=2)
            g = holes[h][0][-1]
            drw.text((g[0] / a.res + 6, (size - g[1]) / a.res - 6), str(h), fill=(255, 255, 0, 255))
        im.convert("RGB").save(os.path.join(a.out, "hole_zones_preview.png"))
        print("wrote", os.path.join(a.out, "hole_zones_preview.png"))


if __name__ == "__main__":
    main()
