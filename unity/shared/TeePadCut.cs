using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// A tee pad cut into a bank (hole 5 Black, Oct 28). hpad.txt "name;cx;cz;len;wid;dirx;dirz;batter;templateTee;clearShrubs"
// Course meshes around it are re-graded (level under the pad, smoothstep batter back to the original ground, fading out within
// 3 m of cart paths), the terrain is pushed under them, a flat tee mesh sits on top, shrubs within clearShrubs m are removed.
// Edited meshes go to Assets/TierraRejada/generated/h5pad (log.txt keeps the originals).
public static class TeePadCut
{
    const string F = "Assets/TierraRejada/generated/h5pad";
    public static string Run()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var a = File.ReadAllText("C:/GSP/GSPBaseProject_v4_2025_12_02/AutomationOutput/p1/hpad.txt").Trim().Split(';');
        string name = a[0]; var c = new Vector2(float.Parse(a[1], ci), float.Parse(a[2], ci)); float L = float.Parse(a[3], ci), Wd = float.Parse(a[4], ci), bat = float.Parse(a[7], ci), clr = float.Parse(a[9], ci);
        var ax = new Vector2(float.Parse(a[5], ci), float.Parse(a[6], ci)).normalized; var ay = new Vector2(-ax.y, ax.x);
        var opcd = GameObject.Find("OPCD Meshes"); Directory.CreateDirectory(F); var sb = new StringBuilder();
        var old = opcd.transform.Find(name); if (old) Undo.DestroyObjectImmediate(old.gameObject);
        System.Func<Vector2, float> dOut = p => { var q = p - c; float u = Mathf.Abs(Vector2.Dot(q, ax)) - L / 2, v = Mathf.Abs(Vector2.Dot(q, ay)) - Wd / 2; return new Vector2(Mathf.Max(u, 0), Mathf.Max(v, 0)).magnitude; };
        var pathPts = GameObject.Find("CART_PATHS").GetComponentsInChildren<MeshFilter>().SelectMany(m => m.sharedMesh.vertices.Select(p => m.transform.TransformPoint(p))).Select(p => new Vector2(p.x, p.z)).Where(p => (p - c).magnitude < L + bat + 10).ToList();
        System.Func<Vector2, float> dPath = p => pathPts.Count == 0 ? 99 : pathPts.Min(q => (q - p).magnitude);
        System.Func<Vector2, float> ground = p => { float y = float.NegativeInfinity; foreach (var h in Physics.RaycastAll(new Vector3(p.x, 1000, p.y), Vector3.down, 2000)) { var r = h.collider.transform.root.name; if (r != "OPCD Meshes" && r != "Terrain (Inner)" && r != "DEEP_ROUGH") continue; if (h.collider.name.StartsWith("SPatch_")) continue; y = Mathf.Max(y, h.point.y); } return y; };
        // pad level: median ground under the pad
        var gs = new List<float>(); for (float u = -L / 2; u <= L / 2 + 0.01f; u += 0.5f) for (float v = -Wd / 2; v <= Wd / 2 + 0.01f; v += 0.5f) gs.Add(ground(c + ax * u + ay * v)); gs.Sort();
        float padY = gs[gs.Count / 2]; sb.AppendLine("ground under pad " + gs[0].ToString("F2") + ".." + gs[gs.Count - 1].ToString("F2") + ", level " + padY.ToString("F2"));
        System.Func<Vector2, float, float> target = (p, y0) => { float t = Mathf.Clamp01(dOut(p) / bat); t = t * t * (3 - 2 * t); float w = Mathf.Clamp01((dPath(p) - 1.5f) / 1.5f); return Mathf.Lerp(y0, Mathf.Lerp(padY - 0.03f, y0, t), w); };
        // re-grade the course meshes around the pad (paths, tees, greens and patches stay)
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => !m.name.Contains("_Tee_") && !m.name.Contains("_Green_") && !m.name.StartsWith("SPatch_")))
        {
            var b = mf.GetComponent<Renderer>().bounds; float R = L + bat + 2; if (b.max.x < c.x - R || b.min.x > c.x + R || b.max.z < c.y - R || b.min.z > c.y + R) continue;
            var src = mf.sharedMesh; var v = src.vertices; var tr = mf.transform; int moved = 0; float maxd = 0;
            for (int i = 0; i < v.Length; i++) { var w = tr.TransformPoint(v[i]); var p = new Vector2(w.x, w.z); if (dOut(p) > bat) continue; float ny = target(p, w.y); if (Mathf.Abs(ny - w.y) < 0.005f) continue; maxd = Mathf.Max(maxd, Mathf.Abs(ny - w.y)); w.y = ny; v[i] = tr.InverseTransformPoint(w); moved++; }
            if (moved == 0) continue;
            var m2 = Object.Instantiate(src); m2.name = src.name; m2.vertices = v; m2.RecalculateNormals(); m2.RecalculateBounds();
            string path = F + "/" + mf.name + ".asset"; if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m2, path);
            File.AppendAllText(F + "/log.txt", mf.name + " was " + AssetDatabase.GetAssetPath(src) + " | " + src.name + "\n");
            Undo.RecordObject(mf, "pad"); mf.sharedMesh = m2; var mc = mf.GetComponent<MeshCollider>(); if (mc) { Undo.RecordObject(mc, "pad"); mc.sharedMesh = null; mc.sharedMesh = m2; }
            sb.AppendLine(mf.name + ": " + moved + " vertices moved, max " + maxd.ToString("F2") + " m");
        }
        // terrain under the re-graded surface
        var terr = Object.FindObjectsOfType<Terrain>().First(t => t.name == "Terrain (Inner)"); var td = terr.terrainData; int res = td.heightmapResolution; float cell = td.size.x / (res - 1); var tp = terr.GetPosition();
        int x0 = Mathf.FloorToInt((c.x - L - bat - 2) / cell), x1 = Mathf.CeilToInt((c.x + L + bat + 2) / cell), z0 = Mathf.FloorToInt((c.y - L - bat - 2) / cell), z1 = Mathf.CeilToInt((c.y + L + bat + 2) / cell);
        var hh = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1); int low = 0;
        for (int j = 0; j <= z1 - z0; j++) for (int i = 0; i <= x1 - x0; i++)
            {
                var p = new Vector2(tp.x + (x0 + i) * cell, tp.z + (z0 + j) * cell); if (dOut(p) > bat + 0.5f) continue;
                float top = float.PositiveInfinity; foreach (var h in Physics.RaycastAll(new Vector3(p.x, 1000, p.y), Vector3.down, 2000)) if (h.collider.transform.root.name == "OPCD Meshes") top = Mathf.Min(top, h.point.y);
                if (float.IsPositiveInfinity(top)) continue; float tgt = (top - 0.2f - tp.y) / td.size.y; if (hh[j, i] > tgt) { hh[j, i] = tgt; low++; }
            }
        Undo.RegisterCompleteObjectUndo(td, "pad"); td.SetHeights(x0, z0, hh); terr.Flush(); sb.AppendLine("terrain lowered at " + low + " points");
        // the tee pad
        var tpl = opcd.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == a[8]);
        var vs = new List<Vector3>(); var tris = new List<int>(); int nu = Mathf.CeilToInt(L / 0.25f), nv = Mathf.CeilToInt(Wd / 0.25f);
        for (int i = 0; i <= nu; i++) for (int j = 0; j <= nv; j++) { var p = c + ax * (-L / 2 + L * i / nu) + ay * (-Wd / 2 + Wd * j / nv); vs.Add(new Vector3(p.x, padY + 0.03f, p.y)); }
        for (int i = 0; i < nu; i++) for (int j = 0; j < nv; j++) { int q = i * (nv + 1) + j; tris.AddRange(new[] { q, q + 1, q + nv + 2, q, q + nv + 2, q + nv + 1 }); }
        var pm = new Mesh { name = name }; pm.SetVertices(vs); pm.SetTriangles(tris, 0); pm.RecalculateNormals();
        if (pm.normals[0].y < 0) { var tt = pm.triangles; for (int k = 0; k < tt.Length; k += 3) { var s = tt[k]; tt[k] = tt[k + 1]; tt[k + 1] = s; } pm.triangles = tt; pm.RecalculateNormals(); }
        var tc = tpl.sharedMesh.colors; if (tc.Length > 0) pm.colors = vs.Select(x => tc[0]).ToArray(); pm.RecalculateBounds();
        string pp = F + "/" + name + ".asset"; if (AssetDatabase.LoadAssetAtPath<Mesh>(pp) != null) AssetDatabase.DeleteAsset(pp); AssetDatabase.CreateAsset(pm, pp);
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "pad"); go.transform.SetParent(opcd.transform, true); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = pm; go.AddComponent<MeshRenderer>().sharedMaterials = tpl.GetComponent<Renderer>().sharedMaterials;
        var pc = go.AddComponent<MeshCollider>(); pc.sharedMesh = pm; pc.sharedMaterial = tpl.GetComponent<Collider>().sharedMaterial; go.layer = tpl.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(tpl.gameObject));
        sb.AppendLine("pad " + name + " at y " + (padY + 0.03f).ToString("F3") + ", phys " + pc.sharedMaterial.name);
        // shrubs within clr m of the pad: capsule plus its triangles in the combined tile mesh
        var shr = GameObject.Find("SHRUBS"); var rem = shr.transform.Find("colliders").GetComponentsInChildren<CapsuleCollider>(true).Where(k => dOut(new Vector2(k.bounds.center.x, k.bounds.center.z)) - k.bounds.extents.x < clr).ToList();
        foreach (var g in rem.GroupBy(k => k.transform.parent.name))
        {
            var tile = shr.transform.Find(g.Key); if (!tile) continue; var caps = g.Select(k => k.bounds).ToList();
            foreach (var mf in tile.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; var tr = mf.transform.localToWorldMatrix; var v = m.vertices; var t = m.triangles; var keep = new List<int>(); int drop = 0;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var ce = (tr.MultiplyPoint3x4(v[t[i]]) + tr.MultiplyPoint3x4(v[t[i + 1]]) + tr.MultiplyPoint3x4(v[t[i + 2]])) / 3;
                    bool kill = caps.Any(bb => { float rr = bb.extents.x * 1.3f + 0.3f; return new Vector2(ce.x - bb.center.x, ce.z - bb.center.z).sqrMagnitude < rr * rr && ce.y > bb.min.y - 1 && ce.y < bb.max.y + 1; });
                    if (kill) drop++; else keep.AddRange(new[] { t[i], t[i + 1], t[i + 2] });
                }
                if (drop == 0) continue; var m2 = Object.Instantiate(m); m2.name = m.name; m2.triangles = keep.ToArray(); m2.RecalculateBounds();
                Directory.CreateDirectory("Assets/TierraRejada/generated/shrub_edits"); string path = "Assets/TierraRejada/generated/shrub_edits/" + mf.name + "_h5pad.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m2, path);
                File.AppendAllText("Assets/TierraRejada/generated/shrub_edits/log.txt", mf.name + " was " + AssetDatabase.GetAssetPath(m) + " | " + m.name + " -> " + path + "\n"); Undo.RecordObject(mf, "pad"); mf.sharedMesh = m2;
            }
        }
        foreach (var k in rem) Undo.DestroyObjectImmediate(k.gameObject);
        var trees = GameObject.Find("LIDAR_TREES").GetComponentsInChildren<LODGroup>(true).Where(l => dOut(new Vector2(l.transform.position.x, l.transform.position.z)) < clr).Select(l => l.name + " " + l.transform.position.ToString("F0")).ToList();
        sb.AppendLine("shrubs removed " + rem.Count + "; LiDAR trees within " + clr + " m (left): " + string.Join(", ", trees.ToArray()));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return sb.ToString();
    }

    // scene layers in use (read only)
    public static string Layers()
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && g.scene == EditorSceneManager.GetActiveScene()).ToList();
        var sb = new StringBuilder();
        foreach (var g in all.GroupBy(o => o.layer).OrderBy(x => x.Key)) sb.AppendLine(g.Key + " '" + LayerMask.LayerToName(g.Key) + "': " + g.Count() + " objects, e.g. " + string.Join(", ", g.Select(o => o.transform.root.name).Distinct().Take(4).ToArray()));
        var cams = all.Select(o => o.GetComponent<Camera>()).Where(c => c).Select(c => c.name + " cullingMask has 31: " + ((c.cullingMask & (1 << 31)) != 0)).ToArray();
        sb.AppendLine("layer 31 name '" + LayerMask.LayerToName(31) + "', objects on it: " + all.Count(o => o.layer == 31) + "; cameras: " + string.Join("; ", cams));
        return sb.ToString();
    }
}
