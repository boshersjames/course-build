# Course build playbooks

One guide per course element, written from what it took to get Tierra Rejada right (Oct 21-31, 2026), plus the Penmar, Harding, Wilson, Hansen Dam, Angeles National and Moorpark passes that reused those lessons. The point is to spend fewer tokens and fewer trial rounds on the next course: every guide carries the defaults that worked, the checks that catch the usual defects, the pitfalls we already paid for, and the questions that only BoJa can answer (ask those once, up front, with a default).

Each file is written so it can be turned into an account skill later: the first lines say when to use it, then defaults, procedure, checks, pitfalls, decision points, and sources.

| # | Playbook | Use it when |
|---|---|---|
| 00 | [Process and gates](00_process_and_gates.md) | Starting any hole or course; before showing BoJa anything |
| 01 | [Terrain, LiDAR and scans](01_terrain_lidar_and_scans.md) | Building the heightmap, deciding what the LiDAR can and can't tell you, applying phone scans |
| 02 | [Greens and collars](02_greens_and_collars.md) | Green surfaces, edge profile, collars, pins, stimp |
| 03 | [Bunkers](03_bunkers.md) | Any bunker: depth, lips, faces, sand, islands, noses |
| 04 | [Fairways, rough and first cut](04_fairways_rough_and_first_cut.md) | Fairway outlines and undulation, first cut, deep rough, turf tone |
| 05 | [Tees](05_tees.md) | Tee boxes, pads, colours, yardages, aim points |
| 06 | [Trees](06_trees.md) | LiDAR trees, species, colliders, corridors, hero trees, new models |
| 07 | [Brush, hillsides and scrub](07_brush_hillsides_and_scrub.md) | Native hillsides, scrub density and colour, rock faces, the dirt toe |
| 08 | [Water, creeks and ponds](08_water_creeks_and_ponds.md) | Lakes, ponds, streams, waterfalls, washes, reeds |
| 09 | [Cart paths, roads and fences](09_cart_paths_roads_and_fences.md) | Paths, curbs, roads, entrances, fences, ropes, nets |
| 10 | [OB, hazards and drop zones](10_ob_hazards_and_drop_zones.md) | GKD boundary, interior OB, penalty areas, drop zones, Greenkeeper limits |
| 11 | [Props, signs and buildings](11_props_signs_and_buildings.md) | Hole signs, bins, clubhouse, restrooms, markers, landmarks |
| 12 | [Materials, colour and look](12_materials_colour_and_look.md) | Turf tints, shared vs hole-only materials, sand colour, post-processing |
| 13 | [Flyovers and review renders](13_flyovers_and_review_renders.md) | Photo-pose renders, gate sheets, flyover camera rules |
| 14 | [Data pipeline (coursekit)](14_data_pipeline_coursekit.md) | Frame, OSM, LiDAR fetch, masks, SVG, GKD generation before Unity |
| 15 | [SpeedTree and tree models](15_speedtree_and_tree_models.md) | Hero trees, SpeedTree assets and licensing, FBX import route, new species models, perf budget |
| 16 | [Site visit capture](16_site_visit_capture.md) | What to photograph, scan and rangefind on the walk so one visit closes every in-person item |
| 17 | [Photo index and audit](17_photo_index_and_audit.md) | Indexing a phone dump by hole and spot, pose solving, the verdict-per-photo audit |
| 18 | [Backdrop and surroundings](18_backdrop_and_surroundings.md) | Far terrain, roads, buildings, parking, off-site trees, fog and far clip |
| 19 | [Unity tool inventory](19_unity_tool_inventory.md) | The editor scripts and batch jobs that already exist, their menus and inputs |
| 20 | [LiDAR detail review](20_lidar_detail_review.md) | D1-D18 as a pre-build pass per course, with the visit / no-visit split |
| 21 | [Publish to GSPro](21_publish_to_gspro.md) | Local install, Greenkeeper save, rollback, playtest, and the OPCD beta-submission gate |

## Rules that apply to every element

These came up on more than one element and more than one course. They are repeated in the guides only where the detail differs.

1. **Never invent ground.** Everything visible comes from the satellite, the LiDAR, a clean scan or BoJa's photos. Where a photo and the LiDAR disagree on a *shape*, the photo wins (BoJa, 10/31), except that green surfaces and contours never change without a scan.
2. **No defects on in-play turf.** Tees, fairways, greens, collars and surrounds are built clean, even and healthy even when a photo shows wear, divots or dry patches. Bare soil and wear go only on out-of-play hillsides and banks, or where BoJa asked (the 17 dirt path).
3. **No plants on mown turf.** Shrubs, grass cards, tufts and detail grass stay off tees, fairways, greens, collars, sand and cart paths. Run the plants-on-turf check after every planting pass.
4. **No islands in bunkers.** The only exception is a grass nose joined to the bank (Tierra 11 greenside), approved by BoJa for that bunker.
5. **Fences are moved, never deleted, and always continuous.** Mulch never sits on concrete. Cart paths are one shared concrete. Bins only where a photo shows one.
6. **Shared materials stay shared.** Fairway, tee, sand and concrete materials are not changed per hole. A per-hole look is a copy (`*_h17`, `*_Tierra`) assigned only to that hole's meshes, and any course-wide material pass is done once and shown on the control hole before and after.
7. **Hole 10 is the control.** Once a hole is signed off, every later course-wide pass is rendered on it before and after (the "hole 10 turf control 0.0" line in each package report). Nothing changes a signed-off hole's look without BoJa seeing the pair.
8. **Build from photos first.** Photo inventory per hole before building; photo | before | after pairs from the same pose for every edit; a verdict per photo before BoJa sees a flyover. BoJa should never be the one to find a mismatch.
9. **Ask once, with a default.** Every question to BoJa carries a default that gets built if there is no answer. Questions that change the goal (what is OB, which green is in play, where the real forward tee is) are asked before building, not after.
10. **Back up before every change** (scene, terrain, GKD), never touch `C:\GSProV1\Core\GSP\Courses\` without BoJa's go, and keep one Unity editor per role (see 00).

## Applying this to the courses in the pipeline

BoJa (10/31): once Tierra is done, apply these guides to every course in the pipeline (Penmar, Harding, Wilson, Hansen Dam, Angeles National, Moorpark, Los Robles). Some of them have newer, more detailed scans, but a scan settles *heights*, not *what a surface is*. Each guide's "Decision points" section starts with a **Needs an in-person look** line, matched to the LiDAR detail review's section 6. Never in any scan, so always a visit (walk, geotagged photos): cover colour and species (D1, D3), bunker islands and noses (D6), collar width and cut step (D9), props under 1 m plus doors, signs and bins (D14), tree species near tees and greens (D18); plus a phone LAS per green (D7). QL1 data (Angeles National, 2023-24, 38-45 pts/m²) settles sand edges (D4), bunker depth and faces (D5), green pad offsets (D8), creek channels (D12) and tee decks (D17); Tierra, Hansen Dam and Moorpark sit on the 2018 QL2 survey, where those still need photos or phone scans.

The realistic flow for every course (BoJa, 10/31): download the public data (LiDAR, satellite, OSM, scorecard, online photos and flyover videos), build everything that data supports, and treat the in-person items as either *finishing touches after a visit* or *unworked areas built to their defaults*, depending on whether BoJa will stand on the course. So each course carries a `visit: planned | none` line in its README and `decisions.csv`:

- **Visit planned**: build the in-person items (D1, D3, D6, D9, D14, D18, greens D7) to their defaults once and stop; no polish passes on them until the visit, the photos are indexed (17) and the slots are filled. The visit list (16) is written from the open points before the walk.
- **No visit**: build the same defaults, mark those holes "data-only" in the course README with the open points listed, and spend the polish passes only on what the data settles (relief, bunker position, water, tees, roads). Online photos and videos may fill a slot when they are dated and clearly show the feature; they never settle colour.

Order of work per pipeline course: 14 (data) → 20 (review, visit flag, open points) → 18 (backdrop) → 00 gates per hole with the element guides → 16 visit → 17 index → close the open points and finish.

## Related

- **LiDAR detail review** (`playbook/LIDAR_DETAIL_REVIEW.md`, from the [LiDAR detail review] thread, branch `claude/project-thread-n5ggr9`): Tierra's LiDAR misreads as 18 decision points. Each guide links its own: ground cover D1-D3 (04, 07), bunkers D4-D6 (03), greens D7-D10 (02), water D11-D12 (08), registration D13-D14 (01, 11, 13), outer ground D15 and mesh order D16 (01), tees D17 (05), trees D18 (06).
- `course-build/BOJA_FEEDBACK_CHECKLIST.md` (project files) is the running feedback log by date. These playbooks are the organised version; keep adding new feedback to the checklist the day it arrives, then fold it into the matching playbook.
- Tool code: `coursekit.py` (this repo and the per-course copies), `unity_editor/*.cs` on `claude/moorpark-unity`, the Hansen Dam and Griffith Park scripts on their thread branches.
