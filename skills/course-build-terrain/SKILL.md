---
name: "course-build-terrain"
description: "Use when building a GSPro course heightmap from LiDAR, judging what the public LiDAR can and cannot tell you, or applying phone scans to greens and bunkers."
---

Rules file: `rules/terrain.json` (every number below is defined there; read it with `tools/rules.py terrain`). Course overrides: `course.json` → `rule_overrides.terrain`.

# Terrain, LiDAR and scans

Use with `course-build-pipeline`, `course-build-lidar-review` (D13-D16 belong here) and `course-build-backdrop`. Full guide: `course-build/playbooks/01_terrain_lidar_and_scans.md` in the moorpark repo.

## Defaults that worked

- **Frame**: square, 2000 m (Tierra) or 3000 m (Moorpark); SW corner in EPSG:6340 in `course.json`. Unity x = m east, z = m north, y = elevation − base.
- **Heightmap**: 2049×2049 16-bit LE raw; Terrain size = square, height = relief (~1 m/sample at 2000 m).
- **Source**: USGS 3DEP EPT (`coursekit.py lidar --ept <survey>`); check WESM footprints, not bounding boxes. QL2 2018: ~4 pts/m², good for terrain, coarse for greens.
- **Terrain under meshes**: 0.25 m (Tierra) to 0.4 m (Moorpark) below course meshes; `LowerTerrain` when GSP's tool does nothing.
- **Relief**: keep 95-100% of LiDAR undulation (15 m running mean removed); no spline flattening, no blur. Tierra fairways RMS 1.3-3.1 cm vs LiDAR.
- **Relief trial (flat courses)**: ×1.3-1.5 rolls, ±1.2 m cap, pins ≤3%; BoJa's call each time, off by default.
- **Green scans**: 3D Scanner app LAS aligned to the course LiDAR, applied at |course − scan| median 1 mm, p90 6 mm.
- **Scan trust**: a clean phone LiDAR scan is followed 100% (outlines, depths, banks); photo-only COLMAP never on putting surfaces.
- **Ground-layer edges**: never cut a layer alpha on the 1 m terrain grid (stair steps); feather it with a distance transform, smootherstep over 3 m, zero within 1.5 m of mown meshes.

## What the public LiDAR can and cannot tell you

Can: overall tilt, tiers, ridges, false fronts, fairway roll, bank heights, cliff extents (>35° cells), tree heights and crowns, roof heights, the real tee shelf.

Cannot: anything under ~1 m wide or ~5 cm tall (collar steps, mow lines, bunker lips, edge rolloff; slopes overstated near edges); grass height (0.3 m deep rough is classed as ground; deep rough comes from the satellite); shrub vs small tree vs rock vs pump station; which of two greens is in play; whether a flat pan is a bunker or a tee pad; whether a pale patch is a wash, road, lot or entrance; anything built since the survey year; the surface detail of a green on a hill (needs a scan); pond and lake beds (no returns under water: the bed reads flat at the water level, so shelves and beds are dug by rule).

## Procedure

1. Fetch and clip; `points.npz`, `heightmap_2049.raw`, hillshade; check elevation range against the card altitude.
2. Green maps and 0.5 m surfaces (`green_maps.py`, `surfaces_05m.py`): per-green tilt, p90 slope, `outline_over_8pct` (high = outline includes banks; check imagery).
3. Tee-to-green profiles per hole (`approach_profiles.py`) so landing-zone roll is judged before Unity.
4. Unity: OPCD conform, Lower Terrain Under Meshes, fairway undulation check (1 m grid vs heightmap per landing zone; RMS, median, `undul_kept_pct`).
5. Scans: register to the course LiDAR (ICP on turf), hold scale within ±15% of the phone-depth scale (the fit once collapsed toward the smooth LiDAR), apply only clean regions (≤5 cm residual, no doubled areas), lower the terrain under conformed meshes, re-run the vertex-paint sweep.
6. Keep every rebuild's report: vertices moved, largest move, "0 with nothing under".

## Checks

- Fairway cross-section vs LiDAR before review (never flat sheets).
- Terrain height at each photo pose within 2 cm of the LiDAR; course-file pins within 5 cm of the heightmap.
- No craters, sunken pads or steps where the real ground is a mound.
- Outer terrain: no cliff at the square edge, no flat farmland where photos show hills; far peaks really visible from course height.

## Pitfalls

- A 3000 m satellite constant in a 2000 m course stretched the photo 1.5×.
- The first cliff drape used flipped mask rows; detail grass was cleared on the wrong cells.
- Lower Terrain Under Meshes flattened a LiDAR mound beside a bunker; check it survives.
- Photo-only reconstruction of greens: 110 m² of patches at 13 cm median; 106 of 430 frames registered. Don't repeat. Polycam raw exports have no poses.
- A green more than 1.5 m off its LiDAR pad moves the pins: stop and send a position sheet before moving anything.
- The repo heightmap is the LiDAR, not the edited Unity terrain: plans computed from it are reference profiles plus a rule; recompute deltas on the live mesh.

## Decision points (ask once, with a default)

QL1 data settles D4, D5, D8, D12, D17; QL2 courses keep those as questions. Never in any scan: D1, D3, D6, D9, D14, D18, plus a phone LAS per green (D7).

1. Which greens get a phone scan on the next visit (default: all 18 for a flagship).
2. Relief exaggeration on flat courses (default: off until shown before/after).
3. A pale patch that could be a wash, road, lot or tee: ask with a crop before building.
