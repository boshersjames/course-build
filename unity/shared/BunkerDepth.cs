using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Gives flat OPCD bunkers a bowl: interior vertices of each "..._Bunker_-_Mesh" go down by depth * smoothstep(d / ramp),
// d = distance to the bunker rim (boundary edges, welded). The rim vertices do not move, so the seam with the blend ring
// and the rough stays closed. The face toward the green (nearest pin) gets a shorter ramp, i.e. a steeper face.
// Settings: AutomationOutput/bunker_depth.txt  "bunkers=Spline_bunker_29,Spline_bunker_30 (or *);depth=0.6;ramp=2.5;steep=1.2;pins=x,z;x,z"
// Meshes are saved under Assets/TierraRejada/generated/bunkers; log.json keeps the previous mesh for Restore.
public static class BunkerDepth
{
    const string Folder = "Assets/TierraRejada/generated/bunkers", LogPath = Folder + "/log.json";
    [Serializable] public class Entry { public string obj; public string previousMesh; public string previousPath; public float maxDrop; }
    [Serializable] public class Log { public List<Entry> entries = new List<Entry>(); }

    static Dictionary<string, string> Settings()
    {
        var d = new Dictionary<string, string> { { "bunkers", "*" }, { "depth", "0.6" }, { "ramp", "2.5" }, { "steep", "1.2" }, { "pins", "" } };
        var f = Path.GetDirectoryName(Application.dataPath) + "/AutomationOutput/bunker_depth.txt";
        if (File.Exists(f)) { string last = null; foreach (var part in File.ReadAllText(f).Split(';')) { var a = part.Split('='); if (a.Length == 2) { last = a[0].Trim(); d[last] = a[1].Trim(); } else if (last != null && part.Trim() != "") d[last] += ";" + part.Trim(); } }   // pins=x,z;x,z;... keeps every pin
        return d;
    }

    [MenuItem("Tierra/Bunker Depth/Apply")]
    public static void Apply()
    {
        var s = Settings(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        float depth = float.Parse(s["depth"], ci), ramp = float.Parse(s["ramp"], ci), steep = float.Parse(s["steep"], ci);
        var pins = s["pins"].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split(',')).Select(a => new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci))).ToList();
        var names = s["bunkers"].Split(',').Select(n => n.Trim()).ToArray();
        var opcd = GameObject.Find("OPCD Meshes"); Directory.CreateDirectory(Folder);
        var log = File.Exists(LogPath) ? JsonUtility.FromJson<Log>(File.ReadAllText(LogPath)) : new Log(); int done = 0;
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.Contains("_Bunker_-_Mesh") && (names.Contains("*") || names.Any(n => m.name.StartsWith(n + "_")))))
        {
            var entry = log.entries.FirstOrDefault(e => e.obj == mf.name); var src = mf.sharedMesh;
            if (entry != null) { var prev = AssetDatabase.LoadAllAssetsAtPath(entry.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == entry.previousMesh); if (prev != null) src = prev; }
            if (!src.isReadable) continue;
            var tr = mf.transform; var v = src.vertices; var t = src.triangles; var w = v.Select(p => tr.TransformPoint(p)).ToArray();
            // weld + boundary edges
            var key = new Dictionary<Vector3Int, int>(); var id = new int[v.Length];
            for (int i = 0; i < v.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(w[i].x * 1000), Mathf.RoundToInt(w[i].y * 1000), Mathf.RoundToInt(w[i].z * 1000)); int j; if (!key.TryGetValue(k, out j)) { j = key.Count; key[k] = j; } id[i] = j; }
            var cnt = new Dictionary<long, int>(); Func<int, int, long> E = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { long k = E(id[t[i + e]], id[t[i + (e + 1) % 3]]); int c; cnt.TryGetValue(k, out c); cnt[k] = c + 1; }
            var rimIds = new HashSet<int>(); var segs = new List<Vector2[]>(); var pos = new Dictionary<int, Vector3>();
            for (int i = 0; i < v.Length; i++) pos[id[i]] = w[i];
            foreach (var kv in cnt.Where(k => k.Value == 1)) { int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff); rimIds.Add(a); rimIds.Add(b); segs.Add(new[] { new Vector2(pos[a].x, pos[a].z), new Vector2(pos[b].x, pos[b].z) }); }
            var c2 = new Vector2(w.Average(p => p.x), w.Average(p => p.z));
            Vector2 pin = pins.Count > 0 ? pins.OrderBy(p => (p - c2).sqrMagnitude).First() : c2;
            Vector2 toPin = (pin - c2).normalized;
            float maxDrop = 0f; var nv = (Vector3[])v.Clone();
            for (int i = 0; i < v.Length; i++)
            {
                if (rimIds.Contains(id[i])) continue;
                var p = new Vector2(w[i].x, w[i].z); float d = float.MaxValue;
                foreach (var sg in segs) { var ab = sg[1] - sg[0]; float tt = Mathf.Clamp01(Vector2.Dot(p - sg[0], ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f)); d = Mathf.Min(d, (sg[0] + ab * tt - p).magnitude); }
                float side = Vector2.Dot((p - c2).normalized, toPin);                  // +1 = green side
                float r = Mathf.Lerp(ramp, steep, Mathf.Clamp01(side));
                float k = Mathf.Clamp01(d / r); k = k * k * (3f - 2f * k);
                float drop = depth * k; var q = w[i]; q.y -= drop; nv[i] = tr.InverseTransformPoint(q); maxDrop = Mathf.Max(maxDrop, drop);
            }
            var m2 = Object.Instantiate(src); m2.name = mf.name + "_deep"; m2.vertices = nv; m2.RecalculateNormals(); m2.RecalculateBounds();
            string path = Folder + "/" + mf.name + "_deep.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m2, old); m2 = old; } else AssetDatabase.CreateAsset(m2, path);
            if (entry == null) { entry = new Entry { obj = mf.name, previousMesh = src.name, previousPath = AssetDatabase.GetAssetPath(src) }; log.entries.Add(entry); }
            entry.maxDrop = maxDrop; File.WriteAllText(LogPath, JsonUtility.ToJson(log, true));
            mf.sharedMesh = m2; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = m2; }
            done++;
        }
        int lowered = LowerTerrainUnder(opcd, m => names.Contains("*") || names.Any(n => m.name.StartsWith(n + "_")));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("BunkerDepth: terrain lowered under bunkers at " + lowered + " heightmap vertices");
        Debug.Log("BunkerDepth: deepened " + done + " bunkers (depth " + depth + ", ramp " + ramp + ", green-side ramp " + steep + ")");
    }

    // Graded profile (Oct 28, BoJa play test: balls ran across the sand). Per bunker: depth from its area (< 60 m2: 0.25 m,
    // 60-150: 0.35, > 150: 0.5), a flat floor (the bunker's tilt kept but capped at 5 % (2.9 deg)), and faces sized per direction so the
    // steepest slope is about 27 deg on the green side (or the high side for a bunker with no green within 60 m) and under 11 deg
    // on the entry side. Rim vertices stay put. Builds from the original mesh in log.json like Apply. Writes bunker_grade.csv.
    [MenuItem("Tierra/Bunker Depth/Apply Graded")]
    public static void ApplyGraded()
    {
        var s = Settings(); var ci = System.Globalization.CultureInfo.InvariantCulture;
        var pins = s["pins"].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split(',')).Select(a => new Vector2(float.Parse(a[0], ci), float.Parse(a[1], ci))).ToList();
        var opcd = GameObject.Find("OPCD Meshes"); Directory.CreateDirectory(Folder);
        var log = File.Exists(LogPath) ? JsonUtility.FromJson<Log>(File.ReadAllText(LogPath)) : new Log(); int done = 0;
        var report = new System.Text.StringBuilder("bunker,area_m2,depth_m,steep_side,face_steep_deg,face_entry_deg,floor_deg,max_drop_m\n");
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.Contains("_Bunker_-_Mesh")))
        {
            var entry = log.entries.FirstOrDefault(e => e.obj == mf.name); var src = mf.sharedMesh;
            if (entry != null) { var prev = AssetDatabase.LoadAllAssetsAtPath(entry.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == entry.previousMesh); if (prev != null) src = prev; }
            if (!src.isReadable) continue;
            var tr = mf.transform; var v = src.vertices; var t = src.triangles; var w = v.Select(p => tr.TransformPoint(p)).ToArray();
            var key = new Dictionary<Vector3Int, int>(); var id = new int[v.Length];
            for (int i = 0; i < v.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(w[i].x * 1000), Mathf.RoundToInt(w[i].y * 1000), Mathf.RoundToInt(w[i].z * 1000)); int j; if (!key.TryGetValue(k, out j)) { j = key.Count; key[k] = j; } id[i] = j; }
            var cnt = new Dictionary<long, int>(); Func<int, int, long> E = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { long k = E(id[t[i + e]], id[t[i + (e + 1) % 3]]); int c; cnt.TryGetValue(k, out c); cnt[k] = c + 1; }
            var rimIds = new HashSet<int>(); var segs = new List<Vector2[]>(); var pos = new Dictionary<int, Vector3>();
            for (int i = 0; i < v.Length; i++) pos[id[i]] = w[i];
            foreach (var kv in cnt.Where(k => k.Value == 1)) { int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff); rimIds.Add(a); rimIds.Add(b); segs.Add(new[] { new Vector2(pos[a].x, pos[a].z), new Vector2(pos[b].x, pos[b].z) }); }
            float area = 0; for (int i = 0; i < t.Length; i += 3) area += Mathf.Abs(Vector3.Cross(w[t[i + 1]] - w[t[i]], w[t[i + 2]] - w[t[i]]).y) / 2;
            float depth = area < 60 ? 0.25f : area <= 150 ? 0.35f : 0.5f;
            var c2 = new Vector2(w.Average(p => p.x), w.Average(p => p.z));
            // steep side: toward the nearest pin if it is within 60 m, else toward the higher rim
            // steep faces allowed toward the green (nearest green within 60 m) and on the high side (uphill rim); the rest is entry
            Vector2 pin = pins.Count > 0 ? pins.OrderBy(p => (p - c2).sqrMagnitude).First() : c2; bool nearGreen = pins.Count > 0 && (pin - c2).magnitude < 60f;
            Vector2 greenDir = (pin - c2).normalized, highDir;
            {
                var rim = rimIds.Select(r => pos[r]).ToList(); float my = rim.Average(p => p.y); var hd = Vector2.zero;
                foreach (var p in rim) hd += (new Vector2(p.x, p.z) - c2).normalized * (p.y - my);
                highDir = hd.sqrMagnitude > 1e-6f ? hd.normalized : Vector2.up;
            }
            string side = nearGreen ? "green+high" : "high";
            Func<Vector2, float> stOf = p => { var d = (p - c2).normalized; return Mathf.Max(nearGreen ? Vector2.Dot(d, greenDir) : -1f, Vector2.Dot(d, highDir)); };
            // floor plane: least-squares plane of the original surface, tilt capped at 5 %
            double sx = 0, sz = 0, sy = 0, sxx = 0, szz = 0, sxz = 0, sxy = 0, szy = 0; int n0 = w.Length;
            foreach (var p in w) { double x = p.x - c2.x, z = p.z - c2.y; sx += x; sz += z; sy += p.y; sxx += x * x; szz += z * z; sxz += x * z; sxy += x * p.y; szy += z * p.y; }
            float gx = 0, gz = 0;
            { double cxy = sxy - sx * sy / n0, czy = szy - sz * sy / n0, cxx = sxx - sx * sx / n0, czz = szz - sz * sz / n0, cxz = sxz - sx * sz / n0; double d2 = cxx * czz - cxz * cxz; if (Math.Abs(d2) > 1e-9) { gx = (float)((cxy * czz - czy * cxz) / d2); gz = (float)((czy * cxx - cxy * cxz) / d2); } }
            var g = new Vector2(gx, gz); if (g.magnitude > 0.05f) g = g.normalized * 0.05f;   // 5 % = 2.9 deg
            var dist = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = new Vector2(w[i].x, w[i].z); float d = float.MaxValue;
                foreach (var sg in segs) { var ab = sg[1] - sg[0]; float tt = Mathf.Clamp01(Vector2.Dot(p - sg[0], ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f)); d = Mathf.Min(d, (sg[0] + ab * tt - p).magnitude); }
                dist[i] = rimIds.Contains(id[i]) ? 0 : d;
            }
            // floor plane: depth below the mean rim height, raised where the faces could not reach it within the slope limits
            // (each face may run at most 85 % of the bunker's inner radius), never above the lowest rim minus 8 cm
            float dmax = dist.Max(), y0 = rimIds.Average(r => pos[r].y) - depth, cap = float.MaxValue;
            Func<Vector3, float> tilt = p => g.x * (p.x - c2.x) + g.y * (p.z - c2.y);
            Func<Vector3, float> floorY = p => y0 + tilt(p);
            foreach (var r in rimIds)
            {
                var rp = pos[r]; float st0 = Mathf.Clamp01(stOf(new Vector2(rp.x, rp.z)));
                float hAllow = Mathf.Tan(Mathf.Lerp(9f, 25f, st0) * Mathf.Deg2Rad) * 0.85f * dmax / 1.5f;
                y0 = Mathf.Max(y0, rp.y - hAllow - tilt(rp)); cap = Mathf.Min(cap, rp.y - 0.08f - tilt(rp));
            }
            y0 = Mathf.Min(y0, cap);
            var rimSegs = cnt.Where(k => k.Value == 1).Select(kv => new[] { pos[(int)(kv.Key >> 32)], pos[(int)(kv.Key & 0xffffffff)] }).ToList();
            float maxDrop = 0f; var nv = (Vector3[])v.Clone(); var onFloor = new bool[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                if (rimIds.Contains(id[i])) continue;
                // face from the height of the nearest rim point, so the hillside grade doesn't add to it
                var p = new Vector2(w[i].x, w[i].z); float rimY = w[i].y;
                { float best = float.MaxValue; foreach (var sg in rimSegs) { var ab = new Vector2(sg[1].x - sg[0].x, sg[1].z - sg[0].z); float tt = Mathf.Clamp01(Vector2.Dot(p - new Vector2(sg[0].x, sg[0].z), ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f)); var c = Vector3.Lerp(sg[0], sg[1], tt); float dd = (new Vector2(c.x, c.z) - p).sqrMagnitude; if (dd < best) { best = dd; rimY = c.y; } } }
                float h = Mathf.Max(0.01f, rimY - floorY(w[i]));
                float st = Mathf.Clamp01(stOf(p));
                float ang = Mathf.Lerp(9f, 25f, st) * Mathf.Deg2Rad;   // targets a bit under the 12 / 27 deg limits: smoothing spreads them
                float R = Mathf.Min(0.85f * dmax, 1.5f * h / Mathf.Tan(ang));                           // smoothstep's steepest slope is 1.5 h / R
                float k = Mathf.Clamp01(dist[i] / R); onFloor[i] = k >= 1f; k = k * k * (3f - 2f * k);
                var q = w[i]; q.y = Mathf.Lerp(rimY, floorY(w[i]), k); nv[i] = tr.InverseTransformPoint(q); maxDrop = Mathf.Max(maxDrop, w[i].y - q.y);
            }
            // smooth the sand (the original surface carries LiDAR noise): Laplacian passes over welded vertices, rim + lip strip fixed
            {
                var ny = new Dictionary<int, float>(); var orig = new Dictionary<int, float>(); var dd = new Dictionary<int, float>();
                for (int i = 0; i < v.Length; i++) { ny[id[i]] = tr.TransformPoint(nv[i]).y; orig[id[i]] = w[i].y; dd[id[i]] = dist[i]; }
                var nb = new Dictionary<int, HashSet<int>>(); foreach (var k in ny.Keys) nb[k] = new HashSet<int>();
                for (int i = 0; i < t.Length; i += 3) for (int e = 0; e < 3; e++) { int a = id[t[i + e]], b = id[t[i + (e + 1) % 3]]; nb[a].Add(b); nb[b].Add(a); }
                var fl0 = new HashSet<int>(); for (int i = 0; i < v.Length; i++) if (onFloor[i]) fl0.Add(id[i]);
                var free = ny.Keys.Where(k => dd[k] >= 0.2f && !rimIds.Contains(k) && !fl0.Contains(k)).ToList();   // the floor stays on its plane
                for (int it = 0; it < 40; it++)
                {
                    var upd = new Dictionary<int, float>();
                    foreach (var k in free) { float avg = nb[k].Average(j => ny[j]); upd[k] = 0.5f * ny[k] + 0.5f * avg; }
                    foreach (var kv in upd) ny[kv.Key] = kv.Value;
                }
                for (int i = 0; i < v.Length; i++) { var q = w[i]; q.y = ny[id[i]]; nv[i] = tr.InverseTransformPoint(q); maxDrop = Mathf.Max(maxDrop, w[i].y - q.y); }
            }
            var m2 = Object.Instantiate(src); m2.name = mf.name + "_deep"; m2.vertices = nv; m2.RecalculateNormals(); m2.RecalculateBounds();
            string path = Folder + "/" + mf.name + "_deep.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { EditorUtility.CopySerialized(m2, old); m2 = old; m2.vertices = nv; m2.RecalculateNormals(); m2.RecalculateBounds(); EditorUtility.SetDirty(m2); }   // re-upload: CopySerialized alone leaves the old render buffers else AssetDatabase.CreateAsset(m2, path);
            if (entry == null) { entry = new Entry { obj = mf.name, previousMesh = src.name, previousPath = AssetDatabase.GetAssetPath(src) }; log.entries.Add(entry); }
            entry.maxDrop = maxDrop; File.WriteAllText(LogPath, JsonUtility.ToJson(log, true));
            mf.sharedMesh = m2; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = m2; }
            // measured: steepest triangle on the steep side, on the entry side, and on the floor
            // measured: area-weighted 95th percentile slope on the steep face, the entry face and the floor (rim lip strip skipped)
            var lS = new List<KeyValuePair<float, float>>(); var lE = new List<KeyValuePair<float, float>>(); var lF = new List<KeyValuePair<float, float>>(); var wn = nv.Select(p => tr.TransformPoint(p)).ToArray();
            for (int i = 0; i < t.Length; i += 3)
            {
                if (Mathf.Min(dist[t[i]], Mathf.Min(dist[t[i + 1]], dist[t[i + 2]])) < 0.3f) continue;   // the mesh's own 6 cm rim lip strip
                var cr = Vector3.Cross(wn[t[i + 1]] - wn[t[i]], wn[t[i + 2]] - wn[t[i]]); float a = Vector3.Angle(cr.y < 0 ? -cr : cr, Vector3.up), ar = Mathf.Abs(cr.y) / 2;
                var cc = (wn[t[i]] + wn[t[i + 1]] + wn[t[i + 2]]) / 3; float st = stOf(new Vector2(cc.x, cc.z));
                var kv = new KeyValuePair<float, float>(a, ar);
                if (onFloor[t[i]] && onFloor[t[i + 1]] && onFloor[t[i + 2]]) lF.Add(kv); else if (st > 0.5f) lS.Add(kv); else if (st < 0) lE.Add(kv);
            }
            Func<List<KeyValuePair<float, float>>, float> p95 = l => { if (l.Count == 0) return 0; var o = l.OrderBy(x => x.Key).ToList(); float tot = o.Sum(x => x.Value), acc = 0; foreach (var x in o) { acc += x.Value; if (acc >= 0.95f * tot) return x.Key; } return o.Last().Key; };
            float fS = p95(lS), fE = p95(lE), fl = p95(lF);
            report.AppendLine(string.Format(ci, "{0},{1:F0},{2:F2},{3},{4:F1},{5:F1},{6:F1},{7:F2},{8:F2},{9:F1}", mf.name.Replace("_Bunker_-_Mesh", ""), area, depth, side, fS, fE, fl, maxDrop, rimIds.Max(r => pos[r].y) - rimIds.Min(r => pos[r].y), dmax));
            done++;
        }
        int lowered = LowerTerrainUnder(opcd, m => true);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        File.WriteAllText(Path.GetDirectoryName(Application.dataPath) + "/AutomationOutput/bunker_grade.csv", report.ToString());
        Debug.Log("BunkerDepth: graded " + done + " bunkers, terrain lowered at " + lowered + " heightmap vertices");
    }

    // the terrain sits just under the course meshes; push it below the new sand so it can't show through
    static int LowerTerrainUnder(GameObject opcd, Func<MeshFilter, bool> sel)
    {
        int lowered = 0;
        foreach (var terrain in Terrain.activeTerrains)
        {
            var td = terrain.terrainData; int res = td.heightmapResolution; float cell = td.size.x / (res - 1), sy = td.size.y; var tp = terrain.GetPosition();
            foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.Contains("_Bunker_-_Mesh") && sel(m)))
            {
                var col = mf.GetComponent<Collider>(); if (col == null) continue; var b = col.bounds;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - tp.x) / cell) - 1), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((b.max.x - tp.x) / cell) + 1);
                int z0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - tp.z) / cell) - 1), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((b.max.z - tp.z) / cell) + 1);
                var h = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1); bool any = false;
                for (int j = 0; j <= z1 - z0; j++) for (int i = 0; i <= x1 - x0; i++)
                    {
                        float x = tp.x + (x0 + i) * cell, z = tp.z + (z0 + j) * cell; float low = float.MaxValue;
                        foreach (var dx in new[] { -0.6f, 0f, 0.6f }) foreach (var dz in new[] { -0.6f, 0f, 0.6f })
                            { RaycastHit hit; if (col.Raycast(new Ray(new Vector3(x + dx, b.max.y + 5f, z + dz), Vector3.down), out hit, b.size.y + 10f)) low = Mathf.Min(low, hit.point.y); }
                        if (low == float.MaxValue) continue;
                        float target = (low - 0.15f - tp.y) / sy; if (h[j, i] > target) { h[j, i] = target; any = true; lowered++; }
                    }
                if (any) { Undo.RegisterCompleteObjectUndo(td, "Bunker terrain"); td.SetHeights(x0, z0, h); }
            }
            terrain.Flush(); EditorUtility.SetDirty(td);
        }
        return lowered;
    }

    [MenuItem("Tierra/Bunker Depth/Restore")]
    public static void Restore()
    {
        if (!File.Exists(LogPath)) return; var opcd = GameObject.Find("OPCD Meshes"); var log = JsonUtility.FromJson<Log>(File.ReadAllText(LogPath));
        foreach (var e in log.entries)
        {
            var mf = opcd.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == e.obj); var prev = AssetDatabase.LoadAllAssetsAtPath(e.previousPath).OfType<Mesh>().FirstOrDefault(m => m.name == e.previousMesh);
            if (mf != null && prev != null) { mf.sharedMesh = prev; var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = prev; }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
