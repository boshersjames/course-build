"""Solve photo poses against a skyline built from the course heightmap and the LiDAR trees. No Unity.

    python tools/pose_solve.py [--index course_photos/index.csv] [--holes 10,11] [--redo] [--sheets] [--workers 4]

For every indexed photo with a draft pose (GPS + compass from photo_index.py): detect the sky/terrain boundary in the photo, build the
same boundary from the heightmap plus tree crowns at candidate poses (a grid of positions around the GPS spot, headings around the
compass, pitch solved), and keep the best. This is tierra_audit_align_new.py / tierra_pose_grid.py with the LiDAR standing in for the
Unity panorama, so it runs anywhere, before any render. Limits: the 2018 LiDAR is ~1 m, trees are the LiDAR list (not the live scene),
buildings are not modelled; a pose that passes here is a good render start, not a verdict pose until the render agrees.

VALIDATION (Tierra, 2026-11-03, 47 photos with Unity-skyline poses as reference): applying the solve made headings WORSE than the
raw compass (median error 20 deg vs 7.5; 25 worse, 6 better) and positions no better (3.4 m vs 3.0). The LiDAR skyline is too coarse
near the camera (1 m terrain, trees as spheres from the 2018 list) to beat the compass. So by default this tool is DIAGNOSTIC: it
writes the fit score into notes and flags photos whose compass is probably far off (a heading >30 deg away fits far better), and
leaves the pose columns alone. --apply writes the solved pose anyway. The real solve stays the Unity panorama fit on the PC.
The compass flag is weak too (same test: 5 right, 6 false alarms, 4 missed of 9 badly-off compasses; suggested headings mostly
wrong): treat it as "look at this photo", never as a heading.

Rules (rules/photos.json pose_solve): grid_search_m, grid_step_m, heading_search_deg, heading_step_deg, pitch_range_deg, horizon_m,
skyline_fit_px_max (pixels at px_ref_height, default 720), accept_vs_draft (best must beat the GPS-spot score by this factor to move).
Writes the pose columns of index.csv in place (pose_x, pose_z, pose_y, pose_yaw, pose_pitch, pose_conf, notes) for rows whose pose_conf
is 'draft' (all rows with --redo); course_photos/poses/<img>_fit.png with --sheets; a summary line.
"""
import argparse, csv, math, os, sys, time
import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap

_HM = None; _TREES = None; _R = None; _HM_PATH = None


def _init(hm_path, C_frame, trees, R):
    """Per-worker state: heightmap loaded once per process."""
    global _HM, _TREES, _R
    class _C(dict):
        def path(self, k): return hm_path
    c = _C(); c["frame"] = C_frame; c.dir = ""
    _HM = Heightmap(c, hm_path); _TREES = trees; _R = R


def photo_skyline(img):
    """Fraction of image height where sky ends per column (nan = no sky in that column); sky must touch the top edge."""
    a = np.asarray(img).astype(float); r, g, b = a[..., 0], a[..., 1], a[..., 2]; L = a.mean(2)
    sky = ((b >= r - 12) & (L > 120)) | (L > 200)
    lab, n = ndimage.label(sky)
    if n == 0: return None
    top = set(np.unique(lab[0])) - {0}
    if not top: return None
    sky = np.isin(lab, list(top)); H = sky.shape[0]
    col = np.argmin(sky, axis=0).astype(float); col[sky.all(0)] = np.nan; col[col == 0] = np.nan
    return col / H


def model_skyline(x, z, eye_y, az_deg, dists):
    """Elevation angle (deg) of the terrain+tree skyline at each azimuth from (x, z, eye_y)."""
    az = np.radians(az_deg)
    X = x + np.outer(np.sin(az), dists); Z = z + np.outer(np.cos(az), dists)
    n = _HM.H.shape[0] - 1; S = _HM.S
    inside = (X >= 0) & (Z >= 0) & (X <= n * S) & (Z <= n * S)
    H = _HM.sample(X.ravel(), Z.ravel(), 1).reshape(X.shape)
    el = np.degrees(np.arctan2(H - eye_y, dists[None, :])); el[~inside] = -90
    sky = el.max(1)
    if _TREES is not None and len(_TREES):
        T = _TREES; dx = T[:, 0] - x; dz = T[:, 1] - z; d = np.hypot(dx, dz)
        keep = (d > 1.0) & (d < dists[-1]); T = T[keep]; dx = dx[keep]; dz = dz[keep]; d = d[keep]
        if len(T):
            r = np.maximum(T[:, 4], 0.8); top = T[:, 2] + T[:, 3]; cy = top - r
            az_t = np.degrees(np.arctan2(dx, dz)) % 360; ang_r = np.degrees(np.arcsin(np.clip(r / d, 0, 1)))
            step = az_deg[1] - az_deg[0]
            for i in range(len(T)):
                k = int(ang_r[i] / step) + 1; j0 = int(round(az_t[i] / step))
                js = (np.arange(j0 - k, j0 + k + 1)) % len(az_deg); da = np.radians((az_deg[js] - az_t[i] + 180) % 360 - 180)
                lat = d[i] * np.sin(da); ok = np.abs(lat) < r[i]
                if not ok.any(): continue
                h = np.sqrt(np.maximum(r[i] ** 2 - lat[ok] ** 2, 0)); e = np.degrees(np.arctan2(cy[i] + h - eye_y, d[i] * np.cos(da[ok])))
                sky[js[ok]] = np.maximum(sky[js[ok]], e)
    return sky


def solve_one(args):
    row, disp_path, params = args
    R = _R; out = dict(row); img = row["img"]
    try:
        im = Image.open(disp_path).convert("RGB"); im.thumbnail((400, 400))
    except Exception as e:
        out["_status"] = "no image: %s" % e; return out
    sp = photo_skyline(im)
    if sp is None or np.isfinite(sp).sum() < 0.25 * len(sp) or np.nanmedian(sp) > 0.75:
        out["_status"] = "no skyline"; return out
    vfov = float(row["fov"] or 50); tv = math.tan(math.radians(vfov) / 2); aspect = im.size[0] / im.size[1]; th = tv * aspect
    u = (np.arange(len(sp)) + 0.5) / len(sp) * 2 - 1
    az_p = np.degrees(np.arctan(u * th)); el_p = np.degrees(np.arctan((0.5 - sp) * 2 * tv)); good = np.isfinite(el_p)
    if good.sum() < 20: out["_status"] = "no skyline"; return out
    if params.get("redo") and row.get("x") not in ("", None) and row.get("heading") not in ("", None):
        x0, z0, hd0 = float(row["x"]), float(row["z"]), float(row["heading"])      # --redo restarts from the GPS/compass draft, not a previous solve
    else:
        x0, z0, hd0 = float(row["pose_x"]), float(row["pose_z"]), float(row["pose_yaw"])
    eye = R["eye_height_m"]
    P = R["pose_solve"]; gs, gstep = P["grid_search_m"], P["grid_step_m"]; hs, hstep = P["heading_search_deg"], P["heading_step_deg"]
    az_deg = np.arange(0, 360, P["azimuth_step_deg"]); dists = np.r_[np.arange(1, 100, 1.0), np.arange(100, 400, 3.0), np.arange(400, P["horizon_m"], 10.0)]
    offs = np.arange(-gs, gs + 1e-6, gstep); dhs = np.arange(-hs, hs + 1e-6, hstep)
    lam_h, lam_m = P["heading_penalty_deg_per_deg"], P["move_penalty_deg_per_m"]
    def fit_at(x, z):
        """(err, dh, dp, sky, errs_over_dh) for the best heading at a position."""
        ey = _HM.sample(x, z, 1) + eye; sky = model_skyline(x, z, ey, az_deg, dists); sky360 = np.r_[sky, sky[0]]; az360 = np.r_[az_deg, 360.0]
        errs = []; dps = []
        for dh in dhs:
            a = (hd0 + dh + az_p[good]) % 360; eg = np.interp(a, az360, sky360)
            dp = float(np.clip(np.median(el_p[good] - eg), P["pitch_range_deg"][0], P["pitch_range_deg"][1])); errs.append(float(np.median(np.abs(el_p[good] - dp - eg)))); dps.append(dp)
        errs = np.array(errs); sc = errs + lam_h * np.abs(dhs); i = int(np.argmin(sc))
        # a heading is only trusted when its minimum is distinct from the run of the mill over the search range
        distinct = errs[i] < P["heading_distinct_ratio"] * float(np.median(errs))
        if not distinct: i = int(np.argmin(np.abs(dhs)))
        return errs[i], float(dhs[i]), dps[i], sky, distinct
    e0, dh0, dp0, sky0, dist0 = fit_at(x0, z0); draft_score = e0
    flip = None
    if P.get("flip_check", True):
        sky360 = np.r_[sky0, sky0[0]]; az360 = np.r_[az_deg, 360.0]; errs = []
        full = np.arange(-180, 180, P.get("flip_step_deg", 2.0))
        for dh in full:
            a = (hd0 + dh + az_p[good]) % 360; eg = np.interp(a, az360, sky360)
            dp = float(np.clip(np.median(el_p[good] - eg), P["pitch_range_deg"][0], P["pitch_range_deg"][1])); errs.append(float(np.median(np.abs(el_p[good] - dp - eg))))
        errs = np.array(errs); i = int(np.argmin(errs))
        if abs(full[i]) > hs and errs[i] < P.get("flip_ratio", 0.5) * e0 and errs[i] < P.get("flip_max_deg", 3.0): flip = (float(full[i]), float(errs[i]))
    best = (e0 + 0.0, 0.0, 0.0, dh0, dp0, sky0, dist0)
    for dx in offs:
        for dz in offs:
            if dx == 0 and dz == 0: continue
            e, dh, dp, sky, dist = fit_at(x0 + dx, z0 + dz); sc = e + lam_m * math.hypot(dx, dz)
            if sc < best[0]: best = (sc, dx, dz, dh, dp, sky, dist)
    sc, dx, dz, dh, dp, sky, dist = best
    err = sc - lam_m * math.hypot(dx, dz)
    # move off the GPS spot only on a clear win (rules accept_vs_draft and min_gain_deg); otherwise keep the GPS position
    if (dx or dz) and not (err < P["accept_vs_draft"] * e0 and e0 - err >= P["min_gain_deg"]):
        dx = dz = 0.0; err, dh, dp, sky, dist = e0, dh0, dp0, sky0, dist0
    moved = math.hypot(dx, dz); px = err / vfov * P["px_ref_height"]
    conf = "high" if px <= P["skyline_fit_px_max"] and dist else "medium" if px <= 2 * P["skyline_fit_px_max"] else "low"
    x, z = x0 + dx, z0 + dz
    out.update({"pose_x": round(x, 2), "pose_z": round(z, 2), "pose_y": round(float(_HM.sample(x, z, 1) + eye), 2), "pose_yaw": round((hd0 + dh) % 360, 1), "pose_pitch": round(dp, 1), "pose_conf": conf,
                "_status": "ok", "_err_deg": round(err, 2), "_px": round(px, 1), "_moved": round(moved, 1), "_turned": round(dh, 1), "_draft_deg": None if draft_score is None else round(draft_score, 2)})
    note = " skyline fit %.1f px (%.2f deg), moved %.1f m, turned %+.1f deg" % (px, err, moved, dh)
    if flip: note += "; COMPASS? a heading %+.0f deg off fits %.2f deg (check the photo)" % flip
    if not params.get("apply"):   # diagnostic by default: the draft pose stays, the fit goes in the notes
        out.update({"pose_x": row["pose_x"], "pose_z": row["pose_z"], "pose_y": row["pose_y"], "pose_yaw": row["pose_yaw"], "pose_pitch": row["pose_pitch"], "pose_conf": row["pose_conf"]})
    out["notes"] = ((row.get("notes") or "").strip() + note).strip(); out["_flip"] = flip
    if params.get("sheets"):
        try:
            W, H = im.size; d = ImageDraw.Draw(im)
            ys = sp * H; xs = np.arange(len(sp)) * (W / len(sp))
            pts = [(float(xx), float(yy)) for xx, yy in zip(xs, ys) if np.isfinite(yy)]
            if len(pts) > 1: d.line(pts, fill=(255, 0, 0), width=2)
            sky360 = np.r_[sky, sky[0]]; az360 = np.r_[az_deg, 360.0]
            a = (hd0 + dh + az_p) % 360; eg = np.interp(a, az360, sky360) + dp
            ym = (0.5 - np.tan(np.radians(eg)) / (2 * tv)) * H
            pts2 = [(float(xx), float(yy)) for xx, yy in zip(xs, ym)]
            d.line(pts2, fill=(0, 255, 0), width=2); d.text((4, 4), "%s %s %.1f px" % (img, conf, px), fill="yellow")
            os.makedirs(params["sheet_dir"], exist_ok=True); im.save(os.path.join(params["sheet_dir"], img + "_fit.png"))
        except Exception as e:
            out["_status"] = "ok (sheet failed: %s)" % e
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--index"); ap.add_argument("--holes"); ap.add_argument("--redo", action="store_true"); ap.add_argument("--sheets", action="store_true")
    ap.add_argument("--workers", type=int, default=4); ap.add_argument("--limit", type=int)
    ap.add_argument("--apply", action="store_true", help="write the solved pose into the pose columns (default: notes only; see the validation note in the docstring)")
    a = ap.parse_args()
    C = course(); RP = rules(C)["photos"]
    root = C.path("photos_root") if "photos_root" in C.get("paths", {}) else os.path.join(C.dir, "course_photos")
    ip = a.index or os.path.join(root, "index.csv")
    rows = list(csv.DictReader(open(ip, encoding="utf-8-sig"))); cols = list(rows[0].keys()) if rows else []
    trees = None
    try:
        tp = C.path("trees_csv")
        if os.path.exists(tp):
            T = list(csv.DictReader(open(tp, encoding="utf-8-sig")))
            trees = np.array([[float(t["x"]), float(t["z"]), float(t["y"]), float(t["height_m"]), float(t["crown_radius_m"])] for t in T])
    except KeyError: pass
    holes = set(a.holes.split(",")) if a.holes else None
    todo = [r for r in rows if r.get("pose_x") not in ("", None) and r.get("pose_yaw") not in ("", None) and (a.redo or r.get("pose_conf") == "draft") and (not holes or r.get("hole") in holes)]
    if a.limit: todo = todo[:a.limit]
    params = {"sheets": a.sheets, "sheet_dir": os.path.join(root, "poses"), "redo": a.redo, "apply": a.apply}
    jobs = [(r, os.path.join(root, "display", r["img"] + ".jpg"), params) for r in todo]
    t0 = time.time(); R = dict(RP["pose"]); R["pose_solve"] = RP["pose_solve"]
    if a.workers > 1 and len(jobs) > 1:
        import multiprocessing as mp
        with mp.Pool(a.workers, initializer=_init, initargs=(C.path("heightmap"), C["frame"], trees, R)) as pool:
            res = pool.map(solve_one, jobs, chunksize=1)
    else:
        _init(C.path("heightmap"), C["frame"], trees, R); res = [solve_one(j) for j in jobs]
    by = {r["img"]: r for r in res}
    n_ok = n_nosky = 0; confs = {}
    for r in rows:
        s = by.get(r["img"])
        if not s: continue
        if s["_status"].startswith("ok"):
            n_ok += 1
            for k in ("pose_x", "pose_z", "pose_y", "pose_yaw", "pose_pitch", "pose_conf", "notes"): r[k] = s[k]
            confs[s["pose_conf"]] = confs.get(s["pose_conf"], 0) + 1
        elif s["_status"] == "no skyline":
            n_nosky += 1
            if a.apply: r["pose_conf"] = "no skyline"
            else: r["notes"] = ((r.get("notes") or "") + " no skyline").strip()
        else:
            r["notes"] = ((r.get("notes") or "") + " " + s["_status"]).strip()
    with open(ip, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=cols, extrasaction="ignore"); w.writeheader(); [w.writerow({k: r.get(k, "") for k in cols}) for r in rows]
    print("%d photos: %d solved %s, %d no skyline, %d other; %.0f s -> %s" % (len(todo), n_ok, confs, n_nosky, len(todo) - n_ok - n_nosky, time.time() - t0, ip))
    for s in res:
        if s["_status"].startswith("ok"): print("  %-9s h%-3s %-6s fit %5.1f px %.2f deg (draft %s) moved %4.1f m turned %+5.1f deg" % (s["img"], s.get("hole"), s["pose_conf"], s["_px"], s["_err_deg"], s["_draft_deg"], s["_moved"], s["_turned"]))
        else: print("  %-9s h%-3s %s" % (s["img"], s.get("hole"), s["_status"]))
    fl = [s for s in res if s.get("_flip")]
    if fl: print("%d photos where a very different heading fits much better (compass suspect): %s" % (len(fl), ", ".join("%s %+.0f" % (s["img"], s["_flip"][0]) for s in fl)))


if __name__ == "__main__":
    main()
