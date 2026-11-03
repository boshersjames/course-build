import json, re, sys
fn, x0, ynew = sys.argv[1], float(sys.argv[2]), sys.argv[3]
raw = open(fn, 'rb').read(); bom = raw.startswith(b'\xef\xbb\xbf'); t = raw[3:].decode('utf-8') if bom else raw.decode('utf-8')
pat = re.compile(r'("DZpos"\s*:\s*\{\s*"x"\s*:\s*' + re.escape('%.3f' % x0).replace(r'\.000', r'\.0+') + r'[0-9]*\s*,\s*"y"\s*:\s*)(-?[\d.]+)')
m = pat.search(t)
if not m:
    pat = re.compile(r'("DZpos"\s*:\s*\{\s*"x"\s*:\s*762(?:\.0+)?\s*,\s*"y"\s*:\s*)(-?[\d.]+)'); m = pat.search(t)
assert m, 'DZpos not found'
t2 = t[:m.start(2)] + ynew + t[m.end(2):]; g = json.loads(t2)
open(fn, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + t2.encode('utf-8'))
print('DZpos y', m.group(2), '->', ynew, [h['DZpos'] for h in g['Hazards'] if h.get('hasDZ') and abs(h['DZpos']['x'] - x0) < 0.01])
