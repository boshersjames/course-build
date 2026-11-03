"""Fix course.svg gaps found against the drone video (drone/svg_check/README.md), then rewrite course.svg.

    python tools/svg_fix.py            # writes course.svg from the original (kept as course_before_fix.svg) and svg_fix_preview/Hxx.jpg
    python tools/svg_fix.py --dry      # previews only

Works on the SVG itself (no OSM): the layers are rasterised at 0.5 m, one class per pixel (same priority as
coursekit: rough < deep < fairway < tee < green < bunker < water), changed, and traced back out.
  fairway gaps : looser photo test (smooth, green turf) in a corridor from the back tee, kept only where it
                 touches the existing fairway or a tee box
  ragged edges : close + open (3 m) of the fairway inside the hole's corridor
  bunkers      : bright, grey sand pieces (15-400 m2) next to the hole's turf
  ponds        : dark blue water pieces next to the hole's turf
"""
import csv, os, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from skimage import measure

R = 0.5
S = 3000
N = int(S / R)
# Clender palette and layer names, exactly as coursekit.SVG_LAYERS writes them (the OPCD web app reads the colours)
LAYERS = [("Rough", "rough", "#278438"), ("Deep", "deep", "#164B20"), ("Fairway", "fairway", "#43E561"),
          ("Tee", "tee", "#A0E5B8"), ("Green", "green", "#BCE5A4"), ("Bunker", "bunker", "#E5E5AA"), ("Water", "water", "#0000C0")]
ROUGH, DEEP, FAIRWAY, TEE, GREEN, BUNKER, WATER = range(1, 8)

# (hole 1-27: Ridgeline 1-9 = 1-9, Creekside = 10-18, Canyon Crest = 19-27, what, settings). Distances in metres; "upto" = along the line from the back tee.
STREAM = [(1373.54, 1801.74), (1368.97, 1802.29), (1365.07, 1803.91), (1360.61, 1803.15), (1356.88, 1804.19), (1351.03, 1805.86),
          (1345.38, 1806.39), (1339.99, 1806.96), (1335.06, 1808.29), (1328.04, 1811.93), (1324.75, 1814.42), (1319.32, 1815.1),
          (1316.58, 1815.71), (1313.53, 1813.11), (1311.5, 1809.05), (1308.87, 1806.53), (1305.42, 1804.46), (1302.12, 1800.7),
          (1297.41, 1796.41)]   # stops ~6 m short of the lake so it keeps its own sloping water surface

# Tried on every gap in drone/svg_check/README.md; only these hold up against the satellite. The others
# (H01, H07, H16, H24 tee ends, H03 approach) are scrub carries or rough in the photo, and the H08 bunkers and
# H19 pond don't show in it, so they're left for a check in Inkscape.
FIXES = [
    # Canyon Crest 2 (20): mown strip from the forward tee up past the fairway bunkers, traced by hand off the photo
    (20, "poly", dict(code=3, pts=[(1897.9, 1728.4), (1926.8, 1723.0), (1919.7, 1701.2), (1912.1, 1674.0), (1905.0, 1646.8),
                                   (1895.7, 1635.9), (1883.2, 1638.7), (1876.7, 1657.7), (1872.9, 1679.5), (1875.1, 1706.7), (1886.0, 1723.0)])),
    # Ridgeline 9: the stream / waterfall that runs past the back of the green into the lake (OSM waterway=stream; the
    # drone frames show water and rocks there), 3 m wide
    (9, "line", dict(code=7, width=3.0, pts=STREAM)),
    (2, "smooth", dict(width=40)),
    (22, "smooth", dict(width=40)),
]


def load_layers(path):
    svg = open(path, encoding="utf-8").read()
    lab = np.zeros((N, N), np.uint8)
    for code, (title, pid, _) in enumerate(LAYERS, start=1):
        i0 = svg.find('id="layer_%s"' % pid)
        if i0 < 0:
            continue
        block = svg[i0:svg.find("</g>", i0)]
        m = np.zeros((N, N), bool)
        for d in re.findall(r' d="([^"]+)"', block):
            for sub in re.split(r"[Zz]", d):
                nums = [float(v) for v in re.findall(r"-?\d+\.?\d*", sub)]
                pts = [(nums[i] / R, nums[i + 1] / R) for i in range(0, len(nums) - 1, 2)]
                if len(pts) > 2:
                    xs = [q[0] for q in pts]; ys = [q[1] for q in pts]
                    a0, b0 = max(int(min(xs)) - 1, 0), max(int(min(ys)) - 1, 0)
                    a1, b1 = min(int(max(xs)) + 2, N), min(int(max(ys)) + 2, N)
                    im = Image.new("L", (a1 - a0, b1 - b0), 0)
                    ImageDraw.Draw(im).polygon([(x - a0, y - b0) for x, y in pts], fill=255)
                    m[b0:b1, a0:a1] ^= np.asarray(im) > 0   # even-odd: holes in a path cut out
        lab[m] = code
    return lab


def write_svg(lab, path):
    out = ['<?xml version="1.0" encoding="UTF-8" standalone="no"?>',
           '<svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" '
           'xmlns:sodipodi="http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd" width="%dmm" height="%dmm" viewBox="0 0 %d %d" version="1.1">' % (S, S, S, S)]
    counts = {}
    for code, (title, pid, col) in enumerate(LAYERS, start=1):
        out.append('<g inkscape:groupmode="layer" id="layer_%s" inkscape:label="%s">' % (pid, title))
        m = lab == code
        comp, _ = ndimage.label(m)
        idx = 0
        for i, s_ in enumerate(ndimage.find_objects(comp), start=1):
            if s_ is None:
                continue
            sub = np.pad((comp[s_] == i).astype(float), 1)
            if sub.sum() * R * R < (150 if code in (DEEP, FAIRWAY) else 25):
                continue
            loops = []
            for cont in measure.find_contours(sub, 0.5):
                cont = measure.approximate_polygon(cont, tolerance=0.15 / R)
                if len(cont) < 4:
                    continue
                ys = (cont[:, 0] - 1 + s_[0].start + 0.5) * R
                xs = (cont[:, 1] - 1 + s_[1].start + 0.5) * R
                loops.append("M " + " L ".join("%.3f,%.3f" % (x, y) for x, y in zip(xs, ys)) + " Z")
            if loops:
                out.append('<path id="%s_%d" style="fill:%s;fill-opacity:1;fill-rule:evenodd;stroke:none" d="%s"/>' % (pid, idx, col, " ".join(loops)))
                idx += 1
        counts[title] = idx
        out.append("</g>")
    out.append("</svg>")
    open(path, "w", encoding="utf-8").write("\n".join(out))
    return counts


def hole_geometry(h, holes, tees):
    """Pixel grids of distance to the back-tee -> green line and distance along it (metres)."""
    g = np.array([float(holes[h]["green_x"]), float(holes[h]["green_z"])])
    t = np.array(tees[h][0])                               # box 1 = back tee
    L = np.linalg.norm(g - t)
    d = (g - t) / L
    X = ((np.arange(N, dtype=np.float32) + 0.5) * R)[None, :]
    Z = (S - (np.arange(N, dtype=np.float32) + 0.5) * R)[:, None]
    along = (X - t[0]) * d[0] + (Z - t[1]) * d[1]
    across = np.abs((X - t[0]) * d[1] - (Z - t[1]) * d[0])
    return along, across, L


def main(dry):
    repo = os.getcwd()
    holes = {int(r["hole"]): r for r in csv.DictReader(open("holes.csv"))}
    tees = {}
    for r in csv.DictReader(open("tees.csv")):
        tees.setdefault(int(r["hole"]), []).append((float(r["x"]), float(r["z"])))
    print("Reading course.svg ...")
    src = "course_before_fix.svg" if os.path.exists("course_before_fix.svg") else "course.svg"   # always fix the original
    lab = load_layers(src)
    before = lab.copy()
    sat = np.asarray(Image.open("satellite.jpg").convert("RGB").resize((N, N), Image.BILINEAR), dtype=np.float32)
    lum = sat.mean(2)
    gr = sat[..., 1] / np.maximum(sat.sum(2), 1)
    k = 15
    tex = np.sqrt(np.maximum(ndimage.uniform_filter(lum ** 2, k) - ndimage.uniform_filter(lum, k) ** 2, 0))
    grs = ndimage.uniform_filter(gr, k)
    sat_ = sat.max(2) - sat.min(2)
    feats = np.isin(lab, (TEE, GREEN, BUNKER, WATER))
    it = int(round(3 / R))
    report = []
    for h, what, p in FIXES:
        along, across, L = hole_geometry(h, holes, tees)
        if what == "poly":
            im = Image.new("L", (N, N), 0)
            ImageDraw.Draw(im).polygon([(x / R, (S - z) / R) for x, z in p["pts"]], fill=255)
            add = (np.asarray(im) > 0) & np.isin(lab, (0, ROUGH, DEEP))
            lab[add] = p["code"]
            report.append("H%02d %s drawn: +%.0f m2" % (h, LAYERS[p["code"] - 1][0].lower(), add.sum() * R * R))
        elif what == "line":
            im = Image.new("L", (N, N), 0)
            ImageDraw.Draw(im).line([(x / R, (S - z) / R) for x, z in p["pts"]], fill=255, width=max(1, int(round(p["width"] / R))), joint="curve")
            add = (np.asarray(im) > 0) & ~np.isin(lab, (GREEN, TEE, BUNKER))
            lab[add] = p["code"]
            report.append("H%02d %s channel drawn: +%.0f m2" % (h, LAYERS[p["code"] - 1][0].lower(), add.sum() * R * R))
        elif what == "gap":
            lo = L - p["from_green"] if "from_green" in p else -15
            region = (across < p["width"]) & (along > lo) & (along < min(p["upto"], L - 10))
            cand = region & (tex < 7.5) & (grs >= 0.345) & ~ndimage.binary_dilation(feats, iterations=2)
            cand = ndimage.binary_closing(ndimage.binary_opening(cand, iterations=it), iterations=it)
            anchor = ndimage.binary_dilation((lab == FAIRWAY) | (lab == TEE), iterations=int(4 / R))
            comp, _ = ndimage.label(cand)
            keep = np.isin(comp, np.unique(comp[anchor & (comp > 0)])) & (comp > 0)
            keep = ndimage.binary_fill_holes(ndimage.binary_closing(keep | (region & (lab == FAIRWAY)), iterations=int(3 / R))) & region
            add = keep & np.isin(lab, (0, ROUGH, DEEP))
            lab[add] = FAIRWAY
            report.append("H%02d fairway gap: +%.0f m2" % (h, add.sum() * R * R))
        elif what == "smooth":
            region = (across < p["width"]) & (along > -10) & (along < L + 10)
            fw = (lab == FAIRWAY) & region
            r_ = int(3 / R)
            sm = ndimage.binary_opening(ndimage.binary_closing(fw, iterations=r_), iterations=r_)
            sm = ndimage.binary_fill_holes(sm)
            add = sm & ~fw & np.isin(lab, (0, ROUGH, DEEP))
            cut = fw & ~sm
            lab[add] = FAIRWAY
            lab[cut] = ROUGH
            report.append("H%02d fairway smoothed: +%.0f m2, -%.0f m2" % (h, add.sum() * R * R, cut.sum() * R * R))
        elif what == "bunkers":
            turf = np.isin(lab, (FAIRWAY, GREEN))
            region = (across < p["width"]) & (along > 0) & (along < L + 25) & ndimage.binary_dilation(turf, iterations=int(6 / R))
            sand = region & (lum > 175) & (sat_ < 45) & (lab != BUNKER) & (lab != WATER) & (lab != TEE)
            sand = ndimage.binary_opening(sand, iterations=2)
            comp, n = ndimage.label(sand)
            sizes = ndimage.sum(sand, comp, range(1, n + 1)) * R * R
            ok = [i + 1 for i, s in enumerate(sizes) if 15 <= s <= 400]
            add = ndimage.binary_closing(np.isin(comp, ok), iterations=2)
            lab[add] = BUNKER
            report.append("H%02d bunkers: +%d (%.0f m2)" % (h, len(ok), add.sum() * R * R))
        elif what == "pond":
            region = (across < p["width"]) & (along > L - p["from_green"]) & (along < L)
            blue = region & (sat[..., 2] > sat[..., 1]) & (lum < 110) & (lab != WATER)
            blue = ndimage.binary_opening(blue, iterations=2)
            comp, n = ndimage.label(blue)
            sizes = ndimage.sum(blue, comp, range(1, n + 1)) * R * R
            ok = [i + 1 for i, s in enumerate(sizes) if 20 <= s <= 3000]
            add = ndimage.binary_fill_holes(ndimage.binary_closing(np.isin(comp, ok), iterations=2))
            lab[add] = WATER
            report.append("H%02d pond: +%d (%.0f m2)" % (h, len(ok), add.sum() * R * R))
        preview(h, holes, tees, before, lab, sat)
    print("\n".join(report))
    if not dry:
        if not os.path.exists("course_before_fix.svg"):
            os.replace("course.svg", "course_before_fix.svg")
        print("Wrote course.svg:", write_svg(lab, "course.svg"))
    open(os.path.join("svg_fix_preview", "report.txt"), "w").write("\n".join(report) + "\n")


def preview(h, holes, tees, before, lab, sat):
    os.makedirs("svg_fix_preview", exist_ok=True)
    pts = tees[h] + [(float(holes[h]["green_x"]), float(holes[h]["green_z"]))]
    x0, x1 = min(p[0] for p in pts) - 60, max(p[0] for p in pts) + 60
    z0, z1 = min(p[1] for p in pts) - 60, max(p[1] for p in pts) + 60
    w = max(x1 - x0, z1 - z0); cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    c0, c1 = int((cx - w / 2) / R), int((cx + w / 2) / R)
    r0, r1 = int((S - cz - w / 2) / R), int((S - cz + w / 2) / R)
    c0, r0 = max(c0, 0), max(r0, 0)
    base = sat[r0:r1, c0:c1].astype(np.uint8)
    tiles = []
    for L_ in (before, lab):
        img = base.copy()
        sub = L_[r0:r1, c0:c1]
        for code, col in ((FAIRWAY, (255, 255, 0)), (GREEN, (0, 255, 0)), (TEE, (0, 200, 255)), (BUNKER, (255, 255, 255)), (WATER, (0, 80, 255))):
            m = sub == code
            edge = m & ~ndimage.binary_erosion(m, iterations=2)
            img[edge] = col
        tiles.append(Image.fromarray(img).resize((800, 800)))
    out = Image.new("RGB", (1600, 800))
    out.paste(tiles[0], (0, 0)); out.paste(tiles[1], (800, 0))
    d = ImageDraw.Draw(out)
    f = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 26) if os.name == "nt" else None
    d.text((10, 10), "H%02d before" % h, fill=(255, 255, 255), font=f, stroke_width=2, stroke_fill=(0, 0, 0))
    d.text((810, 10), "H%02d after" % h, fill=(255, 255, 255), font=f, stroke_width=2, stroke_fill=(0, 0, 0))
    out.save(os.path.join("svg_fix_preview", "H%02d.jpg" % h), quality=85)


if __name__ == "__main__":
    main("--dry" in sys.argv)
