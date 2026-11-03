"""13-14, 16: colour_match, brightness_check on synthetic images; new_course bootstrap."""
import json, os, re
import numpy as np
import pytest
from PIL import Image
from conftest import ROOT, run_tool, rule


def _scene(path, turf, sky=(120, 160, 230), size=(400, 300)):
    a = np.zeros((size[1], size[0], 3), np.uint8); a[: size[1] * 2 // 5] = sky; a[size[1] * 2 // 5:] = turf
    Image.fromarray(a).save(path)
    return path


def test_colour_match_prints_mul_and_verdict(tmp_path):
    photo = _scene(tmp_path / "photo.png", (70, 120, 50)); render = _scene(tmp_path / "render.png", (85, 125, 45))
    js = tmp_path / "tint.json"
    r = run_tool("colour_match", str(photo), str(render), "--box", "50,150,100,80", "--target", "green", "--json", str(js), cwd=str(tmp_path))
    assert r.returncode == 0, r.stderr
    assert "mul [" in r.stdout and re.search(r"\b(PASS|FAIL)\b", r.stdout), r.stdout
    assert "photo [70.0, 120.0, 50.0]" in r.stdout
    d = json.load(open(js))
    b = d["pairs"][0]
    assert len(b["suggested_overall_color_mul"]) == 3 and b["target"] == "green" and "pass" in b
    # the same image against itself is a zero gap and a unit multiplier
    r2 = run_tool("colour_match", str(photo), str(photo), "--box", "50,150,100,80", "--no-warm-offset", cwd=str(tmp_path))
    assert r2.returncode == 0 and "mul [1.0, 1.0, 1.0]" in r2.stdout, r2.stdout


def test_colour_match_render_only(tmp_path):
    render = _scene(tmp_path / "render.png", tuple(rule("look", "measure", "green_photo_median_rgb")))
    r = run_tool("colour_match", "--render-only", str(render), "--box", "50,150,100,80", "--target", "green", "--no-warm-offset", cwd=str(tmp_path))
    assert r.returncode == 0, r.stderr + r.stdout
    assert "PASS" in r.stdout


def test_brightness_check_prints_green_luma(tmp_path):
    lo, hi = rule("look", "brightness", "green_luma")
    g = int((lo + hi) / 2)
    img = _scene(tmp_path / "shot.png", (g, g, g), sky=(40, 60, 120))
    r = run_tool("brightness_check", str(img), "--json", str(tmp_path / "b.json"), cwd=str(tmp_path))
    assert r.returncode == 0, r.stderr
    assert "green luma" in r.stdout and re.search(r"green luma\s+[\d.]+\s+(PASS|FAIL)", r.stdout), r.stdout
    out = json.load(open(tmp_path / "b.json"))
    assert len(out) == 1 and out[0]["green_ok"] is True
    dark = _scene(tmp_path / "dark.png", (10, 10, 10), sky=(40, 60, 120))
    r = run_tool("brightness_check", str(dark), cwd=str(tmp_path))
    assert "green luma" in r.stdout and "FAIL" in r.stdout


def test_new_course_bootstrap(tmp_path):
    dest = tmp_path / "courses"; dest.mkdir()
    r = run_tool("new_course", "TestCourse", "--square", "2000", "--unity", "C:/GSP/TestCourse_Project", "--dest", str(dest), cwd=str(tmp_path))
    assert r.returncode == 0, r.stderr
    folder = dest / "TestCourse"
    assert (folder / "course.json").exists() and (folder / "CLAUDE.md").exists() and (folder / "README.md").exists()
    for sub in ("terrain", "imagery", "greenkeeper", "course_photos", "trees", "progress", "automation"):
        assert (folder / sub).is_dir(), sub
    c = json.load(open(folder / "course.json"))
    assert c["name"] == "TestCourse" and c["frame"]["square_m"] == 2000 and c["holes"] == 18
    assert c["paths"]["gkd"] == "greenkeeper/TestCourse.GKD" and c["paths"]["unity_project"] == "C:/GSP/TestCourse_Project"
    assert os.path.normpath(c["paths"]["toolkit"]) == os.path.normpath(ROOT)
    md = open(folder / "CLAUDE.md").read()
    assert "TestCourse" in md and "{{" not in md
    assert (folder / "automation" / "TestCourse.json").exists()
    # the new folder is a valid course for the loader
    r = run_tool("rules", "greens.collar.width_m", course_json=str(folder / "course.json"), cwd=str(tmp_path))
    assert r.returncode == 0 and float(r.stdout.strip()) == rule("greens", "collar", "width_m")
    # refuses to overwrite without --force
    r = run_tool("new_course", "TestCourse", "--unity", "C:/GSP/X", "--dest", str(dest), cwd=str(tmp_path))
    assert r.returncode != 0 and "exists" in (r.stdout + r.stderr)
