# 09. Cart paths, roads and fences

Use this for cart paths and curbs, dirt paths, public roads and entrances, fences, ropes and range nets.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Cart paths | From OSM (`cartpaths.csv`), 2.4-2.5 m strips, GSPconcrete, lifted ≥4 cm above the course surface so rough doesn't poke through | HANDOFF, playtest P2 |
| Path look | One shared light concrete matching the photos: broom finish, joint every 2.7 m, tyre tracks, dirt on the edges, normal map (`CourseLook.cs`); no brown dirt joins, no dark asphalt segments, no stacked slabs or double curbs at junctions | course_look, BoJa 10/30 |
| Path edge | No dark 15 cm strip and no mulch band beside paths (removed course-wide); a litter strip only where photographed, never on concrete; raised 15 cm rounded curbs only where photographed | morning_1031 Q1 |
| Dirt paths | Where photos show the concrete ending (17 behind the green), the path is dirt visually **and** in physics (no hard bounce); back half mulch over GSPdeeprough where photographed | BoJa 10/30-31 |
| Roads | Build roads, parking and the entrance where visible from flyover height: 4-lane 22.4 m curb-to-curb asphalt with lane lines and shoulders, utility poles, hedge, entrance drive with flared mouth, stone walls and signs from Street View | ROAD_TR |
| Fences | Real type and height from photos (Tierra split-rail 1.07 m, 2 rails, posts every 2.7 m; Penmar 12 ft chain link with a mid rail); solid colliders (ball must not pass); continuous, no gaps or free-ended pieces; moved, never deleted | checklist |
| Fence vs OB | OB follows the real fence line; fence on the crest 1.5 m in front of the trunks (Tierra 5) | |
| Ropes | Low white rope (~1 cm, off-white) on dark brown stakes 0.45-0.5 m every ~5 m, 0.3 m off the concrete, tied ~0.42 m up, fairway side only, only where photographed (Tierra 10, 18, 17 by bunker 9) | ROPE10/ROPE18 |
| Nets | Range net 19 m poles, solid collider (not leaf physics), transparency ~0.5 (real net looks darker); brown timber poles | HANDOFF, playtest |
| Walls | Stone retaining walls (GSPstone) to the photographed length (Tierra 3: 100 m+, not 35 m) | session 10/25 |

## Standing rules (BoJa, 11/1)

1. **Roundabouts are solid circles**: a turning circle is one filled concrete disc, never a ring or a set of arcs with turf in the middle.
2. **Nothing overlaps a path**: no grass, trees or bushes on or over the concrete. Turf is trimmed against the path polygon; plants and crowns stay off it; brush goes on the side the photos show (Tierra 14 had it on the wrong side of the tee path).
3. **Path and turf edges line up**: the turf edge is the path edge, with no bite, gap or lip. When a bank rises over the path (Tierra 10 S-bend by the tees, x 1291-1304, z 1117-1121: `Spline_rough_0_6_Rough_h10` rose 0.7 m over `CartPath_9`), trim the turf against the path polygon first, then re-grade the bank and its neighbour rims with the tee rim pinned. Never drop triangles: cutting the 10 bank sank the `tee_19` corner and had to be reverted. An orphan pad under a path (`Spline_tee_6` under `CartPath_9`) is trimmed, not left.

Check that catches all three: a top-down raycast grid every 0.25 m over every path, where the top hit must be the concrete (anything else is an overlap or a bite), plus one oblique still along each path near every tee and green.

## Procedure

1. `BuildCartPaths` from OSM; check alignment vs the painted paths; lift ≥4 cm; `CourseLook > Apply to cart paths` (check the found count first).
2. Fix junction geometry: smooth kinks into arcs, no stacked slabs, widths 2.4-3 m against photos, one continuous strip past buildings.
3. Fences from `fences.csv` (`BuildFences`): real post line from photos/satellite, regular spacing, snapped to terrain, no segments past either end; move runs behind tree belts when the photos show them there (15).
4. Ropes with the `TierraRope` script marching the path collider edge; end stakes at bare cart entries.
5. Roads as a prefab package (ROAD_TR: 634 objects, built in C, applied in A): road, drive, walls, signs, poles, hedge, tree screen; "no vertex on mown turf", asphalt drape check, island check.

## Checks

- Rough pokes through ≤2% of path samples; no z-fighting at edges.
- Path raycast grid (0.25 m): 100% concrete top hits over every path; oblique still per path at tees and greens (standing rules).
- Surface type matches the photo (concrete / dirt / mulch) in both material and physics.
- Fence gate check: no broken sections, no tight post groups, no fence on turf where photos show none.
- Objects-on-turf inside rope lines; mulch never on concrete; sign beds end at the curb.
- Tee pads ≥2 m off paths, no GKD tee on a path (05).

## Pitfalls

- Paths built white/bright read wrong; a copied constant stretched the concrete tile (Penmar).
- Tierra 10's rope was mis-attributed to 18 from a GPS slip; check which hole a photo's GPS really sits on before building a 160 m rope.
- A split-rail fence placed along the 15 path where the real one is behind the tree row; fence pieces on turf by bunkers (15); "fences moved, never deleted" came from this.
- Highway 23 right of 15 was turned into a wash by a satellite clean; and the pale field beyond 15 is the entrance road, not a tan field.
- Clubhouse plaza built as cream tiles with "umbrella" props; real hardscape is grey concrete with curbs and stone planter walls (11).

## Decision points for BoJa

1. Which off-course roads/entrances are in the flyover envelope and get built (default: anything visible from flyover height along the course edge).
2. Ropes: build as photographed per hole (default yes where a photo shows one).
3. Fence material tint per course (weathered dark brown vs cast faux-wood cream) from a photo.

## Sources

`tierra-rejada-lidar/course_look/README.md`; `progress/tierra/road/roadtr_notes.md`, `streetview_1031/README.md`, `holes/hole_10_state.md` (rope audit), `gate/hole_18/top_items.md` (item 8), `gate/hole_15/top_items.md` (items 1, 6); `unity_editor/BuildCartPaths.cs`, `CourseLook.cs`; Penmar `fences.csv`, `nets.csv`, `unity/ChainLinkFence.cs`; checklist "Cart paths", "Fences", "15 flyover review".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-paths-fences` but not in this playbook when the skill was cut to one page.


### Standing rules for cart paths (BoJa 11/2)
- **Roundabouts and turnarounds are always solid circles**: one filled concrete disc, never a ring with a grass or dirt centre, never a polygon with gaps or slivers. Build the disc as its own mesh (enough segments that the rim reads round from flyover height, at least 48) and weld the joining path strips into its rim.
- **Nothing overlaps a path**: no grass (green, collar, fairway, tee, rough, blend), no trees, no bushes, no brush or patches on or over the concrete. Turf stops at the path edge; crowns and shrub footprints stay clear of it. Plants sit on the side of the path the photos show (Tierra 14: brush was built on the wrong side of the tee path).
- **Edges line up**: the path edge, the turf edge beside it and every joining strip meet on one clean line, with no gaps, steps, saw-tooth, overlaps or double edges. Junctions and roundabout joins are welded, not stacked.

### Defaults that worked
- **Cart paths**: from OSM (`cartpaths.csv`), 2.4-2.5 m strips, GSPconcrete, lifted ≥4 cm above the course surface so rough doesn't poke through. One shared concrete material for the whole course.
- **Path look**: light broom-finish concrete matching the photos, joint every 2.7 m, tyre tracks, dirt on the edges, normal map (`CourseLook.cs`); no brown dirt joins, dark asphalt segments, stacked slabs or double curbs at junctions.
- **Path edge**: no dark 15 cm strip and no mulch band beside paths; litter strip only where photographed, never on concrete; raised 15 cm rounded curbs only where photographed.
- **Dirt paths**: where photos show the concrete ending, the path is dirt visually and in physics; mulch over GSPdeeprough where photographed.
- **Roads**: build roads, parking and the entrance visible from flyover height (4-lane 22.4 m curb-to-curb asphalt, lane lines, shoulders, poles, hedge, flared entrance, walls and signs from Street View).
- **Fences**: real type and height from photos (split-rail 1.07 m, 2 rails, posts every 2.7 m; 12 ft chain link with mid rail); solid colliders; continuous, no gaps or free ends; moved, never deleted. OB follows the real fence line.
- **Ropes**: off-white ~1 cm rope on dark brown stakes 0.45-0.5 m every ~5 m, 0.3 m off the concrete, tied ~0.42 m up, fairway side only, only where photographed.
- **Nets**: 19 m poles, solid collider (not leaf physics), transparency ~0.5, brown timber poles.
- **Walls**: stone retaining walls (GSPstone) to the photographed length.

### Procedure
2. Fix junction geometry: arcs not kinks, widths 2.4-3 m against photos, one continuous strip past buildings. Replace every roundabout or turnaround with a solid disc (see Standing rules) and weld the strips into it.
3. Trim turf to the path: for every turf mesh (green, collar, fairway, tee, rough, blend, patches), drop or clip triangles whose footprint crosses the path polygon, then ease the turf edge so it meets the path edge on one line (never by dropping whole triangles that leave a saw-tooth). Tee pads and greens never spill onto concrete (Tierra 10 S-bend near the tees, 11/2).
4. Clear plants: no trunk, crown or shrub footprint over a path (renderer bounds tested in 16 directions, as for greens). Move away from the path, never onto mown turf, keep the photographed side; shrink if it can't clear; never delete.
5. Fences from `fences.csv` (`BuildFences`): real post line from photos/satellite, snapped to terrain, no segments past either end; move runs behind tree belts when photos show them there.
6. Ropes with a script marching the path collider edge; end stakes at bare cart entries.
7. Roads as a prefab package built in editor C, applied in A; no vertex on mown turf, asphalt drape check, island check.

### Checks (all 18 holes, every install)
- Roundabouts: every one is a single solid disc; no grass or dirt centre, no holes or slivers inside it.
- Overlap: zero turf triangles inside any path polygon (top-down raycast grid at 0.25 m over every path: the top hit is concrete everywhere); zero tree crowns or shrub footprints over a path.
- Edge line-up: sample the path edge every 0.5 m; turf meets it with no gap > 3 cm, no step > 2 cm, no overlap, and joining strips share rim vertices.
- Visual: a high oblique still along every path near tees and greens shows clean edges, no spill, no plants on the wrong side.
- Rough pokes through ≤2% of path samples; no z-fighting.
- Surface type matches the photo (concrete / dirt / mulch) in material and physics.
- Fence gate check: no broken sections, tight post groups, or fence on turf where photos show none.
- Mulch never on concrete; sign beds end at the curb.
- Tee pads ≥2 m off paths; no GKD tee on a path.

### Pitfalls
- Paths built white/bright read wrong; a copied constant stretched the concrete tile.
- Roundabouts built from OSM as rings or loose segments showed a grass donut and gaps; always rebuild them as solid discs.
- Turf spilled over the cart path at Tierra 10's S-bend near the tees, and brush was built on the wrong side of the path by Tierra 14's tees; BoJa found both in the showcase flyovers (11/2). Check every path edge, not only the ones near edits.
- A rope mis-attributed to the wrong hole from a GPS slip; check which hole a photo really sits on before building a 160 m rope.
- A fence placed along the path where the real one is behind the tree row; fence pieces on turf by bunkers.
- A highway was turned into a wash by a satellite clean; the pale field beyond a hole was the entrance road.
- Clubhouse plaza built as cream tiles with umbrella props; real hardscape is grey concrete with curbs and stone planter walls.

### Decision points (ask once, with a default)
**Needs an in-person look:** where the real fence line runs, rope runs, and which paths end in dirt. None of this is in any scan.
1. Which off-course roads/entrances are in the flyover envelope (default: anything visible from flyover height along the edge).
2. Ropes as photographed per hole (default yes where a photo shows one).
3. Fence tint per course from a photo.
