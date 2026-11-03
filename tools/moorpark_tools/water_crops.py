"""Satellite crops (with OSM water) around every water candidate and named spots, for checking by eye.
    python tools/water_crops.py  -> build/water/crops.jpg
"""
import json, os
from PIL import Image, ImageDraw, ImageFont
REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
S = 3000.0
feats = json.load(open(os.path.join(REPO, "build", "water", "water.json")))
sat = Image.open(os.path.join(REPO, "satellite.jpg")).convert("RGB"); k = sat.width / S
spots = [("C9 green", 1300.5, 1700.2), ("R9 green", 1309.9, 1776.1), ("CC1 fairway", 1700, 1560), ("CC9 green", 1535, 1688)]
for i, f in enumerate(feats):
    if "centre" in f: spots.append(("#%d %s" % (i, "/".join(f["holes"])), f["centre"][0], f["centre"][1]))
font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 20)
tiles = []
for name, x, z in spots:
    r = 70
    t = sat.crop((int((x - r) * k), int((S - z - r) * k), int((x + r) * k), int((S - z + r) * k))).resize((420, 420))
    d = ImageDraw.Draw(t)
    for f in feats:
        if "pts" in f:
            P = [((px - x + r) * 420 / (2 * r), (S - pz - (S - z) + r) * 420 / (2 * r)) for px, pz in f["pts"]]
            d.line(P, fill=(0, 200, 255), width=3)
    d.text((6, 6), name, fill=(255, 255, 0), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
    tiles.append(t)
cols = 4
out = Image.new("RGB", (cols * 420, ((len(tiles) + cols - 1) // cols) * 420))
for i, t in enumerate(tiles): out.paste(t, ((i % cols) * 420, (i // cols) * 420))
out.save(os.path.join(REPO, "build", "water", "crops.jpg"), quality=85)
print(len(tiles), "crops")
