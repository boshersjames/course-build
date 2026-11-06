# Photos: from a phone dump to a scene that matches them

What this is for: every build decision about what a hole looks like is checked against a photo taken at a known spot, and the
check is a render from the same spot, not a memory of the place. This page is the whole loop for a new course: where the files go,
how a photo becomes a hole and a camera position, how Unity renders that position, and how the comparison is read and acted on.

The parts are already code. Nothing here is a manual step except shooting the photos and reading the comparison sheets.

## 1. Storage: one R2 bucket per course, git keeps the index

- Originals (HEIC, JPG, LAS scans, MP4 drone video) live in a Cloudflare R2 bucket, one per course, named after the course:
  `tierra-rejada`, `moorepark`, `hansen-damn`, `penmar`, `angeles-national`, `harding`, `wilson` (the names are what was created; the
  course.json points at the real one, misspellings included).
- The bucket mirrors the course folder: `course_photos/unsorted/<date>/IMG_4929.HEIC`, `course_photos/scans/<name>.las`,
  `course_photos/drone/<video>.mp4`, `course_photos/renders/<run>/photo_IMG_4929.png`.
- Git keeps everything derived and small: `course_photos/index.csv`, the contact sheets, the audit sheets' index, the verdicts.
  Originals never enter git.
- `course.json` carries the pointers: `paths.photos_root` (`course_photos`), `paths.photo_store` (`r2://<bucket>`),
  `paths.photo_store_account` (the R2 account id). Credentials never live in a repo: on the PC they are `R2_ACCESS_KEY_ID` and
  `R2_SECRET_ACCESS_KEY` in the environment; in the cloud they are environment variables on the Claude environment.
- `tools/photo_sync.py push|pull|ls|check` moves files both ways, new or changed only, by hole or by image number on pull.

## 2. Indexing: a photo becomes a hole, a spot and a camera

`python ../course-build/tools/photo_index.py --dump course_photos/unsorted/<date> --sheets --shots`

For every image it reads the EXIF and writes one row in `course_photos/index.csv`:

- Position: GPS latitude and longitude to the course frame (EPSG:6340 to Unity x, z), altitude kept.
- Hole: the nearest hole line (back tee, aim points, green centre) within `photos.hole_assignment.corridor_m` (70 m); a second
  hole within the same distance is recorded so a photo between two holes can be moved by content.
- Confidence: `high` when a photo taken within 15 s agrees within 5 m, `medium` for a single shot, `low` for the first shot after a
  cart ride, `unknown` with no GPS (`photos.gps_confidence`).
- Camera: heading from the EXIF compass plus the course's magnetic declination, field of view from the focal length, eye height
  1.6 m on the LiDAR (`photos.pose`). That is the draft pose: `pose_x, pose_z, pose_y, pose_yaw, pose_pitch, pose_conf`.
- Spot: filled later by geometry (`coverage.py`: nearest tee, green or bunker within 25 m, else fairway) or by hand.

It also writes a contact sheet per hole over the satellite crop (`sheets/hole_NN.jpg`), JPEG display copies, and the render shot
list `shots_photo_poses.json`: one Unity camera per high or medium pose. That file is how Unity uses the photos.

Then `python ../course-build/tools/coverage.py`: which items on the capture checklist each hole has a photo or scan behind it
(tees, landing zone, each bunker, green, collar, surrounds, water, trees, props), and the holes under 80% as the next visit list.

## 3. What Unity does with them: it renders the photo's camera, nothing else

Unity never reads a photo. The shot list from step 2 is copied into the project's automation config and rendered in one batch
(`Run-CourseAutomation.ps1 -Config automation\<Course>PhotoPoses.json`, Unity closed, 15 minutes for 475 poses on Tierra), one PNG
per photo named `photo_<IMG>.png`, at the photo's position, heading and field of view. The renders are pushed to the bucket with
`photo_sync.py push --what renders` so the cloud can read them.

So the mapping photo to scene is the camera pose in the index, keyed by image number and hole. A photo at the 16th green's front
produces a render from the 16th green's front, and the two sit side by side in the next step.

## 4. The audit: photo beside render, measured, then read

`python ../course-build/tools/audit_pairs.py --renders latest [--holes 16]`

For every indexed photo with a render it composes `course_photos/audit/<hole>/<IMG>.jpg`, photo on the left, render on the right,
and measures the turf colour in both with the same mask, reporting the r/g and b/g gap against `photos.audit.colour_gap_max`.
That is the measured half. The other half is reading the sheet for content: what is in the photo and not in the render, what is in
the render and not in the photo, trees, props, bunker shapes, collar widths, path edges. The verdict column in the index takes
`match`, `mismatch`, `fixed` or `n-a`, with the finding and the image number in the notes.

The rule that came out of Tierra 16 on 11/4: no edit at a spot until the photos aimed at that spot have been read, and the finding
cites the image number. The willow in front of the 16th green was moved by a geometry tool before anyone looked at IMG_4929, taken
20 m short of that green, which shows no tree there. The photo was in the index with a solved pose the whole time.

## 5. Acting on a finding, and closing the loop

- A finding names an object or a surface and a position (the pose gives the position within a few metres; `CourseSceneOps
  FindNear`, `FindPlants`, `FindOnHole` name the object; `TerrainReport` covers painted terrain details, which are not objects).
- The fix is the matching tool, logged and revertable: `SetOff` for a prop or tree that is not in the photo, the rim and collar tools
  for an edge, the brush sweep for painted scrub on turf, the cart path check for turf over a path.
- Only the poses that look at that spot are re-rendered (`--holes` on the audit, the shot list filtered by hole), the sheet is read
  again, and the row becomes `fixed` when the pass rule is met. BoJa sees a flyover of a hole only when every high and medium pose on
  it has a verdict.

## 6. Scans and drone video in the same index

- Green scans (LAS from the phone): `scan_index.py` writes one row per scan with the green it covers and its fit verdict; acceptance is
  5 cm MAD inside the green outline (`photos.scan`). A scanned green is built from the scan and never contoured from LiDAR again.
- Online drone flyovers: `drone_frames.py fetch|segments|cut` pulls frames per hole from a timed segment list into the same index at
  low confidence; layout and props reference only, never colour or anything in play, and the date is noted.

## 7. A new course, in order

1. Create the bucket, set `paths.photo_store` and `paths.photo_store_account` in `course.json`, set the two keys on the PC.
2. Phone dump to `course_photos/unsorted/<date>/`, then `photo_sync.py push`.
3. `photo_index.py --dump ... --sheets --shots`, fix assignments by content where confidence is low, commit the index and sheets.
4. `coverage.py` for the next visit list.
5. PC: copy `shots_photo_poses.json` into the automation config, batch render, `photo_sync.py push --what renders`.
6. Cloud: `audit_pairs.py --renders latest`, read every sheet per hole, write verdicts and findings with image numbers.
7. Fix by tool, re-render the affected poses, re-read, mark fixed. Repeat until the hole's verdicts are match or accepted.

## 8. What is still manual or missing

- Reading the sheets for content is a person or the cloud model looking at images; it is cheap per sheet and it is the step that was
  skipped on Tierra. It goes into `run_pipeline.py` as a step so the next course cannot skip it.
- Pose refinement beyond the EXIF compass is the Unity panorama fit on the PC; `pose_solve.py` only flags suspect headings.
- Moving files from `unsorted/` into hole and spot folders with the standard name is not scripted yet; the index keeps the original
  path, which works, and the sort is a candidate flag on `photo_index.py`.
- Drone segment times are typed by hand once per video.
