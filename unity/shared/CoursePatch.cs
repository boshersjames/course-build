using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Smooth-edged surface patches draped on the course (Oct 28; replaces FairwayPatch's cut-out rough triangles, whose edges
// were sawtooth and whose rough vertex colours picked the wrong fairway texture channel).
// AutomationOutput/course_patches.txt, one patch per line, fields separated by ';':
//   poly;<name>;<template mesh>;x,z x,z ...                       polygon (corner-cut twice so the outline is round)
//   collar;<name>;<template mesh>;<hole>;side;back;front             ring around that hole's green: widths in metres, back =
//                                                                    away from the approach (green -> Blue tee reversed)
//   deep2rough;<name>;<hole>;radius;backRadius                       course meshes with GSPdeeprough physics: triangles within
//                                                                    radius of the green (backRadius behind it) get a separate
//                                                                    GSPrough collider, the look is unchanged; DEEP_ROUGH patch
//                                                                    triangles there are removed (rough shows through)
// Patches are built on a 0.4 m grid from a signed-distance field (vertices near the edge are snapped onto it), 3 cm above
// the top course surface, with the template's materials, physics, layer, static flags, vertex colour and fitted UVs.
// Greens come from the GKD (GreenCenterPoint, Blue tee); patches stay 0.15 m off the green and 0.4 m off bunkers.
// Meshes go to Assets/TierraRejada/generated/course_patches; log.txt records every replaced mesh for Restore.
public static class CoursePatch
{
    const string Folder = "Assets/TierraRejada/generated/course_patches", Log = Folder + "/log.txt";
    const string Gkd = "C:/GSProV1/Core/GSP/Courses/TierraRejada/TierraRejada.GKD";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static string Cfg { get { return Path.GetDirectoryName(Application.dataPath) + "/AutomationOutput/course_patches.txt"; } }

    // ---------- geometry helpers ----------
    class Shape { public List<Vector2[]> segs = new List<Vector2[]>(); public Bounds2 b; }
    struct Bounds2 { public Vector2 min, max; }
    static Shape FromMesh(MeshFilter mf)
    {
        var v = mf.sharedMesh.vertices.Select(p => mf.transform.TransformPoint(p)).ToArray(); var t = mf.sharedMesh.triangles;
        var key = new Dictionary<Vector2Int, int>(); var id = new int[v.Length]; var pos = new List<Vector2>();
        for (int i = 0; i < v.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(v[i].x * 200), Mathf.RoundToInt(v[i].z * 200)); int j; if (!key.TryGetValue(k, out j)) { j = pos.Count; key[k] = j; pos.Add(new Vector2(v[i].x, v[i].z)); } id[i] = j; }
        var cnt = new Dictionary<long, int>();
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = id[t[i + e]], b = id[t[i + (e + 1) % 3]]; if (a == b) continue; long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; int c; cnt.TryGetValue(k, out c); cnt[k] = c + 1; }
        var s = new Shape(); foreach (var kv in cnt.Where(k => k.Value == 1)) s.segs.Add(new[] { pos[(int)(kv.Key >> 32)], pos[(int)(kv.Key & 0xffffffff)] });
        s.b = new Bounds2 { min = new Vector2(pos.Min(p => p.x), pos.Min(p => p.y)), max = new Vector2(pos.Max(p => p.x), pos.Max(p => p.y)) }; return s;
    }
    static Shape FromPoly(List<Vector2> poly)
    {
        for (int it = 0; it < 2; it++) { var n = new List<Vector2>(); for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; n.Add(Vector2.Lerp(a, b, .25f)); n.Add(Vector2.Lerp(a, b, .75f)); } poly = n; }
        var s = new Shape(); for (int i = 0; i < poly.Count; i++) s.segs.Add(new[] { poly[i], poly[(i + 1) % poly.Count] });
        s.b = new Bounds2 { min = new Vector2(poly.Min(p => p.x), poly.Min(p => p.y)), max = new Vector2(poly.Max(p => p.x), poly.Max(p => p.y)) }; return s;
    }
    // signed distance: negative inside
    static float SD(Shape s, Vector2 p)
    {
        float d = float.MaxValue; bool inside = false;
        foreach (var sg in s.segs)
        {
            var ab = sg[1] - sg[0]; float tt = Mathf.Clamp01(Vector2.Dot(p - sg[0], ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f)); d = Mathf.Min(d, (sg[0] + ab * tt - p).sqrMagnitude);
            if ((sg[0].y > p.y) != (sg[1].y > p.y) && p.x < (sg[1].x - sg[0].x) * (p.y - sg[0].y) / (sg[1].y - sg[0].y) + sg[0].x) inside = !inside;
        }
        d = Mathf.Sqrt(d); return inside ? -d : d;
    }

    // ---------- GKD greens ----------
    static Dictionary<int, Vector2[]> Greens()   // hole -> { green centre, Blue tee (or first enabled tee) }
    {
        var r = new Dictionary<int, Vector2[]>(); var txt = File.ReadAllText(Gkd);
        int i = txt.IndexOf("\"Holes\""); var parts = txt.Substring(i).Split(new[] { "\"HoleNumber\":" }, StringSplitOptions.None);
        foreach (var p in parts.Skip(1))
        {
            int hole = int.Parse(p.Substring(0, p.IndexOfAny(new[] { ',', '}' })).Trim(), CI);
            Func<string, Vector2?> pos = type => { int k = p.IndexOf("\"TeeType\":\"" + type + "\""); if (k < 0) return null; int q = p.IndexOf("\"Position\":", k); var s = p.Substring(q + 11); if (s.StartsWith("null")) return null;
                Func<string, float> f = n => { int a = s.IndexOf("\"" + n + "\":") + n.Length + 3; int b = s.IndexOfAny(new[] { ',', '}' }, a); return float.Parse(s.Substring(a, b - a), CI); }; return new Vector2(f("x"), f("z")); };
            var g = pos("GreenCenterPoint"); var tee = pos("Blue") ?? pos("White") ?? pos("Black"); if (g != null && tee != null) r[hole] = new[] { g.Value, tee.Value };
        }
        return r;
    }
    static MeshFilter GreenMesh(GameObject opcd, Vector2 c)
    {
        return opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.EndsWith("_Green_-_Mesh")).OrderBy(m => { var s = FromMesh(m); return Mathf.Max(0, SD(s, c)); }).ThenBy(m => (new Vector2(m.GetComponent<Renderer>().bounds.center.x, m.GetComponent<Renderer>().bounds.center.z) - c).sqrMagnitude).First();
    }

    // ---------- build ----------
    [MenuItem("Tierra/Course Patches/Apply")]
    // Oct 29 green edge pass: same as Apply, from AutomationOutput/course_patches_collars1029.txt (collar lines may add an 8th field: lift in metres)
    public static string ApplyCollars1029() { return ApplyFile(Path.GetDirectoryName(Application.dataPath) + "/AutomationOutput/course_patches_collars1029.txt"); }
    static float lift = 0.03f;

    public static string Apply() { return ApplyFile(Cfg); }

    static string ApplyFile(string cfg)
    {
        var opcd = GameObject.Find("OPCD Meshes"); Directory.CreateDirectory(Folder); var greens = Greens(); var report = new System.Text.StringBuilder();
        var bunkers = opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.EndsWith("_Bunker_-_Mesh")).Select(FromMesh).ToList();
        foreach (var line in File.ReadAllLines(cfg).Select(l => l.Trim()).Where(l => l != "" && !l.StartsWith("#")))
        {
            var a = line.Split(';'); lift = a[0] == "collar" && a.Length > 7 ? float.Parse(a[7], CI) : 0.03f;
            try { report.AppendLine(One(opcd, greens, bunkers, a)); } catch (Exception e) { report.AppendLine(a[1] + ": FAILED " + e.Message + " " + e.StackTrace.Split('\n').FirstOrDefault()); }
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return report.ToString();
    }

    static string One(GameObject opcd, Dictionary<int, Vector2[]> greens, List<Shape> bunkers, string[] a)
    {
        {
            if (a[0] == "deep2rough") return DeepToRough(opcd, greens, a);
            MeshFilter tpl = null; string pname = "SPatch_" + a[1] + "_-_Mesh";   // keep "_-_Mesh": GSPro's SpeedControl looks at mesh/blend names
            Func<Vector2, float> f; Bounds2 bb; string name = pname;
            if (a[0] == "poly")
            {
                var poly = a[3].Split(' ').Where(s => s != "").Select(s => s.Split(',')).Select(x => new Vector2(float.Parse(x[0], CI), float.Parse(x[1], CI))).ToList();
                var sh = FromPoly(poly); bb = sh.b; var near = bunkers.Where(b => b.b.max.x > bb.min.x - 5 && b.b.min.x < bb.max.x + 5 && b.b.max.y > bb.min.y - 5 && b.b.min.y < bb.max.y + 5).ToList();
                f = p => { float v = -SD(sh, p); foreach (var b in near) v = Mathf.Min(v, SD(b, p) - 0.4f); return v; };
            }
            else
            {
                int hole = int.Parse(a[3], CI); float side = float.Parse(a[4], CI), back = float.Parse(a[5], CI), front = float.Parse(a[6], CI);
                var g = greens[hole]; var gm = FromMesh(GreenMesh(opcd, g[0])); var toBack = (g[0] - g[1]).normalized; var gc = g[0];
                float wmax = Mathf.Max(side, Mathf.Max(back, front)); bb = new Bounds2 { min = gm.b.min - Vector2.one * (wmax + 1), max = gm.b.max + Vector2.one * (wmax + 1) };
                var near = bunkers.Where(b => b.b.max.x > bb.min.x && b.b.min.x < bb.max.x && b.b.max.y > bb.min.y && b.b.min.y < bb.max.y).ToList();
                f = p => { float c = Vector2.Dot((p - gc).normalized, toBack); float w = side + (back - side) * Mathf.Max(0, c) + (front - side) * Mathf.Max(0, -c); float dg = SD(gm, p); float v = Mathf.Min(dg - 0.15f, w - dg); foreach (var b in near) v = Mathf.Min(v, SD(b, p) - 0.4f); return v; };
            }
            var mid = (bb.min + bb.max) / 2;   // template "auto": the fairway mesh with a vertex nearest the patch
            tpl = a[2] != "auto" ? opcd.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == a[2])
                : opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.EndsWith("_Fairway_-_Mesh") && !m.name.StartsWith("Patch_")).OrderBy(m => m.sharedMesh.vertices.Min(p => { var q = m.transform.TransformPoint(p); return (new Vector2(q.x, q.z) - mid).sqrMagnitude; })).First();
            return Build(opcd, tpl, name, f, bb);
        }
    }

    static string Build(GameObject opcd, MeshFilter tpl, string name, Func<Vector2, float> f, Bounds2 bb)
    {
        const float step = 0.4f; int nx = Mathf.CeilToInt((bb.max.x - bb.min.x) / step) + 1, nz = Mathf.CeilToInt((bb.max.y - bb.min.y) / step) + 1;
        var P = new Vector2[nx, nz]; var F = new float[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            {
                var p = bb.min + new Vector2(i, j) * step; float v = f(p);
                for (int it = 0; it < 3 && v < 0 && v > -1.5f * step; it++)   // snap onto the edge along the field's gradient
                {
                    const float e = 0.05f; var gr = new Vector2(f(p + new Vector2(e, 0)) - f(p - new Vector2(e, 0)), f(p + new Vector2(0, e)) - f(p - new Vector2(0, e))) / (2 * e);
                    if (gr.sqrMagnitude < 1e-4f) break; p -= gr * (v / gr.sqrMagnitude); v = f(p);
                }
                P[i, j] = p; F[i, j] = v;
            }
        var old = opcd.transform.Find(name); if (old) Undo.DestroyObjectImmediate(old.gameObject);
        Func<Vector2, float> top = p =>
        {
            float y = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 1000, p.y), Vector3.down, 2000))
            { var r = h.collider.transform.root.name; if ((r != "OPCD Meshes" && r != "DEEP_ROUGH" && !r.StartsWith("Patch_")) || h.collider.name.StartsWith("SPatch_") || h.collider.name.Contains("Water")) continue; y = Mathf.Max(y, h.point.y); }
            return y;
        };
        var verts = new List<Vector3>(); var idx = new Dictionary<int, int>(); var tris = new List<int>();
        Func<int, int, int> V = (i, j) => { int k = i * nz + j, r; if (idx.TryGetValue(k, out r)) return r; float y = top(P[i, j]); if (float.IsNegativeInfinity(y)) return -1; idx[k] = verts.Count; verts.Add(new Vector3(P[i, j].x, y + lift, P[i, j].y)); return verts.Count - 1; };
        Action<int, int, int, int, int, int> T = (i0, j0, i1, j1, i2, j2) =>
        {
            if (F[i0, j0] < -0.01f || F[i1, j1] < -0.01f || F[i2, j2] < -0.01f) return;
            var c = (P[i0, j0] + P[i1, j1] + P[i2, j2]) / 3; if (f(c) < -0.05f) return;
            if (Mathf.Abs((P[i1, j1].x - P[i0, j0].x) * (P[i2, j2].y - P[i0, j0].y) - (P[i2, j2].x - P[i0, j0].x) * (P[i1, j1].y - P[i0, j0].y)) < 1e-4f) return;
            int a = V(i0, j0), b = V(i1, j1), d = V(i2, j2); if (a < 0 || b < 0 || d < 0) return; tris.AddRange(new[] { a, d, b });
        };
        for (int i = 0; i + 1 < nx; i++) for (int j = 0; j + 1 < nz; j++) { T(i, j, i + 1, j, i + 1, j + 1); T(i, j, i + 1, j + 1, i, j + 1); }
        if (tris.Count == 0) return name + ": empty";
        // UVs fitted to the template near the patch (uv = a x + b z + c), vertex colour of the nearest template vertex
        var tv = tpl.sharedMesh.vertices.Select(p => tpl.transform.TransformPoint(p)).ToArray(); var tuv = tpl.sharedMesh.uv; var tcol = tpl.sharedMesh.colors;
        var cen = new Vector2(verts.Average(p => p.x), verts.Average(p => p.z)); var near = Enumerable.Range(0, tv.Length).OrderBy(k => (new Vector2(tv[k].x, tv[k].z) - cen).sqrMagnitude).Take(400).ToList();
        double[,] A = new double[3, 3]; double[] bu = new double[3], bv = new double[3];
        bool hasUV = tuv != null && tuv.Length == tv.Length;   // OPCD course meshes have none: the shader maps in world space
        if (hasUV) foreach (var k in near) { double[] r = { tv[k].x - cen.x, tv[k].z - cen.y, 1 }; for (int x = 0; x < 3; x++) { for (int y = 0; y < 3; y++) A[x, y] += r[x] * r[y]; bu[x] += r[x] * tuv[k].x; bv[x] += r[x] * tuv[k].y; } }
        var cu = hasUV ? Solve(A, bu) : null; var cv = hasUV ? Solve(A, bv) : null;
        var m = new Mesh { name = name }; if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts); m.SetTriangles(tris, 0);
        if (hasUV) m.uv = verts.Select(p => new Vector2((float)(cu[0] * (p.x - cen.x) + cu[1] * (p.z - cen.y) + cu[2]), (float)(cv[0] * (p.x - cen.x) + cv[1] * (p.z - cen.y) + cv[2]))).ToArray();
        if (tcol != null && tcol.Length == tv.Length) { var c0 = tcol[near[0]]; m.colors = verts.Select(p => c0).ToArray(); }
        m.RecalculateNormals(); m.RecalculateBounds();
        string path = Folder + "/" + name + ".asset"; if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "patch"); go.transform.SetParent(opcd.transform, false); go.transform.localPosition = Vector3.zero; go.transform.rotation = Quaternion.identity;
        if (go.transform.lossyScale != Vector3.one) { go.transform.SetParent(null, true); go.transform.localScale = Vector3.one; go.transform.SetParent(opcd.transform, true); }
        go.transform.position = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterials = tpl.GetComponent<Renderer>().sharedMaterials;
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.sharedMaterial = tpl.GetComponent<Collider>().sharedMaterial;
        go.layer = tpl.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(tpl.gameObject));
        float area = 0; for (int i = 0; i < tris.Count; i += 3) area += Mathf.Abs(Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]).y) / 2;
        return string.Format(CI, "{0}: {1:F0} m2, {2} tris, template {3} ({4})", name, area, tris.Count / 3, tpl.name, mc.sharedMaterial.name);
    }
    static double[] Solve(double[,] m, double[] y)
    {
        var a = (double[,])m.Clone(); var b = (double[])y.Clone();
        for (int i = 0; i < 3; i++)
        {
            int p = i; for (int r = i + 1; r < 3; r++) if (Math.Abs(a[r, i]) > Math.Abs(a[p, i])) p = r;
            for (int c = 0; c < 3; c++) { var t = a[i, c]; a[i, c] = a[p, c]; a[p, c] = t; } { var t = b[i]; b[i] = b[p]; b[p] = t; }
            for (int r = 0; r < 3; r++) { if (r == i) continue; double fq = a[r, i] / a[i, i]; for (int c = 0; c < 3; c++) a[r, c] -= fq * a[i, c]; b[r] -= fq * b[i]; }
        }
        return new[] { b[0] / a[0, 0], b[1] / a[1, 1], b[2] / a[2, 2] };
    }

    // deep2rough;<name>;<hole>;radius;backRadius
    static string DeepToRough(GameObject opcd, Dictionary<int, Vector2[]> greens, string[] a)
    {
        int hole = int.Parse(a[2], CI); float rad = float.Parse(a[3], CI), back = float.Parse(a[4], CI);
        var g = greens[hole]; var gm = FromMesh(GreenMesh(opcd, g[0])); var toBack = (g[0] - g[1]).normalized;
        Func<Vector2, bool> near = p => { float c = Vector2.Dot((p - g[0]).normalized, toBack); float r = rad + (back - rad) * Mathf.Max(0, c); return SD(gm, p) < r; };
        var rough = Resources.Load<PhysicMaterial>("Physics/GSPrough"); int split = 0, removed = 0; var sb = new System.Text.StringBuilder();
        // OPCD course meshes playing as deep rough: move the triangles near the green into a collider-only GSPrough mesh
        foreach (var mc in opcd.GetComponentsInChildren<MeshCollider>(true).Where(c => c.sharedMaterial && c.sharedMaterial.name == "GSPdeeprough" && !c.name.Contains("Water")).ToList())
        {
            var src = mc.sharedMesh; if (src == null) continue; var tr = mc.transform; var v = src.vertices; var w = v.Select(p => tr.TransformPoint(p)).ToArray();
            var bnd = mc.bounds; if (bnd.max.x < gm.b.min.x - back - 2 || bnd.min.x > gm.b.max.x + back + 2 || bnd.max.z < gm.b.min.y - back - 2 || bnd.min.z > gm.b.max.y + back + 2) continue;
            var keep = new List<int>(); var take = new List<int>(); var t = src.triangles;
            for (int i = 0; i < t.Length; i += 3) { var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; (near(new Vector2(c.x, c.z)) ? take : keep).AddRange(new[] { t[i], t[i + 1], t[i + 2] }); }
            if (take.Count == 0) continue;
            var cut = new Mesh { name = mc.name + "_col_" + a[1] }; if (v.Length > 65000) cut.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; cut.vertices = v; cut.SetTriangles(keep, 0); cut.RecalculateBounds();
            var nr = new Mesh { name = mc.name + "_rough_" + a[1] }; if (v.Length > 65000) nr.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; nr.vertices = v; nr.SetTriangles(take, 0); nr.RecalculateBounds();
            foreach (var mm in new[] { cut, nr }) { string p = Folder + "/" + mm.name + ".asset"; if (AssetDatabase.LoadAssetAtPath<Mesh>(p) != null) AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(mm, p); }
            File.AppendAllText(Log, "collider " + PathOf(mc.transform) + " was " + AssetDatabase.GetAssetPath(src) + " | " + src.name + "\n");
            Undo.RecordObject(mc, "deep2rough"); mc.sharedMesh = null; mc.sharedMesh = cut;
            var go = new GameObject(mc.name + "_rough_" + a[1]); Undo.RegisterCreatedObjectUndo(go, "deep2rough"); go.transform.SetParent(mc.transform, false);
            var nc = go.AddComponent<MeshCollider>(); nc.sharedMesh = nr; nc.sharedMaterial = rough; go.layer = mc.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(mc.gameObject));
            split += take.Count / 3; sb.Append(mc.name + " " + take.Count / 3 + "; ");
        }
        // DEEP_ROUGH overlay patches: drop the triangles near the green (the rough underneath shows)
        var dr = GameObject.Find("DEEP_ROUGH");
        if (dr) foreach (var mf in dr.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh; var tr = mf.transform; var v = src.vertices; var w = v.Select(p => tr.TransformPoint(p)).ToArray(); var t = src.triangles; var keep = new List<int>();
                for (int i = 0; i < t.Length; i += 3) { var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; if (!near(new Vector2(c.x, c.z))) keep.AddRange(new[] { t[i], t[i + 1], t[i + 2] }); }
                if (keep.Count == t.Length) continue;
                var m2 = Object.Instantiate(src); m2.name = mf.name + "_" + a[1]; m2.SetTriangles(keep, 0); m2.RecalculateBounds();
                string pth = Folder + "/" + m2.name + ".asset"; if (AssetDatabase.LoadAssetAtPath<Mesh>(pth) != null) AssetDatabase.DeleteAsset(pth); AssetDatabase.CreateAsset(m2, pth);
                File.AppendAllText(Log, "meshfilter " + PathOf(mf.transform) + " was " + AssetDatabase.GetAssetPath(src) + " | " + src.name + "\n");
                removed += (t.Length - keep.Count) / 3; Undo.RecordObject(mf, "deep2rough"); mf.sharedMesh = m2; var mc = mf.GetComponent<MeshCollider>(); if (mc) { Undo.RecordObject(mc, "deep2rough"); mc.sharedMesh = null; mc.sharedMesh = m2; }
                sb.Append(mf.name + " -" + (t.Length - keep.Count) / 3 + "; ");
            }
        return a[1] + ": deep-rough triangles now playing as rough " + split + ", DEEP_ROUGH overlay triangles removed " + removed + " (" + sb + ")";
    }
    static string PathOf(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
