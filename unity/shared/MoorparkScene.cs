// Moorpark: rebuild the scene on the GSP base template (Assets/Scenes/SampleScene.unity), which has the objects the
// GSP tools expect (Terrain (Inner), Outer Plot, CourseOverrides, decals, post processing, fog, wind...), and move
// Moorpark's terrain data and course objects into it. Unity 2018.2 compatible.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MoorparkScene
{
    const string Template = "Assets/Scenes/SampleScene.unity";
    const string ScenePath = "Assets/Moorpark/Moorpark.unity";
    const string Backup = "Assets/Moorpark/Moorpark_from_scratch.unity";
    const string TerrainDataPath = "Assets/Moorpark/Moorpark_TerrainData.asset";
    static readonly string[] Move = { "OPCD Meshes", "LIDAR_TREES", "SHRUBS", "CART_PATHS", "COURSE_LOOK_EDGE_GRASS" };

    // what the template scene holds (root objects, their components, terrains)
    public static string InspectTemplate()
    {
        var sb = new StringBuilder();
        var s = EditorSceneManager.OpenScene(Template, OpenSceneMode.Additive);
        try
        {
            foreach (var g in s.GetRootGameObjects())
            {
                var comps = new List<string>();
                foreach (var c in g.GetComponents<Component>()) if (c != null) comps.Add(c.GetType().Name);
                sb.AppendLine(g.name + " (" + g.transform.childCount + " children) pos " + g.transform.position + " layer " + LayerMask.LayerToName(g.layer) + " : " + string.Join(", ", comps.ToArray()));
                var t = g.GetComponent<Terrain>();
                if (t != null && t.terrainData != null)
                    sb.AppendLine("    terrain " + AssetDatabase.GetAssetPath(t.terrainData) + " size " + t.terrainData.size + " res " + t.terrainData.heightmapResolution + " splats " + t.terrainData.splatPrototypes.Length);
            }
        }
        finally { EditorSceneManager.CloseScene(s, true); }
        return sb.ToString();
    }

    // 9 km outer terrain (3 km each side), heights carried out from the edge of the inner square and 3 m under it,
    // so the hills don't end in a cliff at the edge of the course. Keeps the template's outer ground textures.
    static void OuterPlot(GameObject go, TerrainData inner)
    {
        var t = go.GetComponent<Terrain>();
        var old = t.terrainData;
        var od = new TerrainData();
        od.heightmapResolution = 513;
        const float S = 3000f, M = 3000f, W = S + 2 * M;
        float baseY = -10f, H = inner.size.y + 20f;
        od.size = new Vector3(W, H, W);
        int n = od.heightmapResolution;
        var h = new float[n, n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = -M + W * i / (n - 1), z = -M + W * j / (n - 1);
                float u = Mathf.Clamp01(x / S), v = Mathf.Clamp01(z / S);
                float y = inner.GetInterpolatedHeight(u, v) - 3f;
                h[j, i] = Mathf.Clamp01((y - baseY) / H);
            }
        od.SetHeights(0, 0, h);
        if (old != null) { od.splatPrototypes = old.splatPrototypes; }
        string path = "Assets/Moorpark/Moorpark_OuterData.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(od, path);
        t.terrainData = od;
        var tc = go.GetComponent<TerrainCollider>(); if (tc != null) tc.terrainData = od;
        go.transform.position = new Vector3(-M, baseY, -M);
    }

    public static string Rebuild()
    {
        var sb = new StringBuilder();
        var cur = EditorSceneManager.GetActiveScene();
        if (cur.path != ScenePath) return "open " + ScenePath + " first";
        EditorSceneManager.SaveScene(cur);
        AssetDatabase.DeleteAsset(Backup);
        AssetDatabase.CopyAsset(ScenePath, Backup);
        // the template, copied to a temporary path so the original stays untouched
        const string tmp = "Assets/Moorpark/_template_tmp.unity";
        AssetDatabase.DeleteAsset(tmp);
        AssetDatabase.CopyAsset(Template, tmp);
        var dst = EditorSceneManager.OpenScene(tmp, OpenSceneMode.Additive);
        foreach (string n in Move)
        {
            GameObject g = null;
            foreach (var r in cur.GetRootGameObjects()) if (r.name == n) g = r;
            if (g == null) { sb.AppendLine("not in scene: " + n); continue; }
            SceneManager.MoveGameObjectToScene(g, dst);
            sb.AppendLine("moved " + n);
        }
        // the template's sample course goes; its scene set-up (light, camera, fog, post, wind, overrides, terrains) stays
        var keep = new HashSet<string> { "DeferredRendering", "Directional Light", "GradientFog", "ArboreumWindActivator", "CourseOverrides",
                                         "PostProcessingVolume", "Audio Source", "Terrain (Inner)", "Outer Plot", "Main Camera", "SceneWindZone" };
        foreach (string n in Move) keep.Add(n);
        foreach (var r in dst.GetRootGameObjects())
            if (!keep.Contains(r.name)) { sb.AppendLine("removed sample " + r.name); Object.DestroyImmediate(r); }
        // Moorpark's terrain data (heights, satellite splat, grass/scrub details) on the template's Terrain (Inner)
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        foreach (var r in dst.GetRootGameObjects())
        {
            if (r.name == "Outer Plot") { OuterPlot(r, td); sb.AppendLine("Outer Plot rebuilt around the 3000 m square"); continue; }
            if (r.name != "Terrain (Inner)") continue;
            var t = r.GetComponent<Terrain>();
            var tc = r.GetComponent<TerrainCollider>();
            t.terrainData = td; if (tc != null) tc.terrainData = td;
            r.transform.position = Vector3.zero;
            t.heightmapPixelError = 2f; t.basemapDistance = 4000f; t.detailObjectDistance = 250f;
            if (tc != null && tc.sharedMaterial == null) tc.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/GSPdeeprough");
            sb.AppendLine("Terrain (Inner) now uses " + TerrainDataPath + " " + td.size);
        }
        EditorSceneManager.CloseScene(cur, true);
        SceneManager.SetActiveScene(dst);
        EditorSceneManager.SaveScene(dst, ScenePath);       // overwrite Moorpark.unity with the template-based scene
        AssetDatabase.DeleteAsset(tmp);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        sb.AppendLine("saved " + ScenePath + " (old scene kept as " + Backup + ")");
        return sb.ToString();
    }
}
