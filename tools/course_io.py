"""Shared loaders for course data, all resolved from course.json. No machine paths anywhere.

    from course_io import Heightmap, GKD, svg_outlines, automation_export
    C = course(); HM = Heightmap(C); HM.sample(x, z)          # Unity y (elevation - base) at Unity x, z
    G = GKD(C); G.hole(7).green_centre; G.hole(7).pins; G.limits()
    greens = svg_outlines(C, 'green')                          # {id: Nx2 array of Unity x,z}
"""
import json, os, re, sys
import numpy as np
from scipy import ndimage
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rules import course, rules  # noqa


class Heightmap:
    """The course LiDAR heightmap as Unity heights. Unity x = m east of the SW corner, z = m north, y = elevation - base."""

    def __init__(self, C, path=None):
        f = C["frame"]
        p = path or C.path("heightmap")
        n = f.get("heightmap_px", 2049)
        relief = f.get("relief_m")
        raw = np.fromfile(p, "<u2")
        if raw.size != n * n:
            raise ValueError("%s has %d samples, expected %d^2" % (p, raw.size, n))
        if relief is None:
            raise ValueError("course.json frame.relief_m is needed to scale the heightmap (Unity terrain height)")
        self.H = raw.reshape(n, n).astype(float) / 65535.0 * relief
        self.S = f["square_m"] / (n - 1)          # metres per sample
        self.base = f.get("base_elevation_m", 0.0)
        self.square = f["square_m"]
        self._spl = None

    def sample(self, x, z, order=1):
        scalar = np.ndim(x) == 0 and np.ndim(z) == 0
        x = np.atleast_1d(np.asarray(x, float)); z = np.atleast_1d(np.asarray(z, float))
        if order > 1:
            if self._spl is None or self._spl[0] != order:
                self._spl = (order, ndimage.spline_filter(self.H, order=order, mode="nearest"))
            v = ndimage.map_coordinates(self._spl[1], [z / self.S, x / self.S], order=order, mode="nearest", prefilter=False)
        else:
            v = ndimage.map_coordinates(self.H, [z / self.S, x / self.S], order=order, mode="nearest")
        return float(v[0]) if scalar else v

    def grid(self, x0, x1, z0, z1, cell, order=1):
        xs = np.arange(x0, x1, cell); zs = np.arange(z0, z1, cell)
        XX, ZZ = np.meshgrid(xs, zs)
        return XX, ZZ, self.sample(XX, ZZ, order)

    def elevation(self, x, z):
        return self.sample(x, z) + self.base


class Hole:
    def __init__(self, h):
        self.raw = h
        self.number = h["HoleNumber"]; self.par = h.get("Par"); self.enabled = h.get("Enabled", True)
        self.tees = {t["TeeType"]: t for t in h.get("Tees", [])}
        self.pins = [p for p in h.get("Pins", []) if p.get("Position")]

    def pos(self, tee_type):
        t = self.tees.get(tee_type)
        return None if not t or not t.get("Position") else np.array([t["Position"]["x"], t["Position"]["z"]])

    @property
    def green_centre(self):
        return self.pos("GreenCenterPoint")

    @property
    def aims(self):
        return [self.pos(k) for k in ("AimPoint1", "AimPoint2") if self.pos(k) is not None]

    def playing_tees(self):
        return {k: t for k, t in self.tees.items() if k not in ("AimPoint1", "AimPoint2", "GreenCenterPoint") and t.get("Enabled") and t.get("Position")}

    def back_tee(self, order=("Black", "Blue", "White", "Green", "Yellow", "Gold", "Red")):
        for k in order:
            if k in self.playing_tees():
                return self.pos(k)
        pt = self.playing_tees()
        return self.pos(next(iter(pt))) if pt else None

    def line(self):
        """tee -> aims -> green centre, as a list of xz points."""
        pts = [p for p in [self.back_tee()] + self.aims + [self.green_centre] if p is not None]
        return np.array(pts)

    def pin_xz(self):
        return np.array([[p["Position"]["x"], p["Position"]["z"]] for p in self.pins]) if self.pins else np.zeros((0, 2))


class GKD:
    def __init__(self, C=None, path=None):
        p = path or C.path("gkd")
        raw = open(p, "rb").read()
        self.bom = raw.startswith(b"\xef\xbb\xbf")
        self.text = raw[3:].decode("utf-8") if self.bom else raw.decode("utf-8")
        self.data = json.loads(self.text)
        self.path = p
        self.holes = [Hole(h) for h in self.data["Holes"]]

    def hole(self, n):
        for h in self.holes:
            if h.number == n:
                return h
        raise KeyError("hole %d" % n)

    def limits(self):
        R = rules()["ob"]["limits"]
        hz = self.data.get("Hazards", []); poob = self.data.get("pOOB", {})
        npts = poob.get("pointCount", len(poob.get("coords", []))) if isinstance(poob, dict) else len(poob)
        return {"hazards": len(hz), "hazards_max": R["hazards_max"], "hazards_ok": len(hz) <= R["hazards_max"],
                "pOOB_points": npts, "pOOB_max": R["pOOB_points_max"], "pOOB_ok": npts <= R["pOOB_points_max"],
                "hazardCount_field": self.data.get("hazardCount")}

    def write_text(self, new_text, path=None):
        json.loads(new_text)  # must still parse
        open(path or self.path, "wb").write((b"\xef\xbb\xbf" if self.bom else b"") + new_text.encode("utf-8"))


def _parse_subpaths(d):
    """List of sub-paths (each Nx2) from an M/L/Z path; coursekit writes absolute coordinates only."""
    subs = []; cur = []
    for cmd, body in re.findall(r"([MLZmlz])([^MLZmlz]*)", d):
        nums = re.findall(r"-?\d+(?:\.\d+)?", body); pts = [(float(nums[i]), float(nums[i + 1])) for i in range(0, len(nums) - 1, 2)]
        if cmd.upper() == "M":
            if len(cur) >= 3: subs.append(np.array(cur))
            cur = list(pts)
        elif cmd.upper() == "L":
            cur += pts
        else:
            if len(cur) >= 3: subs.append(np.array(cur))
            cur = []
    if len(cur) >= 3: subs.append(np.array(cur))
    return subs


def _parse_path_d(d):
    """Outer ring only (the largest sub-path by bounding box): for centroids and matching."""
    subs = _parse_subpaths(d)
    if not subs: return np.zeros((0, 2))
    return max(subs, key=lambda a: (a[:, 0].max() - a[:, 0].min()) * (a[:, 1].max() - a[:, 1].min()))


def svg_outlines(C, kind="green", path=None, flip_y=None):
    """Outlines from the coursekit SVG, in Unity x,z. The SVG viewBox is the course square in metres with y down; coursekit writes
    y = square - z. flip_y=None autodetects against the GKD green centres (greens only)."""
    p = path or C.path("course_svg")
    s = open(p, encoding="utf-8").read()
    sq = C["frame"]["square_m"]
    out = {}
    for m in re.finditer(r'<path[^>]*\bid="(%s_\d+)[^"]*"[^>]*\bd="([^"]+)"' % kind, s):
        pts = _parse_path_d(m.group(2))
        if len(pts) >= 3:
            out[m.group(1)] = pts
    if flip_y is None:
        flip_y = True
        try:
            G = GKD(C); gc = np.array([h.green_centre for h in G.holes if h.green_centre is not None])
            greens = out if kind == "green" else svg_outlines(C, "green", path, True)
            def score(flip):
                tot = 0
                for pts in greens.values():
                    c = pts.mean(0); c = np.array([c[0], sq - c[1]]) if flip else c
                    tot += np.min(np.hypot(*(gc - c).T))
                return tot
            flip_y = score(True) <= score(False)
        except Exception:
            pass
    if flip_y:
        out = {k: np.c_[v[:, 0], sq - v[:, 1]] for k, v in out.items()}
    return out


def svg_paths(C, kind="green", path=None, flip_y=None):
    """{id: matplotlib Path with every sub-path} in Unity x,z, so contains_points honours holes (even-odd). Same flip rule as svg_outlines."""
    from matplotlib.path import Path as MP
    p = path or C.path("course_svg"); s = open(p, encoding="utf-8").read(); sq = C["frame"]["square_m"]
    if flip_y is None:
        flip_y = True
        try:
            svg_outlines(C, "green", path, None)  # autodetect runs there; coursekit SVGs are y-down, so True
        except Exception: pass
    out = {}
    for m in re.finditer(r'<path[^>]*\bid="(%s_\d+)[^"]*"[^>]*\bd="([^"]+)"' % kind, s):
        subs = _parse_subpaths(m.group(2))
        if not subs: continue
        verts = []; codes = []
        for a in subs:
            a = np.c_[a[:, 0], sq - a[:, 1]] if flip_y else a
            verts += list(a) + [a[0]]; codes += [MP.MOVETO] + [MP.LINETO] * (len(a) - 1) + [MP.CLOSEPOLY]
        out[m.group(1)] = MP(np.array(verts), codes)
    return out


def match_outlines_to_holes(outlines, G, max_m=40):
    """{hole_number: outline} by nearest centroid to the GKD green centre."""
    res = {}
    for h in G.holes:
        if h.green_centre is None:
            continue
        best = min(outlines.items(), key=lambda kv: np.hypot(*(kv[1].mean(0) - h.green_centre)))
        d = np.hypot(*(best[1].mean(0) - h.green_centre))
        if d <= max_m:
            res[h.number] = best[1]
    return res


def automation_export(C, name, sub="fairways"):
    """A JSON the Unity automation wrote (greens.json, greens_info.json, edge_profiles.json...). None if absent on this machine."""
    p = os.path.join(C.path("automation_output"), sub, name)
    return json.load(open(p, encoding="utf-8-sig")) if os.path.exists(p) else None


def latest_automation_run(C):
    lt = os.path.join(C.path("automation_output"), "latest.txt")
    if not os.path.exists(lt):
        return None
    d = open(lt).read().strip()
    return d if os.path.isabs(d) else os.path.join(C.path("automation_output"), d)
