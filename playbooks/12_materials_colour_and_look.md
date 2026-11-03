# 12. Materials, colour and look

Use this for turf tints, shared vs hole-only materials, the satellite grade, sand and concrete colour, lighting and post-processing, and how colour is measured against photos.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Theme | OPCD v4_satellite theme with the course's own satellite at Max Size 4096 and Terrain Size = square; Green/Fairway/Tee on the real default materials under `Assets/Resources`, not the white placeholders | HANDOFF |
| Satellite grade | `<satellite>_graded.jpg`: black point from the darkest 0.5% (same for all channels), a little saturation/contrast, 2× resolution to 8192 with turf grain; swapped in on every material/terrain layer that used the original | course_look |
| Greens | OverallColor ×(1.85, 1.68, 1.42), desaturate 0.2 on a `*_Tierra` copy per green/blend/collar; later "warm all greens slightly" course-wide as a tint only, contours untouched | green_rollout, Q2 |
| Fairway / rough / tee | See 04 (shared materials toned in place once, with `tone_done.txt`) | |
| Sand | Floor (0.70, 0.66, 0.56) / #D8C99B; render sand b/g 5-10% below render concrete | 03 |
| Concrete | One shared light concrete (09) | |
| Shared materials | Fairway, tee, sand, concrete stay shared and unchanged per hole (BoJa Q3); hole-only copies for anything local (`Rough11Fringe_h11edge`, `Green15_h15s`, `mulch_fine_h11`, hole-18 soil/sage copies) | 10/31 |
| Lighting (batch) | Sun ~32° (59° gave flat noon), trilight ambient ×1.3 + shadowless fill 0.28 (batch mode leaves skybox ambient black) | Hansen 8d, Wilson |
| Look target | The *plain* render (deeper turf, blue sky, light haze), not the shipped v4_Sunny_Pop post volume (pale, bleached); tune a course-only copy (TR_Look) + GradientFog/VolumetricLight so in-game matches; keep the original as backup | BoJa 10/31 |
| Turf tone order | Rough darkest (warm, slightly dormant), fairway and tees lighter with mow stripes, greens finest and brightest | Wilson 1b |
| Frame time | Realism first; keep the worst editor view ≤ ~33 ms, don't trade visible quality for a few ms | BoJa 10/30 |

## How we measure colour

- Sample the photo and the render in the same box, same pose, same light; report RGB and the ratios r/g and b/g, which survive exposure differences better than absolute values.
- Compare like with like: shaded sand against shaded sand; sand against concrete in the *same* frame (photo sand runs 3-8% lower b/g than photo concrete).
- The render's lighting runs ~0.04 warmer than the photos on neutral concrete; allow for it before chasing a tint.
- Pepper, eucalyptus, pine and scrub targets are in 06 and 07; green target: photo green median RGB (180,196,127) vs the original render (75,96,58).
- Grey scrub target b/g 0.85-0.95; dormant sage b/g ~1.0; pale cliff b/g 0.90-0.95 and never below 0.88.

## Procedure

1. Course Look pass (cart paths + graded satellite) before the first review.
2. Material audit: null/error-shader materials, `BadMaterials` (Hansen: 8 kinds including every disabled billboard renderer), missing vertex paint.
3. Per-hole tints only on copies; course-wide tints once, rendered on the control hole before/after, with a done-marker so a rerun can't compound.
4. Lighting and post-processing settled once per course with BoJa (plain vs volume) from one side-by-side (IMG pair sheet).

## Pitfalls

- Blend strips with their own lime tint show as rings (Wilson, Hansen).
- The shader's dark grass detail multiplies tints (Hansen dry rough went brick-brown).
- Satellite-based rough carries tree shade; the graded satellite and a fringe fade fix the dark band at green edges.
- Under-course ground texture (`terrainopcdmatchv4`) changed to tan shows tan in any mesh gap; keep the gap check.
- A milder green tint stayed grey; no desaturate went neon; the chosen values above were the third try. Reuse them.
- Editor render with the shipped post volume is far paler (mean 199/212/163 vs 106/134/103); gate renders must say which look they used.

## Decision points for BoJa

1. Plain vs post-processed look (once per course, from one pair).
2. Greens/rough warmth and "spring vibe" (once per course).
3. Sand colour (once per course, from same-light frames).

## Sources

`tierra-rejada-lidar/course_look/README.md`; `progress/tierra/photo_audit_1029/green_test/NOTES.md`, `green_rollout/NOTES.md`, `vegetation_1029/README.md`, `morning_1031/morning_report.md` (Q1-Q5), `gate/hole_16/edit_list.md` (edit 15 evidence); Hansen pass 8d/8f, Wilson pass 1; checklist "Tools and look notes", "Quality over frame time".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-look` but not in this playbook when the skill was cut to one page.


### Defaults that worked
- **Theme**: OPCD v4_satellite theme with the course's own satellite at Max Size 4096 and Terrain Size = square; Green/Fairway/Tee on the real default materials under `Assets/Resources`, not the white placeholders.
- **Satellite grade**: `<satellite>_graded.jpg`: black point from the darkest 0.5% (same for all channels), a little saturation/contrast, 2× to 8192 with turf grain; swapped in on every material/terrain layer that used the original.
- **Greens**: OverallColor ×(1.85, 1.68, 1.42), desaturate 0.2 on a `*_<Course>` copy per green/blend/collar; later warm all greens slightly as a tint only, contours untouched.
- **Rough**: ×(1.4, 1.22, 1.08), desaturate 0.12, done once in place with a `tone_done.txt` marker so a rerun can't compound.
- **Sand**: floor (0.70, 0.66, 0.56) / #D8C99B; render sand b/g 5-10% below render concrete.
- **Concrete**: one shared light concrete.
- **Shared materials**: fairway, tee, sand, concrete stay shared and unchanged per hole; hole-only copies for anything local (`*_h11edge`, `*_h15s`).
- **Lighting (batch)**: sun ~32° (59° gave flat noon), trilight ambient ×1.3 + shadowless fill 0.28 (batch mode leaves skybox ambient black).
- **Look target**: the plain render (deeper turf, blue sky, light haze), not the shipped v4_Sunny_Pop post volume (pale, bleached); tune a course-only copy + GradientFog/VolumetricLight so in-game matches; keep the original as backup.
- **Turf tone order**: rough darkest (warm, slightly dormant), fairway and tees lighter with mow stripes, greens finest and brightest.
- **Frame time**: realism first; keep the worst editor view ≤ ~33 ms; don't trade visible quality for a few ms.

### How we measure colour
- Sample photo and render in the same box, same pose, same light; report RGB and the ratios r/g and b/g (they survive exposure differences).
- Compare like with like: shaded sand vs shaded sand; sand vs concrete in the same frame (photo sand runs 3-8% lower b/g than photo concrete).
- The render runs ~0.04 warmer than photos on neutral concrete; allow for it before chasing a tint.
- Targets: green photo median RGB (180,196,127); grey scrub b/g 0.85-0.95; dormant sage b/g ~1.0; pale cliff b/g 0.90-0.95, never below 0.88; pepper r/g ~1.0; eucalyptus r/g 0.95-1.03, b/g 0.80-0.90; pine r/g 1.0-1.08, b/g 0.60-0.78.

### Procedure
2. Material audit: null/error-shader materials, BadMaterials (including disabled billboard renderers), missing vertex paint.
3. Per-hole tints only on copies; course-wide tints once, rendered on the control hole before/after, with a done marker.
4. Lighting and post-processing settled once per course with BoJa from one side-by-side.

### Pitfalls
- Blend strips with their own lime tint show as rings.
- The shader's dark grass detail multiplies tints (a dry-rough re-skin went brick-brown).
- Under-course ground texture changed to tan shows tan in any mesh gap; keep the gap check.
- A milder green tint stayed grey; no desaturate went neon; the values above were the third try. Reuse them.
- Renders with the shipped post volume are far paler (mean 199/212/163 vs 106/134/103); every gate render must say which look it used.

### Decision points (ask once, with a default)
1. Plain vs post-processed look (once per course, from one pair; default plain).
