# Bunkers vs the Oct 29 photos: which GPS/heading photos look at which model bunker; writes bk_photos.json + a shot config
import json, math, csv, os
import numpy as np
A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/'; SP = os.path.dirname(os.path.abspath(__file__))
B = json.load(open(A + 'bunkers.json'))
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
lines = {}
for h in g['Holes'][:18]:
    T = {t['TeeType']: t['Position'] for t in h['Tees'] if t.get('Position')}
    lines[h['HoleNumber']] = np.array([[T[t]['x'], T[t]['z']] for t in ('Black', 'AimPoint1', 'AimPoint2', 'GreenCenterPoint') if t in T])
def dline(P, q):
    best = 1e9
    for a, b in zip(P[:-1], P[1:]):
        ab = b - a; u = np.clip(np.dot(q - a, ab) / np.dot(ab, ab), 0, 1); best = min(best, np.linalg.norm(q - a - u * ab))
    return best
info = {}
for n, m in B.items():
    if not m['t']: continue
    V = np.array(m['v']); T = np.array(m['t'], int).reshape(-1, 3); a = V[T][:, :, [0, 2]]
    ar = 0.5 * np.abs((a[:, 1, 0] - a[:, 0, 0]) * (a[:, 2, 1] - a[:, 0, 1]) - (a[:, 2, 0] - a[:, 0, 0]) * (a[:, 1, 1] - a[:, 0, 1])); c = (a.mean(1) * ar[:, None]).sum(0) / ar.sum()
    hole = min(lines, key=lambda k: dline(lines[k], c)); gc = lines[hole][-1]
    info[n] = dict(c=c.tolist(), area=float(ar.sum()), depth=float(np.percentile(V[:, 1], 98) - V[:, 1].min()), hole=hole, to_green=float(np.linalg.norm(c - gc)), ymin=float(V[:, 1].min()), ymax=float(V[:, 1].max()))
ph = [r for r in csv.DictReader(open('C:/Users/user/Documents/tierra-rejada/reference/visit_2026-10-29/photos_index.csv', encoding='utf-8')) if r['file'].endswith('.JPEG') and r['unity_x'] and r['heading_deg']]
match = {}
for n, b in info.items():
    cands = []
    for r in ph:
        px, pz, hd = float(r['unity_x']), float(r['unity_z']), float(r['heading_deg']); dx, dz = b['c'][0] - px, b['c'][1] - pz; d = math.hypot(dx, dz)
        if d < 6 or d > 110: continue
        brg = math.degrees(math.atan2(dx, dz)) % 360; off = abs((brg - hd + 180) % 360 - 180)
        if off > 30: continue
        cands.append((off / 30 + abs(d - 35) / 60, r['file'], round(d, 1), round(off, 1), px, pz, hd))
    cands.sort()
    if cands: match[n] = dict(info[n], photos=[dict(file=c[1], dist=c[2], off_deg=c[3], x=c[4], z=c[5], heading=c[6]) for c in cands[:2]])
print(len(info), 'bunkers;', len(match), 'with photos'); 
for n, m in sorted(match.items(), key=lambda kv: (kv[1]['hole'], kv[0])): print(m['hole'], n.replace('Spline_bunker_', '').replace('_Bunker_-_Mesh', ''), 'area %.0f depth %.2f green %.0f m' % (m['area'], m['depth'], m['to_green']), [(p['file'], p['dist'], p['off_deg']) for p in m['photos']])
json.dump(dict(info=info, match=match), open(A + 'bk_photos.json', 'w'), indent=1)
