---
name: "course-build-site-visit"
description: "Use before walking a golf course for a GSPro build: what to photograph, scan and rangefind per hole so one visit closes every in-person item."
---

# Site visit capture

A course starts from LiDAR, the satellite, the scorecard and online photos; those settle heights and layout, never colour, species, collars, bunker lips, props or which tee deck is which. One visit to this list answers every "needs an in-person look" item. Output goes into the `course_photos/` layout in `course-build-photo-index`. Full guide: `course-build/playbooks/16_site_visit_capture.md` in the moorpark repo.

## Before the visit (one day ahead)

1. Run the LiDAR review (`course-build-lidar-review`) and write the per-hole visit list: open points (D1, D3, D6, D9, D14, D18 everywhere; D5, D12, D17 on 2018 QL2 courses) and the photo or scan each needs.
2. One sheet per hole: satellite crop with tee boxes, green, bunkers, hazards and numbered standing spots.
3. Mark greens to scan (all on a first visit), signature hillsides, props we couldn't place.
4. Phone: GPS and compass calibrated, location on for the camera, 3D Scanner app with LAS export, storage (200-500 MB per green). Rangefinder with slope.

## Capture per hole, in walking order

- **Every tee box**: one photo down the line from the back of each box (markers in frame), one across the deck edge and bank, one of the hole sign square-on from 3 m; rangefinder to the pin and first bunker; note which colour is on which deck today. Settles D17, sign style, markers, yardage.
- **Tee surrounds**: each side: beds, ropes, bins, benches, ball washers, steps, railings, fences. Settles D14.
- **Hillsides in view**: one photo across each at eye level, one along the toe where scrub meets turf. Settles D1, D2, D3.
- **Landing zone**: one back to the tee, one to the green; rangefinder to the green front from a fixed spot.
- **Every bunker**: fairway side showing face height against the lip, green side, across the floor for any grass in the sand; note sand colour (white card in frame once per course). Settles D4, D5, D6.
- **Every green**: phone LAS scan of the surface plus 3 m of collar, overlapping lanes 1.5 m apart, slow, end where you started, never pausing over one spot; photo of the collar from the fringe, low along the edge toward each bunker, from each flag position toward the tee. Settles D7, D8, D9.
- **Green surrounds**: each bank behind and beside, the mow pattern behind, anything that stops a ball.
- **Water**: along each bank from a path corner, across the pond; creeks: along the channel showing width and bank material; each bridge. Settles D11, D12.
- **Trees within 35 m of a tee or green**: one each from the tee side with scale, close enough to tell species; note dead or missing. Settles D18.
- **Props and buildings**: each with a landmark in frame; doors, signs, bins, enclosures, pump stations, restrooms, gazebos; clubhouse from each side and cart staging.
- **Boundary**: along each fence line, roads and entrances seen from play, nets.
- **Once per course**: skyline from the highest tee, entrance drive, range, practice green, scorecard, a pin flag and a tee marker up close.

Rangefinder log: hole, tee, target, reading, plays-like, pin position. Readings confirm the model; the card usually runs long. Don't move tees to hit card numbers.

## Shooting so photos are usable

- Stand still 2 s before the shot; GPS is 3-5 m, 40-80 m on the first shot after a cart ride. Two photos per spot, 10 s apart.
- Level, landscape, eye height, no zoom.
- A fixed landmark in frame where possible (sign post, path crest, lip, building corner).
- Sun behind you for colour; the white card once per course in the same light as bunkers and concrete.
- Compass heading on a cart is 50-110° off; the solver uses content.
- Dormant season is fine: that is the look we build.

## Scans that fit

- 3D Scanner app, LiDAR mode, LAS export; one file per green `Gxx_green.las`; tee decks and bunker complexes separately when time allows.
- Phone 1-1.5 m off the ground, pointed down and a little forward, keep moving; doubled ground comes from pausing or re-covering a strip from another height.
- Accepted at fit MAD ≤5 cm vs LiDAR, no doubled areas, ≥80% of the green; else listed for a rescan. Phone altitude is off 2-4 m and position ~1.5 m; the fit absorbs it.
- Photo-only reconstruction of a green doesn't work; scan or nothing.

## Hand-off

1. Dump to `course_photos/unsorted/<date>/` on the share, scans to `scans/`, `rangefinder.csv`, `visit_notes.md` (tee in play, pins moved, temporary works).
2. Cloud side indexes, fills `decisions.csv`, closes the visit list; anything still open gets a second-visit line.
3. Nothing from the visit enters the build until indexed and registered to the LiDAR (D13).

## Decision points (ask once, with a default)

1. Which hillsides are signature views (one eye-level photo each is the spec).
2. Scan tee decks and bunkers on the first visit or greens only (default greens only).
3. Temporary things on the day to leave out of the build.