# Forward-only flyover frames, v2 (BoJa, Oct 30: "not to rotate the camera so quickly" - twice).
#   python tierra_flyover_forward.py <out.json> <holes, e.g. 15,16> [base config json] [plot dir]
# Path rule, every hole:
#  1. open on the TR hole sign with the camera already on the line-of-play heading (sign -> pin): the start position is shifted back and
#     sideways so the sign sits in frame, the camera never turns to it; 1.2 s hold
#  2. rise over the sign to flyover height (28 m over the max-filtered ground) with NO yaw; pitch eases down
#  3. fly forward along the centreline sign -> aim points -> pin; yaw goes from the line of play to the last approach heading spread over the
#     whole flight (smoothstep), pitch follows a point 110 m ahead
#  4. (v4) fly on to ~12 m short of the pin over the green front, down to ~8 m over the ground (6 m floor near the end), pitch ending on the pin (~30-35 deg down); hold 2.5 s
#  Yaw and pitch are smoothed and then rate-limited to 4.5 deg/s; no roll. The script FAILS (exit 1, no json) if any 1 s window changes yaw or
#  pitch by more than 5 deg. A heading/pitch-over-time plot per hole goes to the plot dir.
import json, math, os, sys
import numpy as np
FPS = 25; LIM = 4.5 / FPS
H = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', dtype='<u2').reshape(2049, 2049).astype(float) * 207 / 65535; S = 2000 / 2048
def gy(x, z):
    i = min(max(int(round(z / S)), 0), 2048); j = min(max(int(round(x / S)), 0), 2048); return float(H[i, j])
def gmax(x, z, r=12): return max(gy(x + dx, z + dz) for dx in (-r, 0, r) for dz in (-r, 0, r))
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
signs = {s['hole']: s for s in json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/vegetation/hole_signs.json', encoding='utf-8-sig'))}
def hdg(v): return math.degrees(math.atan2(v[0], v[1])) % 360
def dang(a, b): return (b - a + 180) % 360 - 180
def unit(v): n = np.linalg.norm(v); return v / n if n > 1e-9 else v
def ss(u): u = min(max(u, 0), 1); return u * u * (3 - 2 * u)
def smooth(x, sig):
    k = np.exp(-0.5 * (np.arange(-3 * sig, 3 * sig + 1) / sig) ** 2); k /= k.sum(); xp = np.pad(x, (len(k) // 2, len(k) // 2), mode='edge'); return np.convolve(xp, k, mode='valid')
def ratelimit(x, lim):
    y = np.array(x, float)
    for _ in range(2):   # forward then backward pass so the limit eases both ends
        for i in range(1, len(y)): y[i] = y[i - 1] + max(-lim, min(lim, y[i] - y[i - 1]))
        y = y[::-1]
    return y

out, holes = sys.argv[1], [int(h) for h in sys.argv[2].split(',')]
base = json.load(open(sys.argv[3], encoding='utf-8-sig')) if len(sys.argv) > 3 else {}
plots = sys.argv[4] if len(sys.argv) > 4 else None
shots = []; failed = []
for hole in holes:
    T = {t['TeeType']: np.array([t['Position']['x'], t['Position']['z']]) for t in g['Holes'][hole - 1]['Tees'] if t.get('Position')}
    pin = g['Holes'][hole - 1]['Pins'][0]['Position']; pin = np.array([pin['x'], pin['z']]); aims = [T[k] for k in ('AimPoint1', 'AimPoint2') if k in T]
    sg = signs[hole]; s = np.array([sg['x'], sg['z']])
    pts = [s] + [a for a in aims if np.dot(a - s, unit(pin - s)) > 20] + [pin]
    seg = [np.linalg.norm(pts[i + 1] - pts[i]) for i in range(len(pts) - 1)]; L = sum(seg)
    def at(dist):
        dist = min(max(dist, 0), L)
        for i, sl in enumerate(seg):
            if dist <= sl or i == len(seg) - 1: return pts[i] + (pts[i + 1] - pts[i]) * (dist / sl)
            dist -= sl
    play = unit(pin - s); h_play = hdg(play); h_last = hdg(pts[-1] - pts[-2]); perp = np.array([play[1], -play[0]])
    # 1. opening: on the play heading, 4.5 m back and 1.6 m to the side so the sign is in frame (left or right, whichever keeps it nearer centre)
    cam0 = s - play * 4.5 + perp * 1.6; y0 = gy(*cam0) + 1.6
    P = []; Yd = []; Pd = []
    tilt0 = math.degrees(math.atan2(gy(*s) + 1.2 - y0, 4.6))
    for f in range(int(1.2 * FPS)): P.append([cam0[0], y0, cam0[1]]); Yd.append(h_play); Pd.append(tilt0)
    # 2. rise (4 s), no yaw: forward to the centreline 12 m past the sign, up to cruise height
    n2 = int(4.0 * FPS); p_end = at(12); y_end = gmax(*p_end) + 28
    for f in range(1, n2 + 1):
        e = ss(f / n2); xz = cam0 + (p_end - cam0) * e; y = (1 - e) * y0 + e * y_end; P.append([xz[0], y, xz[1]]); Yd.append(h_play)
        ah = at(12 + 110); Pd.append((1 - e) * tilt0 + e * math.degrees(math.atan2(gy(*ah) + 2 - y, np.linalg.norm(ah - xz))))
    # 3. flight (v4 ending): forward to ~12 m short of the pin (over the green front), descending to ~8 m over the ground; the max-ground
    #    radius shrinks 12 -> 4 m and the floor drops 8 -> 6 m on the final stretch so the green banks don't hold it high; pitch blends to
    #    the pin from mid-flight so it ends looking down ~30-35 deg at the flag with the green in frame. If the 1 s rate check fails, the
    #    flight is lengthened (x1.15 steps), never the limit relaxed.
    P0, Yd0, Pd0 = [list(p) for p in P], list(Yd), list(Pd)   # opening + rise, fixed across retries
    def build(slow):
        P3 = [list(p) for p in P0]; Y3 = list(Yd0); D3 = list(Pd0)
        stop = max(L - 12, 20); dist_fly = stop - 12; tf = max(dist_fly / 26.0, 5) * slow; n3 = int(tf * FPS)
        for f in range(1, n3 + 1):
            u = f / n3; e = 0.5 * ss(u) + 0.5 * u; dd = 12 + dist_fly * e; xz = at(dd); hgt = 28 + (8 - 28) * ss((u - 0.45) / 0.55)
            rr = 12 + (4 - 12) * ss((u - 0.6) / 0.4); floor = 8 if u < 0.8 else 6
            y = max(gmax(*xz, r=rr) + hgt, gy(*xz) + floor); P3.append([xz[0], y, xz[1]])
            Y3.append(h_play + dang(h_play, h_last) * ss(u))
            la = at(min(dd + 110, L)); pa = math.degrees(math.atan2(gy(*la) + 1 - y, max(np.linalg.norm(la - xz), 1)))
            pp = math.degrees(math.atan2(gy(*pin) - y, max(np.linalg.norm(pin - xz), 1))); D3.append(pa + (pp - pa) * ss((u - 0.5) / 0.5))
        # 4. hold on the pin 2.5 s
        pc = np.array(P3[-1]); pp = math.degrees(math.atan2(gy(*pin) - pc[1], max(np.linalg.norm(pin - pc[[0, 2]]), 1)))
        for f in range(int(2.5 * FPS)): P3.append(list(pc)); Y3.append(Y3[-1]); D3.append(pp)
        yu = np.unwrap(np.radians(Y3)) * 180 / math.pi; yaw = ratelimit(smooth(yu, 0.8 * FPS), LIM); pit = ratelimit(smooth(np.array(D3), 0.8 * FPS), LIM)
        yaw[:int(5.2 * FPS)] = yu[0]; yaw = ratelimit(yaw, LIM)
        # the rate limiter can leave the pitch short of the pin target at the end: report the final pitch vs the target
        w_y = max(abs(yaw[i + FPS] - yaw[i]) for i in range(len(yaw) - FPS)); w_p = max(abs(pit[i + FPS] - pit[i]) for i in range(len(pit) - FPS))
        return P3, yaw, pit, w_y, w_p, pp
    for slow in (1.0, 1.15, 1.3, 1.5, 1.75, 2.0):
        P, yaw, pit, w_y, w_p, pend = build(slow)
        ok = w_y <= 5.0 + 1e-6 and w_p <= 5.0 + 1e-6 and abs(pit[-1] - pend) < 1.5
        if ok: break
    gap = min(P[i][1] - gy(P[i][0], P[i][2]) for i in range(len(P)))
    print('hole %d: %d frames %.1f s (flight x%.2f), play %.0f last %.0f, max yaw over 1 s %.2f, max pitch over 1 s %.2f, end pitch %.1f (pin %.1f), end height %.1f m, min clearance %.1f m -> %s' % (hole, len(P), len(P) / FPS, slow, h_play, h_last, w_y, w_p, pit[-1], pend, P[-1][1] - gy(P[-1][0], P[-1][2]), gap, 'OK' if ok else 'FAIL'))
    if not ok: failed.append(hole)
    if plots:
        import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
        t = np.arange(len(P)) / FPS; fig, ax = plt.subplots(2, 1, figsize=(10, 4.5), sharex=True)
        ax[0].plot(t, yaw, lw=1.5); ax[0].set_ylabel('heading (deg)'); ax[0].set_title('Hole %d flyover: max change over any 1 s - yaw %.1f deg, pitch %.1f deg (limit 5)' % (hole, w_y, w_p), fontsize=10)
        ax[1].plot(t, pit, lw=1.5, color='#b85'); ax[1].set_ylabel('pitch (deg)'); ax[1].set_xlabel('seconds')
        for a in ax: a.grid(alpha=0.3)
        fig.tight_layout(); os.makedirs(plots, exist_ok=True); fig.savefig(os.path.join(plots, 'hole%02d_flyover_angles.png' % hole), dpi=110); plt.close(fig)
    for n, p in enumerate(P):
        yr, pr = math.radians(yaw[n]), math.radians(pit[n]); dv = np.array([math.sin(yr) * math.cos(pr), math.sin(pr), math.cos(yr) * math.cos(pr)])
        shots.append(dict(name='fly%02d_%04d' % (hole, n), pos=[round(float(c), 3) for c in p], lookAt=[round(float(c), 3) for c in np.array(p) + dv * 50], aboveGround=False, fov=52))
if failed: print('FAILED (rate limit):', failed); sys.exit(1)
c = dict(base); c['shots'] = shots; json.dump(c, open(out, 'w'))



