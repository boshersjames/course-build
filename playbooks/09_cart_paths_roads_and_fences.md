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
