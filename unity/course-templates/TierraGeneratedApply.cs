using System.Collections.Generic; using System.IO; using System.Linq; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;
// Replays a package built in editor B (Assets/Generated/<pkg>/manifest.json) on the open scene under "OPCD Meshes": delete, mesh swap
// (checks the mesh it expects first), material swap (checks the material it expects), on/off, create (mesh + material + physic material + layer / tag / static flags / shadows).
// Nothing is saved: editor A reviews, backs up and saves. Any expectation that doesn't hold stops the run before the scene is touched.
public static class TierraGeneratedApply
{
    public static string Run(string manifest)
    {
        var J = (Dictionary<string, object>)TierraMiniJson.Parse(File.ReadAllText(manifest)); var ops = ((List<object>)J["ops"]).Cast<Dictionary<string, object>>().ToList();
        var root = GameObject.Find("OPCD Meshes").transform; var problems = new List<string>();
        // a path starting with "/" names a scene root other than OPCD Meshes ("/CART_PATHS/CartPath_0")
        var sceneRoots = root.gameObject.scene.GetRootGameObjects(); var allR = sceneRoots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToList();
        System.Func<string, Transform> Find = p => { Transform t = root; if (p.StartsWith("/")) { var ps = p.Substring(1).Split(new[] { '/' }, 2); var rg = sceneRoots.Where(g => g.name == ps[0]).ToList(); if (rg.Count != 1) return null; t = rg[0].transform; if (ps.Length == 1) return t; p = ps[1]; } foreach (var part in p.Split('/')) { Transform nx = null; foreach (Transform c in t) if (c.name == part) { if (nx) { problems.Add("duplicate " + p); } nx = c; } if (!nx) return null; t = nx; } return t; };
        System.Func<string, string, Mesh> LoadMesh = (path, name) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == name);
        System.Func<Dictionary<string, object>, Material> Mat = o => AssetDatabase.LoadAllAssetsAtPath((string)o["mat"]).OfType<Material>().FirstOrDefault(m => m.name == (string)o["matName"]);
        System.Func<Dictionary<string, object>, PhysicMaterial> PM = o => (string)o["pm"] == "" ? null : AssetDatabase.LoadAllAssetsAtPath((string)o["pm"]).OfType<PhysicMaterial>().FirstOrDefault(m => m.name == (string)o["pmName"]);
        // terrain cells (lowered only, never raised): applied after the mesh ops
        var ter = ops.Where(o => (string)o["op"] == "terrain").ToList(); ops = ops.Where(o => (string)o["op"] != "terrain").ToList();
        foreach (var tn in ter.Select(o => (string)o["terrain"]).Distinct()) if (!Terrain.activeTerrains.Any(t => t.name == tn)) problems.Add("no terrain " + tn);
        // check everything first
        var deleted = new HashSet<string>();
        foreach (var o in ops)
        {
            var op = (string)o["op"];
            if (op == "replaceAll") { var om = AssetDatabase.LoadAllAssetsAtPath((string)o["expect"]).OfType<Material>().FirstOrDefault(m => m.name == (string)o["expectName"]); if (!om) problems.Add("missing material " + o["expect"]); else if (!allR.Any(r => r.sharedMaterials.Contains(om))) problems.Add("nothing uses " + om.name); if (!Mat(o)) problems.Add("missing material " + o["mat"]); continue; }
            var p = (string)o["path"]; var t = Find(p);
            if (op == "delete" && t) { deleted.Add(p); continue; }
            if (op == "create") { if (t && !deleted.Contains(p)) problems.Add("already there: " + p); if (!LoadMesh((string)o["asset"], (string)o["meshName"])) problems.Add("missing mesh " + o["asset"]); if (!Mat(o)) problems.Add("missing material " + o["mat"]); continue; }
            if (!t) { problems.Add("not found: " + p); continue; }
            if (op == "move") { var e = ((List<object>)o["expectPos"]).Select(q => System.Convert.ToSingle(q, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); if ((t.position - new Vector3(e[0], e[1], e[2])).magnitude > 0.05f) problems.Add(p + " at " + t.position + ", expected (" + string.Join(",", e.Select(q => q.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()) + ")"); continue; }
            if (op == "material") { var rr = t.GetComponent<Renderer>(); if (!rr || !rr.sharedMaterial || rr.sharedMaterial.name != (string)o["expect"]) problems.Add(p + " has material " + (rr && rr.sharedMaterial ? rr.sharedMaterial.name : "none") + ", expected " + o["expect"]); if (!Mat(o)) problems.Add("missing material " + o["mat"]); continue; }
            if (op == "mesh") { var mf = t.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || mf.sharedMesh.name != (string)o["expect"]) problems.Add(p + " has " + (mf && mf.sharedMesh ? mf.sharedMesh.name : "no mesh") + ", expected " + o["expect"]); if (!LoadMesh((string)o["asset"], (string)o["meshName"])) problems.Add("missing mesh " + o["asset"] + " / " + o["meshName"]); }
        }
        if (problems.Count > 0) return "NOT APPLIED: " + string.Join("; ", problems.Distinct().Take(30).ToArray());
        int n = 0;
        foreach (var o in ops)
        {
            var op = (string)o["op"]; var p = o.ContainsKey("path") ? (string)o["path"] : "";
            if (op == "replaceAll") { var om = AssetDatabase.LoadAllAssetsAtPath((string)o["expect"]).OfType<Material>().First(m => m.name == (string)o["expectName"]); var nm = Mat(o); foreach (var r in allR) { var ms = r.sharedMaterials; bool hit = false; for (int k = 0; k < ms.Length; k++) if (ms[k] == om) { ms[k] = nm; hit = true; } if (hit) { r.sharedMaterials = ms; n++; } } continue; }   /* every scene renderer using one shared material gets the package copy (pepcol) */
            if (op == "delete") { Object.DestroyImmediate(Find(p).gameObject); }
            else if (op == "active") Find(p).gameObject.SetActive((bool)o["value"]);
            else if (op == "move") { var q = ((List<object>)o["pos"]).Select(v => System.Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); Find(p).position = new Vector3(q[0], q[1], q[2]); }   /* world position of one object (h14water: water plane level, the pond bed) */
            else if (op == "material") Find(p).GetComponent<Renderer>().sharedMaterial = Mat(o);   /* a package's own material copy on one object (15 h15s) */
            else if (op == "mesh") { var mf = Find(p).GetComponent<MeshFilter>(); var m = LoadMesh((string)o["asset"], (string)o["meshName"]); mf.sharedMesh = m; var mc = mf.GetComponent<MeshCollider>(); if (mc) { mc.sharedMesh = null; mc.sharedMesh = m; } }
            else if (op == "create")
            {
                var parts = p.Split('/'); var parent = parts.Length > 1 ? Find(string.Join("/", parts.Take(parts.Length - 1).ToArray())) : root; var go = new GameObject(parts.Last()); go.transform.SetParent(parent, false);
                go.layer = System.Convert.ToInt32(o["layer"]); try { go.tag = (string)o["tag"]; } catch { } GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)System.Convert.ToInt32(o["static"]));
                var m = LoadMesh((string)o["asset"], (string)o["meshName"]); go.AddComponent<MeshFilter>().sharedMesh = m; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Mat(o);
                r.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode)System.Convert.ToInt32(o["shadows"]); r.receiveShadows = (bool)o["receive"];
                var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.sharedMaterial = PM(o); mc.enabled = (bool)o["collider"]; go.SetActive((bool)o["active"]);
            }
            n++;
        }
        foreach (var g in ter.GroupBy(o => (string)o["terrain"]))
        {
            var td = Terrain.activeTerrains.First(t => t.name == g.Key).terrainData; Undo.RegisterCompleteObjectUndo(td, "bunker terrain");
            foreach (var o in g) { int i = System.Convert.ToInt32(o["i"]), j = System.Convert.ToInt32(o["j"]); float h = System.Convert.ToSingle(o["h"], System.Globalization.CultureInfo.InvariantCulture); var H = td.GetHeights(i, j, 1, 1); if (H[0, 0] > h) { H[0, 0] = h; td.SetHeights(i, j, H); } n++; }
            EditorUtility.SetDirty(td);
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene); Physics.SyncTransforms();
        /* island post-check (A's TierraBunkerIslands; only the 15 mounds BoJa approved are allowed) */
        string isl = "island check not available";
        var it = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TierraBunkerIslands")).FirstOrDefault(x => x != null);
        if (it != null) { var r = (string)it.GetMethod("Check").Invoke(null, null); isl = string.IsNullOrEmpty(r) ? "island check clean" : "ISLAND CHECK: " + r; }
        return "applied " + n + " ops from " + manifest + " (scene not saved); " + isl;
    }
}
