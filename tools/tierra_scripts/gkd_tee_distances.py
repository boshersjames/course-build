# Recompute GKD tee Distance for every enabled playing tee (2D metres: tee -> AimPoint1 -> AimPoint2 -> GreenCenterPoint, the way the
# file's correct entries are computed); rewrite only those off by > tol m. Text edit of the Distance numbers only (format, BOM kept).
import json, math, re, sys
fn = sys.argv[1]; tol = float(sys.argv[2]) if len(sys.argv) > 2 else 2.0
raw = open(fn, 'rb').read(); bom = raw.startswith(b'\xef\xbb\xbf'); txt = raw[3:].decode('utf-8') if bom else raw.decode('utf-8')
g = json.loads(txt)
flat = []
for h in g['Holes']:
    T = {t['TeeType']: t for t in h['Tees']}; gc = T.get('GreenCenterPoint', {}).get('Position'); aims = [T[k]['Position'] for k in ('AimPoint1', 'AimPoint2') if k in T and T[k].get('Position')]
    for t in h['Tees']:
        want = None
        if t.get('Enabled') and t.get('Position') and gc and t['TeeType'] not in ('AimPoint1', 'AimPoint2', 'GreenCenterPoint'):
            pts = [t['Position']] + aims + [gc]; want = sum(math.hypot(pts[i + 1]['x'] - pts[i]['x'], pts[i + 1]['z'] - pts[i]['z']) for i in range(len(pts) - 1))
        flat.append((h['HoleNumber'], t['TeeType'], t.get('Distance'), want))
pat = re.compile(r'\{[^{}]*"TeeType"\s*:\s*"([^"]+)"[^{}]*(?:\{[^{}]*\}[^{}]*)?\}')
ms = list(pat.finditer(txt))
# align: walk the text blocks in order and take the next one whose TeeType and Distance match each tee entry (other objects carry TeeType too)
pairs = []; k = 0
for hole, tt, old, want in flat:
    while k < len(ms):
        b = ms[k]; k += 1
        try: o = json.loads(b.group(0))
        except Exception: continue
        if o.get('TeeType') == tt and o.get('Distance') == old: pairs.append((b, (hole, tt, old, want))); break
    else: raise SystemExit('could not align hole %d %s' % (hole, tt))
out = []; pos = 0; changes = []
for m, (hole, tt, old, want) in pairs:
    blk = m.group(0)
    if want is not None and old is not None and abs(old - want) > tol:
        blk = re.sub(r'("Distance"\s*:\s*)-?[\d.eE+-]+', lambda q: q.group(1) + ('%.5f' % want), blk, count=1); changes.append((hole, tt, old, want))
    out.append(txt[pos:m.start()]); out.append(blk); pos = m.end()
out.append(txt[pos:]); txt2 = ''.join(out); g2 = json.loads(txt2)
open(fn, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + txt2.encode('utf-8'))
for c in changes: print('hole %d %-6s %8.2f -> %8.2f m (%+.2f; %.0f yd)' % (c[0], c[1], c[2], c[3], c[3] - c[2], c[3] * 1.09361))
print(len(changes), 'changed of', sum(1 for f in flat if f[3] is not None), 'enabled tees; json valid')
