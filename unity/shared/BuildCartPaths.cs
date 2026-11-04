// Tierra Rejada: build concrete cart paths from Assets/TierraRejada/cartpaths.csv
// (columns: segment, x, z, y in the course's Unity frame; one row per point, in order along each path).
// Put this file in Assets/Editor/ and use the menu  Tierra > Build Cart Paths.  Unity 2018.2 compatible.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildCartPaths : EditorWindow
{
    string csvPath = (Directory.Exists("Assets/Moorpark") ? "Assets/Moorpark" : "Assets/TierraRejada") + "/cartpaths.csv";
    float width = 2.4f;        // ~8 ft
    float step = 1.5f;         // metres between cross-sections
    float lift = 0.04f;        // above the course surface, to avoid flicker
    Material concrete;
    PhysicMaterial phys;
    const string ParentName = "CART_PATHS";
    // course folder: Assets/Moorpark in the Moorpark project (which also has Assets/TierraRejada/Vegetation), else Assets/TierraRejada
    static readonly string CourseFolder = Directory.Exists("Assets/Moorpark") ? "Assets/Moorpark" : "Assets/TierraRejada";
    static readonly string MeshFolder = CourseFolder + "/CartPathMeshes";

    [MenuItem("Tierra/Build Cart Paths")]
    static void Open() { GetWindow<BuildCartPaths>("Build Cart Paths"); }

    void OnGUI()
    {
        csvPath = EditorGUILayout.TextField("Path points CSV", csvPath);
        width = EditorGUILayout.FloatField("Path width (m)", width);
        step = EditorGUILayout.FloatField("Detail step (m)", step);
        lift = EditorGUILayout.FloatField("Lift above ground (m)", lift);
        if (concrete == null) concrete = FindMaterial("OPCD MAHS Concrete - Asphalt default");
        if (phys == null) phys = Resources.Load<PhysicMaterial>("Physics/GSPconcrete");
        concrete = (Material)EditorGUILayout.ObjectField("Path material", concrete, typeof(Material), false);
        phys = (PhysicMaterial)EditorGUILayout.ObjectField("Ball physics", phys, typeof(PhysicMaterial), false);
        EditorGUILayout.Space();
        if (GUILayout.Button("Build cart paths")) Build();
        if (GUILayout.Button("Remove cart paths")) Remove();
        EditorGUILayout.HelpBox("Paths go under a '" + ParentName + "' object; their meshes are saved in " + MeshFolder + ". Building again replaces them.", MessageType.Info);
    }

    static Material FindMaterial(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.Contains("SampleScene")) continue;          // skip the placeholder copies inside sample FBX files
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null && m.name == name) return m;
        }
        return null;
    }

    void Remove()
    {
        var old = GameObject.Find(ParentName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    void Build()
    {
        if (!File.Exists(csvPath)) { EditorUtility.DisplayDialog("Build Cart Paths", "CSV not found:\n" + csvPath, "OK"); return; }
        if (concrete == null) { EditorUtility.DisplayDialog("Build Cart Paths", "Pick a path material first.", "OK"); return; }

        var runs = new SortedDictionary<int, List<Vector3>>();
        var lines = File.ReadAllLines(csvPath);
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split(',');
            if (f.Length < 4) continue;
            int s = int.Parse(f[0].Trim());
            if (!runs.ContainsKey(s)) runs[s] = new List<Vector3>();
            runs[s].Add(new Vector3(P(f[1]), P(f[3]), P(f[2])));
        }

        Remove();
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(CourseFolder, "CartPathMeshes");
        foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

        var parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Build Cart Paths");
        float total = 0f;
        int n = 0;
        foreach (var kv in runs)
        {
            EditorUtility.DisplayProgressBar("Building cart paths", "Path " + (n + 1) + " of " + runs.Count, (float)n / runs.Count);
            var centre = Resample(kv.Value);
            if (centre.Count < 2) continue;
            var mesh = Ribbon(centre, ref total);
            mesh.name = "CartPath_" + kv.Key;
            AssetDatabase.CreateAsset(mesh, MeshFolder + "/" + mesh.name + ".asset");

            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent.transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = concrete;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            if (phys != null) col.sharedMaterial = phys;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            n++;
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log("Built " + n + " cart paths, " + total.ToString("0") + " m in total, " + width + " m wide, material " + concrete.name + (phys != null ? ", " + phys.name + " physics" : ""));
    }

    List<Vector3> Resample(List<Vector3> pin)
    {
        var o = new List<Vector3> { pin[0] };
        for (int i = 0; i + 1 < pin.Count; i++)
        {
            Vector3 a = pin[i], b = pin[i + 1];
            float flat = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            if (flat < 0.01f) continue;
            int k = Mathf.Max(1, Mathf.CeilToInt(flat / step));
            for (int j = 1; j <= k; j++) o.Add(Vector3.Lerp(a, b, (float)j / k));
        }
        return o;
    }

    Mesh Ribbon(List<Vector3> c, ref float total)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float dist = 0f;
        for (int i = 0; i < c.Count; i++)
        {
            Vector3 fwd = (i == 0) ? c[1] - c[0] : (i == c.Count - 1) ? c[i] - c[i - 1] : c[i + 1] - c[i - 1];
            fwd.y = 0f; fwd.Normalize();
            Vector3 side = new Vector3(fwd.z, 0f, -fwd.x) * (width / 2f);
            if (i > 0) dist += new Vector2(c[i].x - c[i - 1].x, c[i].z - c[i - 1].z).magnitude;
            verts.Add(Ground(c[i] - side) + Vector3.up * lift);
            verts.Add(Ground(c[i] + side) + Vector3.up * lift);
            uvs.Add(new Vector2(0f, dist / width));
            uvs.Add(new Vector2(1f, dist / width));
            if (i > 0)
            {
                int b = verts.Count - 4;
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
        }
        total += dist;
        var m = new Mesh();
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // the course surface (OPCD meshes) under a point; the terrain was lowered ~1 m under them
    static Vector3 Ground(Vector3 p)
    {
        float mesh = float.NegativeInfinity, terrain = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 50f, p.z), Vector3.down, 120f))
        {
            if (h.collider is TerrainCollider) { terrain = Mathf.Max(terrain, h.point.y); continue; }
            for (var t = h.collider.transform; t != null; t = t.parent)
                if (t.name == "OPCD Meshes") { mesh = Mathf.Max(mesh, h.point.y); break; }
        }
        float y = !float.IsNegativeInfinity(mesh) ? mesh : Mathf.Max(p.y, terrain);
        return new Vector3(p.x, y, p.z);
    }

    static float P(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

    // ---------- turf over paths (rules/paths_fences.json checks): what sits on top of each cart path, and sinking it under ----------
    // Bridge: invoke -Tool BuildCartPaths -Method CheckTurf [-Set @{ hole = "10" }]   read-only report: per path and per turf mesh, the share of the
    //         path surface where a turf mesh (fairway, tee, rough, blend, collar) is the top collider, with the extent; writes AutomationOutput/checks/path_turf.csv
    //         invoke -Tool BuildCartPaths -Method SinkTurf [-Set @{ hole = "10" }]    every turf vertex over a path collider goes to path top - checks.turf_under_path_sink_m
    //         (an A-owned mesh copy, collider follows, COURSEBUILD_LOG swap: CourseCollarFlush.Revert puts it back). Triangles are never removed, so no sawtooth.
    [Serializable] public class PathChecks { public float overlap_grid_m = 0.25f, rough_poke_max_pct = 2f, tee_pad_off_path_m = 2f, turf_under_path_sink_m = 0.05f; }
    [Serializable] class PathRules { public PathChecks checks = new PathChecks(); }
    public static string hole = "";
    static PathChecks Checks() { var rp = "Assets/CourseBuild/rules/paths_fences.json"; if (!File.Exists(rp)) return new PathChecks(); var r = JsonUtility.FromJson<PathRules>(File.ReadAllText(rp).TrimStart('﻿')); return r != null && r.checks != null ? r.checks : new PathChecks(); }
    static bool IsPathCollider(Collider c) { var n = c.name.ToLower(); var pm = c.sharedMaterial ? c.sharedMaterial.name : ""; return n.Contains("cartpath") || n.Contains("cart_path") || (n.Contains("path") && pm == "GSPconcrete") || c.transform.parent && c.transform.parent.name == ParentName; }
    static bool IsTurf(Collider c) { var n = c.name.ToLower(); if (c.GetComponentInParent<LODGroup>() || IsPathCollider(c)) return false; var pm = c.sharedMaterial ? c.sharedMaterial.name : ""; return pm == "GSPfairway" || pm == "GSPtee" || pm == "GSPrough" || pm == "GSPdeeprough" || pm == "GSPgreen" || n.Contains("fairway") || n.Contains("_tee_-_mesh") || n.Contains("rough") || n.Contains("blend") || n.Contains("collar"); }
    static List<MeshCollider> PathColliders(string holeFilter)
    {
        var all = UnityEngine.Object.FindObjectsOfType<MeshCollider>().Where(c => c.gameObject.activeInHierarchy && c.sharedMesh && IsPathCollider(c)).ToList();
        if (string.IsNullOrEmpty(holeFilter)) return all;
        // hole filter: paths whose bounds come within 40 m of the hole's GKD line (tee -> aims -> green)
        var gp = "Assets/CourseBuild/course.json"; if (!File.Exists(gp)) return all;
        var cj = JsonUtility.FromJson<CJ>(File.ReadAllText(gp).TrimStart('﻿')); if (cj == null || cj.paths == null || string.IsNullOrEmpty(cj.paths.gkd)) return all;
        var gk = Path.IsPathRooted(cj.paths.gkd) ? cj.paths.gkd : Path.GetFullPath(Path.Combine(cj.paths.course_repo_abs ?? ".", cj.paths.gkd)); if (!File.Exists(gk)) return all;
        int hn; if (!int.TryParse(holeFilter, out hn)) return all; var g = JsonUtility.FromJson<GK>(File.ReadAllText(gk).TrimStart('﻿')); var H = g.Holes.FirstOrDefault(h => h.HoleNumber == hn); if (H == null) return all;
        var line = new List<Vector2>(); foreach (var k in new[] { "Black", "Blue", "White", "Gold", "Green", "Red", "Yellow", "AimPoint1", "AimPoint2", "GreenCenterPoint" }) { var t = H.Tees.FirstOrDefault(x => x.TeeType == k && x.Position != null && (x.Position.x != 0 || x.Position.z != 0)); if (t != null && (line.Count == 0 || k.StartsWith("Aim") || k.StartsWith("Green"))) line.Add(new Vector2(t.Position.x, t.Position.z)); }
        Func<Vector2, float> off = q => { float best = 1e9f; for (int i = 1; i < line.Count; i++) { var a = line[i - 1]; var ab = line[i] - a; float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f)); best = Mathf.Min(best, Vector2.Distance(q, a + ab * u)); } return best; };
        return all.Where(c => { var b = c.bounds; return off(new Vector2(b.center.x, b.center.z)) < 40 + Mathf.Max(b.extents.x, b.extents.z); }).ToList();
    }
    [Serializable] class P3 { public float x, y, z; } [Serializable] class TeeK { public string TeeType; public P3 Position; } [Serializable] class HoleK { public int HoleNumber; public TeeK[] Tees; } [Serializable] class GK { public HoleK[] Holes; }
    [Serializable] class CPaths { public string gkd, course_repo_abs; } [Serializable] class CJ { public CPaths paths; }

    public static string CheckTurf() { return TurfPass(false); }
    public static string SinkTurf() { return TurfPass(true); }

    static string TurfPass(bool apply)
    {
        Physics.SyncTransforms(); var R = Checks(); var paths = PathColliders(hole); if (paths.Count == 0) return "no cart path colliders" + (string.IsNullOrEmpty(hole) ? "" : " near hole " + hole);
        var o = new List<string> { (apply ? "SINK" : "CHECK") + " turf over " + paths.Count + " cart path colliders (grid " + R.overlap_grid_m.ToString(CultureInfo.InvariantCulture) + " m, limit " + R.rough_poke_max_pct.ToString(CultureInfo.InvariantCulture) + "% of a path's surface)" + (string.IsNullOrEmpty(hole) ? "" : ", hole " + hole) };
        var csv = new List<string> { "path,turf_mesh,x,z,path_y,turf_y" }; var perMesh = new Dictionary<MeshCollider, List<Vector3>>(); bool anyOver = false;
        foreach (var pc in paths)
        {
            var b = pc.bounds; int samples = 0, over = 0; var byTurf = new Dictionary<string, int>(); float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
            for (float x = b.min.x; x <= b.max.x; x += R.overlap_grid_m) for (float z = b.min.z; z <= b.max.z; z += R.overlap_grid_m)
            {
                RaycastHit ph; if (!pc.Raycast(new Ray(new Vector3(x, b.max.y + 5, z), Vector3.down), out ph, b.size.y + 20)) continue; samples++;
                Collider top = null; float topY = float.MinValue;
                foreach (var h in Physics.RaycastAll(new Vector3(x, b.max.y + 5, z), Vector3.down, b.size.y + 20)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.isTrigger) continue; if (h.point.y > topY) { topY = h.point.y; top = h.collider; } }
                if (top == null || top == pc || !IsTurf(top)) continue;
                over++; int n; byTurf.TryGetValue(top.name, out n); byTurf[top.name] = n + 1; minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
                csv.Add(string.Join(",", new[] { pc.name, top.name, x.ToString("F2", CultureInfo.InvariantCulture), z.ToString("F2", CultureInfo.InvariantCulture), ph.point.y.ToString("F3", CultureInfo.InvariantCulture), topY.ToString("F3", CultureInfo.InvariantCulture) }));
                var tmc = top as MeshCollider; if (tmc) { List<Vector3> l; if (!perMesh.TryGetValue(tmc, out l)) perMesh[tmc] = l = new List<Vector3>(); l.Add(new Vector3(x, ph.point.y, z)); }
            }
            if (samples == 0) continue; float pct = 100f * over / samples; bool bad = pct > R.rough_poke_max_pct; anyOver |= bad;
            o.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1} of {2} samples under turf ({3:F1}%){4}{5}", pc.name, over, samples, pct, over > 0 ? "  x " + minX.ToString("F0") + "-" + maxX.ToString("F0") + " z " + minZ.ToString("F0") + "-" + maxZ.ToString("F0") + "  by: " + string.Join(", ", byTurf.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value).ToArray()) : "", bad ? "  <-- over the limit" : ""));
        }
        string outDir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/AutomationOutput/checks"; Directory.CreateDirectory(outDir); File.WriteAllLines(outDir + "/path_turf.csv", csv.ToArray());
        if (apply)
        {
            CourseCollarFlush.EnsureFolders(); int meshes = 0;
            foreach (var kv in perMesh)
            {
                var mf = kv.Key.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; var src = mf.sharedMesh; var v = src.vertices; int moved = 0;
                var pcs = paths; // sink every vertex of this mesh that stands over any path collider
                for (int i = 0; i < v.Length; i++)
                {
                    var w = mf.transform.TransformPoint(v[i]); float best = float.NaN;
                    foreach (var pc in pcs) { var pb = pc.bounds; if (w.x < pb.min.x - 0.3f || w.x > pb.max.x + 0.3f || w.z < pb.min.z - 0.3f || w.z > pb.max.z + 0.3f) continue; RaycastHit ph; if (pc.Raycast(new Ray(new Vector3(w.x, pb.max.y + 5, w.z), Vector3.down), out ph, pb.size.y + 20)) { if (float.IsNaN(best) || ph.point.y > best) best = ph.point.y; } }
                    if (float.IsNaN(best)) continue; float target = best - R.turf_under_path_sink_m; if (w.y > target) { w.y = target; v[i] = mf.transform.InverseTransformPoint(w); moved++; }
                }
                if (moved == 0) continue; var nm = CourseCollarFlush.SaveMesh(src, v, mf.name + "_underpath_" + DateTime.Now.ToString("HHmmss"), "_up"); CourseCollarFlush.Swap(mf, nm, "collarflush"); meshes++;
                o.Add("  sunk " + moved + " vertices of " + mf.name + " under the path");
            }
            AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            o.Add(meshes + " turf meshes edited; CourseCollarFlush.Revert puts them back; run CheckTurf again to confirm 0%");
        }
        else if (anyOver) o.Add("SinkTurf applies the fix (turf vertices over a path go " + (R.turf_under_path_sink_m * 100).ToString("F0") + " cm under it)");
        var txt = string.Join("\n", o.ToArray()); File.WriteAllText(outDir + "/path_turf.txt", txt); return txt;
    }
}
