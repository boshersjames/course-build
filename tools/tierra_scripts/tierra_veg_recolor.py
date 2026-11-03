# Tierra vegetation recolour from the Oct 29 photos (colours measured from IMG_4621-4725, 4798, 5082).
# Each texture (or atlas cell) keeps its own detail: in Lab, its alpha-weighted mean a/b is moved to the target's, chroma
# around the mean is scaled, and lightness is shifted to the target's L. Originals are copied to Backups/veg_textures_1029 first.
import os, shutil, json, sys
import numpy as np
from PIL import Image
from skimage import color
R = 'C:/GSP/GSPBaseProject_v4_2025_12_02/Assets/'; BK = 'C:/GSP/GSPBaseProject_v4_2025_12_02/Backups/veg_textures_1029/'; os.makedirs(BK, exist_ok=True)
V = R + 'TierraRejada/Vegetation/Textures/'
ALB = 0.85   # texture albedo vs the photo median (sunlit photo -> albedo, matches the current brightness in GSPro)
def T(rgb): return np.array(rgb, float) * ALB
# target colours (photo medians) and chroma scale
JOBS = {
    R + 'Resources/Textures/terrainopcdmatchv4.png': [(None, T((128, 112, 90)), 0.55, 1.25)],          # dry grass / dirt hillside (4719, 5082, 4621)
    V + 'TR_Detail_Buckwheat.png': [(None, T((116, 108, 108)), 0.3, 1.0)],                                 # dormant buckwheat, rust heads muted
    V + 'TR_Detail_GreyBuckwheat.png': [(None, T((120, 114, 112)), 0.4, 1.0)],
    V + 'TR_Detail_PurpleSage.png': [(None, T((116, 118, 110)), 0.7, 1.0)],                               # grey purple sage (4629, 4636)
    V + 'TR_Detail_Sage.png': [(None, T((118, 122, 96)), 0.6, 1.0)],                                      # California sagebrush grey-green (4645, 4642)
    V + 'TR_Detail_CoyoteBrush.png': [(None, T((96, 104, 70)), 0.7, 1.0)],
    V + 'TR_Detail_DeadBrush.png': [(None, T((114, 110, 106)), 0.35, 1.0)],                                 # dead grey sticks (4645 top, 4724)
    V + 'TR_Detail_Mustard.png': [(None, T((126, 120, 108)), 0.3, 1.0)],                                   # dried mustard, Oct
    V + 'TR_Detail_GreenScrub.png': [(None, T((86, 98, 62)), 0.8, 1.0)],                                  # laurel sumac / toyon olive (4722)
    V + 'TR_Grass_GreenDeep.png': [(None, T((104, 106, 72)), 0.55, 1.0)],                                 # hillside "green" grass is olive-tan in Oct
    V + 'TR_Grass_GreenGold.png': [(None, T((122, 114, 92)), 0.4, 1.0)],
}
# shrub atlas: 4 x 4 cells of 512 px (row, col) -> target
ATLAS = {(0, 0): (116, 106, 106), (0, 1): (112, 104, 104), (0, 2): (120, 110, 108), (0, 3): (114, 110, 106), (1, 0): (88, 100, 60), (1, 1): (84, 98, 62), (1, 2): (126, 120, 108), (1, 3): (118, 124, 96),
         (2, 0): (116, 118, 110), (2, 1): (112, 114, 106), (2, 2): (96, 104, 70), (2, 3): (110, 104, 102), (3, 0): (122, 116, 110), (3, 1): (114, 106, 106), (3, 2): (116, 108, 106), (3, 3): (104, 112, 84)}
JOBS[V + 'TR_Shrubs_Atlas.png'] = [((r * 512, c * 512, 512, 512), T(t), 0.35 if t[0] > t[1] - 2 else 0.6, 1.0) for (r, c), t in ATLAS.items()]   # warm cells to grey-purple dormant scrub, low chroma


def shift(rgba, target, chroma, lscale):
    rgb = rgba[..., :3] / 255.0; a = rgba[..., 3] if rgba.shape[-1] == 4 else np.full(rgb.shape[:2], 255.0)
    m = a > 128
    if m.sum() < 50: return rgba
    lab = color.rgb2lab(rgb); tl = color.rgb2lab((target / 255.0)[None, None, :])[0, 0]
    mean = lab[m].mean(0)
    out = lab.copy(); out[..., 0] = np.clip(tl[0] + (lab[..., 0] - mean[0]) * lscale, 0, 100)
    out[..., 1] = tl[1] + (lab[..., 1] - mean[1]) * chroma; out[..., 2] = tl[2] + (lab[..., 2] - mean[2]) * chroma
    res = np.clip(color.lab2rgb(out) * 255, 0, 255)
    o = rgba.copy(); o[..., :3] = np.where(m[..., None] | (rgba.shape[-1] == 3), res, rgba[..., :3]); return o


report = {}
for f, jobs in JOBS.items():
    b = BK + os.path.basename(f)
    if not os.path.exists(b): shutil.copy2(f, b)                     # always recolour from the untouched original
    im = Image.open(b); mode = im.mode; arr = np.asarray(im.convert('RGBA' if 'A' in mode else 'RGB')).astype(float)
    before = arr[..., :3][arr[..., 3] > 128].mean(0) if arr.shape[-1] == 4 else arr.reshape(-1, 3).mean(0)
    for box, tgt, ch, ls in jobs:
        if box is None: arr = shift(arr, tgt, ch, ls)
        else:
            y, x, h, w = box; arr[y:y + h, x:x + w] = shift(arr[y:y + h, x:x + w], tgt, ch, ls)
    after = arr[..., :3][arr[..., 3] > 128].mean(0) if arr.shape[-1] == 4 else arr.reshape(-1, 3).mean(0)
    Image.fromarray(arr.astype(np.uint8), 'RGBA' if arr.shape[-1] == 4 else 'RGB').save(f)
    report[os.path.basename(f)] = dict(before=before.round().tolist(), after=after.round().tolist()); print(os.path.basename(f), before.round(), '->', after.round())
json.dump(report, open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/vegetation/recolor_report.json', 'w'), indent=1)
