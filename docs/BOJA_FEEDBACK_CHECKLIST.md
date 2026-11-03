# BoJa's course feedback: apply to every course before review

This is a running record of what BoJa has asked us to fix. Each new course (Penmar, Harding, Wilson, Hansen Dam and later ones) must pass every item **before** BoJa sees a flyover. Add new feedback here the same day it comes in, with the course it came from.

Status key: [auto] = a tool already does it, [check] = verify by hand or with a script.

## Setup and process
- [check] PC load: at most one interactive Unity editor besides the one BoJa is using. Run builds for courses BoJa isn't viewing in batch mode. Never run heavy jobs (canopy refit, OPCD, 4096 imports) while BoJa is reviewing. Four open editors froze the PC on 10/28.
- [check] Each course gets its own Unity project (C:\GSP\<Name>_Project). Never build inside another course's project.
- [check] Make the Flyover tool read the project's own GKD, so it doesn't depend on the GSPro Courses folder. (Harding, 10/28)
- [check] Leave no compile errors in the console. Write Windows paths in C# as @"..." or with forward slashes. (Harding, 10/28)
- [check] Any tool that rebuilds or wipes a scene (e.g. "Set Up Scene") must ask before it runs, and is never re-run on a built scene. A re-run on Harding wiped the trees and meshes, leaving only the satellite shadows. (Harding, 10/28)
- [check] Back up the scene and the GKD before each change. Back up the GKD before touching the GSPro course folder.
- [check] Keep the course name, author and rating right in GSPro (GSPro.db caches the old entry). Don't show "BoJa" as the author. Use the rating BoJa gives. (Tierra)
- [check] GKD Designer is "jboshers" on every course (BoJa, 10/28); the real architect goes in the CourseInfo description, not Designer.
- [check] Course description (GKD CourseInfo) is the approved, fact-checked text, with Location and CityTown set; course thumbnail (CourseImageFileName) is the course's own 16:9 image (GSPro centre-crops to 16:9, 480x270 in the list), never the template's.

## Terrain and backdrop
- [check] The satellite on course materials uses this course's own square size (from course.json) and is imported at 4096. A copied 3000 m constant stretched the photo 1.5x, leaving pale rough and shadows that don't sit under trees. (Penmar/Harding, 10/28)
- [check] Tools copied from another course must not keep its constants (sizes, names, paths). Grep for 3000 or Moorpark before running. (Harding/Penmar, 10/28)
- [check] Surrounding hills and backdrop are visible and kept. For Harding this is "a big appeal of this course". (Harding, 10/28)
- [check] No abrupt map or sky edge on follow-cam. Extend the terrain 1–2 km with hills plus fog. (Tierra hole 11)
- [check] No craters or sunken pads where the real ground is a mound or hill. (Tierra hole 4)

## Trees and vegetation
- [check] **Tee shot corridors are clear.** Every tee (all colours, Blue first) has a straight shot to the aim point: from the tee box front edge, 2 m either side opening ±5°, out to about 120 m; clear only crowns below 25 m that span a normal drive's height there (crowns overhanging above the flight stay). Trees stay around the tees but out of play: nudge sideways onto rough first, aim for < 20 deletions and list them with hole and tee. Check from the flyover view too. Penmar result: 40 of 534 trees, 32 nudged, 8 scaled, 0 deleted (PenmarBatch.Corridor). (Penmar, 10/28)
- [check] Bunkers are visible: no LiDAR tree crowns over the sand. Move oversized crowns off bunkers. (Penmar hole 1, 10/28)
- [check] Never trim a tree's collider without shrinking or moving the visible tree to match. Nudge it (up to about 12 m), scale it to 0.8–0.9, and delete only as a last resort. (Penmar, 10/28)
- [check] Include known landmark and Easter-egg props from the README (e.g. Penmar's PT-22), out of play. (Penmar, 10/28)
- [check] Map every bunker in the satellite, including ones missing from OSM. (Penmar hole 1 right, 10/28)
- [check] SpeedTree desktop trees keep their leaves as submeshes. Fit canopy colliders to the leaf submeshes, not the whole mesh bounds. (Harding, 10/28)
- [check] 3D trees stand where the satellite shows trees and shadows, with no shadows sitting on bare fairway. Check alignment by overlaying tree positions on the satellite. (Harding, 10/28)
- [check] Trees must not look flat or 2D in the flyover. Use LOD and billboard distances like Tierra's tree settings. (Tierra, Harding)
- [check] Canopy colliders stay within the visible leaves (spheres at about 80% of the crown), with no invisible branches. Run a shot sweep from every tee. (Tierra holes 15 and 18)
- [check] Remove phantom or stray bushes from tee corridors. (Tierra hole 18)
- [check] Hillsides get native brush that stops the ball (deep-rough physics), not bare dirt. (Tierra holes 4 and 8)
- [check] Ball must not pass through nets, fences or bushes. Nets and fences get solid colliders, not leaf physics. (Tierra range net)
- [check] Keep every LiDAR tree in play, including brown or dead-looking ones (cypress, winter colour): don't drop trees on a satellite green check. Trees should block balls like the real ones. (Penmar, 10/28)

## Surfaces and physics
- [check] Rough colour: if the satellite is pale or summer-dry, tint the turf greener inside the boundary so the rough doesn't read grey-white. (Penmar, 10/28)
- [check] Bunkers are GSPro sand ("sand"/"Bunk" in the physics material name). Shallow by size (0.25/0.35/0.5 m), flat floor, steep face on the green side. Sand is warm tan, not bright white. (Tierra)
- [check] Green surrounds set per hole: fairway-height collar of 3 m sides and about 5 m back on flat surrounds; rough close in on uphill banks behind greens; deep rough at least 6 m off greens. (Tierra 6, 8, 15, 18)
- [check] Greens: recommended stimp about 10, firmness Normal. No extreme slopes; flag greens steeper than 3% for review. (Tierra)
- [check] Fairway firmness: moderate. Don't jump it to max; big bounces were wrong. (Tierra)
- [check] Rough shouldn't grab or roll unrealistically on hillsides. (Tierra hole 1)
- [check] Cart paths: right colour (not bright white concrete), lifted at least 4 cm so rough doesn't poke through, and placed right. Harding's paths were in the right place first time. (Tierra; Harding 10/28)
- [check] No jagged or sawtooth fairway patches. Patch edges must be smooth. (Tierra holes 16 and 2) Photo-traced fairways also came out blotchy on Harding: run griffith-park/scripts/smooth_fairways.py (keeps OSM fairways, joins traced pieces across tree shadows, smooths edges) before surrounds/collars. (Harding, 10/28)
- [check] Where the real rough is penal, it must play that way: a short first cut (about 4 m) of rough next to fairways, greens and tees, deep-rough physics beyond it and under tree lines. Fairways play clean. (Penmar, 10/28)

## Tees, yardage and aim
- [check] Every tee, including Alt/back-nine sets on repeated nines, sits on a real tee box, not on fairway or deep rough. (Penmar, 10/28)
- [check] Every tee colour sits on its own real box and is never stacked on another colour. Yardages are checked against the scorecard in yards, not metres. (Tierra)
- [check] **Tierra tees and the club card** (BoJa, Nov 1): Black = Professional (stays on the real back tees; the card runs ~3% long, the GPS pins show it), Grey/Green = Championship, Blue = Tournament (already right), White = Players, Gold = Forward. Grey shares the Black box except on 5 and 8, set 5-10 yd in front of Black on that box so the totals come close to the card. White moves up only onto existing forward boxes where it runs long against the card. Never create new tee locations. BoJa's on-site GPS pins win over the card. When unsure, keep Blue and White back.
- [check] Back tees sit where they really are (Tierra hole 5 Black: up the hill, out of sight of the path and White tees, not in a bush).
- [check] Default aim follows the real line. Par 3 and short-hole tees aim at the green (front edge where BoJa says so), not a "smart" point. (Tierra hole 6)
- [check] Tee pads are never inside OB or hazard areas. Re-check after any OB edit. (Tierra hole 5, 10/28)
- [check] Yardages come from the scorecard, not the tee plaques or signs, even when they disagree. (Penmar hole 7, 10/28)

## OB, hazards and drop zones
- [check] Ask BoJa before deciding whether interior woods or knolls are OB, a penalty area or in play. Don't guess. (Harding knoll, 10/28)
- [check] OB follows real fences and tree lines, with fences built where they exist. (Tierra hole 5)
- [check] A big miss can never land in play on another hole. Use brush plus OB or a penalty area. List the cross-hole spots. (Tierra hole 8)
- [check] OB lines on the GSPro map must be few and clean: merge areas, simplify, drop slivers, no thick white clutter. (Tierra)
- [check] **Drop zones work.** Any hazard area with a drop zone must have innerOOB=false (otherwise GSPro only offers a re-hit). Use noAIL=true for canyons and freeDrop=false. Players should get drop-zone, lateral or flag-line drops instead of a re-hit. Real OB stays stroke and distance. (Tierra, 10/28)
- [check] Fences: get the real height and type from photos (Penmar perimeter is 12 ft chain link with a mid rail, not 6 ft or split rail). Leave temporary construction fencing out. (Penmar, 10/28)

## Clubhouse and props
- [check] Buildings match the photos and satellite in colour and form. No black windows; no white walls or red roofs unless they're real. (Tierra clubhouse)
- [check] Buildings hidden under trees (restrooms, sheds) still get built: use BoJa's pin and look for the roof under the canopy in the LiDAR. (Penmar restroom, 10/28)
- [check] Local landmarks and stories are welcome as props when BoJa mentions them (Penmar: Harrison Ford's 1942 PT-22 between the hole 8 tees). (Penmar, 10/28)

## Data-side helpers (no new feedback, just where the checks are automated)
- Green collars, deep rough 6 m off greens and a per-bunker depth/face plan (`bunkers.csv`) come out of `coursekit.py svg` in griffith-wilson/scripts/coursekit.py (flags --collar, --collar-back, --deep-off-green). It also keeps rivers (OSM water=river/canal) out of the water hazard layer. (Wilson, 10/28)
- griffith-wilson/scripts/apply_checklist.py fixes a coursekit GKD for: stacked tee colours, par-3 aims, tees in OB/hazards, drop zones with innerOOB, course name/designer/location, altitude. It also writes green_slopes.csv to flag greens over 3 %. (Wilson, 10/28)
- griffith-park/scripts/fix_tees.py moves any tee more than 7 m off its card yardage along the line of play, and sets Tierra's approved GKD defaults (stimp/firmness, Parkland not Mountain). (Harding, 10/28)
- Hansen Dam's coursekit (moorpark branch claude/project-thread-f78r3v) does the same in core: `svg` collars 3 m / 5 m back with uphill banks left as rough and deep rough 6 m off greens, and drops natural water away from the holes; `card` never stacks tee colours (a short back tee moves back along the hole line to its card yardage) and drops sliver OB patches. Watch out: griffith-park/scripts/surrounds.py hard-codes `S = 2000`, so set it from course.json before running it on any other square size. (Hansen Dam, 10/28)
