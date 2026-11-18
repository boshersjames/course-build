---
name: "course-build-water"
description: "Use when building lakes, ponds, streams, waterfalls, dry washes, reeds or bridges and their GKD hazards on a GSPro/OPCD course."
---

# Water, creeks and ponds

Use with `course-build-gates` and `course-build-ob` (drop zones). Full guide: `course-build/playbooks/08_water_creeks_and_ponds.md` in the moorpark repo. LiDAR decision points: `playbook/LIDAR_DETAIL_REVIEW.md` D11-D12.

## Defaults that worked

- **Inventory**: OSM water + satellite + drone/photo frames (`water_inventory.py`, `water.json`). Rivers/canals and natural water away from the holes are not hazards.
- **Lakes/ponds**: dug to the LiDAR bed, flat water plane at the real surface height, OPCD-WaterPlane_AquasLite material + GSPwater physics; hazard outline moves with the water.
- **Water height, which rule where**: lakes and ponds sit ~5 cm below the shore, with the plane running ~2 m under the banks so no gap shows; stream and creek water sits 4-10 cm above its own channel bed, inside banks that stand above it. Water is never lifted over the course meshes instead of cutting a channel (retired 11/18: it gave Tierra 13 a stream with no channel).
- **Pond banks**: shelf width = depth ÷ tan(slope), default slope ~11°, eased into the bed.
- **Shoreline wobble**: inward only, so water never pushes onto turf.
- **Pond look**: reflective (mirror) water as one shared copy (TR_PondWater_reflect), retinted per hole only where photos differ; no opaque olive/brown water, no beige rim; thin mud ring with grass to the edge.
- **Streams**: a channel cut into the ground along the evidence route, its bed below the banks (start from about 0.3-0.35 m below the survey ground, then shape each reach, pool and drop from the photos), water 4-10 cm deep over the bed; width to the photo (1.5-2 m by a bridge). The old alternative, "or water lifted 4 cm above the course meshes", is retired (BoJa 11/18): no stream is built without a channel. Sample the bed every 1 m, never rising downstream; the water surface is monotone too.
- **Creek edging**: white broken concrete lip or thin grey curb only where photographed; tan sandstone boulders, thinned; no rows of potato boulders or white pebbles.
- **Waterfalls**: angular tan sandstone tiers on a boulder sill, 2-3 thin uneven falls with foam at each drop, wet faces only behind the falls, base lowered ~1 m into a pool that stays GSPro water; no identical sheets, columnar sprites or dark panels.
- **Reeds**: band 1-4.5 m from the water (`make_reed_mask.py`), reads as plants not a wall.
- **Washes**: dry sandy beds, out-of-play floor 0.5-1 m below the fairway edge, grey-brown silt, cobble edge; interior OB with a drop zone on the tee side where a wash crosses play.
- **Bridges**: railless weathered plank footbridge with sleeper steps; concrete cart crossing with 15 cm curbs; seated on grade.
- **Drop zones**: every water hazard gets a DZ on dry course ground; `innerOOB=false`.

## Procedure

1. Inventory and classify: lake/pond (hazard), stream (hazard or feature), wash (interior OB), river (none).
2. Dig and plane; water heights from the LiDAR surface; GKD hazards from the planes.
3. **Photo enhancement (BoJa 11/16)**, before the water counts as built, via the procedure in `course-build-photo-index` with BoJa's notes: position first (Tierra 14 pond was 6-8 m short on the east), then extent, then bank slope and material per side, reed band width and where it starts and stops, edging, then look. The 11° shelf and the 1-4.5 m reed band stay as `verified unchanged` where they already match the photos, stay `open` with the provisional choice built where the photos conflict with the measurements or are uncertain, and are `provisional base` where no photo covers that bank. Photos set the extent over the LiDAR only when BoJa decides so (decision 2); LiDAR still sets the water level. Never copy faults: algae scum, trash, low-season mud flats or temporary works are left out unless BoJa asks.
4. Creek details from photo GPS (pump station, waterfall, steps).
5. Joins: each water mesh overlaps ≥0.5 m under the surface it feeds, with monotone heights; probe the join for dry samples.
6. Creek GKD hazard = union of the water meshes + ~0.6 m, minus the mown meshes, simplified to 0.1 m. Back up the GKD first.

## Water as connected features (BoJa 11/17-11/18)

Each water system (source, channel reaches, pools, drops, crossings, ponds, outlet) is one feature with a route and an elevation profile, built and checked together.

1. **Route from the evidence first.** Digitise the channel on the aerial/survey, overlay the proposed centre line, and compare it with the matched photo views (`course-build-photo-index` step 0). Record the offset per station; the tolerance comes from the evidence (Tierra 13 aerial trace: about 1 m, so mean ≤ 1 m and max ≤ 2 m). The 1117f stream was 1.8 m mean / 5.2 m max off and cut across the photographed bends.
2. **Profile and parts.** A bed below the water along the whole route (no channel means not built), banks above the water, pools level, drops where photographed, a surface that never rises downstream, and each crossing (bridge, culvert, path) with a type and its evidence. "Survey ground minus 0.3 m" is a starting estimate, not the finished channel: record estimated widths, depths, pools and drops per feature.
3. **Ponds level.** The visible water of one pond is one level surface: its upward-facing water vertices span ≤ 2 cm, with shoreline and banks defined together (Tierra 13, 14, 18 on 1117f: +0.5 to +2.2 m).
4. **Provisional parts.** Sources, outlets and hidden connections that the evidence does not establish stay `provisional` in the build record and the review, are never shown as confirmed geometry, and are not invented to make a route look complete (BoJa 11/17).
5. **One owner per water edge.** Bed, banks and the neighbouring turf share one boundary (`course-build-gates` rule 15). No stacked water strips. Sinking or moving a reach rebuilds its neighbours and the GKD hazard.

## Checks

- No tee, pin, aim point or DZ in water; DZs ≥6 m from the edge and on grass.
- Water sits in a channel, not flat at grade across turf.
- Grid probe over every water mesh: 0 dry samples.
- Seams pinned, no flipped faces.
- No boxed banks or brown slabs; no lily pads hugging the shore; one mid-pond cluster at most.
- GKD hazard outline moves with any water edit; red/yellow set from the card.
- Route offset against the evidence trace per station, inside the recorded tolerance (`skillcheck.py` ROUTE).
- Every pond level within 2 cm; every ribbon monotone with its bed below it (`TierraWaterCheck` ribbons and ponds); no point covered by two water surfaces; every source, outlet and crossing marked confirmed or provisional.

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
2. Pond extent where LiDAR and photos differ. No automatic winner: agreement between photos alone doesn't establish accurate dimensions. Record the disagreement in `enhance.csv` (offset per side, photos, poses), choose the provisional outline from pose confidence, survey quality (QL1/QL2, LiDAR capture date) and the photo capture dates, and write that reason down. The LiDAR still sets the water level, and the edge stays `open` until BoJa answers.
3. Seasonal water level.