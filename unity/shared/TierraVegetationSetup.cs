// Tierra > Vegetation (SoCal set)
// Builds materials and LOD prefabs for the procedural Southern California vegetation in
// Assets/TierraRejada/Vegetation (from vegetation_manifest.json), then can:
//   - load the shrub prefabs into Tierra > Plant Shrubs
//   - load the grass / scrub textures into Tierra > Paint Grass and Brush
//   - swap the trees under LIDAR_TREES for coast live oak / pepper / eucalyptus, matched to each tree's height
// Unity 2018.2, built-in render pipeline, Standard shader (cutout). Put this file in Assets/Editor.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class TierraVegetationSetup : EditorWindow
{
    const string ROOT = "Assets/TierraRejada/Vegetation";

    [Serializable] public class Mat { public string name; public string texture; public string kind; public float cutoff; public float smoothness; }
    [Serializable] public class Part { public string node; public string material; public int lod; }
    [Serializable] public class Asset
    {
        public string name; public string fbx; public string category; public string note;
        public float height; public float width; public float trunkRadius; public float trunkHeight; public float canopyRadius; public float canopyHeight;
        public Part[] parts; public float[] lodScreen; public int[] tris;
    }
    [Serializable] public class Manifest { public Mat[] materials; public Asset[] assets; }

    bool legacyShader = false;          // Legacy Cutout Diffuse instead of Standard (cheaper, flatter)
    bool treeColliders = true;
    bool shrubColliders = true;
    float lodBias = 1f;                 // multiplies the LOD switch heights (lower = switch sooner)
    // tree swap
    float eucalyptusAbove = 16f;
    float pepperShare = 0.2f;
    int seed = 1999;
    Vector2 scroll;

    [MenuItem("Tierra/Vegetation (SoCal set)")]
    static void Open() { GetWindow<TierraVegetationSetup>("SoCal Vegetation"); }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("1. Build", EditorStyles.boldLabel);
        legacyShader = EditorGUILayout.Toggle(new GUIContent("Legacy cutout shader", "Off: Standard shader (best look). On: Legacy Transparent/Cutout/Diffuse (cheapest)."), legacyShader);
        treeColliders = EditorGUILayout.Toggle("Tree colliders (timber + leaves)", treeColliders);
        shrubColliders = EditorGUILayout.Toggle("Shrub colliders (leaves)", shrubColliders);
        lodBias = EditorGUILayout.Slider("LOD distance x", lodBias, 0.5f, 2f);
        if (GUILayout.Button("Build materials and prefabs")) Build();
        EditorGUILayout.HelpBox("Prefabs go to " + ROOT + "/Prefabs/Shrubs and /Trees. Re-running updates them in place.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Fill the Tierra tools", EditorStyles.boldLabel);
        if (GUILayout.Button("Load shrubs into Plant Shrubs")) SendShrubs();
        if (GUILayout.Button("Load grass and scrub into Paint Grass and Brush")) SendGrass();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Swap LiDAR trees", EditorStyles.boldLabel);
        eucalyptusAbove = EditorGUILayout.Slider("Eucalyptus if taller than (m)", eucalyptusAbove, 10f, 30f);
        pepperShare = EditorGUILayout.Slider("Pepper tree share of the rest", pepperShare, 0f, 0.6f);
        seed = EditorGUILayout.IntField("Random seed", seed);
        if (GUILayout.Button("Swap trees under LIDAR_TREES")) SwapTrees();
        EditorGUILayout.HelpBox("Replaces each tree under LIDAR_TREES with a SoCal prefab scaled to the same height " +
            "(oaks, some pepper trees, eucalyptus for the tall ones). Undo works.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ build
    static Manifest Load()
    {
        string path = ROOT + "/vegetation_manifest.json";
        if (!File.Exists(path)) { EditorUtility.DisplayDialog("Vegetation", "Missing " + path + "\nCopy the Vegetation folder into Assets/TierraRejada first.", "OK"); return null; }
        return JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
    }

    // Make the folder on disk and let Unity import it. (AssetDatabase.CreateFolder makes "Vegetation 1" when the
    // copied Vegetation folder hasn't been imported yet, and CreateAsset then fails.)
    static void Folder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    void Build()
    {
        Manifest m = Load();
        if (m == null) return;
        try
        {
            Folder(ROOT + "/Materials"); Folder(ROOT + "/Prefabs/Shrubs"); Folder(ROOT + "/Prefabs/Trees");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportTextures(m);
            Dictionary<string, Material> mats = new Dictionary<string, Material>();
            foreach (Mat mm in m.materials) mats[mm.name] = MakeMaterial(mm);
            int n = 0;
            foreach (Asset a in m.assets)
            {
                EditorUtility.DisplayProgressBar("Vegetation", a.name, (float)(n++) / m.assets.Length);
                MakePrefab(a, mats);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Tierra vegetation: built " + m.assets.Length + " prefabs in " + ROOT + "/Prefabs");
    }

    static void ImportTextures(Manifest m)
    {
        foreach (string f in Directory.GetFiles(ROOT + "/Textures", "*.png"))
        {
            string p = f.Replace('\\', '/');
            TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            string file = Path.GetFileName(p);
            bool bark = file.StartsWith("TR_Bark_");
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = bark ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = !bark;
            ti.mipmapEnabled = true;
            ti.wrapMode = bark ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = bark ? 2 : 1;
            ti.maxTextureSize = file == "TR_Shrubs_Atlas.png" ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            if (!bark)
            {
                ti.mipMapsPreserveCoverage = true;     // stops cutout foliage thinning out with distance
                ti.alphaTestReferenceValue = 0.45f;
            }
            ti.SaveAndReimport();
        }
    }

    Material MakeMaterial(Mat mm)
    {
        string path = ROOT + "/Materials/" + mm.name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool cut = mm.kind == "cutout";
        Shader sh = legacyShader ? Shader.Find(cut ? "Legacy Shaders/Transparent/Cutout/Diffuse" : "Legacy Shaders/Diffuse") : Shader.Find("Standard");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = sh;
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "/Textures/" + mm.texture);
        // the shrub atlas a step darker: it read frosted in GSPro's sun (Oct 26 build)
        mat.color = mm.name == "TR_Shrubs" ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", mm.cutoff > 0 ? mm.cutoff : 0.45f);
        if (!legacyShader)
        {
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", mm.smoothness);
            mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_GlossyReflections", 0f); mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            if (cut)
            {
                mat.SetFloat("_Mode", 1f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_Mode", 0f);
                mat.SetOverrideTag("RenderType", "");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = -1;
            }
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static float RendererHeight(GameObject go)
    {
        bool any = false; Bounds b = new Bounds();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b.max.y - Mathf.Max(b.min.y, go.transform.position.y - 1f) : 0f;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { Transform f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    void MakePrefab(Asset a, Dictionary<string, Material> mats)
    {
        string fbx = ROOT + "/" + a.fbx;
        ModelImporter mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (mi == null) { Debug.LogWarning("Vegetation: missing " + fbx); return; }
        mi.importMaterials = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.None;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importBlendShapes = false;
        mi.addCollider = false;
        mi.isReadable = true;                      // Plant Shrubs combines these meshes
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        GameObject go = (GameObject)Instantiate(model);
        go.name = a.name;
        go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        // the FBX is in metres; if a unit setting slipped (x100 / x0.01), fix the import scale once
        float h = RendererHeight(go);
        if (h > 0f && (h / a.height > 3f || a.height / h > 3f))
        {
            DestroyImmediate(go);
            mi.globalScale *= a.height / h;
            mi.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            go = (GameObject)Instantiate(model);
            go.name = a.name;
            Debug.Log("Vegetation: rescaled " + a.name + " import by " + (a.height / h));
        }
        foreach (LODGroup old in go.GetComponentsInChildren<LODGroup>()) DestroyImmediate(old);

        int nl = a.lodScreen.Length;
        List<Renderer>[] byLod = new List<Renderer>[nl];
        for (int i = 0; i < nl; i++) byLod[i] = new List<Renderer>();
        foreach (Part p in a.parts)
        {
            Transform t = FindDeep(go.transform, p.node);
            if (t == null) { Debug.LogWarning("Vegetation: " + a.name + " has no node " + p.node); continue; }
            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;
            Material mat;
            if (mats.TryGetValue(p.material, out mat)) r.sharedMaterial = mat;
            r.shadowCastingMode = (a.category == "Shrubs" && p.lod > 0) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (p.lod < nl) byLod[p.lod].Add(r);
        }
        LODGroup lg = go.AddComponent<LODGroup>();
        LOD[] lods = new LOD[nl];
        for (int i = 0; i < nl; i++) lods[i] = new LOD(Mathf.Clamp01(a.lodScreen[i] / lodBias), byLod[i].ToArray());
        lg.SetLODs(lods);
        lg.fadeMode = LODFadeMode.None;
        lg.RecalculateBounds();

        PhysicMaterial timber = Resources.Load<PhysicMaterial>("Physics/GSPtimber");
        PhysicMaterial leaves = Resources.Load<PhysicMaterial>("Physics/GSPleaves");
        if (a.category == "Trees" && treeColliders)
        {
            GameObject tc = new GameObject("collider_trunk");
            tc.transform.SetParent(go.transform, false);
            CapsuleCollider cc = tc.AddComponent<CapsuleCollider>();
            cc.direction = 1; cc.radius = Mathf.Max(0.15f, a.trunkRadius); cc.height = a.trunkHeight; cc.center = new Vector3(0, a.trunkHeight * 0.5f, 0);
            cc.sharedMaterial = timber;
            GameObject lc = new GameObject("collider_canopy");
            lc.transform.SetParent(go.transform, false);
            SphereCollider sc = lc.AddComponent<SphereCollider>();
            sc.radius = a.canopyRadius; sc.center = new Vector3(0, a.canopyHeight, 0);
            sc.sharedMaterial = leaves;
        }
        else if (a.category == "Shrubs" && shrubColliders)
        {
            CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
            float r = Mathf.Clamp(a.width * 0.35f, 0.25f, a.height * 0.6f);
            cc.direction = 1; cc.radius = r; cc.height = Mathf.Max(a.height, 2f * r); cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cc.sharedMaterial = leaves;
        }
        foreach (Transform t in go.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

        string path = ROOT + "/Prefabs/" + a.category + "/" + a.name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) PrefabUtility.ReplacePrefab(go, existing, ReplacePrefabOptions.ReplaceNameBased);
        else PrefabUtility.CreatePrefab(path, go);
        DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ hand-off to the other Tierra tools
    static GameObject Prefab(string cat, string name)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "/Prefabs/" + cat + "/" + name + ".prefab");
    }

    static Type FindType(string name)
    {
        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    static void SendShrubs()
    {
        Type t = FindType("TierraPlantShrubs");
        if (t == null) { EditorUtility.DisplayDialog("Vegetation", "TierraPlantShrubs.cs isn't in Assets/Editor.", "OK"); return; }
        // picked uniformly at random, so repeats set the mix. The LiDAR shrubs are the taller ones (the head-high brush by the
        // 8 tees): grey-green sage and coyote brush, rust buckwheat, grey dead brush and the odd dark laurel sumac / toyon.
        string[] mix = { "TR_PurpleSage", "TR_PurpleSage", "TR_PurpleSage_B", "TR_CoyoteBrush", "TR_Scrub_Patch",
                         "TR_LaurelSumac", "TR_LaurelSumac_Big", "TR_Toyon",                        // green / dark: 8 of 20
                         "TR_Buckwheat_A", "TR_Buckwheat_A", "TR_Buckwheat_B", "TR_Buckwheat_B", "TR_Buckwheat_C",
                         "TR_Buckwheat_Patch", "TR_Buckwheat_Patch", "TR_DeadBrush", "TR_DeadBrush",
                         "TR_Sagebrush", "TR_GreyBuckwheat", "TR_Mustard" };                         // brown / grey: 12 of 20
        List<GameObject> list = new List<GameObject>();
        foreach (string n in mix) { GameObject g = Prefab("Shrubs", n); if (g != null) list.Add(g); }
        if (list.Count == 0) { EditorUtility.DisplayDialog("Vegetation", "Build the prefabs first (step 1).", "OK"); return; }
        EditorWindow w = GetWindow(t);
        t.GetField("prefabs").SetValue(w, list.ToArray());
        w.Repaint();
        Debug.Log("Plant Shrubs: loaded " + list.Count + " SoCal shrub prefabs");
    }

    struct G
    {
        public string name, mask, tex; public float density, minH, maxH, minW, maxW; public Color healthy, dry;
        public G(string n, string m, string t, float d, float h0, float h1, float w0, float w1, Color a, Color b)
        { name = n; mask = m; tex = t; density = d; minH = h0; maxH = h1; minW = w0; maxW = w1; healthy = a; dry = b; }
    }

    static void SendGrass()
    {
        Type t = FindType("TierraPaintGrass");
        if (t == null) { EditorUtility.DisplayDialog("Vegetation", "TierraPaintGrass.cs isn't in Assets/Editor.", "OK"); return; }
        Type lt = t.GetNestedType("Layer");
        // textures are already coloured, so the tints only scale brightness (small variation between healthy and dry)
        // The terrain grass billboard shader doubles the detail colour, so white here renders the cards at 2x and they
        // look frosted white in GSPro's sun (seen on the 3 and 10 tees). 0.38 x the old near-white tints (0.45 tested on the PC: still light).
        Color w1 = new Color(0.38f, 0.37f, 0.355f), w2 = new Color(0.345f, 0.32f, 0.28f), g1 = new Color(0.365f, 0.37f, 0.345f), g2 = new Color(0.33f, 0.32f, 0.28f);
        // the golden grass textures are the lightest in the set (wild oat ~160, foxtail ~150, short dry ~140 vs ~80 for scrub):
        // these were the pale cards on the 10 bank (found on the PC by colouring each layer)
        Color o1 = new Color(0.25f, 0.245f, 0.235f), o2 = new Color(0.23f, 0.215f, 0.19f);
        Color f1 = new Color(0.30f, 0.295f, 0.28f), f2 = new Color(0.275f, 0.255f, 0.225f);
        // scrub 0.8 x again after the Oct 26 GSPro build (still frosted): s = buckwheat (0.8 w), q = the palest tufts (grey
        // buckwheat, sagebrush, purple sage), c = coyote / green
        // scrub (0.8 g), k = dark scrub on the hole 2 right hillside (0.5 g: those cards read lighter than the ground).
        // Dead brush and mustard keep w / g.
        Color s1 = new Color(0.304f, 0.296f, 0.284f), s2 = new Color(0.276f, 0.256f, 0.224f);
        Color q1 = new Color(0.24f, 0.24f, 0.224f), q2 = new Color(0.216f, 0.208f, 0.184f);
        Color c1 = new Color(0.292f, 0.296f, 0.276f), c2 = new Color(0.264f, 0.256f, 0.224f);
        Color k1 = new Color(0.184f, 0.188f, 0.176f), k2 = new Color(0.168f, 0.164f, 0.144f);
        const string DR = "grass_deep_rough.mask", HS = "grass_dry_hillside.mask", BR = "brush_sage_scrub.mask", BX = "brush_boost.mask", GD = "grass_green_deep.mask", PR = "pond_reeds.mask";
        const string OG = "ornamental_grass.mask", SD = "scrub_dark.mask";
        // golden grass only where the satellite drape is tan; deep rough over green drape gets green grass instead
        // (vegetation/build/make_sat_masks.py, after the Oct 27 flyover review)
        const string HSS = "grass_dry_hillside_sat.mask", DRS = "grass_deep_rough_sat.mask", DRG = "grass_deep_rough_green.mask";
        const string BRS = "brush_sage_scrub_sat.mask";   // mustard: yellow flowers only on tan ground
        // hole 3 / 4 hill: golden on the upper slope, green deep rough low down (hole3_4_hill.json from the PC, Oct 27)
        const string HU = "hill_u.mask", HD = "hill_dry.mask", HG = "hill_green.mask";
        // the scrub-type cards keep 3 % of the full brush load: any more and inside the detail distance the hillsides turn into
        // a grey / rust carpet instead of golden grass with scattered dark clumps (BoJa, hole 3, Oct 27). Mustard is left as is.
        const float SCRUB = 0.03f;
        // The terrain sits 0.25 m under the course meshes (raised from 1 m on the PC, Oct 26), so the short layers are
        // 0.15-0.25 m taller than the grass they stand for: the visible part above the mesh is the real height.
        G[] defs = {
            new G("Deep rough wild oats", DRS, "TR_Grass_WildOat", 10f, 0.6f, 1.1f, 0.6f, 1.0f, o1, o2),
            new G("Deep rough foxtail", DRS, "TR_Grass_Foxtail", 4f, 0.55f, 0.85f, 0.5f, 0.9f, f1, f2),
            new G("Dry hillside grass", HSS, "TR_Grass_ShortDry", 6f, 0.45f, 0.75f, 0.6f, 1.1f, f1, f2),
            new G("Hillside oats", HSS, "TR_Grass_WildOat", 1.2f, 0.5f, 0.9f, 0.5f, 0.9f, o1, o2),
            // scrub is knee to head high (photo from the 8 blue tee): sizes are metres.
            // About 12 per m2 in the brush (30 % up on the first pass), with green / dark ~20 % of it: browner hillsides.
            new G("Dry scrub (buckwheat)", BR, "TR_Detail_Buckwheat", 4.5f * SCRUB, 0.7f, 1.4f, 1.0f, 1.8f, s1, s2),
            new G("Scrub, grey buckwheat", BR, "TR_Detail_GreyBuckwheat", 1.6f * SCRUB, 0.7f, 1.3f, 1.0f, 1.7f, q1, q2),
            new G("Scrub, purple sage", BR, "TR_Detail_PurpleSage", 1.55f * SCRUB, 1.0f, 2.0f, 1.2f, 2.2f, q1, q2),
            new G("Scrub, coyote brush", BR, "TR_Detail_CoyoteBrush", 0.85f * SCRUB, 1.0f, 2.2f, 1.2f, 2.4f, c1, c2),
            new G("Scrub, sagebrush", BR, "TR_Detail_Sage", 1.3f * SCRUB, 0.8f, 1.5f, 1.0f, 1.8f, q1, q2),
            new G("Scrub, dead brush", BR, "TR_Detail_DeadBrush", 1.25f * SCRUB, 0.7f, 1.5f, 0.9f, 1.6f, w1, w2),
            new G("Scrub, mustard", BRS, "TR_Detail_Mustard", 0.5f, 0.8f, 1.5f, 0.9f, 1.5f, g1, g2),
            new G("Green scrub accent", BR, "TR_Detail_GreenScrub", 0.3f * SCRUB, 1.2f, 2.4f, 1.3f, 2.6f, c1, c2),
            // extra scrub in named areas (brush_boost.mask, from vegetation/build/make_boost_mask.py; 128 = +50 %).
            // Densities here are a full brush load, so the mask value is the fraction added on top.
            new G("Boost: buckwheat", BX, "TR_Detail_Buckwheat", 6.1f * SCRUB, 0.7f, 1.4f, 1.0f, 1.8f, s1, s2),
            new G("Boost: purple sage", BX, "TR_Detail_PurpleSage", 2.4f * SCRUB, 1.0f, 2.0f, 1.2f, 2.2f, q1, q2),
            new G("Boost: sagebrush", BX, "TR_Detail_Sage", 1.3f * SCRUB, 0.8f, 1.5f, 1.0f, 1.8f, q1, q2),
            new G("Boost: dead brush", BX, "TR_Detail_DeadBrush", 2.05f * SCRUB, 0.7f, 1.5f, 0.9f, 1.6f, w1, w2),
            // green deep rough, 18 in and thick (hole 15 left of the green; deep_rough/scripts/green_deep_rough.py)
            new G("Green deep rough (18 in)", GD, "TR_Grass_GreenDeep", 14f, 0.7f, 0.85f, 0.5f, 0.85f, g1, g2),
            new G("Green deep rough, seed heads", GD, "TR_Grass_GreenGold", 2f, 0.65f, 0.8f, 0.5f, 0.8f, g1, g2),
            // reed / long-grass band at pond edges (deep_rough/scripts/pond_reeds.py; first: hole 1 pond, green side)
            new G("Pond edge reeds", PR, "TR_Grass_Reeds", 8f, 0.9f, 1.6f, 0.6f, 1.0f, g1, g2),
            new G("Pond edge foxtail", PR, "TR_Grass_Foxtail", 4f, 0.55f, 0.85f, 0.5f, 0.9f, f1, f2),
            // ornamental grass clumps by the hole 12 tees, clear of the paths (vegetation/build/make_area_masks.py)
            new G("Ornamental grass", OG, "TR_Grass_Ornamental", 10f, 0.9f, 1.2f, 0.9f, 1.3f, f1, f2),
            // darker, denser scrub on the hole 2 right hillside (make_area_masks.py), coyote brush heavy
            new G("Dark scrub: coyote brush", SD, "TR_Detail_CoyoteBrush", 3f * SCRUB, 1.0f, 2.2f, 1.2f, 2.4f, k1, k2),
            new G("Dark scrub: sage", SD, "TR_Detail_Sage", 1.5f * SCRUB, 0.8f, 1.5f, 1.0f, 1.8f, k1, k2),
            new G("Dark scrub: green scrub", SD, "TR_Detail_GreenScrub", 1.5f * SCRUB, 1.2f, 2.4f, 1.3f, 2.6f, k1, k2),
            // appended last so the layer numbers above stay the same
            new G("Deep rough, green over green drape", DRG, "TR_Grass_GreenDeep", 8f, 0.55f, 0.8f, 0.5f, 0.85f, g1, g2),
            new G("Hill: wild oats", HU, "TR_Grass_WildOat", 5f, 0.6f, 1.1f, 0.6f, 1.0f, o1, o2),
            new G("Hill: foxtail", HU, "TR_Grass_Foxtail", 1.5f, 0.55f, 0.85f, 0.5f, 0.9f, f1, f2),
            new G("Hill: dry grass", HD, "TR_Grass_ShortDry", 5f, 0.45f, 0.75f, 0.6f, 1.1f, f1, f2),
            new G("Hill: green deep rough", HG, "TR_Grass_GreenDeep", 7f, 0.7f, 0.85f, 0.5f, 0.85f, g1, g2),
            new G("Hill: green seed heads", HG, "TR_Grass_GreenGold", 0.8f, 0.65f, 0.8f, 0.5f, 0.8f, g1, g2),
        };
        Array arr = Array.CreateInstance(lt, defs.Length);
        for (int i = 0; i < defs.Length; i++)
        {
            object l = Activator.CreateInstance(lt);
            G d = defs[i];
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "/Textures/" + d.tex + ".png");
            if (tex == null) { EditorUtility.DisplayDialog("Vegetation", "Missing texture " + d.tex + ".png", "OK"); return; }
            lt.GetField("name").SetValue(l, d.name);
            lt.GetField("mask").SetValue(l, d.mask);
            lt.GetField("texture").SetValue(l, tex);
            lt.GetField("mesh").SetValue(l, null);
            lt.GetField("density").SetValue(l, d.density);
            lt.GetField("minHeight").SetValue(l, d.minH); lt.GetField("maxHeight").SetValue(l, d.maxH);
            lt.GetField("minWidth").SetValue(l, d.minW); lt.GetField("maxWidth").SetValue(l, d.maxW);
            lt.GetField("healthy").SetValue(l, d.healthy); lt.GetField("dry").SetValue(l, d.dry);
            lt.GetField("enabled").SetValue(l, true);
            arr.SetValue(l, i);
        }
        EditorWindow w = GetWindow(t);
        t.GetField("layers").SetValue(w, arr);
        w.Repaint();
        Debug.Log("Paint Grass and Brush: loaded " + defs.Length + " SoCal layers. Pick the terrain and click Paint.");
    }

    // ------------------------------------------------------------------ swap LiDAR trees
    void SwapTrees()
    {
        GameObject root = GameObject.Find("LIDAR_TREES");
        if (root == null) { EditorUtility.DisplayDialog("Vegetation", "No LIDAR_TREES object in the scene. Run Tierra > Plant LiDAR Trees first.", "OK"); return; }
        GameObject[] oaks = { Prefab("Trees", "TR_Oak_A"), Prefab("Trees", "TR_Oak_B") };
        GameObject[] peppers = { Prefab("Trees", "TR_Pepper_A"), Prefab("Trees", "TR_Pepper_B") };
        GameObject[] eucs = { Prefab("Trees", "TR_Eucalyptus_A"), Prefab("Trees", "TR_Eucalyptus_B") };
        if (oaks[0] == null || peppers[0] == null || eucs[0] == null) { EditorUtility.DisplayDialog("Vegetation", "Build the prefabs first (step 1).", "OK"); return; }
        Dictionary<GameObject, float> ph = new Dictionary<GameObject, float>();
        foreach (GameObject[] set in new[] { oaks, peppers, eucs })
            foreach (GameObject p in set) if (p != null) ph[p] = PrefabHeight(p);
        // collect first: the tree roots are the direct children (or grandchildren under tile groups) that have renderers
        List<Transform> trees = new List<Transform>();
        Collect(root.transform, trees);
        System.Random rnd = new System.Random(seed);
        int done = 0;
        try
        {
            for (int i = 0; i < trees.Count; i++)
            {
                Transform old = trees[i];
                if (i % 50 == 0) EditorUtility.DisplayProgressBar("Swap trees", i + " / " + trees.Count, (float)i / trees.Count);
                float h = RendererHeight(old.gameObject);
                if (h <= 0.5f) continue;
                GameObject[] set = h >= eucalyptusAbove ? eucs : (rnd.NextDouble() < pepperShare ? peppers : oaks);
                GameObject pf = set[rnd.Next(set.Length)] ?? set[0];
                GameObject n = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                Undo.RegisterCreatedObjectUndo(n, "Swap trees");
                n.transform.SetParent(old.parent, false);
                n.transform.position = old.position;
                n.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                n.transform.localScale = Vector3.one * (h / ph[pf]);
                Undo.DestroyObjectImmediate(old.gameObject);
                done++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log("Swapped " + done + " trees under LIDAR_TREES for SoCal species");
    }

    static void Collect(Transform t, List<Transform> outList)
    {
        foreach (Transform c in t)
        {
            bool isTree = c.GetComponent<LODGroup>() != null || c.GetComponent<Renderer>() != null || PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject) != null;
            if (isTree) outList.Add(c);
            else Collect(c, outList);       // a grouping object (e.g. tiles)
        }
    }

    static float PrefabHeight(GameObject prefab)
    {
        GameObject tmp = (GameObject)Instantiate(prefab, Vector3.zero, Quaternion.identity);
        float h = RendererHeight(tmp);
        DestroyImmediate(tmp);
        return Mathf.Max(h, 0.5f);
    }
}
