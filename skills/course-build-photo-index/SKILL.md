---
name: "course-build-photo-index"
description: "Use when a photo or scan dump lands for a course, or when auditing a hole against photos: folder layout, index.csv, pose solving and the verdict-per-photo audit."
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

## Procedure: the audit per hole

1. Inventory from the photos, object by object, each with a verdict against the build.
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