# Handoff: course-build toolkit and Tierra Rejada, 2026-11-06

For whoever picks this up next, person or session. Read `README.md` and `docs/BLUEPRINT.md` once; this page is where things stand,
what is proven, what is half-done, and what to do first. Every number lives in `rules/*.json`; every path in a course's `course.json`.

## Repos and branches

| repo | branch | what it is |
|---|---|---|
| `boshersjames/course-build` | `main` | the toolkit: rules, tools (Python, headless), unity/shared (C#, course-agnostic), skills, playbooks, automation |
| `boshersjames/tierra-rejada` | `claude/toolkit-wiring` | the Tierra course: course.json, GKD, photos index, progress/ (every measurement from 11/3 and 11/4); never merged to main yet |
| `boshersjames/moorpark` | `claude/toolkit-wiring` | wired to the toolkit, headless loop run once, no Unity gate yet |
| hansen-dam, penmar, angeles-national, harding, wilson | `main` | split out and wired; data only, nothing built |

The PC works in `C:\Users\user\Documents\<course>` with `..\course-build` beside it; the Unity project for Tierra is
`C:\GSP\GSPBaseProject_v4_2025_12_02`. The PC pushes to the same branches; pull before pushing from either side.

## How things run (three verbs)

- **Sync the toolkit into Unity, editor open:** `..\course-build\automation\Run-CourseAutomation.ps1 -SyncTools -SyncOnly`, then
  `Send-UnityCommand.ps1 refresh`, then `ping` until it answers. A course script with the same name as a toolkit script is skipped and
  named; delete such copies from the course's `unity_editor/`.
- **Measure:** Unity checks through the bridge (`invoke -Tool <X> -Method <Y> -Set @{...}`) write CSV/MD under
  `AutomationOutput\checks\`; the Python twins read them headless from the course folder.
- **Change:** every edit tool has a dry run, a log entry in the scene object `COURSEBUILD_LOG`, and a Revert. Save the scene after
  every applied pass that measures right; the collar work was lost once by saving at the end of the session.

## State per area (Tierra), with the proof

**Physics materials.** `CoursePhysicsAudit` + `tools/physics_check.py`. 36 name-matched fixes applied; 54 "review" rows (ground
materials under structure names) deliberately untouched; census passes on rough, deep rough, fairway. Per-mesh leftovers: hole 10
scan rough/bunker overlap, widened tee pads carrying tee physics outside their outline, Gully13 and two streams as rough where the
SVG says water. `MaxStimp` 3 in the GKD against the rule's 5: owner's call.

**Green edges.** The step metric is slope-free (rise minus the median of neighbours' rises) in `CourseGreenChecks` and the headless
twin `tools/edge_profiles.py`; the old raw-rise metric failed every bank over 11 deg and misled two days of work. Collars: rims reset by
the survey residual (`CourseRimReset` v2, directional gate, blend target), three course-wide passes on 11/4 took 1,341 real steps over
1 cm to 563 and the outer-rim pairs from 488 rows to 48; blend pass took it to 430; interiors relaxed on the 7 collars the 3 cm cap
allowed. Hole 16 blend seated, hole 17 ghost collider rebaked and its package patch off. What remains is per hole: 1 cm kinks inside
15 bank collars the relax refused, the green-to-blend ramp at 1.4 cm on short blends, hole 6/8/9/11/12/16 outliers of 4-6 cm.
Judge any edge change by the pair table in `edge_profiles.md`, not the per-hole maximum.

**Pins.** `tools/pin_mover.py` moved 50 of 51 failing pins onto flat cells of the live collider grid (1.4 deg placement margin, 3 m
from edge and each other); 17 of 18 greens clean; hole 14 has only three compliant spots (owner: three pins, or a spacing override).
GKD installed to `C:\GSProV1\Core\GSP\Courses\TierraRejada\` with a backup. Greens are never contoured: all 18 are over 3% on LiDAR
and scans alike.

**Flyover pipeline.** `tools/run_pipeline.py --course tierra-rejada --hole 10 --tee Black --keep-frames` is proven end to end on
hole 10: headless flight check (`tools/flyover_path.py`, the twin of `CommercialFlyoverPath`, all 18 holes in a minute, `--set` to
try a rule), Unity path, 4K render (`RenderQueue4K`, 1.0x MSAA 8, 4 sub-frames, resume, housekeeping, own near clip), banner and HEVC
(`flyover_packager.py`, banner via text files, HUD off). 20 s format: 5 s eye-level hold by the hole sign with the banner, 15 s tee to
green, no orbit. Rate limits 20/15 deg per s are the commercial pace; all 18 pass. One hole renders in about 8 minutes.
`--purge` on the packager deletes a hole's frames once the file is approved; `--hole 0` does the course one hole at a time.

**Photos.** 475 photos indexed with poses, 69 on hole 16, synced to R2; 475 renders made from those poses once; audit sheets exist;
the verdict column is still "draft" everywhere because the sheets were never read for content. The full loop and the rule that came
out of it are in `docs/PHOTO_PIPELINE.md`. The willow on 16 was the lesson: IMG_4929 showed no tree, and it was in the index.

**Scene operations.** `CourseSceneOps`: Find, FindNear, FindPlants, FindOnHole, GroundProbe, TerrainReport, SetOff/SetOn with position,
RebakeCollider, ClearTerrainDetails, DetailsOnTurf dry run / clear, UndoActive. The cart path tool has `CheckTurf` / `SinkTurf`
(turf over paths, 0.1-0.2% on Tierra, passing).

## Open on hole 10 (the flyover reference hole)

1. The hold frame showed the underside of the hill: diagnosed as the render camera's near clip plane (the position was measured right,
   71.56 m over turf at 69.86); `render_4k.near_clip_m` 0.3 is in the rule and NOT yet re-rendered. First thing to verify.
2. Sawtooth seam beside the path on the run-in to the green: not turf over the path (CheckTurf passes); a jagged mesh boundary.
   Needs the full-frame screenshot at 9 s to name the mesh; the fix is a patch rebuild, as on holes 2 and 16.
3. The "boulder" short of the green: not an object; painted brush billboards on rough beside the path. Six cleared within 6 m of
   (1536, 1089) and saved. Verify in the next render.
4. Yellow tee's wide copy `Spline_tee_19_Tee_-_Mesh_wide` switched off (the original already carries the widened shape). Verify.
5. Green discoloration seen at the close finish: the green's paint, a look-pass item after the post profile.

## Risks and things to know

- **A PC-side commit edited `unity/shared/CommercialFlyoverPath.cs` directly** (b78ee7b, "Sync CommercialFlyoverPath from Tierra",
  11/4 19:17 PST): new default values, a `green_front` end look, a hard-coded 0.8 m sideways push at the intro, comments removed.
  Defaults in C# are fallbacks, the rules JSON wins, but the twin `flyover_path.py` does not know `green_front` or the 0.8 m push, so
  the twin and Unity now disagree. Reconcile first: either move those changes into rules and the twin, or revert the commit.
  The toolkit rule is that constants live in JSON and the twin and the tool stay in step.
- The sync copies ~140 Tierra-only editor scripts into the project; a full recompile crashed Unity once. `-SyncOnly` and patience.
- The 4K render leaked GPU handles under the old settings and crashed at frame 401; the current rule set rendered 1,198 frames clean.
- `COURSE_JSON` on the PC must point at the course; the toolkit resolves every path from it, and the Unity tools read the copy the
  sync writes to `Assets/CourseBuild/course.json` with `course_repo_abs` added. Copying course.json by hand breaks the GKD lookup.
- Static `-Set` fields on bridge tools persist until a recompile: clear `hole`, `only`, `force`, `target` explicitly on course-wide calls.

## First five things to do next

1. Reconcile the PC-side `CommercialFlyoverPath` edit with the rules and the twin (above), then re-render hole 10 and look at the
   first three seconds and the finish.
2. Read hole 16 and 17's audit sheets against the photos and write the verdicts; that is the first real pass of the photo loop.
3. Hole 10 sawtooth: screenshot, name the mesh, rebuild the patch.
4. Per-hole edge leftovers from the pair table; pins on 14 (owner decision).
5. Then the course: `run_pipeline.py --hole 0 --keep-frames`, approve hole by hole, purge.

## Where results live

Tierra `progress/`: `edge_profiles_*` (every pass), `checks_1103/` (raw survey, physics audit, grids), `pin_moves/`, `flyover_path/`,
`physics_check/`, `fixlist_1103/README.md` (the per-hole list, 125 items, still the master list). Unity side:
`AutomationOutput\checks\` (latest of everything), `AutomationOutput\deliverables\` (videos), `AutomationOutput\logs\` (pipeline,
render queue, packager JSON per run).
