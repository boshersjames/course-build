# 15. SpeedTree and tree models

Use this when a hole needs a hero tree, when a SpeedTree (desktop or Modeler) asset goes into a course, when a new species model is requested, or when deciding whether to pay for SpeedTree assets. Planting, species rules, corridors and colliders in general are in 06; this guide is the model side.

## What SpeedTree is to us

- **SpeedTree desktop trees** ship with the OPCD base project under `Assets/Trees/Broadleaf` and `Conifer` ("Desktop" prefabs; the "Mobile" ones are skipped). `PlantLidarTrees` plants the LiDAR list from these. They are the background trees on every course.
- **SpeedTree Modeler v10.2.0** is installed on the PC (`C:\Program Files\SpeedTree\...`) on an Indie licence. It is a GUI tool; the CLI only exports existing `.spm` files. Shaping a tree needs the GUI and BoJa's sign-in, so a SpeedTree-authored species is a BoJa step, not a cloud step.
- **No connector exists.** SpeedTree has no API; the only hand-off is a file export. A skill can carry the rules below, not drive the Modeler.
- **Our own models** (TR_PepperV2, TR_Oak, TR_Sage, TR_Pampas, TR_Cordyline, the fan palm) come from the tree thread as FBX, imported with Arboreum. Hero trees are built this way unless SpeedTree clearly wins in-game.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Import route | v10 `.spm`/`.st` won't import into Unity 2018.2. Export FBX with the "Unity" preset, no wind; import like the TR_* models (Arboreum, mask maps, LOD 20/6/0.5%) | tools 10/31 |
| Which trees get SpeedTree/hero models | Key trees next to greens, tee boxes, bunkers and hole signs; background trees stay on the low-detail desktop prefabs | BoJa 10/30 |
| Licence | Indie: the 8 hero oaks (12/15 signs and tees) went into the Tierra push as-is. Revisit licensing only if the courses are monetised; then the ~$1k Library. Single species packs ($100-200) only if a sample proves clearly better in-game. Oaks first (Tierra, Los Robles) | BoJa 10/30 |
| Library gaps | No pepper or willow in the SpeedTree library; the pepper was hand-built from the TreeShare mesquite; sycamore = pale mottled-bark variant of the oak | models_1029 |
| LOD / billboards | On the desktop prefabs remove the billboard LOD (`Broadleaf_Desktop_Billboard`, `Conifer_Desktop_Billboard` render magenta/null) and add leaf spheres up to the tops (Wilson 2,245 removed, 11,464 spheres; Hansen 2,995 / 10,022). On our models LOD 20/6/0.5% with a baked billboard checked against the LOD0 silhouette at 10-30 m | Wilson 1, Hansen 8c |
| Colliders | Desktop SpeedTree trees keep leaves as submeshes: fit the GSPleaves canopy spheres to the leaf submeshes (~80% of the leaf cloud), not the mesh bounds; trunk capsule GSPtimber; nothing invisible beyond the crown | Harding, playtest P1 |
| Wind | Scene has `SceneWindZone` + `ArboreumWindActivator`; export SpeedTree FBX without wind and let the scene drive it (GSPro's runtime carries the components) | Hansen audit |
| Scale | Keep the prefab root scale (Broadleaf Small = 0.5); size by LiDAR height ±15%; our models carry a scale note (TR_Oak Y = h / 12.63, XZ = crown / 12.8; TR_PepperMulti divisor 11.84) | 06 |
| Colour targets | Pepper r/g ~1.0; eucalyptus r/g 0.95-1.03, b/g 0.80-0.90; oak as textured (no leaf tint); pine r/g 1.0-1.08, b/g 0.60-0.78; measured in same-light frames | 06, 12 |
| Budget | A SpeedTree tree costs 2-2.7× the triangles of our model in view (Tierra 12 tee: 9.1 M vs 3.5 M tris, 116 vs 103 ms; 15 tee: 10.0 M vs 3.7 M, 96 vs 70 ms). Realism first, but keep the worst editor view ≤ ~33 ms in a build, so SpeedTree only where it is seen close | speedtree_perf.csv |

## Procedure: a new species model

1. Request from the photo audit: `model_requests_h<N>.md` with the named photos, size (height, crown, trunk clear), form words ("one soft mound, not spiky fans"), colour ranges as r/g and b/g per photo, and where it is placed. Priority by hole order.
2. Build (tree thread) → `tree_models/<species>/<Species>.fbx`, under ~3k tris for a hero tree, ~2k for a palm, cheap enough to place hundreds for shrubs/grasses; leaf cards double-sided or normals flipped before export.
3. Import as `TR_<Species>` (Arboreum, mask maps, LOD 20/6/0.5%); photo | model sheet from the named photos; measured colours; scale note.
4. Place by world position fitted to the pair pixels; hole-only material copies for any local tint; mark model-request spots in `trees.csv` until the model lands.
5. Perf check from the tee, aim and green views (`speedtree_perf.csv` columns: tris, draw calls, frame ms, trees at LOD0).

## Procedure: a SpeedTree asset

1. BoJa shapes/exports in the Modeler (Unity preset FBX, no wind) or picks a sample tree; confirm the licence allows it in a distributed course bundle before it goes into a GSPro push (open question for Indie; the draft support email is in `outreach/speedtree_support_question.md`, unsent by BoJa's choice).
2. Same import, sheet and colour checks as a TR_ model.
3. Side-by-side with our model at the same spot (`speedtree_vs_ours.jpg`) and the perf rows; it stays only if it reads better from the tee and the frame cost is acceptable.
4. Refit colliders to the leaf submeshes; remove its billboard LOD if it ships one that renders flat.

## Checks

- No magenta or null-material renderers (`CourseRealism.BadMaterials`: only disabled billboard renderers may report null).
- Both sides of every leaf card read solid on the model sheet.
- Billboard silhouette matches LOD0 at 10-30 m (no straight crop lines).
- Hero trees list per hole agreed before any swap; background trees untouched.
- Shot sweep 0 crowns hit beyond the visible leaves after collider refit.
- Beta gate (21): any billboard that stays must match the LOD0 colour; LOD transitions smooth at the far distance; wind driven by the scene zone only; canopy colliders are spheres/capsules on GSPleaves, never a mesh collider; Benchmark target FPS not dragged down by hero trees (perf rows per view).

## Pitfalls

- Importing `.spm`/`.st` into 2018.2 fails silently; export FBX.
- SpeedTree Modeler exports with wind baked in fight the scene wind zone; export without.
- The first TR_Sage billboard had a straight cut across its lower third; the pampas plumes came out flatter and greyer than the photo and needed a tint on a copy.
- Blobby cartoon crowns (old 6-7k-tri pepper) read worse than the 2.6-2.9k weeping rebuild and cost more.
- Photo-to-mesh generators (Meshy and similar) give solid blobby canopies with no leaf cards, LODs or wind: fine for props (sheds, pump stations, bins, boulders, monuments), not for trees or shrubs.
- A per-tree tint on a shared material (TR_Oak) changes every hero oak; use a hole-only instance.

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** which trees are the hero trees by each sign, tee and green, and their species (D18, never in any scan); photos of the hero trees from the tee side and the green side.

1. Hero trees per hole before any swap (default: the trees within 35 m of a tee, green or sign that a photo shows).
2. Buy a species pack or the Library (default: no; revisit when monetised).
3. SpeedTree vs our model for a given species from one side-by-side (default: ours unless SpeedTree reads better from the tee).

## Sources

`progress/tierra/models_1029/NOTES.md`, `speedtree_perf.csv`, `speedtree_vs_ours.jpg`, `model_requests_h12_h13.md` … `h16_h17.md`, `audit_1030/review_14.md` (hero rule); `outreach/speedtree_support_question.md`; `tierra-rejada-lidar/HANDOFF.md` (PlantLidarTrees); Wilson pass 1, Hansen pass 8c; checklist "SpeedTree decision", "Tools and look notes", "Trees and vegetation".

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-speedtree` but not in this playbook when the skill was cut to one page.


### What SpeedTree is to us
- **Desktop SpeedTree trees** ship with the OPCD base project (`Assets/Trees/Broadleaf`, `Conifer`, "Desktop" prefabs; "Mobile" skipped). `PlantLidarTrees` plants the LiDAR list from these; they are the background trees on every course.
- **SpeedTree Modeler v10.2.0** is on the PC (Indie licence). GUI only; the CLI only exports existing `.spm`. Shaping a species is a BoJa step, not a cloud step.
- **No connector exists**: SpeedTree has no API; the hand-off is a file export.

### Defaults that worked
- **Import route**: v10 `.spm`/`.st` won't import into Unity 2018.2; export FBX with the "Unity" preset, no wind; import like the TR_* models (Arboreum, mask maps, LOD 20/6/0.5%).
- **Which trees get hero models**: key trees next to greens, tee boxes, bunkers and hole signs; background trees stay on the low-detail desktop prefabs.
- **Licence**: Indie; the 8 hero oaks went into the Tierra push as-is. Revisit only if monetised (then the ~$1k Library). Single species packs ($100-200) only if a sample proves clearly better in-game. Oaks first (Tierra, Los Robles).
- **Library gaps**: no pepper or willow; the pepper was hand-built from the TreeShare mesquite; sycamore = pale mottled-bark oak variant.
- **LOD / billboards**: on the desktop prefabs remove the billboard LOD (`*_Desktop_Billboard` renders magenta/null) and add leaf spheres to the tops (Wilson 2,245 removed / 11,464 spheres; Hansen 2,995 / 10,022). On our models LOD 20/6/0.5% with the billboard checked against the LOD0 silhouette at 10-30 m.
- **Colliders**: desktop trees keep leaves as submeshes; fit GSPleaves spheres to the leaf submeshes (~80% of the leaf cloud), never mesh bounds; trunk capsule GSPtimber; never a mesh collider on Leaves.
- **Wind**: scene `SceneWindZone` + `ArboreumWindActivator`; export without wind and let the scene drive it.
- **Scale**: keep prefab root scale (Broadleaf Small = 0.5); size by LiDAR height ±15%; our models carry a scale note (TR_Oak Y = h / 12.63, XZ = crown / 12.8; TR_PepperMulti divisor 11.84).
- **Colour targets** (same-light frames): pepper r/g ~1.0; eucalyptus r/g 0.95-1.03, b/g 0.80-0.90; oak as textured, no leaf tint; pine r/g 1.0-1.08, b/g 0.60-0.78.
- **Budget**: a SpeedTree tree costs 2-2.7× the triangles of ours in view (Tierra 12 tee: 9.1 M vs 3.5 M tris, 116 vs 103 ms; 15 tee: 10.0 M vs 3.7 M). Realism first, worst view ≤ ~33 ms in a build; SpeedTree only where seen close.

### Procedure: a new species model
1. Request from the photo audit (`model_requests_h<N>.md`): named photos, size (height, crown, trunk clear), form words, colour ranges as r/g and b/g per photo, placement. Priority by hole order.
2. Build (tree thread) → `tree_models/<species>/<Species>.fbx`, under ~3k tris for a hero tree, ~2k for a palm, cheap for shrubs/grasses; leaf cards double-sided or normals flipped before export.
3. Import as `TR_<Species>` (Arboreum, mask maps, LOD 20/6/0.5%); photo | model sheet; measured colours; scale note.
4. Place by world position fitted to the pair pixels; hole-only material copies for local tint; mark request spots in `trees.csv` until the model lands.
5. Perf check from tee, aim and green views (tris, draw calls, frame ms, trees at LOD0).

### Procedure: a SpeedTree asset
1. BoJa shapes/exports in the Modeler (Unity preset FBX, no wind) or picks a sample; confirm the licence allows a distributed course bundle before a GSPro push (open question for Indie; draft support email in `outreach/`, unsent by BoJa's choice).
3. Side-by-side with our model at the same spot plus perf rows; it stays only if it reads better from the tee at acceptable cost.
4. Refit colliders to the leaf submeshes; remove a billboard LOD that renders flat.

### Checks
- No magenta or null-material renderers (only disabled billboard renderers may report null).
- Both sides of every leaf card solid on the sheet.
- Billboard silhouette matches LOD0 at 10-30 m; LOD transitions smooth (beta gate).
- Hero list per hole agreed before any swap; background trees untouched.
- Shot sweep 0 crowns hit beyond the visible leaves.

### Pitfalls
- `.spm`/`.st` into 2018.2 fails silently; export FBX.
- Baked SpeedTree wind fights the scene wind zone.
- The first TR_Sage billboard had a straight cut across its lower third; the pampas plumes came out flatter and greyer than the photo.
- Photo-to-mesh generators (Meshy and similar) give solid blobby canopies with no leaf cards, LODs or wind: fine for props, not trees or shrubs.
- A per-tree tint on a shared material changes every hero oak; use a hole-only instance.

### Decision points (ask once, with a default)
**In person even with a newer scan:** which trees are the hero trees by each sign, tee and green, and their species (D18); photos from the tee side and green side.
1. Hero trees per hole before any swap (default: trees within 35 m of a tee, green or sign that a photo shows).
3. SpeedTree vs our model per species from one side-by-side (default: ours unless SpeedTree reads better from the tee).
