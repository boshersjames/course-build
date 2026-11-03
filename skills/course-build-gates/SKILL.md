---
name: "course-build-gates"
description: "Use when starting a hole or course build for GSPro, or before showing BoJa any flyover or gate sheet: the one-hole-at-a-time process, editor roles, automated checks and standing rules."
---

Rules file: `rules/gates.json` (every number below is defined there; read it with `tools/rules.py gates`). Course overrides: `course.json` → `rule_overrides.gates`.

# Course build: process and gates

The order of work and the checks that stop a hole going back for a second or third round. Learned on Tierra Rejada (Oct 2026) and reused on Penmar, Harding, Wilson, Hansen Dam, Angeles National and Moorpark. The full set of element guides is in the moorpark repo under `course-build/playbooks/` (one per element: terrain, greens, bunkers, rough, tees, trees, brush, water, paths, OB, props, look, flyovers, data pipeline); read the matching one before building that element.

## Standing rules (apply to every element)

1. **Never invent ground.** Everything visible comes from the satellite, the LiDAR, a clean scan or BoJa's photos. Where a photo and the LiDAR disagree on a shape, the photo wins, except green surfaces and contours never change without a scan.
2. **No defects on in-play turf.** Tees, fairways, greens, collars and surrounds are built clean and even even when a photo shows wear. Bare soil only on out-of-play hillsides and banks, or where BoJa asked.
3. **No plants on mown turf.** Run the plants-on-turf check after every planting pass.
4. **No islands in bunkers.** Only a grass nose joined to the bank, approved by BoJa per bunker.
5. **Fences are moved, never deleted, and always continuous.** Mulch never on concrete. One shared concrete. Bins only where a photo shows one.
6. **Shared materials stay shared** (fairway, tee, sand, concrete). Per-hole looks are copies (`*_h17`) on that hole's meshes only. Course-wide passes are done once and shown on the control hole before and after.
7. **The first signed-off hole is the control** (Tierra: hole 10). Every later course-wide pass is rendered on it before/after; report "control 0.0".
8. **Photos first.** Photo inventory per hole before building; photo | before | after pairs from the same pose for every edit; a verdict per photo before BoJa sees a flyover. BoJa should never be the one to find a mismatch.
9. **Ask once, with a default.** Every question carries a default that gets built if unanswered. Goal-changing questions (what is OB, which green plays, where the real forward tee is) are asked before building.
10. **Back up before every change** (scene, terrain, GKD). Never touch `C:\GSProV1\Core\GSP\Courses\` without BoJa's go.

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

1. **Photo inventory.** Every geotagged photo for the hole (photo, hole, GPS, heading, confidence). Search BoJa's EXIF originals first, every time.
2. **Step 0: poses.** Render from each photo's position/heading; fix poses by content (skyline, sign, path corner, tee markers), not GPS alone (GPS put the Tierra 10 Blue tee photos 80 m away). Only high/medium confidence poses give verdicts.
3. **Blind reviews, then one edit list.** Independent reviews of the pairs merged with a critic pass into a numbered list: spec, evidence, measured colours (r/g, b/g), photos to check on. Cut to the top 8.
4. **Rules check.** Mark rule breaks (plants on turf, gaps in turf, islands): those go first.
5. **Build one edit at a time.** Back up, edit, render after-pairs at the same poses, run the checks, save, commit, send the hash. Course-wide items done once, shown on the control hole.
6. **Gate.** One sheet with every pair, checks green, rules ticked, plus the flyover. Only a passing hole goes to BoJa.
7. **Apply BoJa's answers** in a "BoJa's answers applied" block; unanswered questions build their default.
8. **Sign-off, then freeze.**

## Automated checks (every time)

- Bunker islands: clean except approved noses.
- Plants on turf: 0 (incl. the own-soil-patch case).
- Floating overlays/objects (above ground by more than their height).
- Unsupported bins; broken/gapped fences; free-ended fence pieces; objects on turf inside a rope line.
- Vertex paint on every rebuilt mesh (else bare dirt).
- Null/magenta materials; z-fighting at low angles from 30-80 m.
- Height gaps where fairway/collar sits above rough (dark seams; up to 14 cm on Tierra 1).
- Control-hole turf change = 0.0 unless intended.
- Prop placement: nothing on paths, sand, tees, greens, water, or in a hitting line unless photographed.

## Pitfalls we paid for

- Multi-hole passes produced three rounds of rework. Only shared materials/models get course-wide passes.
- Knowingly saved "good enough" defects (16 bunker creases, black pocket) came straight back.
- A re-run scene setup tool wiped Harding's trees and meshes: wipe tools must ask and never re-run on a built scene.
- Four editors froze the PC; batch jobs during BoJa's review had to be killed.
- Copied tools kept another course's constants (3000 m, `S = 2000`): grep before running.
- Tools read the GSPro Courses folder instead of the project GKD.
- Pixel-box specs on old renders go stale; say which save and pose set a spec was measured on.

## Ask BoJa once per course (with a default)

1. Target look: plain render (Tierra) or the shipped post-processing volume.
2. Season: green "spring vibe" rough with dormant hillsides (Tierra) or dormant everywhere.
3. Signature holes that get extra passes first.
4. Sign-off unit: hole (default) or nine.
