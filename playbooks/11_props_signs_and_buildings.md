# 11. Props, signs and buildings

Use this for hole signs and their beds, bins, markers, the clubhouse, restrooms, sheds and utility enclosures, monuments and landmarks.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Hole signs | One per hole in the course's style (Tierra: 2.1 m black post with ball finial, arched plaque with monogram, number and PAR; no yardage), no colliders; placed from the sign-photo GPS 2.5 m along the heading, else beside the back tee 3 m forward and 6 m to the path side, facing the flyover start | hole_signs_1029 |
| Sign faces | Drawn (vector text and emblem), never photo crops; real text where a photo shows it ("DON'T BE A DIVOT") | 10/31 |
| Sign beds | Mulch bed with a smooth rounded outline that stops at the curb (half circle against the curb, not a full ellipse), fountain grass, small boulder; never over concrete | BoJa 10/30-31 |
| Bins | Only where a photo shows one (Tierra 10: slatted barrel bin, sized from IMG_4624); none at 15, 17 | BoJa |
| Buildings | From the OSM footprint + LiDAR wall/ridge heights, colours and form from photos: no black windows, no white walls or red roofs unless real; physics materials on every collider (GSPstone/GSPwood/GSPconcrete); hidden restrooms still built from the LiDAR roof under the canopy | Tierra, Penmar, Moorpark |
| Clubhouse hardscape | Grey concrete drive with curbs, ledgestone planter walls with caps, flowering shrubs, dark privacy fence, orange delineators, carts in the staging area; no tan tile plaza, no "umbrella" props, no construction fencing | 10/30, 18 |
| Utility enclosures | Lay out from the photos: which face carries the gate/sign, where the shed sits, the path strip along it, a 3×3 m dirt yard; drawn signs (HIGH VOLTAGE), board fence 15 cm boards with dog-ear tops | 14 |
| Landmarks | Local stories as props when BoJa mentions them, out of play (Penmar PT-22, Tierra Reagan monument, range hillside logo traced from the club logo and draped on the rough) | checklist |
| Markers / stakes | White OB stakes only at photographed spots; tee markers via CourseOverrides (05); flags one texture per course (FLAG_Black); pins out of flyovers | tee_markers |
| Furniture | Benches, ball washers, divot boxes, pedestals, drain grates: only where a photo shows them, flush with the surface, never on paths/tees/greens | gate checks |

## Procedure

1. Prop inventory per hole from the photo audit, object by object (trees, shrubs, beds, grass, fences, walls, rocks, signs, fixtures); log each with a verdict.
2. Build from a spec file + builder (`BuildClubhouse.cs` + `clubhouse_spec.txt`, `MoorparkClubhouse.cs`, `PenmarProps.cs`), in batch, with a before/after sheet from the photo spots.
3. Place by photo GPS where it exists (13 pump station moved 40 m; 14 enclosure standing-spot pinned first), else by satellite.
4. Run the prop placement check after every pass.

## Checks

- Prop placement: nothing on paths, bunkers, tee surfaces/edges, greens or water; nothing inside shrubs or under low canopies; nothing in a hitting line unless photographed.
- No floating props (flags 8-9 m up, the 14 sky card strip, a deck 23 m up on Hansen); shadow audit.
- Every building collider has a GSP physics material; no see-through walls (double-sided or flipped normals).
- Sign readable in the flyover's first frame without blocking the tee shot (real-size plaques are a few pixels at 20-25 m; the camera comes to the sign, not the sign to the camera).

## Pitfalls

- The GKD cannot set the flyover camera; sign placement is the only lever (13).
- Tee-sign photos posed 2 m off put the plaque edge-on and "missing" (11); re-pose before rebuilding a sign.
- Clubhouse patio walls spilled onto the 10 back tee (fix with the clubhouse job).
- Splash/thumbnail: course's own photo at GSPro's exact aspect, EXIF-rotated, `image_altered*` cache deleted after a fix; not the template's image.
- GSPro description: name, "City, State, USA", a sourced paragraph (designer, year, setting, character); Designer field is "jboshers", the architect goes in the description.

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** props under 1 m, doors, signs and bins (D14, never in any scan: GPS photos, not the satellite), ropes and stakes, sign style and beds, enclosure layouts, building colours and window treatments. Roof blocks are reliable in the LiDAR.

LiDAR review decision point for props and buildings: **D14** (where it really is) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Sign style and whether yardages show (default: as the real plaque).
2. Which landmarks/easter eggs are in (ask once per course).
3. Clubhouse detail level (shell + hardscape by default; furniture, string lights and fire pit on request).

## Sources

`progress/tierra/hole_signs_1029/README.md`, `clubhouse_1029/README.md`, `clubhouse_1029b/README.md`, `gate/hole_14/top_items.md` (item 2), `gate/hole_18/top_items.md` (item 7), `holes/hole_10_state.md`; `unity_editor/MoorparkClubhouse.cs`, `MoorparkLandmarks.cs`, `range_logo/README.md` on `claude/moorpark-unity`; checklist "Clubhouse and props", "Signs, bins, hillsides", "Setup and process".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-props` but not in this playbook when the skill was cut to one page.


### Defaults that worked
- **Hole signs**: one per hole in the course's style (Tierra: 2.1 m black post with ball finial, arched plaque with monogram, number and PAR; no yardage), no colliders; placed from the sign-photo GPS 2.5 m along the heading, else beside the back tee 3 m forward and 6 m to the path side, facing the flyover start.
- **Sign faces**: drawn vector text and emblem, never photo crops; real text where a photo shows it.
- **Sign beds**: mulch with a smooth rounded outline that stops at the curb (half circle against the curb), fountain grass, small boulder; never over concrete.
- **Bins**: only where a photo shows one, sized from the photo.
- **Buildings**: OSM footprint + LiDAR wall/ridge heights; colours and form from photos (no black windows, white walls or red roofs unless real); physics materials on every collider (GSPstone/GSPwood/GSPconcrete); hidden restrooms still built from the LiDAR roof.
- **Clubhouse hardscape**: grey concrete drive with curbs, ledgestone planter walls with caps, flowering shrubs, dark privacy fence, delineators, carts in staging; no tan tile plaza, umbrella props or construction fencing.
- **Utility enclosures**: from photos: gate/sign face, shed position, path strip, 3×3 m dirt yard; drawn signs; board fence 15 cm dog-ear boards.
- **Landmarks**: local stories as props when BoJa mentions them, out of play (monuments, a range hillside logo traced from the club logo).
- **Markers/stakes**: white OB stakes only at photographed spots; tee markers via CourseOverrides; one flag texture per course; pins out of flyovers.
- **Furniture**: benches, ball washers, divot boxes, drain grates only where photographed, flush with the surface, never on paths/tees/greens.

### Procedure
1. Prop inventory per hole from the photo audit, object by object, each with a verdict.
2. Build from a spec file + builder script (clubhouse, props) in batch, with a before/after sheet from the photo spots.
3. Place by photo GPS where it exists, else by satellite.

### Checks
- Nothing on paths, bunkers, tee surfaces/edges, greens or water; nothing inside shrubs or under low canopies; nothing in a hitting line unless photographed.
- No floating props; shadow audit.
- Every building collider has a GSP physics material; no see-through walls.
- Sign readable in the flyover's first frame without blocking the tee shot (the camera comes to the sign, not the sign to the camera).

### Pitfalls
- The GKD cannot set the flyover camera; sign placement is the only lever.
- Tee-sign photos posed 2 m off put the plaque edge-on and "missing"; re-pose before rebuilding.
- Clubhouse patio walls spilled onto a back tee.
- Splash/thumbnail: the course's own photo at GSPro's exact aspect, EXIF-rotated, `image_altered*` cache deleted after a fix.
- GSPro description: name, "City, State, USA", a sourced paragraph; Designer field is "jboshers", the architect goes in the description.

### Decision points (ask once, with a default)
**Needs an in-person look even with a newer scan:** props under 1 m, doors, signs and bins (D14, never in any scan: GPS photos, not the satellite), ropes and stakes, sign style and beds, enclosure layouts, building colours and windows. Roof blocks are reliable in LiDAR.
2. Which landmarks/easter eggs are in (once per course).
3. Clubhouse detail level (shell + hardscape by default).
