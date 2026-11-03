// CourseCollarFlush: make every collar flush (course-build-greens, rules/greens.json collar.height = "flush").
//
// Generalised from TierraNight1031.CollarLaplace / SinkUnder / CollarRevert (Tierra, 11/1: BoJa "greens and collars act like bumpers").
// A collar mesh is any active MeshFilter under "OPCD Meshes" whose name contains "collar" (case-insensitive) and carries a MeshCollider.
// For each one: (1) every boundary vertex is pinned to the surface right beside it (first other OPCD collider rim_probe_m off the edge: green or
// green blend inside, approach / rough / bunker blend outside); (2) the interior is solved as a membrane between them (Laplace on the welded
// mesh), so there is no step at either edge and no lip inside (a membrane cannot rise above its rim); (3) every other turf mesh (not green,
// collar, bunker, water, hazard) with vertices under the collar footprint is sunk sink_under_in_m below the collar (sink_under_rim_m within
// sink_rim_band_m of the rim) so rough or blend never pokes through. Green meshes are never touched. New meshes are assets under
// Assets/CourseBuild/generated/collarflush, the MeshCollider follows, and COURSEBUILD_LOG keeps "collarflush|<mesh>|<old asset path>" so
// Revert puts the previous mesh back. Numbers from Assets/CourseBuild/rules/greens.json collar.flush_tool. Dry run unless Apply.
//
// Bridge: invoke -Tool CourseCollarFlush -Method DryRun | Apply | Revert | RelaxDryRun | RelaxApply (rims held, interior membrane; after CourseRimReset)   (optional -Set @{ only = "SPatch_collar1029_h7_-_Mesh" })
// Then run CourseGreenChecks.RunAll: step <= greens.checks.edge_survey.max_step_m on every green is the pass.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class CourseCollarFlush : EditorWindow
{
    [Serializable] public class FlushRule { public float rim_probe_m = 0.12f; public float weld_mm = 5f; public int laplace_iterations = 400; public float sink_under_rim_m = 0.005f, sink_under_in_m = 0.03f, sink_rim_band_m = 0.4f; public float rim_loop_min_frac = 0.25f; public bool conform_max = true; public float conform_probe_m = 0.12f; public float relax_max_move_m = 0.03f; }
    [Serializable] class Collar { public FlushRule flush_tool = new FlushRule(); }
    [Serializable] class GreensRules { public Collar collar = new Collar(); }
    const string CB = "Assets/CourseBuild"; const string GEN = CB + "/generated/collarflush"; const string LOG = "COURSEBUILD_LOG";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public static string only = "";   // bridge -Set: one collar mesh name, or "" for every collar
    public static float px = 0, pz = 0, pr = 0.6f;   // Probe: vertices of `only` within pr m of (px, pz)

    [MenuItem("Course Tools/Greens/Collar flush (dry run)")] static void MenuDry() { Debug.Log(DryRun()); }
    [MenuItem("Course Tools/Greens/Collar flush (apply)")] static void MenuApply() { Debug.Log(Apply()); }
    [MenuItem("Course Tools/Greens/Collar flush (revert all)")] static void MenuRevert() { Debug.Log(Revert()); }

    public static string DryRun() { return Run(false); }

    // read-only: the collar mesh's own vertices near a point, with the top-collider height at each (is the mesh one sheet? did the interior move?)
    public static string Probe()
    {
        Physics.SyncTransforms(); var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo || string.IsNullOrEmpty(only)) return "set only=<collar mesh>, px, pz";
        var mf = opcdGo.transform.GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == only); if (!mf) return "no " + only;
        var src = mf.sharedMesh; var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; var c = new Vector2(px, pz);
        var cnt = new Dictionary<long, int>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(t[i + e], t[i + (e + 1) % 3]); int n; cnt[k] = cnt.TryGetValue(k, out n) ? n + 1 : 1; }
        var rim = new HashSet<int>(); foreach (var kv in cnt) if (kv.Value == 1) { rim.Add((int)(kv.Key >> 32)); rim.Add((int)(kv.Key & 0xffffffff)); }
        var o = new List<string> { only + ": mesh " + src.name + " (" + AssetDatabase.GetAssetPath(src) + "), " + w.Length + " vertices, " + t.Length / 3 + " triangles, " + rim.Count + " unwelded rim vertices; collider mesh " + (mf.GetComponent<MeshCollider>() && mf.GetComponent<MeshCollider>().sharedMesh ? mf.GetComponent<MeshCollider>().sharedMesh.name : "none"), "i,x,z,y,rim,top_y,top_collider" };
        var near = Enumerable.Range(0, w.Length).Where(i => (new Vector2(w[i].x, w[i].z) - c).magnitude <= pr).OrderBy(i => (new Vector2(w[i].x, w[i].z) - c).magnitude).Take(60);
        foreach (var i in near)
        {
            float topY = float.NaN; string topN = "-";
            foreach (var h in Physics.RaycastAll(new Vector3(w[i].x, 600, w[i].z), Vector3.down, 1200)) { if (!h.collider.transform.IsChildOf(opcdGo.transform) || h.collider.GetComponentInParent<LODGroup>()) continue; if (float.IsNaN(topY) || h.point.y > topY) { topY = h.point.y; topN = h.collider.name; } }
            o.Add(i + "," + w[i].x.ToString("F3", CI) + "," + w[i].z.ToString("F3", CI) + "," + w[i].y.ToString("F4", CI) + "," + (rim.Contains(i) ? "rim" : "in") + "," + topY.ToString("F4", CI) + "," + topN);
        }
        return string.Join("\n", o.ToArray());
    }
    public static string Apply() { return Run(true); }

    // interior relax: rims stay exactly where they are (after CourseRimReset), the interior becomes a membrane between them
    public static string RelaxDryRun() { return Relax(false); }
    public static string RelaxApply() { return Relax(true); }

    public static string Relax(bool apply)
    {
        Physics.SyncTransforms(); var R = Rules(); var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no 'OPCD Meshes' root in the scene";
        var opcd = opcdGo.transform; var collars = Collars(opcd); if (collars.Count == 0) return "no collar meshes" + (string.IsNullOrEmpty(only) ? "" : " matching " + only);
        if (apply) EnsureFolders();
        var o = new List<string> { (apply ? "RELAX APPLY" : "RELAX DRY RUN") + ": " + collars.Count + " collar meshes, rims held, interior membrane, cap " + (R.relax_max_move_m * 100).ToString("F0", CI) + " cm" + (string.IsNullOrEmpty(only) ? "" : ", only " + only) };
        foreach (var mf in collars)
        {
            try
            {
                var src = mf.sharedMesh; var col = mf.GetComponent<Collider>(); var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
                float wq = 1000f / Mathf.Max(0.1f, R.weld_mm); var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N];
                for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * wq), Mathf.RoundToInt(w[i].z * wq)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
                var cnt = new Dictionary<long, int>(); var nb = new Dictionary<int, HashSet<int>>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++)
                {
                    int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; if (a == b) continue; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1;
                    HashSet<int> hs; if (!nb.TryGetValue(a, out hs)) nb[a] = hs = new HashSet<int>(); hs.Add(b); if (!nb.TryGetValue(b, out hs)) nb[b] = hs = new HashSet<int>(); hs.Add(a);
                }
                var bEdges = cnt.Where(kv => kv.Value == 1).Select(kv => new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) }).ToList();
                var rim = MajorLoops(bEdges, w, R.rim_loop_min_frac); var reps = wk.Values.ToList(); var y = reps.ToDictionary(r => r, r => w[r].y);
                var free = reps.Where(r => !rim.Contains(r)).ToList(); float resid = 0;
                for (int it = 0; it < R.laplace_iterations; it++) { resid = 0; foreach (var r in free) { HashSet<int> hs; if (!nb.TryGetValue(r, out hs) || hs.Count == 0) continue; float ny = hs.Average(q => y[weld[q]]); resid = Mathf.Max(resid, Mathf.Abs(ny - y[r])); y[r] = ny; } }
                int capped = 0;
                if (R.conform_max) foreach (var r in free)
                {
                    float lim = float.NaN;
                    foreach (var h in Physics.RaycastAll(new Vector3(w[r].x, w[r].y + 5, w[r].z), Vector3.down, 20)) { if (!h.collider.transform.IsChildOf(opcd) || h.collider == col || h.collider.GetComponentInParent<LODGroup>() || h.collider.name.ToLower().Contains("collar") || PinRank(h.collider.name) > 2) continue; if (float.IsNaN(lim) || h.point.y > lim) lim = h.point.y; }
                    if (!float.IsNaN(lim) && y[r] > lim) { y[r] = lim; capped++; }
                }
                float mx = 0, sum = 0; int moved = 0, over = 0; foreach (var r in free) { float d = Mathf.Abs(y[r] - w[r].y); if (d < 0.0005f) continue; moved++; sum += d; mx = Mathf.Max(mx, d); if (d > R.relax_max_move_m) over++; }
                string line = mf.name + ": " + rim.Count + " rim held, " + free.Count + " interior, " + moved + " moved (mean " + (moved > 0 ? sum / moved * 100 : 0).ToString("F1", CI) + " cm, largest " + (mx * 100).ToString("F1", CI) + " cm), " + capped + " held at the designed surface, residual " + (resid * 1000).ToString("F2", CI) + " mm";
                if (over > 0) { line += "  <-- " + over + " interior vertices would move over the cap: not applied (a rim is wrong, not the interior)"; o.Add(line); continue; }
                o.Add(line); if (!apply || moved == 0) continue;
                var nv = new Vector3[N]; for (int i = 0; i < N; i++) { var p = w[i]; p.y = y[weld[i]]; nv[i] = mf.transform.InverseTransformPoint(p); }
                var nm = SaveMesh(src, nv, mf.name + "_relax_" + DateTime.Now.ToString("HHmmss"), "_rx"); Swap(mf, nm, "collarflush");
            }
            catch (Exception e) { o.Add(mf.name + ": FAILED " + e.Message); }
        }
        if (apply) { AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); o.Add("now CourseGreenChecks.RunAll and edge_profiles.py; Revert puts every collar back"); }
        var txt = string.Join("\n", o.ToArray()); string outDir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/AutomationOutput/checks"; Directory.CreateDirectory(outDir); File.WriteAllText(outDir + "/collar_relax.txt", txt); return txt;
    }

    public static FlushRule Rules()
    {
        var rp = CB + "/rules/greens.json";
        if (!File.Exists(rp)) return new FlushRule();
        var g = JsonUtility.FromJson<GreensRules>(File.ReadAllText(rp).TrimStart('﻿')); return g != null && g.collar != null && g.collar.flush_tool != null ? g.collar.flush_tool : new FlushRule();
    }

    static List<MeshFilter> Collars(Transform opcd)
    {
        return opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && f.name.ToLower().Contains("collar") && !f.name.ToLower().Contains("_rough_-_mesh") && !f.name.ToLower().Contains("_fairway_-_mesh") && !f.name.ToLower().Contains("_green_-_mesh") && !f.name.StartsWith("SKIRT_") && f.GetComponent<MeshCollider>() && (string.IsNullOrEmpty(only) || f.name == only)).ToList();   // a rough/fairway/green spline named collar is not a collar (rules greens.collar.rim_reset.collar_name_excludes)
    }

    public static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(CB)) AssetDatabase.CreateFolder("Assets", "CourseBuild");
        if (!AssetDatabase.IsValidFolder(CB + "/generated")) AssetDatabase.CreateFolder(CB, "generated");
        if (!AssetDatabase.IsValidFolder(GEN)) AssetDatabase.CreateFolder(CB + "/generated", "collarflush");
    }

    public static void Swap(MeshFilter f, Mesh nm, string kind)
    {
        var src = f.sharedMesh; var log = GameObject.Find(LOG) ?? new GameObject(LOG);
        new GameObject(kind + "|" + f.name + "|" + AssetDatabase.GetAssetPath(src) + "|" + src.name).transform.SetParent(log.transform, false);
        var mc = f.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; }
        f.sharedMesh = nm; EditorUtility.SetDirty(f);
    }

    public static Mesh SaveMesh(Mesh src, Vector3[] verts, string assetName, string suffix)
    {
        var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + suffix; nm.vertices = verts; nm.RecalculateNormals(); nm.RecalculateBounds();
        var ap = GEN + "/" + assetName + ".asset"; if (AssetDatabase.GetAssetPath(src) == ap) ap = ap.Replace(".asset", "_" + DateTime.Now.ToString("HHmmss") + ".asset");
        AssetDatabase.DeleteAsset(ap); AssetDatabase.CreateAsset(nm, ap); return nm;
    }

    public static string Run(bool apply)
    {
        Physics.SyncTransforms(); var R = Rules(); var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no 'OPCD Meshes' root in the scene";
        var opcd = opcdGo.transform; var collars = Collars(opcd); if (collars.Count == 0) return "no collar meshes (name contains 'collar', MeshCollider)" + (string.IsNullOrEmpty(only) ? "" : " matching " + only);
        if (apply) EnsureFolders();
        var o = new List<string> { (apply ? "APPLY" : "DRY RUN") + ": " + collars.Count + " collar meshes (rim probe " + R.rim_probe_m.ToString(CI) + " m, sink " + (R.sink_under_in_m * 100).ToString(CI) + " cm / " + (R.sink_under_rim_m * 1000).ToString(CI) + " mm within " + R.sink_rim_band_m.ToString(CI) + " m of the rim)" };
        foreach (var cm in collars)
        {
            try { o.Add(Laplace(opcd, cm, R, apply)); if (apply) { Physics.SyncTransforms(); o.Add(SinkUnder(opcd, cm, R)); } }
            catch (Exception e) { o.Add(cm.name + ": FAILED " + e.Message); }
        }
        if (apply) { AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); o.Add("now run CourseGreenChecks.RunAll; Revert puts every collar and sunk mesh back"); }
        var txt = string.Join("\n", o.ToArray());
        string outDir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/AutomationOutput/checks"; Directory.CreateDirectory(outDir); File.WriteAllText(outDir + "/collar_flush.txt", txt);
        return txt;
    }

    public static int PinRank(string n) { n = n.ToLower(); if (n.Contains("_green_-_mesh")) return 0; if (n.Contains("green") && n.Contains("blend")) return 1; if (n.Contains("fairway") || n.Contains("_tee_-_mesh") || n.Contains("apron")) return 2; return 3; }

    // boundary loops by total length; vertices on loops shorter than minFrac x the longest are not rim (seams, slivers)
    public static HashSet<int> MajorLoops(List<int[]> edges, Vector3[] w, float minFrac)
    {
        var adj = new Dictionary<int, List<int>>(); foreach (var e in edges) { List<int> l; if (!adj.TryGetValue(e[0], out l)) adj[e[0]] = l = new List<int>(); l.Add(e[1]); if (!adj.TryGetValue(e[1], out l)) adj[e[1]] = l = new List<int>(); l.Add(e[0]); }
        var seen = new HashSet<int>(); var loops = new List<KeyValuePair<float, List<int>>>();
        foreach (var start in adj.Keys)
        {
            if (seen.Contains(start)) continue; var comp = new List<int>(); var st = new Stack<int>(); st.Push(start); seen.Add(start);
            while (st.Count > 0) { var v = st.Pop(); comp.Add(v); foreach (var u in adj[v]) if (!seen.Contains(u)) { seen.Add(u); st.Push(u); } }
            float len = 0; foreach (var e in edges) if (comp.Contains(e[0]) && comp.Contains(e[1])) len += Vector3.Distance(w[e[0]], w[e[1]]);
            loops.Add(new KeyValuePair<float, List<int>>(len, comp));
        }
        if (loops.Count == 0) return new HashSet<int>(); float longest = loops.Max(l => l.Key); var keep = new HashSet<int>();
        foreach (var l in loops) if (l.Key >= longest * minFrac) foreach (var v in l.Value) keep.Add(v);
        return keep;
    }

    // raw vertex indices on the major rim loops (every raw vertex whose welded position is on a kept loop)
    public static HashSet<int> RimIndices(MeshFilter mf, float minFrac)
    {
        var src = mf.sharedMesh; var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N]; for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; if (a == b) continue; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        var bEdges = cnt.Where(kv => kv.Value == 1).Select(kv => new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) }).ToList();
        var reps = MajorLoops(bEdges, w, minFrac); var outIdx = new HashSet<int>(); for (int i = 0; i < N; i++) if (reps.Contains(weld[i])) outIdx.Add(i); return outIdx;
    }

    // world positions of a mesh's major rim vertices (welded), for the outside-band cap in SinkUnder
    static List<Vector3> RimPoints(MeshFilter mf, float minFrac)
    {
        var src = mf.sharedMesh; var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N]; for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; if (a == b) continue; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        var bEdges = cnt.Where(kv => kv.Value == 1).Select(kv => new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) }).ToList();
        return MajorLoops(bEdges, w, minFrac).Select(i => w[i]).ToList();
    }

    // boundary vertices pinned to the neighbouring surface, interior = membrane between them
    static string Laplace(Transform opcd, MeshFilter mf, FlushRule R, bool apply)
    {
        var src = mf.sharedMesh; var col = mf.GetComponent<Collider>(); var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
        float wq = 1000f / Mathf.Max(0.1f, R.weld_mm);
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N];
        for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * wq), Mathf.RoundToInt(w[i].z * wq)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); var nb = new Dictionary<int, HashSet<int>>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++)
        {
            int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; if (a == b) continue; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1;
            HashSet<int> s; if (!nb.TryGetValue(a, out s)) nb[a] = s = new HashSet<int>(); s.Add(b); if (!nb.TryGetValue(b, out s)) nb[b] = s = new HashSet<int>(); s.Add(a);
        }
        // rim = the long boundary loops only (inner and outer ring); short loops are seams or slivers inside the band and stay free
        var bEdges = cnt.Where(kv => kv.Value == 1).Select(kv => new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) }).ToList();
        var bnd = MajorLoops(bEdges, w, R.rim_loop_min_frac); int seamVerts = bEdges.SelectMany(e => e).Distinct().Count() - bnd.Count;
        var reps = wk.Values.ToList(); var y = reps.ToDictionary(r => r, r => w[r].y); var pinned = new HashSet<int>(); int unpinned = 0; float maxPin = 0;
        var bAdj = new Dictionary<int, List<int>>(); foreach (var e in bEdges) { if (!bnd.Contains(e[0]) || !bnd.Contains(e[1])) continue; List<int> l; if (!bAdj.TryGetValue(e[0], out l)) bAdj[e[0]] = l = new List<int>(); l.Add(e[1]); if (!bAdj.TryGetValue(e[1], out l)) bAdj[e[1]] = l = new List<int>(); l.Add(e[0]); }
        var ownCol = col as MeshCollider;
        foreach (var r in bnd)
        {
            List<int> bn; if (!bAdj.TryGetValue(r, out bn) || bn.Count < 2) { unpinned++; continue; }
            var tan = new Vector2(w[bn[1]].x - w[bn[0]].x, w[bn[1]].z - w[bn[0]].z).normalized; var nrm = new Vector2(-tan.y, tan.x); float best = float.NaN;
            foreach (var sgn in new[] { 1f, -1f })
            {
                var q = new Vector2(w[r].x, w[r].z) + nrm * R.rim_probe_m * sgn;
                RaycastHit own; if (ownCol && ownCol.Raycast(new Ray(new Vector3(q.x, w[r].y + 5, q.y), Vector3.down), out own, 20)) continue;   // this side is still the collar: probe the other way
                // the designed surface beside the rim, by class, never by height: green mesh, green blend, fairway / tee, then anything else (the rough is what gets sunk, not what we pin to)
                var hits = Physics.RaycastAll(new Vector3(q.x, w[r].y + 5, q.y), Vector3.down, 20).Where(h => h.collider.transform.IsChildOf(opcd) && h.collider != col && !h.collider.GetComponentInParent<LODGroup>() && !h.collider.isTrigger && !h.collider.name.ToLower().Contains("collar")).OrderBy(h => PinRank(h.collider.name)).ThenBy(h => h.distance).ToList();
                if (hits.Count == 0) continue; best = hits[0].point.y; break;
            }
            if (float.IsNaN(best)) { unpinned++; continue; } maxPin = Mathf.Max(maxPin, Mathf.Abs(best - w[r].y)); y[r] = best; pinned.Add(r);
        }
        var free = reps.Where(r => !pinned.Contains(r) && !bnd.Contains(r)).ToList();
        float resid = 0;
        for (int it = 0; it < R.laplace_iterations; it++) { resid = 0; foreach (var r in free) { HashSet<int> s; if (!nb.TryGetValue(r, out s) || s.Count == 0) continue; float ny = s.Average(q => y[weld[q]]); resid = Mathf.Max(resid, Mathf.Abs(ny - y[r])); y[r] = ny; } }
        // conform (max): no collar vertex above the designed surface at or beside it (green mesh, green blend, fairway, tee: never the rough, which is sunk instead)
        int capped = 0; float capMax = 0;
        if (R.conform_max) foreach (var r in reps)
        {
            float lim = float.NaN;
            foreach (var off in new[] { Vector2.zero, new Vector2(R.conform_probe_m, 0), new Vector2(-R.conform_probe_m, 0), new Vector2(0, R.conform_probe_m), new Vector2(0, -R.conform_probe_m) })
            {
                foreach (var h in Physics.RaycastAll(new Vector3(w[r].x + off.x, w[r].y + 5, w[r].z + off.y), Vector3.down, 20))
                {
                    if (!h.collider.transform.IsChildOf(opcd) || h.collider == col || h.collider.GetComponentInParent<LODGroup>() || PinRank(h.collider.name) > 2) continue;
                    if (float.IsNaN(lim) || h.point.y > lim) lim = h.point.y;   // the highest designed surface nearby is the ceiling
                }
            }
            if (!float.IsNaN(lim) && y[r] > lim) { capMax = Mathf.Max(capMax, y[r] - lim); y[r] = lim; capped++; }
        }
        float mx = 0, up = 0, inMean = 0; foreach (var r in reps) { float d = y[r] - w[r].y; mx = Mathf.Max(mx, Mathf.Abs(d)); up = Mathf.Max(up, d); } foreach (var r in free) inMean += Mathf.Abs(y[r] - w[r].y); inMean = free.Count > 0 ? inMean / free.Count : 0;
        string res = mf.name + ": " + reps.Count + " welded vertices (" + N + " raw), " + bnd.Count + " rim (" + seamVerts + " seam vertices left free), " + pinned.Count + " pinned (largest rim move " + (maxPin * 100).ToString("F1", CI) + " cm), " + unpinned + " rim left as is; " + free.Count + " interior, mean move " + (inMean * 100).ToString("F1", CI) + " cm, last-pass residual " + (resid * 1000).ToString("F2", CI) + " mm; conform cap on " + capped + " vertices (largest " + (capMax * 100).ToString("F1", CI) + " cm); overall up to " + (mx * 100).ToString("F1", CI) + " cm (max rise " + (up * 100).ToString("F1", CI) + " cm)";
        if (!apply) return res;
        var nv = new Vector3[N]; for (int i = 0; i < N; i++) { var p = w[i]; p.y = y[weld[i]]; nv[i] = mf.transform.InverseTransformPoint(p); }
        var nm = SaveMesh(src, nv, mf.name + "_flush", "_cf"); Swap(mf, nm, "collarflush"); return res;
    }

    // other turf meshes under the collar footprint sink below it
    static string SinkUnder(Transform opcd, MeshFilter cm, FlushRule R)
    {
        var all = opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList(); var cc = cm.GetComponent<MeshCollider>(); if (!cc) return cm.name + ": no MeshCollider, nothing sunk";
        var cb = cm.GetComponent<Renderer>().bounds; var o = new List<string>(); var rim = RimPoints(cm, R.rim_loop_min_frac); int nOut = 0;
        foreach (var f in all)
        {
            if (f == cm) continue; var ln = f.name.ToLower(); if (ln.Contains("green") || ln.Contains("collar") || ln.Contains("bunker") || ln.Contains("water") || ln.Contains("hazard") || f.name.StartsWith("SKIRT_")) continue;   // green mesh and green blend are the reference surface: never lowered
            var cbx = cb; cbx.Expand(R.sink_rim_band_m * 2); var rr = f.GetComponent<Renderer>(); if (!rr || !rr.bounds.Intersects(cbx)) continue;
            var src = f.sharedMesh; var w = src.vertices.Select(v => f.transform.TransformPoint(v)).ToArray(); int n = 0;
            for (int i = 0; i < w.Length; i++)
            {
                if (w[i].x < cb.min.x - R.sink_rim_band_m || w[i].x > cb.max.x + R.sink_rim_band_m || w[i].z < cb.min.z - R.sink_rim_band_m || w[i].z > cb.max.z + R.sink_rim_band_m) continue;
                RaycastHit h;
                if (!cc.Raycast(new Ray(new Vector3(w[i].x, cb.max.y + 5, w[i].z), Vector3.down), out h, cb.size.y + 20))
                {
                    // outside the collar but within the rim band: never above the nearest rim vertex (the proud rough edge the ball saw at the rim)
                    float bd = float.MaxValue, ry = 0; foreach (var rp in rim) { float d = (new Vector2(rp.x, rp.z) - new Vector2(w[i].x, w[i].z)).sqrMagnitude; if (d < bd) { bd = d; ry = rp.y; } }
                    if (bd <= R.sink_rim_band_m * R.sink_rim_band_m && w[i].y > ry - R.sink_under_rim_m) { w[i].y = ry - R.sink_under_rim_m; n++; nOut++; }
                    continue;
                }
                bool nearRim = false; for (int k = 0; k < 8 && !nearRim; k++) { RaycastHit h2; var q = new Vector3(w[i].x + Mathf.Cos(k * Mathf.PI / 4) * R.sink_rim_band_m, cb.max.y + 5, w[i].z + Mathf.Sin(k * Mathf.PI / 4) * R.sink_rim_band_m); if (!cc.Raycast(new Ray(q, Vector3.down), out h2, cb.size.y + 20)) nearRim = true; }
                float tg = h.point.y - (nearRim ? R.sink_under_rim_m : R.sink_under_in_m);
                bool move = nearRim ? (Mathf.Abs(w[i].y - tg) > 0.002f && w[i].y > h.point.y - 2 * R.sink_under_in_m) : (w[i].y > h.point.y - 0.02f);
                if (move) { w[i].y = tg; n++; }
            }
            if (n == 0) continue;
            var nm = SaveMesh(src, w.Select(v => f.transform.InverseTransformPoint(v)).ToArray(), f.name + "_under_" + cm.name.Replace("SPatch_", "").Replace("_-_Mesh", ""), "_su"); Swap(f, nm, "sinkunder"); o.Add(f.name + " " + n);
        }
        return cm.name + ": sunk " + (o.Count == 0 ? "nothing" : string.Join(", ", o.ToArray())) + " (" + nOut + " of those capped in the band outside the rim)";
    }

    // every collarflush / sinkunder swap in COURSEBUILD_LOG goes back, newest first (or only those of one collar when `only` is set)
    public static string Revert()
    {
        var log = GameObject.Find(LOG); if (!log) return "no " + LOG + " in the scene: nothing to revert"; var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no 'OPCD Meshes' root";
        var fs = opcdGo.GetComponentsInChildren<MeshFilter>(true).ToList(); var o = new List<string>(); int n = 0;
        foreach (var e in log.transform.Cast<Transform>().Reverse().ToList())
        {
            var p = e.name.Split('|'); if (p.Length < 4 || (p[0] != "collarflush" && p[0] != "sinkunder" && p[0] != "rimreset")) continue;
            if (!string.IsNullOrEmpty(only) && !(p[1] == only || p[1].Contains(only.Replace("SPatch_", "").Replace("_-_Mesh", "")))) continue;
            var f = fs.FirstOrDefault(x => x.name == p[1]); if (!f) { o.Add(p[1] + " not in the scene"); continue; }
            var src = AssetDatabase.LoadAssetAtPath<Mesh>(p[2]) ?? AssetDatabase.LoadAllAssetsAtPath(p[2]).OfType<Mesh>().FirstOrDefault(m => m.name == p[3]) ?? AssetDatabase.LoadAllAssetsAtPath(p[2]).OfType<Mesh>().FirstOrDefault();
            if (!src) { o.Add(p[1] + ": source " + p[2] + " not found"); continue; }
            var cur = f.sharedMesh; var mc = f.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == cur) { mc.sharedMesh = null; mc.sharedMesh = src; } f.sharedMesh = src; EditorUtility.SetDirty(f); Object.DestroyImmediate(e.gameObject); n++;
        }
        Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return n + " meshes reverted" + (o.Count > 0 ? "\n" + string.Join("\n", o.ToArray()) : "");
    }

    void OnGUI()
    {
        GUILayout.Label("Collar flush (course-build-greens: collar.height = flush). Rim pinned to the neighbours, interior a membrane, turf under it sunk.", EditorStyles.boldLabel);
        only = EditorGUILayout.TextField("Only this collar (blank = all)", only);
        if (GUILayout.Button("Dry run")) Debug.Log(DryRun());
        if (GUILayout.Button("Apply")) Debug.Log(Apply());
        if (GUILayout.Button("Revert")) Debug.Log(Revert());
    }
    [MenuItem("Course Tools/Greens/Collar flush window")] static void Open() { GetWindow<CourseCollarFlush>("Collar flush"); }
}
