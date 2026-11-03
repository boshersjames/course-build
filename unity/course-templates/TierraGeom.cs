using System.Collections.Generic;
using UnityEngine;

// Shared geometry for the course checks (TierraPinArea, TierraBunkerSeams, TierraWaterCheck) and the shaping curves from the 10/31
// review of Green / Bunker / Shoreline Studio. Our own code: those add-ons are GPL-3.0, so only their ideas and numbers were taken.
// Everything here is plain math on world-space arrays (no scene access), so it can be tested outside Unity.
public static class TierraGeom
{
    public const float Rad2Deg = 57.29578f;

    // ------------------------------------------------------------------ curves
    public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    public static float Smoother(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (6 * t - 15) + 10); }   // 6t^5 - 15t^4 + 10t^3

    // contour-edit weight at distance r from the edit: 1 inside the core, smootherstep down to 0 at `outer` (greens: core >= 1.25 m, outer >= 6 m)
    public static float Falloff(float r, float core, float outer)
    {
        if (r <= core) return 1;
        if (r >= outer || outer <= core) return 0;
        return 1 - Smoother((r - core) / (outer - core));
    }

    // pond bank: shelf width for a water depth at a bank slope (default 11 deg: 1.2 m deep -> ~6.2 m of shelf)
    public static float ShelfWidth(float depth, float slopeDeg) { return depth / Mathf.Tan(Mathf.Clamp(slopeDeg, 1f, 60f) / Rad2Deg); }
    // depth below the shoreline at distance d in from the shore: eased, never a straight wedge
    public static float BankDepth(float d, float depth, float slopeDeg) { float w = ShelfWidth(depth, slopeDeg); return depth * Smooth(d / Mathf.Max(w, 1e-4f)); }

    // creek bed (or water surface) sampled upstream first: each value clamped to <= the one upstream (never rises going downstream),
    // then `passes` smoothing passes (own 0.35, neighbours 0.65) with both ends pinned, clamped again after each pass
    public static float[] Downhill(float[] up, int passes = 3, float own = 0.35f)
    {
        var a = (float[])up.Clone();
        for (int i = 1; i < a.Length; i++) a[i] = Mathf.Min(a[i], a[i - 1]);
        for (int p = 0; p < passes; p++)
        {
            var b = (float[])a.Clone();
            for (int i = 1; i < a.Length - 1; i++) b[i] = own * a[i] + (1 - own) * 0.5f * (a[i - 1] + a[i + 1]);
            for (int i = 1; i < b.Length; i++) b[i] = Mathf.Min(b[i], b[i - 1]);
            a = b;
        }
        return a;
    }

    // bunker edge wobble at position s (0..1 round the rim) and t (0 at the seam, 1 at the toe of the face): a 3- and 7-cycle sine mix,
    // zero at the seam and the toe (envelope 4t(1-t)), the same for the same seed (rebuilds match). Returns the outward offset in metres
    // (cap min(0.20, 0.38 x face width)); scale the height wobble by 0.025 / cap.
    public static float Wobble(float s, float t, int seed, float faceWidth)
    {
        var r = new System.Random(seed); float p1 = (float)r.NextDouble() * 6.2831853f, p2 = (float)r.NextDouble() * 6.2831853f;
        float cap = Mathf.Min(0.20f, 0.38f * faceWidth);
        float w = 0.6f * Mathf.Sin(3 * 6.2831853f * s + p1) + 0.4f * Mathf.Sin(7 * 6.2831853f * s + p2);
        float env = 4 * Mathf.Clamp01(t) * (1 - Mathf.Clamp01(t));
        return cap * w * env;
    }

    // shoreline wobble only ever moves the water edge inward (positive = inward), so water never spreads onto turf
    public static float InwardOnly(float offset) { return Mathf.Max(0, offset); }

    // ------------------------------------------------------------------ edges and outlines
    static long EK(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }

    // boundary edges (used by one triangle) with the third vertex of that triangle, so the outward side is known
    public struct BEdge { public int a, b, c; }
    public static List<BEdge> BoundaryEdges(int[] tris)
    {
        var count = new Dictionary<long, int>(); var third = new Dictionary<long, BEdge>();
        for (int i = 0; i + 2 < tris.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = tris[i + e], b = tris[i + (e + 1) % 3], c = tris[i + (e + 2) % 3]; if (a == b) continue;
                long k = EK(a, b); int n; count.TryGetValue(k, out n); count[k] = n + 1; third[k] = new BEdge { a = a, b = b, c = c };
            }
        var o = new List<BEdge>(); foreach (var kv in count) if (kv.Value == 1) o.Add(third[kv.Key]); return o;
    }

    // outward unit normal (xz) of a boundary edge: perpendicular to ab, away from the third vertex
    public static Vector2 Outward(Vector3[] w, BEdge e)
    {
        var a = XZ(w[e.a]); var b = XZ(w[e.b]); var c = XZ(w[e.c]); var d = b - a; var n = new Vector2(d.y, -d.x);
        if (n.sqrMagnitude < 1e-12f) return Vector2.zero; n.Normalize(); if (Vector2.Dot(n, c - a) > 0) n = -n; return n;
    }

    public static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }

    public static float SegDist(Vector2 q, Vector2 a, Vector2 b)
    {
        var ab = b - a; float L = ab.sqrMagnitude; float t = L < 1e-12f ? 0 : Mathf.Clamp01(Vector2.Dot(q - a, ab) / L); return (a + ab * t - q).magnitude;
    }

    // segments in xz on a bucket grid, for "distance to the green edge" on thousands of points
    public class SegIndex
    {
        readonly float cell; readonly Dictionary<long, List<int>> b = new Dictionary<long, List<int>>(); readonly List<Vector2> A = new List<Vector2>(), B = new List<Vector2>();
        public SegIndex(float cell) { this.cell = cell; }
        public int Count { get { return A.Count; } }
        public void Add(Vector2 a, Vector2 bb)
        {
            int id = A.Count; A.Add(a); B.Add(bb);
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, bb.x) / cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, bb.x) / cell), z0 = Mathf.FloorToInt(Mathf.Min(a.y, bb.y) / cell), z1 = Mathf.FloorToInt(Mathf.Max(a.y, bb.y) / cell);
            for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) { List<int> l; long k = TriIndex.Key(x, z); if (!b.TryGetValue(k, out l)) b[k] = l = new List<int>(); l.Add(id); }
        }
        // nearest distance, searched out to maxR (returns maxR when nothing is closer)
        public float Dist(Vector2 q, float maxR)
        {
            float best = maxR; int r = Mathf.CeilToInt(maxR / cell); int cx = Mathf.FloorToInt(q.x / cell), cz = Mathf.FloorToInt(q.y / cell); var seen = new HashSet<int>();
            for (int x = cx - r; x <= cx + r; x++) for (int z = cz - r; z <= cz + r; z++) { List<int> l; if (!b.TryGetValue(TriIndex.Key(x, z), out l)) continue; foreach (int i in l) { if (!seen.Add(i)) continue; float d = SegDist(q, A[i], B[i]); if (d < best) best = d; } }
            return best;
        }
    }

    // ------------------------------------------------------------------ planes
    // least-squares plane y = a + b (x - ox) + c (z - oz); false if the points are (nearly) collinear in xz
    public static bool FitPlane(IList<Vector3> p, out float a, out float b, out float c, out float ox, out float oz)
    {
        a = b = c = 0; ox = oz = 0; int n = p.Count; if (n < 3) return false;
        double sx = 0, sz = 0; for (int i = 0; i < n; i++) { sx += p[i].x; sz += p[i].z; } ox = (float)(sx / n); oz = (float)(sz / n);
        double Sxx = 0, Sxz = 0, Szz = 0, Sx = 0, Sz = 0, Sy = 0, Sxy = 0, Szy = 0;
        for (int i = 0; i < n; i++) { double x = p[i].x - ox, z = p[i].z - oz, y = p[i].y; Sxx += x * x; Sxz += x * z; Szz += z * z; Sx += x; Sz += z; Sy += y; Sxy += x * y; Szy += z * y; }
        // centred: Sx = Sz = 0 (up to rounding), so a = mean y and [b c] solves the 2x2 normal equations
        double det = Sxx * Szz - Sxz * Sxz; if (det < 1e-9 * (Sxx + Szz + 1e-12) * (Sxx + Szz + 1e-12)) return false;
        b = (float)((Sxy * Szz - Szy * Sxz) / det); c = (float)((Szy * Sxx - Sxy * Sxz) / det); a = (float)(Sy / n - b * Sx / n - c * Sz / n); return true;
    }
    public static float PlaneY(float a, float b, float c, float ox, float oz, float x, float z) { return a + b * (x - ox) + c * (z - oz); }
    public static float SlopeDeg(float b, float c) { return Mathf.Atan(Mathf.Sqrt(b * b + c * c)) * Rad2Deg; }

    public static float Percentile(List<float> v, float q)
    {
        if (v.Count == 0) return float.NaN; var s = new List<float>(v); s.Sort(); int i = Mathf.Clamp(Mathf.RoundToInt(q * (s.Count - 1)), 0, s.Count - 1); return s[i];
    }

    // ------------------------------------------------------------------ "what is at (x, z)": world triangles bucketed in xz
    public class TriIndex
    {
        public struct Hit { public int tag; public float y; public int tri; }
        readonly float cell; readonly Dictionary<long, List<int>> b = new Dictionary<long, List<int>>();
        readonly List<Vector3> P = new List<Vector3>(); readonly List<int> Tag = new List<int>();
        public TriIndex(float cell) { this.cell = cell; }
        public static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }
        public int Triangles { get { return Tag.Count; } }
        public Vector3 Corner(int tri, int k) { return P[tri * 3 + k]; }
        public int TagOf(int tri) { return Tag[tri]; }

        // add triangles (world positions); `keep` filters by triangle bounds (e.g. only near the water), null = all
        public void Add(Vector3[] w, int[] tris, int tag, System.Func<float, float, float, float, bool> keep = null)
        {
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = w[tris[i]]; var bb = w[tris[i + 1]]; var c = w[tris[i + 2]];
                float x0 = Mathf.Min(a.x, Mathf.Min(bb.x, c.x)), x1 = Mathf.Max(a.x, Mathf.Max(bb.x, c.x)), z0 = Mathf.Min(a.z, Mathf.Min(bb.z, c.z)), z1 = Mathf.Max(a.z, Mathf.Max(bb.z, c.z));
                if (keep != null && !keep(x0, z0, x1, z1)) continue;
                int id = Tag.Count; Tag.Add(tag); P.Add(a); P.Add(bb); P.Add(c);
                for (int x = Mathf.FloorToInt(x0 / cell); x <= Mathf.FloorToInt(x1 / cell); x++)
                    for (int z = Mathf.FloorToInt(z0 / cell); z <= Mathf.FloorToInt(z1 / cell); z++) { List<int> l; long k = Key(x, z); if (!b.TryGetValue(k, out l)) b[k] = l = new List<int>(); l.Add(id); }
            }
        }

        // every surface over (x, z), any order
        public List<Hit> At(float x, float z)
        {
            var o = new List<Hit>(); List<int> l; if (!b.TryGetValue(Key(Mathf.FloorToInt(x / cell), Mathf.FloorToInt(z / cell)), out l)) return o;
            foreach (int id in l) { float y; if (Y(id, x, z, out y)) o.Add(new Hit { tag = Tag[id], y = y, tri = id }); }
            return o;
        }

        // highest surface over (x, z) among the tags `accept` allows (null = all); false when there is none
        public bool Top(float x, float z, out float y, out int tag, System.Func<int, bool> accept = null)
        {
            y = float.MinValue; tag = -1; bool any = false;
            foreach (var h in At(x, z)) { if (accept != null && !accept(h.tag)) continue; if (h.y > y) { y = h.y; tag = h.tag; any = true; } }
            return any;
        }

        bool Y(int id, float x, float z, out float y)
        {
            y = 0; var a = P[id * 3]; var bb = P[id * 3 + 1]; var c = P[id * 3 + 2];
            float d = (bb.z - c.z) * (a.x - c.x) + (c.x - bb.x) * (a.z - c.z); if (Mathf.Abs(d) < 1e-10f) return false;
            float l1 = ((bb.z - c.z) * (x - c.x) + (c.x - bb.x) * (z - c.z)) / d, l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d, l3 = 1 - l1 - l2;
            const float e = -1e-5f; if (l1 < e || l2 < e || l3 < e) return false;
            y = l1 * a.y + l2 * bb.y + l3 * c.y; return true;
        }
    }
}
