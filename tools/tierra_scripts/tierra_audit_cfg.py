# Photo-audit render configs for editor C: high/medium shots, eye = Unity terrain (2018 heightmap) + 1.5 m, look along `forward`, vfov.
import json, math, sys, numpy as np
SP = sys.argv[1]; holes = [int(h) for h in sys.argv[2].split(',')]
H = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', dtype='<u2').reshape(2049, 2049).astype(float) * 207 / 65535; S = 2000 / 2048
def gy(x, z):
    fx, fz = x / S, z / S; i, j = int(fz), int(fx); u, v = fz - i, fx - j
    return float(H[i, j] * (1 - u) * (1 - v) + H[i + 1, j] * u * (1 - v) + H[i, j + 1] * (1 - u) * v + H[i + 1, j + 1] * u * v)
base = json.load(open(SP + '/green_cur.json'))
for h in holes:
    js = json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/photo_poses/hole_%02d.json' % h))
    for orient in ('P', 'L'):
        shots = []; log = []
        for s in js['shots']:
            if s.get('confidence') not in ('high', 'medium'): continue
            if (s['height'] > s['width']) != (orient == 'P'): continue
            # euler check: forward from heading/pitch must match the stored vector
            hd, pd = math.radians(s['heading_deg']), math.radians(s['pitch_down_deg']); fe = [math.sin(hd) * math.cos(pd), -math.sin(pd), math.cos(hd) * math.cos(pd)]
            err = max(abs(a - b) for a, b in zip(fe, s['forward']))
            ty = gy(s['x'], s['z']); eye = [s['x'], ty + 1.5, s['z']]; f = s['forward']
            shots.append(dict(name='%s_%s' % (s['photo'], s['confidence'][0]), pos=[round(v, 3) for v in eye], lookAt=[round(eye[i] + f[i] * 100, 3) for i in range(3)], aboveGround=False, fov=s['vfov_deg']))
            log.append('%s %s euler-vs-forward %.3f terrain-vs-lidar %.2f m roll %.1f' % (s['photo'], s['confidence'], err, ty + 1.5 - s['eye_y'], s['roll_deg']))
        if not shots: continue
        c = dict(base); c['shots'] = shots; c['steps'] = [dict(name='FullDetail', tool='TierraRenderC', method='FullDetail'), dict(name='TeeMarkers', tool='TierraTeeOverrides', method='Markers')]; c['saveScene'] = False
        c['shotWidth'], c['shotHeight'] = (960, 1280) if orient == 'P' else (1280, 960)
        json.dump(c, open(SP + '/audit_h%02d_%s.json' % (h, orient), 'w')); print('hole', h, orient, len(shots)); print('\n'.join(log))
