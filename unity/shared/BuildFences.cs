// Tierra Rejada: build split-rail wood fences from Assets/TierraRejada/fences.csv
// (columns: segment, x, z, y  in the course's Unity frame; one row per fence point, in order).
// Put this file in Assets/Editor/ and use the menu  Tierra > Build Fences.  Unity 2018.2 compatible.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildFences : EditorWindow
{
    string csvPath = "Assets/TierraRejada/fences.csv";
    float postSpacing = 2.4f;     // ~8 ft rails
    float postHeight = 1.07f;     // 3.5 ft
    float postSize = 0.12f;
    float railLow = 0.45f, railHigh = 0.90f, railThick = 0.08f, railDepth = 0.12f;
    Color wood = new Color(0.45f, 0.33f, 0.22f);
    const string ParentName = "FENCES";

    string wallCsv = "Assets/TierraRejada/walls.csv";
    float wallAbove = 0.5f, wallBelow = 1.0f, wallThick = 0.45f, wallPiece = 2.0f;
    Color stone = new Color(0.55f, 0.50f, 0.44f);
    const string WallParent = "WALLS";

    string netCsv = "Assets/TierraRejada/nets.csv";
    float netHeight = 19f, netPoleSpacing = 10f, netPoleRadius = 0.15f;
    float netAlpha = 0.6f;
    PhysicMaterial netPhys;
    const string NetParent = "NETS";

    [MenuItem("Tierra/Build Fences")]
    static void Open() { GetWindow<BuildFences>("Build Fences"); }

    void OnGUI()
    {
        csvPath = EditorGUILayout.TextField("Fence points CSV", csvPath);
        postSpacing = EditorGUILayout.FloatField("Post spacing (m)", postSpacing);
        postHeight = EditorGUILayout.FloatField("Post height (m)", postHeight);
        railLow = EditorGUILayout.FloatField("Lower rail height (m)", railLow);
        railHigh = EditorGUILayout.FloatField("Upper rail height (m)", railHigh);
        wood = EditorGUILayout.ColorField("Wood colour", wood);
        EditorGUILayout.Space();
        if (GUILayout.Button("Build fences")) Build();
        if (GUILayout.Button("Remove fences")) Remove();
        EditorGUILayout.HelpBox("Fences go under a '" + ParentName + "' object. Building again replaces them.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stone retaining walls", EditorStyles.boldLabel);
        wallCsv = EditorGUILayout.TextField("Wall points CSV", wallCsv);
        wallAbove = EditorGUILayout.FloatField("Top above ground line (m)", wallAbove);
        wallBelow = EditorGUILayout.FloatField("Footing below ground line (m)", wallBelow);
        wallThick = EditorGUILayout.FloatField("Thickness (m)", wallThick);
        stone = EditorGUILayout.ColorField("Stone colour", stone);
        if (GUILayout.Button("Build walls")) BuildWalls();
        if (GUILayout.Button("Remove walls")) { var o = GameObject.Find(WallParent); if (o != null) Undo.DestroyObjectImmediate(o); }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Driving range nets", EditorStyles.boldLabel);
        netCsv = EditorGUILayout.TextField("Net points CSV", netCsv);
        netHeight = EditorGUILayout.FloatField("Net height (m)", netHeight);
        netPoleSpacing = EditorGUILayout.FloatField("Pole spacing (m)", netPoleSpacing);
        netAlpha = EditorGUILayout.Slider("Net visibility", netAlpha, 0.05f, 0.8f);
        if (netPhys == null) netPhys = Resources.Load<PhysicMaterial>("Physics/GSPdeeprough");   // solid: GSPleaves let balls straight through the net
        netPhys = (PhysicMaterial)EditorGUILayout.ObjectField("Net ball physics", netPhys, typeof(PhysicMaterial), false);
        if (GUILayout.Button("Build nets")) BuildNets();
        if (GUILayout.Button("Remove nets")) { var o = GameObject.Find(NetParent); if (o != null) Undo.DestroyObjectImmediate(o); }
    }

    void Remove()
    {
        var old = GameObject.Find(ParentName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    void Build()
    {
        if (!File.Exists(csvPath)) { EditorUtility.DisplayDialog("Build Fences", "CSV not found:\n" + csvPath, "OK"); return; }
        var segs = new SortedDictionary<int, List<Vector3>>();
        var lines = File.ReadAllLines(csvPath);
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split(',');
            if (f.Length < 4) continue;
            int s = int.Parse(f[0].Trim());
            if (!segs.ContainsKey(s)) segs[s] = new List<Vector3>();
            segs[s].Add(new Vector3(P(f[1]), P(f[3]), P(f[2])));
        }

        Remove();
        var parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Build Fences");
        var mat = WoodMaterial();
        var phys = Resources.Load<PhysicMaterial>("Physics/GSPtimber");
        int posts = 0, rails = 0;

        foreach (var kv in segs)
        {
            var seg = new GameObject("Fence_" + kv.Key);
            seg.transform.SetParent(parent.transform);
            var pts = Resample(kv.Value);
            var tops = new List<Vector3>();
            foreach (var p in pts)
            {
                var g = Ground(p);
                tops.Add(g);
                Box(seg.transform, "Post", g + Vector3.up * (postHeight / 2f), Quaternion.identity,
                    new Vector3(postSize, postHeight, postSize), mat, phys);
                posts++;
            }
            for (int i = 0; i + 1 < tops.Count; i++)
            {
                Vector3 a = tops[i], b = tops[i + 1], d = b - a;
                var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
                float len = d.magnitude + postSize;
                foreach (float h in new[] { railLow, railHigh })
                {
                    Box(seg.transform, "Rail", (a + b) / 2f + Vector3.up * h, rot, new Vector3(railDepth, railThick, len), mat, phys);
                    rails++;
                }
            }
        }
        Debug.Log("Built " + segs.Count + " fence runs: " + posts + " posts, " + rails + " rails" + (phys == null ? " (GSPtimber not found!)" : ", GSPtimber physics"));
    }

    static SortedDictionary<int, List<Vector3>> ReadRuns(string path)
    {
        var segs = new SortedDictionary<int, List<Vector3>>();
        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split(',');
            if (f.Length < 4) continue;
            int s = int.Parse(f[0].Trim());
            if (!segs.ContainsKey(s)) segs[s] = new List<Vector3>();
            segs[s].Add(new Vector3(P(f[1]), P(f[3]), P(f[2])));
        }
        return segs;
    }

    void BuildWalls()
    {
        if (!File.Exists(wallCsv)) { EditorUtility.DisplayDialog("Build Fences", "CSV not found:\n" + wallCsv, "OK"); return; }
        var old = GameObject.Find(WallParent);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var parent = new GameObject(WallParent);
        Undo.RegisterCreatedObjectUndo(parent, "Build Walls");
        var mat = ColorMaterial("Assets/TierraRejada/WallStone.mat", stone);
        var phys = Resources.Load<PhysicMaterial>("Physics/GSPstone");
        int pieces = 0;
        float keep = postSpacing; postSpacing = wallPiece;
        foreach (var kv in ReadRuns(wallCsv))
        {
            var run = new GameObject("Wall_" + kv.Key);
            run.transform.SetParent(parent.transform);
            var pts = Resample(kv.Value);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 a = Ground(pts[i]), b = Ground(pts[i + 1]);
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float h = wallAbove + wallBelow;
                float midY = (a.y + b.y) / 2f + (wallAbove - wallBelow) / 2f;
                Box(run.transform, "Stone", new Vector3((a.x + b.x) / 2f, midY, (a.z + b.z) / 2f),
                    Quaternion.LookRotation(flat.normalized, Vector3.up),
                    new Vector3(wallThick, h + Mathf.Abs(b.y - a.y), flat.magnitude + 0.05f), mat, phys);
                pieces++;
            }
        }
        postSpacing = keep;
        Debug.Log("Built walls: " + pieces + " pieces" + (phys == null ? " (GSPstone not found!)" : ", GSPstone physics"));
    }

    void BuildNets()
    {
        if (!File.Exists(netCsv)) { EditorUtility.DisplayDialog("Build Fences", "CSV not found:\n" + netCsv, "OK"); return; }
        var old = GameObject.Find(NetParent);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var parent = new GameObject(NetParent);
        Undo.RegisterCreatedObjectUndo(parent, "Build Nets");
        var poleMat = ColorMaterial("Assets/TierraRejada/NetPole.mat", new Color(0.25f, 0.25f, 0.24f));
        var netMat = NetMaterial();
        var poleTimber = Resources.Load<PhysicMaterial>("Physics/GSPtimber");
        int poles = 0, panels = 0;
        float keep = postSpacing; postSpacing = netPoleSpacing;
        foreach (var kv in ReadRuns(netCsv))
        {
            var run = new GameObject("Net_" + kv.Key);
            run.transform.SetParent(parent.transform);
            var pts = Resample(kv.Value);
            var g = new List<Vector3>();
            foreach (var p in pts) g.Add(Ground(p));
            foreach (var p in g)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(run.transform);
                pole.transform.position = p + Vector3.up * (netHeight / 2f);
                pole.transform.localScale = new Vector3(netPoleRadius * 2f, netHeight / 2f, netPoleRadius * 2f);
                pole.GetComponent<Renderer>().sharedMaterial = poleMat;
                if (poleTimber != null) pole.GetComponent<Collider>().sharedMaterial = poleTimber;
                poles++;
            }
            for (int i = 0; i + 1 < g.Count; i++)
            {
                Vector3 a = g[i], b = g[i + 1];
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float bottom = Mathf.Min(a.y, b.y) - 0.3f, top = (a.y + b.y) / 2f + netHeight;
                Box(run.transform, "NetPanel", new Vector3((a.x + b.x) / 2f, (bottom + top) / 2f, (a.z + b.z) / 2f),
                    Quaternion.LookRotation(flat.normalized, Vector3.up), new Vector3(0.05f, top - bottom, flat.magnitude), netMat, netPhys);
                panels++;
            }
        }
        postSpacing = keep;
        Debug.Log("Built nets: " + poles + " poles, " + panels + " net panels, ball physics " + (netPhys != null ? netPhys.name : "none"));
    }

    Material NetMaterial()
    {
        const string path = "Assets/TierraRejada/RangeNet.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Mode", 3f);   // Transparent
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        m.color = new Color(0.3f, 0.3f, 0.3f, netAlpha);
        m.SetFloat("_Glossiness", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    Material ColorMaterial(string path, Color c)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c;
        m.SetFloat("_Glossiness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // posts at every pin plus evenly spaced in between (no gap wider than postSpacing)
    List<Vector3> Resample(List<Vector3> pin)
    {
        var o = new List<Vector3> { pin[0] };
        for (int i = 0; i + 1 < pin.Count; i++)
        {
            Vector3 a = pin[i], b = pin[i + 1];
            float flat = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(flat / postSpacing));
            for (int k = 1; k <= n; k++) o.Add(Vector3.Lerp(a, b, (float)k / n));
        }
        return o;
    }

    // stand on the course surface (OPCD meshes or terrain), ignoring trees and anything else
    Vector3 Ground(Vector3 p)
    {
        float mesh = float.NegativeInfinity, terrain = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 50f, p.z), Vector3.down, 120f))
        {
            if (h.collider is TerrainCollider) { terrain = Mathf.Max(terrain, h.point.y); continue; }
            for (var t = h.collider.transform; t != null; t = t.parent)
                if (t.name == "OPCD Meshes") { mesh = Mathf.Max(mesh, h.point.y); break; }
        }
        // course meshes are the playing surface; the terrain under them was lowered ~1 m,
        // so without a mesh hit use the original LiDAR ground height from the CSV
        float y = !float.IsNegativeInfinity(mesh) ? mesh : Mathf.Max(p.y, terrain);
        return new Vector3(p.x, y, p.z);
    }

    static void Box(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material m, PhysicMaterial phys)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.position = pos;
        g.transform.rotation = rot;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        var c = g.GetComponent<Collider>();
        if (phys != null) c.sharedMaterial = phys;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
    }

    Material WoodMaterial()
    {
        const string path = "Assets/TierraRejada/FenceWood.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = wood;
        m.SetFloat("_Glossiness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static float P(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }
}
