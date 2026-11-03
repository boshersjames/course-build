# 13. Flyovers and review renders

Use this for photo-pose renders (the pairs), gate sheets, before/after sheets, and the flyover camera path.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Pose render | Camera at the photo's GPS/heading/pitch, same FOV as the phone; refined by content; terrain height at the pose matches LiDAR within 2 cm | audit 10/30 |
| Pair layout | photo \| before \| after at full size, rows per photo; a top view and one low close view for mesh packages | packages |
| Pose confidence | High/medium only for verdicts; "weak until scrub exists there" poses flagged | hole 10 |
| Flyover length | ~20 s at 25 fps, 1280×720 or better; rendered frame by frame; MP4 on the PC (`Documents\<course>\flyovers`), stills committed | flyovers_1029 |
| Opening | First frame on the hole sign at the tee, heading = line of play, sign readable, tee shot not blocked; fine if the sign is ahead of the black tees | BoJa 10/29-30 |
| Path | Rise with no yaw, fly forward only (no 180° flips), yaw and pitch ≤5°/s eased, no roll; the render fails automatically above the limit | BoJa 10/30 (asked twice) |
| Ending | Fly to the front edge of the green (~12 m short of the pin), drop to ~8 m, end looking down at the pin with the whole green in frame, hold 2.5 s | BoJa 10/31 |
| Flags/pins | No pins or flags in standard flyovers (pins change); a per-pin flyover is a parked selling-point idea | BoJa 10/30 |
| First frame clear | Never behind a tree or canopy: move the camera forward/sideways (2-5 m nudges logged), never the tree | Hansen 2, Wilson |
| Look | The plain look unless BoJa chose otherwise; say which look each sheet used | 12 |
| Where | Render-only editor (B/C) on a sync of A's save; never exported or copied back | 00 |

## Procedure

1. Pose solve: GPS + EXIF heading; fix by landmarks (sign post, path crest, tee markers, skyline) when the grid search isn't decisive; log per photo (`refined_poses.json`).
2. Baseline sheet from A's named save before any edit on the hole; after-pairs at the same poses after each edit.
3. Gate sheet: every pair, automated check results, rule ticks, flyover poster frames (first / rise / mid / approach / end).
4. Flyover render with the speed guard; review the first frame, the sign, the end on the green; then send.
5. Showcase flyovers (for the course/online): fix the known gaps in frame first (species behind the green, orchard, speed bump colour), then render.

## Checks

- Yaw max and pitch max reported per flyover (Tierra 10 v2: yaw 0.12°/s, pitch 4.5°/s).
- First frame: sign in frame, not blocking; camera not inside a canopy.
- End frame: whole green in frame, pin visible.
- Pairs rendered from the save the spec was measured on (say which); stale pixel boxes flagged.
- Left/right sweep from every flyover start for empty ground (Hansen 09/10 sheets).

## Pitfalls

- The GKD has no camera fields; flyover/hole-intro settings are GSPro player settings, so a course can't make GSPro start on the sign. The custom flyover tool is ours.
- A camera stopping 30 m short of the pin and only tilting down came straight back (10 v3).
- A camera key at 5 m passed through a tree after the sign (15); raise it to 10 m.
- GPS put a tee burst 80 m away (10 Blue) and a tee set at unresolved headings (13); content beats GPS.
- Renders at the wrong look (post volume) can't be compared to earlier gates.
- Real-size signs are unreadable in the opening frame from 20-25 m; the camera starts close to the sign.

## Decision points for BoJa

Pose solving follows **D13** (register every photo to the LiDAR by skyline and landmarks, never the EXIF heading alone) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Flyover start height/distance per course if the signs are set differently.
2. Showcase holes for outreach (Tierra: 12 and 15).
3. Whether a flag shows in a per-pin variant (parked).

## Sources

`progress/tierra/flyovers_1029/NOTES.md`, `hole_signs_1029/README.md`, `holes/hole_10_state.md`, `gate/hole_11/edit_list.md` (Step 0 pose table), `audit_photo_pairs/AUDIT_FINDINGS_1030.md`; `unity_editor/CourseFlyoverPreview.cs`; checklist "Flyover camera path", "Flyover camera speed", "Flyover ending", "2026-10-30 flyover pins".
