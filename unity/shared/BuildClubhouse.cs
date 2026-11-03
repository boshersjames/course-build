using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the Tierra Rejada clubhouse from clubhouse/clubhouse_spec.txt (Documents/tierra-rejada): stucco wings with
// dark brown clay tile hip roofs (optionally a flat roof well), French doors / windows, a white chimney, glass cupola,
// a timber loggia, a stone-pillared porte-cochere, a draped paver patio with planter walls, a stone fire pit, patio table
// sets, light posts, navy golf carts and a flagpole. Everything goes under
// CLUBHOUSE; re-running replaces it. Building meshes get MeshColliders (balls hit the clubhouse); nothing else changes play.
public static class BuildClubhouse
{
    const string ROOT = "CLUBHOUSE", DIR = "Assets/TierraRejada/generated/clubhouse";
    static string Spec { get { return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "/tierra-rejada/clubhouse/clubhouse_spec.txt"; } }
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    class Wing { public string name; public Vector2 c; public float len, wid, ang, wallH, pitch, flat; public string door; public float floor, low; public Vector2 ax, ay; }

    [MenuItem("Course Tools/Build Clubhouse")]
    public static void Build()
    {
        var lines = File.ReadAllLines(Spec).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).Select(l => l.Split(',').Select(s => s.Trim()).ToArray()).ToList();
        var old = GameObject.Find(ROOT); if (old != null) Undo.DestroyObjectImmediate(old);
        Directory.CreateDirectory(DIR);
        var root = new GameObject(ROOT); Undo.RegisterCreatedObjectUndo(root, "Build clubhouse");
        var mats = new Mats();
        var wings = new Dictionary<string, Wing>();
        // one shared floor level for the whole building: the highest ground under any wing corner
        foreach (var f in lines.Where(f => f[0] == "wing"))
        {
            var w = new Wing { name = f[1], c = new Vector2(F(f[2]), F(f[3])), len = F(f[4]), wid = F(f[5]), ang = F(f[6]), wallH = F(f[7]), pitch = F(f[8]), flat = F(f[9]), door = f[10] };
            float a = w.ang * Mathf.Deg2Rad; w.ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); w.ay = new Vector2(-w.ax.y, w.ax.x); wings[w.name] = w;
        }
        float floor = float.MinValue, low = float.MaxValue;
        foreach (var w in wings.Values) foreach (var p in Corners(w, 0)) { float g = Ground(p.x, p.y); floor = Mathf.Max(floor, g); low = Mathf.Min(low, g); }
        floor += 0.15f;
        var wallsB = new MB(); var roofB = new MB(); var glassB = new MB(); var trimB = new MB(); var flatB = new MB(); var frameB = new MB();
        foreach (var w in wings.Values)
        {
            w.floor = floor; w.low = low - 0.5f;
            var cs = Corners(w, 0); float top = floor + w.wallH;
            wallsB.inside = glassB.inside = trimB.inside = frameB.inside = V(w.c, floor + 1f);
            for (int i = 0; i < 4; i++)
            {
                var a = cs[i]; var b = cs[(i + 1) % 4];
                wallsB.Quad(V(a, w.low), V(b, w.low), V(b, top), V(a, top), true);
                // openings on this side: French doors on the door side, windows elsewhere
                string side = SideName(w, a, b); bool doors = w.door == side;
                float L = (b - a).magnitude; Vector2 dir = (b - a) / L; Vector2 outN = new Vector2(dir.y, -dir.x); if (Vector2.Dot(outN, (a + b) / 2 - w.c) < 0) outN = -outN;
                float step = doors ? 2.6f : 3.4f, ow = doors ? 1.5f : 1.3f, oh = doors ? 2.6f : 1.5f, sill = doors ? 0.0f : 1.0f;
                int n = Mathf.FloorToInt((L - 2f) / step);
                for (int k = 0; k < n; k++)
                {
                    float t = (L - (n - 1) * step) / 2 + k * step; var m = a + dir * t; var o = outN * 0.03f;
                    glassB.Quad(V(m - dir * ow / 2 + o, floor + sill), V(m + dir * ow / 2 + o, floor + sill), V(m + dir * ow / 2 + o, floor + sill + oh), V(m - dir * ow / 2 + o, floor + sill + oh), true);
                    // white frame around each opening + mullion grid (doors: 1 vertical x 3 horizontal, windows: 1 x 1)
                    var o2 = outN * 0.06f; float fw = 0.10f, mw = 0.045f; float y0 = floor + sill, y1 = floor + sill + oh;
                    System.Action<Vector2, Vector2, float, float> bar = (p0, p1, ya, yb) => frameB.Quad(V(p0 + o2, ya), V(p1 + o2, ya), V(p1 + o2, yb), V(p0 + o2, yb), true, 1f);
                    bar(m - dir * (ow / 2 + fw), m + dir * (ow / 2 + fw), y1, y1 + fw);              // head
                    bar(m - dir * (ow / 2 + fw), m + dir * (ow / 2 + fw), y0 - fw, y0);              // sill
                    bar(m - dir * (ow / 2 + fw), m - dir * (ow / 2), y0, y1);                        // jambs
                    bar(m + dir * (ow / 2), m + dir * (ow / 2 + fw), y0, y1);
                    bar(m - dir * mw, m + dir * mw, y0, y1);                                         // centre mullion
                    int rows = doors ? 3 : 1;
                    for (int q = 1; q <= rows; q++) { float yy = y0 + oh * q / (rows + 1f); bar(m - dir * (ow / 2), m + dir * (ow / 2), yy - mw, yy + mw); }
                }
            }
            HipRoof(roofB, flatB, w, top);
        }
        // loggia: posts, beam and a lean-to tile roof along one side of a wing
        foreach (var f in lines.Where(f => f[0] == "loggia"))
        {
            var w = wings[f[1]]; float depth = F(f[3]), sp = F(f[4]);
            var cs = Corners(w, 0); Vector2 a = Vector2.zero, b = Vector2.zero; for (int i = 0; i < 4; i++) if (SideName(w, cs[i], cs[(i + 1) % 4]) == f[2]) { a = cs[i]; b = cs[(i + 1) % 4]; }
            float L = (b - a).magnitude; Vector2 dir = (b - a) / L; Vector2 outN = new Vector2(dir.y, -dir.x); if (Vector2.Dot(outN, (a + b) / 2 - w.c) < 0) outN = -outN;
            float h = floor + 3.2f, hTop = floor + w.wallH - 0.2f;
            int n = Mathf.FloorToInt(L / sp);
            for (int k = 0; k <= n; k++) { var p = a + dir * (L * k / n) + outN * (depth - 0.2f); Box(trimB, p, dir, 0.32f, 0.32f, Ground(p.x, p.y) - 0.3f, h); }
            var a2 = a + outN * depth; var b2 = b + outN * depth;
            Box(trimB, (a2 + b2) / 2 - outN * 0.2f, dir, L, 0.35f, h, h + 0.35f);
            roofB.inside = V(w.c, floor);
            // flat, dark-stained timber loggia roof (BoJa's dusk photo), a slight fall away from the wall
            trimB.inside = V((a + b) / 2, h - 20f); trimB.Quad(V(a2 + outN * 0.5f, h + 0.35f), V(b2 + outN * 0.5f, h + 0.35f), V(b, h + 0.6f), V(a, h + 0.6f), true, 2f);
            trimB.inside = V((a + b) / 2, hTop + 20f);
            trimB.inside = V((a + b) / 2, h + 20f); trimB.Quad(V(a, h + 0.55f), V(b, h + 0.55f), V(b2 + outN * 0.5f, h + 0.3f), V(a2 + outN * 0.5f, h + 0.3f), true);
        }
        // chimney: white stucco with a black cap (BoJa's Oct 29 photos)
        var chimB = new MB(); var capB = new MB();
        foreach (var f in lines.Where(f => f[0] == "chimney"))
        {
            var p = new Vector2(F(f[1]), F(f[2])); var w0 = wings.Values.First();
            Box(chimB, p, w0.ax, F(f[3]), F(f[4]), floor, floor + F(f[5]));
            Box(capB, p, w0.ax, F(f[3]) + 0.2f, F(f[4]) + 0.2f, floor + F(f[5]), floor + F(f[5]) + 0.2f);
            Box(capB, p, w0.ax, 0.5f, 0.5f, floor + F(f[5]) + 0.2f, floor + F(f[5]) + 0.6f);
        }
        // porte-cochere: gabled tile roof with a dark timber gable end on four stone-veneer pillars (IMG_4613/4615/5049)
        // porte,cx,cz,length,width,angle,post_h,pitch : length runs along 'angle' (ridge direction)
        var pillarB = new MB();
        foreach (var f in lines.Where(f => f[0] == "porte"))
        {
            var c = new Vector2(F(f[1]), F(f[2])); float len = F(f[3]), wid = F(f[4]), a = F(f[5]) * Mathf.Deg2Rad, ph = F(f[6]), pitch = F(f[7]);
            var ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var ay = new Vector2(-ax.y, ax.x);
            foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                var p = c + ax * s.x * (len / 2 - 0.5f) + ay * s.y * (wid / 2 - 0.5f);
                Box(pillarB, p, ax, 0.8f, 0.8f, Ground(p.x, p.y) - 0.3f, floor + ph);
            }
            float eave = floor + ph + 0.4f, hl = len / 2 + 0.4f, hw = wid / 2 + 0.6f, ridge = eave + hw * pitch;
            foreach (var sy in new[] { -1f, 1f }) Box(trimB, c + ay * sy * (wid / 2 - 0.5f), ax, len, 0.3f, floor + ph, eave);     // side beams
            foreach (var sx in new[] { -1f, 1f }) Box(trimB, c + ax * sx * (len / 2 - 0.5f), ay, wid, 0.3f, floor + ph, eave);     // end beams
            roofB.inside = V(c, eave - 3f);
            foreach (var sy in new[] { -1f, 1f })
                roofB.Quad(V(c - ax * hl + ay * sy * hw, eave), V(c + ax * hl + ay * sy * hw, eave), V(c + ax * hl, ridge), V(c - ax * hl, ridge), true, 2f);
            foreach (var sx in new[] { -1f, 1f })
            {
                var e = c + ax * sx * (len / 2 + 0.2f); trimB.inside = V(c, eave);
                trimB.Quad(V(e - ay * (hw - 0.2f), eave), V(e + ay * (hw - 0.2f), eave), V(e, ridge - 0.15f), V(e, ridge - 0.15f), true, 2f);
            }
            pillarB.inside = null; flatB.Quad(V(c - ax * (len / 2) - ay * (wid / 2), eave - 0.05f), V(c + ax * (len / 2) - ay * (wid / 2), eave - 0.05f), V(c + ax * (len / 2) + ay * (wid / 2), eave - 0.05f), V(c - ax * (len / 2) + ay * (wid / 2), eave - 0.05f), false);
        }
        foreach (var f in lines.Where(f => f[0] == "cupola"))
        {
            var w = wings[f[1]]; float ridge = floor + w.wallH + w.wid / 2 * w.pitch;
            Box(glassB, w.c, w.ax, 2.2f, 2.2f, ridge - 0.6f, ridge + 1.3f);
            var cw = new Wing { c = w.c, len = 2.8f, wid = 2.8f, ax = w.ax, ay = w.ay, pitch = 0.6f, flat = 0 };
            HipRoof(roofB, flatB, cw, ridge + 1.3f);
        }
        Part(root, "Walls", wallsB, mats.stucco, true); Part(root, "Roof", roofB, mats.tile, true); Part(root, "Roof_Well", flatB, mats.flat, true);
        Part(root, "Glass", glassB, mats.glass, false); Part(root, "Timber", trimB, mats.wood, true); Part(root, "Frames", frameB, mats.frame, false);
        Part(root, "Chimney", chimB, mats.linen, true); Part(root, "Chimney_Cap", capB, mats.ash, true); Part(root, "Porte_Cochere_Pillars", pillarB, mats.stone, true);

        // patio: a draped paver slab over the polygon, planter walls, fire pit
        var poly = lines.Where(f => f[0] == "patio").SelectMany(f => Enumerable.Range(0, (f.Length - 1) / 2).Select(i => new Vector2(F(f[1 + 2 * i]), F(f[2 + 2 * i])))).ToList();
        if (poly.Count >= 3) Part(root, "Patio", Drape(poly, 0.06f, wings.Values.ToList()), mats.pavers, false);
        var planter = new MB(); var caps = new MB();
        foreach (var f in lines.Where(f => f[0] == "wall"))
        {
            var a = new Vector2(F(f[1]), F(f[2])); var b = new Vector2(F(f[3]), F(f[4])); float hh = F(f[5]); var dir = (b - a).normalized;
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 2f));
            for (int i = 0; i < n; i++) { var p = Vector2.Lerp(a, b, (i + 0.5f) / n); float g = Ground(p.x, p.y); Box(planter, p, dir, (b - a).magnitude / n + 0.02f, 0.40f, g - 0.3f, g + hh - 0.06f); Box(caps, p, dir, (b - a).magnitude / n + 0.02f, 0.46f, g + hh - 0.06f, g + hh); }
        }
        // ~3 ft rough tan stone wall with a smooth cream cap (BoJa: a stone wall fences in the patio)
        Part(root, "Patio_Walls", planter, mats.stone, true); Part(root, "Patio_Wall_Caps", caps, mats.stucco, true);
        // square tables (0.9 m, white top) with 4 dark chairs each, under the loggia
        var tTop = new MB(); var tDark = new MB();
        foreach (var f in lines.Where(f => f[0] == "sqtable"))
        {
            var p = new Vector2(F(f[1]), F(f[2])); float a = F(f[3]) * Mathf.Deg2Rad; var ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var ay = new Vector2(-ax.y, ax.x); float g = Mathf.Max(Ground(p.x, p.y), floor - 0.15f) + 0.06f;
            Box(tTop, p, ax, 0.9f, 0.9f, g + 0.72f, g + 0.76f);
            foreach (var s in new[] { new Vector2(-0.38f, -0.38f), new Vector2(0.38f, -0.38f), new Vector2(-0.38f, 0.38f), new Vector2(0.38f, 0.38f) }) Box(tDark, p + ax * s.x + ay * s.y, ax, 0.05f, 0.05f, g, g + 0.72f);
            foreach (var d in new[] { ax, -ax, ay, -ay })
            {
                var c = p + d * 0.75f; var side = new Vector2(-d.y, d.x);
                Box(tDark, c, side, 0.44f, 0.44f, g + 0.42f, g + 0.46f);                       // seat
                Box(tDark, c + d * 0.2f, side, 0.44f, 0.05f, g + 0.46f, g + 0.95f);            // back
                foreach (var l in new[] { -0.19f, 0.19f }) foreach (var m in new[] { -0.19f, 0.19f }) Box(tDark, c + side * l + d * m, side, 0.04f, 0.04f, g, g + 0.42f);
            }
        }
        Part(root, "Square_Table_Tops", tTop, mats.linen, true); Part(root, "Square_Tables_Chairs", tDark, mats.dark, true);
        // golf carts: navy Club Car body, black canopy on 4 posts, tan seats, black wheels, bag rack at the back; ~2.4 x 1.2 x 1.8 m
        var cWhite = new MB(); var cDark = new MB(); var cWheel = new MB(); var cRoof = new MB(); var cSeat = new MB();
        foreach (var f in lines.Where(f => f[0] == "cart"))
        {
            var p = new Vector2(F(f[1]), F(f[2])); float a = F(f[3]) * Mathf.Deg2Rad; var fw = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var sd = new Vector2(-fw.y, fw.x); float g = Ground(p.x, p.y) + 0.06f;
            Box(cWhite, p, fw, 2.3f, 1.15f, g + 0.25f, g + 0.55f);                                   // chassis / floor
            Box(cWhite, p + fw * 0.85f, fw, 0.6f, 1.15f, g + 0.55f, g + 0.95f);                       // front cowl
            Box(cSeat, p - fw * 0.15f, fw, 0.6f, 1.05f, g + 0.55f, g + 0.85f);                        // seat
            Box(cSeat, p - fw * 0.5f, fw, 0.1f, 1.05f, g + 0.85f, g + 1.25f);                         // seat back
            Box(cDark, p - fw * 0.95f, fw, 0.35f, 0.9f, g + 0.55f, g + 1.35f);                        // bag rack
            foreach (var s in new[] { -0.5f, 0.5f }) { Box(cRoof, p + fw * 0.55f + sd * s, fw, 0.05f, 0.05f, g + 0.95f, g + 1.8f); Box(cRoof, p - fw * 0.7f + sd * s, fw, 0.05f, 0.05f, g + 0.85f, g + 1.8f); }
            Box(cRoof, p - fw * 0.05f, fw, 1.6f, 1.2f, g + 1.8f, g + 1.86f);                           // roof
            foreach (var s in new[] { -0.55f, 0.55f }) foreach (var q in new[] { -0.75f, 0.75f }) Box(cWheel, p + fw * q + sd * s, fw, 0.45f, 0.18f, g, g + 0.45f);
        }
        Part(root, "Golf_Carts", cWhite, mats.cartNavy, true); Part(root, "Golf_Carts_Racks", cDark, mats.dark, true); Part(root, "Golf_Carts_Seats", cSeat, mats.cartSeat, true);
        Part(root, "Golf_Carts_Canopy", cRoof, mats.ash, true); Part(root, "Golf_Carts_Wheels", cWheel, mats.ash, true);
        // flagpole by the cart staging (IMG_4615/5048): white pole, US flag at the top. flagpole,x,z,height
        foreach (var f in lines.Where(f => f[0] == "flagpole"))
        {
            var p = new Vector2(F(f[1]), F(f[2])); float h = F(f[3]), g = Ground(p.x, p.y); var pole = new MB(); var flag = new MB();
            Box(pole, p, Vector2.right, 0.14f, 0.14f, g - 0.3f, g + h);
            Box(pole, p, Vector2.right, 0.2f, 0.2f, g + h, g + h + 0.2f);
            float fl = 2.4f, fh = 1.3f; var d = new Vector2(1f, 0.15f).normalized;
            foreach (var o in new[] { -0.02f, 0.02f })
            {
                var q = p + new Vector2(-d.y, d.x) * o; flag.inside = V(q - new Vector2(-d.y, d.x) * o * 10f, g + h - fh / 2);
                flag.Quad(V(q + d * 0.08f, g + h - fh), V(q + d * (0.08f + fl), g + h - fh), V(q + d * (0.08f + fl), g + h), V(q + d * 0.08f, g + h), true, fl);
            }
            Part(root, "Flagpole", pole, mats.frame, true); Part(root, "Flag", flag, mats.flag, false);
        }
        foreach (var f in lines.Where(f => f[0] == "firepit"))
        {
            var p = new Vector2(F(f[1]), F(f[2])); float r = F(f[3]), g = Ground(p.x, p.y); var pit = new MB(); var ash = new MB();
            Ring(pit, p, r, r - 0.35f, g - 0.2f, g + 0.45f, 24); Disc(ash, p, r - 0.35f, g + 0.15f, 24);
            Part(root, "FirePit", pit, mats.stone, true); Part(root, "FirePit_Ash", ash, mats.ash, false);
        }
        // covered patio (IMG_4614): flat light-grey roof on dark posts. cover,cx,cz,length,width,angle,height
        var coverB = new MB(); var postB = new MB();
        foreach (var f in lines.Where(f => f[0] == "cover"))
        {
            var c = new Vector2(F(f[1]), F(f[2])); float len = F(f[3]), wid = F(f[4]), a = F(f[5]) * Mathf.Deg2Rad, h = F(f[6]); var ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var ay = new Vector2(-ax.y, ax.x);
            Box(coverB, c, ax, len, wid, floor + h, floor + h + 0.25f);
            int nx = Mathf.Max(2, Mathf.RoundToInt(len / 3.5f));
            for (int i = 0; i <= nx; i++) foreach (var sy in new[] { -1f, 1f }) { var p = c + ax * (-len / 2 + 0.2f + (len - 0.4f) * i / nx) + ay * sy * (wid / 2 - 0.2f); Box(postB, p, ax, 0.18f, 0.18f, Ground(p.x, p.y) - 0.2f, floor + h); }
        }
        Part(root, "Patio_Cover", coverB, mats.flat, true); Part(root, "Patio_Cover_Posts", postB, mats.wood, true);
        // string lights (IMG_4614): sagging cord between two points with warm bulbs every 0.8 m. strings,x1,z1,x2,z2,height
        var cordB = new MB(); var bulbB = new MB();
        foreach (var f in lines.Where(f => f[0] == "strings"))
        {
            var a = new Vector2(F(f[1]), F(f[2])); var b = new Vector2(F(f[3]), F(f[4])); float h = floor + F(f[5]); float L = (b - a).magnitude; int n = Mathf.Max(2, Mathf.RoundToInt(L / 0.8f)); var d = (b - a).normalized;
            for (int i = 0; i <= n; i++) { float t = (float)i / n; var p = Vector2.Lerp(a, b, t); float y = h - 0.6f * 4f * t * (1 - t); Box(bulbB, p, d, 0.07f, 0.07f, y - 0.12f, y - 0.05f); if (i < n) Box(cordB, Vector2.Lerp(a, b, t + 0.5f / n), d, L / n, 0.015f, y - 0.01f, y + 0.005f); }
            foreach (var e in new[] { a, b }) Box(cordB, e, d, 0.08f, 0.08f, Ground(e.x, e.y), h + 0.1f);
        }
        Part(root, "String_Light_Cords", cordB, mats.ash, false); Part(root, "String_Light_Bulbs", bulbB, mats.bulb, false);
        // rocking chairs round the fire pit (IMG_4614): rocker,cx,cz,radius,count
        var rockB = new MB();
        foreach (var f in lines.Where(f => f[0] == "rocker"))
        {
            var c = new Vector2(F(f[1]), F(f[2])); float r = F(f[3]); int n = int.Parse(f[4]);
            for (int i = 0; i < n; i++)
            {
                float an = i * Mathf.PI * 2 / n; var d = new Vector2(Mathf.Cos(an), Mathf.Sin(an)); var p = c + d * r; var side = new Vector2(-d.y, d.x); float g = Ground(p.x, p.y);
                Box(rockB, p, side, 0.55f, 0.5f, g + 0.42f, g + 0.47f); Box(rockB, p + d * 0.27f, side, 0.55f, 0.06f, g + 0.47f, g + 1.05f);
                foreach (var s2 in new[] { -0.27f, 0.27f }) Box(rockB, p + side * s2, d, 0.75f, 0.05f, g, g + 0.08f);                       // rockers
                foreach (var s2 in new[] { -0.27f, 0.27f }) Box(rockB, p + side * s2, d, 0.05f, 0.05f, g + 0.06f, g + 0.42f);
            }
        }
        Part(root, "Rocking_Chairs", rockB, mats.wood, true);
        // white square planters with a clipped shrub (IMG_4613/4620): planter,x,z
        var potB = new MB(); var shrubB = new MB();
        foreach (var f in lines.Where(f => f[0] == "planter"))
        {
            var p = new Vector2(F(f[1]), F(f[2])); float g = Ground(p.x, p.y); Box(potB, p, Vector2.right, 0.7f, 0.7f, g, g + 0.7f); Sphere(shrubB, new Vector3(p.x, g + 1.05f, p.y), new Vector3(0.45f, 0.4f, 0.45f));
        }
        Part(root, "Planters", potB, mats.linen, true); Part(root, "Planter_Shrubs", shrubB, mats.shrub, false);
        // pergola bar (IMG_4614): pergola,cx,cz,length,width,angle,height: 4 posts, beams, slats, a stone-fronted bar counter
        var perB = new MB(); var barB = new MB();
        foreach (var f in lines.Where(f => f[0] == "pergola"))
        {
            var c = new Vector2(F(f[1]), F(f[2])); float len = F(f[3]), wid = F(f[4]), a = F(f[5]) * Mathf.Deg2Rad, h = floor + F(f[6]); var ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var ay = new Vector2(-ax.y, ax.x);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sy in new[] { -1f, 1f }) { var p = c + ax * sx * (len / 2 - 0.15f) + ay * sy * (wid / 2 - 0.15f); Box(perB, p, ax, 0.2f, 0.2f, Ground(p.x, p.y) - 0.2f, h); }
            foreach (var sy in new[] { -1f, 1f }) Box(perB, c + ay * sy * (wid / 2 - 0.15f), ax, len + 0.6f, 0.12f, h, h + 0.25f);
            for (float t = -len / 2; t <= len / 2 + 0.01f; t += 0.45f) Box(perB, c + ax * t, ay, wid + 0.4f, 0.06f, h + 0.25f, h + 0.37f);
            var bc = c - ay * (wid / 2 - 0.6f); Box(barB, bc, ax, len - 0.6f, 0.6f, Ground(bc.x, bc.y), Ground(bc.x, bc.y) + 1.1f);
        }
        Part(root, "Pergola_Wood", perB, mats.wood, true); Part(root, "Pergola_Bar", barB, mats.stone, true);
        // curbed concrete drive up to the staging (IMG_4613/4615/4620): drive,width,x1,z1,x2,z2,...; rose / shrub beds and low path lights along it
        var driveB = new MB(); var curbB = new MB(); var roseB = new MB(); var bloomB = new MB(); var plB = new MB(); var plTop = new MB();
        foreach (var f in lines.Where(f => f[0] == "drive"))
        {
            float w = F(f[1]); var pts = new List<Vector2>(); for (int i = 2; i + 1 < f.Length; i += 2) pts.Add(new Vector2(F(f[i]), F(f[i + 1])));
            var dense = new List<Vector2>(); for (int i = 0; i + 1 < pts.Count; i++) { int n = Mathf.Max(1, Mathf.CeilToInt((pts[i + 1] - pts[i]).magnitude / 2f)); for (int k = 0; k < n; k++) dense.Add(Vector2.Lerp(pts[i], pts[i + 1], (float)k / n)); } dense.Add(pts[pts.Count - 1]);
            float acc = 0; var rng2 = new System.Random(11);
            for (int i = 0; i + 1 < dense.Count; i++)
            {
                var a = dense[i]; var b = dense[i + 1]; var d = (b - a).normalized; var nrm = new Vector2(-d.y, d.x); float seg = (b - a).magnitude; var m = (a + b) / 2;
                float ga = Ground(a.x, a.y), gb = Ground(b.x, b.y);
                driveB.Quad(V(a - nrm * w / 2, ga + 0.05f), V(b - nrm * w / 2, gb + 0.05f), V(b + nrm * w / 2, gb + 0.05f), V(a + nrm * w / 2, ga + 0.05f), false, 3f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    Box(curbB, m + nrm * sd * (w / 2 + 0.08f), d, seg + 0.02f, 0.16f, (ga + gb) / 2 - 0.1f, (ga + gb) / 2 + 0.17f);
                    var bed = m + nrm * sd * (w / 2 + 1.3f); float gg = Ground(bed.x, bed.y);
                    if (rng2.NextDouble() < 0.8) { Sphere(roseB, new Vector3(bed.x, gg + 0.35f, bed.y), new Vector3(0.5f, 0.38f, 0.5f)); if (rng2.NextDouble() < 0.6) Sphere(bloomB, new Vector3(bed.x + 0.15f, gg + 0.62f, bed.y), new Vector3(0.16f, 0.12f, 0.16f)); }
                }
                acc += seg;
                if (acc >= 7f) { acc = 0; var lp = m + nrm * (w / 2 + 0.6f); float gl = Ground(lp.x, lp.y); Box(plB, lp, d, 0.1f, 0.1f, gl, gl + 0.6f); Box(plTop, lp, d, 0.18f, 0.18f, gl + 0.6f, gl + 0.72f); }
            }
        }
        Part(root, "Drive", driveB, mats.concrete, false); Part(root, "Drive_Curbs", curbB, mats.concrete, true); Part(root, "Drive_Beds", roseB, mats.shrub, false); Part(root, "Drive_Roses", bloomB, mats.rose, false);
        Part(root, "Path_Light_Posts", plB, mats.ash, false); Part(root, "Path_Light_Tops", plTop, mats.bulb, false);
        var props = new GameObject("Patio_Props"); props.transform.SetParent(root.transform);
        var table = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Course Furniture/PatioTable_Round/Patio_Table_Umbrella.prefab");
        var lamp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Course Furniture/Lightpost/Lightpost.prefab");
        var rng = new System.Random(7);
        foreach (var f in lines.Where(f => f[0] == "table" || f[0] == "lamp"))
        {
            var pf = f[0] == "table" ? table : lamp; if (pf == null) continue; var p = new Vector2(F(f[1]), F(f[2]));
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.SetParent(props.transform);
            g.transform.position = new Vector3(p.x, Ground(p.x, p.y) + 0.06f, p.y); g.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials; bool ch = false;
                for (int k = 0; k < ms.Length; k++) if (ms[k] != null && ms[k].HasProperty("_Color")) { var c0 = ms[k].color; if (c0.r > 0.5f && c0.g < 0.35f && c0.b < 0.35f) { ms[k] = mats.canvas; ch = true; } }
                if (ch) r.sharedMaterials = ms;
            }
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Clubhouse built: " + wings.Count + " wings, floor " + floor.ToString("F2") + " (ground " + low.ToString("F2") + " to " + (floor - 0.15f).ToString("F2") + ")");
    }

    // ------------------------------------------------------------------ geometry
    static float F(string s) { return float.Parse(s, CI); }
    static Vector3 V(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
    static Vector2[] Corners(Wing w, float grow)
    {
        float hl = w.len / 2 + grow, hw = w.wid / 2 + grow;
        return new[] { w.c - w.ax * hl - w.ay * hw, w.c + w.ax * hl - w.ay * hw, w.c + w.ax * hl + w.ay * hw, w.c - w.ax * hl + w.ay * hw };
    }
    static string SideName(Wing w, Vector2 a, Vector2 b)
    {
        var m = (a + b) / 2 - w.c; if (Mathf.Abs(m.x) > Mathf.Abs(m.y)) return m.x > 0 ? "E" : "W"; return m.y > 0 ? "N" : "S";
    }

    // hip roof over a wing (0.6 m overhang); flat > 0 cuts the top flat, leaving a roof well that far in from the eaves
    static void HipRoof(MB roof, MB flat, Wing w, float eave)
    {
        roof.inside = V(w.c, eave - 3f); flat.inside = null;
        float ov = 0.6f; var e = Corners(w, ov); float hl = w.len / 2 + ov, hw = w.wid / 2 + ov;
        float run = Mathf.Min(hl, hw); float rise = run * w.pitch;
        float cut = w.flat > 0 ? Mathf.Min(w.flat, run - 0.5f) : run;
        float topH = eave - 0.1f + cut * w.pitch;
        // top outline: corners moved in by 'cut' (for a full hip this collapses to the ridge line)
        var t = new Vector2[4]; float il = hl - cut, iw = hw - cut;
        t[0] = w.c - w.ax * il - w.ay * iw; t[1] = w.c + w.ax * il - w.ay * iw; t[2] = w.c + w.ax * il + w.ay * iw; t[3] = w.c - w.ax * il + w.ay * iw;
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4; float L = (e[j] - e[i]).magnitude;
            roof.Quad(V(e[i], eave - 0.1f), V(e[j], eave - 0.1f), V(t[j], topH), V(t[i], topH), true, 2f, L);
            // soffit / fascia band under the eave
            roof.Quad(V(e[i], eave - 0.35f), V(e[j], eave - 0.35f), V(e[j], eave - 0.1f), V(e[i], eave - 0.1f), true, 2f, L);
        }
        if (cut < run - 0.01f) flat.Quad(V(t[0], topH - 0.4f), V(t[1], topH - 0.4f), V(t[2], topH - 0.4f), V(t[3], topH - 0.4f), false);
    }

    static void Sphere(MB b, Vector3 c, Vector3 r)
    {
        int seg = 6; var ring = new List<Vector3[]>();
        for (int j = 0; j <= seg; j++) { var row = new Vector3[seg * 2 + 1]; for (int i = 0; i <= seg * 2; i++) { float th = Mathf.PI * j / seg, ph = Mathf.PI * i / seg; row[i] = c + Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), r); } ring.Add(row); }
        b.inside = c;
        for (int j = 0; j < seg; j++) for (int i = 0; i < seg * 2; i++) b.Quad(ring[j][i], ring[j][i + 1], ring[j + 1][i + 1], ring[j + 1][i], false, 1f);
    }
    static void Box(MB b, Vector2 c, Vector2 ax, float len, float wid, float y0, float y1)
    {
        b.inside = V(c, (y0 + y1) / 2);
        var ay = new Vector2(-ax.y, ax.x); var p = new[] { c - ax * len / 2 - ay * wid / 2, c + ax * len / 2 - ay * wid / 2, c + ax * len / 2 + ay * wid / 2, c - ax * len / 2 + ay * wid / 2 };
        for (int i = 0; i < 4; i++) b.Quad(V(p[i], y0), V(p[(i + 1) % 4], y0), V(p[(i + 1) % 4], y1), V(p[i], y1), true);
        b.Quad(V(p[0], y1), V(p[1], y1), V(p[2], y1), V(p[3], y1), false);
    }
    static void Ring(MB b, Vector2 c, float r0, float r1, float y0, float y1, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a0 = i * 2 * Mathf.PI / n, a1 = (i + 1) * 2 * Mathf.PI / n; var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)); var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            b.inside = V(c, (y0 + y1) / 2); b.toward = false; b.Quad(V(c + d0 * r0, y0), V(c + d1 * r0, y0), V(c + d1 * r0, y1), V(c + d0 * r0, y1), true);
            b.toward = true; b.Quad(V(c + d1 * r1, y0), V(c + d0 * r1, y0), V(c + d0 * r1, y1), V(c + d1 * r1, y1), true);
            b.toward = false; b.inside = null; b.Quad(V(c + d0 * r0, y1), V(c + d1 * r0, y1), V(c + d1 * r1, y1), V(c + d0 * r1, y1), false);
        }
    }
    static void Disc(MB b, Vector2 c, float r, float y, int n)
    {
        for (int i = 0; i < n; i++) { float a0 = i * 2 * Mathf.PI / n, a1 = (i + 1) * 2 * Mathf.PI / n; b.Tri(V(c, y), V(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, y), V(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, y)); }
    }

    // patio slab: a 1 m grid clipped to the polygon, each vertex on the ground + lift, skipping cells under the building
    static MB Drape(List<Vector2> poly, float lift, List<Wing> wings)
    {
        var b = new MB(); float x0 = poly.Min(p => p.x), x1 = poly.Max(p => p.x), z0 = poly.Min(p => p.y), z1 = poly.Max(p => p.y);
        for (float x = x0; x < x1; x += 1f) for (float z = z0; z < z1; z += 1f)
            {
                var q = new[] { new Vector2(x, z), new Vector2(x + 1, z), new Vector2(x + 1, z + 1), new Vector2(x, z + 1) };
                if (!q.All(p => Inside(poly, p))) continue;
                if (wings.Any(w => q.All(p => { var d = p - w.c; return Mathf.Abs(Vector2.Dot(d, w.ax)) < w.len / 2 && Mathf.Abs(Vector2.Dot(d, w.ay)) < w.wid / 2; }))) continue;
                b.Quad(V(q[0], Ground(q[0].x, q[0].y) + lift), V(q[3], Ground(q[3].x, q[3].y) + lift), V(q[2], Ground(q[2].x, q[2].y) + lift), V(q[1], Ground(q[1].x, q[1].y) + lift), false, 1.5f);
            }
        return b;
    }
    static bool Inside(List<Vector2> poly, Vector2 p) { bool c = false; for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c; return c; }

    // top of the course surface (terrain or OPCD mesh), ignoring trees and anything already in CLUBHOUSE
    static float Ground(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 1000, z), Vector3.down, 2000))
        {
            var r = h.collider.transform.root.name; if (r == ROOT || r == "LIDAR_TREES" || r == "SHRUBS" || r == "NETS" || r == "FENCES") continue;
            var pm = h.collider.sharedMaterial; if (pm != null && pm.name.StartsWith("GSPleaves")) continue;
            best = Mathf.Max(best, h.point.y);
        }
        if (float.IsNegativeInfinity(best)) foreach (var t in Terrain.activeTerrains) best = Mathf.Max(best, t.SampleHeight(new Vector3(x, 0, z)) + t.GetPosition().y);
        return best;
    }

    static void Part(GameObject root, string name, MB b, Material m, bool collider)
    {
        if (b.v.Count == 0) return;
        // recreate the asset each build: copying over an existing mesh asset left its old GPU data in place (rendered black)
        var mesh = b.Mesh(name); string path = DIR + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var g = new GameObject(name); g.transform.SetParent(root.transform);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        if (collider)
        {
            var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
            string pm = name.Contains("Timber") || name.Contains("Pergola") || name.Contains("Wood") ? "GSPwood" : name.Contains("Cart") || name.Contains("Table") || name.Contains("Chair") || name.Contains("Flag") || name.Contains("Light") || name.Contains("Bar") ? "GSPconcrete" : "GSPstone";
            mc.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/" + pm);
        }
    }

    // mesh builder: quads with UVs in metres (u along the bottom edge, v up the face) divided by tile
    class MB
    {
        public List<Vector3> v = new List<Vector3>(); public List<int> t = new List<int>(); public List<Vector2> uv = new List<Vector2>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool twoSided, float tile = 3f, float len = -1)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            float L = len > 0 ? len : (b - a).magnitude, H = ((d + c) / 2 - (a + b) / 2).magnitude;
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(L / tile, 0)); uv.Add(new Vector2((d - a).magnitude > 0.001f ? Vector3.Dot(c - a, (b - a).normalized) / tile : L / tile, H / tile)); uv.Add(new Vector2(Vector3.Dot(d - a, (b - a).normalized) / tile, H / tile));
            // one face per quad, wound so its normal points away from 'inside' (toward it when 'toward'; up when no inside):
            // coincident front + back faces rendered black in this project's deferred setup
            var fn = Vector3.Cross(c - a, b - a); if (fn.sqrMagnitude < 1e-10f) fn = Vector3.Cross(d - a, c - a); fn.Normalize();
            var want = inside.HasValue ? (a + b + c + d) / 4 - inside.Value : Vector3.up; if (toward) want = -want;
            if (Vector3.Dot(fn, want) >= 0) t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else { t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); fn = -fn; }
            n.Add(fn); n.Add(fn); n.Add(fn); n.Add(fn);
        }
        public Vector3? inside; public bool toward;
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
            var fn = Vector3.Cross(b - a, c - a).normalized; if (fn.y < 0) { t.AddRange(new[] { i, i + 2, i + 1 }); fn = -fn; } else t.AddRange(new[] { i, i + 1, i + 2 });
            for (int k = 0; k < 3; k++) n.Add(fn);
        }
        public List<Vector3> n = new List<Vector3>();
        public Mesh Mesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.SetNormals(n); m.RecalculateTangents(); m.RecalculateBounds(); return m;
        }
    }

    // ------------------------------------------------------------------ materials
    class Mats
    {
        public Material stucco, tile, flat, glass, wood, pavers, stone, ash, frame, linen, dark, cartWhite, canvas, cartNavy, cartSeat, flag, bulb, shrub, rose, concrete;
        public Mats()
        {
            const string G = "Assets/TierraRejada/generated/hillside_logo/";
            // warm beige stucco (BoJa: walls are cream/tan, not white): renders ~ (0.80, 0.74, 0.62) with the noise texture
            stucco = Make("CH_Stucco", new Color(0.91f, 0.89f, 0.84f), 0.02f, Noise("CH_Stucco_tex", new Color(0.95f, 0.93f, 0.88f), 0.06f));
            // dark brown clay S-tile (BoJa's Oct 29 photos read darker and less red than the satellite calibration (0.315, 0.221, 0.186)).
            // A neutral noise texture gives tile variation (the red tile texture can't reach this hue)
            tile = Make("CH_Tile", new Color(0.24f, 0.17f, 0.14f), 0.05f, Noise("CH_Tile_tex", new Color(0.95f, 0.95f, 0.95f), 0.12f));
            flat = Make("CH_RoofWell", new Color(0.62f, 0.62f, 0.6f), 0.05f, null);
            // glazing: blue-tinted, glossy, with a faint warm glow so it reads as a lit interior instead of a black hole
            glass = Make("CH_Glass", new Color(0.16f, 0.20f, 0.26f), 0.92f, null);
            glass.EnableKeyword("_EMISSION"); glass.SetColor("_EmissionColor", new Color(0.42f, 0.29f, 0.14f)); glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            frame = Make("CH_Frame", new Color(0.06f, 0.06f, 0.06f), 0.25f, null);   // black window frames
            wood = Make("CH_WoodDark", new Color(0.17f, 0.11f, 0.07f), 0.12f, null);   // dark-stained loggia timber
            pavers = Make("CH_Pavers", new Color(0.72f, 0.64f, 0.54f), 0.05f, Pavers());
            stone = AssetDatabase.LoadAssetAtPath<Material>(G + "cairn_cobbles.mat") ?? Make("CH_Stone", new Color(0.55f, 0.5f, 0.45f), 0.05f, null);
            ash = Make("CH_Ash", new Color(0.12f, 0.11f, 0.1f), 0.0f, null);
            linen = Make("CH_Linen", new Color(0.90f, 0.89f, 0.86f), 0.05f, null);
            dark = Make("CH_DarkFurniture", new Color(0.07f, 0.07f, 0.07f), 0.15f, null);
            cartWhite = Make("CH_CartWhite", new Color(0.88f, 0.88f, 0.86f), 0.35f, null);
            canvas = Make("CH_Canvas", new Color(0.62f, 0.56f, 0.44f), 0.05f, null);
            cartNavy = Make("CH_CartNavy", new Color(0.07f, 0.10f, 0.22f), 0.55f, null);     // Club Car navy fleet
            cartSeat = Make("CH_CartSeat", new Color(0.60f, 0.48f, 0.34f), 0.15f, null);     // tan seats
            flag = Make("CH_Flag", Color.white, 0.05f, Flag());
            bulb = Make("CH_Bulb", new Color(1f, 0.85f, 0.55f), 0.3f, null); bulb.EnableKeyword("_EMISSION"); bulb.SetColor("_EmissionColor", new Color(1.6f, 1.1f, 0.5f)); bulb.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            shrub = Make("CH_Shrub", new Color(0.18f, 0.30f, 0.14f), 0.05f, Noise("CH_Shrub_tex", new Color(0.95f, 0.95f, 0.95f), 0.25f));
            rose = Make("CH_Rose", new Color(0.78f, 0.20f, 0.30f), 0.1f, null);
            concrete = Make("CH_Concrete", new Color(0.78f, 0.76f, 0.72f), 0.05f, Noise("CH_Concrete_tex", new Color(0.97f, 0.97f, 0.97f), 0.08f));
            flag.mainTextureScale = new Vector2(1f, 2.4f / 1.3f);
        }
        // US flag: 13 stripes, blue canton over the top 7, star dots; v runs 0..1 bottom to top, u 0..1 hoist to fly
        static Texture2D Flag()
        {
            string p = DIR + "/CH_Flag_tex.png"; if (!File.Exists(p)) { var t = new Texture2D(256, 128); for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++) { int stripe = (127 - y) * 13 / 128; Color c = stripe % 2 == 0 ? new Color(0.70f, 0.13f, 0.20f) : Color.white; if (x < 102 && y >= 128 - 69) c = ((x % 10 == 5) && (y % 8 == 4)) ? Color.white : new Color(0.24f, 0.23f, 0.43f); t.SetPixel(x, y, c); } t.wrapMode = TextureWrapMode.Clamp; File.WriteAllBytes(p, t.EncodeToPNG()); AssetDatabase.ImportAsset(p); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        static Material Make(string n, Color c, float gloss, Texture2D tex)
        {
            string p = DIR + "/" + n + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
            m.color = c; m.SetFloat("_Glossiness", gloss); m.mainTexture = tex; EditorUtility.SetDirty(m); return m;
        }
        static Texture2D Noise(string n, Color baseC, float amp)
        {
            string p = DIR + "/" + n + ".png"; if (!File.Exists(p)) { var t = new Texture2D(256, 256); var r = new System.Random(3); for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++) { float k = 1f + amp * (Mathf.PerlinNoise(x * 0.05f, y * 0.05f) - 0.5f) * 2f + amp * 0.5f * ((float)r.NextDouble() - 0.5f); t.SetPixel(x, y, baseC * k); } File.WriteAllBytes(p, t.EncodeToPNG()); AssetDatabase.ImportAsset(p); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        static Texture2D Pavers()
        {
            string p = DIR + "/CH_Pavers_tex.png"; if (!File.Exists(p)) { var t = new Texture2D(256, 256); var r = new System.Random(5); for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++) { bool joint = x % 64 < 3 || y % 64 < 3 || ((y / 64) % 2 == 0 ? (x + 32) % 64 < 3 : false); float k = joint ? 0.7f : 0.95f + 0.1f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f) + 0.04f * (float)r.NextDouble(); t.SetPixel(x, y, new Color(k, k * 0.95f, k * 0.9f)); } File.WriteAllBytes(p, t.EncodeToPNG()); AssetDatabase.ImportAsset(p); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
    }
}
