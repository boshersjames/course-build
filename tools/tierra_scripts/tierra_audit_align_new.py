# Align the game view with each photo: skyline (sky/terrain-tree boundary) elevation angle per azimuth in the photo vs a wide level
# panorama rendered at the photo's GPS spot and EXIF heading; search heading offset and pitch, write aligned shot config.
import json, math, sys
import numpy as np
from PIL import Image, ImageOps
from scipy import ndimage
SP = sys.argv[1]; RUN = sys.argv[2]
SRC = 'C:/Users/user/Documents/tierra-rejada/reference/iCloud Photos/iCloud Photos/'; A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/' + RUN + '/shots/'
poses = json.load(open(SP + '/audit/poses_new.json'))


def skyline(img, game):
    a = np.asarray(img).astype(float); r, g, b = a[..., 0], a[..., 1], a[..., 2]; L = a.mean(2)
    if game: sky = (b > g + 15) & (b > r + 35)
    else: sky = ((b >= r - 12) & (L > 120)) | (L > 200)
    lab, n = ndimage.label(sky); top = set(np.unique(lab[0])) - {0}; sky = np.isin(lab, list(top))   # sky must touch the top edge
    H = sky.shape[0]; col = np.argmin(sky, axis=0).astype(float)                                       # first non-sky row
    col[sky.all(0)] = np.nan; col[col == 0] = np.nan
    return col / H


res = []
for p in poses:
    try:
        ph = ImageOps.exif_transpose(Image.open(SP + '/newjpg/' + p['photo'] + '.jpg')).convert('RGB'); ph.thumbnail((300, 400))
        pano = Image.open(A + 'pano_' + p['photo'] + '.png').convert('RGB').resize((1000, 500))
    except Exception as e:
        print('skip', p['photo'], e); continue
    V = math.radians(p['vfov']); tv = math.tan(V / 2); aspect = ph.size[0] / ph.size[1]; th = tv * aspect; tp = 2 * tv   # tan of half fov: photo horizontal, pano horizontal
    sp, sg = skyline(ph, False), skyline(pano, True)
    if np.isfinite(sp).sum() < 20 or np.isfinite(sg).sum() < 50: res.append(dict(p, dh=0.0, dp=0.0, err=None)); print(p['photo'], 'no usable skyline'); continue
    u = (np.arange(len(sp)) + 0.5) / len(sp) * 2 - 1; az_p = np.degrees(np.arctan(u * th)); el_p = np.degrees(np.arctan((0.5 - sp) * 2 * tv))
    ug = (np.arange(len(sg)) + 0.5) / len(sg) * 2 - 1; az_g = np.degrees(np.arctan(ug * tp)); el_g = np.degrees(np.arctan((0.5 - sg) * 2 * tv))
    best = (1e9, 0, 0); lim = max(2.0, np.degrees(np.arctan(tp)) - np.degrees(np.arctan(th)) - 1)
    for dh in np.arange(-min(30, lim), min(30, lim) + 0.01, 0.5):
        eg = np.interp(az_p + dh, az_g, el_g, left=np.nan, right=np.nan); m = np.isfinite(eg) & np.isfinite(el_p)
        if m.sum() < 0.4 * len(sp): continue
        dp = np.median(el_p[m] - eg[m]); dp = float(np.clip(dp, -20, 20)); err = np.median(np.abs(el_p[m] - dp - eg[m]))
        if err < best[0]: best = (float(err), float(dh), dp)
    res.append(dict(p, dh=best[1], dp=-best[2], err=best[0])); print('%s heading %+.1f deg, pitch %+.1f deg, skyline error %.2f deg' % (p['photo'], best[1], -best[2], best[0]))
# aligned portrait shots (photo aspect 3:4, EXIF fov), absolute eye position 1.6 m above the course surface
c = json.load(open(SP + '/veg_views.json')); shots = []
for p in res:
    ok = p['err'] is not None and p['err'] < 3.0; p['used'] = ok
    h = math.radians(float(p['heading']) + (p['dh'] if ok else 0.0)); pt = math.radians(p['dp'] if ok else -8.0)   # dp: camera pitch, + = up (skyline lower in the photo than in the level render)
    x, y, z = p['eye']; d = 100.0; shots.append(dict(name='veg_' + p['photo'], pos=[x, y, z], lookAt=[x + d * math.sin(h) * math.cos(pt), y + d * math.sin(pt), z + d * math.cos(h) * math.cos(pt)], aboveGround=False, fov=p['vfov']))
c['shots'] = shots; c['steps'] = []; c['shotWidth'] = 900; c['shotHeight'] = 1200; c['saveScene'] = False; json.dump(c, open(SP + '/audit_aligned_new.json', 'w'))
json.dump(res, open(SP + '/audit/aligned_new.json', 'w'))
print('aligned', sum(1 for p in res if p.get('used')), 'of', len(res))
