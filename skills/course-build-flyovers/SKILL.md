---
name: "course-build-flyovers"
description: "Use when rendering photo-pose pairs, gate sheets or hole flyovers for a GSPro course review, or setting the flyover camera path."
---

Rules file: `rules/flyovers.json` (every number below is defined there; read it with `tools/rules.py flyovers`). Course overrides: `course.json` → `rule_overrides.flyovers`.

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
