// Tierra > Plant Shrubs
// Plants trees/tierra_shrubs_clean.csv (id,x,z,y,height_m,crown_radius_m; Unity metres) from one or more prefabs.
//   Combined chunks (default): shrub meshes merged per 100 m tile and material -> a few hundred draw calls for ~11.5k shrubs.
//   Prefab instances: one GameObject per shrub (keeps LODs/wind, heavier).
// Colliders: one capsule per shrub on GSPleaves (option), kept in separate collider-only objects.
// Unity 2018.2. Put this file in Assets/Editor.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public class TierraPlantShrubs : EditorWindow
{
    public GameObject[] prefabs = new GameObject[0];
    string csv = "";
    bool combine = true;
    float chunk = 100f;
    float heightScale = 1f;
    float jitter = 0.15f;       // +/- random size variation
    int seed = 1999;
    bool useTerrainHeight = true;
    bool colliders = true;
    PhysicMaterial physics;
    const string ROOT = "SHRUBS";
    const string MESH_DIR = "Assets/TierraRejada/generated/shrubs";
    SerializedObject so;

    [MenuItem("Tierra/Plant Shrubs")]
    static void Open() { GetWindow<TierraPlantShrubs>("Plant Shrubs"); }

    void OnEnable()
    {
        so = new SerializedObject(this);
        if (physics == null) physics = Resources.Load<PhysicMaterial>("Physics/GSPleaves");
        if (string.IsNullOrEmpty(csv))
            csv = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), Directory.Exists("Assets/Moorpark") ? "moorpark/shrubs_clean.csv" : "tierra-rejada/trees/tierra_shrubs_clean.csv");   // Moorpark project: its own LiDAR shrubs
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Shrub CSV");
        EditorGUILayout.BeginHorizontal();
        csv = EditorGUILayout.TextField(csv);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string f = EditorUtility.OpenFilePanel("Shrub CSV", Path.GetDirectoryName(csv), "csv");
            if (!string.IsNullOrEmpty(f)) csv = f;
        }
        EditorGUILayout.EndHorizontal();
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("prefabs"), new GUIContent("Shrub prefabs (picked at random)"), true);
        so.ApplyModifiedProperties();
        combine = EditorGUILayout.Toggle("Combine into chunks", combine);
        if (combine) chunk = EditorGUILayout.Slider("Chunk size (m)", chunk, 25f, 250f);
        heightScale = EditorGUILayout.Slider("Height multiplier", heightScale, 0.3f, 2f);
        jitter = EditorGUILayout.Slider("Random size +/-", jitter, 0f, 0.5f);
        seed = EditorGUILayout.IntField("Random seed", seed);
        useTerrainHeight = EditorGUILayout.Toggle("Snap to terrain", useTerrainHeight);
        colliders = EditorGUILayout.Toggle("Capsule colliders", colliders);
        if (colliders) physics = (PhysicMaterial)EditorGUILayout.ObjectField("Physics material", physics, typeof(PhysicMaterial), false);
        EditorGUILayout.HelpBox("Each shrub is scaled so the prefab's height matches the LiDAR height_m (x multiplier). " +
            "Re-running replaces the " + ROOT + " object.", MessageType.Info);
        if (GUILayout.Button("Plant")) Plant();
        if (GUILayout.Button("Remove " + ROOT)) Clear();
    }

    void Clear()
    {
        GameObject old = GameObject.Find(ROOT);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    struct Shrub { public Vector3 pos; public float h, r; }

    List<Shrub> Read()
    {
        List<Shrub> list = new List<Shrub>();
        Terrain t = CourseTerrain();
        string[] lines = File.ReadAllLines(csv);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] p = lines[i].Split(',');
            if (p.Length < 6) continue;
            CultureInfo ci = CultureInfo.InvariantCulture;
            float x = float.Parse(p[1], ci), z = float.Parse(p[2], ci), y = float.Parse(p[3], ci);
            if (useTerrainHeight && t != null) y = Ground(t, x, z);
            Shrub s = new Shrub();
            s.pos = new Vector3(x, y, z); s.h = float.Parse(p[4], ci); s.r = float.Parse(p[5], ci);
            list.Add(s);
        }
        return list;
    }

    // The course terrain. Terrain.activeTerrain can be the big flat outer terrain, which buries everything.
    static Terrain CourseTerrain()
    {
        GameObject inner = GameObject.Find("Terrain (Inner)");
        if (inner != null && inner.GetComponent<Terrain>() != null) return inner.GetComponent<Terrain>();
        Terrain best = Terrain.activeTerrain; float top = float.MinValue;
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 p = t.transform.position, s = t.terrainData.size;
            if (1000f < p.x || 1000f > p.x + s.x || 1000f < p.z || 1000f > p.z + s.z) continue;
            float h = t.SampleHeight(new Vector3(1000f, 0, 1000f)) + p.y;
            if (h > top) { top = h; best = t; }
        }
        return best;
    }

    // Visible ground: the terrain, or an OPCD surface mesh (rough, tee, fairway) up to 3 m above it.
    // "Lower Terrain Under Meshes" drops the terrain 1 m under those meshes, so the terrain alone half-buries shrubs there.
    static float Ground(Terrain t, float x, float z)
    {
        float y = t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y, terrainY = y;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, terrainY + 50f, z), Vector3.down, 52f))
        {
            if (h.collider.isTrigger || !(h.collider is MeshCollider)) continue;
            if (h.point.y <= y || h.point.y > terrainY + 3f) continue;
            string top = h.transform.root.name;
            if (top == ROOT || top == "LIDAR_TREES" || top == "FENCES" || top == "WALLS" || top == "NETS") continue;
            y = h.point.y;
        }
        return y;
    }

    // LOD0 (or all) renderers of a prefab, and its height at scale 1
    static List<MeshFilter> Lod0(GameObject go)
    {
        List<MeshFilter> mfs = new List<MeshFilter>();
        LODGroup lg = go.GetComponentInChildren<LODGroup>();
        if (lg != null && lg.GetLODs().Length > 0)
        {
            foreach (Renderer r in lg.GetLODs()[0].renderers)
                if (r != null && r.GetComponent<MeshFilter>() != null) mfs.Add(r.GetComponent<MeshFilter>());
        }
        if (mfs.Count == 0)
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) mfs.Add(mf);
        return mfs;
    }

    static float PrefabHeight(GameObject prefab)
    {
        GameObject tmp = (GameObject)Instantiate(prefab, Vector3.zero, Quaternion.identity);
        Bounds b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
        foreach (Renderer r in tmp.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        DestroyImmediate(tmp);
        return any ? Mathf.Max(b.max.y, 0.1f) : 1f;
    }

    void Plant()
    {
        List<GameObject> pf = new List<GameObject>();
        foreach (GameObject g in prefabs) if (g != null) pf.Add(g);
        if (pf.Count == 0) { EditorUtility.DisplayDialog("Plant Shrubs", "Add at least one shrub prefab.", "OK"); return; }
        if (!File.Exists(csv)) { EditorUtility.DisplayDialog("Plant Shrubs", "CSV not found:\n" + csv, "OK"); return; }
        List<Shrub> shrubs = Read();
        float[] ph = new float[pf.Count];
        for (int i = 0; i < pf.Count; i++) ph[i] = PrefabHeight(pf[i]);
        Clear();
        GameObject root = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(root, "Plant shrubs");
        System.Random rnd = new System.Random(seed);
        // per-shrub choice, rotation and scale (decided up front so both modes match)
        int[] pick = new int[shrubs.Count]; Quaternion[] rot = new Quaternion[shrubs.Count]; float[] scl = new float[shrubs.Count];
        for (int i = 0; i < shrubs.Count; i++)
        {
            pick[i] = rnd.Next(pf.Count);
            rot[i] = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            float j = 1f + jitter * (2f * (float)rnd.NextDouble() - 1f);
            scl[i] = shrubs[i].h * heightScale * j / ph[pick[i]];
        }
        try
        {
            if (combine) PlantCombined(root, pf, shrubs, pick, rot, scl);
            else PlantInstances(root, pf, shrubs, pick, rot, scl);
            if (colliders) AddColliders(root, shrubs, scl, ph, pick);
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        Debug.Log("Planted " + shrubs.Count + " shrubs under " + ROOT + (combine ? " (combined)" : " (instances)"));
    }

    void PlantInstances(GameObject root, List<GameObject> pf, List<Shrub> shrubs, int[] pick, Quaternion[] rot, float[] scl)
    {
        Dictionary<string, Transform> tiles = new Dictionary<string, Transform>();
        for (int i = 0; i < shrubs.Count; i++)
        {
            if (i % 250 == 0) EditorUtility.DisplayProgressBar("Plant Shrubs", "Placing " + i + " / " + shrubs.Count, (float)i / shrubs.Count);
            Transform parent = Tile(root.transform, tiles, shrubs[i].pos, 100f);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(pf[pick[i]]);
            go.transform.SetParent(parent, false);
            go.transform.position = shrubs[i].pos; go.transform.rotation = rot[i]; go.transform.localScale = Vector3.one * scl[i];
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) DestroyImmediate(c);   // ours are added separately
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }

    void PlantCombined(GameObject root, List<GameObject> pf, List<Shrub> shrubs, int[] pick, Quaternion[] rot, float[] scl)
    {
        Directory.CreateDirectory(MESH_DIR);
        foreach (string old in Directory.GetFiles(MESH_DIR, "*.asset")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
        // prefab parts: mesh, submesh, material, local matrix relative to the prefab root
        List<List<KeyValuePair<Material, CombineInstance>>> parts = new List<List<KeyValuePair<Material, CombineInstance>>>();
        foreach (GameObject p in pf)
        {
            List<KeyValuePair<Material, CombineInstance>> lst = new List<KeyValuePair<Material, CombineInstance>>();
            Matrix4x4 inv = p.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in Lod0(p))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                Material[] mats = mr.sharedMaterials;
                if (mats.Length == 0) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    if (mats[Mathf.Min(s, mats.Length - 1)] == null) continue;
                    CombineInstance ci = new CombineInstance();
                    ci.mesh = mf.sharedMesh; ci.subMeshIndex = s; ci.transform = inv * mf.transform.localToWorldMatrix;
                    lst.Add(new KeyValuePair<Material, CombineInstance>(mats[Mathf.Min(s, mats.Length - 1)], ci));
                }
            }
            parts.Add(lst);
        }
        // bucket by tile and material
        Dictionary<string, Dictionary<Material, List<CombineInstance>>> buckets = new Dictionary<string, Dictionary<Material, List<CombineInstance>>>();
        for (int i = 0; i < shrubs.Count; i++)
        {
            string key = TileKey(shrubs[i].pos, chunk);
            Dictionary<Material, List<CombineInstance>> byMat;
            if (!buckets.TryGetValue(key, out byMat)) { byMat = new Dictionary<Material, List<CombineInstance>>(); buckets[key] = byMat; }
            Matrix4x4 m = Matrix4x4.TRS(shrubs[i].pos, rot[i], Vector3.one * scl[i]);
            foreach (KeyValuePair<Material, CombineInstance> part in parts[pick[i]])
            {
                List<CombineInstance> l;
                if (!byMat.TryGetValue(part.Key, out l)) { l = new List<CombineInstance>(); byMat[part.Key] = l; }
                CombineInstance ci = part.Value; ci.transform = m * part.Value.transform; l.Add(ci);
            }
        }
        int n = 0, total = buckets.Count;
        foreach (KeyValuePair<string, Dictionary<Material, List<CombineInstance>>> tile in buckets)
        {
            EditorUtility.DisplayProgressBar("Plant Shrubs", "Combining tile " + tile.Key, (float)(n++) / total);
            GameObject tg = new GameObject("tile_" + tile.Key);
            tg.transform.SetParent(root.transform, false);
            int k = 0;
            foreach (KeyValuePair<Material, List<CombineInstance>> mm in tile.Value)
            {
                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(mm.Value.ToArray(), true, true);
                mesh.name = "shrubs_" + tile.Key + "_" + k;
                AssetDatabase.CreateAsset(mesh, MESH_DIR + "/" + mesh.name + ".asset");
                GameObject g = new GameObject(mesh.name);
                g.transform.SetParent(tg.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = mm.Key;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
                k++;
            }
        }
    }

    void AddColliders(GameObject root, List<Shrub> shrubs, float[] scl, float[] ph, int[] pick)
    {
        GameObject cr = new GameObject("colliders");
        cr.transform.SetParent(root.transform, false);
        Dictionary<string, Transform> tiles = new Dictionary<string, Transform>();
        for (int i = 0; i < shrubs.Count; i++)
        {
            if (i % 500 == 0) EditorUtility.DisplayProgressBar("Plant Shrubs", "Colliders " + i + " / " + shrubs.Count, (float)i / shrubs.Count);
            float h = ph[pick[i]] * scl[i];
            float r = Mathf.Clamp(shrubs[i].r, 0.4f, h * 0.6f);
            GameObject g = new GameObject("c");
            g.transform.SetParent(Tile(cr.transform, tiles, shrubs[i].pos, 100f), false);
            g.transform.position = shrubs[i].pos;
            CapsuleCollider cc = g.AddComponent<CapsuleCollider>();
            cc.direction = 1; cc.height = Mathf.Max(h, 2f * r); cc.radius = r; cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cc.sharedMaterial = physics;
            g.isStatic = true;
        }
    }

    static string TileKey(Vector3 p, float size) { return Mathf.FloorToInt(p.x / size) + "_" + Mathf.FloorToInt(p.z / size); }

    static Transform Tile(Transform parent, Dictionary<string, Transform> tiles, Vector3 p, float size)
    {
        string k = TileKey(p, size); Transform t;
        if (!tiles.TryGetValue(k, out t)) { t = new GameObject("tile_" + k).transform; t.SetParent(parent, false); tiles[k] = t; }
        return t;
    }
}
