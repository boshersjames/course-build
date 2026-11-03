---
name: "course-build-unity-tools"
description: "Use before writing any Unity editor script for a GSPro course: the menus, batch jobs and course builders that already exist, their inputs, and the new-course setup order."
---

## Tools
- `tools/run_pipeline.py`: drives the tools below in order through the live bridge (inbox/outbox JSON, same protocol as Send-UnityCommand.ps1) and logs every step; use it instead of hand-sending six invokes. Batch mode cannot replace it: calibration and 4K renders need post-processing, which batch mode does not run.
- Unity `Course Checks > Physics: audit / apply` (`CoursePhysicsAudit.cs`, bridge `invoke -Tool CoursePhysicsAudit -Method Audit` then `-Method ApplyFixes`): physic material on every collider vs its mesh name per `rules/physics.json`; fixes the name-matched ones, lists the unsure ones with x,z for hand labelling and marks a ground material under a structure name (`physics.audit.review_when`) as `review`, never auto-fixed; then `tools/physics_check.py` compares its census to the SVG, counting blend strips as edge and `physics.census.overlays` (paths, shrubs, rocks, fences, outer deep rough on the SVG rough fallback) as overlay, not mismatch.

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `..\course-build\automation\Run-CourseAutomation.ps1 -SyncTools [-CourseJson <path>] [-Steps "a,b"] [-NoShots]`: reads `course.json` (project path, config), copies `unity/shared/*.cs`, the course's own `unity_editor/*.cs`, `course.json` and `rules/*.json` into the project, runs the batch; output under `AutomationOutput/<stamp>/summary.md`.
- `..\course-build\automation\Send-UnityCommand.ps1 ping|run|shots|invoke|describe|menu|save|refresh [-Tool <class>] [-Method <method>] [-Steps ...] [-Path <menu path>]`: the live bridge; `describe -Tool X` lists a tool's menu items, fields and methods before you call it.
- Unity `Course Checks > Greens: all-18 report` (`CourseGreenChecks.cs`, `RunAll` for the bridge): the all-18 green table; written in the cloud, compile it once on the PC.
- Unity `Course Look > Apply Tint JSON` (`ApplyTintJson.cs`): tint multipliers from `colour_match.py --json`; written in the cloud, compile it once on the PC.
- Unity `Course Tools > Flyover Preview` (`CourseFlyoverPreview.cs`), `Course Tools > Automation > Run pipeline / Screenshots only / Inspect scene / Open config` (`CourseAutomation.cs`).
- (no tool yet: a diff of the PC's live `Assets\Editor` against `unity/shared` as a command; today it is a manual `Copy-Item` + compare, see `docs/BRANCHES.md`)

# Unity tool inventory

Reuse before writing. Tools live in `unity_editor/` on `claude/moorpark-unity` (shared set), in each course's thread branch, and in `tierra-rejada-lidar/unity_automation/` (batch runner). Unity 2018.2.8f1. Full guide: `course-build/playbooks/19_unity_tool_inventory.md` in the moorpark repo.

## Shared editor tools

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

## Course-specific builders (the pattern)

- `Tierra*.cs` builders run in editor B/C only and produce packages; `TierraApply_<pkg>.Apply` is the one script that runs in A. Each package carries its island check, the hole-10 control diff (0.0), physics material on every collider, and `adel.json` for objects to `SetActive(false)` rather than delete.
- Gate check set (`TierraGate10` style): islands, plants-on-turf, floating objects, bins, fence continuity, vertex paint, magenta/null materials, turf control. Copy per course, keep the names.
- Course-wide one-shots from Hansen/Wilson: `BunkerDepth Apply Graded`, `SandLook`, `CanopyCover` (leaf spheres to the tops), corridor sweep, `FlyoverTees`, `BadMaterials`, `MeshStats`, `Pins18`, `ShadowAudit`, `SetMeshMaterials`.
- Others: `BuildFences.cs` (fences, walls, nets), `DrawArea.cs`, `TierraRope`, `TierraNativeSlopes`, `TierraPalm.Build`, `BuildClubhouse.cs`, Penmar `PenmarProps.cs`, `ChainLinkFence.cs`.

## Batch and remote running

- `Run-CourseAutomation.ps1` starts Unity in batch mode with the config JSON; `Send-UnityCommand.ps1` sends one command to a running editor.
- Batch mode leaves skybox ambient black: trilight ambient ×1.3 + fill 0.28 before screenshots.
- One editor per role (A edits, B/C render and build packages); never export or copy back from B/C.
- Backups before every pass: scene, terrain data, GKD, to `Backups\<pass>\`.

## New course setup

1. Own Unity project (`C:\GSP\<Name>_Project`), own scene; copy `unity_editor/` in; every constant read from `course.json`. Grep for `2000`, `3000`, `182`, `Tierra`, `Moorpark` before the first run.
2. Set Up Scene → Add Course Meshes → Plant LiDAR Trees → Plant Shrubs → Paint Grass → Cart Paths → Course Look → course-wide one-shots → Flyover Preview.
3. Gate check set to zero before the first review.
4. A course-specific builder only when a feature isn't covered.

## Pitfalls

- A copied `3000` stretched the satellite 1.5×; `surrounds.py` had `S = 2000` hard-coded.
- The small tree prefab planted at double size and trees standing on canopies until the raycast was restricted.
- Menus still named "Tierra" on other courses; keep the names but say so in the course README.
- Scripts that both build and apply in A made rollbacks hard; build-in-B/apply-in-A is the fix.
- Per-hole edits on shared materials leaked across holes; builders assign copies.

## Decision points (ask once, with a default)

1. Merge course-specific builders into the shared set now or after the next course (default: after Wilson proves the set).
2. Two editors or one that day (under ~4 GB free RAM means one).