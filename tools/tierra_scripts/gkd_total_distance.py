# TeeTypeTotalDistance: for each enabled tee type, the sum of the 18 holes' enabled tee Distance; text edit of those numbers only.
import json, re, sys
fn = sys.argv[1]; raw = open(fn, 'rb').read(); bom = raw.startswith(b'\xef\xbb\xbf'); t = raw[3:].decode('utf-8') if bom else raw.decode('utf-8'); g = json.loads(t)
sums = {}
for h in g['Holes']:
    for tt in h['Tees']:
        if tt.get('Enabled') and tt.get('Position') and tt['TeeType'] not in ('AimPoint1', 'AimPoint2', 'GreenCenterPoint'): sums[tt['TeeType']] = sums.get(tt['TeeType'], 0) + tt['Distance']
i0 = t.index('"TeeTypeTotalDistance"'); j0 = t.index(']', i0); seg = t[i0:j0]; out = seg; log = []
for e in g['TeeTypeTotalDistance']:
    name = e.get('TeeType') or e.get('teeType') or e.get('Type')
    if name not in sums: continue
    pat = re.compile(r'(\{[^{}]*"[Tt]ee[Tt]ype"\s*:\s*"%s"[^{}]*"Distance"\s*:\s*)(-?[\d.eE+-]+)' % re.escape(name)); m = pat.search(out)
    if not m: pat = re.compile(r'(\{[^{}]*"Distance"\s*:\s*)(-?[\d.eE+-]+)([^{}]*"[Tt]ee[Tt]ype"\s*:\s*"%s")' % re.escape(name)); m = pat.search(out)
    if not m: raise SystemExit('no entry for ' + name)
    out = out[:m.start(2)] + ('%.5f' % sums[name]) + out[m.end(2):]; log.append('%-6s %.2f -> %.2f' % (name, e['Distance'], sums[name]))
t2 = t[:i0] + out + t[j0:]; json.loads(t2); open(fn, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + t2.encode('utf-8')); print('\n'.join(log))
