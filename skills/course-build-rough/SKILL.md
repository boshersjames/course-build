---
name: "course-build-rough"
description: "Use when tracing fairways, laying first cut and deep rough, setting rough physics or toning turf in a GSPro course build."
---

Rules file: `rules/rough.json` (every number below is defined there; read it with `tools/rules.py rough`). Course overrides: `course.json` → `rule_overrides.rough`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/colour_match.py photo.jpg render.png --box x,y,w,h`: rough / fairway tone against the photo, suggested OverallColor multiplier; apply with Unity `Course Look > Apply Tint JSON` (`ApplyTintJson.cs`, honours the `tone_done` marker so a rerun cannot compound); rules `rough.tint_mul`, `look.tint_mul`.
- `python ../course-build/tools/fairway_vs_lidar.py [before|after]`: fairway mesh vs LiDAR per hole and landing zone (RMS, median, undulation kept); needs the Unity `fairway_samples.bin` export in AutomationOutput; rules `terrain.relief.keep_pct`.
- `python ../course-build/tools/coursekit.py svg --smooth 7 --min-green 0.36 --fw-corridor 45`: the photo-traced fairway outlines; rules `rough.fairway_trace`.
- `python ../course-build/tools/gkd_tools.py check`: FairwayFirmnessDefault against `rough.gkd`.
- (no tool yet: patch edge saw-tooth check, deep rough inside the 6 m green margin, first-cut width; candidate for `unity/shared/`)

# Course build: fairways, rough and first cut

Follow the standing rules in `course-build-gates` (no defects on in-play turf, no plants on mown turf, shared materials stay shared). LiDAR review decision points D1-D3 (ground cover) in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo).

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Fairway outline | OSM where mapped; otherwise photo trace (`svg --smooth 7 --min-green 0.36 --fw-corridor 45`), then `smooth_fairways.py` (joins pieces across tree shadows, smooths edges) before surrounds | Harding, Moorpark |
| Fairway shape | Keep the LiDAR undulation, especially landing zones; never smooth for looks | BoJa 10/29 |
| Fairway firmness | GKD `FairwayFirmnessDefault` 0 (the "firmer" trial caused big bounces); never max | playtest |
| First cut | ~4 m of rough physics next to fairways, greens and tees; deep rough beyond it and under tree lines where the real rough is penal | Penmar |
| Deep rough source | Satellite tan/golden masked by LiDAR (no trees/shrubs >0.8 m), brush, play surfaces +5 m, paths, roads, OB; patches kept only if touching brush/OB or ≥2,500 m² and ≥12 m from a fairway | deep_rough README |
| Deep rough physics | GSPdeeprough on hillsides and outer rough; a ball stops within a few yards | playtest 10/27 |
| Deep rough height | Patches 3 cm above the course surface; terrain detail layers from `terrain_masks/` | automation |
| Blend strips | Exactly the rough's or fairway's settings, never a lime tint; rough darkest, fairway lighter and striped, greens finest and brightest | Wilson 1b |
| Rough tone (Tierra) | Rough ×(1.4, 1.22, 1.08) desat 0.12; fairway ×(1.45, 1.38, 1.2); tee ×(1.42, 1.36, 1.18); blends ×(1.42, 1.3, 1.14) over OPCD MAHS defaults | green_rollout |
| Rough tone (pale satellite) | Tint greener inside the boundary so it doesn't read grey-white; lush green under scene lighting, never olive/yellow | Penmar, Hansen |
| Edge graded | Turf → 4 m deep rough → golden grass to 9 m → scrub | Moorpark masks |
| Realism | Mow stripes, turf normals ×1.5, grass detail and colour variation in the rough, soft edges where cuts meet | Hansen |

## Procedure

1. Trace and smooth fairways in the SVG; `svg_check.py` before any PC build.
2. After OPCD: every fairway mesh vs the LiDAR heightmap in the landing zones (RMS, median, `undul_kept_pct`) and a rollout model (slope %, extra roll, side fall).
3. First cut and deep rough (4 m first cut, deep rough beyond); deep rough ≥6 m off greens; none under tee pads.
4. Hillsides in play: brush + deep-rough physics, never bare dirt.
5. Blend strips with the neighbour's exact settings; bridge height gaps at fairway/collar edges with a strip in the rough's settings.
6. Tone pass once, course-wide, with a `tone_done.txt` marker so it can't compound; shown on the control hole.

## Checks

- No jagged or saw-tooth patch edges (rebuild with CoursePatch, not from rough triangles); low-camera check of every patch.
- No turf tongues into the fairway, no straw strips, ochre bands or smears on in-play rough.
- No lighter rough band crossing a tee's fine cut.
- Rough doesn't grab or roll unrealistically on hillsides: in-play hillside = rough physics, out-of-play = deep rough.
- Photo-traced fairways not blotchy.
- In-play turf clean: no worn strips, bare patches or divots even when photographed.

## Pitfalls

- A course-wide clear of tufts from in-play rough also stripped steep native banks inside the rough mesh; restore with a slope-masked flood fill (≥15°, 2.5 m off turf/sand/water/paths).
- Satellite-based rough carries tree shade; use the graded satellite or a hole-only fringe copy.
- Hansen's dry-rough re-skin read brick-brown: the shader's dark grass detail multiplies any tint. Tint the albedo only.
- Deep rough overlays inside the 6 m green margin stopped balls dead off the green.
- A FairwayPatch cut from rough triangles inherited the rough texture channel.
- Mulch bands beside paths "didn't work": grass or a litter strip only where photographed.

## Ask BoJa once (with a default)

**In person even with a newer scan:** where the first cut ends and deep rough begins (D2), which hillsides are penal vs playable (D1), and the season look. Undulation is settled by the LiDAR.

1. Rough colour for the season: green "spring vibe" (Tierra default) vs dormant straw on named slopes.
2. Penal deep rough vs playable first cut, per side of each hole, when photos don't say.
3. Fairway firmness after a GSPro test at medium (default 0).
