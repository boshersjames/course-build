---
name: "course-build-greens"
description: "Use when building or checking green surfaces, the green-to-collar edge, collars, approach cuts, pins or GKD green settings in a GSPro course build."
---

Rules file: `rules/greens.json` (every number below is defined there; read it with `tools/rules.py greens`). Course overrides: `course.json` → `rule_overrides.greens`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/green_report.py [--holes 7,9] [--smooth 0.6] [--sheets]`: LiDAR slope mean/p90, relief, GreenKeeper pin-area test on every GKD pin, usable %, valid areas; rules `greens.pins.*`, `greens.internal_slope_pct`, `greens.checks.noise_lowpass` (the only smoothing allowed, via `--smooth`).
- `python ../course-build/tools/gkd_tools.py pins`: each pin's lie angle and height offset against the LiDAR; rules `greens.pins.max_lie_angle_deg`, `terrain.checks.pin_height_tol_m`.
- `python ../course-build/tools/scan_index.py`: phone LAS scans registered to the LiDAR, MAD inside the green outline, coverage, doubled ground, ACCEPT / RESCAN; rules `photos.scan.*` (5 cm MAD, 80% coverage).
- Unity `Course Checks > Greens: all-18 report` (`CourseGreenChecks.cs`): the mandatory all-18 table before every install: edge step/lip, collider dy, three tiers, crown overhang, pins, areas; writes `AutomationOutput/checks/greens_all18.csv` and the 0.5 m height grids; rules `greens.checks.edge_survey`, `collider_vs_render_max_dy_m`, `crown_overhang`, `tiers`. The edge profile reads ground colliders only (trees, shrub chunks and plant-physics props are skipped) and names the collider pair at the worst step.
- Unity `Course Tools > Greens > Collar flush` (`CourseCollarFlush.cs`, bridge `invoke -Tool CourseCollarFlush -Method DryRun | Apply | Revert`, `-Set @{ only = "<collar mesh>" }` for one): every collar rim pinned to its neighbours, interior a membrane, turf under it sunk; rules `greens.collar.height` = flush and `greens.collar.flush_tool.*`; the rim pins by surface class (`flush_tool.pin_priority`) and `flush_tool.conform_max` caps every collar vertex at the designed surface beside it; pass = the all-18 table's step column under `greens.checks.edge_survey.max_step_m`.
- Unity `Course Tools > Greens > Rim reset` (`CourseRimReset.cs`, bridge `invoke -Tool CourseRimReset -Method DryRun | Apply | Revert`, `-Set @{ hole = "14" }` or `only = "<collar mesh>"`): moves each collar rim vertex by its own residual to the neighbour sampled `rim_reset.probe_m` outside the rim, signed as measured, near the failing samples in `checks/steps.csv`; the dry run's share-down and net signed move gate the apply (`rim_reset.min_share_down`); rules `greens.collar.rim_reset.*`; loop RunAll, `edge_profiles.py --steps-out`, Apply, RunAll, `edge_profiles.py`; pass = the pair table under `rim_reset.pass_criterion`.
- `python ../course-build/tools/edge_profiles.py`: headless twin of the edge survey on `checks/profiles.csv`; real steps per hole with slope removed (`greens.checks.edge_survey.step_definition`, `slope_window`) and the step table by surface pair across all greens.
- `python ../course-build/tools/edge_survey_summary.py`: groups the edge-survey steps over `greens.checks.edge_survey.max_step_m` by the collider pair they cross; one pair on every green is a convention to fix once, not eighteen edits.
- `python ../course-build/tools/gate_report.py`: merges the above into one table and adds fall direction vs LiDAR from the height grids; rules `greens.checks.fall_direction`.
- (no tool yet: SinkUnder / CollarLaplace / RegionLaplace as course-agnostic editor commands; they are in `unity/course-templates/TierraNight1031.cs`; candidate for `unity/shared/`)

# Course build: greens and collars

Follow the standing rules in `course-build-gates`. Green surfaces and contours never change without a scan. LiDAR review decision points for greens are D7-D10 in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo). Full guide: `course-build/playbooks/02_greens_and_collars.md`.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Stimp / firmness (GKD) | `DefaultStimp 2` (= stimp 10), `MaxStimp 5`, greens Normal | BoJa approved on every course |
| Collar width | 1.2 m fairway-height ring on every green; 3 m sides / 5 m back where the surround is flat and BoJa approved (Tierra 6, 8, 15); 1.5 m where a bank sits right behind (18) | green_edges_1029 |
| Collar height | FLUSH with the green edge and the surround: no proud cut step. Re-grade each collar as a membrane between its rim neighbours (`CollarLaplace`); a 2-3 cm proud collar and its 6-14 cm raised band played as a bumper on every Tierra green (BoJa 11/1: hole 1 rejection, 7 run-out) | BoJa 11/1 |
| Three surface tiers | Green: GSPgreen. Collar: distinct longer-cut band, OPCD `Fairway Apron` material, GSPfairway (rolls less than the green, "a slightly denser fairway"). Next band out: GSPrough first cut (light rough, never deep rough). Tiers differ in material and physics only, never in height | BoJa 11/1 + real 17 photo |
| Collar on top | Any rough/blend/fairway mesh under a collar footprint sits 5 mm under the collar within 0.4 m of the collar rim and 3 cm under further in (`SinkUnder`); otherwise it pokes through as light patches and the ball plays on rough inside the collar | Tierra 14, 11/1 |
| Green edge blend | 0.12 m strip in exactly the fairway/rough tone, no lime, no extra brightness | Wilson 1b |
| Uphill banks behind | Rough, not collar | Tierra 10, 18 |
| Deep rough | ≥6 m off any green | Tierra 6, 15 |
| Pins | None within 3 m of the edge; pin area ≤1.7° (= 3%) by the GreenKeeper method below; flag, don't move silently | pin_check, Green Studio review 10/31 |
| Pin areas | ≥4 valid pin areas per green, ≥3 m apart (Thu-Sun) | Green Studio review |
| Internal slope | 1-3%, run-offs into surrounds, no dead-flat greens; flag greens >3% | Hansen |
| Transition | One profile (collar width, step, rolloff) on all 18 greens, from the clean scans; banks follow each green's LiDAR | BoJa 10/29 |
| Green tint (Tierra) | OverallColor ×(1.85, 1.68, 1.42), desaturate 0.2, on `*_Tierra` copies; milder stayed grey, no-desaturate went neon | green_test |

## Procedure

1. Data: `coursekit.py svg --collar --collar-back --deep-off-green`; `apply_checklist.py` writes `green_slopes.csv`.
2. Confirm the green in play where the satellite shows two (Hansen 11); dress the other as rough; ask BoJa if photos don't settle it.
3. Surface from the LiDAR; apply a clean phone LAS scan where there is one (|course − scan| median 1 mm, p90 6 mm). Spike/pit cleanup on scan-less greens.
4. Apply the course edge profile: ring on greens without one; rebuild wider approved collars; re-grade every collar FLUSH (no proud step; see Collar height). Never lift a collar to hide rough, not even render-only for a video (a 3 cm render lift put pale shards round every green in the Tierra showcase v2).
5. Seams: weld the collar's inner edge to the green edge vertices (a gap where the green blend strip meets the collar shows as a thin line or dark slit: Tierra 11); weld collar holes; ease the rough under the collar rim so the outer edge is one smooth line (Tierra 10, `SinkUnder` rule), never by dropping whole triangles (saw-tooth); collar skirts that show as dark slits or pale shards are a mesh fault, fix them in the scene; drop rough triangles overlapping collar/green/blend (a rough mesh hid Tierra 11's collar behind a saw-tooth band); fade the fringe over ~1.8 m on a hole-only rough copy if a dark band remains. Patches (fairway, first cut, soil) never lie over a green or collar; trim them at the collar's outer edge.
6. Surrounds: one approach cut collar→fairway where photographed, not a long-blade rough ring; bank rough close in on uphill banks.
7. Pins raycast to the new surface and run the pin-area test, after any edit near a green and after every install (raycast on the installed build, not only the editor); when a green moves, move the pins and say so.
8. Contour edits (crest, tier, flatten): smootherstep falloff `6t^5-15t^4+10t^3` with a core ≥1.25 m and outer radius ≥6 m (10 m if the surround moves too); vertical change ≤0.35 m on the green (0.6 m with the surround); fade to zero within 1.5 m of the green edge; green/blend/collar boundary vertices fixed (≤1e-4 m) unless the surround moves with them. Split triangle edges >0.75 m within 1.8 m of the edit first. Keep the edit only if the pin-area test still passes ≥4 areas and the usable-area % didn't drop (Tierra 9 crest: build it, test, ship only on a pass).

## All-18 checks before every install (BoJa 11/1: "collar height and even flush need to be checked on every hole")

Mandatory on all 18 greens before every install, not a sample and not only edited holes. The install report carries a per-hole pass/fail table; any failing hole blocks the install. Thresholds are the ones used on the PC (last section); tighten them from roll tests.

| Check | Tool | Pass |
|---|---|---|
| Flush and collar height (edge survey, every ~1 m, -0.5 to +2 m at 5 cm) | `EdgeSurvey` | no step > 1 cm that isn't plain slope; lip ≤ 2 cm above the chord; bunker rims exempt |
| Collider = render on green, blend, collar; one collider per green point | `ColMismatch`, `ColVsRender`, `AllHits` | dy ≤ 1 cm on every vertex, no duplicates |
| Three tiers: green GSPgreen; collar GSPfairway (never rough or green); next band GSPrough first cut | Collider Inspector / scene query | all 18 correct |
| Fall direction vs LiDAR (Tierra 7) | `GridHeights` | median aspect diff ≤ 20°, ≥ 80% within 45°, ≤ 5% reversed, slope corr ≥ 0.7; no mirror/180° test matches better |
| Approach ledges, aim point → green centre and ±5 m lines | `Profile` | no step > 3 cm or slope > 25° between samples |
| Crowns over greens and collars (Tierra 16) | `CrownsOver`, `PlacementGate` | no trunk on a green or collar; crown overhang < 1.0 m (build blocked at ≥ 1.0 m; smaller overhangs listed for review) |
| Pin areas | `TierraPinArea` | every GKD pin ≤ 1.7°; ≥ 4 valid areas per green |
| Visual edge look (oblique still of every green) | render / flyover end pose | no pale shards, slits, double lines, creases or bunker-lip cracks |

Report table (all 18 rows, every install): hole, max step (cm), max lip (cm), collider dy (cm), tiers OK, fall-direction median (°), approach max step (cm), crown overhang (m), pins OK, visual OK, pass.

## Pin-area test (GreenKeeper method)

- Lie at a point: probe 0.3 m N, E, S, W on the top surface; angle = asin(dz / 0.3) per probe, rounded to 0.1°; worst |angle| counts.
- Pin area passes when the worst angle over 17 points (the pin, 8 at 0.45 m, 8 at 0.9 m) is ≤1.7° (3%). Stimp 10 alone would allow 2.2°, but our rule is 3%.
- Usable area %: share of a 0.75 m grid (≥1.2 m from the edge) that passes; report it before and after any green edit.
- Tool: `TierraPinArea` (Unity editor, A) runs the test on every GKD pin and reports the usable %.

## Trees and plants: never on or over a green

- No trunk AND no crown over a green or collar. Test the crown footprint (renderer bounds radius, sampled in 16 directions out to the edge), not just the trunk: Tierra 16 passed a trunk test with three crowns over the collar (BoJa 11/1: "you can't have trees on the green").
- Keep crowns ≥1 m clear of green and collar. Fix by moving away from the green, but a move may never land a crown on any mown turf (green, collar, fairway, tee); if it can't clear within 8 m, shrink the plant so the crown stops at the edge (default) or list it for BoJa. Never delete.
- Range-net / tee-gate passes that move trees must re-run this test: Tierra's BadSpots pass pushed 24 trees onto the hole 1 fairway edge.
- The install script's placement gate (`TierraNight1031.PlacementGate`, run per green and tee box) blocks a build on any crown ≥1 m over a green or collar; other overhangs print as warnings; BoJa-approved exceptions go in `AutomationOutput/placement_exceptions.txt`.

## Ball-roll checks (after any green, collar or surround edit)

- Edge survey (`EdgeSurvey`): profiles every 1 m around each green, -0.5 to +2 m at 5 cm. Target: no step >1 cm that isn't plain slope, no lip >2 cm above the green-to-approach chord (bunker rims excepted). Report per green before/after.
- Collider = render: `ColMismatch` / `ColVsRender` on the green, blend and collar; one green collider, no duplicates.
- Steep or "fast" greens: compare mesh grade (mean, p90, % over 3/4%) and fall direction (aspect, point by point) with the LiDAR before touching anything. A green that matches the scan is the real green; only a noise low-pass (Gaussian sigma 0.6-1.5 m, edge fixed) is allowed without a scan, and only when the mesh is steeper or spikier than the LiDAR (Tierra 10: spikes; Tierra 7 and 17 matched the scan).
- Approach: profile the line from the aim point to the green centre; no ledge where fairway, rough and collar meet (Tierra 1: 0.27 m drop 13 m short).

## Checks

- Green outline vs LiDAR pad: >1.5 m off → stop and send a top view (pins move with the green).
- Vertex paint on green, collar, blend after any rebuild.
- No lime outline ring; no black slits at the green edge.
- Collar renders 10-20% darker and deeper green than its green (tint the per-green collar material copy), never paler.
- Visual edge check from a high oblique still of every green (the flyover end pose): no pale shards, slits, double lines, creases or bunker-lip cracks; a clean measured edge can still look wrong from above (Tierra 10, 11, 14, 11/2).
- No surface crosses its boundary: turf/tee/green never spills onto a cart path or into a bunker.
- Collar-to-rough height gap ≤ a few cm, bridged in exactly the rough's settings.
- Island check and plants-on-turf after the collar pass.
- `outline_over_8pct` high → the OSM outline took in banks; re-trace.

## Pitfalls

- Photo-only (COLMAP) reconstruction fails on putting surfaces (Tierra 17: 110 m² at 13 cm median); use a phone LAS.
- Reshaping an outline to a photo is allowed only with contours unchanged and on BoJa's answer.
- Shifting a green toward a bunker put dark slivers in the edge; uncovering the hidden collar fixed the photo distance.
- Greens built on the Tee material need their own tinted copy.
- Data scripts that set stimp 11 / Firm were wrong.
- Tierra 2: a fairway patch (SPatch_h2back) covered the back-left of the real green; check every patch against the satellite green.
- Tierra 9's flat crest waits for a green scan and the pin test; no relief edit from photos.
- Green Studio (Blender add-on) is GPL-3.0: borrow its ideas and numbers, never paste its code.
- The OPCD template post profile `v4_Sunny_Pop` (+2.3 EV, ACES) washes greens near-white so contours and putt lines can't be read; ship a course copy with lower exposure (Tierra `TR_Sunny_C`: Neutral, +1.2 EV, contrast 22, saturation +8, bloom 0.6) and check a white line still stands out on the green.
- Re-grading a collar flush makes overlapping rough show through unless it's sunk under the collar (see "Collar on top").
- Measured checks passed while the greens still looked wrong in the flyover (pale shards, seams, creases): the edge survey measures height, not appearance. Always look at an oblique render of every green before calling it done.
- A revised skill only helps once it is saved: the flush-collar rules sat in a draft file for a day while sessions loaded the old skill (1.5 cm proud collars).

## Ask BoJa once (with a default)

**In person even with a newer scan:** collar width and cut step (D9); a phone LAS per green (D7). QL1 data settles the pad offset (D8); on QL2 courses check it against photos. Going beyond the LiDAR around a green (D10) is BoJa's call per green.

1. Collars wider than 1.2 m on specific greens (which, and why).
2. Photo outline vs scan: reshape or keep (default keep the scan).
3. Pins failing the pin-area test (>1.7° / 3%): move or keep (default keep, listed).
4. Relief exaggeration on a flat crest (Tierra 9: ×1.3-1.5, pins ≤3%).

## Exact thresholds used on the PC (11/1)

- Edge survey (`TierraNight1031.EdgeSurvey`): sample points every ~1 m along each green mesh boundary (outward normal from the green centroid); profile from -0.5 m (inside) to +2.0 m (outside) at 0.05 m, height = first OPCD collider hit from above. Step = largest jump between neighbouring 5 cm samples; lip = largest rise above the straight chord from -0.5 m to +2.0 m. Flags: step > 1 cm (note: a 20-25% bank alone gives 1-1.3 cm per 5 cm, so check the lip too), lip > 2 cm. A clean green (Tierra green_12/13) measured step ≤0.8 cm, lip ≤1.2 cm. Bunker-rim points are exempt (profile runs into sand).
- Collider vs visible: `ColMismatch` lists any OPCD MeshFilter whose MeshCollider uses a different mesh asset; `ColVsRender` compares the two vertex-by-vertex (same vertex count) or by raycast; flag |dy| > 1 cm on any vertex. Also: exactly one OPCD collider hit stacked over a green point (`AllHits`), no duplicates.
- Fall direction vs LiDAR: 0.5 m grid of collider heights over the green (`GridHeights`), central-difference slope per point vs the LiDAR heightmap slope at the same point; aspect = compass bearing of the downhill direction. Pass: median |aspect difference| ≤ 20°, ≥ 80% of points within 45°, ≤ 5% reversed (>135°), x/z slope correlation ≥ 0.7. Also run mirror-x / mirror-z / 180° tests: if any of them matches better than the scene, the surface is mis-registered. Tierra 7: median 13°, 89% within 45°, 1% reversed, corr 0.80/0.87 (passes).
- Slope vs LiDAR: area-weighted triangle grade mean / p90 / max and % area > 3% and > 4% (`GreenInfo`), against the LiDAR grade on the SVG green outline (0.5 m grid). Noise low-pass is allowed only when the mesh p90 or max exceeds the LiDAR's (Tierra 10: mesh p90 20.5% vs LiDAR 17.7%, max 146%); sigma 0.6 m first, edge fixed, ease 1.5 m; re-run the pin test after.
- Approach ledges: profile (`Profile`) every 0.25 m along aim point → green centre and ±5 m parallel lines; flag any step > 3 cm between samples or slope > 25° between samples on fairway/collar/rough in the approach. Fix with a local membrane re-grade (`RegionLaplace`, reach 2.5 m, rim pins only to the named neighbours and only within 0.35 m of height). Tierra 1: 0.27 m drop removed.
- Crowns over greens (`CrownsOver` / `PlacementGate`): crown radius = min(x, z) half-extent of the plant's renderer bounds; sampled at 0 / 0.33 / 0.66 / 1.0 of the radius in 16 directions; a sample whose top OPCD surface is a green or collar is a hit; overhang = radius - distance to the hit. Gate FAIL: overhang ≥ 1.0 m over a green or collar. Moves: away from the hit, ≤ 8 m, never onto any mown turf; else shrink by (R - overhang - 0.3) / R, never below 0.5x per pass.
- Collar on top (`SinkUnder`): other turf vertices under the collar footprint: within 0.4 m of the collar rim → collar - 5 mm; further in → collar - 3 cm.
