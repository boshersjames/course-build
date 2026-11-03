# {{NAME}} GSPro course

Real course rebuilt for GSPro (Unity 2018.2.8f1, OPCD). Toolkit: `{{TOOLKIT}}` (the course-build repo). Read its
`README.md` once, then the skill for the element you are working on in `{{TOOLKIT}}/skills/`. All thresholds come from
`{{TOOLKIT}}/rules/*.json`; this course's overrides are in `course.json` under `rule_overrides`.

## Where things are
- `course.json`: frame, paths, GKD fields, tee slot map, overrides. Every script reads paths from here.
- Unity project: `{{UNITY}}`. Editor tools in `Assets\Editor\` are copies from `{{TOOLKIT}}\unity\shared\`; sync with
  `{{TOOLKIT}}\automation\Run-CourseAutomation.ps1 -SyncTools`.
- Automation output: `{{UNITY}}\AutomationOutput\latest.txt` then that folder's `summary.md` and `shots\`.
- Never change `C:\GSProV1\Core\GSP\Courses\` without backing up the GKD first.

## Driving Unity
- Unity closed: `{{TOOLKIT}}\automation\Run-CourseAutomation.ps1 -Config automation\{{NAME}}.json [-Steps "a,b"]`.
- Unity open: `{{TOOLKIT}}\automation\Send-UnityCommand.ps1 <ping|run|shots|invoke|describe|menu|save|refresh>`.

## Process
Follow `course-build-gates`: one hole, one edit, photo pair each; control hole before/after on course-wide passes; back up
scene, terrain and GKD before every change. Run the LiDAR review (D1-D18) before the first gate and record answers in
`course_photos/decisions.csv`.

## Cost discipline
Measure with tools, not eyes. Colour: `colour_match.py`. Green edges: the edge survey. Pins: the pin-area test. Read the
summary a tool writes; open a render only when a number says something is wrong.
