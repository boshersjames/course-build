using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Lowers (or raises) the vertices of an OPCD course mesh near polylines, for banks the LiDAR missed and stream channels.
// sculpt.csv rows: name,mesh,x,z,radius,depth   (rows with the same name form one polyline; depth < 0 lowers).
// A vertex at distance d from a polyline moves by depth * smoothstep(1 - d / radius); where polylines overlap the
// largest move wins. The result is saved under Assets/TierraRejada/generated/sculpt and assigned to the MeshFilter and
// MeshCollider; log.json keeps the mesh that was there before, so "Restore" puts it back (the FBX is never touched).
public class SculptCourseMesh : EditorWindow
{
    string csvPath = "Assets/TierraRejada/sculpt.csv";
    const string Folder = "Assets/TierraRejada/generated/sculpt";
    const string LogPath = Folder + "/log.json";

    [Serializable] public class Entry { public string obj; public string previousMesh; public string previousPath; public string sculptPath; public int moved; }
    [Serializable] public class Log { public List<Entry> entries = new List<Entry>(); }

    [MenuItem("Tierra/Sculpt Course Mesh")]
    static void Open() { GetWindow<SculptCourseMesh>("Sculpt Mesh"); }

    void OnGUI()
    {
        csvPath = EditorGUILayout.TextField("Sculpt CSV", csvPath);
        if (GUILayout.Button("Apply")) Apply();
        if (GUILayout.Button("Restore previous meshes")) Restore();
        EditorGUILayout.HelpBox("Writes " + Folder + ". The original FBX meshes are not changed.", MessageType.Info);
    }

    static float P(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

    static float DistToPolyline(Vector2 p, List<Vector2> line)
    {
        if (line.Count == 1) return (p - line[0]).magnitude;
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a = line[i], b = line[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
            best = Mathf.Min(best, (a + ab * t - p).magnitude);
        }
        return best;
    }

    public void Apply()
    {
        if (!File.Exists(csvPath)) { Debug.LogError("SculptCourseMesh: no " + csvPath); return; }
        // mesh -> list of (polyline, radius, depth)
        var groups = new Dictionary<string, Dictionary<string, List<Vector4>>>();
        foreach (var line in File.ReadAllLines(csvPath).Skip(1))
        {
            var f = line.Split(','); if (f.Length < 6) continue;
            string name = f[0].Trim(), mesh = f[1].Trim();
            if (!groups.ContainsKey(mesh)) groups[mesh] = new Dictionary<string, List<Vector4>>();
            if (!groups[mesh].ContainsKey(name)) groups[mesh][name] = new List<Vector4>();
            groups[mesh][name].Add(new Vector4(P(f[2]), P(f[3]), P(f[4]), P(f[5])));
        }
        Directory.CreateDirectory(Folder);
        Log log = File.Exists(LogPath) ? JsonUtility.FromJson<Log>(File.ReadAllText(LogPath)) : new Log();
        var opcd = GameObject.Find("OPCD Meshes");
        foreach (var kv in groups)
        {
            var mf = opcd != null ? opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == kv.Key) : null;
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("SculptCourseMesh: no mesh " + kv.Key); continue; }
            var src = mf.sharedMesh;
            var prior = log.entries.FirstOrDefault(e => e.obj == kv.Key);
            if (prior != null)   // always sculpt from the mesh that was there before the first run, so re-running doesn't stack
            {
                var prev = AssetDatabase.LoadAllAssetsAtPath(prior.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == prior.previousMesh);
                if (prev != null) src = prev;
            }
            var tr = mf.transform; var v = src.vertices; int moved = 0;
            var lines = kv.Value.Values.Select(pts => new { line = pts.Select(p => new Vector2(p.x, p.y)).ToList(), radius = pts[0].z, depth = pts[0].w }).ToList();
            for (int i = 0; i < v.Length; i++)
            {
                var w = tr.TransformPoint(v[i]); var p = new Vector2(w.x, w.z); float dy = 0f;
                foreach (var l in lines)
                {
                    float d = DistToPolyline(p, l.line); if (d >= l.radius) continue;
                    float k = 1f - d / l.radius; k = k * k * (3f - 2f * k);
                    float m = l.depth * k; if (Mathf.Abs(m) > Mathf.Abs(dy)) dy = m;
                }
                if (dy != 0f) { w.y += dy; v[i] = tr.InverseTransformPoint(w); moved++; }
            }
            if (moved == 0) continue;
            var outMesh = Object.Instantiate(src); outMesh.name = kv.Key + "_sculpt"; outMesh.vertices = v; outMesh.RecalculateNormals(); outMesh.RecalculateBounds();
            string path = Folder + "/" + kv.Key + "_sculpt.asset";
            var entry = log.entries.FirstOrDefault(e => e.obj == kv.Key);
            if (entry == null) { entry = new Entry { obj = kv.Key, previousMesh = src.name, previousPath = AssetDatabase.GetAssetPath(src) }; log.entries.Add(entry); }
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && existing != src) { EditorUtility.CopySerialized(outMesh, existing); outMesh = existing; }
            else if (existing == null) AssetDatabase.CreateAsset(outMesh, path);
            else { EditorUtility.CopySerialized(outMesh, existing); outMesh = existing; }
            entry.sculptPath = path; entry.moved = moved;
            mf.sharedMesh = outMesh; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = outMesh; }
            Debug.Log("SculptCourseMesh: " + kv.Key + " moved " + moved + " vertices (" + lines.Count + " polylines)");
        }
        File.WriteAllText(LogPath, JsonUtility.ToJson(log, true));
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public void Restore()
    {
        if (!File.Exists(LogPath)) return;
        var log = JsonUtility.FromJson<Log>(File.ReadAllText(LogPath)); var opcd = GameObject.Find("OPCD Meshes"); if (opcd == null) return;
        foreach (var e in log.entries)
        {
            var mf = opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == e.obj); if (mf == null) continue;
            var prev = AssetDatabase.LoadAllAssetsAtPath(e.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == e.previousMesh);
            if (prev == null) { Debug.LogWarning("SculptCourseMesh: " + e.previousMesh + " not found in " + e.previousPath); continue; }
            mf.sharedMesh = prev; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = prev;
            Debug.Log("SculptCourseMesh: restored " + e.obj + " to " + e.previousPath);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
