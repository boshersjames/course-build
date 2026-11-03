---
name: course-premiere-flyovers
description: Use when making showcase or premiere flyover videos of a GSPro course to sell the course and get golfers excited about a hole: picking holes, designing the camera path, dressing the frame, rendering, encoding and the send-out check. Separate from the in-game GSPro flyover and from the review renders in course-build-flyovers.
---

# Premiere flyovers (DRAFT, to finalise after the Nov 3 renders are reviewed)

A premiere flyover is a sales video, not a review render. The viewer should want to play the hole: the challenge (carry, drop, dogleg,
water), the unique feature (cliff, creek, plaque, hillside) and the scenery. It is also not the GSPro flyover, which is a player setting
the course cannot change. Use `course-build-flyovers` for review renders and gate sheets; this card is only for the videos that go out.

## Defaults that worked (Tierra, Nov 2-3)

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

## Procedure

1. Candidate list from the person sending; cut by the open-notes check per hole; 3-4 picks with the hook written down per hole.
2. Gate pass on the data (`tierra_flyover_gate.py --end-fit --width 1920 --height 1080`): every pick must pass before an editor is used.
3. Dress what is in frame: thin far trees, pale paint cells, water that renders black offline, haze; tint and LOD only, no reshaping.
4. Render, review the stills (frame 0 sign readable and tee shot clear; top of rise; approach; end frame whole green and pin), sweep
   left/right from the opening spot for empty ground.
5. Encode, then the send-out check against the hook list: is the feature in frame? If not, a camera knob and a rerun, not a rebuild.

## Checks
- Gate JSON per hole: yaw/pitch per 1 s, crown clearance on the rise and flight, green cut-off 0, pin in frame and unhidden.
- Render log says which save (time, commit) the frames came from.
- Nothing in frame violates the build rules (plants on turf, defects on in-play turf, invented ground).

## Pitfalls
- The review-render ending looked right in the gate table but cannot show the whole green; check the end frame on every hole.
- A rise key 12 m past the sign climbs through any 10 m+ crown within 12 m of the sign (Tierra 12's eucalyptus).
- A par 3 at 26 m/s is over in 12 s.
- LFS: PNGs in this repo go through Git LFS, which cloud sessions cannot push; commit poster frames from the PC.

## Decision points (ask once, with a default)
1. Which holes, if the candidate list is longer than four. Default: the cleanest four.
2. Length and format. Default: 17-26 s, 1080p, no text overlay.
3. A feature beat (a pass along a creek or the cliff edge) off the hole line. Default: none; the forward-only rule stays.

## To fill in after the review
- What James changed after seeing the MP4s (speed, end spot, rise) per hole, and whether any hole was swapped.
- Whether a feature beat is wanted, and the waypoint rule if so.
- Encode settings and whether 1080p render time was acceptable.
