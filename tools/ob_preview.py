"""Draw each pairing GKD's OB, hazards, tees, aim points and pins over the satellite photo -> ob_preview.jpg"""
import json, glob, sys
import numpy as np
from PIL import Image, ImageDraw
c = json.load(open("course.json")); S = int(c["size"])
sat = Image.open("satellite.jpg").convert("RGB").resize((S, S))
tiles = []
for f in sorted(glob.glob("Moorpark_*.GKD")):
    d = json.load(open(f, encoding="utf-8-sig"))
    im = sat.copy(); dr = ImageDraw.Draw(im)
    P = lambda q: (q["x"], S - q["z"])
    dr.line([P(q) for q in d["pOOB"]["coords"]] + [P(d["pOOB"]["coords"][0])], fill=(255, 255, 255), width=5)
    for h in d["Hazards"]:
        dr.polygon([P(q) for q in h["coords"]], outline=(255, 60, 60) if h["innerOOB"] else (40, 120, 255), width=4)
    for h in d["Holes"]:
        for t in h["Tees"]:
            if t["Position"]:
                x, y = P(t["Position"]); col = (255, 255, 0) if t["TeeType"].startswith("Aim") else (0, 255, 255) if t["TeeType"] == "GreenCenterPoint" else (255, 120, 0)
                dr.ellipse([x - 5, y - 5, x + 5, y + 5], fill=col)
        x, y = P(h["Pins"][0]["Position"]); dr.text((x + 8, y - 8), str(h["HoleNumber"]), fill=(255, 255, 255))
    im = im.crop((0, 700, S, 2300)); im.thumbnail((1800, 1800))
    ImageDraw.Draw(im).text((10, 10), f.replace(".GKD", "") + ": white = OB line, red = interior OB, blue = water", fill=(255, 255, 255))
    tiles.append(im)
out = Image.new("RGB", (tiles[0].width, sum(t.height for t in tiles)))
y = 0
for t in tiles:
    out.paste(t, (0, y)); y += t.height
out.save("ob_preview.jpg", quality=85); print("Wrote ob_preview.jpg")
