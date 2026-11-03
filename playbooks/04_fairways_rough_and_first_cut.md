# 04. Fairways, rough and first cut

Use this for fairway outlines and undulation, the first cut, deep rough, hillside rough physics, blend strips and turf tone.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Fairway outline | OSM where mapped; otherwise photo trace (`svg --smooth 7 --min-green 0.36 --fw-corridor 45`), then `smooth_fairways.py` (joins pieces across tree shadows, smooths edges) before surrounds | Harding, Moorpark |
| Fairway shape | Keep the LiDAR undulation, especially landing zones; never smooth for looks | BoJa 10/29 |
| Fairway firmness | GKD `FairwayFirmnessDefault` 0 (the "firmer" trial caused big bounces); moderate, never max | playtest |
| First cut | ~4 m of rough physics next to fairways, greens and tees; deep rough beyond it and under tree lines where the real rough is penal | Penmar |
| Deep rough | From the satellite (tan/golden) masked by LiDAR (no trees/shrubs >0.8 m), brush, play surfaces +5 m, paths, roads, OB; patches kept only if touching brush/OB or ≥2,500 m² and ≥12 m from a fairway | deep_rough/README |
| Deep rough physics | GSPdeeprough on hillsides and outer rough; a ball stops within a few yards | playtest 10/27 |
| Deep rough height | Patches 3 cm above the course surface; `Terrain (Inner)` detail layers from `terrain_masks/` | automation check |
| Blend strips | Exactly the rough's or fairway's settings, never a lime tint; rough darkest, fairway lighter and striped, greens finest and brightest | Wilson 1b |
| Rough tone (Tierra) | Rough ×(1.4, 1.22, 1.08) desat 0.12; fairway ×(1.45, 1.38, 1.2); tee ×(1.42, 1.36, 1.18); blends ×(1.42, 1.3, 1.14) over the OPCD MAHS defaults | green_rollout |
| Rough tone (pale satellite) | Tint greener inside the boundary so it doesn't read grey-white (Penmar); lush green under scene lighting, never olive/yellow (Hansen) | |
| Edge graded | Turf → 4 m deep rough → golden grass to 9 m → scrub (Moorpark masks) | Moorpark README |
| Mow stripes / grain | Fairway mow stripes, turf normals ×1.5, grass detail and colour variation in the rough, soft edges where cuts meet | Hansen realism pass |

## Procedure

1. Trace and smooth fairways in the SVG; run `svg_check.py` before any PC build (degenerate subpaths, tee pads on fairway edges).
2. After OPCD: check every fairway mesh against the LiDAR heightmap in the landing zones (RMS, median, `undul_kept_pct`) and the rollout model (slope %, extra roll, side fall) so the hole plays as it rolls.
3. Lay the first cut and deep rough (`deep_rough.py` style: 4 m first cut, deep rough beyond); keep deep rough ≥6 m off greens; no deep rough under tee pads.
4. Hillsides in play: brush + deep-rough physics, never bare dirt (Tierra 4, 8, 12).
5. Blend strips with the neighbour's exact settings; bridge any height gap at fairway/collar edges with a strip in the rough's settings.
6. Tone pass (once, course-wide, with `tone_done.txt` so it can't compound), shown on the control hole.

## Checks

- No jagged or saw-tooth patch edges (Tierra 16 strip was cut from rough triangles with the wrong texture channel; rebuild with CoursePatch). Low-camera check of every patch.
- No turf "tongues" into the fairway, no straw strips, ochre bands or smears on in-play rough (16 edit 2).
- No lighter rough band crossing a tee's fine cut (16 edit 7).
- Rough does not grab or roll unrealistically on hillsides (Tierra 1): in-play hillside rough is rough physics, out-of-play is deep rough.
- Fairway patches/photo-traced pieces are not blotchy (Harding).
- In-play turf stays clean: no worn strips, bare patches or divots even when photographed (BoJa 10/31; hole 10 edit 6 removed under this rule).

## Pitfalls

- An earlier course-wide clear of grass tufts from in-play rough also stripped steep native banks that sat inside the rough mesh (Tierra 14, 17, 18, 12); put the native mix back with a slope-masked flood fill (≥15°, 2.5 m off turf/sand/water/paths).
- Satellite-based rough carries tree shade into the texture (dark rough beside the 11 green); use the graded satellite or a hole-only fringe copy.
- The "dry rough re-skin" on Hansen read brick-brown because the shader's dark grass detail multiplies any tint; revert and tint the albedo only.
- Deep rough overlays over the 6 m green margin made balls stop dead right off the green (Tierra 6, 15).
- A FairwayPatch cut from rough triangles inherited the rough texture channel; check the channel of every patch.
- Mulch bands beside paths "didn't work" (BoJa 10/30): remove, keep grass or a litter strip only where photographed.

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** where the first cut ends and deep rough begins (D2), which hillsides are penal vs playable (D1), and the season look. Fairway undulation is settled by the LiDAR/scan.

LiDAR review decision points for ground cover: **D1-D3** (what each out-of-play cell is, where the mown corridor ends, rock vs scrub on steep cells) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Rough colour for the season: green "spring vibe" (Tierra default) vs dormant straw on named slopes.
2. Where the real rough is penal (deep rough physics) vs playable first cut, per side of each hole, when the photos don't say.
3. Fairway firmness in GSPro after a test at medium (default 0).

## Sources

`tierra-rejada-lidar/deep_rough/README.md`; `progress/tierra/bunkers_fairways_1029/README_fairways.md`; `photo_audit_1029/green_rollout/NOTES.md`, `fixes_1029b/NOTES.md`; `unity_automation/SUMMARY_2026-10-27*.md`, `SUMMARY_2026-10-28_playtest.md`; Penmar `deep_rough.py`, `fairways.py`; Harding `smooth_fairways.py`; checklist "Surfaces and physics".
