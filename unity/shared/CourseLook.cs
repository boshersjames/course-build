// Course Look > Cart Paths and Turf
// Makes cart paths and the satellite-coloured turf look real, on any OPCD / GSPro course (nothing in here is Tierra specific).
//   Cart paths  weathered concrete (joints every 2.7 m, tyre tracks, dirty edges, cracks) instead of flat white, mapped along
//               each path, plus a fringe of short grass along both edges.
//   Turf        a graded copy of the satellite photo: haze taken off, richer greens and browns, double resolution with a
//               fine grain so the ground doesn't look smeared up close. Swapped in wherever the scene used the original.
// Settings are saved in Assets/CourseLook/course_look_settings.json, so every course in this project gets the same look.
// Needs the Assets/CourseLook folder (textures). Unity 2018.2. Put this file in Assets/Editor.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CourseLook : EditorWindow
{
    const string ROOT = "Assets/CourseLook";
    const string SETTINGS = ROOT + "/course_look_settings.json";
    const string GRASS_ROOT = "COURSE_LOOK_EDGE_GRASS";

    [System.Serializable]
    public class Settings
    {
        // cart paths
        public float pathTileLength = 8f;       // metres of path per texture repeat (3 joints)
        public float planarTile = 4f;           // metres per repeat for meshes that are not simple strips
        public float pathBrightness = 0.62f;     // tuned in GSPro (Oct 26): 1 read near white in its sun
        public float pathSmoothness = 0.05f;
        public float pathBumpiness = 1f;
        public bool edgeGrass = true;
        public float grassSpacing = 0.45f;      // metres between tufts along each edge
        public float grassCoverage = 0.85f;
        public float grassMinHeight = 0.18f, grassMaxHeight = 0.38f;
        public float grassMinWidth = 0.45f, grassMaxWidth = 0.8f;
        public float grassMaxOffset = 0.25f;    // how far out from the edge
        public Color grassTint = Color.white;
        // satellite grade (same maths as course_look/build/grade.py)
        public float haze = 1f, saturation = 1.25f, contrast = 1.1f, gamma = 1f, warmth = -0.01f, grain = 0.10f;
        public bool doubleResolution = true;
    }

    Settings s;
    Texture2D satellite;
    List<Renderer> paths = new List<Renderer>();
    Vector2 scroll;

    [MenuItem("Course Look/Cart Paths and Turf")]
    static void Open() { GetWindow<CourseLook>("Course Look"); }

    void OnEnable()
    {
        s = LoadSettings();
        FindPaths();
        if (satellite == null) satellite = FindSatellite();
    }

    static Settings LoadSettings()
    {
        Settings d = new Settings();
        if (File.Exists(SETTINGS)) JsonUtility.FromJsonOverwrite(File.ReadAllText(SETTINGS), d);
        return d;
    }

    void SaveSettings()
    {
        Folder(ROOT);
        File.WriteAllText(SETTINGS, JsonUtility.ToJson(s, true));
    }

    static void Folder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Cart paths", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Finds every mesh named \"cart...\" or with a GSPconcrete collider (" + paths.Count +
            " in this scene). To do only some, select them first; anything selected that isn't a cart path is left alone.", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Find again")) FindPaths();
        if (GUILayout.Button("Select found")) Selection.objects = paths.ConvertAll(r => (Object)r.gameObject).ToArray();
        EditorGUILayout.EndHorizontal();
        s.pathTileLength = EditorGUILayout.Slider("Texture length (m)", s.pathTileLength, 3f, 16f);
        s.pathBrightness = EditorGUILayout.Slider("Brightness", s.pathBrightness, 0.6f, 1.3f);
        s.pathSmoothness = EditorGUILayout.Slider("Smoothness", s.pathSmoothness, 0f, 0.5f);
        s.pathBumpiness = EditorGUILayout.Slider("Bumpiness", s.pathBumpiness, 0f, 2f);
        s.edgeGrass = EditorGUILayout.Toggle("Grass along the edges", s.edgeGrass);
        if (s.edgeGrass)
        {
            EditorGUI.indentLevel++;
            s.grassSpacing = EditorGUILayout.Slider("Spacing (m)", s.grassSpacing, 0.2f, 1.5f);
            s.grassCoverage = EditorGUILayout.Slider("Coverage", s.grassCoverage, 0.1f, 1f);
            EditorGUILayout.MinMaxSlider("Height " + s.grassMinHeight.ToString("0.00") + " - " + s.grassMaxHeight.ToString("0.00") + " m",
                ref s.grassMinHeight, ref s.grassMaxHeight, 0.05f, 1f);
            s.grassMaxOffset = EditorGUILayout.Slider("Spread from edge (m)", s.grassMaxOffset, 0f, 1f);
            s.grassTint = EditorGUILayout.ColorField("Tint", s.grassTint);
            EditorGUI.indentLevel--;
        }
        if (GUILayout.Button("Apply to cart paths", GUILayout.Height(28))) ApplyPaths();
        if (GUILayout.Button("Remove edge grass")) RemoveGrass();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Turf (satellite photo)", EditorStyles.boldLabel);
        satellite = (Texture2D)EditorGUILayout.ObjectField("Satellite texture", satellite, typeof(Texture2D), false);
        s.haze = EditorGUILayout.Slider("Remove haze", s.haze, 0f, 1.5f);
        s.saturation = EditorGUILayout.Slider("Saturation", s.saturation, 0.8f, 1.6f);
        s.contrast = EditorGUILayout.Slider("Contrast", s.contrast, 0.8f, 1.4f);
        s.gamma = EditorGUILayout.Slider("Darken mid-tones", s.gamma, 0.8f, 1.4f);
        s.warmth = EditorGUILayout.Slider("Warmth", s.warmth, -0.08f, 0.08f);
        s.grain = EditorGUILayout.Slider("Turf grain", s.grain, 0f, 0.25f);
        s.doubleResolution = EditorGUILayout.Toggle("Double resolution (max 8192)", s.doubleResolution);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Grade and use", GUILayout.Height(28))) GradeSatellite();
        if (GUILayout.Button("Back to original", GUILayout.Height(28))) RestoreSatellite();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button("Reset settings to defaults")) s = new Settings();
        if (EditorGUI.EndChangeCheck()) SaveSettings();
        EditorGUILayout.EndScrollView();
    }

    // ---------------------------------------------------------------- cart paths

    // other tools' groups are never cart paths, even when they run along one (the hole 2 fence) or are selected
    static readonly string[] NOT_PATHS = { GRASS_ROOT, "FENCES", "WALLS", "NETS", "LIDAR_TREES", "SHRUBS", "DEEP_ROUGH" };

    static bool IsCartPath(Transform t)
    {
        if (System.Array.IndexOf(NOT_PATHS, t.root.name) >= 0) return false;
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.ToLowerInvariant().Contains("cart")) return true;
        foreach (Collider c in t.GetComponents<Collider>())
            if (c.sharedMaterial != null && c.sharedMaterial.name.StartsWith("GSPconcrete")) return true;
        return false;
    }

    void FindPaths()
    {
        paths.Clear();
        foreach (MeshRenderer r in FindObjectsOfType<MeshRenderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && IsCartPath(r.transform)) paths.Add(r);
        }
    }

    void ApplyPaths()
    {
        List<Renderer> todo = new List<Renderer>();
        foreach (GameObject g in Selection.gameObjects)
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponent<MeshFilter>() != null && IsCartPath(r.transform)) todo.Add(r);
        if (todo.Count == 0) { FindPaths(); todo = paths; }
        if (todo.Count == 0)
        {
            EditorUtility.DisplayDialog("Course Look", "No cart path meshes found. Select them in the Hierarchy and click Apply again.", "OK");
            return;
        }
        Material ribbon, planar, grass;
        if (!Materials(out ribbon, out planar, out grass)) return;
        string dir = ROOT + "/Generated/" + SceneName();
        Folder(dir);
        int strips = 0;
        List<Vector3[]> edges = new List<Vector3[]>();     // a, b, outward
        HashSet<string> used = new HashSet<string>();
        try
        {
            for (int i = 0; i < todo.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Course Look", "Cart path " + (i + 1) + " / " + todo.Count, (float)i / todo.Count);
                MeshFilter mf = todo[i].GetComponent<MeshFilter>();
                Mesh m = Instantiate(mf.sharedMesh);
                string stem = MeshName(todo[i].transform, used);
                m.name = stem;
                bool strip = StripUV(m, todo[i].transform, s.pathTileLength, edges);
                if (!strip) PlanarUV(m, todo[i].transform, s.planarTile, edges);
                else strips++;
                AssetDatabase.CreateAsset(m, dir + "/" + stem + ".asset");
                Undo.RecordObject(mf, "Cart path look");
                Undo.RecordObject(todo[i], "Cart path look");
                mf.sharedMesh = m;
                Material[] mats = todo[i].sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = strip ? ribbon : planar;
                todo[i].sharedMaterials = mats;
                todo[i].receiveShadows = true;
            }
            if (s.edgeGrass) BuildGrass(edges, grass, dir);
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Course Look: " + todo.Count + " cart path meshes (" + strips + " mapped along the path, " + (todo.Count - strips) +
                  " mapped from above)");
    }

    // Mesh asset name from the object's place in the hierarchy (CART_PATHS_CartPath_0, GAZEBO_HOLE1_Floor), so adding or
    // removing paths never renames, and never overwrites, a mesh another object still uses.
    static string MeshName(Transform t, HashSet<string> used)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "_" + path;
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        foreach (char c in path) b.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        string stem = b.ToString(), name = stem;
        for (int k = 2; used.Contains(name); k++) name = stem + "_" + k;     // same name twice under one parent
        used.Add(name);
        return name;
    }

    // A strip built as left/right vertex pairs: u across, v = metres along the path. Returns false if the mesh isn't one.
    static bool StripUV(Mesh m, Transform tr, float tile, List<Vector3[]> edges)
    {
        Vector3[] v = m.vertices;
        int[] tri = m.triangles;
        int n = v.Length;
        if (n < 4 || n % 2 != 0) return false;
        Vector3[] w = new Vector3[n];
        for (int i = 0; i < n; i++) w[i] = tr.TransformPoint(v[i]);
        float[] width = new float[n / 2];
        for (int i = 0; i < n / 2; i++) width[i] = Vector3.Distance(w[2 * i], w[2 * i + 1]);
        float[] sorted = (float[])width.Clone();
        System.Array.Sort(sorted);
        float med = sorted[sorted.Length / 2];
        if (med < 0.8f || med > 8f) return false;
        foreach (float x in width) if (x < med * 0.5f || x > med * 1.6f) return false;
        for (int t = 0; t < tri.Length; t += 3)
        {
            int a = tri[t] / 2, b = tri[t + 1] / 2, c = tri[t + 2] / 2;
            if (Mathf.Max(a, Mathf.Max(b, c)) - Mathf.Min(a, Mathf.Min(b, c)) > 1) return false;
        }
        Vector2[] uv = new Vector2[n];
        float d = 0f;
        for (int i = 0; i < n / 2; i++)
        {
            if (i > 0)
            {
                Vector3 step = (w[2 * i] + w[2 * i + 1] - w[2 * i - 2] - w[2 * i - 1]) * 0.5f;
                d += step.magnitude;
                // the two sides of this step become edge-grass edges, pointing away from the other side
                for (int side = 0; side < 2; side++)
                {
                    Vector3 a = w[2 * (i - 1) + side], b = w[2 * i + side];
                    Vector3 other = w[2 * i + 1 - side];
                    edges.Add(new Vector3[] { a, b, Outward(a, b, other) });
                }
            }
            uv[2 * i] = new Vector2(0f, d / tile);
            uv[2 * i + 1] = new Vector2(1f, d / tile);
        }
        m.uv = uv;
        m.RecalculateNormals();
        m.RecalculateTangents();
        return true;
    }

    // Anything else: map from above, and take the open edges of the mesh for the grass.
    static void PlanarUV(Mesh m, Transform tr, float tile, List<Vector3[]> edges)
    {
        Vector3[] v = m.vertices;
        Vector2[] uv = new Vector2[v.Length];
        Vector3[] w = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            w[i] = tr.TransformPoint(v[i]);
            uv[i] = new Vector2(w[i].x / tile, w[i].z / tile);
        }
        m.uv = uv;
        m.RecalculateTangents();
        // open edges = used by one triangle (vertices welded to the centimetre)
        int[] tri = m.triangles;
        Dictionary<long, int> weld = new Dictionary<long, int>();
        int[] id = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            long key = ((long)Mathf.RoundToInt(w[i].x * 100f) << 32) ^ ((long)Mathf.RoundToInt(w[i].z * 100f) & 0xffffffffL);
            int k;
            if (!weld.TryGetValue(key, out k)) { k = weld.Count; weld[key] = k; }
            id[i] = k;
        }
        Dictionary<long, int> count = new Dictionary<long, int>();
        for (int t = 0; t < tri.Length; t += 3)
            for (int e = 0; e < 3; e++)
            {
                long key = EdgeKey(id[tri[t + e]], id[tri[t + (e + 1) % 3]]);
                int c;
                count.TryGetValue(key, out c);
                count[key] = c + 1;
            }
        for (int t = 0; t < tri.Length; t += 3)
            for (int e = 0; e < 3; e++)
            {
                int ia = tri[t + e], ib = tri[t + (e + 1) % 3], ic = tri[t + (e + 2) % 3];
                if (count[EdgeKey(id[ia], id[ib])] != 1) continue;
                edges.Add(new Vector3[] { w[ia], w[ib], Outward(w[ia], w[ib], w[ic]) });
            }
    }

    static long EdgeKey(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

    static Vector3 Outward(Vector3 a, Vector3 b, Vector3 inside)
    {
        Vector3 t = b - a; t.y = 0f;
        Vector3 n = new Vector3(-t.z, 0f, t.x).normalized;
        Vector3 toIn = inside - a; toIn.y = 0f;
        return Vector3.Dot(n, toIn) > 0f ? -n : n;
    }

    // ---------------------------------------------------------------- edge grass

    void RemoveGrass()
    {
        GameObject old = GameObject.Find(GRASS_ROOT);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    void BuildGrass(List<Vector3[]> edges, Material mat, string dir)
    {
        RemoveGrass();
        Terrain terrain = CourseTerrain();
        GameObject root = new GameObject(GRASS_ROOT);
        Undo.RegisterCreatedObjectUndo(root, "Edge grass");
        System.Random rng = new System.Random(11);
        List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        int chunk = 0;
        foreach (Vector3[] e in edges)
        {
            Vector3 a = e[0], b = e[1], o = e[2];
            float len = Vector3.Distance(a, b);
            for (float k = 0f; k < len; k += s.grassSpacing)
            {
                if (rng.NextDouble() > s.grassCoverage) continue;
                float t = Mathf.Clamp01((k + R(rng, -0.2f, 0.2f)) / Mathf.Max(len, 0.01f));
                Vector3 p = Vector3.Lerp(a, b, t) + o * R(rng, 0.02f, s.grassMaxOffset);
                if (terrain != null) p.y = Mathf.Max(Ground(terrain, p.x, p.z), p.y - 0.25f) - 0.03f;
                float yaw = Mathf.Atan2(b.z - a.z, b.x - a.x) + R(rng, -0.45f, 0.45f);
                Vector3 xa = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                float h = R(rng, s.grassMinHeight, s.grassMaxHeight), w = R(rng, s.grassMinWidth, s.grassMaxWidth);
                Card(verts, norms, uvs, tris, p, xa, o, w, h);
                if (verts.Count > 64000) { Flush(root, verts, norms, uvs, tris, mat, dir, chunk++); }
            }
        }
        if (verts.Count > 0) Flush(root, verts, norms, uvs, tris, mat, dir, chunk++);
    }

    static float R(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    // two-sided upright card; normals lean up and out so the tufts light like the ground they sit on
    static void Card(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 p, Vector3 xa, Vector3 o, float w, float h)
    {
        Vector3 nn = (Vector3.up * 0.8f + o * 0.2f).normalized;
        int b = v.Count;
        Vector3 lean = o * h * 0.15f;
        v.Add(p - xa * w * 0.5f); v.Add(p + xa * w * 0.5f); v.Add(p + xa * w * 0.5f + Vector3.up * h + lean); v.Add(p - xa * w * 0.5f + Vector3.up * h + lean);
        for (int i = 0; i < 4; i++) n.Add(nn);
        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
        t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
        t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
    }

    static void Flush(GameObject root, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Material mat, string dir, int i)
    {
        Mesh m = new Mesh();
        m.name = "EdgeGrass_" + i;
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, dir + "/EdgeGrass_" + i + ".asset");
        GameObject g = new GameObject("EdgeGrass_" + i);
        g.transform.SetParent(root.transform, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        v.Clear(); n.Clear(); uv.Clear(); t.Clear();
    }

    // The course terrain. Terrain.activeTerrain can be the big flat outer terrain.
    static Terrain CourseTerrain()
    {
        GameObject inner = GameObject.Find("Terrain (Inner)");
        if (inner != null && inner.GetComponent<Terrain>() != null) return inner.GetComponent<Terrain>();
        Terrain best = Terrain.activeTerrain; float top = float.MinValue;
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 p = t.transform.position, sz = t.terrainData.size, c = p + sz * 0.5f;
            float h = t.SampleHeight(c) + p.y;
            if (t.terrainData.bounds.size.y > 1f && h > top) { top = h; best = t; }
        }
        return best;
    }

    // Visible ground: the terrain, or a surface mesh (rough, tee, fairway) up to 3 m above it. Cart paths don't count.
    static float Ground(Terrain t, float x, float z)
    {
        float y = t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y, terrainY = y;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, terrainY + 50f, z), Vector3.down, 52f))
        {
            if (h.collider.isTrigger || !(h.collider is MeshCollider)) continue;
            if (h.point.y <= y || h.point.y > terrainY + 3f) continue;
            string top = h.transform.root.name;
            if (top == "SHRUBS" || top == "LIDAR_TREES" || top == "FENCES" || top == "WALLS" || top == "NETS") continue;
            if (IsCartPath(h.transform)) continue;
            y = h.point.y;
        }
        return y;
    }

    // ---------------------------------------------------------------- materials

    bool Materials(out Material ribbon, out Material planar, out Material grass)
    {
        ribbon = planar = grass = null;
        string tex = ROOT + "/Textures/";
        if (!File.Exists(tex + "CL_CartPath_Albedo.png"))
        {
            EditorUtility.DisplayDialog("Course Look", "Missing " + tex + "CL_CartPath_Albedo.png\nCopy the CourseLook folder into Assets first.", "OK");
            return false;
        }
        Folder(ROOT + "/Materials");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Import(tex + "CL_CartPath_Albedo.png", false, false);
        Import(tex + "CL_CartPath_Normal.png", true, false);
        Import(tex + "CL_Concrete_Albedo.png", false, false);
        Import(tex + "CL_Concrete_Normal.png", true, false);
        Import(tex + "CL_EdgeGrass.png", false, true);
        ribbon = Concrete("CL_CartPath", tex + "CL_CartPath_Albedo.png", tex + "CL_CartPath_Normal.png");
        planar = Concrete("CL_Concrete", tex + "CL_Concrete_Albedo.png", tex + "CL_Concrete_Normal.png");
        grass = Mat("CL_EdgeGrass");
        grass.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "CL_EdgeGrass.png"));
        grass.color = s.grassTint;
        grass.SetFloat("_Glossiness", 0f);
        grass.SetFloat("_Mode", 1f);
        grass.SetOverrideTag("RenderType", "TransparentCutout");
        grass.SetFloat("_Cutoff", 0.4f);
        grass.EnableKeyword("_ALPHATEST_ON");
        grass.renderQueue = 2450;
        EditorUtility.SetDirty(grass);
        AssetDatabase.SaveAssets();
        return true;
    }

    Material Concrete(string name, string albedo, string normal)
    {
        Material m = Mat(name);
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
        m.SetFloat("_BumpScale", s.pathBumpiness);
        m.EnableKeyword("_NORMALMAP");
        m.color = new Color(s.pathBrightness, s.pathBrightness, s.pathBrightness, 1f);
        m.SetFloat("_Glossiness", s.pathSmoothness);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Mat(string name)
    {
        string path = ROOT + "/Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static void Import(string path, bool normal, bool cutout)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;
        ti.wrapMode = cutout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        ti.maxTextureSize = 2048;
        if (cutout)
        {
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.4f;
        }
        ti.SaveAndReimport();
    }

    static string SceneName()
    {
        string n = EditorSceneManager.GetActiveScene().name;
        return string.IsNullOrEmpty(n) ? "Untitled" : n;
    }

    // ---------------------------------------------------------------- satellite

    static Texture2D FindSatellite()
    {
        Texture2D best = null;
        foreach (Texture2D t in SceneTextures())
        {
            if (t.name.EndsWith("_graded")) { string p = AssetDatabase.GetAssetPath(t); Texture2D o = Original(p); if (o != null) return o; }
            if (t.name.ToLowerInvariant().Contains("satellite") && (best == null || t.width > best.width)) best = t;
        }
        return best;
    }

    static Texture2D Original(string gradedPath)
    {
        string dir = Path.GetDirectoryName(gradedPath), stem = Path.GetFileNameWithoutExtension(gradedPath).Replace("_graded", "");
        foreach (string ext in new[] { ".jpg", ".png", ".jpeg", ".tga", ".tif", ".psd" })
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + stem + ext);
            if (t != null) return t;
        }
        return null;
    }

    static IEnumerable<Texture2D> SceneTextures()
    {
        foreach (Material m in SceneMaterials())
        {
            Shader sh = m.shader;
            for (int i = 0; i < ShaderUtil.GetPropertyCount(sh); i++)
                if (ShaderUtil.GetPropertyType(sh, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    Texture2D t = m.GetTexture(ShaderUtil.GetPropertyName(sh, i)) as Texture2D;
                    if (t != null) yield return t;
                }
        }
        foreach (Terrain tr in Terrain.activeTerrains)
            foreach (SplatPrototype sp in tr.terrainData.splatPrototypes)
                if (sp.texture != null) yield return sp.texture;
    }

    static List<Material> SceneMaterials()
    {
        HashSet<Material> set = new HashSet<Material>();
        foreach (Renderer r in FindObjectsOfType<Renderer>())
            foreach (Material m in r.sharedMaterials) if (m != null) set.Add(m);
        foreach (Terrain t in Terrain.activeTerrains) if (t.materialTemplate != null) set.Add(t.materialTemplate);
        return new List<Material>(set);
    }

    // swaps every use of one texture for another (materials in the scene and terrain layers)
    static int Swap(Texture2D from, Texture2D to)
    {
        int n = 0;
        foreach (Material m in SceneMaterials())
        {
            Shader sh = m.shader;
            for (int i = 0; i < ShaderUtil.GetPropertyCount(sh); i++)
            {
                if (ShaderUtil.GetPropertyType(sh, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                string p = ShaderUtil.GetPropertyName(sh, i);
                if (m.GetTexture(p) != from) continue;
                Undo.RecordObject(m, "Satellite");
                m.SetTexture(p, to);
                EditorUtility.SetDirty(m);
                n++;
            }
        }
        foreach (Terrain t in Terrain.activeTerrains)
        {
            SplatPrototype[] sp = t.terrainData.splatPrototypes;
            bool any = false;
            foreach (SplatPrototype p in sp) if (p.texture == from) { p.texture = to; any = true; n++; }
            if (any)
            {
                Undo.RecordObject(t.terrainData, "Satellite");
                t.terrainData.splatPrototypes = sp;
                EditorUtility.SetDirty(t.terrainData);
            }
        }
        return n;
    }

    void GradeSatellite()
    {
        if (satellite == null) { EditorUtility.DisplayDialog("Course Look", "Pick the satellite texture first.", "OK"); return; }
        string src = AssetDatabase.GetAssetPath(satellite);
        if (Path.GetFileNameWithoutExtension(src).EndsWith("_graded")) { Texture2D o = Original(src); if (o != null) { satellite = o; src = AssetDatabase.GetAssetPath(o); } }
        Texture2D img = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!img.LoadImage(File.ReadAllBytes(src)))
        {
            EditorUtility.DisplayDialog("Course Look", "Can't read " + src + " (use a .jpg or .png).", "OK");
            return;
        }
        Color32[] px = img.GetPixels32();
        int sw = img.width, sh = img.height;
        DestroyImmediate(img);
        int scale = s.doubleResolution && Mathf.Max(sw, sh) * 2 <= 8192 ? 2 : 1;
        int ow = sw * scale, oh = sh * scale;
        // haze floor: mean of the per-channel 0.5 % percentiles, same for all channels (per channel turns turf pink)
        int[,] hist = new int[3, 256];
        for (int i = 0; i < px.Length; i++) { hist[0, px[i].r]++; hist[1, px[i].g]++; hist[2, px[i].b]++; }
        float[] bp = new float[3];
        for (int c = 0; c < 3; c++)
        {
            int acc = 0, k = 0;
            while (k < 255 && acc + hist[c, k] < px.Length * 0.005f) { acc += hist[c, k]; k++; }
            bp[c] = k / 255f * s.haze;
        }
        float mean = (bp[0] + bp[1] + bp[2]) / 3f;
        bp[0] = bp[1] = bp[2] = mean;
        // graded lookup per channel value (grade is per channel apart from saturation, done after)
        Color32[] outPx = new Color32[ow * oh];
        try
        {
            for (int y = 0; y < oh; y++)
            {
                if (y % 256 == 0 && EditorUtility.DisplayCancelableProgressBar("Course Look", "Grading the satellite photo", (float)y / oh)) return;
                float fy = (y + 0.5f) / scale - 0.5f;
                int y0 = Mathf.Clamp((int)Mathf.Floor(fy), 0, sh - 1), y1 = Mathf.Min(y0 + 1, sh - 1);
                float ty = Mathf.Clamp01(fy - y0);
                for (int x = 0; x < ow; x++)
                {
                    float fx = (x + 0.5f) / scale - 0.5f;
                    int x0 = Mathf.Clamp((int)Mathf.Floor(fx), 0, sw - 1), x1 = Mathf.Min(x0 + 1, sw - 1);
                    float tx = Mathf.Clamp01(fx - x0);
                    Color32 a = px[y0 * sw + x0], b = px[y0 * sw + x1], c = px[y1 * sw + x0], d = px[y1 * sw + x1];
                    float r = Bi(a.r, b.r, c.r, d.r, tx, ty), g = Bi(a.g, b.g, c.g, d.g, tx, ty), bl = Bi(a.b, b.b, c.b, d.b, tx, ty);
                    Grade(ref r, ref g, ref bl, bp);
                    float k = 1f + s.grain * Grain(x, oh - 1 - y);         // grain in image rows from the top, like grade.py
                    outPx[y * ow + x] = new Color32(B(r * k), B(g * k), B(bl * k), 255);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Texture2D o2 = new Texture2D(ow, oh, TextureFormat.RGB24, false);
        o2.SetPixels32(outPx);
        o2.Apply();
        string dst = Path.GetDirectoryName(src) + "/" + Path.GetFileNameWithoutExtension(src) + "_graded.jpg";
        File.WriteAllBytes(dst, o2.EncodeToJPG(93));
        DestroyImmediate(o2);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter ti = AssetImporter.GetAtPath(dst) as TextureImporter;
        TextureImporter si = AssetImporter.GetAtPath(src) as TextureImporter;
        if (ti != null)
        {
            ti.maxTextureSize = Mathf.Max(ow, oh) > 4096 ? 8192 : 4096;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.wrapMode = si != null ? si.wrapMode : TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        Texture2D graded = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        int n = Swap(satellite, graded);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Course Look: wrote " + dst + " (" + ow + " x " + oh + "), swapped in at " + n + " places");
    }

    void RestoreSatellite()
    {
        if (satellite == null) return;
        string src = AssetDatabase.GetAssetPath(satellite);
        string dst = Path.GetDirectoryName(src) + "/" + Path.GetFileNameWithoutExtension(src) + "_graded.jpg";
        Texture2D graded = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        if (graded == null) return;
        int n = Swap(graded, satellite);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Course Look: original satellite back at " + n + " places");
    }

    static float Bi(byte a, byte b, byte c, byte d, float tx, float ty)
    {
        return ((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty) / 255f;
    }

    static byte B(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255); }

    void Grade(ref float r, ref float g, ref float b, float[] bp)
    {
        r = Mathf.Pow(Mathf.Clamp01((r - bp[0]) / (1f - bp[0])), s.gamma);
        g = Mathf.Pow(Mathf.Clamp01((g - bp[1]) / (1f - bp[1])), s.gamma);
        b = Mathf.Pow(Mathf.Clamp01((b - bp[2]) / (1f - bp[2])), s.gamma);
        r = (r - 0.42f) * s.contrast + 0.42f; g = (g - 0.42f) * s.contrast + 0.42f; b = (b - 0.42f) * s.contrast + 0.42f;
        float y = 0.299f * r + 0.587f * g + 0.114f * b;
        r = y + (r - y) * s.saturation; g = y + (g - y) * s.saturation; b = y + (b - y) * s.saturation;
        r *= 1f + s.warmth; b *= 1f - s.warmth;
    }

    // value noise, 3 octaves (periods 2, 6, 24 px), -1..1; matches grade.grain()
    static float Grain(int px, int py)
    {
        float sum = 0f;
        int[] period = { 2, 6, 24 };
        float[] weight = { 0.5f, 0.3f, 0.2f };
        for (int o = 0; o < 3; o++)
        {
            float fx = (float)px / period[o], fy = (float)py / period[o];
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            float a = Hash(ix, iy, o) * (1 - tx) + Hash(ix + 1, iy, o) * tx;
            float b = Hash(ix, iy + 1, o) * (1 - tx) + Hash(ix + 1, iy + 1, o) * tx;
            sum += (a * (1 - ty) + b * ty) * weight[o];
        }
        return sum;
    }

    static float Hash(int ix, int iy, int o)
    {
        long h = ((long)ix * 73856093L) ^ ((long)iy * 19349663L) ^ ((long)o * 83492791L);
        h &= 0x7FFFFFFFL;
        h = ((h ^ (h >> 13)) * 1274126177L) & 0x7FFFFFFFL;
        return (h & 0xFFFF) / 65535f * 2f - 1f;
    }
}
