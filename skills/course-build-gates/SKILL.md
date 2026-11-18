---
name: "course-build-gates"
description: "Use when starting a hole or course build for GSPro, or before showing BoJa any flyover or gate sheet: the one-hole-at-a-time process, editor roles, automated checks and standing rules."
---

# Course build: process and gates

The order of work and the checks that stop a hole going back for a second or third round. Learned on Tierra Rejada (Oct 2026) and reused on Penmar, Harding, Wilson, Hansen Dam, Angeles National and Moorpark. The full set of element guides is in the moorpark repo under `course-build/playbooks/` (one per element: terrain, greens, bunkers, rough, tees, trees, brush, water, paths, OB, props, look, flyovers, data pipeline); read the matching one before building that element.

## Standing rules (apply to every element)

1. **Never invent ground.** Everything visible comes from the satellite, the LiDAR, a clean scan or BoJa's photos. Where a photo and the LiDAR disagree on a shape, the conflict stays open with a documented provisional choice and a question to BoJa; a photo does not automatically override measured geometry, and green surfaces and contours never change without a scan.
2. **No defects on in-play turf.** Tees, fairways, greens, collars and surrounds are built clean and even even when a photo shows wear. Bare soil only on out-of-play hillsides and banks, or where BoJa asked.
3. **No plants on mown turf.** Run the plants-on-turf check after every planting pass.
4. **No islands in bunkers.** Only a grass nose joined to the bank, approved by BoJa per bunker.
5. **Fences are moved, never deleted, and always continuous.** Mulch never on concrete. One shared concrete. Bins only where a photo shows one.
6. **Shared materials stay shared** (fairway, tee, sand, concrete). Per-hole looks are copies (`*_h17`) on that hole's meshes only. Course-wide passes are done once and shown on the control hole before and after.
7. **The first signed-off hole is the control** (Tierra: hole 10). Every later course-wide pass is rendered on it before/after; report "control 0.0".
8. **Base from LiDAR and satellite, detail from the photos (BoJa 11/16).** LiDAR and satellite lay the broad base for every feature. Then every feature with indexed photos goes through the photo enhancement procedure (`course-build-photo-index`) before it counts as built: its photos and BoJa's own description of it set the detail the base can't (brush and deep-rough extent, species and cover, where planting starts and stops, bunker face, lip and slope, tee decks, banks and surrounds, collar and surround banks). Photos give shape, extent and species, never faults: divots, ball marks, worn, bare or dead grass, repairs and temporary works are never copied (rule 2). Defaults only for features no photo or description covers, listed per hole as `provisional base`; a base that already matches its photos is recorded `verified unchanged`; conflicts follow rule 1 (statuses in `course-build-photo-index`). The photo | before | after check from the same pose, with a verdict per photo before BoJa sees a flyover, is still required, but it is a check, not the first time the photos are opened. BoJa should never be the one to find a mismatch.
9. **Ask once, with a default.** Every question carries a default that gets built if unanswered. Goal-changing questions (what is OB, which green plays, where the real forward tee is) are asked before building.
10. **Back up before every change** (scene, terrain, GKD). Never touch `C:\GSProV1\Core\GSP\Courses\` without BoJa's go.
11. **No brush on tee boxes.** Brush, scrub and shrubs never come onto a tee box or pad; near tees keep cover to its side of the flight line from the tee (BoJa 11/05, playbook 05).
12. **4K videos first, then slim for GSPro.** Render every 4K flyover from the full-detail course; only afterwards cut size to stay well under ~2.1 GB (BoJa 11/05, playbooks 13 and 21).
13. **8-10 yards of plain rough off every fairway.** No shrubs or dense brush within about 8-10 yd (7-9 m) of either fairway edge (BoJa 11/05, playbook 07).
14. **Edges are built once, as one boundary.** Green, collar, blend, rough and bunker-lip joins are built together at the Blender/OPCD conform stage before export (`course-build-edges`). A later Unity geometry change near a join makes it stale until rebuilt or re-checked; a stale join blocks the gate and the install.
15. **One owner per shared boundary (BoJa 11/17, non-negotiable).** No mesh or object may edit a shared edge independently. A boundary has one owner (its record in the course boundary file: ID, owning surface, neighbours, source evidence), and every neighbouring surface is regenerated from it. Shared vertices come from that one boundary and agree within 1 mm (a floating-point tolerance, not "0 mm"). A local edit rebuilds every affected neighbour and every dependent in the same change: colliders, paths and curbs, fence posts, planting, water and its GKD hazard. Later tools obey this too: a tool that moves ground lists the boundaries and dependents it touched and rebuilds or re-seats them in the same run, or refuses to run. On Tierra 1117f about 60 tools had rewritten 429 of 646 ground meshes one at a time; that is how the shared edges came apart.

## One engine at a time on the PC (11/10)

- One heavy app at a time: Unity (any project), Unreal, GSPro benchmark. Ask the coordinator for a GO, confirm 0 processes of the other engine, report "closed" with the process count.
- Overnight: alternate 60-90 min blocks (Unreal startup is 5-8 min), chain steps in one editor session, close only at group boundaries.
- Install steps need GSPro and Greenkeeper closed. Finish-Course.ps1's preflight checks those, the approved scene hash, D: space and a clean scene; it does not check Unreal, so confirm that by hand.
- Unattended nights: backup before each save, no in-place overwrites, rebuild after each group, before/after list for the morning, skip and list anything needing a permission prompt.
- Scripted edits to prefab instances must call `PrefabUtility.RecordPrefabInstancePropertyModifications` or they reset on reload (Hansen sun and fog); run look checks after a reload.
- Course-to-course numbers live in `rules/finish.json` (explained in `22_course_finish_rules.md`); a course overrides them in its `course.json`, never by copying the file.

## Defaults that worked

| Setting | Value |
|---|---|
| Unit of work | One hole at a time, one edit at a time, each with a photo pair (BoJa 10/31: "we are going in circles") |
| Per-hole scope | Top 8 items first, one check at the end, at most one fix round; the rest on a detail list |
| Editors | A = edits and saves the scene. B = builds packages (bunker rebuilds, seams, tints) and test-renders on a sync of A's save; commits scripts/json only. C = photo-pose renders, gate sheets, flyovers |
| Editor fallback | One editor if free RAM < ~4 GB; never a batch job while BoJa has an editor open |
| Backups | Scene + terrain + GKD before each change |
| Review frame | Full-size frames, photo \| before \| after from the same pose |
| Package apply | `TierraApply_<pkg>.Apply` on a fresh reload of A's named save; the apply refuses if the meshes moved since the package was built |

## Procedure for a hole

1. **Base and inventory.** The hole's LiDAR and satellite base is in place. Pull every geotagged photo for the hole (photo, hole, GPS, heading, confidence) and everything BoJa has written about it (thread messages, visit notes, `decisions.csv`). Search BoJa's EXIF originals first, every time.
1b. **Photo shaping sheet.** Per photographed feature (each hillside, tee surround, bunker, green surround, tree group, water edge): the photos that show it, BoJa's words about it, and the values they set (extent on the ground, species and cover, heights, slopes and angles). These values drive the build in step 5. Every feature with usable photos ends with a status from `course-build-photo-index` (verified unchanged, changed and verified, provisional base with reason, or open); one never reviewed against its photos is a gate failure.
2. **Step 0: poses.** Render from each photo's position/heading; fix poses by content (skyline, sign, path corner, tee markers), not GPS alone (GPS put the Tierra 10 Blue tee photos 80 m away). Only high/medium confidence poses give verdicts.
3. **Blind reviews, then one edit list.** Independent reviews of the pairs merged with a critic pass into a numbered list: spec, evidence, measured colours (r/g, b/g), photos to check on. Cut to the top 8. Every review checks objects that should be absent as well as present (`course-build-photo-index`, the audit). A scoped review (planting only, one feature) still lists obvious discrepancies elsewhere in the frame (a wrong, extra or missing object, a placeholder surface) in the defect register as out-of-scope findings, even when their repair belongs to another task (BoJa 11/18). A column or label such as "rebuilt from the photos" names only what was rebuilt; everything else in the frame carries its own status: corrected, retained with evidence, or unresolved. **Before any approval request (a gate step, BoJa 11/18):** review the whole finished candidate, not the edit list, against every reference photo of the area. `view_inventory.py` lists every built object inside each photo's view from the candidate's own export; the review record (`geometry_gate\REVIEW_RECORD.md`) gives each listed object and each photo-only object a status (matched, differs, extra, missing, hidden) and each mismatch a decision; `geometry_gate.py --mode review` (or `Gate-Candidate.ps1 -Photo ... -Review ...`) must pass. It stops on a missing record, another build's record or renders, a skipped object in view, a mismatch without a decision, a retained item without evidence, an unresolved item, or a review that lists only missing things.
4. **Rules check.** Mark rule breaks (plants on turf, gaps in turf, islands): those go first.
5. **Build one edit at a time.** Back up, edit, render after-pairs at the same poses, run the checks, save, commit, send the hash. Course-wide items done once, shown on the control hole.
6. **Gate.** After changes, recheck the affected features and their joins, then run the integration checks on the final combined candidate. One sheet with every pair (labelled with the build it shows), checks green, rules ticked, remaining questions and defects listed, plus the flyover. Advisory putt results sit on their own line, apart from validated release checks. Only a passing hole goes to BoJa.
7. **Apply BoJa's answers** in a "BoJa's answers applied" block; unanswered questions build their default.
8. **Sign-off, then freeze.**

## Automated checks (every time)

- Bunker islands: clean except approved noses.
- Plants on turf: 0 (incl. the own-soil-patch case).
- Floating and buried objects: every post, stake, pole, sign, bin and prop is judged over its whole footprint against the visible ground (top playing surface or terrain), not at its centre; a visible gap over 5 cm under any part of the foot fails. Re-run after any ground change under it. (Tierra 1117f: five posts behind 7 green stood 14-20 cm up; the first sweep sampled post centres with a 15 cm cutoff and missed them, and the written rule "above ground by more than their height" could never catch a 20 cm gap under a 1 m post.)
- Unsupported bins; broken/gapped fences; free-ended fence pieces; objects on turf inside a rope line.
- Vertex paint on every rebuilt mesh (else bare dirt).
- Null/magenta materials; z-fighting at low angles from 30-80 m.
- Height gaps where fairway/collar sits above rough (dark seams; up to 14 cm on Tierra 1).
- Shared boundaries: every playing-surface boundary is joined (within 1 mm), free (no neighbour boundary within 0.5 m, listed) or broken (1 mm to 0.5 m apart, fails). Report joined, broken and free metres and each broken spot with its meshes.
- Terrain intrusion: base terrain more than 2 cm above any playing surface fails (Tierra 5 tee 1117f: 3.2 m).
- Partial plants: in combined plant tiles every card is whole (4 triangles after welding); a 1-3 triangle remnant fails. Clearing removes whole plant instances (`course-build-brush`).
- Routes and footprints against evidence: streams, paths and fence lines are compared with the aerial/survey trace and their source line, with a tolerance taken from that evidence (`course-build-photo-index` step 0, `course-build-water`, `course-build-paths-fences`).
- Scope: every check runs over the whole course inventory (all holes, every object of its kind) and prints expected = found = processed. A check written for one hole's box is not the course check (`TierraGate10` covers only the hole 10/11 corridor and ROPE10).
- Where they run (11/18): `skillcheck.py` in `E:\GSP\course-build-staging\geometry_gate` on an export bound to the scene's hash (`Dump-Scene.ps1`). A candidate goes to review, matched views or play tests only after `Gate-Candidate.ps1` exits 0: no new or worse defect against the protected baseline, and nothing flagged inside its declared work area. A release builds and installs only after `Build-Install-Tierra.ps1` step 2c passes: a current, bound, all-PASS report for that exact scene, checked again before install. A missing, stale, unbound or wrong-build report stops both. A written check with no tool behind it is reported NOT IMPLEMENTED, never ticked from the rule.
- Control-hole turf change = 0.0 unless intended.
- Photo coverage and evidence: every feature on the hole's photo shaping sheet has a status and its required evidence (views, verdicts, build label). This check covers presence, not looks: visual agreement needs an explicit visual verdict. Missing, failed and not-run are reported separately, and coverage never overrides an unresolved material defect.
- Prop placement: nothing on paths, sand, tees, greens, water, or in a hitting line unless photographed.
- Physics shot test on every hole in the course inventory before any build for BoJa or the beta (`course-build-shot-test`), re-run after fixes.
- Repairs come from an immutable baseline and must give the same output twice; a falling scan count is never proof a defect is gone (`course-build-greens`).
- Joins current: no stale join (`course-build-edges`); every spot in the regression register executed and reported; no established geometry defect and no failure in a qualified acceptance test (an unvalidated putt-lab failure stays listed as an open event pending investigation, not cleared and not a pass); missing required evidence fails the row; and expected features = found = processed, with each hole and green mapped and any practice green's exclusion written down (a check that finds 0 collars on a course with collars is a failure, not a pass). Diagnostic candidates may fail and still be rendered and tested; release acceptance is for release candidates.
- Unreal marketing renders: material-group review and motion preview per `course-build-lighting`.

## Pitfalls we paid for

- Multi-hole passes produced three rounds of rework. Only shared materials/models get course-wide passes.
- Knowingly saved "good enough" defects (16 bunker creases, black pocket) came straight back.
- A re-run scene setup tool wiped Harding's trees and meshes: wipe tools must ask and never re-run on a built scene.
- Four editors froze the PC; batch jobs during BoJa's review had to be killed.
- Copied tools kept another course's constants (3000 m, `S = 2000`): grep before running.
- Tools read the GSPro Courses folder instead of the project GKD.
- Pixel-box specs on old renders go stale; say which save and pose set a spec was measured on.
- A repair applied across holes from the current mesh (GreenTie) folded collars and drifted on re-runs; prove a repair on one hole plus two different shapes first.
- A benchmark taken with Unreal open read as a slower course.

## Review, defect register and evidence (BoJa 11/17-11/18)

- **Whole-course review.** All holes from normal playing views (tee, landing area, approach, green, reverse), a moving flyover, then targeted close-ups (low and reverse) wherever a join, water edge, fence, path or planting edge can hide a fault from above. A flyover alone or a photo-pose set alone is not a review; holes with no photos still get the playing views.
- **Defect register.** One register per course: hole, feature, exact world location, marked screenshot, reproducible camera (position, yaw, pitch, FOV, build), visible symptom, severity, the responsible object (rendered mesh, collider, terrain, neighbours), suspected cause kept apart from confirmed cause, repair scope and neighbours, required verification, status. A symptom is tied to its object and camera before any repair, and is marked resolved only when the same camera on the repaired build shows it gone and its measurement is saved.
- **Three kinds of evidence.** Appearance (matched before/after views, real photos, a moving inspection), geometry (the measurements above) and play (shots, chips, bunker shots, putts, tee cameras, colliders for the changed feature) are separate results, and each needs its own. One never closes another: a clean putt run covers putting on the tested greens only and cannot close a bunker, stream, fence, mesh or look defect.
- **Protected features.** Approved greens (surfaces, contours, pins, outlines) and accepted features (Tierra: the hole 11 bunker, frozen 11/17) do not change without a demonstrated play defect and BoJa's OK; every candidate's geometry regression shows them unchanged.
- **Completion claims.** "Fixed", "done" or "pass" needs saved measurements and before/after evidence of the named build (scene and dependency hashes, source scene path). Without them the result is UNMEASURED.

## Boundary evidence for a candidate (review 11/15)

- Geometry checks, matched views at fixed poses and targeted rolls all name the same immutable candidate (scene and dependency hashes, tool and rule versions, sample set). Looks from one copy and rolls from another don't count together (Tierra 4: rim fix 74 → 0 rolls, then dashes in the views).
- Known defects stay in the regression register; an aggregate improvement never clears a remaining known failure.
- Missing output, a swallowed exception, an unsupported check or zero targets where targets are expected is UNMEASURED or FAILED, never PASS.
- Cheap local checks (topology, known sites, views) before broad rehearsals; reuse a baseline only when every input fingerprint matches.
- Record which skill, rule and tool versions were loaded. A source-only edit or a staged job is not a deployed workflow.

## Ask BoJa once per course (with a default)

1. Target look: plain render (Tierra) or the shipped post-processing volume.
2. Season: green "spring vibe" rough with dormant hillsides (Tierra) or dormant everywhere.
3. Signature holes that get extra passes first.
4. Sign-off unit: hole (default) or nine.