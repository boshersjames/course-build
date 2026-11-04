---
name: "course-build-flyovers"
description: "Use when rendering photo-pose pairs, gate sheets or hole flyovers for a GSPro course review, or setting the flyover camera path."
---

Rules file: `rules/flyovers.json` (every number below is defined there; read it with `tools/rules.py flyovers`). Course overrides: `course.json` → `rule_overrides.flyovers`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/flyover_gate.py --out progress/flyover_gate [--holes 1,9] [--end-fit] [--pin 0] [--no-plots]`: the forward-only path per hole and the pre-render gate: yaw/pitch per 1 s window (`flyovers.path.max_yaw_deg_s`), opening camera clear of crowns with the sign in frame (`flyovers.first_frame`), terrain and crown clearance (`flyovers.path.min_clear_m`), end frame with the whole green and the pin (`flyovers.end`); writes `flyover_gate.md/.json` and `flyover_shots.json` for the PC render.
- Unity `Course Tools > Flyover Preview` (`unity/shared/CourseFlyoverPreview.cs`): the render with the speed guard, from the GKD, first frame on the sign, end on the green.
- `python ../course-build/tools/photo_index.py --shots` then a shots-only `Run-CourseAutomation.ps1 -Steps none` (or `Send-UnityCommand.ps1 shots`): renders every photo's view in one batch for the pairs.
- `python ../course-build/tools/audit_pairs.py --renders latest [--before <previous run>/shots]`: photo | before | after at the same pose with measured turf colour and a verdict row per photo.
- `flyover_review.py` (`automation/`): frame sheets from a rendered flyover.
- (no tool yet: MP4 assembly from the frame-by-frame render with poster frames pulled automatically; candidate for `tools/`)

# Flyovers and review renders

Use with `course-build-gates` and `course-build-look` (say which look each sheet used). Full guide: `course-build/playbooks/13_flyovers_and_review_renders.md` in the moorpark repo. LiDAR decision point: `playbook/LIDAR_DETAIL_REVIEW.md` D13 (register photos by skyline and landmarks, never EXIF heading alone).

## Defaults that worked

- **Pose render**: camera at the photo's GPS/heading/pitch, same FOV as the phone, refined by content; terrain height at the pose within 2 cm of LiDAR.
- **Pair layout**: photo | before | after at full size, one row per photo; a top view and one low close view for mesh packages.
- **Pose confidence**: high/medium only for verdicts; weak poses flagged.
- **Flyover length**: ~20 s at 25 fps, 1280×720 or better, rendered frame by frame; MP4 on the PC (`Documents\<course>\flyovers`), stills committed.
- **Opening**: first frame on the hole sign at the tee, heading = line of play, sign readable, tee shot not blocked.
- **Path**: rise with no yaw, fly forward only (no 180° flips), yaw and pitch ≤5°/s eased, no roll; the render fails automatically above the limit.
- **Ending**: fly to the front edge of the green (~12 m short of the pin), drop to ~8 m, end looking down at the pin with the whole green in frame, hold 2.5 s.
- **Flags/pins**: none in standard flyovers (pins change); a per-pin flyover is a parked idea.
- **First frame clear**: never behind a tree or canopy: move the camera 2-5 m forward/sideways (logged), never the tree.
- **Where**: render-only editor (B/C) on a sync of A's save; never exported or copied back.

## Procedure

1. Pose solve: GPS + EXIF heading, fixed by landmarks (sign post, path crest, tee markers, skyline); log per photo (`refined_poses.json`).
2. Baseline sheet from A's named save before any edit on the hole; after-pairs at the same poses after each edit.
3. Gate sheet: every pair, automated check results, rule ticks, flyover poster frames (first / rise / mid / approach / end).
4. Flyover render with the speed guard; review first frame, sign, and the end on the green; then send.
5. Showcase flyovers: fix the known gaps in frame first, then render.

## Checks

- Yaw max and pitch max reported per flyover.
- First frame: sign in frame, not blocking; camera not inside a canopy.
- End frame: whole green in frame, pin visible.
- Pairs rendered from the save the spec was measured on (say which); stale pixel boxes flagged.
- Left/right sweep from every flyover start for empty ground.

## Pitfalls

- The GKD has no camera fields; flyover settings are GSPro player settings, so the custom flyover tool is ours and sign placement is the only in-game lever.
- A camera stopping 30 m short of the pin and only tilting down came straight back.
- A camera key at 5 m passed through a tree after the sign; raise it to 10 m.
- GPS put a tee burst 80 m away; content beats GPS.
- Renders at the wrong look can't be compared to earlier gates.
- Real-size signs are unreadable from 20-25 m; the camera starts close to the sign.

## Decision points (ask once, with a default)

1. Flyover start height/distance per course if the signs are set differently.
2. Showcase holes for outreach.
3. Whether a flag shows in a per-pin variant (parked).

- Commercial flyover shape (`rules/flyovers.json` cinematic): `intro.seconds` 5 eye-level hold by the hole sign with the packager's banner (`package.banner`: hole, card yardage, par), then `flight_s` 15 tee to green ending over the green on the pin, `orbit.degrees` 0; the path tool prints intro + flight against `target_duration_s` and FAILs on the yaw/pitch limits; a hole that cannot make 15 s within the limits gets a longer `flight_s` in `course.json` rule_overrides.

- `python ../course-build/tools/flyover_path.py [--holes 10] [--tee Black] [--set cinematic.key=value]`: headless twin of CommercialFlyoverPath; builds and checks every hole's flight against `flyovers.cinematic.limits` on the LiDAR ground; `--set` tries a rule value on all 18 before it is committed. `run_pipeline.py` runs it before asking Unity for a path.
- Render crash recovery: `render_4k.resume` keeps frames already on disk, so after a Unity crash reopen the editor with the live bridge and re-run the same pipeline command; `--render-stall` (300 s) is how long the pipeline waits without a new frame before declaring the editor gone.

- One hole's commercial video: `python ../course-build/tools/run_pipeline.py --course <course> --hole 13 --tee Black --keep-frames` (headless check, Unity path, 4K render, banner, HEVC under `AutomationOutput/deliverables`, frames kept); approve, then `python ../course-build/tools/flyover_packager.py --holes 13 --purge` deletes the frames. `--hole 0` does every hole, one at a time.
