"""Shared fixtures for the course-build regression suite. See tests/README.md.

Course data never comes from this repo: the tests read a real course folder (Tierra Rejada by default) and write only into
pytest's tmp_path. Every tool runs as a subprocess with COURSE_JSON set, exactly as the headless loop runs it.
"""
import json, os, shutil, subprocess, sys
import pytest

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, "tools")
RULES = os.path.join(ROOT, "rules")

TIERRA = os.environ.get("COURSE_BUILD_TEST_COURSE", "/home/user/tr-flyover-gate-1102")
MOORPARK = os.environ.get("MOORPARK_TEST_COURSE", "/home/user/mp-unity")
FAKE_RENDERS = os.environ.get("COURSE_BUILD_FAKE_RENDERS",
                              "/tmp/claude-0/-home-user/e920af01-d04f-5fee-8488-a122013cb130/scratchpad/fake_renders")


def pytest_configure(config):
    config.addinivalue_line("markers", "slow: takes more than ~10 s (real LAS / HEIC data); skip with -m 'not slow'")


def _course_or_skip(folder, what):
    if not os.path.exists(os.path.join(folder, "course.json")):
        pytest.skip("%s course folder not found at %s (set %s)" % (what, folder, "COURSE_BUILD_TEST_COURSE" if what == "Tierra" else "MOORPARK_TEST_COURSE"))
    return folder


@pytest.fixture
def tierra():
    """The Tierra Rejada worktree: heightmap, GKD, SVG, signs, trees, photo index, 80 HEIC originals, 3 LAS scans."""
    return _course_or_skip(TIERRA, "Tierra")


@pytest.fixture
def moorpark():
    """Moorpark (3000 m square, 27 holes as 18-hole layouts, no signs file, no course_photos)."""
    return _course_or_skip(MOORPARK, "Moorpark")


@pytest.fixture
def fake_renders():
    if not os.path.isdir(FAKE_RENDERS) or not any(f.startswith("photo_IMG_") for f in os.listdir(FAKE_RENDERS)):
        pytest.skip("stand-in renders not found at %s (set COURSE_BUILD_FAKE_RENDERS)" % FAKE_RENDERS)
    return FAKE_RENDERS


@pytest.fixture
def tools_on_path(monkeypatch):
    """Import the tools in-process (rules, course_io)."""
    monkeypatch.syspath_prepend(TOOLS)
    for m in ("rules", "course_io"):
        sys.modules.pop(m, None)
    return TOOLS


def run_tool(name, *args, cwd=None, course_json=None, env=None, timeout=300, check=False):
    """Run tools/<name>.py as the loop does: a subprocess, cwd in the course folder (or COURSE_JSON set)."""
    e = dict(os.environ)
    e.pop("COURSE_JSON", None)
    if course_json:
        e["COURSE_JSON"] = course_json
    if env:
        e.update(env)
    e.setdefault("MPLBACKEND", "Agg")
    r = subprocess.run([sys.executable, os.path.join(TOOLS, name + ".py")] + [str(a) for a in args],
                       cwd=cwd or ROOT, env=e, capture_output=True, text=True, timeout=timeout)
    if check and r.returncode != 0:
        raise AssertionError("%s %s exited %d\nSTDOUT:\n%s\nSTDERR:\n%s" % (name, " ".join(map(str, args)), r.returncode, r.stdout[-4000:], r.stderr[-4000:]))
    return r


def shadow_course(src_folder, dest_folder, **overrides):
    """Write <dest>/course.json copied from <src>/course.json with every relative path made absolute into <src>, so a tool
    reads the real data but any default output (course_photos, progress) lands under dest. Keyword overrides replace paths."""
    with open(os.path.join(src_folder, "course.json"), encoding="utf-8-sig") as fh:
        c = json.load(fh)
    sys.path.insert(0, TOOLS)
    from rules import is_abs
    P = c.get("paths", {})
    for k, v in list(P.items()):
        if isinstance(v, str) and v and not v.startswith("~") and "://" not in v and not is_abs(v):
            P[k] = os.path.normpath(os.path.join(src_folder, v))
    P.update(overrides)
    os.makedirs(dest_folder, exist_ok=True)
    out = os.path.join(dest_folder, "course.json")
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(c, fh, indent=1)
    return out


def rule(*keys):
    """Read a threshold straight from rules/*.json so tests never hard-code one: rule('greens', 'collar', 'width_m')."""
    with open(os.path.join(RULES, keys[0] + ".json"), encoding="utf-8") as fh:
        v = json.load(fh)
    for k in keys[1:]:
        v = v[k]
    return v


def md_table_rows(md_text):
    """Rows of the first markdown table whose first cell is a hole number."""
    import re
    return [l for l in md_text.splitlines() if re.match(r"^\|\s*\d+\s*\|", l)]


def have(tool):
    return shutil.which(tool) is not None
