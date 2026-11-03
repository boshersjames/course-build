# 06. Trees

Use this for LiDAR tree extraction, species assignment, planting, colliders, corridors, hero trees, and new tree models.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Source | LiDAR trees within 150 m of play (Tierra 1,412; Penmar 534; Hansen 2,022 + 1,959 offsite to 300 m); keep every one, including brown/dead-looking ones | Penmar 10/28 |
| Cleanup | Drop pylons/wires (256 on Moorpark), yard trees, shrubs misread as trees (Tierra: 2,700 "trees" ≥3 m were shrubs); list removals with reasons | tree_clean.py |
| Size | Planted height within ~15% of LiDAR; keep the prefab root scale (Broadleaf Small = 0.5); ground raycast hits ground/course meshes only (never other trees) | Wilson/Harding |
| Species rule (SoCal) | >20 m eucalyptus; oaks otherwise with ~10% pepper; within 35 m of a tee or green, oaks ≤14 m become peppers (Tierra: 426 peppers); palms = tall straight Mexican fan palms, never coconut | vegetation_1029, Hansen |
| Hero trees | Key trees next to greens, tees, bunkers and signs get the high-quality models and per-tree fitting; background trees stay low-detail | BoJa 10/30 |
| Colliders | Trunk capsule GSPtimber; canopy spheres GSPleaves at ~80% of the leaf cloud, fitted to the leaf submeshes (not mesh bounds), up to the tops; no invisible branches | playtest P1, Harding |
| LOD / billboards | LOD 20/6/0.5% (Arboreum models); on SpeedTree desktop trees remove the billboard LOD and add leaf spheres to the tops (2,245 removed on Wilson, 2,995 on Hansen) so nothing reads flat or magenta | Wilson pass 1 |
| Bunker clearance | ≥1.3× LiDAR crown radius + 0.5 m from sand | Wilson/Harding |
| Corridors | See 05 Tees; nudge up to ~12 m, scale 0.8-0.9, delete last | Penmar |
| Skirts | Weeping crowns keep ≥2.5 m clear above paths, tees and turf | 18 |
| Model pipeline | FBX → Arboreum import, mask maps, LOD 20/6/0.5%, photo \| model sheet, measured colours (r/g, b/g) against named photos, scale note (e.g. TR_Oak Y = LiDAR h / 12.63, XZ = crown / 12.8; TR_PepperMulti divisor 11.84) | model_requests_* |

## Procedure

1. `find_trees.py` / `coursekit.py` → `trees.csv` (id, x, z, height, crown, source); `tree_species.py` assigns species by height and distance to tee/green; `offsite_trees.py` for the neighbourhood.
2. Overlay trees on the satellite (`trees_on_satellite.png`): 3D trees stand where the satellite shows trees and shadows; no shadows on bare fairway.
3. Plant (`PlantLidarTrees`), then swap species, then refit colliders, then the corridor and bunker passes, then the shot sweep from every tee (10-45°, 50-300 yd, ±10°): only visible-crown hits allowed.
4. Photo pass per hole: wrong species and wrong positions are the biggest per-hole gap after hillsides. Delete the blocking "giant weeping tree 30 m off the tee" type errors first (12, 17), then plant the photographed trees by world position fitted to the pair pixels, marking model-request spots in `trees.csv` until the model lands.
5. New models only through the pipeline above; placeholders tinted on hole-only copies and marked for the swap.

## Checks

- 0 crowns over sand, 0 corridor hits, 0 floating trees, no magenta, no orphan colliders.
- Shot sweep: 0 shots through crowns (was 29 of 54 before the refit).
- Species colour targets met in the render (pepper r/g ~1.0; eucalyptus r/g 0.95-1.03, b/g 0.80-0.90; oak bluish-green as textured; pine r/g 1.0-1.08, b/g 0.60-0.78).
- Flyover first frame clear.
- Beta gate (21): billboard colours match the highest LOD; LOD transitions smooth and barely noticeable; no trees or grass on cart paths; wind consistent across trees, plants and grass; no mesh collider set to Leaves (capsule/sphere only), no collider without a physics material.

## Pitfalls

- SpeedTree v10 .spm/.st won't import into Unity 2018.2; export FBX ("Unity" preset, no wind). No pepper or willow in its library; the pepper was hand-built from a mesquite.
- Arboreum imports keep single-sided leaf cards, so a model whose back faces weren't flipped shows see-through gaps and dark patches from one side (the 14 shed wall and the first sage had the same read); check both sides on the model sheet and use a double-sided material or flipped normals before import.
- Billboard bakes crop: the first TR_Sage LOD2 billboard had a straight cut across its lower third (fixed at 078fecc); check the billboard against the LOD0 silhouette at 10-30 m.
- The old pepper model was blobby cartoon crowns (6-7k tris); the weeping rebuild (2.6-2.9k) matched the photos and cost less.
- The palm skirt read as a flat card/"trunk billboard" from the tee (13); skirts need volume.
- Satellite "tree" checks dropped real brown trees (cypress, winter colour): never cull on colour.
- A per-tree tint on a shared material (TR_Oak) changes the hero oaks elsewhere; lighten one tree only via a hole-only material instance.
- Fan palm prefabs with "plaid" trunk textures and floating crowns; a procedural palm read as a broom.
- Photo colour checks on peppers must use same-light frames (low morning sun shifts r/g).

## Decision points for BoJa

**Needs an in-person look even with a newer scan:** tree species near tees and greens (D18, never in any scan), tree vs shrub vs hedge vs building on ambiguous returns, and the hero trees list.

LiDAR review decision point for vegetation returns: **D18** (tree, shrub, hedge or building) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Hero trees per hole (which are "the" oaks/peppers by the signs and tees) before any swap.
2. Pay for single SpeedTree species packs ($100-200) only if samples prove better in-game; no Library until monetised. Oaks first (Tierra, Robles).
3. Tree density targets at entrances/backdrops when the LiDAR is sparse ("the trees are a little denser at the entrance").

## Sources

`progress/tierra/vegetation_1029/README.md`, `models_1029/NOTES.md`, `model_requests_h12_h13.md` … `h16_h17.md`, `after_playtest_1031/README.md`; `unity_automation/SUMMARY_2026-10-28_playtest.md`; Wilson pass 1, Hansen pass 8c; `tools/tree_clean.py`, Hansen `tree_species.py`, `offsite_trees.py`; checklist "Trees and vegetation", "SpeedTree decision", "Tools and look notes".
