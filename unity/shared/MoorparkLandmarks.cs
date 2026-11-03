// Moorpark > Range Logo: the white three-horse logo on the hillside at the far end of the range, draped on the terrain
// like Tierra's TR letters (white gravel, GSPrough ball physics). Mesh from moorpark/tools/logo_mesh.py, placed and sized
// to match the satellite (logo/logo_fit.jpg). Unity 2018.2 compatible.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkLandmarks
{
    public static string LogoJson = "C:/Users/user/Documents/moorpark/logo/logo_mesh.json";
    const string Dir = "Assets/Moorpark/generated/landmarks";
    public static float Lift = 0.04f;              // as Tierra's letters

    [System.Serializable] class LogoMesh { public List<V2> verts; public List<I3> tris; }
    [System.Serializable] class V2 { public float x, z; }
    [System.Serializable] class I3 { public int a, b, c; }

    [MenuItem("Moorpark/Range Logo")]
    public static string RangeLogo()
    {
        // logo_mesh.json is lists of [x, z] / [a, b, c]; JsonUtility can't read nested arrays, so parse by hand
        string txt = File.ReadAllText(LogoJson);
        var verts = new List<Vector2>(); var tris = new List<int>();
        int vi = txt.IndexOf("\"verts\""), ti = txt.IndexOf("\"tris\"");
        foreach (var pair in Pairs(txt.Substring(vi, ti - vi))) verts.Add(new Vector2(pair[0], pair[1]));
        foreach (var t in Pairs(txt.Substring(ti))) { if (t.Length == 3) { tris.Add((int)t[0]); tris.Add((int)t[1]); tris.Add((int)t[2]); } }
        var terrain = GameObject.Find("Terrain (Inner)").GetComponent<Terrain>();
        var v3 = new Vector3[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            var p = new Vector3(verts[i].x, 0, verts[i].y);
            v3[i] = new Vector3(p.x, Ground(terrain, p.x, p.z) + Lift, p.z);
        }
        // faces up
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 n = Vector3.Cross(v3[tris[i + 1]] - v3[tris[i]], v3[tris[i + 2]] - v3[tris[i]]);
            if (n.y < 0) { int s = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = s; }
        }
        var mesh = new Mesh { name = "Moorpark_RangeLogo" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v3; mesh.triangles = tris.ToArray();
        var uv = new Vector2[v3.Length]; for (int i = 0; i < v3.Length; i++) uv[i] = new Vector2(v3[i].x, v3[i].z);
        mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        Directory.CreateDirectory(Dir);
        AssetDatabase.DeleteAsset(Dir + "/RangeLogo.asset"); AssetDatabase.CreateAsset(mesh, Dir + "/RangeLogo.asset");

        var old = GameObject.Find("RANGE_LOGO"); if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject("RANGE_LOGO");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = WhiteMaterial();
        var col = go.AddComponent<MeshCollider>(); col.sharedMesh = mesh; col.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/GSPrough");
        go.isStatic = true;
        EditorSceneManager.MarkSceneDirty(go.scene);
        var b = mesh.bounds;
        return "RANGE_LOGO: " + v3.Length + " verts, " + tris.Count / 3 + " tris, centre " + b.center + ", size " + b.size;
    }

    // visible ground: the terrain, or a course mesh (rough on the range hillside) up to 3 m above it, as Tierra's letters
    static float Ground(Terrain t, float x, float z)
    {
        float y = t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y, terrainY = y;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, terrainY + 50f, z), Vector3.down, 52f))
        {
            if (h.collider.isTrigger || !(h.collider is MeshCollider)) continue;
            if (h.point.y <= y || h.point.y > terrainY + 3f) continue;
            string top = h.transform.root.name;
            if (top == "RANGE_LOGO" || top == "LIDAR_TREES" || top == "SHRUBS" || top == "CLUBHOUSE") continue;
            y = h.point.y;
        }
        return y;
    }

    static IEnumerable<float[]> Pairs(string s)
    {
        int i = 0;
        while ((i = s.IndexOf('[', i)) >= 0)
        {
            int j = s.IndexOf(']', i);
            string inner = s.Substring(i + 1, j - i - 1);
            if (inner.Contains("[")) { i++; continue; }
            var parts = inner.Split(',');
            var f = new float[parts.Length];
            for (int k = 0; k < parts.Length; k++) f[k] = float.Parse(parts[k], System.Globalization.CultureInfo.InvariantCulture);
            yield return f;
            i = j + 1;
        }
    }

    static Material WhiteMaterial()
    {
        // Tierra's white gravel (TierraHillsideLogo), if that tool has made it in this project
        foreach (string g in AssetDatabase.FindAssets("white_gravel t:Material"))
        { var tm = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (tm != null) return tm; }
        string p = Dir + "/RangeLogoWhite.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard"));
        m.color = new Color(0.93f, 0.93f, 0.9f);        // painted white turf / white sand, matte
        m.SetFloat("_Glossiness", 0.08f);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
}
