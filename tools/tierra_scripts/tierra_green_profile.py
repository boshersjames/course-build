# Current game surface around a green (BoJa Oct 30, 17: "the whole green is on a hill, the top and bottom slope a lot, especially the sides").
# Surface = the course terrain heightmap (2018 LiDAR, ~1 m) which the OPCD green mesh drapes; outline = the course green mesh.
# Sheet: shaded relief + 2 cm contours on the green / 10 cm around it, slope %, and two cross-sections (along play and across)
# extended 20 m past the edges, with the mesh vertex heights on top so any mesh-vs-terrain offset shows. Analysis only.
import json, sys, os
import numpy as np
from scipy import ndimage
from matplotlib.path import Path
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
hole = int(sys.argv[1]); OUT = sys.argv[2]; os.makedirs(OUT, exist_ok=True)
HM = np.fromfile('C:/Users/user/Documents/tierra-rejada/terrain/tierra_rejada_2049.raw', '<u2').reshape(2049, 2049).astype(float) / 65535 * 207; S = 2000 / 2048
def lidar(x, z): return ndimage.map_coordinates(HM, [np.asarray(z) / S, np.asarray(x) / S], order=3, mode='nearest')
g = json.load(open('C:/Users/user/Documents/tierra-rejada/greenkeeper/TierraRejada.GKD', encoding='utf-8-sig'))
T = {t['TeeType']: t['Position'] for t in g['Holes'][hole - 1]['Tees'] if t['Position']}; gc = T['GreenCenterPoint']
GM = json.load(open('C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/greens.json'))
k = min((k for k in GM if k.endswith('_Green_-_Mesh') and GM[k]), key=lambda k: np.hypot(*(np.array(GM[k]['v'])[:, [0, 2]].mean(0) - [gc['x'], gc['z']])))
V = np.array(GM[k]['v']); tee = T.get('Blue') or T.get('White')
ax_ = np.array([gc['x'] - tee['x'], gc['z'] - tee['z']]); ax_ /= np.linalg.norm(ax_); cr = np.array([-ax_[1], ax_[0]])
C = 0.25; x0, x1 = V[:, 0].min() - 20, V[:, 0].max() + 20; z0, z1 = V[:, 2].min() - 20, V[:, 2].max() + 20
xs = np.arange(x0, x1, C); zs = np.arange(z0, z1, C); XX, ZZ = np.meshgrid(xs, zs); H = lidar(XX, ZZ)
# outline: rasterise mesh vertices, close, fill
gm = np.zeros(H.shape, bool); gm[((V[:, 2] - z0) / C).astype(int), ((V[:, 0] - x0) / C).astype(int)] = True; gm = ndimage.binary_fill_holes(ndimage.binary_closing(gm, iterations=6))
gy, gx = np.gradient(ndimage.gaussian_filter(H, 1.0 / C), C); slope = 100 * np.hypot(gx, gy)
area = gm.sum() * C * C; hg = H[gm]; relief = hg.max() - hg.min()
fig = plt.figure(figsize=(22, 13)); ext = [x0, x1, z0, z1]
a1 = fig.add_subplot(2, 3, 1); ls = matplotlib.colors.LightSource(315, 35); a1.imshow(ls.shade(H, cmap=plt.cm.terrain, vert_exag=4, blend_mode='soft', dx=C, dy=C), origin='lower', extent=ext)
a1.contour(XX, ZZ, np.where(gm, H, np.nan), levels=np.arange(hg.min(), hg.max(), 0.02), colors='k', linewidths=0.3)
a1.contour(XX, ZZ, H, levels=np.arange(H.min(), H.max(), 0.1), colors='w', linewidths=0.25); a1.contour(XX, ZZ, gm, levels=[0.5], colors='r', linewidths=1)
for p in g['Holes'][hole - 1]['Pins']: a1.plot(p['Position']['x'], p['Position']['z'], 'wo', mec='k', ms=5)
c0 = np.array([gc['x'], gc['z']]); L = 35
for d, col, nm in ((ax_, 'b', 'A along play'), (cr, 'm', 'B across')): a1.plot([c0[0] - d[0] * L, c0[0] + d[0] * L], [c0[1] - d[1] * L, c0[1] + d[1] * L], col + '--', lw=1); a1.text(c0[0] + d[0] * L, c0[1] + d[1] * L, nm, color=col)
a1.set_title('hole %d: game surface (2018 LiDAR terrain), 2 cm contours on the green, 10 cm around; red = green edge' % hole, fontsize=9); a1.set_aspect('equal')
a2 = fig.add_subplot(2, 3, 2); im = a2.imshow(np.where(slope < 40, slope, 40), origin='lower', extent=ext, cmap='RdYlGn_r', vmin=0, vmax=15); a2.contour(XX, ZZ, gm, levels=[0.5], colors='k', linewidths=1); plt.colorbar(im, ax=a2, fraction=0.04, label='slope % (2 m)'); a2.set_aspect('equal')
a2.set_title('slope: green %.0f m2, relief %.2f m, mean %.1f %%, p90 %.1f %%' % (area, relief, slope[gm].mean(), np.percentile(slope[gm], 90)), fontsize=9)
# cross-sections
for i, (d, nm) in enumerate(((ax_, 'A: along play (tee -> back)'), (cr, 'B: across (left -> right looking from the tee)'))):
    a = fig.add_subplot(2, 3, 4 + i); t = np.arange(-L, L, 0.1); px = c0[0] + d[0] * t; pz = c0[1] + d[1] * t; h = lidar(px, pz)
    ins = ndimage.map_coordinates(gm.astype(float), [(pz - z0) / C, (px - x0) / C], order=0) > 0.5
    a.plot(t, h, 'k-', lw=1, label='terrain (LiDAR 2018)'); a.plot(t[ins], h[ins], 'g-', lw=3, label='on the green')
    near = np.abs((V[:, 0] - c0[0]) * (-d[1]) + (V[:, 2] - c0[1]) * d[0]) < 0.4; tv = (V[near, 0] - c0[0]) * d[0] + (V[near, 2] - c0[1]) * d[1]
    a.plot(tv, V[near, 1], 'r.', ms=3, label='green mesh vertices'); a.set_aspect(10); a.grid(alpha=0.3); a.set_xlabel('m from the green centre (vertical x10)'); a.set_title(nm, fontsize=9); a.legend(fontsize=7)
    s_ = np.gradient(h, 0.1) * 100; e = np.nonzero(ins)[0]
    if len(e): a.text(0.01, 0.02, 'slope at the near edge %.1f %%, far edge %.1f %%, max on green %.1f %%' % (s_[e[0]], s_[e[-1]], np.abs(s_[ins]).max()), transform=a.transAxes, fontsize=8)
a6 = fig.add_subplot(2, 3, 3); a6.axis('off')
a6.text(0, 0.95, 'Hole %d green: %s\narea %.0f m2, relief %.2f m\nslope on green mean %.1f %%, p90 %.1f %%, max %.1f %%\nmesh vs terrain: median %.3f m (mesh above)\n\nThe LiDAR is a ~1 m grid (2018): breaks shorter than ~2 m\nand the sharp roll-offs at the edges are smoothed.' %
        (hole, k, area, relief, slope[gm].mean(), np.percentile(slope[gm], 90), slope[gm].max(), np.median(V[:, 1] - lidar(V[:, 0], V[:, 2]))), va='top', fontsize=10, family='monospace')
plt.tight_layout(); plt.savefig(OUT + 'h%d_profile_now.jpg' % hole, dpi=60); print(k, round(area), round(relief, 2), round(float(slope[gm].mean()), 1))
