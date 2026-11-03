import json, sys, os
from PIL import Image, ImageOps, ImageDraw
SP, hole, run, out = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]; run2 = sys.argv[5] if len(sys.argv) > 5 else None
SRC = 'C:/Users/user/Documents/tierra-rejada/reference/iCloud Photos/iCloud Photos/'; A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/'
al = [r for r in json.load(open(SP + '/audit/aligned_new.json')) if r['hole'] == hole]
per = 6; ncol = 3 if run2 else 2
for pi in range(0, len(al), per):
    pg = al[pi:pi + per]; s = Image.new('RGB', (2 * (ncol * 280 + 20), 3 * 400), 'white'); d = ImageDraw.Draw(s)
    for i, r in enumerate(pg):
        x0, y0 = (i % 2) * (ncol * 280 + 20), (i // 2) * 400
        s.paste(ImageOps.exif_transpose(Image.open(SP + '/newjpg/' + r['photo'] + '.jpg')).convert('RGB').resize((278, 371)), (x0, y0 + 24))
        for j, rr in enumerate([run] + ([run2] if run2 else [])):
            s.paste(Image.open(A + rr + '/shots/veg_' + r['photo'] + '.png').convert('RGB').resize((278, 371)), (x0 + 280 * (j + 1), y0 + 24))
        d.text((x0 + 2, y0 + 4), '%s %s' % (r['photo'], 'aligned' if r.get('used') else 'EXIF heading'), fill='black')
    s.save(out + '_%02d.jpg' % (pi // per + 1), quality=80)
print((len(al) + per - 1) // per)
