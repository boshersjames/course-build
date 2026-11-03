"""Contact sheets and colour-jump flags for a FlyoverReview run.

    python flyover_review.py <run folder>     (needs Pillow)

For each hole: holeNN_sheet.jpg (grid of flyover frames) and a section in flyover_review.md. A patch is flagged when the terrain
detail cards change its mean luminance (averaged over the frames where it is within 150 m) by more than 8 % (frame rendered with details on vs off, camera within 150 m),
i.e. the colour jump the eye sees as the patch crosses the detail distance.
"""
import colorsys, csv, glob, os, sys
from collections import defaultdict
from PIL import Image

run = sys.argv[1]
md = ['# Flyover review: ' + os.path.basename(run.rstrip('/\\')), '']
flagged = []
for csvp in sorted(glob.glob(os.path.join(run, 'hole*_patches.csv'))):
    hole = os.path.basename(csvp)[:6]
    frames = sorted(glob.glob(os.path.join(run, hole + '_f*.png')))
    if frames:
        cols = 6; w, h = 320, 180; rows = (len(frames) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * w, rows * h))
        for i, f in enumerate(frames): sheet.paste(Image.open(f).convert('RGB').resize((w, h)), ((i % cols) * w, (i // cols) * h))
        sheet.save(os.path.join(run, hole + '_sheet.jpg'), quality=80)
    by = defaultdict(list)
    for r in csv.DictReader(open(csvp)): by[int(r['patch'])].append(r)
    flags = []
    for k, rows_ in by.items():
        near = [float(r['detail_shift']) for r in rows_ if float(r['dist_m']) < 150]
        if len(near) >= 2 and abs(sum(near) / len(near)) > 0.08:
            flags.append('patch %d: detail cards shift its luminance by %+.0f%% on average over %d close frames' % (k, 100 * sum(near) / len(near), len(near)))
    for k, rows_ in ([] if 'detail_shift' in open(csvp).readline() else by.items()):
        rows_.sort(key=lambda r: int(r['frame']))
        for a, b in zip(rows_, rows_[1:]):
            if int(b['frame']) != int(a['frame']) + 1: continue
            la, lb = float(a['lum']), float(b['lum'])
            ha = colorsys.rgb_to_hsv(float(a['r']), float(a['g']), float(a['b']))[0]; hb = colorsys.rgb_to_hsv(float(b['r']), float(b['g']), float(b['b']))[0]
            dl = abs(lb - la) / max(la, 0.05); dh = min(abs(hb - ha), 1 - abs(hb - ha))
            near_switch = any(120 <= float(x['dist_m']) <= 280 for x in (a, b))
            if (dl > 0.08 or dh > 0.08) and near_switch:
                flags.append('patch %d frames %s->%s dist %s->%s m: lum %+.0f%%, hue shift %.2f' % (k, a['frame'], b['frame'], a['dist_m'], b['dist_m'], 100 * (lb - la) / max(la, 0.05), dh))
    md.append('## ' + hole + (' - %d flags' % len(flags) if flags else ' - no flags'))
    md += ['- ' + f for f in flags]
    md.append('')
    if flags: flagged.append(hole)
md.insert(1, 'Flagged holes: ' + (', '.join(flagged) if flagged else 'none'))
open(os.path.join(run, 'flyover_review.md'), 'w').write('\n'.join(md))
print('\n'.join(md[:3]))


