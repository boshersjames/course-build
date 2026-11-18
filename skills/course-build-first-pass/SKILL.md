---
name: "course-build-first-pass"
description: "Use when taking a new course from LiDAR, satellite, OSM and BoJa's photos to a first playable build without BoJa: stage order, photo enhancement, where shot and putt tests run, and the 80% bar."
---

# Course build: first pass to 80% (BoJa 11/16)

The goal: given LiDAR, satellite, OSM and BoJa's indexed photos and notes, a course gets about 80% of the way there with no BoJa input, and BoJa's review time goes on looks and the open questions only. Every stage below points to the skill that owns it; this skill only fixes the order and the bar. Standing rules from `course-build-gates` apply throughout.

## Inputs (all must exist before stage 1)

- `course.json` frame, LiDAR (survey line known), NAIP satellite, OSM/Overture, scorecard.
- Photos indexed and posed (`course-build-photo-index`): `index.csv` with pose_conf, every photo in a hole folder, nothing built from `unsorted/`.
- BoJa's descriptions gathered per hole (thread messages, `visit_notes.md`, `decisions.csv`).

## Stages

1. **Data base** (`course-build-pipeline`, `course-build-lidar-review`): frame, heightmap, masks, SVG, feature manifest, GKD draft. D1-D18 closed from photos and BoJa's notes where they answer, `default` only where nothing does. Each feature in the manifest lists the photo IDs that see it.
2. **Base build, all holes** (conform, export, Unity setup, element skills at their defaults): terrain, greens from LiDAR or clean scan, bunkers by type, tees, rough and deep rough from masks, LiDAR trees and brush, water, paths, OB. No look polish yet.
3. **Base play test.** Static shot test (`-Shots 0`) and the 300-shot pass on every hole (`course-build-shot-test`); putt-roll test on every green join. Fix by type: colliders, surfaces, stuck balls, blocked tees, open joins. This catches structural faults before photo work builds on them.
4. **Photo enhancement, one hole at a time** (procedure in `course-build-photo-index`): every feature with usable photos is pulled, overlaid, traced, measured and its values written into the element's build inputs, together with BoJa's words about it. Order within a hole: hillsides and deep rough, trees, tee surrounds, bunkers, green surrounds, water, paths and props (hillsides and species were the top gaps on every Tierra hole). Element skills rebuild from the new inputs.
5. **Play test again on what moved.** Shot test on each hole after its enhancement (plants, bunkers and tee banks change what the ball hits); putt-roll test on every green join and regression-register site whose geometry changed (stale joins from `course-build-edges`). Fixes go back through the owning skill, never as a patch over a join.
6. **Close-out per feature, then per hole** (`course-build-photo-index` close-out, `course-build-gates`): each feature reviewed with its joins and the ground around it, photo agreement and visible defects both checked, status recorded. After any fix, recheck the affected features and joins, then run the integration checks (shot test, putt test, gates checks) on the final combined candidate. Only then does the hole go to BoJa.

## The 80% bar (reported per hole and for the course)

"First pass" names the stage of work; it never turns an unresolved defect into an accepted result.

- Feature status counts: verified unchanged, changed and verified, provisional base (with reasons), open. Every feature with usable photos has been reviewed against them.
- Photo verdicts: explicit visual verdicts on high/medium poses, at least 80% agreeing before BoJa sees the hole; every mismatch listed with its fix or question.
- No unresolved material defect on in-play surfaces or joins (gaps, exposed triangles, ledges, patches, distorted turf). Coverage and verdict percentages describe progress and never override one.
- Every comparison and test result labelled with the build it describes; nothing reused from an earlier build without its age and applicability.
- Shot test: 0 NOT_SOLID, PASS_THROUGH, INVISIBLE_COLLIDER, SURFACE_MISMATCH, STUCK, TEE_BLOCKED on the 300-shot pass of the final combined candidate.
- Putt test: reported separately from validated release checks while the lab is PENDING VALIDATION (open events listed, never counted as a pass); every regression-register site rolled.
- Automated checks from `course-build-gates` with expected = found = processed counts; missing, failed and not-run reported separately.
- Open questions: one list per course, each with the provisional choice that got built.

BoJa's part is the remaining 20%: the open-question list, the look (season, tint, post volume) and in-hand GSPro play. A hole that needs BoJa to find a photo mismatch did not reach the bar.

## Report to BoJa (one per course run)

Per hole, labelled with the build it describes: feature status counts, verdict share, remaining defects (including ones older than this pass), validated shot results and advisory putt results on separate lines, top open questions. Course: features left on provisional base and why (no photo, weak pose), features still open, what a second visit would close.