# 17. Photo index and audit

Use this the day a photo dump lands (a phone download, a drone folder, Street View grabs, scans) and whenever a hole is reviewed against photos. The aim is one index that says, for every image, which hole and which spot on the hole it shows, so a build runs tee to green from the photos without anyone re-sorting them.

## Folder layout (project files share, one per course)

```
<course>/course_photos/
  README.md                 what each batch is, date, who shot it, phone model
  index.csv                 one row per image (columns below)
  decisions.csv             LiDAR review answers per hole (20)
  rangefinder.csv           hole, tee, target, yards, plays_like, pin_pos, date
  visit_notes.md            anything a photo can't carry
  unsorted/                 raw dumps land here, never built from
  hole_01/ … hole_18/
    tee/                    per box: 01_black_tee_IMG_4641.jpg …
    fairway/                landing zone, mid hole
    bunkers/                b1_, b2_ … numbered from the tee, fairway side then green side
    green/                  collar, edges, from each pin toward the tee
    surrounds/              banks, hillsides, toe lines
    water/
    props/                  signs, bins, benches, enclosures, buildings
    trees/                  hero trees, one file per tree
    scans/                  Gxx_green.las, tee and bunker LAS
    pairs/                  photo | before | after sheets (rendered, not shot)
  course/                   clubhouse, entrance, range, practice green, skyline, card
  drone/by_hole/H01…        drone frames if any
```

File names: `<hole>_<spot>_<orig>.jpg` where spot is `tee_black`, `tee_blue`, `fw1`, `b2_face`, `green_collar_n`, `bank_w`, `sign`, `tree_03`. Keep the original IMG number at the end so EXIF and the index always match.

## index.csv

`hole, img, file, lat, lon, alt, heading, pitch, fov, taken_at, gps_confidence, spot, subject, dist_to_hole_line_m, pose_x, pose_z, pose_y, pose_yaw, pose_pitch, pose_conf, verdict, notes`

- `gps_confidence`: high (within 5 m of a plausible standing spot on the hole), medium, low (40-80 m off, cart-ride first shot), unknown (no EXIF GPS).
- `spot` and `subject` from the capture list in 16 (tee, bunker face, collar, hillside, prop, tree).
- `pose_*` filled by the pose solver (below), `pose_conf` high/medium/weak; only high and medium are used for verdicts.
- `verdict`: match / mismatch / fixed / n/a, with the mismatch written in `notes`.

## Procedure: indexing a dump

1. Copy the dump to `unsorted/<date>/`; never rename in place.
2. `photo_index.py` reads EXIF (GPS, heading, time, FOV from the camera model), projects each GPS into the course frame (EPSG:6340 → Unity x/z), assigns the nearest hole by distance to the hole line, and writes a draft `index.csv` plus a contact sheet per hole with the satellite crop and the photo positions numbered.
3. Fix the assignment by content where GPS is low or unknown: the sign in frame, the bunker count, the sun direction and the skyline settle most of them (Tierra: 4800-4803 were off-under-trees and judged by content).
4. Move each file into its hole and spot folder with the standard name; the index row keeps both names.
5. Scans: one row per LAS with the green it covers and its fit result once registered (16).
6. Commit the index and the sheets; the raw dump stays on the share only.

## Procedure: pose solving (for pairs)

1. Start from GPS + EXIF heading, same FOV as the phone; terrain height at the pose from the LiDAR (must match within 2 cm or the frame is wrong).
2. Refine against the LiDAR by skyline fit and landmarks (sign post, path crest, bunker lip, tee markers, building corner); a grid search over ±6 m and ±30° picks the pose, content decides when the search isn't decisive. EXIF heading alone is never enough (D13).
3. Log the solved pose in the index; flag "weak until <feature> exists there" when the only landmark is something not yet built (hole 10 scrub).
4. Render photo | before | after from the solved pose, same FOV, one edit at a time (13).

## Procedure: the audit per hole

1. Inventory from the photos, object by object (trees, shrubs, beds, grass, fences, walls, rocks, signs, fixtures, water, bunkers), each with a verdict against the current build.
2. Rank the mismatches into the hole's top-8 list (00); biggest visual gaps first (hillsides and species were the top two on every Tierra hole).
3. Judge by measurement, not impression: cover share, colour ratios r/g and b/g in same-light boxes (12), positions in metres, face height over width for bunkers.
4. After the edits, re-render the same poses; a photo is `fixed` only when its pass rule (20) is met. BoJa never sees a flyover before every high/medium pose on the hole has a verdict.

## Checks

- Every image in a hole folder has an index row; no row without a file.
- No build input taken from `unsorted/`.
- Pose convention check once per course (Euler angles vs forward vectors agree).
- Photos with the same spot and 10 s apart agree on GPS within 5 m, else `gps_confidence=low`.

## Pitfalls

- GPS slip put a 160 m rope on the wrong hole (Tierra 10 vs 18); check the hole by content before building anything long from one photo.
- A tee set at unresolved headings (13) and a tee burst 80 m away (10 Blue): content beats GPS.
- A 2 m pose error made a sign look edge-on and "missing" (11); re-pose before rebuilding.
- Photo poses fitted to a mis-placed prop inherit its error (the 14 enclosure, 9.8 m off); fit to things that are already registered.
- Low morning sun shifts r/g on peppers and scrub; compare same-light frames only.
- Online flyover videos and Street View are reference for layout and props, not for colour or for anything in play; note their date.

## Decision points for BoJa

1. Where the share lives per course and who dumps the phone (default: BoJa dumps to `unsorted/`, the cloud side indexes).
2. Whether drone frames are shot (one pass per hole at 30-40 m, nadir and oblique, is the most useful thing for D2, D4 and D8).

## Sources

`progress/tierra/audit_1030/index.csv` and `review_*.md`, `audit_photo_pairs/AUDIT_FINDINGS_1030.md`, `gate/hole_11/edit_list.md` (Step 0 pose table), `holes/hole_10_state.md`, `photo_audit_1029/audit_1029.csv`, `MORNING_1030.md` (far trees with no GPS), LiDAR detail review D13 and section 4.

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-photo-index` but not in this playbook when the skill was cut to one page.


### Folder layout (project files share, one per course)
  README.md        batches: date, who shot, phone model
  hole_01 … hole_18/
    tee/ fairway/ bunkers/ green/ surrounds/ water/ props/ trees/ scans/ pairs/
File names: `<hole>_<spot>_<orig>.jpg` (spot = tee_black, fw1, b2_face, green_collar_n, bank_w, sign, tree_03); keep the original IMG number so EXIF and the index always match.

### index.csv
- gps_confidence: high (within 5 m of a plausible spot), medium, low (40-80 m off, first shot after a cart ride), unknown (no EXIF GPS).
- pose_conf high/medium only are used for verdicts; verdict = match / mismatch / fixed / n/a with the mismatch in notes.

### Procedure: indexing a dump
1. Copy to `unsorted/<date>/`; never rename in place.
2. `photo_index.py`: EXIF (GPS, heading, time, FOV from the camera model) → course frame (EPSG:6340 → Unity x/z) → nearest hole by distance to the hole line → draft index + a contact sheet per hole on the satellite crop.
3. Fix assignments by content where GPS is low or unknown (sign in frame, bunker count, sun direction, skyline).
4. Move files into hole/spot folders with the standard name; the row keeps both names.
5. Scans: one row per LAS with the green it covers and its fit result.
6. Commit the index and sheets; the raw dump stays on the share.

### Procedure: pose solving
1. GPS + EXIF heading, same FOV as the phone; terrain height at the pose from the LiDAR (within 2 cm or the frame is wrong).
2. Refine against the LiDAR by skyline fit and landmarks (sign post, path crest, bunker lip, tee markers, building corner); grid search ±6 m, ±30°; content decides when the search isn't decisive. EXIF heading alone is never enough.
3. Log the pose; flag "weak until <feature> exists there".
4. Render photo | before | after from the solved pose, one edit at a time.

### Procedure: the audit per hole
1. Inventory from the photos, object by object, each with a verdict against the build.
2. Rank the mismatches into the hole's top-8 (hillsides and species were the top two on every Tierra hole).
3. Judge by measurement: cover share, r/g and b/g in same-light boxes, positions in metres, bunker face height over width.
4. Re-render the same poses after the edits; a photo is `fixed` only when its pass rule is met. BoJa never sees a flyover before every high/medium pose on the hole has a verdict.

### Checks
- Two photos at the same spot 10 s apart agree on GPS within 5 m, else low.

### Pitfalls
- A GPS slip put a 160 m rope on the wrong hole; check the hole by content before building anything long from one photo.
- Content beats GPS: a tee set at unresolved headings, a tee burst 80 m away.
- A 2 m pose error made a sign look edge-on and "missing"; re-pose before rebuilding.
- Poses fitted to a mis-placed prop inherit its error; fit to registered things.
- Low morning sun shifts r/g on peppers and scrub; same-light frames only.
- Online flyover videos and Street View are reference for layout and props, not colour or anything in play; note their date.

### Decision points (ask once, with a default)
1. Who dumps the phone and where (default: BoJa to `unsorted/`, the cloud side indexes).
2. Whether drone frames are shot (one pass per hole at 30-40 m, nadir and oblique).
