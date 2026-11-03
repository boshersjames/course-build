using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Read-only audit against the GSPro beta submission checklist (BoJa 10/31): what the Collider Inspector would show red / orange,
// renderers with no collider, and plants or trees standing on cart paths. Changes nothing.
public static class TierraBetaAudit
{
    public static string Colliders()
    {
        var o = new List<string>(); var noPm = new Dictionary<string, int>(); var meshLeaves = new Dictionary<string, int>(); int all = 0;
        foreach (var c in Object.FindObjectsOfType<Collider>())
        {
            if (!c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger) continue; all++; var root = c.transform.root.name;
            if (c is TerrainCollider) continue;
            if (!c.sharedMaterial) { int n; noPm.TryGetValue(root, out n); noPm[root] = n + 1; }
            else if (c is MeshCollider && c.sharedMaterial.name.ToLower().Contains("leaves")) { int n; meshLeaves.TryGetValue(root, out n); meshLeaves[root] = n + 1; }
        }
        o.Add(all + " active colliders");
        o.Add("RED (collider, no physic material): " + noPm.Values.Sum() + (noPm.Count > 0 ? " - " + string.Join(", ", noPm.OrderByDescending(k => k.Value).Take(15).Select(k => k.Key + " " + k.Value).ToArray()) : ""));
        o.Add("ORANGE (MeshCollider set to leaves): " + meshLeaves.Values.Sum() + (meshLeaves.Count > 0 ? " - " + string.Join(", ", meshLeaves.OrderByDescending(k => k.Value).Take(15).Select(k => k.Key + " " + k.Value).ToArray()) : ""));
        return string.Join("\n", o.ToArray());
    }
    // OPCD meshes (playing surfaces) that render but have no collider: a ball would fall through
    public static string OpcdNoCollider()
    {
        var opcd = GameObject.Find("OPCD Meshes").transform; var l = new List<string>();
        foreach (var r in opcd.GetComponentsInChildren<MeshRenderer>()) { if (!r.enabled || !r.gameObject.activeInHierarchy) continue; if (r.GetComponent<Collider>()) continue; l.Add(r.name); }
        return l.Count + " active OPCD renderers without a collider" + (l.Count > 0 ? ": " + string.Join(", ", l.Take(25).ToArray()) : "");
    }
    // trees / shrubs / grasses whose base sits on a cart path or road (top hit at the base is CART_PATHS / ROAD_TR / ROAD16)
    public static string PlantsOnPaths()
    {
        Physics.SyncTransforms(); var skip = new HashSet<string> { "OPCD Meshes", "Terrain (Inner)", "Outer Plot", "CART_PATHS", "ROAD_TR", "ROAD16", "FENCES", "WALLS", "NETS", "HOLE_SIGNS_1029", "CLUBHOUSE", "H18I7", "BRIDGES" };
        var paths = new HashSet<string> { "CART_PATHS", "ROAD_TR", "ROAD16" }; var hit = new Dictionary<string, List<string>>(); int checkedN = 0;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (skip.Contains(root.name) || !root.activeInHierarchy) continue;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>())
            {
                var p = lg.transform.position; checkedN++; string top = ""; float y = float.MinValue;
                foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 30, p.z), Vector3.down, 80)) { var rn = h.collider.transform.root.name; if (h.collider.transform.IsChildOf(lg.transform)) continue; if (!(paths.Contains(rn) || rn == "OPCD Meshes" || h.collider is TerrainCollider)) continue; if (h.point.y > y) { y = h.point.y; top = rn; } }
                if (paths.Contains(top)) { List<string> l; if (!hit.TryGetValue(root.name, out l)) hit[root.name] = l = new List<string>(); l.Add(lg.name + " (" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ")"); }
            }
        }
        return checkedN + " plant bases checked; on a path/road: " + hit.Values.Sum(v => v.Count) + (hit.Count > 0 ? "\n" + string.Join("\n", hit.Select(k => "  " + k.Key + " " + k.Value.Count + ": " + string.Join("; ", k.Value.Take(6).ToArray())).ToArray()) : "");
    }
}
