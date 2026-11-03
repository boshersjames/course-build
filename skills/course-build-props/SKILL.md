---
name: "course-build-props"
description: "Use when placing hole signs, bins, markers, clubhouses, restrooms, sheds, monuments or other props on a GSPro/OPCD course."
---

Rules file: `rules/props.json` (every number below is defined there; read it with `tools/rules.py props`). Course overrides: `course.json` → `rule_overrides.props`.

# Props, signs and buildings

Use with `course-build-gates` and `course-build-flyovers` (the sign is the flyover's first frame). Full guide: `course-build/playbooks/11_props_signs_and_buildings.md` in the moorpark repo. LiDAR decision point: `playbook/LIDAR_DETAIL_REVIEW.md` D14.

## Defaults that worked

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

## Procedure

1. Prop inventory per hole from the photo audit, object by object, each with a verdict.
2. Build from a spec file + builder script (clubhouse, props) in batch, with a before/after sheet from the photo spots.
3. Place by photo GPS where it exists, else by satellite.
4. Run the prop placement check after every pass.

## Checks

- Nothing on paths, bunkers, tee surfaces/edges, greens or water; nothing inside shrubs or under low canopies; nothing in a hitting line unless photographed.
- No floating props; shadow audit.
- Every building collider has a GSP physics material; no see-through walls.
- Sign readable in the flyover's first frame without blocking the tee shot (the camera comes to the sign, not the sign to the camera).

## Pitfalls

- The GKD cannot set the flyover camera; sign placement is the only lever.
- Tee-sign photos posed 2 m off put the plaque edge-on and "missing"; re-pose before rebuilding.
- Clubhouse patio walls spilled onto a back tee.
- Splash/thumbnail: the course's own photo at GSPro's exact aspect, EXIF-rotated, `image_altered*` cache deleted after a fix.
- GSPro description: name, "City, State, USA", a sourced paragraph; Designer field is "jboshers", the architect goes in the description.

## Decision points (ask once, with a default)

**Needs an in-person look even with a newer scan:** props under 1 m, doors, signs and bins (D14, never in any scan: GPS photos, not the satellite), ropes and stakes, sign style and beds, enclosure layouts, building colours and windows. Roof blocks are reliable in LiDAR.

1. Sign style and whether yardages show (default: as the real plaque).
2. Which landmarks/easter eggs are in (once per course).
3. Clubhouse detail level (shell + hardscape by default).
