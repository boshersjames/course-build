using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Flyover review: renders a low GSPro-style flyover (tee -> aim points -> pin, `height` m up, looking ahead and down) for
// one hole per call, a frame every `step` m, and tracks fixed rough patches beside the line: each patch's mean colour is
// sampled in every frame where it is on screen, with and without terrain details: detail_shift is how much the detail
// cards change the patch colour, i.e. the jump the eye sees when the patch crosses the detail distance.
// Also renders close frames of the bunkers and the cart path edge nearest the green.
// Settings: AutomationOutput/flyover_review/settings.txt  "run=<name>;hole=<n>;height=20;step=10"  (hole=0 -> all holes,
// but one hole per call keeps the editor responsive: run it from the bridge in a loop).
// Output: AutomationOutput/flyover_review/<run>/holeNN_fNNN.png, holeNN_patches.csv, holeNN_bunker_k.png, holeNN_path.png
public static class FlyoverReview
{
    static string Root { get { return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/AutomationOutput/flyover_review/"; } }

    [Serializable] class V { public float x, y, z; }
    [Serializable] class Tee { public string TeeType; public bool Enabled; public V Position; }
    [Serializable] class Pin { public V Position; }
    [Serializable] class Hole { public bool Enabled; public int HoleNumber; public Tee[] Tees; public Pin[] Pins; }
    [Serializable] class Course { public Hole[] Holes; }

    static Dictionary<string, string> Settings()
    {
        var d = new Dictionary<string, string> { { "run", "review" }, { "hole", "1" }, { "height", "20" }, { "step", "10" }, { "gkd", "C:/GSProV1/Core/GSP/Courses/TierraRejada/TierraRejada.GKD" } };
        var f = Root + "settings.txt";
        if (File.Exists(f)) foreach (var kv in File.ReadAllText(f).Split(';').Select(s => s.Split('=')).Where(a => a.Length == 2)) d[kv[0].Trim()] = kv[1].Trim();
        return d;
    }

    static float Ground(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 1000f, z), Vector3.down, 2000f))
        {
            var pm = h.collider.sharedMaterial; if (pm != null && pm.name.StartsWith("GSPleaves")) continue;
            best = Mathf.Max(best, h.point.y);
        }
        if (float.IsNegativeInfinity(best)) foreach (var t in Terrain.activeTerrains) best = Mathf.Max(best, t.SampleHeight(new Vector3(x, 0, z)) + t.GetPosition().y);
        return best;
    }

    static List<Vector2> HoleLine(Hole h)
    {
        var tees = h.Tees.Where(t => t.Enabled && t.Position != null && (t.Position.x != 0 || t.Position.z != 0)).ToList();
        var tee = tees.FirstOrDefault(t => t.TeeType == "Black") ?? tees.FirstOrDefault(t => t.TeeType == "Blue") ?? tees.FirstOrDefault(t => !t.TeeType.StartsWith("AimPoint"));
        var line = new List<Vector2> { new Vector2(tee.Position.x, tee.Position.z) };
        foreach (var k in new[] { "AimPoint1", "AimPoint2" }) { var a = tees.FirstOrDefault(t => t.TeeType == k); if (a != null) line.Add(new Vector2(a.Position.x, a.Position.z)); }
        var pin = h.Pins.First(p => p.Position != null); line.Add(new Vector2(pin.Position.x, pin.Position.z));
        return line;
    }

    static Vector2 At(List<Vector2> line, float d, out Vector2 dir)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            float L = (line[i + 1] - line[i]).magnitude; dir = (line[i + 1] - line[i]) / L;
            if (d <= L || i + 2 == line.Count) return line[i] + dir * Mathf.Min(d, L);
            d -= L;
        }
        dir = Vector2.up; return line[line.Count - 1];
    }

    [MenuItem("Course Tools/Automation/Flyover Review (one hole)")]
    public static void Run()
    {
        var s = Settings(); var ci = CultureInfo.InvariantCulture;
        string dir = Root + s["run"] + "/"; Directory.CreateDirectory(dir);
        var course = JsonUtility.FromJson<Course>(File.ReadAllText(s["gkd"]));
        int hn = int.Parse(s["hole"]); float height = float.Parse(s["height"], ci), step = float.Parse(s["step"], ci);
        var hole = course.Holes.First(h => h.HoleNumber == hn);
        var line = HoleLine(hole); float total = 0; for (int i = 0; i + 1 < line.Count; i++) total += (line[i + 1] - line[i]).magnitude;
        // patches: rough spots 35 m left/right of the line at 30/50/70/90 % of the way
        var patches = new List<Vector3>();
        foreach (float f in new[] { 0.3f, 0.5f, 0.7f, 0.9f }) foreach (float side in new[] { -35f, 35f })
            {
                Vector2 dv; var p = At(line, total * f, out dv); var q = p + new Vector2(dv.y, -dv.x) * side; patches.Add(new Vector3(q.x, Ground(q.x, q.y), q.y));
            }
        var go = new GameObject("FlyoverReviewCam"); go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.3f; cam.farClipPlane = 6000f; cam.fieldOfView = 55f; cam.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(960, 540, 24); rt.antiAliasing = 4; cam.targetTexture = rt;
        var csv = new StringBuilder("frame,along_m,patch,dist_m,px,py,r,g,b,lum,lum_nodetail,detail_shift\n"); var terrs = Terrain.activeTerrains; var draw = terrs.Select(t => t.drawTreesAndFoliage).ToArray();
        try
        {
            int fi = 0;
            for (float d = 0; d <= total; d += step, fi++)
            {
                Vector2 dv; var p = At(line, d, out dv); var ahead = At(line, Mathf.Min(total, d + 60f), out dv);
                var pos = new Vector3(p.x, Ground(p.x, p.y) + height, p.y); var look = new Vector3(ahead.x, Ground(ahead.x, ahead.y), ahead.y);
                if ((look - pos).sqrMagnitude < 1f) look = pos + new Vector3(dv.x, -0.5f, dv.y);
                cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized);
                var img = Grab(cam, rt); File.WriteAllBytes(dir + string.Format("hole{0:00}_f{1:000}.png", hn, fi), img.EncodeToJPG(80));
                foreach (var t in terrs) t.drawTreesAndFoliage = false; var img2 = Grab(cam, rt); for (int q = 0; q < terrs.Length; q++) terrs[q].drawTreesAndFoliage = draw[q];
                for (int k = 0; k < patches.Count; k++)
                {
                    var sp = cam.WorldToScreenPoint(patches[k]); if (sp.z <= 0 || sp.x < 20 || sp.y < 20 || sp.x > 940 || sp.y > 520) continue;
                    Color sum = Color.clear, sum2 = Color.clear; int n = 0;
                    for (int y = -10; y <= 10; y += 2) for (int x = -10; x <= 10; x += 2) { sum += img.GetPixel((int)sp.x + x, (int)sp.y + y); sum2 += img2.GetPixel((int)sp.x + x, (int)sp.y + y); n++; }
                    var c = sum / n; var c2 = sum2 / n; float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b, lum2 = 0.3f * c2.r + 0.59f * c2.g + 0.11f * c2.b;
                    csv.AppendLine(string.Format(ci, "{0},{1:F0},{2},{3:F0},{4:F0},{5:F0},{6:F3},{7:F3},{8:F3},{9:F3},{10:F3},{11:F3}", fi, d, k, (patches[k] - pos).magnitude, sp.x, sp.y, c.r, c.g, c.b, lum, lum2, (lum - lum2) / Mathf.Max(lum2, 0.05f)));
                }
                Object.DestroyImmediate(img);
            }
            // close-ups: bunkers and cart path nearest the pin
            var pin = line[line.Count - 1]; var opcd = GameObject.Find("OPCD Meshes");
            if (opcd != null)
            {
                var bunkers = opcd.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("_Bunker_-_Mesh") && (new Vector2(r.bounds.center.x, r.bounds.center.z) - pin).magnitude < 60f).ToList();
                int bi = 0;
                foreach (var b in bunkers)
                {
                    var c = b.bounds.center; var back = (new Vector2(c.x, c.z) - pin).normalized; if (back == Vector2.zero) back = Vector2.up;
                    var pos = new Vector3(c.x + back.x * 14f, Ground(c.x, c.z) + 5f, c.z + back.y * 14f);
                    cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation((c - pos).normalized);
                    var img = Grab(cam, rt); File.WriteAllBytes(dir + string.Format("hole{0:00}_bunker{1}.png", hn, bi++), img.EncodeToJPG(85)); Object.DestroyImmediate(img);
                }
            }
            var paths = GameObject.Find("CART_PATHS");
            if (paths != null)
            {
                Vector3 bestP = Vector3.zero; float bd = float.MaxValue;
                foreach (var mf in paths.GetComponentsInChildren<MeshFilter>())
                {
                    var v = mf.sharedMesh.vertices; var tr = mf.transform;
                    for (int i = 0; i < v.Length; i += 7) { var w = tr.TransformPoint(v[i]); float dd = (new Vector2(w.x, w.z) - pin).sqrMagnitude; if (dd < bd) { bd = dd; bestP = w; } }
                }
                if (bd < 100f * 100f)
                {
                    var pos = bestP + new Vector3(4f, 2.5f, 4f);
                    cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation((bestP - pos).normalized);
                    var img = Grab(cam, rt); File.WriteAllBytes(dir + string.Format("hole{0:00}_path.png", hn), img.EncodeToJPG(85)); Object.DestroyImmediate(img);
                }
            }
        }
        finally { for (int q = 0; q < terrs.Length; q++) terrs[q].drawTreesAndFoliage = draw[q]; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        File.WriteAllText(dir + string.Format("hole{0:00}_patches.csv", hn), csv.ToString());
    }

    static Texture2D Grab(Camera cam, RenderTexture rt)
    {
        cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
        var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); img.Apply();
        RenderTexture.active = prev; return img;
    }
}

