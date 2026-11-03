// Moorpark: put the OPCD course meshes (Assets/Moorpark/FBX, from moorpark/tools/opcd_*.py) in the scene.
// Same as GSP > Add OPCD Meshes, without its folder picker and success dialog, so the automation bridge can run it.
// Unity 2018.2 compatible.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkMeshes
{
    public const string Folder = "Assets/Moorpark/FBX";
    public const string ParentName = "OPCD Meshes";

    // types, menu items and no-argument methods in the GSP editor DLLs (to drive SetMeshMaterials / Lower Terrain)
    public static string GspTools()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            string an = asm.GetName().Name;
            if (an != "SetMeshEditorDLL" && an != "EditorToolsDLL" && an != "SetMeshRuntimeDLL") continue;
            System.Type[] types;
            try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
            foreach (var t in types)
            {
                if (t == null) continue;
                var ms = new System.Collections.Generic.List<string>();
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    string menu = "";
                    foreach (var a in m.GetCustomAttributes(typeof(MenuItem), false)) menu = " [" + ((MenuItem)a).menuItem + "]";
                    if (m.GetParameters().Length == 0 && !m.Name.StartsWith("<") && !m.IsSpecialName) ms.Add(m.Name + menu);
                }
                sb.AppendLine(an + ": " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.Name : "") + "  " + string.Join(", ", ms.ToArray()));
            }
        }
        return sb.ToString();
    }

    // run a no-argument method on the open SetMeshMaterials v4 window and return the full exception if it throws
    public static string RunSetMesh(string method)
    {
        System.Type t = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            if (asm.GetName().Name == "SetMeshEditorDLL") t = asm.GetType("SetMeshEditorDLL.SetMeshMaterialv4_theme");
        var open = Resources.FindObjectsOfTypeAll(t);
        if (open.Length == 0) return "SetMeshMaterials v4 window isn't open";
        var m = t.GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        try { m.Invoke(open[0], null); return method + " ok"; }
        catch (System.Reflection.TargetInvocationException e) { return e.InnerException.ToString(); }
    }
    public static string ApplyMaterials() { return RunSetMesh("SetInkScapeMeshMaterials"); }

    // SetMeshMaterials v4 expects a MeshCollider on every spline mesh (Tierra's FBX came in with colliders)
    public static string AddColliders()
    {
        var root = GameObject.Find(ParentName); int n = 0;
        // and a MeshChannelMaterialsStore (SetMeshRuntimeDLL), which it fills with the theme's channel materials
        // and blend strips a BlendTarget (the material they fade into)
        System.Type store = null, blend = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            if (asm.GetName().Name == "SetMeshRuntimeDLL")
            { store = asm.GetType("SetMeshRuntimeDLL.MeshChannelMaterialsStore"); blend = asm.GetType("SetMeshRuntimeDLL.BlendTarget"); }
        int s = 0, b = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.GetComponent<MeshCollider>() == null) { mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; n++; }
            if (store != null && mf.GetComponent(store) == null) { mf.gameObject.AddComponent(store); s++; }
            if (blend != null && mf.name.Contains("_-_Blend") && mf.GetComponent(blend) == null) { mf.gameObject.AddComponent(blend); b++; }
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        return "added " + n + " mesh colliders, " + s + " MeshChannelMaterialsStore, " + b + " BlendTarget" + (store == null || blend == null ? " (type not found!)" : "");
    }

    // GSP > Lower Terrain Under Meshes didn't change anything here, so the same job: every heightmap sample under a course
    // mesh goes to (mesh height - Clearance). Only Spline_ meshes count (not trees, shrubs, paths or the water planes).
    public static float Clearance = 0.4f;
    // back to the LiDAR heights (Assets/Moorpark/heightmap_2049.raw), before lowering again
    public static string ResetTerrain()
    {
        var t = GameObject.Find("Terrain (Inner)").GetComponent<Terrain>(); var td = t.terrainData;
        byte[] b = File.ReadAllBytes("Assets/Moorpark/heightmap_2049.raw"); int n = 2049;
        var h = new float[n, n];
        for (int z = 0, i = 0; z < n; z++) for (int x = 0; x < n; x++, i += 2) h[z, x] = (b[i] | (b[i + 1] << 8)) / 65535f;
        Undo.RegisterCompleteObjectUndo(td, "Reset terrain"); td.SetHeights(0, 0, h); EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        return "terrain heights reset from the LiDAR heightmap";
    }

    public static string LowerTerrain()
    {
        var t = GameObject.Find("Terrain (Inner)").GetComponent<Terrain>();
        var td = t.terrainData;
        int n = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, n, n);
        Vector3 size = td.size, pos = t.transform.position;
        int lowered = 0;
        try
        {
            for (int j = 0; j < n; j++)
            {
                if (j % 64 == 0 && EditorUtility.DisplayCancelableProgressBar("Lower terrain under meshes", j + " / " + n, (float)j / n)) break;
                float z = pos.z + size.z * j / (n - 1);
                for (int i = 0; i < n; i++)
                {
                    float x = pos.x + size.x * i / (n - 1);
                    float top = -1e9f;
                    foreach (var hit in Physics.RaycastAll(new Vector3(x, pos.y + size.y + 50f, z), Vector3.down, size.y + 200f))
                        if (hit.collider.name.StartsWith("Spline_") && !hit.collider.name.Contains("WaterPlane") && hit.point.y > top) top = hit.point.y;
                    if (top < -1e8f) continue;
                    float want = (top - Clearance - pos.y) / size.y;
                    if (want < h[j, i]) { h[j, i] = Mathf.Max(0f, want); lowered++; }
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Undo.RegisterCompleteObjectUndo(td, "Lower terrain under meshes");
        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        return "lowered " + lowered + " heightmap samples to " + Clearance + " m under the course meshes";
    }

    // terrain height vs the course mesh at a few greens (is the terrain below the meshes?)
    public static string ProbeHeights()
    {
        var t = GameObject.Find("Terrain (Inner)").GetComponent<Terrain>();
        var sb = new System.Text.StringBuilder();
        float[][] pts = { new[] { 1309.9f, 1776.1f }, new[] { 918.3f, 1365.2f }, new[] { 1211.3f, 1365.7f }, new[] { 1170.2f, 1224.7f }, new[] { 1240f, 1320f } };
        foreach (var p in pts)
        {
            float th = t.SampleHeight(new Vector3(p[0], 0, p[1])) + t.transform.position.y;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p[0], 1000, p[1]), Vector3.down, 2000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var hs = new System.Collections.Generic.List<string>();
            foreach (var h in hits) hs.Add(h.collider.name + "@" + h.point.y.ToString("0.00"));
            sb.AppendLine(p[0] + "," + p[1] + " terrain " + th.ToString("0.00") + " hits: " + string.Join(" | ", hs.ToArray()));
        }
        return sb.ToString();
    }

    // the satellite on every course material that reads it (SetMeshMaterials left _Satellite empty and the size at 2000)
    public static string SetSatellite()
    {
        var sat = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Moorpark/satellite.jpg");
        var done = new System.Collections.Generic.HashSet<Material>();
        foreach (var r in GameObject.Find(ParentName).GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || done.Contains(m) || !m.HasProperty("_Satellite")) continue;
                m.SetTexture("_Satellite", sat); m.SetFloat("_TerrainSize", 3000f);
                m.SetFloat("_SatelliteOffsetX", 0f); m.SetFloat("_SatelliteOffsetY", 0f);
                EditorUtility.SetDirty(m); done.Add(m);
            }
        AssetDatabase.SaveAssets();
        var names = new System.Collections.Generic.List<string>(); foreach (var m in done) names.Add(m.name);
        return "satellite set on " + done.Count + " materials: " + string.Join(", ", names.ToArray());
    }

    // every tee should land on a Tee mesh and every pin on a Green mesh, inside the 3000 m square
    [System.Serializable] class V3 { public float x, y, z; }
    [System.Serializable] class Tee { public string TeeType; public bool Enabled; public V3 Position; }
    [System.Serializable] class Pin { public string Day; public V3 Position; }
    [System.Serializable] class Hole { public int HoleNumber; public Tee[] Tees; public Pin[] Pins; }
    [System.Serializable] class Haz { public bool innerOOB, hasDZ; public V3 DZpos; }
    [System.Serializable] class Gkd { public string CourseName; public Hole[] Holes; public Haz[] Hazards; }
    public static string CheckGkds()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string f in Directory.GetFiles("C:/Users/user/Documents/moorpark/build/gspro", "*.GKD", SearchOption.AllDirectories))
        {
            var g = JsonUtility.FromJson<Gkd>(File.ReadAllText(f));
            int tees = 0, teeOk = 0, pins = 0, pinOk = 0; var bad = new System.Collections.Generic.List<string>();
            foreach (var h in g.Holes)
            {
                string label = CourseHoleNames.Label(g.CourseName, h.HoleNumber, g.Holes.Length);
                foreach (var t in h.Tees)
                {
                    if (!t.Enabled || t.Position == null) continue; tees++;
                    string s = Surface(t.Position); if (s.Contains("Tee")) teeOk++; else if (bad.Count < 12) bad.Add(label + " " + t.TeeType + " tee on " + s);
                }
                foreach (var p in h.Pins)
                {
                    if (p.Position == null) continue; pins++;
                    string s = Surface(p.Position); if (s.Contains("Green")) pinOk++; else if (bad.Count < 12) bad.Add(label + " " + p.Day + " pin on " + s);
                }
            }
            int wet = 0; foreach (var h in g.Holes) { foreach (var t in h.Tees) if (t.Enabled && t.Position != null && Surface(t.Position).Contains("Water")) wet++; foreach (var p in h.Pins) if (p.Position != null && Surface(p.Position).Contains("Water")) wet++; }
            var dzs = new System.Collections.Generic.List<string>();
            if (g.Hazards != null) foreach (var z in g.Hazards) if (z.hasDZ) dzs.Add(Surface(z.DZpos).Replace("Spline_", "").Replace("_-_Mesh", ""));
            sb.AppendLine("    in water: " + wet + " tees/pins; drop zones on: " + string.Join(", ", dzs.ToArray()));
            sb.AppendLine(Path.GetFileName(f) + ": tees " + teeOk + "/" + tees + " on a tee box, pins " + pinOk + "/" + pins + " on a green" + (bad.Count > 0 ? "\n    " + string.Join("\n    ", bad.ToArray()) : ""));
        }
        return sb.ToString();
    }

    public static string ProbeDZ()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var q in new[] { new Vector2(1288.5f, 1796.6f), new Vector2(1150.1f, 1780.4f) })
        {
            sb.Append(q + ":");
            foreach (var hit in Physics.RaycastAll(new Vector3(q.x, 400f, q.y), Vector3.down, 800f)) sb.Append(" " + hit.collider.name + "@" + hit.point.y.ToString("0.00"));
            sb.AppendLine();
        }
        var wp = GameObject.Find("Spline_water_1_WaterPlane");
        if (wp) sb.AppendLine("water_1 plane bounds " + wp.GetComponent<MeshRenderer>().bounds);
        return sb.ToString();
    }

    static string Surface(V3 p)    {
        if (p.x < 0 || p.z < 0 || p.x > 3000 || p.z > 3000) return "OUTSIDE the square";
        foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 400f, p.z), Vector3.down, 800f))
            if (hit.collider.name.StartsWith("Spline_")) return hit.collider.name;
        return "terrain (no course mesh)";
    }

    // Flyover Preview remembers a GKD per scene: Moorpark and each layout scene open their own GSPro course file
    public static string SetFlyoverGkds()
    {
        const string C = @"C:\GSProV1\Core\GSP\Courses\";
        EditorPrefs.SetString("CourseFlyover.gkd.Moorpark", C + @"Moorpark_RidgelineCreekside\Moorpark_RidgelineCreekside.GKD");
        int n = 1;
        foreach (string d in Directory.GetDirectories(C, "Moorpark_*"))
        {
            string name = Path.GetFileName(d), gkd = Path.Combine(d, name + ".GKD");
            if (File.Exists(gkd)) { EditorPrefs.SetString("CourseFlyover.gkd." + name, gkd); n++; }
        }
        return "flyover GKD set for " + n + " scenes (Moorpark = Ridgeline / Creekside)";
    }

    public static string MatInfo()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string n in new[] { "Spline_rough_173_0_Rough_-_Mesh", "Spline_green_5_Green_-_Mesh", "Spline_deep_0_0_Deep_-_Mesh", "Spline_water_0_Water_Base_Lake_-_Mesh" })
        {
            var g = GameObject.Find(n); if (g == null) { sb.AppendLine("no " + n); continue; }
            var m = g.GetComponent<MeshRenderer>().sharedMaterial;
            sb.AppendLine(n + ": " + (m ? m.name + " shader " + m.shader.name : "no material"));
            if (m == null) continue;
            foreach (string p in new[] { "_Satellite", "_TerrainSize", "_SatelliteOffsetX", "_SatelliteOffsetY", "_RedUseSatelliteforBase", "_RedBaseIndex" })
                if (m.HasProperty(p)) sb.AppendLine("   " + p + " = " + (p == "_Satellite" ? (m.GetTexture(p) ? m.GetTexture(p).name : "null") : m.GetFloat(p).ToString()));
            var mf = g.GetComponent<MeshFilter>().sharedMesh;
            sb.AppendLine("   mesh colors " + (mf.colors32 != null ? mf.colors32.Length : 0) + " uv " + (mf.uv != null ? mf.uv.Length : 0) + " first color " + (mf.colors32.Length > 0 ? mf.colors32[0].ToString() : "-"));
        }
        return sb.ToString();
    }

    public static string ListDeep()
    {
        var sb = new System.Text.StringBuilder(); int k = 0;
        foreach (var g in Object.FindObjectsOfType<GameObject>())
        {
            if (!g.name.Contains("Spline") || !g.name.Contains("_Deep")) continue;
            var comps = new System.Collections.Generic.List<string>();
            foreach (var c in g.GetComponents<Component>()) comps.Add(c == null ? "MISSING" : c.GetType().Name);
            sb.AppendLine(g.name + " parent=" + (g.transform.parent ? g.transform.parent.name : "-") + " children=" + g.transform.childCount + " : " + string.Join(", ", comps.ToArray()));
            if (++k > 6) break;
        }
        return sb.ToString();
    }

    [MenuItem("Moorpark/Add Course Meshes")]
    public static void AddMeshes()
    {
        var old = GameObject.Find(ParentName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Add course meshes");
        int n = 0;
        foreach (string f in Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace("\\", "/"));
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            n++;
            if (n % 100 == 0) EditorUtility.DisplayProgressBar("Add course meshes", n + " meshes", 0.5f);
        }
        EditorUtility.ClearProgressBar();
        EditorSceneManager.MarkSceneDirty(parent.scene);
        Debug.Log("Moorpark: added " + n + " course meshes under " + ParentName);
    }
}
