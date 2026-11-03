---
name: "course-build-pipeline"
description: "Use when standing up a new GSPro course's data before Unity: frame, OSM, LiDAR, satellite, heightmap, vegetation masks, course SVG, scorecard and GKD with coursekit."
---

Rules file: `rules/pipeline.json` (every number below is defined there; read it with `tools/rules.py pipeline`). Course overrides: `course.json` → `rule_overrides.pipeline`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/new_course.py <Name> --square 2000 --unity "C:/GSP/<Name>_Project" [--holes 18]`: the course folder, `course.json` (toolkit keys), `CLAUDE.md`, automation config, editor set copied into the Unity project.
- `python ../course-build/tools/doctor.py [--fix-lfs]`: the folder is complete before anything is fetched or built.
- `python coursekit.py new|lidar|satellite|heightmap|osm|holes|trees|brush|vegetation|svg|card` (from the course folder; copy in `tools/coursekit.py`): the data pipeline; flags per step in `pipeline.svg_flags`, `pipeline.card_flags`. `course.json` must carry both coursekit's keys (`size`, `sw_e`, `sw_n`, `y_offset`, `epsg`, `loops`) and the toolkit's (`frame`, `paths`).
- `python ../course-build/tools/gkd_tools.py check`: the drafted GKD against the limits before it reaches the PC.
- `python ../course-build/tools/green_report.py --smooth 0.6` and `python ../course-build/tools/flyover_gate.py --out progress/flyover_gate --end-fit`: the first headless pass on the data alone.
- (no tool yet: `svg_check.py`, `apply_checklist.py`, `surrounds.py` and the tee fixers are still per-course copies with constants; `pipeline.grep_before_run` lists what to grep for; candidates for `tools/`)

# Data pipeline (coursekit) before Unity

Use with `course-build-lidar-review` (run it right after the fetch) and `course-build-ob`. Full guide: `course-build/playbooks/14_data_pipeline_coursekit.md` in the moorpark repo; tool: `coursekit.py` and the per-course `scripts/`.

## Defaults that worked

- **Frame**: `course.json` with square size, centre lat/lon, SW corner in EPSG:6340, base elevation, `gkd` block (city, altitude ft, wind, keyword). 2000 m fits an 18; 3000 m for 27 holes or a shared frame.
- **OSM**: Overpass export → `map.osm`; Overture parquet for land use, parking, roads, buildings; `course_features.geojson` for tees, greens, fairways, bunkers. OSM has no course boundary; the bbox comes from the holes.
- **LiDAR**: `coursekit.py lidar --ept <USGS survey>`; check WESM footprints; verify the survey has points on the course with an EPT query (CLIDAR lists empty ones); `points.npz` not in git.
- **Satellite**: NAIP 4096 px (`get_satellite.py`); 0.49 m/px at 2000 m, 0.73 at 3000 m.
- **Heightmap**: `heightmap_2049.raw` 16-bit LE, plain git not LFS (cloud can't push LFS).
- **Vegetation**: `coursekit.py vegetation` (`--wash`, `--parkland`) → four `terrain_masks/*.mask`, `shrubs_clean.csv`, `shrubs_removed.csv`, `trees.csv`, previews on the satellite.
- **SVG**: `coursekit.py svg --collar --collar-back --deep-off-green [--smooth 7 --min-green 0.36 --fw-corridor 45]` → `course.svg`, `bunkers.csv`; `svg_check.py`.
- **Tees / card**: `scorecard.csv`, `tees.csv`, `tee_pins.csv`; `coursekit.py card [--loops] --ob --line-tees [--habitat-penalty --drop-holes]` → `<Course>.GKD`.
- **Fixes**: `apply_checklist.py`, `fix_tees.py`, `fix_box_tees.py`, `teepads.py`, `clear_pads.py`, `pads_off_fairway_edge.py`, `surrounds.py` (set `S` from course.json), `clear_bunker_crowns.py`, `tree_species.py`, `bunker_plan.py`, `approach_profiles.py`, `surfaces_05m.py`, `backdrop.py`, `offsite_trees.py`. Copies live per course; move the good ones into core.
- **Previews**: `ob_preview.py`, `*_on_satellite.png`, `course_svg_preview.png`, profiles per hole; always sent with the draft.
- **README per course**: frame, survey line, holes, vegetation, GKD, files, `visit: planned | none`, "to check" list.

## Procedure

1. Frame and fetch (OSM, LiDAR to 1-2 km for the backdrop, satellite); hillshade and an elevation sanity check against the card altitude.
2. Holes: hole lines, pars from OSM vs the card; loops for 27 holes; map OSM greens to holes (numbered north to south, matched by routing and distance; practice greens left out).
3. LiDAR review D1-D18, registration shifts recorded (D13).
4. Vegetation masks and shrub/tree CSVs; overlay checks on the satellite.
5. SVG with collars, deep-rough offsets, bunker plan, smoothed fairways; `svg_check.py` clean.
6. Card → GKD with OB draft; checklist fixes; previews; README "to check" list; commit.
7. Hand to the PC: own Unity project per course (`C:\GSP\<Name>_Project`), own scene, tools with no foreign constants, batch build, then the Unity checklist passes.

## Checks before anything goes to the PC

- `svg_check.py` clean (no degenerate subpaths, no pad on a fairway edge, no pad merged with a box).
- Every GKD tee/pin/aim/DZ on its surface and in bounds; hazard count ≤120, pOOB ≤~880 points.
- Satellite size constant equals the course square; no "3000" or another course's name left in copied tools (grep first).
- Trees vs satellite overlay; shrubs removed from roads/paths/yards/play surfaces with counts.
- README lists every open question (long OSM hole lines, single-box holes, unmapped forward tees, two greens on a hole).

## Pitfalls

- OSM hole lines starting at practice or back tees run long vs the card (Moorpark 1: 474 vs 380).
- LFS pushes fail from the cloud; heightmaps and masks as plain files.
- `surrounds.py` had `S = 2000` hard-coded; a copied 3000 stretched the satellite 1.5× on Penmar and Harding.
- Greenkeeper's load dialog lists only `.gspcrse`; it reads the same-named GKD beside it; save from Greenkeeper to write coursedetails and scorecards.

## Decision points (ask once, with a default)

1. Frame size and centre when the course edge is near the square edge.
2. Which 9-hole loops pair into 18s (27-hole courses), and nines as standalone courses.
3. Which OSM-mapped features are stale (greens rebuilt, bunkers removed) before tracing.
4. `visit: planned | none` for the course.
