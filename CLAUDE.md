# course-build toolkit

This repo is the shared toolkit for building real golf courses for GSPro. It holds no course data.

- Rules live in `rules/*.json`. Never hard-code a threshold in a script or a skill; read it with `tools/rules.py` and cite the key.
- A change to a rule is a change to the JSON, the one line in the matching `skills/course-build-*/SKILL.md` that quotes it, and a
  sentence in the playbook saying why. All three in one commit.
- New tools go in `tools/` (Python, headless, paths from `course.json`) or `unity/shared/` (C#, no course names, no constants
  that belong in `course.json`). Grep for `2000`, `3000`, `Tierra`, `Moorpark`, `C:/` before committing.
- Course-specific one-offs do not belong here. If a Tierra script is needed on another course, generalise it into `unity/shared/`
  with the hole and course as parameters, then delete the copy.
- Skills are one page: what to run, which rule file, what passes. The playbook carries the reasoning.
