# course-build

Everything learned building Tierra Rejada, Moorpark, Penmar, Harding, Wilson, Hansen Dam and Angeles National for GSPro,
in a form a new course can run rather than re-read. One clone gives a course its rules, tools, Unity editor set, skills and
playbooks. Course repos hold only course data (LiDAR, satellite, SVG, GKD, photos) and a `course.json` that points here.

## Layout

| Folder | What it is | Who reads it |
|---|---|---|
| `rules/` | Every threshold and default as JSON, one file per element, each number with its source | Tools, Unity scripts, skills |
| `tools/` | Headless Python: `rules.py` loader, `colour_match.py`, `coursekit.py`, measurement scripts | Batch runs, Claude |
| `unity/shared/` | Course-agnostic editor tools (setup, meshes, trees, paths, look, flyover, automation runner) | Unity on the PC |
| `unity/course-templates/` | Tierra checkers (edge survey, pin area, gates) kept verbatim until generalised | Copy + rename per course |
| `automation/` | `Run-CourseAutomation.ps1`, `Send-UnityCommand.ps1`, install script, configs | PC |
| `skills/` | The 22 course-build skills (current synced copies) | Claude sessions |
| `playbooks/` | 22 playbooks + the LiDAR detail review: the long-form reasoning behind each rule | Humans, Claude when a rule needs context |
| `templates/` | `course.template.json` and the Moorpark example | New course |
| `docs/` | BoJa feedback checklist, Tierra CLAUDE.md / HANDOFF.md as examples | Reference |

## Start a new course (PC)

```powershell
git clone https://github.com/boshersjames/course-build $HOME\Documents\course-build
cd $HOME\Documents
python course-build\tools\new_course.py NewCourse --square 2000 --unity "C:\GSP\NewCourse_Project"
```

That creates `Documents\NewCourse\` with `course.json`, `CLAUDE.md` pointing at this repo, the folder layout, and copies the
shared Unity editor set and automation configs into the Unity project. A Claude session opened in that folder reads
`CLAUDE.md`, loads the skills from here, and knows where everything is.

## Rules are the source of truth

`rules/*.json` hold the numbers. The skills quote them, the playbooks explain them, the code reads them:

```python
from rules import rules, course
R = rules()                                  # with the course's rule_overrides applied
R["greens"]["pins"]["max_lie_angle_deg"]     # 1.7
course().path("unity_project")               # from course.json
```

```powershell
python tools\rules.py greens.collar.width_m   # 1.2
```

A course that needs a different value sets it in `course.json` under `rule_overrides`, never by editing a script.
When BoJa changes a rule, change the JSON and the matching line in the skill; the playbook gets the reason.

## Tools (all read course.json and rules; run from the course folder or set COURSE_JSON)

| Tool | What it does | Needs |
|---|---|---|
| `tools/gkd_tools.py check` | Limits, stimp, firmness, designer, DZ flags, pins per hole against the rules | GKD |
| `tools/gkd_tools.py distances / totals [--write]` | Recompute tee distances and totals, text-preserving edit with backup | GKD |
| `tools/green_report.py [--smooth 0.6] [--sheets]` | LiDAR slope, relief, GreenKeeper pin-area test, usable %, valid areas per green | heightmap, GKD, SVG |
| `tools/flyover_gate.py --out <dir> [--end-fit]` | Flyover path and first/end frame gate for every hole; writes the shot list for the PC render | GKD, signs, trees, SVG, heightmap |
| `tools/gate_report.py` | One per-hole pass/fail table from the Unity checks, LiDAR proxy, flyover gate and GKD checks | the outputs above |
| `tools/colour_match.py` | Photo vs render colour, PASS/FAIL against the rule band, OverallColor multiplier | two images |
| `tools/fairway_vs_lidar.py`, `scan_compare.py`, `green_edge_measure.py` | Fairway undulation vs LiDAR, phone-scan alignment, collar profiles | Unity exports / LAS on the PC |
| `tools/new_course.py` | Bootstrap a course folder, course.json, CLAUDE.md, Unity editor set | nothing |
| `tools/doctor.py` | Ten-second readiness check of a course folder: course.json, heightmap (LFS pointer?), GKD, SVG match, deps, tools | nothing |
| `tools/photo_index.py [--sheets --shots]` | iPhone dump (HEIC/JPG) to index.csv: GPS to course frame, hole, confidence, draft pose; contact sheets; render shot list | dump, GKD, heightmap |
| `tools/scan_index.py` | Phone LAS scans registered to the LiDAR; MAD, coverage, doubled ground; ACCEPT / RESCAN | LAS, heightmap, SVG |
| `tools/audit_pairs.py --renders <dir>` | Photo, before, after sheets from the same pose with measured turf colour and a verdict row per photo | renders from the PC |
| `tools/pose_solve.py [--sheets]` | Skyline fit of each photo against the heightmap + LiDAR trees; diagnostic only (notes + compass flags). Validated on Tierra: applying it is worse than the compass, so the real pose solve stays the Unity panorama fit | heightmap, trees, display JPEGs |
| `tools/drone_frames.py fetch / segments / cut` | Online flyover to per-hole frames with drafted poses, merged into the photo index at low confidence | yt-dlp, ffmpeg |
| Unity `Course Checks > Greens: all-18 report` (`CourseGreenChecks.cs`) | Edge survey, collider vs render, tiers, crowns over, pin area on every green; writes AutomationOutput/checks | Unity, synced course.json + rules |
| Unity `Course Look > Apply Tint JSON` (`ApplyTintJson.cs`) | Applies colour_match multipliers to material copies, refuses shared materials | Unity |

Typical headless loop on a course, no Unity, no model in it:

```bash
python ../course-build/tools/gkd_tools.py check
python ../course-build/tools/green_report.py --smooth 0.6 --out progress/green_report
python ../course-build/tools/flyover_gate.py --out progress/flyover_gate --end-fit --no-plots
python ../course-build/tools/gate_report.py --out progress/gate_report
```

## Photos, scans and drone frames: one index

```bash
python ../course-build/tools/photo_index.py --sheets --shots       # course_photos/unsorted/<date> -> index.csv, sheets/, shots_photo_poses.json
python ../course-build/tools/scan_index.py                          # course_photos/scans/*.las -> scans/index.csv (+ fit sheets)
python ../course-build/tools/drone_frames.py segments --video course_photos/drone/front_nine.mp4 --holes 1-9   # then edit the seconds
python ../course-build/tools/drone_frames.py cut --video course_photos/drone/front_nine.mp4 --segments course_photos/drone/segments_front_nine.csv --index
# PC: render the shot list in one batch (CourseAutomation shots = shots_photo_poses.json), then
python ../course-build/tools/audit_pairs.py --renders latest [--before <previous run>/shots]
```

Every image, scan and drone frame ends up as a row in `course_photos/index.csv` with a hole, a position confidence and a pose; the
audit adds measured colour and a verdict per photo. The rules are in `rules/photos.json`; drone frames never set colour.

## Colour without eyeballing

```powershell
python tools\colour_match.py photo.jpg render.png --box 120,340,80,60 --target green --json tint.json
```

Prints median RGB, r/g, b/g for both, the gap after the render's known warm offset, PASS/FAIL against the rule band, and the
OverallColor multiplier that closes it. `unity/shared/ApplyTintJson.cs` reads `tint.json` and applies the multipliers to the
named material copies. Loop: render (batch), measure, apply, re-render. No model in the loop.

## State of the migration

- Done: playbooks, LiDAR review, skills, shared Unity tools, automation runner, rule extraction, loader, colour tool; headless
  green report, GKD tools, flyover gate and gate report tested against Tierra; CourseGreenChecks.cs written; runner reads course.json.
- Needs the PC once: compile `CourseGreenChecks.cs` and `ApplyTintJson.cs` (written in the cloud, never compiled), run
  `Run-CourseAutomation.ps1 -SyncTools` from the Tierra folder, run Course Checks > Greens, then `gate_report.py` has the Unity half.
  Diff `C:\GSP\...\Assets\Editor` against `unity/shared` and commit anything newer here.
- Still Tierra-shaped: `tools/tierra_scripts/*` (the originals, kept for reference until each is ported or dropped) and
  `unity/course-templates/*` (the 2,238-line TierraNight1031 and friends: the generic checks are now in CourseGreenChecks.cs; the
  rest are per-hole fixes that stay with Tierra).
