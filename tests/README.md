# Tests

Regression suite for the headless loop: every tool in `tools/` runs as a subprocess against a real course folder, exactly as
the README's loop runs it, and writes only into pytest's `tmp_path`. The course folders are never written to. Thresholds the
tests compare against are read from `rules/*.json` through `conftest.rule()`, never typed into a test.

```bash
pip install pytest                      # once; numpy, scipy, matplotlib, pillow, pillow-heif, pyproj, laspy, boto3 as for the tools
python -m pytest tests                  # everything, about 25 s with a warm file cache (~60 s cold)
python -m pytest tests -m "not slow"    # skip the LAS / HEIC tests (~12 s)
python -m pytest tests -rs              # show why tests were skipped
```

## What the tests need

| Environment variable | Default | Used by |
|---|---|---|
| `COURSE_BUILD_TEST_COURSE` | `/home/user/tr-flyover-gate-1102` | the Tierra Rejada worktree: `course.json`, real `heightmap_2049.raw` (8 396 802 bytes), GKD, course SVG, signs JSON, trees CSV, `course_photos/index.csv`, the 80 HEIC originals under `course_photos/unsorted/2026-10-29`, 3 LAS scans under `course_photos/scans` |
| `MOORPARK_TEST_COURSE` | `/home/user/mp-unity` | Moorpark (3000 m square, no signs file): `doctor`, and the `flyover_gate` "provisional sign spots" path |
| `COURSE_BUILD_FAKE_RENDERS` | the session scratchpad `fake_renders/` | stand-in `photo_IMG_*.png` renders for `audit_pairs` |

When a folder is absent the tests that need it skip with a message naming the variable; the rules, loader, synthetic-image,
`doctor` LFS-pointer, `colour_match`, `brightness_check` and `new_course` tests run anywhere. `ffmpeg`/`ffprobe` are needed for
the `drone_frames` test (skipped otherwise). The `photo_sync` test points the proxy at `127.0.0.1:9` so no request leaves the
machine; it checks the "sending unsigned" message and a clean failure.

## Layout

| File | Covers |
|---|---|
| `conftest.py` | fixtures (`tierra`, `moorpark`, `fake_renders`), `run_tool()` (subprocess with `COURSE_JSON`), `shadow_course()` (a temp `course.json` whose data paths point at the real course but whose outputs land in tmp), `rule()`, the `slow` marker |
| `test_rules_io.py` | every `rules/*.json` parses; `rules()` returns every element; `rule_overrides` merge; `is_abs` on Windows drive paths; `Heightmap` shape/scale/sample; `GKD.limits()`; SVG greens match 18 holes |
| `test_headless_loop.py` | `doctor` (Tierra, Moorpark, LFS pointer -> exit 1); `gkd_tools check` / `distances` dry run; `green_report`; `flyover_gate` (with and without signs); `gate_report` on top of both, 18 rows |
| `test_photos.py` | `photo_index` on a symlinked copy of the dump; `scan_index` on the 3 LAS; `audit_pairs` hole 10; `coverage`; `drone_frames segments` + `cut` on an ffmpeg `testsrc` clip; `photo_sync check` offline; one xfail for a known `photo_index --out` bug |
| `test_images_and_bootstrap.py` | `colour_match` and `brightness_check` on synthetic PNGs; `new_course` into tmp and `rules.py` reading it |

## Rules for new tests

- Run the tool with `run_tool(name, *args, cwd=course)` or `course_json=...`; never import a tool's `main()` with patched argv.
- Output goes under `tmp_path`; for a tool that writes next to `course.json` (`drone_frames`, defaults of `photo_index`), build a
  `shadow_course()` and point `photos_root` / `progress` into tmp.
- Compare against `rule(...)`, never a literal threshold. Mark anything over ~10 s `@pytest.mark.slow`.
- A test that exposes a tool bug is marked `xfail(strict=True)` with the file, line and diagnosis in the reason; the tool is fixed in
  its own commit and the marker removed then.
