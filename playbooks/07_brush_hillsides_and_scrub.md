# 07. Brush, hillsides and scrub

Use this for native hillsides, scrub and sage density and colour, shrubs from LiDAR, rock faces and cliffs, the dirt toe where a hillside meets turf, and hillside physics.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Shrubs | LiDAR shrubs (`find_brush.py` / `coursekit.py vegetation`): Tierra 11,533 after cleanup; removed with reasons (roads, paths, parking, play surfaces, buildings, yards) | shrubs_ob |
| Edge row | Extra shrubs lined along the scrub edge so the boundary reads continuous (Moorpark 6,405) | Moorpark |
| Yards | No brush within 14 m of a house or on lawns/paving within 40 m | Moorpark |
| Density | ~12 plants/m² at the vegetation-thread setting; TR_Sage at scale 0.6-1.3 and 50-60% cover with pale soil between (Tierra 11); 90%+ dense grey sage on the flagged 10/18 hillside with no bare ground | 11, 18 |
| Colour | Dormant SoCal scrub is grey to grey-mauve, not green/orange: render target ~RGB 95-105 / 92-100 / 80-90 (b/g 0.85-0.95); hillside ground tan-grey (128,118,102); bare soil within 5 points of the photo | vegetation_1029, 11 |
| Mix | 60% brown/grey cards; buckwheat rust-brown (not mauve), sagebrush silver-grey, toyon/laurel sumac olive, prickly pear in clumps, a few bright green bushes where BoJa's photo shows them | 10/30 |
| Toe | A 0.3-1 m bare reddish-tan soil strip where scrub meets mown turf (not a mown band), scrub boundary 0.5 m past the path, one clean toe line | 14, 18 |
| Physics | Hillsides get deep-rough physics under brush that stops the ball; shrubs ≥ ~1 m carry GSPleaves colliders; ball must not pass through bushes | Tierra 4, 8 |
| Native banks in play | Dry-grass + scrub mix ~3 cards/m² on slopes ≥15°, 2.5 m off turf/sand/water/paths (0.8 m off a path, 4 m off a tee) | native_slopes |
| Rock faces | Where the LiDAR has >35° cells and the photos show pale rock: real relief as normal + detail (never new ground), vertical rills, pale cream (RGB 190-200/180-190/170-178, b/g 0.90-0.95), sparse sage on edges, loose rock at the foot; not a painted slope | Tierra 11 face |
| Hillside ground | Graded satellite base only where the top surface is outer hillside rough, 6 m feather at turf/sand/water | vegetation_1029 |

## Procedure

1. Data: `coursekit.py vegetation` writes `brush_sage_scrub.mask`, `grass_dry_hillside.mask`, `grass_deep_rough.mask`, `no_grass.mask` (1 px = 1 m, north up) and `shrubs_clean.csv`; `--wash` for sand/cobble washes, `--parkland` for all-turf parks. Keep scrub and golden-grass ground out of the rough mesh in the SVG so the terrain stays paintable.
2. Unity: Plant Shrubs (instanced chunks), Paint Grass and Brush from the masks; `TierraNativeSlopes` for in-play banks.
3. Photo pass per hillside: BoJa's own hillside photo first ("I gave you the right hillside - you didn't look"). Match cover, colour, the bare-soil share, the toe, and the few accent bushes. Hole-only copies for the ground layer and the sage material where a hill's tint differs.
4. Cliff/rock: build from the LiDAR steep band at the photographed bearing from the tee; report width and top/bottom elevations; clear trees and shrubs standing on the face; remove pale patches the first drape put on the wrong slope.
5. Shrub-model swaps: TR_Sage (0.88 m × 1.7 m, no collider) as the base; dormant twiggy variant for the forward-tee banks; native evergreen shrub for accents and hedges; sycamore/cottonwood/olive/pine via the model pipeline (06).

## Checks

- Plants-on-turf 0 after every hillside pass; no shrubs on mown turf, no "scattered shrubs on turf where the photo has bare soil".
- No brush on any tee box or pad (BoJa 11/05); near tees, keep cover to its side of the flight line from the tee (Tierra 4 `h4west_cover` came too far in).
- Hill mean colour in the render inside the target band; if dark, lift the leaf tint, don't add plants.
- No dead-grass scatter or invented tees on hillsides; every brushed area checked against a photo.
- Cliff drapes on the right cells (mask rows not flipped); detail grass restored where the drape was wrong.
- Saw-tooth edges between soil/scrub layers and grass smoothed into one soft boundary (10/11).

## Pitfalls

- Autumn orange/red/yellow and vivid green scrub cards; pink/mauve buckwheat cards; "blossom tree" reads from 3 m shrubs on tree cards (Tierra 15, 16, 18).
- Flat grass cards lit at full sun on steep banks read bright (Tierra 10 left bank); tone them or use 3D plants.
- A tall net or fence hidden in a tee-bank gap; a white streaked "rock" asset on hillsides (9, 10, 14) and "seashell" rocks: replace with rounded fieldstone/sandstone.
- Stretched textures on steep faces (14, 13): triplanar or re-UV.
- Hansen's "dry rough re-skin" read brick-brown (shader multiplies tint); revert.
- The sage LOD2 billboard crop and the one-sided leaf cards (06).
- The whole 2 km square on Tierra was covered by course rough meshes (85% outer hillside rough), so hillside colour came from the satellite base, not the terrain; grade the satellite where the top surface is outer rough.

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** the cover class and colour of every flagged hillside (D1), rock vs scrub on steep cells (D3), the dirt toe line (D2), shrub vs tree (D18). BoJa's own photo of each signature hillside is the spec.

LiDAR review decision points for ground cover: **D1-D3** (cell cover class, corridor edge, rock face vs scrub) and **D18** (shrub vs tree) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Hillside season look: dormant grey-brown (default for SoCal dry season) or green, per course.
2. Density on the signature hillsides (BoJa's photo is the spec; ask for one per flagged hill).
3. Which hills are OB/lost ball vs deep rough in play (10).
4. Accent species (bright green bushes, prickly pear) only from photos.

## Sources

`tierra-rejada-lidar/shrubs_ob/README.md`; `progress/tierra/vegetation_1029/README.md`, `audit_1030/review_hillsides.md`, `photo_audit_1029/fixes_1029b/NOTES.md`, `gate/hole_11/edit_list.md` (edits 1-2), `gate/hole_18/top_items.md` (item 1), `model_requests_h16_h17.md`; Moorpark README (vegetation); checklist "Signs, bins, hillsides", "Hillsides get native brush".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-brush` but not in this playbook when the skill was cut to one page.


### Defaults that worked
- **Shrubs**: LiDAR shrubs via `find_brush.py` / `coursekit.py vegetation`, removed with reasons on roads, paths, parking, play surfaces, buildings, yards. Extra edge row along the scrub boundary so it reads continuous. No brush within 14 m of a house or on lawns/paving within 40 m.
- **Density**: ~12 plants/m²; TR_Sage scale 0.6-1.3 at 50-60% cover with pale soil between; 90%+ dense grey sage on flagged signature hillsides with no bare ground.
- **Colour**: dormant SoCal scrub is grey to grey-mauve, never green/orange. Render target ~RGB 95-105 / 92-100 / 80-90 (b/g 0.85-0.95); hillside ground tan-grey (128,118,102); bare soil within 5 points of the photo. Measure by r/g and b/g ratios in same-light frames.
- **Mix**: 60% brown/grey cards; buckwheat rust-brown (not mauve), sagebrush silver-grey, toyon/laurel sumac olive, prickly pear in clumps, bright green bushes only where a photo shows them.
- **Toe**: 0.3-1 m bare reddish-tan soil strip where scrub meets mown turf (not a mown band); scrub boundary 0.5 m past the path; one clean toe line.
- **Physics**: deep-rough physics under brush; shrubs ≥ ~1 m carry GSPleaves colliders; the ball never passes through a bush.
- **Native banks in play**: dry-grass + scrub ~3 cards/m² on slopes ≥15°, 2.5 m off turf/sand/water/paths (0.8 m off a path, 4 m off a tee).
- **Rock faces**: only where LiDAR has >35° cells and photos show pale rock; real relief as normal + detail (never new ground), vertical rills, pale cream RGB 190-200/180-190/170-178, sparse sage on edges, loose rock at the foot.
- **Hillside ground**: graded satellite base only where the top surface is outer hillside rough, 6 m feather at turf/sand/water.

### Procedure
1. `coursekit.py vegetation` → `brush_sage_scrub.mask`, `grass_dry_hillside.mask`, `grass_deep_rough.mask`, `no_grass.mask` (1 px = 1 m, north up), `shrubs_clean.csv`; `--wash`, `--parkland` as needed. Keep scrub/golden-grass ground out of the rough mesh in the SVG.
2. Unity: Plant Shrubs (instanced chunks), Paint Grass and Brush from the masks; native-slopes script for in-play banks.
3. Photo pass per hillside, BoJa's own hillside photo first. Match cover, colour, bare-soil share, toe, accent bushes. Hole-only copies of the ground layer and sage material where a hill's tint differs.
4. Cliff/rock from the LiDAR steep band at the photographed bearing; report width and elevations; clear trees/shrubs on the face; remove pale patches draped on the wrong slope.
5. Model swaps: TR_Sage (0.88 × 1.7 m, no collider) base; dormant twiggy variant on forward-tee banks; native evergreen for accents/hedges.

### Checks
- Plants-on-turf = 0 after every pass.
- Hill mean colour inside the target band; if dark, lift the leaf tint, don't add plants.
- Cliff drapes on the right cells (mask rows not flipped).
- Saw-tooth soil/scrub/grass edges smoothed into one soft boundary.

### Pitfalls
- Autumn orange/red/yellow or vivid green scrub cards; pink buckwheat; 3 m shrubs on tree cards read as blossom trees.
- Flat grass cards at full sun on steep banks read bright; tone or use 3D plants.
- White streaked rock asset and seashell rocks: replace with rounded fieldstone/sandstone.
- Stretched textures on steep faces: triplanar or re-UV.
- Dry-rough re-skin read brick-brown (shader multiplies tint).
- Course rough meshes covering the whole square means hillside colour comes from the satellite base, not the terrain.

### Decision points (ask once, with a default)
**Needs an in-person look even with a newer scan:** cover class and colour of every flagged hillside (D1), rock vs scrub on steep cells (D3), the dirt toe line (D2), shrub vs tree (D18). BoJa's photo of each signature hillside is the spec.
1. Season look: dormant grey-brown (default) or green.
2. Density on signature hillsides (one photo per flagged hill).
3. Which hills are OB vs deep rough in play.
4. Accent species only from photos.
