"""Load course-build rules and a course's course.json. Every tool reads numbers from here, never hard-codes them.

    from rules import rules, course
    R = rules()                      # dict of every rules/*.json keyed by element
    R["greens"]["pins"]["max_lie_angle_deg"]   -> 1.7
    C = course()                     # course.json found by walking up from cwd, or COURSE_JSON env var
    C.path("unity_project")          # resolved absolute path from course.json "paths"

A course may override any rule in its own course.json under "rule_overrides": {"greens": {"collar": {"width_m": 1.5}}}.
"""
import json, os, sys, copy, functools

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RULES_DIR = os.path.join(ROOT, "rules")


def _merge(a, b):
    for k, v in b.items():
        if isinstance(v, dict) and isinstance(a.get(k), dict):
            _merge(a[k], v)
        else:
            a[k] = v
    return a


@functools.lru_cache(maxsize=None)
def _base_rules():
    out = {}
    for f in sorted(os.listdir(RULES_DIR)):
        if f.endswith(".json") and not f.startswith("_"):
            with open(os.path.join(RULES_DIR, f), encoding="utf-8") as fh:
                out[f[:-5]] = json.load(fh)
    return out


def find_course_json(start=None):
    p = os.environ.get("COURSE_JSON")
    if p and os.path.exists(p):
        return os.path.abspath(p)
    d = os.path.abspath(start or os.getcwd())
    while True:
        c = os.path.join(d, "course.json")
        if os.path.exists(c):
            return c
        parent = os.path.dirname(d)
        if parent == d:
            return None
        d = parent


class Course(dict):
    def __init__(self, data, path):
        super().__init__(data)
        self.file = path
        self.dir = os.path.dirname(path)

    def path(self, key, default=None):
        """Resolve course.json["paths"][key]; relative entries are relative to the course folder."""
        v = self.get("paths", {}).get(key, default)
        if v is None:
            raise KeyError("course.json has no paths.%s" % key)
        v = os.path.expandvars(os.path.expanduser(v))
        return v if os.path.isabs(v) else os.path.normpath(os.path.join(self.dir, v))

    @property
    def square_m(self):
        return self["frame"]["square_m"]

    @property
    def heightmap_px(self):
        return self["frame"].get("heightmap_px", 2049)

    @property
    def m_per_sample(self):
        return self.square_m / (self.heightmap_px - 1)


def course(start=None):
    p = find_course_json(start)
    if not p:
        sys.exit("course.json not found (walk-up from %s); set COURSE_JSON" % (start or os.getcwd()))
    with open(p, encoding="utf-8") as fh:
        return Course(json.load(fh), p)


def rules(course_obj=None):
    R = copy.deepcopy(_base_rules())
    c = course_obj
    if c is None:
        try:
            c = course()
        except SystemExit:
            c = None
    if c and c.get("rule_overrides"):
        _merge(R, c["rule_overrides"])
    return R


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser(description="print a rule: rules.py greens.pins.max_lie_angle_deg")
    ap.add_argument("key", nargs="?")
    a = ap.parse_args()
    R = rules()
    if not a.key:
        print(json.dumps(R, indent=1)); sys.exit()
    v = R
    for k in a.key.split("."):
        v = v[k]
    print(json.dumps(v, indent=1) if isinstance(v, (dict, list)) else v)
