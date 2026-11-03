# Position search for photos whose GPS spot gives no skyline match (11 tee, 13 tee, 18/1 tee by the clubhouse):
#   gen  : render a level panorama at every point of a grid around the GPS spot (eye 1.6 m over the heightmap)
#   solve: skyline-match the photo against every panorama (heading +-30, pitch), keep the best position;
#          accept it only if the skyline error is small and clearly better than at the GPS spot.
import json, math, sys
import numpy as np
from PIL import Image, ImageOps
from scipy import ndimage
SP, mode = sys.argv[1], sys.argv[2]
PHOTOS = sys.argv[3].split(',')
STEP, N = 5.0, 2   # 5 x 5 grid, +-10 m
SRC = 'C:/Users/user/Documents/tierra-rejada/reference/iCloud Photos/iCloud Photos/'
P = {p['photo']: p for p in json.load(open(SP + '/audit/poses.json'))}
H = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', dtype='<u2').reshape(2049, 2049).astype(float) * 207 / 65535
S = 2000 / 2048


def ground(x, z):
    i, j = x / S, z / S; i0, j0 = int(i), int(j); fi, fj = i - i0, j - j0
    return (H[j0, i0] * (1 - fi) * (1 - fj) + H[j0, i0 + 1] * fi * (1 - fj) + H[j0 + 1, i0] * (1 - fi) * fj + H[j0 + 1, i0 + 1] * fi * fj)


def offsets():
    for a in range(-N, N + 1):
        for b in range(-N, N + 1): yield a * STEP, b * STEP


if mode == 'gen':
    c = json.load(open(SP + '/audit_pano.json')); c['shots'] = []; c['shotWidth'], c['shotHeight'] = 1200, 600
    for k in PHOTOS:
        p = P[k]; h = math.radians(p['heading'])
        for dx, dz in offsets():
            x, z = p['eye'][0] + dx, p['eye'][2] + dz; y = ground(x, z) + 1.6
            c['shots'].append(dict(name='pg_%s_%+.0f_%+.0f' % (k, dx, dz), pos=[x, y, z], lookAt=[x + 100 * math.sin(h), y, z + 100 * math.cos(h)], aboveGround=False, fov=p['vfov']))
    c['steps'] = []; c['saveScene'] = False; json.dump(c, open(SP + '/pose_grid.json', 'w')); print(len(c['shots']), 'panoramas')
    sys.exit()


def skyline(img, game):
    a = np.asarray(img).astype(float); r, g, b = a[..., 0], a[..., 1], a[..., 2]; L = a.mean(2)
    if game: sky = (b > g + 15) & (b > r + 35)
    else: sky = ((b >= r - 12) & (L > 120)) | (L > 200)
    lab, n = ndimage.label(sky); top = set(np.unique(lab[0])) - {0}; sky = np.isin(lab, list(top))
    Hh = sky.shape[0]; col = np.argmin(sky, axis=0).astype(float); col[sky.all(0)] = np.nan; col[col == 0] = np.nan
    return col / Hh


def match(ph, pano, vfov):
    V = math.radians(vfov); tv = math.tan(V / 2); aspect = ph.size[0] / ph.size[1]; th = tv * aspect; tp = 2 * tv
    sp, sg = skyline(ph, False), skyline(pano, True)
    if np.isfinite(sp).sum() < 20 or np.isfinite(sg).sum() < 50: return None
    u = (np.arange(len(sp)) + 0.5) / len(sp) * 2 - 1; az_p = np.degrees(np.arctan(u * th)); el_p = np.degrees(np.arctan((0.5 - sp) * 2 * tv))
    ug = (np.arange(len(sg)) + 0.5) / len(sg) * 2 - 1; az_g = np.degrees(np.arctan(ug * tp)); el_g = np.degrees(np.arctan((0.5 - sg) * 2 * tv))
    best = None; lim = max(2.0, np.degrees(np.arctan(tp)) - np.degrees(np.arctan(th)) - 1)
    for dh in np.arange(-min(30, lim), min(30, lim) + 0.01, 0.5):
        eg = np.interp(az_p + dh, az_g, el_g, left=np.nan, right=np.nan); m = np.isfinite(eg) & np.isfinite(el_p)
        if m.sum() < 0.4 * len(sp): continue
        dp = float(np.clip(np.median(el_p[m] - eg[m]), -20, 20)); err = float(np.median(np.abs(el_p[m] - dp - eg[m])))
        if best is None or err < best[0]: best = (err, float(dh), -dp)
    return best


RUN = sys.argv[4]; A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/' + RUN + '/shots/'
out = json.load(open(SP + '/pose_grid_result.json')) if len(sys.argv) > 5 else {}
for k in PHOTOS:
    p = P[k]; ph = ImageOps.exif_transpose(Image.open(SRC + k + '.JPEG')).convert('RGB'); ph.thumbnail((300, 400)); res = []
    for dx, dz in offsets():
        try: pano = Image.open(A + ('pg_%s_%+.0f_%+.0f.png' % (k, dx, dz)).replace('+', '_')).convert('RGB').resize((1000, 500))
        except Exception: continue
        m = match(ph, pano, p['vfov'])
        if m: res.append((m[0], dx, dz, m[1], m[2]))
    if not res: print(k, 'no usable skyline'); continue
    res.sort(); gps = [r for r in res if r[1] == 0 and r[2] == 0]; g0 = gps[0][0] if gps else None; b = res[0]
    ok = b[0] < 2.5 and (g0 is None or b[0] < 0.75 * g0)
    x, z = p['eye'][0] + b[1], p['eye'][2] + b[2]
    out[k] = dict(gps=(gps[0] if gps else None), err=b[0], gps_err=g0, dx=b[1], dz=b[2], dh=b[3], dp=b[4], accepted=ok, eye=[x, ground(x, z) + 1.6, z], heading=p['heading'] + b[3], vfov=p['vfov'])
    print('%s best (%+.0f, %+.0f) m, heading %+.1f, pitch %+.1f, skyline err %.2f deg (GPS spot %s) -> %s' % (k, b[1], b[2], b[3], b[4], b[0], 'n/a' if g0 is None else '%.2f' % g0, 'ACCEPT' if ok else 'keep GPS'))
json.dump(out, open(SP + '/pose_grid_result.json', 'w'), indent=1)
