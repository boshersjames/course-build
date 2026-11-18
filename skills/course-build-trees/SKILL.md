---
name: "course-build-trees"
description: "Use when extracting, planting, swapping species, fitting colliders, clearing corridors or requesting new tree models in a GSPro course build."
---

# Course build: trees

Follow the standing rules in `course-build-gates`. LiDAR review decision point D18 (tree, shrub, hedge or building) in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo).

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Source | LiDAR trees within 150 m of play (Tierra 1,412; Penmar 534; Hansen 2,022 + 1,959 offsite to 300 m); keep every one, including brown or dead-looking | Penmar |
| Cleanup | Drop pylons/wires, yard trees, shrubs misread as trees (Tierra: 2,700 "trees" ≥3 m were shrubs); list removals with reasons | tree_clean.py |
| Size | Planted height within ~15% of LiDAR; keep the prefab root scale (Broadleaf Small = 0.5); ground raycast hits ground/course meshes only | Wilson/Harding |
| Species rule (SoCal) | >20 m eucalyptus; oaks otherwise with ~10% pepper; within 35 m of a tee or green, oaks ≤14 m become peppers; palms are tall straight Mexican fan palms, never coconut. The base only: photos set the species where they show the tree (step 4) | Tierra, Hansen |
| Hero trees | Trees next to greens, tees, bunkers and signs get high-quality models and per-tree fitting; background trees stay low-detail | BoJa 10/30 |
| Colliders | Trunk capsule GSPtimber; canopy spheres GSPleaves at ~80% of the leaf cloud, fitted to leaf submeshes (not mesh bounds), up to the tops; no invisible branches | playtest, Harding |
| LOD / billboards | LOD 20/6/0.5% (Arboreum); on SpeedTree desktop trees remove the billboard LOD and add leaf spheres to the tops so nothing reads flat or magenta | Wilson, Hansen |
| Bunker clearance | ≥1.3× LiDAR crown radius + 0.5 m from sand | Wilson/Harding |
| Corridors | Nudge up to ~12 m, scale 0.8-0.9, delete last (see `course-build-tees`) | Penmar |
| Skirts | Weeping crowns ≥2.5 m clear above paths, tees and turf | Tierra 18 |
| Model pipeline | FBX → Arboreum import, mask maps, LOD 20/6/0.5%, photo \| model sheet, measured colours (r/g, b/g) against named photos, scale note (TR_Oak Y = LiDAR h / 12.63, XZ = crown / 12.8; TR_PepperMulti divisor 11.84) | model_requests |

## Tree physics (11/10)

- Where the course notes say a ball hitting the branches drops (Hansen 5, 13, 16; Tierra 11's corner tree), the crown needs a leaves collider: capsule or sphere on GSPleaves. A mesh collider set to Leaves shows orange in the Collider Inspector and is not allowed.
- Trunks are always solid (GSPtimber). The shot test lists crowns balls fly through (NOT_SOLID), mostly-empty leaf spheres (OVERSIZED_COLLIDER) and colliders with no visible tree (INVISIBLE_COLLIDER, which come out): see `course-build-shot-test`.
- Unreal hero clips: trees come from the species library and pass a look-check alone first (`course-build-unreal`).

## Procedure

1. `trees.csv` (id, x, z, height, crown, source); `tree_species.py` by height and distance to tee/green; `offsite_trees.py` for the neighbourhood.
2. Overlay on the satellite: 3D trees where the satellite shows trees and shadows; no shadows on bare fairway.
3. Plant, swap species, refit colliders, corridor and bunker passes, then the shot sweep from every tee (10-45°, 50-300 yd, ±10°): only visible-crown hits.
4. **Photo enhancement per hole (BoJa 11/16)**, via the procedure in `course-build-photo-index`, with BoJa's notes: the LiDAR list and the species-by-height rule are only the base, and every tree group a photo shows is checked against the photos for species, position and size. Photos set the species. Where a photo estimate of position or height disagrees with the LiDAR, a high/medium pose fitted to registered landmarks makes the tree eligible for review; it never automatically overrides a reliable LiDAR tree. A change needs a recorded reason why the photo estimate is stronger in that case (for example, the LiDAR crown merges two trees, or the tree was planted or removed after the survey); without one, the LiDAR position and height stay and the tree is `open` with one question to BoJa. A tree whose base already matches its photos is recorded `verified unchanged`. Wrong species and positions are the biggest gap after hillsides. Copy the tree, not its damage: broken limbs, storm stumps and stakes are left out unless BoJa asks (real dormant or brown trees still stay, see Pitfalls). Re-run the step 3 corridor, collider and shot-sweep passes on whatever this changes. Delete blocking "giant weeping tree 30 m off the tee" errors first, then plant photographed trees by world position fitted to the pair pixels; mark model-request spots in `trees.csv` until the model lands.
5. New models only through the pipeline; placeholders tinted on hole-only copies and marked for the swap.

## Checks

- 0 crowns over sand, 0 corridor hits, 0 floating trees, no magenta, no orphan colliders.
- Corridor, clearing and swap passes move or remove whole tree instances (all LODs, colliders and impostors together), never parts of a combined mesh (`course-build-brush` for combined tiles).
- Shot sweep: 0 shots through crowns.
- Colour targets in the render: pepper r/g ~1.0; eucalyptus r/g 0.95-1.03, b/g 0.80-0.90; pine r/g 1.0-1.08, b/g 0.60-0.78.
- Flyover first frame clear.

## Pitfalls

- SpeedTree v10 .spm/.st won't import into Unity 2018.2; export FBX ("Unity" preset, no wind). No pepper or willow in its library.
- Arboreum keeps single-sided leaf cards: a model whose back faces weren't flipped shows see-through gaps and dark patches from one side. Check both sides on the model sheet; double-sided material or flipped normals before import.
- Billboard bakes crop: the first TR_Sage LOD2 billboard had a straight cut across its lower third. Check the billboard against the LOD0 silhouette at 10-30 m.
- The old pepper was blobby cartoon crowns (6-7k tris); the weeping rebuild (2.6-2.9k) matched the photos and cost less.
- Palm skirts read as flat cards from the tee; skirts need volume. Avoid plaid trunk textures and floating crowns.
- Never cull trees on satellite colour: real brown trees (cypress, winter colour) got dropped.
- A per-tree tint on a shared material changes the hero trees elsewhere; use a hole-only material instance.
- Pepper colour checks need same-light frames (low sun shifts r/g).

## Ask BoJa once (with a default)

**In person even with a newer scan:** species near tees and greens (D18), tree vs shrub vs hedge vs building on ambiguous returns, the hero trees list.

1. Hero trees per hole before any swap.
2. Single SpeedTree species packs ($100-200) only if samples prove better in-game; no Library until monetised; oaks first.
3. Density targets at entrances/backdrops where the LiDAR is sparse.