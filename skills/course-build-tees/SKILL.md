---
name: "course-build-tees"
description: "Use when placing tees, tee pads, tee colours and slots, yardages, aim points or tee markers in a GSPro course build."
---

Rules file: `rules/tees.json` (every number below is defined there; read it with `tools/rules.py tees`). Course overrides: `course.json` → `rule_overrides.tees`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/gkd_tools.py check`: every enabled tee has a position, unused slots disabled, pins per hole; rules `tees.gkd`.
- `python ../course-build/tools/gkd_tools.py distances [--tol 2] [--write]`: tee Distance recomputed tee -> aims -> green; rewrites only those off by more than `--tol` m, backup first.
- `python ../course-build/tools/gkd_tools.py totals [--write]`: TeeTypeTotalDistance per colour.
- `python ../course-build/tools/flyover_gate.py --out progress/flyover_gate [--end-fit]`: first frame from every tee clear of crowns, sign in frame; rules `flyovers.first_frame`, `flyovers.path`.
- (no tool yet: shared Black/Grey box check against `course.json shared_tee_box_exceptions`, card yardage vs GKD table, tee on a Tee mesh in Unity; `fix_tees.py` / `fix_box_tees.py` / `teepads.py` live per course; candidates for `tools/`)

# Course build: tees

Follow the standing rules in `course-build-gates`. LiDAR review decision point D17 (which flat is the tee, how many decks) in `playbook/LIDAR_DETAIL_REVIEW.md` (moorpark repo). Full guide: `course-build/playbooks/05_tees.md`.

## Standing rule (BoJa, 10/31)

Black and Grey/Green tees share the same box on every hole unless BoJa says otherwise; on Tierra the exceptions are holes 5 and 8. On a shared box set Grey a couple of yards in front of Black so the markers don't overlap. Don't push forward tees up too far ("you are moving everything up too much"): a tee moves only when a pin, a rangefinder reading or a photo puts it on another box. Per-hole table: `course-build/tonight_1031/flyover/tee_yardages_1031.md` (project files).

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
5. Photos: pose tee photos from the tee markers in frame, not GPS; confirm the real box for every colour on the visit (Tierra 17 Gold was under the pepper beyond the path loop).

## Checks

- Every colour on its own real box, yardage within a few yards of the card; a table per course.
- Black and Grey on the same box (except the listed holes), Grey a couple of yards ahead; markers not overlapping.
- Flyover first frame from every tee clear of trees (move the camera, never the tree).
- No pad seams, dirt stripes or bare strips on or beside tees; surrounds repainted with tee/surround turf.
- No stacked tees; no tee on fairway/rough/path/OB after any OB or path edit.
- Tee tone close to fairway.

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
