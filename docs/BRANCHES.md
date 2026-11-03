# Branch map and merge order (2026-11-03)

Where every piece of work sits today, and the order to land it so each course has one branch of truth. Written for the first PC
session on Wednesday; nothing here has been merged yet.

## The shape of the problem

Each Claude thread branched from wherever it started and never merged back. Three repos ended up holding six courses:

| Repo | Branch | Holds | Last commit |
|---|---|---|---|
| tierra-rejada | main | 17 Python files, no unity_editor, no automation | 2026-10-23 (stale) |
| tierra-rejada | claude/flyover-gate-1102 | the newest Tierra line: 202 C#, 147 Python, showcase flyovers, the sent email | 2026-11-03 |
| tierra-rejada | claude/toolkit-wiring | flyover-gate-1102 + course.json, photo sample, scans, PC script snapshot, reports | 2026-11-03 |
| tierra-rejada | claude/project-thread-m1t515 | the PC's own line: GKD syncs, the live Assets\Editor snapshot (164 files), photo sample | 2026-11-02 |
| tierra-rejada | claude/unity-automation | batch runner line (Night1031 WaterStep); mostly inside flyover-gate-1102 already | 2026-11-01 |
| tierra-rejada | project-thread b3724j, epsb9u, j9f4a4, t0xumo, rq5cyi, 711mvd, u616kr | one pass each: showcase v5, fix list v5, hill brush, playbook index, photo poses, 17 green, tee markers | 10-30 to 11-02 |
| tierra-rejada | socal-vegetation, flyover-preview, tr-hillside-logo, unity-deep-rough-shrubs, hole2-ridge-fence | older feature lines, Oct 24-27 | |
| moorpark | master | course data only | 2026-10-24 |
| moorpark | claude/moorpark-unity | shared Unity tools, tools/, drone frames, six GKDs | 2026-10-28 |
| moorpark | claude/toolkit-wiring | moorpark-unity + course.json (toolkit keys), CLAUDE.md, first reports | 2026-11-03 |
| moorpark | claude/project-thread-wgz7f7 | the 22 playbooks (now copied into course-build) | 2026-11-02 |
| moorpark | claude/project-thread-n5ggr9 | LIDAR_DETAIL_REVIEW (now in course-build) | 2026-10-31 |
| moorpark | claude/project-thread-f78r3v | **courses/hansen_dam** (68 files, offsite trees) | 2026-10-29 |
| moorpark | claude/project-thread-47fcq5 | **courses/angeles_national** (54) + an older hansen_dam (27) | 2026-10-29 |
| moorpark | claude/project-thread-j2x22i | **courses/penmar** (23) | 2026-10-28 |
| moorpark | claude/project-thread-a6haj8 | **griffith-park/harding** (23) + shared fix scripts | 2026-10-29 |
| moorpark | claude/project-thread-flaaah | **griffith-park/wilson** (32) | 2026-10-29 |
| moorpark | claude/project-thread-2j1jyq | shot test | 2026-10-28 |
| course-build | main | the toolkit (this repo) | 2026-11-03 |

The five non-Moorpark courses were only ever committed inside moorpark thread branches. Nobody would find them from main.

## Target

One repo per course, each with `course.json` (toolkit keys), `CLAUDE.md` pointing at course-build, and `main` as the only branch
anyone starts from. The toolkit holds everything shared. Thread branches get deleted once their content is on main.

## Merge order

### tierra-rejada
1. `claude/toolkit-wiring` is the newest superset (it contains flyover-gate-1102, the PC snapshot from m1t515 and the photo pipeline).
   On the PC: `git checkout main; git merge --ff-only claude/toolkit-wiring` will not fast-forward (main is 10 days behind on a
   different line). Use `git merge claude/toolkit-wiring`; expect no conflicts because main's files are a strict subset.
2. Check the three thread branches dated 11-02 for anything not in flyover-gate-1102: b3724j (showcase v5 sheets), epsb9u (fix list
   v5), t0xumo (playbook index). `git log main..<branch> --stat` per branch; cherry-pick files, not commits.
3. Delete: unity-automation, flyover-preview, socal-vegetation, tr-hillside-logo, unity-deep-rough-shrubs, hole2-ridge-fence,
   rq5cyi, 711mvd, u616kr, j9f4a4 after confirming `git diff main <branch> -- <their folder>` is empty or only older.
4. m1t515 stays until the PC has switched to main (it is the PC's current branch). Then delete.

### moorpark
1. Merge `claude/toolkit-wiring` into master (superset of moorpark-unity).
2. The playbooks and LIDAR review already live in course-build; wgz7f7 and n5ggr9 can go once a diff against course-build/playbooks
   shows nothing newer.
3. **Split the other courses out** (see below), then delete f78r3v, 47fcq5, j2x22i, a6haj8, flaaah, 2j1jyq.

### New course repos (create empty on GitHub, then push)
| Repo | Source | Notes |
|---|---|---|
| hansen-dam | moorpark f78r3v `courses/hansen_dam` | newest Hansen line; 47fcq5 has an older copy |
| penmar | moorpark j2x22i `courses/penmar` | |
| angeles-national | moorpark 47fcq5 `courses/angeles_national` | QL1 LiDAR course |
| harding | moorpark a6haj8 `griffith-park/harding` + the shared fix scripts beside it | |
| wilson | moorpark flaaah `griffith-park/wilson` | |

Each gets the toolkit `course.json` keys added beside coursekit's, a `CLAUDE.md` from `templates/CLAUDE.course.md`, and
`tools/doctor.py` run once. Hansen Dam and Penmar are prepared first (next courses).

## Rules going forward
- One branch per course that sessions start from: `main`. Work branches merge the same day they pass the gate.
- A session that ends without merging writes what is on its branch into the course README's "Open branches" line.
- Course data never lives inside another course's repo.
