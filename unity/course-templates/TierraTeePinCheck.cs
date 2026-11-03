using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Read-only: for every enabled tee and every pin in a GKD, which surface is under it in the open scene (top OPCD / path / terrain hit of a
// ray from above), and the GKD height minus that surface and minus the terrain. Writes AutomationOutput/teepin_check.csv; changes nothing.
public class TierraTeePinCheck
{
    public string gkd = @"C:\Users\user\Documents\tierra-rejada\greenkeeper\TierraRejada.GKD";
    [System.Serializable] class P { public float x, y, z; }
    [System.Serializable] class Tee { public string TeeType; public bool Enabled; public P Position; }
    [System.Serializable] class Pin { public string Day; public P Position; }
    [System.Serializable] class Hole { public bool Enabled; public int HoleNumber; public Tee[] Tees; public Pin[] Pins; }
    [System.Serializable] class G { public Hole[] Holes; }
    public string Run()
    {
        var g = JsonUtility.FromJson<G>(System.IO.File.ReadAllText(gkd).TrimStart('\uFEFF')); var opcd = GameObject.Find("OPCD Meshes").transform; var paths = GameObject.Find("CART_PATHS").GetComponentsInChildren<Collider>(); Physics.SyncTransforms();
        var rows = new List<string> { "hole,kind,name,x,z,gkd_y,surface,surface_y,above_surface,terrain_y,above_terrain,flag" }; var bad = new List<string>(); var ci = CultureInfo.InvariantCulture;
        foreach (var h in g.Holes)
        {
            var items = new List<KeyValuePair<string, P>>();
            foreach (var t in h.Tees) if (t.Position != null && t.Enabled && !t.TeeType.StartsWith("Aim") && t.TeeType != "GreenCenterPoint") items.Add(new KeyValuePair<string, P>("tee:" + t.TeeType, t.Position));   /* "Green" is a real tee slot (the Grey tees) */
            foreach (var t in h.Tees) if (t.Position != null && t.TeeType == "GreenCenterPoint") items.Add(new KeyValuePair<string, P>("gc:" + t.TeeType, t.Position));
            foreach (var p in h.Pins) if (p.Position != null) items.Add(new KeyValuePair<string, P>("pin:" + p.Day, p.Position));
            foreach (var it in items)
            {
                var q = it.Value; float top = float.MinValue, ty = float.MinValue; string nm = "none";
                foreach (var hit in Physics.RaycastAll(new Vector3(q.x, 600, q.z), Vector3.down, 1200)) { if (hit.collider is TerrainCollider) { ty = Mathf.Max(ty, hit.point.y); continue; } bool isP = paths.Contains(hit.collider); if (!(isP || hit.collider.transform.root == opcd)) continue; if (hit.point.y > top) { top = hit.point.y; nm = isP ? "PATH" : hit.collider.name; } }
                var l = nm.ToLower(); string surf = l.Contains("tee") ? "tee" : l.Contains("green") && !l.Contains("blend") ? "green" : l.Contains("fairway") ? "fairway" : l.Contains("bunker") ? "bunker" : l.Contains("rough") ? "rough" : l == "path" ? "path" : nm;
                bool isTee = it.Key.StartsWith("tee"); bool isGc = it.Key.StartsWith("gc"); string flag = isTee ? (surf == "tee" ? "" : "NOT ON TEE") : (surf == "green" ? "" : "NOT ON GREEN");
                if (top > float.MinValue && Mathf.Abs(q.y - top) > 0.3f) flag += (flag.Length > 0 ? "; " : "") + "height off " + (q.y - top).ToString("F2", ci) + " m";
                rows.Add(string.Join(",", new[] { h.HoleNumber.ToString(), isTee ? "tee" : isGc ? "gc" : "pin", it.Key.Substring(it.Key.IndexOf(':') + 1), q.x.ToString("F1", ci), q.z.ToString("F1", ci), q.y.ToString("F2", ci), surf, top > float.MinValue ? top.ToString("F2", ci) : "", top > float.MinValue ? (q.y - top).ToString("F2", ci) : "", ty > float.MinValue ? ty.ToString("F2", ci) : "", ty > float.MinValue ? (q.y - ty).ToString("F2", ci) : "", flag }));
                if (flag.Length > 0) bad.Add("h" + h.HoleNumber + " " + it.Key + " (" + q.x.ToString("F1", ci) + "," + q.z.ToString("F1", ci) + ") on " + surf + (top > float.MinValue ? " " + (q.y - top).ToString("+0.00;-0.00", ci) + " m vs surface" : "") + ": " + flag);
            }
        }
        System.IO.File.WriteAllLines("AutomationOutput/teepin_check.csv", rows.ToArray());
        return (rows.Count - 1) + " tees/pins checked; " + bad.Count + " flagged:\n" + string.Join("\n", bad.ToArray());
    }
}
