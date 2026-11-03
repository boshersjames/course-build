---
name: "course-build-water"
description: "Use when building lakes, ponds, streams, waterfalls, dry washes, reeds or bridges and their GKD hazards on a GSPro/OPCD course."
---

Rules file: `rules/water.json` (every number below is defined there; read it with `tools/rules.py water`). Course overrides: `course.json` → `rule_overrides.water`.

## Tools

Run from the course folder (or with `COURSE_JSON` set). Every number these enforce comes from `rules/*.json`.

- `python ../course-build/tools/gkd_tools.py check`: every water hazard's DZ flags (`innerOOB=false`); rules `ob.drop_zone`.
- `python ../course-build/tools/scan_index.py`: a pond-bank or creek scan registered to the LiDAR for channel width and bank heights; rules `water.stream`, `water.pond`.
- `water_inventory.py` (`tools/moorpark_tools/`): OSM water + satellite + drone frames into `water.json`.
- (no tool yet: dry-sample grid probe over every water mesh, join overlap `water.join_overlap_m`, monotone bed sampling at `water.stream.bed_sample_m`, hazard outline follows the water edit; candidate for `unity/shared/`)

# Water, creeks and ponds

Use with `course-build-gates` and `course-build-ob` (drop zones). Full guide: `course-build/playbooks/08_water_creeks_and_ponds.md` in the moorpark repo. LiDAR decision points: `playbook/LIDAR_DETAIL_REVIEW.md` D11-D12.

## Defaults that worked

- **Inventory**: OSM water + satellite + drone/photo frames (`water_inventory.py`, `water.json`). Rivers/canals and natural water away from the holes are not hazards.
- **Lakes/ponds**: dug to the LiDAR bed, flat water plane at the real surface height, OPCD-WaterPlane_AquasLite material + GSPwater physics; hazard outline moves with the water.
- **Water height, which rule where**: lakes and ponds sit ~5 cm below the shore, with the plane running ~2 m under the banks so no gap shows; stream and creek ribbons sit ≥4 cm above the bed or the course meshes so they aren't buried.
- **Pond banks**: shelf width = depth ÷ tan(slope), default slope ~11°, eased into the bed.
- **Shoreline wobble**: inward only, so water never pushes onto turf.
- **Pond look**: reflective (mirror) water as one shared copy (TR_PondWater_reflect), retinted per hole only where photos differ; no opaque olive/brown water, no beige rim; thin mud ring with grass to the edge.
- **Streams**: 3 m channel dug 0.35 m with a sloped surface, or water lifted 4 cm above the course meshes so it isn't buried; width to the photo (1.5-2 m by a bridge). Sample the bed every 1 m, never rising downstream; the water surface is monotone too.
- **Creek edging**: white broken concrete lip or thin grey curb only where photographed; tan sandstone boulders, thinned; no rows of potato boulders or white pebbles.
- **Waterfalls**: angular tan sandstone tiers on a boulder sill, 2-3 thin uneven falls with foam at each drop, wet faces only behind the falls, base lowered ~1 m into a pool that stays GSPro water; no identical sheets, columnar sprites or dark panels.
- **Reeds**: band 1-4.5 m from the water (`make_reed_mask.py`), reads as plants not a wall.
- **Washes**: dry sandy beds, out-of-play floor 0.5-1 m below the fairway edge, grey-brown silt, cobble edge; interior OB with a drop zone on the tee side where a wash crosses play.
- **Bridges**: railless weathered plank footbridge with sleeper steps; concrete cart crossing with 15 cm curbs; seated on grade.
- **Drop zones**: every water hazard gets a DZ on dry course ground; `innerOOB=false`.

## Procedure

1. Inventory and classify: lake/pond (hazard), stream (hazard or feature), wash (interior OB), river (none).
2. Dig and plane; water heights from the LiDAR surface; GKD hazards from the planes.
3. Photo pass: position first (Tierra 14 pond was 6-8 m short on the east), then extent, then look. Photos win over LiDAR for extent when BoJa says so.
4. Creek details from photo GPS (pump station, waterfall, steps).
5. Joins: each water mesh overlaps ≥0.5 m under the surface it feeds, with monotone heights; probe the join for dry samples.
6. Creek GKD hazard = union of the water meshes + ~0.6 m, minus the mown meshes, simplified to 0.1 m. Back up the GKD first.

## Checks

- No tee, pin, aim point or DZ in water; DZs ≥6 m from the edge and on grass.
- Water sits in a channel, not flat at grade across turf.
- Grid probe over every water mesh: 0 dry samples.
- Seams pinned, no flipped faces.
- No boxed banks or brown slabs; no lily pads hugging the shore; one mid-pond cluster at most.
- GKD hazard outline moves with any water edit; red/yellow set from the card.

## Pitfalls

- A stream buried under the course meshes rendered as grass.
- Coarse stream quads poked through the course meshes; sample the bed every 1 m.
- Sinking a creek reach opened a 6 m dry gap at the next join (Tierra 13 fill); re-probe joins after any sink.
- Plank ramps, box bridges with rails and stylised waterfalls were all rejected; build from the photo.
- A highway beside a hole was cleaned into a sandy wash; never clean real features out of the satellite.
- Washes picked as sand patches come split with grass gaps; join in Greenkeeper where it matters.
- Water objects with null materials show magenta.
- Shoreline Studio (Blender add-on) is GPL-3.0: borrow its ideas and numbers, never paste its code.

## Decision points (ask once, with a default)

**Needs an in-person look:** wash = OB or penalty area; seasonal fill when photos and LiDAR differ. Water levels (D11) are reliable in LiDAR; QL1 settles channels (D12), on QL2 courses confirm channel vs sheet from photos.

1. Washes/creeks: interior OB (default for dry washes) vs penalty with DZ (default for water).
2. Pond extent where LiDAR and photos differ (default: photos).
3. Seasonal water level.
