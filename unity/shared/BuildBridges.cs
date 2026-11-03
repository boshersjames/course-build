using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Simple wooden footbridges for cart path crossings: a deck plus two low side rails per row of bridges.csv
// (x1,z1,x2,z2,width; Unity metres). The deck sits 5 cm above the highest course surface under its ends
// (cart path, OPCD mesh or terrain). Everything goes under a BRIDGES object; building again replaces it.
public class BuildBridges : EditorWindow
{
    string csvPath = "Assets/TierraRejada/bridges.csv";
    float deckThickness = 0.15f, lift = 0.05f, railHeight = 0.6f, railThickness = 0.08f;
    const string ParentName = "BRIDGES";

    [MenuItem("Tierra/Build Bridges")]
    static void Open() { GetWindow<BuildBridges>("Build Bridges"); }

    void OnGUI()
    {
        csvPath = EditorGUILayout.TextField("Bridges CSV", csvPath);
        deckThickness = EditorGUILayout.FloatField("Deck thickness (m)", deckThickness);
        railHeight = EditorGUILayout.FloatField("Rail height (m)", railHeight);
        if (GUILayout.Button("Build bridges")) Build();
        if (GUILayout.Button("Remove bridges")) { var o = GameObject.Find(ParentName); if (o != null) Undo.DestroyObjectImmediate(o); }
        EditorGUILayout.HelpBox("Bridges go under a '" + ParentName + "' object. Building again replaces them.", MessageType.Info);
    }

    static float P(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

    static float Surface(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f))
        {
            string root = h.collider.transform.root.name;
            if (root == "OPCD Meshes" || root == "CART_PATHS" || h.collider is TerrainCollider) best = Mathf.Max(best, h.point.y);
        }
        return float.IsNegativeInfinity(best) ? 0f : best;
    }

    static Material Wood()
    {
        // same wood as the fences when they exist, otherwise a plain brown material
        var f = GameObject.Find("FENCES");
        if (f != null) { var r = f.GetComponentInChildren<Renderer>(); if (r != null && r.sharedMaterial != null) return r.sharedMaterial; }
        string path = "Assets/TierraRejada/generated/BridgeWood.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { Directory.CreateDirectory("Assets/TierraRejada/generated"); m = new Material(Shader.Find("Standard")) { color = new Color(0.36f, 0.25f, 0.16f) }; AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static PhysicMaterial Timber()
    {
        var g = AssetDatabase.FindAssets("GSPtimber t:PhysicMaterial").FirstOrDefault();
        return g != null ? AssetDatabase.LoadAssetAtPath<PhysicMaterial>(AssetDatabase.GUIDToAssetPath(g)) : Resources.Load<PhysicMaterial>("Physics/GSPtimber");
    }

    public void Build()
    {
        if (!File.Exists(csvPath)) { Debug.LogError("BuildBridges: no " + csvPath); return; }
        var old = GameObject.Find(ParentName); if (old != null) Undo.DestroyObjectImmediate(old);
        var parent = new GameObject(ParentName); Undo.RegisterCreatedObjectUndo(parent, "Build bridges");
        Material wood = Wood(); PhysicMaterial timber = Timber();
        int n = 0;
        foreach (var line in File.ReadAllLines(csvPath).Skip(1))
        {
            var f = line.Split(','); if (f.Length < 5) continue;
            Vector2 a = new Vector2(P(f[0]), P(f[1])), b = new Vector2(P(f[2]), P(f[3])); float width = P(f[4]);
            float ya = Surface(a.x, a.y), yb = Surface(b.x, b.y);
            Vector3 A = new Vector3(a.x, ya + lift, a.y), B = new Vector3(b.x, yb + lift, b.y);
            Vector3 mid = (A + B) / 2f; float len = (B - A).magnitude;
            Quaternion rot = Quaternion.LookRotation(B - A);
            var bridge = new GameObject("Bridge_" + n++); bridge.transform.SetParent(parent.transform, false);
            bridge.transform.position = mid; bridge.transform.rotation = rot;
            Box(bridge.transform, "Deck", new Vector3(0, -deckThickness / 2f, 0), new Vector3(width, deckThickness, len), wood, timber);
            foreach (float side in new[] { -1f, 1f })
                Box(bridge.transform, side < 0 ? "Rail_L" : "Rail_R", new Vector3(side * (width / 2f - railThickness / 2f), railHeight / 2f, 0), new Vector3(railThickness, railHeight, len), wood, timber);
            Debug.Log(string.Format("BuildBridges: Bridge_{0} {1:F1} m from ({2:F1},{3:F1}) to ({4:F1},{5:F1}), deck y {6:F2}-{7:F2}", n - 1, len, a.x, a.y, b.x, b.y, A.y, B.y));
        }
        foreach (var t in parent.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        EditorSceneManager.MarkSceneDirty(parent.scene);
    }

    static void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material m, PhysicMaterial phys)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos; g.transform.localRotation = Quaternion.identity; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        var c = g.GetComponent<BoxCollider>(); if (phys != null) c.sharedMaterial = phys;
    }
}
