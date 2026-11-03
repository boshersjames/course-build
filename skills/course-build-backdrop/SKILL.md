---
name: "course-build-backdrop"
description: "Use when building what a GSPro course sees past its square: far terrain, roads, buildings, parking, rivers, off-site trees, fog and far clip. Never invent ground."
---

Rules file: `rules/backdrop.json` (every number below is defined there; read it with `tools/rules.py backdrop`). Course overrides: `course.json` → `rule_overrides.backdrop`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/flyover_gate.py --out progress/flyover_gate [--end-fit]`: every flight path and poster frame, so what the backdrop must cover is known before anything is built; rules `flyovers.render`, `backdrop.far_backdrop_km`.
- `offsite_trees.py` and `backdrop.py` (per course, from `pipeline.fixes`): LiDAR trees to `backdrop.offsite_trees_m` and the outer DEM; `buildings.py`, `building_heights.py` (`tools/moorpark_tools/`): OSM footprints with LiDAR roof heights.
- (no tool yet: skyline fit RMS against the photo from the highest tee at `backdrop.skyline_fit_rms_px_max`; `tools/pose_solve.py` (diagnostic only) computes the LiDAR skyline that this needs; candidate for `tools/`)

# Backdrop and surroundings

Use with `course-build-gates`, `course-build-paths-fences` and `course-build-pipeline`. Full guide: `course-build/playbooks/18_backdrop_and_surroundings.md` in the moorpark repo. LiDAR decision point D15.

## Defaults that worked

- **Inner terrain**: the course square from the LiDAR heightmap; untouched by the backdrop.
- **Outer plot**: a coarser DEM continuing the square's edge outward with no step (Wilson: 8 km Outer Plot), stitched to the inner edge, satellite or graded land-cover texture.
- **Far backdrop**: USGS 3DEP DEM out to the farthest ridge any flyover sees (Wilson: 140 km, Verdugos, San Gabriels, Griffith ridges) as a low-poly mesh.
- **Fog and clip**: GradientFog from the outer plot edge to the far ridge (Wilson 8-140 km, inner 1.8-6 km); far clip past the last ridge (150 km). GSPro sets its own sky.
- **Roads**: OSM/Overture roads, freeways and the entrance wherever a tee, green or flyover frame sees them; widths from OSM lanes; lane lines, shoulders, poles, hedges, walls and signs from Street View.
- **Buildings**: OSM footprints + LiDAR roof heights for everything in the flyover envelope (Wilson: 112); simple shells with real roof colours; homes beyond 300 m as blocks.
- **Parking**: OSM lots as asphalt with lines; the clubhouse lot with cart staging.
- **Rivers/channels**: built as what they are (LA River as concrete channel), not hazards; washes beyond the course as dry beds.
- **Off-site trees**: LiDAR trees to 300 m from play (Hansen: 1,959), low-detail prefabs, denser at entrances where the satellite shows it.
- **Landmarks**: far peaks and named landmarks from bearings off known tees; orchards and fields as textured ground from the satellite date.
- **Power lines**: pole lines along boundaries where photos show them on the skyline.

## Procedure

1. Day one with the LiDAR: 3DEP 1/3 arc-second (or 1 m) to 1-2 km for the outer plot and 10 m DEM to the horizon; OSM/Overture roads, buildings, parking, water, land use for the same extent.
2. Outer plot continuing the inner edge; check the seam from every edge tee at ground level.
3. Far backdrop mesh, fog and far clip; one render from the highest tee against the skyline photo; skyline fit RMS < 10 px passes.
4. Roads, parking, buildings, river as a package (built in editor C, applied in A): no vertex on mown turf, drape check, island check.
5. Off-site trees, pole lines, landmarks; left/right sweep from every flyover start for empty or invented ground.

## Checks

- Skyline from each tee matches the photo; no invented hills.
- No plane or cliff at the course edge.
- Every road or building a flyover frame sees exists; nothing cleaned out of the satellite.
- Fog start past the last in-play feature.
- Worst-tee frame time within budget.

## Pitfalls

- The first Tierra horizon was a flat plane with a cliff, then invented hills; both came back.
- A "pale field" was the highway and entrance road; an "orchard across the valley" was old imagery; check the satellite date and Street View first.
- A satellite clean turned a road into a sandy wash.
- Scene fog is overridden by GSPro's sky; test in GSPro before tuning.
- Off-site trees on roofs or roads: same removal reasons as the tree skill.

## Decision points (ask once, with a default)

Nothing here needs a visit if the satellite is recent; a skyline photo from the highest tee is the only capture.

1. Flyover envelope per course (default: everything visible from flyover height along the edge, far ridges as backdrop).
2. Named landmarks to show (default: what is on the real skyline).
3. Neighbourhood detail (default: shells with real roof colours).
