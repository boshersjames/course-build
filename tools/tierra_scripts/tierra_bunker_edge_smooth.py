# Re-mesh the hole-10 scan bunkers: jagged sand outline -> smooth sand curve + grass ring that stitches exactly to the existing grass edge.
import json, math, sys
import numpy as np, triangle
from scipy import ndimage
from skimage import measure
from matplotlib.path import Path as MP
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
D = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/scan_apply/'
FIG = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/visit_2026-10-29/scan_apply/'
R_OPEN = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0   # spikes narrower than ~2R get trimmed back to grass
S = json.load(open(D + 'sand_mesh.json')); V = np.array(S['v']); T = np.array(S['t']).reshape(-1, 3)
_, first, inv = np.unique(np.round(V[:, [0, 2]] / 0.002).astype(np.int64), axis=0, return_index=True, return_inverse=True); V = V[first]; T = inv.ravel()[T]; T = T[(T[:, 0] != T[:, 1]) & (T[:, 1] != T[:, 2]) & (T[:, 0] != T[:, 2])]
# heights: the current (scan-conformed) sand surface, barycentric
from scipy.spatial import Delaunay
def surf_height(P):
    out = np.full(len(P), np.nan); A = V[T][:, :, [0, 2]]
    for k, (a, b, c) in enumerate(A):
        lo = np.minimum(np.minimum(a, b), c) - 1e-6; hi = np.maximum(np.maximum(a, b), c) + 1e-6
        sel = np.nonzero(np.isnan(out) & np.all(P >= lo, 1) & np.all(P <= hi, 1))[0]
        if not len(sel): continue
        M = np.array([[b[0] - a[0], c[0] - a[0]], [b[1] - a[1], c[1] - a[1]]]); det = np.linalg.det(M)
        if abs(det) < 1e-12: continue
        uv = np.linalg.solve(M, (P[sel] - a).T).T; l1, l2 = uv[:, 0], uv[:, 1]; ok = (l1 >= -1e-6) & (l2 >= -1e-6) & (l1 + l2 <= 1 + 1e-6)
        s = sel[ok]; y = V[T[k]][:, 1]; out[s] = y[0] * (1 - l1[ok] - l2[ok]) + y[1] * l1[ok] + y[2] * l2[ok]
    return out
# boundary loops
E = {}
for t in T:
    for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])): E[(a, b)] = E.get((a, b), 0) + 1
bd = [(a, b) for (a, b) in E if (b, a) not in E]; nxt = {}
for a, b in bd: nxt.setdefault(a, []).append(b)
loops = []; used = set()
for a, b in bd:
    if (a, b) in used: continue
    L = [a]; used.add((a, b)); cur = b
    while cur != a:
        L.append(cur); nb = [q for q in nxt[cur] if (cur, q) not in used][0]; used.add((cur, nb)); cur = nb
    loops.append(L)
def area(P): return 0.5 * np.sum(P[:, 0] * np.roll(P[:, 1], -1) - np.roll(P[:, 0], -1) * P[:, 1])
print('loops', sorted([(len(L), round(float(area(V[L][:, [0, 2]])), 1)) for L in loops], key=lambda q: -abs(q[1]))[:8], len(loops))
outers = [L for L in loops if abs(area(V[L][:, [0, 2]])) > 5]
res = dict(sand=dict(v=[], t=[]), ring=dict(v=[], t=[])); C = 0.05; stats = {}
fig, axs = plt.subplots(1, len(outers), figsize=(9 * len(outers), 9))
for ax, L in zip(np.atleast_1d(axs), outers):
    P = V[L][:, [0, 2]]; x0, z0 = P.min(0) - 1; W, H = ((P.max(0) + 1 - (x0, z0)) / C).astype(int)
    jj, ii = np.mgrid[0:H, 0:W]; cen = np.c_[x0 + (ii.ravel() + 0.5) * C, z0 + (jj.ravel() + 0.5) * C]
    mask = MP(P).contains_points(cen).reshape(H, W)
    r = int(R_OPEN / C); yy, xx = np.mgrid[-r:r + 1, -r:r + 1]; disk = xx ** 2 + yy ** 2 <= r * r
    op = ndimage.binary_opening(mask, structure=disk)
    sm = ndimage.gaussian_filter(op.astype(float), 0.4 / C)
    cs = [c for c in measure.find_contours(sm, 0.5) if len(c) > 40]
    dt = (ndimage.distance_transform_edt(mask) - ndimage.distance_transform_edt(~mask)) * C; gzd, gxd = np.gradient(ndimage.gaussian_filter(dt, 2))
    def inside_fix(Q, margin=0.12):
        def lap(Q): return (np.roll(Q, 1, 0) + np.roll(Q, -1, 0)) / 2 - Q
        gz1, gx1 = np.gradient(dt)
        for outer in range(6):
            for it in range(15): Q = Q + 0.5 * lap(Q); Q = Q - 0.53 * lap(Q)   # Taubin: smooths without shrinking
            for it in range(30):
                ci = np.c_[(Q[:, 1] - z0) / C - 0.5, (Q[:, 0] - x0) / C - 0.5].T; d = ndimage.map_coordinates(dt, ci, order=1); bad = d < margin
                if not bad.any(): break
                gx = ndimage.map_coordinates(gx1, ci, order=1); gz = ndimage.map_coordinates(gz1, ci, order=1); g = np.hypot(gx, gz) + 1e-9
                Q[bad] += (np.c_[gx / g, gz / g] * np.maximum(margin + 0.02 - d, 0.01)[:, None])[bad]
        return Q, d.min()
    pts_all = []; segs = []; regions = []
    base = len(P); verts = [tuple(p) for p in P]; segs += [(i, (i + 1) % base) for i in range(base)]
    for c in cs:
        Q = np.c_[x0 + (c[:, 1] + 0.5) * C, z0 + (c[:, 0] + 0.5) * C]
        # resample at ~0.35 m
        d = np.r_[0, np.cumsum(np.hypot(*np.diff(Q, axis=0).T))]; n = max(12, int(d[-1] / 0.35)); s = np.linspace(0, d[-1], n, endpoint=False)
        Q = np.c_[np.interp(s, d, Q[:, 0]), np.interp(s, d, Q[:, 1])]; Q, dmin = inside_fix(Q); print('curve min distance inside the old edge %.2f m' % dmin)
        assert MP(P).contains_points(Q).all()
        k0 = len(verts); verts += [tuple(q) for q in Q]; segs += [(k0 + i, k0 + (i + 1) % len(Q)) for i in range(len(Q))]
        inner = np.argwhere(sm > 0.9); jc, ic = inner[len(inner) // 2]
        # a seed inside this curve
        cand = np.c_[x0 + (inner[:, 1] + 0.5) * C, z0 + (inner[:, 0] + 0.5) * C]; ins = cand[MP(Q).contains_points(cand)]
        regions.append([ins[len(ins) // 2][0], ins[len(ins) // 2][1], 1, 0.3])
        ax.plot(np.r_[Q[:, 0], Q[0, 0]], np.r_[Q[:, 1], Q[0, 1]], 'orange', lw=2)
        gx_, gz_ = np.meshgrid(np.arange(Q[:, 0].min(), Q[:, 0].max(), 0.5), np.arange(Q[:, 1].min(), Q[:, 1].max(), 0.5)); G = np.c_[gx_.ravel(), gz_.ravel()]
        G = G[MP(Q).contains_points(G)]; dq = np.min(np.hypot(G[:, None, 0] - Q[None, :, 0], G[:, None, 1] - Q[None, :, 1]), 1); verts += [tuple(g) for g in G[dq > 0.3]]
    tri = triangle.triangulate(dict(vertices=np.array(verts), segments=np.array(segs), regions=np.array(regions)), 'pq28YYA')
    tv = tri['vertices']; tt = tri['triangles']; lab = tri['triangle_attributes'][:, 0].astype(int)
    assert np.allclose(tv[:base], P)   # the jagged edge vertices are kept exactly (no gaps to the grass)
    y = surf_height(tv); y[:base] = V[L][:, 1]
    if np.isnan(y).any(): print('nan heights', np.isnan(y).sum()); y[np.isnan(y)] = np.nanmean(y)
    # probe per ring triangle: 0.3 m outside the nearest old-edge segment (which mesh borders it there decides its material)
    A_ = P; B_ = np.roll(P, -1, 0); probes = []
    for t in tt[lab == 0]:
        c = tv[t].mean(0); ab = B_ - A_; u = np.clip(np.einsum('ij,ij->i', c - A_, ab) / np.einsum('ij,ij->i', ab, ab), 0, 1); F = A_ + ab * u[:, None]; k = np.argmin(np.hypot(*(F - c).T))
        nrm = np.array([ab[k, 1], -ab[k, 0]]) / np.hypot(*ab[k]); nrm = nrm if area(P) > 0 else -nrm; q = F[k] + 0.3 * nrm
        if MP(P).contains_point(q): q = F[k] - 0.3 * nrm
        probes.append([float(q[0]), float(q[1])])
    res.setdefault('ring_probe', []).extend(probes)
    for part, want in (('sand', 1), ('ring', 0)):
        sel = tt[lab == want]; u = np.unique(sel); remap = -np.ones(len(tv), int); o = len(res[part]['v']); remap[u] = np.arange(len(u)) + o
        res[part]['v'] += [[float(tv[i, 0]), float(tv[i, 1]), float(y[i])] for i in u]; res[part]['t'] += remap[sel].ravel().tolist()
    sa = sum(abs(area(tv[t][:, :])) for t in tt[lab == 1]); ra = sum(abs(area(tv[t])) for t in tt[lab == 0])
    key = 'b24' if P[:, 1].mean() < 1110 else 'b26'; stats[key] = dict(old_sand_m2=round(abs(area(P)), 1), new_sand_m2=round(sa, 1), ring_m2=round(ra, 1), edge_vertices=base, curves=len(cs))
    ax.triplot(tv[:, 0], tv[:, 1], tt[lab == 1], color='tan', lw=0.3); ax.triplot(tv[:, 0], tv[:, 1], tt[lab == 0], color='green', lw=0.3)
    ax.plot(np.r_[P[:, 0], P[0, 0]], np.r_[P[:, 1], P[0, 1]], 'k', lw=1); ax.set_aspect('equal'); ax.set_title('%s: old jagged edge (black), new smooth sand edge (orange)' % key)
plt.tight_layout(); plt.savefig(FIG + 'bunker_edge_remesh_plan.jpg', dpi=70); plt.close()
json.dump(res, open(D + 'smooth_meshes.json', 'w')); print(stats); print({k: (len(v['v']), len(v['t']) // 3) for k, v in res.items() if k != 'ring_probe'})
