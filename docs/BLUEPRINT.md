# Blueprint

What we are building, why the pipeline is shaped the way it is, and what the next cuts are. Read this before the README.

## The objective

Recreations of real courses for GSPro that a member recognises hole by hole. Built for many courses, not one, at a cost in
days and credits that stays sane. Tierra Rejada is the proof piece and the door-opener with clubs; the toolkit exists so course
two costs a fraction of course one. The judge is a person who knows the course. Every tool here exists to get a change in
front of that judge faster and with the question already answered by a number.

## Where the cost went

Not the modelling. The hand-offs. Tierra was built as a multi-stage pipeline: LiDAR to coursekit to SVG to Unity to per-element
passes to photo audit. Each stage was precise and each stage added a hand-off: a path typed into a script, a script written for one
hole, a render looked at by a model, a scratchpad that died with the session. Collapsing hand-offs is the work. Three were
removed on 2026-11-03 (hard-coded paths, per-hole scripts, eyeballed colour). Three remain, in the order to cut them:

1. **Cloud to PC for every render.** Every photo-vs-build question still needs Unity on the PC. Cut: a shots-only batch run that
   renders every indexed photo's pose in one go, so the PC is touched once per edit round, not once per question.
2. **Geometry checks that live only in Unity.** Edge survey, collider checks, crowns over greens. Cut: run them headless from the
   automation bridge (CourseGreenChecks.RunAll) and read the table, never the scene.
3. **Decisions that live in chat.** A verdict, a rule change, an accepted exception. Cut: every decision lands in a file the next
   session reads (index.csv verdicts, rules/*.json, placement_exceptions.txt, decisions.csv).

## Layers by how fast they change

The data is one dataset with three rates of change. Each element carries its layer and its source, so a session knows what to
trust and for how long.

| Layer | Elements | Source of truth | Changes | Where it lives |
|---|---|---|---|---|
| Permanent | terrain, routing, green and bunker footprints, water levels, roofs | LiDAR, satellite, OSM | once per course (new survey) | heightmap, course.svg, course.json frame |
| Per visit | green surfaces, bunker faces and depths, collars, tee decks in play, props, signs, fences, species near play | phone scans, geotagged photos, rangefinder | each site visit | course_photos/index.csv, scans/index.csv, decisions.csv |
| Per round | pins, which colour plays which deck, mow lines, rough colour by season | the day's setup, BoJa's call | weekly or seasonal | GKD pins, course.json tee_slot_map, rule_overrides, a default when unanswered |

Rules: permanent-layer elements never change from a photo (a photo can only raise a question). Per-visit elements change only
from an indexed, registered capture (D13). Per-round elements are built to a default once and left, and never block a gate.

## Coverage: one number per hole

A single capture pass never gets everything (the AV mapping world measured one drive at about 65% of local elements; the fix was
fusing passes, not a better pass). We fuse iPhone photos, phone scans, drone frames and rangefinder readings into one index, and
we measure how much of each hole they cover.

Coverage per hole = share of the site-visit checklist items (course-build-site-visit, "Capture per hole") that have at least one
indexed capture: a photo row with pose_conf high or medium, a scan with verdict ACCEPT, or a drone frame, assigned to that hole and
tagged with that spot (tee_<colour>, bunker_<n>, green, collar, surround_<side>, water, props, trees, skyline). Reported as:

    hole | items | covered | coverage % | missing items

Computed from course_photos/index.csv and scans/index.csv (tool: `tools/coverage.py`; spots are tagged in index.csv or inferred from the camera position).
A hole below 80% goes on the next visit list before any polish pass. Drone frames count for position and layout only, never colour.

## What we do not do

- **Learned end-to-end generation.** The AV map producers trained on years of surveyed ground truth across hundreds of cities. We
  have a handful of courses and one judge. The method does not transfer; the diagnosis (hand-offs are the cost) does.
- **Polish before coverage.** No pass on a hole whose coverage is under the line; the pass will be redone after the visit.
- **Eyes where a number exists.** Colour, slope, pin lie, collar step, crown overhang, flyover framing all have tools. A render is
  opened when a number says something is wrong, not to find out whether something is.

## What we bank

Every audit row is a photo, a render from the same pose, and a verdict. Across courses that is a dataset nobody else has: real
course versus build, judged. Keep writing it honestly, including the rows that pass, and never skip the audit because the judge is
already happy. If learned generation ever becomes feasible at our scale, this is the training set.

## The loop, as it should run on the next course

1. `doctor` then the pipeline to a first GKD and SVG (permanent layer). LiDAR review D1-D18, visit list written.
2. Site visit to the checklist. Index photos, scans, drone frames. Coverage per hole.
3. PC: one batch render of every photo pose. Audit sheets and measured colour. Verdicts into the index.
4. Edit one hole, one element, one edit. Re-render the same poses. Gate report. Only a passing hole goes to the judge.
5. Decisions into files. Rule changes into rules/*.json with the skill line and the playbook sentence.
