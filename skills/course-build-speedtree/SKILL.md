---
name: "course-build-speedtree"
description: "Use when a GSPro hole needs a hero tree, a SpeedTree asset goes into a course, a new species model is requested, or SpeedTree licensing comes up."
---

# SpeedTree and tree models

Use with `course-build-trees` (planting, species rules, corridors, colliders). This is the model side. Full guide: `course-build/playbooks/15_speedtree_and_tree_models.md` in the moorpark repo.

## What SpeedTree is to us

- **Desktop SpeedTree trees** ship with the OPCD base project (`Assets/Trees/Broadleaf`, `Conifer`, "Desktop" prefabs; "Mobile" skipped). `PlantLidarTrees` plants the LiDAR list from these; they are the background trees on every course.
- **SpeedTree Modeler v10.2.0** is on the PC (Indie licence). GUI only; the CLI only exports existing `.spm`. Shaping a species is a BoJa step, not a cloud step.
- **No connector exists**: SpeedTree has no API; the hand-off is a file export.
- **Our own models** (TR_PepperV2, TR_Oak, TR_Sage, TR_Pampas, TR_Cordyline, the fan palm) come from the tree thread as FBX, imported with Arboreum. Hero trees are built this way unless SpeedTree clearly wins in-game.

## Defaults that worked

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

## Procedure: a new species model

1. Request from the photo audit (`model_requests_h<N>.md`): named photos, size (height, crown, trunk clear), form words, colour ranges as r/g and b/g per photo, placement. Priority by hole order.
2. Build (tree thread) → `tree_models/<species>/<Species>.fbx`, under ~3k tris for a hero tree, ~2k for a palm, cheap for shrubs/grasses; leaf cards double-sided or normals flipped before export.
3. Import as `TR_<Species>` (Arboreum, mask maps, LOD 20/6/0.5%); photo | model sheet; measured colours; scale note.
4. Place by world position fitted to the pair pixels; hole-only material copies for local tint; mark request spots in `trees.csv` until the model lands.
5. Perf check from tee, aim and green views (tris, draw calls, frame ms, trees at LOD0).

## Procedure: a SpeedTree asset

1. BoJa shapes/exports in the Modeler (Unity preset FBX, no wind) or picks a sample; confirm the licence allows a distributed course bundle before a GSPro push (open question for Indie; draft support email in `outreach/`, unsent by BoJa's choice).
2. Same import, sheet and colour checks as a TR_ model.
3. Side-by-side with our model at the same spot plus perf rows; it stays only if it reads better from the tee at acceptable cost.
4. Refit colliders to the leaf submeshes; remove a billboard LOD that renders flat.

## Checks

- No magenta or null-material renderers (only disabled billboard renderers may report null).
- Both sides of every leaf card solid on the sheet.
- Billboard silhouette matches LOD0 at 10-30 m; LOD transitions smooth (beta gate).
- Hero list per hole agreed before any swap; background trees untouched.
- Shot sweep 0 crowns hit beyond the visible leaves.

## Pitfalls

- `.spm`/`.st` into 2018.2 fails silently; export FBX.
- Baked SpeedTree wind fights the scene wind zone.
- The first TR_Sage billboard had a straight cut across its lower third; the pampas plumes came out flatter and greyer than the photo.
- Blobby cartoon crowns (old 6-7k-tri pepper) read worse than the 2.6-2.9k weeping rebuild and cost more.
- Photo-to-mesh generators (Meshy and similar) give solid blobby canopies with no leaf cards, LODs or wind: fine for props, not trees or shrubs.
- A per-tree tint on a shared material changes every hero oak; use a hole-only instance.

## Decision points (ask once, with a default)

**In person even with a newer scan:** which trees are the hero trees by each sign, tee and green, and their species (D18); photos from the tee side and green side.

1. Hero trees per hole before any swap (default: trees within 35 m of a tee, green or sign that a photo shows).
2. Buy a species pack or the Library (default: no; revisit when monetised).
3. SpeedTree vs our model per species from one side-by-side (default: ours unless SpeedTree reads better from the tee).