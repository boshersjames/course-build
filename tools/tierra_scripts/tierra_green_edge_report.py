# Green edge pass outputs: per-green sheets (front nine + scanned 14 reference), edge profile table, pin checks.
import json, csv, os
import numpy as np
from scipy.spatial import cKDTree
from matplotlib.path import Path as MP
from PIL import Image, ImageDraw
A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/'; OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/green_edges_1029/'; os.makedirs(OUT, exist_ok=True)
BEF, AFT = A + '20261029_135904/shots/', A + '20261029_135934/shots/'
GI = json.load(open(A + 'fairways/greens_info.json')); G = json.load(open(A + 'fairways/greens.json')); EP = json.load(open(A + 'fairways/edge_profiles.json'))
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
# sheets
for hn in range(1, 10):
    s = Image.new('RGB', (1600, 2 * 455 + 30), 'white'); d = ImageDraw.Draw(s)
    for r, h in enumerate((hn, 14)):
        for j, (dd, n) in enumerate(((BEF, 'approach'), (AFT, 'approach'), (BEF, 'top'), (AFT, 'top'))):
            im = Image.open(dd + 'ge_h%d_%s.png' % (h, n)).convert('RGB').resize((400, 225)); s.paste(im, (j * 400, 30 + r * 455))
            z = Image.open(dd + 'ge_h%d_%s.png' % (h, n)).convert('RGB').crop((500, 250, 1100, 588)).resize((400, 225)); s.paste(z, (j * 400, 30 + r * 455 + 228))
        d.text((4, 30 + r * 455 + 4), ('Hole %d' % h) + ('' if r == 0 else ' (scanned back-nine green, reference)'), fill='white')
    d.text((4, 8), 'Hole %d green edge: approach before | after, top-down before | after (lower rows: zoom on the edge). Bottom: scanned hole 14 green for reference.' % hn, fill='black')
    s.save(OUT + 'green_edge_h%d_before_after.jpg' % hn, quality=82)
# pin checks: distance to the green edge and local slope from the green mesh
def weld(V, T):
    _, f, inv = np.unique(np.round(V[:, [0, 2]] / 0.002).astype(np.int64), axis=0, return_index=True, return_inverse=True); return V[f], inv.ravel()[T]
pins = []
for h in g['Holes'][:18]:
    gi = GI[str(h['HoleNumber'])]; edge = np.array(gi['edge']); P = MP(edge[:, [0, 2]]); kd = cKDTree(edge[:, [0, 2]])
    V = np.array(G[gi['mesh']]['v'])
    for p in h.get('Pins', []):
        if not p.get('Position'): continue
        q = np.array([p['Position']['x'], p['Position']['z']]); de = kd.query(q)[0] * (1 if P.contains_point(q) else -1)
        m = np.linalg.norm(V[:, [0, 2]] - q, axis=1) < 1.5
        sl = np.nan
        if m.sum() >= 6:
            M = np.c_[np.ones(m.sum()), V[m, 0] - q[0], V[m, 2] - q[1]]; c, *_ = np.linalg.lstsq(M, V[m, 1], rcond=None); sl = 100 * np.hypot(c[1], c[2])
        flag = []
        if de < 3: flag.append('within 3 m of the edge')
        if sl > 3.5: flag.append('slope over 3.5%')
        pins.append(dict(hole=h['HoleNumber'], day=p['Day'], edge_m=round(float(de), 1), slope_pct=round(float(sl), 1), flag='; '.join(flag)))
with open(OUT + 'pin_check.csv', 'w', newline='') as f: w = csv.DictWriter(f, fieldnames=list(pins[0])); w.writeheader(); w.writerows(pins)
for p in pins:
    if p['flag']: print(p)
# edge profile table
dist = np.array(EP['dist']); rows = []
def at(pr, a, b): pr = np.array(pr, float); m = (dist >= a) & (dist < b); return round(float(np.nanmedian(pr[m]) * 100), 1) if np.isfinite(pr[m]).any() else ''
for k, pr in list(EP['profiles'].items()) + [('median', EP['median'])]:
    rows.append(dict(green=k, rise_0_0p5m_cm=at(pr, 0, 0.5), rise_0p5_1p5m_cm=at(pr, 0.5, 1.5), rise_1p5_3m_cm=at(pr, 1.5, 3), rise_3_5m_cm=at(pr, 3, 5), on_green_minus2_0m_cm=at(pr, -2, 0)))
with open(OUT + 'edge_profile_by_green.csv', 'w', newline='') as f: w = csv.DictWriter(f, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
for r in rows: print(r)
