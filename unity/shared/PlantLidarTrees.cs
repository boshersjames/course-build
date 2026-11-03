// Tierra Rejada: plant the LiDAR tree list (trees/tierra_trees.csv) in the open scene.
// Put this file in Assets/Editor/ then use the menu  Tierra > Plant LiDAR Trees.
// Unity 2018.2 compatible.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PlantLidarTrees : EditorWindow
{
    string csvPath = "Assets/TierraRejada/trees/tierra_trees.csv";
    DefaultAsset treeFolder;
    string nameMustContain = "", nameMustNotContain = "Mobile";
    PhysicMaterial timber, leaves;
    int seed = 1234;
    float minScale = 0.3f, maxScale = 3f;
    const string ParentName = "LIDAR_TREES";

    [MenuItem("Tierra/Plant LiDAR Trees")]
    static void Open() { GetWindow<PlantLidarTrees>("Plant LiDAR Trees"); }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Tree list (CSV inside the project)", EditorStyles.boldLabel);
        csvPath = EditorGUILayout.TextField("CSV path", csvPath);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Folder of tree prefabs to choose from", EditorStyles.boldLabel);
        treeFolder = (DefaultAsset)EditorGUILayout.ObjectField("Tree prefab folder", treeFolder, typeof(DefaultAsset), false);
        nameMustContain = EditorGUILayout.TextField("Name must contain", nameMustContain);
        nameMustNotContain = EditorGUILayout.TextField("Name must NOT contain", nameMustNotContain);
        if (treeFolder != null && GUILayout.Button("List trees that would be used")) ListTrees();
        seed = EditorGUILayout.IntField("Random seed", seed);
        minScale = EditorGUILayout.FloatField("Min scale", minScale);
        maxScale = EditorGUILayout.FloatField("Max scale", maxScale);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ball physics for tree colliders", EditorStyles.boldLabel);
        if (timber == null) timber = Resources.Load<PhysicMaterial>("Physics/GSPtimber");
        if (leaves == null) leaves = Resources.Load<PhysicMaterial>("Physics/GSPleaves");
        timber = (PhysicMaterial)EditorGUILayout.ObjectField("Trunk (lower) material", timber, typeof(PhysicMaterial), false);
        leaves = (PhysicMaterial)EditorGUILayout.ObjectField("Canopy (upper) material", leaves, typeof(PhysicMaterial), false);
        if (GUILayout.Button("Assign physics to planted trees")) AssignPhysics();
        EditorGUILayout.Space();
        if (GUILayout.Button("Plant trees")) Plant();
        if (GUILayout.Button("Remove planted trees")) Remove();
        EditorGUILayout.HelpBox("Trees are planted under a '" + ParentName + "' object. Planting again replaces them.", MessageType.Info);
    }

    void Remove()
    {
        var old = GameObject.Find(ParentName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    void Plant()
    {
        if (!File.Exists(csvPath)) { EditorUtility.DisplayDialog("Plant LiDAR Trees", "CSV not found:\n" + csvPath, "OK"); return; }
        if (treeFolder == null) { EditorUtility.DisplayDialog("Plant LiDAR Trees", "Pick a folder of tree prefabs first.", "OK"); return; }

        var prefabs = new List<GameObject>();
        var heights = new List<float>();
        foreach (var p in Candidates())
        {
            float h = PrefabHeight(p);
            if (h > 0.5f) { prefabs.Add(p); heights.Add(h); }
        }
        string folder = AssetDatabase.GetAssetPath(treeFolder);
        if (prefabs.Count == 0) { EditorUtility.DisplayDialog("Plant LiDAR Trees", "No tree prefabs with a visible mesh in\n" + folder, "OK"); return; }

        Remove();
        var parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Plant LiDAR Trees");
        var rng = new System.Random(seed);
        var lines = File.ReadAllLines(csvPath);
        int planted = 0, onMesh = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split(',');
            if (f.Length < 6) continue;
            float x = Parse(f[1]), z = Parse(f[2]), y = Parse(f[3]), h = Parse(f[4]);

            // stand the tree on whatever surface is there (course meshes sit above the lowered terrain)
            RaycastHit hit;
            // (never below the LiDAR ground: the terrain itself was lowered ~1 m under the meshes)
            // ground only: a raycast that stops on an already planted tree's canopy left trees floating 100 m up (Wilson, 10/28)
            hit = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.Where(Physics.RaycastAll(new Vector3(x, y + 200f, z), Vector3.down, 400f),
                q => { var rn = q.collider.transform.root.name; return rn != ParentName && rn != "SHRUBS" && rn != "FENCES" && rn != "NETS" && rn != "CART_PATHS"; }), q => q.distance));
            if (hit.collider != null) { y = Mathf.Max(hit.point.y, y); onMesh++; }

            int k = rng.Next(prefabs.Count);
            var t = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[k]);
            t.transform.SetParent(parent.transform);
            t.transform.position = new Vector3(x, y, z);
            t.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float s = Mathf.Clamp(h / heights[k], minScale, maxScale);
            t.transform.localScale = prefabs[k].transform.localScale * s;   // keep the prefab's own root scale (Broadleaf_Desktop_Small is 0.5): it planted double size
            t.name = prefabs[k].name + "_" + f[0];
            planted++;
            if (i % 100 == 0 && EditorUtility.DisplayCancelableProgressBar("Planting trees", planted + " planted", (float)i / lines.Length)) break;
        }
        EditorUtility.ClearProgressBar();
        Debug.Log("Planted " + planted + " LiDAR trees using " + prefabs.Count + " prefabs (" + onMesh + " placed on a surface hit).");
        AssignPhysics();
    }

    void AssignPhysics()
    {
        var parent = GameObject.Find(ParentName);
        if (parent == null) { Debug.LogWarning("No " + ParentName + " in the scene yet."); return; }
        if (timber == null || leaves == null) { EditorUtility.DisplayDialog("Plant LiDAR Trees", "Pick both physics materials first (GSPtimber and GSPleaves).", "OK"); return; }
        int trunk = 0, canopy = 0;
        foreach (Transform tree in parent.transform)
        {
            float baseY = tree.position.y, top = baseY;
            foreach (var r in tree.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
            float split = baseY + 0.35f * (top - baseY);   // lower third = trunk, above = canopy
            foreach (var c in tree.GetComponentsInChildren<Collider>(true))
            {
                Undo.RecordObject(c, "Assign tree physics");
                if (c.bounds.center.y <= split) { c.sharedMaterial = timber; trunk++; }
                else { c.sharedMaterial = leaves; canopy++; }
            }
        }
        int missing = 0; var examples = new List<string>();
        foreach (var c in Object.FindObjectsOfType<Collider>())
            if (c.sharedMaterial == null && !(c is TerrainCollider)) { missing++; if (examples.Count < 5) examples.Add(c.gameObject.name); }
        Debug.Log("Tree physics: " + trunk + " trunk colliders -> " + timber.name + ", " + canopy + " canopy colliders -> " + leaves.name +
                  ". Colliders elsewhere still without a physics material: " + missing + (missing > 0 ? " (e.g. " + string.Join(", ", examples.ToArray()) + ")" : ""));
    }

    List<GameObject> Candidates()
    {
        var list = new List<GameObject>();
        var seen = new HashSet<string>();
        // prefabs and models (SpeedTree .spm / FBX) in the folder and its subfolders
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { AssetDatabase.GetAssetPath(treeFolder) }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!seen.Add(path)) continue;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) continue;
            if (nameMustContain.Length > 0 && p.name.IndexOf(nameMustContain, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (nameMustNotContain.Length > 0 && p.name.IndexOf(nameMustNotContain, System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            list.Add(p);
        }
        return list;
    }

    void ListTrees()
    {
        var c = Candidates();
        var sb = new System.Text.StringBuilder("Trees that would be used (" + c.Count + "):\n");
        foreach (var p in c) sb.AppendLine("  " + AssetDatabase.GetAssetPath(p) + "   height " + PrefabHeight(p).ToString("0.0") + " m");
        Debug.Log(sb.ToString());
    }

    static float Parse(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

    static float PrefabHeight(GameObject p)
    {
        if (p == null) return 0f;
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(p);   // measure a real instance; asset bounds are not reliable
        tmp.transform.position = Vector3.zero;
        float h = 0f;
        var rends = tmp.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            h = b.size.y;
        }
        DestroyImmediate(tmp);
        return h;
    }
}
