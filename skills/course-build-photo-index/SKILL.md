---
name: "course-build-photo-index"
description: "Use when a photo or scan dump lands for a course, when shaping any feature from photos after the LiDAR and satellite base, or when auditing a hole against photos: folder layout, index.csv, pose solving, the photo enhancement procedure and the audit."
---

# Photo index and audit

Use with `course-build-gates` and `course-build-flyovers`. Full guide: `course-build/playbooks/17_photo_index_and_audit.md` in the moorpark repo. Registration rule: `playbook/LIDAR_DETAIL_REVIEW.md` D13.

## Folder layout (project files share, one per course)

```
<course>/course_photos/
  README.md        batches: date, who shot, phone model
  index.csv        one row per image
  decisions.csv    LiDAR review answers per hole
  rangefinder.csv  hole, tee, target, yards, plays_like, pin_pos, date
  enhance.csv      one row per photographed feature (the enhancement record below)
  visit_notes.md
  unsorted/        raw dumps land here, never built from
  hole_01 … hole_18/
    tee/ fairway/ bunkers/ green/ surrounds/ water/ props/ trees/ scans/ pairs/
  course/          clubhouse, entrance, range, practice green, skyline, card
  drone/by_hole/H01…
```

File names: `<hole>_<spot>_<orig>.jpg` (spot = tee_black, fw1, b2_face, green_collar_n, bank_w, sign, tree_03); keep the original IMG number so EXIF and the index always match.

## index.csv

`hole, img, file, lat, lon, alt, heading, pitch, fov, taken_at, gps_confidence, spot, subject, dist_to_hole_line_m, pose_x, pose_z, pose_y, pose_yaw, pose_pitch, pose_conf, verdict, notes`

- gps_confidence: high (within 5 m of a plausible spot), medium, low (40-80 m off, first shot after a cart ride), unknown (no EXIF GPS).
- pose_conf high/medium only are used for verdicts; verdict = match / mismatch / fixed / n/a with the mismatch in notes.
- Every photo is linked to its location (pose_x, pose_z, pose_y), direction (pose_yaw, pose_pitch, fov), the feature(s) it shows (`feature_id` from the manifest, `;`-separated) and its confidence (gps_confidence, pose_conf). Extra columns: `reviewed` (yes/no), `reviewed_build` (the build it was compared with), `supports` (the enhance.csv rows and decisions.csv points it backs). A photo never reviewed is `reviewed = no`, never blank.
- **Coverage map.** Per hole, features and areas that no high/medium pose sees are listed `unverified` on the hole's sheet and in the review, never folded into a pass. (Tierra 1117f: holes 2-8 have no indexed photos, all 475 rows still point into `unsorted/`, and none has a verdict, subject or feature.)

## Procedure: indexing a dump

1. Copy to `unsorted/<date>/`; never rename in place.
2. `photo_index.py`: EXIF (GPS, heading, time, FOV from the camera model) → course frame (EPSG:6340 → Unity x/z) → nearest hole by distance to the hole line → draft index + a contact sheet per hole on the satellite crop.
3. Fix assignments by content where GPS is low or unknown (sign in frame, bunker count, sun direction, skyline).
4. Move files into hole/spot folders with the standard name; the row keeps both names.
5. Scans: one row per LAS with the green it covers and its fit result.
6. Commit the index and sheets; the raw dump stays on the share.

## Procedure: pose solving

1. GPS + EXIF heading, same FOV as the phone; terrain height at the pose from the LiDAR (within 2 cm or the frame is wrong).
2. Refine against the LiDAR by skyline fit and landmarks (sign post, path crest, bunker lip, tee markers, building corner); grid search ±6 m, ±30°; content decides when the search isn't decisive. EXIF heading alone is never enough.
3. Log the pose; flag "weak until <feature> exists there".
4. Render photo | before | after from the solved pose, one edit at a time.

## Procedure: step 0, overlay before building (BoJa 11/18)

Before a feature is built or rebuilt, overlay its proposed outline or route on the aerial and the survey, and on the matched photo views from solved poses. Quantify the difference: route offset per station, footprint area and edge offset per side, each with a tolerance taken from the evidence (aerial trace about 1 m; QL2 LiDAR about 0.5 m in plan; photo traces from their pose error). Record the numbers in `enhance.csv`. Photo coverage, a photo in frame or a colour match does not count as a successful match; only a measured difference inside tolerance does. (Tierra 13: the photo audit compared turf colour only, and the stream was built 1.8 m mean / 5.2 m max off the aerial channel.)

## Procedure: photo enhancement (every feature, after the base) (BoJa 11/16)

One process for every feature type. LiDAR and satellite lay the broad base first; then photos and BoJa's own description set the detail. Feature skills point here instead of improvising their own photo step. A feature with usable photos is not "built" until this is done.

1. **Pull.** For one feature (manifest ID from `course-build-pipeline`): every index row whose solved pose sees it (high/medium pose_conf, in frame by heading and FOV, within range), plus everything BoJa has said about it (thread messages, `visit_notes.md`, `decisions.csv`). Write the row in `enhance.csv`: hole, feature_id, type, photo files, BoJa's notes quoted, observations, proposed changes, confidence, status `pulled`.
2. **Overlay.** Render the base from each pose (photo | base, same FOV). Trace the feature on the photo: its edges, where cover starts and stops, the top and toe of slopes, the lip line. Project the traced lines onto the LiDAR terrain from the pose to get world positions; cross-check with a second photo from another angle where one exists.
3. **Measure the feature, never its faults.** Record directly visible layout (edges, where cover starts and stops, what is present) separately from inferred dimensions (heights, angles, widths), each with a confidence. Use a known-size reference only when its size is confirmed for this course (a measured flagstick, the course's own tee marker, a path width from the LiDAR or OSM); an assumed size is labelled as an assumption. Values per feature type:
   - hillside / brush / deep rough: extent polygon, offset from the fairway edge per side, species mix, cover %, bare-soil share, plant height, r/g and b/g in same-light boxes
   - trees: position, species, height and crown against the LiDAR tree, missing or extra trees
   - bunkers: high side, face height and angle per side, sand flash, lip height, floor low point, the surround leading in
   - tees: decks, deck outline, bank height and slope per side, where cover starts, beds, steps, walls
   - green surrounds: collar width, bank slope behind and beside, approach cut (never the green surface)
   - water: edge position, bank material, reed band, edging
   - paths, fences, props: as `course-build-paths-fences` and `course-build-props`
   Faults are never measured into a build: divots, ball marks, worn, bare or dead grass, repairs, debris and temporary works are left out on tees, fairways, greens, collars, bunkers and in-play rough (`course-build-gates` rule 2). Bare soil only out of play, where it is really there.
4. **Write build inputs.** The values go into the input file the feature's skill builds from (`bunkers.csv`, `trees.csv`, mask edits, tee pad specs, rough edge offsets), each value tagged with its photo IDs. The existing authority rules decide conflicts: the satellite confirms outlines, a clean scan wins on heights, protected green geometry (surface, contours, pins) never comes from photos and a green outline changes only on BoJa's explicit answer (`course-build-greens` step 6b), LiDAR wins water level and fairway relief, and BoJa's decisions stand. A photo does not automatically override measured geometry. Conflicting evidence stays `open` with a documented provisional choice built meanwhile and one question to BoJa; insufficient evidence is `provisional base`, with the reason (step 5). A reviewed base that already matches the evidence stays unchanged and is recorded `verified unchanged` with the views that show it.
5. **Close out the whole feature.** Review the complete feature, its joins to neighbouring surfaces and the surrounding ground the work touched. Check both photo agreement (an explicit visual verdict per photo against the step 3 values) and visible defects: gaps, exposed triangles, abrupt ledges, artificial patches, distorted turf. Use representative views from different sides; add a moving inspection where stills can't establish continuity. Record what improved and what remains unresolved, including defects older than this change. Status is exactly one of:
   - `verified unchanged`: the reviewed base already matches the evidence; supporting views listed
   - `changed and verified`: changed from the evidence and checked
   - `provisional base`: insufficient evidence, with the reason
   - `open`: mismatch, defect or incomplete verification
   Coverage percentages describe progress only; they never override an unresolved material defect.

## Revision evidence

- Every comparison and test report is labelled with the exact build it describes (candidate name, scene and dependency hashes).
- Revision reviews show the previous candidate beside the current candidate from the same camera, with the source photo separately where relevant.
- Earlier screenshots or test results are never evidence for a changed build unless their age and applicability are stated.
- Missing, failed and not-run checks are reported as three different results, never folded into a pass.

## Procedure: the audit per hole

1. Inventory from the photos, object by object, each with a verdict against the build. The audit is the last check after the enhancement procedure above, not a substitute for it.
2. Rank the mismatches into the hole's top-8 (hillsides and species were the top two on every Tierra hole).
3. Judge by measurement: cover share, r/g and b/g in same-light boxes, positions in metres, bunker face height over width.
4. Re-render the same poses after the edits; a photo is `fixed` only when its pass rule is met. BoJa never sees a flyover before every high/medium pose on the hole has a verdict.

## Checks

- Every image in a hole folder has an index row; no row without a file.
- No build input taken from `unsorted/`.
- Pose convention check once per course (Euler angles vs forward vectors agree).
- Two photos at the same spot 10 s apart agree on GPS within 5 m, else low.

## Pitfalls

- A GPS slip put a 160 m rope on the wrong hole; check the hole by content before building anything long from one photo.
- Content beats GPS: a tee set at unresolved headings, a tee burst 80 m away.
- A 2 m pose error made a sign look edge-on and "missing"; re-pose before rebuilding.
- Poses fitted to a mis-placed prop inherit its error; fit to registered things.
- Low morning sun shifts r/g on peppers and scrub; same-light frames only.
- Online flyover videos and Street View are reference for layout and props, not colour or anything in play; note their date.

## Decision points (ask once, with a default)

1. Who dumps the phone and where (default: BoJa to `unsorted/`, the cloud side indexes).
2. Whether drone frames are shot (one pass per hole at 30-40 m, nadir and oblique).