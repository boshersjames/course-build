// Moorpark > Build Clubhouse: the clubhouse and its smaller left wing from the OSM footprints, with wall and roof heights
// from the 2018 LiDAR (moorpark/buildings/buildings.json, tools/building_heights.py), styled after BoJa's photos from
// Canyon Crest 9: big grey hipped roofs, white dormers, a white cupola on the ridge, brick walls on a stone base and
// tall white-framed windows. Walls follow the footprint; each roof is a hip over the footprint's oriented rectangle.
// Unity 2018.2 compatible.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkClubhouse
{
    public static string Json = "C:/Users/user/Documents/moorpark/buildings/buildings.json";
    const string Dir = "Assets/Moorpark/generated/clubhouse";
    const string Root = "CLUBHOUSE";

    // footprint id -> wall height, ridge above ground, dormers per long side, cupola size (0 = none). Wall and ridge
    // heights from the LiDAR (eave returns + overhang give the wall top, the 99th percentile minus the cupola the ridge).
    static readonly Dictionary<string, float[]> Spec = new Dictionary<string, float[]>
    {
        { "931609673", new[] { 4.6f, 9.8f, 6f, 4.4f } },    // main clubhouse, 83 x 31 m, cupola top ~11.5 m
        { "931609674", new[] { 3.8f, 8.6f, 2f, 0f } },      // the smaller left wing
        { "931609675", new[] { 3.8f, 6.9f, 0f, 2.6f } },    // pavilion beyond it, small cupola
        { "931609676", new[] { 3.4f, 5.6f, 0f, 0f } },
    };
    const float Overhang = 0.9f, StoneBand = 1.0f;

    [System.Serializable] class B { public string id; public float base_y; public float[][] ring; }

    static Material brick, stone, roof, white, glass, trim;

    [MenuItem("Moorpark/Build Clubhouse")]
    public static string Build()
    {
        Directory.CreateDirectory(Dir);
        Materials();
        var old = GameObject.Find(Root); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(Root);
        var log = new System.Text.StringBuilder();
        string txt = File.ReadAllText(Json);
        var phys = Resources.Load<PhysicMaterial>("Physics/GSPstone");
        foreach (var b in ReadBuildings(txt))
        {
            float[] sp; if (!Spec.TryGetValue(b.id, out sp)) continue;
            var go = new GameObject("Building_" + b.id); go.transform.SetParent(root.transform);
            var parts = new Dictionary<Material, MeshBuilder>();
            System.Func<Material, MeshBuilder> M = m => { MeshBuilder mb; if (!parts.TryGetValue(m, out mb)) parts[m] = mb = new MeshBuilder(); return mb; };
            Walls(b, sp[0], M);
            float ridgeY = Roof(b, sp[0], sp[1], (int)sp[2], sp[3], M);
            foreach (var kv in parts)
            {
                var child = new GameObject(kv.Key.name); child.transform.SetParent(go.transform);
                var mesh = kv.Value.ToMesh(b.id + "_" + kv.Key.name);
                AssetDatabase.CreateAsset(mesh, Dir + "/" + b.id + "_" + kv.Key.name + ".asset");
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
                var mc = child.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = phys;
                child.isStatic = true;
            }
            log.AppendLine("Building_" + b.id + ": walls " + sp[0] + " m, ridge " + (ridgeY - b.base_y).ToString("0.0") + " m above ground");
        }
        log.AppendLine(ClearTrees(txt) + " trees removed from inside or within 3 m of the buildings");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        return log.ToString();
    }

    static int ClearTrees(string txt)
    {
        var trees = GameObject.Find("LIDAR_TREES"); if (trees == null) return 0;
        var rings = new List<List<Vector2>>();
        foreach (var b in ReadBuildings(txt)) if (Spec.ContainsKey(b.id)) rings.Add(Ring(b));
        var kill = new List<GameObject>();
        foreach (Transform t in trees.transform)
        {
            var q = new Vector2(t.position.x, t.position.z);
            foreach (var r in rings) if (Inside(r, q) || EdgeDistance(r, q) < 3f) { kill.Add(t.gameObject); break; }
        }
        foreach (var g in kill) Object.DestroyImmediate(g);
        return kill.Count;
    }
    static bool Inside(List<Vector2> r, Vector2 p)
    {
        bool c = false;
        for (int i = 0, j = r.Count - 1; i < r.Count; j = i++)
            if ((r[i].y > p.y) != (r[j].y > p.y) && p.x < (r[j].x - r[i].x) * (p.y - r[i].y) / (r[j].y - r[i].y) + r[i].x) c = !c;
        return c;
    }
    static float EdgeDistance(List<Vector2> r, Vector2 p)
    {
        float best = 1e9f;
        for (int i = 0; i < r.Count; i++)
        {
            Vector2 a = r[i], b = r[(i + 1) % r.Count], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            best = Mathf.Min(best, (a + ab * t - p).magnitude);
        }
        return best;
    }

    // ---------------------------------------------------------------- walls and windows
    static void Walls(B b, float h, System.Func<Material, MeshBuilder> M)
    {
        var ring = Ring(b);
        float y0 = b.base_y - 0.6f, y1 = b.base_y + h, ys = b.base_y + StoneBand;
        bool ccw = Area(ring) > 0;
        for (int i = 0; i < ring.Count; i++)
        {
            Vector2 a = ring[i], c = ring[(i + 1) % ring.Count];
            float L = (c - a).magnitude; if (L < 0.1f) continue;
            Vector2 d = (c - a) / L, n = ccw ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x);   // outward
            Quad(M(stone), a, c, y0, ys, n, 0f);
            Quad(M(brick), a, c, ys, y1, n, 0f);
            // tall windows: 1.8 m wide, from 0.5 m to 0.6 m under the eave, about every 3.4 m on walls of 5 m or more
            if (L < 5f) continue;
            int k = Mathf.FloorToInt((L - 1.6f) / 3.4f);
            float wy0 = b.base_y + 0.5f, wy1 = y1 - 0.6f;
            for (int j = 0; j < k; j++)
            {
                float t = (j + 0.5f) / k * L;
                Vector2 p = a + d * t;
                Quad(M(white), p - d * 1.05f, p + d * 1.05f, wy0 - 0.12f, wy1 + 0.12f, n, 0.03f);
                Quad(M(glass), p - d * 0.9f, p + d * 0.9f, wy0, wy1, n, 0.05f);
                Quad(M(white), p - d * 0.05f, p + d * 0.05f, wy0, wy1, n, 0.07f);                       // mullion
                for (float yy = wy0 + 1.1f; yy < wy1 - 0.3f; yy += 1.1f)                                  // transoms
                    Quad(M(white), p - d * 0.9f, p + d * 0.9f, yy - 0.04f, yy + 0.04f, n, 0.07f);
            }
        }
    }

    // vertical quad from a to c, y0..y1, pushed out along n by off, facing n; UVs in metres
    static void Quad(MeshBuilder mb, Vector2 a, Vector2 c, float y0, float y1, Vector2 n, float off)
    {
        Vector3 o = new Vector3(n.x, 0, n.y) * off;
        Vector3 p0 = new Vector3(a.x, y0, a.y) + o, p1 = new Vector3(c.x, y0, c.y) + o, p2 = new Vector3(c.x, y1, c.y) + o, p3 = new Vector3(a.x, y1, a.y) + o;
        float L = (c - a).magnitude;
        mb.Quad(p0, p1, p2, p3, new Vector2(0, y0), new Vector2(L, y0), new Vector2(L, y1), new Vector2(0, y1), new Vector3(n.x, 0, n.y));
    }

    // ---------------------------------------------------------------- hip roof, dormers, cupola
    static float Roof(B b, float wallH, float ridgeH, int dormers, float cupola, System.Func<Material, MeshBuilder> M)
    {
        var ring = Ring(b);
        Vector2 c, ax, ay; float hx, hy;
        OrientedRect(ring, out c, out ax, out ay, out hx, out hy);      // hx >= hy: ax along the ridge
        hx += Overhang; hy += Overhang;
        float eaveY = b.base_y + wallH - 0.15f;                          // slightly under the wall top so no gap shows
        float rise = ridgeH - wallH;
        float slope = rise / hy;
        float ridgeHalf = Mathf.Max(0f, hx - hy);                      // 45-degree hips in plan
        Vector2 c_ = c, ax_ = ax, ay_ = ay;
        System.Func<float, float, float, Vector3> E = (u, v, y) => { var p = c_ + ax_ * u + ay_ * v; return new Vector3(p.x, y, p.y); };
        float ry = eaveY + rise;
        Vector3 a = E(-hx, -hy, eaveY), bb = E(hx, -hy, eaveY), cc = E(hx, hy, eaveY), d = E(-hx, hy, eaveY);
        Vector3 r0 = E(-ridgeHalf, 0, ry), r1 = E(ridgeHalf, 0, ry);
        var mb = M(roof);
        mb.Poly(new[] { a, bb, r1, r0 });          // long sides
        mb.Poly(new[] { cc, d, r0, r1 });
        mb.Poly(new[] { bb, cc, r1 });             // hip ends
        mb.Poly(new[] { d, a, r0 });
        mb.Poly(new[] { d, cc, bb, a });           // soffit (faces down)
        // fascia: a white board round the eaves
        var wb = M(white);
        Vector3[] eav = { a, bb, cc, d };
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = eav[i], q = eav[(i + 1) % 4];
            wb.Poly(new[] { p - Vector3.up * 0.3f, q - Vector3.up * 0.3f, q, p });
        }
        // dormers along both long sides, in the middle 70% of the ridge, a third of the way up the slope
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < dormers; i++)
            {
                float u = Mathf.Lerp(-hx * 0.62f, hx * 0.62f, dormers == 1 ? 0.5f : i / (float)(dormers - 1));
                float v = side * hy * 0.55f;                            // front face this far from the ridge line
                Dormer(c, ax, ay * side, u, Mathf.Abs(v), eaveY, slope, hy, M);
            }
        if (cupola > 0f) Cupola(E(0, 0, ry - 0.3f), ax, ay, cupola, M);
        return ry;
    }

    // a white dormer: box 2.6 m wide, front wall at distance v from the ridge line, gable roof running back into the slope
    static void Dormer(Vector2 c, Vector2 ax, Vector2 ay, float u, float v, float eaveY, float slope, float hy, System.Func<Material, MeshBuilder> M)
    {
        float w = 1.3f, back = 3.2f;
        float roofAtFront = eaveY + (hy - v) * slope;                  // roof surface height at the dormer's front wall
        float y0 = roofAtFront - 0.2f, y1 = roofAtFront + 1.7f, yr = y1 + 0.9f;
        System.Func<float, float, float, Vector3> P = (du, dv, y) => { var p = c + ax * (u + du) + ay * (v + dv); return new Vector3(p.x, y, p.y); };
        var wh = M(White()); var rf = M(roof); var gl = M(glass);
        // front wall with gable, and side walls back into the roof
        wh.Poly(new[] { P(-w, 0, y0), P(w, 0, y0), P(w, 0, y1), P(0, 0, yr), P(-w, 0, y1) });
        wh.Poly(new[] { P(w, 0, y0), P(w, -back, y0 + back * slope), P(w, -back, y1), P(w, 0, y1) });
        wh.Poly(new[] { P(-w, -back, y0 + back * slope), P(-w, 0, y0), P(-w, 0, y1), P(-w, -back, y1) });
        // window
        Vector2 nrm2 = ay; Vector3 nrm = new Vector3(nrm2.x, 0, nrm2.y) * 0.04f;
        gl.Poly(new[] { P(-0.7f, 0, y0 + 0.35f) + nrm, P(0.7f, 0, y0 + 0.35f) + nrm, P(0.7f, 0, y1 - 0.15f) + nrm, P(-0.7f, 0, y1 - 0.15f) + nrm });
        wh.Poly(new[] { P(-0.04f, 0, y0 + 0.35f) + nrm * 2, P(0.04f, 0, y0 + 0.35f) + nrm * 2, P(0.04f, 0, y1 - 0.15f) + nrm * 2, P(-0.04f, 0, y1 - 0.15f) + nrm * 2 });
        // gable roof, 0.25 m overhang
        float o = 0.25f;
        rf.Poly(new[] { P(-w - o, o, y1 - o * 0.7f), P(0, o, yr), P(0, -back - 0.5f, yr), P(-w - o, -back - 0.5f, y1 - o * 0.7f) });
        rf.Poly(new[] { P(0, o, yr), P(w + o, o, y1 - o * 0.7f), P(w + o, -back - 0.5f, y1 - o * 0.7f), P(0, -back - 0.5f, yr) });
    }

    static Material White() { return white; }

    // a square white cupola with dark louvred openings, a grey pyramid roof and a finial
    static void Cupola(Vector3 baseCentre, Vector2 ax, Vector2 ay, float s, System.Func<Material, MeshBuilder> M)
    {
        float h = s * 0.7f, half = s / 2f;
        Vector3 X = new Vector3(ax.x, 0, ax.y), Z = new Vector3(ay.x, 0, ay.y);
        System.Func<float, float, float, Vector3> C = (u, v, y) => baseCentre + X * u + Z * v + Vector3.up * y;
        var wh = M(white); var gl = M(glass); var rf = M(roof);
        Vector3[] k = { C(-half, -half, 0), C(half, -half, 0), C(half, half, 0), C(-half, half, 0) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = k[i], q = k[(i + 1) % 4];
            wh.Poly(new[] { p, q, q + Vector3.up * h, p + Vector3.up * h });
            Vector3 mid = (p + q) / 2, along = (q - p).normalized, outw = Vector3.Cross(Vector3.up, along) * -0.03f;
            if (Vector3.Dot(outw, mid - baseCentre) < 0) outw = -outw;
            gl.Poly(new[] { mid - along * half * 0.6f + Vector3.up * h * 0.25f + outw, mid + along * half * 0.6f + Vector3.up * h * 0.25f + outw,
                            mid + along * half * 0.6f + Vector3.up * h * 0.85f + outw, mid - along * half * 0.6f + Vector3.up * h * 0.85f + outw });
        }
        float o = 0.3f, top = h + s * 0.55f;
        Vector3[] e = { C(-half - o, -half - o, h), C(half + o, -half - o, h), C(half + o, half + o, h), C(-half - o, half + o, h) };
        Vector3 apex = C(0, 0, top);
        for (int i = 0; i < 4; i++) rf.Poly(new[] { e[i], e[(i + 1) % 4], apex });
        rf.Poly(new[] { e[3], e[2], e[1], e[0] });
        // finial
        wh.Poly(new[] { apex - X * 0.05f, apex + X * 0.05f, apex + X * 0.05f + Vector3.up * 1.0f, apex - X * 0.05f + Vector3.up * 1.0f });
        wh.Poly(new[] { apex - Z * 0.05f, apex + Z * 0.05f, apex + Z * 0.05f + Vector3.up * 1.0f, apex - Z * 0.05f + Vector3.up * 1.0f });
    }

    // ---------------------------------------------------------------- geometry helpers
    static List<Vector2> Ring(B b) { var r = new List<Vector2>(); foreach (var p in b.ring) r.Add(new Vector2(p[0], p[1])); return r; }
    static float Area(List<Vector2> r) { float a = 0; for (int i = 0; i < r.Count; i++) { var p = r[i]; var q = r[(i + 1) % r.Count]; a += p.x * q.y - q.x * p.y; } return a / 2; }

    // smallest-area rectangle aligned with one of the ring's edges
    static void OrientedRect(List<Vector2> r, out Vector2 c, out Vector2 ax, out Vector2 ay, out float hx, out float hy)
    {
        float best = float.MaxValue; c = ax = ay = Vector2.zero; hx = hy = 0;
        for (int i = 0; i < r.Count; i++)
        {
            Vector2 d = (r[(i + 1) % r.Count] - r[i]); if (d.sqrMagnitude < 1e-4f) continue; d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x);
            float u0 = 1e9f, u1 = -1e9f, v0 = 1e9f, v1 = -1e9f;
            foreach (var p in r) { float u = Vector2.Dot(p, d), v = Vector2.Dot(p, n); u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u); v0 = Mathf.Min(v0, v); v1 = Mathf.Max(v1, v); }
            float area = (u1 - u0) * (v1 - v0);
            if (area < best)
            {
                best = area; c = d * (u0 + u1) / 2 + n * (v0 + v1) / 2;
                if (u1 - u0 >= v1 - v0) { ax = d; ay = n; hx = (u1 - u0) / 2; hy = (v1 - v0) / 2; }
                else { ax = n; ay = -d; hx = (v1 - v0) / 2; hy = (u1 - u0) / 2; }
            }
        }
    }

    // JsonUtility can't read nested arrays: pull id, base_y and ring out by hand
    static List<B> ReadBuildings(string txt)
    {
        var list = new List<B>(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        int i = 0;
        while ((i = txt.IndexOf("\"id\"", i)) >= 0)
        {
            var b = new B();
            int q0 = txt.IndexOf('"', txt.IndexOf(':', i) + 1), q1 = txt.IndexOf('"', q0 + 1); b.id = txt.Substring(q0 + 1, q1 - q0 - 1);
            int by = txt.IndexOf("\"base_y\"", i); int c0 = txt.IndexOf(':', by) + 1, c1 = txt.IndexOfAny(new[] { ',', '\n', '}' }, c0);
            b.base_y = float.Parse(txt.Substring(c0, c1 - c0).Trim(), ci);
            int ri = txt.IndexOf("\"ring\"", i); int s0 = txt.IndexOf('[', ri); int depth = 0, s1 = s0;
            for (int k = s0; k < txt.Length; k++) { if (txt[k] == '[') depth++; else if (txt[k] == ']') { depth--; if (depth == 0) { s1 = k; break; } } }
            var pts = new List<float[]>(); string body = txt.Substring(s0 + 1, s1 - s0 - 1);
            int p = 0;
            while ((p = body.IndexOf('[', p)) >= 0)
            {
                int e = body.IndexOf(']', p); var xs = body.Substring(p + 1, e - p - 1).Split(',');
                pts.Add(new[] { float.Parse(xs[0].Trim(), ci), float.Parse(xs[1].Trim(), ci) }); p = e + 1;
            }
            b.ring = pts.ToArray(); list.Add(b);
            i = s1;
        }
        return list;
    }

    class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
        {
            int k = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0;
            // Unity: a triangle faces the way Cross(b - a, c - a) points
            if (!flip) { t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3); }
            else { t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2); }
        }
        // convex polygon, both sides (roof pieces are seen from above and below), planar UVs in metres
        public void Poly(Vector3[] p)
        {
            Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
            Vector3 tu = (p[1] - p[0]).normalized, tv = Vector3.Cross(n, tu);
            for (int side = 0; side < 2; side++)
            {
                int k = v.Count;
                foreach (var q in p) { v.Add(q); uv.Add(new Vector2(Vector3.Dot(q - p[0], tu), Vector3.Dot(q - p[0], tv))); }
                for (int i = 1; i < p.Length - 1; i++)
                    if (side == 0) { t.Add(k); t.Add(k + i + 1); t.Add(k + i); } else { t.Add(k); t.Add(k + i); t.Add(k + i + 1); }
            }
        }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }

    // ---------------------------------------------------------------- materials (procedural textures, 1 repeat = 1 m)
    static void Materials()
    {
        brick = Textured("ch_brick", 256, 0.06f, 1f, (x, y, r) =>
        {
            int row = y / 21, off = (row % 2) * 32;                           // 12 courses / m, 4 bricks / m
            bool mortar = y % 21 < 3 || (x + off) % 64 < 3;
            float j = 0.88f + (float)r.NextDouble() * 0.2f;
            return mortar ? new Color(0.62f, 0.58f, 0.52f) : new Color(0.50f * j, 0.30f * j, 0.22f * j);
        });
        stone = Textured("ch_stone", 256, 0.05f, 1f, (x, y, r) =>
        {
            int row = y / 64, off = (row * 37) % 96; bool joint = y % 64 < 4 || (x + off) % 96 < 4;
            float j = 0.85f + (float)r.NextDouble() * 0.25f;
            return joint ? new Color(0.45f, 0.43f, 0.40f) : new Color(0.66f * j, 0.60f * j, 0.50f * j);
        });
        roof = Textured("ch_roof", 256, 0.08f, 1f, (x, y, r) =>                       // grey composition shingles
        {
            int row = y / 32, off = (row % 2) * 40; bool gap = y % 32 < 3 || (x + off) % 80 < 2;
            float j = 0.9f + (float)r.NextDouble() * 0.16f;
            return gap ? new Color(0.20f, 0.21f, 0.23f) : new Color(0.36f * j, 0.38f * j, 0.41f * j);   // blue-grey slate
        });
        white = Colour("ch_white", new Color(0.93f, 0.93f, 0.90f), 0.15f);
        glass = Colour("ch_glass", new Color(0.10f, 0.13f, 0.16f), 0.85f);
    }

    static Material Colour(string name, Color c, float gloss)
    {
        string p = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(m);
        return m;
    }

    static Material Textured(string name, int size, float gloss, float metres, System.Func<int, int, System.Random, Color> px)
    {
        string tp = Dir + "/" + name + ".png";
        var rnd = new System.Random(name.GetHashCode());
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) tex.SetPixel(x, y, px(x, y, rnd));
        File.WriteAllBytes(tp, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(tp);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); t.wrapMode = TextureWrapMode.Repeat;
        var m = Colour(name, Color.white, gloss);
        m.mainTexture = t; m.mainTextureScale = new Vector2(1f / metres, 1f / metres);
        return m;
    }
}
