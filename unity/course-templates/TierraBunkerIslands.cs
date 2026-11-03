using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Turf islands inside bunkers (BoJa, Oct 30: "you keep adding an island green in the bunkers"). Three sources are checked for every active
// OPCD bunker mesh:
//  holes:   interior boundary loops of the sand mesh (the OPCD bunker spline had an island cut out, the rough underneath shows through)
//  overlay: triangles of other OPCD turf meshes (rough / fairway / collar / blend) drawn over the sand more than 0.8 m inside the outline
//  terrain: Unity terrain cells above the sand (renders through it)
// Only the sand meshes (*Bunker_-_Mesh); the *_Bunker_-_Blend lip rings are rings by design (their inner loop is the sand edge).
// Scan() reports per hole; Fill() fixes them: holes are closed with sand at the loop height (copy *_isl), overlay triangles are cut (copy *_isl),
// terrain cells are lowered to 0.3 m under the sand. Skips 18 (B package h18) and 15's 37/38 (B package h15 / h15b).
// Check() is the post-check other tools can call: returns "" when clean, else the offending bunkers.
public static class TierraBunkerIslands
{
    const string Dir = "Assets/TierraRejada/generated/bunkerislands";
    static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;
    class Hole { public int n; public Vector2 a, b; }
    static List<Hole> Holes()
    {
        var gk = (Dictionary<string, object>)TierraMiniJson.Parse(File.ReadAllText(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "/tierra-rejada/greenkeeper/TierraRejada.GKD"));
        var hs = new List<Hole>(); int n = 0;
        foreach (Dictionary<string, object> h in (List<object>)gk["Holes"])
        {
            n++; Vector2? tee = null, gc = null;
            foreach (Dictionary<string, object> t in (List<object>)h["Tees"]) { if (t["Position"] == null) continue; var q = (Dictionary<string, object>)t["Position"]; var p = new Vector2(System.Convert.ToSingle(q["x"], CI), System.Convert.ToSingle(q["z"], CI)); var tt = (string)t["TeeType"]; if (tt == "Black") tee = p; if (tt == "GreenCenterPoint") gc = p; }
            if (tee.HasValue && gc.HasValue) hs.Add(new Hole { n = n, a = tee.Value, b = gc.Value });
        }
        return hs;
    }
    static int HoleOf(List<Hole> hs, Vector2 p) { int best = 0; float bd = float.MaxValue; foreach (var h in hs) { var ab = h.b - h.a; float u = Mathf.Clamp01(Vector2.Dot(p - h.a, ab) / ab.sqrMagnitude); float d = (h.a + ab * u - p).magnitude; if (d < bd) { bd = d; best = h.n; } } return best; }
    static bool Skip(int hole, Vector3 c) { return hole == 18 || (hole == 15 && Mathf.Abs(c.x - 990) < 40 && Mathf.Abs(c.z - 797) < 40); }
    static bool Turf(string n) { var l = n.ToLower(); return !l.Contains("bunker") && (l.Contains("rough") || l.Contains("fairway") || l.Contains("collar") || l.Contains("blend") || l.Contains("green")); }

    // boundary loops of a mesh (world space), longest first
    static List<List<int>> Loops(Mesh m)
    {
        var t = m.triangles; var cnt = new Dictionary<long, int>(); var dir = new Dictionary<long, int[]>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        // weld by position so split uv seams don't count as boundary
        var v = m.vertices; var weld = new int[v.Length]; var map = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000)); int w; if (!map.TryGetValue(k, out w)) { w = i; map[k] = i; } weld[i] = w; }
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; dir[k] = new[] { a, b }; }
        var next = new Dictionary<int, int>(); foreach (var kv in cnt) if (kv.Value == 1) { var d = dir[kv.Key]; next[d[0]] = d[1]; }
        var loops = new List<List<int>>(); var used = new HashSet<int>();
        foreach (var s in next.Keys.ToList()) { if (used.Contains(s)) continue; var L = new List<int>(); int cur = s; while (!used.Contains(cur) && next.ContainsKey(cur)) { used.Add(cur); L.Add(cur); cur = next[cur]; } if (L.Count >= 3) loops.Add(L); }
        return loops.OrderByDescending(L => { float len = 0; for (int i = 0; i < L.Count; i++) len += (v[L[i]] - v[L[(i + 1) % L.Count]]).magnitude; return len; }).ToList();
    }
    static float Area(List<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; a += p[i].x * q.y - q.x * p[i].y; } return a / 2; }
    static bool Inside(List<Vector2> poly, Vector2 p) { bool c = false; for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c; return c; }
    static float EdgeDist(List<Vector2> poly, Vector2 p) { float d = float.MaxValue; for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; var ab = b - a; float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f)); d = Mathf.Min(d, (a + ab * u - p).magnitude); } return d; }

    static string Only;
    public static string Fill11Front() { Only = "11front"; try { return Run(true); } finally { Only = null; } }
    public static string Scan() { return Run(false); }
    public static string Fill() { return Run(true); }
    public static string Check() { var r = Run(false, true); return r; }
    static string Run(bool fix, bool quiet = false)
    {
        if (fix && !AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "bunkerislands");
        var hs = Holes(); var opcd = GameObject.Find("OPCD Meshes").transform; Physics.SyncTransforms();
        var bunkers = opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && f.name.Contains("Bunker") && !f.name.Contains("Blend") && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled).ToList();
        var turf = opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && Turf(f.name) && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled).ToList();
        var te = Object.FindObjectsOfType<Terrain>().First(t => t.name == "Terrain (Inner)"); var td = te.terrainData; var tp = te.transform.position; int R = td.heightmapResolution; float cx = td.size.x / (R - 1), cz = td.size.z / (R - 1);
        if (fix) Undo.RegisterCompleteObjectUndo(td, "bunker islands terrain");
        var perHole = new SortedDictionary<int, int[]>(); var lines = new List<string>(); var cutTris = new Dictionary<MeshFilter, HashSet<int>>();
        foreach (var bf in bunkers)
        {
            var bt = bf.transform; var bm = bf.sharedMesh; var bv = bm.vertices; var bb = bf.GetComponent<Renderer>().bounds; int hole = HoleOf(hs, new Vector2(bb.center.x, bb.center.z)); if (Skip(hole, bb.center)) continue;
            var loops = Loops(bm); if (loops.Count == 0) continue; var outer = loops[0].Select(i => { var w = bt.TransformPoint(bv[i]); return new Vector2(w.x, w.z); }).ToList();
            var holesL = new List<List<int>>(); for (int k = 1; k < loops.Count; k++) { var p2 = loops[k].Select(i => { var w = bt.TransformPoint(bv[i]); return new Vector2(w.x, w.z); }).ToList(); if (Inside(outer, p2[0]) && Mathf.Abs(Area(p2)) > 0.3f) holesL.Add(loops[k]); }
            var col = bf.GetComponent<MeshCollider>(); System.Func<float, float, float> Sand = (x, z) => { RaycastHit h; if (col && col.Raycast(new Ray(new Vector3(x, bb.max.y + 5, z), Vector3.down), out h, bb.size.y + 20)) return h.point.y; return float.NaN; };
            // overlay: turf triangles over the sand, centroid > 0.8 m inside the outline and not in a hole we fill anyway, above the sand
            int ov = 0;
            foreach (var tf in turf)
            {
                var tb = tf.GetComponent<Renderer>().bounds; if (tb.max.x < bb.min.x || tb.min.x > bb.max.x || tb.max.z < bb.min.z || tb.min.z > bb.max.z) continue;
                var tv = tf.sharedMesh.vertices; var tt = tf.sharedMesh.triangles; var tr = tf.transform; HashSet<int> set; if (!cutTris.TryGetValue(tf, out set)) { set = new HashSet<int>(); cutTris[tf] = set; }
                for (int k = 0; k < tt.Length; k += 3)
                {
                    var c = tr.TransformPoint((tv[tt[k]] + tv[tt[k + 1]] + tv[tt[k + 2]]) / 3); if (c.x < bb.min.x || c.x > bb.max.x || c.z < bb.min.z || c.z > bb.max.z) continue; var c2 = new Vector2(c.x, c.z);
                    if (!Inside(outer, c2) || EdgeDist(outer, c2) < (Only != null && bf.name.Contains(Only) ? 0.25f : 0.8f)) continue; float sy = Sand(c.x, c.z); bool inHole = float.IsNaN(sy);
                    if (!inHole && c.y < sy - 0.05f) continue;   /* under the sand: hidden */
                    if (set.Add(k)) ov++;
                }
            }
            // terrain cells above the sand
            int i0 = Mathf.Max(0, Mathf.FloorToInt((bb.min.x - tp.x) / cx)), i1 = Mathf.Min(R - 1, Mathf.CeilToInt((bb.max.x - tp.x) / cx)), j0 = Mathf.Max(0, Mathf.FloorToInt((bb.min.z - tp.z) / cz)), j1 = Mathf.Min(R - 1, Mathf.CeilToInt((bb.max.z - tp.z) / cz));
            var H = td.GetHeights(i0, j0, i1 - i0 + 1, j1 - j0 + 1); int tc = 0;
            for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) { float x = tp.x + i * cx, z = tp.z + j * cz; float sy = Sand(x, z); if (float.IsNaN(sy)) continue; float hy = tp.y + H[j - j0, i - i0] * td.size.y; if (hy > sy - 0.05f) { tc++; H[j - j0, i - i0] = (sy - 0.3f - tp.y) / td.size.y; } }
            if (holesL.Count + ov + tc == 0) continue;
            int[] c3; if (!perHole.TryGetValue(hole, out c3)) { c3 = new int[3]; perHole[hole] = c3; } c3[0] += holesL.Count; c3[1] += ov > 0 ? 1 : 0; c3[2] += tc > 0 ? 1 : 0;
            lines.Add(string.Format(CI, "h{0} {1} at ({2:F0},{3:F0}): {4} holes, {5} turf triangles over the sand, {6} terrain cells above it", hole, bf.name, bb.center.x, bb.center.z, holesL.Count, ov, tc));
            if (!fix) continue;
            if (tc > 0) td.SetHeights(i0, j0, H);
            if (holesL.Count > 0)
            {
                var v = bm.vertices.ToList(); var uv = bm.uv.ToList(); var cl = bm.colors.ToList(); var nm = bm.normals.ToList(); var tris = bm.GetTriangles(0).ToList();
                foreach (var L in holesL)
                {
                    // fan from the loop centroid at the mean loop height (sand level round the island); winding matched to an up normal
                    var cen = Vector3.zero; foreach (var i in L) cen += bv[i]; cen /= L.Count; int ci = v.Count; v.Add(cen);
                    if (uv.Count > 0) uv.Add(L.Select(i => bm.uv[i]).Aggregate((a, b) => a + b) / L.Count); if (cl.Count > 0) cl.Add(L.Select(i => bm.colors[i]).Aggregate((a, b) => a + b) / L.Count); if (nm.Count > 0) nm.Add(bt.InverseTransformDirection(Vector3.up));
                    for (int k = 0; k < L.Count; k++) { int a = L[k], b = L[(k + 1) % L.Count]; var n = Vector3.Cross(bt.TransformPoint(bv[b]) - bt.TransformPoint(bv[a]), bt.TransformPoint(cen) - bt.TransformPoint(bv[a])); if (n.y >= 0) tris.AddRange(new[] { a, b, ci }); else tris.AddRange(new[] { b, a, ci }); }
                }
                var m2 = Object.Instantiate(bm); m2.name = bm.name.Replace("(Clone)", "") + "_isl"; m2.SetVertices(v); if (uv.Count == v.Count) m2.SetUVs(0, uv); if (cl.Count == v.Count) m2.SetColors(cl); if (nm.Count == v.Count) m2.SetNormals(nm); m2.SetTriangles(tris, 0); m2.RecalculateBounds();
                var p = Dir + "/" + bf.name + "_isl.asset"; AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(m2, p); bf.sharedMesh = m2; if (col) { col.sharedMesh = null; col.sharedMesh = m2; }
            }
        }
        if (fix)
            foreach (var kv in cutTris.Where(k => k.Value.Count > 0))
            {
                var tf = kv.Key; var m = tf.sharedMesh; var tt = m.triangles; var keep = new List<int>(); for (int k = 0; k < tt.Length; k += 3) if (!kv.Value.Contains(k)) { keep.Add(tt[k]); keep.Add(tt[k + 1]); keep.Add(tt[k + 2]); }
                var m2 = Object.Instantiate(m); m2.name = m.name.Replace("(Clone)", "") + "_isl"; if (m2.subMeshCount > 1) m2.subMeshCount = 1; m2.SetTriangles(keep, 0); var p = Dir + "/" + tf.name + "_isl.asset"; AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(m2, p); tf.sharedMesh = m2;
                var mc = tf.GetComponent<MeshCollider>(); if (mc) { mc.sharedMesh = null; mc.sharedMesh = m2; }
            }
        if (fix) { EditorUtility.SetDirty(td); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(opcd.gameObject.scene); }
        var sum = string.Join("; ", perHole.Select(kv => string.Format("h{0}: {1} hole islands, {2} bunkers with turf over sand, {3} with terrain through", kv.Key, kv.Value[0], kv.Value[1], kv.Value[2])).ToArray());
        if (quiet) return lines.Count == 0 ? "" : sum;
        File.WriteAllText("AutomationOutput/vegetation/bunker_islands.txt", sum + "\n" + string.Join("\n", lines.ToArray()));
        return (fix ? "FIXED " : "SCAN ") + lines.Count + " bunkers. " + sum;
    }
    // Plants in the sand: triangles of the scatter chunk meshes (SHRUBS, HILL_BRUSH, B11_MOUND_TUFTS, LANDSCAPE_1029 beds) whose centroid
    // stands over bunker sand are cut (copies *_sand). PlantsScan() counts only.
    public static string PlantsScan() { return Plants(false); }
    public static string PlantsOffSand() { return Plants(true); }
    static string Plants(bool fix)
    {
        if (fix && !AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "bunkerislands");
        var hs = Holes(); var opcd = GameObject.Find("OPCD Meshes").transform; Physics.SyncTransforms();
        var sands = opcd.GetComponentsInChildren<MeshCollider>(false).Where(c => c.name.Contains("Bunker") && !c.name.Contains("Blend")).Where(c => { var b = c.bounds; return !Skip(HoleOf(hs, new Vector2(b.center.x, b.center.z)), b.center); }).ToList();
        var roots = new[] { "SHRUBS", "HILL_BRUSH", "B11_MOUND_TUFTS", "LANDSCAPE_1029", "HOLE4_LEFT_SHRUBS", "HOLE8_LEFT_SHRUBS" };
        var mfs = roots.Select(r => GameObject.Find(r)).Where(g => g).SelectMany(g => g.GetComponentsInChildren<MeshFilter>(false)).Where(f => f.sharedMesh && !f.GetComponentInParent<LODGroup>()).ToList();
        int total = 0; var per = new SortedDictionary<int, int>();
        foreach (var mf in mfs)
        {
            var rb = mf.GetComponent<Renderer>().bounds; var hit = sands.Where(s => s.bounds.Intersects(rb)).ToList(); if (hit.Count == 0) continue;
            var m = mf.sharedMesh; var v = m.vertices; var tr = mf.transform; var subs = new List<int[]>(); int n = 0;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tt = m.GetTriangles(s); var keep = new List<int>();
                for (int k = 0; k < tt.Length; k += 3)
                {
                    var c = tr.TransformPoint((v[tt[k]] + v[tt[k + 1]] + v[tt[k + 2]]) / 3); bool on = false;
                    foreach (var sc in hit) { if (!sc.bounds.Contains(new Vector3(c.x, sc.bounds.center.y, c.z))) continue; RaycastHit h; if (sc.Raycast(new Ray(new Vector3(c.x, sc.bounds.max.y + 5, c.z), Vector3.down), out h, sc.bounds.size.y + 20) && c.y > h.point.y - 0.2f && c.y < h.point.y + 4f) { on = true; int ho = HoleOf(hs, new Vector2(c.x, c.z)); int q; per[ho] = per.TryGetValue(ho, out q) ? q + 1 : 1; break; } }
                    if (on) { n++; continue; } keep.Add(tt[k]); keep.Add(tt[k + 1]); keep.Add(tt[k + 2]);
                }
                subs.Add(keep.ToArray());
            }
            if (n == 0) continue; total += n; if (!fix) continue;
            var m2 = Object.Instantiate(m); m2.name = m.name.Replace("(Clone)", "") + "_sand"; for (int s = 0; s < subs.Count; s++) m2.SetTriangles(subs[s], s); var p = Dir + "/" + mf.name + "_" + mf.GetInstanceID() + "_sand.asset"; AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(m2, p); mf.sharedMesh = m2;
        }
        if (fix) { AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(opcd.gameObject.scene); }
        return (fix ? "cut " : "found ") + total + " plant triangles on sand: " + string.Join(", ", per.Select(kv => "h" + kv.Key + " " + kv.Value).ToArray());
    }
}

