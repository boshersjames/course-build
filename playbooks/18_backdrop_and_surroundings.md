# 18. Backdrop and surroundings

Use this for everything a tee, green or flyover frame sees past the course square: far terrain, neighbourhood buildings, roads, freeways, parking, rivers, off-site trees, fog and far clip. The rule is BoJa's: never invent ground; everything visible comes from real DEM, the satellite and OSM.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Inner terrain | The course square (2000 m, 3000 m for 27 holes) from the LiDAR heightmap; untouched by the backdrop | 14 |
| Outer plot | A coarser DEM continuing the square's edge outward with no step (Wilson: 8 km Outer Plot), stitched to the inner edge, satellite or graded land-cover texture on it | Wilson 1 |
| Far backdrop | USGS 3DEP DEM out to the farthest ridge any flyover sees (Wilson: 140 km, Verdugos, San Gabriels, Griffith ridges), as a low-poly mesh | Wilson 1 |
| Fog and clip | GradientFog from the outer plot edge to the far ridge (Wilson 8-140 km, inner 1.8-6 km), camera far clip past the last ridge (150 km); GSPro sets its own sky, so don't fight it | Wilson, Tierra 10/30 |
| Roads | OSM/Overture roads, freeways and the entrance built wherever a tee, green or flyover frame sees them (Tierra: Highway 23, the entrance drive; Wilson: I-5 / 134, Griffith Park Dr); widths from OSM lanes, lane lines, shoulders, poles, hedges, walls and signs from Street View | ROAD_TR, 09 |
| Buildings | OSM footprints + LiDAR roof heights for every building in the flyover envelope (Wilson: 112); simple shells with real roof colour from the satellite; homes beyond 300 m as blocks | Wilson, 11 |
| Parking and hardscape | OSM parking lots as asphalt with lines, 20 on Wilson; the clubhouse lot with cart staging | Wilson |
| Rivers and channels | The LA River channel built as concrete channel, not a hazard; natural washes beyond the course as dry beds | Wilson, 08 |
| Off-site trees | LiDAR trees out to 300 m from play (Hansen: 1,959 off-site), low-detail prefabs, denser at entrances where the satellite shows it | 06 |
| Landmarks | Far peaks and named landmarks placed from bearings off known tees (the Reagan Library from Tierra 12); the orchard and farm fields as textured ground from the satellite date | D15 |
| Power lines | Pole lines along boundaries where the photos show them on the skyline (Tierra 16 south boundary) | visit 10/29 |

## Procedure

1. Day one with the LiDAR: fetch 3DEP 1/3 arc-second (or 1 m where it exists) out to 1-2 km for the outer plot and 10 m DEM to the horizon; OSM/Overture roads, buildings, parking, water, land use for the same extent.
2. Build the outer plot continuing the inner edge; check the seam from every edge tee at ground level (no cliff, no step).
3. Far backdrop mesh from the coarse DEM, fog and far clip set; one render from the highest tee against the skyline photo (16); skyline fit RMS under 10 px is the pass.
4. Roads, parking, buildings, river as a package (built in editor C, applied in A): no vertex on mown turf, drape check, island check.
5. Off-site trees, pole lines, landmarks; then a left/right sweep from every flyover start for empty or invented ground.

## Checks

- Skyline from each tee matches the photo (fit RMS < 10 px); no invented hills.
- No flat plane or cliff at the course edge; outer plot continues the slope.
- Every road or building a flyover frame sees exists; nothing cleaned out of the satellite (Highway 23 lesson).
- Fog start past the last in-play feature so the back nine doesn't grey out.
- Frame time from the worst tee still under the budget (12).

## Pitfalls

- The first Tierra horizon was a flat plane with a cliff at the edge, then invented hills; both came back.
- A "pale field" beyond 15 was the highway and entrance road; the "orchard across the valley" was old imagery. Check the satellite date and Street View before deciding what a patch is.
- Satellite cleans that removed a road turned it into a sandy wash.
- Fog set in the scene is overridden by GSPro's sky; test in GSPro before tuning further.
- Off-site trees planted on roofs or roads: run the same removal reasons as 06.

## Decision points for BoJa

**Needs an in-person look:** nothing here needs a visit if the satellite is recent; a skyline photo from the highest tee is the only capture (16).

1. How far the flyover envelope reaches per course (default: everything visible from flyover height along the course edge, far ridges as backdrop).
2. Named landmarks to show (default: whatever is on the real skyline).
3. Neighbourhood detail level (default: shells with real roof colours; no interiors, no fences between homes).

## Sources

`progress/wilson/pass1/checklist_wilson.md` (Hills/backdrop, Backdrop, Surroundings rows), Hansen pass 8 (off-site trees), `progress/tierra/road/roadtr_notes.md`, `streetview_1031/README.md`, `sr23/`, LiDAR detail review D15, checklist "never invent ground", "don't clean real features out of the satellite".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-backdrop` but not in this playbook when the skill was cut to one page.


### Defaults that worked
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

### Procedure
1. Day one with the LiDAR: 3DEP 1/3 arc-second (or 1 m) to 1-2 km for the outer plot and 10 m DEM to the horizon; OSM/Overture roads, buildings, parking, water, land use for the same extent.
2. Outer plot continuing the inner edge; check the seam from every edge tee at ground level.
3. Far backdrop mesh, fog and far clip; one render from the highest tee against the skyline photo; skyline fit RMS < 10 px passes.
5. Off-site trees, pole lines, landmarks; left/right sweep from every flyover start for empty or invented ground.

### Checks
- Skyline from each tee matches the photo; no invented hills.
- No plane or cliff at the course edge.
- Every road or building a flyover frame sees exists; nothing cleaned out of the satellite.
- Fog start past the last in-play feature.
- Worst-tee frame time within budget.

### Pitfalls
- The first Tierra horizon was a flat plane with a cliff, then invented hills; both came back.
- A "pale field" was the highway and entrance road; an "orchard across the valley" was old imagery; check the satellite date and Street View first.
- A satellite clean turned a road into a sandy wash.
- Scene fog is overridden by GSPro's sky; test in GSPro before tuning.
- Off-site trees on roofs or roads: same removal reasons as the tree skill.

### Decision points (ask once, with a default)
Nothing here needs a visit if the satellite is recent; a skyline photo from the highest tee is the only capture.
1. Flyover envelope per course (default: everything visible from flyover height along the edge, far ridges as backdrop).
2. Named landmarks to show (default: what is on the real skyline).
3. Neighbourhood detail (default: shells with real roof colours).
