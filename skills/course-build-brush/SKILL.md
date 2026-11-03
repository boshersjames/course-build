---
name: "course-build-brush"
description: "Use when building or fixing native hillsides, scrub, sage, shrubs, rock faces or the dirt toe on a GSPro/OPCD course in Unity."
---

Rules file: `rules/brush.json` (every number below is defined there; read it with `tools/rules.py brush`). Course overrides: `course.json` → `rule_overrides.brush`.

# Brush, hillsides and scrub

Use with `course-build-gates` (process, editors, photo pairs, hole-10 control). Full guide: `course-build/playbooks/07_brush_hillsides_and_scrub.md` in the moorpark repo. LiDAR decision points: `playbook/LIDAR_DETAIL_REVIEW.md` D1-D3, D18.

## Defaults that worked

- **Shrubs**: LiDAR shrubs via `find_brush.py` / `coursekit.py vegetation`, removed with reasons on roads, paths, parking, play surfaces, buildings, yards. Extra edge row along the scrub boundary so it reads continuous. No brush within 14 m of a house or on lawns/paving within 40 m.
- **Density**: ~12 plants/m²; TR_Sage scale 0.6-1.3 at 50-60% cover with pale soil between; 90%+ dense grey sage on flagged signature hillsides with no bare ground.
- **Colour**: dormant SoCal scrub is grey to grey-mauve, never green/orange. Render target ~RGB 95-105 / 92-100 / 80-90 (b/g 0.85-0.95); hillside ground tan-grey (128,118,102); bare soil within 5 points of the photo. Measure by r/g and b/g ratios in same-light frames.
- **Mix**: 60% brown/grey cards; buckwheat rust-brown (not mauve), sagebrush silver-grey, toyon/laurel sumac olive, prickly pear in clumps, bright green bushes only where a photo shows them.
- **Toe**: 0.3-1 m bare reddish-tan soil strip where scrub meets mown turf (not a mown band); scrub boundary 0.5 m past the path; one clean toe line.
- **Physics**: deep-rough physics under brush; shrubs ≥ ~1 m carry GSPleaves colliders; the ball never passes through a bush.
- **Native banks in play**: dry-grass + scrub ~3 cards/m² on slopes ≥15°, 2.5 m off turf/sand/water/paths (0.8 m off a path, 4 m off a tee).
- **Rock faces**: only where LiDAR has >35° cells and photos show pale rock; real relief as normal + detail (never new ground), vertical rills, pale cream RGB 190-200/180-190/170-178, sparse sage on edges, loose rock at the foot.
- **Hillside ground**: graded satellite base only where the top surface is outer hillside rough, 6 m feather at turf/sand/water.

## Procedure

1. `coursekit.py vegetation` → `brush_sage_scrub.mask`, `grass_dry_hillside.mask`, `grass_deep_rough.mask`, `no_grass.mask` (1 px = 1 m, north up), `shrubs_clean.csv`; `--wash`, `--parkland` as needed. Keep scrub/golden-grass ground out of the rough mesh in the SVG.
2. Unity: Plant Shrubs (instanced chunks), Paint Grass and Brush from the masks; native-slopes script for in-play banks.
3. Photo pass per hillside, BoJa's own hillside photo first. Match cover, colour, bare-soil share, toe, accent bushes. Hole-only copies of the ground layer and sage material where a hill's tint differs.
4. Cliff/rock from the LiDAR steep band at the photographed bearing; report width and elevations; clear trees/shrubs on the face; remove pale patches draped on the wrong slope.
5. Model swaps: TR_Sage (0.88 × 1.7 m, no collider) base; dormant twiggy variant on forward-tee banks; native evergreen for accents/hedges.

## Checks

- Plants-on-turf = 0 after every pass.
- Hill mean colour inside the target band; if dark, lift the leaf tint, don't add plants.
- No dead-grass scatter or invented tees on hillsides; every brushed area checked against a photo.
- Cliff drapes on the right cells (mask rows not flipped).
- Saw-tooth soil/scrub/grass edges smoothed into one soft boundary.

## Pitfalls

- Autumn orange/red/yellow or vivid green scrub cards; pink buckwheat; 3 m shrubs on tree cards read as blossom trees.
- Flat grass cards at full sun on steep banks read bright; tone or use 3D plants.
- White streaked rock asset and seashell rocks: replace with rounded fieldstone/sandstone.
- Stretched textures on steep faces: triplanar or re-UV.
- Dry-rough re-skin read brick-brown (shader multiplies tint).
- Course rough meshes covering the whole square means hillside colour comes from the satellite base, not the terrain.

## Decision points (ask once, with a default)

**Needs an in-person look even with a newer scan:** cover class and colour of every flagged hillside (D1), rock vs scrub on steep cells (D3), the dirt toe line (D2), shrub vs tree (D18). BoJa's photo of each signature hillside is the spec.

1. Season look: dormant grey-brown (default) or green.
2. Density on signature hillsides (one photo per flagged hill).
3. Which hills are OB vs deep rough in play.
4. Accent species only from photos.
