# Showcase flyover frames for one or more holes: opens on the hole sign, rises behind the back tee, flies tee -> aim -> green,
# settles on the Thursday pin. Camera keys are Catmull-Rom splined; heights are over the 2018 heightmap.
import json, math, sys
import numpy as np
SP = sys.argv[1]; holes = [int(h) for h in sys.argv[2].split(',')]; FPS = 25
H = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', dtype='<u2').reshape(2049, 2049).astype(float) * 207 / 65535; S = 2000 / 2048
def gy(x, z): return float(H[int(round(z / S)), int(round(x / S))])
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
signs = {s['hole']: s for s in json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/vegetation/hole_signs.json'))}
def cr(p, t):   # Catmull-Rom through key list p at t in [0, len-1]
    i = min(int(t), len(p) - 2); u = t - i; P = [p[max(i - 1, 0)], p[i], p[i + 1], p[min(i + 2, len(p) - 1)]]
    return 0.5 * ((2 * P[1]) + (-P[0] + P[2]) * u + (2 * P[0] - 5 * P[1] + 4 * P[2] - P[3]) * u * u + (-P[0] + 3 * P[1] - 3 * P[2] + P[3]) * u ** 3)
shots = []
for hole in holes:
    T = {t['TeeType']: np.array([t['Position']['x'], t['Position']['z']]) for t in g['Holes'][hole - 1]['Tees'] if t.get('Position')}
    pin = g['Holes'][hole - 1]['Pins'][0]['Position']; pin = np.array([pin['x'], pin['z']]); tee = T['Black']; aims = [T[k] for k in ('AimPoint1', 'AimPoint2') if k in T]
    sg = signs[hole]; s = np.array([sg['x'], sg['z']]); fwd = np.array([math.sin(math.radians(sg['yaw'])), math.cos(math.radians(sg['yaw']))])
    d = (pin - tee) / np.linalg.norm(pin - tee); mid = aims[0] if aims else (tee + pin) / 2
    def P(xz, up): return np.array([xz[0], gy(*xz) + up, xz[1]])
    cam = [P(s + fwd * 3.2, 1.5), P(s + fwd * 4.5 - d * 4, 10),   # second key 10 m up: the 15 path clipped a tree at 5 m
           P(tee - d * 22, 14), P(tee + (mid - tee) * 0.55, 32), P(mid + (pin - mid) * 0.5, 28), P(pin - d * 45, 16), P(pin - d * 22, 7)]
    look = [P(s, 1.25), P(s + d * 6, 2), P(mid, 0), P(pin, 0), P(pin, 0), P(pin, 0.5), P(pin, 0.5)]
    dur = [2.5, 2.5, 4.0, 4.0, 3.5, 3.5]   # seconds per segment
    n = 0
    for k, sec in enumerate(dur):
        frames = int(sec * FPS)
        for f in range(frames):
            u = f / frames; u = u * u * (3 - 2 * u) if k in (0, len(dur) - 1) else u; t = k + u
            shots.append(dict(name='fly%02d_%04d' % (hole, n), pos=cr(cam, t).round(3).tolist(), lookAt=cr(look, t).round(3).tolist(), aboveGround=False, fov=52)); n += 1
    for f in range(int(1.5 * FPS)):   # hold on the pin
        shots.append(dict(name='fly%02d_%04d' % (hole, n), pos=cam[-1].round(3).tolist(), lookAt=look[-1].round(3).tolist(), aboveGround=False, fov=52)); n += 1
    print(hole, n, 'frames')
c = json.load(open(SP + '/green_cur.json')); c['shots'] = shots; c['steps'] = []; c['saveScene'] = False; c['shotWidth'], c['shotHeight'] = 1280, 720
json.dump(c, open(SP + '/flyC_' + sys.argv[2].replace(',','_') + '.json', 'w'))

