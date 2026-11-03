using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Turns an area of rough into fairway (e.g. a false front up to a green). fairway_patches.csv rows:
//   patch,roughMesh,templateFairway,x,z       (rows with the same patch name form the polygon, Unity metres)
// Rough triangles whose centroid is inside the polygon and where that rough is the top course surface are moved into a
// new mesh "Patch_<name>_Fairway_-_Mesh" under OPCD Meshes, lifted `lift`, with the template fairway's materials,
// collider physics, layer and static flags, and UVs fitted to the template fairway (so the mowing texture lines up).
// The rough without those triangles is saved under Assets/TierraRejada/generated/fairway_patches and assigned to its
// MeshFilter/MeshCollider; log.json keeps the previous rough mesh for Restore (the FBX is never changed).
public class FairwayPatch : EditorWindow
{
    string csvPath = "Assets/TierraRejada/fairway_patches.csv";
    float lift = 0.01f;
    const string Folder = "Assets/TierraRejada/generated/fairway_patches", LogPath = Folder + "/log.json";

    [Serializable] public class Entry { public string patch; public string rough; public string previousMesh; public string previousPath; public int moved; }
    [Serializable] public class Log { public List<Entry> entries = new List<Entry>(); }

    [MenuItem("Tierra/Fairway Patch")]
    static void Open() { GetWindow<FairwayPatch>("Fairway Patch"); }

    void OnGUI()
    {
        csvPath = EditorGUILayout.TextField("Patches CSV", csvPath);
        lift = EditorGUILayout.FloatField("Lift above rough (m)", lift);
        if (GUILayout.Button("Apply")) Apply();
        if (GUILayout.Button("Restore rough, remove patches")) Restore();
    }

    static float P(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

    static bool Inside(List<Vector2> poly, Vector2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }

    // least-squares fit uv = a*x + b*z + c (per channel) on template vertices near the patch
    static Vector3[] FitUV(MeshFilter tpl, Vector2 centre, float radius)
    {
        var v = tpl.sharedMesh.vertices; var uv = tpl.sharedMesh.uv; var tr = tpl.transform; if (uv == null || uv.Length != v.Length) return null;
        double[,] ata = new double[3, 3]; double[] atu = new double[3], atv = new double[3]; int n = 0;
        for (int i = 0; i < v.Length; i++)
        {
            var w = tr.TransformPoint(v[i]); if ((new Vector2(w.x, w.z) - centre).magnitude > radius) continue;
            double[] r = { w.x - centre.x, w.z - centre.y, 1 };
            for (int a = 0; a < 3; a++) { for (int b = 0; b < 3; b++) ata[a, b] += r[a] * r[b]; atu[a] += r[a] * uv[i].x; atv[a] += r[a] * uv[i].y; }
            n++;
        }
        if (n < 3) return null;
        return new[] { Solve(ata, atu), Solve(ata, atv) };
    }

    static Vector3 Solve(double[,] m, double[] y)
    {
        var a = (double[,])m.Clone(); var b = (double[])y.Clone();
        for (int i = 0; i < 3; i++)
        {
            int p = i; for (int r = i + 1; r < 3; r++) if (Math.Abs(a[r, i]) > Math.Abs(a[p, i])) p = r;
            for (int c = 0; c < 3; c++) { var t = a[i, c]; a[i, c] = a[p, c]; a[p, c] = t; } { var t = b[i]; b[i] = b[p]; b[p] = t; }
            for (int r = 0; r < 3; r++) { if (r == i) continue; double f = a[r, i] / a[i, i]; for (int c = 0; c < 3; c++) a[r, c] -= f * a[i, c]; b[r] -= f * b[i]; }
        }
        return new Vector3((float)(b[0] / a[0, 0]), (float)(b[1] / a[1, 1]), (float)(b[2] / a[2, 2]));
    }

    public void Apply()
    {
        if (!File.Exists(csvPath)) { Debug.LogError("FairwayPatch: no " + csvPath); return; }
        var opcd = GameObject.Find("OPCD Meshes"); if (opcd == null) { Debug.LogError("FairwayPatch: no OPCD Meshes"); return; }
        var patches = new Dictionary<string, KeyValuePair<string[], List<Vector2>>>();
        foreach (var line in File.ReadAllLines(csvPath).Skip(1))
        {
            var f = line.Split(','); if (f.Length < 5) continue;
            if (!patches.ContainsKey(f[0])) patches[f[0]] = new KeyValuePair<string[], List<Vector2>>(new[] { f[1].Trim(), f[2].Trim() }, new List<Vector2>());
            patches[f[0]].Value.Add(new Vector2(P(f[3]), P(f[4])));
        }
        Directory.CreateDirectory(Folder);
        var log = File.Exists(LogPath) ? JsonUtility.FromJson<Log>(File.ReadAllText(LogPath)) : new Log();
        var mfs = opcd.GetComponentsInChildren<MeshFilter>(true);
        foreach (var kv in patches)
        {
            string name = kv.Key; var poly = kv.Value.Value;
            var rough = mfs.FirstOrDefault(m => m.name == kv.Value.Key[0]); var tpl = mfs.FirstOrDefault(m => m.name == kv.Value.Key[1]);
            if (rough == null || tpl == null) { Debug.LogError("FairwayPatch " + name + ": rough or template not found"); continue; }
            var entry = log.entries.FirstOrDefault(e => e.patch == name);
            Mesh src = rough.sharedMesh;
            if (entry != null) { var prev = AssetDatabase.LoadAllAssetsAtPath(entry.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == entry.previousMesh); if (prev != null) src = prev; }
            var rc = rough.GetComponent<MeshCollider>();
            var v = src.vertices; var tr = rough.transform;
            var keep = new List<List<int>>(); var moved = new List<int>();
            // temporarily use the source mesh on the collider so the top-surface test sees the unpatched rough
            if (rc != null) { rc.sharedMesh = null; rc.sharedMesh = src; }
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var t = src.GetTriangles(s); var k = new List<int>();
                for (int i = 0; i < t.Length; i += 3)
                {
                    var c = (tr.TransformPoint(v[t[i]]) + tr.TransformPoint(v[t[i + 1]]) + tr.TransformPoint(v[t[i + 2]])) / 3f;
                    bool take = Inside(poly, new Vector2(c.x, c.z));
                    if (take)
                    {
                        var top = Physics.RaycastAll(new Vector3(c.x, c.y + 50f, c.z), Vector3.down, 100f).Where(h => h.collider.transform.IsChildOf(opcd.transform) && !h.collider.name.Contains("Water") && !h.collider.name.StartsWith("Patch_")).OrderByDescending(h => h.point.y).FirstOrDefault();
                        take = top.collider != null && top.collider == rc;
                    }
                    if (take) moved.AddRange(new[] { t[i], t[i + 1], t[i + 2] }); else k.AddRange(new[] { t[i], t[i + 1], t[i + 2] });
                }
                keep.Add(k);
            }
            if (moved.Count == 0) { Debug.LogWarning("FairwayPatch " + name + ": no rough triangles inside"); continue; }
            // new rough
            var cut = Object.Instantiate(src); cut.name = rough.name + "_fp";
            for (int s = 0; s < keep.Count; s++) cut.SetTriangles(keep[s], s);
            string rp = Folder + "/" + rough.name + "_fp.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(rp);
            if (old != null) { EditorUtility.CopySerialized(cut, old); cut = old; } else AssetDatabase.CreateAsset(cut, rp);
            if (entry == null) { entry = new Entry { patch = name, rough = rough.name, previousMesh = src.name, previousPath = AssetDatabase.GetAssetPath(src) }; log.entries.Add(entry); }
            entry.moved = moved.Count / 3; File.WriteAllText(LogPath, JsonUtility.ToJson(log, true));   // log first, so Restore works even if the rest fails
            rough.sharedMesh = cut; if (rc != null) { rc.sharedMesh = null; rc.sharedMesh = cut; }
            // patch mesh in world space
            var centre = new Vector2(poly.Average(p => p.x), poly.Average(p => p.y));
            var fit = FitUV(tpl, centre, 40f);
            var map = new Dictionary<int, int>(); var pv = new List<Vector3>(); var puv = new List<Vector2>(); var pt = new List<int>();
            foreach (int i in moved)
            {
                int j; if (!map.TryGetValue(i, out j)) { j = pv.Count; map[i] = j; var w = tr.TransformPoint(v[i]); w.y += lift; pv.Add(w);
                    puv.Add(fit != null ? new Vector2(fit[0].x * (w.x - centre.x) + fit[0].y * (w.z - centre.y) + fit[0].z, fit[1].x * (w.x - centre.x) + fit[1].y * (w.z - centre.y) + fit[1].z) : new Vector2(w.x / 10f, w.z / 10f)); }
                pt.Add(j);
            }
            var pm = new Mesh { name = "Patch_" + name }; pm.SetVertices(pv); pm.SetUVs(0, puv); pm.SetTriangles(pt, 0); pm.RecalculateNormals(); pm.RecalculateBounds();
            string pp = Folder + "/Patch_" + name + ".asset"; AssetDatabase.DeleteAsset(pp); AssetDatabase.CreateAsset(pm, pp);
            var goName = "Patch_" + name + "_Fairway_-_Mesh"; var prevGo = opcd.transform.Find(goName); if (prevGo != null) Object.DestroyImmediate(prevGo.gameObject);
            var go = new GameObject(goName); go.transform.SetParent(opcd.transform, false); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(1f / opcd.transform.lossyScale.x, 1f / opcd.transform.lossyScale.y, 1f / opcd.transform.lossyScale.z);
            go.AddComponent<MeshFilter>().sharedMesh = pm;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = new[] { tpl.GetComponent<Renderer>().sharedMaterials[0] };
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = pm; var tc = tpl.GetComponent<Collider>(); if (tc != null) mc.sharedMaterial = tc.sharedMaterial;
            go.layer = tpl.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(tpl.gameObject));
            Debug.Log(string.Format("FairwayPatch {0}: moved {1} rough triangles from {2} into {3} ({4}, physics {5}), uv fit {6}", name, moved.Count / 3, rough.name, goName, mr.sharedMaterial.name, mc.sharedMaterial ? mc.sharedMaterial.name : "none", fit != null));
        }
        File.WriteAllText(LogPath, JsonUtility.ToJson(log, true));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public void Restore()
    {
        if (!File.Exists(LogPath)) return; var opcd = GameObject.Find("OPCD Meshes"); if (opcd == null) return;
        var log = JsonUtility.FromJson<Log>(File.ReadAllText(LogPath));
        foreach (var e in log.entries)
        {
            var mf = opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == e.rough);
            var prev = AssetDatabase.LoadAllAssetsAtPath(e.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == e.previousMesh);
            if (mf != null && prev != null) { mf.sharedMesh = prev; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = prev; }
            var p = opcd.transform.Find("Patch_" + e.patch + "_Fairway_-_Mesh"); if (p != null) Object.DestroyImmediate(p.gameObject);
            Debug.Log("FairwayPatch: restored " + e.rough + ", removed patch " + e.patch);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}

