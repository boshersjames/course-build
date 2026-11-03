# Grade the satellite base colour the OPCD rough shader uses (TierraRejada_satellite_graded.jpg, 8192 px over the 2000 m square)
# toward the Oct 29 photos: outer hillside rough -> dormant grey scrub and tan dry grass (photo hillside medians), in-play rough a
# little duller (browning bermuda), turf untouched. Masks from AutomationOutput/vegetation/surface_class.png, feathered 6 m.
import os, shutil, json
import numpy as np
from PIL import Image
from scipy import ndimage
from skimage import color
Image.MAX_IMAGE_PIXELS = None
F = 'C:/GSP/GSPBaseProject_v4_2025_12_02/Assets/TierraRejada/TierraRejada_satellite_graded.jpg'
BK = 'C:/GSP/GSPBaseProject_v4_2025_12_02/Backups/veg_textures_1029/TierraRejada_satellite_graded.jpg'
if not os.path.exists(BK): shutil.copy2(F, BK)
src = np.asarray(Image.open(BK).convert('RGB')).astype(np.float32) / 255.0; H, W = src.shape[:2]
cls = np.asarray(Image.open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/vegetation/surface_class.png').convert('RGB'))   # same orientation as the satellite jpg (row 0 = z 0; checked against the fairways)
def mask(ch):
    m = ndimage.gaussian_filter((cls[..., ch] > 128).astype(np.float32), 3.0)   # ~6 m feather at 1 m / px
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(np.float32) / 255.0
# rough more than 30 m from any turf (fairway/green/tee) reads as hillside too (the green slopes above fairways, IMG_4636)
turf = cls[..., 2] > 128; far = ndimage.distance_transform_edt(~turf) > 30
cls = cls.copy(); sel = (cls[..., 1] > 128) & far; cls[sel, 0] = 255; cls[sel, 1] = 0
mo, mr = mask(0), mask(1)
# keep tree canopy / orchards / irrigated green as they are (the avocado orchards below 12 and 1, oak groves): dark, green-leaning pixels
_g = src[..., 1] - (src[..., 0] + src[..., 2]) / 2; _L = src.mean(2)
veg = np.clip((_g - 0.015) / 0.04, 0, 1) * np.clip((0.42 - _L) / 0.12, 0, 1)
veg = np.asarray(Image.fromarray((veg * 255).astype(np.uint8)).filter(__import__('PIL.ImageFilter', fromlist=['x']).GaussianBlur(2))).astype(np.float32) / 255.0
mo = mo * (1 - veg); mr = mr * (1 - 0.7 * veg)
out = np.empty_like(src); step = 1024
targets = {'outer': (np.array([124, 116, 110]) / 255.0, 0.4, 1.05), 'rough': (None, 0.85, 1.0)}
stats = {}
# global Lab means per class (sampled) so the shift is a single colour move per class, keeping all the satellite's local detail
lab_s = color.rgb2lab(src[::16, ::16]); mo_s, mr_s = mo[::16, ::16] > 0.5, mr[::16, ::16] > 0.5
mean_o = lab_s[mo_s].mean(0); mean_r = lab_s[mr_s].mean(0); tl = color.rgb2lab(targets['outer'][0][None, None, :])[0, 0]; tr_ = color.rgb2lab((np.array([92, 94, 58]) / 255.0)[None, None, :])[0, 0]   # in-play rough: browning bermuda, a step lighter and yellower than the irrigated turf
stats['outer_before'] = (color.lab2rgb(mean_o[None, None, :])[0, 0] * 255).round().tolist(); stats['rough_before'] = (color.lab2rgb(mean_r[None, None, :])[0, 0] * 255).round().tolist()
for y in range(0, H, step):
    lab = color.rgb2lab(src[y:y + step])
    o = lab.copy(); o[..., 0] = np.clip(tl[0] + (lab[..., 0] - mean_o[0]) * 1.05, 0, 100); o[..., 1] = tl[1] + (lab[..., 1] - mean_o[1]) * 0.4; o[..., 2] = tl[2] + (lab[..., 2] - mean_o[2]) * 0.4
    r = lab.copy(); r[..., 0] = np.clip(tr_[0] + (lab[..., 0] - mean_r[0]), 0, 100); r[..., 1] = tr_[1] + (lab[..., 1] - mean_r[1]) * 0.8; r[..., 2] = tr_[2] + (lab[..., 2] - mean_r[2]) * 0.8
    a_o = mo[y:y + step, :, None]; a_r = mr[y:y + step, :, None] * (1 - a_o)
    res = lab * (1 - a_o - a_r) + o * a_o + r * a_r
    out[y:y + step] = np.clip(color.lab2rgb(res), 0, 1)
Image.fromarray((out * 255).astype(np.uint8)).save(F, quality=92)
lab_a = color.rgb2lab(out[::16, ::16]); stats['outer_after'] = (color.lab2rgb(lab_a[mo_s].mean(0)[None, None, :])[0, 0] * 255).round().tolist(); stats['rough_after'] = (color.lab2rgb(lab_a[mr_s].mean(0)[None, None, :])[0, 0] * 255).round().tolist()
json.dump(stats, open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/vegetation/satellite_grade.json', 'w')); print(stats)
Image.fromarray((out[::8, ::8] * 255).astype(np.uint8)).save('C:/Users/user/AppData/Local/Temp/claude/C--Users-user-Documents-tierra-rejada/3efaf824-bf28-4c71-a80a-e8bab2dd9e22/scratchpad/veg/sat_after_small.jpg', quality=80)
Image.fromarray((src[::8, ::8] * 255).astype(np.uint8)).save('C:/Users/user/AppData/Local/Temp/claude/C--Users-user-Documents-tierra-rejada/3efaf824-bf28-4c71-a80a-e8bab2dd9e22/scratchpad/veg/sat_before_small.jpg', quality=80)
