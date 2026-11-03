# 05. Tees

Use this for tee boxes, pads, tee colours and slots, yardages, aim points, tee surrounds and tee markers.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Source of yardage | The scorecard, in yards, not plaques or signs; keep true distances when the card runs long (Tierra ~3% long) | Penmar 7, Tierra |
| Tee slots | Map card colours to GKD slots once per course (Tierra: Grey → Green slot, Gold → Yellow); unused slots `Enabled: false` or GSPro shows "500 yds par 0" | HANDOFF |
| One colour per box | Never stack colours; two colours on one mapped box go 3 m apart, 1.5 m inside the edge (`fix_box_tees.py`); a short back tee moves back along the hole line to its card yardage | Wilson, Hansen |
| Pad for unmapped tees | 9×7 m (long side to the green), 2 m first cut, terrain levelled under it; tee heights = levelled tee-shape mean | Harding/Hansen/Angeles |
| Pad placement | Fully inside fairway with a closed ring, or clear of the fairway edge; never straddling it (OPCD fills notches with fairway) | Wilson/Hansen |
| Clearances | Pads ≥2 m from cart paths (slide the tee, not the path); never on a path; never in OB/hazard; no merge with a mapped box | Hansen 5 Yellow, Tierra 5 |
| Tee as built | Raised, levelled pad with a short bank, not painted on the slope; clean, even turf; fine cut running to the crests and path edge | Hansen 10/29, Tierra 16 |
| Tee corridor | Clear straight shot from the box front, 2 m either side opening ±5°, to ~120 m, for crowns <25 m at drive height; nudge trees sideways first, scale 0.8-0.9, delete last (<20 per course, listed) | Penmar |
| Tee-line clear zone | 60 m × 4 m with no shrubs or trunks | Wilson round 2 |
| Aim points | Widest part of the fairway on the driver line (~250 carry from 6,000-yd tees), lay-up on par 5s; par 3s and short holes aim at the green (front edge where BoJa says) | HANDOFF, Tierra 6 |
| Tee markers | GSPro draws its own; replace via `CourseOverrides` prefabs (Black/Green/Blue/White/Yellow, TeeMarkerWidth 5 m); one TR-logo marker for all holes, later | tee_markers_1029 |
| Back tees | Where they really are, even up a hill and out of sight (Tierra 5 Black: a cut pad above the path, 376 yd) | 10/28 |

## Procedure

1. `coursekit.py card` from `scorecard.csv`: tees onto mapped OSM boxes; forward tees without a box on the hole line at the card yardage, listed for pinning (`tee_pins.csv`).
2. `fix_tees.py` (tee >7 m off card yardage slides along the line of play), `fix_box_tees.py` (shared boxes), `teepads.py`/`tee_pads_harding.py` (pads + first cut + levelled heightmap), `clear_pads.py`, `pads_off_fairway_edge.py`, `box_tees_inward.py`; then `svg_check.py`.
3. `apply_checklist.py`: unstacked colours, par-3 aims, tees out of OB/hazards, course name/designer/location, altitude in feet.
4. Unity: every GKD tee on a Tee mesh (collider, visible mesh and GKD agree; Tierra 13 Green tee was 12 m in the air); clear shrubs and trees at every tee (`MoorparkClearTees`, `clear_tees_shrubs.py`); corridor pass.
5. Photos: pose the tee photos from the tee markers in frame, not GPS (Tierra 10 Blue); confirm the real box for every colour on the visit (Tierra 17 Gold was under the pepper beyond the path loop, not on the slope).

## Checks

- Every colour on its own real box, yardage within a few yards of the card; a table per course (`tee_table.txt`, `tee_colours.csv`).
- Flyover first frame from every tee is clear of trees (move the camera, never the tree).
- No pad seams, dirt stripes or bare strips on or beside tees (12, 17); surrounds repainted with tee/surround turf.
- No stacked tees, no tee on fairway/rough/deep rough/path/OB after any OB or path edit.
- Tee surface tone close to fairway (10: tee b/g 0.52 vs fairway 0.37 was flagged).

## Pitfalls

- OSM had Tierra 5 Black's real box mapped as a small green; a tee box 45 m from any box is a question, not a fact.
- Temporary move-ups on visit day (Tierra 18 White) don't move tees.
- A fake tee box on a brush hill (Tierra 3 Gold) became rough once the real forward tee was found.
- "Smart" aim points on par 3s point into bunkers; aim at the green.
- An unused tee pad sunk into a hillside read as a crater (Tierra 4).

## Standing rule (BoJa, 10/31)

Black and Grey/Green tees share the same box on every hole unless BoJa says otherwise; on Tierra the exceptions are holes 5 and 8. On a shared box set Grey a couple of yards in front of Black so the markers don't overlap. Don't push forward tees up too far ("you are moving everything up too much"): a tee moves only when a pin, a reading or a photo puts it on another box. Per-hole table: `course-build/tonight_1031/flyover/tee_yardages_1031.md` (project files).

## Decision points for BoJa

**Needs an in-person look:** which colour plays from which box, and any tee more than ~15 m from a mapped box. QL1 data settles which flat is the tee and the deck count (D17); on QL2 courses (Tierra, Hansen, Moorpark) confirm the decks from photos.

LiDAR review decision point for tees: **D17** (which flat is the tee, how many decks) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Which tee slot each card colour uses (once per course).
2. Any tee more than ~15 m from a mapped box, or a hole with only one box mapped: pin the real spots.
3. Par-3 aim at centre or front edge.
4. Tee marker style (GSPro's or the course logo) once for the course.

## Sources

`HANDOFF.md`; `progress/tierra/tee_markers_1029/README.md`; `unity_automation/MORNING_SUMMARY_2026-10-27.md`, `SUMMARY_2026-10-27_morning_round.md`; Hansen/Harding/Wilson `scripts/`; checklist "Tees, yardage and aim".
