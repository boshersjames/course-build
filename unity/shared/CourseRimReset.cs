// CourseRimReset v2: move collar rim vertices by the vertex-to-neighbour residual (course-build-greens, rules/greens.json collar.rim_reset).
//
// Input: <project>/AutomationOutput/checks/steps.csv from tools/edge_profiles.py (the real steps of the edge survey, slope removed, with
// the sample position sx, sz and the two colliders crossed). A row only says WHERE the survey failed and WHICH collar mesh was in the
// crossing. Direction and amount come from the vertex itself: for every major-loop rim vertex within radius_m of a failing sample, the
// neighbour height is sampled probe_m outside the rim (the side where the collar's own collider is not underfoot; top collider that is
// not a collar, a tree or a plant-physics prop) and the move is neighbour_y - vertex_y, signed as measured. No pair category decides a sign
// (v1 inferred the sign from the pair and doubled the outer-rim defect on Tierra 14, 2026-11-03). A residual over max_move_m is skipped and
// counted; no vertex ends above the designed surface at its own position (CourseCollarFlush.PinRank <= 2); only meshes named collar are
// edited; green mesh and green blend are never moved. Directional gate: a collar whose share of vertices moving down is under
// min_share_down, or whose net signed move is not negative, is flagged and NOT applied unless -Set force = "1" with only = that collar.
// New meshes under Assets/CourseBuild/generated/collarflush, MeshCollider follows, COURSEBUILD_LOG keeps the swap for Revert.
//
// Bridge: invoke -Tool CourseRimReset -Method DryRun | Apply | Revert
//         -Set @{ target = "collar" | "blend" } | @{ hole = "14" } | @{ only = "<mesh>" } | @{ radius = "0.4"; maxMove = "0.1" } (0 = rule) | @{ force = "1" } | @{ stepsPath = "<path>" }
// target = "blend" edits the green blends instead: inner rim seats to the green, outer rim to the collar, same residual, same gate (the
// green-edge class: Tierra 16, a 5 cm drop from the green onto its own blend). Static fields persist between calls until a recompile:
// clear them explicitly (hole = ""; only = ""; force = "0"; target = "collar") for a course-wide collar call.
// Loop: RunAll -> edge_profiles.py -> DryRun (read the gate) -> Apply -> RunAll -> edge_profiles.py; never Apply twice on one steps file.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CourseRimReset : EditorWindow
{
    [Serializable] public class ResetRule { public float radius_m = 0.4f, max_move_m = 0.10f, probe_m = 0.03f, min_share_down = 0.7f; public int passes = 2, max_rows_over = 10; public string[] collar_name_excludes = new string[0]; }
    [Serializable] class Collar { public ResetRule rim_reset = new ResetRule(); public CourseCollarFlush.FlushRule flush_tool = new CourseCollarFlush.FlushRule(); }
    [Serializable] class GreensRules { public Collar collar = new Collar(); }
    const string CB = "Assets/CourseBuild";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public static string only = "", hole = "", stepsPath = "";
    public static float radius = 0f, maxMove = 0f;   // -Set overrides for one call; 0 = the rule value (greens.collar.rim_reset.radius_m / max_move_m)

    public static bool IsCollar(string n, ResetRule R) { var l = n.ToLower(); if (!l.Contains("collar")) return false; foreach (var x in R.collar_name_excludes ?? new string[0]) if (!string.IsNullOrEmpty(x) && l.Contains(x.ToLower())) return false; return true; }
    public static bool IsBlend(string n) { var l = n.ToLower(); return l.Contains("green") && l.Contains("blend"); }
    static bool IsTarget(string n, ResetRule R) { return target == "blend" ? IsBlend(n) : IsCollar(n, R); }
    public static string DryRun() { return Run(false); }
    public static string Apply() { return Run(true); }
    public static string Revert() { return CourseCollarFlush.Revert(); }

    [MenuItem("Course Tools/Greens/Rim reset (dry run)")] static void MenuDry() { Debug.Log(DryRun()); }
    [MenuItem("Course Tools/Greens/Rim reset (apply)")] static void MenuApply() { Debug.Log(Apply()); }

    public static float force = 0f;
    public static string target = "collar";   // -Set target = "collar" (default) | "blend": which mesh class is edited; its own class is never the neighbour   // -Set force = "1": apply even on a collar the directional gate flagged (one collar via `only`)


    public static string Run(bool apply)
    {
        Physics.SyncTransforms();
        var rp = CB + "/rules/greens.json"; var G = File.Exists(rp) ? JsonUtility.FromJson<GreensRules>(File.ReadAllText(rp).TrimStart('﻿')) : new GreensRules();
        var R = G.collar.rim_reset; float minFrac = G.collar.flush_tool != null ? G.collar.flush_tool.rim_loop_min_frac : 0.25f;
        if (radius > 0) R.radius_m = radius; if (maxMove > 0) R.max_move_m = maxMove;
        string projDir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); var sp = string.IsNullOrEmpty(stepsPath) ? projDir + "/AutomationOutput/checks/steps.csv" : stepsPath;
        if (!File.Exists(sp)) return "no steps file: " + sp + " (run CourseGreenChecks.RunAll, then tools/edge_profiles.py)";
        var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no 'OPCD Meshes' root"; var opcd = opcdGo.transform;
        var filters = opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToDictionary(f => f.name, f => f);
        var lines = File.ReadAllLines(sp); if (lines.Length < 2) return "steps file is empty: " + sp;
        var head = lines[0].Split(',').ToList(); int iH = head.IndexOf("hole"), iFrom = head.IndexOf("from"), iTo = head.IndexOf("to"), iSx = head.IndexOf("sx"), iSz = head.IndexOf("sz");
        if (iSx < 0) return "steps file has no sx/sz columns: regenerate with the current edge_profiles.py";
        // 1. which collar meshes, and which failing samples lie near them (the sample only says WHERE to look; direction comes from the vertex itself)
        var samples = new Dictionary<MeshFilter, List<Vector2>>(); int rows = 0, skippedHole = 0, noMesh = 0, noCollar = 0;
        foreach (var ln in lines.Skip(1))
        {
            var a = ln.Split(','); if (a.Length <= Math.Max(iSz, iTo)) continue; rows++;
            if (!string.IsNullOrEmpty(hole) && a[iH] != hole) { skippedHole++; continue; }
            string cname = IsTarget(a[iFrom], R) ? a[iFrom] : IsTarget(a[iTo], R) ? a[iTo] : null; if (cname == null) { noCollar++; continue; }
            if (!string.IsNullOrEmpty(only) && cname != only) { skippedHole++; continue; }
            MeshFilter mf; if (!filters.TryGetValue(cname, out mf)) { noMesh++; continue; }
            List<Vector2> l; if (!samples.TryGetValue(mf, out l)) samples[mf] = l = new List<Vector2>(); l.Add(new Vector2(float.Parse(a[iSx], CI), float.Parse(a[iSz], CI)));
        }
        var o = new List<string> { (apply ? "APPLY" : "DRY RUN") + " v2 vertex residual, target " + target + " (probe " + (R.probe_m * 100).ToString("F0", CI) + " cm, radius " + R.radius_m.ToString(CI) + " m, max move " + (R.max_move_m * 100).ToString("F0", CI) + " cm, gate share-down >= " + (R.min_share_down * 100).ToString("F0", CI) + "%" + (string.IsNullOrEmpty(hole) ? "" : ", hole " + hole) + (string.IsNullOrEmpty(only) ? "" : ", only " + only) + "): " + rows + " step rows, " + noCollar + " with no " + target + " in the pair, " + skippedHole + " outside the hole/only filter, " + noMesh + " target mesh not in scene, " + samples.Count + " " + target + " meshes" };
        if (apply) CourseCollarFlush.EnsureFolders();
        float r2 = R.radius_m * R.radius_m;
        foreach (var kv in samples)
        {
            var mf = kv.Key; var col = mf.GetComponent<Collider>(); var src = mf.sharedMesh; var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
            // welded boundary graph (as CourseCollarFlush): rim vertices and their two boundary neighbours give the edge tangent
            var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N]; for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
            var cnt = new Dictionary<long, int>(); Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; if (a == b) continue; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
            var bEdges = cnt.Where(q => q.Value == 1).Select(q => new[] { (int)(q.Key >> 32), (int)(q.Key & 0xffffffff) }).ToList();
            var rim = CourseCollarFlush.MajorLoops(bEdges, w, minFrac);
            var bAdj = new Dictionary<int, List<int>>(); foreach (var e in bEdges) { if (!rim.Contains(e[0]) || !rim.Contains(e[1])) continue; List<int> l; if (!bAdj.TryGetValue(e[0], out l)) bAdj[e[0]] = l = new List<int>(); l.Add(e[1]); if (!bAdj.TryGetValue(e[1], out l)) bAdj[e[1]] = l = new List<int>(); l.Add(e[0]); }
            // 2. per rim rep near a failing sample: neighbour height probe_m outside the rim, residual = neighbour - vertex
            var res = new Dictionary<int, float>(); int reached = 0, unprobed = 0, overCap = 0, capped = 0;
            foreach (var r in rim)
            {
                bool near = false; foreach (var sm in kv.Value) if ((new Vector2(w[r].x, w[r].z) - sm).sqrMagnitude <= r2) { near = true; break; } if (!near) continue; reached++;
                List<int> bn; if (!bAdj.TryGetValue(r, out bn) || bn.Count < 2) { unprobed++; continue; }
                var tan = new Vector2(w[bn[1]].x - w[bn[0]].x, w[bn[1]].z - w[bn[0]].z).normalized; var nrm = new Vector2(-tan.y, tan.x); float ny = float.NaN;
                foreach (var sgn in new[] { 1f, -1f })
                {
                    var q = new Vector2(w[r].x, w[r].z) + nrm * R.probe_m * sgn; RaycastHit own;
                    if (col && col.Raycast(new Ray(new Vector3(q.x, w[r].y + 5, q.y), Vector3.down), out own, 20)) continue;   // still on the collar: that is the inside
                    float top = float.NaN;
                    foreach (var h in Physics.RaycastAll(new Vector3(q.x, w[r].y + 5, q.y), Vector3.down, 20))
                    {
                        if (!h.collider.transform.IsChildOf(opcd) || h.collider == col || h.collider.GetComponentInParent<LODGroup>() || h.collider.isTrigger || IsTarget(h.collider.name, R)) continue;   // its own class is never the neighbour
                        var pm = h.collider.sharedMaterial ? h.collider.sharedMaterial.name : ""; if (pm == "GSPleaves" || pm == "GSPtimber") continue;
                        if (float.IsNaN(top) || h.point.y > top) top = h.point.y;
                    }
                    if (!float.IsNaN(top)) { ny = top; break; }
                }
                if (float.IsNaN(ny)) { unprobed++; continue; }
                float d = ny - w[r].y; if (Mathf.Abs(d) > R.max_move_m) { overCap++; continue; }
                // conform cap: never end above the designed surface at the vertex itself
                float lim = float.NaN; foreach (var h in Physics.RaycastAll(new Vector3(w[r].x, w[r].y + 5, w[r].z), Vector3.down, 20)) { if (!h.collider.transform.IsChildOf(opcd) || h.collider == col || h.collider.GetComponentInParent<LODGroup>() || IsTarget(h.collider.name, R) || CourseCollarFlush.PinRank(h.collider.name) > 2) continue; if (float.IsNaN(lim) || h.point.y > lim) lim = h.point.y; }
                if (!float.IsNaN(lim) && w[r].y + d > lim) { d = lim - w[r].y; capped++; }
                if (Mathf.Abs(d) >= 0.0005f) res[r] = d;
            }
            int down = res.Values.Count(v => v < 0), up = res.Values.Count(v => v > 0); float net = res.Values.Sum(), mean = res.Count > 0 ? net / res.Count : 0;
            float maxDown = res.Count > 0 ? -Mathf.Min(0, res.Values.Min()) : 0, maxUp = res.Count > 0 ? Mathf.Max(0, res.Values.Max()) : 0; float share = res.Count > 0 ? (float)down / res.Count : 0;
            bool gateOk = res.Count > 0 && share >= R.min_share_down && net < 0;
            o.Add(mf.name + ": " + reached + " rim vertices near failing samples, " + res.Count + " with a move (" + down + " down, " + up + " up, " + (share * 100).ToString("F0", CI) + "% down), net " + (net * 100).ToString("+0.0;-0.0", CI) + " cm, mean " + (mean * 100).ToString("+0.00;-0.00", CI) + " cm, largest down " + (maxDown * 100).ToString("F1", CI) + " cm, largest up " + (maxUp * 100).ToString("F1", CI) + " cm, " + capped + " held at the designed surface, " + overCap + " over the cap (skipped), " + unprobed + " unprobed" + (gateOk ? "" : "  <-- GATE: not applied" + (force > 0 && !string.IsNullOrEmpty(only) ? " unless forced (forced)" : "")));
            if (!apply || res.Count == 0) continue;
            if (!gateOk && !(force > 0 && only == mf.name)) continue;
            var nv = (Vector3[])src.vertices.Clone(); int moved = 0;
            for (int i = 0; i < N; i++) { float d; if (!res.TryGetValue(weld[i], out d)) continue; var p = w[i]; p.y += d; nv[i] = mf.transform.InverseTransformPoint(p); moved++; }
            var nm = CourseCollarFlush.SaveMesh(src, nv, mf.name + "_rimreset_" + DateTime.Now.ToString("HHmmss"), "_rr"); CourseCollarFlush.Swap(mf, nm, "rimreset"); o.Add("  applied: " + moved + " raw vertices moved on " + mf.name);
        }
        if (apply) { AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); o.Add("now CourseGreenChecks.RunAll and edge_profiles.py (a fresh steps file before any further Apply); Revert puts every collar back"); }
        var txt = string.Join("\n", o.ToArray()); Directory.CreateDirectory(projDir + "/AutomationOutput/checks"); File.WriteAllText(projDir + "/AutomationOutput/checks/rim_reset.txt", txt); return txt;
    }

    void OnGUI()
    {
        GUILayout.Label("Rim reset (course-build-greens): collar rim vertices move by the survey residual in checks/steps.csv.", EditorStyles.boldLabel);
        hole = EditorGUILayout.TextField("Only this hole (blank = all)", hole); only = EditorGUILayout.TextField("Only this collar (blank = all)", only);
        if (GUILayout.Button("Dry run")) Debug.Log(DryRun()); if (GUILayout.Button("Apply")) Debug.Log(Apply()); if (GUILayout.Button("Revert (all collar edits)")) Debug.Log(Revert());
    }
    [MenuItem("Course Tools/Greens/Rim reset window")] static void Open() { GetWindow<CourseRimReset>("Rim reset"); }
}
