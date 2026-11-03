"""The Moorpark three-horse logo on the range hillside: trace BoJa's logo image (gold strokes on white), then find the
flip / turn / size / position that best matches the white shape in the satellite at the far end of the range.
Writes logo/logo_strokes.json (stroke polygons in course metres, x east, z north) and logo/logo_fit.jpg.
    python tools/logo_fit.py <logo image>
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage
from skimage import measure

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(REPO, "logo"); os.makedirs(OUT, exist_ok=True)
S = 3000.0
PX = 6.0                        # pixels per metre for the fitting grids
X0, Z0, W = 1470.0, 1380.0, 60.0  # satellite window (course metres)

# --- logo strokes: gold, not the text below the emblem
lg = np.asarray(Image.open(sys.argv[1]).convert("RGB"), dtype=np.float32)
gold = (lg[..., 0] > 120) & (lg[..., 2] < lg[..., 0] - 50) & (lg[..., 1] > 80)
rows = np.nonzero(gold.any(1))[0]
lab, n = ndimage.label(gold)
sizes = ndimage.sum(gold, lab, range(1, n + 1))
gold = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 30])
# drop the "Moorpark" script and "COUNTRY CLUB" text: keep strokes above the script's top line
cut = 272                       # the "Moorpark" script starts here (row profile of the 447 px logo); the heads are above
emblem = gold.copy(); emblem[cut:, :] = False
lab, n = ndimage.label(emblem)
sizes = ndimage.sum(emblem, lab, range(1, n + 1))
emblem = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 60])
yy, xx = np.nonzero(emblem)
emb = emblem[yy.min():yy.max() + 1, xx.min():xx.max() + 1]
eh, ew = emb.shape
print("logo emblem %dx%d px, %d strokes" % (ew, eh, ndimage.label(emb)[1]))

# --- satellite white shape
sat = Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB"); k = sat.width / S
crop = sat.crop((int(X0 * k), int((S - Z0 - W) * k), int((X0 + W) * k), int((S - Z0) * k))).resize((int(W * PX), int(W * PX)), Image.BICUBIC)
c = np.asarray(crop, dtype=np.float32)
white = (c.mean(2) > 175) & ((c.max(2) - c.min(2)) < 45)
lab, n = ndimage.label(white)
sizes = ndimage.sum(white, lab, range(1, n + 1))
big = np.argmax(sizes) + 1
# the logo sits around (1501.5, 1410.5); the cart path below it is white too, so only look within 12 m of there
cx, cy = (1501.5 - X0) * PX, (Z0 + W - 1410.5) * PX
white = ndimage.binary_dilation(white, iterations=1) & (np.hypot(*np.indices(white.shape) - np.array([[[cy]], [[cx]]])) < 12 * PX)
target = ndimage.gaussian_filter(white.astype(float), 2.5)
wy, wx = np.nonzero(white)
print("satellite logo centre (%.1f, %.1f), extent %.1f x %.1f m" % (X0 + wx.mean() / PX, Z0 + W - wy.mean() / PX, (wx.max() - wx.min()) / PX, (wy.max() - wy.min()) / PX))


def render(flip, ang, width_m, dx, dy):
    """emblem placed in the window grid: width in metres, centre offset (px), rotation (deg, image coords)"""
    e = emb[:, ::-1] if flip else emb
    im = Image.fromarray((e * 255).astype(np.uint8))
    sc = width_m * PX / ew
    im = im.resize((max(1, int(ew * sc)), max(1, int(eh * sc))), Image.BILINEAR).rotate(ang, expand=True, resample=Image.BILINEAR)
    g = Image.new("L", (int(W * PX), int(W * PX)), 0)
    g.paste(im, (int(wx.mean() + dx - im.width / 2), int(wy.mean() + dy - im.height / 2)))
    return np.asarray(g, dtype=np.float32) / 255


# The range tee is downhill to the NORTH (terrain 15-30 m lower there), looking south up the slope, so the logo is laid
# out to read upright from there: turned about 180 degrees in plan (a free search over every turn and mirror also landed
# at 192). Fit only the turn near that, the size around the ~21 m the satellite shows and the position.
def score(m, blur):
    m = ndimage.gaussian_filter(ndimage.grey_dilation(m, size=(5, 5)), blur)
    return (m * target).sum() / np.sqrt((m * m).sum() * (target * target).sum() + 1e-9)

flip, best = False, None
for ang in range(160, 205, 4):
    for wm in np.arange(18, 23.1, 1.0):
        for dx in range(-18, 19, 6):
            for dy in range(-18, 19, 6):
                sc = score(render(flip, ang, wm, dx, dy), 2.0)
                if best is None or sc > best[0]: best = (sc, ang, wm, dx, dy)
sc, ang, wm, dx, dy = best
for a in range(ang - 3, ang + 4):
    for w2 in np.arange(wm - 1, wm + 1.01, 0.5):
        for ddx in range(dx - 4, dx + 5, 2):
            for ddy in range(dy - 4, dy + 5, 2):
                s2 = score(render(flip, a, w2, ddx, ddy), 1.5)
                if s2 > sc: sc, ang, wm, dx, dy = s2, a, w2, ddx, ddy
# the matcher favours a smaller logo (its thin strokes fall inside the blurred white); the shape in the satellite is
# 21 m across, so set that size and re-centre
wm = 21.0
best = None
for ddx in range(-24, 25, 2):
    for ddy in range(-24, 25, 2):
        s2 = score(render(flip, ang, wm, ddx, ddy), 1.5)
        if best is None or s2 > best[0]: best = (s2, ddx, ddy)
sc, dx, dy = best
print("best fit: flip %s, turn %d deg, width %.1f m, offset (%d,%d) px, score %.3f" % (flip, ang, wm, dx, dy, sc))

# --- stroke outlines -> course metres
fit = ndimage.grey_dilation(render(flip, ang, wm, dx, dy), size=(5, 5)) > 0.5      # ~0.8 m wide painted lines
polys = []
for cont in measure.find_contours(np.pad(fit, 1).astype(float), 0.5):
    cont = measure.approximate_polygon(cont, 0.6)
    if len(cont) < 4: continue
    pts = [[round(X0 + (p[1] - 1) / PX, 3), round(Z0 + W - (p[0] - 1) / PX, 3)] for p in cont[:-1]]
    a = 0.5 * abs(sum(pts[i][0] * pts[i - 1][1] - pts[i - 1][0] * pts[i][1] for i in range(len(pts))))
    if a < 0.15: continue
    polys.append(pts)
json.dump({"flip": flip, "turn_deg": ang, "width_m": float(wm), "score": float(sc), "polys": polys}, open(os.path.join(OUT, "logo_strokes.json"), "w"))
ov = np.asarray(crop).copy()
ov[fit] = (ov[fit] * 0.3 + np.array([255, 0, 0]) * 0.7).astype(np.uint8)
Image.fromarray(np.hstack([np.asarray(crop), ov])).save(os.path.join(OUT, "logo_fit.jpg"), quality=90)
print(len(polys), "outline loops written")
