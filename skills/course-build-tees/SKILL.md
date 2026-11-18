---
name: "course-build-tees"
description: "Use when placing tees, building tee pads and banks, mapping card colours to GKD slots or clearing tee corridors in a GSPro course build."
---

# Course build: tees

Follow the standing rules in `course-build-gates`. LiDAR review decision point D17 (which flat is the tee, how many decks) in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo). Full guide: `course-build/playbooks/05_tees.md`.

## Standing rule (BoJa, 10/31)

Black and Grey/Green tees share the same box on every hole unless BoJa says otherwise; on Tierra the exceptions are holes 5 and 8. On a shared box set Grey a couple of yards in front of Black so the markers don't overlap. Don't push forward tees up too far ("you are moving everything up too much"): a tee moves only when a pin, a rangefinder reading or a photo puts it on another box. Per-hole table: `course-build/tonight_1031/flyover/tee_yardages_1031.md` (project files).

## Tee markers on shared boxes (BoJa, 11/10, new courses)

- A marker pair sits 5 m apart across the line of play, in the middle of the tee box (the connected part of the `Spline_tee_*` mesh under the GKD tee).
- Two colours on one box: longer tee in the back 20%, shorter in the front 20%; three or more spread between. This is the general rule for new courses; Tierra's markers were placed by hand after review and stay as they are (BoJa 11/11).
- Render-only helper: `C:\GSP\course-build-staging\annotate\unity\TeeMarkerRender.cs`; reusable for `CourseOverrides` placement.
- The shot test flags a tee that plays as green (Tierra 5 Black: physics GSPgreen); every GKD tee sits on a Tee mesh with tee physics.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Yardage source | The scorecard, in yards, not plaques or signs; keep true distances when the card runs long (Tierra ~3%) | Penmar 7, Tierra |
| Tee slots | Map card colours to GKD slots once per course (Tierra: Grey → Green slot, Gold → Yellow); unused slots `Enabled: false` or GSPro shows "500 yds par 0" | HANDOFF |
| One colour per box | Never stack; two colours on one mapped box go 3 m apart, 1.5 m inside the edge (`fix_box_tees.py`); a short back tee moves back along the hole line to its card yardage | Wilson, Hansen |
| Pad for unmapped tees | 9×7 m (long side to the green), 2 m first cut, terrain levelled under it; tee height = levelled tee-shape mean | Harding/Hansen/Angeles |
| Pad placement | Fully inside fairway with a closed ring, or clear of the fairway edge; never straddling it (OPCD fills notches with fairway) | Wilson/Hansen |
| Clearances | ≥2 m from cart paths (slide the tee, not the path); never on a path, in OB/hazard, or merged with a mapped box | Hansen 5, Tierra 5 |
| Tee as built | Raised, levelled pad with a short bank; clean even turf; fine cut running to the crests and path edge | Hansen, Tierra 16 |
| Tee banks | 3:1 max (ease = 3 × step, 4:1 under 0.5 m), smootherstep, outward over rough only, flat top/markers/gate untouched; where the hill is steeper than 3:1, lower the pad or meet the hill at its natural slope with a 1.5 m fillet, never a slab (Tierra 18 "yikes") | after_test_1101 |
| Tee corridor | Clear shot from the box front, 2 m either side opening ±5°, to ~120 m, for crowns <25 m at drive height; nudge trees first, scale 0.8-0.9, delete last (<20 per course, listed) | Penmar |
| Tee-line clear zone | 60 m × 4 m with no shrubs or trunks | Wilson |
| Aim points | Widest part of the fairway on the driver line (~250 carry from 6,000-yd tees), lay-up on par 5s; par 3s aim at the green (front edge where BoJa says) | Tierra 6 |
| Tee markers | GSPro draws its own; replace via `CourseOverrides` prefabs (TeeMarkerWidth 5 m); one logo marker for all holes, later | tee_markers_1029 |
| Back tees | Where they really are, even up a hill out of sight (Tierra 5 Black: a cut pad above the path) | 10/28 |

## Procedure

1. `coursekit.py card` from `scorecard.csv`: tees onto mapped OSM boxes; forward tees without a box on the hole line at the card yardage, listed for pinning (`tee_pins.csv`).
2. `fix_tees.py` (>7 m off card yardage slides along the line of play), `fix_box_tees.py`, `teepads.py` (pads + first cut + levelled heightmap), `clear_pads.py`, `pads_off_fairway_edge.py`, `box_tees_inward.py`; then `svg_check.py`.
3. `apply_checklist.py`: unstacked colours, par-3 aims, tees out of OB/hazards, name/designer/location, altitude in feet.
4. Unity: every GKD tee on a Tee mesh (collider, visible mesh and GKD agree; Tierra 13 Green tee was 12 m in the air); clear shrubs and trees at every tee; corridor pass.
5. **Photo shaping (BoJa 11/16), before the tee counts as built**, via the photo enhancement procedure (`course-build-photo-index`). Pose tee photos from the tee markers in frame, not GPS; confirm the real box for every colour (Tierra 17 Gold was under the pepper beyond the path loop). From the photos down the line, across the deck and of each side of the tee, plus BoJa's notes on that tee, set: deck shape and number of decks, bank height and steepness on each side, where brush, shrubs and trees start around the tee and on which side of the path, beds, steps and walls. The tee surface itself is always built clean, never with the divots or worn turf a photo shows. The 9×7 m pad stays as `verified unchanged` where it already matches the photos, is `open` with the pad built where the photos conflict with the measurements, and is `provisional base` where no photo shows the tee; list those tees. Bank steepness is not a photo default: no steeper than 3:1 (4:1 under 0.5 m), no walls, is BoJa's standing rule (11/01, `playbook/02_standing_rules.md`), with its steep-hill handling (natural slope and a 1.5 m fillet on the cut side; a lowered pad or a fill bank up to 2:1 on the fill side, as on Tierra 5 and 18 in `after_test_1101`). Photos set bank height, where the bank runs and where cover starts, within that rule; a photo showing a steeper bank or a wall stays `open` with one question to BoJa and the rule built meanwhile. The tee flat and deck count still come from the LiDAR (D17) or a clean scan; a photo that disagrees stays `open`. A tee whose base already matches its photos is `verified unchanged`.

## Checks

- Every colour on its own real box, yardage within a few yards of the card; a table per course.
- Black and Grey on the same box (except the listed holes), Grey a couple of yards ahead; markers not overlapping.
- Flyover first frame from every tee clear of trees (move the camera, never the tree).
- No pad seams, dirt stripes or bare strips on or beside tees; surrounds repainted with tee/surround turf.
- No stacked tees; no tee on fairway/rough/path/OB after any OB or path edit.
- Tee tone close to fairway.
- Every photographed tee and its surround matches its photos (deck, banks, where cover starts), with a verdict per photo.

## Pitfalls

- OSM mapped Tierra 5 Black's real box as a small green; a tee 45 m from any box is a question, not a fact.
- Visit-day move-ups (Tierra 18 White) don't move tees.
- A fake tee box on a brush hill became rough once the real forward tee was found.
- "Smart" aim points on par 3s point into bunkers.
- An unused tee pad sunk into a hillside read as a crater (Tierra 4).
- Rangefinder readings confirmed the model on 10, 16, 17, 18; it is the card that runs long. Don't push markers off the boxes to hit card numbers.

## Ask BoJa once (with a default)

**In person:** which colour plays from which box; any tee >15 m from a mapped box. QL1 data settles the tee flat and deck count (D17); on QL2 courses confirm decks from photos.

1. Tee slot per card colour.
2. Pins for tees far from a box or holes with one box mapped.
3. Par-3 aim: centre or front edge.
4. Tee marker style once for the course.
5. Holes where Black and Grey do not share a box (Tierra: 5 and 8).