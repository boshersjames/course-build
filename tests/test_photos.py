"""8-12, 15: the photo / scan / drone index, the audit, coverage and the bucket sync."""
import ast, csv, json, os, re, subprocess, sys
import pytest
from conftest import run_tool, rule, shadow_course, have


def tierra_originals(tierra):
    d = os.path.join(tierra, "course_photos", "unsorted", "2026-10-29")
    files = sorted(f for f in os.listdir(d) if f.lower().endswith((".heic", ".jpg", ".jpeg"))) if os.path.isdir(d) else []
    if len(files) < 10:
        pytest.skip("Tierra originals not on this machine (%s)" % d)
    return d, files


@pytest.mark.slow
def test_photo_index_on_a_dump_copy(tierra, tmp_path):
    src, files = tierra_originals(tierra)
    dump = tmp_path / "dump" / "2026-10-29"; dump.mkdir(parents=True)
    for f in files:
        os.symlink(os.path.join(src, f), dump / f)
    out = tmp_path / "photos"
    r = run_tool("photo_index", "--dump", str(dump), "--out", str(out), "--no-display", cwd=tierra, timeout=240)
    assert r.returncode == 0, r.stderr
    rows = list(csv.DictReader(open(out / "index.csv", encoding="utf-8-sig")))
    stems = {os.path.splitext(f)[0] for f in files}
    assert len(rows) == len(stems) == 80
    assert all(r["lat"] and r["lon"] and r["x"] and r["z"] for r in rows), "every original carries GPS"
    assert all(r["hole"] for r in rows) and all(r["pose_conf"] for r in rows)
    assert all(r["batch"] == "2026-10-29" and r["source"] == "iphone" for r in rows)
    assert {r["gps_confidence"] for r in rows} <= {"high", "medium", "low"}
    m = re.search(r"per hole: (\{.*\})", r.stdout)
    assert m, r.stdout
    by_hole = ast.literal_eval(m.group(1))
    assert isinstance(by_hole, dict) and sum(by_hole.values()) == 80
    assert set(map(str, by_hole)) == {r["hole"] for r in rows}
    assert not os.listdir(out / "display"), "--no-display writes no JPEG copies"
    # the course folder was not written to
    assert not os.path.exists(os.path.join(src, "index.csv"))


@pytest.mark.slow
def test_scan_index_three_las(tierra, tmp_path):
    sdir = os.path.join(tierra, "course_photos", "scans")
    las = sorted(f for f in os.listdir(sdir) if f.lower().endswith(".las")) if os.path.isdir(sdir) else []
    if len(las) != 3:
        pytest.skip("expected 3 LAS scans under %s, found %d" % (sdir, len(las)))
    out = tmp_path / "scans"
    r = run_tool("scan_index", "--scans", sdir, "--out", str(out), "--no-sheets", cwd=tierra, timeout=300)
    assert r.returncode == 0, r.stderr
    rows = list(csv.DictReader(open(out / "index.csv", encoding="utf-8-sig")))
    assert len(rows) == 3 and "verdict" in rows[0] and "coverage_pct" in rows[0]
    assert {r["verdict"] for r in rows} <= {"ACCEPT", "RESCAN"}
    for r in rows:
        if r["verdict"] == "RESCAN":
            assert r["reason"]
    green10 = [r for r in rows if r["hole"] == "10" and r["kind"] == "green"]
    assert len(green10) == 1
    assert float(green10[0]["coverage_pct"]) >= rule("photos", "scan", "coverage_min_pct")
    assert sorted(f for f in os.listdir(out) if f.endswith("_grid.npz")) == sorted(os.path.splitext(f)[0] + "_grid.npz" for f in las)
    assert not any(f.endswith("_fit.png") for f in os.listdir(out)), "--no-sheets"


def test_audit_pairs_hole_10(tierra, fake_renders, tmp_path):
    out = tmp_path / "audit"
    r = run_tool("audit_pairs", "--renders", fake_renders, "--include-draft", "--holes", "10", "--out", str(out), cwd=tierra, timeout=300)
    assert r.returncode == 0, r.stderr
    rows = list(csv.DictReader(open(out / "index.csv", encoding="utf-8-sig")))
    assert rows and all(r["hole"] == "10" for r in rows)
    for c in ("gap_rg", "gap_bg", "colour_ok", "verdict", "notes", "render", "pose_conf"):
        assert c in rows[0]
    with_render = [r for r in rows if r["render"] != "missing"]
    assert with_render, "no indexed hole-10 photo had a stand-in render"
    gapmax = rule("photos", "audit", "colour_gap_max")
    for r in with_render:
        g1, g2 = float(r["gap_rg"]), float(r["gap_bg"])
        assert r["colour_ok"] == ("yes" if abs(g1) <= gapmax and abs(g2) <= gapmax else "no")
        assert os.path.exists(out / "10" / (r["img"] + ".jpg"))


def test_coverage_tierra(tierra, tmp_path):
    out = tmp_path / "coverage"
    r = run_tool("coverage", "--out", str(out), cwd=tierra, timeout=120)
    assert r.returncode == 0, r.stderr
    rows = list(csv.DictReader(open(out / "coverage.csv")))
    assert [r["hole"] for r in rows] == [str(h) for h in range(1, 19)]
    assert list(rows[0].keys()) == ["hole", "items", "covered", "coverage_pct", "captures", "missing", "manual_untagged"]
    by = {r["hole"]: r for r in rows}
    assert float(by["13"]["coverage_pct"]) >= rule("photos", "coverage", "min_pct")
    for r in rows:
        assert 0 <= float(r["coverage_pct"]) <= 100 and int(r["covered"]) <= int(r["items"])
    assert os.path.exists(out / "coverage.md")


@pytest.mark.skipif(not (have("ffmpeg") and have("ffprobe")), reason="ffmpeg not on PATH")
def test_drone_frames_segments_and_cut(tierra, tmp_path):
    photos = tmp_path / "course_photos"
    cj = shadow_course(tierra, str(tmp_path / "course"), photos_root=str(photos))
    video = tmp_path / "front_three.mp4"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=duration=10:size=320x240:rate=10", str(video)], check=True)
    r = run_tool("drone_frames", "segments", "--video", str(video), "--holes", "1-3", course_json=cj, cwd=str(tmp_path), timeout=120)
    assert r.returncode == 0, r.stderr
    seg = photos / "drone" / "segments_front_three.csv"
    assert seg.exists()
    segs = list(csv.DictReader(open(seg)))
    assert [s["hole"] for s in segs] == ["1", "2", "3"]
    assert all(float(s["end_s"]) > float(s["start_s"]) for s in segs) and float(segs[-1]["end_s"]) <= 10
    r = run_tool("drone_frames", "cut", "--video", str(video), "--segments", str(seg), course_json=cj, cwd=str(tmp_path), timeout=300)
    assert r.returncode == 0, r.stderr
    per = rule("photos", "drone", "frames_per_hole")
    frames = sorted((photos / "drone" / "by_hole").rglob("H*_t*.jpg"))
    sheets = sorted((photos / "drone" / "by_hole").rglob("contact_H*.jpg"))
    assert len(frames) == 3 * per == 18 and len(sheets) == 3
    assert {f.name.split("_")[2] for f in frames} == {"tee", "mid", "green"}
    assert not (photos / "index.csv").exists(), "no --index: the photo index is left alone"
    assert not os.path.exists(os.path.join(tierra, "course_photos", "drone", "segments_front_three.csv")), "nothing written into the course folder"


def test_photo_sync_check_without_credentials(tierra, tmp_path):
    env = {"HTTPS_PROXY": "http://127.0.0.1:9", "HTTP_PROXY": "http://127.0.0.1:9", "https_proxy": "http://127.0.0.1:9", "http_proxy": "http://127.0.0.1:9",
           "NO_PROXY": "", "no_proxy": "", "AWS_MAX_ATTEMPTS": "1", "AWS_RETRY_MODE": "standard",
           "R2_ACCESS_KEY_ID": "", "R2_SECRET_ACCESS_KEY": "", "AWS_ACCESS_KEY_ID": "", "AWS_SECRET_ACCESS_KEY": ""}
    r = run_tool("photo_sync", "check", cwd=tierra, env=env, timeout=120)
    assert "unsigned" in r.stdout or "set R2" in r.stdout, r.stdout + r.stderr
    assert "Traceback" not in r.stderr
    if r.returncode == 0:
        pytest.skip("bucket reachable; the no-credentials path was not exercised")
    assert "bucket check failed" in r.stdout + r.stderr


def test_photo_index_out_honoured_without_photos_root(tierra, tmp_path):
    src, files = tierra_originals(tierra)
    cj = shadow_course(tierra, str(tmp_path / "course"))
    c = json.load(open(cj)); del c["paths"]["photos_root"]; json.dump(c, open(cj, "w"))
    dump = tmp_path / "dump"; dump.mkdir()
    for f in files[:2]:
        os.symlink(os.path.join(src, f), dump / f)
    out = tmp_path / "out"
    r = run_tool("photo_index", "--dump", str(dump), "--out", str(out), "--no-display", course_json=cj, cwd=str(tmp_path), timeout=120)
    assert r.returncode == 0, r.stderr
    assert (out / "index.csv").exists(), r.stdout
