// CourseSceneOps: small, logged scene operations through the bridge (course-build-unity-tools). No course names, no constants.
//   Find          -Set @{ name = "<substring>" }   lists active and inactive objects (any root) whose name contains it
//   FindPlants    -Set @{ px = "1688"; pz = "815"; pr = "15" }  LODGroup plants within pr m of a point: name, root, height, crown
//   FindNear      -Set @{ px = ...; pz = ...; pr = "10" }  every renderer near a point, collider or not (render-only props)
//   FindOnHole    -Set @{ hole = "10"; width = "30" }       every non-plant renderer within width m of the hole line (rocks, props on a fairway)
//   GroundProbe   -Set @{ px = ...; pz = ... }               every collider under a point with height and layer, the terrain height, what Ground() returns
//   TerrainReport -Set @{ px = ...; pz = ...; pr = "10" }  terrain detail / tree prototypes, and what is painted near a point (painted objects are not GameObjects)
//   ClearTerrainDetails -Set @{ px = ...; pz = ...; pr = "3" }  clear painted details within pr m (Undo-registered)
//   DetailsOnTurfDryRun / DetailsOnTurfClear                  painted details whose top collider is green / collar / fairway / tee turf: list, or clear (rules brush.no_details_on_mown_turf): active, position, collider
//                                                  vs render dy (MeshCollider raycast at 1/50 of the vertices), physic material
//   SetOff/SetOn  -Set @{ name = "<exact name>" }  SetActive(false/true) on that object; logged "active|<name>|<was>" in COURSEBUILD_LOG
//   RebakeCollider -Set @{ name = "<exact name>" } MeshCollider.sharedMesh re-pointed at the MeshFilter's mesh (dy back to 0 when the
//                                                  render mesh was swapped under a stale collider); reports dy before and after
//   UndoActive                                     every "active|" log entry put back, newest first
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CourseSceneOps : EditorWindow
{
    const string LOG = "COURSEBUILD_LOG"; static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public static string name = ""; public static float px = 0, pz = 0, pr = 15f;   // FindPlants: LODGroup plants within pr m of (px, pz), any root

    // every transform in the scene (all roots), so plants, props and course meshes can all be named, switched and listed
    static IEnumerable<Transform> All() { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)); }

    // plants near a point: every LODGroup within pr m of (px, pz), with its root, height, crown radius (renderer bounds) and state
    public static string FindPlants()
    {
        var c = new Vector2(px, pz); var o = new List<string>();
        foreach (var lg in UnityEngine.Object.FindObjectsOfType<LODGroup>().Concat(All().Select(t => t.GetComponent<LODGroup>()).Where(l => l)).Distinct())
        {
            var p = lg.transform.position; float d = (new Vector2(p.x, p.z) - c).magnitude; if (d > pr) continue;
            var rs = lg.GetComponentsInChildren<Renderer>(true); Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(p, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds);
            o.Add(lg.name + "  root " + lg.transform.root.name + (lg.gameObject.activeInHierarchy ? "  active" : "  OFF") + "  at (" + p.x.ToString("F1", CI) + ", " + p.z.ToString("F1", CI) + ")  " + d.ToString("F1", CI) + " m away, height " + b.size.y.ToString("F1", CI) + " m, crown r " + (Mathf.Max(b.extents.x, b.extents.z)).ToString("F1", CI) + " m");
        }
        return o.Count + " plants within " + pr.ToString(CI) + " m of (" + px.ToString("F1", CI) + ", " + pz.ToString("F1", CI) + ")\n" + string.Join("\n", o.OrderBy(x => x).ToArray());
    }

    static float ColliderDy(MeshFilter f)
    {
        var mc = f ? f.GetComponent<MeshCollider>() : null; if (!f || !mc || !f.sharedMesh || !mc.sharedMesh) return float.NaN;
        var ra = f.sharedMesh.vertices; float worst = 0; int step = Mathf.Max(1, ra.Length / 50);
        for (int i = 0; i < ra.Length; i += step) { var wp = f.transform.TransformPoint(ra[i]); RaycastHit h; if (mc.Raycast(new Ray(wp + Vector3.up * 5, Vector3.down), out h, 20)) worst = Mathf.Max(worst, Mathf.Abs(h.point.y - wp.y)); }
        return worst;
    }

    // every renderer within pr m of (px, pz), collider or not: render-only props that no census or audit can see (the rock on Tierra 10's fairway)
    public static string FindNear()
    {
        var c = new Vector2(px, pz); var o = new List<string>();
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            var b = r.bounds; float d = (new Vector2(b.center.x, b.center.z) - c).magnitude; if (d > pr) continue; if (r.transform.GetComponentInParent<Terrain>()) continue;
            var t = r.transform; string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(path + "  at (" + b.center.x.ToString("F1", CI) + ", " + b.center.z.ToString("F1", CI) + ")  " + d.ToString("F1", CI) + " m, size " + b.size.x.ToString("F1", CI) + "x" + b.size.y.ToString("F1", CI) + "x" + b.size.z.ToString("F1", CI) + (r.GetComponent<Collider>() ? "  collider" : "  no collider") + (r.GetComponentInParent<LODGroup>() ? "  plant" : ""));
        }
        return o.Count + " renderers within " + pr.ToString(CI) + " m of (" + px.ToString("F1", CI) + ", " + pz.ToString("F1", CI) + ")\n" + string.Join("\n", o.OrderBy(x => x).Take(80).ToArray());
    }

    public static string hole = ""; public static float width = 30f;
    // every non-plant renderer within `width` m of a hole's line (tee -> aims -> green from the GKD in Assets/CourseBuild), collider or not:
    // rocks, props and stray meshes on a fairway (the boulder on Tierra 10). OPCD course meshes are listed only when they carry no collider.
    public static string FindOnHole()
    {
        int hn; if (!int.TryParse(hole, out hn)) return "set hole = <number>";
        var gkdPath = "Assets/CourseBuild/course.json"; if (!System.IO.File.Exists(gkdPath)) return "no Assets/CourseBuild/course.json (run the tool sync)";
        var cj = JsonUtility.FromJson<CourseJsonLite>(System.IO.File.ReadAllText(gkdPath).TrimStart('\uFEFF')); string gp = cj != null && cj.paths != null ? cj.paths.gkd : null; if (string.IsNullOrEmpty(gp)) return "no paths.gkd in course.json";
        if (!System.IO.Path.IsPathRooted(gp)) gp = System.IO.Path.GetFullPath(System.IO.Path.Combine(cj.paths.course_repo_abs ?? ".", gp)); if (!System.IO.File.Exists(gp)) return "GKD not found: " + gp;
        var g = JsonUtility.FromJson<GLite>(System.IO.File.ReadAllText(gp).TrimStart('\uFEFF')); var H = g.Holes.FirstOrDefault(h => h.HoleNumber == hn); if (H == null) return "hole " + hn + " not in the GKD";
        var line = new List<Vector2>(); foreach (var k in new[] { "Black", "Blue", "White", "Gold", "Green", "Red", "Yellow" }) { var t = H.Tees.FirstOrDefault(x => x.TeeType == k && x.Position != null && (x.Position.x != 0 || x.Position.z != 0)); if (t != null) { line.Add(new Vector2(t.Position.x, t.Position.z)); break; } }
        foreach (var k in new[] { "AimPoint1", "AimPoint2", "GreenCenterPoint" }) { var t = H.Tees.FirstOrDefault(x => x.TeeType == k && x.Position != null && (x.Position.x != 0 || x.Position.z != 0)); if (t != null) line.Add(new Vector2(t.Position.x, t.Position.z)); }
        if (line.Count < 2) return "hole " + hn + ": no line (tee and green centre needed)";
        Func<Vector2, float> off = q => { float best = 1e9f; for (int i = 1; i < line.Count; i++) { var a = line[i - 1]; var ab = line[i] - a; float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f)); best = Mathf.Min(best, Vector2.Distance(q, a + ab * u)); } return best; };
        var opcd = GameObject.Find("OPCD Meshes"); var o = new List<string>();
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            if (r.GetComponentInParent<Terrain>() || r.GetComponentInParent<LODGroup>()) continue; var b = r.bounds; var c = new Vector2(b.center.x, b.center.z); float d = off(c); if (d > width) continue;
            bool isOpcd = opcd && r.transform.IsChildOf(opcd.transform); bool hasCol = r.GetComponent<Collider>(); if (isOpcd && hasCol) continue; if (b.size.x > 60 || b.size.z > 60) continue;
            var t = r.transform; string path = t.name; for (var u = t.parent; u; u = u.parent) path = u.name + "/" + path;
            o.Add(string.Format(CI, "{0}  at ({1:F1}, {2:F1})  {3:F1} m off the line, size {4:F1}x{5:F1}x{6:F1}{7}", path, b.center.x, b.center.z, d, b.size.x, b.size.y, b.size.z, hasCol ? "  collider" : "  no collider"));
        }
        return o.Count + " non-plant renderers within " + width.ToString(CI) + " m of hole " + hn + "'s line (OPCD meshes with colliders left out)\n" + string.Join("\n", o.OrderBy(x => x).Take(120).ToArray());
    }
    [Serializable] class PLite { public float x, y, z; } [Serializable] class TeeLite { public string TeeType; public PLite Position; } [Serializable] class HoleLite { public int HoleNumber; public TeeLite[] Tees; } [Serializable] class GLite { public HoleLite[] Holes; }
    [Serializable] class PathsLite { public string gkd, course_repo_abs; } [Serializable] class CourseJsonLite { public PathsLite paths; }

    // what "ground" means at a point: every collider under it with height and layer (all layers), the active terrain's own sampled height, and what
    // the flyover tools' Ground() (default raycast layers, LODGroups skipped) would return. Says why a camera at ground + 1.7 m ends up inside the hill.
    public static string GroundProbe()
    {
        var o = new List<string> { "ground at (" + px.ToString("F1", CI) + ", " + pz.ToString("F1", CI) + ")" }; float best = float.MinValue, bestDef = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(px, 800, pz), Vector3.down, 1600, ~0).OrderByDescending(h => h.point.y))
        {
            bool lod = h.collider.GetComponentInParent<LODGroup>(); bool inDefault = (Physics.DefaultRaycastLayers & (1 << h.collider.gameObject.layer)) != 0;
            o.Add("  " + h.point.y.ToString("F2", CI) + "  " + h.collider.name + "  layer " + LayerMask.LayerToName(h.collider.gameObject.layer) + (inDefault ? "" : " (NOT in the default raycast layers)") + (lod ? "  plant" : "") + (h.collider is TerrainCollider ? "  terrain" : "") + (h.collider.isTrigger ? "  trigger" : ""));
            if (h.point.y > best) best = h.point.y; if (inDefault && !lod && !h.collider.isTrigger && h.point.y > bestDef) bestDef = h.point.y;
        }
        foreach (var t in Terrain.activeTerrains) { var tp = t.GetPosition(); var sz = t.terrainData.size; if (px >= tp.x && pz >= tp.z && px <= tp.x + sz.x && pz <= tp.z + sz.z) o.Add("  terrain " + t.name + " SampleHeight " + (t.SampleHeight(new Vector3(px, 0, pz)) + tp.y).ToString("F2", CI) + "  layer " + LayerMask.LayerToName(t.gameObject.layer) + (t.GetComponent<TerrainCollider>() ? "" : "  NO TerrainCollider")); }
        o.Add("highest collider " + (best == float.MinValue ? "none" : best.ToString("F2", CI)) + "; flyover Ground() would use " + (bestDef == float.MinValue ? "the terrain fallback" : bestDef.ToString("F2", CI)));
        return string.Join("\n", o.ToArray());
    }

    // the Unity terrains' painted layers: detail prototypes (grass / rock meshes) and tree prototypes, and near (px, pz) within pr m the
    // painted detail counts per layer and the tree instances. Painted objects are not GameObjects, so no Find sees them.
    public static string TerrainReport()
    {
        var o = new List<string>(); var c = new Vector2(px, pz);
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var tp = t.GetPosition(); var sz = td.size; o.Add(t.name + ": " + sz.x.ToString("F0", CI) + " x " + sz.z.ToString("F0", CI) + " m, " + td.detailPrototypes.Length + " detail prototypes, " + td.treePrototypes.Length + " tree prototypes, " + td.treeInstanceCount + " tree instances");
            for (int i = 0; i < td.detailPrototypes.Length; i++) { var dp = td.detailPrototypes[i]; o.Add("  detail " + i + ": " + (dp.usePrototypeMesh ? "mesh " + (dp.prototype ? dp.prototype.name : "?") : "texture " + (dp.prototypeTexture ? dp.prototypeTexture.name : "?")) + " render " + dp.renderMode); }
            for (int i = 0; i < td.treePrototypes.Length; i++) { var tr = td.treePrototypes[i]; o.Add("  tree " + i + ": " + (tr.prefab ? tr.prefab.name : "?")); }
            if (px == 0 && pz == 0) continue;
            if (px < tp.x || pz < tp.z || px > tp.x + sz.x || pz > tp.z + sz.z) { o.Add("  point outside this terrain"); continue; }
            int dw = td.detailWidth, dh = td.detailHeight; float cx = (px - tp.x) / sz.x * dw, cz = (pz - tp.z) / sz.z * dh; int r = Mathf.CeilToInt(pr / sz.x * dw) + 1;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(cx - r), 0, dw - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(cx + r), 0, dw - 1), z0 = Mathf.Clamp(Mathf.FloorToInt(cz - r), 0, dh - 1), z1 = Mathf.Clamp(Mathf.CeilToInt(cz + r), 0, dh - 1);
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                var layer = td.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, i); int n = 0, cells = 0;
                for (int a = 0; a < layer.GetLength(0); a++) for (int b = 0; b < layer.GetLength(1); b++) if (layer[a, b] > 0) { n += layer[a, b]; cells++; }
                if (n > 0) o.Add("  near the point: detail " + i + " painted in " + cells + " cells (" + n + " instances) within ~" + pr.ToString(CI) + " m");
            }
            int trees = 0; foreach (var ti in td.treeInstances) { var wp = new Vector2(tp.x + ti.position.x * sz.x, tp.z + ti.position.z * sz.z); if ((wp - c).magnitude <= pr) { trees++; if (trees <= 20) o.Add("  near the point: tree prototype " + ti.prototypeIndex + " (" + (td.treePrototypes[ti.prototypeIndex].prefab ? td.treePrototypes[ti.prototypeIndex].prefab.name : "?") + ") at (" + wp.x.ToString("F1", CI) + ", " + wp.y.ToString("F1", CI) + ")"); } }
            if (trees > 20) o.Add("  ... " + trees + " tree instances within " + pr.ToString(CI) + " m");
        }
        return string.Join("\n", o.ToArray());
    }

    // clear painted detail layers (and optionally terrain trees) within pr m of (px, pz): Undo-registered on the TerrainData, so Edit > Undo reverses it
    public static string ClearTerrainDetails()
    {
        var c = new Vector2(px, pz); var o = new List<string>(); int total = 0;
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; var tp = t.GetPosition(); var sz = td.size; if (px < tp.x || pz < tp.z || px > tp.x + sz.x || pz > tp.z + sz.z) continue;
            Undo.RegisterCompleteObjectUndo(td, "clear terrain details"); int dw = td.detailWidth, dh = td.detailHeight; float cx = (px - tp.x) / sz.x * dw, cz = (pz - tp.z) / sz.z * dh; float rr = pr / sz.x * dw;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(cx - rr), 0, dw - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(cx + rr), 0, dw - 1), z0 = Mathf.Clamp(Mathf.FloorToInt(cz - rr), 0, dh - 1), z1 = Mathf.Clamp(Mathf.CeilToInt(cz + rr), 0, dh - 1);
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                var layer = td.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, i); int n = 0;
                for (int a = 0; a < layer.GetLength(0); a++) for (int b = 0; b < layer.GetLength(1); b++) { float dx = (x0 + b) - cx, dz = (z0 + a) - cz; if (dx * dx + dz * dz <= rr * rr && layer[a, b] > 0) { n += layer[a, b]; layer[a, b] = 0; } }
                if (n > 0) { td.SetDetailLayer(x0, z0, i, layer); total += n; o.Add(t.name + ": detail " + i + " cleared " + n + " instances"); }
            }
        }
        return (total == 0 ? "nothing painted within " + pr.ToString(CI) + " m of (" + px.ToString("F1", CI) + ", " + pz.ToString("F1", CI) + ")" : string.Join("\n", o.ToArray()) + "\n(Edit > Undo reverses; save the scene to keep)");
    }

    // painted terrain details on mown turf: every detail cell with instances whose top collider is green, collar, fairway or tee turf is listed (dry run)
    // or cleared (apply). A dead-brush billboard on a fairway reads as a rock from the air (Tierra 10, 11/4). Undo-registered.
    public static string DetailsOnTurfDryRun() { return DetailsOnTurf(false); }
    public static string DetailsOnTurfClear() { return DetailsOnTurf(true); }
    static bool MownTurf(Collider c)
    {
        if (!c || c.GetComponentInParent<LODGroup>() || c is TerrainCollider) return false; var pm = c.sharedMaterial ? c.sharedMaterial.name : ""; var n = c.name.ToLower();
        return pm == "GSPgreen" || pm == "GSPfairway" || pm == "GSPtee" || n.Contains("_green_-_mesh") || n.Contains("fairway") || n.Contains("_tee_-_mesh") || n.Contains("collar");
    }
    static string DetailsOnTurf(bool apply)
    {
        Physics.SyncTransforms(); var o = new List<string>(); int totalCells = 0, totalInst = 0;
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; if (td.detailPrototypes.Length == 0) continue; var tp = t.GetPosition(); var sz = td.size; int dw = td.detailWidth, dh = td.detailHeight;
            if (apply) Undo.RegisterCompleteObjectUndo(td, "clear details on turf");
            var perLayer = new int[td.detailPrototypes.Length]; var byMesh = new Dictionary<string, int>(); var turfCell = new bool?[dh, dw];
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                var layer = td.GetDetailLayer(0, 0, dw, dh, i); bool changed = false;
                for (int a = 0; a < dh; a++) for (int b = 0; b < dw; b++)
                {
                    if (layer[a, b] == 0) continue;
                    if (turfCell[a, b] == null)
                    {
                        float wx = tp.x + (b + 0.5f) / dw * sz.x, wz = tp.z + (a + 0.5f) / dh * sz.z; Collider top = null; float topY = float.MinValue;
                        foreach (var h in Physics.RaycastAll(new Vector3(wx, 800, wz), Vector3.down, 1600)) { if (h.collider.GetComponentInParent<LODGroup>() || h.collider.isTrigger) continue; if (h.point.y > topY) { topY = h.point.y; top = h.collider; } }
                        bool turf = MownTurf(top); turfCell[a, b] = turf; if (turf) { int n; byMesh.TryGetValue(top.name, out n); byMesh[top.name] = n + 1; }
                    }
                    if (turfCell[a, b] == true) { perLayer[i] += layer[a, b]; totalInst += layer[a, b]; totalCells++; if (apply) { layer[a, b] = 0; changed = true; } }
                }
                if (apply && changed) td.SetDetailLayer(0, 0, i, layer);
            }
            o.Add(t.name + ": " + totalCells + " detail cells on mown turf, " + totalInst + " instances" + (apply ? " CLEARED" : ""));
            for (int i = 0; i < perLayer.Length; i++) if (perLayer[i] > 0) o.Add("  detail " + i + " " + (td.detailPrototypes[i].prototypeTexture ? td.detailPrototypes[i].prototypeTexture.name : td.detailPrototypes[i].prototype ? td.detailPrototypes[i].prototype.name : "?") + ": " + perLayer[i]);
            foreach (var kv in byMesh.OrderByDescending(k => k.Value).Take(25)) o.Add("  on " + kv.Key + ": " + kv.Value + " cells");
        }
        if (o.Count == 0) return "no terrain with detail layers";
        return string.Join("\n", o.ToArray()) + (apply ? "\n(Edit > Undo reverses; save the scene to keep)" : "\nDetailsOnTurfClear clears them");
    }

    public static string Find()
    {
        if (string.IsNullOrEmpty(name)) return "set name = <substring>"; var o = new List<string>(); string n = name.ToLower();
        foreach (var t in All().Where(t => t.name.ToLower().Contains(n)).OrderBy(t => t.name))
        {
            var f = t.GetComponent<MeshFilter>(); var c = t.GetComponent<Collider>(); float dy = ColliderDy(f);
            o.Add(t.name + (t.gameObject.activeInHierarchy ? "  active" : "  OFF") + "  at (" + t.position.x.ToString("F1", CI) + ", " + t.position.z.ToString("F1", CI) + ")" + (f && f.sharedMesh ? "  mesh " + f.sharedMesh.name + " (" + f.sharedMesh.vertexCount + " v)" : "  no mesh") + (c ? "  collider " + (c.sharedMaterial ? c.sharedMaterial.name : "no material") + (float.IsNaN(dy) ? "" : " dy " + (dy * 100).ToString("F1", CI) + " cm") : "  no collider"));
        }
        return o.Count + " objects\n" + string.Join("\n", o.ToArray());
    }

    static string SetActive(bool on)
    {
        // several objects may share a name (tree prefabs): with px/pz set, the one nearest that point is taken, and the result says where it was
        var cands = All().Where(x => x.name == name).ToList(); if (cands.Count == 0) return "no object named " + name;
        var t = (px != 0 || pz != 0) ? cands.OrderBy(x => (new Vector2(x.position.x, x.position.z) - new Vector2(px, pz)).sqrMagnitude).First() : cands[0];
        if (cands.Count > 1 && px == 0 && pz == 0) return cands.Count + " objects named " + name + ": pass px, pz to pick the one nearest a point";
        bool was = t.gameObject.activeSelf; if (was == on) return name + " already " + (on ? "active" : "off");
        var log = GameObject.Find(LOG) ?? new GameObject(LOG); new GameObject("active|" + name + "|" + (was ? "1" : "0") + "|" + t.position.x.ToString("F2", CI) + "|" + t.position.z.ToString("F2", CI)).transform.SetParent(log.transform, false);
        Undo.RecordObject(t.gameObject, "scene ops"); t.gameObject.SetActive(on); EditorUtility.SetDirty(t.gameObject); Physics.SyncTransforms();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene); return name + " under " + t.root.name + " at (" + t.position.x.ToString("F1", CI) + ", " + t.position.z.ToString("F1", CI) + ")" + (on ? " active" : " OFF") + " (was " + (was ? "active" : "off") + "; UndoActive puts it back)";
    }
    public static string SetOff() { return SetActive(false); }
    public static string SetOn() { return SetActive(true); }

    public static string RebakeCollider()
    {
        var t = All().FirstOrDefault(x => x.name == name); if (!t) return "no OPCD object named " + name;
        var f = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>(); if (!f || !f.sharedMesh) return name + ": no mesh"; if (!mc) return name + ": no MeshCollider";
        float before = ColliderDy(f); string was = mc.sharedMesh ? mc.sharedMesh.name : "none";
        Undo.RecordObject(mc, "rebake collider"); mc.sharedMesh = null; mc.sharedMesh = f.sharedMesh; EditorUtility.SetDirty(mc); Physics.SyncTransforms();
        float after = ColliderDy(f); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        return name + ": collider was " + was + ", now " + f.sharedMesh.name + "; dy " + (before * 100).ToString("F1", CI) + " cm -> " + (after * 100).ToString("F1", CI) + " cm";
    }

    public static string UndoActive()
    {
        var log = GameObject.Find(LOG); if (!log) return "no " + LOG; int n = 0; var o = new List<string>();
        foreach (var e in log.transform.Cast<Transform>().Reverse().ToList())
        {
            var p = e.name.Split('|'); if (p.Length < 3 || p[0] != "active") continue; var same = All().Where(x => x.name == p[1]).ToList(); if (same.Count == 0) { o.Add(p[1] + " not found"); continue; }
            var t = p.Length >= 5 ? same.OrderBy(x => (new Vector2(x.position.x, x.position.z) - new Vector2(float.Parse(p[3], CI), float.Parse(p[4], CI))).sqrMagnitude).First() : same[0];
            t.gameObject.SetActive(p[2] == "1"); EditorUtility.SetDirty(t.gameObject); UnityEngine.Object.DestroyImmediate(e.gameObject); n++;
        }
        Physics.SyncTransforms(); return n + " objects put back" + (o.Count > 0 ? "\n" + string.Join("\n", o.ToArray()) : "");
    }

    void OnGUI() { name = EditorGUILayout.TextField("Name", name); if (GUILayout.Button("Find")) Debug.Log(Find()); if (GUILayout.Button("Set off")) Debug.Log(SetOff()); if (GUILayout.Button("Set on")) Debug.Log(SetOn()); if (GUILayout.Button("Rebake collider")) Debug.Log(RebakeCollider()); if (GUILayout.Button("Undo active changes")) Debug.Log(UndoActive()); }
    [MenuItem("Course Tools/Scene ops")] static void Open() { GetWindow<CourseSceneOps>("Scene ops"); }
}
