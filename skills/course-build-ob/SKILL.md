---
name: "course-build-ob"
description: "Use when drafting or fixing the GKD boundary, interior OB, penalty areas, drop zones or Greenkeeper limits for a GSPro course."
---

Rules file: `rules/ob.json` (every number below is defined there; read it with `tools/rules.py ob`). Course overrides: `course.json` → `rule_overrides.ob`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/gkd_tools.py check`: hazard count and pOOB points against `ob.limits`, every DZ hazard with `innerOOB` ticked flagged (re-hit only), Designer field; exit 1 on a failure.
- `python ../course-build/tools/coursekit.py card --ob [--loops ...] [--habitat-penalty --drop-holes]`: drafts pOOB and interior OB from brush, roads, homes; rules `ob.perimeter`, `ob.interior`, `ob.clean`.
- `python ob_preview.py` (per course, copy in `tools/ob_preview.py`): red boundary and filled areas on the satellite for the hole list.
- (no tool yet: every tee/pin/aim/DZ in bounds and on grass by raycast, OB within `ob.clean.min_from_play_m` of play, DZ `ob.drop_zone.min_from_water_m`; the Tierra `gkd_apply_dz_bands.py` splice stays per course; candidates for `gkd_tools.py`)

# OB, hazards and drop zones (GKD)

Use with `course-build-gates`, `course-build-water` and `course-build-tees`. Full guide: `course-build/playbooks/10_ob_hazards_and_drop_zones.md` in the moorpark repo.

## Defaults that worked

- **In bounds**: turf, cart paths and ~10 m of natural ground beside play; homes, yards and roads are always OB.
- **Perimeter line**: rays sideways from the tee→aim→green line every 2 m, stopping at dense brush (≥400 m²), roads, parking, buildings, farmland, housing; max 40 m on OB sides, 55 m elsewhere; greens get 25 m; cart-path sides stop 3 m past the path.
- **Interior OB**: brush islands ≥400 m² (≥60 m² where deep brush is OB) between holes, 2 m off rough/fairway/tee/green/bunker/water/paths, cut open 6 m around any tee or pin.
- **Clean lines**: merge areas, close corridors <8 m, drop slivers <5 m, simplify ~2.5 m; ≥8 m from any fairway, tee or green; few clean white lines on the GSPro map.
- **Greenkeeper limits**: pOOB ≤ ~880 points (1,024 buffer); ≤120 hazards (128 max; keep interior OB within 120 m of the layout's holes) or Greenkeeper hangs at 90%.
- **Drop zones**: hazard with DZ = `innerOOB=false`, `noAIL=true` (canyons), `freeDrop=false`, `hasDZ=true`. With innerOOB ticked GSPro only offers a re-hit.
- **OB past the perimeter with a DZ**: the perimeter can't give a DZ, so grow the line by a band (≤50 m) where a ±40° tee-shot fan crosses it and make the band a DZ hazard; keep other holes' fans, tees (25 m) and greens (50 m) out.
- **Real OB** stays stroke and distance (no DZ unless BoJa says).
- **Cross-hole misses**: a big miss never lands in play on another hole: brush + OB or a penalty area; list the spots (`cross_hole_spots.csv`).
- **Water**: ponds = hazards with DZ on dry ground; rivers/canals not hazards; washes = interior OB with DZ where they cross play. Protected habitat = red penalty areas (`card --habitat-penalty`).
- **GKD fields**: Designer "jboshers"; altitude in feet; CourseInfo with the approved description; save once from Greenkeeper so coursedetails.txt and scorecards get written.

## Procedure

1. `coursekit.py card --ob` drafts pOOB and interior OB; `fix_ob.py` / `gspro_courses.py` simplify, drop slivers, cap hazards; `apply_checklist.py` fixes stacked tees, par-3 aims, tees in OB, DZ flags.
2. Check every tee, pin, aim point and DZ in bounds and on rough/fairway (raycast to ground).
3. Preview (`ob_preview.py`: red boundary, filled areas) on the satellite; send with the hole list before Greenkeeper.
4. Later edits as text splices of pOOB/Hazards/hazardCount only (`gkd_apply_dz_bands.py`), refusing if those changed since the patch; "already applied" on a rerun.
5. Test in GSPro: hit into each DZ hazard and confirm the drop is offered.

## Checks

- No tee/pin/aim/DZ in OB or outside the boundary; DZs ≥6 m from water, on grass.
- No OB within 8 m of a fairway, tee or green edge.
- Hazard count and pOOB length under the limits for every layout (nines included).
- After any OB edit: re-check tee pads and the 9-hole derivations.

## Pitfalls

- DZ hazards with innerOOB ticked gave re-hits only.
- 9-hole GKDs made from 18-hole ones must keep 18 holes with the back nine disabled, or the handicap index check fails.
- GSPro.db caches the old course entry: fix name/author/rating in the GKD and re-save from Greenkeeper.
- Interior woods/knolls: don't guess OB vs penalty vs in play; ask with a crop.
- Thin OB slivers along cart paths and around the clubhouse clutter the map.

## Decision points (ask once, with a default)

**Needs an in-person look:** OB vs penalty vs in play for every interior wood, knoll, wash and hillside; where the real fence line runs; which tees get a drop zone. None of this is in any scan.

1. Each interior wood/knoll/wash/hillside: OB, penalty with DZ, or in play. Ask with the preview, once.
2. Which tees get "OB off the tee drops in the DZ" vs re-hit.
3. Red vs yellow penalty areas when the card doesn't say.
4. Ratings/slopes source and which tee gets which rating slot.
