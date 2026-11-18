---
name: "course-build-unity-tools"
description: "Use before writing any Unity editor script for a GSPro course: the menus, batch jobs and course builders that already exist, their inputs, and the new-course setup order."
---

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

## Added 11/10

- **Course Tools > Shot Test** (`CourseShotTest.cs`, `Run-ShotTest.ps1`, config `Assets/CourseAutomation/<scene>.shottest.json`): see `course-build-shot-test`. Source moorpark PR #1 `shot_test/`; live copy `C:\GSP\course-build-staging\shottest_1110\`.
- **Fixed-boundary collar strip** (`h16strip.py`, offline: numpy, shapely, `offline/umesh`): baseline from the saved scene's assets, strip and rough band from the green loop, report with seam/flip/coverage/repeatability. The method for every green-edge repair (`course-build-greens`).
- **Solid-collider pass** for poles, signs, posts, dividers (146 on Tierra; soft plants skipped on purpose; undo `beta_1110_solid_undo.tsv`).
- **Putt lab** (`PuttLab.cs`, throwaway project `C:\GSP\putt_lab`, batch Unity, results `out\<time>\`; 11/11, pending validation): the green seam / putt-roll test (`course-build-shot-test`). Never in a course editor.
- **Finish-Course.ps1** (`C:\GSP\course-build-staging\finish_course\`, per-course `courses.json`): owned by the course-finishing work; dry run unless `-Go`; every course needs a `dirty_tool`.

## Known tool defects (engineering review 11/10)

- `GreenTie` (`TierraBeta1109.cs`): folds triangles, reads its own output. Retired.
- `EdgeScan`: diagnostic only; can't certify an edge.
- `CourseMeshPatch` drops UVs when adding 2+ vertices (hole 12 `..._h12kidneyband` UV0 all zero; harmless only because its shader maps by world XZ).
- `CourseAutomation` saves after a failed step and ignores `ABORT:` returns (Tierra config `saveScene: true`).
- `SceneFingerprint` (frame resume) misses mesh contents, terrain and most material properties: new render folder after any look change.
- `gate_report.py` passes on the flyover alone and ignores a `gkd_tools` crash: not a release gate.
- Toolkit (`Documents\course-build\unity\shared`) and installed copies diverged; installed Tierra and Hansen copies are the working versions. Never `-SyncTools` into a course until each file is reviewed.
- Repair tools must read an immutable baseline, write versioned assets and keep every undo list; never delete and recreate the same named output.
- Plant clearing by triangle (skills audit 11/18): 38 methods in 29 Tierra editor scripts (`TierraTallShrubs`, `TierraPathSweep`, `TierraSweep`, `TierraClear17`, `TierraHillBrush.ClearShrubCards`, `TierraH11Round3`, `TierraA_Fixes.ClearBox`/`TrimNearMown`, `TeePadCut`, the `Tmp*` hole passes, and `TierraRayCut` / `TierraMownCut` with `go = true`) deleted plant triangles or connected pieces one at a time, leaving partial plants (1,680 remnant cards on Tierra 1117f). They are RETIRED: each now calls `PlantClearRetired.Refuse` first and throws before touching the scene (list and original files: `retire_manifest.json` and `tool_backup` in `E:/GSP/course-build-staging/skills_audit_1118`). Report-only modes still run. A replacement clears whole plant instances; until one exists, a clearing need rebuilds the tile from the shrub list.
- `TierraGate10` checks only the hole 10/11 corridor box and ROPE10: its floating, fence and plants-on-turf results are not course results.
- `BuildFences` seats posts on the ground when it builds them and never again; later ground edits leave them floating or buried.
- `BuildStreams` follows the top surface under its `streams.csv` centre line; it has no route-against-aerial or never-rises guard (`TierraWaterCheck` reports rises, but no 1117f run of it was found).
- `CourseOverlapScan` (course-wide STEP, TERRAIN, SLIVER, COVER scan) is in the Tierra project but no skill named it and no 1117f run was found.

## Geometry checks (skills audit 11/18)

- **`skillcheck.py`** (`E:\GSP\course-build-staging\skills_audit_1118`, offline Python on a FinishDump export; `FinishDump.cs` from `finish_1118`): ROUTE (water vs the aerial channel, posts vs `fences.csv`), EDGE (1 mm joins, broken and free boundary metres), TERR (terrain through playing surfaces), SEAT (whole-footprint post gaps), PLANT (partial cards). Whole course, absolute and as a geometry regression against the protected baseline. Writes `verdict.json` with the scene path and hash, dump hashes, tool hash and tolerances; exit code 0 only on PASS. Status: demonstrated 11/18 on a fault copy (4 of 4 injected faults caught, baseline-vs-itself control clean); code on `tierra-rejada` branch `claude/retire-plant-clearing` under `audit/skills_audit_1118`; not yet wired into `Finish-Course.ps1` or the gate scripts.

## Course-specific builders (the pattern)

- `Tierra*.cs` builders run in editor B/C only and produce packages; `TierraApply_<pkg>.Apply` is the one script that runs in A. Each package carries its island check, the hole-10 control diff (0.0), physics material on every collider, and `adel.json` for objects to `SetActive(false)` rather than delete.
- Gate check set (`TierraGate10` style): islands, plants-on-turf, floating objects, bins, fence continuity, vertex paint, magenta/null materials, turf control. Copy per course, keep the names.
- Course-wide one-shots from Hansen/Wilson: `BunkerDepth Apply Graded`, `SandLook`, `CanopyCover` (leaf spheres to the tops), corridor sweep, `FlyoverTees`, `BadMaterials`, `MeshStats`, `Pins18`, `ShadowAudit`, `SetMeshMaterials`.
- Others: `BuildFences.cs` (fences, walls, nets), `DrawArea.cs`, `TierraRope`, `TierraNativeSlopes`, `TierraPalm.Build`, `BuildClubhouse.cs`, Penmar `PenmarProps.cs`, `ChainLinkFence.cs`.

## Batch and remote running

- `Run-CourseAutomation.ps1` starts Unity in batch mode with the config JSON; `Send-UnityCommand.ps1` sends one command to a running editor.
- Batch mode leaves skybox ambient black: trilight ambient ×1.3 + fill 0.28 before screenshots.
- One editor per role (A edits, B/C render and build packages); never export or copy back from B/C.
- **GPU and Unity lock (BoJa 11/18).** Every Unity launch, graphics or batch, goes through `C:\GSP\job_runner\Use-GpuLock.ps1 -Owner <thread> -Job <name> -- <command>` (holder: `Get-GpuLock.ps1`); it waits up to 90 minutes and exits 75 without running on timeout. Edits to `Assets\Editor` happen while holding the lock too, because the folder compiles as one assembly: a compile error there fails every `-executeMethod` in the project, Unreal exports included. Confirm 0 `error CS` lines in the Unity log before releasing the lock, and restore the originals if there are any. Until the wrapper is installed: runner PAUSE present, `running\` empty, no Unity, Unreal or GSPro process, re-checked before each launch.
- Backups before every pass: scene, terrain data, GKD, to `Backups\<pass>\`.

## New course setup

1. Own Unity project (`C:\GSP\<Name>_Project`), own scene; copy `unity_editor/` in; every constant read from `course.json`. Grep for `2000`, `3000`, `182`, `Tierra`, `Moorpark` before the first run.
2. Set Up Scene → Add Course Meshes → Plant LiDAR Trees → Plant Shrubs → Paint Grass → Cart Paths → Course Look → course-wide one-shots → green seam / putt-roll test (pending validation) → shot test → Flyover Preview.
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