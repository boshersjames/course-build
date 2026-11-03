// Tierra > Paint Grass & Brush
// Fills terrain detail layers (grass billboards or detail meshes) from the course masks in terrain_masks/
// (2000 x 2000 px PNG images saved as .mask so GitHub LFS is not needed, 1 px = 1 m, north up, same frame as the terrain: x east, z north from the SW corner).
//   grass_deep_rough.mask   dense tall golden grass in the deep rough patches
//   grass_dry_hillside.mask golden dry grass on the hills outside play and inside OB
//   brush_sage_scrub.mask   green-grey coastal sage scrub tufts
//   no_grass.mask           playing surfaces, cart paths, water, roads, buildings: always cleared
// Unity 2018.2. Put this file in Assets/Editor.
using System.IO;
using UnityEditor;
using UnityEngine;

public class TierraPaintGrass : EditorWindow
{
    [System.Serializable]
    public class Layer
    {
        public string name = "Layer";
        public string mask = "";
        public Texture2D texture;          // grass billboard (alpha)
        public GameObject mesh;            // or a detail mesh prefab
        public float density = 8f;         // max instances per detail cell (0-16)
        public float minHeight = 0.5f, maxHeight = 1f, minWidth = 0.5f, maxWidth = 1f;
        public Color healthy = new Color(0.78f, 0.68f, 0.40f), dry = new Color(0.86f, 0.74f, 0.45f);
        public bool enabled = true;
    }

    public Layer[] layers;
    public Terrain terrain;
    string folder = "";
    string noGrass = "no_grass.mask";
    int resolution = 2048;
    float detailDistance = 250f;   // 150 popped the brush in visibly (Oct 27)
    int seed = 7;
    Vector2 scroll;
    SerializedObject so;

    [MenuItem("Tierra/Paint Grass and Brush")]
    static void Open() { GetWindow<TierraPaintGrass>("Grass & Brush"); }

    void OnEnable()
    {
        so = new SerializedObject(this);
        if (string.IsNullOrEmpty(folder))
            folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), Directory.Exists("Assets/Moorpark") ? "moorpark/terrain_masks" : "tierra-rejada/terrain_masks");   // Moorpark project: its own masks
        if (terrain == null)
        {
            GameObject inner = GameObject.Find("Terrain (Inner)");
            terrain = inner != null ? inner.GetComponent<Terrain>() : Terrain.activeTerrain;
        }
        if (layers == null || layers.Length == 0) layers = DefaultLayers();
    }

    static Layer[] DefaultLayers()
    {
        Layer a = new Layer(); a.name = "Deep rough grass"; a.mask = "grass_deep_rough.mask"; a.density = 12;
        a.minHeight = 0.6f; a.maxHeight = 1.1f; a.minWidth = 0.6f; a.maxWidth = 1.0f;
        a.healthy = new Color(0.80f, 0.70f, 0.42f); a.dry = new Color(0.90f, 0.78f, 0.50f);
        Layer b = new Layer(); b.name = "Dry hillside grass"; b.mask = "grass_dry_hillside.mask"; b.density = 7;
        b.minHeight = 0.35f; b.maxHeight = 0.8f; b.minWidth = 0.5f; b.maxWidth = 0.9f;
        b.healthy = new Color(0.76f, 0.66f, 0.42f); b.dry = new Color(0.88f, 0.77f, 0.52f);
        // hillside brush (e.g. left of 8 above the 9 tees): dense dry buckwheat/sage, rust-brown to tan, knee to waist high
        Layer c = new Layer(); c.name = "Dry scrub (buckwheat/sage)"; c.mask = "brush_sage_scrub.mask"; c.density = 9;
        c.minHeight = 0.5f; c.maxHeight = 1.0f; c.minWidth = 0.7f; c.maxWidth = 1.3f;
        c.healthy = new Color(0.55f, 0.36f, 0.22f); c.dry = new Color(0.74f, 0.56f, 0.36f);
        // the occasional green bush in the same brush (the LiDAR shrubs / Azalea cover the bigger ones)
        Layer e = new Layer(); e.name = "Green scrub accent"; e.mask = "brush_sage_scrub.mask"; e.density = 0.4f;
        e.minHeight = 0.6f; e.maxHeight = 1.2f; e.minWidth = 0.8f; e.maxWidth = 1.4f;
        e.healthy = new Color(0.40f, 0.48f, 0.30f); e.dry = new Color(0.52f, 0.55f, 0.38f);
        return new Layer[] { a, b, c, e };
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        EditorGUILayout.LabelField("Mask folder");
        EditorGUILayout.BeginHorizontal();
        folder = EditorGUILayout.TextField(folder);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string f = EditorUtility.OpenFolderPanel("terrain_masks folder", folder, "");
            if (!string.IsNullOrEmpty(f)) folder = f;
        }
        EditorGUILayout.EndHorizontal();
        noGrass = EditorGUILayout.TextField("No-grass mask", noGrass);
        resolution = EditorGUILayout.IntPopup("Detail resolution", resolution, new string[] { "1024 (2 m)", "2048 (1 m)", "4048 (0.5 m)" }, new int[] { 1024, 2048, 4048 });
        detailDistance = EditorGUILayout.Slider("Detail distance (m)", detailDistance, 50f, 250f);
        seed = EditorGUILayout.IntField("Random seed", seed);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("layers"), new GUIContent("Layers (texture OR mesh each)"), true);
        so.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Each layer needs a grass texture with alpha (billboard) or a small mesh prefab. " +
            "Layers with neither are skipped. Painting replaces all detail layers on this terrain.", MessageType.Info);
        if (GUILayout.Button("Reset layers to Tierra defaults")) { layers = DefaultLayers(); so = new SerializedObject(this); }
        if (GUILayout.Button("Paint")) Paint();
        if (GUILayout.Button("Clear all terrain details")) ClearDetails();
        EditorGUILayout.EndScrollView();
    }

    class Mask
    {
        public int w, h; public byte[] v;
        public static Mask Load(string path)
        {
            if (!File.Exists(path)) return null;
            Texture2D t = new Texture2D(2, 2);
            if (!t.LoadImage(File.ReadAllBytes(path))) return null;
            Mask m = new Mask(); m.w = t.width; m.h = t.height;
            Color32[] px = t.GetPixels32(); m.v = new byte[px.Length];
            for (int i = 0; i < px.Length; i++) m.v[i] = px[i].r;    // row 0 = south
            Object.DestroyImmediate(t);
            return m;
        }
        public float At(float x, float z)       // course metres, 1 px = 1 m
        {
            int ix = (int)x, iz = (int)z;
            if (ix < 0 || iz < 0 || ix >= w || iz >= h) return 0f;
            return v[iz * w + ix] / 255f;
        }
    }

    void ClearDetails()
    {
        if (terrain == null) return;
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Clear details");
        terrain.terrainData.detailPrototypes = new DetailPrototype[0];
    }

    void Paint()
    {
        if (terrain == null) { EditorUtility.DisplayDialog("Grass", "Pick the terrain (Terrain (Inner)).", "OK"); return; }
        TerrainData td = terrain.terrainData;
        Mask none = Mask.Load(Path.Combine(folder, noGrass));
        System.Collections.Generic.List<Layer> use = new System.Collections.Generic.List<Layer>();
        System.Collections.Generic.List<Mask> masks = new System.Collections.Generic.List<Mask>();
        System.Collections.Generic.List<DetailPrototype> protos = new System.Collections.Generic.List<DetailPrototype>();
        foreach (Layer l in layers)
        {
            if (!l.enabled || (l.texture == null && l.mesh == null)) continue;
            Mask m = Mask.Load(Path.Combine(folder, l.mask));
            if (m == null) { Debug.LogWarning("Mask not found: " + l.mask); continue; }
            DetailPrototype p = new DetailPrototype();
            if (l.mesh != null) { p.usePrototypeMesh = true; p.prototype = l.mesh; p.renderMode = DetailRenderMode.VertexLit; }
            else { p.usePrototypeMesh = false; p.prototypeTexture = l.texture; p.renderMode = DetailRenderMode.GrassBillboard; }
            p.minHeight = l.minHeight; p.maxHeight = l.maxHeight; p.minWidth = l.minWidth; p.maxWidth = l.maxWidth;
            p.healthyColor = l.healthy; p.dryColor = l.dry; p.noiseSpread = 0.3f; p.bendFactor = 0.3f;
            use.Add(l); masks.Add(m); protos.Add(p);
        }
        if (use.Count == 0) { EditorUtility.DisplayDialog("Grass", "No layer has a texture or mesh, or the masks weren't found in\n" + folder, "OK"); return; }
        Undo.RegisterCompleteObjectUndo(td, "Paint grass");
        td.SetDetailResolution(resolution, 16);
        td.detailPrototypes = protos.ToArray();
        td.wavingGrassStrength = 0.25f; td.wavingGrassAmount = 0.25f; td.wavingGrassSpeed = 0.4f; td.wavingGrassTint = Color.white;
        terrain.detailObjectDistance = detailDistance;
        terrain.detailObjectDensity = 1f;
        Vector3 pos = terrain.transform.position, size = td.size;
        int res = td.detailResolution;
        System.Random rnd = new System.Random(seed);
        float ox = (float)rnd.NextDouble() * 1000f, oz = (float)rnd.NextDouble() * 1000f;
        try
        {
            for (int k = 0; k < use.Count; k++)
            {
                int[,] map = new int[res, res];
                long total = 0;
                for (int j = 0; j < res; j++)
                {
                    if (j % 64 == 0) EditorUtility.DisplayProgressBar("Grass", use[k].name, (k + (float)j / res) / use.Count);
                    float z = pos.z + (j + 0.5f) * size.z / res;
                    for (int i = 0; i < res; i++)
                    {
                        float x = pos.x + (i + 0.5f) * size.x / res;
                        if (none != null && none.At(x, z) > 0.5f) continue;
                        float m = masks[k].At(x, z);
                        if (m <= 0.02f) continue;
                        // clumpy variation so it doesn't read as a carpet
                        float n = 0.45f + 1.1f * Mathf.PerlinNoise((x + ox) * 0.07f + k * 13f, (z + oz) * 0.07f);
                        float dv = use[k].density * m * n * (size.x / res) * (size.z / res);   // density is per m2
                        int c = (int)dv; if (rnd.NextDouble() < dv - c) c++;
                        c = Mathf.Clamp(c, 0, 16);
                        map[j, i] = c; total += c;
                    }
                }
                td.SetDetailLayer(0, 0, k, map);
                Debug.Log("Grass layer " + use[k].name + ": " + total + " instances");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
    }
}
