// Moorpark: clear trees, shrubs and painted scrub on and around every tee pad and behind every tee, on all 27 holes
// (the Ridgeline 9 Black-tee flyover started inside a bush). Tees and pins from the six GSPro GKDs; pads are the
// Spline_tee_ meshes. What's removed is listed in moorpark/vegetation/cleared_at_tees.csv. Unity 2018.2 compatible.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkClearTees
{
    public static float PadMargin = 4f;          // around a pad (shrubs, scrub); trees: trunk within this + TreeExtra
    public static float TreeExtra = 2f;
    public static float Behind = 25f;            // metres behind each tee
    public static float HalfWidth = 6f;          // corridor half-width at the tee, widening 0.15 m per metre back
    public static float Ahead = 12f;             // in front of the tee, half-width AheadHalf
    public static float AheadHalf = 4f;
    public static string Log = "C:/Users/user/Documents/moorpark/vegetation/cleared_at_tees.csv";

    [System.Serializable] class V3 { public float x, y, z; }
    [System.Serializable] class Tee { public string TeeType; public bool Enabled; public V3 Position; }
    [System.Serializable] class Pin { public V3 Position; }
    [System.Serializable] class Hole { public int HoleNumber; public bool Enabled; public Tee[] Tees; public Pin[] Pins; }
    [System.Serializable] class Gkd { public string CourseName; public Hole[] Holes; }

    struct TeeDir { public Vector2 p, d; }

    static List<TeeDir> Tees()
    {
        var list = new List<TeeDir>(); var seen = new HashSet<string>();
        foreach (string f in Directory.GetFiles(@"C:\GSProV1\Core\GSP\Courses", "Moorpark_*.GKD", SearchOption.AllDirectories))
        {
            var g = JsonUtility.FromJson<Gkd>(File.ReadAllText(f));
            foreach (var h in g.Holes)
            {
                if (h.Pins == null || h.Pins.Length == 0 || h.Pins[0].Position == null) continue;
                var pin = new Vector2(h.Pins[0].Position.x, h.Pins[0].Position.z);
                foreach (var t in h.Tees)
                {
                    if (!t.Enabled || t.Position == null || (t.Position.x == 0 && t.Position.z == 0) || t.TeeType.StartsWith("AimPoint")) continue;
                    var p = new Vector2(t.Position.x, t.Position.z);
                    string k = Mathf.Round(p.x * 10) + "," + Mathf.Round(p.y * 10);
                    if (!seen.Add(k)) continue;
                    // the shot goes to the first aim point if there is one, else the pin
                    Vector2 to = pin;
                    foreach (var a in h.Tees) if (a.TeeType == "AimPoint1" && a.Position != null && (a.Position.x != 0 || a.Position.z != 0) && a.Enabled) to = new Vector2(a.Position.x, a.Position.z);
                    list.Add(new TeeDir { p = p, d = (to - p).normalized });
                }
            }
        }
        return list;
    }

    // tee pad outline points (every vertex of the Spline_tee meshes and blends), bucketed on a 5 m grid
    static Dictionary<long, List<Vector2>> PadGrid()
    {
        var grid = new Dictionary<long, List<Vector2>>();
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.StartsWith("Spline_tee_")) continue;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = mf.transform.TransformPoint(v); var q = new Vector2(w.x, w.z);
                long k = Key(q); List<Vector2> l; if (!grid.TryGetValue(k, out l)) grid[k] = l = new List<Vector2>(); l.Add(q);
            }
        }
        return grid;
    }
    static long Key(Vector2 q) { return ((long)Mathf.FloorToInt(q.x / 5f) << 32) ^ (uint)Mathf.FloorToInt(q.y / 5f); }

    static float PadDistance(Dictionary<long, List<Vector2>> grid, Vector2 q, float max)
    {
        float best = float.MaxValue; int r = Mathf.CeilToInt(max / 5f);
        int cx = Mathf.FloorToInt(q.x / 5f), cy = Mathf.FloorToInt(q.y / 5f);
        for (int i = -r; i <= r; i++) for (int j = -r; j <= r; j++)
            {
                List<Vector2> l; if (!grid.TryGetValue(((long)(cx + i) << 32) ^ (uint)(cy + j), out l)) continue;
                foreach (var v in l) { float d = (v - q).sqrMagnitude; if (d < best) best = d; }
            }
        return Mathf.Sqrt(best);
    }

    static bool InCorridor(List<TeeDir> tees, Vector2 q, float extra)
    {
        foreach (var t in tees)
        {
            var r = q - t.p; float along = Vector2.Dot(r, t.d), across = Mathf.Abs(r.x * t.d.y - r.y * t.d.x);
            if (along < -Behind - extra || along > Ahead + extra) continue;
            float half = along < 0 ? HalfWidth + 0.15f * -along : AheadHalf;
            if (across <= half + extra) return true;
        }
        return false;
    }

    public static string Tree()
    {
        var sb = new StringBuilder();
        foreach (string group in new[] { "LIDAR_TREES", "SHRUBS" })
        {
            var root = GameObject.Find(group); sb.AppendLine(group + ": " + root.transform.childCount + " children");
            int k = 0;
            foreach (Transform c in root.transform)
            {
                var comps = new List<string>(); foreach (var x in c.GetComponents<Component>()) comps.Add(x ? x.GetType().Name : "null");
                sb.AppendLine("  " + c.name + " @" + c.position + " children " + c.childCount + " [" + string.Join(",", comps.ToArray()) + "]");
                if (c.childCount > 0) { var cc = c.GetChild(0); sb.AppendLine("     first child " + cc.name + " @" + cc.position + " children " + cc.childCount); }
                if (++k >= 4) break;
            }
        }
        return sb.ToString();
    }

    public static string Clear()
    {
        var tees = Tees(); var grid = PadGrid();
        var sb = new StringBuilder(); var csv = new StringBuilder("group,name,x,z,reason\n");
        int nTree = 0, nShrub = 0;
        foreach (string group in new[] { "LIDAR_TREES", "SHRUBS" })
        {
            var root = GameObject.Find(group); if (root == null) { sb.AppendLine("no " + group); continue; }
            bool tree = group == "LIDAR_TREES";
            var kill = new List<GameObject>();
            foreach (Transform c in root.transform)
            {
                var q = new Vector2(c.position.x, c.position.z);
                float extra = tree ? TreeExtra : 0f;
                string why = null;
                if (PadDistance(grid, q, PadMargin + extra + 1f) <= PadMargin + extra) why = "near pad";
                else if (InCorridor(tees, q, extra)) why = "behind/in front of tee";
                if (why == null) continue;
                kill.Add(c.gameObject);
                csv.AppendLine(group + "," + c.name.Replace(",", " ") + "," + q.x.ToString("0.0") + "," + q.y.ToString("0.0") + "," + why);
            }
            foreach (var g in kill) Undo.DestroyObjectImmediate(g);
            if (tree) nTree = kill.Count; else nShrub = kill.Count;
        }
        // painted scrub (the TR_Detail_ brush layers: buckwheat, sage, coyote brush ...) in the same places; grass layers stay
        var terrain = GameObject.Find("Terrain (Inner)").GetComponent<Terrain>(); var td = terrain.terrainData;
        int res = td.detailResolution; Vector3 size = td.size, pos = terrain.transform.position;
        var protos = td.detailPrototypes; int cells = 0; var layers = new List<string>();
        Undo.RegisterCompleteObjectUndo(td, "Clear scrub at tees");
        for (int L = 0; L < protos.Length; L++)
        {
            string pname = protos[L].usePrototypeMesh ? (protos[L].prototype ? protos[L].prototype.name : "") : (protos[L].prototypeTexture ? protos[L].prototypeTexture.name : "");
            if (!protos[L].usePrototypeMesh && !pname.Contains("Detail_")) continue;
            layers.Add(pname);
            int[,] d = td.GetDetailLayer(0, 0, res, res, L);
            foreach (var t in tees)
            {
                float reach = Behind + 10f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((t.p.x - reach - pos.x) / size.x * res), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((t.p.x + reach - pos.x) / size.x * res), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((t.p.y - reach - pos.z) / size.z * res), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((t.p.y + reach - pos.z) / size.z * res), 0, res - 1);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                    {
                        if (d[z, x] == 0) continue;
                        var q = new Vector2(pos.x + (x + 0.5f) * size.x / res, pos.z + (z + 0.5f) * size.z / res);
                        if (InCorridor(new List<TeeDir> { t }, q, 0f) || PadDistance(grid, q, PadMargin + 1f) <= PadMargin) { d[z, x] = 0; cells++; }
                    }
            }
            td.SetDetailLayer(0, 0, L, d);
        }
        EditorUtility.SetDirty(td);
        Directory.CreateDirectory(Path.GetDirectoryName(Log)); if (File.Exists(Log)) File.AppendAllText(Log, csv.ToString().Substring(csv.ToString().IndexOf('\n') + 1)); else File.WriteAllText(Log, csv.ToString());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        sb.AppendLine(tees.Count + " tee spots; removed " + nTree + " trees and " + nShrub + " shrubs; cleared " + cells + " scrub detail cells in " + string.Join(", ", layers.ToArray()) + "; list in " + Log);
        return sb.ToString();
    }
}
