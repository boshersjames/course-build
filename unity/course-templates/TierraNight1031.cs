using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 10/31 night list (BoJa's flyover notes still open after the play test): 16a rogue fence, 18v4 smeared tree + course-wide tilt scan.
public class TierraNight1031
{
    public float x0 = 1200, x1 = 1500, z0 = 760, z1 = 880; public string roots = "FENCES";
    // every renderer-bearing child (first level under each root's groups) whose bounds touch the box
    public string List()
    {
        var o = new List<string>();
        foreach (var rn in roots.Split(','))
        {
            var r = GameObject.Find(rn); if (!r) { o.Add(rn + " missing"); continue; }
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null) continue; var rs = t.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                if (t.parent != r.transform && t.parent.parent != r.transform) continue;
                var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); if (b.max.x < x0 || b.min.x > x1 || b.max.z < z0 || b.min.z > z1) continue;
                string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
                o.Add(path + " active " + t.gameObject.activeInHierarchy + " " + b.min.ToString("F0") + ".." + b.max.ToString("F0") + " len " + new Vector2(b.size.x, b.size.z).magnitude.ToString("F1"));
            }
        }
        return o.Count + " objects\n" + string.Join("\n", o.ToArray());
    }
    // any object in any root whose name contains one of `words`, with bounds touching the box (all depths, grouped by top match)
    public string words = "fence,rail,post,slab,card,billboard,impostor,marker";
    public string Named()
    {
        var o = new List<string>(); var w = words.Split(',');
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>().Where(x => x.gameObject.scene.IsValid()))
        {
            var n = t.name.ToLower(); if (!w.Any(k => n.Contains(k))) continue; if (t.parent && w.Any(k => t.parent.name.ToLower().Contains(k))) continue;
            var rs = t.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
            if (b.max.x < x0 || b.min.x > x1 || b.max.z < z0 || b.min.z > z1) continue;
            string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(path + " active " + t.gameObject.activeInHierarchy + " " + b.min.ToString("F0") + ".." + b.max.ToString("F0") + " kids " + t.childCount + " mat " + (rs[0].sharedMaterial ? rs[0].sharedMaterial.name : "-"));
        }
        return o.Count + " objects\n" + string.Join("\n", o.Take(120).ToArray());
    }
    // every non-OPCD renderer whose bounds centre lies within r of (cx, cz), largest first
    public float cx = 1594, cz = 921, r = 30;
    public string Near()
    {
        var o = new List<KeyValuePair<float, string>>();
        foreach (var rd in Object.FindObjectsOfType<Renderer>())
        {
            var t = rd.transform; if (t.root.name == "OPCD Meshes" || t.root.name.StartsWith("Terrain")) continue; var b = rd.bounds; if (new Vector2(b.center.x - cx, b.center.z - cz).magnitude > r) continue;
            string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(new KeyValuePair<float, string>(b.size.magnitude, path + " " + b.min.ToString("F1") + ".." + b.max.ToString("F1") + " up " + Vector3.Angle(t.up, Vector3.up).ToString("F0") + " scl " + t.lossyScale.ToString("F2") + " mat " + (rd.sharedMaterial ? rd.sharedMaterial.name + "/" + (rd.sharedMaterial.mainTexture ? rd.sharedMaterial.mainTexture.name : "notex") : "null") + " " + rd.GetType().Name));
        }
        return string.Join("\n", o.OrderByDescending(k => k.Key).Take(60).Select(k => k.Value).ToArray());
    }
    // 16t: trees (LODGroups taller than 3 m) of `treeRoots` with trunks in the box move `move` m along (dx, dz) toward the cart path; the trunk
    // stops 1.5 m short of the path edge (raycast samples along the way), never onto mown turf; y re-seated on the ground. Recorded.
    public string treeRoots = "LIDAR_TREES,TALL_SHRUB_TREES"; public float dx = 0, dz = -1, move = 3; public bool apply = false;
    public string Toward()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); var dir = new Vector2(dx, dz).normalized;
        var log = apply ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        System.Func<Vector2, bool> PathNear = p => { for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; foreach (var h in Physics.RaycastAll(new Vector3(p.x + Mathf.Cos(a) * 1.5f, 400, p.y + Mathf.Sin(a) * 1.5f), Vector3.down, 800)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn == "ROAD_TR" || rn == "ROAD16") return true; } } foreach (var h in Physics.RaycastAll(new Vector3(p.x, 400, p.y), Vector3.down, 800)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn == "ROAD_TR" || rn == "ROAD16" || rn == "FENCES") return true; } return false; };
        System.Func<Vector2, bool> Mown = p => { string nm = ""; float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(p.x, 400, p.y), Vector3.down, 800)) { if (h.collider.transform.root != opcd) continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } } return TierraMownCut.Mown(nm); };
        foreach (var rn in treeRoots.Split(','))
        {
            var root = GameObject.Find(rn); if (!root) continue;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var t = lg.transform; var p = new Vector2(t.position.x, t.position.z); if (p.x < x0 || p.x > x1 || p.y < z0 || p.y > z1) continue;
                var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); if (b.size.y < 3f) continue;
                float got = 0; for (float m = 0.25f; m <= move + 0.001f; m += 0.25f) { var q = p + dir * m; if (PathNear(q) || Mown(q)) break; got = m; }
                var np = p + dir * got; o.Add(rn + "/" + t.name + " (" + p.x.ToString("F1") + "," + p.y.ToString("F1") + ") h " + b.size.y.ToString("F1") + " -> " + got.ToString("F2") + " m");
                if (apply && got > 0.2f)
                {
                    float gy = float.MinValue; foreach (var hh in Physics.RaycastAll(new Vector3(np.x, 400, np.y), Vector3.down, 800)) { if (hh.collider.transform.IsChildOf(t)) continue; if (hh.collider.transform.root == opcd || hh.collider is TerrainCollider) gy = Mathf.Max(gy, hh.point.y); }
                    var old = t.position; new GameObject("moved|16t|" + rn + "|" + t.name + "|" + old.x.ToString("F2") + "|" + old.y.ToString("F2") + "|" + old.z.ToString("F2")).transform.SetParent(log.transform, false);
                    t.position = new Vector3(np.x, gy > float.MinValue ? gy - 0.05f : old.y, np.y);
                }
            }
        }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return o.Count + " trees" + (apply ? " (applied)" : " (dry run)") + "\n" + string.Join("\n", o.ToArray());
    }
    // 18v2/18v4: SpeedTree BillboardRenderers draw as flat grey cards in GSPro. For every LODGroup holding one, drop the billboard level
    // (the last mesh LOD takes its cull height) and switch the billboard object off. Recorded; dry run unless apply.
    public string NoBillboards()
    {
        var o = new List<string>(); var log = apply ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        foreach (var lg in Object.FindObjectsOfType<LODGroup>())
        {
            var lods = lg.GetLODs(); int bi = System.Array.FindIndex(lods, l => l.renderers.Any(r => r is BillboardRenderer)); if (bi < 1) continue;
            string path = lg.name; for (var u = lg.transform.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(path + " (" + lg.transform.position.x.ToString("F0") + "," + lg.transform.position.z.ToString("F0") + ") billboard LOD " + bi + "/" + lods.Length);
            if (!apply) continue;
            var keep = lods.Take(bi).ToArray(); keep[keep.Length - 1].screenRelativeTransitionHeight = lods[lods.Length - 1].screenRelativeTransitionHeight;
            foreach (var r in lods[bi].renderers.Where(r => r is BillboardRenderer)) r.gameObject.SetActive(false);
            lg.SetLODs(keep); new GameObject("nobb|" + path).transform.SetParent(log.transform, false);
        }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return o.Count + " LODGroups with a SpeedTree billboard" + (apply ? " (fixed)" : " (dry run)") + "\n" + string.Join("\n", o.Take(40).ToArray());
    }
    // B2 (12 left greenside bunker, "5 yards wider into the hill"): list the bunkers round the green; Widen12 writes the outline grown
    // `grow` m on the side facing `hx, hz` (the hill), then rebuilds it with TierraBunkerRebuild.Apply (sand + surround heightfield)
    public float gx = 1247, gz = 845; public string bunker = ""; public float hx = 0, hz = 0, grow = 4.6f, depth = 0.9f, lip = 0.15f;
    public string Bunkers()
    {
        var o = new List<string>();
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).Where(f => f.name.EndsWith("Bunker_-_Mesh") && f.sharedMesh))
        { var b = mf.GetComponent<Renderer>().bounds; var d = new Vector2(b.center.x - gx, b.center.z - gz); if (d.magnitude > 45) continue; o.Add(mf.name + " centre (" + b.center.x.ToString("F1") + "," + b.center.z.ToString("F1") + ") size " + b.size.x.ToString("F1") + " x " + b.size.z.ToString("F1") + " dist " + d.magnitude.ToString("F1")); }
        return string.Join("\n", o.ToArray());
    }
    public string Widen()
    {
        var pre = bunker.Replace("Bunker_-_Mesh", "");
        var r = TierraBunkerRebuild.Outline(pre, "12left", depth, lip, new Vector2(hx, hz), grow);
        // one clean bowl, no nose: replace the outline by its convex hull, resampled every 0.4 m and rounded (Chaikin x3)
        var path = "AutomationOutput/fairways/bunker_12left.json"; var txt = System.IO.File.ReadAllText(path); int a = txt.IndexOf("\"poly\": [") + 9; int b = txt.LastIndexOf("]}");
        var pts = System.Text.RegularExpressions.Regex.Matches(txt.Substring(a, b - a), @"\[([-0-9.]+), ([-0-9.]+)\]").Cast<System.Text.RegularExpressions.Match>().Select(m => new Vector2(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture))).ToList();
        var srt = pts.OrderBy(q => q.x).ThenBy(q => q.y).ToList(); var h = new List<Vector2>(); System.Func<Vector2, Vector2, Vector2, float> Cr = (o2, p1, p2) => (p1.x - o2.x) * (p2.y - o2.y) - (p1.y - o2.y) * (p2.x - o2.x);
        foreach (var q in srt) { while (h.Count >= 2 && Cr(h[h.Count - 2], h[h.Count - 1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        int lo = h.Count + 1; for (int i = srt.Count - 2; i >= 0; i--) { var q = srt[i]; while (h.Count >= lo && Cr(h[h.Count - 2], h[h.Count - 1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); } h.RemoveAt(h.Count - 1);
        for (int it = 0; it < 5; it++) { var hq = new List<Vector2>(); for (int i = 0; i < h.Count; i++) { var p0 = h[i]; var p1 = h[(i + 1) % h.Count]; hq.Add(p0 * 0.75f + p1 * 0.25f); hq.Add(p0 * 0.25f + p1 * 0.75f); } h = hq; }
        var dense = new List<Vector2>(); for (int i = 0; i < h.Count; i++) { var p0 = h[i]; var p1 = h[(i + 1) % h.Count]; int n = Mathf.Max(1, Mathf.CeilToInt((p1 - p0).magnitude / 0.4f)); for (int k = 0; k < n; k++) dense.Add(Vector2.Lerp(p0, p1, k / (float)n)); }
        for (int it = 0; it < 3; it++) { var nq = new List<Vector2>(); for (int i = 0; i < dense.Count; i++) { var p0 = dense[i]; var p1 = dense[(i + 1) % dense.Count]; nq.Add(p0 * 0.75f + p1 * 0.25f); nq.Add(p0 * 0.25f + p1 * 0.75f); } dense = nq; }
        var fin = new List<Vector2> { dense[0] }; float acc = 0; for (int i = 1; i < dense.Count; i++) { acc += (dense[i] - dense[i - 1]).magnitude; if (acc >= 0.4f) { fin.Add(dense[i]); acc = 0; } }
        txt = txt.Substring(0, a) + string.Join(", ", fin.Select(q => "[" + q.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ", " + q.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "]").ToArray()) + txt.Substring(b);
        System.IO.File.WriteAllText(path, txt);
        float minx = fin.Min(q => q.x), maxx = fin.Max(q => q.x), minz = fin.Min(q => q.y), maxz = fin.Max(q => q.y);
        return r + "; hull " + fin.Count + " pts, " + (maxx - minx).ToString("F1") + " x " + (maxz - minz).ToString("F1") + " m\n" + (apply ? EvenApply() : "(outline only)");
    }
    // PH1: H13_PumpStation squared to the range net line (Net_1 first to last pole) and moved `move` m toward the 13 fairway (-z side),
    // never onto mown turf (footprint corners + centre checked); re-seated so the enclosure base sits 2 cm into the ground. Recorded.
    public string Pump()
    {
        Physics.SyncTransforms(); var root = GameObject.Find("VISIT_ITEMS_1029").transform.Find("H13_PumpStation"); var enc = root.Find("Enclosures"); var opcd = GameObject.Find("OPCD Meshes").transform;
        var poles = GameObject.Find("NETS").transform.Find("Net_1").GetComponentsInChildren<Transform>().Where(t => t.name == "Pole").Select(t => new Vector2(t.position.x, t.position.z)).ToList();
        var nd = (poles.OrderBy(p => p.x).Last() - poles.OrderBy(p => p.x).First()).normalized; float netYaw = Mathf.Atan2(nd.x, nd.y) * Mathf.Rad2Deg;
        var w = enc.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => enc.TransformPoint(v)).Select(v => new Vector2(v.x, v.z)).ToList(); var c = w.Aggregate(Vector2.zero, (s, v) => s + v) / w.Count;
        float sxx = 0, szz = 0, sxz = 0; foreach (var v in w) { var d = v - c; sxx += d.x * d.x; szz += d.y * d.y; sxz += d.x * d.y; } float ang = 0.5f * Mathf.Atan2(2 * sxz, sxx - szz); var ax = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); float encYaw = Mathf.Atan2(ax.x, ax.y) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(encYaw, netYaw); while (delta > 45) delta -= 90; while (delta < -45) delta += 90;
        var pivot = new Vector3(c.x, root.position.y, c.y); var log = apply ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        System.Func<Vector2, bool> Mown = p => { string nm = ""; float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(p.x, 400, p.y), Vector3.down, 800)) { if (h.collider.transform.root != opcd) continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } } return TierraMownCut.Mown(nm); };
        var bb = enc.GetComponent<Renderer>().bounds; string under = ""; { float yy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(c.x, 400, c.y + dz * 4), Vector3.down, 800)) { if (h.collider.transform.root != opcd) continue; if (h.point.y > yy) { yy = h.point.y; under = h.collider.name; } } }
        float got = -1; for (float m = 0f; m <= move + 0.001f; m += 0.25f) { bool bad = false; foreach (var q in new[] { new Vector2(bb.min.x, bb.min.z), new Vector2(bb.max.x, bb.min.z), new Vector2(bb.min.x, bb.max.z), new Vector2(bb.max.x, bb.max.z), c }) if (Mown(q + new Vector2(dx, dz).normalized * m)) bad = true; if (!bad) { got = m; break; } } if (got < 0) got = 0;
        string rep = "net yaw " + netYaw.ToString("F1") + ", enclosure yaw " + encYaw.ToString("F1") + " -> rotate " + delta.ToString("F1") + " deg; move " + got.ToString("F2") + " m toward (" + dx + "," + dz + "); 4 m that way is " + under;
        if (!apply) return rep + " (dry run)";
        new GameObject("pump|" + root.position.ToString("F3") + "|" + root.rotation.eulerAngles.ToString("F2")).transform.SetParent(log.transform, false);
        root.RotateAround(pivot, Vector3.up, delta); root.position += new Vector3(dx, 0, dz).normalized * got; Physics.SyncTransforms();
        var eb = enc.GetComponent<Renderer>().bounds; float gy = float.MinValue; foreach (var q in new[] { new Vector3(eb.min.x, 0, eb.min.z), new Vector3(eb.max.x, 0, eb.min.z), new Vector3(eb.min.x, 0, eb.max.z), new Vector3(eb.max.x, 0, eb.max.z), eb.center }) foreach (var h in Physics.RaycastAll(new Vector3(q.x, 400, q.z), Vector3.down, 800)) { if (h.collider.transform.IsChildOf(root)) continue; if (h.collider.transform.root == opcd || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); }
        float lift = gy > float.MinValue ? (gy - 0.02f) - eb.min.y : 0; if (Mathf.Abs(lift) < 1.5f) root.position += new Vector3(0, lift, 0);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return rep + "; re-seated " + lift.ToString("F2") + " m";
    }
    // S2: move HOLE_SIGNS_1029/<sign> (all parts) to (tx, tz), turned by the change in bearing to its tee (old spot -> tee vs new spot -> tee),
    // lowest part 3 cm into the ground; refuses a spot on path or tee turf (checked over the sign's footprint). Recorded.
    public string sign = "TR_Sign_H2"; public float tx = 664.3f, tz = 821.0f, teeX = 679.0f, teeZ = 812.6f;
    public string SignMove()
    {
        Physics.SyncTransforms(); var s = GameObject.Find("HOLE_SIGNS_1029").transform.Find(sign); if (!s) return sign + " not found"; var opcd = GameObject.Find("OPCD Meshes").transform;
        var rs = s.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var oldC = new Vector2(b.center.x, b.center.z); var tee = new Vector2(teeX, teeZ);
        float a0 = Mathf.Atan2((tee - oldC).x, (tee - oldC).y) * Mathf.Rad2Deg, a1 = Mathf.Atan2(tee.x - tx, tee.y - tz) * Mathf.Rad2Deg; float turn = Mathf.DeltaAngle(a0, a1);
        float hx2 = b.extents.x + 0.3f, hz2 = b.extents.z + 0.3f;
        System.Func<float, float, List<string>> Check = (cx2, cz2) => { var bl = new List<string>(); foreach (var q in new[] { new Vector2(0, 0), new Vector2(hx2, hz2), new Vector2(-hx2, hz2), new Vector2(hx2, -hz2), new Vector2(-hx2, -hz2) }) foreach (var h in Physics.RaycastAll(new Vector3(cx2 + q.x, 400, cz2 + q.y), Vector3.down, 800)) { if (h.collider.transform.IsChildOf(s)) continue; var rn = h.collider.transform.root.name; var n = h.collider.name.ToLower(); if (rn == "CART_PATHS" || rn.StartsWith("ROAD") || n.Contains("tee")) bl.Add(rn + "/" + h.collider.name); } return bl; };
        float gx0 = tx, gz0 = tz; var bad = Check(tx, tz);
        if (bad.Count > 0) { bool found = false; for (float rr = 0.25f; rr <= 3f && !found; rr += 0.25f) for (int k = 0; k < 24 && !found; k++) { float an = k * Mathf.PI / 12; float cx2 = gx0 + Mathf.Cos(an) * rr, cz2 = gz0 + Mathf.Sin(an) * rr; if (Check(cx2, cz2).Count == 0) { tx = cx2; tz = cz2; found = true; } } if (found) bad.Clear(); }        string rep = sign + " at (" + oldC.x.ToString("F1") + "," + oldC.y.ToString("F1") + ") -> (" + tx.ToString("F2") + "," + tz.ToString("F2") + ") [pin " + gx0 + "," + gz0 + ", off " + new Vector2(tx - gx0, tz - gz0).magnitude.ToString("F2") + " m], turn " + turn.ToString("F0") + " deg";
        if (bad.Count > 0) return rep + ": spot touches " + string.Join(", ", bad.Distinct().ToArray());
        if (!apply) return rep + " (dry run)";
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("sign|" + sign + "|" + s.position.ToString("F3") + "|" + s.rotation.eulerAngles.ToString("F2")).transform.SetParent(log.transform, false);
        s.RotateAround(new Vector3(oldC.x, s.position.y, oldC.y), Vector3.up, turn); s.position += new Vector3(tx - oldC.x, 0, tz - oldC.y); Physics.SyncTransforms();
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); float gy = float.MaxValue;
        foreach (var q in new[] { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z), new Vector2(b.center.x, b.center.z) }) { float top = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 400, q.y), Vector3.down, 800)) { if (h.collider.transform.IsChildOf(s)) continue; if (h.collider.transform.root == opcd || h.collider is TerrainCollider) top = Mathf.Max(top, h.point.y); } if (top > float.MinValue) gy = Mathf.Min(gy, top); }
        float dy = gy < float.MaxValue ? (gy - 0.03f) - b.min.y : 0; s.position += new Vector3(0, dy, 0);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s.gameObject.scene); return rep + "; seated " + dy.ToString("F2") + " m";
    }
    // T2: every LODGroup within r of (cx, cz): its LOD levels (renderer names, transition heights) and size
    public string Lods()
    {
        var o = new List<string>();
        foreach (var lg in Object.FindObjectsOfType<LODGroup>())
        {
            var p = lg.transform.position; if (new Vector2(p.x - cx, p.z - cz).magnitude > r) continue; var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); if (b.size.y < 3) continue;
            string path = lg.name; for (var u = lg.transform.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(path + " (" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ") h " + b.size.y.ToString("F1") + " scl " + lg.transform.lossyScale.ToString("F2") + " LODs: " + string.Join(" | ", lg.GetLODs().Select(l => l.screenRelativeTransitionHeight.ToString("F3") + " " + string.Join("+", l.renderers.Where(x => x).Select(x => x.name + (x.sharedMaterial ? "[" + x.sharedMaterial.name + "]" : "")).ToArray())).ToArray()));
        }
        return string.Join("\n", o.ToArray());
    }
    // W2: plants (any LODGroup) whose trunk is on the turf side of a retaining wall, or within 1 m of it, move to 1.5-3 m behind the wall
    // (the side away from mown turf), keeping their position along the wall; never onto mown turf or a path. Walls = `walls` (root/child paths).
    public string walls = "WALLS/Wall_0,VISIT_ITEMS_1029/H12_StoneWall"; public float wallBand = 3.5f;
    public string WallTrees()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); int moved = 0, stuck = 0; var log = apply ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        System.Func<Vector2, bool> Mown = p => { string nm = ""; float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(p.x, 400, p.y), Vector3.down, 800)) { if (h.collider.transform.root != opcd) continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } } return TierraMownCut.Mown(nm); };
        System.Func<Vector2, bool> Path = p => { foreach (var h in Physics.RaycastAll(new Vector3(p.x, 400, p.y), Vector3.down, 800)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD")) return true; } return false; };
        foreach (var wpath in walls.Split(','))
        {
            var parts = wpath.Split('/'); var r0 = GameObject.Find(parts[0]); if (!r0) { o.Add(wpath + " missing"); continue; } var w = parts.Length > 1 ? r0.transform.Find(parts[1]) : r0.transform; if (!w) { o.Add(wpath + " missing"); continue; }
            var pts = w.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).Select(v => new Vector2(v.x, v.z)).ToList();
            var wb = w.GetComponentsInChildren<Renderer>()[0].bounds; foreach (var rr in w.GetComponentsInChildren<Renderer>()) wb.Encapsulate(rr.bounds);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.activeInHierarchy || root.name == "OPCD Meshes") continue;
                foreach (var lg in root.GetComponentsInChildren<LODGroup>())
                {
                    var t = lg.transform; var p = new Vector2(t.position.x, t.position.z); if (p.x < wb.min.x - wallBand - 1 || p.x > wb.max.x + wallBand + 1 || p.y < wb.min.z - wallBand - 1 || p.y > wb.max.z + wallBand + 1) continue;
                    var near = pts.OrderBy(q => (q - p).sqrMagnitude).First(); float d = (near - p).magnitude; if (d > wallBand) continue;
                    var loc = pts.Where(q => (q - near).sqrMagnitude < 9).ToList(); var c = loc.Aggregate(Vector2.zero, (s, v) => s + v) / loc.Count; float sxx = 0, szz = 0, sxz = 0; foreach (var v in loc) { var dd = v - c; sxx += dd.x * dd.x; szz += dd.y * dd.y; sxz += dd.x * dd.y; } float an = 0.5f * Mathf.Atan2(2 * sxz, sxx - szz); var tg = new Vector2(Mathf.Cos(an), Mathf.Sin(an)); var n = new Vector2(-tg.y, tg.x);
                    bool mA = false, mB = false; foreach (var dist in new[] { 2.5f, 5f, 8f, 12f, 18f, 25f }) { mA = Mown(c + n * dist); mB = Mown(c - n * dist); if (mA != mB) break; } if (mA == mB) { o.Add(t.name + " at (" + p.x.ToString("F1") + "," + p.y.ToString("F1") + "): turf side unclear (" + mA + "/" + mB + "), left"); continue; }
                    var turf = mA ? n : -n; float side = Vector2.Dot(p - c, turf); if (side < 0 && d >= 1f) continue;
                    string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
                    float along = Vector2.Dot(p - c, tg); Vector2 np = Vector2.zero; bool ok = false; foreach (var back in new[] { 1.5f, 2f, 2.5f, 3f }) { var q = c + tg * along - turf * back; if (!Mown(q) && !Path(q)) { np = q; ok = true; break; } }
                    o.Add(wpath + ": " + path + " at (" + p.x.ToString("F1") + "," + p.y.ToString("F1") + ") " + (side >= 0 ? "turf side" : "behind") + ", " + d.ToString("F1") + " m from wall -> " + (ok ? "(" + np.x.ToString("F1") + "," + np.y.ToString("F1") + ")" : "no clear spot behind"));
                    if (!ok) { stuck++; continue; } if (!apply) continue;
                    float gy = float.MinValue; foreach (var hh in Physics.RaycastAll(new Vector3(np.x, 400, np.y), Vector3.down, 800)) { if (hh.collider.transform.IsChildOf(t) || hh.collider.transform.IsChildOf(w)) continue; if (hh.collider.transform.root == opcd || hh.collider is TerrainCollider) gy = Mathf.Max(gy, hh.point.y); }
                    new GameObject("wall|" + path + "|" + t.position.ToString("F3")).transform.SetParent(log.transform, false); t.position = new Vector3(np.x, gy > float.MinValue ? gy - 0.05f : t.position.y, np.y); moved++;
                }
            }
        }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return o.Count + " plants listed" + (apply ? ", " + moved + " moved" : " (dry run)") + ", " + stuck + " with no spot\n" + string.Join("\n", o.ToArray());
    }
    // materials (and their main colours / textures / shader) of the OPCD meshes whose names contain any of `words`
    public string Mats()
    {
        var o = new List<string>(); var w = words.Split(',');
        foreach (var r in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!w.Any(k => r.name.Contains(k))) continue;
            o.Add(r.name + " active " + r.gameObject.activeInHierarchy + ": " + string.Join(" | ", r.sharedMaterials.Where(m => m).Select(m => m.name + " [" + m.shader.name + "] " + string.Join(" ", new[] { "_Color", "_RedOverallColor", "_GreenOverallColor", "_BlueOverallColor", "_OverallColor" }.Where(p => m.HasProperty(p)).Select(p => p + "=" + m.GetColor(p).ToString("F2")).ToArray()) + (m.mainTexture ? " tex " + m.mainTexture.name : "") + " path " + UnityEditor.AssetDatabase.GetAssetPath(m)).ToArray()));
        }
        return string.Join("\n", o.ToArray());
    }
    // renderers under `roots` (any depth) whose bounds touch the box: name, bounds, material
    public string RendersIn()
    {
        var o = new List<string>();
        foreach (var rn in roots.Split(','))
        {
            var r0 = GameObject.Find(rn); if (!r0) continue;
            foreach (var r in r0.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds; if (b.max.x < x0 || b.min.x > x1 || b.max.z < z0 || b.min.z > z1) continue; string path = r.name; for (var u = r.transform.parent; u; u = u.parent) path = u.name + "/" + path;
                o.Add(path + " active " + r.gameObject.activeInHierarchy + " " + b.min.ToString("F0") + ".." + b.max.ToString("F0") + " mat " + (r.sharedMaterial ? r.sharedMaterial.name : "-"));
            }
        }
        return o.Count + "\n" + string.Join("\n", o.Take(80).ToArray());
    }
    // UV units per world metre (median over triangles) and vertex-colour mean for each OPCD mesh in `words`
    public string UvDensity()
    {
        var o = new List<string>();
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(true).Where(f => words.Split(',').Contains(f.name) && f.sharedMesh))
        {
            var m = mf.sharedMesh; var v = m.vertices; var uv = m.uv; var t = m.triangles; var col = m.colors; var dens = new List<float>();
            for (int i = 0; i + 2 < t.Length && dens.Count < 4000; i += 3 * Mathf.Max(1, t.Length / 12000)) { var a = mf.transform.TransformPoint(v[t[i]]); var b = mf.transform.TransformPoint(v[t[i + 1]]); float w = (a - b).magnitude; if (w < 0.01f || uv.Length == 0) continue; dens.Add((uv[t[i]] - uv[t[i + 1]]).magnitude / w); }
            dens.Sort(); var cm = col.Length > 0 ? new Color(col.Average(c => c.r), col.Average(c => c.g), col.Average(c => c.b), col.Average(c => c.a)) : Color.clear;
            o.Add(mf.name + ": uv/m " + (dens.Count > 0 ? dens[dens.Count / 2].ToString("F3") : "-") + ", colours " + (col.Length > 0 ? cm.ToString("F2") : "none") + ", verts " + v.Length);
        }
        return string.Join("\n", o.ToArray());
    }
    // Plan item 8: the cloud clubhouse shell (Props/clubhouse/TR_Clubhouse.fbx) at (1222, 59.68, 1020) rot Y -13 under CLUBHOUSE_1101; the
    // Oct 29 CLUBHOUSE building shell (walls, roof well, timber, frames, chimney, porte pillars) switched off; patio, carts, flag kept.
    public string Clubhouse()
    {
        var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/clubhouse/TR_Clubhouse.fbx"); if (!fbx) return "fbx not imported";
        var old = GameObject.Find("CLUBHOUSE_1101"); if (old) Object.DestroyImmediate(old);
        var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx); go.name = "CLUBHOUSE_1101"; go.transform.position = new Vector3(1222.0f, 59.68f, 1020.0f); go.transform.rotation = Quaternion.Euler(0, -13, 0);
        var ch = GameObject.Find("CLUBHOUSE").transform; var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var n in new[] { "Walls", "Roof_Well", "Timber", "Frames", "Chimney", "Chimney_Cap", "Porte_Cochere_Pillars", "Roof", "Glass" }) { var t = ch.Find(n); if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); new GameObject("off|clubhouse|CLUBHOUSE/" + n).transform.SetParent(log.transform, false); o.Add(n); } }
        var colFbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/clubhouse/TR_Clubhouse_COL.fbx");
        if (colFbx) { var cg = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(colFbx); cg.transform.SetParent(go.transform, false); cg.transform.localPosition = Vector3.zero; cg.transform.localRotation = Quaternion.identity; foreach (var mf in cg.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<MeshCollider>()) { var c = mf.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = mf.sharedMesh; c.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/GSPstone"); } foreach (var mr in cg.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(mr); }
        var rnc = GameObject.Find("H18I7") ? GameObject.Find("H18I7").transform.Find("RoofNoCupola") : null; if (rnc && rnc.gameObject.activeSelf) { rnc.gameObject.SetActive(false); new GameObject("off|clubhouse|H18I7/RoofNoCupola").transform.SetParent((GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")).transform, false); }
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene); return "placed CLUBHOUSE_1101, bounds " + b.min.ToString("F1") + ".." + b.max.ToString("F1") + ", " + go.GetComponentsInChildren<MeshCollider>().Length + " colliders; old shell off: " + string.Join(", ", o.ToArray()) + "; materials " + string.Join(", ", rs.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name + (m.mainTexture ? "" : "(no tex)")).Distinct().ToArray());
    }
    // water planes (any active renderer whose name contains Water) with their bounds; ShoreRibbon builds PondShoreRibbon strips on `ids`
    public string WaterPlanes()
    {
        return string.Join("\n", Object.FindObjectsOfType<MeshRenderer>().Where(r => r.name.Contains("Water") || r.name.Contains("water")).Select(r => { string p = r.name; for (var u = r.transform.parent; u; u = u.parent) p = u.name + "/" + p; return p + " " + r.bounds.min.ToString("F0") + ".." + r.bounds.max.ToString("F0") + " mat " + (r.sharedMaterial ? r.sharedMaterial.name : "-"); }).ToArray());
    }
    public string ShoreRibbon()
    {
        var gos = new List<Object>(); foreach (var p in ids.Split('|').Where(s => s.Length > 0)) { var parts = p.Split('/'); var r0 = GameObject.Find(parts[0]); var t = r0 ? (parts.Length > 1 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r0.transform) : null; if (t) gos.Add(t.gameObject); }
        UnityEditor.Selection.objects = gos.ToArray(); var mi = typeof(PondShoreRibbon).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); mi.Invoke(null, new object[] { matPath });
        var ps = GameObject.Find("POND_SHORE"); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return gos.Count + " planes; POND_SHORE children: " + (ps ? string.Join(", ", ps.transform.Cast<Transform>().Select(t => t.name).ToArray()) : "none");
    }
    // rebuild one bunker on its own outline (no grow, no hull) with an even low lip (bunker skill: lip 0.12-0.15 m, no long-grass wall)
    public string tag2 = "16b5";
    public string RebuildLip()
    {
        var pre = bunker.Replace("Bunker_-_Mesh", ""); var r = TierraBunkerRebuild.Outline(pre, tag2, depth, lip, new Vector2(gx, gz), 0f);
        TierraBunkerRebuild.EvenRim = true; try { r += "\n" + TierraBunkerRebuild.Apply(tag2); } finally { TierraBunkerRebuild.EvenRim = false; }
        r += "\n" + TierraTerrainUnderSand.Run(new[] { "Spline_bunker_" + tag2 + "_Bunker_-_Mesh" }, null); return r;
    }
    // 16L: fill gaps in the road-side tree line (x x0..x1, z z0..z1) with copies of existing LIDAR_TREES eucalyptus/oak/pepper (prefab
    // source of a real instance), 7 m grid jittered, only where no tree trunk is within `gap` m, the ground is rough (not mown, path, road,
    // fence, tee) and 1.5 m clear of road/path/fence. Heights 0.9-1.2x. Under ROADLINE_1101 so it can be switched off as one.
    public float gap = 6f;
    public string TreeLine()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var lt = GameObject.Find("LIDAR_TREES").transform;
        var src = new[] { "TR_Eucalyptus_A", "TR_Eucalyptus_B", "TR_Eucalyptus_B", "TR_Oak_B", "TR_Pepper_A" }.Select(n => lt.Cast<Transform>().FirstOrDefault(t => t.name == n)).Where(t => t).Select(t => new KeyValuePair<GameObject, Vector3>((GameObject)UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) ?? t.gameObject, t.localScale)).ToArray();
        if (src.Length == 0) return "no source trees";
        var trunks = Object.FindObjectsOfType<LODGroup>().Where(l => l.gameObject.activeInHierarchy).Select(l => new Vector2(l.transform.position.x, l.transform.position.z)).Where(p => p.x > x0 - 10 && p.x < x1 + 10 && p.y > z0 - 10 && p.y < z1 + 10).ToList();
        System.Func<Vector2, bool> Ok = q => { string nm = ""; float y = float.MinValue; for (int k = 0; k < 9; k++) { var o = k == 0 ? Vector2.zero : new Vector2(Mathf.Cos(k * Mathf.PI / 4), Mathf.Sin(k * Mathf.PI / 4)) * 1.5f; foreach (var h in Physics.RaycastAll(new Vector3(q.x + o.x, 600, q.y + o.y), Vector3.down, 1200)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD") || rn == "FENCES") return false; if (k == 0 && h.collider.transform.root == opcd && h.point.y > y) { y = h.point.y; nm = h.collider.name; } } } return nm.Length > 0 && !TierraMownCut.Mown(nm) && !nm.ToLower().Contains("tee"); };
        var parent = GameObject.Find("ROADLINE_1101") ?? new GameObject("ROADLINE_1101"); var rnd = new System.Random(1101); int planted = 0;
        for (float x = x0; x <= x1; x += 7f) for (float z = z0; z <= z1; z += 7f)
        {
            var q = new Vector2(x + (float)rnd.NextDouble() * 3 - 1.5f, z + (float)rnd.NextDouble() * 3 - 1.5f); if (trunks.Any(t => (t - q).sqrMagnitude < gap * gap) || !Ok(q)) continue;
            float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) if (h.collider.transform.root == opcd || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); if (gy == float.MinValue) continue;
            var s = src[rnd.Next(src.Length)]; var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(s.Key) ?? Object.Instantiate(s.Key); go.transform.SetParent(parent.transform, true);
            go.transform.position = new Vector3(q.x, gy - 0.05f, q.y); go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0); go.transform.localScale = s.Value * (0.9f + 0.3f * (float)rnd.NextDouble()); trunks.Add(q); planted++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(parent.scene); return planted + " trees planted under ROADLINE_1101 from " + src.Length + " sources";
    }
    // 10 collar slits: the 17/18 smooth-collar rebuild applied to 10 (private Run called through reflection; the original collar mesh name
    // is the current collar mesh's)
    public string Collar10()
    {
        var col = Object.FindObjectsOfType<MeshFilter>().First(f => f.name == "SPatch_collar1029_h10_-_Mesh"); var orig = col.sharedMesh.name.Replace("_smooth", "").Replace("_tone", "");
        var mi = typeof(TierraCollarSmooth).GetMethod("Run", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(string), typeof(string) }, null);
        return (string)mi.Invoke(null, new object[] { "Spline_green_3_Green_-_Mesh", "SPatch_collar1029_h10_-_Mesh", "Spline_rough_10collarR_Rough_-_Mesh", orig });
    }
    // ASCII map of path colliders (CART_PATHS/ROAD_TR) over the box x0..x1, z0..z1 at `step`: a letter per collider, '.' none; north row first
    public string PathMap()
    {
        Physics.SyncTransforms(); var names = new List<string>(); var rows = new List<string>();
        for (float z = z1; z >= z0; z -= step)
        {
            var sb = new System.Text.StringBuilder(); sb.Append(z.ToString("F0").PadLeft(5) + " ");
            for (float x = x0; x <= x1; x += step)
            {
                Collider best = null; float by = float.MinValue;
                foreach (var h in Physics.RaycastAll(new Vector3(x, 400, z), Vector3.down, 800)) { var rn = h.collider.transform.root.name; if ((rn == "CART_PATHS" || rn == "ROAD_TR") && h.point.y > by) { by = h.point.y; best = h.collider; } }
                if (!best) { sb.Append('.'); continue; }
                string path = best.name; for (var u = best.transform.parent; u; u = u.parent) path = u.name + "/" + path;
                int i = names.IndexOf(path); if (i < 0) { names.Add(path); i = names.Count - 1; } sb.Append((char)('A' + i));
            }
            rows.Add(sb.ToString());
        }
        return "x from " + x0 + " step " + step + "\n" + string.Join("\n", rows.ToArray()) + "\n" + string.Join("\n", names.Select((n, i) => (char)('A' + i) + " " + n).ToArray());
    }
    // 8b: turf slivers/holes between cart path pieces. In the box x0..x1, z0..z1, every `step` cell with no path under it but path on both
    // sides (an opposite pair of the 8 directions, each within `reach` m) gets a concrete quad at the mean of those two path heights - 1 cm,
    // path material, MeshCollider with the neighbour's physic material. One object per run under CART_PATHS ("PathFill_<tag>").
    public float reach = 1.25f;
    public string FillPathGaps()
    {
        Physics.SyncTransforms(); var root = GameObject.Find("CART_PATHS");
        System.Func<float, float, RaycastHit?> PH = (x, z) => { RaycastHit? b = null; foreach (var h in Physics.RaycastAll(new Vector3(x, 400, z), Vector3.down, 800)) { var rn = h.collider.transform.root.name; if ((rn == "CART_PATHS" || rn == "ROAD_TR") && !h.collider.name.ToLower().Contains("curb") && (b == null || h.point.y > b.Value.point.y)) b = h; } return b; };
        var dirs = new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f) };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); Collider src = null; float hs = step / 2;
        for (float x = x0; x <= x1; x += step) for (float z = z0; z <= z1; z += step)
        {
            if (PH(x, z) != null) continue; float y = float.NaN;
            foreach (var d in dirs)
            {
                RaycastHit? a = null, b = null;
                for (float s = step; s <= reach && a == null; s += step) a = PH(x + d.x * s, z + d.y * s);
                for (float s = step; s <= reach && b == null; s += step) b = PH(x - d.x * s, z - d.y * s);
                if (a != null && b != null) { y = (a.Value.point.y + b.Value.point.y) / 2 - 0.01f; if (!src) src = a.Value.collider; break; }
            }
            if (float.IsNaN(y)) continue; int i = v.Count;
            v.Add(new Vector3(x - hs, y, z - hs)); v.Add(new Vector3(x - hs, y, z + hs)); v.Add(new Vector3(x + hs, y, z + hs)); v.Add(new Vector3(x + hs, y, z - hs));
            foreach (var p in v.Skip(i)) uv.Add(new Vector2(p.x / 3f, p.z / 3f)); t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        if (v.Count == 0) return "no gaps";
        var m = new Mesh { name = "PathFill_" + tag }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/pathfill")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "pathfill");
        var ap = "Assets/TierraRejada/generated/pathfill/PathFill_" + tag + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var old = root.transform.Find("PathFill_" + tag); if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("PathFill_" + tag); go.transform.SetParent(root.transform, false); go.AddComponent<MeshFilter>().sharedMesh = m;
        var sr = src.GetComponent<Renderer>(); go.AddComponent<MeshRenderer>().sharedMaterial = sr ? sr.sharedMaterial : null;
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.sharedMaterial = src.sharedMaterial;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return (t.Count / 6) + " cells (" + (t.Count / 6 * step * step).ToString("F1") + " m2) filled from " + src.name + ", mat " + (sr ? sr.sharedMaterial.name : "-") + ", phys " + (src.sharedMaterial ? src.sharedMaterial.name : "-");
    }
    // trunks (LODGroups under `treeRoots`) in the box x0..x1, z0..z1: distance to the nearest path/road collider (0.25 m rings to 3 m); with
    // apply, a trunk closer than 1.5 m steps straight away from that path point until 1.5 m clear (re-seated on the ground, NIGHT1031_LOG)
    public string PathClear()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        System.Func<float, float, bool> OnPath = (x, z) => Physics.RaycastAll(new Vector3(x, 400, z), Vector3.down, 800).Any(h => { var rn = h.collider.transform.root.name; return (rn == "CART_PATHS" || rn.StartsWith("ROAD")) && h.collider.gameObject.activeInHierarchy; });
        System.Func<Vector2, Vector2?> Nearest = q => { for (float r = 0; r <= 3f; r += 0.25f) for (int k = 0; k < (r == 0 ? 1 : 24); k++) { float a = k * Mathf.PI / 12; var p = q + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; if (OnPath(p.x, p.y)) return p; } return null; };
        foreach (var rn in treeRoots.Split(',')) { var root = GameObject.Find(rn); if (!root) continue;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var t = lg.transform; var c = new Vector2(t.position.x, t.position.z); if (c.x < x0 || c.x > x1 || c.y < z0 || c.y > z1) continue;
                var np = Nearest(c); float d = np.HasValue ? (np.Value - c).magnitude : 99; if (d >= 1.5f) continue;
                string line = rn + "/" + t.name + " (" + c.x.ToString("F1") + "," + c.y.ToString("F1") + ") " + d.ToString("F2") + " m from a path";
                if (apply)
                {
                    var away = (c - np.Value).sqrMagnitude > 1e-4f ? (c - np.Value).normalized : new Vector2(0, 1); var q = c;
                    for (float s = 0.25f; s <= 6; s += 0.25f) { q = c + away * s; var n2 = Nearest(q); if (!n2.HasValue || (n2.Value - q).magnitude >= 1.5f) break; }
                    float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { if (h.collider.transform.IsChildOf(t)) continue; if (h.collider.transform.root.name == "OPCD Meshes" || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); }
                    new GameObject("move|" + t.name + "|" + t.position.ToString("F3")).transform.SetParent(log.transform, false); t.position = new Vector3(q.x, gy > float.MinValue ? gy - 0.05f : t.position.y, q.y); line += " -> moved " + (q - c).magnitude.ToString("F2") + " m";
                }
                o.Add(line);
            } }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return o.Count + " trunks within 1.5 m of a path\n" + string.Join("\n", o.ToArray());
    }
    // green-edge slits (17c, 18g, 10r): a skirt strip under the green's outer edge, `skirtW` m wide, top 3 mm under the green edge and
    // dropping 3 cm outward, in the collar's material with uv / uv2 / vertex colour taken from the nearest collar vertex. Gaps between the
    // green and the collar then show collar instead of black. Green and collar meshes are untouched; no collider. "GreenSkirt_<tag>" under
    // GREEN_SKIRTS; switch it off to undo.
    public string greenName = "", collarName = ""; public float skirtW = 0.35f;
    public string GreenSkirt()
    {
        var all = Object.FindObjectsOfType<MeshFilter>(); var g = all.FirstOrDefault(f => f.name == greenName); var c = all.FirstOrDefault(f => f.name == collarName);
        if (!g || !c) return "missing " + (!g ? greenName : collarName);
        var gm = g.sharedMesh; var gv = gm.vertices.Select(q => g.transform.TransformPoint(q)).ToArray(); var gt = gm.triangles;
        var cnt = new Dictionary<long, int>(); var third = new Dictionary<long, int>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        // weld green vertices by position so split normals don't fake a boundary
        var key = new Dictionary<Vector3Int, int>(); var id = new int[gv.Length]; for (int i = 0; i < gv.Length; i++) { var k3 = new Vector3Int(Mathf.RoundToInt(gv[i].x * 1000), Mathf.RoundToInt(gv[i].y * 1000), Mathf.RoundToInt(gv[i].z * 1000)); int j; if (!key.TryGetValue(k3, out j)) { j = i; key[k3] = i; } id[i] = j; }
        for (int i = 0; i < gt.Length; i += 3) for (int e = 0; e < 3; e++) { int a = id[gt[i + e]], b = id[gt[i + (e + 1) % 3]], o = id[gt[i + (e + 2) % 3]]; var kk = K(a, b); cnt[kk] = cnt.ContainsKey(kk) ? cnt[kk] + 1 : 1; third[kk] = o; }
        var cm = c.sharedMesh; var cv = cm.vertices.Select(q => c.transform.TransformPoint(q)).ToArray(); var cuv = cm.uv; var cuv2 = cm.uv2; var ccol = cm.colors;
        System.Func<Vector3, int> NearC = p => { int best = 0; float bd = float.MaxValue; for (int i = 0; i < cv.Length; i++) { float d = (cv[i].x - p.x) * (cv[i].x - p.x) + (cv[i].z - p.z) * (cv[i].z - p.z); if (d < bd) { bd = d; best = i; } } return best; };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var col = new List<Color>(); var t = new List<int>(); int edges = 0;
        foreach (var kv in cnt.Where(q => q.Value == 1))
        {
            int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff); var A = gv[a]; var B = gv[b]; var O = gv[third[kv.Key]];
            var d2 = new Vector2(B.x - A.x, B.z - A.z); if (d2.magnitude < 1e-4f) continue; var n = new Vector2(-d2.y, d2.x).normalized; var mid = new Vector2((A.x + B.x) / 2, (A.z + B.z) / 2);
            if (Vector2.Dot(n, new Vector2(O.x, O.z) - mid) > 0) n = -n; // outward
            var pts = new[] { A - Vector3.up * 0.003f, B - Vector3.up * 0.003f, B + new Vector3(n.x, 0, n.y) * skirtW - Vector3.up * 0.033f, A + new Vector3(n.x, 0, n.y) * skirtW - Vector3.up * 0.033f };
            int i0 = v.Count; foreach (var p in pts) { int k = NearC(p); v.Add(p); uv.Add(cuv.Length > k ? cuv[k] : Vector2.zero); uv2.Add(cuv2.Length > k ? cuv2[k] : Vector2.zero); col.Add(ccol.Length > k ? ccol[k] : Color.white); }
            t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3, i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 }); edges++; // both faces
        }
        if (edges == 0) return "no boundary";
        var m = new Mesh { name = "GreenSkirt_" + tag }; if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; m.SetVertices(v); m.SetUVs(0, uv); m.SetUVs(1, uv2); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // normals straight up so both faces light like the turf
        m.normals = Enumerable.Repeat(Vector3.up, v.Count).ToArray();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/greenskirt")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "greenskirt");
        var ap = "Assets/TierraRejada/generated/greenskirt/GreenSkirt_" + tag + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var root = GameObject.Find("GREEN_SKIRTS") ?? new GameObject("GREEN_SKIRTS"); var old = root.transform.Find("GreenSkirt_" + tag); if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("GreenSkirt_" + tag); go.transform.SetParent(root.transform, false); go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = c.GetComponent<Renderer>().sharedMaterial;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return edges + " green edge segments skirted (" + greenName + "), collar " + collarName + " mat " + c.GetComponent<Renderer>().sharedMaterial.name;
    }
    // brush clearing (11a): in every mesh under the `roots` objects ("A/B,C"), whole plants (connected pieces of the combined mesh) whose
    // centre lies inside the polygon `poly` ("x,z;x,z;...") are dropped. A-owned copies in generated/brushclear (original mesh name kept in
    // BRUSHCLEAR_LOG for undo); colliders that used the old mesh follow. Trees (LODGroups) are not touched.
    public string poly = ""; public bool triMode = false;
    public string ClearBrush()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var P = poly.Split(';').Where(q => q.Length > 0).Select(q => { var a = q.Split(','); return new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci)); }).ToArray();
        System.Func<Vector2, bool> In = q => { bool r = false; for (int i = 0, j = P.Length - 1; i < P.Length; j = i++) if ((P[i].y > q.y) != (P[j].y > q.y) && q.x < (P[j].x - P[i].x) * (q.y - P[i].y) / (P[j].y - P[i].y) + P[i].x) r = !r; return r; };
        var o = new List<string>(); var log = GameObject.Find("BRUSHCLEAR_LOG") ?? new GameObject("BRUSHCLEAR_LOG");
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/brushclear")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "brushclear");
        foreach (var rp in roots.Split(',').Where(q => q.Length > 0))
        {
            var parts = rp.Split('/'); var r0 = GameObject.Find(parts[0]); var root = r0 ? (parts.Length > 1 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r0.transform) : null; if (!root) { o.Add("no " + rp); continue; }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                { var lg = mf.GetComponentInParent<LODGroup>(); if (lg && lg.name.StartsWith("TR_")) continue; }
                var src = mf.sharedMesh; if (!src) continue; var b = mf.GetComponent<Renderer>().bounds; if (b.min.x > P.Max(q => q.x) || b.max.x < P.Min(q => q.x) || b.min.z > P.Max(q => q.y) || b.max.z < P.Min(q => q.y)) continue;
                var v = src.vertices; var w = v.Select(q => mf.transform.TransformPoint(q)).ToArray();
                // union-find over vertices by triangles, then by position (split vertices)
                var par = Enumerable.Range(0, v.Length).ToArray(); System.Func<int, int> F = null; F = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
                var subs = new List<int[]>(); for (int s = 0; s < src.subMeshCount; s++) subs.Add(src.GetTriangles(s));
                foreach (var t in subs) for (int i = 0; i < t.Length; i += 3) { par[F(t[i + 1])] = F(t[i]); par[F(t[i + 2])] = F(t[i]); }
                var pos = new Dictionary<Vector3Int, int>(); for (int i = 0; i < w.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].y * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (pos.TryGetValue(k, out j)) par[F(i)] = F(j); else pos[k] = i; }
                var sum = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>(); for (int i = 0; i < w.Length; i++) { int r = F(i); Vector3 sv; sum.TryGetValue(r, out sv); sum[r] = sv + w[i]; int c; cnt.TryGetValue(r, out c); cnt[r] = c + 1; }
                var drop = new HashSet<int>(sum.Keys.Where(r => { var c = sum[r] / cnt[r]; return In(new Vector2(c.x, c.z)); }));
                bool tri = triMode && (mf.name.Contains("cover") || mf.name.Contains("_toe")); // ground sheets: cut triangles, not whole pieces
                if (drop.Count == 0 && !tri) continue; int dt = 0;
                var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_bc";
                for (int s = 0; s < subs.Count; s++) { var keep = new List<int>(); var t = subs[s]; for (int i = 0; i < t.Length; i += 3) { if (tri ? In(new Vector2((w[t[i]].x + w[t[i + 1]].x + w[t[i + 2]].x) / 3, (w[t[i]].z + w[t[i + 1]].z + w[t[i + 2]].z) / 3)) : drop.Contains(F(t[i]))) { dt++; continue; } keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); } m.SetTriangles(keep.ToArray(), s); }
                var ap = "Assets/TierraRejada/generated/brushclear/" + mf.name + "_" + tag + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
                new GameObject("mesh|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src) + "|" + src.name).transform.SetParent(log.transform, false);
                var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) mc.sharedMesh = m; mf.sharedMesh = m;
                o.Add(mf.name + ": " + drop.Count + " plants (" + dt + " tris) cleared");
            }
        }
        UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return o.Count + " meshes\n" + string.Join("\n", o.ToArray());
    }
    // vertex-colour repaint (11a): on an A-owned copy of `meshName`, vertices inside `poly` take colour `rgb` (alpha kept), fading back to
    // their own colour over `feather` m outside it. Changes only how the shader layers the turf; geometry and physics unchanged.
    public float feather = 3f;
    public string VPaint()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var P = poly.Split(';').Where(q => q.Length > 0).Select(q => { var a = q.Split(','); return new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci)); }).ToArray();
        System.Func<Vector2, bool> In = q => { bool r = false; for (int i = 0, j = P.Length - 1; i < P.Length; j = i++) if ((P[i].y > q.y) != (P[j].y > q.y) && q.x < (P[j].x - P[i].x) * (q.y - P[i].y) / (P[j].y - P[i].y) + P[i].x) r = !r; return r; };
        System.Func<Vector2, float> Dist = q => { if (In(q)) return 0; float d = float.MaxValue; for (int i = 0, j = P.Length - 1; i < P.Length; j = i++) { var ab = P[i] - P[j]; float t = Mathf.Clamp01(Vector2.Dot(q - P[j], ab) / ab.sqrMagnitude); d = Mathf.Min(d, (P[j] + ab * t - q).magnitude); } return d; };
        var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var src = mf.sharedMesh; var v = src.vertices; var c = src.colors; if (c.Length != v.Length) return "no colours";
        var tc = rgb.Split(',').Select(q => float.Parse(q, ci)).ToArray(); int n = 0;
        for (int i = 0; i < v.Length; i++) { var w = mf.transform.TransformPoint(v[i]); float d = Dist(new Vector2(w.x, w.z)); if (d >= feather) continue; float k = 1 - Mathf.SmoothStep(0, 1, d / feather); var t = new Color(tc[0], tc[1], tc[2], c[i].a); c[i] = Color.Lerp(c[i], t, k); n++; }
        var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_vp"; m.colors = c;
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/vpaint")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "vpaint");
        var ap = "Assets/TierraRejada/generated/vpaint/" + meshName + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("vpaint|" + meshName + "|" + UnityEditor.AssetDatabase.GetAssetPath(src) + "|" + src.name).transform.SetParent(log.transform, false);
        var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) mc.sharedMesh = m; mf.sharedMesh = m;
        UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);
        return n + " vertices repainted on " + meshName + " -> " + ap + " (was " + UnityEditor.AssetDatabase.GetAssetPath(src) + ")";
    }
    // put mesh asset `assetPath` back on OPCD object `meshName` (renderer and collider)
    public string assetPath = "";
    public string SetMesh()
    {
        var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(assetPath); if (!m) return "no asset " + assetPath;
        mf.sharedMesh = m; var mc = mf.GetComponent<MeshCollider>(); if (mc) { mc.sharedMesh = null; mc.sharedMesh = m; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mf.gameObject.scene); return meshName + " <- " + assetPath + " (" + m.vertexCount + " verts)";
    }
    // terrain detail grass standing on cart paths / roads (16 road plants, 17 dirt path): every detail cell of every active terrain whose
    // centre, or a point `pad2` m round it, has a CART_PATHS / ROAD* collider under it gets density 0 in all layers. Dry run unless apply.
    public float pad2 = 0.3f;
    public string DetailOffPaths()
    {
        Physics.SyncTransforms(); var o = new List<string>(); int total = 0;
        System.Func<float, float, bool> OnPath = (x, z) => Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200).Any(h => { var rn = h.collider.transform.root.name; return rn == "CART_PATHS" || rn.StartsWith("ROAD"); });
        var boxes = new List<Bounds>(); foreach (var rn in new[] { "CART_PATHS", "ROAD_TR", "ROAD16" }) { var g = GameObject.Find(rn); if (g) foreach (var c in g.GetComponentsInChildren<Collider>()) boxes.Add(c.bounds); }
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; int res = td.detailResolution; if (res == 0 || td.detailPrototypes.Length == 0) continue; var tp = t.transform.position; float cw = td.size.x / res, ch = td.size.z / res; int n = 0;
            var layers = Enumerable.Range(0, td.detailPrototypes.Length).Select(l => td.GetDetailLayer(0, 0, res, res, l)).ToArray();
            foreach (var b in boxes)
            {
                int x0i = Mathf.Max(0, (int)((b.min.x - pad2 - tp.x) / cw)), x1i = Mathf.Min(res - 1, (int)((b.max.x + pad2 - tp.x) / cw)), z0i = Mathf.Max(0, (int)((b.min.z - pad2 - tp.z) / ch)), z1i = Mathf.Min(res - 1, (int)((b.max.z + pad2 - tp.z) / ch));
                for (int zi = z0i; zi <= z1i; zi++) for (int xi = x0i; xi <= x1i; xi++)
                {
                    if (!layers.Any(L => L[zi, xi] > 0)) continue; float wx = tp.x + (xi + 0.5f) * cw, wz = tp.z + (zi + 0.5f) * ch;
                    if (!OnPath(wx, wz) && !OnPath(wx + pad2, wz) && !OnPath(wx - pad2, wz) && !OnPath(wx, wz + pad2) && !OnPath(wx, wz - pad2)) continue;
                    foreach (var L in layers) L[zi, xi] = 0; n++;
                }
            }
            if (n > 0 && apply) { UnityEditor.Undo.RegisterCompleteObjectUndo(td, "detail off paths"); for (int l = 0; l < layers.Length; l++) td.SetDetailLayer(0, 0, l, layers[l]); UnityEditor.EditorUtility.SetDirty(td); }
            o.Add(t.name + ": " + n + " detail cells on paths" + (apply ? " cleared" : "")); total += n;
        }
        if (apply) UnityEditor.AssetDatabase.SaveAssets();
        return total + " cells\n" + string.Join("\n", o.ToArray());
    }
    // pond bank top (W1, 1c): round the crest where the turf meets the lake bed. One height field, -lipDrop * (1 - smoothstep(|d| / band)),
    // d = distance to the lake-bed mesh outline (`meshName`), is applied to EVERY active OPCD mesh vertex and the terrain inside the band,
    // so meshes that share an edge move together (no gaps). A-owned mesh copies (generated/pondlip, originals in NIGHT1031_LOG); colliders
    // follow. Water planes are not touched. Dry run unless apply.
    public float band = 1.8f, lipDrop = 0.35f; public string planeName = "";
    public string PondLip()
    {
        var lake = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == meshName); if (!lake) return "no " + meshName;
        var lm = lake.sharedMesh; var lv = lm.vertices.Select(q => lake.transform.TransformPoint(q)).ToArray(); var lt = lm.triangles;
        var weld = new int[lv.Length]; var map = new Dictionary<Vector2Int, int>(); for (int i = 0; i < lv.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(lv[i].x * 200), Mathf.RoundToInt(lv[i].z * 200)); int w; if (!map.TryGetValue(k, out w)) { w = i; map[k] = i; } weld[i] = w; }
        var cnt = new Dictionary<long, int>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < lt.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(weld[lt[i + e]], weld[lt[i + (e + 1) % 3]]); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        var segs = cnt.Where(kv => kv.Value == 1).Select(kv => new Vector4(lv[(int)(kv.Key >> 32)].x, lv[(int)(kv.Key >> 32)].z, lv[(int)(kv.Key & 0xffffffff)].x, lv[(int)(kv.Key & 0xffffffff)].z)).ToList();
        if (segs.Count == 0) return "no outline";
        var bb = lake.GetComponent<Renderer>().bounds; bb.Expand(new Vector3(2 * band + 1, 0, 2 * band + 1));
        var plane = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == planeName); if (!plane) return "no " + planeName; float wy = plane.GetComponent<Renderer>().bounds.max.y;
        System.Func<float, float, float> Delta = (x, z) => { if (x < bb.min.x || x > bb.max.x || z < bb.min.z || z > bb.max.z) return 0; float d = float.MaxValue; var q = new Vector2(x, z); foreach (var s in segs) { var a = new Vector2(s.x, s.y); var ab = new Vector2(s.z, s.w) - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); d = Mathf.Min(d, (a + ab * t - q).magnitude); if (d < 1e-3f) break; } if (d >= band) return 0; return -lipDrop * (1 - Mathf.SmoothStep(0, 1, d / band)); };
        var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        if (apply && !UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/pondlip")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "pondlip");
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh || mf.name.Contains("WaterPlane")) continue; var b = mf.GetComponent<Renderer>() ? mf.GetComponent<Renderer>().bounds : new Bounds(); if (b.max.x < bb.min.x || b.min.x > bb.max.x || b.max.z < bb.min.z || b.min.z > bb.max.z) continue;
            var src = mf.sharedMesh; var v = src.vertices; int n = 0; float worst = 0;
            for (int i = 0; i < v.Length; i++) { var w = mf.transform.TransformPoint(v[i]); float dl = Delta(w.x, w.z); if (dl == 0) continue; float ny = w.y + dl; if (w.y > wy) ny = Mathf.Max(ny, Mathf.Min(w.y, wy + 0.06f)); if (ny == w.y) continue; worst = Mathf.Min(worst, ny - w.y); w.y = ny; v[i] = mf.transform.InverseTransformPoint(w); n++; }
            if (n == 0) continue; o.Add(mf.name + ": " + n + " vertices, down to " + worst.ToString("F2") + " m");
            if (!apply) continue;
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_pl"; m.vertices = v; m.RecalculateBounds(); m.RecalculateNormals();
            var ap = "Assets/TierraRejada/generated/pondlip/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            new GameObject("pondlip|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
            var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
        }
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var tp = t.transform.position; int R = td.heightmapResolution; float cx = td.size.x / (R - 1), cz = td.size.z / (R - 1);
            int i0 = Mathf.Max(0, Mathf.FloorToInt((bb.min.x - tp.x) / cx)), i1 = Mathf.Min(R - 1, Mathf.CeilToInt((bb.max.x - tp.x) / cx)), j0 = Mathf.Max(0, Mathf.FloorToInt((bb.min.z - tp.z) / cz)), j1 = Mathf.Min(R - 1, Mathf.CeilToInt((bb.max.z - tp.z) / cz));
            if (i1 < i0 || j1 < j0) continue; var H = td.GetHeights(i0, j0, i1 - i0 + 1, j1 - j0 + 1); int k = 0;
            for (int j = 0; j < H.GetLength(0); j++) for (int i = 0; i < H.GetLength(1); i++) { float dl = Delta(tp.x + (i0 + i) * cx, tp.z + (j0 + j) * cz); if (dl == 0) continue; float y0 = tp.y + H[j, i] * td.size.y, ny = y0 + dl; if (y0 > wy - 0.1f) ny = Mathf.Max(ny, Mathf.Min(y0, wy - 0.1f)); H[j, i] = Mathf.Max(0, (ny - tp.y) / td.size.y); k++; }
            o.Add(t.name + ": " + k + " height cells"); if (apply && k > 0) { td.SetHeights(i0, j0, H); UnityEditor.EditorUtility.SetDirty(td); }
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return (apply ? "" : "(dry run) ") + segs.Count + " outline edges\n" + string.Join("\n", o.ToArray());
    }
    // undo PondLip meshes: every "pondlip|<mesh>|<asset path>" entry in NIGHT1031_LOG puts that asset back on the mesh (terrain is left)
    public string PondLipUndo()
    {
        var log = GameObject.Find("NIGHT1031_LOG"); if (!log) return "no log"; var o = new List<string>();
        foreach (var t in log.transform.Cast<Transform>().Where(x => x.name.StartsWith("pondlip|")).ToList())
        {
            var p = t.name.Split('|'); meshName = p[1]; assetPath = p[2]; o.Add(SetMesh()); Object.DestroyImmediate(t.gameObject);
        }
        return string.Join("\n", o.ToArray());
    }
    // OPCD meshes whose name contains `words` (comma list): triangles whose centre (or a corner) has a CART_PATHS / ROAD* collider under it
    // are dropped (A-owned copies in generated/offpath, originals in NIGHT1031_LOG). For extra skirts like the tee "_wide" pads.
    public string TrisOffPaths()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        System.Func<float, float, bool> OnPath = (x, z) => Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200).Any(h => { var rn = h.collider.transform.root.name; return (rn == "CART_PATHS" || rn.StartsWith("ROAD")) && h.collider.gameObject.activeInHierarchy && !h.collider.name.ToLower().Contains("curb"); });
        if (apply && !UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/offpath")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "offpath");
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && words.Split(',').Any(w => f.name.Contains(w))))
        {
            var src = mf.sharedMesh; var w = src.vertices.Select(q => mf.transform.TransformPoint(q)).ToArray(); int dropped = 0; var subs = new List<int[]>();
            for (int s = 0; s < src.subMeshCount; s++) { var t = src.GetTriangles(s); var keep = new List<int>(); for (int i = 0; i < t.Length; i += 3) { var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; if (OnPath(c.x, c.z)) { dropped++; continue; } keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); } subs.Add(keep.ToArray()); }
            if (dropped == 0) continue; o.Add(mf.name + ": " + dropped + " triangles over a path" + (apply ? " dropped" : ""));
            if (!apply) continue;
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_op"; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
            var ap = "Assets/TierraRejada/generated/offpath/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            new GameObject("offpath|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
            var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return o.Count + " meshes\n" + string.Join("\n", o.ToArray());
    }
    // ground skins (meshes under `roots` whose name contains `words`): triangles steeper than `maxDeg` are listed by 10 m cell (dry run) or
    // dropped (apply; A-owned copies in generated/steep, originals in NIGHT1031_LOG). A toe/cover draped on a steep face smears its texture.
    public float maxDeg = 35f;
    public string SteepTris()
    {
        var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        if (apply && !UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/steep")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "steep");
        foreach (var rp in roots.Split(',').Where(q => q.Length > 0))
        {
            var r0 = GameObject.Find(rp); if (!r0) { o.Add("no " + rp); continue; }
            foreach (var mf in r0.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && words.Split(',').Any(w => f.name.Contains(w))))
            {
                var src = mf.sharedMesh; var w = src.vertices.Select(q => mf.transform.TransformPoint(q)).ToArray(); var cells = new Dictionary<string, int>(); int dropped = 0; var subs = new List<int[]>();
                for (int s = 0; s < src.subMeshCount; s++) { var t = src.GetTriangles(s); var keep = new List<int>(); for (int i = 0; i < t.Length; i += 3) { var n = Vector3.Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]); if (n.sqrMagnitude < 1e-10f || Vector3.Angle(n.y < 0 ? -n : n, Vector3.up) <= maxDeg) { keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); continue; } dropped++; var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; var k = (Mathf.Floor(c.x / 10) * 10) + "," + (Mathf.Floor(c.z / 10) * 10); int v; cells[k] = cells.TryGetValue(k, out v) ? v + 1 : 1; } subs.Add(keep.ToArray()); }
                if (dropped == 0) continue; o.Add(mf.name + ": " + dropped + " triangles over " + maxDeg + " deg; cells " + string.Join(" ", cells.OrderByDescending(q => q.Value).Take(12).Select(q => "(" + q.Key + ")x" + q.Value).ToArray()));
                if (!apply) continue;
                var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_st"; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
                var ap = "Assets/TierraRejada/generated/steep/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
                new GameObject("steep|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); mf.sharedMesh = m;
            }
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return string.Join("\n", o.ToArray());
    }
    // pond shelf (W1): lake-bed vertices (meshes whose name contains `words`, e.g. "Spline_water_0_Water_Base_Lake,Spline_water_0_B_") within
    // `band` m inside the bed outline (`meshName`) are RAISED (never lowered) to waterY - max(shelfMin, d * tan(shelfDeg)), so the bank runs
    // into the water as a shallow shelf instead of a dark drop. Turf is not touched. A-owned copies in generated/pondshelf. Dry run unless apply.
    public float shelfDeg = 12f, shelfMin = 0.06f;
    public string PondShelf()
    {
        var opcd = GameObject.Find("OPCD Meshes"); var lake = opcd.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == meshName); if (!lake) return "no " + meshName;
        var plane = opcd.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == planeName); if (!plane) return "no " + planeName; var pys = plane.sharedMesh.vertices.Select(q => plane.transform.TransformPoint(q).y).OrderBy(q => q).ToArray(); float wy = pys[pys.Length / 2];
        var lm = lake.sharedMesh; var lv = lm.vertices.Select(q => lake.transform.TransformPoint(q)).ToArray(); var lt = lm.triangles;
        var weld = new int[lv.Length]; var map = new Dictionary<Vector2Int, int>(); for (int i = 0; i < lv.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(lv[i].x * 200), Mathf.RoundToInt(lv[i].z * 200)); int w; if (!map.TryGetValue(k, out w)) { w = i; map[k] = i; } weld[i] = w; }
        var cnt = new Dictionary<long, int>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < lt.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(weld[lt[i + e]], weld[lt[i + (e + 1) % 3]]); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        var segs = cnt.Where(kv => kv.Value == 1).Select(kv => new Vector4(lv[(int)(kv.Key >> 32)].x, lv[(int)(kv.Key >> 32)].z, lv[(int)(kv.Key & 0xffffffff)].x, lv[(int)(kv.Key & 0xffffffff)].z)).ToList();
        System.Func<float, float, float> Dist = (x, z) => { float d = float.MaxValue; var q = new Vector2(x, z); foreach (var s in segs) { var a = new Vector2(s.x, s.y); var ab = new Vector2(s.z, s.w) - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); d = Mathf.Min(d, (a + ab * t - q).magnitude); } return d; };
        float tn = Mathf.Tan(shelfDeg * Mathf.Deg2Rad); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        if (apply && !UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/pondshelf")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "pondshelf");
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && words.Split(',').Any(w => f.name.Contains(w))))
        {
            var src = mf.sharedMesh; var v = src.vertices; int n = 0; float most = 0;
            for (int i = 0; i < v.Length; i++) { var w = mf.transform.TransformPoint(v[i]); if (w.y >= wy - shelfMin) continue; float d = Dist(w.x, w.z); if (d > band) continue; float target = wy - Mathf.Max(shelfMin, d * tn); if (target <= w.y) continue; most = Mathf.Max(most, target - w.y); w.y = target; v[i] = mf.transform.InverseTransformPoint(w); n++; }
            if (n == 0) continue; o.Add(mf.name + ": " + n + " vertices raised, up to " + most.ToString("F2") + " m");
            if (!apply) continue;
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_sh"; m.vertices = v; m.RecalculateBounds(); m.RecalculateNormals();
            var ap = "Assets/TierraRejada/generated/pondshelf/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            new GameObject("pondshelf|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
            var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return (apply ? "" : "(dry run) ") + "water y " + wy.ToString("F2") + ", " + segs.Count + " outline edges\n" + string.Join("\n", o.ToArray());
    }
    // exposed dirt / path at tee boxes (BoJa h5_f): for every active tee mesh (OPCD "Spline_tee_*Tee_-_Mesh*"), 0.5 m samples on the pad and in a
    // `ring` m band round it; the top surface there (any root) is flagged when it is a path, bare ground (terrain or Outer Plot showing, no
    // OPCD turf), or a material named dirt/soil/toe/mulch/path/sand; and pad-edge samples where the ground just outside drops or rises
    // > `stepMax` m within 0.6 m (a hard turf step). Output per tee mesh: counts + worst spots.
    public float ring = 2f, stepMax = 0.25f;
    public string TeeDirt()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes"); var o = new List<string>(); int bad = 0;
        string[] dirtWords = { "dirt", "soil", "toe", "mulch", "path", "sand", "bare", "pavers", "concrete" };
        System.Func<float, float, KeyValuePair<string, float>> Top = (x, z) => { RaycastHit? b = null; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.name.StartsWith("collider_")) continue; if (b == null || h.point.y > b.Value.point.y) b = h; } if (b == null) return new KeyValuePair<string, float>("none", 0); var c = b.Value.collider; var r = c.GetComponent<Renderer>(); string m = r && r.sharedMaterial ? r.sharedMaterial.name : ""; return new KeyValuePair<string, float>(c.transform.root.name + "/" + c.name + "|" + m + "|" + (c is TerrainCollider ? "terrain" : ""), b.Value.point.y); };
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && f.name.StartsWith("Spline_tee_") && f.name.Contains("Tee_-_Mesh")))
        {
            var mc = mf.GetComponent<MeshCollider>(); if (!mc) continue; var bb = mf.GetComponent<Renderer>().bounds; var hits = new Dictionary<string, int>(); var where = new Dictionary<string, Vector3>(); int steps = 0; Vector3 stepAt = Vector3.zero; float stepWorst = 0;
            for (float x = bb.min.x - ring; x <= bb.max.x + ring; x += 0.5f) for (float z = bb.min.z - ring; z <= bb.max.z + ring; z += 0.5f)
            {
                RaycastHit th; bool onPad = mc.Raycast(new Ray(new Vector3(x, 600, z), Vector3.down), out th, 1200);
                if (!onPad) { bool near = false; for (int k = 0; k < 8 && !near; k++) { float a = k * Mathf.PI / 4; RaycastHit t2; if (mc.Raycast(new Ray(new Vector3(x + Mathf.Cos(a) * ring, 600, z + Mathf.Sin(a) * ring), Vector3.down), out t2, 1200)) near = true; } if (!near) continue; }
                var t = Top(x, z); var s = t.Key.ToLower(); string why = null;
                if (s.StartsWith("cart_paths/") || s.StartsWith("road")) why = "path"; else if (s.Contains("|terrain") || s.StartsWith("outer plot/")) why = "bare ground"; else if (dirtWords.Any(w => s.Split('|')[1].Contains(w)) && !s.Contains("_tee")) why = "dirt material " + t.Key.Split('|')[1];
                if (why != null) { int c; hits[why] = hits.TryGetValue(why, out c) ? c + 1 : 1; if (!where.ContainsKey(why)) where[why] = new Vector3(x, t.Value, z); }
                if (onPad) { for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; float qx = x + Mathf.Cos(a) * 0.6f, qz = z + Mathf.Sin(a) * 0.6f; RaycastHit t3; if (mc.Raycast(new Ray(new Vector3(qx, 600, qz), Vector3.down), out t3, 1200)) continue; var q = Top(qx, qz); if (q.Key == "none") continue; float dy = Mathf.Abs(q.Value - th.point.y); if (dy > stepMax) { steps++; if (dy > stepWorst) { stepWorst = dy; stepAt = new Vector3(qx, q.Value, qz); } } } }
            }
            if (hits.Count == 0 && steps == 0) continue; bad++;
            o.Add(mf.name + ": " + string.Join(", ", hits.Select(kv => kv.Key + " x" + kv.Value + " e.g. (" + where[kv.Key].x.ToString("F1") + "," + where[kv.Key].z.ToString("F1") + ")").ToArray()) + (steps > 0 ? (hits.Count > 0 ? "; " : "") + "edge steps > " + stepMax + " m x" + steps + ", worst " + stepWorst.ToString("F2") + " m at (" + stepAt.x.ToString("F1") + "," + stepAt.z.ToString("F1") + ")" : ""));
        }
        return bad + " tee meshes flagged\n" + string.Join("\n", o.ToArray());
    }
    // remove a bunker (BoJa Nov 1, hole 5 fairway bunker): OPCD objects named in `ids` ("a|b") are switched off (NIGHT1031_LOG), then every
    // OPCD turf mesh with an inner boundary loop whose centre lies within `r` m of (cx, cz) gets that hole capped: boundary vertices keep their
    // exact positions (no seam), 4 inner rings ease toward a least-squares plane of the loop, centre on the plane; uv / uv2 / colour from the
    // nearest boundary vertex; same material; the turf's collider gets the capped mesh too. A-owned copies in generated/fillhole.
    public string FillHole()
    {
        var opcd = GameObject.Find("OPCD Meshes"); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var id in ids.Split('|').Where(q => q.Length > 0)) { var t = opcd.GetComponentsInChildren<Transform>(true).FirstOrDefault(q => q.name == id); if (!t) { o.Add("no " + id); continue; } if (t.gameObject.activeSelf) { new GameObject("off|" + id + "|" + tag).transform.SetParent(log.transform, false); t.gameObject.SetActive(false); o.Add(id + " off"); } }
        Physics.SyncTransforms();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/fillhole")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "fillhole");
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && !f.name.Contains("bunker") && !f.name.Contains("water")).ToList())
        {
            var b = mf.GetComponent<Renderer>().bounds; if (cx < b.min.x - r || cx > b.max.x + r || cz < b.min.z - r || cz > b.max.z + r) continue;
            var src = mf.sharedMesh; var lv = src.vertices; var w = lv.Select(q => mf.transform.TransformPoint(q)).ToArray(); var tri = src.triangles;
            var weld = new int[w.Length]; var map = new Dictionary<Vector3Int, int>(); for (int i = 0; i < w.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(w[i].x * 500), Mathf.RoundToInt(w[i].y * 500), Mathf.RoundToInt(w[i].z * 500)); int j; if (!map.TryGetValue(k, out j)) { j = i; map[k] = i; } weld[i] = j; }
            var cnt = new Dictionary<long, int>(); var dir = new Dictionary<long, int[]>(); System.Func<int, int, long> K = (a, c) => a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
            for (int i = 0; i < tri.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[tri[i + e]], c = weld[tri[i + (e + 1) % 3]]; var k = K(a, c); int n; cnt[k] = cnt.TryGetValue(k, out n) ? n + 1 : 1; dir[k] = new[] { a, c }; }
            var next = new Dictionary<int, int>(); foreach (var kv in cnt) if (kv.Value == 1) next[dir[kv.Key][0]] = dir[kv.Key][1];
            var used = new HashSet<int>(); var loops = new List<List<int>>();
            foreach (var s0 in next.Keys.ToList()) { if (used.Contains(s0)) continue; var L = new List<int>(); int cur = s0; while (!used.Contains(cur) && next.ContainsKey(cur)) { used.Add(cur); L.Add(cur); cur = next[cur]; } if (L.Count >= 3 && cur == s0) loops.Add(L); }
            foreach (var L in loops)
            {
                var c2 = new Vector2(L.Average(i => w[i].x), L.Average(i => w[i].z)); if ((c2 - new Vector2(cx, cz)).magnitude > r) continue;
                float area = 0; for (int i = 0; i < L.Count; i++) { var p = w[L[i]]; var q = w[L[(i + 1) % L.Count]]; area += p.x * q.z - q.x * p.z; } if (Mathf.Abs(area) / 2 > 2000) continue;   // not an outer outline
                double sxx = 0, sxz = 0, szz = 0, sx = 0, sz = 0, sy = 0, sxy = 0, szy = 0; int N = L.Count; foreach (var i in L) { var p = w[i]; sxx += p.x * p.x; sxz += p.x * p.z; szz += p.z * p.z; sx += p.x; sz += p.z; sy += p.y; sxy += p.x * p.y; szy += p.z * p.y; }
                var M = new double[,] { { sxx, sxz, sx }, { sxz, szz, sz }, { sx, sz, N } }; var V = new[] { sxy, szy, sy };
                System.Func<double[,], double> Det = mm => mm[0, 0] * (mm[1, 1] * mm[2, 2] - mm[1, 2] * mm[2, 1]) - mm[0, 1] * (mm[1, 0] * mm[2, 2] - mm[1, 2] * mm[2, 0]) + mm[0, 2] * (mm[1, 0] * mm[2, 1] - mm[1, 1] * mm[2, 0]);
                double D = Det(M); var co = new double[3]; for (int k = 0; k < 3; k++) { var Mk = (double[,])M.Clone(); for (int rr = 0; rr < 3; rr++) Mk[rr, k] = V[rr]; co[k] = Det(Mk) / D; }
                System.Func<float, float, float> Pl = (x, z) => (float)(co[0] * x + co[1] * z + co[2]);
                var nv = new List<Vector3>(lv); var uv = new List<Vector2>(src.uv); var uv2 = new List<Vector2>(src.uv2); var col = new List<Color>(src.colors); bool hasUv = uv.Count == lv.Length, hasUv2 = uv2.Count == lv.Length, hasCol = col.Count == lv.Length;
                int R = 4; var idx = new int[R + 1, N]; for (int i = 0; i < N; i++) idx[0, i] = L[i];
                for (int ring = 1; ring <= R; ring++) { float f = ring / (float)(R + 1); for (int i = 0; i < N; i++) { var p = w[L[i]]; var xz = Vector2.Lerp(new Vector2(p.x, p.z), c2, f); float y = Mathf.Lerp(p.y, Pl(xz.x, xz.y), Mathf.SmoothStep(0, 1, Mathf.Min(1, f * 1.4f))); idx[ring, i] = nv.Count; nv.Add(mf.transform.InverseTransformPoint(new Vector3(xz.x, y, xz.y))); if (hasUv) uv.Add(uv[L[i]]); if (hasUv2) uv2.Add(uv2[L[i]]); if (hasCol) col.Add(col[L[i]]); } }
                int ci = nv.Count; nv.Add(mf.transform.InverseTransformPoint(new Vector3(c2.x, Pl(c2.x, c2.y), c2.y))); if (hasUv) uv.Add(uv[L[0]]); if (hasUv2) uv2.Add(uv2[L[0]]); if (hasCol) col.Add(col[L[0]]);
                var add = new List<int>(); for (int ring = 0; ring < R; ring++) for (int i = 0; i < N; i++) { int a = idx[ring, i], bq = idx[ring, (i + 1) % N], c = idx[ring + 1, i], d = idx[ring + 1, (i + 1) % N]; add.AddRange(new[] { a, bq, d, a, d, c }); }
                for (int i = 0; i < N; i++) add.AddRange(new[] { idx[R, i], idx[R, (i + 1) % N], ci });
                for (int k = 0; k < add.Count; k += 3) { var nn = Vector3.Cross(mf.transform.TransformPoint(nv[add[k + 1]]) - mf.transform.TransformPoint(nv[add[k]]), mf.transform.TransformPoint(nv[add[k + 2]]) - mf.transform.TransformPoint(nv[add[k]])); if (nn.y < 0) { int q = add[k + 1]; add[k + 1] = add[k + 2]; add[k + 2] = q; } }
                var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_fh"; if (nv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; m.SetVertices(nv); if (hasUv) m.SetUVs(0, uv); if (hasUv2) m.SetUVs(1, uv2); if (hasCol) m.SetColors(col);
                var t0 = new List<int>(src.GetTriangles(0)); t0.AddRange(add); m.SetTriangles(t0, 0); for (int s = 1; s < src.subMeshCount; s++) m.SetTriangles(src.GetTriangles(s), s); m.RecalculateNormals(); m.RecalculateBounds();
                var ap = "Assets/TierraRejada/generated/fillhole/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
                new GameObject("fillhole|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
                var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
                o.Add(mf.name + ": hole of " + N + " edges at (" + c2.x.ToString("F1") + "," + c2.y.ToString("F1") + "), " + (Mathf.Abs(area) / 2).ToString("F0") + " m2 capped");
                break;
            }
        }
        UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return string.Join("\n", o.ToArray());
    }
    // new plain Standard material at `matPath` with colour `rgb`, smoothness `gloss` (e.g. the teal lake bed GSPro's hole map shows)
    public string NewMat()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var c = rgb.Split(',').Select(q => float.Parse(q, ci)).ToArray();
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); bool made = !m; if (made) m = new Material(Shader.Find("Standard"));
        m.SetColor("_Color", new Color(c[0], c[1], c[2], 1)); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0);
        if (made) UnityEditor.AssetDatabase.CreateAsset(m, matPath); else UnityEditor.EditorUtility.SetDirty(m); UnityEditor.AssetDatabase.SaveAssets();
        return (made ? "created " : "updated ") + matPath;
    }
    // undo SwapMix for the swaps whose new prefab name is in `words` ("TR_Sage_leafy,..."): the original plant logged in SWAP_LOG at that spot
    // goes back on, the log entry is renamed "unswap|...", and root `propName` (the new plants) is switched off, not deleted.
    public string SwapUndo()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var log = GameObject.Find("SWAP_LOG"); if (!log) return "no SWAP_LOG"; var names = words.Split(','); int on = 0, miss = 0;
        foreach (Transform e in log.transform.Cast<Transform>().ToList())
        {
            var f = e.name.Split('|'); if (f.Length < 4 || f[0] != "swap" || !names.Contains(f[3])) continue;
            var rp = f[1].Split('/'); var pp = f[2].Trim('(', ')').Split(','); var pos = new Vector3(float.Parse(pp[0], ci), float.Parse(pp[1], ci), float.Parse(pp[2], ci));
            var r0 = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.transform.parent == null && g.name == rp[0]);
            var t = r0 ? r0.GetComponentsInChildren<LODGroup>(true).Select(l => l.transform).Where(x => x.name == rp[rp.Length - 1] && !x.gameObject.activeSelf).OrderBy(x => (x.position - pos).sqrMagnitude).FirstOrDefault() : null;
            if (!t || (t.position - pos).magnitude > 0.05f) { miss++; continue; } t.gameObject.SetActive(true); e.name = "un" + e.name; on++;
        }
        var root = GameObject.Find(propName); if (root) root.SetActive(false); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return on + " originals back on, " + miss + " not found; " + propName + (root ? " off" : " not found");
    }
    // read-only: renderers whose object name or any parent name contains one of `words` (case-insensitive, "*" = every renderer) inside the
    // box x0..x1, z0..z1; for each, the ground straight below its bounds centre (OPCD meshes and terrains, its own colliders ignored) and the gap
    // from that ground to the renderer's lowest vertex near the centre. Only gaps over `gap` m are listed when gap > 0.
    public string Floaters()
    {
        Physics.SyncTransforms(); var ws = words.ToLower().Split(','); var opcdT = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); int n = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            var b = r.bounds; if (b.center.x < x0 || b.center.x > x1 || b.center.z < z0 || b.center.z > z1) continue; string path = r.name; for (var u = r.transform.parent; u; u = u.parent) path = u.name + "/" + path;
            if (words != "*" && !ws.Any(w => path.ToLower().Contains(w))) continue; if (r.transform.IsChildOf(opcdT)) continue;
            float ground = float.NaN; foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 50, b.center.z), Vector3.down, 200).OrderBy(h => h.distance)) { if (h.collider.transform.IsChildOf(r.transform.root) && !h.collider.transform.IsChildOf(opcdT)) continue; if (h.collider.transform.IsChildOf(opcdT) || h.collider is TerrainCollider) { ground = h.point.y; break; } }
            float low = b.min.y; var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh && mf.sharedMesh.isReadable) { var c2 = new Vector2(b.center.x, b.center.z); var vs = mf.sharedMesh.vertices.Select(v => r.transform.TransformPoint(v)).Where(v => (new Vector2(v.x, v.z) - c2).magnitude < Mathf.Max(0.5f, Mathf.Min(b.extents.x, b.extents.z) * 0.5f)).ToList(); if (vs.Count > 0) low = vs.Min(v => v.y); }
            float g = low - ground; if (gap > 0 && !(g > gap)) continue; n++; if (o.Count < 60) o.Add(path + " at (" + b.center.x.ToString("F1") + "," + b.center.z.ToString("F1") + ") size " + b.size.x.ToString("F1") + "x" + b.size.z.ToString("F1") + " low " + low.ToString("F2") + " ground " + ground.ToString("F2") + " gap " + g.ToString("+0.00;-0.00") + (r.gameObject.activeInHierarchy ? "" : " (off)"));
        }
        return n + " found\n" + string.Join("\n", o.ToArray());
    }
    // collar re-grade (BoJa: greens and collars "act like bumpers"): collar mesh `meshName` gets every boundary vertex pinned to the surface
    // right beside it (green / green blend inside, approach / rough / bunker blend outside: first other OPCD collider 0.12 m off the edge) and
    // its interior solved as a membrane between them (Laplace, welded mesh), so no step at either edge and no lip inside (a membrane can't rise
    // above its rim). Green meshes are never touched. A-owned copy (generated/collarfeather), collider follows. Dry run unless apply.
    public string CollarLaplace()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var mf = opcdT.GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var src = mf.sharedMesh; var col = mf.GetComponent<Collider>(); var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N]; for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); var nb = new Dictionary<int, HashSet<int>>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; HashSet<int> s; if (!nb.TryGetValue(a, out s)) nb[a] = s = new HashSet<int>(); s.Add(b); if (!nb.TryGetValue(b, out s)) nb[b] = s = new HashSet<int>(); s.Add(a); }
        var bnd = new HashSet<int>(); foreach (var kv in cnt) if (kv.Value == 1) { bnd.Add((int)(kv.Key >> 32)); bnd.Add((int)(kv.Key & 0xffffffff)); }
        var reps = wk.Values.ToList(); var y = reps.ToDictionary(r => r, r => w[r].y); var pinned = new HashSet<int>(); int unpinned = 0; float maxPin = 0;
        foreach (var r in bnd)
        {
            // edge direction: average of the two boundary neighbours; its normal points off the mesh on one side and into it on the other
            var bn = nb[r].Where(q => bnd.Contains(q) && cnt.ContainsKey(K(r, q)) && cnt[K(r, q)] == 1).ToList(); if (bn.Count < 2) { unpinned++; continue; }
            var tan = new Vector2(w[bn[1]].x - w[bn[0]].x, w[bn[1]].z - w[bn[0]].z).normalized; var nrm = new Vector2(-tan.y, tan.x); float best = float.NaN;
            foreach (var sgn in new[] { 1f, -1f })
            {
                var q = new Vector2(w[r].x, w[r].z) + nrm * 0.12f * sgn; var hits = Physics.RaycastAll(new Vector3(q.x, w[r].y + 5, q.y), Vector3.down, 20).Where(h => h.collider.transform.IsChildOf(opcdT)).OrderBy(h => h.distance).ToList();
                if (hits.Count == 0 || hits[0].collider == col) continue; best = hits[0].point.y; break;
            }
            if (float.IsNaN(best)) { unpinned++; continue; } maxPin = Mathf.Max(maxPin, Mathf.Abs(best - w[r].y)); y[r] = best; pinned.Add(r);
        }
        var free = reps.Where(r => !pinned.Contains(r) && !bnd.Contains(r)).ToList();
        for (int it = 0; it < 400; it++) foreach (var r in free) { HashSet<int> s; if (!nb.TryGetValue(r, out s) || s.Count == 0) continue; y[r] = s.Average(q => y[weld[q]]); }
        float mx = 0, up = 0; foreach (var r in reps) { float d = y[r] - w[r].y; mx = Mathf.Max(mx, Mathf.Abs(d)); up = Mathf.Max(up, d); }
        string res = meshName + ": " + reps.Count + " vertices, " + pinned.Count + " rim pinned (largest rim move " + (maxPin * 100).ToString("F1") + " cm), " + unpinned + " rim left as is, interior moved up to " + (mx * 100).ToString("F1") + " cm (max rise " + (up * 100).ToString("F1") + " cm)";
        if (!apply) return "(dry run) " + res;
        var nv = new Vector3[N]; for (int i = 0; i < N; i++) { var p = w[i]; p.y = y[weld[i]]; nv[i] = mf.transform.InverseTransformPoint(p); }
        const string dir = "Assets/TierraRejada/generated/collarfeather"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "collarfeather");
        var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_cf"; nm.vertices = nv; nm.RecalculateNormals(); nm.RecalculateBounds();
        var ap = dir + "/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("collarfeather|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
        var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; } mf.sharedMesh = nm; UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return res;
    }
    // undo TierraTeeGate.BadSpots range-net moves (BoJa 1: "trees by the range net were moved into the fairway"): for each BADSPOT_LOG
    // entry whose old spot is in the box x0..x1, z0..z1, the same-named active tree that BadSpots put just outside the net band (nearest to the
    // old spot, within 45 m) goes back to the old spot; if its crown would then touch the netting (trunk nearer the pole line than 0.6 x crown
    // radius + 0.5 m) it is pushed straight away from the net on the old spot's side until it clears. Seated on the ground. Dry run unless apply.
    public string RestoreBad()
    {
        Physics.SyncTransforms(); var ci = System.Globalization.CultureInfo.InvariantCulture; var log = GameObject.Find("BADSPOT_LOG"); if (!log) return "no BADSPOT_LOG"; var opcdT = GameObject.Find("OPCD Meshes").transform;
        var segs = new List<Vector4>(); var nr = GameObject.Find("NETS"); if (nr) foreach (Transform n in nr.transform) { var ps = n.GetComponentsInChildren<Transform>().Where(x => x.name == "Pole").Select(x => { var rb = x.GetComponent<Renderer>(); var cc = rb ? rb.bounds.center : x.position; return new Vector2(cc.x, cc.z); }).ToList(); for (int i = 0; i + 1 < ps.Count; i++) segs.Add(new Vector4(ps[i].x, ps[i].y, ps[i + 1].x, ps[i + 1].y)); }
        System.Func<Vector2, KeyValuePair<float, Vector2>> NearNet = q => { float bd = 1e9f; Vector2 bp = q; foreach (var sg in segs) { var a0 = new Vector2(sg.x, sg.y); var ab = new Vector2(sg.z, sg.w) - a0; float tt = Mathf.Clamp01(Vector2.Dot(q - a0, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude)); var p = a0 + ab * tt; float d = (p - q).magnitude; if (d < bd) { bd = d; bp = p; } } return new KeyValuePair<float, Vector2>(bd, bp); };
        var used = new HashSet<Transform>(); var o = new List<string>(); int done = 0;
        foreach (Transform e in log.transform.Cast<Transform>().ToList())
        {
            var f = e.name.Split('|'); if (f.Length < 3 || f[0] != "bad") continue; var pp = f[2].Trim('(', ')').Split(','); var old = new Vector3(float.Parse(pp[0], ci), float.Parse(pp[1], ci), float.Parse(pp[2], ci)); var o2 = new Vector2(old.x, old.z);
            if (o2.x < x0 || o2.x > x1 || o2.y < z0 || o2.y > z1) continue; var parts = f[1].Split('/'); var r0 = GameObject.Find(parts[0]); if (!r0) { o.Add(f[1] + " root missing"); continue; }
            Transform best = null; float bestD = 1e9f, cr = 3f;
            foreach (var t in r0.GetComponentsInChildren<LODGroup>().Select(l => l.transform).Where(t => t.name == parts[parts.Length - 1] && !used.Contains(t)))
            {
                float d = (new Vector2(t.position.x, t.position.z) - o2).magnitude; if (d < 0.3f || d > 45 || d >= bestD) continue; var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float crr = Mathf.Max(b.extents.x, b.extents.z), band = 4 + crr * 0.6f, sd = NearNet(new Vector2(t.position.x, t.position.z)).Key; if (sd >= band - 0.2f && sd <= band + 1.5f) { bestD = d; best = t; cr = crr; }
            }
            if (!best) { o.Add(f[1] + " old (" + o2.x.ToString("F1") + "," + o2.y.ToString("F1") + "): moved tree not identified, left"); continue; }
            used.Add(best); var tgt = o2; var nn = NearNet(tgt); float need = cr * 0.6f + 0.5f; if (nn.Key < need) { var away = (tgt - nn.Value).sqrMagnitude > 1e-4f ? (tgt - nn.Value).normalized : (o2 - new Vector2(best.position.x, best.position.z)).normalized; tgt = nn.Value + away * need; }
            o.Add(f[1] + ": (" + best.position.x.ToString("F1") + "," + best.position.z.ToString("F1") + ") -> (" + tgt.x.ToString("F1") + "," + tgt.y.ToString("F1") + ")" + ((tgt - o2).magnitude > 0.05f ? " [pushed " + (tgt - o2).magnitude.ToString("F1") + " m off the net]" : " [logged spot]"));
            if (!apply) continue; float gy = float.MinValue; foreach (var hh in Physics.RaycastAll(new Vector3(tgt.x, 600, tgt.y), Vector3.down, 1200)) { if (hh.collider.transform.IsChildOf(best)) continue; if (hh.collider.transform.IsChildOf(opcdT) || hh.collider is TerrainCollider) gy = Mathf.Max(gy, hh.point.y); }
            best.position = new Vector3(tgt.x, gy > float.MinValue ? gy - 0.05f : old.y, tgt.y); e.name = "restored|" + string.Join("|", f.Skip(1).ToArray()); done++;
        }
        if (apply) { Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return (apply ? done + " restored" : "(dry run)") + "\n" + string.Join("\n", o.ToArray());
    }
    // green low-pass (after-test pack d, 14 green): Gaussian of sigma `sigma` m over the green mesh `greenName` (welded vertices, xz distance),
    // blended in with smootherstep over `ease` m from the mesh boundary, so edge vertices (shared with the collar) stay exactly put. Pin test
    // before/after for every "x,z" in `pins` (";"): plane fit and max pairwise slope of the vertices in a 0.9 m disc, pass <= 1.7 deg. Pins are
    // never moved. A-owned copy (generated/greenlow), collider follows. Dry run unless apply.
    public float sigma = 1.5f; public string pins = "";
    public string GreenLow()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var gm = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == greenName); if (!gm) return "no " + greenName;
        var m = gm.sharedMesh; var w = m.vertices.Select(q => gm.transform.TransformPoint(q)).ToArray(); var t = m.triangles; int N = w.Length;
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N]; for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(weld[t[i + e]], weld[t[i + (e + 1) % 3]]); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        var bnd = new HashSet<int>(); foreach (var kv in cnt) if (kv.Value == 1) { bnd.Add((int)(kv.Key >> 32)); bnd.Add((int)(kv.Key & 0xffffffff)); }
        var reps = wk.Values.ToArray(); var B = bnd.Select(i => new Vector2(w[i].x, w[i].z)).ToArray();
        // spatial hash for the gaussian
        float cell = sigma; var grid = new Dictionary<Vector2Int, List<int>>(); foreach (var r in reps) { var k = new Vector2Int(Mathf.FloorToInt(w[r].x / cell), Mathf.FloorToInt(w[r].z / cell)); List<int> l; if (!grid.TryGetValue(k, out l)) grid[k] = l = new List<int>(); l.Add(r); }
        var ny = new Dictionary<int, float>(); int R3 = 3; float mx = 0, ss = 0;
        foreach (var r in reps)
        {
            var q = new Vector2(w[r].x, w[r].z); float d = bnd.Contains(r) ? 0 : B.Min(b => (b - q).magnitude); float tt = Mathf.Clamp01(d / ease); float wt = tt * tt * tt * (tt * (tt * 6 - 15) + 10); if (wt <= 0) { ny[r] = w[r].y; continue; }
            var c0 = new Vector2Int(Mathf.FloorToInt(q.x / cell), Mathf.FloorToInt(q.y / cell)); float sw = 0, sy = 0;
            for (int a = -R3; a <= R3; a++) for (int b = -R3; b <= R3; b++) { List<int> l; if (!grid.TryGetValue(new Vector2Int(c0.x + a, c0.y + b), out l)) continue; foreach (var j in l) { float dd = (new Vector2(w[j].x, w[j].z) - q).sqrMagnitude; float g = Mathf.Exp(-0.5f * dd / (sigma * sigma)); sw += g; sy += g * w[j].y; } }
            float y = Mathf.Lerp(w[r].y, sy / sw, wt); ny[r] = y; mx = Mathf.Max(mx, Mathf.Abs(y - w[r].y)); ss += (y - w[r].y) * (y - w[r].y);
        }
        System.Func<Vector2, bool, string> Pin = (p, after) =>
        {
            var pts = reps.Where(r => (new Vector2(w[r].x, w[r].z) - p).magnitude <= 0.45f).Select(r => new Vector3(w[r].x, after ? ny[r] : w[r].y, w[r].z)).ToList(); if (pts.Count < 3) return "too few vertices";
            // least-squares plane y = a x + b z + c
            double sx = 0, sz = 0, sy2 = 0, sxx = 0, szz = 0, sxz = 0, sxy = 0, szy = 0; int n = pts.Count; foreach (var u in pts) { double x = u.x - p.x, z = u.z - p.y; sx += x; sz += z; sy2 += u.y; sxx += x * x; szz += z * z; sxz += x * z; sxy += x * u.y; szy += z * u.y; }
            var M = new double[,] { { sxx, sxz, sx }, { sxz, szz, sz }, { sx, sz, n } }; var Y = new[] { sxy, szy, sy2 };
            System.Func<double[,], double> Det = A => A[0, 0] * (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1]) - A[0, 1] * (A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0]) + A[0, 2] * (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]); double D = Det(M); if (System.Math.Abs(D) < 1e-12) return "degenerate";
            var co = new double[3]; for (int c = 0; c < 3; c++) { var A = (double[,])M.Clone(); for (int rr = 0; rr < 3; rr++) A[rr, c] = Y[rr]; co[c] = Det(A) / D; }
            float plane = Mathf.Rad2Deg * Mathf.Atan((float)System.Math.Sqrt(co[0] * co[0] + co[1] * co[1])); float mxp = 0;
            for (int i = 0; i < pts.Count; i++) for (int j = i + 1; j < pts.Count; j++) { float h = new Vector2(pts[i].x - pts[j].x, pts[i].z - pts[j].z).magnitude; if (h >= 0.3f) mxp = Mathf.Max(mxp, Mathf.Rad2Deg * Mathf.Atan(Mathf.Abs(pts[i].y - pts[j].y) / h)); }
            return "plane " + plane.ToString("F2") + " deg, max " + mxp.ToString("F2") + " deg (" + pts.Count + " v) " + (plane <= 1.7f ? "PASS" : "FAIL");
        };
        var o = new List<string> { greenName + ": " + reps.Length + " vertices, boundary " + bnd.Count + ", sigma " + sigma + " ease " + ease + ": max change " + mx.ToString("F3") + " m, rms " + Mathf.Sqrt(ss / reps.Length).ToString("F3") + " m" };
        foreach (var s in pins.Split(';').Where(s => s.Length > 0)) { var a = s.Split(','); var p = new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci)); o.Add("pin (" + s + "): before " + Pin(p, false) + " | after " + Pin(p, true)); }
        if (!apply) return "(dry run) " + string.Join("\n", o.ToArray());
        var nv = new Vector3[N]; for (int i = 0; i < N; i++) { var p = w[i]; p.y = ny[weld[i]]; nv[i] = gm.transform.InverseTransformPoint(p); }
        const string dir = "Assets/TierraRejada/generated/greenlow"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "greenlow");
        var nm = Object.Instantiate(m); nm.name = m.name.Replace("(Clone)", "") + "_gl"; nm.vertices = nv; nm.RecalculateNormals(); nm.RecalculateBounds();
        var ap = dir + "/" + gm.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(m) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("greenlow|" + gm.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(m)).transform.SetParent(log.transform, false);
        var mc = gm.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == m) { mc.sharedMesh = null; mc.sharedMesh = nm; } gm.sharedMesh = nm; UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return string.Join("\n", o.ToArray());
    }
    // rough -> fairway retype (BoJa 4: "rough still in the fairway, left side up toward the green"): triangles of rough mesh `meshName` whose
    // centre lies within `reach` m of fairway mesh `fromName`, within `ease` m of any OPCD mesh whose name contains one of `words` (bunkers /
    // collar), inside the box x0..x1, z0..z1, move to a new mesh "<fromName>_ext_<tag>" with the fairway's material, its first vertex colour
    // and its collider's physic material; the rough mesh keeps the rest (A-owned copy). Heights unchanged, so no new step. Dry run unless apply.
    public string RetypeToFairway()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var mfs = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList();
        var rf = mfs.FirstOrDefault(f => f.name == meshName); var ff = mfs.FirstOrDefault(f => f.name == fromName); if (!rf || !ff) return "need meshName (rough) and fromName (fairway)";
        var fw = ff.sharedMesh.vertices.Select(v => { var p = ff.transform.TransformPoint(v); return new Vector2(p.x, p.z); }).ToArray(); var ws = words.ToLower().Split(',');
        var near = mfs.Where(f => ws.Any(wd => f.name.ToLower().Contains(wd))).SelectMany(f => f.sharedMesh.vertices.Select(v => { var p = f.transform.TransformPoint(v); return new Vector2(p.x, p.z); })).Where(p => p.x > x0 - ease && p.x < x1 + ease && p.y > z0 - ease && p.y < z1 + ease).ToArray();
        var fwl = fw.Where(p => p.x > x0 - reach && p.x < x1 + reach && p.y > z0 - reach && p.y < z1 + reach).ToArray();
        var src = rf.sharedMesh; var w = src.vertices.Select(v => rf.transform.TransformPoint(v)).ToArray(); var keep = new List<int>(); var move = new List<int>(); float area = 0;
        for (int sm = 0; sm < src.subMeshCount; sm++) { var t = src.GetTriangles(sm); for (int i = 0; i < t.Length; i += 3) { var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; var q = new Vector2(c.x, c.z); bool sel = sm == 0 && q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1 && fwl.Any(p => (p - q).sqrMagnitude < reach * reach) && near.Any(p => (p - q).sqrMagnitude < ease * ease); if (sel) { move.AddRange(new[] { t[i], t[i + 1], t[i + 2] }); area += Vector3.Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]).magnitude * 0.5f; } } }
        string res = move.Count / 3 + " rough triangles (" + area.ToString("F0") + " m2) -> fairway";
        if (!apply || move.Count == 0) return (apply ? "" : "(dry run) ") + res;
        const string dir = "Assets/TierraRejada/generated/retype"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "retype");
        var idx = move.Distinct().ToList(); var map = idx.Select((v, i) => new KeyValuePair<int, int>(v, i)).ToDictionary(k => k.Key, k => k.Value); var fcol = ff.sharedMesh.colors.Length > 0 ? ff.sharedMesh.colors[0] : Color.white;
        var nm = new Mesh { name = fromName + "_ext_" + tag, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; nm.vertices = idx.Select(i => w[i]).ToArray(); if (src.uv.Length == w.Length) nm.uv = idx.Select(i => src.uv[i]).ToArray(); nm.colors = Enumerable.Repeat(fcol, idx.Count).ToArray(); nm.triangles = move.Select(i => map[i]).ToArray(); nm.RecalculateNormals(); nm.RecalculateBounds();
        UnityEditor.AssetDatabase.CreateAsset(nm, dir + "/" + nm.name + ".asset");
        var go = new GameObject(nm.name); go.transform.SetParent(opcdT, false); go.layer = ff.gameObject.layer; go.AddComponent<MeshFilter>().sharedMesh = nm; go.AddComponent<MeshRenderer>().sharedMaterials = ff.GetComponent<MeshRenderer>().sharedMaterials.Take(1).ToArray(); var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = nm; var fc = ff.GetComponent<Collider>(); if (fc) mc.sharedMaterial = fc.sharedMaterial;
        var rm = Object.Instantiate(src); rm.name = src.name.Replace("(Clone)", "") + "_rt"; var mv = new HashSet<long>(); for (int i = 0; i < move.Count; i += 3) mv.Add(((long)move[i] << 42) | ((long)move[i + 1] << 21) | (long)move[i + 2]);
        for (int sm = 0; sm < src.subMeshCount; sm++) { var t = src.GetTriangles(sm); var k = new List<int>(); for (int i = 0; i < t.Length; i += 3) if (!mv.Contains(((long)t[i] << 42) | ((long)t[i + 1] << 21) | (long)t[i + 2])) k.AddRange(new[] { t[i], t[i + 1], t[i + 2] }); rm.SetTriangles(k, sm); }
        var ap = dir + "/" + rf.name + "_" + tag + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(rm, ap); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("retype|" + rf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src) + "|" + nm.name).transform.SetParent(log.transform, false);
        var rc = rf.GetComponent<MeshCollider>(); if (rc && rc.sharedMesh == src) { rc.sharedMesh = null; rc.sharedMesh = rm; } rf.sharedMesh = rm; UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return res;
    }
    // local membrane re-grade of mesh `meshName` (e.g. hole 1: rough strip with a 0.27 m ledge under the fairway edge; h1seam bank): vertices
    // inside the box x0..x1, z0..z1 and within `reach` m of any mesh named with one of `words` are re-solved (Laplace); this mesh's own rim
    // there is pinned to the surface beside it, everything else stays put, so the region meets its neighbours with no step and no ridge.
    // A-owned copy (generated/regionlap), collider follows. Dry run unless apply.
    public string RegionLaplace()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var mfs = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList(); var mf = mfs.FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var wsd = words.ToLower().Split(','); var refs = mfs.Where(f => f != mf && wsd.Any(s => f.name.ToLower().Contains(s))).SelectMany(f => f.sharedMesh.vertices.Select(v => { var p = f.transform.TransformPoint(v); return new Vector2(p.x, p.z); })).Where(p => p.x > x0 - reach && p.x < x1 + reach && p.y > z0 - reach && p.y < z1 + reach).ToArray();
        var src = mf.sharedMesh; var col = mf.GetComponent<Collider>(); var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = src.triangles; int N = w.Length;
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[N]; for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); var nb = new Dictionary<int, HashSet<int>>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; HashSet<int> s; if (!nb.TryGetValue(a, out s)) nb[a] = s = new HashSet<int>(); s.Add(b); if (!nb.TryGetValue(b, out s)) nb[b] = s = new HashSet<int>(); s.Add(a); }
        var bnd = new HashSet<int>(); foreach (var kv in cnt) if (kv.Value == 1) { bnd.Add((int)(kv.Key >> 32)); bnd.Add((int)(kv.Key & 0xffffffff)); }
        System.Func<int, bool> InReg = r => { var q = new Vector2(w[r].x, w[r].z); return q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1 && refs.Any(p => (p - q).sqrMagnitude < reach * reach); };
        var reps = wk.Values.ToList(); var y = reps.ToDictionary(r => r, r => w[r].y); var free = new List<int>(); int pinned = 0;
        foreach (var r in reps)
        {
            if (!InReg(r)) continue; if (!bnd.Contains(r)) { free.Add(r); continue; }
            var bn = nb[r].Where(q => bnd.Contains(q) && cnt[K(r, q)] == 1).ToList(); if (bn.Count < 2) continue; var tan = new Vector2(w[bn[1]].x - w[bn[0]].x, w[bn[1]].z - w[bn[0]].z).normalized; var nrm = new Vector2(-tan.y, tan.x);
            foreach (var sgn in new[] { 1f, -1f }) { var q = new Vector2(w[r].x, w[r].z) + nrm * 0.12f * sgn; var hits = Physics.RaycastAll(new Vector3(q.x, w[r].y + 5, q.y), Vector3.down, 20).Where(h => h.collider.transform.IsChildOf(opcdT)).OrderBy(h => h.distance).ToList(); if (hits.Count == 0 || hits[0].collider == col || !wsd.Any(s => hits[0].collider.name.ToLower().Contains(s)) || Mathf.Abs(hits[0].point.y - w[r].y) > (ease > 0 ? ease : 0.35f)) continue; y[r] = hits[0].point.y; pinned++; break; }   /* ease > 0: pin cap in m */
        }
        for (int it = 0; it < 500; it++) foreach (var r in free) { HashSet<int> s; if (!nb.TryGetValue(r, out s) || s.Count == 0) continue; y[r] = s.Average(q => y[weld[q]]); }
        float mx = 0, up = 0; foreach (var r in reps) { float d = y[r] - w[r].y; mx = Mathf.Max(mx, Mathf.Abs(d)); up = Mathf.Max(up, d); }
        string res = meshName + ": " + free.Count + " vertices re-solved, " + pinned + " rim pinned, largest move " + (mx * 100).ToString("F1") + " cm (max rise " + (up * 100).ToString("F1") + " cm)";
        if (!apply) return "(dry run) " + res;
        var nv = new Vector3[N]; for (int i = 0; i < N; i++) { var p = w[i]; p.y = y[weld[i]]; nv[i] = mf.transform.InverseTransformPoint(p); }
        const string dir = "Assets/TierraRejada/generated/regionlap"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "regionlap");
        var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_rl"; nm.vertices = nv; nm.RecalculateNormals(); nm.RecalculateBounds(); var ap = dir + "/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("regionlap|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
        var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; } mf.sharedMesh = nm; UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return res;
    }
    // fill a path ring's grass centre (BoJa 13: "the roundabout should not have a gap of grass in the middle"): path object `fromName` (under
    // CART_PATHS) around (cx, cz); the ring's inner radius is the nearest of its vertices to the centre; a disc of that radius + 0.4 m (tucked
    // under the ring's inner edge) at the ring's own inner-rim height (inverse-distance from 48 rim samples, +5 mm), same material, collider and
    // physic material, named <fromName>_fill. Dry run unless apply.
    public string PathDisc()
    {
        Physics.SyncTransforms(); var cp = GameObject.Find("CART_PATHS"); var pf = cp ? cp.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == fromName) : null; if (!pf) return "no " + fromName;
        var c = new Vector2(cx, cz); var pw = pf.sharedMesh.vertices.Select(v => pf.transform.TransformPoint(v)).Where(p => (new Vector2(p.x, p.z) - c).magnitude < 12).ToList(); if (pw.Count == 0) return "no ring vertices near the centre";
        float rin = pw.Min(p => (new Vector2(p.x, p.z) - c).magnitude); var pcol = pf.GetComponent<Collider>(); var rim = new List<Vector3>();
        for (int k = 0; k < 48; k++) { float a = k * Mathf.PI * 2 / 48; var q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rin + 0.25f); foreach (var h in Physics.RaycastAll(new Vector3(q.x, 400, q.y), Vector3.down, 800)) if (h.collider == pcol) { rim.Add(h.point); break; } }
        if (rim.Count < 12) return "ring top found at only " + rim.Count + " of 48 rim samples (inner radius " + rin.ToString("F2") + ")";
        float R = rin + (reach > 0 ? reach : 0.4f); float lift = reach > 0 ? -0.01f : 0.005f;   /* reach > 0: outer under-disc that only shows in the ring's notches */ int rings = Mathf.Max(3, Mathf.CeilToInt(R / 0.5f)), seg = 48; var V = new List<Vector3>(); var T = new List<int>(); var U = new List<Vector2>();
        var opcdT = GameObject.Find("OPCD Meshes").transform; System.Func<Vector2, float> Ground = q => { float g = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 400, q.y), Vector3.down, 800)) if (h.collider.transform.IsChildOf(opcdT) || h.collider is TerrainCollider) g = Mathf.Max(g, h.point.y); foreach (var te in Terrain.activeTerrains) { var tp = te.transform.position; var sz = te.terrainData.size; if (q.x >= tp.x && q.x <= tp.x + sz.x && q.y >= tp.z && q.y <= tp.z + sz.z) g = Mathf.Max(g, te.SampleHeight(new Vector3(q.x, 0, q.y)) + tp.y); } return g; };   /* terrain too: its collider may be off */
        System.Func<Vector2, float> Hgt = q => { float sw = 0, sy = 0; foreach (var p in rim) { float d2 = Mathf.Max(0.01f, (new Vector2(p.x, p.z) - q).sqrMagnitude); sw += 1 / d2; sy += p.y / d2; } return Mathf.Max(sy / sw + lift, Ground(q) + 0.01f); };   /* never under the turf */
        V.Add(new Vector3(c.x, Hgt(c), c.y)); U.Add(new Vector2(0.5f, c.y * 0.25f));   /* strip texture: stay in its middle (its edges are grass verges) */
        for (int r = 1; r <= rings; r++) for (int k = 0; k < seg; k++) { float a = k * Mathf.PI * 2 / seg; var q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R * r / rings); V.Add(new Vector3(q.x, Hgt(q), q.y)); U.Add(new Vector2(0.5f, q.y * 0.25f)); }
        for (int k = 0; k < seg; k++) T.AddRange(new[] { 0, 1 + (k + 1) % seg, 1 + k });
        for (int r = 1; r < rings; r++) for (int k = 0; k < seg; k++) { int a = 1 + (r - 1) * seg + k, b = 1 + (r - 1) * seg + (k + 1) % seg, d = 1 + r * seg + k, e = 1 + r * seg + (k + 1) % seg; T.AddRange(new[] { a, b, d, b, e, d }); }
        string res = fromName + ": ring inner radius " + rin.ToString("F2") + " m, disc radius " + R.ToString("F2") + " m, rim height " + rim.Min(p => p.y).ToString("F2") + "-" + rim.Max(p => p.y).ToString("F2") + " (" + rim.Count + " samples)";
        if (!apply) return "(dry run) " + res;
        var m = new Mesh { name = fromName + (reach > 0 ? "_fill_outer" : "_fill") }; m.SetVertices(V); m.SetTriangles(T, 0); m.SetUVs(0, U); m.RecalculateNormals(); if (m.normals[0].y < 0) { var tt = m.triangles; for (int i = 0; i < tt.Length; i += 3) { var x = tt[i + 1]; tt[i + 1] = tt[i + 2]; tt[i + 2] = x; } m.triangles = tt; m.RecalculateNormals(); } m.RecalculateBounds();
        const string dir = "Assets/TierraRejada/generated/paths"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "paths"); var ap = dir + "/" + m.name + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var old = cp.transform.Find(m.name); if (old) Object.DestroyImmediate(old.gameObject);   /* only ever this tool's own fill */ var go = new GameObject(m.name); go.transform.SetParent(cp.transform, false); go.layer = pf.gameObject.layer; go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterials = pf.GetComponent<MeshRenderer>().sharedMaterials.Take(1).ToArray(); var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; if (pcol) mc.sharedMaterial = pcol.sharedMaterial;
        UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cp.scene); return res;
    }
    // crowns over mown turf (BoJa 16: "16 still has trees on the green"; the trunk test missed overhanging canopies): every active LODGroup
    // plant in the box x0..x1, z0..z1 (all roots) gets its crown footprint (renderer bounds radius, xz) sampled at 0/0.33/0.66/1.0 of the
    // radius in 16 directions; a sample whose top OPCD surface is a green, collar, fairway or tee mesh is a hit. Reports plant, surface type and
    // overhang (crown radius minus distance to the nearest hit). With apply, plants hitting a green or collar (only) are moved straight away
    // from the hit until the crown clears it by `gap` m (logged MOVE in NIGHT1031_LOG, never deleted). `csvPath` gets every hit.
    public bool shrink = false;
    public string CrownsOver()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); var rows = new List<string> { "root,name,x,z,radius,surface,overhang" }; int moved = 0;
        var log = apply ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        System.Func<Vector2, string> Top = q => { var h = Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200).Where(r => r.collider.transform.IsChildOf(opcdT)).OrderBy(r => r.distance).FirstOrDefault(); if (!h.collider) return null; var n = h.collider.name.ToLower(); return n.Contains("_green_-_mesh") || n.Contains("green_-_blend") ? "green" : n.Contains("collar") ? "collar" : n.Contains("_tee_") ? "tee" : n.Contains("fairway") ? "fairway" : null; };
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.activeInHierarchy && g.name != "OPCD Meshes"))
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var p = lg.transform.position; if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue; var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float R = Mathf.Max(0.3f, Mathf.Min(b.extents.x, b.extents.z)); var c = new Vector2(p.x, p.z); string worst = null; float over = 0; Vector2 hitAt = c;
                for (int k = 0; k < 16; k++) foreach (var f in new[] { 0f, 0.33f, 0.66f, 1f }) { if (f == 0 && k > 0) continue; var q = c + new Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8)) * R * f; var s = Top(q); if (s == null) continue; float ov = R - (q - c).magnitude; if (worst == null || (s == "green" || s == "collar") && !(worst == "green" || worst == "collar") || ov > over) { worst = s; over = ov; hitAt = q; } }
                if (worst == null) continue; rows.Add(root.name + "," + lg.name + "," + p.x.ToString("F1") + "," + p.z.ToString("F1") + "," + R.ToString("F1") + "," + worst + "," + over.ToString("F2"));
                string line = root.name + "/" + lg.name + " (" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ") crown r " + R.ToString("F1") + " m over " + worst + " by " + over.ToString("F1") + " m";
                var clearOf = (words.Length > 0 && words != "*" ? words : "green,collar").Split(',');
                if (apply && shrink && clearOf.Contains(worst)) { if (over > 0.05f) { float s = Mathf.Clamp((R - over - 0.3f) / R, 0.5f, 1f); new GameObject("shrink|" + root.name + "/" + lg.name + "|" + p.ToString("F3") + "|" + lg.transform.localScale.x.ToString("F3")).transform.SetParent(log.transform, false); lg.transform.localScale *= s; moved++; line += " -> crown scaled x" + s.ToString("F2"); } o.Add(line); continue; }   /* BoJa default: shrink so the crown stops at the mown edge */
                if (apply && clearOf.Contains(worst))
                {
                    var away = (c - hitAt).sqrMagnitude > 1e-4f ? (c - hitAt).normalized : new Vector2(1, 0); var np = c; bool found = false; float lim = reach > 0 ? reach : 25; for (float d = 0.5f; d <= lim; d += 0.5f) { np = c + away * d; bool clear = true; for (int k = 0; k < 16 && clear; k++) foreach (var f in new[] { 0.5f, 1f }) { var q = np + new Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8)) * (R + gap) * f; var s = Top(q); if (s != null) { clear = false; break; }   /* never land a crown on any mown turf (BoJa: nothing moved into a fairway without his OK) */ } if (clear) { found = true; break; } }
                    if (!found) { o.Add(line + " -> cannot clear within " + lim + " m, left for BoJa"); continue; }
                    float gy = float.MinValue; foreach (var hh in Physics.RaycastAll(new Vector3(np.x, 600, np.y), Vector3.down, 1200)) { if (hh.collider.transform.IsChildOf(lg.transform)) continue; if (hh.collider.transform.IsChildOf(opcdT) || hh.collider is TerrainCollider) gy = Mathf.Max(gy, hh.point.y); }
                    new GameObject("move|" + root.name + "/" + lg.name + "|" + p.ToString("F3") + "|crown").transform.SetParent(log.transform, false); lg.transform.position = new Vector3(np.x, gy > float.MinValue ? gy - 0.05f : p.y, np.y); moved++; line += " -> moved " + (np - c).magnitude.ToString("F1") + " m";
                }
                o.Add(line);
            }
        if (csvPath.Length > 0) System.IO.File.WriteAllLines(csvPath, rows.ToArray()); if (apply) { Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return o.Count + " plants with crowns over mown turf" + (apply ? ", " + moved + " moved off greens/collars" : "") + "\n" + string.Join("\n", o.OrderByDescending(s => s.Contains(" green ") || s.Contains(" collar ")).Take(60).ToArray());
    }
    // undo CollarLaplace on `meshName`: the mesh it replaced (from its "collarfeather|" log entry) goes back on the filter and collider
    public string CollarRevert()
    {
        var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == meshName); var log = GameObject.Find("NIGHT1031_LOG"); if (!mf || !log) return "no " + meshName;
        var e = log.transform.Cast<Transform>().LastOrDefault(t => t.name.StartsWith("collarfeather|" + meshName + "|")); if (!e) return "no collarfeather entry for " + meshName;
        var src = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(e.name.Split('|')[2]); if (!src) { var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(e.name.Split('|')[2]).OfType<Mesh>().ToList(); src = all.FirstOrDefault(m => m.name == mf.sharedMesh.name.Replace("_cf", "")) ?? all.FirstOrDefault(); } if (!src) return "source mesh not found at " + e.name.Split('|')[2];
        var cur = mf.sharedMesh; var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == cur) { mc.sharedMesh = null; mc.sharedMesh = src; } mf.sharedMesh = src; e.name = "un" + e.name; Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return meshName + " back to " + src.name;
    }
    // read-only green-edge survey (BoJa: greens and collars "act like bumpers"): for each active "Spline_green_*_Green_-_Mesh" (or the one
    // `greenName`), boundary points every ~1 m; along the outward normal from -0.5 to +2.0 m every 5 cm the top OPCD surface height (first
    // collider from above). Step = largest jump between neighbouring samples; lip = largest rise above the straight chord from -0.5 m to +2.0 m.
    // Per green: points, max/p90 step, points with step > 1 cm, max lip, points with lip > 2 cm, worst spots. `csvPath` gets every point.
    public string EdgeSurvey()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); var rows = new List<string> { "green,x,z,step,lip,stepAt,surfaces" };
        foreach (var gm in opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && (greenName.Length > 0 ? f.name == greenName : System.Text.RegularExpressions.Regex.IsMatch(f.name, @"^Spline_green_\d+_Green_-_Mesh$"))))
        {
            var w = gm.sharedMesh.vertices.Select(v => gm.transform.TransformPoint(v)).ToArray(); var t = gm.sharedMesh.triangles; var wk = new Dictionary<Vector2Int, int>(); var weld = new int[w.Length];
            for (int i = 0; i < w.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!wk.TryGetValue(k, out j)) { j = i; wk[k] = i; } weld[i] = j; }
            var cnt = new Dictionary<long, int>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(weld[t[i + e]], weld[t[i + (e + 1) % 3]]); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
            var edges = cnt.Where(kv => kv.Value == 1).Select(kv => new[] { (int)(kv.Key >> 32), (int)(kv.Key & 0xffffffff) }).ToList(); if (edges.Count == 0) continue;
            var cen = new Vector2(w.Average(v => v.x), w.Average(v => v.z)); var steps = new List<float>(); var lips = new List<float>(); var worst = new List<KeyValuePair<float, string>>(); float acc = 0;
            foreach (var e in edges)
            {
                Vector2 a = new Vector2(w[e[0]].x, w[e[0]].z), b = new Vector2(w[e[1]].x, w[e[1]].z); float L = (b - a).magnitude; acc += L; if (acc < 1f) continue; acc = 0;
                var m = (a + b) / 2; var nrm = new Vector2(-(b - a).y, (b - a).x).normalized; if (Vector2.Dot(nrm, m - cen) < 0) nrm = -nrm;
                var ys = new List<float>(); var names = new List<string>();
                for (float d = -0.5f; d <= 2.0001f; d += 0.05f) { var q = m + nrm * d; var h = Physics.RaycastAll(new Vector3(q.x, 400, q.y), Vector3.down, 800).Where(r => r.collider.transform.IsChildOf(opcdT)).OrderBy(r => r.distance).FirstOrDefault(); ys.Add(h.collider ? h.point.y : float.NaN); names.Add(h.collider ? h.collider.name : "-"); }
                if (ys.Any(float.IsNaN)) continue; float st = 0; int si = 0; for (int i = 1; i < ys.Count; i++) { float s = Mathf.Abs(ys[i] - ys[i - 1]); if (s > st) { st = s; si = i; } }
                float lp = 0; for (int i = 0; i < ys.Count; i++) { float chord = Mathf.Lerp(ys[0], ys[ys.Count - 1], i / (float)(ys.Count - 1)); lp = Mathf.Max(lp, ys[i] - chord); }
                steps.Add(st); lips.Add(lp); string surf = string.Join(">", names.Distinct().Select(s => s.Replace("_-_Mesh", "").Replace("Spline_", "")).ToArray());
                rows.Add(gm.name + "," + m.x.ToString("F2") + "," + m.y.ToString("F2") + "," + st.ToString("F3") + "," + lp.ToString("F3") + "," + (-0.5f + si * 0.05f).ToString("F2") + "," + surf);
                worst.Add(new KeyValuePair<float, string>(st + lp, "(" + m.x.ToString("F1") + "," + m.y.ToString("F1") + ") step " + (st * 100).ToString("F1") + " cm at " + (-0.5f + si * 0.05f).ToString("F2") + " m, lip " + (lp * 100).ToString("F1") + " cm [" + names[Mathf.Max(0, si - 1)] + " > " + names[si] + "]"));
            }
            if (steps.Count == 0) continue; var ss = steps.OrderBy(x => x).ToList();
            o.Add(gm.name + ": " + steps.Count + " pts, step max " + (ss.Last() * 100).ToString("F1") + " cm p90 " + (ss[(int)(0.9f * (ss.Count - 1))] * 100).ToString("F1") + " cm, " + steps.Count(x => x > 0.01f) + " pts > 1 cm; lip max " + (lips.Max() * 100).ToString("F1") + " cm, " + lips.Count(x => x > 0.02f) + " pts > 2 cm. worst: " + string.Join(" | ", worst.OrderByDescending(k => k.Key).Take(2).Select(k => k.Value).ToArray()));
        }
        if (csvPath.Length > 0) System.IO.File.WriteAllLines(csvPath, rows.ToArray());
        return string.Join("\n", o.ToArray());
    }
    // read-only: near-vertical faces (grade over 100%) in OPCD meshes whose name contains one of `words` (e.g. "collar,green"): per mesh the
    // count, area, tallest face, and how far its top stands above the highest OTHER OPCD surface right next to it (a "bumper" sticks up)
    public string Walls()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var ws = words.ToLower().Split(','); var o = new List<string>();
        foreach (var mf in opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && ws.Any(w => f.name.ToLower().Contains(w))))
        {
            var w = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = mf.sharedMesh.triangles; int n = 0; float area = 0, tall = 0, proud = -9; Vector3 at = Vector3.zero; var col = mf.GetComponent<Collider>();
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = w[t[i]], b = w[t[i + 1]], d = w[t[i + 2]]; var nrm = Vector3.Cross(b - a, d - a); float ar = nrm.magnitude * 0.5f; if (ar < 1e-5f) continue; nrm.Normalize(); if (Mathf.Abs(nrm.y) > 0.7071f) continue;
                n++; area += ar; float top = Mathf.Max(a.y, Mathf.Max(b.y, d.y)), bot = Mathf.Min(a.y, Mathf.Min(b.y, d.y)); tall = Mathf.Max(tall, top - bot);
                var cen = (a + b + d) / 3; float other = float.NegativeInfinity; foreach (var off in new[] { new Vector3(0.15f, 0, 0), new Vector3(-0.15f, 0, 0), new Vector3(0, 0, 0.15f), new Vector3(0, 0, -0.15f) }) foreach (var h in Physics.RaycastAll(new Vector3(cen.x + off.x, top + 5, cen.z + off.z), Vector3.down, 20)) if (h.collider != col && h.collider.transform.IsChildOf(opcdT)) other = Mathf.Max(other, h.point.y);
                if (!float.IsInfinity(other) && top - other > proud) { proud = top - other; at = cen; }
            }
            if (n > 0) o.Add(mf.name + ": " + n + " near-vertical faces, " + area.ToString("F2") + " m2, tallest " + tall.ToString("F2") + " m, top stands " + proud.ToString("+0.00;-0.00") + " m above the neighbouring surface at (" + at.x.ToString("F1") + "," + at.z.ToString("F1") + ")" + (col ? " [" + col.GetType().Name + "]" : " [no collider]"));
        }
        return o.Count + " meshes with near-vertical faces\n" + string.Join("\n", o.ToArray());
    }
    // read-only: every OPCD surface within `r` m of (cx, cz): collider type, physics material, layer, and over that disc the area-weighted slope
    // of its triangles (mean, p90, max, % of area over 3% and 4% grade), the largest single-edge step, and any non-OPCD collider within r
    public string GreenInfo()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var c = new Vector2(cx, cz); var o = new List<string>();
        foreach (var mf in opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh))
        {
            var bb = mf.GetComponent<Renderer>().bounds; if (bb.max.x < cx - r || bb.min.x > cx + r || bb.max.z < cz - r || bb.min.z > cz + r) continue;
            var w = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var t = mf.sharedMesh.triangles; var sl = new List<KeyValuePair<float, float>>(); float step = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = w[t[i]], b = w[t[i + 1]], d = w[t[i + 2]]; var cen = (a + b + d) / 3; if ((new Vector2(cen.x, cen.z) - c).magnitude > r) continue; var nrm = Vector3.Cross(b - a, d - a); float ar = nrm.magnitude * 0.5f; if (ar < 1e-6f) continue; nrm.Normalize(); if (nrm.y < 0) nrm = -nrm;
                float g = 100f * Mathf.Sqrt(1 - nrm.y * nrm.y) / Mathf.Max(1e-4f, nrm.y); sl.Add(new KeyValuePair<float, float>(g, ar)); foreach (var e in new[] { new[] { a, b }, new[] { b, d }, new[] { d, a } }) { float hz = new Vector2(e[0].x - e[1].x, e[0].z - e[1].z).magnitude; if (hz > 0.01f) step = Mathf.Max(step, Mathf.Abs(e[0].y - e[1].y) / hz * 100); }
            }
            if (sl.Count == 0) continue; float A = sl.Sum(k => k.Value); var srt = sl.OrderBy(k => k.Key).ToList(); float acc = 0, p90 = 0; foreach (var k in srt) { acc += k.Value; if (acc >= 0.9f * A) { p90 = k.Key; break; } }
            var col = mf.GetComponent<Collider>(); string pm = col ? (col.sharedMaterial ? col.sharedMaterial.name + " (dyn " + col.sharedMaterial.dynamicFriction + ", bounce " + col.sharedMaterial.bounciness + ")" : "no physic material") : "NO COLLIDER";
            o.Add(mf.name + ": " + A.ToString("F0") + " m2, slope mean " + (sl.Sum(k => k.Key * k.Value) / A).ToString("F1") + "% p90 " + p90.ToString("F1") + "% max " + srt.Last().Key.ToString("F1") + "%, >3% " + (100 * sl.Where(k => k.Key > 3).Sum(k => k.Value) / A).ToString("F0") + "% of area, >4% " + (100 * sl.Where(k => k.Key > 4).Sum(k => k.Value) / A).ToString("F0") + "%, steepest edge " + step.ToString("F0") + "% | " + (col ? col.GetType().Name : "") + " " + pm + " layer " + LayerMask.LayerToName(mf.gameObject.layer));
        }
        foreach (var cl in Object.FindObjectsOfType<Collider>()) { if (cl.transform.IsChildOf(opcdT) || cl is TerrainCollider) continue; var b = cl.bounds; if ((new Vector2(b.center.x, b.center.z) - c).magnitude > r + b.extents.magnitude) continue; string p = cl.name; for (var u = cl.transform.parent; u; u = u.parent) p = u.name + "/" + p; o.Add("other collider: " + p + " " + cl.GetType().Name + " at (" + b.center.x.ToString("F1") + "," + b.center.y.ToString("F1") + "," + b.center.z.ToString("F1") + ") size " + b.size.ToString("F1")); }
        return string.Join("\n", o.ToArray());
    }
    // read-only: grid every `gap` m inside polygon `poly` ("x,z;..."): cells whose top OPCD surface name has none of `words`, grouped by
    // surface name with their area and bounding box (e.g. rough inside an SVG fairway outline)
    public string SurfaceGrid()
    {
        Physics.SyncTransforms(); var ci = System.Globalization.CultureInfo.InvariantCulture; var opcdT = GameObject.Find("OPCD Meshes").transform; var ws = words.ToLower().Split(',');
        var P = poly.Split(';').Where(q => q.Length > 0).Select(q => { var a = q.Split(','); return new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci)); }).ToList();
        System.Func<Vector2, bool> In = q => { bool c = false; for (int i = 0, j = P.Count - 1; i < P.Count; j = i++) if ((P[i].y > q.y) != (P[j].y > q.y) && q.x < (P[j].x - P[i].x) * (q.y - P[i].y) / (P[j].y - P[i].y) + P[i].x) c = !c; return c; };
        float s = Mathf.Max(0.25f, gap); var g = new Dictionary<string, List<Vector2>>(); int tot = 0;
        for (float x = P.Min(q => q.x); x <= P.Max(q => q.x); x += s) for (float z = P.Min(q => q.y); z <= P.Max(q => q.y); z += s)
            {
                var q = new Vector2(x, z); if (!In(q)) continue; tot++; var h = Physics.RaycastAll(new Vector3(x, 400, z), Vector3.down, 800).Where(r => r.collider.transform.IsChildOf(opcdT)).OrderBy(r => r.distance).FirstOrDefault(); string n = h.collider ? h.collider.name : "(none)";
                if (ws.Any(w => n.ToLower().Contains(w))) continue; List<Vector2> l; if (!g.TryGetValue(n, out l)) g[n] = l = new List<Vector2>(); l.Add(q);
            }
        return tot + " cells inside, " + g.Sum(k => k.Value.Count) + " not " + words + "\n" + string.Join("\n", g.OrderByDescending(k => k.Value.Count).Select(k => k.Key + ": " + (k.Value.Count * s * s).ToString("F0") + " m2, x " + k.Value.Min(v => v.x).ToString("F0") + "-" + k.Value.Max(v => v.x).ToString("F0") + " z " + k.Value.Min(v => v.y).ToString("F0") + "-" + k.Value.Max(v => v.y).ToString("F0") + " e.g. (" + k.Value[k.Value.Count / 2].x.ToString("F1") + "," + k.Value[k.Value.Count / 2].y.ToString("F1") + ")").ToArray());
    }
    // read-only: ground profile from (px, pz) to (cx, cz) every 0.25 m, as a ball sees it (first collider hit straight down, any layer):
    // reports every change of collider and every step over 3 cm or slope over 25 deg between samples, plus non-OPCD colliders on the line
    public string Profile()
    {
        Physics.SyncTransforms(); var a = new Vector2(px, pz); var b = new Vector2(cx, cz); int n = Mathf.CeilToInt((b - a).magnitude / 0.25f); var o = new List<string>(); string lastC = null; float lastY = float.NaN; var opcdT = GameObject.Find("OPCD Meshes").transform;
        for (int i = 0; i <= n; i++)
        {
            var q = Vector2.Lerp(a, b, i / (float)n); RaycastHit h; if (!Physics.Raycast(new Vector3(q.x, 400, q.y), Vector3.down, out h, 800)) { o.Add(i * 0.25f + " m: no hit"); continue; }
            var c = h.collider.name + (h.collider.transform.IsChildOf(opcdT) ? "" : " (NOT OPCD: " + h.collider.GetType().Name + " under " + h.collider.transform.root.name + ")"); float y = h.point.y;
            bool step = !float.IsNaN(lastY) && (Mathf.Abs(y - lastY) > 0.03f || Mathf.Atan(Mathf.Abs(y - lastY) / 0.25f) * Mathf.Rad2Deg > 25);
            if (c != lastC || step || i % 20 == 0) o.Add((i * 0.25f).ToString("F2") + " m (" + q.x.ToString("F1") + "," + q.y.ToString("F1") + ") y " + y.ToString("F3") + (step ? " STEP " + (y - lastY).ToString("+0.000;-0.000") : "") + "  " + c);
            lastC = c; lastY = y;
        }
        return string.Join("\n", o.ToArray());
    }
    // read-only: active trees (LODGroups over 3 m) in the box x0..x1, z0..z1 whose trunk stands over mown turf (the first OPCD mesh hit
    // straight down is a fairway, green, tee or collar), with the nearest NIGHT1031_LOG / SWAP_LOG entry naming them
    public string TreesOnTurf()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); var logs = new List<Transform>();
        foreach (var ln in new[] { "NIGHT1031_LOG", "SWAP_LOG" }) { var l = GameObject.Find(ln); if (l) logs.AddRange(l.transform.Cast<Transform>()); }
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.activeInHierarchy))
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var p = lg.transform.position; if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue; var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); if (b.size.y < 3) continue;
                var hit = Physics.RaycastAll(new Vector3(p.x, p.y + 30, p.z), Vector3.down, 80).Where(h => h.collider.transform.IsChildOf(opcdT)).OrderBy(h => h.distance).FirstOrDefault(); if (!hit.collider) continue;
                var hn = hit.collider.name.ToLower(); if (!(hn.Contains("fairway") || hn.Contains("green") || hn.Contains("_tee_") || hn.Contains("collar"))) continue;
                string near = ""; foreach (var t in logs) { var m = System.Text.RegularExpressions.Regex.Match(t.name, @"\((-?[\d.]+), (-?[\d.]+), (-?[\d.]+)\)"); if (!m.Success) continue; var q = new Vector2(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)); if ((q - new Vector2(p.x, p.z)).magnitude < 1.5f) near += " [" + t.name + "]"; }
                o.Add(root.name + "/" + lg.name + " (" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ") h " + b.size.y.ToString("F1") + " over " + hit.collider.name + near);
            }
        return o.Count + " trees over mown turf\n" + string.Join("\n", o.ToArray());
    }
    // read-only: meshes that only switched-off objects use (they still go into the bundle), grouped by their top switched-off ancestor, with an
    // estimate of their bundle weight (vertices x 48 bytes + indices x 4)
    public string InactiveWeight()
    {
        var all = Resources.FindObjectsOfTypeAll<MeshFilter>().Where(f => f.gameObject.scene.IsValid() && f.sharedMesh).ToList(); var live = new HashSet<Mesh>(all.Where(f => f.gameObject.activeInHierarchy).Select(f => f.sharedMesh));
        var grp = new Dictionary<string, long>(); var seen = new HashSet<Mesh>(); long tot = 0;
        foreach (var f in all.Where(f => !f.gameObject.activeInHierarchy))
        {
            var m = f.sharedMesh; if (live.Contains(m) || !seen.Add(m)) continue; long b = 48L * m.vertexCount; for (int s = 0; s < m.subMeshCount; s++) b += 4L * m.GetIndexCount(s);
            var t = f.transform; Transform off = t; for (var u = t; u; u = u.parent) if (!u.gameObject.activeSelf) off = u; string path = off.name; for (var u = off.parent; u; u = u.parent) path = u.name + "/" + path;
            long c; grp[path] = (grp.TryGetValue(path, out c) ? c : 0) + b; tot += b;
        }
        return "meshes used only by switched-off objects: ~" + (tot / 1048576) + " MB\n" + string.Join("\n", grp.OrderByDescending(k => k.Value).Take(25).Select(k => (k.Value / 1048576.0).ToString("F1") + " MB  " + k.Key).ToArray());
    }
    // remove a SwapMix root that SwapUndo already switched off (`propName`, must be inactive and logged as "unswap" in SWAP_LOG): inactive
    // objects still go into the bundle, and SAGEMIX_1101 carried 6 unused species (Nov 1 size cut). SwapMix can rebuild it.
    public string DropSwapRoot()
    {
        var r0 = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.transform.parent == null && g.name == propName); if (!r0) return "no " + propName;
        if (r0.activeSelf) return propName + " is active: SwapUndo it first"; var log = GameObject.Find("SWAP_LOG"); int un = log ? log.transform.Cast<Transform>().Count(t => t.name.StartsWith("unswap|")) : 0; if (un == 0) return "no unswap entries in SWAP_LOG";
        int n = r0.GetComponentsInChildren<LODGroup>(true).Length; Object.DestroyImmediate(r0); new GameObject("dropswap|" + propName + "|" + n + " plants").transform.SetParent(log.transform, false); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return propName + " removed (" + n + " inactive plants; " + un + " originals already back on)";
    }
    // switch back on the objects in `ids` ("root/path|...") that Off() switched off (inactive ones included)
    public string On()
    {
        var o = new List<string>();
        foreach (var p in ids.Split('|').Where(s => s.Length > 0))
        {
            var parts = p.Split('/'); var r0 = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.transform.parent == null && g.name == parts[0]);
            var t = r0 ? (parts.Length > 1 ? r0.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => { string q = x.name; for (var u = x.parent; u; u = u.parent) q = u.name + "/" + q; return q == p; }) : r0.transform) : null;
            if (!t) { o.Add(p + " not found"); continue; } t.gameObject.SetActive(true); o.Add(p + " on");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); return string.Join("; ", o.ToArray());
    }
    // what is at pixel (u, v) (0..1, v from the top) of a camera at (px, py above ground, pz) looking at (lx, ly above ground, lz) with vertical fov:
    // every renderer whose bounds the ray passes within 0.5 m of, nearest first (path + LODGroup root), plus the first collider hit
    public float u = 0.5f, v = 0.5f, fov = 55f, ly2 = 0f;
    public string Pick()
    {
        Physics.SyncTransforms(); System.Func<float, float, float> G = (x, z) => { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) if (h.collider.transform.root.name == "OPCD Meshes" || h.collider is TerrainCollider) y = Mathf.Max(y, h.point.y); return y; };
        var eye = new Vector3(px, G(px, pz) + py, pz); var tgt = new Vector3(cx, G(cx, cz) + ly2, cz); var f = (tgt - eye).normalized; var r = Vector3.Cross(Vector3.up, f).normalized; var up = Vector3.Cross(f, r);
        float th = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), asp = 4f / 3f; var d = (f + r * ((u * 2 - 1) * th * asp) + up * ((1 - v * 2) * th)).normalized; var ray = new Ray(eye, d);
        var o = new List<KeyValuePair<float, string>>();
        foreach (var rd in Object.FindObjectsOfType<Renderer>()) { float dist; var b = rd.bounds; b.Expand(1f); if (!b.IntersectRay(ray, out dist) || dist > 400) continue; if (rd.transform.root.name == "OPCD Meshes" || rd.transform.root.name.StartsWith("Terrain") || rd.transform.root.name == "Outer Plot") continue; var lg = rd.GetComponentInParent<LODGroup>(); var t = lg ? lg.transform : rd.transform; string p = t.name; for (var q = t.parent; q; q = q.parent) p = q.name + "/" + p; o.Add(new KeyValuePair<float, string>(dist, p + " @(" + t.position.x.ToString("F1") + "," + t.position.z.ToString("F1") + ")")); }
        RaycastHit hit; string first = Physics.Raycast(ray, out hit, 1000) ? hit.collider.transform.root.name + "/" + hit.collider.name + " at (" + hit.point.x.ToString("F1") + "," + hit.point.z.ToString("F1") + ")" : "none";
        return "first collider: " + first + "\n" + string.Join("\n", o.OrderBy(k => k.Key).Select(k => k.Key.ToString("F0") + " m " + k.Value).Distinct().Take(12).ToArray());
    }
    // switch off the LODGroup/plant named `fromName` under root `roots` whose pivot is nearest (cx, cz) within r (recorded in NIGHT1031_LOG)
    public string OffAt()
    {
        var root = GameObject.Find(roots); if (!root) return "no " + roots; Transform best = null; float bd = r;
        foreach (var t in root.GetComponentsInChildren<Transform>().Where(x => x.name == fromName)) { float d = new Vector2(t.position.x - cx, t.position.z - cz).magnitude; if (d < bd) { bd = d; best = t; } }
        if (!best) return "nothing named " + fromName + " within " + r + " m";
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("offat|" + roots + "/" + fromName + "|" + best.position.ToString("F2") + "|" + tag).transform.SetParent(log.transform, false);
        best.gameObject.SetActive(false); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return roots + "/" + fromName + " at " + best.position.ToString("F1") + " off";
    }
    // trees whose trunk stands within `nearGreen` m of a green / collar / green blend edge (BoJa 12h: willow at the green front). Dry run lists them;
    // apply moves each trunk straight away from the nearest green sample until it is `nearGreen` m clear, never onto mown turf, sand, tee or path
    // (stops if it can't), re-seated on the ground; recorded in NIGHT1031_LOG. `treeRoots` ("A,B") limits the roots; "*" = every root with LODGroups.
    public float nearGreen = 6f;
    public string GreenTrees()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        System.Func<float, float, string> Top = (x, z) => { string nm = "none"; float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.name.StartsWith("collider_")) continue; var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD")) return "path"; if (rn != "OPCD Meshes") continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name.ToLower(); } } return nm; };
        System.Func<string, bool> IsGreen = n => n.Contains("_green") || n.Contains("collar");
        System.Func<string, bool> Bad = n => n == "path" || n.Contains("fairway") || n.Contains("_green") || n.Contains("collar") || n.Contains("tee") || n.Contains("bunker") || n.Contains("water");
        System.Func<Vector2, Vector2?> NearG = q => { for (float rr = 0; rr <= nearGreen; rr += 0.5f) for (int k = 0; k < (rr == 0 ? 1 : 16); k++) { float a = k * Mathf.PI / 8; var p = q + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; if (IsGreen(Top(p.x, p.y))) return p; } return null; };
        var roots = treeRoots == "*" ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.activeInHierarchy && g.GetComponentInChildren<LODGroup>()).ToArray() : treeRoots.Split(',').Select(GameObject.Find).Where(g => g).ToArray();
        foreach (var root in roots) foreach (var lg in root.GetComponentsInChildren<LODGroup>())
        {
            var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); if (b.size.y < 3f) continue;
            var t = lg.transform; var c = new Vector2(t.position.x, t.position.z); if (c.x < x0 || c.x > x1 || c.y < z0 || c.y > z1) continue;
            var g = NearG(c); if (!g.HasValue) continue; string path = root.name + "/" + t.name + " (" + c.x.ToString("F1") + "," + c.y.ToString("F1") + ")"; float d0 = (g.Value - c).magnitude;
            if (!apply) { o.Add(path + " " + d0.ToString("F1") + " m from green/collar"); continue; }
            var away = (c - g.Value).sqrMagnitude > 1e-4f ? (c - g.Value).normalized : new Vector2(1, 0); Vector2 q = c; bool ok = false;
            for (float s = 0.5f; s <= 20; s += 0.5f) { var p = c + away * s; if (Bad(Top(p.x, p.y))) continue; if (NearG(p).HasValue) continue; q = p; ok = true; break; }
            if (!ok) { o.Add(path + " " + d0.ToString("F1") + " m: no clear spot within 20 m, left"); continue; }
            float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { if (h.collider.transform.IsChildOf(t)) continue; if (h.collider.transform.root.name == "OPCD Meshes" || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); }
            new GameObject("move|" + t.name + "|" + t.position.ToString("F3")).transform.SetParent(log.transform, false); t.position = new Vector3(q.x, gy > float.MinValue ? gy - 0.05f : t.position.y, q.y); Physics.SyncTransforms();
            o.Add(path + " " + d0.ToString("F1") + " m -> moved " + (q - c).magnitude.ToString("F1") + " m");
        }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return o.Count + " trees within " + nearGreen + " m of a green/collar\n" + string.Join("\n", o.ToArray());
    }
    // move-log entries ("<kind>|<name>|(x, y, z)" children of any root whose name ends in _LOG) whose OLD position lies in the box x0..x1, z0..z1
    public string LogsIn()
    {
        var o = new List<string>();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name.EndsWith("_LOG")))
            foreach (Transform c in root.transform)
            {
                var p = c.name.Split('|'); if (p.Length < 3) continue; var s = p[p.Length - 1].Trim('(', ')').Split(','); if (s.Length < 3) continue; float x, z;
                if (!float.TryParse(s[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) || !float.TryParse(s[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z)) continue;
                if (x < x0 || x > x1 || z < z0 || z > z1) continue; o.Add(root.name + ": " + c.name);
            }
        return o.Count + "\n" + string.Join("\n", o.Take(80).ToArray());
    }
    // water on turf (BoJa 15a): for every active water mesh (name or material contains "water", not the lake BASE meshes) the triangles whose
    // centre sits more than `above` m over the top non-water course surface under it (turf / terrain): count + area + worst spots per mesh.
    // With apply those triangles are dropped (A-owned copies in generated/wateron, originals in NIGHT1031_LOG). `words` limits mesh names.
    public float above = 0.03f;
    public string WaterOnTurf()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        if (apply && !UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/wateron")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "wateron");
        var all = Object.FindObjectsOfType<MeshFilter>().Where(f => f.sharedMesh && f.gameObject.activeInHierarchy && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled).ToList();
        System.Func<MeshFilter, bool> IsWater = f => { var n = f.name.ToLower(); var m = f.GetComponent<Renderer>().sharedMaterial; var mn = m ? m.name.ToLower() : ""; return (n.Contains("water") || mn.Contains("water")) && !n.Contains("base_lake") && !mn.Contains("lakebed"); };
        System.Func<float, float, float> Ground = (x, z) => { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { var c = h.collider; if (c.GetComponentInParent<LODGroup>() || c.name.StartsWith("collider_")) continue; var n = c.name.ToLower(); var rd = c.GetComponent<Renderer>(); var mn = rd && rd.sharedMaterial ? rd.sharedMaterial.name.ToLower() : ""; if (n.Contains("water") || mn.Contains("water") || mn.Contains("lakebed")) continue; if (c.transform.root.name == "OPCD Meshes" || c is TerrainCollider) y = Mathf.Max(y, h.point.y); } return y; };
        foreach (var mf in all.Where(IsWater))
        {
            { string fp = mf.name; for (var uu = mf.transform.parent; uu; uu = uu.parent) fp = uu.name + "/" + fp; if (words.Length > 0 && words != "*" && !words.Split(',').Any(wd => fp == wd || fp.EndsWith("/" + wd))) continue; }
            var src = mf.sharedMesh; var w = src.vertices.Select(q => mf.transform.TransformPoint(q)).ToArray(); int bad = 0; float area = 0, worst = 0; Vector3 at = Vector3.zero; var subs = new List<int[]>();
            for (int s = 0; s < src.subMeshCount; s++) { var t = src.GetTriangles(s); var keep = new List<int>(); for (int i = 0; i < t.Length; i += 3) { var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; float g = Ground(c.x, c.z); if (g > float.MinValue && c.y - g > above) { bad++; area += Vector3.Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]).magnitude / 2; if (c.y - g > worst) { worst = c.y - g; at = c; } continue; } keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); } subs.Add(keep.ToArray()); }
            if (bad == 0) continue; string path = mf.name; for (var u = mf.transform.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(path + ": " + bad + " tris, " + area.ToString("F1") + " m2 over turf, worst " + worst.ToString("F2") + " m at (" + at.x.ToString("F1") + "," + at.z.ToString("F1") + ")");
            if (!apply) continue;
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_wt"; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s); m.RecalculateBounds();
            var ap = "Assets/TierraRejada/generated/wateron/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            new GameObject("wateron|" + path + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return o.Count + " water meshes with water over turf\n" + string.Join("\n", o.ToArray());
    }
    // every active renderer (any root, colliders or not) whose MESH has a triangle over (cx, cz): name, material, the y there, colour channel there
    public string Over()
    {
        var o = new List<KeyValuePair<float, string>>(); var q = new Vector2(cx, cz);
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            var rd = mf.GetComponent<Renderer>(); if (!rd || !rd.enabled || !mf.sharedMesh) continue; var b = rd.bounds; if (cx < b.min.x || cx > b.max.x || cz < b.min.z || cz > b.max.z) continue; if (mf.GetComponentInParent<LODGroup>()) continue;
            var m = mf.sharedMesh; if (!m.isReadable) continue; var v = m.vertices; var t = m.triangles; var col = m.colors;
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = mf.transform.TransformPoint(v[t[i]]); var b2 = mf.transform.TransformPoint(v[t[i + 1]]); var c = mf.transform.TransformPoint(v[t[i + 2]]);
                float d = (b2.z - c.z) * (a.x - c.x) + (c.x - b2.x) * (a.z - c.z); if (Mathf.Abs(d) < 1e-8f) continue;
                float l1 = ((b2.z - c.z) * (cx - c.x) + (c.x - b2.x) * (cz - c.z)) / d, l2 = ((c.z - a.z) * (cx - c.x) + (a.x - c.x) * (cz - c.z)) / d, l3 = 1 - l1 - l2;
                if (l1 < 0 || l2 < 0 || l3 < 0) continue; float y = l1 * a.y + l2 * b2.y + l3 * c.y;
                string cc = col.Length == v.Length ? (col[t[i]] * l1 + col[t[i + 1]] * l2 + col[t[i + 2]] * l3).ToString("F2") : "-";
                string path = mf.name; for (var u = mf.transform.parent; u; u = u.parent) path = u.name + "/" + path;
                o.Add(new KeyValuePair<float, string>(y, "y " + y.ToString("F2") + " " + path + " mat " + (rd.sharedMaterial ? rd.sharedMaterial.name : "-") + " col " + cc + (mf.GetComponent<Collider>() ? "" : " (no collider)"))); break;
            }
        }
        return string.Join("\n", o.OrderByDescending(k => k.Key).Select(k => k.Value).ToArray());
    }
    // small plants (LODGroups under 3 m, and prefab instances under plant roots: shrubs, grasses, pampas, flowers) whose base stands on
    // mown turf (fairway / green / collar / tee / approach) or sand. Dry run lists by root; apply moves each to the nearest spot within 15 m that
    // is rough / terrain and >= `gapM` m from mown turf (recorded in NIGHT1031_LOG). Combined shrub tile meshes are not individual plants: skipped.
    public float gapM = 1.0f; public bool nearMode = false;   // nearMode: also plants within gapM of mown turf
    public string PlantsOnTurf()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); int n = 0, mv = 0, left = 0;
        System.Func<float, float, string> Top = (x, z) => { string nm = "none"; float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { var c = h.collider; if (c.GetComponentInParent<LODGroup>() || c.name.StartsWith("collider_")) continue; var rn = c.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD")) return "path"; if (rn != "OPCD Meshes") continue; if (h.point.y > y) { y = h.point.y; nm = c.name.ToLower(); } } return nm; };
        System.Func<string, bool> Mown = s => s.Contains("fairway") || s.Contains("_green") || s.Contains("collar") || s.Contains("tee") || s.Contains("approach") || s.Contains("bunker");
        System.Func<Vector2, bool> Clear = q => { if (Mown(Top(q.x, q.y)) || Top(q.x, q.y) == "path") return false; for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; if (Mown(Top(q.x + Mathf.Cos(a) * gapM, q.y + Mathf.Sin(a) * gapM))) return false; } return true; };
        var items = new List<Transform>();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.activeInHierarchy && g.name != "OPCD Meshes" && !g.name.StartsWith("Terrain") && g.name != "CLUBHOUSE" && !g.name.StartsWith("HOLE_SIGNS") && !g.name.Contains("_LOG")))
        {
            foreach (var lg in root.GetComponentsInChildren<LODGroup>()) { var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); if (b.size.y < 3f) items.Add(lg.transform); }
            foreach (var t in root.GetComponentsInChildren<Transform>()) { if (t.GetComponentInParent<LODGroup>()) continue; var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject); if (src == null || UnityEditor.PrefabUtility.FindPrefabRoot(t.gameObject) != t.gameObject) continue; var p = UnityEditor.AssetDatabase.GetAssetPath(src).ToLower(); if (p.Contains("/shrubs/") || p.Contains("grass") || p.Contains("pampas") || p.Contains("flower") || p.Contains("sage") || p.Contains("buckwheat")) items.Add(t); }
        }
        foreach (var t in items.Distinct())
        {
            var c = new Vector2(t.position.x, t.position.z); if (c.x < x0 || c.x > x1 || c.y < z0 || c.y > z1) continue; var s = Top(c.x, c.y); if (nearMode ? Clear(c) : !Mown(s)) continue; n++;
            string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path; string line = path + " (" + c.x.ToString("F1") + "," + c.y.ToString("F1") + ") on " + s;
            if (apply)
            {
                Vector2 q = c; bool ok = false; for (float rr = 0.5f; rr <= 15 && !ok; rr += 0.5f) for (int k = 0; k < 24 && !ok; k++) { float a = k * Mathf.PI / 12; var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; if (Clear(p)) { q = p; ok = true; } }
                if (!ok) { line += " -> no spot, left"; left++; }
                else { float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { if (h.collider.transform.IsChildOf(t)) continue; if (h.collider.transform.root.name == "OPCD Meshes" || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); } new GameObject("move|" + t.name + "|" + t.position.ToString("F3")).transform.SetParent(log.transform, false); t.position = new Vector3(q.x, gy > float.MinValue ? gy - 0.03f : t.position.y, q.y); Physics.SyncTransforms(); mv++; line += " -> moved " + (q - c).magnitude.ToString("F1") + " m"; }
            }
            o.Add(line);
        }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return n + " small plants on mown turf" + (apply ? ", " + mv + " moved, " + left + " left" : " (dry run)") + "\n" + string.Join("\n", o.Take(80).ToArray());
    }
    // cart path strips from a polyline csv (piece,i,x,y,z in travel order; BoJa 17 path): one draped strip per piece, `width` m wide, square
    // ends on a shared centreline (tangents taken over the whole line, so pieces meet seamlessly), seated `lift` m over the top turf/terrain
    // (paths ignored), every 0.5 m. Material per piece from `matMap` ("piece=Assets/..mat;piece2=..."), physics from `physMap`
    // ("piece=GSPconcrete;..."). Objects CART_PATHS/<tag>_<piece>, uv along the path / width for the path strip shader. Re-running replaces.
    public string csvPath = "", matMap = "", physMap = ""; public float width = 2.6f, lift = 0.03f;
    public string BuildPath()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var rows = System.IO.File.ReadAllLines(csvPath).Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',')).ToList();
        var pieces = new List<string>(); var pts = new List<Vector2>(); var pc = new List<string>();
        foreach (var r in rows) { var p = new Vector2(float.Parse(r[2], ci), float.Parse(r[4], ci)); if (pts.Count > 0 && (pts[pts.Count - 1] - p).magnitude < 0.05f) { pc[pc.Count - 1] = pc[pc.Count - 1]; if (!pieces.Contains(r[0])) pieces.Add(r[0]); continue; } pts.Add(p); pc.Add(r[0]); if (!pieces.Contains(r[0])) pieces.Add(r[0]); }
        // resample the whole line every 0.5 m, keeping piece labels and exact joint points
        var S = new List<Vector2>(); var SP = new List<string>(); S.Add(pts[0]); SP.Add(pc[0]);
        for (int i = 1; i < pts.Count; i++) { var a = pts[i - 1]; var b = pts[i]; int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.5f)); for (int k = 1; k <= n; k++) { S.Add(Vector2.Lerp(a, b, k / (float)n)); SP.Add(pc[i]); } }
        Physics.SyncTransforms(); System.Func<float, float, float> G = (x, z) => { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD") || h.collider.GetComponentInParent<LODGroup>() || h.collider.name.StartsWith("collider_")) continue; if (rn == "OPCD Meshes" || h.collider is TerrainCollider) y = Mathf.Max(y, h.point.y); } return y; };
        var T = new Vector2[S.Count]; for (int i = 0; i < S.Count; i++) { var a = S[Mathf.Max(0, i - 2)]; var b = S[Mathf.Min(S.Count - 1, i + 2)]; T[i] = (b - a).normalized; }
        var root = GameObject.Find("CART_PATHS").transform; var mm = matMap.Split(';').Where(q => q.Contains("=")).ToDictionary(q => q.Split('=')[0], q => q.Split('=')[1]); var pm = physMap.Split(';').Where(q => q.Contains("=")).ToDictionary(q => q.Split('=')[0], q => q.Split('=')[1]);
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/paths")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "paths");
        var o = new List<string>();
        foreach (var piece in pieces)
        {
            // indices of this piece plus the shared joint point before it
            var idx = Enumerable.Range(0, S.Count).Where(i => SP[i] == piece).ToList(); if (idx.Count == 0) continue; if (idx[0] > 0) idx.Insert(0, idx[0] - 1);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); float along = 0;
            for (int k = 0; k < idx.Count; k++)
            {
                int i = idx[k]; if (k > 0) along += (S[i] - S[idx[k - 1]]).magnitude; var nrm = new Vector2(-T[i].y, T[i].x);
                var l = S[i] + nrm * (width / 2); var r = S[i] - nrm * (width / 2); var c = S[i]; float yc = G(c.x, c.y), yl = G(l.x, l.y), yr = G(r.x, r.y); float y = Mathf.Max(yc, (yl + yr) / 2);
                v.Add(new Vector3(l.x, Mathf.Max(yl, y - 0.15f) + lift, l.y)); v.Add(new Vector3(r.x, Mathf.Max(yr, y - 0.15f) + lift, r.y)); uv.Add(new Vector2(0, along / width)); uv.Add(new Vector2(1, along / width));
                if (k > 0) { int b0 = v.Count - 4; t.AddRange(new[] { b0, b0 + 2, b0 + 1, b0 + 1, b0 + 2, b0 + 3 }); }
            }
            var m = new Mesh { name = tag + "_" + piece }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            for (int k = 0; k < t.Count; k += 3) { var n2 = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]); if (n2.y < 0) { int q = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = q; } } m.SetTriangles(t, 0); m.RecalculateNormals();
            var ap = "Assets/TierraRejada/generated/paths/" + tag + "_" + piece + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            var old = root.Find(tag + "_" + piece); if (old) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(tag + "_" + piece); go.transform.SetParent(root, false); go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>();
            string matp; if (mm.TryGetValue(piece, out matp)) mr.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matp);
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; string ph; if (pm.TryGetValue(piece, out ph)) mc.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + ph);
            o.Add(go.name + ": " + along.ToString("F1") + " m, mat " + (mr.sharedMaterial ? mr.sharedMaterial.name : "NONE") + ", phys " + (mc.sharedMaterial ? mc.sharedMaterial.name : "NONE"));
        }
        UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene); return string.Join("\n", o.ToArray());
    }
    // flat pad (5 restroom): inside the rotated rectangle centred (cx, cz), half sizes hx, hz (m), yaw2 (deg, Unity Y rotation) every OPCD turf mesh
    // vertex and terrain cell goes to padY; out to `ease` m beyond it, y = lerp(padY, own y, smoothstep(d / ease)). Paths, water, bunkers, greens
    // and tees are not touched. A-owned mesh copies in generated/gradepad (NIGHT1031_LOG). Dry run unless apply.
    public float padY = 0f, ease = 4f;
    public string GradePad()
    {
        var rot = Quaternion.Euler(0, yaw2, 0); var inv = Quaternion.Inverse(rot); var c0 = new Vector3(cx, 0, cz);
        System.Func<float, float, float> Dist = (x, z) => { var l = inv * new Vector3(x - cx, 0, z - cz); float dx = Mathf.Max(0, Mathf.Abs(l.x) - hx), dz = Mathf.Max(0, Mathf.Abs(l.z) - hz); return Mathf.Sqrt(dx * dx + dz * dz); };
        System.Func<float, float, float, float> NewY = (x, z, y) => { float d = Dist(x, z); if (d >= ease) return y; return Mathf.Lerp(padY, y, Mathf.SmoothStep(0, 1, d / ease)); };
        float R = Mathf.Sqrt(hx * hx + hz * hz) + ease + 1; var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        if (apply && !UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/gradepad")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "gradepad");
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>())
        {
            var n = mf.name.ToLower(); if (!mf.sharedMesh || n.Contains("water") || n.Contains("bunker") || n.Contains("green") || n.Contains("_tee_")) continue; var b = mf.GetComponent<Renderer>().bounds; if (b.max.x < cx - R || b.min.x > cx + R || b.max.z < cz - R || b.min.z > cz + R) continue;
            var src = mf.sharedMesh; var v = src.vertices; int k = 0; float mx = 0;
            for (int i = 0; i < v.Length; i++) { var w = mf.transform.TransformPoint(v[i]); float ny = NewY(w.x, w.z, w.y); if (Mathf.Abs(ny - w.y) < 1e-4f) continue; mx = Mathf.Max(mx, Mathf.Abs(ny - w.y)); w.y = ny; v[i] = mf.transform.InverseTransformPoint(w); k++; }
            if (k == 0) continue; o.Add(mf.name + ": " + k + " vertices, up to " + mx.ToString("F2") + " m"); if (!apply) continue;
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_gp"; m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds();
            var ap = "Assets/TierraRejada/generated/gradepad/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            new GameObject("gradepad|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
        }
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var tp = t.transform.position; int Rh = td.heightmapResolution; float sx = td.size.x / (Rh - 1), sz = td.size.z / (Rh - 1);
            int i0 = Mathf.Max(0, Mathf.FloorToInt((cx - R - tp.x) / sx)), i1 = Mathf.Min(Rh - 1, Mathf.CeilToInt((cx + R - tp.x) / sx)), j0 = Mathf.Max(0, Mathf.FloorToInt((cz - R - tp.z) / sz)), j1 = Mathf.Min(Rh - 1, Mathf.CeilToInt((cz + R - tp.z) / sz)); if (i1 < i0 || j1 < j0) continue;
            var H = td.GetHeights(i0, j0, i1 - i0 + 1, j1 - j0 + 1); int k = 0;
            for (int j = 0; j < H.GetLength(0); j++) for (int i = 0; i < H.GetLength(1); i++) { float x = tp.x + (i0 + i) * sx, z = tp.z + (j0 + j) * sz, y = tp.y + H[j, i] * td.size.y; float ny = NewY(x, z, y) - 0.25f * (Dist(x, z) < ease ? 1 - Mathf.SmoothStep(0, 1, Dist(x, z) / ease) : 0); if (Mathf.Abs(ny - y) < 1e-4f) continue; H[j, i] = Mathf.Max(0, (ny - tp.y) / td.size.y); k++; }
            o.Add(t.name + ": " + k + " cells"); if (apply && k > 0) { td.SetHeights(i0, j0, H); UnityEditor.EditorUtility.SetDirty(td); }
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return (apply ? "" : "(dry run) ") + string.Join("\n", o.ToArray());
    }
    // transform + local mesh bounds of the objects in `ids` ("root/path|...")
    public string Xform()
    {
        var o = new List<string>();
        foreach (var p in ids.Split('|').Where(s => s.Length > 0))
        {
            var parts = p.Split('/'); var r0 = GameObject.Find(parts[0]); var t = r0 ? (parts.Length > 1 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r0.transform) : null; if (!t) { o.Add(p + " not found"); continue; }
            var mf = t.GetComponent<MeshFilter>(); o.Add(p + " pos " + t.position.ToString("F3") + " rotY " + t.eulerAngles.y.ToString("F1") + " scale " + t.lossyScale.ToString("F3") + (mf && mf.sharedMesh ? " localBounds " + mf.sharedMesh.bounds.min.ToString("F2") + ".." + mf.sharedMesh.bounds.max.ToString("F2") : ""));
        }
        return string.Join("\n", o.ToArray());
    }
    // cap a removed bunker (BoJa: no bunker on the 5 fairway): a copy of the (switched-off) bunker mesh `meshName` with every vertex lifted to a
    // least-squares plane through its boundary (the lip), 2 cm under the surrounding turf there; material `matPath`, vertex colour `rgb` (rough
    // layer), collider with physic material `fromName` (e.g. GSPrough). Object "Cap_<tag>" under OPCD Meshes (GSPro reads the material/physics).
    public string CapBunker()
    {
        var opcd = GameObject.Find("OPCD Meshes"); var mf = opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var src = mf.sharedMesh; var w = src.vertices.Select(q => mf.transform.TransformPoint(q)).ToArray(); var tri = src.triangles;
        var weld = new int[w.Length]; var map = new Dictionary<Vector2Int, int>(); for (int i = 0; i < w.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!map.TryGetValue(k, out j)) { j = i; map[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < tri.Length; i += 3) for (int e = 0; e < 3; e++) { var k = K(weld[tri[i + e]], weld[tri[i + (e + 1) % 3]]); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; }
        var bnd = new HashSet<int>(); foreach (var kv in cnt) if (kv.Value == 1) { bnd.Add((int)(kv.Key >> 32)); bnd.Add((int)(kv.Key & 0xffffffff)); }
        // turf height at each boundary vertex (top OPCD turf collider, bunker off)
        Physics.SyncTransforms(); var bp = new List<Vector3>();
        foreach (var i in bnd) { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(w[i].x, 600, w[i].z), Vector3.down, 1200)) { if (h.collider.transform.root != opcd.transform) continue; var n = h.collider.name.ToLower(); if (n.Contains("bunker") || n.StartsWith("cap_")) continue; y = Mathf.Max(y, h.point.y); } bp.Add(new Vector3(w[i].x, y > float.MinValue ? y : w[i].y, w[i].z)); }
        double sxx = 0, sxz = 0, szz = 0, sx = 0, sz = 0, sy = 0, sxy = 0, szy = 0; int N = bp.Count; foreach (var p in bp) { sxx += p.x * p.x; sxz += p.x * p.z; szz += p.z * p.z; sx += p.x; sz += p.z; sy += p.y; sxy += p.x * p.y; szy += p.z * p.y; }
        var M = new double[,] { { sxx, sxz, sx }, { sxz, szz, sz }, { sx, sz, N } }; var V = new[] { sxy, szy, sy }; System.Func<double[,], double> Det = mm => mm[0, 0] * (mm[1, 1] * mm[2, 2] - mm[1, 2] * mm[2, 1]) - mm[0, 1] * (mm[1, 0] * mm[2, 2] - mm[1, 2] * mm[2, 0]) + mm[0, 2] * (mm[1, 0] * mm[2, 1] - mm[1, 1] * mm[2, 0]);
        double D = Det(M); var co = new double[3]; for (int k = 0; k < 3; k++) { var Mk = (double[,])M.Clone(); for (int rr = 0; rr < 3; rr++) Mk[rr, k] = V[rr]; co[k] = Det(Mk) / D; }
        var nv = w.Select(p => new Vector3(p.x, (float)(co[0] * p.x + co[1] * p.z + co[2]) - 0.01f, p.z)).ToArray();
        { var bl = bnd.ToList(); var cen = new Vector3(bp.Average(q => q.x), 0, bp.Average(q => q.z)); for (int q = 0; q < bl.Count; q++) for (int i = 0; i < w.Length; i++) if (weld[i] == bl[q]) { var dir = new Vector3(nv[i].x - cen.x, 0, nv[i].z - cen.z).normalized; nv[i] += dir * 0.3f; nv[i].y = bp[q].y - 0.01f; } }   // lip vertices exactly on the turf: no seam
        var ci = System.Globalization.CultureInfo.InvariantCulture; var cc = rgb.Split(',').Select(q => float.Parse(q, ci)).ToArray();
        var m = new Mesh { name = "Cap_" + tag }; m.vertices = nv; m.uv = src.uv; if (src.uv2.Length == nv.Length) m.uv2 = src.uv2; m.colors = Enumerable.Repeat(new Color(cc[0], cc[1], cc[2], 1), nv.Length).ToArray(); m.triangles = tri;
        var t2 = m.triangles; for (int k = 0; k < t2.Length; k += 3) { var n2 = Vector3.Cross(nv[t2[k + 1]] - nv[t2[k]], nv[t2[k + 2]] - nv[t2[k]]); if (n2.y < 0) { int q = t2[k + 1]; t2[k + 1] = t2[k + 2]; t2[k + 2] = q; } } m.triangles = t2; m.RecalculateNormals(); m.RecalculateBounds();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/caps")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "caps");
        var ap = "Assets/TierraRejada/generated/caps/Cap_" + tag + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var old = opcd.transform.Find("Cap_" + tag); if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Cap_" + tag); go.transform.SetParent(opcd.transform, false); go.layer = mf.gameObject.layer; go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + fromName);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return go.name + ": " + nv.Length + " verts, plane through " + N + " lip points, mat " + (go.GetComponent<MeshRenderer>().sharedMaterial ? go.GetComponent<MeshRenderer>().sharedMaterial.name : "NONE") + ", phys " + (mc.sharedMaterial ? mc.sharedMaterial.name : "NONE");
    }
    // place prop models from a csv (name,..,x,y,z,yaw columns by header; range signs): <dir>/<name>.fbx at (x, ground, z) with Y rotation yaw,
    // plus <dir>/<name>_COL.fbx as invisible MeshColliders (physic material `fromName`, e.g. GSPwood). All under root object `propName`
    // (replaced on re-run). Ground = top OPCD turf / terrain at (x, z) (paths too).
    public string dir = "";
    public string PlaceCsv()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var lines = System.IO.File.ReadAllLines(csvPath).Where(l => l.Trim().Length > 0).ToList(); var hd = lines[0].Split(',').ToList();
        int iN = hd.IndexOf("name"), iX = hd.IndexOf("x"), iZ = hd.IndexOf("z"), iY = hd.IndexOf("yaw"); if (iN < 0 || iX < 0 || iZ < 0 || iY < 0) return "csv needs name,x,z,yaw";
        var old = GameObject.Find(propName); if (old) Object.DestroyImmediate(old); var root = new GameObject(propName); var phys = Resources.Load<PhysicMaterial>("Physics/" + fromName) ?? Resources.Load<PhysicMaterial>("Physics/GSPstone"); var o = new List<string>();
        Physics.SyncTransforms();
        foreach (var l in lines.Skip(1))
        {
            var c = l.Split(','); string nm = c[iN]; float x = float.Parse(c[iX], ci), z = float.Parse(c[iZ], ci), yaw = float.Parse(c[iY], ci);
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + nm + ".fbx"); if (!a) { o.Add(nm + ": no model"); continue; }
            float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.name.StartsWith("collider_") || h.collider.transform.IsChildOf(root.transform)) continue; var rn = h.collider.transform.root.name; if (rn == "OPCD Meshes" || rn == "CART_PATHS" || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); }
            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(a); go.transform.SetParent(root.transform, true); go.transform.position = new Vector3(x, gy > float.MinValue ? gy : 0, z); go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var ca = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + nm + "_COL.fbx"); if (ca) { var cg = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(ca); cg.name = nm + "_COL"; cg.transform.SetParent(go.transform, false); foreach (var mf in cg.GetComponentsInChildren<MeshFilter>()) { var mc = mf.GetComponent<MeshCollider>(); if (!mc) mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.sharedMaterial = phys; var rr = mf.GetComponent<MeshRenderer>(); if (rr) Object.DestroyImmediate(rr); } }
            o.Add(nm + " at (" + x.ToString("F1") + ", " + gy.ToString("F2") + ", " + z.ToString("F1") + ") yaw " + yaw.ToString("F0"));
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene); return o.Count + " placed under " + propName + "\n" + string.Join("\n", o.ToArray());
    }
    // a net run from a csv (segment,x,z,y; posts in order) built from the first Pole and NetPanel of NETS/<fromName> (same materials, physics,
    // pole size); posts seated on the ground, net height `ease` m (top above the higher post base). Under NETS/<propName> (replaced on re-run).
    public string BuildNet()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var nets = GameObject.Find("NETS"); var src = nets.transform.Find(fromName); if (!src) return "no NETS/" + fromName;
        var pole = src.Cast<Transform>().First(t => t.name == "Pole"); var panel = src.Cast<Transform>().First(t => t.name == "NetPanel");
        var old = nets.transform.Find(propName); if (old) Object.DestroyImmediate(old.gameObject); var run = new GameObject(propName); run.transform.SetParent(nets.transform, false);
        var pts = System.IO.File.ReadAllLines(csvPath).Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',')).Select(c => new Vector2(float.Parse(c[1], ci), float.Parse(c[2], ci))).ToList();
        Physics.SyncTransforms(); System.Func<Vector2, Vector3> G = q => { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.transform.IsChildOf(nets.transform)) continue; var rn = h.collider.transform.root.name; if (rn == "OPCD Meshes" || h.collider is TerrainCollider) y = Mathf.Max(y, h.point.y); } return new Vector3(q.x, y, q.y); };
        var g = pts.Select(G).ToList(); float H = ease; var ps = pole.localScale;
        foreach (var p in g) { var pl = Object.Instantiate(pole.gameObject, run.transform); pl.name = "Pole"; pl.transform.position = p + Vector3.up * (H / 2f); pl.transform.rotation = Quaternion.identity; pl.transform.localScale = new Vector3(ps.x, H / 2f, ps.z); }
        for (int i = 0; i + 1 < g.Count; i++) { var a = g[i]; var b = g[i + 1]; var flat = new Vector3(b.x - a.x, 0, b.z - a.z); float bottom = Mathf.Min(a.y, b.y) - 0.3f, top = Mathf.Max(a.y, b.y) + H; var np = Object.Instantiate(panel.gameObject, run.transform); np.name = "NetPanel"; np.transform.position = new Vector3((a.x + b.x) / 2f, (bottom + top) / 2f, (a.z + b.z) / 2f); np.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up); np.transform.localScale = new Vector3(panel.localScale.x, top - bottom, flat.magnitude); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(run.scene); return "NETS/" + propName + ": " + g.Count + " poles, " + (g.Count - 1) + " panels, " + H + " m";
    }
    // split-rail fence run along a csv polyline (segment,x,z,y) in the style of FENCES/<fromName>: its first Post and its first two Rails are
    // cloned (same mesh / material / collider), posts every `gap` m seated on the ground at the template's height offset, two rails per bay at
    // the template rails' heights above ground. Under FENCES/<propName> (replaced on re-run).
    public string BuildFence()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var fences = GameObject.Find("FENCES"); var src = fences.transform.Find(fromName); if (!src) return "no FENCES/" + fromName;
        Physics.SyncTransforms(); System.Func<float, float, float> G = (x, z) => { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.transform.IsChildOf(fences.transform) || h.collider.name.StartsWith("collider_")) continue; var rn = h.collider.transform.root.name; if (rn == "OPCD Meshes" || h.collider is TerrainCollider) y = Mathf.Max(y, h.point.y); } return y; };
        var post = src.Cast<Transform>().First(t => t.name == "Post"); var rails = src.Cast<Transform>().Where(t => t.name == "Rail").Take(2).ToList();
        float postOff = post.position.y - G(post.position.x, post.position.z); var railOff = rails.Select(r => r.position.y - G(r.position.x, r.position.z)).ToList();
        var old = fences.transform.Find(propName); if (old) Object.DestroyImmediate(old.gameObject); var run = new GameObject(propName); run.transform.SetParent(fences.transform, false);
        var pts = System.IO.File.ReadAllLines(csvPath).Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',')).Select(c => new Vector2(float.Parse(c[1], ci), float.Parse(c[2], ci))).ToList();
        var S = new List<Vector2> { pts[0] }; float acc = 0; for (int i = 1; i < pts.Count; i++) { var a = pts[i - 1]; var b = pts[i]; float L = (b - a).magnitude; float t = 0; while (acc + (L - t) >= gap) { t += gap - acc; acc = 0; S.Add(Vector2.Lerp(a, b, t / L)); } acc += L - t; }
        var P = S.Select(q => new Vector3(q.x, G(q.x, q.y), q.y)).ToList(); var rl = rails[0].localScale;
        foreach (var p in P) { var g = Object.Instantiate(post.gameObject, run.transform); g.name = "Post"; g.transform.position = new Vector3(p.x, p.y + postOff, p.z); }
        for (int i = 0; i + 1 < P.Count; i++) for (int k = 0; k < rails.Count; k++)
        {
            var a = P[i] + Vector3.up * railOff[k]; var b = P[i + 1] + Vector3.up * railOff[k]; var g = Object.Instantiate(rails[k].gameObject, run.transform); g.name = "Rail";
            g.transform.position = (a + b) / 2; g.transform.rotation = Quaternion.LookRotation(b - a) * Quaternion.Inverse(Quaternion.LookRotation(rails[k].InverseTransformDirection(rails[k].forward)));
            // stretch along the rail's long axis to the bay length
            var ms = rails[k].GetComponent<MeshFilter>().sharedMesh.bounds.size; var es = Vector3.Scale(ms, rails[k].localScale); int ax = es.x >= es.y && es.x >= es.z ? 0 : es.y >= es.z ? 1 : 2; var sc = rails[k].localScale; float len = (b - a).magnitude / Mathf.Max(1e-4f, ms[ax]); sc[ax] = len; g.transform.localScale = sc;
            var dirAx = ax == 0 ? Vector3.right : ax == 1 ? Vector3.up : Vector3.forward; var tdir = rails[k].rotation * dirAx; g.transform.rotation = Quaternion.FromToRotation(tdir, (b - a).normalized) * rails[k].rotation;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(run.scene); return "FENCES/" + propName + ": " + P.Count + " posts, " + (P.Count - 1) * rails.Count + " rails, post offset " + postOff.ToString("F2") + ", rails " + string.Join("/", railOff.Select(v => v.ToString("F2")).ToArray());
    }
    // clip a water mesh to a channel: triangles of `meshName` (scene path) whose centre has no `fromName` (channel bed mesh, scene path) under
    // it within `ease` m below, or sits more than `above` m over the course turf/terrain there, are dropped. A-owned copy (generated/wateron,
    // NIGHT1031_LOG). Dry run unless apply.
    public bool needBed = true, contain = false;   // contain: keep only water held between higher ground on two opposite sides (ease m away)   // false: drop only triangles over turf
    public string ClipWater()
    {
        Physics.SyncTransforms(); System.Func<string, Transform> F = p => { var parts = p.Split('/'); var r0 = GameObject.Find(parts[0]); return r0 ? (parts.Length > 1 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r0.transform) : null; };
        var wt = F(meshName); var bed = F(fromName); if (!wt || !bed) return "missing"; var mf = wt.GetComponent<MeshFilter>(); var bm = bed.GetComponent<MeshFilter>(); if (!mf || !bm) return "no mesh";
        var bedc = bed.GetComponent<MeshCollider>(); bool tmp = false; if (!bedc) { bedc = bed.gameObject.AddComponent<MeshCollider>(); bedc.sharedMesh = bm.sharedMesh; tmp = true; } Physics.SyncTransforms();
        System.Func<float, float, float> Turf = (x, z) => { float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { var c = h.collider; if (c.GetComponentInParent<LODGroup>() || c.name.StartsWith("collider_") || c == bedc) continue; if (c.transform.root.name == "OPCD Meshes" || c is TerrainCollider) y = Mathf.Max(y, h.point.y); } return y; };
        var src = mf.sharedMesh; var w = src.vertices.Select(q => wt.TransformPoint(q)).ToArray(); var subs = new List<int[]>(); int drop = 0, keepN = 0;
        for (int s = 0; s < src.subMeshCount; s++) { var t = src.GetTriangles(s); var keep = new List<int>(); for (int i = 0; i < t.Length; i += 3) { var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; RaycastHit h; bool onBed = bedc.Raycast(new Ray(new Vector3(c.x, c.y + 3, c.z), Vector3.down), out h, 3 + ease); float g = Turf(c.x, c.z); bool overTurf = g > float.MinValue && c.y - g > above; if (contain) { System.Func<float, float, float> Top2 = (x2, z2) => { float y2 = float.MinValue; foreach (var h2 in Physics.RaycastAll(new Vector3(x2, 600, z2), Vector3.down, 1200)) { var c2 = h2.collider; if (c2.GetComponentInParent<LODGroup>() || c2.name.StartsWith("collider_") || c2.transform.IsChildOf(wt)) continue; var nn = c2.name.ToLower(); if (nn.Contains("water")) continue; if (c2.transform.root.name == "OPCD Meshes" || c2 is TerrainCollider || c2 == bedc) y2 = Mathf.Max(y2, h2.point.y); } return y2; }; bool held = false; for (int k = 0; k < 4 && !held; k++) { float an = k * Mathf.PI / 4; var dv = new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * ease; if (Top2(c.x + dv.x, c.z + dv.y) >= c.y - 0.02f && Top2(c.x - dv.x, c.z - dv.y) >= c.y - 0.02f) held = true; } overTurf = !held; } if ((needBed && !onBed) || overTurf) { drop++; continue; } keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); keepN++; } subs.Add(keep.ToArray()); }
        if (tmp) Object.DestroyImmediate(bedc);
        string r = meshName + ": keep " + keepN + ", drop " + drop + " triangles"; if (!apply || drop == 0) return r;
        var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_cw"; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s); m.RecalculateBounds();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/wateron")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "wateron");
        var ap = "Assets/TierraRejada/generated/wateron/" + wt.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("clipwater|" + meshName + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); mf.sharedMesh = m; var mc = wt.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; }
        UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return r + " -> applied";
    }
    // smooth a bunker floor (BoJa 12: hump in the widened left bunker): vertices of `meshName` more than `ease` m inside its boundary get
    // `gap` (rounded) passes of height-only Laplacian smoothing over the welded mesh, never rising above where they were (no new humps, the
    // hump comes down); the lip band stays exact. A-owned copy (generated/floor), collider follows.
    public string SmoothFloor()
    {
        var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var src = mf.sharedMesh; var w = src.vertices.Select(q => mf.transform.TransformPoint(q)).ToArray(); var tri = src.triangles; int N = w.Length;
        var weld = new int[N]; var map = new Dictionary<Vector2Int, int>(); for (int i = 0; i < N; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x * 200), Mathf.RoundToInt(w[i].z * 200)); int j; if (!map.TryGetValue(k, out j)) { j = i; map[k] = i; } weld[i] = j; }
        var cnt = new Dictionary<long, int>(); var nb = new Dictionary<int, HashSet<int>>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < tri.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[tri[i + e]], b = weld[tri[i + (e + 1) % 3]]; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; HashSet<int> s; if (!nb.TryGetValue(a, out s)) nb[a] = s = new HashSet<int>(); s.Add(b); if (!nb.TryGetValue(b, out s)) nb[b] = s = new HashSet<int>(); s.Add(a); }
        var bnd = new List<Vector2>(); foreach (var kv in cnt) if (kv.Value == 1) { bnd.Add(new Vector2(w[(int)(kv.Key >> 32)].x, w[(int)(kv.Key >> 32)].z)); }
        var reps = map.Values.ToList(); var y = new Dictionary<int, float>(); foreach (var r in reps) y[r] = w[r].y;
        var free = reps.Where(r => bnd.Min(b => (b - new Vector2(w[r].x, w[r].z)).magnitude) > ease).ToList(); int passes = Mathf.Max(1, Mathf.RoundToInt(gap));
        for (int p = 0; p < passes; p++) { var ny = new Dictionary<int, float>(); foreach (var r in free) { HashSet<int> s; if (!nb.TryGetValue(r, out s) || s.Count == 0) continue; float avg = s.Average(q => y[weld[q]]); ny[r] = Mathf.Min(w[r].y, 0.5f * y[r] + 0.5f * avg); } foreach (var kv in ny) y[kv.Key] = kv.Value; }
        float mx = 0; var v = src.vertices; for (int i = 0; i < N; i++) { float d = w[i].y - y[weld[i]]; if (d > mx) mx = d; var q = w[i]; q.y = y[weld[i]]; v[i] = mf.transform.InverseTransformPoint(q); }
        var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_sf"; m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/floor")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "floor");
        var ap = "Assets/TierraRejada/generated/floor/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap + ": use another tag"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("floor|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
        var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m; UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return mf.name + ": " + free.Count + " floor vertices smoothed (" + passes + " passes), largest drop " + mx.ToString("F2") + " m";
    }
    // soft cover edges in place (BoJa "square stair steps"; cloud SoftMask maths without re-running the areas): for every mesh named *_cover
    // under HILL_BRUSH (or `words` list), the 1 m grid vertex alpha is box-blurred over `gap` cells (2 passes, missing cells = 0), wobbled with
    // Perlin noise and smoothstepped, times the mesh's own max alpha. Plants, cuts and materials untouched. A-owned copies (generated/softcover).
    public string SoftCover()
    {
        var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); int r = Mathf.Max(1, Mathf.RoundToInt(gap));
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/softcover")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "softcover");
        foreach (var mf in roots.Split(',').Select(GameObject.Find).Where(g => g).SelectMany(g => g.GetComponentsInChildren<MeshFilter>()).Where(f => f.sharedMesh && (f.name.EndsWith("_cover") || words.Split(',').Contains(f.name)) && (words == "*" || words.Split(',').Contains(f.name))))
        {
            var src = mf.sharedMesh; var v = src.vertices; var col = src.colors; if (col.Length != v.Length) { o.Add(mf.name + ": no colours"); continue; }
            var w = v.Select(q => mf.transform.TransformPoint(q)).ToArray(); var key = new Dictionary<Vector2Int, float>(); float amax = 0;
            for (int i = 0; i < v.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x), Mathf.RoundToInt(w[i].z)); key[k] = Mathf.Max(key.ContainsKey(k) ? key[k] : 0, col[i].a); amax = Mathf.Max(amax, col[i].a); }
            if (amax <= 0) continue; var cur = key.ToDictionary(kv => kv.Key, kv => kv.Value / amax); var cells = cur.Keys.ToList();
            for (int pass = 0; pass < 2; pass++) { var bx = new Dictionary<Vector2Int, float>(); foreach (var c in cells) { float s = 0; for (int d = -r; d <= r; d++) { float a; s += cur.TryGetValue(new Vector2Int(c.x + d, c.y), out a) ? a : 0; } bx[c] = s / (2 * r + 1); } var by = new Dictionary<Vector2Int, float>(); foreach (var c in cells) { float s = 0; for (int d = -r; d <= r; d++) { float a; s += bx.TryGetValue(new Vector2Int(c.x, c.y + d), out a) ? a : 0; } by[c] = s / (2 * r + 1); } cur = by; }
            for (int i = 0; i < v.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(w[i].x), Mathf.RoundToInt(w[i].z)); float sm = cur[k]; float wob = (Mathf.PerlinNoise(k.x * 0.21f, k.y * 0.21f) - 0.5f) * 0.3f; float al = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sm + wob - 0.3f) / 0.4f)) * amax; col[i].a = Mathf.Min(col[i].a, al); }
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_sc"; m.colors = col; var ap = "Assets/TierraRejada/generated/softcover/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) { o.Add(mf.name + ": already " + tag); continue; } UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
            new GameObject("softcover|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); mf.sharedMesh = m; o.Add(mf.name + ": " + v.Length + " vertices softened");
        }
        UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return string.Join("\n", o.ToArray());
    }
    // read-only plant census: every active LODGroup in the box x0..x1, z0..z1 as "root/name x z height" (csv to `csvPath`), plus counts per name
    public string TreeCensus()
    {
        var rows = new List<string> { "root,name,x,z,y,height" }; var cnt = new Dictionary<string, int>();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.activeInHierarchy))
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var p = lg.transform.position; if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue; var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                rows.Add(root.name + "," + lg.name + "," + p.x.ToString("F1") + "," + p.z.ToString("F1") + "," + p.y.ToString("F2") + "," + b.size.y.ToString("F1")); var k = root.name + "/" + lg.name; int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1;
            }
        System.IO.File.WriteAllLines(csvPath, rows.ToArray());
        return (rows.Count - 1) + " plants -> " + csvPath + "\n" + string.Join("\n", cnt.OrderByDescending(k => k.Value).Take(40).Select(k => k.Value + " " + k.Key).ToArray());
    }
    // swap a fraction of existing plants for new prefabs at the same spot (install 5 trees): LODGroups under `roots` ("A,B") named in `words`
    // ("TR_Oak_A,TR_Pepper_B"), inside the box x0..x1, z0..z1, optionally within `reach` m of the polyline `poly` ("x,z;x,z", empty = no limit)
    // and never within `ease` m of the segment (px,pz)-(cx,cz) (keep-out, e.g. the 14 tee shot). Every `gap`-th match (deterministic, 1 = all)
    // becomes one of `prefabList` (";" paths, round-robin), scaled so its height matches the old one (0.7-1.4 clamp), random yaw, seated on the
    // ground; the old one is switched off (SWAP_LOG). New plants go under root `propName`. Dry run unless apply.
    public string prefabList = "";
    public string SwapMix()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var pre = prefabList.Split(';').Where(q => q.Length > 0).Select(q => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(q)).ToList(); if (pre.Any(p => !p)) return "missing prefab";
        var P = poly.Split(';').Where(q => q.Length > 0).Select(q => { var a = q.Split(','); return new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci)); }).ToList();
        System.Func<Vector2, Vector2, Vector2, float> SD = (q, a, b) => { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return (a + ab * t - q).magnitude; };
        var pH = pre.Select(p => { var rs = p.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b.size.y; }).ToList();
        var names = words.Split(','); var root = GameObject.Find(propName) ?? new GameObject(propName); var log = GameObject.Find("SWAP_LOG") ?? new GameObject("SWAP_LOG"); var rnd = new System.Random(1101); int k = 0, made = 0; var o = new List<string>();
        Physics.SyncTransforms();
        foreach (var rn in roots.Split(',')) { var r0 = GameObject.Find(rn); if (!r0) continue;
            foreach (var lg in r0.GetComponentsInChildren<LODGroup>().Where(l => names.Contains(l.name)).ToList())
            {
                var t = lg.transform; var c = new Vector2(t.position.x, t.position.z); if (c.x < x0 || c.x > x1 || c.y < z0 || c.y > z1) continue;
                if (P.Count > 1 && reach > 0) { float d = 1e9f; for (int i = 0; i + 1 < P.Count; i++) d = Mathf.Min(d, SD(c, P[i], P[i + 1])); if (d > reach) continue; }
                if (ease > 0 && SD(c, new Vector2(px, pz), new Vector2(cx, cz)) < ease) continue;
                k++; if (gap > 1 && (k % Mathf.RoundToInt(gap)) != 0) continue;
                var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                int pi = made % pre.Count; float s = Mathf.Clamp(b.size.y / Mathf.Max(0.1f, pH[pi]), 0.7f, 1.4f);
                if (apply)
                {
                    var g = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pre[pi]); g.transform.SetParent(root.transform, true); g.transform.position = t.position; g.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0); g.transform.localScale = Vector3.one * s;
                    new GameObject("swap|" + rn + "/" + t.name + "|" + t.position.ToString("F2") + "|" + pre[pi].name).transform.SetParent(log.transform, false); t.gameObject.SetActive(false);
                }
                made++; if (o.Count < 12) o.Add(rn + "/" + t.name + " (" + c.x.ToString("F0") + "," + c.y.ToString("F0") + ") h " + b.size.y.ToString("F1") + " -> " + pre[pi].name + " x" + s.ToString("F2"));
            } }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        return (apply ? "" : "(dry run) ") + made + " swapped of " + k + " candidates\n" + string.Join("\n", o.ToArray());
    }
    // palm in the lake: move the listed objects (`ids`) together by (dx, dz) and re-seat on the ground at the new trunk spot (first id = trunk)
    public bool noLift = false; // props on a slab: shift only, no ground re-seat
    public string MoveGroup()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var ts = new List<Transform>();
        foreach (var p in ids.Split('|').Where(s => s.Length > 0)) { var parts = p.Split('/'); var r0 = GameObject.Find(parts[0]); var t = r0 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : null; if (t) ts.Add(t); }
        if (ts.Count == 0) return "nothing found"; var trunk = ts[0].GetComponent<Renderer>().bounds; var np = new Vector2(trunk.center.x + dx, trunk.center.z + dz);
        float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(np.x, 600, np.y), Vector3.down, 1200)) { if (ts.Any(t => h.collider.transform.IsChildOf(t))) continue; if (h.collider.transform.root == opcd && !h.collider.name.Contains("Water") || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); }
        float lift = noLift ? 0 : gy > float.MinValue ? (gy - 0.1f) - trunk.min.y : 0; var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var t in ts) { new GameObject("move|" + t.name + "|" + t.position.ToString("F3")).transform.SetParent(log.transform, false); t.position += new Vector3(dx, lift, dz); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return ts.Count + " objects moved (" + dx + ", " + dz + "), lifted " + lift.ToString("F2") + " m onto " + (gy > float.MinValue ? "ground y " + gy.ToString("F2") : "nothing");
    }
    // colliders under `roots` with no physic material get Resources/Physics/<matPath> (e.g. GSPstone)
    public string PhysFix()
    {
        var pm = Resources.Load<PhysicMaterial>("Physics/" + matPath); if (!pm) return matPath + " not found"; var o = new List<string>();
        foreach (var rn in roots.Split(',')) { var r = GameObject.Find(rn); if (!r) continue; foreach (var c in r.GetComponentsInChildren<Collider>(true)) if (!c.sharedMaterial) { c.sharedMaterial = pm; o.Add(c.name); } }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); return o.Count + " colliders -> " + pm.name + ": " + string.Join(", ", o.ToArray());
    }
    // vertex-colour census of OPCD meshes `words` inside the box: dominant channel counts and centroids (OPCD 4-colour paint: R/G/B/A layers)
    public string VColors()
    {
        var o = new List<string>();
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).Where(f => words.Split(',').Contains(f.name) && f.sharedMesh))
        {
            var m = mf.sharedMesh; var v = m.vertices; var c = m.colors; if (c.Length != v.Length) { o.Add(mf.name + ": no colours"); continue; }
            var groups = new Dictionary<string, List<Vector3>>();
            for (int i = 0; i < v.Length; i++) { var w = mf.transform.TransformPoint(v[i]); if (w.x < x0 || w.x > x1 || w.z < z0 || w.z > z1) continue; var col = c[i]; string k = col.r >= Mathf.Max(col.g, col.b) ? "R" : col.g >= col.b ? "G" : "B"; if (col.a < 0.5f) k += "a"; List<Vector3> l; if (!groups.TryGetValue(k, out l)) groups[k] = l = new List<Vector3>(); l.Add(w); }
            foreach (var g in groups.OrderByDescending(q => q.Value.Count)) o.Add(mf.name + " " + g.Key + ": " + g.Value.Count + " c(" + g.Value.Average(q => q.x).ToString("F0") + "," + g.Value.Average(q => q.z).ToString("F0") + ") x " + g.Value.Min(q => q.x).ToString("F0") + "-" + g.Value.Max(q => q.x).ToString("F0") + " z " + g.Value.Min(q => q.z).ToString("F0") + "-" + g.Value.Max(q => q.z).ToString("F0"));
        }
        return string.Join("\n", o.ToArray());
    }
    // read-only: OPCD object `meshName` whose MeshCollider mesh differs from its render mesh: per-vertex height difference (same vertex
    // count) or, if counts differ, the collider surface height under each render vertex; max/mean, worst spot, and slope of each at the worst
    public string ColVsRender()
    {
        Physics.SyncTransforms(); var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName; var mc = mf.GetComponent<MeshCollider>(); if (!mc || !mc.sharedMesh) return "no mesh collider";
        var a = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var b = mc.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var d = new List<float>(); int wi = 0; float wd = 0;
        if (a.Length == b.Length) for (int i = 0; i < a.Length; i++) { float dy = b[i].y - a[i].y; float dxz = new Vector2(b[i].x - a[i].x, b[i].z - a[i].z).magnitude; d.Add(Mathf.Abs(dy)); if (Mathf.Abs(dy) + dxz > wd) { wd = Mathf.Abs(dy) + dxz; wi = i; } }
        else for (int i = 0; i < a.Length; i++) { RaycastHit h; if (mc.Raycast(new Ray(new Vector3(a[i].x, a[i].y + 5, a[i].z), Vector3.down), out h, 20)) { float dy = h.point.y - a[i].y; d.Add(Mathf.Abs(dy)); if (Mathf.Abs(dy) > wd) { wd = Mathf.Abs(dy); wi = i; } } }
        if (d.Count == 0) return "nothing compared";
        return meshName + ": render " + mf.sharedMesh.name + " (" + a.Length + " v) vs collider " + mc.sharedMesh.name + " (" + b.Length + " v): |dy| max " + (d.Max() * 100).ToString("F1") + " cm, mean " + (d.Average() * 100).ToString("F2") + " cm, " + d.Count(x => x > 0.01f) + " vertices over 1 cm; worst at (" + a[wi].x.ToString("F1") + "," + a[wi].z.ToString("F1") + ")" + (a.Length == b.Length ? " render y " + a[wi].y.ToString("F3") + " collider " + b[wi].ToString("F3") : "");
    }
    // brightness (BoJa: "green and lighting very bright"): the scene's global PostProcessVolume points at profile asset `matPath` (e.g. a
    // Tierra copy of the template's v4_Sunny_Pop with a lower postExposure). Logged (old profile path) so it can be switched back.
    public string SetPostProfile()
    {
        var prof = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(matPath); if (!prof) return "no profile at " + matPath;
        foreach (var c in Resources.FindObjectsOfTypeAll<Component>().Where(c => c && c.gameObject.scene.IsValid() && c.GetType().Name == "PostProcessVolume"))
        {
            var f = c.GetType().GetField("sharedProfile"); var old = f.GetValue(c) as Object; f.SetValue(c, prof); var pf = c.GetType().GetField("profile"); if (pf != null) pf.SetValue(c, null); UnityEditor.EditorUtility.SetDirty(c);
            var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("postprofile|" + c.gameObject.name + "|" + (old ? UnityEditor.AssetDatabase.GetAssetPath(old) : "-")).transform.SetParent(log.transform, false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene); return c.gameObject.name + ": " + (old ? old.name : "-") + " -> " + prof.name;
        }
        return "no PostProcessVolume";
    }
    // install gate (BoJa: nothing planted or moved onto play surfaces without his OK): CrownsOver (dry run) in a box around every active green
    // mesh (+20 m) and tee mesh (+10 m). A crown over a green, collar or tee is a FAIL, over fairway a WARN, unless "root/name x z" (1 m) is
    // listed in AutomationOutput/placement_exceptions.txt (BoJa-approved). Returns "GATE PASS" or "GATE FAIL" first, then the list.
    public string PlacementGate()
    {
        var opcdT = GameObject.Find("OPCD Meshes").transform; var ex = new List<string>(); var exf = "AutomationOutput/placement_exceptions.txt"; if (System.IO.File.Exists(exf)) ex = System.IO.File.ReadAllLines(exf).Where(l => l.Trim().Length > 0 && !l.StartsWith("#")).ToList();
        var boxes = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh && (System.Text.RegularExpressions.Regex.IsMatch(f.name, @"_Green_-_Mesh") || f.name.Contains("_Tee_-_Mesh"))).Select(f => { var b = f.GetComponent<Renderer>().bounds; float m = f.name.Contains("_Tee_") ? 10 : 20; return new Vector4(b.min.x - m, b.max.x + m, b.min.z - m, b.max.z + m); }).ToList();
        var seen = new HashSet<string>(); var fails = new List<string>(); var warns = new List<string>(); bool ap = apply; string cp = csvPath, wd = words; apply = false; csvPath = ""; words = "green,collar";
        try
        {
            foreach (var bx in boxes)
            {
                x0 = bx.x; x1 = bx.y; z0 = bx.z; z1 = bx.w; var res = CrownsOver().Split('\n').Skip(1);
                foreach (var l in res)
                {
                    if (l.Length == 0 || !seen.Add(l)) continue; var m = System.Text.RegularExpressions.Regex.Match(l, @"^(\S+) \(([-\d.]+),([-\d.]+)\)"); if (m.Success && ex.Any(e => { var p = e.Split(' '); return p.Length >= 3 && p[0] == m.Groups[1].Value && Mathf.Abs(float.Parse(p[1]) - float.Parse(m.Groups[2].Value)) < 1 && Mathf.Abs(float.Parse(p[2]) - float.Parse(m.Groups[3].Value)) < 1; })) continue;
                    var ov = System.Text.RegularExpressions.Regex.Match(l, @" by ([-\d.]+) m"); float ovm = ov.Success ? float.Parse(ov.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0; if ((l.Contains(" over green ") || l.Contains(" over collar ")) && ovm >= 1f) fails.Add(l); else warns.Add(l);   /* edge touches and tee shade trees are warnings for BoJa, not blockers */
                }
            }
        }
        finally { apply = ap; csvPath = cp; words = wd; }
        return (fails.Count == 0 ? "GATE PASS" : "GATE FAIL") + ": " + fails.Count + " crowns 1 m+ over a green/collar, " + warns.Count + " other overhangs (warn, for BoJa)\n" + string.Join("\n", fails.Select(s => "FAIL " + s).Concat(warns.Take(80).Select(s => "WARN " + s)).ToArray());
    }
    // undo CrownsOver moves: every "move|<root>/<name>|<old pos>|crown" entry whose path is in `ids` ("root/name|..."; "*" = all) puts the
    // plant of that name nearest its old spot (within 30 m) back there; entry renamed "unmove|...".
    public string CrownUndo()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture; var log = GameObject.Find("NIGHT1031_LOG"); if (!log) return "no log"; var want = ids.Split('|').Where(s => s.Length > 0).ToList(); var o = new List<string>();
        foreach (Transform e in log.transform.Cast<Transform>().ToList())
        {
            var f = e.name.Split('|'); if (f.Length < 4 || f[0] != "move" || f[3] != "crown" || (ids != "*" && !want.Contains(f[1]))) continue; var pp = f[2].Trim('(', ')').Split(','); var old = new Vector3(float.Parse(pp[0], ci), float.Parse(pp[1], ci), float.Parse(pp[2], ci));
            var parts = f[1].Split('/'); var r0 = GameObject.Find(parts[0]); if (!r0) continue; var t = r0.GetComponentsInChildren<LODGroup>().Select(l => l.transform).Where(x => x.name == parts[parts.Length - 1]).OrderBy(x => (x.position - old).sqrMagnitude).FirstOrDefault(); if (!t || (t.position - old).magnitude > 30) { o.Add(f[1] + " not found"); continue; }
            t.position = old; e.name = "un" + e.name; o.Add(f[1] + " back at " + old.ToString("F1"));
        }
        Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return string.Join("\n", o.ToArray());
    }
    // keep a (re-graded) collar on top: every other OPCD turf mesh (not green / collar / bunker / water / hazard) with vertices under the
    // footprint of collar mesh `meshName` gets those vertices sunk to 3 cm below the collar if they were within 2 cm of it or above it, so
    // rough/blend never pokes through the collar band (look and ball). A-owned copies (generated/sinkunder), colliders follow.
    public string SinkUnder()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var all = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList(); var cm = all.FirstOrDefault(f => f.name == meshName); if (!cm) return "no " + meshName;
        var cc = cm.GetComponent<MeshCollider>(); if (!cc) return "collar has no MeshCollider"; var cb = cm.GetComponent<Renderer>().bounds; var o = new List<string>(); const string dir = "Assets/TierraRejada/generated/sinkunder";
        if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "sinkunder"); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var f in all)
        {
            if (f == cm) continue; var ln = f.name.ToLower(); if (ln.Contains("_green_-_mesh") || ln.Contains("collar") || ln.Contains("bunker") || ln.Contains("water") || ln.Contains("hazard")) continue; var fb = f.GetComponent<Renderer>().bounds; if (!fb.Intersects(cb)) continue;
            var src = f.sharedMesh; var w = src.vertices.Select(v => f.transform.TransformPoint(v)).ToArray(); int n = 0;
            for (int i = 0; i < w.Length; i++) { if (w[i].x < cb.min.x || w[i].x > cb.max.x || w[i].z < cb.min.z || w[i].z > cb.max.z) continue; RaycastHit h; if (!cc.Raycast(new Ray(new Vector3(w[i].x, cb.max.y + 5, w[i].z), Vector3.down), out h, cb.size.y + 20)) continue; bool nearRim = false; for (int k = 0; k < 8 && !nearRim; k++) { RaycastHit h2; var q = new Vector3(w[i].x + Mathf.Cos(k * Mathf.PI / 4) * 0.4f, cb.max.y + 5, w[i].z + Mathf.Sin(k * Mathf.PI / 4) * 0.4f); if (!cc.Raycast(new Ray(q, Vector3.down), out h2, cb.size.y + 20)) nearRim = true; } float tg = h.point.y - (nearRim ? 0.005f : 0.03f); if (nearRim ? Mathf.Abs(w[i].y - tg) > 0.002f && w[i].y > h.point.y - 0.06f : w[i].y > h.point.y - 0.02f) { w[i].y = tg; n++; } }   /* within 0.4 m of the rim: 5 mm under (no dip at the edge); inside: 3 cm under */
            if (n == 0) continue; var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_su"; nm.vertices = w.Select(v => f.transform.InverseTransformPoint(v)).ToArray(); nm.RecalculateNormals(); nm.RecalculateBounds();
            var ap = dir + "/" + f.name + "_" + meshName.Replace("SPatch_", "").Replace("_-_Mesh", "") + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) ap = ap.Replace(".asset", "_2.asset"); UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
            new GameObject("sinkunder|" + f.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); var mc = f.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; } f.sharedMesh = nm; o.Add(f.name + " " + n);
        }
        UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return meshName + ": sunk " + (o.Count == 0 ? "nothing" : string.Join(", ", o.ToArray()));
    }
    // read-only: heights the ball sees (first OPCD collider from above, with its name) on a `gap`-m grid over the box x0..x1, z0..z1 -> csvPath
    public string GridHeights()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var rows = new List<string> { "x,z,y,surface" }; float s = Mathf.Max(0.1f, gap);
        for (float x = x0; x <= x1; x += s) for (float z = z0; z <= z1; z += s) { var h = Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200).Where(r => r.collider.transform.IsChildOf(opcdT)).OrderBy(r => r.distance).FirstOrDefault(); if (h.collider) rows.Add(x.ToString("F2") + "," + z.ToString("F2") + "," + h.point.y.ToString("F4") + "," + h.collider.name); }
        System.IO.File.WriteAllLines(csvPath, rows.ToArray()); return (rows.Count - 1) + " samples -> " + csvPath;
    }
    // dark/odd paint patch (BoJa 17: dark round blotch on the hill): OPCD mesh `meshName` vertex colours within `r` m of (cx, cz) are blended
    // toward the mean colour of its vertices in the ring r..reach (smootherstep, full at the centre). Dry run lists inside vs ring means.
    public string ColorPatch()
    {
        var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName; var src = mf.sharedMesh; var col = src.colors; if (col.Length == 0) return "no vertex colours";
        var w = src.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); var c = new Vector2(cx, cz); Color ins = Color.clear, ring = Color.clear; int ni = 0, nr = 0;
        for (int i = 0; i < w.Length; i++) { float d = (new Vector2(w[i].x, w[i].z) - c).magnitude; if (d < r) { ins += col[i]; ni++; } else if (d < reach) { ring += col[i]; nr++; } }
        if (nr == 0) return "no ring vertices"; ins = ni > 0 ? ins / ni : Color.clear; ring /= nr; string res = meshName + ": inside " + ni + " v mean " + ins + ", ring " + nr + " v mean " + ring;
        if (!apply || ni == 0) return "(dry run) " + res;
        var nc = (Color[])col.Clone(); for (int i = 0; i < w.Length; i++) { float d = (new Vector2(w[i].x, w[i].z) - c).magnitude; if (d >= r) continue; float t = Mathf.Clamp01(d / r); float s = 1 - t * t * t * (t * (t * 6 - 15) + 10); nc[i] = Color.Lerp(col[i], ring, s); }
        const string dir = "Assets/TierraRejada/generated/vpaint"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "vpaint");
        var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_cp"; nm.colors = nc; var ap = dir + "/" + mf.name + "_cp_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("colorpatch|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
        var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; } mf.sharedMesh = nm; UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return res;
    }
    // draped turf patch from a triangulated CSV (`csvPath`: "v,N" then x,z,isBoundary rows, "t,M" then a,b,c rows): heights = top OPCD
    // surface under each vertex + 5 mm (2 mm on the boundary, so no step), material / first vertex colour / physic material / UV scale
    // copied from OPCD mesh `fromName` (e.g. the hole's fairway); object "<fromName>_patch_<tag>" under OPCD Meshes (replaced on re-run).
    public string BuildPatchCsv()
    {
        Physics.SyncTransforms(); var ci = System.Globalization.CultureInfo.InvariantCulture; var opcdT = GameObject.Find("OPCD Meshes").transform; var ff = opcdT.GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == fromName); if (!ff) return "no " + fromName;
        var L = System.IO.File.ReadAllLines(csvPath); int i = 0; int nv = int.Parse(L[i++].Split(',')[1]); var xz = new List<Vector2>(); var bnd = new List<bool>(); var fy = new List<float>(); var tuck = new List<bool>(); for (int k = 0; k < nv; k++) { var a = L[i++].Split(','); xz.Add(new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci))); bnd.Add(a[2] == "1"); tuck.Add(a[2] == "2"); fy.Add(a.Length > 3 ? float.Parse(a[3], ci) : float.NaN); }   /* optional 4th column: ramp height, kept >= surface + 5 mm (flag 2: exact, tucked under a pad) */
        int nt = int.Parse(L[i++].Split(',')[1]); var T = new List<int>(); for (int k = 0; k < nt; k++) { var a = L[i++].Split(','); T.AddRange(new[] { int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]) }); }
        var name = fromName + "_patch_" + tag; var old = opcdT.Find(name); if (old) Object.DestroyImmediate(old.gameObject);   /* only this tool's own patch */
        var V = new List<Vector3>(); int miss = 0; foreach (var q in xz.Select((p, k) => new { p, k })) { var h = Physics.RaycastAll(new Vector3(q.p.x, 600, q.p.y), Vector3.down, 1200).Where(r => r.collider.transform.IsChildOf(opcdT)).OrderBy(r => r.distance).FirstOrDefault(); if (!h.collider) miss++; float sy = (h.collider ? h.point.y : 0) + (bnd[q.k] ? 0.002f : 0.005f); V.Add(new Vector3(q.p.x, float.IsNaN(fy[q.k]) ? sy : tuck[q.k] ? fy[q.k] : Mathf.Max(fy[q.k], sy), q.p.y)); }
        var fm = ff.sharedMesh; var fw = fm.vertices.Select(v => ff.transform.TransformPoint(v)).ToArray(); var fu = fm.uv; float ku = 1, kv = 1; if (fu.Length == fw.Length && fw.Length > 2) { float wx = fw.Max(p => p.x) - fw.Min(p => p.x), wz = fw.Max(p => p.z) - fw.Min(p => p.z), ux = fu.Max(u => u.x) - fu.Min(u => u.x), uy = fu.Max(u => u.y) - fu.Min(u => u.y); if (wx > 0.1f && wz > 0.1f) { ku = ux / wx; kv = uy / wz; } }
        var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(V); m.SetTriangles(T, 0); m.SetUVs(0, xz.Select(p => fu.Length == fw.Length ? new Vector2(p.x * ku, p.y * kv) : Vector2.zero).ToList());   /* a source with no UVs samples one texel: match it, or the patch reads paler */ m.colors = Enumerable.Repeat(fm.colors.Length > 0 ? fm.colors[0] : Color.white, V.Count).ToArray(); m.RecalculateNormals();
        if (m.normals.Average(n => n.y) < 0) { var tt = m.triangles; for (int k = 0; k < tt.Length; k += 3) { var x = tt[k + 1]; tt[k + 1] = tt[k + 2]; tt[k + 2] = x; } m.triangles = tt; m.RecalculateNormals(); } m.RecalculateBounds();
        const string dir = "Assets/TierraRejada/generated/patches"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "patches"); var ap = dir + "/" + name + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var go = new GameObject(name); go.transform.SetParent(opcdT, false); go.layer = ff.gameObject.layer; go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterials = ff.GetComponent<MeshRenderer>().sharedMaterials.Take(1).ToArray(); var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; var fc = ff.GetComponent<Collider>(); if (fc) mc.sharedMaterial = fc.sharedMaterial;
        UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(opcdT.gameObject.scene);
        return name + ": " + V.Count + " vertices, " + T.Count / 3 + " triangles, " + miss + " with nothing under them; material " + ff.GetComponent<MeshRenderer>().sharedMaterial.name + ", physics " + (fc && fc.sharedMaterial ? fc.sharedMaterial.name : "-") + ", uv scale " + ku.ToString("F3") + "/" + kv.ToString("F3");
    }
    // ramp the turf between two tee pads (BoJa 5: hard ledge + trough between the back strip and the next box): mesh `meshName` vertices
    // between pad `fromName` and pad `greenName` (OPCD tee meshes, "_wide" included by name), not under either pad, get the straight ramp height
    // from pad A's nearest edge vertex to pad B's (weighted by distance), at full strength within the pads' common width and smootherstep to
    // nothing over `ease` m beyond it. A-owned copy (generated/padbridge), collider follows. Dry run unless apply.
    public string PadBridge()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var mfs = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList(); var mf = mfs.FirstOrDefault(f => f.name == meshName); var pa = mfs.FirstOrDefault(f => f.name == fromName); var pb = mfs.FirstOrDefault(f => f.name == greenName); if (!mf || !pa || !pb) return "need meshName, fromName (pad A), greenName (pad B)";
        System.Func<MeshFilter, Vector3[]> W = f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray(); var A = W(pa); var B = W(pb); var ca = new Vector2(A.Average(v => v.x), A.Average(v => v.z)); var cb = new Vector2(B.Average(v => v.x), B.Average(v => v.z));
        var ax = (cb - ca).normalized; var lat = new Vector2(-ax.y, ax.x); float half = Mathf.Min(A.Max(v => Mathf.Abs(Vector2.Dot(new Vector2(v.x, v.z) - ca, lat))), B.Max(v => Mathf.Abs(Vector2.Dot(new Vector2(v.x, v.z) - cb, lat))));
        var ra = pa.GetComponent<Collider>(); var rb = pb.GetComponent<Collider>(); float fe = ease > 0 ? ease : 2f; float aFront = A.Max(v => Vector2.Dot(new Vector2(v.x, v.z) - ca, ax)), bBack = B.Min(v => Vector2.Dot(new Vector2(v.x, v.z) - ca, ax));   /* only the gap between the pads' facing edges */
        var src = mf.sharedMesh; var w = W(mf); int n = 0; float mx = 0;
        for (int i = 0; i < w.Length; i++)
        {
            var q = new Vector2(w[i].x, w[i].z); float sAx = Vector2.Dot(q - ca, ax); if (sAx < aFront - 0.3f || sAx > bBack + 0.3f) continue; float t = sAx / Mathf.Max(0.1f, (cb - ca).magnitude); float l = Mathf.Abs(Vector2.Dot(q - ca, lat)) - Mathf.Abs(Vector2.Dot((cb - ca) * t, lat)); if (l > half + fe) continue;
            RaycastHit hh; var down = new Ray(new Vector3(q.x, w[i].y + 30, q.y), Vector3.down); if ((ra && ra.Raycast(down, out hh, 60)) || (rb && rb.Raycast(down, out hh, 60))) continue;   /* under a pad */
            var na = A.OrderBy(v => (new Vector2(v.x, v.z) - q).sqrMagnitude).First(); var nb = B.OrderBy(v => (new Vector2(v.x, v.z) - q).sqrMagnitude).First(); float da = (new Vector2(na.x, na.z) - q).magnitude, db = (new Vector2(nb.x, nb.z) - q).magnitude;
            float tgt = Mathf.Lerp(na.y, nb.y, da / Mathf.Max(0.01f, da + db)); float k = l <= half ? 1 : 1 - Mathf.Clamp01((l - half) / fe); k = k * k * k * (k * (k * 6 - 15) + 10); float ny = Mathf.Lerp(w[i].y, tgt, k); if (Mathf.Abs(ny - w[i].y) < 0.002f) continue; mx = Mathf.Max(mx, Mathf.Abs(ny - w[i].y)); w[i].y = ny; n++;
        }
        string res = meshName + ": " + n + " vertices ramped between " + fromName + " and " + greenName + " (common half-width " + half.ToString("F1") + " m), largest move " + (mx * 100).ToString("F1") + " cm";
        if (!apply || n == 0) return (apply ? "" : "(dry run) ") + res;
        const string dir = "Assets/TierraRejada/generated/padbridge"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "padbridge");
        var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_pb"; nm.vertices = w.Select(v => mf.transform.InverseTransformPoint(v)).ToArray(); nm.RecalculateNormals(); nm.RecalculateBounds(); var ap = dir + "/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("padbridge|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false);
        var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; } mf.sharedMesh = nm; UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return res;
    }
    // ramp patch between two tee pads (5 tee ledge): 0.25 m grid on the pad-centre axis; per lateral row the pad A front edge and pad B back
    // edge are found by raycasting each pad's collider along the row, the row ramps straight between the two edge heights; rows where a pad
    // is missing take the nearest full row and feather to the current surface over `ease` m. Writes csvPath (x,z,0,y + grid triangles) for
    // BuildPatchCsv; fill only (the patch keeps >= surface + 5 mm). Read-only.
    public string PadRamp()
    {
        Physics.SyncTransforms(); var ci = System.Globalization.CultureInfo.InvariantCulture; var opcdT = GameObject.Find("OPCD Meshes").transform; var mfs = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList(); var pa = mfs.FirstOrDefault(f => f.name == fromName); var pb = mfs.FirstOrDefault(f => f.name == greenName); if (!pa || !pb) return "need fromName (pad A), greenName (pad B)";
        var ra = pa.GetComponent<Collider>(); var rb = pb.GetComponent<Collider>(); System.Func<MeshFilter, Vector2> C = f => { var b = f.GetComponent<Renderer>().bounds; return new Vector2(b.center.x, b.center.z); }; var ca = C(pa); var cb = C(pb); var ax = (cb - ca).normalized; var lat = new Vector2(-ax.y, ax.x); float D = (cb - ca).magnitude;
        System.Func<Collider, Vector2, float> Hit = (c, p) => { RaycastHit h; return c.Raycast(new Ray(new Vector3(p.x, 600, p.y), Vector3.down), out h, 1200) ? h.point.y : float.NaN; };
        System.Func<Vector2, float> Surf = p => { var h = Physics.RaycastAll(new Vector3(p.x, 600, p.y), Vector3.down, 1200).Where(r => r.collider.transform.IsChildOf(opcdT) && !r.collider.name.Contains("_patch_")).OrderBy(r => r.distance).FirstOrDefault(); return h.collider ? h.point.y : float.NaN; };
        const float g = 0.25f; float fe = ease > 0 ? ease : 2f; var rows = new Dictionary<int, float[]>();   /* lateral index -> sAfront, yA, sBback, yB */
        for (int li = -60; li <= 60; li++)
        {
            float l = li * g; float sAf = float.NaN, yA = 0, sBb = float.NaN, yB = 0;
            for (float s = 0; s <= D; s += 0.05f) { var p = ca + ax * s + lat * l; float y = Hit(ra, p); if (!float.IsNaN(y)) { sAf = s; yA = y; } }
            for (float s = D; s >= 0; s -= 0.05f) { var p = ca + ax * s + lat * l; float y = Hit(rb, p); if (!float.IsNaN(y)) { sBb = s; yB = y; } }
            if (!float.IsNaN(sAf) && !float.IsNaN(sBb) && sBb > sAf + 0.3f && sBb - sAf < 15f) rows[li] = new[] { sAf, yA, sBb, yB };
        }
        if (rows.Count == 0) return "no common rows"; if (!rows.ContainsKey(0)) return "centre row has no gap"; float cg = rows[0][2] - rows[0][0]; System.Func<int, bool> Full = k => rows.ContainsKey(k) && rows[k][2] - rows[k][0] <= cg + 1.5f;   /* a row whose gap opens past a pad corner is shoulder, not ramp */
        int lo = 0, hi = 0; while (Full(lo - 1)) lo--; while (Full(hi + 1)) hi++;
        int nfe = Mathf.CeilToInt(fe / g); var idx = new Dictionary<long, int>(); var o = new List<string>(); var T = new List<string>(); int nv = 0; float mx = 0;
        for (int li = lo - nfe; li <= hi + nfe; li++)
        {
            var r = rows[Mathf.Clamp(li, lo, hi)]; float k = li < lo ? 1 - (lo - li) * g / fe : li > hi ? 1 - (li - hi) * g / fe : 1; k = Mathf.Clamp01(k); k = k * k * k * (k * (k * 6 - 15) + 10);
            for (int si = Mathf.FloorToInt((r[0] - 0.25f) / g); si * g <= r[2] + 0.25f + 1e-3f; si++)
            {
                float s = si * g; var p = ca + ax * s + lat * (li * g); float sf = Surf(p); if (float.IsNaN(sf)) continue; float t = Mathf.Clamp01((s - r[0]) / (r[2] - r[0])); float y = Mathf.Lerp(sf, Mathf.Lerp(r[1], r[3], t), k);
                bool under = !float.IsNaN(Hit(ra, p)) || !float.IsNaN(Hit(rb, p)); if (under) y = sf - 0.03f;   /* ends tuck 3 cm under the pad, never over it */
                mx = Mathf.Max(mx, y - sf); idx[(li + 1000L) * 10000 + si + 1000] = nv++; o.Add(p.x.ToString("F3", ci) + "," + p.y.ToString("F3", ci) + (under ? ",2," : ",0,") + y.ToString("F3", ci) + "," + sf.ToString("F3", ci) + "," + li + "," + si);
            }
        }
        foreach (var kv in idx.ToList()) { long kk = kv.Key; int b, c, d; if (idx.TryGetValue(kk + 10000, out b) && idx.TryGetValue(kk + 1, out c) && idx.TryGetValue(kk + 10001, out d)) { T.Add(kv.Value + "," + b + "," + d); T.Add(kv.Value + "," + d + "," + c); } }
        System.IO.File.WriteAllLines(csvPath, new[] { "v," + nv }.Concat(o).Concat(new[] { "t," + T.Count }).Concat(T).ToArray());
        return nv + " vertices, " + T.Count + " triangles -> " + csvPath + "; full rows " + (lo * g).ToString("F2") + ".." + (hi * g).ToString("F2") + " m, centre gap " + rows[0][0].ToString("F2") + "->" + rows[0][2].ToString("F2") + " m (" + rows[0][1].ToString("F3") + " -> " + rows[0][3].ToString("F3") + "), largest fill " + (mx * 100).ToString("F1") + " cm";
    }
    // read-only: world position, uv, uv2, colour of a few vertices of OPCD mesh `meshName`, plus the material's texture names and scales
    public string UvProbe()
    {
        var mf = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName; var m = mf.sharedMesh; var v = m.vertices; var u = m.uv; var u2 = m.uv2; var c = m.colors; var o = new List<string>();
        foreach (int i in new[] { 0, v.Length / 3, v.Length / 2, v.Length - 1 }) { var w = mf.transform.TransformPoint(v[i]); o.Add(w.ToString("F2") + " uv " + (u.Length > i ? u[i].ToString("F4") : "-") + " uv2 " + (u2.Length > i ? u2[i].ToString("F4") : "-") + " col " + (c.Length > i ? c[i].ToString() : "-")); }
        var mat = mf.GetComponent<Renderer>().sharedMaterial; o.Add(mat.shader.name); foreach (var p in new[] { "_MainTex", "_Splat0", "_Control", "_BaseMap", "_SatTex", "_Satellite" }) if (mat.HasProperty(p)) o.Add(p + " " + (mat.GetTexture(p) ? mat.GetTexture(p).name : "-") + " st " + mat.GetTextureScale(p) + mat.GetTextureOffset(p));
        return string.Join("\n", o.ToArray());
    }
    // read-only: every renderer/collider under root `colRoot` whose bounds touch the box x0..x1, z0..z1: path, mesh, material, verts, bounds
    public string Kids()
    {
        var o = new List<string>(); var all = colRoot == "*" ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)) : (GameObject.Find(colRoot) ? GameObject.Find(colRoot).GetComponentsInChildren<Transform>(true) : new Transform[0]);
        foreach (var t in all)
        {
            var r = t.GetComponent<Renderer>(); var c = t.GetComponent<Collider>(); if (!r && !c) continue; if (colRoot == "*" && (!r || c || t.GetComponentInParent<LODGroup>() || !t.gameObject.activeInHierarchy)) continue;   /* "*": active render-only, no plants */ var b = r ? r.bounds : c.bounds; if (b.max.x < x0 || b.min.x > x1 || b.max.z < z0 || b.min.z > z1) continue; var mf = t.GetComponent<MeshFilter>();
            o.Add((t.parent ? t.parent.name + "/" : "") + t.name + (t.gameObject.activeInHierarchy ? "" : " (off)") + " mesh " + (mf && mf.sharedMesh ? mf.sharedMesh.name + " v" + mf.sharedMesh.vertexCount : "-") + " mat " + (r && r.sharedMaterial ? r.sharedMaterial.name + " / " + r.sharedMaterial.shader.name : "-") + " col " + (c ? c.GetType().Name : "-") + " b " + b.min.ToString("F0") + "-" + b.max.ToString("F0"));
        }
        return o.Count + "\n" + string.Join("\n", o.Take(60).ToArray());
    }
    // 18 hill feather (after-test e): the SoftDecal meshes named in `words` (children of root `colRoot`) were cut on the 1 m grid, so their
    // outline is a stair-step. Vertex alpha = smootherstep of the xz distance to the outline over `feather` m (0 on the outline), so the soil
    // fades into the turf with no square edge. Edges shared between the listed meshes (lower/upper band seam) are not outline. A-owned copies
    // (generated/h18hill/*_fe); geometry untouched; no plants touched. Dry run unless apply.
    public string DecalFeather()
    {
        var root = GameObject.Find(colRoot); if (!root) return "no " + colRoot; var names = words.Split(','); var mfs = root.GetComponentsInChildren<MeshFilter>(true).Where(f => names.Contains(f.name) && f.sharedMesh).ToList(); if (mfs.Count == 0) return "none of " + words;
        System.Func<Vector3, long> K = p => Mathf.RoundToInt(p.x * 50) * 100000000L + Mathf.RoundToInt(p.z * 50);
        var outl = new Dictionary<MeshFilter, List<Vector2>>(); var bpos = new Dictionary<long, int>();   /* outline vertex positions, counted per mesh */
        foreach (var f in mfs)
        {
            var m = f.sharedMesh; var v = m.vertices.Select(q => f.transform.TransformPoint(q)).ToArray(); var t = m.triangles; var ec = new Dictionary<long, int>();
            for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = t[i + e], b = t[i + (e + 1) % 3]; long k = (long)Mathf.Min(a, b) * 10000000 + Mathf.Max(a, b); int c; ec.TryGetValue(k, out c); ec[k] = c + 1; }
            var bv = new HashSet<int>(); foreach (var kv in ec) if (kv.Value == 1) { bv.Add((int)(kv.Key / 10000000)); bv.Add((int)(kv.Key % 10000000)); }
            outl[f] = bv.Select(i => new Vector2(v[i].x, v[i].z)).ToList(); foreach (var i in bv) { long k = K(v[i]); int c; bpos.TryGetValue(k, out c); bpos[k] = c + 1; }
        }
        var pts = outl.Values.SelectMany(l => l).Where(p => bpos[K(new Vector3(p.x, 0, p.y))] == 1).ToList();   /* on two meshes' outlines = the band seam: not an edge */
        float fe = feather > 0 ? feather : 3f; var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var f in mfs)
        {
            var src = f.sharedMesh; var v = src.vertices.Select(q => f.transform.TransformPoint(q)).ToArray(); var col = new Color[v.Length]; int part = 0;
            for (int i = 0; i < v.Length; i++) { var q = new Vector2(v[i].x, v[i].z); float d = float.MaxValue; foreach (var p in pts) { float dd = (p - q).sqrMagnitude; if (dd < d) d = dd; } d = Mathf.Sqrt(d); float k = Mathf.Clamp01(d / fe); k = k * k * k * (k * (k * 6 - 15) + 10); col[i] = new Color(1, 1, 1, k); if (k < 1) part++; }
            o.Add(f.name + ": " + v.Length + " vertices, " + part + " faded (outline points " + outl[f].Count + ")"); if (!apply) continue;
            const string dir = "Assets/TierraRejada/generated/h18hill"; var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_fe"; nm.colors = col; var ap = dir + "/" + f.name + "_fe.asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap);
            new GameObject("decalfeather|" + f.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); f.sharedMesh = nm;
        }
        if (apply) { UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); }
        return (apply ? "" : "(dry run) ") + "seam-free outline points " + pts.Count + ", feather " + fe + " m; " + string.Join("; ", o.ToArray());
    }
    // 18 tee banks, cut side only (BoJa/coordinator 11/1: ease the bank tops, never pull a pad): around pad `greenName` (+ its "_wide"),
    // turf on bearings `b0`..`b1` (deg from north, clockwise, 15 deg taper) is held under a bank that starts `shelf` m off the pad edge at the
    // pad top - 3 cm and rises at 1:`cutG`, smooth-min'd over `fillet` m. Cut only (never raised), at most `maxCut` m, faded over the last
    // 1.5 m before `reach` m. Pinned: every tee pad, blend, green, collar, bunker, water and path vertex, with the change faded to zero from
    // 0.5 to 2 m off any of them; no vertex within 1 m of a plant base (LODGroup) moves, faded to full at 3 m. Terrain under the ring is kept 0.3 m below the
    // new turf. A-owned copies (generated/bankcut). Dry run unless apply.
    public float b0 = 0f, b1 = 0f, shelf = 1.5f, maxCut = 1.0f, cutG = 1.6f, fillet = 0.4f;
    public string BankCut()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var mfs = opcdT.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh).ToList();
        var padF = mfs.Where(f => f.name == greenName || f.name == greenName + "_wide").ToList(); if (padF.Count == 0) return "no " + greenName;
        var pw = padF.SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray(); var pxz = pw.Select(v => new Vector2(v.x, v.z)).ToArray(); var pc = new Vector2(pxz.Average(p => p.x), pxz.Average(p => p.y)); float top = pw.Max(v => v.y) - 0.03f;
        var hull = new List<Vector2>(); { var P = pxz.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList(); System.Func<Vector2, Vector2, Vector2, float> X = (o2, a, b) => (a.x - o2.x) * (b.y - o2.y) - (a.y - o2.y) * (b.x - o2.x); foreach (var pass in new[] { P, Enumerable.Reverse(P).ToList() }) { int st = hull.Count; foreach (var p in pass) { while (hull.Count >= st + 2 && X(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); } hull.RemoveAt(hull.Count - 1); } }
        System.Func<Vector2, float> DPad = q => { bool ins = true; float d = 1e9f; for (int i = 0; i < hull.Count; i++) { var a = hull[i]; var b = hull[(i + 1) % hull.Count]; var ab = b - a; float tt = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude); d = Mathf.Min(d, (a + ab * tt - q).magnitude); if ((ab.x * (q.y - a.y) - ab.y * (q.x - a.x)) < 0) ins = false; } return ins ? 0 : d; };
        System.Func<float, float, float, float> SMin = (a, b, k) => { float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k); return Mathf.Lerp(b, a, h) - k * h * (1 - h); };
        var box = new Rect(pc.x - reach - 12, pc.y - reach - 12, 2 * reach + 24, 2 * reach + 24);
        System.Func<string, bool> Prot = n => { var l = n.ToLower(); return l.Contains("_tee_") || l.Contains("green") || l.Contains("collar") || l.Contains("blend") || l.Contains("bunker") || l.Contains("water") || l.Contains("path") || l.Contains("fairway") || l.Contains("stair"); };
        var pins = new List<Vector2>(); foreach (var f in mfs.Where(f => Prot(f.name) && !padF.Contains(f))) { var b = f.GetComponent<Renderer>().bounds; if (b.max.x < box.xMin || b.min.x > box.xMax || b.max.z < box.yMin || b.min.z > box.yMax) continue; foreach (var v in f.sharedMesh.vertices) { var p = f.transform.TransformPoint(v); var q = new Vector2(p.x, p.z); if (box.Contains(q)) pins.Add(q); } }
        foreach (var root in new[] { "CART_PATHS" }) { var r0 = GameObject.Find(root); if (!r0) continue; foreach (var f in r0.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh)) { var b = f.GetComponent<Renderer>() ? f.GetComponent<Renderer>().bounds : new Bounds(); if (b.max.x < box.xMin || b.min.x > box.xMax || b.max.z < box.yMin || b.min.z > box.yMax) continue; foreach (var v in f.sharedMesh.vertices) { var p = f.transform.TransformPoint(v); var q = new Vector2(p.x, p.z); if (box.Contains(q)) pins.Add(q); } } }
        var plants = Object.FindObjectsOfType<LODGroup>().Where(l => l.gameObject.activeInHierarchy).Select(l => new Vector2(l.transform.position.x, l.transform.position.z)).Where(q => box.Contains(q)).ToList();
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.GetComponentInParent<LODGroup>() && !r.transform.IsChildOf(opcdT)))   /* combined plant meshes (hill brush, shrub tiles): every vertex within 0.4 m of the ground is a base */
        { var rn = r.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD") || rn == "FENCES" || rn == "NETS" || rn.StartsWith("DEEP") || rn == "NIGHT1031_LOG") continue; var b2 = r.bounds; if (b2.max.x < box.xMin || b2.min.x > box.xMax || b2.max.z < box.yMin || b2.min.z > box.yMax) continue; var mf2 = r.GetComponent<MeshFilter>(); if (!mf2 || !mf2.sharedMesh || r.sharedMaterial && r.sharedMaterial.shader.name.Contains("Decal")) continue; var seen = new HashSet<long>();
          foreach (var v in mf2.sharedMesh.vertices) { var p = r.transform.TransformPoint(v); var q = new Vector2(p.x, p.z); if (!box.Contains(q)) continue; long ck = Mathf.FloorToInt(p.x * 2) * 100000L + Mathf.FloorToInt(p.z * 2); if (seen.Contains(ck)) continue; RaycastHit gh; if (Physics.Raycast(new Vector3(p.x, p.y + 0.5f, p.z), Vector3.down, out gh, 1.0f) && gh.collider.transform.IsChildOf(opcdT)) { seen.Add(ck); plants.Add(q); } } }
        System.Func<Vector2, Vector2> Target = q =>
        {
            float d = DPad(q); var NA = new Vector2(float.NaN, 0); if (d <= 0.05f || d >= reach) return NA; float brg = (Mathf.Atan2(q.x - pc.x, q.y - pc.y) * Mathf.Rad2Deg + 360) % 360; float span = (b1 - b0 + 360) % 360, off = (brg - b0 + 360) % 360; float sw = off <= span ? Mathf.Clamp01(Mathf.Min(off, span - off) / 15f + 0.0001f) : 0; if (sw <= 0) return NA;
            float dp = float.MaxValue; foreach (var p in pins) { float dd = (p - q).sqrMagnitude; if (dd < dp) dp = dd; } dp = Mathf.Sqrt(dp); float pw2 = Mathf.Clamp01((dp - 0.5f) / 1.5f); float dpl = 99; foreach (var p in plants) { float dd = (p - q).sqrMagnitude; if (dd < dpl * dpl) dpl = Mathf.Sqrt(dd); } pw2 = Mathf.Min(pw2, Mathf.Clamp01((dpl - 1.0f) / 2.0f)); if (pw2 <= 0) return NA;   /* plant bases: no change within 1 m, full at 3 m */
            float fade = 1 - Mathf.Clamp01((d - (reach - 1.5f)) / 1.5f); fade = fade * fade * (3 - 2 * fade); float k = sw * sw * (3 - 2 * sw) * pw2 * pw2 * (3 - 2 * pw2) * fade; return k <= 0 ? new Vector2(float.NaN, 0) : new Vector2(top + Mathf.Max(0, d - shelf) / cutG, k);
        };
        var changes = new List<KeyValuePair<MeshFilter, Vector3[]>>(); var o = new List<string>(); int tot = 0; float down = 0; var vmin = new Dictionary<long, float>();
        foreach (var f in mfs)
        {
            if (padF.Contains(f) || Prot(f.name)) continue; var bb = f.GetComponent<Renderer>().bounds; if (bb.max.x < pc.x - reach - 6 || bb.min.x > pc.x + reach + 6 || bb.max.z < pc.y - reach - 6 || bb.min.z > pc.y + reach + 6) continue;
            var w = f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray(); int n = 0;
            for (int i = 0; i < w.Length; i++)
            {
                var q = new Vector2(w[i].x, w[i].z); if ((q - pc).magnitude > reach + 8) continue; var tg = Target(q); if (float.IsNaN(tg.x)) continue; float y = w[i].y;
                float ny = Mathf.Lerp(y, Mathf.Min(y, SMin(y, tg.x, fillet)), tg.y); ny = Mathf.Max(ny, y - maxCut); if (y - ny < 0.003f) continue; down = Mathf.Max(down, y - ny); w[i].y = ny; n++; long key = Mathf.RoundToInt(q.x * 2) * 100000L + Mathf.RoundToInt(q.y * 2); float mv; vmin[key] = vmin.TryGetValue(key, out mv) ? Mathf.Min(mv, ny) : ny;
            }
            if (n > 0) { changes.Add(new KeyValuePair<MeshFilter, Vector3[]>(f, w)); tot += n; o.Add(f.name + " " + n); }
        }
        string res = greenName + ": top " + (top + 0.03f).ToString("F2") + ", " + tot + " vertices cut (max " + down.ToString("F2") + " m) in " + string.Join(", ", o.ToArray()) + "; pins " + pins.Count + ", plants guarded " + plants.Count;
        if (!apply) return "(dry run) " + res;
        const string dir = "Assets/TierraRejada/generated/bankcut"; if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "bankcut"); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var kv in changes) { var f = kv.Key; var src = f.sharedMesh; var nm = Object.Instantiate(src); nm.name = src.name.Replace("(Clone)", "") + "_bc"; nm.vertices = kv.Value.Select(v => f.transform.InverseTransformPoint(v)).ToArray(); nm.RecalculateNormals(); nm.RecalculateBounds(); var ap = dir + "/" + f.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) ap = ap.Replace(".asset", "_2.asset"); UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(nm, ap); new GameObject("bankcut|" + f.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); var mc = f.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = nm; } f.sharedMesh = nm; }
        var te = Object.FindObjectsOfType<Terrain>().FirstOrDefault(tt => tt.name == "Terrain (Inner)"); int tk = 0;
        if (te && vmin.Count > 0) { var td = te.terrainData; var tp = te.transform.position; int Rr = td.heightmapResolution; float sx = td.size.x / (Rr - 1), sz = td.size.z / (Rr - 1); int i0 = Mathf.Max(0, Mathf.FloorToInt((pc.x - reach - 3 - tp.x) / sx)), i1 = Mathf.Min(Rr - 1, Mathf.CeilToInt((pc.x + reach + 3 - tp.x) / sx)), j0 = Mathf.Max(0, Mathf.FloorToInt((pc.y - reach - 3 - tp.z) / sz)), j1 = Mathf.Min(Rr - 1, Mathf.CeilToInt((pc.y + reach + 3 - tp.z) / sz)); var Hh = td.GetHeights(i0, j0, i1 - i0 + 1, j1 - j0 + 1);
            for (int j = 0; j < Hh.GetLength(0); j++) for (int i = 0; i < Hh.GetLength(1); i++) { var q = new Vector2(tp.x + (i0 + i) * sx, tp.z + (j0 + j) * sz); float best = float.MaxValue; for (int dx = -3; dx <= 3; dx++) for (int dz = -3; dz <= 3; dz++) { float mv; if (vmin.TryGetValue((Mathf.RoundToInt(q.x * 2) + dx) * 100000L + Mathf.RoundToInt(q.y * 2) + dz, out mv)) best = Mathf.Min(best, mv); } if (best == float.MaxValue) continue; float tg = (best - 0.3f - tp.y) / td.size.y; if (Hh[j, i] > tg) { Hh[j, i] = tg; tk++; } }
            if (tk > 0) { td.SetHeights(i0, j0, Hh); UnityEditor.EditorUtility.SetDirty(td); } }
        UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return res + "; terrain cells lowered " + tk;
    }
    // read-only: combined plant meshes (any active non-OPCD, non-path MeshRenderer, LOD0 only) with vertices in the box: per 1 m cell the lowest
    // vertex vs the top OPCD ground under it; cells floating more than `gap` m listed (after a ground cut)
    public string FloatCells()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var low = new Dictionary<long, KeyValuePair<float, string>>();
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
        {
            var rn = r.transform.root.name; if (r.transform.IsChildOf(opcdT) || rn == "CART_PATHS" || rn == "Terrain (Inner)" || rn.StartsWith("DEEP") || rn == "FENCES") continue; var lg = r.GetComponentInParent<LODGroup>(); if (lg && lg.GetLODs().Length > 0 && !lg.GetLODs()[0].renderers.Contains(r)) continue;
            var b = r.bounds; if (b.max.x < x0 || b.min.x > x1 || b.max.z < z0 || b.min.z > z1) continue; var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            foreach (var v in mf.sharedMesh.vertices) { var p = r.transform.TransformPoint(v); if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue; long k = Mathf.FloorToInt(p.x) * 100000L + Mathf.FloorToInt(p.z); KeyValuePair<float, string> c; if (!low.TryGetValue(k, out c) || p.y < c.Key) low[k] = new KeyValuePair<float, string>(p.y, rn + "/" + r.name); }
        }
        var o = new List<string>();
        foreach (var kv in low) { float x = kv.Key / 100000L + 0.5f, z = kv.Key % 100000L + 0.5f; var h = Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200).Where(q => q.collider.transform.IsChildOf(opcdT)).OrderBy(q => q.distance).FirstOrDefault(); if (!h.collider) continue; float g = kv.Value.Key - h.point.y; if (g > gap) o.Add(x.ToString("F1") + "," + z.ToString("F1") + " gap " + g.ToString("F2") + " " + kv.Value.Value); }
        return low.Count + " cells, " + o.Count + " floating > " + gap + " m\n" + string.Join("\n", o.OrderByDescending(s => s).Take(40).ToArray());
    }
    // hole 1 pond edge (coordinator 11/1): triangles of OPCD water mesh `meshName` with any vertex more than `ease` m below `padY` (the pond
    // level) are the sheet running down to the lake; listed by 5 m x-band with what the top non-water surface under them is. With apply they
    // are dropped (A-owned copy, generated/wateron, collider follows).
    public string WaterStep()
    {
        Physics.SyncTransforms(); var opcdT = GameObject.Find("OPCD Meshes").transform; var mf = opcdT.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == meshName); if (!mf) return "no " + meshName;
        var src = mf.sharedMesh; var w = src.vertices.Select(q => mf.transform.TransformPoint(q)).ToArray(); var subs = new List<int[]>(); var band = new SortedDictionary<int, List<string>>(); int drop = 0, keep = 0; var dropT = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s); var k = new List<int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                float lo = Mathf.Min(w[t[i]].y, Mathf.Min(w[t[i + 1]].y, w[t[i + 2]].y)); if (lo >= padY - ease) { k.AddRange(new[] { t[i], t[i + 1], t[i + 2] }); keep++; continue; } drop++; dropT.AddRange(new[] { t[i], t[i + 1], t[i + 2] });
                var c = (w[t[i]] + w[t[i + 1]] + w[t[i + 2]]) / 3; int b = Mathf.FloorToInt(c.x / 5) * 5; List<string> l; if (!band.TryGetValue(b, out l)) band[b] = l = new List<string>();
                if (l.Count < 400) { var h = Physics.RaycastAll(new Vector3(c.x, 600, c.z), Vector3.down, 1200).Where(r => r.collider.transform.IsChildOf(opcdT) && !r.collider.name.ToLower().Contains("waterplane")).OrderBy(r => r.distance).FirstOrDefault(); l.Add(c.y.ToString("F1") + ":" + (h.collider ? h.collider.name + "@" + h.point.y.ToString("F1") : "-")); }
            }
            subs.Add(k.ToArray());
        }
        var o = band.Select(kv => "x " + kv.Key + "-" + (kv.Key + 5) + ": " + kv.Value.Count + " tris, y " + kv.Value.Select(v => float.Parse(v.Split(':')[0])).Min().ToString("F1") + "-" + kv.Value.Select(v => float.Parse(v.Split(':')[0])).Max().ToString("F1") + ", under " + string.Join(", ", kv.Value.Select(v => v.Split(':')[1].Split('@')[0]).GroupBy(v => v).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key + " x" + g.Count()).ToArray())).ToList();
        string res = meshName + ": keep " + keep + ", drop " + drop + " (below " + (padY - ease).ToString("F2") + ")\n" + string.Join("\n", o.ToArray());
        if (csvPath.Length > 0 && dropT.Count > 0)   /* the dropped footprint as a BuildPatchCsv triangulation (boundary = edges used once) */
        { var ec = new Dictionary<long, int>(); for (int i = 0; i < dropT.Count; i += 3) for (int e = 0; e < 3; e++) { int p = dropT[i + e], q = dropT[i + (e + 1) % 3]; long kk = (long)Mathf.Min(p, q) * 10000000 + Mathf.Max(p, q); int c; ec.TryGetValue(kk, out c); ec[kk] = c + 1; }
          var bd = new HashSet<int>(); foreach (var kv in ec) if (kv.Value == 1) { bd.Add((int)(kv.Key / 10000000)); bd.Add((int)(kv.Key % 10000000)); } var ids = dropT.Distinct().ToList(); var map = ids.Select((v, j) => new { v, j }).ToDictionary(p => p.v, p => p.j); var ci2 = System.Globalization.CultureInfo.InvariantCulture;
          var lines = new List<string> { "v," + ids.Count }; lines.AddRange(ids.Select(v => w[v].x.ToString("F3", ci2) + "," + w[v].z.ToString("F3", ci2) + "," + (bd.Contains(v) ? 1 : 0) + "," + (w[v].y + 0.02f).ToString("F3", ci2)));   /* grass at the old sheet height: a bank from the pond rim down to the gully */ lines.Add("t," + dropT.Count / 3); for (int i = 0; i < dropT.Count; i += 3) lines.Add(map[dropT[i]] + "," + map[dropT[i + 1]] + "," + map[dropT[i + 2]]); System.IO.File.WriteAllLines(csvPath, lines.ToArray()); res += "\npatch csv " + csvPath; } if (!apply || drop == 0) return res;
        var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "") + "_ws"; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s); m.RecalculateBounds();
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/TierraRejada/generated/wateron")) UnityEditor.AssetDatabase.CreateFolder("Assets/TierraRejada/generated", "wateron"); var ap = "Assets/TierraRejada/generated/wateron/" + mf.name + "_" + tag + ".asset"; if (UnityEditor.AssetDatabase.GetAssetPath(src) == ap) return "source is " + ap; UnityEditor.AssetDatabase.DeleteAsset(ap); UnityEditor.AssetDatabase.CreateAsset(m, ap);
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("waterstep|" + mf.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(src)).transform.SetParent(log.transform, false); var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) { mc.sharedMesh = null; mc.sharedMesh = m; } mf.sharedMesh = m;
        UnityEditor.AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return res + "\n-> applied";
    }
    public string colRoot = "OPCD Meshes";
    public string ColMismatch()
    {
        var o = new List<string>();
        foreach (var mf in GameObject.Find(colRoot).GetComponentsInChildren<MeshFilter>(false)) { var mc = mf.GetComponent<MeshCollider>(); if (colRoot != "OPCD Meshes") o.Add("  " + mf.name + " render " + (mf.sharedMesh ? mf.sharedMesh.name : "-") + " col " + (mc && mc.sharedMesh ? mc.sharedMesh.name : "-") + " rend on " + (mf.GetComponent<Renderer>() && mf.GetComponent<Renderer>().enabled)); if (!mc || !mf.sharedMesh || mc.sharedMesh == mf.sharedMesh) continue; o.Add(mf.name + ": render " + mf.sharedMesh.name + " (" + mf.sharedMesh.triangles.Length / 3 + " tris) vs collider " + (mc.sharedMesh ? mc.sharedMesh.name + " (" + mc.sharedMesh.triangles.Length / 3 + ")" : "none")); }
        return o.Count + "\n" + string.Join("\n", o.ToArray());
    }
    public string AllHits()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var s in pts.Split(';').Where(q => q.Contains(","))) { var a = s.Split(','); float x = float.Parse(a[0], ci), z = float.Parse(a[1], ci); o.Add(s + ": " + string.Join(" | ", Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200).OrderBy(h => h.distance).Take(6).Select(h => { var r = h.collider.GetComponent<Renderer>(); return h.collider.transform.root.name + "/" + h.collider.name + " y" + h.point.y.ToString("F2") + (r ? " [" + (r.sharedMaterial ? r.sharedMaterial.name : "-") + (r.enabled ? "" : " off") + "]" : " [nr]"); }).ToArray())); }
        return string.Join("\n", o.ToArray());
    }
    // census of top OPCD/overlay surfaces on a `step` grid in the box: collider name + material, with cell counts and centroid
    public float step = 3f;
    public string Census()
    {
        Physics.SyncTransforms(); var d = new Dictionary<string, List<Vector2>>();
        for (float x = x0; x <= x1; x += step) for (float z = z0; z <= z1; z += step)
        {
            RaycastHit best = new RaycastHit(); bool any = false; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.name.StartsWith("collider_")) continue; if (!any || h.point.y > best.point.y) { best = h; any = true; } }
            if (!any) continue; var r = best.collider.GetComponent<Renderer>(); string path = best.collider.transform.root.name + "/" + best.collider.name + " [" + (r && r.sharedMaterial ? r.sharedMaterial.name : "-") + "]"; List<Vector2> l; if (!d.TryGetValue(path, out l)) d[path] = l = new List<Vector2>(); l.Add(new Vector2(x, z));
        }
        return string.Join("\n", d.OrderByDescending(k => k.Value.Count).Select(k => k.Value.Count + "  " + k.Key + "  c(" + k.Value.Average(v => v.x).ToString("F0") + "," + k.Value.Average(v => v.y).ToString("F0") + ") x " + k.Value.Min(v => v.x).ToString("F0") + "-" + k.Value.Max(v => v.x).ToString("F0") + " z " + k.Value.Min(v => v.y).ToString("F0") + "-" + k.Value.Max(v => v.y).ToString("F0")).ToArray());
    }
    // 3/4 bare hillside detail: scatter `count` native shrubs (copies of the course's own sage / coyote brush / toyon / laurel sumac LOD
    // prefabs, 0.7-1.3x, clumped) over the box where the top surface is rough on `onMesh` meshes, never on mown turf, tees, paths or roads,
    // 2 m apart. Under HILLDETAIL_1101 (switch off as one).
    public int count = 350; public string onMesh = "Rough_Outer";
    public string Scatter()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var srcs = new List<GameObject>();
        foreach (var n in new[] { "TR_Sage", "TR_CoyoteBrush", "TR_Toyon", "TR_LaurelSumac", "TR_Sage" }) { var lg = Object.FindObjectsOfType<LODGroup>().FirstOrDefault(l => l.name == n && l.gameObject.activeInHierarchy); if (lg) srcs.Add((GameObject)UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(lg.gameObject) ?? lg.gameObject); }
        if (srcs.Count == 0) return "no sources"; var parent = GameObject.Find("HILLDETAIL_1101") ?? new GameObject("HILLDETAIL_1101"); var rnd = new System.Random(3401); var placed = new List<Vector2>(); int tries = 0;
        var centres = Enumerable.Range(0, Mathf.Max(6, count / 12)).Select(i => new Vector2(x0 + (float)rnd.NextDouble() * (x1 - x0), z0 + (float)rnd.NextDouble() * (z1 - z0))).ToArray();
        while (placed.Count < count && tries++ < count * 40)
        {
            var c = centres[rnd.Next(centres.Length)]; var q = c + new Vector2((float)(rnd.NextDouble() * 2 - 1), (float)(rnd.NextDouble() * 2 - 1)) * 14f; if (q.x < x0 || q.x > x1 || q.y < z0 || q.y > z1) continue; if (placed.Any(p => (p - q).sqrMagnitude < 4f)) continue;
            string nm = ""; float y = float.MinValue; bool bad = false; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD") || rn == "FENCES" || rn == "NETS") bad = true; if (h.collider.transform.root != opcd) continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } }
            if (bad || !nm.Contains(onMesh) || TierraMownCut.Mown(nm) || nm.ToLower().Contains("tee")) continue;
            var s = srcs[rnd.Next(srcs.Count)]; var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(s) ?? Object.Instantiate(s); go.transform.SetParent(parent.transform, true); go.transform.position = new Vector3(q.x, y - 0.05f, q.y); go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0); float sc = 0.7f + 0.6f * (float)rnd.NextDouble(); go.transform.localScale = Vector3.one * sc; placed.Add(q);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(parent.scene); return placed.Count + " shrubs under HILLDETAIL_1101 from " + srcs.Count + " sources (" + string.Join(", ", srcs.Select(g => g.name).Distinct().ToArray()) + ")";
    }
    // set float properties on matPath: "name=value;name=value" (old values returned)
    public string props = "";
    public string SetFloats()
    {
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); if (!m) return "no mat"; var ci = System.Globalization.CultureInfo.InvariantCulture; var o = new List<string>();
        foreach (var kv in props.Split(';').Where(s => s.Contains("="))) { var a = kv.Split('='); if (!m.HasProperty(a[0])) { o.Add(a[0] + " missing"); continue; } o.Add(a[0] + " " + m.GetFloat(a[0]) + " -> " + a[1]); m.SetFloat(a[0], float.Parse(a[1], ci)); }
        UnityEditor.EditorUtility.SetDirty(m); UnityEditor.AssetDatabase.SaveAssets(); return m.name + ": " + string.Join(", ", o.ToArray());
    }
    // swap: every active LODGroup named `fromName` under `roots` is switched off and a `prefabPath` instance put at its position (random yaw,
    // height-matched scale, clamped 0.6-1.4). Recorded under SWAP_<tag>.
    public string fromName = "Conifer_Desktop", prefabPath = "Assets/TierraRejada/Vegetation/Prefabs/Trees/TR_Pine_big.prefab";
    public string Swap()
    {
        var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); if (!pf) return "no prefab"; var pr = pf.GetComponentsInChildren<Renderer>(); var ph = pr[0].bounds; foreach (var r in pr) ph.Encapsulate(r.bounds); float pH = ph.size.y;
        var parent = new GameObject("SWAP_" + tag); var o = new List<string>(); var rnd = new System.Random(77);
        foreach (var rn in roots.Split(',')) { var r0 = GameObject.Find(rn); if (!r0) continue; foreach (var lg in r0.GetComponentsInChildren<LODGroup>().Where(l => l.name == fromName).ToList()) {
            var rs = lg.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pf); go.transform.SetParent(parent.transform, true);
            go.transform.position = lg.transform.position; go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0); float s = Mathf.Clamp(b.size.y / pH, 0.6f, 1.4f); go.transform.localScale = Vector3.one * s; lg.gameObject.SetActive(false); o.Add(rn + " (" + lg.transform.position.x.ToString("F0") + "," + lg.transform.position.z.ToString("F0") + ") h " + b.size.y.ToString("F1") + " -> x" + s.ToString("F2")); } }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(parent.scene); return o.Count + " swapped: " + string.Join("; ", o.ToArray());
    }
    // Pond edge (cloud edge kit): along the water plane `meshName`'s longest boundary loop: a TR_Edge_DirtBank_4 mud strip every 4 m and a
    // boulder group (A/B/C rotating) every ~12 m, X along the shore, +Z inland, Y = water height. Skips any spot whose inland 1.2 m point is
    // mown turf, tee, green, path or another water. Colliders from the _COL files. Under EDGE_<tag>.
    public string Shore()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var pm = opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == meshName); if (!pm) return "no plane";
        var m = pm.sharedMesh; var v = m.vertices.Select(q => pm.transform.TransformPoint(q)).ToArray(); var t = m.triangles; float wy = v.Average(q => q.y);
        var wk = new Dictionary<Vector2Int, int>(); var weld = new int[v.Length]; for (int i = 0; i < v.Length; i++) { var k = new Vector2Int(Mathf.RoundToInt(v[i].x * 100), Mathf.RoundToInt(v[i].z * 100)); int w; if (!wk.TryGetValue(k, out w)) { w = i; wk[k] = i; } weld[i] = w; }
        var cnt = new Dictionary<long, int>(); var dirE = new Dictionary<long, int[]>(); System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = weld[t[i + e]], b = weld[t[i + (e + 1) % 3]]; var k = K(a, b); int c; cnt[k] = cnt.TryGetValue(k, out c) ? c + 1 : 1; dirE[k] = new[] { a, b }; }
        var next = new Dictionary<int, int>(); foreach (var kv in cnt) if (kv.Value == 1) next[dirE[kv.Key][0]] = dirE[kv.Key][1]; var bestL = new List<int>(); var used = new HashSet<int>();
        foreach (var s in next.Keys.ToList()) { if (used.Contains(s)) continue; var L = new List<int>(); int cur = s; while (!used.Contains(cur) && next.ContainsKey(cur)) { used.Add(cur); L.Add(cur); cur = next[cur]; } if (L.Count > bestL.Count) bestL = L; }
        var p = bestL.Select(i => new Vector2(v[i].x, v[i].z)).ToList(); if (p.Count < 8) return "no loop"; float area = 0; for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; area += a.x * b.y - b.x * a.y; } float sg = area > 0 ? 1 : -1;
        var pts = new List<Vector2> { p[0] }; float acc = 0; for (int i = 1; i <= p.Count; i++) { var a = p[i - 1]; var b = p[i % p.Count]; float seg = (b - a).magnitude; while (acc + seg >= 4f) { float f = (4f - acc) / seg; var q = a + (b - a) * f; pts.Add(q); a = q; seg = (b - a).magnitude; acc = 0; } acc += seg; }
        System.Func<Vector2, bool> Bad = q => { string nm = ""; float y = float.MinValue; bool path = false; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { var rn = h.collider.transform.root.name; if (rn == "CART_PATHS" || rn.StartsWith("ROAD")) path = true; if (h.collider.transform.root != opcd || h.collider.name.Contains("Water")) continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } } var l = nm.ToLower(); return path || TierraMownCut.Mown(nm) || l.Contains("tee") || l.Contains("green") || l.Contains("bunker"); };
        System.Func<string, GameObject> L2 = n => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/edge_kit/" + n + ".fbx");
        var old = GameObject.Find("EDGE_" + tag); if (old) Object.DestroyImmediate(old); var parent = new GameObject("EDGE_" + tag); int mud = 0, bould = 0, skip = 0; var groups = new[] { "TR_Edge_Boulders_A", "TR_Edge_Boulders_B", "TR_Edge_Boulders_C" }; var rnd = new System.Random(1318);
        System.Action<string, Vector3, float> Put = (n, pos, yw) => { var f = L2(n); if (!f) return; var g = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(f); g.transform.SetParent(parent.transform, false); g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0, yw, 0); var cf = L2(n + "_COL"); if (cf) { var cg = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(cf); cg.transform.SetParent(g.transform, false); foreach (var mf in cg.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<MeshCollider>()) { var c = mf.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = mf.sharedMesh; c.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/GSPstone"); } foreach (var mr in cg.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(mr); } };
        int sinceB = 2 + rnd.Next(2);
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count]; var tg = (b - a).normalized; var inland = new Vector2(tg.y, -tg.x) * sg; var mid = (a + b) / 2; if (Bad(mid + inland * 1.2f)) { skip++; continue; }
            float yw = Mathf.Atan2(tg.x, tg.y) * Mathf.Rad2Deg - 90f; // local X along tg
            var zf = Quaternion.Euler(0, yw, 0) * Vector3.forward; if (Vector2.Dot(new Vector2(zf.x, zf.z), inland) < 0) yw += 180f;
            Put("TR_Edge_DirtBank_4", new Vector3(mid.x, wy, mid.y), yw); mud++;
            if (--sinceB <= 0) { Put(groups[rnd.Next(groups.Length)], new Vector3(mid.x + tg.x * (float)(rnd.NextDouble() - 0.5), wy, mid.y + tg.y * (float)(rnd.NextDouble() - 0.5)), yw + (float)(rnd.NextDouble() * 40 - 20)); bould++; sinceB = 2 + rnd.Next(2); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(parent.scene); return meshName + ": water y " + wy.ToString("F2") + ", shore " + pts.Count * 4 + " m, " + mud + " mud strips, " + bould + " boulder groups, " + skip + " spots skipped (turf/path)";
    }
    // place a cloud prop: fbx list `ids` ('|', first = model; others same transform, *_COL become colliders) at (px,py,pz) yaw; switch off `fromName` root
    public float px, py, pz, yaw2; public string propName = "PROP";
    public string PlaceProp()
    {
        var old = GameObject.Find(propName); if (old) Object.DestroyImmediate(old); var root = new GameObject(propName); root.transform.position = new Vector3(px, py, pz); root.transform.rotation = Quaternion.Euler(0, yaw2, 0); var o = new List<string>();
        foreach (var p in ids.Split('|').Where(s => s.Length > 0))
        {
            var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p); if (!fbx) { o.Add(p + " missing"); continue; } var g = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx); g.transform.SetParent(root.transform, false);
            if (p.Contains("_COL")) { foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<MeshCollider>()) { var c = mf.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = mf.sharedMesh; c.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + (mf.name.Contains("GSPtimber") ? "GSPtimber" : "GSPstone")); } foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(mr); }
            o.Add(g.name);
        }
        if (fromName.Length > 0) { var f = GameObject.Find(fromName); if (f) { f.SetActive(false); o.Add(fromName + " off"); } }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene); return propName + ": " + string.Join(", ", o.ToArray());
    }
    public string PropUndo() { var g = GameObject.Find(propName); if (g) Object.DestroyImmediate(g); foreach (var t in Resources.FindObjectsOfTypeAll<Transform>().Where(x => x.gameObject.scene.IsValid() && x.name == fromName && x.parent == null)) t.gameObject.SetActive(true); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); return "undone"; }
    // 5 tee steps: from the raised box `meshName`, nearest cart-path point within 25 m; steps run from the path edge up to the box edge.
    // Variant = box top minus path (0.6-1.8 m). Top tread stops AT the box edge. Terrain lowered 0.25 m under the steps only where the
    // top surface is not tee turf or path.
    public string style = "Timber";
    public string TeeSteps()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var pad = opcd.GetComponentsInChildren<MeshFilter>(false).FirstOrDefault(f => f.name == meshName); if (!pad) return "no pad";
        var pb = pad.GetComponent<Renderer>().bounds; float H = pb.max.y; var pc = new Vector2(pb.center.x, pb.center.z);
        Vector2 best = Vector2.zero; float bd = 1e9f, py = 0; for (float x = pb.min.x - 25; x <= pb.max.x + 25; x += 0.5f) for (float z = pb.min.z - 25; z <= pb.max.z + 25; z += 0.5f) { foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) if (h.collider.transform.root.name == "CART_PATHS") { float d = (new Vector2(x, z) - pc).sqrMagnitude; if (d < bd) { bd = d; best = new Vector2(x, z); py = h.point.y; } break; } }
        if (bd > 1e8f) return "no path near";
        var dir = (pc - best).normalized; Vector2 edge = best; for (float s = 0; s < 40; s += 0.1f) { var q = best + dir * s; bool tee = Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200).Any(h => h.collider.transform.root == opcd && h.collider.name.ToLower().Contains("tee") && !h.collider.name.Contains("Blend")); if (tee) { edge = q; break; } }
        Vector2 foot = best; for (float s = 0; s < 10; s += 0.1f) { var q = best + dir * s; if (!Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200).Any(h => h.collider.transform.root.name == "CART_PATHS")) { foot = q + dir * 0.2f; break; } }
        float rise = H - py; var hs = new[] { 0.6f, 0.9f, 1.2f, 1.5f, 1.8f }; float pick = hs.OrderBy(v => Mathf.Abs(v - rise)).First(); string nm = "TR_TeeSteps_" + style + "_" + Mathf.RoundToInt(pick * 10).ToString("00");
        var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/tee_steps/" + nm + ".fbx"); var colf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/tee_steps/" + nm + "_COL.fbx"); if (!fbx) return nm + " missing";
        var old = GameObject.Find("TEESTEPS_" + tag); if (old) Object.DestroyImmediate(old); var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx); go.name = "TEESTEPS_" + tag; float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.position = new Vector3(foot.x, H - pick, foot.y); Physics.SyncTransforms();
        var rr = go.GetComponentsInChildren<Renderer>().Where(r => !r.name.Contains("Handrail")).ToArray(); float maxS = rr.SelectMany(r => new[] { r.bounds.min, r.bounds.max, new Vector3(r.bounds.min.x, 0, r.bounds.max.z), new Vector3(r.bounds.max.x, 0, r.bounds.min.z) }).Max(v => Vector2.Dot(new Vector2(v.x, v.z) - foot, dir));
        float target = Vector2.Dot(edge - foot, dir); go.transform.position += new Vector3(dir.x, 0, dir.y) * (target - maxS);
        if (colf) { var cg = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(colf); cg.transform.SetParent(go.transform, false); foreach (var mf in cg.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<MeshCollider>()) { var c = mf.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = mf.sharedMesh; c.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + (mf.name.Contains("GSPstone") ? "GSPstone" : "GSPtimber")); } foreach (var mr in cg.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(mr); }
        Physics.SyncTransforms(); var te = Object.FindObjectsOfType<Terrain>().First(t => t.name == "Terrain (Inner)"); var td = te.terrainData; var tp = te.transform.position; int R = td.heightmapResolution; float cx = td.size.x / (R - 1), cz = td.size.z / (R - 1); var gb = go.GetComponentsInChildren<Renderer>()[0].bounds; foreach (var r in go.GetComponentsInChildren<Renderer>()) gb.Encapsulate(r.bounds);
        int i0 = Mathf.FloorToInt((gb.min.x - tp.x) / cx), i1 = Mathf.CeilToInt((gb.max.x - tp.x) / cx), j0 = Mathf.FloorToInt((gb.min.z - tp.z) / cz), j1 = Mathf.CeilToInt((gb.max.z - tp.z) / cz); var Hh = td.GetHeights(i0, j0, i1 - i0 + 1, j1 - j0 + 1); int k = 0;
        for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) { float x = tp.x + i * cx, z = tp.z + j * cz; string top = ""; float ty = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider is TerrainCollider || h.collider.transform.IsChildOf(go.transform)) continue; if (h.point.y > ty) { ty = h.point.y; top = h.collider.transform.root.name + "/" + h.collider.name; } } if (top.ToLower().Contains("tee") || top.StartsWith("CART_PATHS")) continue; RaycastHit sh; foreach (var c in go.GetComponentsInChildren<MeshCollider>()) if (c.Raycast(new Ray(new Vector3(x, 600, z), Vector3.down), out sh, 1200)) { float tgt = (sh.point.y - 0.25f - tp.y) / td.size.y; if (Hh[j - j0, i - i0] > tgt) { Hh[j - j0, i - i0] = tgt; k++; } break; } }
        td.SetHeights(i0, j0, Hh); UnityEditor.EditorUtility.SetDirty(td); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return nm + " (box top " + H.ToString("F2") + ", path " + py.ToString("F2") + ", rise " + rise.ToString("F2") + ") from path (" + best.x.ToString("F1") + "," + best.y.ToString("F1") + ") to box edge (" + edge.x.ToString("F1") + "," + edge.y.ToString("F1") + "), yaw " + yaw.ToString("F0") + ", terrain cells lowered " + k;
    }
    public float glossScale = 0.3f;
    public string GlossScale() { var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); if (!m) return "no mat"; var old = m.GetFloat("_GlossMapScale"); m.SetFloat("_GlossMapScale", glossScale); UnityEditor.EditorUtility.SetDirty(m); UnityEditor.AssetDatabase.SaveAssets(); return m.name + " gloss scale " + old + " -> " + glossScale; }
    public float dy = 0.08f;
    public float rdx = 0, rdz = 0;   // Raise also shifts sideways by (rdx, rdz)
    public string Raise()
    {
        var o = new List<string>(); var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var p in ids.Split('|').Where(s => s.Length > 0)) { var parts = p.Split('/'); var r0 = GameObject.Find(parts[0]); var t = r0 ? (parts.Length > 1 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r0.transform) : null; if (!t) { o.Add(p + " not found"); continue; } new GameObject("raise|" + p + "|" + t.position.ToString("F3")).transform.SetParent(log.transform, false); t.position += new Vector3(rdx, dy, rdz); o.Add(p + " +(" + rdx + ", " + dy + ", " + rdz + ")"); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return string.Join("; ", o.ToArray());
    }
    // 13b: cloud bridge kit (Props/bridge13) at the existing PlankBridge deck (centre, deck yaw); old PlankBridge switched off. Landings and
    // rocks dropped onto the ground under each piece (rocks 8% buried, landings 1.2 cm proud). Undo = Bridge13Undo.
    public string Bridge13()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var pb = GameObject.Find("CREEK13_1029").transform.Find("PlankBridge"); var deck = pb.Find("bridge_deck"); var db = deck.GetComponent<Renderer>().bounds;
        var dm = deck.GetComponent<MeshFilter>().sharedMesh; var w = dm.vertices.Select(v => deck.TransformPoint(v)).Select(v => new Vector2(v.x, v.z)).ToList(); var c = w.Aggregate(Vector2.zero, (s, v) => s + v) / w.Count; float sxx = 0, szz = 0, sxz = 0; foreach (var v in w) { var d = v - c; sxx += d.x * d.x; szz += d.y * d.y; sxz += d.x * d.y; } float an = 0.5f * Mathf.Atan2(2 * sxz, sxx - szz); var ax = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
        var green = new Vector2(828, 894); if (Vector2.Dot(ax, green - c) < 0) ax = -ax; float yaw = Mathf.Atan2(ax.x, ax.y) * Mathf.Rad2Deg;
        var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/bridge13/TR_Bridge13.fbx"); var colf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TierraRejada/Props/bridge13/TR_Bridge13_COL.fbx"); if (!fbx) return "fbx missing";
        var old = GameObject.Find("BRIDGE13_1101"); if (old) Object.DestroyImmediate(old); var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx); go.name = "BRIDGE13_1101";
        go.transform.position = new Vector3(c.x, db.max.y - 0.05f, c.y); go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        if (colf) { var cg = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(colf); cg.transform.SetParent(go.transform, false); foreach (var mr in cg.GetComponentsInChildren<MeshRenderer>()) mr.enabled = false; foreach (var mf in cg.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<MeshCollider>()) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + (mf.name.Contains("GSPtimber") ? "GSPtimber" : "GSPstone")); } }
        Physics.SyncTransforms(); var o = new List<string>();
        foreach (Transform t in go.transform) { if (!t.GetComponent<Renderer>() || t.name.Contains("Deck") || t.name.Contains("Steps")) continue; var b = t.GetComponent<Renderer>().bounds; float gy = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, 400, b.center.z), Vector3.down, 800)) { if (h.collider.transform.IsChildOf(go.transform)) continue; if (h.collider.transform.root == opcd || h.collider is TerrainCollider) gy = Mathf.Max(gy, h.point.y); } if (gy == float.MinValue) continue; float target = t.name.Contains("Landing") ? gy + 0.012f : gy - 0.08f * b.size.y; t.position += new Vector3(0, target - b.min.y, 0); o.Add(t.name); }
        pb.gameObject.SetActive(false); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return "BRIDGE13_1101 at (" + c.x.ToString("F1") + "," + (db.max.y - 0.05f).ToString("F2") + "," + c.y.ToString("F1") + ") yaw " + yaw.ToString("F0") + "; seated " + string.Join(", ", o.ToArray()) + "; old PlankBridge off";
    }
    public string Bridge13Undo() { var go = GameObject.Find("BRIDGE13_1101"); if (go) Object.DestroyImmediate(go); var pb = GameObject.Find("CREEK13_1029").transform.Find("PlankBridge"); pb.gameObject.SetActive(true); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pb.gameObject.scene); return "undone"; }
    public string ClubhouseUndo()
    {
        var go = GameObject.Find("CLUBHOUSE_1101"); if (go) Object.DestroyImmediate(go); var ch = GameObject.Find("CLUBHOUSE").transform; var o = new List<string>();
        foreach (var n in new[] { "Walls", "Roof_Well", "Timber", "Frames", "Chimney", "Chimney_Cap", "Porte_Cochere_Pillars" }) { var t = ch.Find(n); if (t && !t.gameObject.activeSelf) { t.gameObject.SetActive(true); o.Add(n); } } var rnc = GameObject.Find("H18I7") ? GameObject.Find("H18I7").transform.Find("RoofNoCupola") : null; if (rnc) { rnc.gameObject.SetActive(true); o.Add("H18I7/RoofNoCupola"); }
        var log = GameObject.Find("NIGHT1031_LOG"); if (log) foreach (Transform t in log.transform.Cast<Transform>().ToList()) if (t.name.StartsWith("off|clubhouse|")) Object.DestroyImmediate(t.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ch.gameObject.scene); return "new shell removed; old shell back on: " + string.Join(", ", o.ToArray());
    }
    // set `_Color` (r,g,b) / `_Glossiness` / `_Metallic` on the material at matPath (Standard shader); old values returned
    public string rgb = "0.16,0.27,0.30"; public float gloss = 0.92f, metal = 0.08f;
    public string colorProp = "";
    // colorProp set: only that colour property changes (alpha kept), nothing else
    public string SetColor()
    {
        if (colorProp.Length > 0) { var mm = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); if (!mm) return matPath + " not found"; var cc = rgb.Split(',').Select(q => float.Parse(q, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); var o0 = mm.GetColor(colorProp); mm.SetColor(colorProp, new Color(cc[0], cc[1], cc[2], o0.a)); UnityEditor.EditorUtility.SetDirty(mm); UnityEditor.AssetDatabase.SaveAssets(); return mm.name + " " + colorProp + ": " + o0 + " -> " + mm.GetColor(colorProp); }
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); if (!m) return matPath + " not found"; var ci = System.Globalization.CultureInfo.InvariantCulture; var c = rgb.Split(',').Select(s => float.Parse(s, ci)).ToArray();
        var old = m.GetColor("_Color") + " gloss " + m.GetFloat("_Glossiness") + " metal " + m.GetFloat("_Metallic"); m.SetColor("_Color", new Color(c[0], c[1], c[2], 1)); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal);
        UnityEditor.EditorUtility.SetDirty(m); UnityEditor.AssetDatabase.SaveAssets(); return m.name + ": " + old + " -> " + m.GetColor("_Color") + " gloss " + gloss + " metal " + metal;
    }
    // top collider of ANY root at each "x,z;..." point (root/name, y, material)
    public string SurfAll()
    {
        Physics.SyncTransforms(); var o = new List<string>(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var s in pts.Split(';').Where(q => q.Contains(",")))
        {
            var a = s.Split(','); float x = float.Parse(a[0], ci), z = float.Parse(a[1], ci); RaycastHit best = new RaycastHit(); bool any = false;
            foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (!any || h.point.y > best.point.y) { best = h; any = true; } }
            if (!any) { o.Add(s + " nothing"); continue; } var r = best.collider.GetComponent<Renderer>();
            o.Add(s + " " + best.collider.transform.root.name + "/" + best.collider.name + " y " + best.point.y.ToString("F2") + " mat " + (r && r.sharedMaterial ? r.sharedMaterial.name : "-"));
        }
        return string.Join("\n", o.ToArray());
    }
    // 13p: give the renderers at `ids` (root/child paths, '|'-separated) the material at `matPath`; recorded with the old material
    public string SetMatPaths()
    {
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); if (!m) return matPath + " not found"; var o = new List<string>();
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG");
        foreach (var p in ids.Split('|').Where(s => s.Length > 0))
        {
            var parts = p.Split('/'); var r0 = GameObject.Find(parts[0]); var t = r0 ? (parts.Length > 1 ? r0.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r0.transform) : null; if (!t) { o.Add(p + " not found"); continue; }
            var r = t.GetComponent<Renderer>(); if (!r) { o.Add(p + " has no renderer"); continue; }
            new GameObject("mat|" + p + "|" + UnityEditor.AssetDatabase.GetAssetPath(r.sharedMaterial)).transform.SetParent(log.transform, false); o.Add(p + ": " + r.sharedMaterial.name + " -> " + m.name); r.sharedMaterial = m;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return string.Join("; ", o.ToArray());
    }
    // 10r: give the OPCD mesh `meshName` the material at `matPath` (recorded with the old material's path)
    public string meshName = "Spline_rough_0_4_Rough_h10_-_Mesh", matPath = "Assets/Resources/Materials/Theme - Satellite/OPCD MAHS Rough No Mowlines_Sat.mat";
    public string SetMat()
    {
        var r = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(q => q.name == meshName); if (!r) return meshName + " not found";
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath); if (!m) return matPath + " not found";
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); new GameObject("mat|" + meshName + "|" + UnityEditor.AssetDatabase.GetAssetPath(r.sharedMaterial)).transform.SetParent(log.transform, false);
        var old = r.sharedMaterial.name; r.sharedMaterial = m; UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(r.gameObject.scene); return meshName + ": " + old + " -> " + m.name;
    }
    // top OPCD surface (collider name, y) at each "x,z;x,z;..." point
    public string pts = "";
    public string Surf()
    {
        Physics.SyncTransforms(); var opcd = GameObject.Find("OPCD Meshes").transform; var o = new List<string>(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var s in pts.Split(';').Where(q => q.Contains(",")))
        {
            var a = s.Split(','); float x = float.Parse(a[0], ci), z = float.Parse(a[1], ci); string nm = "-"; float y = float.MinValue;
            foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200)) { if (h.collider.transform.root != opcd) continue; if (h.point.y > y) { y = h.point.y; nm = h.collider.name; } }
            o.Add(x.ToString("F1", ci) + "," + z.ToString("F1", ci) + "," + y.ToString("F3", ci) + "," + nm);
        }
        return string.Join("\n", o.ToArray());
    }
    // 12a (hole 11 greenside): shrub-size plants (LODGroups under 5 m) within `pad` m of the listed bunkers' sand switched off. Recorded.
    public string bunkersCsv = ""; public float pad = 4;
    public string ClearBunkerBushes()
    {
        Physics.SyncTransforms(); var sands = GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(false).Where(f => bunkersCsv.Split(',').Contains(f.name)).ToList();
        var pts = sands.SelectMany(f => f.sharedMesh.vertices.Where((v, i) => i % 3 == 0).Select(v => f.transform.TransformPoint(v))).Select(v => new Vector2(v.x, v.z)).ToList();
        var o = new List<string>(); var log = apply ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!root.activeInHierarchy || root.name == "OPCD Meshes") continue;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var p = new Vector2(lg.transform.position.x, lg.transform.position.z); if (!pts.Any(q => (q - p).sqrMagnitude < pad * pad)) continue;
                var rs = lg.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); if (b.size.y > 5f) continue;
                string path = lg.name; for (var u = lg.transform.parent; u; u = u.parent) path = u.name + "/" + path; o.Add(path + " (" + p.x.ToString("F1") + "," + p.y.ToString("F1") + ") h " + b.size.y.ToString("F1"));
                if (apply) { lg.gameObject.SetActive(false); new GameObject("off|11bush|" + path + "|" + p.x.ToString("F2") + "|" + p.y.ToString("F2")).transform.SetParent(log.transform, false); }
            }
        }
        if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene);
        return sands.Count + " bunkers, " + o.Count + " plants" + (apply ? " switched off" : " (dry run)") + "\n" + string.Join("\n", o.ToArray());
    }
    static string EvenApply() { TierraBunkerRebuild.EvenRim = true; try { return TierraBunkerRebuild.Apply("12left"); } finally { TierraBunkerRebuild.EvenRim = false; } }
    public string Under12() { return TierraTerrainUnderSand.Run(new[] { "Spline_bunker_12left_Bunker_-_Mesh" }, null); }
    // discard unsaved scene edits: reopen the saved scene file (no save prompt from code)
    public string Reload() { UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/TierraRejada.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); return "reloaded from disk"; }
    public string ids = ""; public string tag = "night1031";
    // switch off listed paths (root/.../name), recorded under NIGHT1031_LOG; nothing deleted
    public string Off()
    {
        var log = GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG"); var o = new List<string>();
        foreach (var p in ids.Split('|').Where(s => s.Length > 0))
        {
            var parts = p.Split('/'); var r = GameObject.Find(parts[0]); if (!r) { o.Add(p + " root missing"); continue; }
            var t = parts.Length > 1 ? r.transform.Find(string.Join("/", parts.Skip(1).ToArray())) : r.transform; if (!t) { o.Add(p + " not found"); continue; }
            t.gameObject.SetActive(false); new GameObject("off|" + tag + "|" + p).transform.SetParent(log.transform, false); o.Add(p + " off");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(log.scene); return string.Join("; ", o.ToArray());
    }
    // plants tilted > maxTilt deg or with uneven scale (max/min axis > maxRatio), course-wide, LODGroups only
    public float maxTilt = 5, maxRatio = 1.15f; public bool fix = false;
    public string Tilt()
    {
        var o = new List<string>(); int n = 0, fixedN = 0; var log = fix ? (GameObject.Find("NIGHT1031_LOG") ?? new GameObject("NIGHT1031_LOG")) : null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!root.activeInHierarchy || root.name == "OPCD Meshes") continue;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var t = lg.transform; n++; float tilt = Vector3.Angle(t.up, Vector3.up); var s = t.lossyScale; float mx = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)), mn = Mathf.Min(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                float ratio = mn > 0 ? mx / mn : 99; if (tilt <= maxTilt && ratio <= maxRatio) continue;
                string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
                o.Add(path + " at (" + t.position.x.ToString("F1") + "," + t.position.z.ToString("F1") + ") tilt " + tilt.ToString("F0") + " scale " + s.ToString("F2"));
                if (fix)
                {
                    new GameObject("tilt|" + path + "|" + t.localRotation.eulerAngles.ToString("F2") + "|" + t.localScale.ToString("F3")).transform.SetParent(log.transform, false);
                    t.rotation = Quaternion.Euler(0, t.rotation.eulerAngles.y, 0); float u = Mathf.Abs(t.localScale.y); if (ratio > maxRatio) { float m = (Mathf.Abs(t.localScale.x) + Mathf.Abs(t.localScale.z)) * 0.5f; t.localScale = new Vector3(m, m, m); } fixedN++;
                }
            }
        }
        if (fix) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return n + " plants scanned, " + o.Count + " tilted/uneven" + (fix ? ", " + fixedN + " stood upright / evened" : "") + "\n" + string.Join("\n", o.Take(80).ToArray());
    }
}
