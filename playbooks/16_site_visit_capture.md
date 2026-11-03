# 16. Site visit capture

Use this before anyone walks a pipeline course. A course starts from LiDAR, the satellite, the scorecard and whatever photos or flyover videos are online; those settle heights and layout but never cover colour, species, collars, bunker lips, props or which tee deck is which. One visit, done to this list, answers every "needs an in-person look" item in the other guides. The output goes into the photo index folder described in 17.

## Before the visit (cloud side, one day ahead)

1. Run the LiDAR detail review (20) for the course and write the per-hole visit list: for each hole, which decision points are still open (D1, D3, D6, D9, D14, D18 on every course; D5, D12, D17 on 2018 QL2 courses) and the photo or scan each one needs.
2. Print or send a one-page sheet per hole: satellite crop with the tee boxes, green, bunkers, hazards and the numbered standing spots below marked on it.
3. Mark the greens that need a scan (all of them on a first visit), the hillsides flagged as signature views, and any prop we couldn't place from the satellite.
4. Phone: GPS and compass calibrated (figure-eight), location on for the camera, 3D Scanner app installed with LAS export, enough storage (a green scan is 200-500 MB). Rangefinder with slope on.

## What to capture per hole, in order of walking

| Spot | Capture | Settles |
|---|---|---|
| Every tee box | 1 photo down the line of play from the back of each box (markers in frame), 1 across the box showing the deck edge and bank, 1 of the hole sign square-on from 3 m; rangefinder to the pin and to the first bunker; note which colour sits on which deck today | D17 tee decks, sign style, tee markers, yardage check |
| Tee surrounds | 1 photo each side: beds, ropes, bins, benches, ball washers, steps, railings, fences | D14 props, 09 ropes/fences, 11 props |
| Hillsides and banks in view from the tee | 1 photo across each hillside at eye level, 1 along the toe where scrub meets turf; stand where the satellite sheet says | D1 cover and colour, D2 toe, D3 rock vs scrub |
| Landing zone | 1 photo back toward the tee, 1 toward the green; rangefinder to the green front edge from a known spot (a sprinkler head or path corner) | Fairway position check, flyover mid frames |
| Every bunker | 1 photo from the fairway side showing the face height against the lip, 1 from the green side, 1 across the floor showing any grass inside the sand; note raked vs rough, sand colour (hold a white card in frame once per course) | D4 edge, D5 depth and face, D6 island or nose, 03 sand colour |
| Every green | **Phone LAS scan** of the putting surface plus 3 m of collar, walked in overlapping lanes 1.5 m apart at a slow pace, ending where it started, no stopping over one spot (it stacks ground); 1 photo of the collar from the fringe, 1 low along the green edge toward each bunker, 1 from each flag position toward the tee | D7 surface, D8 outline, D9 collar width and step, D16 edge order |
| Green surrounds | 1 photo of each bank behind and beside the green, the mow pattern behind, and anything that stops a ball (fence, net, slope) | D10 banks, 04 surrounds, 10 OB |
| Water | 1 photo along each bank from a path corner, 1 across the pond; for creeks 1 along the channel showing width and bank material, 1 of each bridge and crossing | D11 edge, D12 channel, 08 bridges |
| Trees within 35 m of a tee or green | 1 photo of each from the tee side with a person or marker for scale, close enough to tell the species; note dead or missing ones | D18 species, 06 hero trees |
| Props and buildings | 1 photo of each with a landmark in frame (path, sign, building corner); doors, signs, bins, enclosures, pump stations, restrooms, gazebos; the clubhouse from each side and the cart staging | D14 placement, 11 colours and windows |
| Boundary | 1 photo along each fence line, roads and entrances visible from play, nets | 09 fences, 10 OB, 18 backdrop |
| Course-wide, once | Sky and horizon from the highest tee (skyline for pose fits, D13), the entrance drive, the range, the practice green, the scorecard and any yardage book, a photo of a pin flag and a tee marker up close | 18 backdrop, 13 flyovers, 05 markers |

Rangefinder log: hole, tee colour, target (flag, bunker front edge, path corner), reading, plays-like, pin position (front/middle/back). Tierra's readings on 10, 16, 17 and 18 confirmed the model and showed the card runs long; don't move tees to hit card numbers.

## How to shoot so the photos are usable

- Stand still 2 s before the shot so the GPS settles; phone GPS is 3-5 m, sometimes 40-80 m on the first shot after a cart ride. Each spot gets two photos 10 s apart.
- Hold the phone level, landscape, at eye height; no zoom (the FOV is what the pose solver uses).
- Keep a fixed landmark in frame where possible (sign post, path crest, bunker lip, building corner). A photo with nothing fixed in it can't be posed.
- Morning or evening light with the sun behind you for colour photos; the white card once per course in the same light as the bunkers and concrete.
- Compass heading is unreliable on a cart (50-110° off at Tierra); the solver uses content, so don't worry about it.
- Dormant season is fine and often better: that is the look we build (12).

## Scans: what makes one usable

- 3D Scanner app, LiDAR mode, LAS export with the app's own georef; one file per green named `Gxx_green.las`; tee decks and bunker complexes as separate files when time allows.
- Walk lanes, keep the phone 1-1.5 m off the ground, pointed down and slightly forward, keep moving; a doubled or stacked ground (Tierra 13, 16) comes from pausing or re-covering the same strip from a different height.
- A scan is accepted at fit MAD ≤5 cm against the LiDAR with no doubled areas over at least 80% of the green; otherwise the green is listed for a rescan. Phone altitude is off by 2-4 m and position by ~1.5 m; the fit absorbs that.
- Photo-only reconstruction of a green doesn't work (putting surfaces have no texture); scan or nothing.

## Hand-off after the visit

1. Dump the phone into `course_photos/` on the project files share (structure in 17), one folder per hole, scans into `scans/`, the rangefinder log as `rangefinder.csv`, and a `visit_notes.md` with anything you noticed that a photo can't carry (which tee was in play, pins moved, temporary works).
2. The cloud side indexes it (17), fills the `decisions.csv` slots, and the per-hole visit list is closed out: anything still open gets a second-visit line.
3. Nothing from the visit goes into the build until it is indexed and its registration to the LiDAR is recorded (D13).

## Decision points for BoJa

1. Which hillsides are signature views (one eye-level photo each is the spec).
2. Whether to scan tee decks and bunker complexes on the first visit or only greens (default: greens only, bunkers on the second visit).
3. Anything temporary on the day (construction fencing, moved markers) to leave out of the build.

## Sources

`progress/tierra/visit_2026-10-29/VISIT_NOTES.md` (rangefinder table, hole notes), `scan_apply_1029b/`, `scan_review_las/summary.json`, LiDAR detail review sections 5-6, `BOJA_FEEDBACK_CHECKLIST.md` (scans, photo-only reconstruction, "I gave you the right hillside").
