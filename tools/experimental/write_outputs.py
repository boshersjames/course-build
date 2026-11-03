"""Turn review.json into course_photos/decisions.csv and course_photos/visit_list.md."""
import json, csv, os, math
os.chdir('/home/user/hansen-dam')
r = json.load(open('/tmp/claude-0/-home-user/e920af01-d04f-5fee-8488-a122013cb130/scratchpad/review.json'))
DATE = '2026-11-03'
rows = []
def row(point, hole, answer, source, conf, photo, by):
    rows.append(dict(point=point, hole=hole, answer=answer, source=source, confidence=conf, photo=photo, decided_by=by, date=DATE))

cr = r['course']
osm_sat = cr['cartpath_vs_sat']; bsat = cr['bunker_vs_sat']
W = cr['water']['water_0']
visit = {}
def V(h, cat, text):
    visit.setdefault(h, {}).setdefault(cat, []).append(text)

holes = sorted(r['holes'], key=int)
for hs in holes:
    h = int(hs); H = r['holes'][hs]
    d1, d2, d3, d7, d8, d10, d12, d14, d15, d16, d17, d18 = (H.get(k) for k in ('D1', 'D2', 'D3', 'D7', 'D8', 'D10', 'D12', 'D14', 'D15', 'D16', 'D17', 'D18'))
    B = H['bunkers']
    # ---- D1 ----
    steep = d1['slope_gt15_m2']
    cls = 'mown rough %d%% of the 60 m corridor by satellite tone (green), dry grass %d%% (tan), litter/shade %d%% (dark); slopes over 15 deg %s m2 (%.1f%%)' % (
        d1['green_tone_pct'], d1['tan_pct'], d1['dark_pct'], steep, d1['slope_gt15_pct'])
    if steep >= 500:
        cls += ' centred at (%d, %d) %s of the line: draft scrub on soil 70-80%% cover (rules lidar_review.d1_scrub_cover_pct)' % (d1['steep_centroid'][0], d1['steep_centroid'][1], d1['steep_side'])
        V(h, 'hillside', 'Bank/slope %s of the line around Unity (%d, %d): %s m2 over 15 deg. One eye-level photo across it, one along the toe where scrub meets turf (D1, D2, D3).' % (d1['steep_side'], d1['steep_centroid'][0], d1['steep_centroid'][1], steep))
    else:
        cls += ': flat corridor, no hillside; class is dry grass vs mown only'
    cls += '; %d LiDAR shrubs in the corridor (%d over 3 m)' % (d1['shrubs_in_corridor'], d1['shrubs_ge3m'])
    row('D1', h, cls, 'heightmap slope + satellite tone + shrubs_clean.csv', 'low', 'hillside eye-level + toe' if steep >= 500 else 'rough band photo from the tee', 'data')
    # ---- D2 ----
    row('D2', h, 'default: satellite mown edge. Green-tone share by 15 m band out from the line: %s%%; the corridor dries past %s' % (
        '/'.join(str(v) for v in d2['green_tone_pct_by_band_15m']),
        ('15 m' if d2['green_tone_pct_by_band_15m'][1] < 60 else '30 m' if d2['green_tone_pct_by_band_15m'][2] < 60 else '45 m' if d2['green_tone_pct_by_band_15m'][3] < 60 else 'nowhere (mown to 60 m)')),
        'satellite NAIP 0.42 m/px (date not recorded; dormant-season check needed)', 'low', 'photo along the mow line/toe', 'default')
    # ---- D3 ----
    if d3['gt35_m2'] == 0:
        row('D3', h, 'n/a: no cells over 35 deg within 60 m of the line (rules brush.rock.slope_min_deg); no rock', 'heightmap slope', 'high', '', 'data')
    else:
        txt = '%s m2 over 35 deg near (%d, %d); %d pale satellite cells, %d within 3 m of a LiDAR shrub: draft scrub, rock only on the pale cells' % (d3['gt35_m2'], d3['centroid'][0], d3['centroid'][1], d3['gt35_pale_cells'], d3['gt35_with_shrub'])
        if h == 11: txt += ' (dam embankment toe and spillway side, elevation 302-332 m within 40 m)'
        if h == 18: txt += ' (Sheldon Pit quarry edge south of the back tees, 37 m drop within 40 m; OSM landuse=quarry)'
        if h == 4: txt += ' (NW hill above the green, Osborne St side)'
        if h == 16: txt += ' (scattered bank cuts between the pond and the tees; the E-W profile is a smooth 6% rise, no cliff)'
        row('D3', h, txt, 'heightmap slope + satellite tone + shrubs_clean.csv', 'low', 'photo framing the face', 'data')
        V(h, 'hillside', 'Steep cells over 35 deg near Unity (%d, %d), %s m2: photo framing the face to settle rock vs scrub and its colour (D3).' % (d3['centroid'][0], d3['centroid'][1], d3['gt35_m2']))
    # ---- D4 / D5 / D6 per bunker ----
    if not B:
        row('D4', h, 'no bunkers on this hole (course.svg / bunkers.csv)', 'course.svg', 'high', '', 'data')
        row('D5', h, 'no bunkers on this hole', 'course.svg', 'high', '', 'data')
        row('D6', h, 'no bunkers on this hole', 'course.svg', 'high', '', 'default')
    else:
        d4p = []; d5p = []; d4conf = 'medium'; d5conf = 'low'; d4photo = ''
        for b in B:
            s = b['sat']; tag = '%s(csv %d,%s,%d m2)' % (b['id'], b['csv'], b['kind'], b['area_m2'])
            if s['sand_inside_pct'] < 50:
                d4p.append('%s: satellite shows sand on only %d%% of the outline (best shift %.1f m): confirm the bunker exists / is still sand' % (tag, s['sand_inside_pct'], s['best_shift_m']))
                d4conf = 'low'; d4photo = 'bunker fairway side + across floor'
                V(h, 'bunker', '%s at (%.0f, %.0f): satellite shows sand on only %d%% of the OSM outline. Photo from the fairway side and across the floor: does it exist, is it sand or grassed over (D4).' % (tag, b['centroid'][0], b['centroid'][1], s['sand_inside_pct']))
            else:
                d4p.append('%s: outline on satellite sand %d%% inside, %d m2 sand outside within 3 m, best outline shift %.1f m (%+.1f E, %+.1f N)' % (tag, s['sand_inside_pct'], s['sand_outside_3m_m2'], s['best_shift_m'], s['best_dx'], s['best_dz']))
                if s['best_shift_m'] > 1.5 or s['sand_outside_3m_m2'] > 50:
                    d4conf = 'low'
                    V(h, 'bunker', '%s at (%.0f, %.0f): satellite sand runs %d m2 outside the OSM outline / best shift %.1f m. Photo from the fairway side showing the sand line (D4).' % (tag, b['centroid'][0], b['centroid'][1], s['sand_outside_3m_m2'], s['best_shift_m']))
            f = b['green_face'] or {}
            d5p.append('%s: heightmap rim-to-floor %.2f m (plan %.2f m), %s-side face %.2f m over %.2f m (%.0f deg, h/w %.2f)' % (
                tag, b['lidar_depth_m'], b['plan_depth_m'], 'green' if b['kind'] == 'greenside' else 'far', f.get('height_m', 0), f.get('run_m', 0), f.get('deg', 0), f.get('h_over_w', 0)))
            V(h, 'bunker', '%s at (%.0f, %.0f): heightmap depth %.2f m vs plan %.2f m. Photo from the fairway side with the face against the lip, one from the green side, one across the floor for grass (D5, D6).' % (tag, b['centroid'][0], b['centroid'][1], b['lidar_depth_m'], b['plan_depth_m']))
        d4txt = 'LiDAR depression edge not resolvable here: the 0.5 m surface (surfaces/) does not cover bunkers (coverage %s) and the 1.66 m heightmap blurs the edge; satellite sand edge used. ' % ('/'.join('%.0f%%' % (100 * b['coverage']) for b in B)) + '; '.join(d4p)
        row('D4', h, d4txt, 'satellite sand mask vs course.svg bunker outlines; heightmap', d4conf, d4photo, 'data')
        d5txt = 'heightmap (1.66 m) rim-to-floor is a floor, not the depth (under-reads like QL2; build >= LiDAR + 0.3 m, rules lidar_review D5): ' + '; '.join(d5p) + '. Not computed to 10 cm: needs the 0.5 m surface extended to bunkers + 3 m (points.npz, not in git)'
        row('D5', h, d5txt, 'heightmap_2049.raw; bunkers.csv', 'low', 'bunker fairway side face + green side', 'data')
        row('D6', h, 'default: no island, no nose (%d bunkers)' % len(B), 'rules bunkers.islands', '', 'bunker across floor', 'default')
    # ---- D7 ----
    pins = d7['pins_failing']
    row('D7', h, 'LiDAR surface (QL1, 0.5 m bare-earth) with the slope report: %s m2, relief %s m, slope mean %s%% p90 %s%%, %s%% over 3%%, %s/4 pins fail 1.7 deg, usable %s%%, %s valid areas%s. Phone LAS scan still wins: scan on the visit' % (
        d7['area_m2'], d7['relief_m'], d7['slope_mean_pct'], d7['slope_p90_pct'], d7['pct_over_3'], pins, d7['usable_pct'], d7['valid_areas'], ('; FLAG ' + d7['flag_slope']) if d7['flag_slope'] else ''),
        'progress/green_report/green_report.csv (green_report.py --smooth 0.6)', 'medium', 'LAS scan of green + 3 m collar', 'data')
    V(h, 'green', 'LAS scan of the green plus 3 m of collar (lanes 1.5 m apart). Slope report: mean %s%%, p90 %s%%, %s/4 pins fail%s. Photo of the collar from the fringe and low along the edge toward each bunker (D7, D8, D9).' % (d7['slope_mean_pct'], d7['slope_p90_pct'], pins, (' - ' + d7['flag_slope']) if d7['flag_slope'] else ''))
    # ---- D8 ----
    onpad = d8['outline_on_pad_pct']
    d8txt = 'SVG outline sits on the LiDAR pad: %d%% of the outline on the connected flat under 7%% (pad %s m2 incl. approach/collar, outline %s m2), %d%% of outline cells over 7%%; mean slope inside %.1f%% vs 3 m ring %.1f%%; SVG centroid %.2f m from the GKD green centre. Shift fit over +-3 m ill-posed on this flat pad (best %.1f m at the search edge), not used' % (
        onpad, d8['pad_m2'], d8['outline_m2'], d8['outline_cells_over_7pct'], d8['slope_in_zero_pct'], d8['slope_ring_zero_pct'], d8['svg_centroid_vs_gkd_m'], d8['best_shift_m'])
    conf = 'high' if onpad >= 95 and d8['outline_cells_over_7pct'] < 5 else 'medium'
    if h == 4: d8txt = 'green on a 4.4% tilt: no connected flat under 7% at the centroid, 9% of outline cells over 7%; ring slope 8.8% vs inside 4.4% so the pad edge is readable; SVG centroid 0.17 m from the GKD centre; outline accepted, flagged with D7'
    if h == 18: d8txt = 'green on a 5.1% tilt: 88% of the outline on the connected flat, 12% of cells over 7%, ring slope 3.8% below inside 5.1% (the green is steeper than its surround); SVG centroid 1.47 m from the GKD centre; outline accepted, flagged with D7'
    row('D8', h, d8txt, 'surfaces/surface_05m.npz slope vs course.svg green outline; HansenDam.GKD', conf, '', 'data')
    # ---- D9 ----
    row('D9', h, 'default: collar 1.2 m flush (rules greens.collar), 3 m sides / 5 m behind fairway height (course.svg already draws this ring), deep rough 6 m off', 'rules greens.collar', '', 'collar from the fringe', 'default')
    # ---- D10 ----
    d10txt = 'LiDAR as is. Behind the green along the line: max rise %s m / drop %s m over 15 m; 95th pct slope on the 6 m surround %s%%' % (d10['back_rise_max_m'], d10['back_drop_max_m'], d10['ring_slope_max_pct'])
    photo = ''
    if d10['ring_slope_max_pct'] >= 20 or (d10['back_rise_max_m'] or 0) >= 1.0:
        d10txt += ': a real bank; photo check whether it rises past the LiDAR'
        photo = 'green surround bank photo'
        V(h, 'green', 'Bank behind/beside the green (rise %s m, surround slope up to %s%%): photo of the bank with scale (D10).' % (d10['back_rise_max_m'], d10['ring_slope_max_pct']))
    row('D10', h, d10txt, 'surfaces/surface_05m.npz + heightmap profile', 'medium', photo, 'data')
    # ---- D11 ----
    if h == 16:
        lvl = W['h_inside_p5']
        row('D11', h, 'pond: LiDAR water level Unity y %.2f (elev %.1f m) = 5th pct inside the OSM outline (min %.2f); %d%% of the %d m2 outline reads within 0.3 m of it, the rest (peninsula, banks) rises to %.1f; no low cells outside the outline; bank 3 m ring median %.2f. GKD hazard: %d points, mean %.2f m from the outline (max %.1f m at the N tip), vertex y median %.2f (on the bank), no DZ' % (
            lvl, lvl + 207, W['h_inside_min'], round(100 * sum(W['hist_0.1m'][:3]) * 0.25 / W['area_m2']), W['area_m2'], W['h_inside_max'], W['ring3m_median'], W['gkd_hazard']['points'], W['gkd_hazard']['edge_dist_mean'], W['gkd_hazard']['edge_dist_max'], W['gkd_hazard']['y_median']),
            'heightmap_2049.raw inside course.svg water_0; HansenDam.GKD Hazards', 'high', 'photo along each bank', 'data')
        V(h, 'water', 'Pond: photo along each bank from a path corner and across the pond; LiDAR level elev %.1f m. The 2023-24 LiDAR reads ground on 28%% of the OSM outline (peninsula and margins): confirm the water reaches the mapped edge (D11).' % (lvl + 207))
    else:
        row('D11', h, 'no water within 60 m of the hole line (course.svg water layer, OSM natural=water / water_hazard)', 'course.svg; map.osm', 'high', '', 'data')
    # ---- D12 ----
    if d12:
        if h == 11:
            row('D12', h, 'OSM intermittent drain (way 1375447127) at the dam toe runs 4 m from the line beside the Black tee, spillway 50 m; heightmap E-W profiles at z 1830-1890 show no channel (smooth 7 m dam toe fall): nothing to cut; the real drain is below 1.66 m resolution or buried', 'map.osm waterway; heightmap profiles', 'high', 'photo if a ditch is visible at the 11 tee', 'data')
        elif h == 16:
            row('D12', h, 'no creek; the only water is the pond (D11). Middle Lake / Tujunga Wash / Lopez Creek are outside the holes', 'map.osm', 'high', '', 'data')
        else:
            row('D12', h, 'OSM water features within 80 m: ' + '; '.join('%s %s %.0f m' % (c['tag'], c['name'], c['min_dist_to_line_m']) for c in d12), 'map.osm', 'medium', '', 'data')
    else:
        row('D12', h, 'no creek or channel within 80 m of the line (OSM waterway, natural=water); no channel to build', 'map.osm', 'high', '', 'data')
    # ---- D13 ----
    sg = [v for v in cr['svg_vs_gkd_green'] if v[0] == h][0]
    extra = ''
    if h == 5: extra = '; GKD Yellow tee y is 1.05 m below the heightmap at (883, 2481) (card-placed pad on a 12% slope)'
    if h == 17: extra = '; GKD White tee y is 0.37 m above the heightmap at (2491, 1533)'
    row('D13', h, 'SVG green centroid vs GKD green centre: %+.1f m E, %+.1f m N (%.1f m; the GKD centre is the OSM pin, not the centroid). Course-level OSM-to-satellite +1.0 m E / 0 N; all tee y within 0.3 m of the heightmap except as noted%s' % (sg[1], sg[2], math.hypot(sg[1], sg[2]), extra),
        'course.svg; HansenDam.GKD; satellite.jpg; heightmap_2049.raw', 'high' if not extra else 'medium', '', 'data')
    # ---- D14 ----
    near = []; seen = set()
    for p in d14:
        if p['kind'] and not p['kind'].startswith('highway') and tuple(p['at']) not in seen:
            seen.add(tuple(p['at'])); near.append(p)
    roads = [p for p in d14 if p['kind'] and p['kind'].startswith('highway') and p['name']]
    d14txt = 'default: not built until it has a source. OSM within 80 m of the line: ' + ('; '.join('%s%s %.0f m at (%d, %d)' % (p['kind'], (' ' + p['name']) if p['name'] else '', p['dist_m'], p['at'][0], p['at'][1]) for p in near[:8]) if near else 'nothing')
    if roads: d14txt += '. Roads within 40 m: ' + ', '.join(sorted(set(p['name'] for p in roads)))
    d14txt += '. Roof blocks not computed: needs points.npz (not in git); the heightmap is bare earth. Signs, bins, markers, benches, ball washers: not in any data'
    row('D14', h, d14txt, 'map.osm', '', 'tee surrounds + props with a landmark', 'default')
    V(h, 'props', 'Hole sign square-on from 3 m, tee markers, bins, benches, ball washers, ropes at every box (D14).' + ((' OSM candidates to confirm: ' + '; '.join('%s%s at (%d, %d)' % (p['kind'], (' ' + p['name']) if p['name'] else '', p['at'][0], p['at'][1]) for p in near[:4])) if near else ''))
    # ---- D15 ----
    d15txt = 'real ground to the frame edge (nearest %d m from the hole) and the 36 km backdrop DEM (backdrop/, USGS 1/3 arc-second) beyond; OSM roads/buildings for the neighbourhood (offsite_trees.csv)' % d15['min_dist_to_frame_edge_m']
    if roads: d15txt += '; roads in view: ' + ', '.join(sorted(set(p['name'] for p in roads)))
    if h == 18: d15txt += '; Sheldon Pit quarry (37 m deep) directly south of the back tees is in the LiDAR, not golf: keep it real, nothing invented'
    if h in (11, 10, 12): d15txt += '; the Hansen Dam embankment (to 332 m) is the skyline north'
    if h in (13, 14): d15txt += '; Wentworth St houses east within 50 m'
    row('D15', h, d15txt, 'backdrop/backdrop.json; map.osm', 'medium', 'skyline photo from the tee', 'data')
    # ---- D16 ----
    viol = {k: v for k, v in d16.items() if k in ('bunker_in_green_m2', 'tee_in_green_m2', 'tee_in_bunker_m2', 'water_under_turf_m2', 'tees_straddling_fairway_edge') and v}
    row('D16', h, 'SVG layer check in the 60 m corridor: %s; fairway layer under green %d m2 and bunker %d m2 is the collar ring/clip order, fine. Unity mesh clip-order and z-fight scan not run (needs the PC automation)' % (
        'no violations (bunker-in-green 0, tee-in-green 0, tee-in-bunker 0, water-under-turf 0, tees straddling the fairway edge 0)' if not viol else 'VIOLATIONS ' + json.dumps(viol), d16['green_in_fairway_m2'], d16['bunker_in_fairway_m2']),
        'course.svg layers; rules terrain.clip_order', 'medium', '', 'data')
    # ---- D17 ----
    tt = d17['tees']; parts = []; bad = []
    for t in tt:
        placed = t['on_mapped_box_csv'].startswith('no')
        deck = ('deck y %s' % '/'.join(str(v) for v in t['decks_y'])) if t['decks_y'] else 'no 40 m2 deck'
        s = '%s: %s %s m2, %s, local slope %s%%, card %s / gkd %s yd%s' % (t['colour'], 'mapped box' if not placed else 'card-placed pad', t['box_area_m2'], deck, t['local_slope_p50_pct'], t['card_yards'], t['gkd_yards'], ' (card-placed)' if placed else '')
        parts.append(s)
        if placed and (t['local_slope_p50_pct'] or 0) > 4:
            bad.append('%s on %.0f%% ground, no LiDAR flat: the real tee is elsewhere' % (t['colour'], t['local_slope_p50_pct']))
        elif placed:
            bad.append('%s card-placed: which deck?' % t['colour'])
    decks = sorted(set(v for t in tt for v in t['decks_y']))
    d17txt = '%d LiDAR decks under the mapped boxes (y %s). %s' % (len(decks), ', '.join(str(v) for v in decks), '; '.join(parts))
    if bad: d17txt += '. OPEN: ' + '; '.join(bad)
    if h == 3: d17txt += '. Black 404 sits on a card-placed pad behind the mapped start (README)'
    if h == 11: d17txt += '. Black 340 sits on the dam toe slope behind the mapped start (README): 7 m fall in 30 m'
    conf = 'high' if not bad else ('low' if any('no LiDAR flat' in b for b in bad) else 'medium')
    row('D17', h, d17txt, 'surfaces/surface_05m.npz under course.svg tee shapes; checks/tee_colours.csv; HansenDam.GKD', conf, 'tee down-the-line + deck edge per box', 'data')
    teetxt = 'Every box: down the line from the back with markers, across the deck edge/bank, note which colour is on which deck. ' + ('Confirm: ' + '; '.join(bad) if bad else 'All four colours on mapped boxes with a LiDAR deck.')
    V(h, 'tee', teetxt)
    # ---- D18 ----
    row('D18', h, 'draft from trees.csv (LiDAR height/crown + species guess): %d trees within 35 m of a tee or the green, heights %s-%s m, %d over 16 m, species draft %s (%d low confidence), %d wider than tall (shrub suspects); %d LiDAR shrubs within 35 m (%d at/over 3 m, tree-vs-shrub ambiguous). Species blank until a photo names it' % (
        d18['trees_within_35m'], d18['h_min'], d18['h_max'], d18['over_16m'], json.dumps(d18['species_draft']), d18['low_confidence_species'], d18['wider_than_tall'], d18['shrubs_within_35m'], d18['shrubs_ge3m_within_35m']),
        'trees.csv; shrubs_clean.csv; rules trees.species_socal.near_tee_green_m', 'low', 'one photo per tree within 35 m, with scale', 'data')
    V(h, 'trees', '%d LiDAR trees within 35 m of a tee or the green (%d over 16 m; draft %s): one photo each from the tee side with scale, close enough for species; note dead/missing. %d shrubs at/over 3 m near the tees/green: tree or shrub? (D18)' % (
        d18['trees_within_35m'], d18['over_16m'], ', '.join('%s %d' % (k, v) for k, v in d18['species_draft'].items()), d18['shrubs_ge3m_within_35m']))

# ---- course-level rows ----
row('D1', 'all', 'terrain_masks/ (brush_sage_scrub, grass_dry_hillside, grass_deep_rough, no_grass) exist from coursekit but were not validated here (binary .mask format not read); cover classes above are satellite tone + slope only. Hillsides that matter: NW hill above 4/5, the dam embankment above 10/11/12, Sheldon Pit south of 18, banks right of 6/7', 'terrain_masks/; heightmap; satellite', 'low', 'eye-level photo per hillside', 'data')
row('D3', 'all', 'no pale (rock-toned) steep ground anywhere in the corridors except 24 cells on 11 (dam toe) and 47 on 18 (pit edge), 99 on 16 (bank cuts): default scrub everywhere; the dam face itself is grassed riprap/earth (not in the corridor)', 'heightmap slope + satellite tone', 'low', 'photo of the dam face from 11 tee and the pit edge from 18 tee', 'data')
lowsand = [(int(hs), b) for hs in holes for b in r['holes'][hs]['bunkers'] if b['sat']['sand_inside_pct'] < 50]
row('D4', 'all', 'OSM bunker outlines vs satellite sand: whole-course best shift %+.1f m E / %+.1f m N (mean brightness %.1f vs %.1f at zero): outlines sit on the sand within about 1 m. 44 OSM bunkers; %d show sand on under 50%% of the outline (%s): confirm they exist and are still sand' % (
    bsat['best_dx'], bsat['best_dz'], bsat['bright_best'], bsat['bright_zero'], len(lowsand), '; '.join('%d: %s %d%%' % (h, b['id'], b['sat']['sand_inside_pct']) for h, b in lowsand)), 'satellite.jpg; course.svg', 'medium', 'sand white card once in bunker light', 'data')
import statistics
gs = [b['lidar_depth_m'] for hs in holes for b in r['holes'][hs]['bunkers'] if b['kind'] == 'greenside']
fs = [b['lidar_depth_m'] for hs in holes for b in r['holes'][hs]['bunkers'] if b['kind'] == 'fairway']
row('D5', 'all', 'bunkers.csv plan depths (greenside 0.8-1.5, fairway 0.4-0.64 m) all exceed the heightmap rim-to-floor (median %.2f m over %d greenside, %.2f m over %d fairway bunkers on the 1.66 m grid; max %.2f m, 4 bunker_3), as the QL2 under-read rule expects; a true QL1 depth needs the 0.5 m surface rebuilt with bunkers + 3 m in the mask (scripts/surfaces_05m.py from points.npz, not in git)' % (statistics.median(gs), len(gs), statistics.median(fs), len(fs), max(gs + fs)), 'bunkers.csv; heightmap', 'low', 'bunker faces', 'data')
row('D6', 'all', 'default none; sand colour not settled (rules bunkers.sand default #D8C99B): white card photo in bunker light once', 'rules bunkers', '', 'white card in sand', 'default')
row('D7', 'all', 'all 18 greens from the QL1 LiDAR 0.5 m surface; 7 flagged steep over 3% (2, 3, 4, 6, 9, 18 plus 11 at 2.95%), 4 and 18 fail all 4 pins; scan every green on the first visit (greens only, default)', 'green_report.csv', 'medium', 'LAS scan per green', 'data')
row('D8', 'all', '18 of 18 SVG green outlines matched to GKD holes (doctor.py); outlines sit on the LiDAR pads (16 at 98-100%, 18 at 88%, 4 on a tilted green); no green mesh offset expected beyond 1 m', 'surface_05m slope; course.svg', 'high', '', 'data')
row('D9', 'all', 'default collar profile; README notes course.svg already draws fairway-height 3 m sides / 5 m back around every green and found no uphill bank over 0.25 m/m (max 0.18)', 'rules greens.collar; README', '', 'collar from the fringe on every green', 'default')
row('D10', 'all', 'LiDAR; banks worth a photo check behind 3, 4 (1.1 m rise), 10 (3.2 m rise, 26% surround), 15/16 (0.65 m)', 'surface_05m profiles', 'medium', 'bank photos', 'data')
row('D11', 'all', 'one pond (hole 16) at LiDAR level elev %.1f m; GKD penalty area follows the OSM outline within 0.8 m mean; 1 hazard of 17 is the pond, 16 are interior OB. Water colour from BoJa (#669E7D), never the satellite' % (W['h_inside_p5'] + 207), 'heightmap; GKD', 'high', '', 'data')
row('D12', 'all', 'no creek crosses any hole; OSM waterways in the frame are the Big Tujunga culverts under the dam, the spillway, Lopez Creek (1760, 2562), the spreading grounds and Middle Lake, all outside the 80 m corridors', 'map.osm', 'high', '', 'data')
row('D13', 'all', 'registration recorded: (a) OSM cart paths vs satellite: best +%.1f m E / %+.1f m N (brightness %.1f vs %.1f at zero; weak signal under canopy); (b) OSM bunkers vs satellite sand: +%.1f m E / %+.1f m N; (c) GKD tee y vs heightmap: mean %.2f m, max 1.05 m (5 Yellow); vs 0.5 m surface mean %.3f m; (d) SVG green centroid vs GKD centre 0.2-3.5 m (pin vs centroid, not a shift); (e) OSM vs LiDAR shift not measurable on the 1.66 m heightmap (bunker depressions too shallow; green pads flat): assume OSM is within 1 m of the satellite and the satellite is the NAIP ortho on the same EPSG:6340 frame (course.json), no scan or photo GPS yet' % (
    osm_sat['best_dx'], osm_sat['best_dz'], osm_sat['bright_best'], osm_sat['bright_zero'], bsat['best_dx'], bsat['best_dz'], cr['gkd_tee_y_minus_heightmap']['mean'], cr['gkd_tee_y_minus_surface05']['mean']),
    'cartpaths.csv; course.svg; satellite.jpg; heightmap; GKD', 'high', '', 'data')
row('D14', 'all', 'default. Roof blocks not computed (needs points.npz). OSM sources: driving range (1773, 1855); roof/shelter at (1712, 1724) and parking (1740, 1667) by the 10th tee = clubhouse area; toilets at (1024, 2161) near 6 tee / 2 green, (1247, 2531) campground, (3238, 1822) north of 13; shelters (1289, 2488); drinking water (2144, 1905) near 11 tee and (3207, 1608) near 13/14; Metro bus parking (1833, 1468). Clubhouse footprint itself is not tagged in OSM', 'map.osm', '', 'clubhouse from each side + cart staging', 'default')
row('D15', 'all', 'real DEM: 3400 m LiDAR square plus backdrop/ 36 km USGS 1/3 arc-second (San Gabriels N/NE, Verdugos SE, Santa Monica Mts S, the dam); OSM roads: Glenoaks Blvd (S of 2/3), Osborne St (W of 4), Wentworth St (E of 13/14); offsite_trees.csv for the neighbourhood. Sheldon Pit quarry south of 18 and the spreading grounds are real ground in the LiDAR: keep', 'backdrop/backdrop.json; map.osm', 'high', 'skyline from the highest tee (11 Black, 322 m)', 'data')
row('D16', 'all', 'SVG: zero overlaps between bunker/green/tee/water layers and zero tees straddling the fairway edge on all 18 holes; Unity clip-order (water > bunker > green > collar > tee > fairway > rough), vertex-paint and z-fight scans pending on the PC', 'course.svg; rules terrain.clip_order', 'medium', '', 'data')
cp = [(int(hs), t) for hs in holes for t in r['holes'][hs]['D17']['tees'] if t['on_mapped_box_csv'].startswith('no')]
cp_bad = ['%d %s (%.0f%%)' % (h, t['colour'], t['local_slope_p50_pct']) for h, t in cp if (t['local_slope_p50_pct'] or 0) > 4]
cp_ok = ['%d %s' % (h, t['colour']) for h, t in cp if (t['local_slope_p50_pct'] or 0) <= 4]
row('D17', 'all', '72 GKD tees all inside a Tee shape; %d card-placed on drawn 9x7 pads (checks/tee_colours.csv), the rest on mapped OSM boxes. %d card-placed pads sit on ground sloping over 4%% with no LiDAR flat: %s: the real tee for those colours is on a mapped box or an unmapped deck; confirm on the visit. Card-placed on flat ground (which deck?): %s' % (len(cp), len(cp_bad), '; '.join(cp_bad), ', '.join(cp_ok)), 'surface_05m under tee shapes; tee_colours.csv', 'medium', 'tee photos', 'data')
row('D18', 'all', 'trees.csv 1,993 LiDAR trees with a species guess (palm 76, pine 311, eucalyptus 255, broadleaf 1,351, estimate only); shrubs_clean.csv 7,491 (LiDAR, 0.5-3 m+). The survey has no vegetation classes, so tree vs shrub is height/crown only; flyover gate crowns on 2, 3, 9 (negative rise through a crown) suggest crowns on the tee line worth a look (trees 542, 872, 34)', 'trees.csv; shrubs_clean.csv; flyover_gate', 'low', 'trees within 35 m of tees/greens', 'data')
row('D2', 'all', 'default satellite edge; the NAIP date is not recorded in course.json (record it); corridor tone is 35-76% green at 0-15 m and falls off by 30-45 m on most holes', 'satellite.jpg', 'low', '', 'default')

os.makedirs('course_photos', exist_ok=True)
with open('course_photos/decisions.csv', 'w', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['point', 'hole', 'answer', 'source', 'confidence', 'photo', 'decided_by', 'date'])
    w.writeheader()
    order = {'D%d' % i: i for i in range(1, 19)}
    for rr in sorted(rows, key=lambda x: (0 if x['hole'] == 'all' else int(x['hole']), order[x['point']])):
        w.writerow(rr)

# counts
per_hole = [x for x in rows if x['hole'] != 'all']
settled = sum(1 for x in per_hole if x['decided_by'] == 'data' and x['confidence'] == 'high')
drafted = sum(1 for x in per_hole if x['decided_by'] == 'data' and x['confidence'] != 'high')
defaulted = sum(1 for x in per_hole if x['decided_by'] == 'default')
print('rows', len(rows), 'per-hole', len(per_hole), 'settled', settled, 'drafted', drafted, 'defaulted', defaulted)
json.dump({'settled': settled, 'drafted': drafted, 'defaulted': defaulted, 'rows': len(rows)}, open('/tmp/claude-0/-home-user/e920af01-d04f-5fee-8488-a122013cb130/scratchpad/counts.json', 'w'))

# ---- visit list ----
L = []
L.append('# Hansen Dam: in-person visit list (from the LiDAR review, 2026-11-03)\n')
L.append('Written from `course_photos/decisions.csv` before any visit. Captures follow `course-build-site-visit` (tee boxes, tee surrounds, hillsides, bunkers, greens, water, trees within 35 m, props, boundary, once-per-course). `course.json` has `visit: none`; this list is what a visit would close. Walk in hole order; two photos per spot 10 s apart, landscape, eye height, a landmark in frame.\n')
L.append('What the QL1 data already settles (no capture needed): green outlines on their pads (D8), the pond level and extent (D11), no creeks (D12), registration (D13), layer order in the SVG (D16), which mapped boxes carry a LiDAR deck (D17). What only the visit answers: cover colour and soil share (D1), rock vs scrub (D3), grass in sand (D6), collars (D9), props under 1 m (D14), species (D18), which colour plays which deck (D17), and a phone LAS per green (D7).\n')
L.append('## Course-level, once\n')
L.append('- Skyline from the highest tee: 11 Black (elev 322 m on the dam toe) toward the course and the San Gabriels; also from 4 tee (NW hill) and 18 tee (Sheldon Pit south). Pass: skyline fit under 10 px (D15).')
L.append('- Sand colour: white card in the sand in the same light, once (D6/look). Also the card against a cart path for concrete.')
L.append('- Clubhouse: each side, doors, cart staging, the starter hut, the range tee line (OSM: roof at (1712, 1724), parking (1740, 1667), range (1773, 1855)). Practice greens by 10 tee and the clubhouse (five, kept) (D14).')
L.append('- A pin flag and a tee marker up close; the scorecard; the entrance drive off Glenoaks Blvd.')
L.append('- Hansen Dam embankment face from 10/11/12: one eye-level photo for cover class (grass vs riprap vs scrub) (D1/D3).')
L.append('- Sheldon Pit quarry edge south of the 18 tees: one photo across it for the fence line and cover (D1/D15).')
L.append('- Toilets/shelters OSM lists: (1024, 2161) between 2 green and 6 tee, (1289, 2488) shelter and (1247, 2531) toilets by the campground west of 4/5, (3238, 1822) north of 13. Photo each with a landmark (D14).')
L.append('- Rangefinder log on every tee: hole, tee, target, reading, plays-like, pin. The card runs long; do not move tees to match it.\n')
cats = [('tee', 'Tee boxes (D17, signs, markers)'), ('props', 'Tee surrounds and props (D14)'), ('hillside', 'Hillsides and steep ground (D1, D2, D3)'), ('bunker', 'Bunkers (D4, D5, D6)'), ('green', 'Green (D7, D8, D9, D10)'), ('water', 'Water (D11)'), ('trees', 'Trees within 35 m (D18)')]
for hs in holes:
    h = int(hs); L.append('## Hole %d\n' % h)
    for key, title in cats:
        items = visit.get(h, {}).get(key, [])
        if not items: continue
        L.append('**%s**' % title)
        for it in items: L.append('- ' + it)
        L.append('')
open('course_photos/visit_list.md', 'w').write('\n'.join(L) + '\n')
print('wrote visit_list.md', len(L), 'lines')
