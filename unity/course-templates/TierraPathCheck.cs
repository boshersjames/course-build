using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// Beta checklist helpers (read-only): "cart paths should be flat and smooth" and "tee markers on the tee box, not hovering or sunken".
// Paths(): every mesh under `pathRoots` is sampled on a `step` grid (top path surface); at each sample a plane is fitted to the samples
//   within `radius` and the sample's height off that plane is its bump. Bumps over `bump` (default 1.5 cm) are grouped into patches
//   and listed with their centre, size and worst height (steps between overlapping path pieces show up the same way).
// TeeMarkers(): GSPro sets the tee markers at runtime from CourseOverrides (our TM_* prefabs, width 5). We assume they stand `width`/2
//   either side of each GKD tee spot, across the line to the hole's first aim point (or the green centre). Under each marker spot: the
//   top course surface (OPCD meshes and paths), flagged when it isn't a tee pad or sits more than `markerTol` above or below the tee spot.
//   It also lists holes and slits in the path (empty cells with path on both sides within `maxGap`), course turf (OPCD meshes) sitting
//   more than `cover` above the path, breaks between separate path pieces whose ends come within `breakDist` (10 m), and terrain detail grass on it (BoJa's hole 3 and hole 8 flyover notes, Oct 31).
// Writes AutomationOutput/path_check.txt / .csv and tee_markers.txt / .csv; changes nothing.
public class TierraPathCheck
{
    public string gkd = @"C:\Users\user\Documents\newcourses\tr-automation\greenkeeper\TierraRejada.GKD";
    public string pathRoots = "CART_PATHS,ROAD_TR,ROAD16";
    public float step = 0.25f, radius = 0.6f, bump = 0.015f, stepTol = 0.01f, cover = 0.005f, maxGap = 1.5f, breakDist = 10f, width = 5f, markerTol = 0.05f;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------ path bumps (core)
    public class Patch { public float x, z, worst, step; public int n; public string what = ""; }
    // heights: grid samples (x, y, z) over the paths; returns the bump patches, worst first
    public List<Patch> Bumps(List<Vector3> s, out int checkedN)
    {
        checkedN = 0; var grid = new Dictionary<long, int>(); for (int i = 0; i < s.Count; i++) grid[TierraGeom.TriIndex.Key(Mathf.RoundToInt(s[i].x / step), Mathf.RoundToInt(s[i].z / step))] = i;
        int r = Mathf.CeilToInt(radius / step); var off = new Dictionary<long, float>(); var jumps = new Dictionary<long, float>(); var nb = new List<Vector3>();
        foreach (var kv in grid)
        {
            var p = s[kv.Value]; int ix = Mathf.RoundToInt(p.x / step), iz = Mathf.RoundToInt(p.z / step); nb.Clear();
            for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++) { if ((dx * dx + dz * dz) * step * step > radius * radius) continue; int j; if (grid.TryGetValue(TierraGeom.TriIndex.Key(ix + dx, iz + dz), out j)) nb.Add(s[j]); }
            if (nb.Count < 12) continue;   /* path edges and slivers: too few neighbours for a fair plane */
            float a, b, c, ox, oz; if (!TierraGeom.FitPlane(nb, out a, out b, out c, out ox, out oz)) continue; checkedN++;
            float e = p.y - TierraGeom.PlaneY(a, b, c, ox, oz, p.x, p.z); if (Mathf.Abs(e) > bump) off[kv.Key] = e;
            /* steps: the second difference along x and z (0.25 m each way) beyond `stepTol` marks a lip or an overlapping piece */
            for (int d2 = 0; d2 < 2; d2++)
            {
                int j, k; if (!grid.TryGetValue(TierraGeom.TriIndex.Key(ix + (d2 == 0 ? 1 : 0), iz + (d2 == 1 ? 1 : 0)), out j) || !grid.TryGetValue(TierraGeom.TriIndex.Key(ix - (d2 == 0 ? 1 : 0), iz - (d2 == 1 ? 1 : 0)), out k)) continue;
                float jump = (s[j].y - p.y) - (p.y - s[k].y); if (Mathf.Abs(jump) <= stepTol) continue;
                float was; if (!jumps.TryGetValue(kv.Key, out was) || Mathf.Abs(jump) > Mathf.Abs(was)) jumps[kv.Key] = jump; if (!off.ContainsKey(kv.Key)) off[kv.Key] = e;
            }
        }
        var o = new List<Patch>(); var seen = new HashSet<long>();
        foreach (var k0 in off.Keys)
        {
            if (seen.Contains(k0)) continue; var pt = new Patch(); var q = new Queue<long>(); q.Enqueue(k0); seen.Add(k0); float sx = 0, sz = 0;
            while (q.Count > 0)
            {
                var k = q.Dequeue(); var p = s[grid[k]]; pt.n++; sx += p.x; sz += p.z; if (Mathf.Abs(off[k]) > Mathf.Abs(pt.worst)) pt.worst = off[k]; float jk; if (jumps.TryGetValue(k, out jk) && Mathf.Abs(jk) > Mathf.Abs(pt.step)) pt.step = jk;
                int ix = Mathf.RoundToInt(p.x / step), iz = Mathf.RoundToInt(p.z / step);
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) { long nk = TierraGeom.TriIndex.Key(ix + dx, iz + dz); if (off.ContainsKey(nk) && seen.Add(nk)) q.Enqueue(nk); }
            }
            pt.x = sx / pt.n; pt.z = sz / pt.n; o.Add(pt);
        }
        return o.OrderByDescending(p => Mathf.Max(Mathf.Abs(p.worst), Mathf.Abs(p.step))).ToList();
    }

    // marker spots: width/2 either side of the tee, across the tee -> aim line
    public static Vector2[] MarkerSpots(Vector2 tee, Vector2 aim, float width)
    {
        var d = aim - tee; if (d.sqrMagnitude < 1e-6f) d = new Vector2(0, 1); d.Normalize(); var side = new Vector2(-d.y, d.x) * (width / 2);
        return new[] { tee + side, tee - side };
    }

    // ------------------------------------------------------------------ holes and covers (core)
    // empty cells of the path footprint with path on both sides within `reach` cells along x or along z: holes and slits in the path
    public static List<long> Holes(HashSet<long> have, IEnumerable<long> footprint, int reach)
    {
        var o = new List<long>();
        foreach (var k in footprint)
        {
            if (have.Contains(k)) continue; int x = (int)(k >> 32), z = (int)(uint)k;
            if ((Near(have, x, z, 1, 0, reach) && Near(have, x, z, -1, 0, reach)) || (Near(have, x, z, 0, 1, reach) && Near(have, x, z, 0, -1, reach))) o.Add(k);
        }
        return o;
    }
    static bool Near(HashSet<long> have, int x, int z, int dx, int dz, int reach) { for (int i = 1; i <= reach; i++) if (have.Contains(TierraGeom.TriIndex.Key(x + dx * i, z + dz * i))) return true; return false; }

    // group flagged grid cells (8-neighbour) into patches; `what` names the most common source in each
    public List<Patch> Group(Dictionary<long, float> flagged, Dictionary<long, string> what)
    {
        var o = new List<Patch>(); var seen = new HashSet<long>();
        foreach (var k0 in flagged.Keys)
        {
            if (!seen.Add(k0)) continue; var pt = new Patch(); var q = new Queue<long>(); q.Enqueue(k0); float sx = 0, sz = 0; var names = new Dictionary<string, int>();
            while (q.Count > 0)
            {
                var k = q.Dequeue(); int ix = (int)(k >> 32), iz = (int)(uint)k; pt.n++; sx += ix * step; sz += iz * step; if (Mathf.Abs(flagged[k]) > Mathf.Abs(pt.worst)) pt.worst = flagged[k];
                string nm; if (what != null && what.TryGetValue(k, out nm)) { int c; names.TryGetValue(nm, out c); names[nm] = c + 1; }
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) { long nk = TierraGeom.TriIndex.Key(ix + dx, iz + dz); if (flagged.ContainsKey(nk) && seen.Add(nk)) q.Enqueue(nk); }
            }
            pt.x = sx / pt.n; pt.z = sz / pt.n; pt.what = names.Count > 0 ? names.OrderByDescending(e => e.Value).First().Key : ""; o.Add(pt);
        }
        return o.OrderByDescending(p => p.n).ToList();
    }

    // breaks between path pieces: separate pieces (8-neighbour) whose edges come within `maxDist` of each other without joining.
    // Returns one gap per pair of pieces, the closest edge points first-piece-to-second, nearest first.
    public class Gap { public float ax, az, bx, bz, d; public int a, b; }
    public List<Gap> Gaps(HashSet<long> have, float maxDist)
    {
        var comp = new Dictionary<long, int>(); int nc = 0;
        foreach (var k0 in have)
        {
            if (comp.ContainsKey(k0)) continue; var q = new Queue<long>(); q.Enqueue(k0); comp[k0] = nc;
            while (q.Count > 0) { var k = q.Dequeue(); int ix = (int)(k >> 32), iz = (int)(uint)k; for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) { long nk = TierraGeom.TriIndex.Key(ix + dx, iz + dz); if (have.Contains(nk) && !comp.ContainsKey(nk)) { comp[nk] = nc; q.Enqueue(nk); } } }
            nc++;
        }
        if (nc < 2) return new List<Gap>();
        /* edge cells into a coarse bucket grid */
        float bs = Mathf.Max(maxDist, 1f); var buckets = new Dictionary<long, List<long>>(); var edge = new List<long>();
        foreach (var k in have)
        {
            int ix = (int)(k >> 32), iz = (int)(uint)k; bool e = false; for (int d4 = 0; d4 < 4 && !e; d4++) e = !have.Contains(TierraGeom.TriIndex.Key(ix + (d4 == 0 ? 1 : d4 == 1 ? -1 : 0), iz + (d4 == 2 ? 1 : d4 == 3 ? -1 : 0)));
            if (!e) continue; edge.Add(k); long bk = TierraGeom.TriIndex.Key(Mathf.FloorToInt(ix * step / bs), Mathf.FloorToInt(iz * step / bs)); List<long> l; if (!buckets.TryGetValue(bk, out l)) buckets[bk] = l = new List<long>(); l.Add(k);
        }
        var best = new Dictionary<long, Gap>();
        foreach (var k in edge)
        {
            int ix = (int)(k >> 32), iz = (int)(uint)k, ca = comp[k]; int bx = Mathf.FloorToInt(ix * step / bs), bz = Mathf.FloorToInt(iz * step / bs);
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                List<long> l; if (!buckets.TryGetValue(TierraGeom.TriIndex.Key(bx + dx, bz + dz), out l)) continue;
                foreach (var o in l)
                {
                    int cb = comp[o]; if (cb <= ca) continue; int ox = (int)(o >> 32), oz = (int)(uint)o; float d = Mathf.Sqrt((ox - ix) * (ox - ix) + (oz - iz) * (oz - iz)) * step; if (d > maxDist) continue;
                    long pk = ((long)ca << 32) | (uint)cb; Gap g; if (!best.TryGetValue(pk, out g) || d < g.d) best[pk] = new Gap { a = ca, b = cb, ax = ix * step, az = iz * step, bx = ox * step, bz = oz * step, d = d };
                }
            }
        }
        return best.Values.OrderBy(g => g.d).ToList();
    }

    // ------------------------------------------------------------------ scene side
    // Paths(): bumps and steps on the path surface, holes and slits in it, course turf over it, and terrain detail grass on it
    public string Paths()
    {
        var idx = new TierraGeom.TriIndex(1f); var foot = new HashSet<long>(); bool any = false; var roots = new List<string>();
        foreach (var rn in pathRoots.Split(','))
        {
            var root = GameObject.Find(rn.Trim()); if (!root) continue; roots.Add(rn.Trim());
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>(); if (!mf.sharedMesh || (r && !r.enabled)) continue; var tr = mf.transform; var w = mf.sharedMesh.vertices.Select(v => tr.TransformPoint(v)).ToArray(); var t = mf.sharedMesh.triangles; idx.Add(w, t, 0); any = true;
                /* grid points only under path triangles, not the whole course box */
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 p0 = w[t[i]], p1 = w[t[i + 1]], p2 = w[t[i + 2]];
                    int x0 = Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)) / step), x1 = Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)) / step);
                    int z0 = Mathf.FloorToInt(Mathf.Min(p0.z, Mathf.Min(p1.z, p2.z)) / step), z1 = Mathf.CeilToInt(Mathf.Max(p0.z, Mathf.Max(p1.z, p2.z)) / step);
                    if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) > 40000) continue;   /* a stray huge triangle */
                    for (int gx = x0; gx <= x1; gx++) for (int gz = z0; gz <= z1; gz++) foot.Add(TierraGeom.TriIndex.Key(gx, gz));
                }
            }
        }
        if (!any) return "no path meshes under " + pathRoots;
        var s = new List<Vector3>(); var have = new HashSet<long>(); var ys = new Dictionary<long, float>();
        foreach (var k in foot) { int gx = (int)(k >> 32), gz = (int)(uint)k; float x = gx * step, z = gz * step, y; int tg; if (idx.Top(x, z, out y, out tg)) { s.Add(new Vector3(x, y, z)); have.Add(k); ys[k] = y; } }
        int n; var ps = Bumps(s, out n);

        /* holes and slits up to maxGap wide */
        var holes = new Dictionary<long, float>(); foreach (var k in Holes(have, foot, Mathf.CeilToInt(maxGap / step))) holes[k] = 0;
        var hp = Group(holes, null);

        /* course turf (OPCD meshes) above the path surface */
        var over = new Dictionary<long, float>(); var overWhat = new Dictionary<long, string>(); var names = new List<string>();
        var opcd = GameObject.Find("OPCD Meshes");
        if (opcd)
        {
            var cidx = new TierraGeom.TriIndex(1f); Bounds pb = new Bounds(); bool f = true; foreach (var v in s) { if (f) { pb = new Bounds(v, Vector3.zero); f = false; } else pb.Encapsulate(v); }
            foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<Renderer>(); if (!mf.sharedMesh || (r && !r.enabled) || (r && !r.bounds.Intersects(pb))) continue; var tr = mf.transform;
                var w = mf.sharedMesh.vertices.Select(v => tr.TransformPoint(v)).ToArray(); names.Add(mf.name); int tag = names.Count - 1;
                cidx.Add(w, mf.sharedMesh.triangles, tag, (x0, z0, x1, z1) => { for (int gx = Mathf.FloorToInt(x0 / step); gx <= Mathf.CeilToInt(x1 / step); gx++) for (int gz = Mathf.FloorToInt(z0 / step); gz <= Mathf.CeilToInt(z1 / step); gz++) if (have.Contains(TierraGeom.TriIndex.Key(gx, gz))) return true; return false; });
            }
            foreach (var kv in ys)
            {
                int gx = (int)(kv.Key >> 32), gz = (int)(uint)kv.Key; float ty; int tg; if (!cidx.Top(gx * step, gz * step, out ty, out tg) || ty <= kv.Value + cover) continue;
                over[kv.Key] = ty - kv.Value; overWhat[kv.Key] = names[tg];
            }
        }
        var op = Group(over, overWhat);

        /* terrain detail grass on the path */
        var grass = new Dictionary<long, float>(); var grassWhat = new Dictionary<long, string>();
        foreach (var t in Terrain.activeTerrains)
        {
            var td = t.terrainData; if (!td || td.detailPrototypes == null) continue; int W = td.detailWidth, H = td.detailHeight; if (W <= 0 || H <= 0) continue; var o = t.transform.position;
            for (int l = 0; l < td.detailPrototypes.Length; l++)
            {
                var m = td.GetDetailLayer(0, 0, W, H, l); string nm = td.detailPrototypes[l].prototypeTexture ? td.detailPrototypes[l].prototypeTexture.name : td.detailPrototypes[l].prototype ? td.detailPrototypes[l].prototype.name : "detail " + l;
                foreach (var k in have)
                {
                    int gx = (int)(k >> 32), gz = (int)(uint)k; int ix = Mathf.FloorToInt((gx * step - o.x) / td.size.x * W), iz = Mathf.FloorToInt((gz * step - o.z) / td.size.z * H);
                    if (ix < 0 || iz < 0 || ix >= W || iz >= H || m[iz, ix] <= 0) continue; float was; if (!grass.TryGetValue(k, out was) || m[iz, ix] > was) { grass[k] = m[iz, ix]; grassWhat[k] = nm; }
                }
            }
        }
        var gp = Group(grass, grassWhat);

        /* breaks between path pieces */
        var gaps = Gaps(have, breakDist);

        var csv = new List<string> { "kind,x,z,area_m2,value,what" };
        foreach (var p in ps) csv.Add("bump," + p.x.ToString("F1", CI) + "," + p.z.ToString("F1", CI) + "," + (p.n * step * step).ToString("F2", CI) + "," + (p.worst * 100).ToString("F1", CI) + "cm/step " + (p.step * 100).ToString("F1", CI) + "cm,");
        foreach (var p in hp) csv.Add("hole," + p.x.ToString("F1", CI) + "," + p.z.ToString("F1", CI) + "," + (p.n * step * step).ToString("F2", CI) + ",,");
        foreach (var p in op) csv.Add("turf_over," + p.x.ToString("F1", CI) + "," + p.z.ToString("F1", CI) + "," + (p.n * step * step).ToString("F2", CI) + "," + (p.worst * 100).ToString("F1", CI) + "cm," + p.what);
        foreach (var g2 in gaps) csv.Add("break," + ((g2.ax + g2.bx) / 2).ToString("F1", CI) + "," + ((g2.az + g2.bz) / 2).ToString("F1", CI) + ",," + g2.d.ToString("F2", CI) + "m,(" + g2.ax.ToString("F1", CI) + " " + g2.az.ToString("F1", CI) + ") to (" + g2.bx.ToString("F1", CI) + " " + g2.bz.ToString("F1", CI) + ")");
        foreach (var p in gp) csv.Add("detail_grass," + p.x.ToString("F1", CI) + "," + p.z.ToString("F1", CI) + "," + (p.n * step * step).ToString("F2", CI) + "," + p.worst.ToString("F0", CI) + "," + p.what);
        System.Func<List<Patch>, System.Func<Patch, string>, string> list = (l, f2) => l.Count == 0 ? "" : ":\n" + string.Join("\n", l.Take(30).Select(f2).ToArray()) + (l.Count > 30 ? "\n+" + (l.Count - 30) + " more in path_check.csv" : "");
        System.Func<Patch, string> at = p => "(" + p.x.ToString("F1", CI) + ", " + p.z.ToString("F1", CI) + ") " + (p.n * step * step).ToString("F2", CI) + " m2";
        var txt = "Paths under " + string.Join(", ", roots.ToArray()) + ": " + (s.Count * step * step).ToString("F0", CI) + " m2, " + n + " samples checked"
            + "\n\nBumps and steps: " + ps.Count + " patches with a bump over " + (bump * 100).ToString("F1", CI) + " cm or a step over " + (stepTol * 100).ToString("F1", CI) + " cm" + list(ps, p => at(p) + ", bump " + (p.worst > 0 ? "+" : "") + (p.worst * 100).ToString("F1", CI) + " cm" + (p.step != 0 ? ", step " + (Mathf.Abs(p.step) * 100).ToString("F1", CI) + " cm" : ""))
            + "\n\nHoles and slits (up to " + maxGap.ToString("F1", CI) + " m wide): " + hp.Count + list(hp, at)
            + "\n\nCourse turf over the path (more than " + (cover * 100).ToString("F1", CI) + " cm above it): " + op.Count + (opcd ? "" : " (no OPCD Meshes)") + list(op, p => at(p) + ", up to " + (p.worst * 100).ToString("F1", CI) + " cm over, " + p.what)
            + "\n\nBreaks between path pieces (ends within " + breakDist.ToString("F0", CI) + " m that don't join): " + gaps.Count + (gaps.Count == 0 ? "" : ":\n" + string.Join("\n", gaps.Take(30).Select(g2 => "(" + g2.ax.ToString("F1", CI) + ", " + g2.az.ToString("F1", CI) + ") to (" + g2.bx.ToString("F1", CI) + ", " + g2.bz.ToString("F1", CI) + "), " + g2.d.ToString("F1", CI) + " m").ToArray()) + (gaps.Count > 30 ? "\n+" + (gaps.Count - 30) + " more in path_check.csv" : ""))
            + "\n\nTerrain detail grass on the path: " + gp.Count + list(gp, p => at(p) + ", density " + p.worst.ToString("F0", CI) + ", " + p.what);
        try { Directory.CreateDirectory("AutomationOutput"); File.WriteAllText("AutomationOutput/path_check.txt", txt); File.WriteAllLines("AutomationOutput/path_check.csv", csv.ToArray()); } catch (System.Exception e) { txt += "\n(could not write AutomationOutput: " + e.Message + ")"; }
        return txt;
    }

    [System.Serializable] class P { public float x, y, z; }
    [System.Serializable] class Tee { public string TeeType; public bool Enabled; public P Position; }
    [System.Serializable] class Hole { public bool Enabled; public int HoleNumber; public Tee[] Tees; }
    [System.Serializable] class G { public Hole[] Holes; }

    // JsonUtility turns a null position into (0, 0, 0)
    static bool Has(P p) { return p != null && (p.x != 0 || p.z != 0); }

    public string TeeMarkers()
    {
        Physics.SyncTransforms(); var opcdGo = GameObject.Find("OPCD Meshes"); if (!opcdGo) return "no OPCD Meshes in this scene"; var opcd = opcdGo.transform;
        var pathGo = GameObject.Find("CART_PATHS"); var paths = pathGo ? pathGo.GetComponentsInChildren<Collider>() : new Collider[0];
        System.Func<Vector2, KeyValuePair<string, float>> Top = q => { string nm = "none"; float y = float.MinValue; foreach (var h in Physics.RaycastAll(new Vector3(q.x, 600, q.y), Vector3.down, 1200)) { bool isP = paths.Contains(h.collider); if (!(isP || h.collider.transform.root == opcd)) continue; if (h.point.y > y) { y = h.point.y; nm = isP ? "PATH" : h.collider.name; } } return new KeyValuePair<string, float>(nm, y); };
        var g = JsonUtility.FromJson<G>(File.ReadAllText(gkd).TrimStart('\uFEFF'));
        var csv = new List<string> { "hole,tee,side,x,z,surface,ground_y,tee_y,diff_cm,flag" }; var bad = new List<string>(); int n = 0;
        foreach (var h in g.Holes)
        {
            if (!h.Enabled) continue; var aim = h.Tees.Where(t => t.Enabled && Has(t.Position) && t.TeeType.StartsWith("Aim")).Select(t => new Vector2(t.Position.x, t.Position.z)).ToList();
            var gc = h.Tees.FirstOrDefault(t => Has(t.Position) && t.TeeType == "GreenCenterPoint");   /* GreenKeeper keeps it disabled; the position is still the green centre */
            foreach (var t in h.Tees)
            {
                if (!Has(t.Position) || !t.Enabled || t.TeeType.StartsWith("Aim") || t.TeeType == "GreenCenterPoint") continue;
                var tee = new Vector2(t.Position.x, t.Position.z); var target = aim.Count > 0 ? aim.OrderBy(a => (a - tee).sqrMagnitude).First() : gc != null ? new Vector2(gc.Position.x, gc.Position.z) : tee + new Vector2(0, 1);
                var spots = MarkerSpots(tee, target, width);
                for (int k = 0; k < 2; k++)
                {
                    var top = Top(spots[k]); n++; float diff = top.Value - t.Position.y; var flags = new List<string>();
                    if (!top.Key.ToLower().Contains("tee")) flags.Add("NOT ON TEE (" + top.Key + ")"); if (top.Value > float.MinValue && Mathf.Abs(diff) > markerTol) flags.Add((diff > 0 ? "ground above the tee spot " : "ground below the tee spot ") + (Mathf.Abs(diff) * 100).ToString("F0", CI) + " cm");
                    csv.Add(string.Join(",", new[] { h.HoleNumber.ToString(), t.TeeType, k == 0 ? "left" : "right", spots[k].x.ToString("F2", CI), spots[k].y.ToString("F2", CI), top.Key, top.Value.ToString("F3", CI), t.Position.y.ToString("F3", CI), (diff * 100).ToString("F1", CI), string.Join("; ", flags.ToArray()) }));
                    if (flags.Count > 0) bad.Add("h" + h.HoleNumber + " " + t.TeeType + " " + (k == 0 ? "left" : "right") + " marker (" + spots[k].x.ToString("F1", CI) + ", " + spots[k].y.ToString("F1", CI) + "): " + string.Join(", ", flags.ToArray()));
                }
            }
        }
        var txt = n + " marker spots checked (assumed " + (width / 2).ToString("F1", CI) + " m either side of the tee spot); " + bad.Count + " flagged" + (bad.Count > 0 ? ":\n" + string.Join("\n", bad.ToArray()) : "");
        try { Directory.CreateDirectory("AutomationOutput"); File.WriteAllText("AutomationOutput/tee_markers.txt", txt); File.WriteAllLines("AutomationOutput/tee_markers.csv", csv.ToArray()); } catch (System.Exception e) { txt += "\n(could not write AutomationOutput: " + e.Message + ")"; }
        return txt;
    }
}
