---
name: "course-build-lidar-review"
description: "Use before the first gate on any GSPro course: run the 18 LiDAR decision points (D1-D18), record answers per hole, set the visit flag and write the in-person list."
---

Rules file: `rules/lidar_review.json` (every number below is defined there; read it with `tools/rules.py lidar_review`). Course overrides: `course.json` → `rule_overrides.lidar_review`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/doctor.py`: survey line (`course.json lidar.survey`), heightmap, GKD and SVG registered before the review starts (D13).
- `python ../course-build/tools/green_report.py --smooth 0.6 --sheets`: per-green slope, relief, p90, pins over `greens.pins.max_lie_angle_deg`: the D7 data-side answer and the `outline_over_8pct`-style flag (`flag_slope`).
- `python ../course-build/tools/scan_index.py`: when a phone LAS exists, its fit against the LiDAR (MAD, coverage, doubled) decides whether the scan replaces the LiDAR (D7); rules `photos.scan`, `lidar_review.d7_scan_fit`.
- `python ../course-build/tools/flyover_gate.py --no-plots`: terrain and crown clearance along every hole from the LiDAR trees: a quick check that `trees_csv` and the heightmap agree (D18 tree vs shrub misreads show as crowns in odd places).
- (no tool yet: `decisions.csv` writer with the 18 points pre-filled from the data (D1-D18 defaults from `rules/lidar_review.json`); candidate for `tools/`)

# LiDAR detail review (D1-D18) as a pre-build pass

Run against the course's LiDAR, satellite and OSM before anything is built; record answers per hole in `course_photos/decisions.csv` (point, hole, answer, source, photo, decided_by, date); write the in-person list for `course-build-site-visit`. Full review with the Tierra cases and pass rules: `playbook/LIDAR_DETAIL_REVIEW.md`; summary: `course-build/playbooks/20_lidar_detail_review.md` (moorpark repo).

## First: which survey

Each course README states it. Tierra, Hansen Dam, Moorpark: 2018 SoCal Wildfires B3, QL2, ~4 pts/m² (nothing under ~1 m wide or 5 cm tall is real). Angeles National: CA_LosAngeles_1_B23, QL1 2023-24, 38-45 pts/m². The LiDAR is the frame; every other input is fitted to it and its shift recorded before use (D13).

## The decision points

| # | Question | QL2 | QL1 / phone scan | Default | In person? |
|---|---|---|---|---|---|
| D1 | Out-of-play cell: mown, dry grass, scrub, soil, rock? | Brush mask only | Dry vs mown resolves; colour/species don't | Scrub on grey-brown litter 70-80% on slopes >15° outside the corridor; no shrubs on turf | **Yes** |
| D2 | Where does the mown corridor end? | Satellite | Satellite | Satellite edge | If satellite old/dry |
| D3 | Steep cells: rock or scrub? | Slope only | Face shape resolves | Scrub; rock only where pale and >35° | **Yes** |
| D4 | Sand edge | Satellite over OSM | Depression edge ~0.3 m | Satellite sand edge | No if recent |
| D5 | Bunker depth/face | Under-read; rule depths, LiDAR +0.3 m min | Within ~10 cm | Greenside 0.8-1.2 m, fairway 0.4-0.7 m, steep green-side face | Yes on QL2 |
| D6 | Grass in sand: island/nose? | Not in data | Not in data | None | **Yes** |
| D7 | Green surface source | LiDAR unless scan fits (MAD ≤5 cm, ≥80%, no doubling) | Phone LAS still wins | LiDAR + slope report; pins off >3% | Scan per green |
| D8 | Outline vs pad | Shift fit + satellite tone; disagree → satellite outline, LiDAR banks | Pad edge ~0.3 m | Satellite outline, LiDAR surround | No |
| D9 | Collar, step, surround | Not in data | Not in data | Collar 1.2 m at 1.5 cm; 3 m sides / 5 m back; deep rough 6 m off | **Yes** |
| D10 | Beyond LiDAR around a green | Only where a photo shows a bank/lip; greens never | Rarely | LiDAR | Photo check |
| D11 | Water level/extent | Reliable | Reliable | LiDAR level and outline; nothing covers water | No |
| D12 | Creek: channel or sheet | Mostly not in data | Resolves >0.5 m | 1.5 m channel 0.4 m deep, water 4 cm above bed | Yes on QL2 |
| D13 | Inputs registered? | Fit and record shifts (OSM 1.5-3 m, scans 1.5 m + 2-4 m vertical, photo GPS 3-80 m) | Same | No input used until its shift is recorded | No |
| D14 | Props/buildings: where really | Roofs only | Roofs, enclosures >1 m, poles | Not built until it has a source | **Yes** <1 m, doors, signs, bins |
| D15 | How far does real ground go | DEM to horizon, OSM roads/buildings | Same | Real DEM, OSM roads, no invented hills | No |
| D16 | Lower surface covering a higher one | Build check: water > bunker > green > collar > tee > fairway > rough | Same | Clip order, zero violations | No |
| D17 | Which flat is the tee, decks | Flats ≥40 m² within 60 m of an OSM tee | Flats and steps | LiDAR flat nearest card yardage; unused pads = mounds | Which colour on which deck |
| D18 | Tree, shrub, hedge, building | <3 m wider than tall = shrub; >3 m = tree, species blank | Crown shape | Oak <16 m, eucalyptus over, species blank | **Yes** within 35 m of tee/green |

QL1 courses skip the data-side guesses in D4, D5, D8, D12, D17 and still get the in-person list (D1, D3, D6, D9, D14, D18 + scan per green). Photos win on bunker faces, banks, lips; LiDAR wins on green surfaces, water levels, fairway relief; a clean scan wins over both.

## What the LiDAR gets right (keep as checks)

Fairway relief (95-100% kept, RMS 1.3-3.1 cm; never smooth for looks), green tiers and false fronts, tee pads, water levels, roof blocks, rangefinder geometry (the card runs long, not the model).

## Procedure

1. Read the survey line; fetch the extent; D13 registration on a cart path, a roof and a pond edge; record shifts.
2. D1-D18 per hole from the data into `decisions.csv`, `default` where nothing answers.
3. Set `visit: planned | none` with BoJa, once.
4. Build what the data settles to gate quality; build the open points to defaults **once** and leave them. Visit planned: finish after the visit and index. No visit: mark holes data-only with the open points listed; no polish passes on them. Dated online photos may close a slot when they clearly show the feature; never colour.
5. After the visit: fill the slots, close the points, run the gates on finished holes.

## The ranked Tierra misreads (don't repeat)

Hillsides as mown turf; shallow flat-faced bunkers; greens from 2018 LiDAR then rescanned; pond plane 0.47 m above the water with a slab hiding a third of it; green meshes 1.5-3 m off the pad; outer ground as old drape and a highway read as a wash; cliffs on the wrong cells (mask rows flipped); rough over collars; tee pads as greens or craters; 2,714 shrubs planted as trees; a creek as a flat sheet; props by eye 10-40 m off; collars assumed from nothing.

## Decision points (ask once, with a default)

1. Which course is next and its survey line.
2. Which points may stay on default for a first playable version (default: D7 and D13 must be closed; the rest may wait for the visit).
