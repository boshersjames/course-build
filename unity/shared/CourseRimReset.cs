// CourseRimReset: move collar rim vertices by the measured edge-survey residual (course-build-greens, rules/greens.json collar.rim_reset).
//
// Input: <project>/AutomationOutput/checks/steps.csv written by tools/edge_profiles.py --steps-out (hole, edge_x, edge_z, d_m, step_cm,
// from, to, sx, sz, pair): one row per real step (slope removed) with the sample position sx, sz and the two colliders it crosses.
// For every row whose pair is in collar.rim_reset.pairs, the collar mesh in that pair (name contains "collar") has its major-loop rim
// vertices within radius_m of the sample moved in y by minus the signed step, weighted 1 - d/radius; where several samples reach one
// vertex the weighted mean of their demands applies. A demand over max_move_m is logged and skipped. No vertex is raised above the
// designed surface beside it (CourseCollarFlush.PinRank <= 2: green mesh, green blend, fairway, tee). Only meshes named collar are
// edited. New meshes go to Assets/CourseBuild/generated/collarflush, the MeshCollider follows, COURSEBUILD_LOG keeps the swap so
// CourseCollarFlush.Revert (or this tool's Revert) puts the previous mesh back. Dry run unless Apply.
//
// Bridge: invoke -Tool CourseRimReset -Method DryRun | Apply | Revert  [-Set @{ hole = "14" }] [-Set @{ only = "<collar mesh>" }]
//         [-Set @{ stepsPath = "<path>"; radius = "0.4"; maxMove = "0.1" }]  (radius / maxMove override the rule for that call; 0 = rule)
// Loop: CourseGreenChecks.RunAll -> edge_profiles.py --steps-out -> Apply -> RunAll -> edge_profiles.py; pass = the pair table under
// collar.rim_reset.pass_criterion.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CourseRimReset : EditorWindow
{
    [Serializable] public class ResetRule { public string[] pairs = new string[0]; public float radius_m = 0.3f, max_move_m = 0.05f; public int passes = 2, max_rows_over = 10; }
    [Serializable] class Collar { public ResetRule rim_reset = new ResetRule(); public CourseCollarFlush.FlushRule flush_tool = new CourseCollarFlush.FlushRule(); }
    [Serializable] class GreensRules { public Collar collar = new Collar(); }
    const string CB = "Assets/CourseBuild";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public static string only = "", hole = "", stepsPath = "";
    public static float radius = 0f, maxMove = 0f;   // -Set overrides for one call; 0 = the rule value (greens.collar.rim_reset.radius_m / max_move_m)

    public static string DryRun() { return Run(false); }
    public static string Apply() { return Run(true); }
    public static string Revert() { return CourseCollarFlush.Revert(); }

    [MenuItem("Course Tools/Greens/Rim reset (dry run)")] static void MenuDry() { Debug.Log(DryRun()); }
    [MenuItem("Course Tools/Greens/Rim reset (apply)")] static void MenuApply() { Debug.Log(Apply()); }

    class Demand { public float sumW, sumWMove; public int n; }

    public static string Run(bool apply)
    {
        Physics.SyncTransforms();
        var rp = CB + "/rules/greens.json"; var G = File.Exists(rp) ? JsonUtility.FromJson<GreensRules>(File.ReadAllText(rp).TrimStart('﻿')) : new GreensRules();
        var R = G.collar.rim_reset; float minFrac = G.collar.flush_tool != null ? G.collar.flush_tool.rim_loop_min_frac : 0.25f;
        if (R.pairs.Length == 0) return "no collar.rim_reset.pairs in " + rp + " (sync rules)";
        if (radius > 0) R.radius_m = radius; if (maxMove > 0) R.max_move_m = maxMove;
        string projDir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); var sp = string.IsNullOrEmpty(stepsPath) ? projDir + "/AutomationOutput/checks/steps.csv" : stepsPath;
        if (!File.Exists(sp)) return "no steps file: " + sp + " (run CourseGreenChecks.RunAll, then tools/edge_profiles.py --steps-out " + projDir + "/AutomationOutput/checks/steps.csv)";
        var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no 'OPCD Meshes' root"; var opcd = opcdGo.transform;
        var filters = opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToDictionary(f => f.name, f => f);
        var allowed = new HashSet<string>(R.pairs);
        var lines = File.ReadAllLines(sp); if (lines.Length < 2) return "steps file is empty: " + sp;
        var head = lines[0].Split(',').ToList(); int iH = head.IndexOf("hole"), iStep = head.IndexOf("step_cm"), iFrom = head.IndexOf("from"), iTo = head.IndexOf("to"), iSx = head.IndexOf("sx"), iSz = head.IndexOf("sz"), iPair = head.IndexOf("pair");
        if (iSx < 0 || iPair < 0) return "steps file has no sx/sz/pair columns: regenerate with the current edge_profiles.py";
        var demands = new Dictionary<MeshFilter, Dictionary<int, Demand>>(); var world = new Dictionary<MeshFilter, Vector3[]>(); var rims = new Dictionary<MeshFilter, HashSet<int>>();
        int rows = 0, used = 0, skippedPair = 0, skippedBig = 0, skippedHole = 0, noMesh = 0, noRim = 0; float r2 = R.radius_m * R.radius_m;
        foreach (var ln in lines.Skip(1))
        {
            var a = ln.Split(','); if (a.Length <= Math.Max(iPair, Math.Max(iSz, iTo))) continue; rows++;
            if (!string.IsNullOrEmpty(hole) && a[iH] != hole) { skippedHole++; continue; }
            if (!allowed.Contains(a[iPair])) { skippedPair++; continue; }
            float stepM = float.Parse(a[iStep], CI) / 100f; if (Mathf.Abs(stepM) > R.max_move_m) { skippedBig++; continue; }
            string cname = a[iFrom].ToLower().Contains("collar") ? a[iFrom] : a[iTo].ToLower().Contains("collar") ? a[iTo] : null; if (cname == null) { skippedPair++; continue; }
            if (!string.IsNullOrEmpty(only) && cname != only) { skippedHole++; continue; }
            MeshFilter mf; if (!filters.TryGetValue(cname, out mf)) { noMesh++; continue; }
            if (!world.ContainsKey(mf)) { world[mf] = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); rims[mf] = CourseCollarFlush.RimIndices(mf, minFrac); demands[mf] = new Dictionary<int, Demand>(); }
            var w = world[mf]; var s = new Vector2(float.Parse(a[iSx], CI), float.Parse(a[iSz], CI)); int hit = 0;
            foreach (var i in rims[mf])
            {
                float d2 = (new Vector2(w[i].x, w[i].z) - s).sqrMagnitude; if (d2 > r2) continue;
                float wt = 1f - Mathf.Sqrt(d2) / R.radius_m; Demand dm; if (!demands[mf].TryGetValue(i, out dm)) demands[mf][i] = dm = new Demand();
                dm.sumW += wt; dm.sumWMove += wt * -stepM; dm.n++; hit++;
            }
            if (hit == 0) noRim++; else used++;
        }
        var o = new List<string> { (apply ? "APPLY" : "DRY RUN") + " (radius " + R.radius_m.ToString(CI) + " m, max move " + (R.max_move_m * 100).ToString("F0", CI) + " cm" + (string.IsNullOrEmpty(hole) ? "" : ", hole " + hole) + (string.IsNullOrEmpty(only) ? "" : ", only " + only) + "): " + rows + " step rows, " + used + " used, " + skippedPair + " pairs not listed, " + skippedBig + " over max_move " + (R.max_move_m * 100).ToString("F0", CI) + " cm (skipped), " + skippedHole + " outside the hole/only filter, " + noMesh + " collar mesh not in scene, " + noRim + " with no rim vertex within " + R.radius_m.ToString(CI) + " m" };
        if (apply) CourseCollarFlush.EnsureFolders();
        foreach (var kv in demands)
        {
            var mf = kv.Key; var w = world[mf]; var nv = (Vector3[])mf.sharedMesh.vertices.Clone(); int moved = 0, capped = 0; float sum = 0, mx = 0;
            foreach (var d in kv.Value)
            {
                int i = d.Key; float move = d.Value.sumWMove / Mathf.Max(1e-6f, d.Value.sumW); float ny = w[i].y + move;
                float lim = float.NaN;
                foreach (var h in Physics.RaycastAll(new Vector3(w[i].x, w[i].y + 5, w[i].z), Vector3.down, 20)) { if (!h.collider.transform.IsChildOf(opcd) || h.collider.transform == mf.transform || h.collider.GetComponentInParent<LODGroup>() || CourseCollarFlush.PinRank(h.collider.name) > 2) continue; if (float.IsNaN(lim) || h.point.y > lim) lim = h.point.y; }
                if (!float.IsNaN(lim) && ny > lim) { ny = lim; capped++; }
                float dy = ny - w[i].y; if (Mathf.Abs(dy) < 0.0005f) continue; moved++; sum += Mathf.Abs(dy); mx = Mathf.Max(mx, Mathf.Abs(dy));
                var p = w[i]; p.y = ny; nv[i] = mf.transform.InverseTransformPoint(p);
            }
            o.Add(mf.name + ": " + kv.Value.Count + " rim vertices reached, " + moved + " moved (mean " + (moved > 0 ? sum / moved * 100 : 0).ToString("F1", CI) + " cm, largest " + (mx * 100).ToString("F1", CI) + " cm), " + capped + " held at the designed surface");
            if (apply && moved > 0) { var nm = CourseCollarFlush.SaveMesh(mf.sharedMesh, nv, mf.name + "_rimreset_" + DateTime.Now.ToString("HHmmss"), "_rr"); CourseCollarFlush.Swap(mf, nm, "rimreset"); }
        }
        if (apply) { AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); o.Add("now CourseGreenChecks.RunAll and edge_profiles.py; Revert puts every collar back"); }
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
