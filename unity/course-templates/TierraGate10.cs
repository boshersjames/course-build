using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Hole 10 gate: automated checks (read-only). Returns one line per check: "ok" or the failures.
public class TierraGate10
{
    static Collider[] paths; static Transform opcd;
    static bool On(Vector2 q) { return paths.Any(pc => { RaycastHit h; return pc.Raycast(new Ray(new Vector3(q.x, 300, q.y), Vector3.down), out h, 600); }); }
    static KeyValuePair<float, string> Top(Vector2 q, bool withPath = false) { float y = float.MinValue; string nm = ""; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 300, q.y), Vector3.down, 600)) { bool isP = paths.Contains(h.collider); if (isP && !withPath) continue; if (isP || h.collider.transform.root == opcd || h.collider is TerrainCollider) if (h.point.y > y) { y = h.point.y; nm = isP ? "PATH" : h.collider.name; } } return new KeyValuePair<float, string>(y, nm); }
    static float ax0 = 1180, ax1 = 1590, az0 = 1035, az1 = 1270;   /* default: the hole 10/11 corridor; PlantsOnTurf takes x0/x1/z0/z1 for other holes */
    static bool InH10(Vector3 c) { return c.x > ax0 && c.x < ax1 && c.z > az0 && c.z < az1; }
    static Bounds AreaB() { return new Bounds(new Vector3((ax0 + ax1) / 2, 80, (az0 + az1) / 2), new Vector3(ax1 - ax0, 400, az1 - az0)); }
    public static string Checks()
    {
        paths = GameObject.Find("CART_PATHS").GetComponentsInChildren<Collider>(); opcd = GameObject.Find("OPCD Meshes").transform; Physics.SyncTransforms(); var outp = new List<string>();
        var bi = TierraBunkerIslands.Check(); outp.Add("bunker islands: " + (string.IsNullOrEmpty(bi) ? "ok" : bi));
        // floating overlays: draped transparent meshes in the hole 10 box, sampled vertices vs ground (path counts as ground)
        var fl = new List<string>();
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r.transform.root == opcd || !r.sharedMaterial || !r.sharedMaterial.shader.name.Contains("Transparent") || r.sharedMaterial.shader.name.Contains("Cutout") || r.bounds.size.y > 40) continue;   /* draped decals only (Legacy Transparent), not alpha-cutout plants */ if (!r.bounds.Intersects(AreaB())) continue;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; var v = mf.sharedMesh.vertices; int bad = 0, n = 0; float worst = 0;
            for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 400)) { var w = r.transform.TransformPoint(v[i]); if (!InH10(w)) continue; var g = Top(new Vector2(w.x, w.z), true).Key; if (g < -1e30f) continue; n++; float d = w.y - g; if (d > 0.25f || d < -0.25f) { bad++; if (Mathf.Abs(d) > Mathf.Abs(worst)) worst = d; } }
            if (n > 0 && bad > n * 0.02f) fl.Add(r.transform.root.name + "/" + r.name + " " + bad + "/" + n + " worst " + worst.ToString("F2") + " m");
        }
        outp.Add("floating overlays: " + (fl.Count == 0 ? "ok" : string.Join("; ", fl.ToArray())));
        // unsupported bins
        var bins = new List<string>(); int nb = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r.transform.root == opcd) continue; var nm = r.name.ToLower(); if (!(nm.Contains("bin") || nm.Contains("trash") || nm.Contains("divot"))) continue; if (nm.Contains("cabin")) continue; if (!InH10(r.bounds.center)) continue; nb++; var grp = r.transform.parent ? r.transform.parent : r.transform; var b = r.bounds; foreach (var r2 in grp.GetComponentsInChildren<Renderer>()) if (r2.enabled && (r2.bounds.center - r.bounds.center).magnitude < 3f) b.Encapsulate(r2.bounds);   /* the whole bin / sign assembly (post, body, lid) within 3 m */
            float gmax = float.MinValue; foreach (var q in new[] { new Vector2(b.center.x, b.center.z), new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.max.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.min.z) }) gmax = Mathf.Max(gmax, Top(q, true).Key);
            float gap = b.min.y - gmax; if (gap > 0.08f) bins.Add(r.transform.root.name + "/" + r.name + " gap " + gap.ToString("F2") + " m");
        }
        outp.Add("unsupported bins (" + nb + " checked): " + (bins.Count == 0 ? "ok" : string.Join("; ", bins.ToArray())));
        // broken fences: ROPE10 stakes (8-vertex boxes) on the ground, rope above the ground and continuous
        var fe = new List<string>(); var rope = GameObject.Find("ROPE10");
        if (!rope) fe.Add("ROPE10 missing"); else
        {
            var st = rope.transform.Find("stakes").GetComponent<MeshFilter>().sharedMesh.vertices; for (int i = 0; i + 7 < st.Length; i += 8) { float ymin = Enumerable.Range(i, 8).Min(k => st[k].y); var c = (st[i] + st[i + 7]) / 2; float g = Top(new Vector2(c.x, c.z)).Key; if (ymin - g > 0.03f || g - ymin > 0.3f) fe.Add("stake " + (i / 8) + " at (" + c.x.ToString("F0") + "," + c.z.ToString("F0") + ") off ground " + (ymin - g).ToString("F2")); }
            var rv = rope.transform.Find("rope").GetComponent<MeshFilter>().sharedMesh.vertices; int low = 0; for (int i = 0; i < rv.Length; i += 8) { float g = Top(new Vector2(rv[i].x, rv[i].z)).Key; if (rv[i].y - g < 0.08f) low++; } if (low > 0) fe.Add(low + " rope segments under 8 cm");
            var segC = new List<Vector3>(); for (int i = 0; i + 7 < rv.Length; i += 8) segC.Add((rv[i] + rv[i + 7]) / 2); int gaps = 0; for (int i = 1; i < segC.Count; i++) { float d = (segC[i] - segC[i - 1]).magnitude; if (d > 2.0f && d < 6f) gaps++; } if (gaps > 0) fe.Add(gaps + " rope gaps");
        }
        foreach (var r in Object.FindObjectsOfType<Renderer>()) { if (!r.enabled || !r.gameObject.activeInHierarchy) continue; var nm = r.name.ToLower(); if (!(nm.Contains("fence") || nm.Contains("rope")) || r.transform.IsChildOf(rope.transform)) continue; if (InH10(r.bounds.center)) fe.Add("other fence/rope object active: " + r.transform.root.name + "/" + r.name); }
        outp.Add("broken fences: " + (fe.Count == 0 ? "ok" : string.Join("; ", fe.Distinct().Take(12).ToArray())));
        // objects on turf inside the rope: anything (not rope, trees, hill brush, overlays) north of the 10 path between x 1300 and 1535 on mown ground
        var ins = new HashSet<string>(); var hb = GameObject.Find("HILL_BRUSH");
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r.transform.root == opcd || r.transform.IsChildOf(rope.transform) || (hb && r.transform.IsChildOf(hb.transform)) || r.transform.root.name == "FACE11") continue;   /* FACE11 = hole 11 tee-hill objects */ if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2440 && r.sharedMaterial.renderQueue < 3000) continue;
            var b = r.bounds; if (b.size.x > 12 || b.size.z > 12) continue; var c = b.center; if (c.x < 1300 || c.x > 1535 || c.z < 1070 || c.z > 1160) continue; var nm = r.name; if (nm.Contains("Trunk") || nm.Contains("Frond") || r.transform.root.name.StartsWith("PALMS") || nm.Contains("Oak") || nm.Contains("Pepper") || nm.Contains("Palm") || nm.Contains("Euc") || nm.Contains("Sycamore") || nm.Contains("Tree")) continue;
            var p = new Vector2(c.x, c.z); bool north = false; for (float d = 0.2f; d < 6f; d += 0.2f) { if (On(p + new Vector2(0, -d))) { north = true; break; } if (On(p + new Vector2(0, d))) break; } if (!north) continue;
            var gn = Top(p).Value.ToLower(); if (!(gn.Contains("ough") || gn.Contains("fairway") || gn.Contains("tee") || gn.Contains("green"))) continue; if (gn.Contains("outer")) continue;
            ins.Add(r.transform.root.name + "/" + r.name + " (" + c.x.ToString("F0") + "," + c.z.ToString("F0") + ")");
        }
        outp.Add("objects on turf inside the rope: " + (ins.Count == 0 ? "ok" : string.Join("; ", ins.Take(20).ToArray()) + (ins.Count > 20 ? " +" + (ins.Count - 20) : "")));
        return string.Join("\n", outp.ToArray());
    }
    // plants on bare mown turf anywhere in the hole 10 corridor: instance bases and combined-chunk triangles whose ground is rough /
    // fairway / tee / green / collar (not outer rough) with no soil / cover decal over it. fix = true removes them (chunk triangles cut,
    // instances switched off).
    public bool fix; public float x0, x1, z0, z1;   /* optional area (all 0: the hole 10/11 corridor) */
    // accepted: billboard (LOD2) cards of trees standing in beds overhang the bed edge; their trunks are in the bed (coordinator, Oct 31)
    static bool Accepted(string n) { return n.Contains("Cordyline_LOD2"); }
    static readonly string[] PlantRoots = { "SHRUBS", "HILL_BRUSH", "LANDSCAPE_1029", "TALL_SHRUB_TREES", "HOLE10_SAGE", "SMALL_PROPS_1029", "BEDS", "SIGN_BEDS" };
    static bool PlantName(string n) { n = n.ToLower(); return n.Contains("shrub") || n.Contains("sage") || n.Contains("buckwheat") || n.Contains("brush") || n.Contains("bush") || n.Contains("grass") || n.Contains("clump") || n.Contains("scrub") || n.Contains("toyon") || n.Contains("sumac") || n.Contains("tuft") || n.Contains("h10hill") || n.Contains("h10bank") || n.Contains("cliff11") || n.Contains("cordyline") || n.Contains("agave") || n.Contains("flower"); }
    static bool Mown(string g) { g = g.ToLower(); if (g.Contains("outer")) return false; return g.Contains("rough") || g.Contains("fairway") || g.Contains("tee") || g.Contains("green") || g.Contains("collar") || g.Contains("apron"); }
    public string PlantsOnTurf()
    {
        if (x1 > x0 && z1 > z0) { ax0 = x0; ax1 = x1; az0 = z0; az1 = z1; } else { ax0 = 1180; ax1 = 1590; az0 = 1035; az1 = 1270; }
        paths = GameObject.Find("CART_PATHS").GetComponentsInChildren<Collider>(); opcd = GameObject.Find("OPCD Meshes").transform; var tmp = new List<MeshCollider>();
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>()) { if (!r.enabled || !r.gameObject.activeInHierarchy || r.transform.root == opcd || !r.sharedMaterial) continue; var n = r.name.ToLower(); if (!(n.Contains("cover") || n.Contains("soil") || n.Contains("dirt") || n.Contains("bank") || n.Contains("toe") || n.Contains("mulch") || n.Contains("bed"))) continue; if (!r.bounds.Intersects(AreaB())) continue; var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || r.GetComponent<Collider>()) continue; var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; tmp.Add(mc); }
        Physics.SyncTransforms(); var deco = new HashSet<Collider>(tmp.Cast<Collider>());
        System.Func<Vector2, KeyValuePair<float, string>> GroundAt = q => { float y = float.MinValue; string nm = ""; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 400, q.y), Vector3.down, 800)) { if (deco.Contains(h.collider)) continue; if (paths.Contains(h.collider)) { if (h.point.y > y) { y = h.point.y; nm = "PATH"; } continue; } if (h.collider.transform.root == opcd || h.collider is TerrainCollider) if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } } return new KeyValuePair<float, string>(y, nm); };
        System.Func<Vector2, bool> Covered = q => Physics.RaycastAll(new Vector3(q.x, 400, q.y), Vector3.down, 800).Any(h => deco.Contains(h.collider));
        // a base is bad if its ground is mown turf and no soil / cover decal lies within 1.2 m of it
        System.Func<string, bool> Hard = gname => { gname = gname.ToLower(); if (gname.Contains("blend") || gname.Contains("rough")) return false; return gname.Contains("green") || gname.Contains("collar") || gname.Contains("fairway") || gname.Contains("apron"); };   /* not the wide rough-blend bands */
        System.Func<string, bool> GreenLike = gname => { gname = gname.ToLower(); return !gname.Contains("blend") && (gname.Contains("green") || gname.Contains("collar")); };
        System.Func<Vector2, bool> NearHard = q => { if (Hard(GroundAt(q).Value)) return true; for (int a = 0; a < 12; a++) { var d = new Vector2(Mathf.Cos(a * 0.5236f), Mathf.Sin(a * 0.5236f)); if (Hard(GroundAt(q + d * 2f).Value)) return true; if (GreenLike(GroundAt(q + d * 4f).Value) || GreenLike(GroundAt(q + d * 6f).Value)) return true; }   /* 6 m from greens / collars, 2 m from fairways / aprons */ for (int a = 0; a < 8; a++) { var d = new Vector2(Mathf.Cos(a * 0.785f), Mathf.Sin(a * 0.785f)) * 1.5f; if (GroundAt(q + d).Value.ToLower().Contains("tee")) return true; } return false; };
        /* bad: within 6 m of a green / collar / fairway / apron or 1.5 m of a tee even on a soil decal (scrub must not reach the play surfaces); otherwise on mown turf with no soil / cover decal within 1.2 m */
        System.Func<Vector2, bool> Bad = q => { if (NearHard(q)) return true; if (!Mown(GroundAt(q).Value)) return false; if (Covered(q)) return false; for (int a = 0; a < 8; a++) { var d = new Vector2(Mathf.Cos(a * 0.785f), Mathf.Sin(a * 0.785f)) * 1.2f; if (Covered(q + d)) return false; } return true; };
        var found = new List<string>(); int cut = 0, off = 0, nPlants = 0;
        foreach (var rn in PlantRoots)
        {
            var root = GameObject.Find(rn); if (!root) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<Renderer>(); if (!r || !r.enabled || !mf.gameObject.activeInHierarchy || !mf.sharedMesh || Accepted(mf.name)) continue; if (!r.bounds.Intersects(AreaB())) continue;
                bool chunk = r.bounds.size.x > 6 || r.bounds.size.z > 6; var nm = mf.name; var lnm = nm.ToLower(); if (lnm.Contains("oak") || lnm.Contains("pepper") || lnm.Contains("euc") || lnm.Contains("palm") || lnm.Contains("impostor") || lnm.Contains("_bark") || lnm.Contains("leaves") || lnm.Contains("cliff") && !lnm.StartsWith("cliff11") || lnm.Contains("rock") || lnm.Contains("stone") || lnm.Contains("trunk") || lnm.Contains("frond")) continue;   /* trees and rocks are not ground plants */ if (!chunk && !PlantName(nm) && !PlantName(mf.transform.parent ? mf.transform.parent.name : "")) continue;
                if (!chunk)
                {
                    var b = r.bounds; var basePt = new Vector2(b.center.x, b.center.z); if (!InH10(b.center) || !Bad(basePt)) continue;
                    var lg = mf.GetComponentInParent<LODGroup>(); var go = lg ? lg.gameObject : mf.gameObject; found.Add(rn + "/" + go.name + " (" + b.center.x.ToString("F0") + "," + b.center.z.ToString("F0") + ")"); if (fix) { go.SetActive(false); off++; } continue;
                }
                // chunk: plants = connected triangle groups (shared vertices); groups whose lowest point touches the ground are bases, the
                // others (loose cards) belong to the nearest base within 1.5 m
                var m = mf.sharedMesh; var v = m.vertices; var tr = mf.transform; var w = new Vector3[v.Length]; for (int i = 0; i < v.Length; i++) w[i] = tr.TransformPoint(v[i]);
                var par = Enumerable.Range(0, v.Length).ToArray(); System.Func<int, int> Fd = null; Fd = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
                var tris = new List<int[]>(); for (int s = 0; s < m.subMeshCount; s++) tris.Add(m.GetTriangles(s));
                foreach (var t in tris) for (int k = 0; k < t.Length; k += 3) { int a = Fd(t[k]), b = Fd(t[k + 1]), c = Fd(t[k + 2]); par[b] = a; par[Fd(c)] = a; }
                var lowest = new Dictionary<int, Vector3>(); var sumC = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>();
                foreach (var t in tris) foreach (var vi in t) { int c = Fd(vi); Vector3 lo; if (!lowest.TryGetValue(c, out lo) || w[vi].y < lo.y) lowest[c] = w[vi]; Vector3 sc; sumC[c] = (sumC.TryGetValue(c, out sc) ? sc : Vector3.zero) + w[vi]; int n0; cnt[c] = (cnt.TryGetValue(c, out n0) ? n0 : 0) + 1; }
                var badC = new HashSet<int>(); var bases = new List<KeyValuePair<Vector2, bool>>(); var isBase = new Dictionary<int, bool>();
                foreach (var kv in lowest) { if (!InH10(kv.Value)) continue; var q = new Vector2(kv.Value.x, kv.Value.z); var g = GroundAt(q); if (kv.Value.y - g.Key < 0.45f) { bool bad = Bad(q); isBase[kv.Key] = bad; bases.Add(new KeyValuePair<Vector2, bool>(q, bad)); if (bad) badC.Add(kv.Key); } }
                nPlants += bases.Count;
                foreach (var kv in lowest) { if (isBase.ContainsKey(kv.Key) || !InH10(kv.Value)) continue; var cen = sumC[kv.Key] / cnt[kv.Key]; var q = new Vector2(cen.x, cen.z); float bd = 1.5f; bool bad = false, hit = false; foreach (var b in bases) { float d = (b.Key - q).magnitude; if (d < bd) { bd = d; bad = b.Value; hit = true; } } if (hit && bad) badC.Add(kv.Key); }
                if (badC.Count == 0) continue; int nb = bases.Count(b => b.Value); var bb = bases.Where(b => b.Value).Select(b => b.Key).ToList(); var bc = bb.Count > 0 ? new Vector2(bb.Average(q => q.x), bb.Average(q => q.y)) : Vector2.zero; found.Add(rn + "/" + mf.name + " chunk: " + nb + " plants on bare turf, around (" + bc.x.ToString("F0") + "," + bc.y.ToString("F0") + ")" + (nb > 0 ? " x " + bb.Min(q => q.x).ToString("F0") + "-" + bb.Max(q => q.x).ToString("F0") + " z " + bb.Min(q => q.y).ToString("F0") + "-" + bb.Max(q => q.y).ToString("F0") : ""));
                if (fix)
                {
                    var m2 = Object.Instantiate(m); m2.name = m.name; for (int s = 0; s < tris.Count; s++) { var keep = new List<int>(); var t = tris[s]; for (int k = 0; k < t.Length; k += 3) { if (badC.Contains(Fd(t[k]))) { cut++; continue; } keep.Add(t[k]); keep.Add(t[k + 1]); keep.Add(t[k + 2]); } m2.SetTriangles(keep, s); }
                    var dir = "Assets/TierraRejada/generated/hole10/cut"; if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/TierraRejada/generated/hole10", "cut"); var np = dir + "/" + rn + "_" + mf.name + ".asset"; AssetDatabase.DeleteAsset(np); AssetDatabase.CreateAsset(m2, np); mf.sharedMesh = m2;
                }
            }
        }
        foreach (var mc in tmp) Object.DestroyImmediate(mc); if (fix) { AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(opcd.gameObject.scene); }
        return "plants checked " + nPlants + "; " + (found.Count == 0 ? "on bare mown turf: ok" : "on bare mown turf: " + found.Count + " groups\n" + string.Join("\n", found.Take(80).ToArray()) + (found.Count > 80 ? "\n+" + (found.Count - 80) : "")) + (fix ? "\nfixed: " + off + " instances off, " + cut + " chunk triangles cut" : "");
    }
    // rope side clean: no scrub chunk or soil / cover decal between the 10 path's edge and 3 m past the rope line
    public string RopeSide()
    {
        if (x1 > x0 && z1 > z0) { ax0 = x0; ax1 = x1; az0 = z0; az1 = z1; } else { ax0 = 1180; ax1 = 1590; az0 = 1035; az1 = 1270; }
        paths = GameObject.Find("CART_PATHS").GetComponentsInChildren<Collider>(); opcd = GameObject.Find("OPCD Meshes").transform; var tmp = new List<MeshCollider>(); var box = new Bounds(new Vector3(1385, 80, 1100), new Vector3(420, 300, 140));
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r.transform.root == opcd || r.GetComponent<Collider>() || !r.bounds.Intersects(box)) continue; var rn = r.transform.root.name; var n = r.name.ToLower();
            bool scrub = rn == "HILL_BRUSH" || rn == "SHRUBS" || rn == "HOLE10_SAGE"; bool decal = n.Contains("cover") || n.Contains("soil") || n.Contains("dirt") || n.Contains("bank") || n.Contains("toe") || n.Contains("worn");   /* the dry-rough overlay is rough colour, not soil: the S-bend rope stands on it (4622) */
            if (!scrub && !decal) continue; var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; tmp.Add(mc);
        }
        Physics.SyncTransforms(); var set = new HashSet<Collider>(tmp.Cast<Collider>());
        var rope = GameObject.Find("ROPE10/rope"); var rv = rope.GetComponent<MeshFilter>().sharedMesh.vertices; var bad = new List<string>();
        for (int i = 0; i + 7 < rv.Length; i += 32)
        {
            var c3 = rope.transform.TransformPoint((rv[i] + rv[i + 7]) / 2); var c = new Vector2(c3.x, c3.z); Vector2 np = c; float bd = 9; for (float dx = -5; dx <= 5; dx += 0.25f) for (float dz = -5; dz <= 5; dz += 0.25f) { var q = c + new Vector2(dx, dz); float d = new Vector2(dx, dz).magnitude; if (d < bd && paths.Any(pc => { RaycastHit h; return pc.Raycast(new Ray(new Vector3(q.x, 300, q.y), Vector3.down), out h, 600); })) { bd = d; np = q; } }
            if (bd > 8) continue; var dir = (c - np).normalized; float L = (c - np).magnitude + 3f;
            for (float s = 0.3f; s <= L; s += 0.3f) { var q = np + dir * s; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 300, q.y), Vector3.down, 600)) if (set.Contains(h.collider)) { bad.Add(h.collider.transform.root.name + "/" + h.collider.name + " at (" + q.x.ToString("F1") + "," + q.y.ToString("F1") + ")"); goto next; } }
            next:;
        }
        var hitPts = new List<Vector2>(); foreach (var b in bad) { if (!b.StartsWith("SHRUBS/")) continue; var s0 = b.Substring(b.IndexOf("(") + 1).TrimEnd(')').Split(','); hitPts.Add(new Vector2(float.Parse(s0[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(s0[1], System.Globalization.CultureInfo.InvariantCulture))); }
        int cutN = 0;
        if (fix && hitPts.Count > 0)
            foreach (var mf in GameObject.Find("SHRUBS").GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<Renderer>(); if (!r || !r.enabled || !r.bounds.Intersects(box) || !mf.sharedMesh) continue; var m = mf.sharedMesh; var v = m.vertices; var w = v.Select(q => mf.transform.TransformPoint(q)).ToArray();
                var par = Enumerable.Range(0, v.Length).ToArray(); System.Func<int, int> Fd = null; Fd = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
                var tris = new List<int[]>(); for (int s = 0; s < m.subMeshCount; s++) tris.Add(m.GetTriangles(s)); foreach (var tt in tris) for (int k = 0; k < tt.Length; k += 3) { int a = Fd(tt[k]); par[Fd(tt[k + 1])] = a; par[Fd(tt[k + 2])] = a; }
                var sum = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>(); foreach (var tt in tris) foreach (var vi in tt) { int c = Fd(vi); Vector3 sc; sum[c] = (sum.TryGetValue(c, out sc) ? sc : Vector3.zero) + w[vi]; int n0; cnt[c] = (cnt.TryGetValue(c, out n0) ? n0 : 0) + 1; }
                var badC = new HashSet<int>(sum.Keys.Where(c => { var ce = sum[c] / cnt[c]; var q = new Vector2(ce.x, ce.z); return hitPts.Any(hp => (hp - q).magnitude < 3f); }));
                if (badC.Count == 0) continue; var m2 = Object.Instantiate(m); m2.name = m.name; for (int s = 0; s < tris.Count; s++) { var keep = new List<int>(); var tt = tris[s]; for (int k = 0; k < tt.Length; k += 3) { if (badC.Contains(Fd(tt[k]))) { cutN++; continue; } keep.Add(tt[k]); keep.Add(tt[k + 1]); keep.Add(tt[k + 2]); } m2.SetTriangles(keep, s); }
                var dir2 = "Assets/TierraRejada/generated/hole10/cut"; var np2 = dir2 + "/ropeside_" + mf.name + ".asset"; AssetDatabase.DeleteAsset(np2); AssetDatabase.CreateAsset(m2, np2); mf.sharedMesh = m2;
            }
        if (cutN > 0) { AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(opcd.gameObject.scene); }
        foreach (var mc in tmp) Object.DestroyImmediate(mc);
        return (cutN > 0 ? "cut " + cutN + " SHRUBS triangles at the rope line; " : "") + "rope side (path edge to rope + 3 m): " + (bad.Count == 0 ? "ok" : bad.Count + " samples hit scrub/decal: " + string.Join("; ", bad.Distinct().Take(20).ToArray()));
    }
    // removed-plants map: for every cut chunk under generated/hole10/cut, find the scene object using it and the original mesh asset of the
    // same name; plants (connected components) present in the original but not in the cut are written as x,z,layer lines
    public string outPath = "";
    public string RemovedMap()
    {
        var lines = new List<string>(); var log = new List<string>(); var cutPaths = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/TierraRejada/generated/hole10/cut" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        var mfs = Object.FindObjectsOfType<MeshFilter>();
        foreach (var cp in cutPaths)
        {
            var cut = AssetDatabase.LoadAssetAtPath<Mesh>(cp); var mf = mfs.FirstOrDefault(q => q.sharedMesh == cut); if (!mf) { log.Add("unused " + cp); continue; }
            var orig = AssetDatabase.FindAssets("t:Mesh " + cut.name).Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("/cut/")).Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Mesh>().FirstOrDefault(m => m.name == cut.name)).FirstOrDefault(m => m && m.vertexCount == cut.vertexCount);
            if (!orig) { log.Add("no original for " + cut.name + " (" + mf.transform.root.name + ")"); continue; }
            var keep = new HashSet<int>(); for (int s = 0; s < cut.subMeshCount; s++) foreach (var i in cut.GetTriangles(s)) keep.Add(i);
            var v = orig.vertices; var tr = mf.transform; var par = Enumerable.Range(0, v.Length).ToArray(); System.Func<int, int> Fd = null; Fd = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
            var tris = new List<int[]>(); for (int s = 0; s < orig.subMeshCount; s++) tris.Add(orig.GetTriangles(s)); foreach (var tt in tris) for (int k = 0; k < tt.Length; k += 3) { int a = Fd(tt[k]); par[Fd(tt[k + 1])] = a; par[Fd(tt[k + 2])] = a; }
            var gone = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>(); foreach (var tt in tris) foreach (var i in tt) { if (keep.Contains(i)) continue; int c = Fd(i); Vector3 sc; gone[c] = (gone.TryGetValue(c, out sc) ? sc : Vector3.zero) + tr.TransformPoint(v[i]); int n0; cnt[c] = (cnt.TryGetValue(c, out n0) ? n0 : 0) + 1; }
            foreach (var kv in gone) { var p = kv.Value / cnt[kv.Key]; lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1:F2},{2}", p.x, p.z, mf.transform.root.name)); }
            log.Add(mf.transform.root.name + "/" + mf.name + ": " + gone.Count + " removed pieces");
        }
        System.IO.File.WriteAllLines(outPath, lines.ToArray()); return lines.Count + " removed pieces\n" + string.Join("\n", log.ToArray());
    }
    // plant centroids (connected components) of the SHRUBS chunks in the hole 10 box, plus active loose shrub instances -> x,z,layer lines
    public string DumpPlants()
    {
        var lines = new List<string>(); var box = AreaB();
        var sh = GameObject.Find("SHRUBS");
        if (sh) foreach (var mf in sh.GetComponentsInChildren<MeshFilter>())
            {
                var r = mf.GetComponent<Renderer>(); if (!r || !r.enabled || !mf.sharedMesh || !r.bounds.Intersects(box)) continue; var m = mf.sharedMesh; var v = m.vertices; var par = Enumerable.Range(0, v.Length).ToArray(); System.Func<int, int> Fd = null; Fd = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
                var used = new HashSet<int>(); for (int s = 0; s < m.subMeshCount; s++) { var tt = m.GetTriangles(s); for (int k = 0; k < tt.Length; k += 3) { int a = Fd(tt[k]); par[Fd(tt[k + 1])] = a; par[Fd(tt[k + 2])] = a; used.Add(tt[k]); used.Add(tt[k + 1]); used.Add(tt[k + 2]); } }
                var sum = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>(); foreach (var i in used) { int c = Fd(i); Vector3 sc; sum[c] = (sum.TryGetValue(c, out sc) ? sc : Vector3.zero) + mf.transform.TransformPoint(v[i]); int n0; cnt[c] = (cnt.TryGetValue(c, out n0) ? n0 : 0) + 1; }
                foreach (var kv in sum) { var p = kv.Value / cnt[kv.Key]; if (InH10(p)) lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1:F2},SHRUBS", p.x, p.z)); }
            }
        foreach (var rn in new[] { "LANDSCAPE_1029", "TALL_SHRUB_TREES" }) { var g = GameObject.Find(rn); if (!g) continue; foreach (var lg in g.GetComponentsInChildren<LODGroup>()) { if (!lg.gameObject.activeInHierarchy) continue; var n = lg.name; if (!(n.Contains("Sage") || n.Contains("Toyon") || n.Contains("Sumac") || n.Contains("Brush") || n.Contains("Buckwheat"))) continue; var p = lg.transform.position; if (InH10(p)) lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1:F2},{2}", p.x, p.z, rn + ":" + n)); } }
        System.IO.File.WriteAllLines(outPath, lines.ToArray()); return lines.Count + " plants dumped";
    }
}





