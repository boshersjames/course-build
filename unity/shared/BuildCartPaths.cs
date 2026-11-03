// Tierra Rejada: build concrete cart paths from Assets/TierraRejada/cartpaths.csv
// (columns: segment, x, z, y in the course's Unity frame; one row per point, in order along each path).
// Put this file in Assets/Editor/ and use the menu  Tierra > Build Cart Paths.  Unity 2018.2 compatible.
using System.Collections.Generic;
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
}
