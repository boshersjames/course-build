using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Course Checks > Greens: all-18 report
// Course-agnostic version of the Tierra checkers (TierraNight1031.EdgeSurvey / ColVsRender / CrownsOver / PlacementGate, TierraPinArea).
// Read-only. Runs every check from course-build-greens "All-18 checks before every install" on every active green and writes
//   <project>/AutomationOutput/checks/greens_all18.csv   one row per hole: hole, green mesh, max step cm, max lip cm, collider dy cm,
//                                                          tiers ok, crown overhang m, pins failing, usable %, valid areas, pass
//   <project>/AutomationOutput/checks/greens_all18.md    the same as a table, FAIL rows first
//   <project>/AutomationOutput/checks/edge_survey.csv     every edge point (green,x,z,step,lip,stepAt,surfaces)
//   <project>/AutomationOutput/checks/pin_area.csv        every GKD pin (hole,day,x,z,green,worst_deg,edge_m,flags)
//   <project>/AutomationOutput/checks/grid_heights_h<NN>.csv  0.5 m collider heights over each green (fall-direction vs LiDAR is done
//                                                          by tools/green_report.py --grid on the PC or in the cloud)
// Inputs:
//   Assets/CourseBuild/course.json    a copy of the course's course.json (paths.gkd is read; relative to the course repo folder given
//                                     in paths.course_repo_abs, else next to the project).  Run-CourseAutomation.ps1 -SyncTools copies it.
//   Assets/CourseBuild/rules/greens.json   the toolkit rule file; every threshold below comes from it (JsonUtility reads the subset).
// Nothing here is course specific: green meshes are found by the OPCD name pattern and matched to GKD holes by green centre.
// NOT YET COMPILED ON THE PC (written in the cloud 2026-11-03, desk-checked against the live Assets/Editor scripts, see DESKCHECK.md):
// first run, fix any compile message, then commit the fix to course-build.
public class CourseGreenChecks : EditorWindow
{
    // ---------- rule subset (mirrors rules/greens.json; unknown keys are ignored by JsonUtility) ----------
    [Serializable] public class EdgeSurveyRule { public float sample_along_m = 1f, profile_from_m = -0.5f, profile_to_m = 2f, step_m = 0.05f, max_step_m = 0.01f, max_lip_m = 0.02f; public bool bunker_rims_exempt = true; }
    [Serializable] public class FallRule { public float grid_m = 0.5f; }
    [Serializable] public class CrownRule { public float block_at_m = 1f, clearance_m = 1f; public int directions = 16; public float[] radii = { 0f, 0.33f, 0.66f, 1f }; }
    [Serializable] public class Checks { public EdgeSurveyRule edge_survey = new EdgeSurveyRule(); public float collider_vs_render_max_dy_m = 0.01f; public FallRule fall_direction = new FallRule(); public CrownRule crown_overhang = new CrownRule(); }
    [Serializable] public class PinRule { public float min_from_edge_m = 3f, max_lie_angle_deg = 1.7f, probe_radius_m = 0.3f, ring1_m = 0.45f, ring2_m = 0.9f; public int ring_points = 8, min_valid_areas = 4; public float min_area_spacing_m = 3f, usable_grid_m = 0.75f, usable_inset_m = 1.2f; }
    [Serializable] public class Tiers { public string green = "GSPgreen", collar = "GSPfairway", next_band = "GSPrough"; }
    [Serializable] public class GreensRules { public Checks checks = new Checks(); public PinRule pins = new PinRule(); public Tiers tiers = new Tiers(); }
    // ---------- course.json subset ----------
    [Serializable] public class Paths { public string gkd, course_repo_abs, automation_output; }
    [Serializable] public class CourseJson { public string name; public int holes = 18; public Paths paths = new Paths(); }
    // ---------- GKD subset ----------
    [Serializable] class P { public float x, y, z; }
    [Serializable] class Pin { public string Day; public P Position; }
    [Serializable] class Tee { public string TeeType; public P Position; public bool Enabled; }
    [Serializable] class Hole { public int HoleNumber; public Tee[] Tees; public Pin[] Pins; }
    [Serializable] class G { public Hole[] Holes; }

    const string CB = "Assets/CourseBuild";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public string holesFilter = "";
    public bool writeGrids = true;
    string lastResult = "";
    Vector2 scroll;

    [MenuItem("Course Checks/Greens: all-18 report")]
    static void Open() { GetWindow<CourseGreenChecks>("Green checks"); }

    void OnGUI()
    {
        GUILayout.Label("All-18 green checks (course-build-greens). Read-only.", EditorStyles.boldLabel);
        holesFilter = EditorGUILayout.TextField("Holes (blank = all)", holesFilter);
        writeGrids = EditorGUILayout.Toggle("Write 0.5 m height grids", writeGrids);
        if (GUILayout.Button("Run")) lastResult = Run(holesFilter, writeGrids);
        scroll = EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(lastResult); EditorGUILayout.EndScrollView();
    }

    // entry for the automation bridge: invoke -Tool CourseGreenChecks -Method RunAll
    public static string RunAll() { return Run("", true); }

    static T LoadJson<T>(string path, T fallback) where T : class
    {
        if (!File.Exists(path)) { Debug.LogWarning("CourseGreenChecks: " + path + " missing, using defaults"); return fallback; }
        return JsonUtility.FromJson<T>(File.ReadAllText(path).TrimStart('\uFEFF'));
    }

    static string ProjectDir() { return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); }

    static string ResolveGkd(CourseJson c)
    {
        if (c.paths == null || string.IsNullOrEmpty(c.paths.gkd)) return null;
        if (Path.IsPathRooted(c.paths.gkd)) return c.paths.gkd;
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(c.paths.course_repo_abs)) roots.Add(c.paths.course_repo_abs);
        roots.Add(Path.Combine(ProjectDir(), ".."));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), c.name ?? ""));
        foreach (var r in roots) { var p = Path.GetFullPath(Path.Combine(r, c.paths.gkd)); if (File.Exists(p)) return p; }
        return null;
    }

    // top OPCD collider hit at x,z: (collider name, y, physic material name)
    struct Top { public string name; public float y; public string phys; public bool ok; }
    static Top TopHit(Transform opcd, float x, float z)
    {
        var t = new Top { y = float.MinValue, name = "", phys = "" };
        foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200))
        {
            if (!h.collider.transform.IsChildOf(opcd) || h.collider.isTrigger) continue;
            if (h.collider.GetComponentInParent<LODGroup>()) continue;   // trees and shrub chunks are not ground: the ball rolls under them
            var pmn = h.collider.sharedMaterial ? h.collider.sharedMaterial.name : "";
            if (pmn == "GSPleaves" || pmn == "GSPtimber") continue;      // plant physics on a prop or shrub collider: not the ground either
            if (h.point.y > t.y) { t.y = h.point.y; t.name = h.collider.name; t.phys = h.collider.sharedMaterial ? h.collider.sharedMaterial.name : ""; t.ok = true; }
        }
        return t;
    }

    static string Surface(string colliderName)
    {
        var n = colliderName.ToLower();
        if (n.Contains("_green_-_mesh") || n.Contains("green_-_blend")) return "green";
        if (n.Contains("collar")) return "collar";
        if (n.Contains("_tee_-_mesh") || n.Contains("tee_-_blend")) return "tee";
        if (n.Contains("fairway")) return "fairway";
        if (n.Contains("bunker")) return "bunker";
        if (n.Contains("water")) return "water";
        return "rough";
    }

    static List<int[]> BoundaryEdges(Vector3[] w, int[] t)
    {
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[w.Length];
        for (int i = 0; i < w.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(weld[t[i + e]], weld[t[i + (e + 1) % 3]]); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        return cnt.Where(kv => kv.Value == 1).Select(kv => new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) }).ToList();
    }

    static float DistToEdges(List<int[]> edges, Vector3[] w, Vector2 q)
    {
        float best = float.MaxValue;
        foreach (var e in edges)
        {
            Vector2 a = new Vector2(w[e[0]].x, w[e[0]].z), b = new Vector2(w[e[1]].x, w[e[1]].z); var ab = b - a; float L2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / L2); best = Mathf.Min(best, (q - (a + ab * u)).magnitude);
        }
        return best;
    }

    // GreenKeeper lie: worst |asin(dz/r)| over N/E/S/W probes, 0.1 deg; pin area = worst over 17 points
    static float Lie(Transform opcd, float x, float z, float r)
    {
        float c = TopHit(opcd, x, z).y, worst = 0;
        foreach (var d in new[] { new Vector2(r, 0), new Vector2(-r, 0), new Vector2(0, r), new Vector2(0, -r) })
        { float y = TopHit(opcd, x + d.x, z + d.y).y; float a = Mathf.Round(Mathf.Asin(Mathf.Clamp((y - c) / r, -1, 1)) * Mathf.Rad2Deg * 10) / 10f; worst = Mathf.Max(worst, Mathf.Abs(a)); }
        return worst;
    }
    static float PinArea(Transform opcd, float x, float z, PinRule P)
    {
        float w = Lie(opcd, x, z, P.probe_radius_m);
        foreach (var rr in new[] { P.ring1_m, P.ring2_m }) for (int k = 0; k < P.ring_points; k++) { float a = k * 2 * Mathf.PI / P.ring_points; w = Mathf.Max(w, Lie(opcd, x + Mathf.Cos(a) * rr, z + Mathf.Sin(a) * rr, P.probe_radius_m)); }
        return w;
    }

    class Row { public int hole; public string green = ""; public float maxStep, maxLip, colDy, crown; public int stepPts, lipPts; public string stepAt = ""; public bool tiersOk = true; public string tiersNote = ""; public int pins, pinsFail, areas; public float usable; public List<string> notes = new List<string>(); public bool pass; }

    public static string Run(string holesFilter, bool writeGrids)
    {
        Physics.SyncTransforms();
        var R = LoadJson(CB + "/rules/greens.json", new GreensRules()); var C = LoadJson(CB + "/course.json", new CourseJson());
        var E = R.checks.edge_survey; var P = R.pins; var CR = R.checks.crown_overhang;
        var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no 'OPCD Meshes' root in the scene";
        var opcd = opcdGo.transform;
        var gkdPath = ResolveGkd(C); if (gkdPath == null) return "GKD not found: set paths.gkd (and paths.course_repo_abs) in " + CB + "/course.json";
        var g = JsonUtility.FromJson<G>(File.ReadAllText(gkdPath).TrimStart('\uFEFF'));
        var only = new HashSet<int>(holesFilter.Split(',').Where(s => s.Trim().Length > 0).Select(s => int.Parse(s.Trim())));
        string outDir = ProjectDir() + "/AutomationOutput/checks"; Directory.CreateDirectory(outDir);

        // green meshes and their centroids
        var greens = opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && f.name.Contains("_Green_-_Mesh") && !f.name.StartsWith("SKIRT_") && f.GetComponent<MeshCollider>()).ToList();   // render-only SKIRT_ copies have no collider and are not greens
        var world = greens.ToDictionary(f => f, f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray());
        var cen = greens.ToDictionary(f => f, f => new Vector2(world[f].Average(v => v.x), world[f].Average(v => v.z)));

        var rows = new List<Row>(); var edgeCsv = new List<string> { "green,x,z,step,lip,stepAt,surfaces" }; var pinCsv = new List<string> { "hole,day,x,z,green,worst_deg,edge_m,flags" };
        foreach (var h in g.Holes.OrderBy(ho => ho.HoleNumber))   // desk-check: lambda param renamed, loop var and lambda param shared a name (CS0136 before C# 8)
        {
            if (only.Count > 0 && !only.Contains(h.HoleNumber)) continue;
            var gc = h.Tees == null ? null : h.Tees.FirstOrDefault(t => t.TeeType == "GreenCenterPoint" && t.Position != null);
            var row = new Row { hole = h.HoleNumber }; rows.Add(row);
            if (gc == null || greens.Count == 0) { row.notes.Add("no green centre or no green meshes"); continue; }
            var gcp = new Vector2(gc.Position.x, gc.Position.z);
            var gm = greens.OrderBy(f => (cen[f] - gcp).sqrMagnitude).First(); float off = (cen[gm] - gcp).magnitude;
            if (off > 40) { row.notes.Add("nearest green mesh " + gm.name + " is " + off.ToString("F0", CI) + " m from the GKD centre"); continue; }
            row.green = gm.name; var w = world[gm]; var tris = gm.sharedMesh.triangles; var edges = BoundaryEdges(w, tris);
            var b = gm.GetComponent<Renderer>().bounds;

            // 1. edge survey
            var c2 = cen[gm]; float acc = 0; var steps = new List<float>(); var lips = new List<float>();
            foreach (var e in edges)
            {
                Vector2 a = new Vector2(w[e[0]].x, w[e[0]].z), bb = new Vector2(w[e[1]].x, w[e[1]].z); float L = (bb - a).magnitude; acc += L; if (acc < E.sample_along_m) continue; acc = 0;
                var m = (a + bb) / 2; var nrm = new Vector2(-(bb - a).y, (bb - a).x).normalized; if (Vector2.Dot(nrm, m - c2) < 0) nrm = -nrm;
                var ys = new List<float>(); var names = new List<string>(); bool sand = false;
                for (float d = E.profile_from_m; d <= E.profile_to_m + 1e-4f; d += E.step_m) { var q = m + nrm * d; var t = TopHit(opcd, q.x, q.y); ys.Add(t.ok ? t.y : float.NaN); names.Add(t.ok ? t.name : "-"); if (t.ok && Surface(t.name) == "bunker") sand = true; }
                if (ys.Any(float.IsNaN) || (sand && E.bunker_rims_exempt)) continue;
                float st = 0; int si = 0; for (int i = 1; i < ys.Count; i++) { float s = Mathf.Abs(ys[i] - ys[i - 1]); if (s > st) { st = s; si = i; } }
                float lp = 0; for (int i = 0; i < ys.Count; i++) { float chord = Mathf.Lerp(ys[0], ys[ys.Count - 1], i / (float)(ys.Count - 1)); lp = Mathf.Max(lp, ys[i] - chord); }
                steps.Add(st); lips.Add(lp);
                if (st >= steps.Max()) row.stepAt = (names[Mathf.Max(si - 1, 0)] + ">" + names[si]).Replace("_-_Mesh", "").Replace("Spline_", "");   // the two colliders the worst step crosses
                edgeCsv.Add(gm.name + "," + m.x.ToString("F2", CI) + "," + m.y.ToString("F2", CI) + "," + st.ToString("F3", CI) + "," + lp.ToString("F3", CI) + "," + (E.profile_from_m + si * E.step_m).ToString("F2", CI) + "," + string.Join(">", names.Distinct().Select(s => s.Replace("_-_Mesh", "").Replace("Spline_", "")).ToArray()));
            }
            if (steps.Count > 0) { row.maxStep = steps.Max(); row.maxLip = lips.Max(); row.stepPts = steps.Count(x => x > E.max_step_m); row.lipPts = lips.Count(x => x > E.max_lip_m); if (row.maxStep > E.max_step_m) row.notes.Add("worst step " + row.stepAt); }

            // 2. collider = render on green, blend, collar meshes near this green
            float worstDy = 0; int dup = 0;
            foreach (var f in opcd.GetComponentsInChildren<MeshFilter>(false).Where(mf => mf.sharedMesh && !mf.name.StartsWith("SKIRT_") && mf.GetComponent<Renderer>() && mf.GetComponent<Renderer>().bounds.Intersects(b) && (Surface(mf.name) == "green" || Surface(mf.name) == "collar")))   // desk-check: lambda param renamed
            {
                var mc = f.GetComponent<MeshCollider>(); if (!mc || !mc.sharedMesh) { row.notes.Add(f.name + " has no mesh collider"); continue; }
                var ra = f.sharedMesh.vertices; var ca = mc.sharedMesh.vertices;
                if (ra.Length == ca.Length) for (int i = 0; i < ra.Length; i++) worstDy = Mathf.Max(worstDy, Mathf.Abs(ca[i].y - ra[i].y));
                else { for (int i = 0; i < ra.Length; i += Mathf.Max(1, ra.Length / 2000)) { var wp = f.transform.TransformPoint(ra[i]); RaycastHit hh; if (mc.Raycast(new Ray(wp + Vector3.up * 5, Vector3.down), out hh, 20)) worstDy = Mathf.Max(worstDy, Mathf.Abs(hh.point.y - wp.y)); } }
            }
            // duplicate green colliders over the centre
            dup = Physics.RaycastAll(new Vector3(gcp.x, 600, gcp.y), Vector3.down, 1200).Count(hh => hh.collider.transform.IsChildOf(opcd) && Surface(hh.collider.name) == "green");
            row.colDy = worstDy; if (dup > 1) row.notes.Add(dup + " green colliders stacked at the centre");

            // 3. tiers: physics material at the green centre (green), 0.6 m outside the edge (collar), 2.5 m outside (first cut)
            var tg = TopHit(opcd, gcp.x, gcp.y); if (!tg.phys.Contains(R.tiers.green)) { row.tiersOk = false; row.tiersNote += "green=" + tg.phys + " "; }
            if (edges.Count > 0)
            {
                var e0 = edges[edges.Count / 2]; Vector2 a0 = new Vector2(w[e0[0]].x, w[e0[0]].z), b0 = new Vector2(w[e0[1]].x, w[e0[1]].z); var m0 = (a0 + b0) / 2; var n0 = new Vector2(-(b0 - a0).y, (b0 - a0).x).normalized; if (Vector2.Dot(n0, m0 - c2) < 0) n0 = -n0;
                var tc = TopHit(opcd, m0.x + n0.x * 0.6f, m0.y + n0.y * 0.6f); if (tc.ok && Surface(tc.name) == "collar" && !tc.phys.Contains(R.tiers.collar)) { row.tiersOk = false; row.tiersNote += "collar=" + tc.phys + " "; }
                if (tc.ok && Surface(tc.name) != "collar" && Surface(tc.name) != "bunker") { row.tiersOk = false; row.tiersNote += "no collar at 0.6 m (" + Surface(tc.name) + ") "; }
            }

            // 4. crowns over the green or collar (renderer bounds radius, 16 directions, 4 radii)
            float worstOver = 0; float x0 = b.min.x - 20, x1 = b.max.x + 20, z0 = b.min.z - 20, z1 = b.max.z + 20;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(rg => rg.activeInHierarchy && rg != opcdGo))   // desk-check: lambda param renamed (inner foreach declares r)
                foreach (var lg in root.GetComponentsInChildren<LODGroup>())
                {
                    var p = lg.transform.position; if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue; var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                    float Rr = Mathf.Max(0.3f, Mathf.Min(bb.extents.x, bb.extents.z)); var c = new Vector2(p.x, p.z);
                    for (int k = 0; k < CR.directions; k++) foreach (var fr in CR.radii) { if (fr == 0 && k > 0) continue; var q = c + new Vector2(Mathf.Cos(k * 2 * Mathf.PI / CR.directions), Mathf.Sin(k * 2 * Mathf.PI / CR.directions)) * Rr * fr; var t = TopHit(opcd, q.x, q.y); if (!t.ok) continue; var s = Surface(t.name); if (s == "green" || s == "collar") { float ov = Rr - (q - c).magnitude; if (ov > worstOver) { worstOver = ov; } } }
                }
            row.crown = worstOver;

            // 5. pins: GreenKeeper pin-area test + edge distance; usable % and valid areas on the green
            var pins = (h.Pins ?? new Pin[0]).Where(p => p.Position != null).ToList(); row.pins = pins.Count;
            foreach (var p in pins)
            {
                float ang = PinArea(opcd, p.Position.x, p.Position.z, P); var on = TopHit(opcd, p.Position.x, p.Position.z); float ed = DistToEdges(edges, w, new Vector2(p.Position.x, p.Position.z));
                var fl = new List<string>(); if (ang > P.max_lie_angle_deg + 1e-4f) fl.Add("STEEP"); if (ed < P.min_from_edge_m) fl.Add("EDGE"); if (on.name != gm.name) fl.Add("NOT ON " + gm.name);
                if (fl.Count > 0) row.pinsFail++;
                pinCsv.Add(string.Join(",", new[] { h.HoleNumber.ToString(), p.Day, p.Position.x.ToString("F2", CI), p.Position.z.ToString("F2", CI), gm.name, ang.ToString("F1", CI), ed.ToString("F1", CI), string.Join(" ", fl.ToArray()) }));
            }
            int tot = 0, okc = 0; var cand = new List<KeyValuePair<Vector2, float>>();
            for (float x = b.min.x; x <= b.max.x; x += P.usable_grid_m) for (float z = b.min.z; z <= b.max.z; z += P.usable_grid_m)
            {
                var t = TopHit(opcd, x, z); if (!t.ok || t.name != gm.name) continue; float ed = DistToEdges(edges, w, new Vector2(x, z)); if (ed < P.usable_inset_m) continue;
                tot++; float ang = PinArea(opcd, x, z, P); if (ang <= P.max_lie_angle_deg + 1e-4f) { okc++; if (ed >= P.min_from_edge_m) cand.Add(new KeyValuePair<Vector2, float>(new Vector2(x, z), ang)); }
            }
            row.usable = tot > 0 ? 100f * okc / tot : 0;
            var pick = new List<Vector2>(); foreach (var cnd in cand.OrderBy(q => q.Value).ThenBy(q => q.Key.x).ThenBy(q => q.Key.y)) if (pick.All(pp => (pp - cnd.Key).magnitude >= P.min_area_spacing_m)) pick.Add(cnd.Key);
            row.areas = pick.Count;

            // 6. height grid for the Python fall-direction check
            if (writeGrids)
            {
                var gr = new List<string> { "x,z,y,surface" }; float s = R.checks.fall_direction.grid_m;
                for (float x = b.min.x - 2; x <= b.max.x + 2; x += s) for (float z = b.min.z - 2; z <= b.max.z + 2; z += s) { var t = TopHit(opcd, x, z); if (t.ok) gr.Add(x.ToString("F2", CI) + "," + z.ToString("F2", CI) + "," + t.y.ToString("F4", CI) + "," + t.name); }
                File.WriteAllLines(outDir + "/grid_heights_h" + h.HoleNumber.ToString("00") + ".csv", gr.ToArray());
            }

            row.pass = row.maxStep <= E.max_step_m && row.maxLip <= E.max_lip_m && row.colDy <= R.checks.collider_vs_render_max_dy_m && row.tiersOk && row.crown < CR.block_at_m && row.pinsFail == 0 && row.areas >= P.min_valid_areas && dup <= 1;
        }

        File.WriteAllLines(outDir + "/edge_survey.csv", edgeCsv.ToArray()); File.WriteAllLines(outDir + "/pin_area.csv", pinCsv.ToArray());
        var csv = new List<string> { "hole,green,max_step_cm,max_lip_cm,step_pts_over,lip_pts_over,collider_dy_cm,tiers_ok,tiers_note,crown_overhang_m,pins,pins_failing,usable_pct,valid_areas,pass,notes" };
        var md = new List<string> { "# Green checks: all " + rows.Count + " holes (" + C.name + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")", "",
            "Rules: step <= " + (E.max_step_m * 100).ToString(CI) + " cm, lip <= " + (E.max_lip_m * 100).ToString(CI) + " cm, collider dy <= " + (R.checks.collider_vs_render_max_dy_m * 100).ToString(CI) + " cm, crown < " + CR.block_at_m.ToString(CI) + " m over green/collar, pins <= " + P.max_lie_angle_deg.ToString(CI) + " deg and >= " + P.min_from_edge_m.ToString(CI) + " m from the edge, >= " + P.min_valid_areas + " areas.", "",
            "| hole | green | step cm | lip cm | col dy cm | tiers | crown m | pins fail | usable % | areas | pass | notes |", "|---|---|---|---|---|---|---|---|---|---|---|---|" };
        foreach (var r in rows.OrderBy(q => q.pass).ThenBy(q => q.hole))   // desk-check: lambda param renamed
        {
            csv.Add(string.Join(",", new[] { r.hole.ToString(), r.green, (r.maxStep * 100).ToString("F1", CI), (r.maxLip * 100).ToString("F1", CI), r.stepPts.ToString(), r.lipPts.ToString(), (r.colDy * 100).ToString("F1", CI), r.tiersOk ? "1" : "0", r.tiersNote.Trim().Replace(',', ';'), r.crown.ToString("F2", CI), r.pins.ToString(), r.pinsFail.ToString(), r.usable.ToString("F0", CI), r.areas.ToString(), r.pass ? "PASS" : "FAIL", string.Join("; ", r.notes.ToArray()).Replace(',', ';') }));
            md.Add("| " + r.hole + " | " + r.green.Replace("_Green_-_Mesh", "") + " | " + (r.maxStep * 100).ToString("F1", CI) + " | " + (r.maxLip * 100).ToString("F1", CI) + " | " + (r.colDy * 100).ToString("F1", CI) + " | " + (r.tiersOk ? "ok" : r.tiersNote.Trim()) + " | " + r.crown.ToString("F2", CI) + " | " + r.pinsFail + "/" + r.pins + " | " + r.usable.ToString("F0", CI) + " | " + r.areas + " | " + (r.pass ? "PASS" : "**FAIL**") + " | " + string.Join("; ", r.notes.ToArray()) + " |");
        }
        File.WriteAllLines(outDir + "/greens_all18.csv", csv.ToArray()); File.WriteAllLines(outDir + "/greens_all18.md", md.ToArray());
        var head = rows.Count(r => !r.pass) + " of " + rows.Count + " holes FAIL -> " + outDir + "/greens_all18.md";
        Debug.Log("CourseGreenChecks: " + head); return head + "\n" + string.Join("\n", md.Skip(4).ToArray());
    }
}
