# Cleaned satellite base for the OPCD _Sat materials (the rough channel takes its base colour from the photo): road, parking and
# concrete pixels (saturation < 0.14, brightness > 0.45) fade to a dry-grass shoulder tan with two-scale noise; 4096 px output.
# BoJa / coordinator Oct 30: the strip right of the 15 cart path was a smeared white-and-tan copy of the highway.
import numpy as np, sys
from PIL import Image
from scipy import ndimage
Image.MAX_IMAGE_PIXELS = None
src, dst = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(src).convert('RGB').resize((4096, 4096), Image.LANCZOS)).astype(np.float32) / 255
mx = a.max(2); mn = a.min(2); L = a.mean(2); sat = (mx - mn) / np.maximum(mx, 1e-3)
pave = ndimage.binary_dilation(ndimage.binary_opening((sat < 0.14) & (L > 0.45), iterations=1), iterations=2)
soft = ndimage.gaussian_filter(pave.astype(np.float32), 2.0)
rng = np.random.default_rng(3)
n1 = ndimage.gaussian_filter(rng.normal(0, 1, L.shape).astype(np.float32), 1.0); n2 = ndimage.gaussian_filter(rng.normal(0, 1, L.shape).astype(np.float32), 12.0)
n = 0.5 * n1 / n1.std() + 0.5 * n2 / n2.std()
n3 = ndimage.gaussian_filter(rng.normal(0, 1, L.shape).astype(np.float32), 40.0); n3 /= n3.std()
# dull grey-brown / tan shoulder (coordinator: ~0.55 0.50 0.40, 40 % less saturated than the first pass), large-scale patches
dry = 0.74 * np.stack([0.55 + 0.035 * n + 0.04 * n3, 0.50 + 0.035 * n + 0.03 * n3, 0.40 + 0.03 * n + 0.02 * n3], -1)   # x0.74: the _Sat shader brightens its base (first render read ~0.75 for 0.55)
# patchy darker green-grey sage flecks
sage = ndimage.gaussian_filter((rng.random(L.shape) < 0.035).astype(np.float32), 1.6); sage = np.clip(sage / sage.max() * 3, 0, 1) * (n3 > -0.3)
dry = dry * (1 - sage[..., None] * 0.55) + np.array([0.36, 0.40, 0.33]) * (sage[..., None] * 0.55)
out = a * (1 - soft[..., None] * 0.85) + dry * (soft[..., None] * 0.85)
Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(dst, quality=93)
print('pavement %.1f%%' % (pave.mean() * 100))
