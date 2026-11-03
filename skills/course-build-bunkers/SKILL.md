---
name: "course-build-bunkers"
description: "Use when mapping, grading, rebuilding or checking any bunker in a GSPro course build: depth by type, crisp low lips, flashed faces, sand colour, noses, tree clearance and the package workflow."
---

Rules file: `rules/bunkers.json` (every number below is defined there; read it with `tools/rules.py bunkers`). Course overrides: `course.json` → `rule_overrides.bunkers`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/scan_index.py`: a bunker scan gives rim-to-floor depth per sand blob against the LiDAR (`extra` column); rules `bunkers.greenside.depth_m`, `bunkers.fairway.depth_m` are the targets to compare against.
- `python ../course-build/tools/colour_match.py photo.jpg render.png --box x,y,w,h --target sand`: sand colour in a same-light frame, PASS/FAIL against `bunkers.sand`, suggested multiplier; render sand b/g must sit `bunkers.sand.bg_ratio_vs_concrete_pct` below render concrete.
- `python ../course-build/tools/flyover_gate.py --out progress/flyover_gate`: crown clearance along the flight shows trees over sand near the line (rules `bunkers.tree_clearance` is checked in Unity, not here).
- (no tool yet: island check, black-pocket check, lip height and face angle from the mesh, outline vs satellite offset; Tierra versions in `unity/course-templates/TierraBunkerIslands.cs`, `BunkerDepth.cs` in `unity/shared/`; candidate for a `CourseBunkerChecks.cs`)

# Course build: bunkers

Follow the standing rules in `course-build-gates` (no islands, never invent ground, shared sand stays shared, photo pairs for every edit). LiDAR review decision points for bunkers are D4-D6 in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo). Full guide: `course-build/playbooks/03_bunkers.md`.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Mapping | Every bunker in the satellite, including ones missing from OSM | Penmar 1 |
| Depth reference | Depths are measured below a plane fitted to the rim, not from the lowest rim point or the green | Bunker Studio |
| Greenside depth | 0.8-1.2 m below the green, ~38° face on the green side, flat floor (≤2.9°); Tierra 12/16 rebuilt to 1.3-1.4 m bowls | Hansen plan |
| Fairway depth | 0.4-0.7 m, ~30° face, long flat floor | Hansen, Tierra 18 |
| Face shape | Steep on the high/green side, gentle walk-in on the low side | Bunker Studio |
| Lip | Crisp and low: turf rolls over with ~0.1 m of grass on the face, then a near-vertical cut in the sand mesh; soil cut 5-10 cm (cap 10 cm) coloured as dirt lip, fading over 12 cm | h11b v2, h15b |
| Lip height | Green-side 0.15 m (0.3 m made a grass wall); mown lips ≤0.3 m; fairway lips 0.12 m, no hedge band | h11b, h16 |
| Lip shape | Rounded/blunt kidney ends, never pointed; one clean curve, no saw-tooth | 16, 17 |
| Edge wobble | Seeded (repeatable) and zero at the seam | Bunker Studio |
| Face | Sand flashed up the far face to a thin lip (as photographed), not a tall turf wall; the turf face starts at the raised rim and rises with a smoothstep, no V-trench between turf and sand | BoJa 10/31, h16fw3 |
| Floor | Follows the LiDAR/scan inside the outline | h11b v2 |
| Sand colour | Warm tan, not white: (0.70, 0.66, 0.56) / #D8C99B; render sand b/g 5-10% lower than render concrete | playtest, Hansen |
| Rakes | Rake-mark normal map on the sand; **no rake props** | BoJa |
| Islands | None; a *nose* (raised rounded tongue of rough joined to the back bank, sod-wall face on the sand side) only where BoJa approves per bunker | BoJa 10/31 |
| Trees over sand | ≥1.3× LiDAR crown radius + 0.5 m from sand (rendered crowns are bigger); nudge or shrink, delete last | Wilson/Harding |
| Physics | Material name contains "sand"/"Bunk" → GSPro TVGsand | playtest |
| Rim rings / undersides | None under or around the sand (z-fighting, black skirts); terrain lowered under deepened bunkers by depth + 5 cm | Hansen 1, Tierra 18 |

## Seams

- The surround's outer edge stays fixed; only its inner row follows the sand.
- Reject a surround stretched outside 0.75-1.5× its width.
- Use one shared, averaged normal on both sides of the sand seam, so no lit line shows.
- Every sand outline vertex has a matching surround vertex (no slit).

## Procedure

1. Data: `bunkers.csv` from `coursekit.py svg` (type, depth, face, lip); `clear_bunker_crowns.py`; `bunker_plan.py`.
2. Unity: BunkerDepth "Apply Graded" by type (the old 0.25/0.35/0.5 m by area was too shallow); SandLook for colour and rake normals.
3. Per photographed bunker: match GPS photos to bunkers in view (within 30° of heading, 6-110 m), render photo | game, compare outline, lip height, face, depth, sand line. A clean scan wins.
4. Rebuild as a *package* (editor B) on a named save: keep sand-edge vertices, re-mesh the floor to scan/LiDAR, build the lip cut and dirt band, re-mesh the surround rough, lower the terrain, re-run vertex paint, island check, render photo | now | package at the same poses plus a top view and a low view along the lip. Editor A applies on a fresh reload.
5. Noses/mounds: own mesh in the plain (non-satellite) semi-rough material with white vertex colours, domed ~0.4 m, soil cut along the sand side; edge against the bank is the old sand outline, not triangle teeth. Bury the rim ~2 cm; sinking the outer rings deeper shrinks the visible bulb (Tierra 15 round 4).

## Checks

- Island check clean (approved noses listed).
- No black pockets (max channel <28 within 6 px of sand), creases, spikes, facets, kinks; footprint unchanged after a dressing-only edit.
- No turf squares in sand, no overhanging lips, no saw-tooth edges.
- No turf flaps over a neighbouring bunker's sand: drop surround triangles within 3 cm of a neighbour's outline.
- The LiDAR mound between bunker and green survives Lower Terrain Under Meshes.
- Photos show ~3× more face than the render while the LiDAR supports today's shape → BoJa question (raise the bank), not a silent fix.
- Mesh vs satellite sand offset (Tierra 18: 1.5-2 m): report; re-seat only what BoJa names.
- Sand colour measured in a same-light frame.

## Pitfalls

- Long-grass lip bands and hedge-like rims read as walls from the fairway.
- Rims as separate rings z-fight and show black skirts; build the rim in the sand/surround meshes.
- Never move sand on a photo alone; the satellite must confirm.
- Reopen an approved mesh once, carrying every known defect fix in the same package.
- Lower a lip by raising the sand margin 0.12-0.18 m feathered into the bunker; don't drop the fairway.
- Pixel-box specs rot once poses move; measure in world coordinates.
- Bunker Studio's preset depths (0.28-0.70 m) are too shallow for greensides; use the depths above.
- Bunker Studio (Blender add-on) is GPL-3.0: borrow its ideas and numbers, never paste its code.

## Ask BoJa once (with a default)

**In person even with a newer scan:** islands, noses and mounds inside sand (D6). QL1 data settles sand edges (D4) and depth/faces (D5); on QL2 courses (Tierra, Hansen, Moorpark) those come from photos and phone scans.

1. Course bunker style (deep sod bowls vs shallow flashed sand) from 2-3 reference photos before the plan runs.
2. Any nose or mound, per bunker, from a photo.
3. Face height where LiDAR and photos disagree (default keep the scan).
4. Sand colour once for the course.
5. Rakes: default none.
