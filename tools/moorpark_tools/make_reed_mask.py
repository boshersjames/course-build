"""terrain_masks/pond_reeds.mask: the Tierra pond-edge band (reeds and foxtail, "Pond edge" layers in the SoCal set):
1 to 4.5 m out from every water outline in course.svg, never on fairway, tee, green or bunker. 1 px = 1 m, north up.
    python tools/make_reed_mask.py
"""
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import svg_fix

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
os.chdir(REPO)
lab = svg_fix.load_layers("course.svg")                     # 0.5 m, row 0 = north
water = lab == svg_fix.WATER
d = ndimage.distance_transform_edt(~water) * svg_fix.R
band = (d >= 1.0) & (d <= 4.5) & ~np.isin(lab, (svg_fix.FAIRWAY, svg_fix.TEE, svg_fix.GREEN, svg_fix.BUNKER))
soft = np.clip(1.0 - (d - 3.0) / 1.5, 0, 1) * band            # full to 3 m, fading out by 4.5 m
img = Image.fromarray((soft * 255).astype(np.uint8)).resize((svg_fix.S, svg_fix.S), Image.BILINEAR)
img.save(os.path.join(REPO, "terrain_masks", "pond_reeds.mask"), format="PNG")
print("pond_reeds.mask: %.0f m2 of pond edge" % (np.asarray(img).astype(float).sum() / 255))
