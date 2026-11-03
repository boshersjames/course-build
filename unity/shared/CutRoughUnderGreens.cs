using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Removes the rough triangles that lie under a green (same height, so the two surfaces z-fight in the flyover and in GSPro).
// For each green mesh (OPCD "..._Green_-_Mesh") the xz footprint comes from the mesh's boundary edges, shrunk by `inset`.
// A rough triangle is dropped when its centroid is inside a footprint and within `maxDy` of the green surface above/below it.
// The cut mesh is saved under Assets/TierraRejada/generated/cut_meshes and assigned to the MeshFilter and MeshCollider; the
// original FBX is not touched and log.json records what was replaced, so "Restore" puts the original meshes back.
public class CutRoughUnderGreens : EditorWindow
{
    const string Folder = "Assets/TierraRejada/generated/cut_meshes";
    const string LogPath = Folder + "/log.json";
    string greenFilter = "Spline_green_14_Green_-_Mesh";   // comma separated names, or * for every green
    float inset = 0.1f, maxDy = 0.3f;

    [Serializable] public class Entry { public string obj; public string originalMesh; public string originalPath; public string cutPath; public int removed; public int kept; }
    [Serializable] public class Log { public List<Entry> entries = new List<Entry>(); }

    [MenuItem("Tierra/Cut Rough Under Greens")]
    static void Open() { GetWindow<CutRoughUnderGreens>("Cut Rough"); }

    void OnGUI()
    {
        greenFilter = EditorGUILayout.TextField("Greens (names or *)", greenFilter);
        inset = EditorGUILayout.FloatField("Footprint inset (m)", inset);
        maxDy = EditorGUILayout.FloatField("Max height gap (m)", maxDy);
        if (GUILayout.Button("Cut")) Cut();
        if (GUILayout.Button("Restore original meshes")) Restore();
        EditorGUILayout.HelpBox("Writes " + Folder + " and " + LogPath + ". Green meshes are not changed.", MessageType.Info);
    }

    static Transform Opcd() { var g = GameObject.Find("OPCD Meshes"); return g != null ? g.transform : null; }

    class Footprint { public List<Vector2[]> loops = new List<Vector2[]>(); public Collider col; public Bounds b; }

    // boundary edges (used by one triangle) chained into loops, in world xz
    static Footprint MakeFootprint(MeshFilter mf)
    {
        var m = mf.sharedMesh; var v = m.vertices; var t = m.triangles; var tr = mf.transform;
        // weld by position: OPCD meshes repeat vertices at UV seams
        var key = new Dictionary<Vector3Int, int>(); var id = new int[v.Length]; var pos = new List<Vector2>();
        for (int i = 0; i < v.Length; i++)
        {
            var w = tr.TransformPoint(v[i]); var k = new Vector3Int(Mathf.RoundToInt(w.x * 1000), Mathf.RoundToInt(w.y * 1000), Mathf.RoundToInt(w.z * 1000));
            int j; if (!key.TryGetValue(k, out j)) { j = pos.Count; key[k] = j; pos.Add(new Vector2(w.x, w.z)); }
            id[i] = j;
        }
        var count = new Dictionary<long, int>();
        Func<int, int, long> E = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3)
            for (int e = 0; e < 3; e++) { long k = E(id[t[i + e]], id[t[i + (e + 1) % 3]]); int c; count.TryGetValue(k, out c); count[k] = c + 1; }
        var next = new Dictionary<int, List<int>>();
        foreach (var kv in count.Where(k => k.Value == 1))
        {
            int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
            if (!next.ContainsKey(a)) next[a] = new List<int>(); if (!next.ContainsKey(b)) next[b] = new List<int>();
            next[a].Add(b); next[b].Add(a);
        }
        var fp = new Footprint { col = mf.GetComponent<Collider>(), b = mf.GetComponent<Renderer>().bounds };
        var used = new HashSet<int>();
        foreach (var start in next.Keys)
        {
            if (used.Contains(start)) continue;
            var loop = new List<Vector2>(); int cur = start, prev = -1;
            while (cur >= 0 && !used.Contains(cur))
            {
                used.Add(cur); loop.Add(pos[cur]);
                int nx = -1; foreach (var n in next[cur]) if (n != prev && !used.Contains(n)) { nx = n; break; }
                prev = cur; cur = nx;
            }
            if (loop.Count >= 3) fp.loops.Add(loop.ToArray());
        }
        return fp;
    }

    static bool Inside(Footprint f, Vector2 p, float inset)
    {
        bool inside = false; float dmin = float.MaxValue;
        foreach (var poly in f.loops)
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                var ab = b - a; float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
                dmin = Mathf.Min(dmin, (a + ab * tt - p).magnitude);
            }
        return inside && dmin > inset;
    }

    static void LoadLog(out Log log) { log = File.Exists(LogPath) ? JsonUtility.FromJson<Log>(File.ReadAllText(LogPath)) : new Log(); }

    public void Cut()
    {
        var opcd = Opcd(); if (opcd == null) { Debug.LogError("No 'OPCD Meshes' in the scene"); return; }
        var names = greenFilter.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        var greens = opcd.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.name.Contains("_Green_-_Mesh") && m.sharedMesh != null && (names.Contains("*") || names.Contains(m.name)))
            .Select(MakeFootprint).ToList();
        if (greens.Count == 0) { Debug.LogWarning("CutRoughUnderGreens: no green matches '" + greenFilter + "'"); return; }
        Directory.CreateDirectory(Folder);
        Log log; LoadLog(out log);
        var roughs = opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null && (m.name.Contains("Rough_-_Mesh") || m.name.Contains("Semi_Rough")) && !m.name.Contains("_B_")).ToList();
        foreach (var mf in roughs)
        {
            var rb = mf.GetComponent<Renderer>().bounds;
            var hit = greens.Where(g => g.b.max.x > rb.min.x && g.b.min.x < rb.max.x && g.b.max.z > rb.min.z && g.b.min.z < rb.max.z).ToList();
            if (hit.Count == 0) continue;
            var src = mf.sharedMesh;
            if (!src.isReadable) { Debug.LogWarning(mf.name + ": mesh not readable, skipped"); continue; }
            var v = src.vertices; var tr = mf.transform; var keep = new List<List<int>>(); int removed = 0, kept = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s); var k = new List<int>(tri.Length);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var c = (tr.TransformPoint(v[tri[i]]) + tr.TransformPoint(v[tri[i + 1]]) + tr.TransformPoint(v[tri[i + 2]])) / 3f;
                    bool drop = false;
                    foreach (var g in hit)
                    {
                        if (c.x < g.b.min.x || c.x > g.b.max.x || c.z < g.b.min.z || c.z > g.b.max.z) continue;
                        if (!Inside(g, new Vector2(c.x, c.z), 0f)) continue;
                        // a centroid near the rim is only dropped when it's well inside; centroids deeper in always go
                        if (!Inside(g, new Vector2(c.x, c.z), inset)) continue;
                        RaycastHit h;
                        if (g.col != null && g.col.Raycast(new Ray(new Vector3(c.x, c.y + 50f, c.z), Vector3.down), out h, 100f) && Mathf.Abs(h.point.y - c.y) <= maxDy) { drop = true; break; }
                    }
                    if (drop) removed++; else { kept++; k.Add(tri[i]); k.Add(tri[i + 1]); k.Add(tri[i + 2]); }
                }
                keep.Add(k);
            }
            if (removed == 0) continue;
            var cut = Object.Instantiate(src); cut.name = src.name + "_cut";
            for (int s = 0; s < keep.Count; s++) cut.SetTriangles(keep[s], s);
            cut.RecalculateBounds();
            string path = Folder + "/" + mf.name + "_cut.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(cut, old); cut = old; } else AssetDatabase.CreateAsset(cut, path);
            var entry = log.entries.FirstOrDefault(e => e.obj == mf.name);
            if (entry == null) { entry = new Entry { obj = mf.name, originalMesh = src.name, originalPath = AssetDatabase.GetAssetPath(src) }; log.entries.Add(entry); }
            entry.cutPath = path; entry.removed = removed; entry.kept = kept;
            mf.sharedMesh = cut;
            var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = cut;
            EditorUtility.SetDirty(mf); if (mc != null) EditorUtility.SetDirty(mc);
            Debug.Log("CutRoughUnderGreens: " + mf.name + " removed " + removed + " triangles under " + string.Join(", ", hit.Select(h => h.col ? h.col.name : "?").ToArray()) + ", kept " + kept);
        }
        File.WriteAllText(LogPath, JsonUtility.ToJson(log, true));
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public void Restore()
    {
        Log log; LoadLog(out log); var opcd = Opcd(); if (opcd == null) return;
        foreach (var e in log.entries)
        {
            var mf = opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == e.obj); if (mf == null) continue;
            var orig = AssetDatabase.LoadAllAssetsAtPath(e.originalPath).OfType<Mesh>().FirstOrDefault(m => m.name == e.originalMesh);
            if (orig == null) { Debug.LogWarning("Original mesh " + e.originalMesh + " not found in " + e.originalPath); continue; }
            mf.sharedMesh = orig; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = orig;
            Debug.Log("CutRoughUnderGreens: restored " + e.obj);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
