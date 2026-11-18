---
name: "course-build-lighting"
description: "Use when lighting or look-developing Unreal marketing flyovers or hero clips for a GSPro course, or when plants read too dark, too pale or inconsistent across groups."
---

# Lighting and vegetation look (Unreal marketing)

Use with `course-build-unreal` (export, levels, scored one-change runs, engine slot rules) and `course-build-look` (colour measurement, turf tone order, which look a render used). Geometry repair skills (edges, greens, bunkers) never carry material or lighting recipes; they point here.

Pilot: Tierra Rejada holes 10-12 hero flyovers, 11/15/2026. BoJa's verdict that night: ambient light much better, but plants lit inconsistently. Shrub beds near-black and flat, plume grass and sage chalk-white, trees either detail-less dark or pale yellow-green, plantings that start and stop on the hill. The hole 12 foreground wheat grass (HillGrass_H12_v4/v5) was the best-lit plant.

## Target

Inviting, coherent Southern California course photography: rich but believable turf, warm dry hills, textured chaparral, readable tree crowns, cream sand, atmospheric depth. Two reference looks, kept separate when comparing:

- **Daylight aerials** (course website) for vegetation coverage and natural colour.
- **Warm photo** (course website) for sunlight and mood.

Acceptance is a coherent, appealing view with natural variation between species. **A brighter frame alone is not a pass.** No universal numeric brightness target: website photos have different sky, shadow and terrain proportions, so a whole-frame number from them misleads.

## Standing rules

1. **Review by material group, not whole-frame brightness.** Every candidate is judged on each group: dark trees, pale trees, dark shrubs, pale shrubs, dry grass, turf (greens, fairway, rough), sand, distant terrain. A change that helps one group and hurts another is not adopted as is.
2. **Freeze before you tune.** Save the current look and lock camera, sun, exposure and colour treatment (post volume, LUT, tone curve). Record the settings and material/MI versions in the run summary. Nothing else moves during a vegetation comparison.
3. **Fixed regression set.** Keep BoJa's flagged views as permanent cases, full frame plus fixed crops of each group present. Every later look change is rendered on all of them with identical framing.
4. **Causes are hypotheses.** Before changing anything, find which assets/MIs make the dark and pale groups and check their inputs one by one: base colour/albedo, vertex or instance tint, normals (card normals facing away from the sun, flipped or unsmoothed), two-sided/backface/subsurface foliage shading, shadow and AO (contact shadow, self-shadow on dense cards, distance-field AO), baked shading in the texture, LOD/billboard materials. Write down what was verified, not just what was suspected.
5. **One cause at a time, on copies.** Reversible, per-group candidates; auto-undo on a failed check (as in `course-build-unreal`). Then combine the winners into one strongest correction.
6. **Present baseline vs strongest correction**, same framing, full frames plus the group crops side by side. Not a grid of small exposure steps.
7. **Whole frame last.** Only once vegetation is coherent, judge the frame against the chosen website reference: greens keep texture, hills keep depth, sand keeps detail, foliage has both highlights and readable shadows.
8. **Validate in motion before final renders.** One short preview (about 8-10 s, 1080p) on the actual proposed marketing camera. Check foliage shimmer, disappearing detail, popping/LOD swaps, abrupt asset changes and exposure stability. Record the measured render time and estimate the long queue from it, never from a guess.
9. **No long 4K or alternate-light (golden hour) chains while the vegetation fails** on the regression set.

## Per-group checks

- **Trees**: crown and branch structure readable inside the shadow side. Some shadow is wanted; losing the interior is the defect. Leaves not pale yellow-green next to darker neighbours unless the species really is.
- **Dark shrubs**: no solid brown-black masses or silhouettes; individual plants separate; lit side shows texture.
- **Pale shrubs, plume and dry grass**: no chalk or silver-cream blowout; plants separate from each other; keep each species' own hue (sage grey-green, buckwheat rust, straw gold). Measure hue with the r/g and b/g ratios in `course-build-look`.
- **Reference plant**: the best-lit plant on the course (Tierra: H12 wheat grass) is the yardstick for *lighting response*: lit side, shadow side and separation between clumps. Bring other groups toward that response, not to its colour or one shared brightness. Swap a species only when it is implausible for the site (pampas/plume grass on Tierra's native hills went to wheat grass; coastal sage stayed sage).
- **Distant terrain**: warmth BoJa likes is kept, but saturated orange or banded hills are checked against the reference hue and texture (often the older backdrop texture, see `course-build-backdrop`).
- **Turf and sand**: tone order from `course-build-look` holds; greens don't glow against the fairway.

## Planting edges

Beds must blend into the hill, not start and stop. Check every bed edge in the regression views: density tapers over a few metres, dry ground/soil sits under and beyond the edge (bright turf between shrubs reads as parkland), mixed species at the boundary, no straight or mask-shaped lines. Placement rules (toe strip, distances from turf and paths) stay in `course-build-brush`; this skill only judges how the edge reads in the render.

## Measured on Tierra 11/15 (reproduce before reusing)

- Exposure +1.0 was the best of the tested values for the proposal look.
- Greens at 60% brightness dropped from 18 points brighter than the fairway to 4, same colour family, no glow.
- Shadows: render shadow p10 29-38 against 62 in the photos before the shadow-lift tests.
- Height-fog density scaling (×2, ×4, ×8) gave no visible change on the far ridges; layered fade needs sky-atmosphere aerial perspective and fog that starts closer and rises higher.
- Contiguous rendering of a frame range ran 0.3 s/frame vs 10.6 s/frame per-frame, about 35× faster, identical framing.
- Whole-image brightening could not fix dark beds, pale grass and dark trees together.

## Run summary (each comparison)

Frozen settings and versions; group crops before/after; which cause each candidate tested and whether it was verified; adopted/undone with reason; motion preview path and measured time; anything for BoJa.

## Ask BoJa once (with a default)

1. Which website photo is the reference for this course: daylight aerial or warm (default: aerial for vegetation, warm for mood).
2. Which views join the regression set (default: every view BoJa flagged plus one per hole from the marketing camera).
3. Species swaps for implausible plants (default: swap to the course's reference plant only where the photos don't show the original).