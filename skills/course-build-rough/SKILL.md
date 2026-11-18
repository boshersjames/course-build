---
name: "course-build-rough"
description: "Use when tracing fairways, laying first cut and deep rough, setting rough physics or toning turf in a GSPro course build."
---

# Course build: fairways, rough and first cut

Follow the standing rules in `course-build-gates` (no defects on in-play turf, no plants on mown turf, shared materials stay shared). LiDAR review decision points D1-D3 (ground cover) in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo).

Rules file: `rules/rough.json` (read it with `tools/rules.py rough`); course overrides in `course.json` → `rule_overrides.rough`.

**Edges:** where rough, blend strips or fairway meet a collar, green or bunker lip, the join is built with its neighbours at the Blender/OPCD conform stage (`course-build-edges`). Raised overlay patches and stacked repair strips are not the default inside rebuilt playing zones; a Unity patch over a built join makes that join stale until re-checked.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Fairway outline | OSM where mapped; otherwise photo trace (`svg --smooth 7 --min-green 0.36 --fw-corridor 45`), then `smooth_fairways.py` (joins pieces across tree shadows, smooths edges) before surrounds | Harding, Moorpark |
| Fairway shape | Keep the LiDAR undulation, especially landing zones; never smooth for looks | BoJa 10/29 |
| Fairway firmness | GKD `FairwayFirmnessDefault` 0 (the "firmer" trial caused big bounces); never max | playtest |
| First cut | ~4 m of rough physics next to fairways, greens and tees; deep rough beyond it and under tree lines where the real rough is penal | Penmar |
| Deep rough source | Satellite tan/golden masked by LiDAR (no trees/shrubs >0.8 m), brush, play surfaces +5 m, paths, roads, OB; patches kept only if touching brush/OB or ≥2,500 m² and ≥12 m from a fairway. This is the base only: where photos cover a side of the hole, they and BoJa's description review the edges and change them where the evidence supports it (procedure step 3b) | deep_rough README, BoJa 11/16 |
| Deep rough physics | GSPdeeprough on hillsides and outer rough; a ball stops within a few yards | playtest 10/27 |
| Deep rough height | Patches 3 cm above the course surface away from built joins; terrain detail layers from `terrain_masks/`. Never within the 6 m green margin or over a collar, blend or lip zone (clip there instead) | automation; review 11/15 |
| Blend strips | Exactly the rough's or fairway's settings, never a lime tint; rough darkest, fairway lighter and striped, greens finest and brightest | Wilson 1b |
| Rough tone (Tierra) | Rough ×(1.4, 1.22, 1.08) desat 0.12; fairway ×(1.45, 1.38, 1.2); tee ×(1.42, 1.36, 1.18); blends ×(1.42, 1.3, 1.14) over OPCD MAHS defaults | green_rollout |
| Rough tone (pale satellite) | Tint greener inside the boundary so it doesn't read grey-white; lush green under scene lighting, never olive/yellow | Penmar, Hansen |
| Edge graded | Turf → 4 m deep rough → golden grass to 9 m → scrub | Moorpark masks |
| Realism | Mow stripes, turf normals ×1.5, grass detail and colour variation in the rough, soft edges where cuts meet | Hansen |

## Procedure

1. Trace and smooth fairways in the SVG; `svg_check.py` before any PC build.
2. After OPCD: every fairway mesh vs the LiDAR heightmap in the landing zones (RMS, median, `undul_kept_pct`) and a rollout model (slope %, extra roll, side fall).
3. First cut and deep rough (4 m first cut, deep rough beyond); deep rough ≥6 m off greens; none under tee pads.
3b. **Photo shaping (BoJa 11/16).** Before the rough counts as built, run the photo enhancement procedure (`course-build-photo-index`) on each side of the hole: every photo looking across or along the rough (tee, fairway and green photos, hillside and toe shots) plus BoJa's notes. From them set, per side: where first cut ends and deep rough begins, where deep rough turns to golden grass or scrub, and how it reads in colour and height. Move the mask edges to match, in metres from the fairway edge at the photo poses. Copy the layout, not the faults: dead or worn grass, divots and bare patches on in-play rough are never built in. A mask edge that already matches its photos stays and is recorded `verified unchanged`; where the photos disagree but are uncertain, the edge stays `open` with the provisional edge built and one question to BoJa; sides no photo covers are `provisional base`. List each in the hole summary.
4. Hillsides in play: brush + deep-rough physics, never bare dirt.
5. Blend strips with the neighbour's exact settings. At greens, collars and bunker lips they are part of the coordinated boundary (`course-build-edges`): shared edge vertices, competing faces clipped, internal creases checked. Elsewhere, bridge height gaps at fairway/collar edges with a strip in the rough's settings.
6. Tone pass once, course-wide, with a `tone_done.txt` marker so it can't compound; shown on the control hole.

## Checks

- No jagged or saw-tooth patch edges (rebuild with CoursePatch, not from rough triangles); low-camera check of every patch.
- No turf tongues into the fairway, no straw strips, ochre bands or smears on in-play rough.
- No lighter rough band crossing a tee's fine cut.
- Rough doesn't grab or roll unrealistically on hillsides: in-play hillside = rough physics, out-of-play = deep rough.
- Photo-traced fairways not blotchy.
- Every side of a hole that a photo covers has its first-cut and deep-rough edges reviewed against the photo (step 3b) with a status (verified unchanged, changed and verified, or open); none left on the satellite mask unreviewed.
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