"""Measure photo vs render colour in matching boxes and propose the OverallColor multiplier that closes the gap.

Replaces eyeballing screenshots. Reads the comparison method and targets from rules/look.json.

    python tools/colour_match.py photo.jpg render.png --box 120,340,80,60 --box 400,300,80,60 [--target green|sand|rough|scrub|cliff]
    python tools/colour_match.py photo.jpg render.png --mask photo_mask.png --render-mask render_mask.png
    python tools/colour_match.py --render-only render.png --box ... --target green     (compare to the rule target instead of a photo)

Box = x,y,w,h in pixels of the image it belongs to. Give --box pairs in order photo,render,photo,render... or one list to use the
same boxes in both (same pose renders). Output: per-box median RGB, r/g, b/g for both, the ratio gap, the suggested multiplier
(render * mul = photo) after the render's known warm offset, and PASS/FAIL against the target band. --json writes it for the Unity side.
"""
import argparse, json, os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rules import rules

R = rules()["look"]

TARGETS = {
    "green": {"rgb": R["measure"]["green_photo_median_rgb"]},
    "sand": {"rgb": [int(v * 255) for v in R["sand_rgb"]], "bg_vs_concrete_pct": R["sand_bg_vs_concrete_pct"]},
}
try:
    B = rules()["brush"]["color"]
    TARGETS["scrub"] = {"bg": B["scrub_bg"]}
    TARGETS["cliff"] = {"bg": B["cliff_bg"], "bg_floor": B["cliff_bg_floor"]}
    T = rules()["trees"]["color_targets"]
    TARGETS["pepper"] = {"rg": T["pepper_rg"]}
    TARGETS["eucalyptus"] = {"rg": T["eucalyptus_rg"], "bg": T["eucalyptus_bg"]}
    TARGETS["pine"] = {"rg": T["pine_rg"], "bg": T["pine_bg"]}
except KeyError:
    pass


def load(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(float)


def box_pixels(img, box):
    x, y, w, h = box
    return img[y:y + h, x:x + w].reshape(-1, 3)


def mask_pixels(img, mask_path):
    m = np.asarray(Image.open(mask_path).convert("L")) > 127
    return img[m]


def stats(px):
    med = np.median(px, axis=0)
    r, g, b = med
    return {"rgb": [round(float(v), 1) for v in med], "rg": round(float(r / g), 3), "bg": round(float(b / g), 3), "n": int(len(px))}


def in_band(v, band):
    return band[0] <= v <= band[1]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("photo", nargs="?")
    ap.add_argument("render", nargs="?")
    ap.add_argument("--render-only", metavar="RENDER")
    ap.add_argument("--box", action="append", default=[], help="x,y,w,h (repeat)")
    ap.add_argument("--mask"); ap.add_argument("--render-mask")
    ap.add_argument("--target", choices=sorted(TARGETS), help="rule target band to judge against")
    ap.add_argument("--no-warm-offset", action="store_true", help="skip the render warm offset from rules/look.json")
    ap.add_argument("--json", help="write result here")
    a = ap.parse_args()

    warm = 0.0 if a.no_warm_offset else R["measure"]["render_warm_offset"]
    boxes = [tuple(int(v) for v in b.split(",")) for b in a.box]
    out = {"method": R["measure"]["method"], "warm_offset_applied": warm, "pairs": []}

    if a.render_only:
        ren = load(a.render_only)
        sets = [box_pixels(ren, b) for b in boxes] if boxes else [mask_pixels(ren, a.render_mask)]
        for i, px in enumerate(sets):
            s = stats(px); s["rg_corrected"] = round(s["rg"] - warm, 3)
            out["pairs"].append({"box": i, "render": s})
    else:
        if not (a.photo and a.render):
            ap.error("photo and render, or --render-only")
        pho, ren = load(a.photo), load(a.render)
        if boxes:
            if len(boxes) % 2 == 0 and len(boxes) >= 2 and not all(b == boxes[0] for b in boxes):
                pairs = list(zip(boxes[0::2], boxes[1::2]))
            else:
                pairs = [(b, b) for b in boxes]
            sets = [(box_pixels(pho, pb), box_pixels(ren, rb)) for pb, rb in pairs]
        else:
            sets = [(mask_pixels(pho, a.mask), mask_pixels(ren, a.render_mask or a.mask))]
        for i, (pp, rp) in enumerate(sets):
            ps, rs = stats(pp), stats(rp)
            rs["rg_corrected"] = round(rs["rg"] - warm, 3)
            mul = [round(float(p / r), 3) if r else None for p, r in zip(ps["rgb"], rs["rgb"])]
            out["pairs"].append({"box": i, "photo": ps, "render": rs,
                                 "gap": {"rg": round(rs["rg_corrected"] - ps["rg"], 3), "bg": round(rs["bg"] - ps["bg"], 3)},
                                 "suggested_overall_color_mul": mul})

    if a.target:
        t = TARGETS[a.target]
        for p in out["pairs"]:
            r = p["render"]; verdict = []
            if "rg" in t: verdict.append(("r/g", in_band(r["rg_corrected"], t["rg"])))
            if "bg" in t: verdict.append(("b/g", in_band(r["bg"], t["bg"]) and r["bg"] >= t.get("bg_floor", 0)))
            if "rgb" in t:
                tol = R["measure"].get("rgb_tol", 12); d = max(abs(x - y) for x, y in zip(r["rgb"], t["rgb"])); verdict.append(("rgb within %d" % tol, d <= tol))
            p["target"] = a.target; p["checks"] = {k: ("PASS" if ok else "FAIL") for k, ok in verdict}
            p["pass"] = all(ok for _, ok in verdict)

    for p in out["pairs"]:
        line = "box %d" % p["box"]
        if "photo" in p: line += "  photo %s r/g %.3f b/g %.3f" % (p["photo"]["rgb"], p["photo"]["rg"], p["photo"]["bg"])
        line += "  render %s r/g %.3f(corr) b/g %.3f" % (p["render"]["rgb"], p["render"]["rg_corrected"], p["render"]["bg"])
        if "suggested_overall_color_mul" in p: line += "  mul %s" % p["suggested_overall_color_mul"]
        if "pass" in p: line += "  %s %s" % (p["target"], "PASS" if p["pass"] else "FAIL " + str(p["checks"]))
        print(line)
    if a.json:
        with open(a.json, "w") as fh: json.dump(out, fh, indent=1)
        print("wrote", a.json)


if __name__ == "__main__":
    main()
