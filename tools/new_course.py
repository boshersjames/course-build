"""Bootstrap a new course folder next to the course-build checkout.

    python tools/new_course.py NewCourse --square 2000 --unity "C:/GSP/NewCourse_Project" [--display "New Course GC"] [--dest ..]

Creates <dest>/<Name>/ with course.json, CLAUDE.md, README.md, the folder layout and an automation config; copies the shared
Unity editor set into <unity>/Assets/Editor if that folder exists. Nothing is fetched; run coursekit afterwards.
"""
import argparse, json, os, shutil, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FOLDERS = ["terrain", "imagery", "svg", "greenkeeper", "course_photos", "trees", "scripts", "automation", "progress", "flyovers"]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("name"); ap.add_argument("--display"); ap.add_argument("--square", type=int, default=2000)
    ap.add_argument("--unity", required=True, help="Unity project folder for this course")
    ap.add_argument("--dest", default=os.path.dirname(ROOT), help="parent folder (default: beside course-build)")
    ap.add_argument("--holes", type=int, default=18); ap.add_argument("--force", action="store_true")
    a = ap.parse_args()

    dest = os.path.join(os.path.abspath(a.dest), a.name)
    if os.path.exists(dest) and not a.force:
        sys.exit("%s exists; --force to write into it" % dest)
    os.makedirs(dest, exist_ok=True)
    for f in FOLDERS:
        os.makedirs(os.path.join(dest, f), exist_ok=True)

    with open(os.path.join(ROOT, "templates", "course.template.json"), encoding="utf-8") as fh:
        c = json.load(fh)
    unity = a.unity.replace("\\", "/")
    c["name"] = a.name; c["display_name"] = a.display or a.name; c["holes"] = a.holes
    c["frame"]["square_m"] = a.square
    c["paths"].update({
        "unity_project": unity, "unity_editor_dir": unity + "/Assets/Editor", "automation_output": unity + "/AutomationOutput",
        "gkd": "greenkeeper/%s.GKD" % a.name, "heightmap": "terrain/heightmap_2049.raw",
        "flyovers": "~/Documents/%s/flyovers" % a.name, "backups": "~/Documents/%s/Backups" % a.name, "toolkit": ROOT.replace("\\", "/")})
    with open(os.path.join(dest, "course.json"), "w", encoding="utf-8") as fh:
        json.dump(c, fh, indent=2)

    with open(os.path.join(ROOT, "templates", "CLAUDE.course.md"), encoding="utf-8") as fh:
        md = fh.read().replace("{{NAME}}", a.name).replace("{{TOOLKIT}}", ROOT).replace("{{UNITY}}", a.unity)
    with open(os.path.join(dest, "CLAUDE.md"), "w", encoding="utf-8") as fh:
        fh.write(md)
    with open(os.path.join(dest, "README.md"), "w", encoding="utf-8") as fh:
        fh.write("# %s\n\nFrame: %d m square. Survey: (fill in). Holes: %d. visit: planned\n\nTo check:\n- \n" % (a.name, a.square, a.holes))

    cfg_src = os.path.join(ROOT, "automation", "configs", "TierraRejada.json")
    if os.path.exists(cfg_src):
        with open(cfg_src, encoding="utf-8") as fh:
            txt = fh.read().replace("TierraRejada", a.name).replace("Tierra Rejada", c["display_name"])
        with open(os.path.join(dest, "automation", a.name + ".json"), "w", encoding="utf-8") as fh:
            fh.write(txt)

    ed = os.path.join(a.unity, "Assets", "Editor")
    if os.path.isdir(os.path.join(a.unity, "Assets")):
        os.makedirs(ed, exist_ok=True)
        n = 0
        for f in os.listdir(os.path.join(ROOT, "unity", "shared")):
            if f.endswith(".cs"):
                shutil.copy2(os.path.join(ROOT, "unity", "shared", f), ed); n += 1
        print("copied %d editor tools to %s" % (n, ed))
    else:
        print("Unity project not found at %s; copy unity/shared/*.cs into Assets/Editor later" % a.unity)

    print("created", dest)
    print("next: fill course.json frame + lidar survey, then coursekit.py lidar/svg/card; grep the tools for 2000/3000/Tierra before the first run")


if __name__ == "__main__":
    main()
