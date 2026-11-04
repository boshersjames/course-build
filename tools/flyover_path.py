"""Headless twin of CommercialFlyoverPath: build and check the cinematic flight for every hole without Unity.

    python tools/flyover_path.py [--holes 10] [--tee Black] [--out progress/flyover_path] [--set key=value ...]

Same control points (intro hold by the hole sign, rise, reveal, bunker dips on slow flights, approach, end or orbit), the same centripetal
Catmull-Rom spline, speed profile, flight_s scaling, gaze blends and the same 1 s yaw / pitch window check as the Unity tool, with the
ground from the LiDAR heightmap (course.json paths.heightmap) and the green radius from the course SVG instead of the scene. Rules:
rules/flyovers.json cinematic (course.json rule_overrides apply); --set cinematic.key=value tries a value without editing the file, so a
rule can be tuned against all 18 holes here before it is committed. Writes flyover_path.md (one row per hole: seconds, arc, worst yaw and
pitch, clearance, PASS/FAIL, where the worst pitch sits) and per-hole control-point CSVs. Exit 1 if any hole fails. Nothing is rendered.
"""
import argparse, csv, json, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_io import course, rules, Heightmap, GKD, svg_outlines


def smoother(t): t = min(1.0, max(0.0, t)); return t * t * t * (t * (t * 6 - 15) + 10)


def catmull(p0, p1, p2, p3, t):
    def tj(ti, a, b): return ti + math.sqrt(max(np.linalg.norm(b - a), 1e-9))
    t0 = 0.0; t1 = tj(t0, p0, p1); t2 = tj(t1, p1, p2); t3 = tj(t2, p2, p3)
    if t2 - t1 < 1e-4: return p1.copy()
    tt = t1 + (t2 - t1) * t
    a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1; a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2; a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3
    b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2; b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3
    return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2


def sample_spline(cp, step):
    pts = []; ext = [cp[0]] + list(cp) + [cp[-1]]
    for i in range(1, len(ext) - 2):
        n = max(2, int(np.linalg.norm(ext[i + 1] - ext[i]) / step))
        for k in range(n): pts.append(catmull(ext[i - 1], ext[i], ext[i + 1], ext[i + 2], k / float(n)))
    pts.append(cp[-1].copy()); return pts


def set_path(d, key, val):
    ks = key.split("."); cur = d
    for k in ks[:-1]: cur = cur.setdefault(k, {})
    try: v = json.loads(val)
    except Exception: v = val
    cur[ks[-1]] = v


def build(C, HM, R, G, hole, tee, sign, green_r, limits):
    h = G.hole(hole); tees = {t["TeeType"]: t for t in h.raw.get("Tees", []) if t.get("Position") and (t["Position"]["x"] or t["Position"]["z"])}
    def P(k): t = tees.get(k); return None if not t else np.array([t["Position"]["x"], t["Position"]["y"], t["Position"]["z"]], float)
    teeP = next((v for v in (P(tee), P("Blue"), P("White"), P("Black")) if v is not None), None); gc = P("GreenCenterPoint")
    if teeP is None or gc is None: return None, "tee or green centre missing"
    line = [teeP] + [P(k) for k in ("AimPoint1", "AimPoint2") if P(k) is not None] + [gc]
    pins = [p for p in h.pins if p["Position"]["x"] or p["Position"]["z"]]; pin = np.array([pins[0]["Position"]["x"], pins[0]["Position"]["y"], pins[0]["Position"]["z"]]) if pins else gc
    def xz(v): return np.array([v[0], v[2]])
    seg = [0.0]
    for i in range(1, len(line)): seg.append(seg[-1] + float(np.linalg.norm(xz(line[i]) - xz(line[i - 1]))))
    holeLen = seg[-1]
    def along(d):
        d = min(max(d, 0.0), holeLen)
        for i in range(1, len(line)):
            if d <= seg[i]: u = (d - seg[i - 1]) / max(seg[i] - seg[i - 1], 1e-3); return line[i - 1] + (line[i] - line[i - 1]) * u
        return line[-1].copy()
    def along_of(p):
        best, bd = 1e9, 0.0
        for i in range(1, len(line)):
            a, b = xz(line[i - 1]), xz(line[i]); ab = b - a; u = min(1.0, max(0.0, float(np.dot(xz(p) - a, ab) / max(ab @ ab, 1e-6)))); d = float(np.linalg.norm(xz(p) - (a + ab * u)))
            if d < best: best, bd = d, seg[i - 1] + u * float(np.linalg.norm(ab))
        return bd
    def ground(x, z): return float(HM.sample(x, z, 1))
    def gmax(x, z, r): return max(ground(x + i * r, z + j * r) for i in (-1, 0, 1) for j in (-1, 0, 1))
    dir0 = xz(line[1]) - xz(line[0]); dir0 /= max(np.linalg.norm(dir0), 1e-6); right = np.array([dir0[1], -dir0[0]])
    I = R["intro"]
    if sign is not None: ixz = np.array([sign["x"], sign["z"]]) - dir0 * I["back_m"] + right * I["side_m"]
    else: ixz = xz(teeP) - dir0 * (R["start_behind_tee_m"] + I["back_m"])
    introPos = np.array([ixz[0], ground(ixz[0], ixz[1]) + I["eye_height_m"], ixz[1]]); il = along(min(I["look_ahead_m"], holeLen)); introLook = np.array([il[0], ground(il[0], il[2]) + I["eye_height_m"], il[2]])
    cps = []; names = []
    def add(p, n): cps.append(np.array(p, float)); names.append(n)
    add(introPos, "start")
    # the rise point keeps part of the intro camera's sideways offset from the line, so the return to the line is spread over the rise and reveal legs
    latOff = float(np.dot(xz(introPos) - xz(along(along_of(introPos))), right))
    s0 = along(min(along_of(introPos) + R.get("rise_ahead_m", 15.0), holeLen * 0.2)); s0 = s0 + np.array([right[0], 0, right[1]]) * latOff * R.get("rise_keep_offset", 0.0)
    add([s0[0], gmax(s0[0], s0[2], 10) + R["start_height_m"], s0[2]], "rise")
    revealD = max(60.0, holeLen * R.get("reveal_fraction", 0.3)); rv = along(revealD); add([rv[0], gmax(rv[0], rv[2], 20) + R["reveal_height_m"], rv[2]], "reveal")
    fast = R.get("flight_s", 0) > 0 and holeLen / R["flight_s"] > R.get("fast_flight_m_s", 20)
    orbit_on = R["orbit"]["degrees"] >= 15
    vOrbit = R["speed"]["base_m_s"] * R["orbit"]["speed_factor"]; radius = max(green_r + R["orbit"]["radius_margin_m"], vOrbit / math.radians(limits["max_yaw_deg_s"]))
    approachD = max(R["approach_short_m"], radius + R.get("approach_outside_orbit_m", 8)) if orbit_on else R["approach_short_m"]
    ap = along(holeLen - approachD); add([ap[0], gmax(ap[0], ap[2], 15) + R["approach_height_m"], ap[2]], "approach")
    orbit_start = len(cps)
    if orbit_on:
        ang0 = math.atan2(ap[0] - gc[0], ap[2] - gc[2]); nOrb = max(8, int(round(R["orbit"]["degrees"] / 30.0)))
        for k in range(nOrb + 1):
            a = ang0 + math.radians(R["orbit"]["degrees"]) * k / nOrb; px, pz = gc[0] + math.sin(a) * radius, gc[2] + math.cos(a) * radius
            add([px, gmax(px, pz, 8) + R["orbit"]["height_m"], pz], "orbit-in" if k == 0 else "orbit-out" if k == nOrb else "orbit")
    else:
        e = along(holeLen - R.get("end_short_m", 20)); add([e[0], gmax(e[0], e[2], 10) + R.get("end_height_m", 15), e[2]], "end")
    pts = sample_spline(cps, R["sample_m"]); lifted = 0
    for i, p in enumerate(pts):
        gm = gmax(p[0], p[2], 4)
        if p[1] < gm + limits["min_clear_m"]: pts[i] = np.array([p[0], gm + limits["min_clear_m"], p[2]]); lifted += 1
    arc = [0.0]
    for i in range(1, len(pts)): arc.append(arc[-1] + float(np.linalg.norm(pts[i] - pts[i - 1])))
    total = arc[-1]; arcA = np.array(arc)
    orbitArc = total
    if orbit_on: oin = cps[orbit_start]; orbitArc = arc[int(np.argmin([np.linalg.norm(p - oin) for p in pts]))]
    turn = np.zeros(len(pts))
    for i in range(1, len(pts) - 1):
        a = xz(pts[i] - pts[i - 1]); b = xz(pts[i + 1] - pts[i]); na, nb = np.linalg.norm(a), np.linalg.norm(b)
        if na > 1e-6 and nb > 1e-6: ang = math.degrees(math.acos(min(1, max(-1, float(np.dot(a, b) / (na * nb)))))); turn[i] = ang / max(np.linalg.norm(pts[i + 1] - pts[i - 1]) / 2, 1e-3)
    S = R["speed"]; straight = np.zeros(len(pts), bool); run = -1
    for i in range(len(pts) + 1):
        s = i < len(pts) and turn[i] <= S["straight_max_turn_deg_per_m"] and arc[i] < orbitArc - R["orbit"]["decel_lead_m"]
        if s and run < 0: run = i
        if not s and run >= 0:
            if arc[i - 1] - arc[run] >= S["straight_min_len_m"]: straight[run:i] = True
            run = -1
    def speed_at(d):
        v = S["base_m_s"]; i = min(max(int(np.searchsorted(arcA, d)), 0), len(pts) - 1)
        v *= S["ease_in_start_factor"] + (1 - S["ease_in_start_factor"]) * smoother(d / max(total * S["ease_in_fraction"], 1))
        if straight[i]: v *= S["straight_boost"]
        lead = R["orbit"]["decel_lead_m"]
        if orbit_on:
            if d >= orbitArc: v = S["base_m_s"] * R["orbit"]["speed_factor"]
            elif d >= orbitArc - lead: v = v + (S["base_m_s"] * R["orbit"]["speed_factor"] - v) * smoother((d - (orbitArc - lead)) / lead)
        else:
            el = R.get("end_ease_m", 0.0)
            if el > 0 and d > total - el: v = v + (S["base_m_s"] * R.get("end_speed_factor", 0.25) - v) * smoother((d - (total - el)) / el)   # settle: the camera slows into the end so a near target does not sweep the pitch
        return max(v, 1.0)
    def pos_at(d):
        d = min(max(d, 0.0), total); i = int(np.searchsorted(arcA, d)); i = min(max(i, 1), len(pts) - 1); u = (d - arc[i - 1]) / max(arc[i] - arc[i - 1], 1e-4); return pts[i - 1] + (pts[i] - pts[i - 1]) * u
    fps = R["fps"]; dt = 1.0 / fps
    speedScale = 1.0
    if R.get("flight_s", 0) > 0:
        tt = dd = 0.0; guard = 0
        while dd <= total and guard < 200000: dd += speed_at(dd) * dt; tt += dt; guard += 1
        speedScale = tt / max(R["flight_s"], 1.0)
    frames = []; introN = int(round(I["seconds"] * fps))
    for k in range(introN): frames.append((0.0, introPos, introLook, True))
    d = 0.0; look2 = R.get("lookahead_m", 30) * 2.0; gz = max(2 * R["orbit"]["decel_lead_m"], total * min(1.0, max(0.0, R.get("end_gaze_fraction", 0.0))))
    end_target = pin if R.get("end_look", "green") == "pin" else gc
    while d <= total and len(frames) < 20000:
        p = pos_at(d); inOrbit = d >= orbitArc; ahead = pos_at(min(d + R["lookahead_m"], total)); look = (pin if R["orbit"]["look_at"] == "pin" else gc) if inOrbit else ahead
        lead = R["orbit"]["decel_lead_m"]
        if not inOrbit and d > orbitArc - lead: look = ahead + (pin - ahead) * smoother((d - (orbitArc - lead)) / lead)
        if not orbit_on and d > total - gz: look = ahead + (end_target - ahead) * smoother((d - (total - gz)) / gz)
        if introN > 0 and d < look2: look = introLook + (look - introLook) * smoother(d / look2)
        frames.append((d, p, look, False)); d += speed_at(d) * speedScale * dt
    yaws = []; pitches = []; prev = None; minClear = 1e9
    for d, p, look, intro in frames:
        f = look - p; yaw = math.degrees(math.atan2(f[0], f[2])); pitch = -math.degrees(math.atan2(f[1], math.hypot(f[0], f[2])))
        if not intro: minClear = min(minClear, p[1] - ground(p[0], p[2]))
        if prev is None: yaws.append(0.0); pitches.append(0.0)
        else: dy = (yaw - prev[0] + 540) % 360 - 180; yaws.append(dy / dt); pitches.append((pitch - prev[1]) / dt)
        prev = (yaw, pitch)
    win = fps; maxY = maxP = 0.0; atP = 0
    for i in range(0, len(yaws) - win + 1):
        sy = abs(sum(yaws[i:i + win]) * dt); sp = abs(sum(pitches[i:i + win]) * dt); maxY = max(maxY, sy)
        if sp > maxP: maxP, atP = sp, i
    tf = len(frames) * dt; ok = maxY <= limits["max_yaw_deg_s"] and maxP <= limits["max_pitch_deg_s"] and minClear >= limits["min_clear_m"] - 0.05
    where = "intro" if frames[min(atP, len(frames) - 1)][3] else "d=%.0f m of %.0f" % (frames[min(atP, len(frames) - 1)][0], total)
    return {"hole": hole, "par": h.par, "line_m": holeLen, "arc_m": total, "seconds": tf, "intro_s": introN * dt, "flight_s": tf - introN * dt, "max_yaw": maxY, "max_pitch": maxP, "pitch_at": where, "min_clear": minClear, "lifted": lifted, "pass": ok, "cps": list(zip(names, cps)), "fast": fast, "speed_scale": speedScale}, None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--holes"); ap.add_argument("--tee", default="Black"); ap.add_argument("--out"); ap.add_argument("--set", action="append", default=[], help="cinematic.key=value override for this run")
    a = ap.parse_args(); C = course(); RR = rules(C)["flyovers"]; R = json.loads(json.dumps(RR["cinematic"]))
    for kv in a.set: k, v = kv.split("=", 1); set_path(R, k.replace("cinematic.", ""), v)
    limits = R["limits"]; HM = Heightmap(C); G = GKD(C)
    for h in G.holes: h.raw = next(x for x in G.data["Holes"] if x.get("HoleNumber") == h.number)
    signs = {}
    try:
        sp = C.path("signs")
        if sp and os.path.exists(sp):
            for e in json.load(open(sp, encoding="utf-8-sig")): signs[int(e["hole"])] = e
    except Exception: pass
    greens = {}
    try:
        for k, poly in svg_outlines(C, "green").items(): pass
    except Exception: pass
    out = a.out or os.path.join(C.dir, "progress", "flyover_path"); os.makedirs(out, exist_ok=True)
    holes = [int(x) for x in a.holes.split(",")] if a.holes else [h.number for h in G.holes if h.green_centre is not None]
    md = ["# Cinematic flight check: %s (%s tee)" % (C["name"], a.tee), "", "Rules flyovers.cinematic%s: target %s s (intro %s + flight %s), limits yaw %s / pitch %s deg per s, clearance %s m." % (" + " + ", ".join(a.set) if a.set else "", R.get("target_duration_s"), R["intro"]["seconds"], R.get("flight_s"), limits["max_yaw_deg_s"], limits["max_pitch_deg_s"], limits["min_clear_m"]), "",
          "| hole | par | line m | arc m | seconds | yaw deg/s | pitch deg/s | pitch at | clear m | sign | result |", "|---|---|---|---|---|---|---|---|---|---|---|"]
    allok = True
    for hn in holes:
        r, err = build(C, HM, R, G, hn, a.tee, signs.get(hn), 12.0, limits)
        if r is None: md.append("| %d | - | - | - | - | - | - | - | - | - | %s |" % (hn, err)); allok = False; continue
        allok &= r["pass"]
        md.append("| %d | %s | %.0f | %.0f | %.1f | %.1f | %.1f | %s | %.1f | %s | %s |" % (hn, r["par"], r["line_m"], r["arc_m"], r["seconds"], r["max_yaw"], r["max_pitch"], r["pitch_at"], r["min_clear"], "yes" if hn in signs else "no", "PASS" if r["pass"] else "**FAIL**"))
        with open(os.path.join(out, "H%02d_control_points.csv" % hn), "w", newline="") as fh:
            w = csv.writer(fh); w.writerow(["name", "x", "y", "z"]); [w.writerow([n, "%.2f" % p[0], "%.2f" % p[1], "%.2f" % p[2]]) for n, p in r["cps"]]
    open(os.path.join(out, "flyover_path.md"), "w").write("\n".join(md) + "\n"); print("\n".join(md)); print("wrote", out); sys.exit(0 if allok else 1)


if __name__ == "__main__":
    main()
