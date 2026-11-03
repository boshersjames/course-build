"""Measure in-game brightness from screenshots: is the green readable (putt lines visible) and the sky dark enough for the tracer?

    python tools/brightness_check.py <screenshot.png> [...] [--green-box x,y,w,h] [--sky-rows 0.25] [--line-box x,y,w,h] [--json out.json]

The post-processing profile GSPro plays with is set once per course (course-build-look / greens: the shipped v4_Sunny_Pop at +2.3 EV
washed greens near white so the auto-putt lines could not be read, and lifted the sky so the ball tracer vanished). This measures a
screenshot taken IN GSPro (or the editor Game view with the post volume on; batch renders have no post and must not be used here):
  - green luminance: median Rec.601 luma over --green-box (default: a box in the lower-middle of the frame) vs rules look.brightness.green_luma
  - sky luminance: median luma over the top --sky-rows fraction (sky pixels only: blue-dominant or bright) vs look.brightness.sky_luma_max
  - line contrast: if --line-box covers a white putt line or a white marker, the Michelson contrast between the brightest 10% and the
    median of the box vs look.brightness.line_contrast_min
Prints one line per image with PASS/FAIL per check. Targets are provisional until calibrated on a screenshot BoJa accepts (rules note).
"""
import argparse, json, os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rules import rules


def luma(a): return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("images", nargs="+"); ap.add_argument("--green-box"); ap.add_argument("--sky-rows", type=float, default=0.25); ap.add_argument("--line-box"); ap.add_argument("--json")
    a = ap.parse_args()
    B = rules()["look"].get("brightness", {})
    gl = B.get("green_luma", [100, 160]); sm = B.get("sky_luma_max", 200); lc = B.get("line_contrast_min", 0.25)
    out = []
    for p in a.images:
        img = np.asarray(Image.open(p).convert("RGB")).astype(float); H, W = img.shape[:2]; L = luma(img)
        gb = [int(v) for v in a.green_box.split(",")] if a.green_box else [int(W * 0.3), int(H * 0.55), int(W * 0.4), int(H * 0.3)]
        g = L[gb[1]:gb[1] + gb[3], gb[0]:gb[0] + gb[2]]; gmed = float(np.median(g))
        top = img[: int(H * a.sky_rows)]; r, gch, b = top[..., 0], top[..., 1], top[..., 2]; skym = (b > gch + 8) & (b > r + 20) | (luma(top) > 225)
        smed = float(np.median(luma(top)[skym])) if skym.sum() > 100 else float("nan")
        res = {"image": os.path.basename(p), "green_luma": round(gmed, 1), "green_ok": gl[0] <= gmed <= gl[1], "sky_luma": round(smed, 1) if smed == smed else None, "sky_ok": (smed <= sm) if smed == smed else None, "sky_px": int(skym.sum())}
        if a.line_box:
            lb = [int(v) for v in a.line_box.split(",")]; l = L[lb[1]:lb[1] + lb[3], lb[0]:lb[0] + lb[2]]
            hi = float(np.percentile(l, 90)); md = float(np.median(l)); c = (hi - md) / max(hi + md, 1)
            res.update({"line_contrast": round(c, 3), "line_ok": c >= lc})
        out.append(res)
        print("%-32s green luma %5.1f %s | sky luma %s %s%s" % (res["image"], gmed, "PASS" if res["green_ok"] else "FAIL (band %s)" % gl,
              ("%5.1f" % smed) if smed == smed else "  n/a", ("PASS" if res["sky_ok"] else "FAIL (max %s)" % sm) if res["sky_ok"] is not None else "(no sky in top rows)",
              (" | line contrast %.2f %s" % (res["line_contrast"], "PASS" if res["line_ok"] else "FAIL (min %s)" % lc)) if "line_contrast" in res else ""))
    if a.json: json.dump(out, open(a.json, "w"), indent=1)


if __name__ == "__main__":
    main()
