---
name: "course-build-shot-test"
description: "Use before any GSPro build for BoJa or the beta, or after a collider, collar, bunker or plant pass: run CourseShotTest and the green seam / putt-roll test, and fix what plays wrong."
---

# Physics shot test

Use with `course-build-gates` and `course-build-publish`. Full guide: `course-build/playbooks/24_shot_test.md` in the moorpark repo.

**What a result covers (BoJa 11/17).** A shot or putt result covers only what it tested: those shots, or putting on the tested greens and joins. It never closes a geometry, appearance, water, fence or plant defect, and "zero problem putts" is never reported as "the remaining defects are cosmetic" (`course-build-gates`, three kinds of evidence).

## Run it

- Tool: `CourseShotTest.cs` in `Assets/Editor` (Unity 2018.2); config `Assets/CourseAutomation/<scene>.shottest.json`. Source: moorpark PR #1 `shot_test/`; live copy `C:\GSP\course-build-staging\shottest_1110\`.
- `Run-ShotTest.ps1 -Config TierraRejada [-Holes "16"] [-Shots 300]`: batch mode with Unity closed, the live bridge (`Send-UnityCommand.ps1 invoke -Tool CourseShotTest -Method Run`) with it open. `-Shots 0` = static checks only. It never changes or saves the scene, but it is still an engine run: get the PC slot first.
- Beta pass: every hole in the course inventory (18 on Tierra, 27 at Temecula), 300 shots per hole (55% tee, 25% approach, 20% chips, 35% of chips from sand), fixed seed, no wind. Tierra: 43 s.
- Repair check (hole 16 collar, 200 shots): 25 chips from each of four sides across the collar, 20 putts off the collar from two sides, 30 slow rolls into the bunker over the tongue, 30 toward its west end.
- Output: `AutomationOutput/shottest_<stamp>/` (`summary.md`, `anomalies.csv`, `shots.csv`, `holeNN.png`, `index.html`); path in `latest_shottest.txt`.

## Finding classes

| Class | Action |
|---|---|
| NOT_SOLID / PASS_THROUGH | poles, signs, posts, dividers, monuments, trunks get solid colliders (Tierra 146). Grass and soft low plants (pampas, toyon, fountain grass) stay soft on purpose. Crowns where the course notes say branches drop the ball need a leaves collider (capsule/sphere) |
| INVISIBLE_COLLIDER / PHANTOM_COLLIDER | remove or fit (Tierra 12: tree colliders with no visible tree) |
| BUNKER_ROLLOUT | fix on real sand; waste-bunker rough-blend rings (`*_B_Rough_Bunker_-_Blend`) are false positives |
| SURFACE_MISMATCH | right physics material (sandstone cliffs looked like bunker and fell through; 13 waterfall rocks played as sand) |
| STUCK | find the stacked collider the ball rests on |
| WRONG_HOLE | OB or trees as the course notes say (Tierra 1 into 9) |
| TEE_BLOCKED | move the object, not the tee |
| OVERSIZED_COLLIDER, ROUGH_GRAB, DEEP_ROUGH_AT_GREEN | review list (shrub tile colliders are mostly empty skin) |

Also read the per-hole table and hole notes (Tierra: "Hole 5: Black tee sits on green (physics GSPgreen)").

## Green seam / putt-roll test (PENDING VALIDATION, BoJa 11/11)

Part of every new-course bring-up so greens play right and look right. `PuttLab.cs` is proven on built cases only; until the hole 16 run and GSPro reference putts agree, run it every time but treat results as advisory. Testing a seam and predicting a whole putt are separate runs: a long putt that stops before the join says nothing about the join.

- **Where:** throwaway project `C:\GSP\putt_lab`, batch Unity, no graphics; course meshes as collision-only copies; never the shared editor. Output `out\<time>\` (summary.md, runs.csv).
- **Solver:** real ball (21.34 mm, 45.93 g) on unchanged mesh colliders, fixed-step `Physics.Simulate`, no ground snap, fresh ball per run; rolling resistance separate from contact friction (0.55 m/s² stimp-10 placeholder, friction 0.4, bounce 0.1); unknown surfaces reported, never guessed.
- **Step size:** 1 ms; 0.5 ms for gap-like joins. 2 ms is ruled out (invents bumps at clean joins).

1. **Step-size proof on clean splits first:** one mesh, the same plane split (matched and mismatched vertices), coplanar overlap, a 5% rise split at its foot. Clean cases 100/100 like the one-mesh baseline, repeats identical, and the 1/3/6 cm steps, 1/2/5 cm gaps and 1/3 cm overlaps caught. Any event on a clean split: fix the tester or step before touching a course.
2. **Crossing test on every green join** (green to collar strip, rough band, lip rough, bunker lip): 5 positions × 5 speeds (0.2-2.0 m/s at the join) × 4 directions = 100 runs from 0.4 m before the join, judged against an identical-input flat baseline. Events: catch ≥5% slower, deflect >1°, stopped short/at join, came back, ran long >2 cm, hop >2 mm, drop >5 mm, blocked. Never reached the join = not exercised, not pass or fail.
3. **Full-putt test** from set green spots toward each bunker or tongue that matters: reaches the lip? where does it finish? Geometry findings apart from model-dependent outcomes; write expected results for gentle rises first.
4. **GSPro check:** the same putts by hand (speed, direction, spot, build recorded, repeats), plus one over a known clean join.

Pass once validated: 0 events at any green join at 0.4-2.0 m/s, clean splits 100/100, full putts inside the GSPro spread. Fix failures with the fixed-boundary rebuild (`course-build-greens`) and re-run with the saved inputs. Seen so far: 1 cm defects change 95/100 runs, and a ball can roll under a raised turf edge (single-sheet meshes, no vertical face). Full step: playbook 24.

**Known sites and the regression register (review 11/15).** The release protocol samples 6 edge joins × 48 rolls per green, so a zero there covers only those joins: Tierra 16 scored 0 of 288 while its known spot still failed about 21 targeted rolls. Every spot that has ever failed (from a run, a view or BoJa) gets an ID, world position, surfaces, symptom and the run that found it, and is rolled in both directions on every candidate before broad sampling, for good, including after it is fixed. Geometry flags from `course-build-edges` are added as targets the same way. Report expected, executed, skipped, held-out and failed per site; a site never reached is unmeasured, not passed. Rolls are judged together with the geometry and matched views of the same candidate. While the lab is PENDING VALIDATION, a failing roll is an open event pending investigation: it stays on every report until explained, a sampled zero never clears it, and on its own it neither passes nor blocks a release. Release is blocked by established geometry defects and by failures in qualified acceptance tests. Failing diagnostic candidates can still be rendered and rolled; release acceptance runs only on release candidates.

## Procedure

1. Green seam / putt-roll test on the regression register sites first, then every green join → all holes in the course inventory (18, or 27 at Temecula) → fix by type, one undoable step each with its own undo list → re-run and report before/after counts per site and per hole to BoJa. A fix that moves geometry in Unity makes the touched joins stale (`course-build-edges`): re-run their sites.
2. Then slimming cuts, rebuild, BoJa's benchmark.

## Limits

- Our own ball model, not GSPro's: geometry findings are solid; carry and roll are comparisons against flat ground.
- **Putt model too weak for rolls**: its flat putts roll 1-3 yd, so collar-to-green and tongue rolls go to the green seam / putt-roll test above, then BoJa's hands in GSPro.
- Collider checks cap at 40,000 per run; say so in the report.
- A falling finding count is not proof a surface is right; triangle and boundary checks are (see `course-build-greens`).