"""Headless LiDAR detail review (D1-D18) for a course folder. Writes a JSON of per-hole measurements to stdout path arg."""
import sys, os, re, csv, json, math
sys.path.insert(0, '/home/user/course-build/tools')
os.chdir('/home/user/hansen-dam')
import numpy as np
from scipy import ndimage
from matplotlib.path import Path
from PIL import Image
import xml.etree.ElementTree as ET
from pyproj import Transformer
from course_io import course, Heightmap, GKD, svg_outlines, match_outlines_to_holes
from rules import rules

OUT = sys.argv[1]
C = course(); HM = Heightmap(C); G = GKD(C); R = rules()
LR = R['lidar_review']
SQ = C['frame']['square_m']

# ---------- outlines ----------
greens = svg_outlines(C, 'green')
# detect flip once via greens
gc = np.array([h.green_centre for h in G.holes])
def _score(flip):
    tot = 0
    for pts in greens.values():
        c = pts.mean(0)
        tot += np.min(np.hypot(*(gc - c).T))
    return tot
# greens already flipped by autodetect; use the same flip for the others by re-reading raw
raw_greens = svg_outlines(C, 'green', flip_y=False)
flip = abs(list(greens.values())[0][0, 1] - (SQ - list(raw_greens.values())[0][0, 1])) < 1e-6
bunk = svg_outlines(C, 'bunker', flip_y=flip); tees_svg = svg_outlines(C, 'tee', flip_y=flip)
fw = svg_outlines(C, 'fairway', flip_y=flip); water = svg_outlines(C, 'water', flip_y=flip)
deep = svg_outlines(C, 'deep', flip_y=flip); rough = svg_outlines(C, 'rough', flip_y=flip)
green_by_hole = match_outlines_to_holes(greens, G)

def poly_area(p):
    x, y = p[:, 0], p[:, 1]
    return 0.5 * abs(np.dot(x, np.roll(y, 1)) - np.dot(y, np.roll(x, 1)))

def inside(poly, X, Z):
    return Path(poly).contains_points(np.c_[X.ravel(), Z.ravel()]).reshape(X.shape)

def dist_to_polyline(P, line):
    """distance from points P (N,2) to polyline (M,2)"""
    d = np.full(len(P), np.inf)
    for a, b in zip(line[:-1], line[1:]):
        ab = b - a; L2 = ab @ ab
        t = np.clip(((P - a) @ ab) / (L2 if L2 > 0 else 1), 0, 1)
        proj = a + t[:, None] * ab
        d = np.minimum(d, np.hypot(*(P - proj).T))
    return d

def dist_to_polygon_edge(P, poly):
    return dist_to_polyline(P, np.vstack([poly, poly[:1]]))

# ---------- surfaces ----------
S05 = np.load('surfaces/surface_05m.npz')
Sh = S05['h'].astype(float); Sh[Sh == 0] = np.nan; Sh /= 100.0
Sx0 = float(S05['x0']); Sz0 = float(S05['z0']); Sres = float(S05['res'])
def surf(X, Z):
    c = np.round((np.asarray(X) - Sx0) / Sres).astype(int); r = np.round((np.asarray(Z) - Sz0) / Sres).astype(int)
    ok = (c >= 0) & (c < Sh.shape[1]) & (r >= 0) & (r < Sh.shape[0])
    out = np.full(np.shape(X), np.nan)
    out[ok] = Sh[r[ok], c[ok]]
    return out
# slope of 0.5 m surface (smoothed 0.5 m sigma) in percent
Sh_f = Sh.copy(); nanm = np.isnan(Sh_f); Sh_f[nanm] = 0
w = ndimage.gaussian_filter((~nanm).astype(float), 1.0); Sh_s = ndimage.gaussian_filter(Sh_f, 1.0) / np.where(w > 0, w, 1)
Sh_s[w < 0.3] = np.nan
gz, gx = np.gradient(Sh_s, Sres)
Sslope_pct = np.hypot(gx, gz) * 100
def surf_slope(X, Z):
    c = np.round((np.asarray(X) - Sx0) / Sres).astype(int); r = np.round((np.asarray(Z) - Sz0) / Sres).astype(int)
    ok = (c >= 0) & (c < Sh.shape[1]) & (r >= 0) & (r < Sh.shape[0])
    out = np.full(np.shape(X), np.nan); out[ok] = Sslope_pct[r[ok], c[ok]]; return out

# heightmap slope in degrees
hz, hx = np.gradient(HM.H, HM.S)
Hslope_deg = np.degrees(np.arctan(np.hypot(hx, hz)))
def hm_slope(X, Z):
    return ndimage.map_coordinates(Hslope_deg, [np.asarray(Z) / HM.S, np.asarray(X) / HM.S], order=1, mode='nearest')

# ---------- satellite ----------
Image.MAX_IMAGE_PIXELS = None
sat = np.asarray(Image.open('satellite.jpg').convert('RGB'))
SPX = sat.shape[0]
def sat_rgb(X, Z):
    c = np.clip((np.asarray(X) / SQ * SPX).astype(int), 0, SPX - 1); r = np.clip(((SQ - np.asarray(Z)) / SQ * SPX).astype(int), 0, SPX - 1)
    return sat[r, c].astype(float)
def tone_class(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    bright = rgb.mean(-1)
    green = (g > r * 1.03) & (g > b * 1.1)
    pale = (bright > 170) & (np.abs(r - g) < 25) & (np.abs(g - b) < 30)
    tan = (~green) & (~pale) & (r > g) & (g > b) & (bright > 95)
    dark = (~green) & (~pale) & (~tan)
    return green, tan, dark, pale

# ---------- OSM ----------
tr = Transformer.from_crs('EPSG:4326', 'EPSG:%d' % C['epsg'], always_xy=True)
nodes = {}; ways = []; node_tags = {}
for ev, el in ET.iterparse('map.osm', events=('end',)):
    if el.tag == 'node':
        e, n = tr.transform(float(el.get('lon')), float(el.get('lat')))
        nodes[el.get('id')] = (e - C['sw_e'], n - C['sw_n'])
        tags = {t.get('k'): t.get('v') for t in el.findall('tag')}
        if tags: node_tags[el.get('id')] = tags
    elif el.tag == 'way':
        tags = {t.get('k'): t.get('v') for t in el.findall('tag')}
        nds = [nd.get('ref') for nd in el.findall('nd')]
        ways.append((el.get('id'), tags, nds))
    if el.tag in ('node', 'way'): el.clear()
def way_pts(nds):
    return np.array([nodes[n] for n in nds if n in nodes])
osm_water = [(i, t, way_pts(n)) for i, t, n in ways if 'waterway' in t or t.get('natural') == 'water']
osm_build = [(i, t, way_pts(n)) for i, t, n in ways if 'building' in t]
osm_roads = [(i, t, way_pts(n)) for i, t, n in ways if t.get('highway') in ('primary', 'secondary', 'tertiary', 'residential', 'service')]
osm_props = [(i, t, np.array([nodes[i]])) for i, t in node_tags.items() if i in nodes and (t.get('amenity') or t.get('man_made') or t.get('leisure') in ('picnic_table',))]
osm_props += [(i, t, way_pts(n)) for i, t, n in ways if t.get('amenity') in ('toilets', 'shelter', 'parking') or t.get('man_made')]

# ---------- CSVs ----------
trees = np.genfromtxt('trees.csv', delimiter=',', names=True, dtype=None, encoding='utf-8')
shrubs = np.genfromtxt('shrubs_clean.csv', delimiter=',', names=True, dtype=None, encoding='utf-8')
bcsv = list(csv.DictReader(open('bunkers.csv')))
cart = np.genfromtxt('cartpaths.csv', delimiter=',', names=True)
teecol = list(csv.DictReader(open('checks/tee_colours.csv')))
grep = {int(r['hole']): r for r in csv.DictReader(open('progress/green_report/green_report.csv'))}

# bunker outline -> bunkers.csv row
bunk_rows = {}
for bid, poly in bunk.items():
    c = poly.mean(0)
    best = min(bcsv, key=lambda r: math.hypot(float(r['x']) - c[0], float(r['z']) - c[1]))
    d = math.hypot(float(best['x']) - c[0], float(best['z']) - c[1])
    bunk_rows[bid] = (best, d)

res = {'holes': {}, 'course': {}}

# ---------- D13 registration ----------
# a) SVG green centroid vs GKD green centre
reg = []
for h in G.holes:
    p = green_by_hole.get(h.number)
    if p is None: continue
    c = p.mean(0); reg.append((h.number, float(c[0] - h.green_centre[0]), float(c[1] - h.green_centre[1])))
res['course']['svg_vs_gkd_green'] = reg
# b) cart paths (OSM) vs satellite: shift that maximises brightness under the path points
P = np.c_[cart['x'], cart['z']]
shifts = np.arange(-4, 4.01, 0.5)
best = None; grid = {}
for dx in shifts:
    for dz in shifts:
        b = sat_rgb(P[:, 0] + dx, P[:, 1] + dz).mean(-1).mean()
        grid[(dx, dz)] = b
        if best is None or b > best[2]: best = (dx, dz, b)
res['course']['cartpath_vs_sat'] = {'best_dx': best[0], 'best_dz': best[1], 'bright_best': round(best[2], 1), 'bright_zero': round(grid[(0.0, 0.0)], 1)}
# c) bunker outlines (OSM/SVG) vs satellite sand: shift maximising mean brightness inside outlines
allb = np.vstack([p for p in bunk.values()])
# sample points inside bunkers at 1 m
samp = []
for p in bunk.values():
    x0, z0 = p.min(0); x1, z1 = p.max(0)
    X, Z = np.meshgrid(np.arange(x0, x1, 1.0), np.arange(z0, z1, 1.0))
    m = inside(p, X, Z); samp.append(np.c_[X[m], Z[m]])
samp = np.vstack(samp)
best = None; g0 = None
for dx in shifts:
    for dz in shifts:
        b = sat_rgb(samp[:, 0] + dx, samp[:, 1] + dz).mean(-1).mean()
        if dx == 0 and dz == 0: g0 = b
        if best is None or b > best[2]: best = (dx, dz, b)
res['course']['bunker_vs_sat'] = {'best_dx': best[0], 'best_dz': best[1], 'bright_best': round(best[2], 1), 'bright_zero': round(g0, 1)}
# d) vertical: GKD tee y vs heightmap, vs 0.5 m surface
dv = []; dv2 = []
for h in G.holes:
    for k, t in h.playing_tees().items():
        p = t['Position']; dv.append(p['y'] - HM.sample(p['x'], p['z']))
        s = surf(np.array([p['x']]), np.array([p['z']]))[0]
        if not np.isnan(s): dv2.append(p['y'] - s)
res['course']['gkd_tee_y_minus_heightmap'] = {'mean': round(float(np.mean(dv)), 3), 'max_abs': round(float(np.max(np.abs(dv))), 3)}
res['course']['gkd_tee_y_minus_surface05'] = {'mean': round(float(np.mean(dv2)), 3), 'max_abs': round(float(np.max(np.abs(dv2))), 3), 'n': len(dv2)}
# e) OSM tee outlines vs GKD tee points: share of GKD tees inside an SVG tee outline
# ---------- D11 water ----------
wres = {}
for wid, poly in water.items():
    x0, z0 = poly.min(0) - 8; x1, z1 = poly.max(0) + 8
    X, Z = np.meshgrid(np.arange(x0, x1, 0.5), np.arange(z0, z1, 0.5))
    H = HM.sample(X, Z); m = inside(poly, X, Z)
    hin = H[m]; d = dist_to_polygon_edge(np.c_[X.ravel(), Z.ravel()], poly).reshape(X.shape)
    ring = H[(~m) & (d < 3)]
    hist, edges = np.histogram(hin, bins=np.arange(hin.min(), hin.max() + 0.1, 0.1))
    # lowest connected flat: cells within 0.15 m of p5
    level = float(np.percentile(hin, 5))
    below = (H <= level + 0.15)
    lab, n = ndimage.label(below & (d < 8))
    # component with most inside cells
    if n:
        counts = ndimage.sum(m, lab, range(1, n + 1)); k = int(np.argmax(counts)) + 1
        comp = lab == k
        ext_outside = float(comp[~m].sum() * 0.25); ext_inside = float(comp[m].sum() * 0.25)
    else:
        ext_outside = ext_inside = 0
    # GKD hazard (penalty area, innerOOB False) nearest
    hz = [h for h in G.data['Hazards'] if not h.get('innerOOB')]
    hzinfo = None
    for h in hz:
        pts = np.array([[c['x'], c['z']] for c in h['coords']]); ys = np.array([c['y'] for c in h['coords']])
        if np.hypot(*(pts.mean(0) - poly.mean(0))) < 60:
            dd = dist_to_polygon_edge(pts, poly)
            hzinfo = {'points': len(pts), 'y_median': round(float(np.median(ys)), 2), 'y_min': round(float(ys.min()), 2), 'edge_dist_mean': round(float(dd.mean()), 2), 'edge_dist_max': round(float(dd.max()), 2), 'hasDZ': h.get('hasDZ')}
    wres[wid] = {'area_m2': round(poly_area(poly)), 'centroid': [round(float(v), 1) for v in poly.mean(0)],
                 'h_inside_min': round(float(hin.min()), 2), 'h_inside_p5': round(level, 2), 'h_inside_median': round(float(np.median(hin)), 2), 'h_inside_max': round(float(hin.max()), 2),
                 'ring3m_median': round(float(np.median(ring)), 2), 'ring3m_p10': round(float(np.percentile(ring, 10)), 2),
                 'hist_0.1m': [int(v) for v in hist], 'hist_from': round(float(edges[0]), 2),
                 'low_flat_inside_m2': ext_inside, 'low_flat_outside_outline_m2': ext_outside, 'gkd_hazard': hzinfo}
res['course']['water'] = wres

# ---------- per hole ----------
def hole_line(h):
    return h.line()

turf_polys = list(fw.values()) + list(greens.values()) + list(tees_svg.values()) + list(bunk.values())

for h in G.holes:
    n = h.number; out = {}
    line = hole_line(h); gcen = h.green_centre
    gpoly = green_by_hole.get(n)
    # ----- corridor cells (heightmap res) -----
    x0, z0 = line.min(0) - 70; x1, z1 = line.max(0) + 70
    X, Z = np.meshgrid(np.arange(x0, x1, HM.S), np.arange(z0, z1, HM.S))
    Pc = np.c_[X.ravel(), Z.ravel()]
    dl = dist_to_polyline(Pc, line).reshape(X.shape)
    corr = dl <= 60
    turf = np.zeros_like(corr)
    for p in turf_polys:
        if p[:, 0].max() < x0 or p[:, 0].min() > x1 or p[:, 1].max() < z0 or p[:, 1].min() > z1: continue
        turf |= inside(p, X, Z)
    oop = corr & ~turf
    slope = hm_slope(X, Z)
    rgb = sat_rgb(X, Z); green_t, tan_t, dark_t, pale_t = tone_class(rgb)
    ncell = oop.sum(); cell_m2 = HM.S ** 2
    # shrubs / trees in corridor out of play
    sp = np.c_[shrubs['x'], shrubs['z']]; sin = dist_to_polyline(sp, line) <= 60
    tp = np.c_[trees['x'], trees['z']]; tin = dist_to_polyline(tp, line) <= 60
    steep15 = oop & (slope > LR['d1_slope_min_deg']); steep35 = oop & (slope > R['brush']['rock']['slope_min_deg'])
    out['D1'] = {'oop_area_ha': round(ncell * cell_m2 / 1e4, 2),
                 'green_tone_pct': round(100 * (oop & green_t).sum() / max(ncell, 1), 1),
                 'tan_pct': round(100 * (oop & tan_t).sum() / max(ncell, 1), 1),
                 'dark_pct': round(100 * (oop & dark_t).sum() / max(ncell, 1), 1),
                 'pale_pct': round(100 * (oop & pale_t).sum() / max(ncell, 1), 1),
                 'slope_gt15_pct': round(100 * steep15.sum() / max(ncell, 1), 1),
                 'slope_gt15_m2': round(steep15.sum() * cell_m2),
                 'shrubs_in_corridor': int(sin.sum()), 'shrubs_ge3m': int((sin & (shrubs['height_m'] >= 3)).sum()),
                 'trees_in_corridor': int(tin.sum()),
                 'slope_p90_deg': round(float(np.percentile(slope[oop], 90)), 1) if ncell else None,
                 'slope_max_deg': round(float(slope[oop].max()), 1) if ncell else None}
    # where is the steep ground (for the visit list): centroid of >15 cells
    if steep15.sum():
        cxy = np.c_[X[steep15], Z[steep15]]
        out['D1']['steep_centroid'] = [round(float(v)) for v in cxy.mean(0)]
        # along-hole position
        out['D1']['steep_side'] = 'left' if ((line[-1]-line[0])[0]*(cxy.mean(0)-line[0])[1] - (line[-1]-line[0])[1]*(cxy.mean(0)-line[0])[0]) > 0 else 'right'
    out['D3'] = {'gt35_cells': int(steep35.sum()), 'gt35_m2': round(steep35.sum() * cell_m2),
                 'gt35_pale_cells': int((steep35 & pale_t).sum()), 'gt35_with_shrub': None}
    if steep35.sum():
        cxy = np.c_[X[steep35], Z[steep35]]
        out['D3']['centroid'] = [round(float(v)) for v in cxy.mean(0)]
        dd = np.min(np.hypot(cxy[:, None, 0] - sp[None, :, 0], cxy[:, None, 1] - sp[None, :, 1]), axis=1) if len(sp) else np.full(len(cxy), 99)
        out['D3']['gt35_with_shrub'] = int((dd < 3).sum())
    # ----- D2: mown edge: satellite tone of the corridor band 0-60 m by distance -----
    bands = []
    for a, b in ((0, 15), (15, 30), (30, 45), (45, 60)):
        m = oop & (dl > a) & (dl <= b)
        bands.append(round(100 * (m & green_t).sum() / max(m.sum(), 1)))
    out['D2'] = {'green_tone_pct_by_band_15m': bands}
    # ----- bunkers D4/D5 -----
    bl = []
    for bid, poly in bunk.items():
        row, dmatch = bunk_rows[bid]
        if int(row['hole']) != n: continue
        c = poly.mean(0); area = poly_area(poly)
        bx0, bz0 = poly.min(0) - 6; bx1, bz1 = poly.max(0) + 6
        BX, BZ = np.meshgrid(np.arange(bx0, bx1, 0.5), np.arange(bz0, bz1, 0.5))
        Hs = surf(BX, BZ); cov = float(np.isfinite(Hs[inside(poly, BX, BZ)]).mean())
        src = 'surface_05m'
        if cov < 0.8:
            Hs = HM.sample(BX, BZ, order=3); src = 'heightmap_1.66m'
        m = inside(poly, BX, BZ)
        dd = dist_to_polygon_edge(np.c_[BX.ravel(), BZ.ravel()], poly).reshape(BX.shape)
        ring = (~m) & (dd >= 1) & (dd <= 3)
        rim = float(np.nanmedian(Hs[ring])); floor = float(np.nanpercentile(Hs[m], 10))
        depth = rim - floor
        # depression: cells below rim-0.25 connected to the lowest inside cell
        below = np.isfinite(Hs) & (Hs < rim - 0.25) & (dd < 6)
        lab, nl = ndimage.label(below)
        dep = None
        if nl:
            counts = ndimage.sum(m, lab, range(1, nl + 1)); k = int(np.argmax(counts)) + 1
            if counts[k - 1] > 0:
                comp = lab == k
                dc = np.array([BX[comp].mean(), BZ[comp].mean()])
                dep = {'centre_offset_m': round(float(np.hypot(*(dc - c))), 2), 'dx': round(float(dc[0] - c[0]), 2), 'dz': round(float(dc[1] - c[1]), 2),
                       'area_m2': round(comp.sum() * 0.25), 'outside_outline_m2': round((comp & ~m).sum() * 0.25), 'outline_not_depressed_m2': round((m & ~comp).sum() * 0.25)}
        # green-side face: profile from centroid toward the green
        gdir = np.array([float(row['face_toward_x']), float(row['face_toward_z'])]) - c; gdir /= (np.hypot(*gdir) or 1)
        ts = np.arange(0, 14, 0.25); prof_pts = c + ts[:, None] * gdir
        prof = surf(prof_pts[:, 0], prof_pts[:, 1]) if src == 'surface_05m' else HM.sample(prof_pts[:, 0], prof_pts[:, 1], order=3)
        edge_t = None
        ins = Path(poly).contains_points(prof_pts)
        if ins.any():
            edge_t = ts[np.where(ins)[0].max()]
        face = None
        if edge_t is not None:
            seg = (ts >= max(edge_t - 2, 0)) & (ts <= edge_t + 4) & np.isfinite(prof)
            if seg.sum() > 4:
                top = float(np.nanmax(prof[seg])); t_top = ts[seg][np.nanargmax(prof[seg])]
                lowmask = seg & (ts <= t_top)
                low = float(np.nanmin(prof[lowmask])); t_low = ts[lowmask][np.nanargmin(prof[lowmask])]
                run = max(t_top - t_low, 0.25); fh = top - low
                face = {'height_m': round(fh, 2), 'run_m': round(float(run), 2), 'deg': round(math.degrees(math.atan2(fh, run)), 1), 'h_over_w': round(fh / run, 2)}
        # D4 satellite sand: sand = bright, low greenness, vs the turf 3-8 m out
        rgbb = sat_rgb(BX, BZ); brb = rgbb.mean(-1); grn = rgbb[..., 1] - rgbb[..., 0]
        turf_ref = np.median(brb[(dd > 3) & (dd < 8) & ~m])
        sandm = (brb > turf_ref + 25) & (grn < 8)
        sand_in = round(100 * (sandm & m).sum() / max(m.sum(), 1)); sand_out = round((sandm & ~m & (dd <= 3)).sum() * 0.25)
        bests = None
        for sdx in np.arange(-3, 3.01, 0.5):
            for sdz in np.arange(-3, 3.01, 0.5):
                ms = inside(poly + [sdx, sdz], BX, BZ)
                sc = (sandm & ms).sum() / max(ms.sum(), 1) - (sandm & ~ms & (dist_to_polygon_edge(np.c_[BX.ravel(), BZ.ravel()], poly + [sdx, sdz]).reshape(BX.shape) <= 3)).sum() / max(ms.sum(), 1)
                if bests is None or sc > bests[0]: bests = (sc, sdx, sdz)
        satfit = {'sand_inside_pct': sand_in, 'sand_outside_3m_m2': sand_out, 'best_dx': float(bests[1]), 'best_dz': float(bests[2]), 'best_shift_m': round(float(np.hypot(bests[1], bests[2])), 2)}
        bl.append({'sat': satfit, 'id': bid, 'csv': int(row['bunker']), 'kind': row['kind'], 'centroid': [round(float(c[0]), 1), round(float(c[1]), 1)], 'area_m2': round(area),
                   'plan_depth_m': float(row['depth_m']), 'plan_face_deg': float(row['face_slope_deg']),
                   'lidar_depth_m': round(depth, 2), 'rim_y': round(rim, 2), 'floor_y': round(floor, 2), 'surface': src, 'coverage': round(cov, 2),
                   'depression': dep, 'green_face': face, 'floor_slope_p50_pct': round(float(np.nanmedian(surf_slope(BX[m], BZ[m]))), 1) if src == 'surface_05m' else None})
    out['bunkers'] = bl
    # ----- D8 shift fit + pad -----
    if gpoly is not None:
        gx0, gz0 = gpoly.min(0) - 10; gx1, gz1 = gpoly.max(0) + 10
        GX, GZ = np.meshgrid(np.arange(gx0, gx1, 0.5), np.arange(gz0, gz1, 0.5))
        SL = surf_slope(GX, GZ)
        Pg = np.c_[GX.ravel(), GZ.ravel()]
        def fit(dx, dz):
            sh = gpoly + [dx, dz]
            m = inside(sh, GX, GZ); d = dist_to_polygon_edge(Pg, sh).reshape(GX.shape)
            ring = (~m) & (d < 3)
            si = SL[m]; sr = SL[ring]
            if np.isfinite(si).sum() < 10 or np.isfinite(sr).sum() < 10: return np.inf, np.nan, np.nan
            return -(np.nanmean(sr) - np.nanmean(si)), np.nanmean(si), np.nanmean(sr)
        bestf = None; zero = fit(0, 0)
        for dx in np.arange(-3, 3.01, 0.5):
            for dz in np.arange(-3, 3.01, 0.5):
                f = fit(dx, dz)
                if bestf is None or f[0] < bestf[0][0]: bestf = (f, dx, dz)
        # pad: connected cells slope<4% (approx 4 deg = 7% ; use 4 deg -> 7%) around centroid
        pad = np.isfinite(SL) & (SL < 7.0)
        lab, nl = ndimage.label(pad)
        cc = np.round((gcen - [gx0, gz0]) / 0.5).astype(int)
        k = lab[min(cc[1], lab.shape[0] - 1), min(cc[0], lab.shape[1] - 1)]
        m0 = inside(gpoly, GX, GZ)
        if k:
            comp = lab == k
            padinfo = {'pad_m2': round(comp.sum() * 0.25), 'outline_m2': round(poly_area(gpoly)), 'outline_on_pad_pct': round(100 * (m0 & comp).sum() / max(m0.sum(), 1)),
                       'pad_outside_outline_m2': round((comp & ~m0).sum() * 0.25)}
        else:
            padinfo = {'pad_m2': 0, 'outline_m2': round(poly_area(gpoly)), 'outline_on_pad_pct': 0}
        outline_steep = round(100 * np.nanmean(SL[m0] > 7.0))
        # banks: max slope on the 3 m ring, by side
        out['D8'] = {'best_dx': float(bestf[1]), 'best_dz': float(bestf[2]), 'best_shift_m': round(float(np.hypot(bestf[1], bestf[2])), 2),
                     'score_zero': round(float(zero[0]), 2), 'score_best': round(float(bestf[0][0]), 2),
                     'slope_in_zero_pct': round(float(zero[1]), 1), 'slope_ring_zero_pct': round(float(zero[2]), 1),
                     'outline_cells_over_7pct': outline_steep, **padinfo,
                     'svg_centroid_vs_gkd_m': round(float(np.hypot(*(gpoly.mean(0) - gcen))), 2)}
        # D10: bank behind and beside the green
        prev = [q for q in line[:-1] if np.hypot(*(line[-1] - q)) > 1][-1]
        back = (line[-1] - prev); back /= np.hypot(*back)
        edge_pt = gcen + back * (np.sqrt(poly_area(gpoly) / math.pi))
        ts = np.arange(0, 15.1, 0.5); pp = edge_pt + ts[:, None] * back
        pr = surf(pp[:, 0], pp[:, 1]); pr2 = HM.sample(pp[:, 0], pp[:, 1], order=3)
        prf = np.where(np.isfinite(pr), pr, pr2)
        out['D10'] = {'back_rise_max_m': round(float(np.nanmax(prf) - prf[0]), 2), 'back_drop_max_m': round(float(prf[0] - np.nanmin(prf)), 2),
                      'back_profile_15m_coverage_05m': round(float(np.isfinite(pr).mean()), 2),
                      'ring_slope_max_pct': round(float(np.nanpercentile(SL[(~m0) & (dist_to_polygon_edge(Pg, gpoly).reshape(GX.shape) < 6)], 95)), 1)}
    # ----- D7 -----
    gr = grep[n]
    out['D7'] = {k: gr[k] for k in ('area_m2', 'relief_m', 'slope_mean_pct', 'slope_p90_pct', 'pct_over_3', 'pins_failing', 'usable_pct', 'valid_areas', 'flag_slope', 'pass')}
    # ----- D12 creeks -----
    cr = []
    for wid, t, pts in osm_water:
        if len(pts) < 2: continue
        d = dist_to_polyline(pts, line) if len(pts) else np.array([np.inf])
        if d.min() <= 80:
            cr.append({'osm': wid, 'tag': t.get('waterway') or 'natural=water', 'name': t.get('name', ''), 'min_dist_to_line_m': round(float(d.min()), 1)})
    out['D12'] = cr
    # ----- D14 props/buildings near hole -----
    pr = []
    for wid, t, pts in osm_build + osm_props + osm_roads:
        if len(pts) == 0: continue
        d = dist_to_polyline(pts, line).min()
        lim = 80 if ('building' in t or t.get('amenity')) else 40
        if d <= lim:
            kind = t.get('building') and 'building=' + t['building'] or t.get('amenity') and 'amenity=' + t['amenity'] or t.get('highway') and 'highway=' + t['highway'] or t.get('man_made') and 'man_made=' + t['man_made']
            pr.append({'osm': wid, 'kind': kind, 'name': t.get('name', ''), 'dist_m': round(float(d), 1), 'at': [round(float(v)) for v in pts.mean(0)]})
    pr.sort(key=lambda r: r['dist_m'])
    out['D14'] = pr
    # ----- D15 -----
    allpts = np.vstack([line, gpoly if gpoly is not None else line])
    out['D15'] = {'min_dist_to_frame_edge_m': round(float(min(allpts[:, 0].min(), allpts[:, 1].min(), SQ - allpts[:, 0].max(), SQ - allpts[:, 1].max())))}
    # ----- D16 SVG overlaps -----
    # per-hole bbox at 0.5 m; layers: water>bunker>green>tee>fairway>(rough/deep)
    bx0, bz0 = line.min(0) - 70; bx1, bz1 = line.max(0) + 70
    OX, OZ = np.meshgrid(np.arange(bx0, bx1, 1.0), np.arange(bz0, bz1, 1.0))
    def lay(polys):
        m = np.zeros(OX.shape, bool)
        for p in polys:
            if p[:, 0].max() < bx0 or p[:, 0].min() > bx1 or p[:, 1].max() < bz0 or p[:, 1].min() > bz1: continue
            m |= inside(p, OX, OZ)
        return m
    Lw, Lb, Lg, Lt, Lf = lay(water.values()), lay(bunk.values()), lay(greens.values()), lay(tees_svg.values()), lay(fw.values())
    corr1 = dist_to_polyline(np.c_[OX.ravel(), OZ.ravel()], line).reshape(OX.shape) <= 60
    ov = {'bunker_in_green_m2': int((Lb & Lg & corr1).sum()), 'tee_in_green_m2': int((Lt & Lg & corr1).sum()), 'tee_in_bunker_m2': int((Lt & Lb & corr1).sum()),
          'water_under_turf_m2': int((Lw & (Lb | Lg | Lt | Lf) & corr1).sum()), 'tee_in_fairway_m2': int((Lt & Lf & corr1).sum()), 'tee_m2': int((Lt & corr1).sum()),
          'bunker_in_fairway_m2': int((Lb & Lf & corr1).sum()), 'green_in_fairway_m2': int((Lg & Lf & corr1).sum())}
    # tees straddling the fairway edge
    strad = 0
    for p in tees_svg.values():
        if dist_to_polyline(p.mean(0)[None], line)[0] > 60: continue
        TX, TZ = np.meshgrid(np.arange(p[:, 0].min(), p[:, 0].max(), 0.5), np.arange(p[:, 1].min(), p[:, 1].max(), 0.5))
        if TX.size == 0: continue
        mt = inside(p, TX, TZ)
        if mt.sum() == 0: continue
        mf = np.zeros_like(mt)
        for f in fw.values(): mf |= inside(f, TX, TZ)
        share = (mt & mf).sum() / mt.sum()
        if 0.05 < share < 0.95: strad += 1
    ov['tees_straddling_fairway_edge'] = strad
    out['D16'] = ov
    # ----- D17 tees -----
    tl = []
    tc = {r['colour']: r for r in teecol if int(r['hole']) == n}
    for k, t in h.playing_tees().items():
        p = np.array([t['Position']['x'], t['Position']['z']])
        # svg tee outline containing / nearest
        cont = None; nd = 1e9
        for tid, poly in tees_svg.items():
            if Path(poly).contains_point(p): cont = tid; nd = 0; break
            d = dist_to_polygon_edge(p[None], poly)[0]
            if d < nd: nd = d; cont = tid
        poly = tees_svg[cont]
        TX, TZ = np.meshgrid(np.arange(poly[:, 0].min() - 1, poly[:, 0].max() + 1, 0.5), np.arange(poly[:, 1].min() - 1, poly[:, 1].max() + 1, 0.5))
        mt = inside(poly, TX, TZ); Hs = surf(TX, TZ); SL = surf_slope(TX, TZ)
        hin = Hs[mt]; hin = hin[np.isfinite(hin)]
        decks = []
        if len(hin):
            hist, edges = np.histogram(hin, bins=np.arange(hin.min() - 0.05, hin.max() + 0.2, 0.15))
            # decks = histogram peaks holding >= 40 m2 (160 cells at 0.25 m2)
            for i, v in enumerate(hist):
                if v * 0.25 >= LR['d17_tee_flat_min_m2'] and (i == 0 or hist[i - 1] <= v) and (i == len(hist) - 1 or hist[i + 1] < v):
                    decks.append(round(float(edges[i] + 0.075), 2))
        # local deck at the tee point: 3 m radius
        r3 = (np.hypot(TX - p[0], TZ - p[1]) <= 3)
        loc = Hs[r3]; locs = SL[r3]
        tl.append({'colour': k, 'gkd_y': round(t['Position']['y'], 2), 'svg_tee': cont, 'inside_box': nd == 0, 'dist_to_box_m': round(float(nd), 1),
                   'box_area_m2': round(poly_area(poly)), 'box_slope_p90_pct': round(float(np.nanpercentile(SL[mt], 90)), 1) if np.isfinite(SL[mt]).any() else None,
                   'box_h_range_m': round(float(hin.max() - hin.min()), 2) if len(hin) else None, 'decks_y': decks,
                   'local_slope_p50_pct': round(float(np.nanmedian(locs)), 1) if np.isfinite(locs).any() else None,
                   'on_mapped_box_csv': tc.get(k, {}).get('on_mapped_box', ''), 'card_yards': tc.get(k, {}).get('card_yards', ''), 'gkd_yards': tc.get(k, {}).get('gkd_yards', '')})
    # LiDAR flats >= 40 m2 within 60 m of the tees (heightmap slope < 2 %, outside fairway/green) - count
    tp0 = np.array([t['Position']['x'] for t in h.playing_tees().values()]); tz0 = np.array([t['Position']['z'] for t in h.playing_tees().values()])
    FX, FZ = np.meshgrid(np.arange(tp0.min() - 60, tp0.max() + 60, HM.S), np.arange(tz0.min() - 60, tz0.max() + 60, HM.S))
    fs = hm_slope(FX, FZ); near = np.min(np.hypot(FX[..., None] - tp0, FZ[..., None] - tz0), axis=-1) <= LR['d17_within_osm_tee_m']
    flat = (fs < math.degrees(math.atan(0.02))) & near
    fwm = np.zeros_like(flat)
    for f in list(fw.values()) + list(greens.values()):
        if f[:, 0].max() < FX.min() or f[:, 0].min() > FX.max() or f[:, 1].max() < FZ.min() or f[:, 1].min() > FZ.max(): continue
        fwm |= inside(f, FX, FZ)
    lab, nl = ndimage.label(flat & ~fwm)
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, nl + 1)) * HM.S ** 2 if nl else []
    out['D17'] = {'tees': tl, 'lidar_flats_ge40m2_within_60m_outside_fairway': int((np.asarray(sizes) >= 40).sum()),
                  'flats_m2': sorted([int(s) for s in sizes if s >= 40], reverse=True)[:8]}
    # ----- D18 trees within 35 m of tee/green -----
    near_pts = np.vstack([np.c_[tp0, tz0], gcen[None]])
    dtree = np.min(np.hypot(tp[:, None, 0] - near_pts[None, :, 0], tp[:, None, 1] - near_pts[None, :, 1]), axis=1)
    dsh = np.min(np.hypot(sp[:, None, 0] - near_pts[None, :, 0], sp[:, None, 1] - near_pts[None, :, 1]), axis=1)
    sel = dtree <= R['trees']['species_socal']['near_tee_green_m']
    tsel = trees[sel]
    spc = {}
    for s in tsel['species']: spc[s] = spc.get(s, 0) + 1
    wider = int(((tsel['crown_radius_m'] * 2) > tsel['height_m']).sum())
    out['D18'] = {'trees_within_35m': int(sel.sum()), 'species_draft': spc, 'h_min': round(float(tsel['height_m'].min()), 1) if sel.sum() else None, 'h_max': round(float(tsel['height_m'].max()), 1) if sel.sum() else None,
                  'over_16m': int((tsel['height_m'] > LR['d18_oak_max_m']).sum()), 'wider_than_tall': wider, 'low_confidence_species': int((tsel['species_confidence'] == 'low').sum()),
                  'shrubs_within_35m': int((dsh <= 35).sum()), 'shrubs_ge3m_within_35m': int(((dsh <= 35) & (shrubs['height_m'] >= LR['d18_shrub_max_h_m'])).sum()),
                  'tree_ids_near_green': [int(i) for i in trees['id'][(np.hypot(tp[:, 0] - gcen[0], tp[:, 1] - gcen[1]) <= 35)]][:12]}
    res['holes'][n] = out
    print('hole', n, 'done', file=sys.stderr)

json.dump(res, open(OUT, 'w'), indent=1, default=lambda o: float(o) if isinstance(o, (np.floating,)) else int(o) if isinstance(o, np.integer) else str(o))
print('wrote', OUT)
