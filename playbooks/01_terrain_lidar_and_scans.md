# 01. Terrain, LiDAR and scans

Use this when building the heightmap, deciding what the public LiDAR can and cannot tell you, and applying phone scans (3D Scanner LAS, Polycam) to greens and bunkers. For the list of LiDAR misreads turned into decision points, see the **LiDAR detail review** thread; this guide keeps the build-side rules.

## Defaults that worked

| Setting | Value | Notes |
|---|---|---|
| Frame | Square, 2000 m (Tierra) or 3000 m (Moorpark); SW corner in EPSG:6340 (UTM 11N) stored in `course.json` | Unity x = m east, z = m north, y = elevation − base |
| Heightmap | 2049×2049 16-bit LE raw; Unity Terrain size = square, height = relief | ~1 m/sample at 2000 m, 1.5 m at 3000 m |
| Source LiDAR | USGS 3DEP EPT (`coursekit.py lidar --ept <survey>`); check WESM footprints, not bounding boxes | CLIDAR lists surveys that have 0 points on the course |
| Density | QL2 (2018 SoCal B3): ~4 pts/m², ~3 ground pts/m² | Good for terrain, coarse for greens |
| Terrain under meshes | 0.25 m (Tierra), 0.4 m (Moorpark) below course meshes | `MoorparkMeshes.LowerTerrain` when GSP's tool does nothing |
| Relief | Keep 95-100% of LiDAR undulation (15 m running mean removed); no spline flattening, no blur | Tierra fairways: RMS 1.3-3.1 cm vs LiDAR |
| Relief trial (flat courses) | ×1.3-1.5 rolls, ±1.2 m cap, pins ≤3% | Hansen 6/7/16, Tierra 9 green crest; pending BoJa each time |
| Backdrop | Extend 1-2 km of hills past the square (edge heights carried out, or 3DEP 1" DEM to 36-140 km) + fog | "No abrupt map edge on follow-cam" |
| Green scans | 3D Scanner app LAS, aligned to the course LiDAR; applied at |course − scan| median 1 mm, p90 6 mm | Tierra 12-15 |
| Scan trust | Phone LiDAR scan clean → follow it 100% (outlines, depths, banks); photo-only COLMAP → never on putting surfaces | BoJa 10/29; 17/18 photo stereo failed |

## What the public LiDAR can and cannot tell you

**LiDAR detail review:** the misreads below are turned into 18 decision points (D1-D18) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md). Registration (D13-D14), outer ground (D15) and mesh order (D16) belong to this guide; the rest are linked from their element guides.

Can: overall tilt, tiers, ridges, false fronts, fairway roll, bank heights, cliff extents (>35° cells), tree heights and crown radii, building roof heights, the real tee shelf.

Cannot (and where it misled us):
- Anything under ~1 m wide or ~5 cm tall: collar steps, mow lines, bunker lips, green edge rolloff. Smoothing across banks also overstates slope near edges.
- Grass height: 0.3 m deep rough is classed as ground. Deep rough comes from the satellite (tan) with LiDAR masks, not from the LiDAR.
- Shrubs vs small trees vs rocks: 2,700 "trees" of 3 m+ on Tierra were shrubs; 256 Moorpark "trees" were pylons and wires; a 2 m "shrub" on 13 was the pump station.
- Which of two greens is in play, whether a flat pan is a bunker or a tee pad (Tierra 4 "crater" was a sunk unused tee pad), whether a pale patch is a wash, a road, a parking lot or an entrance drive (Tierra 15).
- Sand edges: the LiDAR gives the hollow; the outline comes from the satellite and the photos.
- Anything built or rebuilt since the survey year (2018): the 12 orchard, the clubhouse construction.
- Where a hole's whole green sits on a hill (Tierra 17), the LiDAR has the slope but not the surface detail BoJa cares about; it needs a scan.

## Procedure

1. Fetch and clip (`coursekit.py lidar`), build `points.npz`, `heightmap_2049.raw`, hillshade; check elevation range against the card's altitude.
2. Build green maps and 0.5 m surfaces (`green_maps.py`, `surfaces_05m.py`) for review: per-green tilt, p90 slope, `outline_over_8pct` (outline includes banks → check against imagery).
3. Tee-to-green profiles per hole (LiDAR vs heightmap vs 0.5 m surface) so landing-zone roll can be judged before Unity (`approach_profiles.py`).
4. In Unity: OPCD conform, Lower Terrain Under Meshes, then the fairway undulation check (sample meshes on a 1 m grid vs the heightmap in each landing zone; report RMS, median, `undul_kept_pct`).
5. Scans: register to the course LiDAR (ICP on turf), hold the scale within ±15% of the phone-depth scale (the fit collapsed toward the smooth LiDAR once), apply only clean regions (≤5 cm residual, no doubled areas), lower the terrain under conformed meshes, re-run the vertex-paint sweep.
6. Keep every mesh rebuild's report: vertices moved, largest move, "0 with nothing under".

## Checks

- Fairway cross-section vs LiDAR before review (not flat sheets; Hansen's first pass "very much a sim").
- Terrain height at each photo pose matches the LiDAR within 2 cm (convention check for the pose renders).
- No craters, sunken pads or steps where the real ground is a mound.
- Outer terrain: no cliff at the square edge, no flat farmland where the photos show hills; far peaks really visible from course height (Hansen: the 2,700 m peaks sit behind the front range, so "real heights" showed nothing).

## Pitfalls

- A 3000 m satellite constant copied into a 2000 m course stretched the photo 1.5× (Penmar/Harding).
- The first cliff drape (Tierra 11/18) used flipped mask rows; the detail grass under it was cleared on the wrong cells.
- `Lower Terrain Under Meshes` can flatten a LiDAR mound next to a bunker (Tierra 18 greenside); check the mound survives.
- Photo-only reconstruction of greens: 17 got 110 m² of patches at 13 cm median / 75 cm p90; 18 registered 106 of 430 frames. Don't repeat.
- Polycam "raw" exports have no camera poses; COLMAP sequential matching registered 34 of 291 frames. Use the 3D Scanner LAS route instead.
- A wrong green position moves the pins: when a LiDAR fit says a green is more than 1.5 m off its pad (Tierra 14 ~5 m, 18 ~3 m), stop and send a position sheet before moving anything.

## Decision points for BoJa

**What a newer scan settles vs what needs an in-person look** (LiDAR review section 6): QL1 data (Angeles National, 2023-24, 38-45 pts/m²) settles sand edges (D4), bunker depth and faces (D5), green pad offsets (D8), creek channels (D12) and tee decks (D17). Tierra, Hansen Dam and Moorpark are on the 2018 QL2 survey, where those stay questions. Never in any scan, so always walked and photographed before building: cover colour and species (D1, D3), bunker islands and noses (D6), collar width and cut step (D9), props under 1 m plus doors, signs and bins (D14), tree species near tees and greens (D18). A phone LAS is needed per green (D7).

1. Which greens get a phone scan on the next visit (all 18 is the default for a flagship; at least the signature and hillside greens).
2. Relief exaggeration on flat courses: on (×1.3-1.5) or off? Default: off until shown before/after.
3. When a pale LiDAR/satellite patch could be a wash, a road, a lot or a tee: ask with a crop before building (the list lives in the LiDAR detail review thread).

## Sources

`tierra-rejada-lidar/README.md`, `greens/README.md`, `holes/README.md`, `HANDOFF.md`; `progress/tierra/bunkers_fairways_1029/README_fairways.md`; `scan_apply_1029b_report.txt`; `MORNING_1030.md`; Hansen `scripts/surfaces_05m.py`, `approach_profiles.py`, `backdrop.py`; checklist "Terrain and backdrop", "Shape and realism".
