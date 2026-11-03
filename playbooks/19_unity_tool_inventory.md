# 19. Unity tool inventory

Use this before writing any editor script for a new course. Most of what a build needs already exists as a menu item or a batch job; reuse it, parameterise it from `course.json`, and only write new code for what is missing. Tools live in `unity_editor/` on `claude/moorpark-unity` (the shared set), in each course's thread branch, and in `tierra-rejada-lidar/unity_automation/` (batch runner).

## Shared editor tools (`unity_editor/`, Unity 2018.2.8f1)

| Menu | Script | Input | What it does | Notes |
|---|---|---|---|---|
| Moorpark > Set Up Scene | `MoorparkSetup.cs` | `heightmap_2049.raw`, satellite, `course.json` | Builds the scene: terrain (square × height × square, y = elevation − base), satellite as the only texture, light, camera | Rerun any time to rebuild; rename per course |
| Moorpark > Add Course Meshes | `MoorparkMeshes.cs` | OPCD meshes from `course.svg` | Imports the course meshes, assigns the theme materials, conforms terrain 0.4 m under meshes | 14, 12 |
| Tierra > Plant LiDAR Trees | `PlantLidarTrees.cs` | `trees.csv` (id, x, z, height, crown, species), prefab folder | Plants Desktop SpeedTree/Arboreum prefabs at LiDAR height, trunk GSPtimber, canopy GSPleaves, parent `LIDAR_TREES` | Skips "Mobile" prefabs; keep root scale; 06 |
| Tierra > Plant Shrubs | `TierraPlantShrubs.cs` | `shrubs_clean.csv`, shrub prefabs/atlas | Instanced shrub chunks, parent `SHRUBS` | 07 |
| Tierra > Paint Grass and Brush | `TierraPaintGrass.cs` | `terrain_masks/*.mask` (1 px = 1 m, north up) | Paints detail grass and brush layers from the masks | Check mask orientation on a known feature first |
| Tierra > Vegetation (SoCal set) | `TierraVegetationSetup.cs` | | Sets up the SoCal detail prototypes (sage, dry grass, deep rough tufts) and densities | 07 |
| Tierra > Build Cart Paths | `BuildCartPaths.cs` | `cartpaths.csv` | 2.4 m concrete strips, GSPconcrete, lifted above the surface | 09 |
| Course Look > Cart Paths and Turf | `CourseLook.cs` + `course_look_settings.json` | graded satellite, path material settings | Applies the shared concrete look to paths and the graded satellite to every layer that used the original | 12; check the found count first |
| Course Tools > Flyover Preview | `CourseFlyoverPreview.cs` | the course GKD | Renders the flyover path per hole with the speed guard (yaw/pitch ≤5°/s), first frame on the sign, end on the green | 13 |
| Course Tools > Automation > Run pipeline / Screenshots only / Inspect scene | `CourseAutomation.cs` + `<Course>.json` | config JSON | Batch-mode runner: opens the scene, runs the named passes, screenshots, writes a report | Driven by `Run-CourseAutomation.ps1` / `Send-UnityCommand.ps1` |
| Moorpark > Build Clubhouse | `MoorparkClubhouse.cs` + `clubhouse_spec.txt` | spec file | Clubhouse shell, roof, hardscape, planters, carts from the spec | 11 |
| Moorpark > Range Logo | `MoorparkLandmarks.cs` | club logo SVG | Drapes a traced logo on the range hillside; other landmarks | 11 |
| (no menu) | `MoorparkWater.cs`, `MoorparkClearTees.cs`, `MoorparkFbxImport.cs`, `MoorparkLook.cs`, `CourseHoleNames.cs`, `MoorparkBuild.cs` | | Water dig and planes (08), tee pad clears (05), FBX import with Arboreum settings (15), look presets (12), hole naming, batch build entry | Called from the automation runner |

## Course-specific builders (pattern, not shared code)

- `Tierra*.cs` builders live in editor B/C only and produce packages; `TierraApply_<pkg>.Apply` is the one script that runs in A. Each package carries its island check, the hole-10 control diff (0.0), physics material on every collider, and `adel.json` for objects to `SetActive(false)` rather than delete (00).
- Gate checks (`TierraGate10` style): islands, plants-on-turf, floating objects, bins, fence continuity, vertex paint, magenta/null materials, turf control. Copy the set per course and keep the names.
- Hansen/Wilson pass scripts: `BunkerDepth Apply Graded`, `SandLook`, `CanopyCover` (leaf spheres to the tops), corridor sweep, `FlyoverTees`, `BadMaterials`, `MeshStats`, `Pins18`, `ShadowAudit`, `SetMeshMaterials`. These are the course-wide one-shots a new course runs first.
- Penmar: `PenmarProps.cs`, `ChainLinkFence.cs`; Tierra: `BuildFences.cs` (fences, walls, nets), `DrawArea.cs` (click-drawn rough/OB patches), `TierraRope`, `TierraNativeSlopes`, `TierraPalm.Build`, `BuildClubhouse.cs`.

## Batch and remote running

- `unity_automation/Run-CourseAutomation.ps1` starts Unity in batch mode on the PC with the config JSON; `Send-UnityCommand.ps1` sends a single command to a running editor; reports land in `Documents\<course>\automation\`.
- Batch mode leaves skybox ambient black: set trilight ambient ×1.3 + fill 0.28 before screenshots (12).
- One editor per role (A edits, B/C render and build packages); never export or copy back from B/C (00).
- Backups before every pass: scene, terrain data, GKD, to `Backups\<pass>\`.

## Procedure: new course setup

1. Own Unity project per course (`C:\GSP\<Name>_Project`), own scene, copy `unity_editor/` in, and replace every constant with a read from `course.json` (square size, base elevation, satellite size). Grep for `2000`, `3000`, `182`, `Tierra`, `Moorpark` before the first run.
2. Set Up Scene → Add Course Meshes → Plant LiDAR Trees → Plant Shrubs → Paint Grass → Cart Paths → Course Look → the course-wide one-shots (bunker depth, sand look, canopy cover, corridors, bad materials) → Flyover Preview.
3. Run the gate check set and fix to zero before the first review.
4. Add a course-specific builder only when a feature isn't covered (water, clubhouse, fences from CSV are covered).

## Pitfalls

- A copied `3000` stretched the satellite 1.5× on Penmar and Harding; `surrounds.py` had `S = 2000` hard-coded.
- The small tree prefab was planted at double size and trees stood on other trees' canopies until the planter raycast was restricted to ground/course meshes (Wilson).
- A menu still named "Tierra" on Moorpark confused the hand-off; keep the names but say so in the course README.
- Scripts that both build and apply in A made rollbacks hard; the build-in-B/apply-in-A split is the fix.
- Per-hole material edits on shared materials leaked across holes (12); builders assign copies.

## Decision points for BoJa

1. Whether to merge the course-specific builders into the shared `unity_editor/` set now or after the next course (default: after Wilson proves the set).
2. Two editors or one on the PC that day (RAM under ~4 GB free means one).

## Sources

`unity_editor/*.cs` and `OPEN_ME.md` on `claude/moorpark-unity`; `tierra-rejada-lidar/unity_automation/README.md`, `PC_SETUP.md`, `TierraRejada.json`; `HANDOFF.md` (Tierra menu); Wilson pass 1 and Hansen pass 8 batch outputs; 00 process guide.

## Carried over from the skill (2026-11-03)

Lines that were in `course-build-unity-tools` but not in this playbook when the skill was cut to one page.


### Shared editor tools
- **Moorpark > Set Up Scene** (`MoorparkSetup.cs`): terrain from `heightmap_2049.raw` (square × height × square, y = elevation − base), satellite as the only texture, light, camera. Rerun to rebuild.
- **Moorpark > Add Course Meshes** (`MoorparkMeshes.cs`): imports OPCD meshes from `course.svg`, theme materials, terrain conformed 0.4 m under meshes.
- **Tierra > Plant LiDAR Trees** (`PlantLidarTrees.cs`): `trees.csv` + prefab folder → Desktop SpeedTree/Arboreum trees at LiDAR height, GSPtimber trunks, GSPleaves canopies, parent `LIDAR_TREES`; skips Mobile prefabs; raycast to ground/course meshes only.
- **Tierra > Plant Shrubs** (`TierraPlantShrubs.cs`): `shrubs_clean.csv` → instanced chunks under `SHRUBS`.
- **Tierra > Paint Grass and Brush** (`TierraPaintGrass.cs`): `terrain_masks/*.mask` (1 px = 1 m, north up); check mask orientation on a known feature first.
- **Tierra > Vegetation (SoCal set)** (`TierraVegetationSetup.cs`): detail prototypes and densities.
- **Tierra > Build Cart Paths** (`BuildCartPaths.cs`): `cartpaths.csv` → 2.4 m GSPconcrete strips, lifted.
- **Course Look > Cart Paths and Turf** (`CourseLook.cs` + `course_look_settings.json`): shared concrete look, graded satellite on every layer that used the original; check the found count.
- **Course Tools > Flyover Preview** (`CourseFlyoverPreview.cs`): per-hole flyover from the GKD with the ≤5°/s guard, first frame on the sign, end on the green.
- **Course Tools > Automation > Run pipeline / Screenshots only / Inspect scene** (`CourseAutomation.cs` + `<Course>.json`): batch runner driven by `Run-CourseAutomation.ps1` / `Send-UnityCommand.ps1`; reports to `Documents\<course>\automation\`.
- **Moorpark > Build Clubhouse** (`MoorparkClubhouse.cs` + `clubhouse_spec.txt`); **Moorpark > Range Logo** (`MoorparkLandmarks.cs`).
- No menu, called by the runner: `MoorparkWater.cs` (dig and planes), `MoorparkClearTees.cs`, `MoorparkFbxImport.cs` (Arboreum settings), `MoorparkLook.cs`, `CourseHoleNames.cs`, `MoorparkBuild.cs` (batch build entry).

### Course-specific builders (the pattern)
- `Tierra*.cs` builders run in editor B/C only and produce packages; `TierraApply_<pkg>.Apply` is the one script that runs in A. Each package carries its island check, the hole-10 control diff (0.0), physics material on every collider, and `adel.json` for objects to `SetActive(false)` rather than delete.
- Gate check set (`TierraGate10` style): islands, plants-on-turf, floating objects, bins, fence continuity, vertex paint, magenta/null materials, turf control. Copy per course, keep the names.
- Course-wide one-shots from Hansen/Wilson: `BunkerDepth Apply Graded`, `SandLook`, `CanopyCover` (leaf spheres to the tops), corridor sweep, `FlyoverTees`, `BadMaterials`, `MeshStats`, `Pins18`, `ShadowAudit`, `SetMeshMaterials`.
- Others: `BuildFences.cs` (fences, walls, nets), `DrawArea.cs`, `TierraRope`, `TierraNativeSlopes`, `TierraPalm.Build`, `BuildClubhouse.cs`, Penmar `PenmarProps.cs`, `ChainLinkFence.cs`.

### Batch and remote running
- `Run-CourseAutomation.ps1` starts Unity in batch mode with the config JSON; `Send-UnityCommand.ps1` sends one command to a running editor.
- Batch mode leaves skybox ambient black: trilight ambient ×1.3 + fill 0.28 before screenshots.
- One editor per role (A edits, B/C render and build packages); never export or copy back from B/C.

### New course setup
1. Own Unity project (`C:\GSP\<Name>_Project`), own scene; copy `unity_editor/` in; every constant read from `course.json`. Grep for `2000`, `3000`, `182`, `Tierra`, `Moorpark` before the first run.
2. Set Up Scene → Add Course Meshes → Plant LiDAR Trees → Plant Shrubs → Paint Grass → Cart Paths → Course Look → course-wide one-shots → Flyover Preview.
3. Gate check set to zero before the first review.
4. A course-specific builder only when a feature isn't covered.

### Pitfalls
- A copied `3000` stretched the satellite 1.5×; `surrounds.py` had `S = 2000` hard-coded.
- The small tree prefab planted at double size and trees standing on canopies until the raycast was restricted.
- Menus still named "Tierra" on other courses; keep the names but say so in the course README.
- Scripts that both build and apply in A made rollbacks hard; build-in-B/apply-in-A is the fix.
- Per-hole edits on shared materials leaked across holes; builders assign copies.

### Decision points (ask once, with a default)
1. Merge course-specific builders into the shared set now or after the next course (default: after Wilson proves the set).
2. Two editors or one that day (under ~4 GB free RAM means one).


## Physics labelling is audited, not assumed (2026-11-03)

GSPro's ball physics come from the physic material on each collider. The names of the OPCD meshes say what each piece is meant to be and the course SVG says what the ground is at every point, so both are ground truth for an audit. `CoursePhysicsAudit.cs` compares every collider's material with the class its name implies (`rules/physics.json` name_rules, first match wins, 'deep' before 'rough'), applies the unambiguous fixes on request, and writes an unsure list with positions for a human to label; its 2 m census is compared with the SVG by `tools/physics_check.py`, which reports the mismatch share per class against `physics.census.mismatch_tolerance_pct` and names the offending meshes. Trees are left to the tree tools (trunk GSPtimber, canopy GSPleaves). Run the audit before every install; the publish skill lists it.
