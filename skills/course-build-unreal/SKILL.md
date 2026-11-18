---
name: "course-build-unreal"
description: "Use when a GSPro course built in Unity gets an Unreal Engine realism pass for flyovers or hero clips: export, species library, scored one-change runs and Fab setup."
---

# Unreal flyovers and hero clips

Pilot: Tierra hole 10 approach (UE 5.8, 11/07-11/11). Goal (BoJa 11/10): one command per course after a one-time library setup, runnable without him; same shape as `Finish-Course.ps1`. Anything that works goes into the per-course spec the scripts replay, never left as a one-off editor fix. Full guide: `course-build/playbooks/23_unreal_flyovers.md` (moorpark repo); living doc: the look doc and `HERO_PIPELINE_PLAN.md` in `staged_unreal`. Lighting, exposure and vegetation look (material-group review, frozen baseline, regression views, motion preview): `course-build-lighting`.

## Standing rules

1. **One engine at a time** on the PC (12 GB GPU). Ask the coordinator for the slot, confirm 0 Unity processes, `pc_guard` before each launch, report "closed" with the process count. Plan 60-90 min blocks (startup is 5-8 min), chain steps in one session.
2. **One change per scored run**, auto-undo on a failed check, record why.
3. **Never sign in, download or accept terms**: Fab "Add to Project" is BoJa's (his Epic account, his licence).
4. Unity export: check no other Unity instance has the scene, never save it, close Unity fully before launching UE.

## Defaults that worked

- Export FBX (ASCII 7.4) + textures, blend and grass density maps (hole 10: ~3.5 GB, 133 textures, 13 maps). Axis Unity → UE: X = -x, Y = z, Z = y, ×100 cm; never add extra scale.
- Two levels: *matching* (Unity look, production flyover) and *hero* copy. Correctness fixes go to both.
- Look v1: physical sun and sky, bounce light, clouds, haze; sand and green colours from the scans.
- Dark edge lines are the turf pieces' own shadows: OPCD turf meshes don't cast shadows (`turf_noshadow`).
- Score: 1080p check frames, ΔE per class (vegetation, turf, sand, gap/edge) and changed-pixel % vs the same baseline; adopt only if the target class improves with no regressions. 4K only for kept steps and the final clip.
- Speed: one MRQ queue for all frames (`--single-queue`, ~2×, identical frames); sweep 3 values of a tunable in one launch, each scored as one change.
- Trees: our own models first (TR_Oak, TR_PepperV2), sized by crown width and height (TR_Oak Y = h/12.63, XZ = crown/12.8; pepper divisor 11.84), leaves two-sided masked. SpeedTree imports natively; we write a design per species and BoJa shapes it once (~20 min).
- Species library: each species built once and mapped from `trees.csv` / Unity prefab names to `/Game/.../TreeModels`.
- **Species look-check gate**: every new species rendered alone at flyover distance in hero light (`hero_lookcheck.py`, level copy) before it goes on a course.
- Brush order: dry ground first, then shrubs, then density, then tint.
- **Vegetation: base, then photos (BoJa 11/16).** LiDAR masks and the satellite lay the base for trees and brush; the hole's indexed photos and BoJa's notes then set species, extent, cover and where planting starts and stops, through the photo enhancement procedure in `course-build-photo-index`. Unreal places what the Unity build's photo-checked inputs say; it never re-invents vegetation, and never copies faults (dead or bare patches in play).
- **Vegetation photo gate**: before a hero or flyover clip is kept, every hillside and tee surround with high/medium photo poses gets a photo | Unreal frame from the same pose with an explicit verdict; a mismatch is fixed in the shared inputs (both levels), not as an editor-only tweak.

## Background and neighbouring holes

- **Never crop the world to the hole.** Export the whole course: the current hole at full detail, every other hole and the Unity Outer Plot at reduced detail. Any neighbouring hole the camera sees must show its real turf, bunkers, paths and trees, never bare ground.
- **Far terrain ring** per course from USGS 3DEP DEM (`far_ring.py`, ~13.5 km, LiDAR heightmap inside the course square, DEM outside, joined within ±0.5 m), with **land cover on it**: satellite or NLCD colour plus scattered far trees and brush. A single flat colour reads as a bare tan plain. Remove the flat 20 km plane; fog start ~1.5 km, as in Unity.
- Check: no flat single-colour region above X% of the frame beyond the course; skyline compared against a course photo.
- Reference textures: BoJa's FC_Coastal_Landscape (dry grass, soil, rock) is texture only, with no heightmaps.

## Overlay and props

- Guided version (pin, heat map, distance countdown, fairway lines) and the clean version both come from one render, via `flyover_packager.py` / `annotate_overlay.py` on the logged camera. Deliver both.
- Hole banner: hole number, par and distances from course data.
- Tee markers, hole signs and other props come from the Unity course's own props (course-build-props), never generic primitives. A placeholder box reads as a rock.

## One-time setup with BoJa

- **Project path ≤ 130 characters** or the project browser refuses it. Open `C:\UE\CourseRenderTest\CourseRenderTest.uproject` (short junction) by typing it into Browse; scripts keep the long path. Move the real folder only if that still fails.
- **EngineAssociation**: a script-made `.uproject` may not match the launcher's version key: fix the one line (backup `.bak_assoc`) or More Options → **Convert in-place**, never "Open a copy".
- **Fab**: Window → Fab → My Library, quality High, Free filter, Quixel items, Add to Project, then **Save All**; on close "Don't Save" the Untitled level but save anything under Fab/Megascans. Only saved files on disk count; re-inventory `/Content/Fab` from disk.

## Procedure per course

1. Export (Unity closed after) → import into the matching level.
2. `apply_spec.py` replays the course spec onto the hero copy.
3. Species map → place trees and brush from the photo-checked inputs (`trees.csv`, brush masks); look-check gate for new species; vegetation photo gate per hillside and tee surround.
4. Camera path from the approved Unity flyover; check frames, score, pass the `course-build-lighting` regression views and short motion preview, then 4K.
5. `run_hole.py --restart` for a fresh run (it otherwise reuses the newest run dir and keeps finished stages).
6. Summary per run: change, scores, adopted/undone, still and clip paths; list anything needing BoJa.

## Engine settings that matter

- **Nanite off on terrain, turf, collar, green, blend and path meshes.** Nanite's position quantisation pushes terrain 20-40 cm below through the turf (dark patches, bunker islands, green spots) and makes spiky collar and path edges. Keep Nanite for trees and props.
- Deferred decals don't compile at SM5 with Substrate (the UE 5.8 default). Blend dry soil into the turf/terrain material from the course brush mask instead.
- Rough and green tiling: two-scale randomised tiling at about 0.25 strength plus large-scale colour variation. Stronger tiling moves rough colour away from the target.
- Mowing: sine-based cross-hatch on tees and green (the Noise node won't connect in UE 5.8).

## Automatic checks

Green showing on a scrub mask; tree crown fill below the Unity reference; white or invisible materials; compile-only check of any new material (<1 min) before a run.

## Pitfalls

- Shrubs without dry ground: bright green between them read as parkland.
- Dry-ground decal material failed to compile and rendered nothing.
- Fab trees: willow without textures rendered white; scanned live oak leaves didn't render; olive sparse; cork oak thin. Height-only scaling thinned the masses (vegetation ΔE 8.6 → 11.1).
- Bunker scans as drop-ins looked worse; undone.
- Unreal open during a GSPro benchmark wrecks the score.

## Ask BoJa once (with a default)

1. Which species get a SpeedTree session (default: top two by count near play).
2. Hero holes per course (default: signature holes from the course notes).