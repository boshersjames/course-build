"""Unity screenshots next to the drone frames, per hole: top row drone tee + green, bottom row Unity tee + green.
    python tools/compare_drone.py <shots folder> Ridgeline_9 Creekside_8 CanyonCrest_4 ...   -> previews/unity_vs_drone/
"""
import glob, os, sys
from PIL import Image, ImageDraw, ImageFont

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
shots = sys.argv[1]
out = os.path.join(REPO, "previews", "unity_vs_drone"); os.makedirs(out, exist_ok=True)
font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 26)
W, H = 960, 540
for tag in sys.argv[2:]:
    d = os.path.join(REPO, "drone", "by_hole", tag)
    tee = sorted(glob.glob(os.path.join(d, "*_tee_*.jpg")))[0]
    green = sorted(glob.glob(os.path.join(d, "*_green_*.jpg")))[0]
    u = tag.replace("CanyonCrest", "Canyon_Crest")
    ut, ug = os.path.join(shots, u + "_tee.png"), os.path.join(shots, u + "_green.png")
    img = Image.new("RGB", (2 * W, 2 * H))
    for k, (p, lab) in enumerate([(tee, "drone: tee"), (green, "drone: green"), (ut, "Unity: tee"), (ug, "Unity: green")]):
        im = Image.open(p).convert("RGB").resize((W, H))
        ImageDraw.Draw(im).text((12, 10), tag.replace("_", " ").replace("CanyonCrest", "Canyon Crest") + "  " + lab, fill=(255, 255, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
        img.paste(im, ((k % 2) * W, (k // 2) * H))
    img.save(os.path.join(out, tag + ".jpg"), quality=85)
    print("wrote", tag)
