---
name: "course-build-edges"
description: "Use when building, repairing or checking the joins between green, collar, blend, fairway, rough, bunker lip and sand in a GSPro course build: one coordinated boundary built at the Blender/OPCD conform stage before export, then verified with geometry, matched views and rolls on the same candidate."
---

# Course build: coordinated edges and bunker lips

Status: PROPOSED (review package 11/15). The documented method below is not yet backed by a qualified conform-stage tool. A first Blender-side checker, step 6b builder and feature manifest exist for Tierra only (`tierra-rejada` PR #7, unmerged: geometry only, no Unity round trip, views or rolls); stale-join tracking does not exist. Documentation is not enforcement. Until a qualified tool exists, report this stage as UNIMPLEMENTED for new courses (UNQUALIFIED on Tierra), keep using the fixed-boundary repair in `course-build-greens` for existing courses, and never mark an unmeasured check as PASS.

This skill owns the joins between surfaces. `course-build-greens` owns approved green contours and pins, `course-build-bunkers` owns the photographed face shape and sand depth, `course-build-rough` owns the surrounding turf. A boundary edit always includes every surface that meets at that boundary, with its patches and colliders. Full contract: `course-build/playbooks/edges_and_lips.md`; rules: `rules/edges.json`.

## Why (Tierra Rejada, Oct-Nov 2026)

Every green edge and bunker lip on Tierra was a stack of separate single-sided sheets (OPCD green, blend strips, fairway, rough, bunker, then our collar patches, lip rings and repair strips) that never shared a boundary. Three failure modes followed: a gap (black slit, ball drops 25-30 cm to the lowered terrain: holes 9, 12; 1.3 m under the 16 bunker edge), two sheets a few mm apart (dark flickering line: 1, 5, 7), and an upper sheet's open edge above a lower one (ball rolls under it: 4, 5, 11, 16). Each later pass moved one sheet and opened a new seam beside it. Problem putt rolls at the 18 green edges: 165 on the saved course, 6 with all repairs combined on a copy (11/15).

Separate objects are allowed. The requirement is a shared boundary and exactly one playing sheet at every point, with the right physics on each side.

**Scope: every shared boundary (BoJa 11/17).** The one-owner rule (`course-build-gates` rule 15) covers every boundary on the course, not only greens and bunker lips: rough to rough, fairway to blend, tee pads and their banks, paths and curbs, water edges, soil and planting beds. No mesh or object edits a shared edge independently; the boundary's owner is edited and every neighbour is regenerated from it, shared vertices agreeing within 1 mm. Dependents (colliders, fence posts, curbs, plants, water, GKD hazards) are rebuilt or re-seated in the same step. Tierra 1117f: only 217 of 646 ground meshes were still the original export, and 19.7 km of playing-surface boundary sat 1 mm to 0.5 m from its neighbour (`skillcheck.py` EDGE).

## Where it runs: the Blender/OPCD conform stage, before FBX export

The course meshes are made in Blender by OPCD (Tierra's headless replica: `tierra-rejada/blender_scripts/conform.py`, then `export.py` writes one FBX per mesh). The boundary build belongs in that same Blender session:

1. Import terrain, then the SVG-validation gap snap (conform.py step 1b).
2. Conform all meshes to the terrain (step 2).
3. Bunker depth (step 3), water depth and planes (4-5), flatten tees (6).
4. **Boundary build (new, step 6b)**, after every shape and depth step and before the save and export (below).
5. Boundary checks on the Blender result (below). A failing join blocks export of that hole's meshes.
6. Save, then `export.py`. Record the hash of every exported FBX with the boundary IDs it carries.

Unity then imports, assigns materials and physics, lowers the terrain under the meshes and builds colliders. Those steps must not move a boundary vertex.

**Any later geometry change in Unity invalidates the joins it touches.** That includes collar flush or rim tools, `LipDrop`, `BunkerDepth` re-runs, Lower Terrain Under Meshes, tee-pad levelling, patches, path trims and any repair candidate. The changed meshes' boundary IDs are marked stale, and the hole cannot pass a gate or install until each stale join is rebuilt (preferred: back in the conform stage) or re-checked in Unity with the same checks and gets a new evidence record.

## Build (step 6b)

1. **Feature manifest.** Every expected green, collar region, blend strip and bunker lip has a stable ID, its source outline, its neighbours and an owner. Material or name is not identity: a collar drawn as fairway grass is still a collar. Compare expected vs found vs processed IDs for the actual course (27 greens at Temecula, not 18); an unexplained missing feature blocks the stage.
2. **Freeze shapes first.** Approved green contours and pins, collar widths, outer patch boundaries, photographed bunker shape and graded sand are inputs. Uncertain outlines (NAIP traces, unmapped bunkers) stay provisional and are not polished.
3. **Build each boundary as a unit.** The green's own edge vertices are the collar's inner ring (the fixed-boundary method of `course-build-greens`, run at build time); the collar's outer ring is the rough band's inner ring; the rough band's sand-side ring is the sand edge. Clip competing faces and retriangulate the surviving neighbours, including endpoints, narrow strips, concave corners and three-way joins. Do not sink another playable sheet under the new one, and do not delete whole intersecting triangles as a shortcut.
4. **Bunker lip is explicit.** After sand grading, build the lip band from the rough/collar edge to the sand edge, sharing vertices with both. Keep intended sharp bunker cuts; flush turf never means flattening a sand face.
5. **Finish the mesh in the same step.** UVs, vertex colours, material slots, normals and tangents, and the physics name the Unity physics pass reads. A rebuilt mesh that renders black (Tierra 11 tee v2) or keeps the stock near-black `Fairway Apron` is incomplete.

## Verify one candidate

- **Geometry first**, in world coordinates: unmatched boundary segments, gaps, duplicate or intersecting playable faces, open edges standing over a lower surface, degenerate triangles, wrong winding, internal creases (Tierra 16: a 4 cm crease inside one rough mesh), collider vs render. A top-height survey alone is not enough. A ray grid is a supplement only: it misses cracks narrower than its spacing, and Unity's RaycastAll must not be assumed to enumerate every overlapping triangle inside one mesh collider; qualify the query on fixtures with known overlaps, or use explicit triangle queries. Run time is unmeasured until benchmarked.
- **Known sites, then broad samples.** Every known defect and every new geometry flag is rolled in both directions with the putt lab protocol (`course-build-shot-test`), before broad sampling. Known sites stay in the regression register for good. Report tested, failed, skipped and unmeasured separately. Until the putt lab is validated its failures are open events pending investigation, not release failures or passes; release is blocked by established geometry defects and qualified acceptance failures.
- **Matched views of the same spots, from the same candidate.** Fixed low and oblique cameras, photo references where they exist, and a short moving view for flicker. Geometry, views and rolls all name the same candidate hash.
- **Combined, then release.** After local results pass, the combined-course regression and the existing qualifying run, install gates and BoJa's look approval still apply. Reuse a baseline only when scene, dependencies, tester, rules and sample set all match.
- **Geometry-aware regression.** Compare world-space vertices, boundaries, object counts and colliders against the immutable baseline, by position. File hashes alone are not a regression check: a shared material or asset reference change must not fail an untouched area, and an unchanged file elsewhere says nothing about a neighbour whose edge moved.

## Legacy tools (kept for reproducing old builds and repairing existing courses)

`ApplyCollars1029` (proud collar patches), `CourseCollarFlush` / `CollarLaplace` with `SinkUnder` (other turf 5 mm / 3 cm under the collar) and `LipDrop` (turf ring lowered within 1.4 m of sand) are not defaults for new builds and are not the broken-edge repair; keep them for reproducing old builds and DryRun diagnostics, and give any exceptional Apply a scoped reason and the full geometry and view checks. `GreenTie` (retired) is prohibited outright. Running any of the others on a built course is a Unity geometry change and makes the touched joins stale (above). Whether `SinkUnder`'s 5 mm spacing caused Tierra's dark lines is unproven: test it with the lower layer kept vs removed, same camera, light and material, in motion.

## Rollout

Prove on one representative green join and one irregular bunker lip, on copies, against the current repair method with identical views and rolls; measure elapsed time and manual steps. First a no-edit round trip (conform → export → Unity) must keep position, scale, boundary coordinates, colours, materials and physics. Approved courses are migrated only as separately reviewed candidates; installing this skill authorises no rebuild.