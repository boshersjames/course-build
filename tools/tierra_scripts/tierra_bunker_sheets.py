import json, sys, os
from PIL import Image, ImageOps, ImageDraw
run = sys.argv[1]; out = sys.argv[2]; os.makedirs(out, exist_ok=True)
M = json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/bk_photos.json'))['match']
SRC = 'C:/Users/user/Documents/tierra-rejada/reference/iCloud Photos/iCloud Photos/'
by = {}
for n, m in M.items():
    k = m['photos'][0]['file'].split('.')[0]; by.setdefault(k, []).append((m['hole'], n.replace('Spline_bunker_', 'B').replace('_Bunker_-_Mesh', '')))
keys = sorted(by); pages = [keys[i:i + 6] for i in range(0, len(keys), 6)]
for pi, pg in enumerate(pages):
    s = Image.new('RGB', (1800, 430 * ((len(pg) + 2) // 3)), 'white'); d = ImageDraw.Draw(s)
    for i, k in enumerate(pg):
        r, c = divmod(i, 3); x0, y0 = c * 600, r * 430
        ph = ImageOps.exif_transpose(Image.open(SRC + k + '.JPEG')).convert('RGB'); ph.thumbnail((300, 400))
        gm = Image.open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/%s/shots/pv_%s.png' % (run, k)).convert('RGB').resize((300, 400))
        s.paste(ph, (x0, y0 + 24)); s.paste(gm, (x0 + 300, y0 + 24))
        d.text((x0 + 4, y0 + 4), '%s photo | in-game, %s' % (k, ', '.join('h%d %s' % hb for hb in by[k])), fill='black')
    s.save(out + 'bunker_photo_vs_game_%d.jpg' % (pi + 1), quality=82)
print(len(pages))
