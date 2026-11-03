# 20. LiDAR detail review (D1-D18) as a pre-build pass

Use this before the first gate on any course: run the 18 decision points against the course's LiDAR, satellite and OSM, record the answers per hole in `course_photos/decisions.csv` (columns: point, hole, answer, source, photo, decided_by, date), and write the in-person list for the visit (16). The full review with the Tierra cases, checks and pass rules is `playbook/LIDAR_DETAIL_REVIEW.md` (from the LiDAR detail review thread); this guide is the working summary.

## First: which survey is the course on

Each course README states it. Tierra, Hansen Dam and Moorpark: 2018 SoCal Wildfires B3, QL2, ~4 pts/m² (one ground point per 55-70 cm, smoothed over ~1 m; nothing under ~1 m wide or 5 cm tall is real). Angeles National: CA_LosAngeles_1_B23, QL1 2023-24, 38-45 pts/m². Check the line rather than assume. The LiDAR is the frame; every other input is fitted to it and its shift recorded before use (D13).

## The decision points

| # | Question | QL2 (2018) | QL1 or phone scan | Default | In person? |
|---|---|---|---|---|---|
| D1 | Each out-of-play cell: mown, dry grass, scrub, soil or rock? | Brush mask only | Dry grass vs mown resolves; colour and species don't | Scrub on grey-brown litter at 70-80% on slopes >15° outside the corridor; no shrubs on turf | **Yes**: colour, species, soil share |
| D2 | Where does the mown corridor end? | Satellite only | Satellite only | The satellite edge | Where the satellite is old or dry |
| D3 | Steep cells: rock or scrub? | Slope only | Face shape resolves; bare rock still from imagery | Scrub; rock only where pale and >35° | **Yes**: rock extent and colour |
| D4 | Where is the sand edge? | Satellite wins over OSM | Depression edge to ~0.3 m | Satellite sand edge | No if the satellite is recent |
| D5 | Bunker depth and face | Under-read; rule depths (greenside 0.8-1.2 m, fairway 0.4-0.7 m, LiDAR depth +0.3 m minimum) | Within ~10 cm | Rule depths, steep green-side face, sand flashed up the far face | Yes on QL2; photo check on QL1 |
| D6 | Grass inside sand: island, nose, nothing? | Not in data | Not in data | None | **Yes** |
| D7 | Which surface is the green built from? | LiDAR unless a scan fits (MAD ≤5 cm, ≥80%, no doubling) | QL1 gives tiers; a phone LAS still wins | LiDAR with the slope report; pins off >3% | Scan visit per green |
| D8 | Green outline vs LiDAR pad | Shift fit + satellite tone fit; disagree → satellite for outline, LiDAR for banks | Pad edge to ~0.3 m | Satellite outline, LiDAR surround | No |
| D9 | Collar, cut step, surround profile | Not in data | Not in data | Collar 1.2 m at 1.5 cm, fairway height 3 m sides / 5 m back, deep rough 6 m off | **Yes** |
| D10 | Go beyond the LiDAR around a green? | Only where a photo shows a bank/lip; greens never | Rarely needed | The LiDAR | Photo check |
| D11 | Water level and extent | Reliable (lowest flat level around the no-return gap) | Reliable | LiDAR level and outline; nothing may cover water | No |
| D12 | Creek: channel or sheet? | Mostly not in data | Channel resolves if >0.5 m | 1.5 m channel 0.4 m deep, rock banks, water 4 cm above bed | Yes on QL2 |
| D13 | Inputs registered to the LiDAR? | Always fit and record (OSM 1.5-3 m off, phone scans 1.5 m + 2-4 m vertical, photo GPS 3-80 m) | Same | No input used until its shift is recorded | No |
| D14 | Props and buildings: where really? | Roofs only | Roofs, enclosures >1 m, poles | Not built until it has a source (satellite, roof block, solved photo) | **Yes** for anything <1 m, every door, sign, bin |
| D15 | How far does real ground go? | Fetch DEM to the horizon, OSM roads/buildings | Same | Real DEM, OSM roads, no invented hills | No; Street View fills roads |
| D16 | Lower surface covering a higher one? | Build check: water > bunker > green > collar > tee > fairway > rough, zero violations | Same | The clip order | No |
| D17 | Which flat is the tee, how many decks? | Flats ≥40 m² within 60 m of an OSM tee resolve; match to card colours by yardage | Flats and steps resolve | LiDAR flat nearest the card yardage; unused pads become mounds | Yes for which colour is on which deck |
| D18 | Vegetation return: tree, shrub, hedge, building? | <3 m and wider than tall = shrub; >3 m = tree, species blank | Crown shape resolves; species doesn't | Oak <16 m, eucalyptus over, species blank until a photo names it | **Yes** for every tree within 35 m of a tee or green |

Rule for the pipeline: a QL1 course skips the data-side guesses in D4, D5, D8, D12 and D17 and still gets the in-person list (D1, D3, D6, D9, D14, D18, plus a scan per green for D7). Photos win over the LiDAR on bunker faces, banks and lips; the LiDAR wins on green surfaces, water levels and fairway relief; a clean scan wins over both.

## Dated drone or online video as a third source

Video settles different points from the LiDAR and the satellite, and it is usually available before any visit (Moorpark had a spring drone video over the 2018 QL2 survey).

- **Settles**: D2 (mow corridor, toe lines), D4 (which bunkers exist and today's sand edge), D11 (pond extent and season), D12 (dry wash vs creek: Moorpark's Creekside "creeks" were washes in both satellite and video), D14 (roofs and props over 1 m), part of D18 (crown colour, rough species class), anything rebuilt since the survey year, and a rough D5 face read from oblique passes.
- **Does not settle**: heights or contours (no scale without an overlapped nadir survey and ground control), D9 collars and cut steps, props under 1 m, species at ground level near tees and greens, and colour (the season is usually wrong: the Moorpark video was spring, the build is dormant).
- **How to use it**: one frame per hole per pass, registered to the satellite by homography on paths and bunker outlines (not GPS), filed under `course_photos/drone/by_hole/<date>/`, treated as a dated overlay in `decisions.csv` (source = video, with the date), never as a height source. A `unity_vs_drone` side-by-side per hole is the cheapest first gate a course gets.
- **Flying it ourselves**: 60-80 m nadir at 75% overlap plus one low oblique lap per green complex gives a 3-5 cm photogrammetry surface that beats QL2 on bunkers and tee decks; greens still need the phone LAS.

## What the LiDAR gets right (keep as checks)

Fairway relief (meshes keep 95-100% of the undulation, RMS 1.3-3.1 cm in landing zones; never smooth fairways for looks), green tiers and false fronts, tee pads (3-14 cm vs scans), water levels, roof blocks, rangefinder geometry (it is the card that runs long).

## Procedure

1. Read the survey line; fetch the extent in 14; run the D13 registration on a cart path, a roof and a pond edge; record shifts.
2. Run D1-D18 per hole from the data (`decisions.csv`), marking `default` where nothing answers.
3. Set the course's `visit: planned | none` flag with BoJa (one question, once).
4. Build everything the data settles to gate quality; build the open points to their defaults **once** and leave them: on a planned-visit course they are finished after the visit and index (16, 17); on a no-visit course the holes are marked "data-only" in the README with the open points listed, and no polish passes are spent on them. Dated online photos or videos may close a slot when they clearly show the feature; colour is never settled that way.
5. After the visit, fill the photo slots, close the points, and run the per-hole gates (00) on the finished holes.

## Pitfalls (the ranked Tierra misreads)

Hillsides as mown turf (4 course-wide passes); bunkers too shallow with flat faces; greens built from 2018 LiDAR then rescanned; the 14 pond plane 0.47 m above the LiDAR water with a slab hiding a third of the pond; green meshes 1.5-3 m off the pad; outer ground left as old drape and a highway read as a wash; cliffs on the wrong cells (mask rows flipped); rough drawn over collars; tee pads mapped as greens or sunk as craters; 2,714 shrubs planted as trees; the 13 creek as a flat sheet; props placed by eye 10-40 m off; collars assumed from nothing.

## Decision points for BoJa

1. Which course is next and whether its survey line is confirmed.
2. Which points may stay on `default` for a first playable version vs must be closed before any gate (default: D7 and D13 must be closed; the rest may be default until the visit).

## Sources

`playbook/LIDAR_DETAIL_REVIEW.md` (branch `claude/project-thread-n5ggr9`, sections 1-6), `tierra-rejada-lidar/README.md` and `greens/README.md` (survey facts), `progress/tierra/scan_review_las/`, `BOJA_FEEDBACK_CHECKLIST.md`.
