# 21. Publish to GSPro (local install, playtest, beta submission)

Use this for every push of a course into GSPro: the local install on BoJa's PC, the playtest, the rollback, and the beta-submission gate for the OPCD/GSPro community. A local push needs the install and rollback steps; a beta submission needs the whole pre-beta gate below. Nothing goes into `C:\GSProV1\Core\GSP\Courses\` without BoJa's go (standing rule, 00).

**Order (BoJa 11/05, standing rule): 4K flyover videos first, then slim for GSPro.** All 4K exports are rendered from the full-detail scene (13, step 6); size cuts to stay well under ~2.1 GB come after, never before.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Build | Unity batch build of the course asset bundle from editor A's saved scene; `.gspcrse` beside the GKD; rebuilding replaces only `.gspcrse`, the GKD and other files stay | HANDOFF |
| Course folder | `C:\GSProV1\Core\GSP\Courses\<Course>\`: `.gspcrse`, `<Course>.GKD`, splash `.jpg` (EXIF-rotated, GSPro's exact aspect), coursedetails.txt, scorecards (the last two written by Greenkeeper's own save) | checklist |
| Greenkeeper save | Open the course once in Greenkeeper (its load dialog lists `.gspcrse`, it reads the same-named GKD beside it) and save from there; that writes coursedetails and scorecards | 14 |
| GKD fields | Designer "jboshers", altitude in feet, course name and "City, State, USA", CourseInfo description (designer, year, setting, character), ratings per tee (SCGA), `DefaultStimp 2` / `MaxStimp 5`, unused tee slots `Enabled: false` | 10, 02 |
| Cache | GSPro.db caches the course entry: a renamed or re-rated course needs the GKD fixed and a Greenkeeper re-save; delete `image_altered*` after a splash fix | checklist |
| Backup / rollback | Before the copy: zip the existing course folder to `Backups\gspro_<date>\`; after: keep the previous `.gspcrse` and GKD as `*.prev` for one push. Rollback = put the zip back, no Greenkeeper step needed | A 10/31 |
| Playtest | Hit one shot into every drop-zone hazard, play one full hole per nine from each tee colour, check the flyover start on 3 holes, the sign readability, and the bunker lie; log in `after_playtest_<date>/README.md` | playtest P1-P2 |
| Pins | GSPro's lie-angle warnings on every pin: none red; inner ring green, outer ring on the green (the pin placement guide); our own ≤3% test runs first (02) | beta checklist |

## Procedure: local push

1. A saves the scene; gate checks (00) at zero; backups taken.
2. Batch build → `.gspcrse`; copy with the GKD and splash into the course folder (BoJa's go first).
3. Greenkeeper: open, run the checks below (Base, OOB, Hazards, Tees, Pins, Conditions, Scorecard, Collider Inspector), save.
4. GSPro: load the course, playtest list, log findings, fix in A, repeat.
5. Rollback if anything in play is worse than the previous push (a push is never "mostly better").

## Pre-beta gate (the OPCD Beta Submission Checklist, as our checks)

Visuals:
- Tree billboard colours match the highest LOD of the trees; LOD transitions smooth and barely noticeable, billboards included (06, 15: we remove the desktop billboard LOD and add leaf spheres, so check the far transition is still clean).
- Smooth terrain transitions under bridges, houses and buildings (no steps or gaps at footings).
- No grasses or trees on cart paths (plants-on-turf check extended to paths).
- Water reflections from the common viewpoints line up with the surrounding objects (reflection probe placement).
- Cart paths flat and smooth (09: lifted, no z-fighting, no steps at junctions).
- Tee markers on the tee box, not hovering or sunken (05).
- Detail grass (3DG) blends into the terrain and matches the theme; mesh materials and vegetation complementary (12).
- Wind: trees, plants and grasses move consistently (one scene wind zone, Arboreum/SpeedTree wind not baked, 15).
- Overall colours natural, not over-saturated (12: the plain look, measured ratios).
- Physics visible: deep rough looks like deep rough, sand like sand (04, 03).
- V4 blend materials applied correctly, soft transitions (02 edge blend, 04).

Greenkeeper:
- **Base**: splash in the course folder as JPG; lat/long points to the course; every other field correct.
- **OOB**: correct, never crossing a hazard. Water partly in and partly out of bounds: move the OOB so all the water is in bounds and fix the hazard, or make the water wholly OOB and remove its mesh collider so drops work.
- **Hazards**: no overlaps with other hazards or OOB; every water body wholly inside its hazard. Use Tools → Collider Inspector (right-click to refresh the view while flying).
- **Tees**: click through every camera icon, move to each tee, click-and-hold to confirm nothing blocks the game camera, with Trees Generated / Vspro / Arborist enabled (13 first-frame rule).
- **Pins**: no red lie-angle warnings; minimal red in the outer ring, inner ring green, outer ring on the green where size allows; aim for stimp 13 where the greens allow it.
- **Conditions**: all settings correct, Max Stimp consistent with the pin settings.
- **Scorecard**: every section filled, ratings set, all male ratings used and appropriate; splash shows in the scorecard image.
- **Collider Inspector**: from the 1st tee through the course: green = wood, blue = leaves, pink = meshes, yellow = timber/stone. Red = collider with no physics material (fix); fully visible mesh = probably no collider (check walls and bridges from several angles); orange = a mesh collider set to Leaves, which is not allowed: change to capsule/sphere.
- **Benchmark**: Tools → Benchmark at 1080p or 4K with a "target FPS" shown (not "GPU not supported"); 100/100 average and min is the ideal; anything lower gets investigated by area (SpeedTree hero trees and dense shrub chunks are the usual suspects, 15).

## Checks (ours, before the community's)

- Gate set at zero on every hole (00); shot sweep clean (06); no magenta; prop placement clean (11).
- Every tee/pin/aim/DZ on its surface and in bounds; hazard count and pOOB under the limits (10).
- Flyover first frames clear on all 18 (13).
- Splash is the course's own photo; description sourced; Designer "jboshers".

## Pitfalls

- A Greenkeeper save was skipped and coursedetails/scorecards were missing in GSPro.
- Unused tee slots without `Enabled: false` showed "500 yds par 0".
- Drop-zone hazards with innerOOB ticked only offered re-hits (10).
- Sideways splash from an un-rotated phone photo; cache kept the old one.
- A push without a backup made a rollback a rebuild.

## Decision points for BoJa

1. Which push is the first beta submission (default: after the Tierra visit data is in and all 18 holes pass the gate).
2. Target stimp for beta (default: our stimp 10 setting unless the pin test allows 13).

## Sources

OPCD Beta Submission Checklist (BoJa, 10/31, from the Unity A thread); `tierra-rejada-lidar/HANDOFF.md` (Greenkeeper lessons); `progress/tierra/after_playtest_1031/README.md`; checklist "OB, hazards and drop zones", "Setup and process".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-publish` but not in this playbook when the skill was cut to one page.


### Defaults that worked
- **Build**: Unity batch build of the asset bundle from editor A's saved scene; `.gspcrse` beside the GKD. A rebuild replaces only `.gspcrse`; GKD and other files stay.
- **Course folder**: `.gspcrse`, `<Course>.GKD`, splash `.jpg` (EXIF-rotated, GSPro's exact aspect), coursedetails.txt and scorecards (written by Greenkeeper's own save).
- **Greenkeeper save**: open the course once in Greenkeeper (its dialog lists `.gspcrse`; it reads the same-named GKD beside it), run the checks below, save from there.
- **GKD fields**: Designer "jboshers", altitude in feet, "City, State, USA", sourced CourseInfo description, SCGA ratings per tee, `DefaultStimp 2` / `MaxStimp 5`, unused tee slots `Enabled: false` (else "500 yds par 0").
- **Cache**: GSPro.db caches the course entry; a renamed or re-rated course needs the GKD fixed and a Greenkeeper re-save; delete `image_altered*` after a splash fix.
- **Backup / rollback**: zip the existing course folder to `Backups\gspro_<date>\` before the copy; keep the previous `.gspcrse` and GKD as `*.prev`. Rollback = restore the zip. A push is never "mostly better".
- **Playtest**: one shot into every drop-zone hazard; one full hole per nine from each tee colour; flyover start on 3 holes; sign readability; bunker lie; logged in `after_playtest_<date>/README.md`.

### Procedure: local push
1. A saves; gate checks at zero; backups taken.
2. Batch build; copy `.gspcrse` + GKD + splash into the course folder (BoJa's go first).
3. Greenkeeper checks and save.
4. GSPro playtest; fix in A; repeat. Roll back if play is worse anywhere.

### Pre-beta gate (OPCD Beta Submission Checklist)
Visuals: tree billboard colours match the highest LOD; LOD transitions smooth, billboards included; no grass or trees on cart paths; smooth terrain under bridges and buildings; water reflections line up from common viewpoints; cart paths flat and smooth; tee markers on the box, not hovering or sunken; detail grass blends into the terrain and the theme; materials and vegetation complementary; wind consistent on trees, plants and grass; colours natural, not over-saturated; physics visible (deep rough looks like deep rough); V4 blend materials applied with soft transitions.
- **Base**: splash in the folder as JPG; lat/long points to the course; other info correct.
- **OOB**: never crosses a hazard. Water partly OOB: move the OOB to keep all the water in bounds and fix the hazard, or make it wholly OOB and remove its mesh collider.
- **Hazards**: no overlaps with hazards or OOB; every water body wholly inside its hazard (Tools → Collider Inspector; right-click to refresh while flying).
- **Tees**: click every camera icon, move to the tee, click-and-hold to confirm nothing blocks the game camera, with Trees Generated / Vspro / Arborist on.
- **Pins**: no red lie-angle warnings; inner ring green, outer ring on the green; stimp 13 where possible.
- **Conditions**: Max Stimp consistent with the pins.
- **Scorecard**: all sections, ratings set, all male ratings used; splash visible in the scorecard image.
- **Collider Inspector** from the 1st tee: green = wood, blue = leaves, pink = meshes, yellow = timber/stone. Red = collider without physics material (fix); fully visible mesh = probably no collider; orange = mesh collider set to Leaves (not allowed; use capsule/sphere).
- **Benchmark**: Tools → Benchmark at 1080p or 4K with a target FPS shown; 100/100 average and min is the ideal; investigate lower by area.

### Checks (ours, first)
- Gate set at zero per hole; shot sweep clean; no magenta; prop placement clean.
- Every tee/pin/aim/DZ on its surface and in bounds; hazard count ≤120 and pOOB ≤~880 points.
- Flyover first frames clear on all 18.
- Splash is the course's own photo; description sourced.

### Pitfalls
- Skipped Greenkeeper save: no coursedetails or scorecards in GSPro.
- DZ hazards with innerOOB ticked only offered re-hits.
- Sideways splash from an un-rotated phone photo; the cache kept the old one.

### Decision points (ask once, with a default)
1. Which push is the first beta submission (default: after the visit data is in and all 18 pass the gate).
2. Target stimp for beta (default: our stimp 10 unless the pin test allows 13).
