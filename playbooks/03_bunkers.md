# 03. Bunkers

Use this for every bunker: mapping, depth and face plan, lips, sand, noses and mounds, trees over sand, and the rebuild packages.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Mapping | Every bunker in the satellite, including ones missing from OSM | Penmar 1 right |
| Greenside depth | 0.8-1.2 m below the green, steep face (~38°) on the green side, flat floor (≤2.9°) | Hansen plan, Tierra 12/16 rebuilt to 1.3-1.4 m bowls |
| Fairway depth | 0.4-0.7 m, ~30° face, long flat floor | Hansen, Tierra 18 B27 |
| Lip | Crisp and low: turf rolls over with ~0.1 m of grass on the face, then a near-vertical cut in the sand mesh; soil cut 5-10 cm (cap 10 cm) coloured as dirt lip, fading over 12 cm | h11b v2, h15b |
| Lip height | Green-side 0.15 m (0.3 m made a grass wall); mown lips ≤0.3 m; fairway bunker lips 0.12 m, no hedge band | h11b, h16 edit 18 |
| Lip shape | Rounded/blunt kidney ends, never pointed; one clean curve, no saw-tooth | 16, 17 |
| Face | Sand flashed up the far face to a thin lip (as photographed), not a tall turf wall | BoJa 10/31 on 16 fairway bunkers |
| Floor | Follows the LiDAR/scan inside the outline ("scan": 11front rises 1.3 m toward the green) | h11b v2 |
| Sand colour | Warm tan, not white: floor (0.70, 0.66, 0.56) / #D8C99B; render sand b/g 5-10% lower than render concrete | playtest P8, Hansen 8d, hole 16 check |
| Rake / rakes | Rake-mark normal map on the sand; **no rake props** | BoJa 10/31 |
| Islands | None, ever; a *nose* (raised, rounded tongue of rough joined to the back bank, sod-wall face on the sand side) only where BoJa approves per bunker (Tierra 11; two mounds on 15) | BoJa 10/31 |
| Trees over sand | Keep trees ≥ 1.3× LiDAR crown radius + 0.5 m from sand (rendered crowns are bigger); nudge or shrink, delete last | Wilson/Harding |
| Physics | Material name contains "sand"/"Bunk" → GSPro TVGsand (7-9% speed penalty, its own model) | playtest P7 |
| Rim rings / undersides | None left under or around the sand (z-fighting, black skirts); terrain lowered under deepened bunkers by depth + 5 cm | Hansen 1, Tierra 18 |

## Procedure

1. Data side: `bunkers.csv` from `coursekit.py svg` (per-bunker type, depth, face angle, lip height); `clear_bunker_crowns.py` for tree clearance; `bunker_plan.py` for the depth plan.
2. Unity: BunkerDepth "Apply Graded" by type (the old 0.25/0.35/0.5 m by area was too shallow); SandLook for colour and rake normals.
3. Per photographed bunker: match each GPS photo to the bunkers in view (within 30° of heading, 6-110 m), render photo | game, and compare outline, lip height, face steepness, depth and sand line. A clean scan wins where there is one.
4. Rebuild as a *package* (editor B) on a named save: keep sand-edge vertices (nothing opens to the neighbours), re-mesh the floor to the scan/LiDAR, build the lip cut and dirt band, re-mesh the surround rough to the new outline, lower the terrain under it, re-run vertex paint, island check, and render photo | now | package from the same poses plus a top view and one low view along the lip. A (editor A) applies it on a fresh reload; the apply refuses if the meshes moved.
5. Noses and mounds: own mesh in the plain (non-satellite) semi-rough material with white vertex colours, domed ~0.4 m, soil cut along the sand side; the edge against the bank is the old sand outline, not triangle teeth.

## Checks

- Island check clean (approved noses listed as exceptions).
- No black pockets (max channel <28 within 6 px of sand), creases, spikes, facets or kinks; footprint unchanged after a dressing-only edit.
- No turf squares inside sand, no overhanging lips, no saw-tooth near edges (17 B9).
- No LiDAR mound between bunker and green lost to Lower Terrain Under Meshes (18).
- Apparent sand height from the fairway: if the photos show ~3× more face than the render and the LiDAR supports today's shape, it is a BoJa question (raise the bank behind), not a silent fix (11).
- Bunker mesh vs satellite sand offset (18: 1.75 m west, 1.5-2 m on several meshes): report it; re-seat only the one BoJa names.
- Sand colour measured against the same-light photo frame (shaded sand in a frame with no concrete is not a colour error).

## Pitfalls

- Deep-rough/long-grass lip bands and "hedge-like" rims read as walls from the fairway (16); swap to the plain surround and a crisp edge.
- Bunker rims built as separate rings z-fight and show black skirts at low angles (Hansen 1); build the rim in the sand/surround meshes.
- A moved bunker needs the satellite to confirm (Tierra 9 B33): never move sand on a photo alone.
- Reopening an approved mesh for one change (16 h16fw) should carry every known defect fix in the same package so it is reopened once.
- The first bunker package on 15 lowered a "plain lip" by raising the sand margin 0.12-0.18 m and feathering into the bunker, touching only the outer 0.3-0.5 m of turf: don't drop the fairway to fix a lip.
- Specs in render pixel boxes rot once poses move; measure in world coordinates (±1.5 m) and give the boxes only as recognition aids.

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** grass islands, noses and mounds inside sand (D6, never in any scan). QL1 data settles sand edges (D4) and depth and faces (D5); on QL2 courses (Tierra, Hansen, Moorpark) those come from photos and phone scans.

LiDAR review decision points for bunkers: **D4-D6** (sand edge, depth and face, grass inside the sand) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md). Answer those before the ones below.

1. Bunker depth style for the course (deep bowls with sod faces vs shallow flashed sand) from 2-3 reference photos, before the plan runs.
2. Any grass nose or mound inside a bunker (per bunker, from a photo).
3. Face height where the LiDAR and photos disagree (keep scan vs raise bank).
4. Sand colour once for the course (shared material), checked in same-light frames.
5. Rakes: default none.

## Sources

`progress/tierra/bunkers_fairways_1029/README_bunkers.md`, `hole_11/edits/h11b_notes.md`, `gate/hole_15/edits/h15b_spec.md`, `gate/hole_16/edit_list.md` (edits 15, 18), `gate/hole_18/edit_list.md` (edit 2), `unity_automation/SUMMARY_2026-10-28_playtest.md` (P7/P8); Hansen pass 8 bunkers, Wilson pass 1; checklist "Surfaces and physics", "Hole 11 greenside bunker nose", "15 flyover review".
