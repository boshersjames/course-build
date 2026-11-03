using System.Collections.Generic; using System.IO; using System.Linq; using UnityEditor; using UnityEngine;
// Material-copy packages (editor B): copies of a shared material, tuned, assigned only to the named objects; shared materials are never
// edited. Spec AutomationOutput/bpass/<pkg>_mat.json: { "copies": [ { "name", "src" (object whose material is copied), "targets" [objects],
// "channels" [Red|Green|Blue|Black], "overallScale" [r,g,b], "detailOpacityScale", "detailTint" [r,g,b], "normalScale", "surfaceOpacityScale",
// "baseTintScale" [r,g,b] } ] }. Manifest: "material" ops for TierraGeneratedApply (checks the material it expects).
public static class TierraMatPkg
{
    static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;
    static float F(object o) { return System.Convert.ToSingle(o, CI); }
    static float[] V(Dictionary<string, object> d, string k) { return d.ContainsKey(k) ? ((List<object>)d[k]).Select(F).ToArray() : null; }
    public static string Build(string pkg)
    {
        TierraBTools.Reload(); var J = (Dictionary<string, object>)TierraMiniJson.Parse(File.ReadAllText("AutomationOutput/bpass/" + pkg + "_mat.json"));
        var dir = "Assets/Generated/" + pkg; if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Generated", pkg);
        var root = GameObject.Find("OPCD Meshes").transform; var all = root.GetComponentsInChildren<Renderer>(true);
        System.Func<string, Renderer> R = n => { var r = all.FirstOrDefault(x => x.name == n); if (!r) throw new System.Exception("no object " + n); return r; };
        System.Func<Transform, string> Path = t => { string p = t.name; for (var q = t.parent; q && q != root; q = q.parent) p = q.name + "/" + p; return p; };
        var ops = new List<string>(); var log = new List<string>(); var splitOrig = new Dictionary<string, string>(); var splitLast = new Dictionary<string, string>();
        foreach (Dictionary<string, object> c in (List<object>)J["copies"])
        {
            Renderer splitR = null;
            if (c.ContainsKey("splitPoly"))
            {   /* split (11 crest): the src mesh's triangles inside splitPoly move to a new sibling object that gets the copy; the rest stay */
                var sr = R((string)c["src"]); var smf = sr.GetComponent<MeshFilter>(); var sm = smf.sharedMesh; var poly = ((List<object>)c["splitPoly"]).Select(p => { var a = (List<object>)p; return new Vector2(F(a[0]), F(a[1])); }).ToList();
                var w = sm.vertices.Select(v => smf.transform.TransformPoint(v)).ToArray(); var tris = sm.triangles; var keep = new List<int>(); var mv = new List<int>();
                for (int k = 0; k < tris.Length; k += 3) { var cc = (w[tris[k]] + w[tris[k + 1]] + w[tris[k + 2]]) / 3; (Inside(poly, new Vector2(cc.x, cc.z)) ? mv : keep).AddRange(new[] { tris[k], tris[k + 1], tris[k + 2] }); }
                var rest = Object.Instantiate(sm); rest.name = sm.name; rest.subMeshCount = 1; rest.SetTriangles(keep, 0); rest.RecalculateBounds(); var rp = dir + "/" + sm.name + "_split" + pkg + ".asset"; AssetDatabase.DeleteAsset(rp); AssetDatabase.CreateAsset(rest, rp);
                string orig; if (!splitOrig.TryGetValue(Path(sr.transform), out orig)) splitOrig[Path(sr.transform)] = orig = sm.name; splitLast[Path(sr.transform)] = rp + "|" + rest.name;   /* one mesh op per source, against its state before the package */
                var part = Object.Instantiate(sm); part.name = sr.name + "_" + (string)c["name"]; part.subMeshCount = 1; part.SetTriangles(mv, 0); part.RecalculateBounds(); var pp = dir + "/" + part.name + ".asset"; AssetDatabase.DeleteAsset(pp); AssetDatabase.CreateAsset(part, pp);
                smf.sharedMesh = rest; var smc = sr.GetComponent<MeshCollider>(); if (smc) { smc.sharedMesh = null; smc.sharedMesh = rest; }
                var go = new GameObject(part.name); go.transform.SetParent(sr.transform.parent, false); go.transform.localPosition = sr.transform.localPosition; go.transform.localRotation = sr.transform.localRotation; go.transform.localScale = sr.transform.localScale;
                go.layer = sr.gameObject.layer; go.tag = sr.gameObject.tag; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(sr.gameObject));
                go.AddComponent<MeshFilter>().sharedMesh = part; var nr = go.AddComponent<MeshRenderer>(); nr.shadowCastingMode = sr.shadowCastingMode; nr.receiveShadows = sr.receiveShadows; var nmc = go.AddComponent<MeshCollider>(); nmc.sharedMesh = part; nmc.sharedMaterial = smc ? smc.sharedMaterial : null;
                splitR = nr; nr.sharedMaterial = sr.sharedMaterial; log.Add(sr.name + ": " + mv.Count / 3 + " triangles split off");
            }
            var src = (splitR ? splitR : R((string)c["src"])).sharedMaterial; var m = new Material(src) { name = (string)c["name"] };
            var chs = c.ContainsKey("channels") ? ((List<object>)c["channels"]).Cast<string>().ToArray() : new[] { "Red", "Green", "Blue", "Black" };
            var os = V(c, "overallScale"); var dt = V(c, "detailTint"); var bt = V(c, "baseTintScale");
            foreach (var ch in chs)
            {
                if (os != null && m.HasProperty("_" + ch + "OverallColor")) { var k = m.GetColor("_" + ch + "OverallColor"); m.SetColor("_" + ch + "OverallColor", new Color(k.r * os[0], k.g * os[1], k.b * os[2], k.a)); }
                if (bt != null && m.HasProperty("_" + ch + "BaseColorTint")) { var k = m.GetColor("_" + ch + "BaseColorTint"); m.SetColor("_" + ch + "BaseColorTint", new Color(k.r * bt[0], k.g * bt[1], k.b * bt[2], k.a)); }
                if (c.ContainsKey("detailOpacityScale") && m.HasProperty("_" + ch + "DetailOpacity")) m.SetFloat("_" + ch + "DetailOpacity", m.GetFloat("_" + ch + "DetailOpacity") * F(c["detailOpacityScale"]));
                if (dt != null && m.HasProperty("_" + ch + "DetailColorTint")) { var k = m.GetColor("_" + ch + "DetailColorTint"); m.SetColor("_" + ch + "DetailColorTint", new Color(dt[0], dt[1], dt[2], k.a)); }
                if (c.ContainsKey("normalScale")) foreach (var p in new[] { "NormalStrength", "NearNRMStrength", "FarNRMStrength" }) if (m.HasProperty("_" + ch + p)) m.SetFloat("_" + ch + p, m.GetFloat("_" + ch + p) * F(c["normalScale"]));
                if (c.ContainsKey("surfaceOpacityScale") && m.HasProperty("_" + ch + "SurfaceOpacity")) m.SetFloat("_" + ch + "SurfaceOpacity", m.GetFloat("_" + ch + "SurfaceOpacity") * F(c["surfaceOpacityScale"]));
            }
            var mp = dir + "/" + m.name + ".mat"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(m, mp);
            if (splitR) { var mc2 = splitR.GetComponent<MeshCollider>(); splitR.sharedMaterial = m; var mfp = splitR.GetComponent<MeshFilter>();
                ops.Add("{\"op\":\"create\",\"path\":\"" + Path(splitR.transform) + "\",\"active\":true,\"asset\":\"" + AssetDatabase.GetAssetPath(mfp.sharedMesh) + "\",\"meshName\":\"" + mfp.sharedMesh.name + "\",\"mat\":\"" + dir + "/" + m.name + ".mat\",\"matName\":\"" + m.name + "\",\"pm\":\"" + (mc2.sharedMaterial ? AssetDatabase.GetAssetPath(mc2.sharedMaterial) : "") + "\",\"pmName\":\"" + (mc2.sharedMaterial ? mc2.sharedMaterial.name : "") + "\",\"layer\":" + splitR.gameObject.layer + ",\"tag\":\"" + splitR.gameObject.tag + "\",\"static\":" + (int)GameObjectUtility.GetStaticEditorFlags(splitR.gameObject) + ",\"shadows\":" + (int)splitR.shadowCastingMode + ",\"receive\":" + (splitR.receiveShadows ? "true" : "false") + ",\"collider\":true}"); }
            if (c.ContainsKey("targets")) foreach (string tn in (List<object>)c["targets"]) { var r = R(tn); ops.Add("{\"op\":\"material\",\"path\":\"" + Path(r.transform) + "\",\"expect\":\"" + r.sharedMaterial.name + "\",\"mat\":\"" + mp + "\",\"matName\":\"" + m.name + "\"}"); r.sharedMaterial = m; }
            log.Add(m.name + " (copy of " + src.name + ")" + (c.ContainsKey("targets") ? " on " + ((List<object>)c["targets"]).Count + " objects" : " on the split part"));
        }
        foreach (var kv in splitOrig) { var pr = splitLast[kv.Key].Split('|'); ops.Insert(0, "{\"op\":\"mesh\",\"path\":\"" + kv.Key + "\",\"expect\":\"" + kv.Value + "\",\"asset\":\"" + pr[0] + "\",\"meshName\":\"" + pr[1] + "\"}"); }
        File.WriteAllText(dir + "/manifest.json", "{\"pkg\":\"" + pkg + "\",\"built\":\"" + System.DateTime.Now.ToString("s") + "\",\"ops\":[\n" + string.Join(",\n", ops.ToArray()) + "\n]}");
        AssetDatabase.SaveAssets(); return "manifest: " + ops.Count + " material swaps; " + string.Join("; ", log.ToArray());
    }
    static bool Inside(List<Vector2> p, Vector2 q) { bool r = false; for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) if ((p[i].y > q.y) != (p[j].y > q.y) && q.x < (p[j].x - p[i].x) * (q.y - p[i].y) / (p[j].y - p[i].y) + p[i].x) r = !r; return r; }
    public static string Build_h11sand() { return Build("h11sand"); }
    public static string Build_greenswarm() { return Build("greenswarm"); }
    public static string Build_pepcol() { return TierraPepCol.Build(); }
    public static string Build_h11bv3() { return TierraH11bv3.Build(); }
    public static string Build_g9crest() { return TierraG9Crest.Build(); }
    public static string Build_h12tint() { return TierraH12Tint.Build(); }
    public static string Build_h12hill() { return Build("h12hill"); }
    public static string Build_h11edge() { return TierraH11Edge.Build(); }
    public static string Build_h14water() { return TierraH14Water.Build(); }
    public static string Build_h12sur() { return TierraH12Sur.Build(); }
    public static string Build_pathstrip() { return TierraPathStrip.Build(); }
    public static string Build_h11crest() { return Build("h11crest"); }
}
