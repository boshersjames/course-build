// Moorpark: course mesh materials without SetMeshMaterials (Tierra's choices: Green / Fairway / Tee on the default
// theme materials under Assets/Resources, Bunker = Sand Raked Dirt Lip Thin), blend strips, and a material report.
// Unity 2018.2 compatible.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MoorparkLook
{
    const string Def = "Assets/Resources/Materials/Theme - Default/", Sat = "Assets/Resources/Materials/Theme - Satellite/";
    const string BlendDir = "Assets/Moorpark/generated/blends";
    // surface name (as in the mesh names) -> material and ball physics
    static readonly Dictionary<string, string[]> Surf = new Dictionary<string, string[]>
    {
        { "Rough", new[] { Sat + "OPCD MAHS Rough No Mowlines_Sat.mat", "GSPrough" } },
        { "Deep", new[] { Sat + "OPCD MAHS Deep Rough No Mowlines_Sat.mat", "GSPdeeprough" } },
        { "Fairway", new[] { Def + "OPCD MAHS Fairway default.mat", "GSPfairway" } },
        { "Tee", new[] { Def + "OPCD MAHS Tee default.mat", "GSPtee" } },
        { "Green", new[] { Def + "OPCD MAHS Green default.mat", "GSPgreen" } },
        { "Bunker", new[] { "Assets/Resources/OPCD MAHS Resources/ExtraMaterials/OPCD MAHS Sand Raked Dirt Lip Thin.mat", "GSPsand" } },
        { "Water_Base_Lake", new[] { Sat + "OPCD MAHS Water Base Lake_Sat.mat", "GSPdeeprough" } },
    };

    static Material Mat(string surface)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Surf[surface][0]);
        if (m == null) throw new System.Exception("material not found: " + Surf[surface][0]);
        return m;
    }

    // Spline_<layer>_<i>_<Own>_-_Mesh, Spline_<layer>_<i>_B_[<Outside>_]<Own>_-_Blend, Spline_<layer>_<i>_hole<k>_B_<Own>_-_Blend
    static bool Parse(string n, out string own, out string outside, out bool blend)
    {
        own = outside = null; blend = n.EndsWith("_-_Blend");
        if (!n.StartsWith("Spline_") || n.Contains("WaterPlane")) return false;
        string body = n.Substring(0, n.LastIndexOf("_-_"));
        foreach (var s in Surf.Keys) if (body.EndsWith("_" + s) && (own == null || s.Length > own.Length)) own = s;
        if (own == null) return false;
        if (blend)
        {
            int b = body.IndexOf("_B_");
            string mid = body.Substring(b + 3, body.Length - (b + 3) - own.Length);   // "<Outside>_" or ""
            outside = mid.Length > 1 ? mid.TrimEnd('_') : own;
            if (!Surf.ContainsKey(outside)) outside = own;
        }
        return true;
    }

    // a blend strip: its own surface on the red (mesh) side fading into the outside surface on the black (outline) side,
    // i.e. a copy of the own material with the outside material's red channel copied into the black channel
    static Material BlendMat(string own, string outside)
    {
        string path = BlendDir + "/Blend " + own + " to " + outside + ".mat";
        var a = Mat(own); var b = Mat(outside);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(a); AssetDatabase.CreateAsset(m, path); }
        else m.CopyPropertiesFromMaterial(a);
        var s = a.shader;
        for (int i = 0; i < ShaderUtil.GetPropertyCount(s); i++)
        {
            string p = ShaderUtil.GetPropertyName(s, i);
            if (!p.StartsWith("_Red")) continue;
            string q = "_Black" + p.Substring(4);
            if (!m.HasProperty(q) || !b.HasProperty(p)) continue;
            switch (ShaderUtil.GetPropertyType(s, i))
            {
                case ShaderUtil.ShaderPropertyType.Color: m.SetColor(q, b.GetColor(p)); break;
                case ShaderUtil.ShaderPropertyType.Vector: m.SetVector(q, b.GetVector(p)); break;
                case ShaderUtil.ShaderPropertyType.TexEnv: m.SetTexture(q, b.GetTexture(p)); m.SetTextureScale(q, b.GetTextureScale(p)); break;
                default: m.SetFloat(q, b.GetFloat(p)); break;
            }
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // materials, blend materials and ball physics on every course mesh
    public static string Apply()
    {
        if (!AssetDatabase.IsValidFolder(BlendDir)) { Directory.CreateDirectory(BlendDir); AssetDatabase.Refresh(); }
        var counts = new SortedDictionary<string, int>(); int skipped = 0;
        foreach (var r in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshRenderer>(true))
        {
            string own, outside; bool blend;
            if (!Parse(r.name, out own, out outside, out blend)) { skipped++; continue; }
            var mat = blend ? BlendMat(own, outside) : Mat(own);
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var mf = r.GetComponent<MeshFilter>();
            var c = r.GetComponent<MeshCollider>() ?? r.gameObject.AddComponent<MeshCollider>();
            if (mf) c.sharedMesh = mf.sharedMesh;
            c.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + Surf[own][1]);
            string k = mat.name; counts[k] = counts.ContainsKey(k) ? counts[k] + 1 : 1;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        var sb = new StringBuilder();
        foreach (var kv in counts) sb.AppendLine(kv.Value + "x " + kv.Key);
        sb.AppendLine(skipped + " skipped (water planes)");
        return sb.ToString();
    }

    // cart paths as on Tierra now: rebuilt from cartpaths.csv (BuildCartPaths, GSPconcrete), then Course Look's weathered
    // concrete at grey 0.62 / gloss 0.05 and no edge grass (Assets/CourseLook/course_look_settings.json, as Tierra's)
    public static string CartPaths()
    {
        var sb = new StringBuilder();
        var cfg = CourseAutomation.LoadConfig("Assets/CourseAutomation/Moorpark.json");
        CourseAutomation.Step step = null;
        foreach (var st in cfg.steps) if (st.name == "Cart paths") step = st;
        sb.AppendLine("BuildCartPaths.Build: " + CourseAutomation.Invoke(cfg, step.tool, step.method, step.set));
        var g = GameObject.Find("COURSE_LOOK_EDGE_GRASS"); if (g != null) { Object.DestroyImmediate(g); sb.AppendLine("removed COURSE_LOOK_EDGE_GRASS"); }
        Selection.objects = new Object[0];
        var w = ScriptableObject.CreateInstance<CourseLook>();
        try
        {
            typeof(CourseLook).GetMethod("ApplyPaths", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(w, null);
        }
        finally { Object.DestroyImmediate(w); }
        var root = GameObject.Find("CART_PATHS");
        var mats = new HashSet<string>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) foreach (var m in r.sharedMaterials) if (m) mats.Add(m.name + " (colour " + (m.HasProperty("_Color") ? m.color.ToString() : "-") + ", gloss " + (m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness").ToString() : "-") + ")");
        sb.AppendLine("CART_PATHS: " + root.transform.childCount + " paths, materials: " + string.Join("; ", new List<string>(mats).ToArray()));
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return sb.ToString();
    }

    // what's on a few meshes of each kind, with shader properties
    public static string Report()
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        var root = GameObject.Find("OPCD Meshes");
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string kind = Kind(r.name);
            if (seen.Contains(kind)) continue; seen.Add(kind);
            sb.AppendLine("== " + kind + "  e.g. " + r.name);
            foreach (var m in r.sharedMaterials) Describe(m, sb);
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) sb.AppendLine("   mesh: verts " + mf.sharedMesh.vertexCount + " colors " + mf.sharedMesh.colors32.Length + " uv2 " + mf.sharedMesh.uv2.Length + " uv " + mf.sharedMesh.uv.Length + UvRange(mf.sharedMesh));
            foreach (var c in r.GetComponents<Component>()) sb.Append(c == null ? " MISSING" : " " + c.GetType().Name); sb.AppendLine();
        }
        return sb.ToString();
    }

    // kind -> materials and collider physics material, as they are now
    public static string Snapshot()
    {
        var d = new SortedDictionary<string, string>();
        foreach (var r in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshRenderer>(true))
        {
            string k = Kind(r.name); var c = r.GetComponent<Collider>();
            var mats = new List<string>(); foreach (var m in r.sharedMaterials) mats.Add(m ? m.name : "null");
            string v = string.Join("+", mats.ToArray()) + " | phys " + (c == null ? "no collider" : (c.sharedMaterial ? c.sharedMaterial.name : "none")) + " | layer " + LayerMask.LayerToName(r.gameObject.layer) + " static " + r.gameObject.isStatic + " shadows " + r.shadowCastingMode;
            string key = k + " => " + v;
            d[key] = d.ContainsKey(key) ? (int.Parse(d[key]) + 1).ToString() : "1";
        }
        var sb = new StringBuilder(); foreach (var kv in d) sb.AppendLine(kv.Value + "x " + kv.Key); return sb.ToString();
    }

    static string UvRange(Mesh m)
    {
        var uv = m.uv; if (uv.Length == 0) return "";
        Vector2 a = uv[0], b = uv[0]; foreach (var u in uv) { a = Vector2.Min(a, u); b = Vector2.Max(b, u); }
        return " uv range " + a + " - " + b + " bounds " + m.bounds.size;
    }

    public static string DescribePath(string path)
    {
        var sb = new StringBuilder(); Describe(AssetDatabase.LoadAssetAtPath<Material>(path), sb); return sb.ToString();
    }

    static void Describe(Material m, StringBuilder sb)
    {
        if (m == null) { sb.AppendLine("   (null material)"); return; }
        sb.AppendLine("   material " + m.name + " [" + AssetDatabase.GetAssetPath(m) + "] shader " + m.shader.name + " queue " + m.renderQueue);
        var s = m.shader;
        for (int i = 0; i < ShaderUtil.GetPropertyCount(s); i++)
        {
            string p = ShaderUtil.GetPropertyName(s, i); var t = ShaderUtil.GetPropertyType(s, i);
            string v;
            if (t == ShaderUtil.ShaderPropertyType.TexEnv) { var tx = m.GetTexture(p); v = tx ? tx.name : "null"; }
            else if (t == ShaderUtil.ShaderPropertyType.Color) v = m.GetColor(p).ToString();
            else if (t == ShaderUtil.ShaderPropertyType.Vector) v = m.GetVector(p).ToString();
            else v = m.GetFloat(p).ToString();
            sb.AppendLine("      " + p + " = " + v);
        }
    }

    static string Kind(string n)
    {
        int i = n.IndexOf("_-_");
        string tail = i >= 0 ? n.Substring(i) : n;
        string[] parts = n.Split('_');
        if (n.Contains("_-_Blend")) { int b = n.IndexOf("_B_"); return "Blend " + (b >= 0 ? n.Substring(b + 3, n.IndexOf("_-_") - b - 3) : n); }
        return parts.Length > 1 ? parts[1] + tail : n;
    }
}
