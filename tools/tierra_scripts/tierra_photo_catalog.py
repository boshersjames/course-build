# Catalog of every BoJa photo under Documents/tierra-rejada/reference (HEIC + JPEG copies of the same shot grouped by stem; EXIF read from
# the HEIC's embedded TIFF block when there is no JPEG). Writes <out>/photo_catalog.csv and a display JPEG per stem in <cache>
# (ffmpeg converts HEIC-only shots). Photos are never modified, renamed or committed.
#   python tierra_photo_catalog.py <out dir> <jpeg cache dir>
import csv, glob, os, re, subprocess, sys
from PIL import Image
from PIL.ExifTags import GPSTAGS, TAGS
from pyproj import Transformer
out, cache = sys.argv[1], sys.argv[2]; os.makedirs(out, exist_ok=True); os.makedirs(cache, exist_ok=True)
tf = Transformer.from_crs('EPSG:4326', 'EPSG:6340', always_xy=True); E0, N0 = 328865.84, 3792716.77
def dms(v): return float(v[0]) + float(v[1]) / 60 + float(v[2]) / 3600
def exif_of(p):
    if p.lower().endswith(('.jpg', '.jpeg')):
        try: ex = Image.open(p).getexif()
        except Exception: return None
    else:
        b = open(p, 'rb').read(); ex = None
        for m in re.finditer(rb'(MM\x00\x2a|II\x2a\x00)', b):
            try:
                e = Image.Exif(); e.load(b[m.start():])
                if e.get_ifd(0x8825): ex = e; break
            except Exception: pass
        if ex is None: return None
    g = {GPSTAGS.get(k, k): v for k, v in ex.get_ifd(0x8825).items()}; x = ex.get_ifd(0x8769)
    return g, x.get(0x9003, ''), x.get(0xA405, '')
roots = glob.glob('C:/Users/user/Documents/tierra-rejada/reference/**/', recursive=True)
files = {}
for r in roots:
    for p in glob.glob(r + '*'):
        if not os.path.isfile(p) or not p.lower().endswith(('.heic', '.jpg', '.jpeg')) or '/review_jpg' in p.replace('\\', '/'): continue
        stem = re.sub(r'\(\d+\)$', '', os.path.splitext(os.path.basename(p))[0]); files.setdefault(stem, []).append(p.replace('\\', '/'))
rows = []
for stem, ps in sorted(files.items()):
    ps = sorted(set(ps)); info = None; src = None
    for p in sorted(ps, key=lambda q: (not q.lower().endswith('.heic'), -os.path.getsize(q))):
        info = exif_of(p)
        if info and 'GPSLatitude' in info[0]: src = p; break
    jpg = next((p for p in ps if p.lower().endswith(('.jpg', '.jpeg'))), None)
    if not jpg:
        jpg = os.path.join(cache, stem + '.jpg').replace('\\', '/')
        if not os.path.exists(jpg): subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', ps[0], '-frames:v', '1', '-q:v', '3', jpg])
    row = dict(photo=stem, files=' | '.join(ps), exif_source=src or '', display_jpg=jpg, time='', x='', z='', exif_heading_true='', f35='')
    if info and 'GPSLatitude' in info[0]:
        g = info[0]; lat = dms(g['GPSLatitude']) * (-1 if g.get('GPSLatitudeRef') == 'S' else 1); lon = dms(g['GPSLongitude']) * (-1 if g.get('GPSLongitudeRef') == 'W' else 1)
        X, Y = tf.transform(lon, lat); row.update(time=info[1], x=round(X - E0, 1), z=round(Y - N0, 1), f35=info[2])
        if g.get('GPSImgDirection') is not None: row['exif_heading_true'] = round((float(g['GPSImgDirection']) + (12 if g.get('GPSImgDirectionRef', 'M') == 'M' else 0)) % 360, 1)
    rows.append(row)
with open(os.path.join(out, 'photo_catalog.csv'), 'w', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
print(len(rows), 'shots,', sum(1 for r in rows if r['x'] != ''), 'with GPS,', sum(1 for r in rows if len(r['files'].split(' | ')) > 1), 'with duplicate files')
