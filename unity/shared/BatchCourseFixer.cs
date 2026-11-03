using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Course Fixes > Batch fixer (audit JSON)
// Reads an audit JSON (schema: Assets/CourseBuild/batch_fixer_schema.json, example below), applies the mechanical fixes it lists, and
// writes an execution log for review BEFORE the gate. Dry run by default: the log says what WOULD change; apply=true changes it,
// with Undo registered for every terrain and every prefab so one Ctrl+Z per item reverts it.
//
//   { "course": "TierraRejada", "generated": "2026-11-03",
//     "bunkers": [ { "hole": 12, "spline": "Spline_bunker_7", "steepness_delta_pct": 22.5,
//                    "edge": [ {"x": 1512.3, "z": 988.1}, {"x": 1513.0, "z": 989.4}, ... ] } ],
//     "trees":   [ { "hole": 6, "x": 1388.2, "z": 1120.7, "osm_tag": "natural=tree;species=Quercus agrifolia", "region_tag": "socal_oak",
//                    "height_m": 11.5, "crown_radius_m": 4.0, "action": "add" },
//                  { "hole": 6, "x": 1390.0, "z": 1118.0, "action": "move", "existing_name": "TR_Oak_0412", "to_x": 1396.5, "to_z": 1116.0 } ],
//     "colour":  [ { "hole": 10, "material": "Fairway_Tierra", "delta_rg": -0.14, "delta_bg": -0.09 } ] }
//
// Rules applied (course-build):
//  - bunkers: only items with steepness_delta_pct > bunkerDeltaMinPct (default 15) are touched; Laplacian smoothing of the Unity terrain
//    heightmap within smoothRadiusM (default 2 m) of the spline edge, weight falling to zero at the radius (smootherstep) so no step is cut,
//    N passes. The visible lip at an OPCD course is the sand/surround MESH draped on this terrain; this pass fixes the drape under it, a
//    lip that lives in the mesh still needs the bunker rebuild package (course-build-bunkers). Terrain under greens/collars/tees is never
//    touched: samples whose top collider carries GSPgreen/GSPfairway/GSPtee (or a name matching green/collar/tee) are masked out and logged.
//  - trees: raycast from above onto terrain + course meshes; a hit on mown turf (physic material GSPgreen/GSPfairway/GSPtee or name match)
//    is REFUSED and logged (no plants on mown turf, never delete, never move a tree onto turf). Prefab chosen by region_tag from
//    Assets/CourseBuild/tree_library.json ( [{"region":"socal_oak","prefabs":["Assets/.../TR_Oak.prefab"],"ref_height_m":12.63}] ), scaled to
//    height_m when given, placed at the hit point, tilted toward the surface normal by at most maxTiltDeg (default 8: a tree leaning with
//    a 20% bank reads wrong; set 90 for full alignment). Trunk/canopy colliders are left to the tree tools (PlantLidarTrees / CanopyCover).
//  - colour: NOT applied here (the season decision shelved turf tint; use Course Look > Apply Tint JSON on material copies); logged as skipped.
// Output: <project>/AutomationOutput/checks/batch_fixer_<yyyyMMdd_HHmmss>.md and .json: per terrain the sample rect modified, samples
// changed, max |dz|; per tree the prefab, position, rotation, scale, tilt, and refusals with reasons.
// Bridge: invoke -Tool BatchCourseFixer -Method RunDry -Set @{ jsonPath = "..." }  /  -Method RunApply
// NOT YET COMPILED ON THE PC (written in the cloud 2026-11-03).
public class BatchCourseFixer : EditorWindow
{
    [Serializable] public class P2 { public float x, z; }
    [Serializable] public class BunkerItem { public int hole; public string spline; public float steepness_delta_pct; public P2[] edge; }
    [Serializable] public class TreeItem { public int hole; public float x, z; public string osm_tag, region_tag, action = "add", existing_name; public float height_m, crown_radius_m, to_x, to_z; }
    [Serializable] public class ColourItem { public int hole; public string material; public float delta_rg, delta_bg; }
    [Serializable] public class Audit { public string course, generated; public BunkerItem[] bunkers = new BunkerItem[0]; public TreeItem[] trees = new TreeItem[0]; public ColourItem[] colour = new ColourItem[0]; }
    [Serializable] public class LibEntry { public string region; public string[] prefabs; public float ref_height_m; }
    [Serializable] public class Lib { public LibEntry[] entries = new LibEntry[0]; }

    public static string jsonPath = "";
    public static float bunkerDeltaMinPct = 15f, smoothRadiusM = 2f, maxTiltDeg = 8f; public static int smoothPasses = 3;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    const string CB = "Assets/CourseBuild";
    string last = ""; Vector2 scroll; bool applyToggle = false;

    [MenuItem("Course Fixes/Batch fixer (audit JSON)")]
    static void Open() { GetWindow<BatchCourseFixer>("Batch fixer"); }

    void OnGUI()
    {
        jsonPath = EditorGUILayout.TextField("Audit JSON", jsonPath);
        if (GUILayout.Button("Browse")) { var p = EditorUtility.OpenFilePanel("audit json", Path.GetDirectoryName(Application.dataPath), "json"); if (!string.IsNullOrEmpty(p)) jsonPath = p; }
        bunkerDeltaMinPct = EditorGUILayout.FloatField("Bunker delta min %", bunkerDeltaMinPct); smoothRadiusM = EditorGUILayout.FloatField("Smooth radius m", smoothRadiusM);
        smoothPasses = EditorGUILayout.IntField("Smooth passes", smoothPasses); maxTiltDeg = EditorGUILayout.FloatField("Tree max tilt deg", maxTiltDeg);
        applyToggle = EditorGUILayout.Toggle("Apply (else dry run)", applyToggle);
        if (GUILayout.Button(applyToggle ? "RUN AND APPLY" : "Dry run")) last = Run(jsonPath, applyToggle);
        scroll = EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(last); EditorGUILayout.EndScrollView();
    }

    public static string RunDry() { return Run(jsonPath, false); }
    public static string RunApply() { return Run(jsonPath, true); }

    // ---------- helpers ----------
    static bool IsMownTurf(Collider c)
    {
        if (!c) return false;
        var pm = c.sharedMaterial ? c.sharedMaterial.name : ""; var n = c.name.ToLower();
        return pm == "GSPgreen" || pm == "GSPfairway" || pm == "GSPtee" || n.Contains("_green_-_mesh") || n.Contains("collar") || n.Contains("_tee_-_mesh") || n.Contains("fairway");
    }
    static bool TopHit(float x, float z, out RaycastHit best)
    {
        best = new RaycastHit(); bool any = false;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200))
        {
            if (h.collider.GetComponentInParent<LODGroup>()) continue;              // never land on another tree
            if (!any || h.point.y > best.point.y) { best = h; any = true; }
        }
        return any;
    }
    static Terrain TerrainAt(float x, float z)
    {
        foreach (var t in Terrain.activeTerrains) { var o = t.GetPosition(); var s = t.terrainData.size; if (x >= o.x && z >= o.z && x <= o.x + s.x && z <= o.z + s.z) return t; }
        return null;
    }
    static float Smootherstep(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

    // ---------- bunkers: Laplacian smoothing of the terrain heightmap near the spline edge ----------
    class TerrainEdit { public string terrain; public int x0, z0, w, h, changed; public float maxDz, masked; }
    static TerrainEdit SmoothBunker(BunkerItem b, bool apply, List<string> log)
    {
        if (b.edge == null || b.edge.Length < 3) { log.Add("  bunker " + b.spline + ": no edge points, skipped"); return null; }
        var t = TerrainAt(b.edge[0].x, b.edge[0].z); if (!t) { log.Add("  bunker " + b.spline + ": no terrain under the edge, skipped"); return null; }
        var td = t.terrainData; var o = t.GetPosition(); var size = td.size; int res = td.heightmapResolution; float sx = size.x / (res - 1), sz = size.z / (res - 1);
        float minx = b.edge.Min(p => p.x) - smoothRadiusM, maxx = b.edge.Max(p => p.x) + smoothRadiusM, minz = b.edge.Min(p => p.z) - smoothRadiusM, maxz = b.edge.Max(p => p.z) + smoothRadiusM;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minx - o.x) / sx) - 1, 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxx - o.x) / sx) + 1, 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((minz - o.z) / sz) - 1, 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((maxz - o.z) / sz) + 1, 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1; if (w < 3 || h < 3) { log.Add("  bunker " + b.spline + ": edit window too small, skipped"); return null; }
        var H = td.GetHeights(x0, z0, w, h);      // [z, x] normalised 0..1
        var W = new float[h, w]; int masked = 0;
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            float wx = o.x + (x0 + i) * sx, wz = o.z + (z0 + j) * sz; float d = float.MaxValue;
            for (int k = 0; k < b.edge.Length; k++)      // distance to the polyline (segments), not only the vertices
            {
                var a = b.edge[k]; var c = b.edge[(k + 1) % b.edge.Length]; float ax = c.x - a.x, az = c.z - a.z; float L2 = Mathf.Max(ax * ax + az * az, 1e-6f);
                float u = Mathf.Clamp01(((wx - a.x) * ax + (wz - a.z) * az) / L2); float px = a.x + u * ax, pz = a.z + u * az; d = Mathf.Min(d, Mathf.Sqrt((wx - px) * (wx - px) + (wz - pz) * (wz - pz)));
            }
            if (d > smoothRadiusM) continue;
            RaycastHit hit; if (TopHit(wx, wz, out hit) && IsMownTurf(hit.collider)) { masked++; continue; }   // never under greens/collars/tees
            W[j, i] = 1f - Smootherstep(d / smoothRadiusM);
        }
        var S = (float[,])H.Clone(); float maxDz = 0; int changed = 0;
        for (int pass = 0; pass < smoothPasses; pass++)
        {
            var N = (float[,])S.Clone();
            for (int j = 1; j < h - 1; j++) for (int i = 1; i < w - 1; i++)
            {
                if (W[j, i] <= 0) continue;
                float lap = 0.25f * (S[j - 1, i] + S[j + 1, i] + S[j, i - 1] + S[j, i + 1]) - S[j, i];   // 4-neighbour Laplacian
                N[j, i] = S[j, i] + 0.5f * W[j, i] * lap;
            }
            S = N;
        }
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) { float dz = Mathf.Abs(S[j, i] - H[j, i]) * size.y; if (dz > 0.0005f) { changed++; maxDz = Mathf.Max(maxDz, dz); } }
        if (apply && changed > 0) { Undo.RegisterCompleteObjectUndo(td, "batch fixer bunker " + b.spline); td.SetHeights(x0, z0, S); }
        log.Add(string.Format(CI, "  bunker {0} (hole {1}, delta {2:F1}%): terrain {3} rect x{4}..{5} z{6}..{7}, {8} samples changed, max |dz| {9:F3} m, {10} samples masked (mown turf){11}",
            b.spline, b.hole, b.steepness_delta_pct, t.name, x0, x1, z0, z1, changed, maxDz, masked, apply ? "" : " [dry run]"));
        return new TerrainEdit { terrain = t.name, x0 = x0, z0 = z0, w = w, h = h, changed = changed, maxDz = maxDz, masked = masked };
    }

    // ---------- trees ----------
    static Lib LoadLib()
    {
        var p = CB + "/tree_library.json"; if (!File.Exists(p)) return new Lib();
        var txt = File.ReadAllText(p).TrimStart('﻿'); if (txt.TrimStart().StartsWith("[")) txt = "{\"entries\":" + txt + "}";   // accept a bare list
        return JsonUtility.FromJson<Lib>(txt);
    }
    static GameObject PickPrefab(Lib lib, string region, int seed, out float refH)
    {
        refH = 0; var e = lib.entries.FirstOrDefault(x => x.region == region) ?? lib.entries.FirstOrDefault();
        if (e == null || e.prefabs == null || e.prefabs.Length == 0) return null;
        refH = e.ref_height_m; var path = e.prefabs[Mathf.Abs(seed) % e.prefabs.Length]; return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
    static string PlaceTree(TreeItem it, Lib lib, Transform parent, bool apply, List<string> log)
    {
        float x = it.action == "move" ? it.to_x : it.x, z = it.action == "move" ? it.to_z : it.z;
        RaycastHit hit; if (!TopHit(x, z, out hit)) { log.Add(string.Format(CI, "  tree hole {0} ({1:F1},{2:F1}): REFUSED, nothing under the point", it.hole, x, z)); return "refused"; }
        if (IsMownTurf(hit.collider)) { log.Add(string.Format(CI, "  tree hole {0} ({1:F1},{2:F1}): REFUSED, mown turf ({3}); list for BoJa", it.hole, x, z, hit.collider.name)); return "refused"; }
        var up = Vector3.Lerp(Vector3.up, hit.normal, Mathf.Clamp01(maxTiltDeg / Mathf.Max(Vector3.Angle(Vector3.up, hit.normal), 1e-3f))).normalized;   // tilt toward the normal, capped
        var rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, (it.x * 7.3f + it.z * 3.1f) % 360f, 0);
        float tilt = Vector3.Angle(Vector3.up, up);
        if (it.action == "move")
        {
            var go = GameObject.Find(it.existing_name); if (!go) { log.Add("  tree " + it.existing_name + ": not found, skipped"); return "skipped"; }
            if (apply) { Undo.RecordObject(go.transform, "batch fixer move tree"); go.transform.position = hit.point; go.transform.rotation = rot; }
            log.Add(string.Format(CI, "  tree {0} hole {1}: move ({2:F1},{3:F1}) -> ({4:F1},{5:F2},{6:F1}) tilt {7:F1} deg on {8}{9}", it.existing_name, it.hole, it.x, it.z, hit.point.x, hit.point.y, hit.point.z, tilt, hit.collider.name, apply ? "" : " [dry run]"));
            return "moved";
        }
        if (it.action == "remove") { log.Add("  tree " + it.existing_name + ": REMOVE requested; never delete (course-build-gates), disable it by hand if BoJa approves"); return "refused"; }
        float refH; var prefab = PickPrefab(lib, it.region_tag, (int)(it.x * 10 + it.z), out refH);
        if (!prefab) { log.Add("  tree hole " + it.hole + ": no prefab for region_tag '" + it.region_tag + "' in " + CB + "/tree_library.json, skipped"); return "skipped"; }
        float scale = (it.height_m > 0 && refH > 0) ? it.height_m / refH : 1f;
        string name = prefab.name + "_fx_" + it.hole + "_" + ((int)it.x).ToString() + "_" + ((int)it.z).ToString();
        if (apply)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = hit.point; go.transform.rotation = rot; go.transform.localScale = prefab.transform.localScale * scale;   // keep the prefab's own root scale
            Undo.RegisterCreatedObjectUndo(go, "batch fixer add tree");
        }
        log.Add(string.Format(CI, "  tree hole {0}: add {1} as {2} at ({3:F1},{4:F2},{5:F1}) scale x{6:F2} tilt {7:F1} deg on {8} [{9}]{10}", it.hole, prefab.name, name, hit.point.x, hit.point.y, hit.point.z, scale, tilt, hit.collider.name, it.osm_tag, apply ? "" : " [dry run]"));
        return "added";
    }

    // ---------- run ----------
    public static string Run(string path, bool apply)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "audit json not found: " + path;
        Physics.SyncTransforms();
        var A = JsonUtility.FromJson<Audit>(File.ReadAllText(path).TrimStart('﻿'));
        var log = new List<string> { "# Batch fixer " + (apply ? "APPLY" : "DRY RUN") + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " on " + (A.course ?? "?") + " (audit " + (A.generated ?? "?") + ", file " + path + ")", "" };
        var edits = new List<TerrainEdit>(); int bDone = 0, bSkip = 0;
        log.Add("## Bunker lips (terrain Laplacian, radius " + smoothRadiusM.ToString(CI) + " m, " + smoothPasses + " passes, only delta > " + bunkerDeltaMinPct.ToString(CI) + "%)");
        foreach (var b in A.bunkers)
        {
            if (b.steepness_delta_pct <= bunkerDeltaMinPct) { bSkip++; log.Add(string.Format(CI, "  bunker {0} (hole {1}): delta {2:F1}% <= {3}%, left alone", b.spline, b.hole, b.steepness_delta_pct, bunkerDeltaMinPct)); continue; }
            var e = SmoothBunker(b, apply, log); if (e != null) { edits.Add(e); bDone++; }
        }
        log.Add(""); log.Add("## Trees (prefab by region_tag, raycast to the top surface, refused on mown turf, tilt <= " + maxTiltDeg.ToString(CI) + " deg)");
        var lib = LoadLib(); var parent = GameObject.Find("FIXER_TREES") ?? (apply ? new GameObject("FIXER_TREES") : null);
        int tAdd = 0, tMove = 0, tRef = 0, tSkip = 0;
        foreach (var it in A.trees)
        {
            var r = PlaceTree(it, lib, parent ? parent.transform : null, apply, log);
            if (r == "added") tAdd++; else if (r == "moved") tMove++; else if (r == "refused") tRef++; else tSkip++;
        }
        log.Add(""); log.Add("## Colour deltas: " + A.colour.Length + " items SKIPPED (turf tint shelved by the season decision; use Course Look > Apply Tint JSON on material copies)");
        foreach (var c in A.colour) log.Add(string.Format(CI, "  hole {0} {1}: delta r/g {2:+0.000;-0.000} b/g {3:+0.000;-0.000} (not applied)", c.hole, c.material, c.delta_rg, c.delta_bg));
        if (apply && (edits.Count > 0 || tAdd + tMove > 0)) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        string dir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/AutomationOutput/checks"; Directory.CreateDirectory(dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); var summary = string.Format("bunkers {0} smoothed / {1} under threshold; trees {2} added, {3} moved, {4} refused, {5} skipped; colour {6} skipped; {7}", bDone, bSkip, tAdd, tMove, tRef, tSkip, A.colour.Length, apply ? "APPLIED (Undo available)" : "dry run");
        log.Insert(2, summary); log.Insert(3, "");
        File.WriteAllLines(dir + "/batch_fixer_" + stamp + ".md", log.ToArray());
        File.WriteAllText(dir + "/batch_fixer_" + stamp + ".json", "{\"summary\":\"" + summary.Replace("\"", "'") + "\",\"terrain_edits\":[" + string.Join(",", edits.Select(e => string.Format(CI, "{{\"terrain\":\"{0}\",\"x0\":{1},\"z0\":{2},\"w\":{3},\"h\":{4},\"changed\":{5},\"max_dz_m\":{6:F4},\"masked\":{7}}}", e.terrain, e.x0, e.z0, e.w, e.h, e.changed, e.maxDz, e.masked)).ToArray()) + "]}");
        Debug.Log("BatchCourseFixer: " + summary + " -> " + dir + "/batch_fixer_" + stamp + ".md"); return string.Join("\n", log.ToArray());
    }
}
