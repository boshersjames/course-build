"""Golden grass only where the satellite drape is tan: Moorpark version of tierra-rejada
vegetation/build/make_sat_masks.py (same thresholds, tuned on Tierra's flyover review), for the 3000 m square.

Writes into terrain_masks/ (1 px = 1 m, north up), next to the masks from `coursekit.py vegetation`:
  grass_dry_hillside_sat.mask   grass_dry_hillside.mask, unfiltered (Dry hillside grass, Hillside oats; see below)
  grass_deep_rough_sat.mask     grass_deep_rough.mask x tan       (Deep rough wild oats, Deep rough foxtail)
  grass_deep_rough_green.mask   grass_deep_rough.mask x (1 - tan) (green deep rough where the drape is green)
  brush_sage_scrub_sat.mask     brush_sage_scrub.mask x tan       (Scrub, mustard)
No hole-specific hill or boost areas yet (Tierra's are in its own repo).
    python tools/make_sat_masks.py
"""
import json
import os

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
MASKS = os.path.join(REPO, "terrain_masks")
S = int(json.load(open(os.path.join(REPO, "course.json")))["size"])
WARM = (0.03, 0.08)
LUMA = (0.32, 0.44)


def smoothstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def tan_map():
    sat = Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB")
    a = np.asarray(sat.resize((S, S), Image.BOX), dtype=np.float32) / 255.0
    a = np.stack([gaussian_filter(a[..., c], 1.0) for c in range(3)], -1)
    luma = a @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    return smoothstep(a[..., 0] - a[..., 1], WARM[0], WARM[1]) * smoothstep(luma, LUMA[0], LUMA[1])


def load(name):
    return np.asarray(Image.open(os.path.join(MASKS, name)).convert("L"), dtype=np.float32) / 255.0


def save(v, name):
    Image.fromarray(np.clip(v * 255 + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(MASKS, name), format="PNG")
    print("%-30s mean %.3f" % (name, v.mean()))


if __name__ == "__main__":
    tan = tan_map()
    hs, dr, br = load("grass_dry_hillside.mask"), load("grass_deep_rough.mask"), load("brush_sage_scrub.mask")
    # coursekit already put the golden-grass band only on natural ground (orchard / woodland edges and the 4-9 m band),
    # and Moorpark's NAIP drape is greener than Tierra's (only 2 % of that band reads tan), so it is not filtered again
    save(hs, "grass_dry_hillside_sat.mask")
    save(dr * tan, "grass_deep_rough_sat.mask")
    save(dr * (1 - tan), "grass_deep_rough_green.mask")
    save(br * tan, "brush_sage_scrub_sat.mask")
