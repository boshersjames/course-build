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
6. **4K first, then slim (BoJa 11/05, standing rule).** Render every 4K flyover video from the full-detail course before any size reduction for GSPro. Only after the 4K exports are done and approved, slim the course (textures, tree counts, LODs, detail) to get it well under the ~2.1 GB GSPro limit. Never trim detail to fit GSPro while 4K videos are still to be made.

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

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-flyovers` but not in this playbook when the skill was cut to one page.


### Defaults that worked
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

### Procedure
1. Pose solve: GPS + EXIF heading, fixed by landmarks (sign post, path crest, tee markers, skyline); log per photo (`refined_poses.json`).
4. Flyover render with the speed guard; review first frame, sign, and the end on the green; then send.
5. Showcase flyovers: fix the known gaps in frame first, then render.

### Checks
- Yaw max and pitch max reported per flyover.
- Left/right sweep from every flyover start for empty ground.

### Pitfalls
- The GKD has no camera fields; flyover settings are GSPro player settings, so the custom flyover tool is ours and sign placement is the only in-game lever.
- A camera stopping 30 m short of the pin and only tilting down came straight back.
- A camera key at 5 m passed through a tree after the sign; raise it to 10 m.
- GPS put a tee burst 80 m away; content beats GPS.
- Renders at the wrong look can't be compared to earlier gates.
- Real-size signs are unreadable from 20-25 m; the camera starts close to the sign.

### Decision points (ask once, with a default)
2. Showcase holes for outreach.

## Carried over from the skill (2026-11-03)

Lines that were in `course-premiere-flyovers` but not in this playbook when the skill was cut to one page. (premiere flyovers: kept here as the nearest playbook.)


### Premiere flyovers (DRAFT, to finalise after the Nov 3 renders are reviewed)
A premiere flyover is a sales video, not a review render. The viewer should want to play the hole: the challenge (carry, drop, dogleg,
water), the unique feature (cliff, creek, plaque, hillside) and the scenery. It is also not the GSPro flyover, which is a player setting
the course cannot change. Use `course-build-flyovers` for review renders and gate sheets; this card is only for the videos that go out.

### Defaults that worked (Tierra, Nov 2-3)
- **Pick holes by cleanliness first, drama second.** A hole with open build notes in frame (bare paint, unbuilt pond bank, fence
  through a tee) is out, however good the tee shot. 3-4 holes per send, the data-complete nine first.
- **Path**: `scripts/tierra_flyover_gate.py` (forward-only v4 path with the gate checks). Open on the hole sign, rise with no yaw, fly
  the hole line, end with the whole green in frame. Yaw and pitch under 5 deg/s (BoJa, Oct 30: "not to rotate the camera so quickly").
- **Ending**: per hole `--end-fit`, which lands 18-35 m short of the pin at 11-16 m up, looking down 25-31 deg. The review-render ending
  (12 m short, 8 m up) cuts the sides off every green.
- **Speed**: 26 m/s for par 4/5, 14-20 m/s on par 3s and short holes so the video runs 17-26 s. Under 15 s feels rushed.
- **Trees by the sign**: never move the tree. Move the opening camera up to ~9 m (`--max-move`), or lengthen the rise (`--rise-dist 40`)
  so the climb stays under the canopy until it is past the trunk. Both logged in the gate JSON.
- **Format**: 1920x1080, 25 fps, frame-by-frame from CourseAutomation shots; ffmpeg libx264 crf 17, yuv420p, faststart. MP4s on the
  PC, poster frames (first / rise / mid / approach / end) committed as JPG.
- **Where**: render editor C on a sync of A's save; dressing fixes stay in C and are logged.

### Procedure
1. Candidate list from the person sending; cut by the open-notes check per hole; 3-4 picks with the hook written down per hole.
2. Gate pass on the data (`tierra_flyover_gate.py --end-fit --width 1920 --height 1080`): every pick must pass before an editor is used.
3. Dress what is in frame: thin far trees, pale paint cells, water that renders black offline, haze; tint and LOD only, no reshaping.
4. Render, review the stills (frame 0 sign readable and tee shot clear; top of rise; approach; end frame whole green and pin), sweep
   left/right from the opening spot for empty ground.
5. Encode, then the send-out check against the hook list: is the feature in frame? If not, a camera knob and a rerun, not a rebuild.

### Checks
- Gate JSON per hole: yaw/pitch per 1 s, crown clearance on the rise and flight, green cut-off 0, pin in frame and unhidden.
- Render log says which save (time, commit) the frames came from.
- Nothing in frame violates the build rules (plants on turf, defects on in-play turf, invented ground).

### Pitfalls
- The review-render ending looked right in the gate table but cannot show the whole green; check the end frame on every hole.
- A rise key 12 m past the sign climbs through any 10 m+ crown within 12 m of the sign (Tierra 12's eucalyptus).
- A par 3 at 26 m/s is over in 12 s.
- LFS: PNGs in this repo go through Git LFS, which cloud sessions cannot push; commit poster frames from the PC.

### Decision points (ask once, with a default)
1. Which holes, if the candidate list is longer than four. Default: the cleanest four.
2. Length and format. Default: 17-26 s, 1080p, no text overlay.
3. A feature beat (a pass along a creek or the cliff edge) off the hole line. Default: none; the forward-only rule stays.

### To fill in after the review
- What James changed after seeing the MP4s (speed, end spot, rise) per hole, and whether any hole was swapped.
- Whether a feature beat is wanted, and the waypoint rule if so.
- Encode settings and whether 1080p render time was acceptable.


## Cinematic paths have their own limits (2026-11-03)

`CommercialFlyoverPath.cs` builds the premiere flyover from the hole data: a centripetal Catmull-Rom spline from 15 m behind the active tee through a fairway reveal, a 3.5 m dip over each bunker complex in the corridor, an approach point, and a 360 degree orbit of the green complex. BoJa's rule against fast camera rotation still applies, so the orbit radius is never smaller than the orbit speed divided by the cinematic yaw limit (`flyovers.cinematic.limits.max_yaw_deg_s`, 12 deg/s, against 5 deg/s for review renders), bank is a capped function of turn rate (6 deg, 0 disables it), the worst one-second window of yaw and pitch is logged with a PASS/FAIL, and the path is lifted wherever it would come within 3 m of the ground or a course mesh. The frames render through the normal batch shot list so the premiere look (post profile on) needs the editor, as every post-processed render does.


## 4K frames are rendered offline and deterministically (2026-11-03)

`RenderQueue4K.cs` renders the cinematic clips frame by frame without the Unity Recorder (the 2018.2 base project): each frame samples the AnimationClip at an exact time through AnimationMode, renders a copy of the Main Camera (post-processing layer included) to an MSAA, supersampled target and writes a PNG, so nothing depends on the real-time clock and no frame can drop. Motion blur is a true 180 degree shutter by accumulating sub-frames across half the frame interval (`flyovers.render_4k.motion_blur`), with the post-processing blur switched off for the pass. Quality overrides (shadow distance, cascades, LOD bias, LODGroups forced to LOD 0 so SpeedTree billboards never pop, terrain distances, texture limit, anisotropic) are applied for the pass and restored. A 25 s hole at 60 fps is about 1,500 frames and 18 GB; the queue checks free disk per hole and refuses rather than filling the drive. Editor only, like every post-processed render.


## Deliverables are packaged by one script (2026-11-03)

`tools/flyover_packager.py` takes a hole's 4K PNG sequence from RenderQueue4K to a client MP4 in one pass: it verifies the frames first (expected count from the cinematic trajectory JSON, contiguous numbering, every PNG decodes, one frame size), then a single ffmpeg filtergraph burns the intro card (course name, hole, par, tee yardages from the GKD, with fades over `flyovers.package.intro.seconds`), the semi-transparent logo in the corner, and a lower-third distance-to-pin readout with a progress bar driven by the trajectory through sendcmd, so the HUD updates every quarter second without per-frame re-encoding. Encoding is HEVC at crf 18, preset slow, 60 fps, hvc1-tagged for Apple players (ProRes 422 HQ on request). The MP4's frame count is checked against the source before the PNG folder is purged; a mismatch keeps the frames and fails the hole. Overlay sizes in the rules are for 2160 px and scale to whatever the frames are.

- A GKD point that reads as (0, 0, 0) is a null, not a point: Unity's JsonUtility cannot represent `"Position": null` and zeroes it, so hole 10's disabled second aim point put the course origin into the flight line and the path tool built a 4,023 m, 115 s flight that failed its own yaw and pitch check (11/3). Every shared GKD reader now drops origin points, and the pipeline treats a path whose own report says FAIL as a failed step; the render never starts on it.

- The approach point must sit outside the orbit circle. With a 40 m approach inside a 52.5 m orbit the camera flew past the entry and doubled back, a 180 deg hairpin the smoothness check caught at 130 deg/s (Tierra 10, 11/3). And length is a rule, not an accident: the yaw limit makes an orbit cost its degrees divided by the limit in seconds, so a 15 s commercial flyover is a 60 deg sweep and a fast fairway leg, and the tool prints its duration against `target_duration_s`.

- The commercial length is 20 s, not 45 and not 15 (11/4): a 5 s hold at eye level by the hole sign, banner lower-left with hole, card yardage and par, then 15 s tee to green. No title card, no 360 orbit, no logo outro. The flight speed is scaled to the 15 s, and the yaw and pitch limits still decide whether a hole can be flown that fast; when it cannot, that hole gets more seconds, never a faster camera.

- Tune a flight on the twin, not on Unity (11/4). Six Unity round trips found one constraint each: an orbit entry behind the approach, a rise point behind the sign, a bunker detour, the end gaze, the end speed, the sideways jog from the sign. `flyover_path.py` found the last three together in one sweep over all 18 holes and measured the trade-off that set the limits: at 15 s of flight every hole passes 20 / 15 deg per s, and the old 12 / 8 fail ten of them at any length. The pipeline now runs the twin first and never asks Unity for a failing path.

- The 4K render is the heaviest thing the 2018 editor does (11/3): a 1.5x supersampled MSAA-8 target and eight manual renders per frame through the post stack killed it in a Direct3D resource call at frame 401 of 1,200. The render rule is now a 1.0x target with MSAA 8 and four accumulated sub-frames, the loop flushes and collects every 25 frames, frames on disk are kept on a re-run, and the pipeline watches the frame folder: five minutes without a new frame means the editor is gone and the step fails instead of waiting two hours.

- First watched 4K flyover, Tierra 10 (11/4), and what each fault turned into: the camera 'going underground' was a canopy by the tee (the ground test skipped tree colliders; the lift now treats crowns as ground, and the climb out of the hold cannot sag); the surging speed was the slow design's ease-in, straight boost and ease-out (now near-constant with short eases); the close finish was end_short_m 15 (now 25 at 15 m, FOV 48); the boulder is a render-only prop with no collider (`CourseSceneOps FindOnHole` lists those along a hole line); the tee spilling onto the cart path was a widened pad copy (`_wide`) that the physics census had already flagged outside its SVG outline; the banner text goes to ffmpeg through text files because 9.0 on Windows refused a bullet inline.

- Turf over a cart path belongs to the cart path tool (11/4): the paths rule file always had the overlap grid and the rough-poke limit and nothing implemented them. `BuildCartPaths.CheckTurf` measures it per path and `SinkTurf` fixes it by sinking vertices, never by deleting triangles, because a deleted triangle is exactly the sawtooth the 4K flyover showed on the run-in to Tierra 10's green.

- The 'boulder' on Tierra 10's approach (11/4) was not an object: every object search within 10 m found plants and the cliff cluster, and the terrain report found six brush detail layers painted there. A dead-brush billboard on mown turf is a rock from the air. Painted details never belong on green, collar, fairway or tee turf, and the sweep that enforces it is a rule in brush.json.

- 'The camera goes under ground' at the hold (11/4) was the near clip plane: the path data put the camera 1.7 m over the turf exactly as designed, and the render camera, a copy of the Main Camera, carried a near plane of metres, so the ground within it was not drawn and the sky showed through below the horizon. Measure the position first (it was right), then the camera. The render camera now sets its own near clip from render_4k.near_clip_m.
