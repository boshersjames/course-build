---
name: "course-build-paths-fences"
description: "Use when building cart paths, curbs, dirt paths, public roads, entrances, fences, ropes, nets or retaining walls on a GSPro/OPCD course."
---

Rules file: `rules/paths_fences.json` (every number below is defined there; read it with `tools/rules.py paths_fences`). Course overrides: `course.json` → `rule_overrides.paths_fences`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/colour_match.py photo.jpg render.png --box x,y,w,h`: concrete tone against the photo in the same light (sand is judged relative to it: `bunkers.sand.bg_ratio_vs_concrete_pct`).
- Unity `Course Look > Cart Paths and Turf` (`CourseLook.cs`): shared concrete look and graded satellite on every layer; check the found count.
- `BuildCartPaths.cs` / `BuildFences.cs` / `BuildBridges.cs` (`unity/shared/`): paths from `cartpaths.csv`, fences from `fences.csv`, bridges; run through `Send-UnityCommand.ps1 invoke -Tool <name> -Method <method>` (`describe -Tool <name>` lists the methods).
- (no tool yet: roundabout solid-disc check, turf-over-path raycast grid at `paths_fences.checks.overlap_grid_m`, edge line-up at `edge_sample_m` / `edge_gap_max_m` / `edge_step_max_m`, rough poke-through %, fence continuity; candidate for a `CoursePathChecks.cs`)

# Cart paths, roads and fences

Use with `course-build-gates`. Full guide: `course-build/playbooks/09_cart_paths_roads_and_fences.md` in the moorpark repo.

## Standing rules for cart paths (BoJa 11/2)

- **Roundabouts and turnarounds are always solid circles**: one filled concrete disc, never a ring with a grass or dirt centre, never a polygon with gaps or slivers. Build the disc as its own mesh (enough segments that the rim reads round from flyover height, at least 48) and weld the joining path strips into its rim.
- **Nothing overlaps a path**: no grass (green, collar, fairway, tee, rough, blend), no trees, no bushes, no brush or patches on or over the concrete. Turf stops at the path edge; crowns and shrub footprints stay clear of it. Plants sit on the side of the path the photos show (Tierra 14: brush was built on the wrong side of the tee path).
- **Edges line up**: the path edge, the turf edge beside it and every joining strip meet on one clean line, with no gaps, steps, saw-tooth, overlaps or double edges. Junctions and roundabout joins are welded, not stacked.

## Defaults that worked

- **Cart paths**: from OSM (`cartpaths.csv`), 2.4-2.5 m strips, GSPconcrete, lifted ≥4 cm above the course surface so rough doesn't poke through. One shared concrete material for the whole course.
- **Path look**: light broom-finish concrete matching the photos, joint every 2.7 m, tyre tracks, dirt on the edges, normal map (`CourseLook.cs`); no brown dirt joins, dark asphalt segments, stacked slabs or double curbs at junctions.
- **Path edge**: no dark 15 cm strip and no mulch band beside paths; litter strip only where photographed, never on concrete; raised 15 cm rounded curbs only where photographed.
- **Dirt paths**: where photos show the concrete ending, the path is dirt visually and in physics; mulch over GSPdeeprough where photographed.
- **Roads**: build roads, parking and the entrance visible from flyover height (4-lane 22.4 m curb-to-curb asphalt, lane lines, shoulders, poles, hedge, flared entrance, walls and signs from Street View).
- **Fences**: real type and height from photos (split-rail 1.07 m, 2 rails, posts every 2.7 m; 12 ft chain link with mid rail); solid colliders; continuous, no gaps or free ends; moved, never deleted. OB follows the real fence line.
- **Ropes**: off-white ~1 cm rope on dark brown stakes 0.45-0.5 m every ~5 m, 0.3 m off the concrete, tied ~0.42 m up, fairway side only, only where photographed.
- **Nets**: 19 m poles, solid collider (not leaf physics), transparency ~0.5, brown timber poles.
- **Walls**: stone retaining walls (GSPstone) to the photographed length.

## Procedure

1. `BuildCartPaths` from OSM; check alignment vs the painted paths; lift ≥4 cm; `CourseLook > Apply to cart paths` (check the found count first).
2. Fix junction geometry: arcs not kinks, widths 2.4-3 m against photos, one continuous strip past buildings. Replace every roundabout or turnaround with a solid disc (see Standing rules) and weld the strips into it.
3. Trim turf to the path: for every turf mesh (green, collar, fairway, tee, rough, blend, patches), drop or clip triangles whose footprint crosses the path polygon, then ease the turf edge so it meets the path edge on one line (never by dropping whole triangles that leave a saw-tooth). Tee pads and greens never spill onto concrete (Tierra 10 S-bend near the tees, 11/2).
4. Clear plants: no trunk, crown or shrub footprint over a path (renderer bounds tested in 16 directions, as for greens). Move away from the path, never onto mown turf, keep the photographed side; shrink if it can't clear; never delete.
5. Fences from `fences.csv` (`BuildFences`): real post line from photos/satellite, snapped to terrain, no segments past either end; move runs behind tree belts when photos show them there.
6. Ropes with a script marching the path collider edge; end stakes at bare cart entries.
7. Roads as a prefab package built in editor C, applied in A; no vertex on mown turf, asphalt drape check, island check.

## Checks (all 18 holes, every install)

- Roundabouts: every one is a single solid disc; no grass or dirt centre, no holes or slivers inside it.
- Overlap: zero turf triangles inside any path polygon (top-down raycast grid at 0.25 m over every path: the top hit is concrete everywhere); zero tree crowns or shrub footprints over a path.
- Edge line-up: sample the path edge every 0.5 m; turf meets it with no gap > 3 cm, no step > 2 cm, no overlap, and joining strips share rim vertices.
- Visual: a high oblique still along every path near tees and greens shows clean edges, no spill, no plants on the wrong side.
- Rough pokes through ≤2% of path samples; no z-fighting.
- Surface type matches the photo (concrete / dirt / mulch) in material and physics.
- Fence gate check: no broken sections, tight post groups, or fence on turf where photos show none.
- Mulch never on concrete; sign beds end at the curb.
- Tee pads ≥2 m off paths; no GKD tee on a path.

## Pitfalls

- Paths built white/bright read wrong; a copied constant stretched the concrete tile.
- Roundabouts built from OSM as rings or loose segments showed a grass donut and gaps; always rebuild them as solid discs.
- Turf spilled over the cart path at Tierra 10's S-bend near the tees, and brush was built on the wrong side of the path by Tierra 14's tees; BoJa found both in the showcase flyovers (11/2). Check every path edge, not only the ones near edits.
- A rope mis-attributed to the wrong hole from a GPS slip; check which hole a photo really sits on before building a 160 m rope.
- A fence placed along the path where the real one is behind the tree row; fence pieces on turf by bunkers.
- A highway was turned into a wash by a satellite clean; the pale field beyond a hole was the entrance road.
- Clubhouse plaza built as cream tiles with umbrella props; real hardscape is grey concrete with curbs and stone planter walls.

## Decision points (ask once, with a default)

**Needs an in-person look:** where the real fence line runs, rope runs, and which paths end in dirt. None of this is in any scan.

1. Which off-course roads/entrances are in the flyover envelope (default: anything visible from flyover height along the edge).
2. Ropes as photographed per hole (default yes where a photo shows one).
3. Fence tint per course from a photo.

- Unity `BuildCartPaths` bridge `invoke -Tool BuildCartPaths -Method CheckTurf [-Set @{ hole = "10" }]`: share of each cart path's surface where a turf mesh is the top collider, by mesh and extent, against `paths_fences.checks.rough_poke_max_pct`; `-Method SinkTurf` sinks those turf vertices `checks.turf_under_path_sink_m` under the path (no triangles removed, so no sawtooth; revert via `CourseCollarFlush.Revert`).
