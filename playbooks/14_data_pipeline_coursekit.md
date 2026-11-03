# 14. Data pipeline (coursekit) before Unity

Use this to stand up a new course's data folder: frame, OSM, LiDAR, satellite, heightmap, vegetation masks, course SVG, scorecard and GKD, with the checks that stop a bad draft reaching the PC.

## Defaults that worked

| Step | Command / file | Notes |
|---|---|---|
| Frame | `course.json`: square size, centre lat/lon, SW corner EPSG:6340, base elevation, `gkd` block (city, altitude ft, wind, keyword) | 2000 m fits an 18; 3000 m for 27 holes or a shared Griffith frame |
| OSM | Overpass export → `map.osm`; Overture parquet for land use/parking/roads/buildings; `course_features.geojson` for tees/greens/fairways/bunkers | OSM has no course boundary polygon: bbox comes from the holes |
| LiDAR | `coursekit.py lidar --ept <USGS survey>`; check WESM footprints; rockyweb is slow (60 KB/s) | `points.npz` not in git |
| Satellite | NAIP 4096 px (`get_satellite.py`); 0.49 m/px at 2000 m, 0.73 at 3000 m | |
| Heightmap | `heightmap_2049.raw` 16-bit LE; plain git, not LFS (cloud can't push LFS) | Hansen/Angeles lesson |
| Vegetation | `coursekit.py vegetation` (`--wash`, `--parkland`) → four `terrain_masks/*.mask`, `shrubs_clean.csv`, `shrubs_removed.csv`, `trees.csv`, previews on the satellite | 07, 06 |
| SVG | `coursekit.py svg --collar --collar-back --deep-off-green [--smooth 7 --min-green 0.36 --fw-corridor 45]` → `course.svg`, `bunkers.csv`; `svg_check.py` | 02, 03, 04 |
| Tees / card | `scorecard.csv`, `tees.csv`, `tee_pins.csv`; `coursekit.py card [--loops] --ob --line-tees [--habitat-penalty --drop-holes]` → `<Course>.GKD` | 05, 10 |
| Fixes | `apply_checklist.py`, `fix_tees.py`, `fix_box_tees.py`, `teepads.py`, `clear_pads.py`, `pads_off_fairway_edge.py`, `surrounds.py` (set `S` from course.json), `clear_bunker_crowns.py`, `tree_species.py`, `bunker_plan.py`, `approach_profiles.py`, `surfaces_05m.py`, `backdrop.py`, `offsite_trees.py` | copies live per course; move the good ones into core |
| Previews | `ob_preview.py`, `*_on_satellite.png`, `course_svg_preview.png`, profiles per hole | always sent with the draft |
| README per course | Frame, LiDAR, holes, vegetation, GKD, files, "to check" list | every course has one |

## Procedure

1. Frame and fetch (OSM, LiDAR, satellite); hillshade and a quick elevation sanity check against the card altitude.
2. Holes: hole lines, pars from OSM vs the card; loops for 27-hole courses; map OSM greens to holes (G-numbers north to south, matched by routing and distance; practice greens left out).
3. Vegetation masks and shrub/tree CSVs; overlay checks on the satellite.
4. SVG with collars, deep rough offsets, bunker plan, smoothed fairways; `svg_check.py` clean.
5. Card → GKD with OB draft; checklist fixes; previews; README "to check" list; commit.
6. Hand to the PC: own Unity project per course (`C:\GSP\<Name>_Project`), own scene, tools with no foreign constants, batch build, then the Unity checklist passes (00).

## Checks before anything goes to the PC

- `svg_check.py` clean (no degenerate subpaths, no pad on a fairway edge, no pad merged with a box).
- Every GKD tee/pin/aim/DZ on its surface and in bounds; hazard count and pOOB under the Greenkeeper limits.
- Satellite size constant equals the course square; no "3000"/"Moorpark" left in copied tools.
- Trees vs satellite overlay; shrubs removed from roads/paths/yards/play surfaces with counts.
- README lists every open question (long OSM hole lines, single-box holes, unmapped forward tees, two greens on a hole).

## Pitfalls

- OSM hole lines starting at practice/back tees run long vs the card (Moorpark Ridgeline 1: 474 vs 380).
- LFS pushes fail from the cloud; keep heightmaps/masks as plain files.
- `surrounds.py` hard-coded `S = 2000`.
- CLIDAR lists surveys with no points on the course; verify with an EPT query.
- Greenkeeper's load dialog only lists `.gspcrse`; it reads the same-named GKD beside it; save from Greenkeeper to write coursedetails and scorecards.

## Decision points for BoJa

1. Frame size and centre when the course edge is near the square edge (backdrop needs).
2. Which 9-hole loops pair into 18s (27-hole courses), and nines as standalone courses.
3. Which OSM-mapped features are stale (greens rebuilt, bunkers removed) before tracing.

## Sources

`coursekit.py` and `README.md` in this repo; `tierra-rejada-lidar/README.md`, `fetch_lidar.py`, `find_course_bbox.py`; Hansen (`claude/project-thread-f78r3v`), Harding/Wilson (`claude/project-thread-a6haj8`, `-flaaah`), Penmar (`-j2x22i`), Angeles (`-47fcq5`) READMEs and `scripts/`; checklist "Data-side helpers".
