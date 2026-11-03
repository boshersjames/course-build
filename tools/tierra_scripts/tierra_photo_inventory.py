# Per-hole photo inventory for the gate (progress/tierra/gate/hole_NN_photos.csv).
#   python tierra_photo_inventory.py <holes e.g. 15,16,17,18> <out dir>
# Every BoJa photo with EXIF GPS is placed in the course frame (EPSG:6340 minus the SW corner) and assigned to each hole whose tee->green
# corridor it lies within 70 m of (a photo can belong to two holes). Heading: the solved pose from photo_poses/hole_NN.json when there is
# one (confidence noted), else the EXIF compass (magnetic, +12 deg to true, flagged unsolved). No GPS -> listed at the end as 'no_gps'.
import csv, glob, json, math, os, sys
from PIL import Image
from PIL.ExifTags import GPSTAGS, TAGS
from pyproj import Transformer
holes = [int(h) for h in sys.argv[1].split(',')]; out = sys.argv[2]; os.makedirs(out, exist_ok=True)
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
seg = {}
for n, h in enumerate(g['Holes'], 1):
    T = {t['TeeType']: (t['Position']['x'], t['Position']['z']) for t in h['Tees'] if t.get('Position')}
    pts = [T['Black']] + [T[k] for k in ('AimPoint1', 'AimPoint2') if k in T] + [T['GreenCenterPoint']]; seg[n] = pts
def dist_hole(n, x, z):
    best = 1e9
    for (ax, az), (bx, bz) in zip(seg[n], seg[n][1:]):
        dx, dz = bx - ax, bz - az; u = max(0, min(1, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz))); best = min(best, math.hypot(ax + dx * u - x, az + dz * u - z))
    return best
poses = {}
for f in glob.glob('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/photo_poses/hole_*.json'):
    for s in json.load(open(f))['shots']: poses[s['photo']] = s
def dms(v): return float(v[0]) + float(v[1]) / 60 + float(v[2]) / 3600
roots = ['C:/Users/user/Documents/tierra-rejada/reference/iCloud Photos/iCloud Photos', 'C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/photos/iCloud Photos']
rows = {h: [] for h in holes}; nogps = []; seen = set()
for r in roots:
    for p in sorted(glob.glob(r + '/*.JPEG') + glob.glob(r + '/*.jpg') + glob.glob(r + '/*.JPG')):
        name = os.path.splitext(os.path.basename(p))[0]
        if name in seen: continue
        seen.add(name)
        try:
            ex = Image.open(p)._getexif() or {}
        except Exception:
            continue
        e = {TAGS.get(k, k): v for k, v in ex.items()}; gps = {GPSTAGS.get(k, k): v for k, v in (e.get('GPSInfo') or {}).items()}
        if 'GPSLatitude' not in gps: nogps.append(name); continue
        lat = dms(gps['GPSLatitude']) * (-1 if gps.get('GPSLatitudeRef') == 'S' else 1); lon = dms(gps['GPSLongitude']) * (-1 if gps.get('GPSLongitudeRef') == 'W' else 1)
        X, Y = tf.transform(lon, lat); x, z = X - E0, Y - N0
        hd = gps.get('GPSImgDirection'); hd = float(hd) if hd is not None else None; ref = gps.get('GPSImgDirectionRef', 'M')
        true_h = None if hd is None else (hd + (12 if ref == 'M' else 0)) % 360
        ps = poses.get(name)
        for h in holes:
            d = dist_hole(h, x, z)
            if d > 70: continue
            rows[h].append(dict(photo=name, path=p.replace('\\', '/'), time=e.get('DateTimeOriginal', ''), x=round(x, 1), z=round(z, 1), dist_to_hole_m=round(d, 1),
                                exif_heading_true=None if true_h is None else round(true_h, 1), f35=e.get('FocalLengthIn35mmFilm', ''),
                                solved_heading=round(ps['heading_deg'], 1) if ps and ps.get('heading_deg') is not None else '', solved_pitch_down=round(ps['pitch_down_deg'], 1) if ps and ps.get('pitch_down_deg') is not None else '', confidence=ps.get('confidence', 'none') if ps else 'unsolved',
                                pose_source='photo_poses' if ps else 'exif (magnetic +12)'))
for h in holes:
    fn = os.path.join(out, 'hole_%02d_photos.csv' % h); rs = sorted(rows[h], key=lambda r: r['time'])
    with open(fn, 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['photo', 'path', 'time', 'x', 'z', 'dist_to_hole_m', 'exif_heading_true', 'f35', 'solved_heading', 'solved_pitch_down', 'confidence', 'pose_source']); w.writeheader(); w.writerows(rs)
    print('hole %d: %d photos (%d solved) -> %s' % (h, len(rs), sum(1 for r in rs if r['confidence'] != 'unsolved'), fn))
print('no GPS:', len(nogps))

