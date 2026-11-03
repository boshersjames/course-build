---
name: "course-build-publish"
description: "Use when pushing a course into GSPro: local install, Greenkeeper save, rollback, playtest, and the OPCD beta-submission gate."
---

Rules file: `rules/publish.json` (every number below is defined there; read it with `tools/rules.py publish`). Course overrides: `course.json` → `rule_overrides.publish`.

# Publish to GSPro

Use with `course-build-gates` and `course-build-ob`. Full guide: `course-build/playbooks/21_publish_to_gspro.md` in the moorpark repo. Nothing goes into `C:\GSProV1\Core\GSP\Courses\` without BoJa's go.

## Defaults that worked

- **Build**: Unity batch build of the asset bundle from editor A's saved scene; `.gspcrse` beside the GKD. A rebuild replaces only `.gspcrse`; GKD and other files stay.
- **Course folder**: `.gspcrse`, `<Course>.GKD`, splash `.jpg` (EXIF-rotated, GSPro's exact aspect), coursedetails.txt and scorecards (written by Greenkeeper's own save).
- **Greenkeeper save**: open the course once in Greenkeeper (its dialog lists `.gspcrse`; it reads the same-named GKD beside it), run the checks below, save from there.
- **GKD fields**: Designer "jboshers", altitude in feet, "City, State, USA", sourced CourseInfo description, SCGA ratings per tee, `DefaultStimp 2` / `MaxStimp 5`, unused tee slots `Enabled: false` (else "500 yds par 0").
- **Cache**: GSPro.db caches the course entry; a renamed or re-rated course needs the GKD fixed and a Greenkeeper re-save; delete `image_altered*` after a splash fix.
- **Backup / rollback**: zip the existing course folder to `Backups\gspro_<date>\` before the copy; keep the previous `.gspcrse` and GKD as `*.prev`. Rollback = restore the zip. A push is never "mostly better".
- **Playtest**: one shot into every drop-zone hazard; one full hole per nine from each tee colour; flyover start on 3 holes; sign readability; bunker lie; logged in `after_playtest_<date>/README.md`.

## Procedure: local push

1. A saves; gate checks at zero; backups taken.
2. Batch build; copy `.gspcrse` + GKD + splash into the course folder (BoJa's go first).
3. Greenkeeper checks and save.
4. GSPro playtest; fix in A; repeat. Roll back if play is worse anywhere.

## Pre-beta gate (OPCD Beta Submission Checklist)

Visuals: tree billboard colours match the highest LOD; LOD transitions smooth, billboards included; no grass or trees on cart paths; smooth terrain under bridges and buildings; water reflections line up from common viewpoints; cart paths flat and smooth; tee markers on the box, not hovering or sunken; detail grass blends into the terrain and the theme; materials and vegetation complementary; wind consistent on trees, plants and grass; colours natural, not over-saturated; physics visible (deep rough looks like deep rough); V4 blend materials applied with soft transitions.

Greenkeeper:
- **Base**: splash in the folder as JPG; lat/long points to the course; other info correct.
- **OOB**: never crosses a hazard. Water partly OOB: move the OOB to keep all the water in bounds and fix the hazard, or make it wholly OOB and remove its mesh collider.
- **Hazards**: no overlaps with hazards or OOB; every water body wholly inside its hazard (Tools → Collider Inspector; right-click to refresh while flying).
- **Tees**: click every camera icon, move to the tee, click-and-hold to confirm nothing blocks the game camera, with Trees Generated / Vspro / Arborist on.
- **Pins**: no red lie-angle warnings; inner ring green, outer ring on the green; stimp 13 where possible.
- **Conditions**: Max Stimp consistent with the pins.
- **Scorecard**: all sections, ratings set, all male ratings used; splash visible in the scorecard image.
- **Collider Inspector** from the 1st tee: green = wood, blue = leaves, pink = meshes, yellow = timber/stone. Red = collider without physics material (fix); fully visible mesh = probably no collider; orange = mesh collider set to Leaves (not allowed; use capsule/sphere).
- **Benchmark**: Tools → Benchmark at 1080p or 4K with a target FPS shown; 100/100 average and min is the ideal; investigate lower by area.

## Checks (ours, first)

- Gate set at zero per hole; shot sweep clean; no magenta; prop placement clean.
- Every tee/pin/aim/DZ on its surface and in bounds; hazard count ≤120 and pOOB ≤~880 points.
- Flyover first frames clear on all 18.
- Splash is the course's own photo; description sourced.

## Pitfalls

- Skipped Greenkeeper save: no coursedetails or scorecards in GSPro.
- DZ hazards with innerOOB ticked only offered re-hits.
- Sideways splash from an un-rotated phone photo; the cache kept the old one.
- A push without a backup made a rollback a rebuild.

## Decision points (ask once, with a default)

1. Which push is the first beta submission (default: after the visit data is in and all 18 pass the gate).
2. Target stimp for beta (default: our stimp 10 unless the pin test allows 13).
