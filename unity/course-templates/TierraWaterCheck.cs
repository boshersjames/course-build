using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// Water check: our own version of the checks in Shoreline Studio (10/31 review; GPL-3.0, no code used) plus what Tierra taught us
// (the 13 fill's 6 m dry gap, coarse stream quads poking through). Read-only. Every water surface (material or physics named water,
// mostly facing up; falls and foam are skipped) is sampled on a 0.5 m grid against the course meshes (OPCD Meshes, water excluded) and the terrain:
//   dry       course ground above the water (buried water reads as grass) away from the under-bank edge, with the patches' centres
//   flipped   water triangles facing down (invisible from above)
//   ribbons   (long, narrow water: streams, creek fills) water surface and bed every 1 m from the upstream end; any rise going downstream
//             (surface > 1 cm, bed > 2 cm) is listed; spots where the ribbon is under 4 cm over the ground below it
//   ponds     freeboard (bank height 1 m out from the waterline; flat banks shimmer), exposed edge (ground below the water at its own edge),
//             depth and the bank shelf it implies at 11 deg (shelf = depth / tan(slope))
//   joins     water meshes that touch or overlap: how far one reaches under the other (rule >= 0.5 m), the height step, and dry gaps
//             between them where the ground is below both waters
//   GKD       water outside every penalty area, and water out of bounds (outside the course line or inside interior OB)
// Writes AutomationOutput/water_check.txt and water_check.csv; changes nothing.
public class TierraWaterCheck
{
    public string gkd = @"C:\Users\user\Documents\newcourses\tr-automation\greenkeeper\TierraRejada.GKD";
    public float cell = 0.5f, dryTol = 0.005f, riseSurface = 0.01f, riseBed = 0.02f, ribbonLift = 0.04f, minReach = 0.5f, pondUnderBank = 3f, minFreeboard = 0.03f, joinNear = 1.5f;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public class WM { public string name; public Vector3[] w; public int[] t; public Bounds b; }
    public static WM Of(string name, Vector3[] w, int[] t) { var m = new WM { name = name, w = w, t = t, b = new Bounds(w.Length > 0 ? w[0] : Vector3.zero, Vector3.zero) }; foreach (var p in w) m.b.Encapsulate(p); return m; }

    public class Cluster { public float x, z, maxBurial, maxIn; public int n; public bool touchesEdge; public List<long> keys = new List<long>(); }
    public class Rise { public float station, x, z, rise; }
    public class R
    {
        public string name, kind; public int cells, flipped, tris, dry, thin; public int dryRim; public float area, visibleM2, length, width, waterY, freeMed = float.NaN, freeP10 = float.NaN, exposedM, depth = float.NaN, shelf11 = float.NaN, outsideHazardM2, obM2;
        public List<Cluster> dryAt = new List<Cluster>(); public List<Rise> surfRises = new List<Rise>(), bedRises = new List<Rise>();
        public Dictionary<long, float> wy = new Dictionary<long, float>(), bed = new Dictionary<long, float>();
    }
    public class Join { public string a, b; public float reach, dy, gapM2; public int overlap; }

    static long K(int x, int z) { return TierraGeom.TriIndex.Key(x, z); }
    static int KX(long k) { return (int)(k >> 32); }
    static int KZ(long k, int x) { return (int)((uint)(k ^ ((long)x << 32))); }
    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

    // ------------------------------------------------------------------ core (no scene: testable)
    // hazards / ob: polygons in xz; ob = the course line (pOOB) first, then interior OB areas
    public List<Vector2[]> hazards = new List<Vector2[]>(), interiorOB = new List<Vector2[]>(); public Vector2[] courseLine;

    public List<R> CheckAll(List<WM> water, List<WM> course, System.Func<float, float, float> terrain, out List<Join> joins)
    {
        var area = new List<Bounds>(); foreach (var m in water) { var b = m.b; b.Expand(6f); area.Add(b); }
        var cidx = new TierraGeom.TriIndex(1f); for (int k = 0; k < course.Count; k++) { if (!area.Any(b => b.Intersects(course[k].b))) continue; cidx.Add(course[k].w, course[k].t, k, (x0, z0, x1, z1) => area.Any(b => x1 >= b.min.x && x0 <= b.max.x && z1 >= b.min.z && z0 <= b.max.z)); }
        System.Func<float, float, float> Ground = (x, z) => { float y; int tg; float tt = terrain != null ? terrain(x, z) : float.NaN; bool any = cidx.Top(x, z, out y, out tg); if (!any) return float.IsNaN(tt) ? float.MinValue : tt; return float.IsNaN(tt) ? y : Mathf.Max(y, tt); };
        System.Func<float, float, float, float> BedUnder = (x, z, wy) => { float best = float.MinValue; foreach (var h in cidx.At(x, z)) if (h.y <= wy + dryTol && h.y > best) best = h.y; float tt = terrain != null ? terrain(x, z) : float.NaN; if (!float.IsNaN(tt) && tt <= wy + dryTol && tt > best) best = tt; return best; };
        var rs = new List<R>();
        foreach (var m in water) rs.Add(One(m, Ground, BedUnder));
        joins = Joins(rs, Ground);
        return rs;
    }

    R One(WM m, System.Func<float, float, float> Ground, System.Func<float, float, float, float> BedUnder)
    {
        var r = new R { name = m.name, tris = m.t.Length / 3 };
        for (int i = 0; i + 2 < m.t.Length; i += 3) if (Vector3.Cross(m.w[m.t[i + 1]] - m.w[m.t[i]], m.w[m.t[i + 2]] - m.w[m.t[i]]).y < 0) r.flipped++;
        var widx = new TierraGeom.TriIndex(1f); widx.Add(m.w, m.t, 0);
        int x0 = Mathf.FloorToInt(m.b.min.x / cell), x1 = Mathf.FloorToInt(m.b.max.x / cell), z0 = Mathf.FloorToInt(m.b.min.z / cell), z1 = Mathf.FloorToInt(m.b.max.z / cell);
        var burial = new Dictionary<long, float>(); var ground = new Dictionary<long, float>();
        for (int ix = x0; ix <= x1; ix++) for (int iz = z0; iz <= z1; iz++)
        {
            float x = (ix + 0.5f) * cell, z = (iz + 0.5f) * cell, wy; int tg; if (!widx.Top(x, z, out wy, out tg)) continue;
            long k = K(ix, iz); r.wy[k] = wy; float g = Ground(x, z); ground[k] = g; if (g > wy + dryTol) burial[k] = g - wy;
            r.bed[k] = BedUnder(x, z, wy);
            if (hazards.Count > 0 && !InPolys(hazards, x, z)) r.outsideHazardM2 += cell * cell;
            if (courseLine != null && (!Inside(courseLine, new Vector2(x, z)) || InPolys(interiorOB, x, z))) r.obM2 += cell * cell;
        }
        r.cells = r.wy.Count; r.area = r.cells * cell * cell; if (r.cells == 0) { r.kind = "empty"; return r; }
        r.waterY = TierraGeom.Percentile(r.wy.Values.ToList(), 0.5f);

        // shape: geodesic length along the footprint
        var keys = r.wy.Keys.ToList(); var d0 = Geo(r.wy, keys[0]); long u = d0.OrderByDescending(kv => kv.Value).First().Key; var du = Geo(r.wy, u); long v = du.OrderByDescending(kv => kv.Value).First().Key; var dv = Geo(r.wy, v);
        r.length = du[v] * cell; r.width = r.area / Mathf.Max(r.length, cell); bool ribbon = r.length > 5 * r.width && r.width < 8;
        r.kind = ribbon ? "ribbon" : "pond";

        // buried water: ponds run ~2 m under their banks and ribbons tuck their edges under the channel sides, so buried cells along the
        // footprint's edge are by design. A buried patch counts when it doesn't touch the edge (ground poking through) or reaches into the
        // middle of the water (ponds: >= `pondUnderBank` in from the edge; ribbons: >= 35 % of the width in, i.e. across the channel).
        var dIn = InsideDist(r.wy); float allow = ribbon ? 0.35f * r.width : pondUnderBank;
        var bad = new Dictionary<long, float>();
        foreach (var c in Clusters(burial, dIn)) if (!c.touchesEdge) foreach (var k in c.keys) bad[k] = burial[k];          /* ground poking through */
        foreach (var kv in burial) { float di; if (dIn.TryGetValue(kv.Key, out di) && di * cell >= allow) bad[kv.Key] = kv.Value; }   /* too far in */
        r.dry = bad.Count; r.dryRim = burial.Count - bad.Count; r.dryAt = Clusters(bad, dIn);
        r.visibleM2 = (r.cells - burial.Count) * cell * cell;

        if (ribbon)
        {
            float wu = Mean(r.wy, du, 2f / cell), wv = Mean(r.wy, dv, 2f / cell); var st = wu >= wv ? du : dv;   /* upstream = the higher end */
            var bins = new SortedDictionary<int, List<long>>(); foreach (var kv in st) { if (burial.ContainsKey(kv.Key)) continue; int bi = Mathf.FloorToInt(kv.Value * cell / 1f); List<long> l; if (!bins.TryGetValue(bi, out l)) bins[bi] = l = new List<long>(); l.Add(kv.Key); }
            r.surfRises = Rises(bins, r.wy, riseSurface); r.bedRises = Rises(bins, r.bed.Where(kv => kv.Value > -1e5f).ToDictionary(kv => kv.Key, kv => kv.Value), riseBed);
            foreach (var kv in r.wy) { float bd; if (r.bed.TryGetValue(kv.Key, out bd) && bd > -1e5f && kv.Value - bd < ribbonLift && !burial.ContainsKey(kv.Key)) r.thin++; }
        }
        else
        {
            // freeboard: at every visible waterline cell (open water next to buried water or the footprint edge), the ground 1 m further out
            // above the water. Under ~3 cm the bank lies flat at water level (a shimmering band, water seeming to lap over the turf).
            var free = new List<float>(); int step = Mathf.Max(1, Mathf.RoundToInt(1f / cell));
            foreach (var k in keys)
            {
                if (burial.ContainsKey(k)) continue; int ix = KX(k), iz = KZ(k, ix);
                for (int d = 0; d < 4; d++)
                {
                    long q = K(ix + DX[d], iz + DZ[d]); if (r.wy.ContainsKey(q) && !burial.ContainsKey(q)) continue;
                    float g = Ground((ix + DX[d] * step + 0.5f) * cell, (iz + DZ[d] * step + 0.5f) * cell); if (g > -1e5f) free.Add(g - r.wy[k]);
                    /* the water mesh's own edge with open air under it: ground below the water here and just outside */
                    if (!r.wy.ContainsKey(q) && ground[k] < r.wy[k] && Ground((ix + DX[d] + 0.5f) * cell, (iz + DZ[d] + 0.5f) * cell) < r.wy[k]) r.exposedM += cell;
                    break;
                }
            }
            if (free.Count > 0) { r.freeMed = TierraGeom.Percentile(free, 0.5f); r.freeP10 = TierraGeom.Percentile(free, 0.1f); }
            var beds = r.bed.Values.Where(bb => bb > -1e5f).ToList(); if (beds.Count > 0) { r.depth = r.waterY - beds.Min(); r.shelf11 = TierraGeom.ShelfWidth(r.depth, 11f); }
        }
        return r;
    }

    List<Join> Joins(List<R> rs, System.Func<float, float, float> Ground)
    {
        var o = new List<Join>(); var dIn = rs.Select(r => InsideDist(r.wy)).ToList();
        for (int i = 0; i < rs.Count; i++) for (int j = i + 1; j < rs.Count; j++)
        {
            var A = rs[i]; var B = rs[j]; if (A.cells == 0 || B.cells == 0) continue;
            int nr = Mathf.Max(1, Mathf.RoundToInt(joinNear / cell)); var nearA = Dilate(new HashSet<long>(A.wy.Keys), nr); var nearB = Dilate(new HashSet<long>(B.wy.Keys), nr); if (!nearA.Overlaps(B.wy.Keys)) continue;
            var jn = new Join { a = A.name, b = B.name }; var ov = A.wy.Keys.Where(k => B.wy.ContainsKey(k)).ToList(); jn.overlap = ov.Count;
            foreach (var k in ov) jn.reach = Mathf.Max(jn.reach, Mathf.Max(dIn[i][k], dIn[j][k]) * cell);
            jn.dy = ov.Count > 0 ? TierraGeom.Percentile(ov.Select(k => A.wy[k] - B.wy[k]).ToList(), 0.5f) : float.NaN;
            var la = A.wy.Where(kv => nearB.Contains(kv.Key)).Select(kv => kv.Value).ToList(); var lb = B.wy.Where(kv => nearA.Contains(kv.Key)).Select(kv => kv.Value).ToList();
            if (la.Count > 0 && lb.Count > 0)
            {
                float lo = Mathf.Min(TierraGeom.Percentile(la, 0.5f), TierraGeom.Percentile(lb, 0.5f));
                foreach (var k in nearA) { if (!nearB.Contains(k) || A.wy.ContainsKey(k) || B.wy.ContainsKey(k)) continue; int ix = KX(k), iz = KZ(k, ix); float g = Ground((ix + 0.5f) * cell, (iz + 0.5f) * cell); if (g < lo) jn.gapM2 += cell * cell; }
            }
            o.Add(jn);
        }
        return o;
    }

    // ------------------------------------------------------------------ grid helpers
    // geodesic distance in cells (8-neighbour, diagonals sqrt 2) from `start` over the footprint
    static Dictionary<long, float> Geo(Dictionary<long, float> F, long start)
    {
        var d = new Dictionary<long, float> { { start, 0 } }; var h = new Heap(); h.Push(start, 0);
        while (h.Count > 0)
        {
            float dk; long k = h.Pop(out dk); if (dk > d[k] + 1e-6f) continue; int ix = KX(k), iz = KZ(k, ix);
            for (int n = 0; n < 8; n++) { long q = K(ix + DX[n], iz + DZ[n]); if (!F.ContainsKey(q)) continue; float nd = dk + (n < 4 ? 1f : 1.41421356f); float cur; if (!d.TryGetValue(q, out cur) || nd < cur - 1e-6f) { d[q] = nd; h.Push(q, nd); } }
        }
        return d;
    }
    // distance (cells) from each footprint cell to the nearest cell outside it
    static Dictionary<long, float> InsideDist(Dictionary<long, float> F)
    {
        var d = new Dictionary<long, float>(); var h = new Heap();
        foreach (var k in F.Keys) { int ix = KX(k), iz = KZ(k, ix); bool edge = false; for (int n = 0; n < 4 && !edge; n++) if (!F.ContainsKey(K(ix + DX[n], iz + DZ[n]))) edge = true; if (edge) { d[k] = 0.5f; h.Push(k, 0.5f); } }
        while (h.Count > 0)
        {
            float dk; long k = h.Pop(out dk); if (dk > d[k] + 1e-6f) continue; int ix = KX(k), iz = KZ(k, ix);
            for (int n = 0; n < 8; n++) { long q = K(ix + DX[n], iz + DZ[n]); if (!F.ContainsKey(q)) continue; float nd = dk + (n < 4 ? 1f : 1.41421356f); float cur; if (!d.TryGetValue(q, out cur) || nd < cur - 1e-6f) { d[q] = nd; h.Push(q, nd); } }
        }
        return d;
    }
    static HashSet<long> Dilate(HashSet<long> set, int r)
    {
        /* only edge cells need growing: the inside is covered by the footprint itself */
        var s = new HashSet<long>(set);
        foreach (var k in set) { int ix = KX(k), iz = KZ(k, ix); bool edge = false; for (int n = 0; n < 4 && !edge; n++) if (!set.Contains(K(ix + DX[n], iz + DZ[n]))) edge = true; if (!edge) continue; for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++) s.Add(K(ix + dx, iz + dz)); }
        return s;
    }
    static float Mean(Dictionary<long, float> wy, Dictionary<long, float> dist, float within) { var l = dist.Where(kv => kv.Value <= within).Select(kv => wy[kv.Key]).ToList(); return l.Count > 0 ? l.Average() : float.MinValue; }
    List<Rise> Rises(SortedDictionary<int, List<long>> bins, Dictionary<long, float> val, float tol)
    {
        var o = new List<Rise>(); float lowest = float.MaxValue;
        foreach (var kv in bins)
        {
            var l = kv.Value.Where(val.ContainsKey).ToList(); if (l.Count == 0) continue; float med = TierraGeom.Percentile(l.Select(k => val[k]).ToList(), 0.5f);
            if (med > lowest + tol) { var mid = l[l.Count / 2]; int ix = KX(mid); o.Add(new Rise { station = kv.Key, x = (ix + 0.5f) * cell, z = (KZ(mid, ix) + 0.5f) * cell, rise = med - lowest }); }
            lowest = Mathf.Min(lowest, med);
        }
        return o;
    }
    List<Cluster> Clusters(Dictionary<long, float> burial, Dictionary<long, float> dIn)
    {
        var o = new List<Cluster>(); var seen = new HashSet<long>();
        foreach (var s in burial.Keys)
        {
            if (seen.Contains(s)) continue; var c = new Cluster(); var q = new Queue<long>(); q.Enqueue(s); seen.Add(s); float sx = 0, sz = 0;
            while (q.Count > 0)
            {
                var k = q.Dequeue(); int ix = KX(k), iz = KZ(k, ix); c.n++; c.keys.Add(k); sx += (ix + 0.5f) * cell; sz += (iz + 0.5f) * cell; c.maxBurial = Mathf.Max(c.maxBurial, burial[k]);
                float di; if (dIn.TryGetValue(k, out di)) { c.maxIn = Mathf.Max(c.maxIn, di * cell); if (di <= 0.5f) c.touchesEdge = true; }
                for (int n = 0; n < 8; n++) { long nq = K(ix + DX[n], iz + DZ[n]); if (burial.ContainsKey(nq) && seen.Add(nq)) q.Enqueue(nq); }
            }
            c.x = sx / c.n; c.z = sz / c.n; o.Add(c);
        }
        return o.OrderByDescending(c => c.n).ToList();
    }
    public static bool Inside(Vector2[] p, Vector2 q) { bool r = false; for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) if ((p[i].y > q.y) != (p[j].y > q.y) && q.x < (p[j].x - p[i].x) * (q.y - p[i].y) / (p[j].y - p[i].y) + p[i].x) r = !r; return r; }
    static bool InPolys(List<Vector2[]> ps, float x, float z) { var q = new Vector2(x, z); foreach (var p in ps) if (Inside(p, q)) return true; return false; }

    class Heap
    {
        readonly List<KeyValuePair<long, float>> a = new List<KeyValuePair<long, float>>(); public int Count { get { return a.Count; } }
        public void Push(long k, float d) { a.Add(new KeyValuePair<long, float>(k, d)); int i = a.Count - 1; while (i > 0) { int p = (i - 1) / 2; if (a[p].Value <= a[i].Value) break; var t = a[p]; a[p] = a[i]; a[i] = t; i = p; } }
        public long Pop(out float d)
        {
            var top = a[0]; d = top.Value; var last = a[a.Count - 1]; a.RemoveAt(a.Count - 1); if (a.Count == 0) return top.Key; a[0] = last; int i = 0;
            while (true) { int l = 2 * i + 1, r = l + 1, m = i; if (l < a.Count && a[l].Value < a[m].Value) m = l; if (r < a.Count && a[r].Value < a[m].Value) m = r; if (m == i) break; var t = a[m]; a[m] = a[i]; a[i] = t; i = m; }
            return top.Key;
        }
    }

    // ------------------------------------------------------------------ report
    public string Write(List<R> rs, List<Join> js)
    {
        var csv = new List<string> { "water,kind,area_m2,visible_m2,length_m,width_m,water_y,dry_cells,dry_clusters,under_bank_cells,flipped_tris,surface_rises,bed_rises,thin_cells,freeboard_median,freeboard_p10,exposed_edge_m,depth_m,shelf_11deg_m,outside_hazard_m2,ob_m2" };
        var txt = new List<string>();
        foreach (var r in rs)
        {
            csv.Add(string.Join(",", new[] { r.name, r.kind, r.area.ToString("F1", CI), r.visibleM2.ToString("F1", CI), r.length.ToString("F1", CI), r.width.ToString("F1", CI), r.waterY.ToString("F3", CI), r.dry.ToString(), r.dryAt.Count.ToString(), r.dryRim.ToString(), r.flipped.ToString(), r.surfRises.Count.ToString(), r.bedRises.Count.ToString(), r.thin.ToString(), r.freeMed.ToString("F3", CI), r.freeP10.ToString("F3", CI), r.exposedM.ToString("F1", CI), r.depth.ToString("F2", CI), r.shelf11.ToString("F1", CI), r.outsideHazardM2.ToString("F1", CI), r.obM2.ToString("F1", CI) }));
            var f = new List<string>();
            if (r.dry > 0) f.Add((r.dry * cell * cell).ToString("F1", CI) + " m2 of water under the ground (" + string.Join("; ", r.dryAt.Take(4).Select(c => "(" + c.x.ToString("F1", CI) + ", " + c.z.ToString("F1", CI) + ") " + (c.n * cell * cell).ToString("F1", CI) + " m2, up to " + (c.maxBurial * 100).ToString("F0", CI) + " cm under").ToArray()) + (r.dryAt.Count > 4 ? "; +" + (r.dryAt.Count - 4) + " more" : "") + ")");
            if (r.flipped > 0) f.Add(r.flipped + " of " + r.tris + " triangles face down");
            if (r.surfRises.Count > 0) f.Add("surface rises downstream at " + string.Join("; ", r.surfRises.Take(3).Select(q => q.station + " m (" + q.x.ToString("F1", CI) + ", " + q.z.ToString("F1", CI) + ") +" + (q.rise * 100).ToString("F0", CI) + " cm").ToArray()));
            if (r.bedRises.Count > 0) f.Add("bed rises downstream at " + r.bedRises.Count + " stations (worst +" + (r.bedRises.Max(q => q.rise) * 100).ToString("F0", CI) + " cm)");
            if (r.thin > 0) f.Add((r.thin * cell * cell).ToString("F1", CI) + " m2 under " + (ribbonLift * 100).ToString("F0", CI) + " cm over the ground below");
            if (r.kind == "pond" && !float.IsNaN(r.freeP10) && r.freeP10 < minFreeboard) f.Add("flat shore: 10 % of the waterline has under " + (minFreeboard * 100).ToString("F0", CI) + " cm of bank 1 m out (p10 " + (r.freeP10 * 100).ToString("F1", CI) + " cm)");
            if (r.exposedM > 0.5f) f.Add(r.exposedM.ToString("F1", CI) + " m of exposed water edge");
            if (r.outsideHazardM2 > 0.25f) f.Add(r.outsideHazardM2.ToString("F1", CI) + " m2 outside every penalty area");
            if (r.obM2 > 0.25f && r.obM2 < r.area - 0.25f) f.Add(r.obM2.ToString("F1", CI) + " m2 out of bounds (partly OB)");
            if (f.Count > 0) txt.Add(r.name + " (" + r.kind + ", " + r.area.ToString("F0", CI) + " m2" + (r.kind == "pond" && !float.IsNaN(r.depth) ? ", depth " + r.depth.ToString("F2", CI) + " m, 11 deg shelf " + r.shelf11.ToString("F1", CI) + " m" : "") + "): " + string.Join(", ", f.ToArray()));
        }
        foreach (var j in js)
        {
            var f = new List<string>(); if (j.overlap == 0) f.Add("touch without overlap"); else if (j.reach < minReach) f.Add("overlap reaches only " + j.reach.ToString("F2", CI) + " m under (rule " + minReach.ToString(CI) + ")"); if (j.gapM2 > 0.25f) f.Add(j.gapM2.ToString("F1", CI) + " m2 dry gap between them");
            csv.Add("JOIN " + j.a + " / " + j.b + ",join,,,,," + j.overlap + ",,,,,,,,,," + j.reach.ToString("F2", CI) + "," + j.gapM2.ToString("F1", CI) + "," + j.dy.ToString("F3", CI));
            if (f.Count > 0) txt.Add("join " + j.a + " / " + j.b + " (step " + (float.IsNaN(j.dy) ? "-" : (j.dy * 100).ToString("F0", CI) + " cm") + "): " + string.Join(", ", f.ToArray()));
        }
        var head = rs.Count + " water surfaces, " + js.Count + " joins; " + txt.Count + " with findings; water under the ground " + (rs.Sum(r => r.dry) * cell * cell).ToString("F1", CI) + " m2 (under-bank edges not counted)" + ", flipped " + rs.Sum(r => r.flipped) + ", downstream rises " + rs.Sum(r => r.surfRises.Count + r.bedRises.Count);
        var all = head + "\n" + string.Join("\n", txt.ToArray());
        try { Directory.CreateDirectory("AutomationOutput"); File.WriteAllText("AutomationOutput/water_check.txt", all); File.WriteAllLines("AutomationOutput/water_check.csv", csv.ToArray()); } catch (System.Exception e) { all += "\n(could not write AutomationOutput: " + e.Message + ")"; }
        return all;
    }

    // ------------------------------------------------------------------ scene side
    [System.Serializable] class P { public float x, y, z; }
    [System.Serializable] class Hz { public P[] coords; public bool innerOOB; }
    [System.Serializable] class Ob { public P[] coords; }
    [System.Serializable] class G { public Hz[] Hazards; public Ob pOOB; }
    static Vector2[] Poly(P[] c) { return c == null ? new Vector2[0] : c.Select(p => new Vector2(p.x, p.z)).ToArray(); }

    static bool IsWater(MeshFilter mf)
    {
        var n = mf.name.ToLower(); if (n.Contains("fall") || n.Contains("foam") || n.Contains("wet") || n.Contains("lip") || n.Contains("curb") || n.Contains("edging") || n.Contains("base") || n.Contains("_-_blend")) return false;   /* B_Water_Base_* is the lake bed */
        var r = mf.GetComponent<Renderer>(); var mc = mf.GetComponent<MeshCollider>();
        bool mat = r && r.sharedMaterials != null && r.sharedMaterials.Any(m => m && m.name.ToLower().Contains("water")); bool phys = mc && mc.sharedMaterial && mc.sharedMaterial.name.ToLower().Contains("water");
        return mat || phys;
    }

    public string Run()
    {
        Physics.SyncTransforms(); var opcdGo = GameObject.Find("OPCD Meshes"); var opcd = opcdGo ? opcdGo.transform : null;
        var water = new List<WM>(); var course = new List<WM>();
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            var r = mf.GetComponent<Renderer>(); if (!mf.sharedMesh || !mf.gameObject.activeInHierarchy || (r && !r.enabled)) continue;
            var tr = mf.transform; bool isW = IsWater(mf); bool isC = !isW && opcd && tr.IsChildOf(opcd) && !mf.name.StartsWith("SKIRT_") && !mf.name.Contains("_-_Blend");   /* blends are see-through overlays */
            if (!isW && !isC) continue;   /* trees, shrubs, props: never read their vertices */
            var w = mf.sharedMesh.vertices.Select(v => tr.TransformPoint(v)).ToArray(); var t = mf.sharedMesh.triangles; if (t.Length == 0) continue;
            if (isW)
            {
                double up = 0, all = 0; for (int i = 0; i + 2 < t.Length; i += 3) { var c = Vector3.Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]); all += c.magnitude; up += Mathf.Abs(c.y); }
                if (all > 0 && up / all > 0.8) water.Add(Of(tr.root.name + "/" + mf.name, w, t));
            }
            else course.Add(Of(mf.name, w, t));
        }
        if (File.Exists(gkd))
        {
            var g = JsonUtility.FromJson<G>(File.ReadAllText(gkd).TrimStart('\uFEFF'));
            if (g != null && g.Hazards != null) foreach (var h in g.Hazards) (h.innerOOB ? interiorOB : hazards).Add(Poly(h.coords));
            if (g != null && g.pOOB != null && g.pOOB.coords != null && g.pOOB.coords.Length > 2) courseLine = Poly(g.pOOB.coords);
        }
        var terr = Terrain.activeTerrains;
        System.Func<float, float, float> T = (x, z) => { foreach (var te in terr) { var p = te.transform.position; var s = te.terrainData.size; if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z) return te.SampleHeight(new Vector3(x, 0, z)) + p.y; } return float.NaN; };
        List<Join> js; var rs = CheckAll(water, course, T, out js);
        return Write(rs, js);
    }
}
