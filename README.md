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

## Colour without eyeballing

```powershell
python tools\colour_match.py photo.jpg render.png --box 120,340,80,60 --target green --json tint.json
```

Prints median RGB, r/g, b/g for both, the gap after the render's known warm offset, PASS/FAIL against the rule band, and the
OverallColor multiplier that closes it. `unity/shared/ApplyTintJson.cs` reads `tint.json` and applies the multipliers to the
named material copies. Loop: render (batch), measure, apply, re-render. No model in the loop.

## State of the migration

- Done: playbooks, LiDAR review, skills, shared Unity tools, automation runner, rule extraction, loader, colour tool.
- Next: generalise the Tierra checkers in `unity/course-templates/` (hole number as a parameter, paths from course.json);
  rewrite `tools/tierra_scripts/*` to read `course.json` instead of `C:/GSP/...`; shrink each skill to one page that points at
  a rule file and a tool.
- The authoritative copies of the Unity scripts were in `C:\GSP\GSPBaseProject_v4_2025_12_02\Assets\Editor` on the PC.
  Diff that folder against `unity/` once and commit any newer versions here.
