# Append the hazards in a patch json to a GKD and bump hazardCount. The new hazards are inserted after the last one and hazardCount
# is bumped; every other byte (BOM, CRLF, number text, the existing hazards and hole/pin blocks) stays as it is. Refuses if Hazards changed since the patch was made; a second run
# says "already applied". Run: python scripts/gkd_append_hazards.py <GKD> <patch.json>
import json, hashlib, re, sys
fn, pf = sys.argv[1], sys.argv[2]
P = json.load(open(pf))
raw = open(fn, 'rb').read(); bom = raw.startswith(b'\xef\xbb\xbf'); txt = (raw[3:] if bom else raw).decode('utf-8')
nl = '\r\n' if '\r\n' in txt else '\n'
g = json.loads(txt)
key = lambda v: hashlib.sha1(json.dumps(v, sort_keys=True).encode()).hexdigest()
if any(h['coords'] == a['coords'] for a in P['append_hazards'] for h in g['Hazards']): raise SystemExit('already applied')
if key(g['Hazards']) != P['expect']['Hazards'] or g['hazardCount'] != P['expect']['hazardCount']:
    raise SystemExit('Hazards differ from what the patch was made from; not applied')
m = re.search(r'"Hazards":\s*', txt); i = m.end(); assert txt[i] == '['
d = 0
for j in range(i, len(txt)):
    if txt[j] == '[': d += 1
    elif txt[j] == ']':
        d -= 1
        if d == 0: break
new_h = g['Hazards'] + P['append_hazards']
k = j - 1
while txt[k] in ' \t\r\n': k -= 1          # k = the closing brace of the last hazard; insert after it, existing text untouched
assert txt[k] == '}'
if '\n' in txt[i:j + 1]:
    ind = re.search(r'\[\s*?\n([ \t]*)\{', txt[i:j + 1]).group(1)
    add = ''.join(',' + nl + ind + json.dumps(a, indent=2, ensure_ascii=False).replace('\n', nl + ind) for a in P['append_hazards'])
else:
    add = ''.join(',' + json.dumps(a, ensure_ascii=False) for a in P['append_hazards'])
txt = txt[:k + 1] + add + txt[k + 1:]
txt, n = re.subn(r'("hazardCount":\s*)\d+', lambda mm: mm.group(1) + str(len(new_h)), txt, count=1); assert n == 1
g2 = json.loads(txt)
for k in g:
    if k not in ('Hazards', 'hazardCount'): assert g2[k] == g[k], k
assert g2['hazardCount'] == len(g2['Hazards'])
open(fn, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + txt.encode('utf-8'))
print('applied: hazards %d -> %d; everything else unchanged' % (len(g['Hazards']), len(g2['Hazards'])))
for a in P['append_hazards']: print('  + hazard %d: %d pts' % (g['hazardCount'] + P['append_hazards'].index(a), a['pointCount']))
