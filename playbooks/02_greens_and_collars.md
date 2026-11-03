# 02. Greens and collars

Use this for green surfaces, the green-to-collar edge, collars and approach cuts, pins and GKD green settings.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Stimp / firmness (GKD) | `DefaultStimp 2` (= stimp 10), `MaxStimp 5`, greens Normal | BoJa approved; Penmar, Hansen, Angeles, Wilson all set to this |
| Collar width | 1.2 m fairway-height ring on every green; 3 m sides / 5 m back where the surround is flat and BoJa approved wider (Tierra 6, 8, 15); 1.5 m where a bank sits right behind (18) | green_edges_1029 |
| Cut step | Collar sits 1.5 cm proud of the surround (was 3 cm; the real step ~1 cm is below scan noise) | measured on green 10 scan |
| Collar material / physics | Fairway material (own copy, same brightness as the green), GSPfairway | |
| Collar tint | About 10% darker than the green; never long blades | hole 12 list |
| Green edge blend | 0.12 m blend strip using exactly the fairway/rough tone (no lime, no extra brightness) | Wilson 1b |
| Uphill banks behind a green | Rough, not collar (`--collar` skips uphill banks) | Tierra 10, 18 |
| Deep rough | At least 6 m off any green | Tierra 6, 15 |
| Pin check | No pin within 3 m of the edge; flag pins on >3.5% slope for review (don't move them silently) | pin_check.csv |
| Internal slope target | 1-3% gentle slopes, run-offs into surrounds; no dead-flat greens; flag greens steeper than 3% | Hansen 10/29 |
| Green-to-collar transition | One profile on all 18 greens (collar width, step, rolloff), measured from the clean scans; banks follow each green's LiDAR | BoJa 10/29 |

## Procedure

1. From the data side: `coursekit.py svg --collar --collar-back --deep-off-green` gives the collar ring and the 6 m deep-rough offset; `apply_checklist.py` writes `green_slopes.csv` and flags >3%.
2. Confirm the green in play where the satellite shows two (Hansen 11 plays the right one); dress the other as rough. Ask BoJa if photos don't settle it.
3. Build the surface from the LiDAR; apply a clean phone scan where there is one (01). Spike and pit cleanup on scan-less greens.
4. Apply the course edge profile (`CoursePatch.ApplyCollars1029` style): ring on greens that have none; rebuild approved wider collars at the same step; re-seat collar vertices 1.5 cm over the course (largest move ≤0.15 m, 0 with nothing under).
5. Seams: weld collar holes (18 had six), drop rough triangles that overlap the collar/green/blend (11front rough hid the collar behind a saw-tooth band), fade the fringe over ~1.8 m from the collar on a hole-only rough copy if a hard dark band remains.
6. Surrounds: one approach cut from the collar to the fairway where the photos show one (18), not a long-blade rough ring; bank rough close in on uphill banks.
7. Pins: GKD pins raycast to the new surface; when a green moves (LiDAR fit), move the pins with it and say so.

## Checks

- Green outline vs LiDAR pad: if the fit is >1.5 m off, stop and send a top view (moving a green moves the pins).
- Vertex paint on green, collar, blend after any rebuild (bare-dirt otherwise).
- No lime/bright outline ring around any green (Wilson 1b "green outline rings" was still open after the pass: check the blend channel).
- No black slits/slivers at the green edge (12, 13, 14 had them; the `h1Xs` seam packages fix them).
- Height gap between collar and rough ≤ a few cm; bridge with a strip in exactly the rough's settings.
- Island check and plants-on-turf after the collar pass.
- `outline_over_8pct` high → the OSM outline took in banks; re-trace against imagery.

## Pitfalls

- Green scans from phone *photos* don't work on putting surfaces (01). Photos are for the surrounds.
- Reshaping a green outline to a photo (17 at 4958: straight edge vs arc) is allowed with contours unchanged, only on BoJa's answer.
- A 1.2 m green/collar shift toward a bunker put dark slivers into the green edge (11); uncovering the hidden collar fixed the photo distance without the shift.
- Greens built on the Tee material (Tierra 16) need their own tinted copy in any green tint pass.
- GKD stimp 11 / Firm from a data script was wrong; the approved values are stimp 10 / Normal.
- A warmer greens tint (OverallColor ×(1.85, 1.68, 1.42), desaturate 0.2) was needed to match the photos; milder (1.29,1.09,1.16) stayed grey, (1.8,1.55,1.2) without desaturate went neon (12).

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** collar width and cut step (D9, never in any scan); a phone LAS per green for the surface (D7). QL1 data settles the green pad offset (D8); on QL2 courses (Tierra, Hansen, Moorpark) check it against photos. Going beyond the LiDAR around a green (D10) is BoJa's call per green.

LiDAR review decision points for greens: **D7-D10** (which surface the green is built from, outline vs LiDAR pad, collar/cut step/surround profile, going beyond the LiDAR around a green) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md). Answer those before the ones below.

1. Collar widths beyond 1.2 m on specific greens (which, and why: flat surround, photographed approach cut).
2. Any green whose photo outline differs from the scan/LiDAR: reshape or keep (default keep the scan).
3. Pins on >3.5% slopes: move or keep (default keep, listed).
4. Relief exaggeration on a flat green crest (Tierra 9: ×1.3-1.5 on the ridge, pins ≤3%).

## Sources

`progress/tierra/green_edges_1029/README.md`, `course_patches_collars1029.txt`, `greens_cleanup/collar_reseat_report.txt`, `gate/hole_11/edits/h11edge_notes.md`, `photo_audit_1029/green_test/NOTES.md`, `green_rollout/NOTES.md`; checklist "Surfaces and physics", "front-nine greens"; Hansen/Wilson `surrounds.py`, `apply_checklist.py`.
