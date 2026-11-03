"""1-2: the rules loader and the shared course loaders (course_io)."""
import glob, json, os, sys
import pytest
from conftest import ROOT, RULES, TOOLS, run_tool, rule


def test_every_rules_file_parses():
    files = sorted(glob.glob(os.path.join(RULES, "*.json")))
    assert files, "no rules/*.json"
    for f in files:
        with open(f, encoding="utf-8") as fh:
            d = json.load(fh)
        assert isinstance(d, dict), f


def test_rules_returns_every_element_key(tools_on_path, monkeypatch, tmp_path):
    monkeypatch.setenv("COURSE_JSON", str(tmp_path / "none.json"))  # absent: rules() must still work without a course
    monkeypatch.chdir(tmp_path)
    from rules import rules
    R = rules()
    expected = {os.path.basename(f)[:-5] for f in glob.glob(os.path.join(RULES, "*.json")) if not os.path.basename(f).startswith("_")}
    assert set(R) == expected
    assert R["greens"]["pins"]["max_lie_angle_deg"] == rule("greens", "pins", "max_lie_angle_deg")


def test_course_overrides_merge(tools_on_path, monkeypatch, tmp_path):
    base = rule("greens", "collar", "width_m")
    cj = tmp_path / "course.json"
    cj.write_text(json.dumps({"name": "T", "frame": {"square_m": 2000}, "paths": {"heightmap": "terrain/h.raw", "unity_project": "C:/GSP/T"},
                              "rule_overrides": {"greens": {"collar": {"width_m": base + 0.3}}, "terrain": {"terrain_under_meshes_m": 0.42}}}))
    monkeypatch.setenv("COURSE_JSON", str(cj))
    from rules import rules, course
    R = rules()
    assert R["greens"]["collar"]["width_m"] == pytest.approx(base + 0.3)
    assert R["greens"]["pins"]["max_lie_angle_deg"] == rule("greens", "pins", "max_lie_angle_deg"), "sibling keys survive the merge"
    assert R["terrain"]["terrain_under_meshes_m"] == 0.42
    C = course()
    assert C.path("heightmap") == os.path.join(str(tmp_path), "terrain", "h.raw")
    assert C.path("unity_project") == "C:/GSP/T", "Windows paths pass through untouched"
    assert C.m_per_sample == pytest.approx(2000 / 2048)
    # the CLI prints the overridden value too
    r = run_tool("rules", "greens.collar.width_m", course_json=str(cj))
    assert r.returncode == 0, r.stderr
    assert float(r.stdout.strip()) == pytest.approx(base + 0.3)


def test_is_abs_windows_drive(tools_on_path):
    from rules import is_abs
    assert is_abs("C:/GSP/Project") and is_abs("D:\\GSP\\Project") and is_abs("/home/x")
    assert not is_abs("terrain/h.raw") and not is_abs("../course-build")


@pytest.fixture
def tierra_io(tierra, tools_on_path, monkeypatch):
    monkeypatch.setenv("COURSE_JSON", os.path.join(tierra, "course.json"))
    import course_io
    return course_io


def test_heightmap_shape_scale_sample(tierra_io):
    import numpy as np
    C = tierra_io.course(); HM = tierra_io.Heightmap(C)
    n = C["frame"].get("heightmap_px", 2049)
    assert HM.H.shape == (n, n)
    assert HM.S == pytest.approx(C["frame"]["square_m"] / (n - 1))
    assert 0 <= HM.H.min() and HM.H.max() <= C["frame"]["relief_m"] + 1e-6
    v = HM.sample(1000.0, 1000.0)
    assert isinstance(v, float) and 0 <= v <= C["frame"]["relief_m"]
    assert HM.sample(1000.0, 1000.0, 3) == pytest.approx(v, abs=2.0)
    xs = np.linspace(500, 1500, 7); zs = np.linspace(500, 1500, 7)
    vec = HM.sample(xs, zs)
    assert vec.shape == (7,) and np.all((vec >= 0) & (vec <= C["frame"]["relief_m"]))
    XX, ZZ, H = HM.grid(900, 910, 900, 910, 1.0)
    assert H.shape == XX.shape == ZZ.shape == (10, 10)
    assert HM.elevation(1000.0, 1000.0) == pytest.approx(v + C["frame"]["base_elevation_m"])


def test_gkd_limits_and_holes(tierra_io):
    C = tierra_io.course(); G = tierra_io.GKD(C)
    assert len(G.holes) == C.get("holes", 18)
    L = G.limits()
    assert L["hazards_max"] == rule("ob", "limits", "hazards_max") and L["pOOB_max"] == rule("ob", "limits", "pOOB_points_max")
    assert L["hazards_ok"] and L["pOOB_ok"]
    h = G.hole(7)
    assert h.green_centre is not None and h.green_centre.shape == (2,)
    assert h.playing_tees() and h.back_tee() is not None and len(h.line()) >= 2
    assert h.pins and h.pin_xz().shape[1] == 2
    with pytest.raises(KeyError):
        G.hole(99)


def test_svg_greens_match_18_holes(tierra_io):
    C = tierra_io.course(); G = tierra_io.GKD(C)
    greens = tierra_io.svg_outlines(C, "green")
    assert len(greens) >= 18
    m = tierra_io.match_outlines_to_holes(greens, G)
    assert sorted(m) == list(range(1, 19))
    for hn, pts in m.items():
        assert pts.ndim == 2 and pts.shape[1] == 2 and len(pts) >= 3
