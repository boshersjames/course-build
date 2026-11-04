using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Course Flyover > Generate Cinematic Path
// A premiere/commercial drone path for one hole from the course data in the scene (GKD tees, aims, pins, green centre; OPCD bunker and
// green meshes; terrain + course colliders for ground clearance). Numbers come from Assets/CourseBuild/rules/flyovers.json "cinematic".
//  1. Spline: centripetal Catmull-Rom through control points: start start_behind_tee_m behind the active tee at start_height_m, a reveal
//     point over the fairway at reveal_height_m, a dip to bunker_dip_height_m over each bunker complex (clusters of bunker meshes within
//     bunker_corridor_m of the hole line, in the middle of the hole) with entry/exit points, an approach point approach_short_m short of the
//     green at approach_height_m, then a 360 deg orbit around the green complex (radius = green radius + margin, never smaller than
//     v_orbit / max yaw rate so the limits hold), ending on the approach side.
//  2. Framing: look-at tracks lookahead_m ahead along the arc; inside the orbit it blends to the pin. FOV reveal_fov_deg when high and far,
//     approach_fov_deg near the green and in the orbit, blended over fov_blend_m.
//  3. Speed: S-curve ease-in off the tee (ease_in_start_factor -> 1 over ease_in_fraction of the arc, smootherstep), straight_boost on
//     straight sections longer than straight_min_len_m, decel to orbit speed_factor over decel_lead_m before the orbit, orbit at that speed.
//  4. Guard: per-frame yaw / pitch / roll rates; the worst 1 s window vs limits; terrain and course-mesh clearance vs min_clear_m (the
//     path is lifted where it would cut the ground, logged). Roll is a bank = bank_per_yaw_rate * yaw rate, capped at bank_max_deg (0 = none).
//  5. Output: AutomationOutput/flyovers/H<NN>_cinematic_<stamp>.json (control points, per-frame pos / lookAt / yaw / pitch / roll / fov /
//     speed / ground clearance, max rates, flags), a CourseAutomation shot list H<NN>_cinematic_shots.json (one shot per frame, so the
//     existing batch renderer makes the frames), and an AnimationClip Assets/CourseBuild/flyovers/H<NN>_cinematic.anim (position, rotation
//     euler, FOV curves) for a camera in the editor.
// Menu: Course Flyover > Generate Cinematic Path (fields: hole, tee, fps, width, height).
// Bridge: invoke -Tool CommercialFlyoverPath -Method RunPathGeneration -Set @{ hole = 10; tee = "Blue" }
// NOT YET COMPILED ON THE PC (written in the cloud 2026-11-03).
public class CommercialFlyoverPath : EditorWindow
{
    // ---- rules subset (JsonUtility) ----
    [Serializable] public class Orbit { public float radius_margin_m = 18, height_m = 10, degrees = 360, speed_factor = 0.5f, decel_lead_m = 30; public string look_at = "pin"; }
    [Serializable] public class Speed { public float base_m_s = 22, ease_in_fraction = 0.2f, ease_in_start_factor = 0.15f, straight_boost = 1.4f, straight_min_len_m = 80, straight_max_turn_deg_per_m = 0.25f; }
    [Serializable] public class Limits { public float max_yaw_deg_s = 12, max_pitch_deg_s = 8, bank_max_deg = 6, bank_per_yaw_rate = 0.4f, min_clear_m = 3; }
    [Serializable] public class Cinematic
    {
        public float start_behind_tee_m = 15, start_height_m = 12, reveal_height_m = 28, reveal_fov_deg = 42, approach_fov_deg = 52, fov_blend_m = 40;
        public float bunker_dip_height_m = 3.5f, bunker_dip_entry_m = 40, bunker_cluster_m = 25, bunker_corridor_m = 28; public float[] bunker_along_range = { 0.25f, 0.85f };
        public float lookahead_m = 30, approach_short_m = 40, approach_height_m = 15; public Orbit orbit = new Orbit(); public Speed speed = new Speed(); public Limits limits = new Limits(); public int fps = 25; public float sample_m = 0.5f;
    }
    [Serializable] public class FlyRules { public Cinematic cinematic = new Cinematic(); }
    [Serializable] class P { public float x, y, z; }
    [Serializable] class Tee { public string TeeType; public P Position; public bool Enabled; }
    [Serializable] class Pin { public string Day; public P Position; }
    [Serializable] class Hole { public int HoleNumber; public int Par; public Tee[] Tees; public Pin[] Pins; }
    [Serializable] class G { public Hole[] Holes; }
    [Serializable] public class Paths { public string gkd, course_repo_abs; }
    [Serializable] public class CourseJson { public string name; public Paths paths = new Paths(); }

    const string CB = "Assets/CourseBuild";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public static int hole = 1, width = 1920, height = 1080; public static string tee = "Blue"; public static int pinIndex = 0;
    string last = ""; Vector2 scroll;

    [MenuItem("Course Flyover/Generate Cinematic Path")]
    static void Open() { GetWindow<CommercialFlyoverPath>("Cinematic path"); }
    void OnGUI()
    {
        hole = EditorGUILayout.IntField("Hole", hole); tee = EditorGUILayout.TextField("Tee colour", tee); pinIndex = EditorGUILayout.IntField("Pin index", pinIndex);
        width = EditorGUILayout.IntField("Width", width); height = EditorGUILayout.IntField("Height", height);
        if (GUILayout.Button("Generate")) last = RunPathGeneration();
        scroll = EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(last); EditorGUILayout.EndScrollView();
    }

    // ---------- helpers ----------
    static string ProjectDir() { return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); }
    static T LoadJson<T>(string p, T fb) where T : class { return File.Exists(p) ? JsonUtility.FromJson<T>(File.ReadAllText(p).TrimStart('﻿')) : fb; }
    static string ResolveGkd(CourseJson c)
    {
        if (c == null || c.paths == null || string.IsNullOrEmpty(c.paths.gkd)) return null;
        if (Path.IsPathRooted(c.paths.gkd)) return c.paths.gkd;
        foreach (var r in new[] { c.paths.course_repo_abs, Path.Combine(ProjectDir(), "..") }) { if (string.IsNullOrEmpty(r)) continue; var p = Path.GetFullPath(Path.Combine(r, c.paths.gkd)); if (File.Exists(p)) return p; }
        return null;
    }
    static float Ground(float x, float z)
    {
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 800, z), Vector3.down, 1600)) { if (h.collider.GetComponentInParent<LODGroup>()) continue; if (h.point.y > best) best = h.point.y; }
        if (best == float.MinValue) foreach (var t in Terrain.activeTerrains) { var o = t.GetPosition(); var s = t.terrainData.size; if (x >= o.x && z >= o.z && x <= o.x + s.x && z <= o.z + s.z) best = Mathf.Max(best, t.SampleHeight(new Vector3(x, 0, z)) + o.y); }
        return best == float.MinValue ? 0 : best;
    }
    static float GroundMax(float x, float z, float r) { float m = float.MinValue; for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) m = Mathf.Max(m, Ground(x + i * r, z + j * r)); return m; }
    static float Smootherstep(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }
    static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }

    // centripetal Catmull-Rom (alpha 0.5): no loops or overshoot between uneven control points
    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        Func<float, Vector3, Vector3, float> tj = (ti, a, b) => ti + Mathf.Pow(Vector3.Distance(a, b), 0.5f);
        float t0 = 0, t1 = tj(t0, p0, p1), t2 = tj(t1, p1, p2), t3 = tj(t2, p2, p3); if (t2 - t1 < 1e-4f) return p1;
        float tt = Mathf.Lerp(t1, t2, t);
        Vector3 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1, a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2, a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
        Vector3 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2, b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
        return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
    }
    static List<Vector3> SampleSpline(List<Vector3> cp, float step)
    {
        var pts = new List<Vector3>(); if (cp.Count < 2) return pts;
        var ext = new List<Vector3> { cp[0] + (cp[0] - cp[1]) }; ext.AddRange(cp); ext.Add(cp[cp.Count - 1] + (cp[cp.Count - 1] - cp[cp.Count - 2]));
        for (int i = 1; i < ext.Count - 2; i++)
        {
            float L = Vector3.Distance(ext[i], ext[i + 1]); int n = Mathf.Max(2, Mathf.CeilToInt(L / step));
            for (int k = 0; k < n; k++) pts.Add(CatmullRom(ext[i - 1], ext[i], ext[i + 1], ext[i + 2], k / (float)n));
        }
        pts.Add(cp[cp.Count - 1]); return pts;
    }

    // ---------- run ----------
    public static string RunPathGeneration()
    {
        Physics.SyncTransforms();
        var R = LoadJson(CB + "/rules/flyovers.json", new FlyRules()).cinematic; var C = LoadJson(CB + "/course.json", new CourseJson()) ?? new CourseJson();
        var gkdPath = ResolveGkd(C); if (gkdPath == null) return "GKD not found: set paths.gkd (and paths.course_repo_abs) in " + CB + "/course.json";
        var g = JsonUtility.FromJson<G>(File.ReadAllText(gkdPath).TrimStart('﻿')); var H = g.Holes.FirstOrDefault(h => h.HoleNumber == hole); if (H == null) return "hole " + hole + " not in the GKD";
        Func<string, Vector3?> pos = k => { var t = H.Tees.FirstOrDefault(x => x.TeeType == k && x.Position != null && (x.Position.x != 0 || x.Position.z != 0)); return t == null ? (Vector3?)null : new Vector3(t.Position.x, t.Position.y, t.Position.z); };   // JsonUtility turns "Position": null into (0,0,0): the origin is never a real point (Tierra 10: a 4,023 m line through the corner, 2026-11-03)
        var teeP = pos(tee) ?? pos("Blue") ?? pos("White") ?? pos("Black"); var gc = pos("GreenCenterPoint"); if (teeP == null || gc == null) return "tee '" + tee + "' or green centre missing on hole " + hole;
        var line = new List<Vector3> { teeP.Value }; foreach (var k in new[] { "AimPoint1", "AimPoint2" }) { var a = pos(k); if (a != null) line.Add(a.Value); } line.Add(gc.Value);
        var pin = H.Pins != null && H.Pins.Length > 0 && H.Pins[Mathf.Clamp(pinIndex, 0, H.Pins.Length - 1)].Position != null && (H.Pins[Mathf.Clamp(pinIndex, 0, H.Pins.Length - 1)].Position.x != 0 || H.Pins[Mathf.Clamp(pinIndex, 0, H.Pins.Length - 1)].Position.z != 0) ? new Vector3(H.Pins[Mathf.Clamp(pinIndex, 0, H.Pins.Length - 1)].Position.x, H.Pins[Mathf.Clamp(pinIndex, 0, H.Pins.Length - 1)].Position.y, H.Pins[Mathf.Clamp(pinIndex, 0, H.Pins.Length - 1)].Position.z) : gc.Value;
        // hole-line arc length helpers
        var seg = new List<float> { 0 }; for (int i = 1; i < line.Count; i++) seg.Add(seg[i - 1] + Vector2.Distance(XZ(line[i - 1]), XZ(line[i]))); float holeLen = seg[seg.Count - 1];
        Func<float, Vector3> along = d => { d = Mathf.Clamp(d, 0, holeLen); for (int i = 1; i < line.Count; i++) if (d <= seg[i]) { float u = (d - seg[i - 1]) / Mathf.Max(seg[i] - seg[i - 1], 1e-3f); return Vector3.Lerp(line[i - 1], line[i], u); } return line[line.Count - 1]; };
        Func<Vector3, float> alongOf = p => { float best = 1e9f, bd = 0; for (int i = 1; i < line.Count; i++) { var a = XZ(line[i - 1]); var b = XZ(line[i]); var ab = b - a; float u = Mathf.Clamp01(Vector2.Dot(XZ(p) - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f)); float d = Vector2.Distance(XZ(p), a + ab * u); if (d < best) { best = d; bd = seg[i - 1] + u * ab.magnitude; } } return bd; };
        Func<Vector3, float> offLine = p => { float best = 1e9f; for (int i = 1; i < line.Count; i++) { var a = XZ(line[i - 1]); var b = XZ(line[i]); var ab = b - a; float u = Mathf.Clamp01(Vector2.Dot(XZ(p) - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f)); best = Mathf.Min(best, Vector2.Distance(XZ(p), a + ab * u)); } return best; };
        var log = new List<string>();
        // scene features: green radius from the nearest green mesh, bunker complexes from bunker meshes in the corridor
        var opcd = GameObject.Find("OPCD Meshes"); float greenR = 12f; var bunkerPts = new List<Vector3>();
        if (opcd)
        {
            foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(false))
            {
                if (!mf.sharedMesh) continue; var n = mf.name.ToLower(); var b = mf.GetComponent<Renderer>() ? mf.GetComponent<Renderer>().bounds : new Bounds();
                if (n.EndsWith("_green_-_mesh") && Vector2.Distance(XZ(b.center), XZ(gc.Value)) < 40) greenR = Mathf.Max(greenR, Mathf.Max(b.extents.x, b.extents.z));
                if (n.Contains("bunker") && offLine(b.center) <= R.bunker_corridor_m) { float f = alongOf(b.center) / Mathf.Max(holeLen, 1); if (f >= R.bunker_along_range[0] && f <= R.bunker_along_range[1]) bunkerPts.Add(b.center); }
            }
        }
        else log.Add("warning: no 'OPCD Meshes' root; green radius default " + greenR + " m, no bunker dips");
        var clusters = new List<Vector3>(); var used = new bool[bunkerPts.Count];
        for (int i = 0; i < bunkerPts.Count; i++) { if (used[i]) continue; var acc = new List<Vector3> { bunkerPts[i] }; used[i] = true; for (int j = i + 1; j < bunkerPts.Count; j++) if (!used[j] && Vector2.Distance(XZ(bunkerPts[i]), XZ(bunkerPts[j])) <= R.bunker_cluster_m) { acc.Add(bunkerPts[j]); used[j] = true; } clusters.Add(new Vector3(acc.Average(v => v.x), acc.Average(v => v.y), acc.Average(v => v.z))); }
        clusters.Sort((a, b) => alongOf(a).CompareTo(alongOf(b)));
        // control points
        var dir0 = (XZ(line[1]) - XZ(line[0])).normalized; var cps = new List<Vector3>(); var names = new List<string>();
        Action<Vector3, string> add = (p, nm) => { cps.Add(p); names.Add(nm); };
        var s0 = teeP.Value - new Vector3(dir0.x, 0, dir0.y) * R.start_behind_tee_m; add(new Vector3(s0.x, Ground(s0.x, s0.z) + R.start_height_m, s0.z), "start");
        float revealD = Mathf.Min(holeLen * 0.3f, Mathf.Max(60f, holeLen * 0.3f)); var rv = along(revealD); add(new Vector3(rv.x, GroundMax(rv.x, rv.z, 20) + R.reveal_height_m, rv.z), "reveal");
        float lastD = revealD;
        foreach (var c in clusters)
        {
            float d = alongOf(c); if (d - lastD < R.bunker_dip_entry_m + 10) continue;
            var e = along(d - R.bunker_dip_entry_m); add(new Vector3(e.x, GroundMax(e.x, e.z, 15) + R.start_height_m, e.z), "dip-entry");
            add(new Vector3(c.x, GroundMax(c.x, c.z, 6) + R.bunker_dip_height_m, c.z), "bunker-dip");
            var x = along(Mathf.Min(d + R.bunker_dip_entry_m, holeLen - R.approach_short_m - 10)); add(new Vector3(x.x, GroundMax(x.x, x.z, 15) + R.start_height_m, x.z), "dip-exit"); lastD = d + R.bunker_dip_entry_m;
        }
        var ap = along(holeLen - R.approach_short_m); add(new Vector3(ap.x, GroundMax(ap.x, ap.z, 15) + R.approach_height_m, ap.z), "approach");
        // orbit: radius from green size, never below v_orbit / max yaw rate; start on the approach side, go round, end there
        float vOrbit = R.speed.base_m_s * R.orbit.speed_factor; float radius = Mathf.Max(greenR + R.orbit.radius_margin_m, vOrbit / (R.limits.max_yaw_deg_s * Mathf.Deg2Rad));
        float ang0 = Mathf.Atan2(ap.x - gc.Value.x, ap.z - gc.Value.z); int nOrb = Mathf.Max(8, Mathf.RoundToInt(R.orbit.degrees / 30f)); int orbitStart = cps.Count;
        for (int k = 0; k <= nOrb; k++) { float a = ang0 + Mathf.Deg2Rad * R.orbit.degrees * k / nOrb; var p = new Vector3(gc.Value.x + Mathf.Sin(a) * radius, 0, gc.Value.z + Mathf.Cos(a) * radius); add(new Vector3(p.x, GroundMax(p.x, p.z, 8) + R.orbit.height_m, p.z), k == 0 ? "orbit-in" : k == nOrb ? "orbit-out" : "orbit"); }
        log.Add(string.Format(CI, "hole {0} par {1}: tee {2}, line {3:F0} m, green r {4:F1} m, {5} bunker complex(es) dipped, orbit r {6:F1} m at {7:F1} m/s ({8} control points)", hole, H.Par, tee, holeLen, greenR, clusters.Count, radius, vOrbit, cps.Count));
        // sample, clearance lift, arc length
        var pts = SampleSpline(cps, R.sample_m); int lifted = 0;
        for (int i = 0; i < pts.Count; i++) { float gmax = GroundMax(pts[i].x, pts[i].z, 4); if (pts[i].y < gmax + R.limits.min_clear_m) { pts[i] = new Vector3(pts[i].x, gmax + R.limits.min_clear_m, pts[i].z); lifted++; } }
        var arc = new List<float> { 0 }; for (int i = 1; i < pts.Count; i++) arc.Add(arc[i - 1] + Vector3.Distance(pts[i - 1], pts[i])); float total = arc[arc.Count - 1];
        float orbitArc = 0; { var oin = cps[orbitStart]; int bi = 0; float bd = 1e9f; for (int i = 0; i < pts.Count; i++) { float d = Vector3.Distance(pts[i], oin); if (d < bd) { bd = d; bi = i; } } orbitArc = arc[bi]; }
        // straight sections: turn per metre below threshold over >= straight_min_len_m
        var turnPerM = new float[pts.Count];
        for (int i = 1; i < pts.Count - 1; i++) { var a = XZ(pts[i] - pts[i - 1]); var b = XZ(pts[i + 1] - pts[i]); float ang = Vector2.Angle(a, b); turnPerM[i] = ang / Mathf.Max(Vector3.Distance(pts[i - 1], pts[i + 1]) / 2, 1e-3f); }
        var straight = new bool[pts.Count]; int runStart = -1;
        for (int i = 0; i <= pts.Count; i++)
        {
            bool s = i < pts.Count && turnPerM[i] <= R.speed.straight_max_turn_deg_per_m && arc[i] < orbitArc - R.orbit.decel_lead_m;
            if (s && runStart < 0) runStart = i; if (!s && runStart >= 0) { if (arc[i - 1] - arc[runStart] >= R.speed.straight_min_len_m) for (int k = runStart; k < i; k++) straight[k] = true; runStart = -1; }
        }
        // speed profile along the arc
        Func<float, float> speedAt = d =>
        {
            float v = R.speed.base_m_s; int i = Mathf.Clamp(arc.BinarySearch(d) < 0 ? ~arc.BinarySearch(d) : arc.BinarySearch(d), 0, pts.Count - 1);
            float easeIn = Smootherstep(d / Mathf.Max(total * R.speed.ease_in_fraction, 1)); v *= Mathf.Lerp(R.speed.ease_in_start_factor, 1f, easeIn);
            if (straight[i]) v *= R.speed.straight_boost;
            if (d >= orbitArc) v = R.speed.base_m_s * R.orbit.speed_factor; else if (d >= orbitArc - R.orbit.decel_lead_m) v = Mathf.Lerp(v, R.speed.base_m_s * R.orbit.speed_factor, Smootherstep((d - (orbitArc - R.orbit.decel_lead_m)) / R.orbit.decel_lead_m));
            return Mathf.Max(v, 1f);
        };
        Func<float, Vector3> posAt = d => { d = Mathf.Clamp(d, 0, total); int i = arc.BinarySearch(d); if (i < 0) i = ~i; i = Mathf.Clamp(i, 1, pts.Count - 1); float u = (d - arc[i - 1]) / Mathf.Max(arc[i] - arc[i - 1], 1e-4f); return Vector3.Lerp(pts[i - 1], pts[i], u); };
        // frames
        float dt = 1f / R.fps; var frames = new List<string>(); var shots = new List<string>(); var yaws = new List<float>(); var pitches = new List<float>(); var rolls = new List<float>();
        var posCurveX = new AnimationCurve(); var posCurveY = new AnimationCurve(); var posCurveZ = new AnimationCurve(); var rotX = new AnimationCurve(); var rotY = new AnimationCurve(); var rotZ = new AnimationCurve(); var fovCurve = new AnimationCurve();
        float d_ = 0, tf = 0; int fi = 0; float prevYaw = float.NaN, prevPitch = 0, minClear = 1e9f;
        while (d_ <= total && fi < 20000)
        {
            var p = posAt(d_); bool inOrbit = d_ >= orbitArc; var ahead = posAt(Mathf.Min(d_ + R.lookahead_m, total));
            Vector3 lookAt = inOrbit ? (R.orbit.look_at == "pin" ? pin : gc.Value) : ahead;
            if (!inOrbit && d_ > orbitArc - R.orbit.decel_lead_m) lookAt = Vector3.Lerp(ahead, pin, Smootherstep((d_ - (orbitArc - R.orbit.decel_lead_m)) / R.orbit.decel_lead_m));
            float distGreen = Vector2.Distance(XZ(p), XZ(gc.Value)); float hAbove = p.y - Ground(p.x, p.z); minClear = Mathf.Min(minClear, hAbove);
            float fovT = Smootherstep((hAbove - 12f) / 16f) * (1f - Smootherstep(1f - Mathf.Clamp01((distGreen - radius - 20f) / R.fov_blend_m)));   // high & far -> wide
            float fov = Mathf.Lerp(R.approach_fov_deg, R.reveal_fov_deg, fovT); if (inOrbit) fov = R.approach_fov_deg;
            var fwd = (lookAt - p); float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg; float pitch = -Mathf.Atan2(fwd.y, new Vector2(fwd.x, fwd.z).magnitude) * Mathf.Rad2Deg;
            float yawRate = float.IsNaN(prevYaw) ? 0 : Mathf.DeltaAngle(prevYaw, yaw) / dt; float pitchRate = float.IsNaN(prevYaw) ? 0 : (pitch - prevPitch) / dt;
            float roll = Mathf.Clamp(-R.limits.bank_per_yaw_rate * yawRate, -R.limits.bank_max_deg, R.limits.bank_max_deg);
            yaws.Add(yawRate); pitches.Add(pitchRate); rolls.Add(roll); float v = speedAt(d_);
            frames.Add(string.Format(CI, "{{\"f\":{0},\"t\":{1:F3},\"d\":{2:F2},\"pos\":[{3:F2},{4:F2},{5:F2}],\"lookAt\":[{6:F2},{7:F2},{8:F2}],\"yaw\":{9:F2},\"pitch\":{10:F2},\"roll\":{11:F2},\"fov\":{12:F1},\"speed\":{13:F2},\"clear\":{14:F2},\"orbit\":{15}}}", fi, tf, d_, p.x, p.y, p.z, lookAt.x, lookAt.y, lookAt.z, yaw, pitch, roll, fov, v, hAbove, inOrbit ? "true" : "false"));
            shots.Add(string.Format(CI, "{{\"name\":\"H{0:00}_cin_{1:0000}\",\"pos\":[{2:F3},{3:F3},{4:F3}],\"lookAt\":[{5:F3},{6:F3},{7:F3}],\"aboveGround\":false,\"fov\":{8:F2}}}", hole, fi, p.x, p.y, p.z, lookAt.x, lookAt.y, lookAt.z, fov));
            posCurveX.AddKey(tf, p.x); posCurveY.AddKey(tf, p.y); posCurveZ.AddKey(tf, p.z); rotX.AddKey(tf, pitch); rotY.AddKey(tf, yaw); rotZ.AddKey(tf, roll); fovCurve.AddKey(tf, fov);
            prevYaw = yaw; prevPitch = pitch; d_ += v * dt; tf += dt; fi++;
        }
        // worst 1 s window of the rates
        int win = R.fps; float maxYaw = 0, maxPitch = 0;
        for (int i = 0; i + win <= yaws.Count; i++) { float sy = 0, sp = 0; for (int k = i; k < i + win; k++) { sy += yaws[k] * dt; sp += pitches[k] * dt; } maxYaw = Mathf.Max(maxYaw, Mathf.Abs(sy)); maxPitch = Mathf.Max(maxPitch, Mathf.Abs(sp)); }
        bool rateOk = maxYaw <= R.limits.max_yaw_deg_s && maxPitch <= R.limits.max_pitch_deg_s; bool clearOk = minClear >= R.limits.min_clear_m - 0.05f;
        log.Add(string.Format(CI, "{0} frames, {1:F1} s at {2} fps, arc {3:F0} m; worst 1 s yaw {4:F1} deg (limit {5}), pitch {6:F1} deg (limit {7}); min clearance {8:F1} m ({9} samples lifted); max bank {10:F1} deg -> {11}",
            fi, tf, R.fps, total, maxYaw, R.limits.max_yaw_deg_s, maxPitch, R.limits.max_pitch_deg_s, minClear, lifted, rolls.Count > 0 ? rolls.Max(Mathf.Abs) : 0, rateOk && clearOk ? "PASS" : "FAIL"));
        // outputs
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); string dir = ProjectDir() + "/AutomationOutput/flyovers"; Directory.CreateDirectory(dir);
        var cpJson = string.Join(",", cps.Select((c, i) => string.Format(CI, "{{\"name\":\"{0}\",\"pos\":[{1:F2},{2:F2},{3:F2}]}}", names[i], c.x, c.y, c.z)).ToArray());
        File.WriteAllText(dir + "/H" + hole.ToString("00") + "_cinematic_" + stamp + ".json", "{" + string.Format(CI, "\"hole\":{0},\"tee\":\"{1}\",\"fps\":{2},\"frames\":{3},\"seconds\":{4:F2},\"arc_m\":{5:F1},\"orbit_radius_m\":{6:F1},\"max_yaw_deg_s\":{7:F2},\"max_pitch_deg_s\":{8:F2},\"min_clear_m\":{9:F2},\"lifted\":{10},\"pass\":{11},\"control_points\":[{12}],\"frames_data\":[{13}]", hole, tee, R.fps, fi, tf, total, radius, maxYaw, maxPitch, minClear, lifted, rateOk && clearOk ? "true" : "false", cpJson, string.Join(",", frames.ToArray())) + "}");
        File.WriteAllText(dir + "/H" + hole.ToString("00") + "_cinematic_shots.json", "{\"course\":\"" + (C.name ?? "course") + "\",\"saveScene\":false,\"steps\":[],\"inspect\":[],\"teeShots\":false,\"greenShots\":false,\"shotWidth\":" + width + ",\"shotHeight\":" + height + ",\"shots\":[" + string.Join(",", shots.ToArray()) + "]}");
        var clip = new AnimationClip(); clip.frameRate = R.fps;
        clip.SetCurve("", typeof(Transform), "localPosition.x", posCurveX); clip.SetCurve("", typeof(Transform), "localPosition.y", posCurveY); clip.SetCurve("", typeof(Transform), "localPosition.z", posCurveZ);
        clip.SetCurve("", typeof(Transform), "localEulerAngles.x", rotX); clip.SetCurve("", typeof(Transform), "localEulerAngles.y", rotY); clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rotZ); clip.SetCurve("", typeof(Camera), "field of view", fovCurve);
        if (!AssetDatabase.IsValidFolder(CB)) AssetDatabase.CreateFolder("Assets", "CourseBuild"); if (!AssetDatabase.IsValidFolder(CB + "/flyovers")) AssetDatabase.CreateFolder(CB, "flyovers");
        var clipPath = CB + "/flyovers/H" + hole.ToString("00") + "_cinematic.anim"; AssetDatabase.CreateAsset(clip, clipPath); AssetDatabase.SaveAssets();
        log.Add("wrote " + dir + "/H" + hole.ToString("00") + "_cinematic_" + stamp + ".json, _cinematic_shots.json (batch render config), " + clipPath);
        var txt = string.Join("\n", log.ToArray()); Debug.Log("CommercialFlyoverPath:\n" + txt); return txt;
    }
}
