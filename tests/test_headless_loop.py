"""3-7: doctor, GKD checks, green report, flyover gate, gate report: the headless loop from the README, on real data."""
import csv, json, os, re, time
import pytest
from conftest import run_tool, rule, md_table_rows


def test_doctor_tierra(tierra):
    r = run_tool("doctor", cwd=tierra)
    assert r.returncode == 0, r.stdout + r.stderr
    assert "0 FAIL" in r.stdout and "heightmap size" in r.stdout and "gkd parses" in r.stdout


def test_doctor_moorpark(moorpark):
    r = run_tool("doctor", cwd=moorpark)
    assert r.returncode == 0, r.stdout + r.stderr
    assert "0 FAIL" in r.stdout


def test_doctor_fails_on_lfs_pointer(tmp_path):
    (tmp_path / "terrain").mkdir()
    ptr = "version https://git-lfs.github.com/spec/v1\noid sha256:" + "0" * 64 + "\nsize 8396802\n"
    assert len(ptr.encode()) == 132
    (tmp_path / "terrain" / "h.raw").write_text(ptr)
    (tmp_path / "course.json").write_text(json.dumps({
        "name": "Ptr", "frame": {"square_m": 2000, "heightmap_px": 2049, "sw_corner": [0, 0], "base_elevation_m": 0, "relief_m": 100, "crs": "EPSG:6340"},
        "paths": {"toolkit": "nowhere", "heightmap": "terrain/h.raw", "gkd": "greenkeeper/Ptr.GKD"}}))
    r = run_tool("doctor", cwd=str(tmp_path))
    assert r.returncode == 1
    assert "LFS pointer" in r.stdout and "FAIL" in r.stdout


def test_gkd_check_reports_stimp(tierra):
    r = run_tool("gkd_tools", "check", cwd=tierra)
    assert r.returncode in (0, 1), r.stderr
    assert re.search(r"\d+ checks, \d+ failing", r.stdout)
    r2 = run_tool("gkd_tools", "check", "--all", cwd=tierra)
    assert "MaxStimp = %d" % rule("greens", "gkd", "MaxStimp") in r2.stdout
    if r.returncode == 1:
        assert "FAIL" in r.stdout


def test_gkd_distances_dry_run_does_not_write(tierra):
    gkd = json.load(open(os.path.join(tierra, "course.json")))["paths"]["gkd"]
    p = os.path.join(tierra, gkd); before = os.path.getmtime(p); baks = set(os.listdir(os.path.dirname(p)))
    r = run_tool("gkd_tools", "distances", cwd=tierra)
    assert r.returncode == 0, r.stderr
    assert "tees off by" in r.stdout
    assert os.path.getmtime(p) == before and set(os.listdir(os.path.dirname(p))) == baks, "dry run must not touch the GKD"


GREEN_COLS = ["hole", "outline", "area_m2", "relief_m", "slope_mean_pct", "slope_p90_pct", "slope_max_pct", "pct_over_3", "pct_over_4",
              "pins", "pins_failing", "pin_angles", "usable_pct", "valid_areas", "flag_slope", "pass", "note"]


def run_green_report(tierra, out):
    t0 = time.time()
    r = run_tool("green_report", "--holes", "7,10", "--smooth", "0.6", "--out", out, cwd=tierra, timeout=120)
    assert r.returncode == 0, r.stderr
    assert time.time() - t0 < 60
    return r


def test_green_report_two_holes(tierra, tmp_path):
    out = str(tmp_path / "green_report")
    run_green_report(tierra, out)
    rows = list(csv.DictReader(open(os.path.join(out, "green_report.csv"))))
    assert [r["hole"] for r in rows] == ["7", "10"]
    assert list(rows[0].keys()) == GREEN_COLS
    for r in rows:
        assert r["outline"] == "course SVG" and float(r["area_m2"]) > 100
        assert int(r["pins"]) >= 1
        angles = re.findall(r"[A-Za-z]{3}:(\d+\.\d)", r["pin_angles"])
        assert len(angles) == int(r["pins"]), r["pin_angles"]
        # a pin fails on angle or on edge distance, so angle failures are a lower bound
        assert int(r["pins_failing"]) >= sum(1 for a in angles if float(a) > rule("greens", "pins", "max_lie_angle_deg"))
    assert os.path.exists(os.path.join(out, "green_report.md"))


def run_flyover(course, out, extra=()):
    r = run_tool("flyover_gate", "--holes", "1,2", "--no-plots", "--end-fit", "--out", out, *extra, cwd=course, timeout=180)
    assert r.returncode == 0, r.stderr
    return r


def test_flyover_gate_two_holes(tierra, tmp_path):
    out = str(tmp_path / "flyover_gate")
    r = run_flyover(tierra, out)
    fg = json.load(open(os.path.join(out, "flyover_gate.json")))
    assert [h["hole"] for h in fg["holes"]] == [1, 2]
    for h in fg["holes"]:
        assert "ok" in h
    assert "inputs" in fg and fg["inputs"]["pin"] == 0
    md = open(os.path.join(out, "flyover_gate.md")).read()
    assert md.startswith("# Flyover gate") and len(md_table_rows(md)) == 2
    assert os.path.exists(os.path.join(out, "flyover_shots.json"))
    assert "provisional" not in r.stdout, "Tierra has a signs file"


def test_flyover_gate_moorpark_without_signs(moorpark, tmp_path):
    out = str(tmp_path / "flyover_gate")
    r = run_flyover(moorpark, out)
    assert "provisional" in r.stdout
    fg = json.load(open(os.path.join(out, "flyover_gate.json")))
    assert len(fg["holes"]) == 2


def test_gate_report_after_green_and_flyover(tierra, tmp_path):
    parent = tmp_path / "progress"
    run_green_report(tierra, str(parent / "green_report"))
    run_flyover(tierra, str(parent / "flyover_gate"))
    r = run_tool("gate_report", "--out", str(parent / "gate_report"), cwd=tierra, timeout=180)
    assert r.returncode in (0, 1), r.stderr
    md = open(parent / "gate_report" / "gate_report.md").read()
    assert md.startswith("# Gate report")
    rows = md_table_rows(md)
    assert len(rows) == 18
    assert "flyover" in md and "lidar-proxy" in md
    js = json.load(open(parent / "gate_report" / "gate_report.json"))
    assert set(js) == {"holes", "missing", "gkd_fail"}
    holes = js["holes"]
    assert len(holes) == 18
    by = {int(h["hole"]): h for h in holes}
    assert "flyover" in by[1]["source"] and "flyover" in by[2]["source"]
    assert "lidar-proxy" in by[7]["source"] and "lidar-proxy" in by[10]["source"]
    assert by[3]["status"] == "NO GATE DATA"
