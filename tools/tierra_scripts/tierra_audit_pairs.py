# Per-photo audit (BoJa Oct 30: before any GSPro upload, every geotagged back-nine photo against the game from the same spot).
#   prep <hole>    writes audit_<hole>_p.json / _l.json (portrait / landscape) run configs into the scratchpad
#   pairs <hole> <run dir> [<run dir>]   composes progress/tierra/audit_1030/pairs/<hole>/<hole>_<IMG>.jpg (photo left, game right, ~1600 px)
#                  and appends / replaces that hole's rows in progress/tierra/audit_1030/index.csv
# Poses: the HEIC set (IMG_4753+) from new_index.json (GPS + compass heading, eye 1.6 m over the LiDAR); the earlier set (10/11, IMG_46xx)
# from the aligned audit configs. gps_confidence: off-under-trees where we know the GPS sat under the canopy, else good.
import json, math, os, sys, csv
from PIL import Image, ImageOps
SP = 'C:/Users/user/AppData/Local/Temp/claude/C--Users-user-Documents-tierra-rejada/3efaf824-bf28-4c71-a80a-e8bab2dd9e22/scratchpad'
OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/audit_1030/'
OLD = 'C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/review_jpg/'
OFF = {'IMG_4800', 'IMG_4801', 'IMG_4802', 'IMG_4803'}
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tierra_newshots.py')).read().replace("sys.argv[1]", "SP"))
gps = json.load(open(SP + '/heic_gps.json')) if os.path.exists(SP + '/heic_gps.json') else {}
def photos(h):
    out = []
    for k, v in sorted(idx.items()):
        if v.get('hole') == h and v.get('x') is not None: out.append((k, SP + '/newjpg/%s.jpg' % k, shot(k), v.get('lat'), v.get('lon'), v.get('heading')))
    if h in (10, 11):
        for s in json.load(open(SP + '/audit_fix%d.json' % h))['shots']:
            k = s['name'][4:]; out.append((k, OLD + k + '.jpg', dict(s), None, None, None))
    return out
if sys.argv[1] == 'prep':
    h = int(sys.argv[2]); base = json.load(open(SP + '/flyover.json')); base.update(steps=[], saveScene=False)
    por, lan = [], []
    for k, f, s, *_ in photos(h):
        im = ImageOps.exif_transpose(Image.open(f)); s = dict(s, name='aud%d_%s' % (h, k)); s.pop('up', None)
        if im.width > im.height: s['fov'] = s['fov'] * im.height / im.width if s['fov'] > 60 else s['fov']; lan.append(s)
        else: por.append(s)
    json.dump(dict(base, shotWidth=900, shotHeight=1200, shots=por), open(SP + '/audit_%d_p.json' % h, 'w'))
    json.dump(dict(base, shotWidth=1200, shotHeight=900, shots=lan), open(SP + '/audit_%d_l.json' % h, 'w'))
    print(h, len(por), 'portrait', len(lan), 'landscape')
else:
    h = int(sys.argv[2]); runs = sys.argv[3:]; od = OUT + 'pairs/%d/' % h; os.makedirs(od, exist_ok=True); rows = []
    for k, f, s, lat, lon, hd in photos(h):
        g = next((r + '/shots/aud%d_%s.png' % (h, k) for r in runs if os.path.exists(r + '/shots/aud%d_%s.png' % (h, k))), None)
        if not g: continue
        a = ImageOps.exif_transpose(Image.open(f)).convert('RGB'); b = Image.open(g).convert('RGB'); H = 1000
        a = a.resize((int(a.width * H / a.height), H)); b = b.resize((int(b.width * H / b.height), H))
        S = Image.new('RGB', (a.width + b.width + 10, H), 'white'); S.paste(a, (0, 0)); S.paste(b, (a.width + 10, 0)); S.thumbnail((1600, 1600))
        p = od + '%d_%s.jpg' % (h, k); S.save(p, quality=82)
        if lat is None and k in gps: lat, lon, hd = gps[k].get('lat'), gps[k].get('lon'), gps[k].get('heading')
        rows.append([h, k, lat, lon, hd, 'off-under-trees' if k in OFF else ('good' if lat is not None else 'unknown'), 'pairs/%d/%d_%s.jpg' % (h, h, k)])
    ip = OUT + 'index.csv'; old = [r for r in csv.reader(open(ip))][1:] if os.path.exists(ip) else []
    old = [r for r in old if r[0] != str(h)]
    with open(ip, 'w', newline='') as fh: w = csv.writer(fh); w.writerow(['hole', 'img', 'lat', 'lon', 'heading', 'gps_confidence', 'pair_path']); w.writerows(old + rows)
    print(h, len(rows), 'pairs')
