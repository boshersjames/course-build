"""Forward-only flyover path (v4 rules) plus the pre-render gate checks from the flyovers skill. Runs anywhere: every input
comes from the repo (GKD, hole signs, LiDAR trees, course SVG, heightmap), no Unity and no PC paths.

    python tierra_flyover_gate.py --out <dir> [--holes 1,2,..] [--base <automation config json>] [--pin 0]

Path (unchanged from tierra_flyover_forward.py v4, so renders stay comparable with the Oct 30/31 gates):
  1. open on the TR hole sign with the camera already on the line-of-play heading, 4.5 m back and 1.6 m to the side; 1.2 s hold
  2. rise over the sign to 28 m over the max-filtered ground with no yaw; pitch eases down
  3. fly forward sign -> aim points -> pin; yaw spread over the flight; pitch follows a point 110 m ahead
  4. on to ~12 m short of the pin, down to ~8 m, pitch ending on the pin; hold 2.5 s
  Yaw and pitch rate-limited to 4.5 deg/s, no roll; a hole FAILS if any 1 s window moves yaw or pitch more than 5 deg.

Gate checks added here (skill: "Checks"), all reported per hole in flyover_gate.md / .json:
  - first frame: the camera is not inside a tree crown, the sign is in the frame and not hidden behind a crown. If the opening
    spot fails, the camera is moved 2-5 m forward or sideways (logged); the tree is never moved.
  - whole path: minimum terrain clearance and minimum crown clearance (the hole 15 key that went through a tree at 5 m).
  - end frame: fraction of the green outline inside the frame, pin in frame, pin not hidden behind a crown.
  - yaw and pitch max over any 1 s window, end pitch vs the pin target.
Limits: the heightmap is the 2018 LiDAR (not the regraded Unity terrain), the trees are the LiDAR list (not the live scene after
the Oct 31 / Nov 1 moves), and the greens are the OSM/SVG outlines. The PC render is still the real gate; this one catches
the path and framing problems before an editor is tied up.
"""
import argparse, csv, json, math, os, re, sys
import numpy as np

FPS = 25
LIM = 4.5 / FPS
FOV_V = 52.0          # CourseAutomation shot fov = Unity vertical field of view
ASPECT = 16 / 9
CROWN_CLEAR = 1.5     # m: camera closer than this to a crown counts as a hit
SIGN_H = 1.75         # ball-top height of the TR signs (BoJa, Oct 29)

# ---------- inputs ----------
def load_hm(path):
    return np.fromfile(path, dtype='<u2').reshape(2049, 2049).astype(float) * 207 / 65535

CELL = 2000 / 2048
def make_gy(H):
    def gy(x, z):
        i = min(max(int(round(z / CELL)), 0), 2048); j = min(max(int(round(x / CELL)), 0), 2048); return float(H[i, j])
    return gy

def load_svg_greens(path):
    s = open(path, encoding='utf-8').read(); out = []
    for g in re.split(r'<g\b', s)[1:]:
        m = re.search(r'inkscape:label="([^"]+)"', g)
        if not m or m.group(1) != 'Green': continue
        for pm in re.finditer(r'<path id="([^"]+)"[^>]*\sd="([^"]+)"', g):
            pid, d = pm.groups(); sub = re.findall(r'M[^MZz]+', d)[0]
            pts = np.array([(float(a), 2000 - float(b)) for a, b in re.findall(r'(-?[\d.]+),(-?[\d.]+)', sub)])
            out.append((pid, pts))
    return out

def pip(poly, x, z):
    px, pz = poly[:, 0], poly[:, 1]; qx, qz = np.roll(px, 1), np.roll(pz, 1)
    cond = (pz > z) != (qz > z); xi = px + (z - pz) * (qx - px) / np.where(cond, (qz - pz), 1)
    return np.sum(cond & (x < xi)) % 2 == 1

def dist_to_poly(poly, x, z):
    if pip(poly, x, z): return 0.0
    a = poly; b = np.roll(poly, -1, 0); ab = b - a; ap = np.array([x, z]) - a
    t = np.clip((ap * ab).sum(1) / ((ab * ab).sum(1) + 1e-9), 0, 1); return float(np.linalg.norm(ap - ab * t[:, None], axis=1).min())

def load_trees(path):
    rows = list(csv.DictReader(open(path, encoding='utf-8-sig')))
    T = np.array([[float(r['x']), float(r['z']), float(r['y']), float(r['height_m']), float(r['crown_radius_m'])] for r in rows])
    ids = np.array([int(r['id']) for r in rows])
    # crown = ellipsoid, horizontal radius = crown_radius, vertical half-depth = 0.3 h, centre at ground + 0.7 h (top at tree height)
    C = np.stack([T[:, 0], T[:, 2] + 0.7 * T[:, 3], T[:, 1]], 1); R = T[:, 4]; V = np.maximum(0.3 * T[:, 3], 1.0)
    return ids, C, R, V

def crown_clearance(C, R, V, p):
    """Approximate metres from point p (x, y, z) to the nearest crown surface; negative = inside."""
    d = np.asarray(p) - C; s = np.sqrt((d[:, 0] ** 2 + d[:, 2] ** 2) / R ** 2 + d[:, 1] ** 2 / V ** 2)
    m = (s - 1) * np.minimum(R, V); k = int(np.argmin(m)); return float(m[k]), k

def segment_blocked(C, R, V, a, b, step=0.5):
    a = np.asarray(a, float); b = np.asarray(b, float); n = max(2, int(np.linalg.norm(b - a) / step))
    worst = (1e9, -1)
    for t in np.linspace(0, 1, n + 1)[1:-1]:
        m, k = crown_clearance(C, R, V, a + (b - a) * t)
        if m < worst[0]: worst = (m, k)
    return worst

# ---------- maths ----------
def hdg(v): return math.degrees(math.atan2(v[0], v[1])) % 360
def dang(a, b): return (b - a + 180) % 360 - 180
def unit(v): n = np.linalg.norm(v); return v / n if n > 1e-9 else v
def ss(u): u = min(max(u, 0), 1); return u * u * (3 - 2 * u)
def smooth(x, sig):
    hw = int(3 * sig); k = np.exp(-0.5 * (np.arange(-hw, hw + 1) / sig) ** 2); k /= k.sum()
    xp = np.pad(x, (hw, hw), mode='edge'); return np.convolve(xp, k, mode='valid')
def ratelimit(x, lim):
    y = np.array(x, float)
    for _ in range(2):
        for i in range(1, len(y)): y[i] = y[i - 1] + max(-lim, min(lim, y[i] - y[i - 1]))
        y = y[::-1]
    return y

def in_frame(cam, yaw, pitch, target):
    """(in, dyaw, dpitch): is target inside a FOV_V x ASPECT frame from cam looking (yaw, pitch)."""
    d = np.asarray(target, float) - np.asarray(cam, float); flat = math.hypot(d[0], d[2])
    dy = dang(yaw, hdg([d[0], d[2]])); dp = math.degrees(math.atan2(d[1], max(flat, 1e-6))) - pitch
    hf = math.degrees(math.atan(math.tan(math.radians(FOV_V / 2)) * ASPECT))
    return abs(dy) <= hf and abs(dp) <= FOV_V / 2, dy, dp

# ---------- per hole ----------
def build_hole(hole, g, signs, gy, gmax, trees, greens, pin_day, log, end_short=12.0, end_height=8.0, max_move=5.5, rise_dist=12.0, speed=26.0):
    ids, C, R, V = trees
    H = g['Holes'][hole - 1]
    T = {t['TeeType']: np.array([t['Position']['x'], t['Position']['z']]) for t in H['Tees'] if t.get('Position')}
    pin3 = H['Pins'][pin_day]['Position']; pin = np.array([pin3['x'], pin3['z']])
    aims = [T[k] for k in ('AimPoint1', 'AimPoint2') if k in T]
    sg = signs[hole]; s = np.array([sg['x'], sg['z']])
    pts = [s] + [a for a in aims if np.dot(a - s, unit(pin - s)) > 20] + [pin]
    seg = [np.linalg.norm(pts[i + 1] - pts[i]) for i in range(len(pts) - 1)]; L = sum(seg)
    def at(dist):
        dist = min(max(dist, 0), L)
        for i, sl in enumerate(seg):
            if dist <= sl or i == len(seg) - 1: return pts[i] + (pts[i + 1] - pts[i]) * (dist / sl)
            dist -= sl
    play = unit(pin - s); h_play = hdg(play); h_last = hdg(pts[-1] - pts[-2]); perp = np.array([play[1], -play[0]])
    sign_top = np.array([s[0], gy(*s) + SIGN_H, s[1]]); sign_mid = np.array([s[0], gy(*s) + 1.2, s[1]])
    top = C[:, 1] + V   # tree tops
    def tfloor(xz):
        """Lowest camera height over point xz that keeps CROWN_CLEAR above every crown the camera would pass through."""
        d = np.hypot(C[:, 0] - xz[0], C[:, 2] - xz[1]); near = d < R + CROWN_CLEAR
        return float((top[near] + CROWN_CLEAR).max()) if near.any() else -1e9
    def lift(ys, req):
        """Raise the height series ys to at least req, with the bump widened by 1 s each side and smoothed so the climb stays eased."""
        ys = np.array(ys, float); req = np.array(req, float); need = np.maximum(req - ys, 0)
        if need.max() <= 0: return ys, 0.0, -1
        w = FPS; dil = np.array([need[max(0, i - w):i + w + 1].max() for i in range(len(need))]); sm = smooth(dil, 0.5 * FPS)
        out = np.maximum(ys, ys + sm); out = np.maximum(out, req); return out, float(need.max()), int(np.argmax(need))

    # 1. opening spot: 4.5 m back, 1.6 m to the side. Gate: not in a crown, sign in frame, sign not behind a crown.
    p_rise = at(rise_dist); y_rise = max(gmax(*p_rise) + 28, tfloor(p_rise))
    def opening(back, side):
        cam0 = s - play * back + perp * side; y0 = gy(*cam0) + 1.6; p0 = np.array([cam0[0], y0, cam0[1]])
        tilt0 = math.degrees(math.atan2(sign_mid[1] - y0, max(np.linalg.norm(cam0 - s), 1)))
        cl, ck = crown_clearance(C, R, V, p0); bl, bk = segment_blocked(C, R, V, p0, sign_top)
        ok_frame, dy, dp = in_frame(p0, h_play, tilt0, sign_top)
        # the rise is a straight eased climb from p0 to the rise key: the whole segment must stay clear of crowns
        rl, rk = segment_blocked(C, R, V, p0, [p_rise[0], y_rise, p_rise[1]])
        return dict(cam0=cam0, y0=y0, tilt0=tilt0, clear=cl, clear_tree=int(ids[ck]), sight=bl, sight_tree=int(ids[bk]) if bk >= 0 else -1,
                    rise=rl, rise_tree=int(ids[rk]) if rk >= 0 else -1, sign_in_frame=ok_frame, sign_dyaw=dy, sign_dpitch=dp, back=back, side=side)
    def good(o): return o['clear'] >= CROWN_CLEAR and o['sight'] >= 0.5 and o['sign_in_frame'] and o['rise'] >= CROWN_CLEAR
    o = opening(4.5, 1.6); moved = ''
    if not good(o):
        sides = [0] + [v for k in range(2, int(max_move) + 1) for v in (k, -k)]
        tried = sorted([(4.5 - f, 1.6 + sd) for f in range(0, int(max_move) + 1) for sd in sides] + [(4.5 + f, 1.6 + sd) for f in range(2, int(max_move) + 1) for sd in sides],
                       key=lambda bs: math.hypot(bs[0] - 4.5, bs[1] - 1.6))
        for back, side in tried:
            o2 = opening(back, side)
            if good(o2):
                dx = np.linalg.norm(o2['cam0'] - o['cam0'])
                if dx <= max_move:
                    moved = 'opening moved %.1f m (back %.1f -> %.1f, side %.1f -> %.1f): %s' % (dx, 4.5, back, 1.6, side,
                            'camera in crown of tree %d' % o['clear_tree'] if o['clear'] < CROWN_CLEAR else ('sign behind crown of tree %d' % o['sight_tree'] if o['sight'] < 0.5 else ('rise through crown of tree %d (%.1f m)' % (o['rise_tree'], o['rise']) if o['rise'] < CROWN_CLEAR else 'sign out of frame')))
                    o = o2; break
        if not moved: moved = 'opening NOT fixed within %.0f m' % max_move + ' (crown %.1f m, sign sightline %.1f m, sign in frame %s, rise %.1f m from crown of tree %d)' % (o['clear'], o['sight'], o['sign_in_frame'], o['rise'], o['rise_tree'])
    cam0, y0, tilt0 = o['cam0'], o['y0'], o['tilt0']
    P = []; Yd = []; Pd = []
    for f in range(int(1.2 * FPS)): P.append([cam0[0], y0, cam0[1]]); Yd.append(h_play); Pd.append(tilt0)
    # 2. rise (4 s), no yaw
    n2 = int(4.0 * FPS); p_end = p_rise; y_end = y_rise; rise_lift = y_end - (gmax(*p_end) + 28)
    for f in range(1, n2 + 1):
        e = ss(f / n2); xz = cam0 + (p_end - cam0) * e; y = (1 - e) * y0 + e * y_end; P.append([xz[0], float(y), xz[1]]); Yd.append(h_play)
        ah = at(rise_dist + 110); Pd.append((1 - e) * tilt0 + e * math.degrees(math.atan2(gy(*ah) + 2 - y, np.linalg.norm(ah - xz))))
    P0, Yd0, Pd0 = [list(p) for p in P], list(Yd), list(Pd); lifts = {}
    def build(slow):
        P3 = [list(p) for p in P0]; Y3 = list(Yd0); D3 = list(Pd0)
        stop = max(L - end_short, 20); dist_fly = stop - rise_dist; tf = max(dist_fly / speed, 5) * slow; n3 = int(tf * FPS)
        xs = []; ys = []; req = []; us = []; dds = []
        for f in range(1, n3 + 1):
            u = f / n3; e = 0.5 * ss(u) + 0.5 * u; dd = rise_dist + dist_fly * e; xz = at(dd); hgt = 28 + (end_height - 28) * ss((u - 0.45) / 0.55)
            rr = 12 + (4 - 12) * ss((u - 0.6) / 0.4); floor = end_height if u < 0.8 else end_height - 2
            xs.append(xz); ys.append(max(gmax(*xz, r=rr) + hgt, gy(*xz) + floor)); req.append(tfloor(xz) if u < 0.97 else -1e9); us.append(u); dds.append(dd)
        ys, fl, flf = lift(ys, req); lifts['flight'] = (fl, flf + len(P0))
        for xz, y, u, dd in zip(xs, ys, us, dds):
            P3.append([xz[0], float(y), xz[1]])
            Y3.append(h_play + dang(h_play, h_last) * ss(u))
            la = at(min(dd + 110, L)); pa = math.degrees(math.atan2(gy(*la) + 1 - y, max(np.linalg.norm(la - xz), 1)))
            pp = math.degrees(math.atan2(gy(*pin) - y, max(np.linalg.norm(pin - xz), 1))); D3.append(pa + (pp - pa) * ss((u - 0.5) / 0.5))
        pc = np.array(P3[-1]); pp = math.degrees(math.atan2(gy(*pin) - pc[1], max(np.linalg.norm(pin - pc[[0, 2]]), 1)))
        for f in range(int(2.5 * FPS)): P3.append(list(pc)); Y3.append(Y3[-1]); D3.append(pp)
        yu = np.unwrap(np.radians(Y3)) * 180 / math.pi; yaw = ratelimit(smooth(yu, 0.8 * FPS), LIM); pit = ratelimit(smooth(np.array(D3), 0.8 * FPS), LIM)
        yaw[:int(5.2 * FPS)] = yu[0]; yaw = ratelimit(yaw, LIM)
        w_y = max(abs(yaw[i + FPS] - yaw[i]) for i in range(len(yaw) - FPS)); w_p = max(abs(pit[i + FPS] - pit[i]) for i in range(len(pit) - FPS))
        return P3, yaw, pit, w_y, w_p, pp
    for slow in (1.0, 1.15, 1.3, 1.5, 1.75, 2.0):
        P, yaw, pit, w_y, w_p, pend = build(slow)
        rate_ok = w_y <= 5.0 + 1e-6 and w_p <= 5.0 + 1e-6 and abs(pit[-1] - pend) < 1.5
        if rate_ok: break
    Pa = np.array(P)
    # whole-path clearances
    ter = np.array([gy(p[0], p[2]) for p in P]); f_rise = int(1.2 * FPS) + n2   # the 5 m rule starts once the rise is done
    gap = float((Pa[f_rise:, 1] - ter[f_rise:]).min()); gap_f = f_rise + int(np.argmin(Pa[f_rise:, 1] - ter[f_rise:]))
    cc = [crown_clearance(C, R, V, p) for p in P[::2]]; ccm = min(cc, key=lambda t: t[0]); crown_min, crown_tree = ccm[0], int(ids[ccm[1]]); crown_f = 2 * cc.index(ccm)
    # end frame: green outline in frame, pin in frame, pin sightline
    pe = Pa[-1]; ye, pte = yaw[-1], pit[-1]
    green = min(greens, key=lambda gp: dist_to_poly(gp[1], pin[0], pin[1])); gid, gpoly = green
    gpts = [np.array([q[0], gy(*q), q[1]]) for q in gpoly]
    gin = [in_frame(pe, ye, pte, q)[0] for q in gpts]; green_frac = sum(gin) / len(gin)
    gdy = [in_frame(pe, ye, pte, q)[1] for q in gpts]; gdp = [in_frame(pe, ye, pte, q)[2] for q in gpts]
    hf = math.degrees(math.atan(math.tan(math.radians(FOV_V / 2)) * ASPECT))
    cut_off = sum(1 for dy, dp in zip(gdy, gdp) if abs(dy) > hf or dp > FOV_V / 2)          # out ahead or to the side (not under the camera)
    under = sum(1 for dy, dp in zip(gdy, gdp) if abs(dy) <= hf and dp < -FOV_V / 2)         # below the frame = under the camera
    # advisory: how far back along the final heading (same 8 m height rule, pitch on the pin) the end spot must sit to fit the whole green
    back_dir = -np.array([math.sin(math.radians(ye)), math.cos(math.radians(ye))]); pull_back = None
    for pb in np.arange(0, 60.5, 0.5):
        q = pe[[0, 2]] + back_dir * pb; qy = max(gmax(*q, r=4) + 8, gy(*q) + 6); qc = np.array([q[0], qy, q[1]])
        qp = math.degrees(math.atan2(gy(*pin) - qy, max(np.linalg.norm(pin - q), 1)))
        if all(in_frame(qc, ye, qp, gq)[0] for gq in gpts): pull_back = float(pb); break
    pin_ok, pin_dy, pin_dp = in_frame(pe, ye, pte, [pin[0], gy(*pin) + 1.0, pin[1]])
    pin_sight, pin_tree = segment_blocked(C, R, V, pe, [pin[0], gy(*pin) + 1.0, pin[1]])
    end_h = float(pe[1] - gy(pe[0], pe[2])); end_short = float(np.linalg.norm(pe[[0, 2]] - pin))
    first_ok = good(o)
    ok = rate_ok and first_ok and gap >= 5.0 and crown_min >= CROWN_CLEAR and cut_off == 0 and pin_ok and pin_sight >= 0.5
    rec = dict(hole=hole, par=H['Par'], frames=len(P), seconds=round(len(P) / FPS, 1), flight_x=slow, play_hdg=round(h_play), last_hdg=round(h_last),
               yaw_1s=round(w_y, 2), pitch_1s=round(w_p, 2), end_pitch=round(float(pit[-1]), 1), end_pitch_target=round(pend, 1),
               end_height=round(end_h, 1), end_short_of_pin=round(end_short, 1), min_terrain_clear=round(gap, 1), min_terrain_frame=gap_f,
               crown_lift_rise=round(rise_lift, 1), crown_lift_flight=round(lifts['flight'][0], 1), crown_lift_flight_frame=lifts['flight'][1],
               min_crown_clear=round(crown_min, 1), min_crown_tree=crown_tree, min_crown_frame=crown_f, min_crown_tree_xz=[round(float(C[ccm[1]][0]), 1), round(float(C[ccm[1]][2]), 1)], min_crown_cam=[round(float(v), 1) for v in P[crown_f]],
               first_crown_clear=round(o['clear'], 1), first_sign_sight=round(o['sight'], 1), rise_crown_clear=round(o['rise'], 1), rise_crown_tree=o['rise_tree'], first_sign_in_frame=bool(o['sign_in_frame']),
               first_sign_dyaw=round(o['sign_dyaw'], 1), first_sign_dpitch=round(o['sign_dpitch'], 1), opening_move=moved,
               green_id=gid, green_in_frame=round(green_frac, 3), green_pts=len(gpts), green_cut_off=cut_off, green_under_camera=under, green_fit_pull_back_m=pull_back, green_dyaw_range=[round(min(gdy), 1), round(max(gdy), 1)], green_dpitch_range=[round(min(gdp), 1), round(max(gdp), 1)],
               pin_in_frame=bool(pin_ok), pin_sight_clear=round(pin_sight, 1), pin_sight_tree=int(ids[pin_tree]) if pin_tree >= 0 else -1,
               rate_ok=bool(rate_ok), ok=bool(ok))
    shots = []
    for n, p in enumerate(P):
        yr, pr = math.radians(yaw[n]), math.radians(pit[n]); dv = np.array([math.sin(yr) * math.cos(pr), math.sin(pr), math.cos(yr) * math.cos(pr)])
        shots.append(dict(name='fly%02d_%04d' % (hole, n), pos=[round(float(c), 3) for c in p], lookAt=[round(float(c), 3) for c in np.array(p) + dv * 50], aboveGround=False, fov=FOV_V))
    poster = {k: int(v) for k, v in dict(first=0, rise=int(1.2 * FPS) + n2, mid=len(P) // 2, approach=len(P) - int(2.5 * FPS) - 2 * FPS, end=len(P) - 1).items()}
    return rec, shots, (np.arange(len(P)) / FPS, yaw, pit), poster

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    here = os.path.dirname(os.path.abspath(__file__)); repo = os.path.dirname(here)
    ap.add_argument('--repo', default=repo); ap.add_argument('--out', required=True); ap.add_argument('--holes', default=','.join(str(h) for h in range(1, 19)))
    ap.add_argument('--gkd'); ap.add_argument('--signs'); ap.add_argument('--trees'); ap.add_argument('--svg'); ap.add_argument('--heightmap')
    ap.add_argument('--base', help='automation config json to copy, with shots replaced'); ap.add_argument('--pin', type=int, default=0, help='pin index (0 = Thursday)')
    ap.add_argument('--no-plots', action='store_true')
    ap.add_argument('--speed', type=float, default=26.0, help='average ground speed on the flight, m/s (v4: 26); slower for showcase holes')
    ap.add_argument('--width', type=int, default=1280); ap.add_argument('--height', type=int, default=720)
    ap.add_argument('--rise-dist', type=float, default=12.0, help='rise key this far past the sign along the line (v4: 12); longer = shallower climb')
    ap.add_argument('--max-move', type=float, default=5.5, help='largest opening-camera move allowed (skill: 2-5 m)')
    ap.add_argument('--end-fit', action='store_true', help='per hole, pull the end spot back (and up, to keep ~30 deg down) until the whole green is in frame')
    ap.add_argument('--end-short', type=float, default=12.0, help='end spot this far short of the pin (v4: 12)'); ap.add_argument('--end-height', type=float, default=8.0, help='end height over the ground (v4: 8)')
    a = ap.parse_args(); rp = a.repo
    gkd = a.gkd or os.path.join(rp, 'greenkeeper', 'TierraRejada.GKD'); signs_p = a.signs or os.path.join(rp, 'progress', 'tierra', 'hole_signs_1029', 'hole_signs.json')
    trees_p = a.trees or os.path.join(rp, 'trees', 'tierra_trees.csv'); svg_p = a.svg or os.path.join(rp, 'svg', 'TierraRejada_course.svg'); hm_p = a.heightmap or os.path.join(rp, 'terrain', 'tierra_rejada_2049.raw')
    if os.path.getsize(hm_p) < 1000000: sys.exit('heightmap %s is an LFS pointer: run git lfs pull --include=terrain/tierra_rejada_2049.raw' % hm_p)
    H = load_hm(hm_p); gy = make_gy(H)
    def gmax(x, z, r=12): return max(gy(x + dx, z + dz) for dx in (-r, 0, r) for dz in (-r, 0, r))
    g = json.load(open(gkd, encoding='utf-8-sig')); signs = {s['hole']: s for s in json.load(open(signs_p, encoding='utf-8-sig'))}
    trees = load_trees(trees_p); greens = load_svg_greens(svg_p)
    os.makedirs(a.out, exist_ok=True); holes = [int(h) for h in a.holes.split(',')]
    recs = []; shots = []; posters = {}; log = []
    for hole in holes:
        es, eh = a.end_short, a.end_height
        rec, sh, curves, poster = build_hole(hole, g, signs, gy, gmax, trees, greens, a.pin, log, es, eh, a.max_move, a.rise_dist, a.speed)
        if a.end_fit:
            for _ in range(4):   # pull the end spot back until no part of the green is cut off; keep the look-down near 30 deg
                if rec['green_cut_off'] == 0 or rec['green_fit_pull_back_m'] is None: break
                es = es + rec['green_fit_pull_back_m'] + 1.0; eh = min(16.0, max(a.end_height, es * math.tan(math.radians(30))))
                rec, sh, curves, poster = build_hole(hole, g, signs, gy, gmax, trees, greens, a.pin, log, es, eh, a.max_move, a.rise_dist, a.speed)
        rec['end_short_used'] = es; rec['end_height_used'] = eh; rec['rise_dist'] = a.rise_dist; rec['max_move'] = a.max_move; rec['speed'] = a.speed
        recs.append(rec); shots += sh; posters[hole] = poster
        print('hole %2d: %3d frames %5.1f s (x%.2f) yaw/1s %.2f pitch/1s %.2f | end pitch %.1f (pin %.1f) h %.1f m, %.1f m short | terrain %.1f m crown %.1f m (tree %d, lift %.1f/%.1f) | first: crown %.1f sight %.1f in-frame %s | green %.0f%% (cut off %d, pull-back %s) pin %s sight %.1f -> %s%s' % (
            rec['hole'], rec['frames'], rec['seconds'], rec['flight_x'], rec['yaw_1s'], rec['pitch_1s'], rec['end_pitch'], rec['end_pitch_target'], rec['end_height'], rec['end_short_of_pin'],
            rec['min_terrain_clear'], rec['min_crown_clear'], rec['min_crown_tree'], rec['crown_lift_rise'], rec['crown_lift_flight'], rec['first_crown_clear'], rec['first_sign_sight'], rec['first_sign_in_frame'], 100 * rec['green_in_frame'], rec['green_cut_off'], rec['green_fit_pull_back_m'],
            rec['pin_in_frame'], rec['pin_sight_clear'], 'OK' if rec['ok'] else 'FAIL', ('  [' + rec['opening_move'] + ']') if rec['opening_move'] else ''))
        if not a.no_plots:
            try:
                import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
                t, yaw, pit = curves; fig, ax = plt.subplots(2, 1, figsize=(10, 4.5), sharex=True)
                ax[0].plot(t, yaw, lw=1.5); ax[0].set_ylabel('heading (deg)'); ax[0].set_title('Hole %d flyover: max change over any 1 s - yaw %.1f deg, pitch %.1f deg (limit 5)' % (hole, rec['yaw_1s'], rec['pitch_1s']), fontsize=10)
                ax[1].plot(t, pit, lw=1.5, color='#b85'); ax[1].set_ylabel('pitch (deg)'); ax[1].set_xlabel('seconds')
                for x in ax: x.grid(alpha=0.3)
                fig.tight_layout(); fig.savefig(os.path.join(a.out, 'hole%02d_flyover_angles.png' % hole), dpi=110); plt.close(fig)
            except ImportError: pass
    c = json.load(open(a.base, encoding='utf-8-sig')) if a.base else {}
    c['shots'] = shots; c['steps'] = []; c['saveScene'] = False; c['shotWidth'], c['shotHeight'] = a.width, a.height
    json.dump(c, open(os.path.join(a.out, 'flyover_shots.json'), 'w'))
    json.dump(dict(holes=recs, poster_frames=posters, inputs=dict(gkd=gkd, signs=signs_p, trees=trees_p, svg=svg_p, heightmap=hm_p, pin=a.pin, end_short=a.end_short, end_height=a.end_height)), open(os.path.join(a.out, 'flyover_gate.json'), 'w'), indent=1)
    md = ['# Flyover gate (data pass)', '', 'Inputs: GKD `%s`, signs `%s`, trees `%s`, greens `%s`, heightmap `%s`, pin index %d.' % (tuple(os.path.relpath(p, rp) for p in (gkd, signs_p, trees_p, svg_p, hm_p)) + (a.pin,)), '',
          'Pass = yaw and pitch under 5 deg in every 1 s window, opening camera clear of crowns with the sign in frame and unhidden, terrain clearance 5 m+, crown clearance %.1f m+ on the whole path, no part of the green cut off ahead or to the side in the end frame (the part under the camera is reported, not failed), pin in frame and not behind a crown.' % CROWN_CLEAR, '',
          '| Hole | s | yaw/1s | pitch/1s | end pitch (target) | end h / short (used) | terrain min (after rise) | crown min (tree, frame), lift rise/flight | first: crown / sight / in frame | green: in / under cam / cut off (pull-back to fit) | pin | result | opening move |', '|---|---|---|---|---|---|---|---|---|---|---|---|---|']
    for r in recs:
        md.append('| %d | %.1f | %.1f | %.1f | %.1f (%.1f) | %.1f / %.1f | %.1f | %.1f (%d, f%d), %.1f / %.1f | %.1f / %.1f / %s | %.0f%% / %d / %d (%s) | %s, sight %.1f | **%s** | %s |' % (
            r['hole'], r['seconds'], r['yaw_1s'], r['pitch_1s'], r['end_pitch'], r['end_pitch_target'], r['end_height'], r['end_short_of_pin'], r['min_terrain_clear'], r['min_crown_clear'], r['min_crown_tree'], r['min_crown_frame'], r['crown_lift_rise'], r['crown_lift_flight'],
            r['first_crown_clear'], r['first_sign_sight'], 'yes' if r['first_sign_in_frame'] else 'NO', 100 * r['green_in_frame'], r['green_under_camera'], r['green_cut_off'], ('%.0f m' % r['green_fit_pull_back_m']) if r['green_fit_pull_back_m'] is not None else '>60 m',
            'in' if r['pin_in_frame'] else 'OUT', r['pin_sight_clear'], 'OK' if r['ok'] else 'FAIL', r['opening_move'] or ''))
    fails = [r['hole'] for r in recs if not r['ok']]
    md += ['', 'End spot: %.0f m short of the pin, %.0f m over the ground (v4 default 12 / 8)%s.' % (a.end_short, a.end_height, ', pulled back per hole until the green fits (--end-fit)' if a.end_fit else ''), '', 'Failed holes: ' + (', '.join(str(h) for h in fails) if fails else 'none'), '', 'Poster frames (first / rise / mid / approach / end) per hole are in flyover_gate.json; shots for the PC render in flyover_shots.json (%d frames).' % len(shots)]
    open(os.path.join(a.out, 'flyover_gate.md'), 'w').write('\n'.join(md) + '\n')
    print('\n'.join(md[-3:]))
    if fails: sys.exit(1)

if __name__ == '__main__': main()
