# 08. Water, creeks and ponds

Use this for lakes, ponds, streams, waterfalls, dry washes, reeds and pond edges, and the matching GKD hazards.

## Defaults that worked

| Setting | Value | Source |
|---|---|---|
| Inventory | OSM water + satellite + drone/photo frames (`water_inventory.py`, `water.json`); rivers/canals (OSM water=river/canal) and natural water away from the holes are *not* hazards (LA River) | Wilson, Hansen |
| Lakes/ponds | Dug to the LiDAR bed, flat water plane at the real surface height, Tierra's water material (OPCD-WaterPlane_AquasLite) and GSPwater physics; hazard outline moves with the water | Moorpark water |
| Pond look | Reflective (mirror) water as one shared copy (TR_PondWater_reflect) reused per course, retinted per hole only where photos differ; no opaque olive/brown water, no beige rim; thin mud ring with grass to the edge | 13, 14, 18 |
| Streams | 3 m channel dug 0.35 m with a sloped surface, or (Tierra 13) water lifted 4 cm above the course meshes so it isn't buried; narrow to the photo (1.5-2 m by a bridge) | Moorpark, creek13 |
| Creek edging | Cream concrete lip / thin curb where photographed; tan sandstone boulders, thinned; not rows of "potato" boulders or white pebble rows | 13 |
| Waterfalls | Boulder sill, 2-3 uneven falls in two tiers with foam, base lowered ~1 m into a green-level pool; no identical thin sheets, no columnar sprites, no dark panel | 13 |
| Reeds | Pond-edge reeds and foxtail band 1-4.5 m from the water (`make_reed_mask.py`); not a vertical fence-like wall | 13 pond |
| Washes | Dry sandy creek beds: out-of-play wash floor 0.5-1 m below the fairway edge, grey-brown silt, cobble edge, litter under peppers; interior OB with a drop zone on the tee side where a wash crosses play | Moorpark, Tierra 15 |
| Bridges | Railless weathered plank footbridge with sleeper steps; concrete cart crossing with 15 cm curbs; seat on grade, no boulders through the deck | 13 |
| Drop zones | Every water hazard with a drop zone on dry course ground; innerOOB=false (see 10) | |

## Procedure

1. Inventory (`water_inventory.py`, `water_crops.py`, `wash_candidates.py`), classify: lake/pond (hazard), stream (hazard or feature), wash (interior OB), river (none).
2. Dig and plane (`MoorparkWater`, `stream_dig.py`); water heights from the LiDAR surface; GKD hazards from the planes.
3. Photo pass: position first (Tierra 14 pond was 6-8 m short on the east; green and hazard outline checked against the LiDAR pad), then extent, then look. Follow the photos over the LiDAR for the water extent when BoJa says so (14).
4. Creek details from photo GPS (13: pump station 40 m off, moved; waterfall and steps rebuilt).

## Checks

- No tee, pin, aim point or drop zone in water; drop zones ≥6 m from the water edge and on grass.
- Water sits in a channel, not flat at grade across turf (13 creek).
- No boxed banks or brown slabs along the bank (14); no lily pads hugging the shore; one mid-pond cluster at most.
- Reeds read as plants from the green, not a wall.
- GKD hazard outline moves with any water edit; red/yellow set from the card (drafts are plain hazards).

## Pitfalls

- A stream buried under the course meshes rendered as grass (13).
- Streaked plank ramps, box bridges with 0.6 m rails and "stylised" waterfalls all got rejected; build from the photo.
- Highway 23 beside 15 was "cleaned" into a sandy wash; never clean real features out of the satellite (09).
- Washes picked as sand patches come split with grass gaps; join them in Greenkeeper where the gaps matter.
- Pond fountain and water objects with null materials show as magenta in checks (Hansen).

## Decision points for BoJa

**Needs an in-person look:** whether a wash is OB or a penalty area, and the seasonal fill when photos and LiDAR differ. Water levels (D11) are reliable in the LiDAR; QL1 data settles creek channels (D12), on QL2 courses confirm channel vs sheet from photos.

LiDAR review decision points for water: **D11-D12** (water level and extent, channel vs sheet) in [LIDAR_DETAIL_REVIEW.md](../../playbook/LIDAR_DETAIL_REVIEW.md).

1. Which washes/creeks are interior OB (lost ball) vs penalty areas with drop zones (default: dry washes OB, water hazards with DZ).
2. Pond extent where LiDAR (2018) and photos differ (default: photos).
3. Seasonal water level (dry pond beds in the satellite vs full in the photos).

## Sources

`MORNING_NOTES.md` (Water, Washes) and `tools/water_*.py`, `stream_dig.py` on `claude/moorpark-unity`; `progress/tierra/MORNING_1030.md` (13 creek), `creek13/`, `gate/hole_13/top_items.md` (items 4, 6), `gate/hole_14/top_items.md` (item 1), `gate/hole_18/top_items.md` (item 3), `audit_photo_pairs/AUDIT_FINDINGS_1030.md`.
