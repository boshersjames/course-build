import numpy as np, matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
A = 'C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/fairways/'; OUT = 'C:/Users/user/Documents/newcourses/tr-automation/progress/tierra/bunkers_fairways_1029/'
heat = np.load(A + 'heat_before.npy', allow_pickle=True)[0]
fig, axs = plt.subplots(4, 4, figsize=(24, 24)); axs = axs.ravel()
for ax, (hn, hd) in zip(axs, sorted(heat.items())):
    D = hd['diff']; H, W = D.shape; ext = [hd['x0'], hd['x0'] + W, hd['z0'], hd['z0'] + H]
    im = ax.imshow(D - np.nanmedian(D), origin='lower', extent=ext, cmap='RdBu_r', vmin=-0.1, vmax=0.1)
    ax.contour(np.arange(W) + hd['x0'], np.arange(H) + hd['z0'], hd['lz'].astype(float), levels=[0.5], colors='k', linewidths=1)
    ax.set_title('Hole %d fairway: course mesh - LiDAR (m), landing zone outlined; RMS %.3f m' % (hn, np.sqrt(np.nanmean((D - np.nanmedian(D)) ** 2))), fontsize=9); ax.set_aspect('equal')
for a in axs[len(heat):]: a.axis('off')
plt.colorbar(im, ax=axs[-1], fraction=0.5, label='m'); plt.tight_layout(); plt.savefig(OUT + 'fairway_mesh_vs_lidar_heat.jpg', dpi=50); plt.close()
