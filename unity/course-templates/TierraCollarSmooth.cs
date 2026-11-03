using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Smooth collar edge (coordinator, Oct 30: the 17 collar's outer edge is a triangle sawtooth from flyover height). The collar and the rough
// ring outside it are rebuilt as ring strips along smooth offsets of the green outline: collar 0.02 -> 1.5 m (Tone17 hides the blend ring),
// rough ring 1.5 -> 2.6 m (overlaps the rough cut at ~2.0 m). Heights inside 0.6 m come from the blend ring. Heights, uv and vertex colour are sampled from the ORIGINAL collar mesh
// (temporary collider), so the surface does not move. Outline: the green mesh boundary, smoothed (3 passes) and resampled every 0.4 m.
public static class TierraCollarSmooth
{
    const string Dir = "Assets/TierraRejada/generated/green17";
    public static string Run18() { var col = Object.FindObjectsOfType<MeshFilter>().First(f => f.name == "SPatch_h18collar_-_Mesh"); return Run("Spline_green_4_Green_-_Mesh", "SPatch_h18collar_-_Mesh", "Spline_rough_18collarR_Rough_-_Mesh", col.sharedMesh.name.Replace("_smooth", "").Replace("_tone", "")); }
    public static string Tone18() { return Tone("Spline_green_4_Green_-_Mesh", "Spline_green_4_B_Rough_Green_-_Blend", "SPatch_h18collar_-_Mesh", "18"); }
    public static string Run17() { return Run("Spline_green_2_Green_-_Mesh_17B", "SPatch_collar1029_h17_-_Mesh_17B", "Spline_rough_17collarR_Rough_-_Mesh", "SPatch_collar1029_h17_-_Mesh_17B"); }
    static Mesh FindAsset(string name) { foreach (var gid in AssetDatabase.FindAssets("t:Mesh " + name)) { var ap = AssetDatabase.GUIDToAssetPath(gid); foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ap)) { var mm = o as Mesh; if (mm && mm.name == name) return mm; } } return null; }
    static List<Vector2> Outline(MeshFilter green)
    {
        var gm = green.sharedMesh; var gv = gm.vertices.Select(q => green.transform.TransformPoint(q)).ToArray(); var gt = gm.triangles; var weld = new int[gv.Length]; var map = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < gv.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(gv[i].x * 200), Mathf.RoundToInt(gv[i].z * 200)); int w; if (!map.TryGetValue(k, out w)) { w = i; map[k] = i; } weld[i] = w; }
        var cnt = new Dictionary<long, int>(); var dir = new Dictionary<long, int[]>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < gt.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[gt[i + e]], b = weld[gt[i + (e + 1) % 3]]; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; dir[k] = new[] { a, b }; }
        var next = new Dictionary<int, int>(); foreach (var kv in cnt) if (kv.Value == 1) next[dir[kv.Key][0]] = dir[kv.Key][1];
        var best = new List<int>(); var used = new HashSet<int>();
        foreach (var s in next.Keys.ToList()) { if (used.Contains(s)) continue; var L = new List<int>(); int cur = s; while (!used.Contains(cur) && next.ContainsKey(cur)) { used.Add(cur); L.Add(cur); cur = next[cur]; } if (L.Count > best.Count) best = L; }
        var p = best.Select(i => new Vector2(gv[i].x, gv[i].z)).ToList();
        for (int pass = 0; pass < 3; pass++) { var q = new List<Vector2>(); for (int i = 0; i < p.Count; i++) q.Add((p[(i - 1 + p.Count) % p.Count] + 2 * p[i] + p[(i + 1) % p.Count]) / 4); p = q; }
        // resample every 0.4 m
        float len = 0; for (int i = 0; i < p.Count; i++) len += (p[(i + 1) % p.Count] - p[i]).magnitude; int n = Mathf.Max(24, Mathf.RoundToInt(len / 0.4f)); var r = new List<Vector2>(); float step = len / n, acc = 0; int j = 0; float t = 0;
        for (int k = 0; k < n; k++) { float target = k * step; while (acc + (p[(j + 1) % p.Count] - p[j]).magnitude < target && j < p.Count * 2) { acc += (p[(j + 1) % p.Count] - p[j]).magnitude; j++; } var a = p[j % p.Count]; var b = p[(j + 1) % p.Count]; float sl = (b - a).magnitude; t = sl > 1e-6f ? (target - acc) / sl : 0; r.Add(Vector2.Lerp(a, b, t)); }
        float area = 0; for (int i = 0; i < r.Count; i++) { var q = r[(i + 1) % r.Count]; area += r[i].x * q.y - q.x * r[i].y; } if (area < 0) r.Reverse();   // counter-clockwise (x right, z up)
        return r;
    }
    static string Run(string greenName, string collarName, string roughRingName, string collarOrigName)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "green17");
        var all = Object.FindObjectsOfType<MeshFilter>(); var green = all.First(f => f.name == greenName); var collar = all.First(f => f.name == collarName); var ring = all.FirstOrDefault(f => f.name == roughRingName);
        if (!ring)
        {
            // no outer ring yet: create one in the rough material of the OPCD rough mesh under the green
            var gcen0 = green.GetComponent<Renderer>().bounds.center; var roughUnder = all.Where(f => f.name.StartsWith("Spline_rough") && !f.name.Contains("collarR") && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().bounds.Contains(new Vector3(gcen0.x, f.GetComponent<Renderer>().bounds.center.y, gcen0.z))).OrderBy(f => f.GetComponent<Renderer>().bounds.size.magnitude).FirstOrDefault() ?? all.First(f => f.name == "Spline_rough_0_4_Rough_-_Mesh");
            var go = new GameObject(roughRingName); go.transform.SetParent(roughUnder.transform.parent, false); ring = go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>().sharedMaterials = roughUnder.GetComponent<Renderer>().sharedMaterials.Take(1).ToArray(); var mc0 = go.AddComponent<MeshCollider>(); var rc0c = roughUnder.GetComponent<MeshCollider>(); if (rc0c) mc0.sharedMaterial = rc0c.sharedMaterial; go.layer = roughUnder.gameObject.layer;
        }
        var orig = FindAsset(collarOrigName); if (!orig) return "original collar mesh not found";
        var tmp = new GameObject("tmp_collar_orig"); tmp.transform.position = collar.transform.position; tmp.transform.rotation = collar.transform.rotation; tmp.transform.localScale = collar.transform.lossyScale; var tc = tmp.AddComponent<MeshCollider>(); tc.sharedMesh = orig; Physics.SyncTransforms();
        var blendMf = all.FirstOrDefault(f => f.name == greenName.Replace("Green_-_Mesh", "B_Rough_Green_-_Blend")); var tmpB = new GameObject("tmp_blend"); tmpB.transform.position = blendMf.transform.position; tmpB.transform.rotation = blendMf.transform.rotation; tmpB.transform.localScale = blendMf.transform.lossyScale; var tb = tmpB.AddComponent<MeshCollider>(); tb.sharedMesh = blendMf.sharedMesh; Physics.SyncTransforms();
        var ouv = orig.uv; var ocol = orig.colors; var otri = orig.triangles;
        var o = Outline(green); int N = o.Count; var nrm = new Vector2[N];
        for (int i = 0; i < N; i++) { var d = o[(i + 1) % N] - o[(i - 1 + N) % N]; nrm[i] = new Vector2(d.y, -d.x).normalized; }   // ccw -> right-hand normal points out
        var gb0 = green.GetComponent<Renderer>().bounds; gb0.Expand(10); var sands = Object.FindObjectsOfType<MeshCollider>().Where(sc => sc.gameObject.activeInHierarchy && sc.name.Contains("Bunker") && !sc.name.Contains("Blend") && sc.bounds.Intersects(gb0)).ToList();
        float lastY = green.GetComponent<Renderer>().bounds.center.y; int miss = 0; Color lastC = Color.white; Vector2 lastU = Vector2.zero; int dropped = 0;
        // rough tone for the outer ring: mean vertex colour of the rough mesh near the green
        var rough = all.First(f => f.name == "Spline_rough_0_4_Rough_-_Mesh"); var rv0 = rough.sharedMesh.vertices; var rc0 = rough.sharedMesh.colors; var gcen = green.GetComponent<Renderer>().bounds.center; Color rmean = Color.clear; int rn = 0; for (int i = 0; i < rv0.Length && rc0.Length == rv0.Length; i++) { var w = rough.transform.TransformPoint(rv0[i]); if (new Vector2(w.x - gcen.x, w.z - gcen.z).magnitude < 40) { rmean += rc0[i]; rn++; } } if (rn > 0) rmean /= rn; else rmean = Color.white;
        System.Func<float[], bool, Mesh> Build = (offs, sampleUv) =>
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var cl = new List<Color>(); var t = new List<int>(); int R = offs.Length;
            for (int i = 0; i < N; i++) for (int k = 0; k < R; k++)
                {
                    var p = o[i] + nrm[i] * offs[k]; RaycastHit h; float y; Vector2 u = Vector2.zero; Color c = Color.white;
                    if (tc.Raycast(new Ray(new Vector3(p.x, lastY + 30, p.y), Vector3.down), out h, 80)) { y = h.point.y; lastY = y; if (ouv.Length > 0) u = h.textureCoord; if (ocol.Length > 0) { var b = h.barycentricCoordinate; int ti = h.triangleIndex * 3; c = ocol[otri[ti]] * b.x + ocol[otri[ti + 1]] * b.y + ocol[otri[ti + 2]] * b.z; } lastC = c; lastU = u; }
                    else { y = lastY; miss++; c = lastC; u = lastU; RaycastHit hb; if (tb.Raycast(new Ray(new Vector3(p.x, lastY + 30, p.y), Vector3.down), out hb, 80)) { y = hb.point.y; goto done; } RaycastHit h2; foreach (var hh in Physics.RaycastAll(new Vector3(p.x, lastY + 30, p.y), Vector3.down, 80).OrderByDescending(q => q.point.y)) { if (hh.collider.transform.root.name == "OPCD Meshes" && !hh.collider.name.Contains("collar") && !hh.collider.name.Contains("17collarR")) { y = hh.point.y; break; } } done:; }
                    v.Add(new Vector3(p.x, y + 0.004f, p.y)); uv.Add(u); cl.Add(sampleUv ? c : rmean);
                }
            for (int i = 0; i < N; i++) { int a = i * R, b = ((i + 1) % N) * R; for (int k = 0; k + 1 < R; k++) { var cen = (v[a + k] + v[b + k] + v[b + k + 1] + v[a + k + 1]) / 4; RaycastHit hc; if ((!tc.Raycast(new Ray(cen + Vector3.up * 30, Vector3.down), out hc, 80) && !tb.Raycast(new Ray(cen + Vector3.up * 30, Vector3.down), out hc, 80)) || new[] { v[a + k], v[b + k], v[b + k + 1], v[a + k + 1] }.All(q => sands.Any(sc => { RaycastHit hs; return sc.Raycast(new Ray(q + Vector3.up * 30, Vector3.down), out hs, 80); }))) { dropped++; continue; } t.AddRange(new[] { a + k, b + k, b + k + 1, a + k, b + k + 1, a + k + 1 }); } }
            var m = new Mesh { name = "ring" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(cl); m.SetTriangles(t, 0); m.RecalculateNormals(); if (m.normals.Average(q => q.y) < 0) { var tt = m.triangles; for (int q = 0; q < tt.Length; q += 3) { int s = tt[q + 1]; tt[q + 1] = tt[q + 2]; tt[q + 2] = s; } m.triangles = tt; m.RecalculateNormals(); } m.RecalculateTangents(); m.RecalculateBounds(); return m;
        };
        var cm = Build(new[] { 0.02f, 0.3f, 0.6f, 0.9f, 1.25f, 1.5f }, true); var rm = Build(new[] { 1.5f, 2.0f, 2.6f }, false);
        Object.DestroyImmediate(tmp); Object.DestroyImmediate(tmpB);
        // collar mesh is in the collar object's local space
        var lv = cm.vertices.Select(q => collar.transform.InverseTransformPoint(q)).ToArray(); cm.vertices = lv; cm.RecalculateNormals(); cm.RecalculateBounds(); cm.name = collarName + "_smooth";
        var p1 = Dir + "/collar_smooth_" + greenName + ".asset"; AssetDatabase.DeleteAsset(p1); AssetDatabase.CreateAsset(cm, p1); collar.sharedMesh = cm; var cc = collar.GetComponent<MeshCollider>(); if (cc) { cc.sharedMesh = null; cc.sharedMesh = cm; }
        if (ring) { var rl = rm.vertices.Select(q => ring.transform.InverseTransformPoint(q)).ToArray(); rm.vertices = rl; rm.RecalculateBounds(); rm.name = roughRingName + "_smooth"; var p2 = Dir + "/roughring_smooth_" + greenName + ".asset"; AssetDatabase.DeleteAsset(p2); AssetDatabase.CreateAsset(rm, p2); ring.sharedMesh = rm; var rc = ring.GetComponent<MeshCollider>(); if (rc) { rc.sharedMesh = null; rc.sharedMesh = rm; } }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(collar.gameObject.scene);
        return string.Format("{0}: outline {1} pts, collar 0.02-1.5 m and rough ring 1.5-2.6 m rebuilt as smooth rings ({2} height samples off the old collar, {3} quads dropped outside the old collar footprint)", greenName, N, miss, dropped);
    }
    // One collar tone (coordinator): the collar runs from the green edge (0.02 m) and the 0-0.6 m blend ring is no longer drawn; the collar takes a copy of the green
    // material at 0.88, so the green reads as green + one slightly darker collar band, no pale strip or dark line between.
    public static string Tone17() { return Tone("Spline_green_2_Green_-_Mesh_17B", "Spline_green_2_B_Rough_Green_-_Blend_17B", "SPatch_collar1029_h17_-_Mesh_17B", "17"); }
    static string Tone(string gName, string bName, string cName, string tag)
    {
        var all = Object.FindObjectsOfType<MeshFilter>(); var green = all.First(f => f.name == gName); var blend = all.First(f => f.name == bName); var collar = all.First(f => f.name == cName);
        blend.GetComponent<Renderer>().enabled = false; foreach (Transform sk in blend.transform) { var r0 = sk.GetComponent<Renderer>(); if (r0) r0.enabled = false; }   /* the collar now starts at the green edge; the blend ring keeps its collider (physics) but is not drawn */
        var gmat = green.GetComponent<Renderer>().sharedMaterial; var cmat = new Material(gmat) { name = "collar" + tag + "_green_088" }; foreach (var prop in new[] { "_RedOverallColor", "_GreenOverallColor", "_BlueOverallColor", "_BlackOverallColor" }) if (cmat.HasProperty(prop)) { var oc = cmat.GetColor(prop); cmat.SetColor(prop, new Color(oc.r * 0.84f, oc.g * 0.88f, oc.b * 0.82f, oc.a)); }   /* OPCD 4-colour shader: per-channel overall colour */
        var mp = Dir + "/collar" + tag + "_green_088.mat"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(cmat, mp); var cr = collar.GetComponent<Renderer>(); var ms = cr.sharedMaterials; ms[0] = cmat; cr.sharedMaterials = ms;
        // collar uvs follow the green's mapping (sample the green mesh uv at the nearest green vertex is not needed: the green material is world-scaled? copy uv from nearest green vertex)
        var gv = green.sharedMesh.vertices.Select(q => green.transform.TransformPoint(q)).ToArray(); var guv = green.sharedMesh.uv; var cm = Object.Instantiate(collar.sharedMesh); var cv = cm.vertices; var cuv = new Vector2[cv.Length];
        if (guv.Length == gv.Length && gv.Length > 3)
        {
            // fit uv = A x + B z + C over the green
            double sxx = 0, sxz = 0, szz = 0, sx = 0, sz = 0, n = gv.Length, ux = 0, uz = 0, u1 = 0, vx = 0, vz = 0, v1 = 0; foreach (var i in Enumerable.Range(0, gv.Length)) { double x = gv[i].x, z = gv[i].z; sxx += x * x; sxz += x * z; szz += z * z; sx += x; sz += z; ux += guv[i].x * x; uz += guv[i].x * z; u1 += guv[i].x; vx += guv[i].y * x; vz += guv[i].y * z; v1 += guv[i].y; }
            System.Func<double, double, double, double[]> Solve = (bx, bz, b1) => { var M = new double[,] { { sxx, sxz, sx }, { sxz, szz, sz }, { sx, sz, n } }; var B = new[] { bx, bz, b1 }; for (int k = 0; k < 3; k++) { for (int r = 0; r < 3; r++) { if (r == k) continue; var f = M[r, k] / M[k, k]; for (int q = 0; q < 3; q++) M[r, q] -= f * M[k, q]; B[r] -= f * B[k]; } } return new[] { B[0] / M[0, 0], B[1] / M[1, 1], B[2] / M[2, 2] }; };
            var fu = Solve(ux, uz, u1); var fv = Solve(vx, vz, v1);
            for (int i = 0; i < cv.Length; i++) { var w = collar.transform.TransformPoint(cv[i]); cuv[i] = new Vector2((float)(fu[0] * w.x + fu[1] * w.z + fu[2]), (float)(fv[0] * w.x + fv[1] * w.z + fv[2])); }
            cm.uv = cuv;
        }
        var cc = cm.colors; var gc = green.sharedMesh.colors; if (gc.Length > 0) { var mean = gc.Aggregate((a, b) => a + b) / gc.Length; cm.colors = Enumerable.Repeat(mean, cv.Length).ToArray(); }
        cm.name = collar.sharedMesh.name + "_tone"; var cp = Dir + "/collar" + tag + "_tone.asset"; AssetDatabase.DeleteAsset(cp); AssetDatabase.CreateAsset(cm, cp); collar.sharedMesh = cm; var mc = collar.GetComponent<MeshCollider>(); if (mc) { mc.sharedMesh = null; mc.sharedMesh = cm; }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(collar.gameObject.scene); return "blend ring hidden, collar on green material x0.88 (uv fitted from the green)";
    }
}





