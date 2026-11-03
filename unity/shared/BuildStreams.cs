using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Small streams as water ribbons. streams.csv rows: stream,x,z,width,depth   (Unity metres, upstream first).
// The ribbon follows the highest course surface (OPCD mesh / cart path / terrain) under its centre line, `depth` below
// it, with the water material and GSPwater MeshCollider copied from a template water object (default: the lake's
// Spline_water_1_WaterPlane_Upper, or the original Spline_water_1_WaterPlane). Everything goes under STREAMS;
// building again replaces it. Meshes are saved in Assets/TierraRejada/generated/streams.
public class BuildStreams : EditorWindow
{
    string csvPath = "Assets/TierraRejada/streams.csv";
    string template = "Spline_water_1_WaterPlane_Upper";
    float step = 0.75f;
    const string ParentName = "STREAMS", Folder = "Assets/TierraRejada/generated/streams";

    [MenuItem("Tierra/Build Streams")]
    static void Open() { GetWindow<BuildStreams>("Build Streams"); }

    void OnGUI()
    {
        csvPath = EditorGUILayout.TextField("Streams CSV", csvPath);
        template = EditorGUILayout.TextField("Water template object", template);
        step = EditorGUILayout.FloatField("Sample step (m)", step);
        if (GUILayout.Button("Build streams")) Build();
        if (GUILayout.Button("Remove streams")) { var o = GameObject.Find(ParentName); if (o != null) Undo.DestroyObjectImmediate(o); }
    }

    static float P(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

    static float Surface(float x, float z, Transform ignore)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f))
        {
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            var pm = h.collider.sharedMaterial; if (pm != null && pm.name.StartsWith("GSPwater")) continue;
            if (h.collider.name.Contains("Water")) continue;      // lake beds (Water_Base_Lake) sit metres below the surface
            string root = h.collider.transform.root.name;
            if (root == "OPCD Meshes" || h.collider is TerrainCollider) best = Mathf.Max(best, h.point.y);
        }
        return float.IsNegativeInfinity(best) ? 0f : best;
    }

    public void Build()
    {
        if (!File.Exists(csvPath)) { Debug.LogError("BuildStreams: no " + csvPath); return; }
        GameObject tpl = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == template)
                      ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == "Spline_water_1_WaterPlane");
        if (tpl == null) { Debug.LogError("BuildStreams: no water template " + template); return; }
        var rows = new SortedDictionary<string, List<Vector4>>();
        foreach (var line in File.ReadAllLines(csvPath).Skip(1))
        {
            var f = line.Split(','); if (f.Length < 5) continue;
            string s = f[0].Trim(); if (!rows.ContainsKey(s)) rows[s] = new List<Vector4>();
            rows[s].Add(new Vector4(P(f[1]), P(f[2]), P(f[3]), P(f[4])));
        }
        var old = GameObject.Find(ParentName); if (old != null) Undo.DestroyObjectImmediate(old);
        var parent = new GameObject(ParentName); Undo.RegisterCreatedObjectUndo(parent, "Build streams");
        Directory.CreateDirectory(Folder);
        var mat = tpl.GetComponent<Renderer>().sharedMaterial; var phys = tpl.GetComponent<Collider>() ? tpl.GetComponent<Collider>().sharedMaterial : null;
        foreach (var kv in rows)
        {
            // resample the centre line
            var c = new List<Vector4>(); var pts = kv.Value;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float len = Vector2.Distance(new Vector2(pts[i].x, pts[i].y), new Vector2(pts[i + 1].x, pts[i + 1].y));
                int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                for (int k = 0; k < n; k++) c.Add(Vector4.Lerp(pts[i], pts[i + 1], k / (float)n));
            }
            c.Add(pts[pts.Count - 1]);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); float dist = 0, prevY = float.MaxValue;
            for (int i = 0; i < c.Count; i++)
            {
                Vector2 a = new Vector2(c[Mathf.Max(0, i - 1)].x, c[Mathf.Max(0, i - 1)].y), b = new Vector2(c[Mathf.Min(c.Count - 1, i + 1)].x, c[Mathf.Min(c.Count - 1, i + 1)].y);
                Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x) * (c[i].z / 2f);
                float y = Surface(c[i].x, c[i].y, parent.transform) - c[i].w;
                y = Mathf.Min(y, prevY); prevY = y;                       // water never runs uphill
                if (i > 0) dist += Vector2.Distance(new Vector2(c[i - 1].x, c[i - 1].y), new Vector2(c[i].x, c[i].y));
                verts.Add(new Vector3(c[i].x + side.x, y, c[i].y + side.y)); verts.Add(new Vector3(c[i].x - side.x, y, c[i].y - side.y));
                uvs.Add(new Vector2(0, dist / 4f)); uvs.Add(new Vector2(1, dist / 4f));
                if (i > 0) { int q = verts.Count - 4; tris.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3 }); }
            }
            var mesh = new Mesh { name = "Stream_" + kv.Key }; mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = Folder + "/Stream_" + kv.Key + ".asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject("Stream_" + kv.Key); go.transform.SetParent(parent.transform, false); go.layer = tpl.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<MeshCollider>(); col.sharedMesh = mesh; col.sharedMaterial = phys;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(tpl));
            Debug.Log(string.Format("BuildStreams: Stream_{0} {1:F0} m, {2} points, y {3:F2} -> {4:F2}, material {5}, physics {6}", kv.Key, dist, c.Count, verts[0].y, verts[verts.Count - 1].y, mat ? mat.name : "none", phys ? phys.name : "none"));
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(parent.scene);
    }
}
