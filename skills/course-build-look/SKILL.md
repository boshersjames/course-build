---
name: "course-build-look"
description: "Use when tinting turf, grading the satellite, setting sand or concrete colour, lighting or post-processing, or measuring colour against photos on a GSPro/OPCD course."
---

Rules file: `rules/look.json` (every number below is defined there; read it with `tools/rules.py look`). Course overrides: `course.json` → `rule_overrides.look`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/colour_match.py photo.jpg render.png --box x,y,w,h [--box ...] [--target green|sand|scrub|cliff|pepper|eucalyptus|pine] [--json tint.json]`: median RGB, r/g, b/g of photo and render in the same boxes, the gap after the render warm offset (`look.measure.render_warm_offset`), PASS/FAIL against the rule band, and the OverallColor multiplier that closes it. `--render-only` judges a render against the rule target alone.
- Unity `Course Look > Apply Tint JSON` (`unity/shared/ApplyTintJson.cs`): applies `tint.json` multipliers (and desaturate) to the named material copies, refuses shared materials, writes a done marker so a rerun cannot compound; rules `look.tint_mul`, `rough.tint_mul.done_marker`.
- `python ../course-build/tools/audit_pairs.py --renders latest [--before <run>/shots]`: turf colour measured in the same region of every photo and its render, `colour_ok` per photo; rules `photos.audit.colour_gap_max`.
- Unity `Course Look > Cart Paths and Turf` (`CourseLook.cs`): graded satellite and shared concrete; `TierraLookProbe.cs` (`unity/course-templates/`) lists the post-processing, fog and volumetric settings that make the scene camera differ from the render camera.
- (no tool yet: lighting defaults `look.lighting_batch` and the post profile `greens.post_profile` applied by command; candidate for `unity/shared/`)

# Materials, colour and look

Use with `course-build-gates` (hole-10 control rule). Full guide: `course-build/playbooks/12_materials_colour_and_look.md` in the moorpark repo.

## Defaults that worked

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

## How we measure colour

- Sample photo and render in the same box, same pose, same light; report RGB and the ratios r/g and b/g (they survive exposure differences).
- Compare like with like: shaded sand vs shaded sand; sand vs concrete in the same frame (photo sand runs 3-8% lower b/g than photo concrete).
- The render runs ~0.04 warmer than photos on neutral concrete; allow for it before chasing a tint.
- Targets: green photo median RGB (180,196,127); grey scrub b/g 0.85-0.95; dormant sage b/g ~1.0; pale cliff b/g 0.90-0.95, never below 0.88; pepper r/g ~1.0; eucalyptus r/g 0.95-1.03, b/g 0.80-0.90; pine r/g 1.0-1.08, b/g 0.60-0.78.

## Procedure

1. Course Look pass (cart paths + graded satellite) before the first review.
2. Material audit: null/error-shader materials, BadMaterials (including disabled billboard renderers), missing vertex paint.
3. Per-hole tints only on copies; course-wide tints once, rendered on the control hole before/after, with a done marker.
4. Lighting and post-processing settled once per course with BoJa from one side-by-side.

## Pitfalls

- Blend strips with their own lime tint show as rings.
- The shader's dark grass detail multiplies tints (a dry-rough re-skin went brick-brown).
- Satellite-based rough carries tree shade; the graded satellite and a fringe fade fix the dark band at green edges.
- Under-course ground texture changed to tan shows tan in any mesh gap; keep the gap check.
- A milder green tint stayed grey; no desaturate went neon; the values above were the third try. Reuse them.
- Renders with the shipped post volume are far paler (mean 199/212/163 vs 106/134/103); every gate render must say which look it used.

## Decision points (ask once, with a default)

1. Plain vs post-processed look (once per course, from one pair; default plain).
2. Greens/rough warmth and "spring vibe" (once per course).
3. Sand colour (once per course, from same-light frames).
