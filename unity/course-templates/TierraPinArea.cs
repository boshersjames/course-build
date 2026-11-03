using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// Pin-area test the way GreenKeeper reads a lie (our own version of the idea in Green Studio, 10/31): at a point, probe 0.3 m N/E/S/W
// on the top surface, angle = asin(dz / 0.3), rounded to 0.1 deg; a pin area passes when the worst angle over 17 points (the pin, 8 at
// 0.45 m, 8 at 0.9 m) is <= `limitDeg` (1.7 deg = our 3 %). For every GKD pin it reports the worst angle; per green it reports the
// usable-area % (0.75 m grid over the green under the pins, points >= 1.2 m inside the green edge) and the pins >= 3 m apart.
//
// Also, per pin: the distance to the green edge (flag < `pinEdge`), and `fit` = the slope of a least-squares plane through all 85
// probe heights with its RMS residual in mm. The 0.3 m probes see mesh facets (a 9 mm step over 0.3 m is already 1.7 deg), so a pin
// that FAILs on the probes but has a low fit and a high residual reads facet noise, while a high fit is a genuinely steep spot.
// Per green: `areas` = how many pin spots >= `pinSep` apart pass (>= `pinEdge` inside the edge, flattest first); the rule is >= 4.
// Writes AutomationOutput/pin_area.txt and pin_area.csv (one row per pin); changes nothing.
public class TierraPinArea
{
    public string gkd = @"C:\Users\user\Documents\newcourses\tr-automation\greenkeeper\TierraRejada.GKD";
    public float limitDeg = 1.7f, probe = 0.3f, grid = 0.75f, edge = 1.2f;
    public float pinEdge = 3f, pinSep = 3f; public int minAreas = 4;
    public string holes = "";   // e.g. "2,9"; empty = all
    [System.Serializable] class P { public float x, y, z; }
    [System.Serializable] class Pin { public string Day; public P Position; }
    [System.Serializable] class Hole { public int HoleNumber; public Pin[] Pins; }
    [System.Serializable] class G { public Hole[] Holes; }
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    // surface lookup: the top OPCD hit (name, y); tests swap in an analytic surface
    public System.Func<float, float, KeyValuePair<string, float>> top;
    Transform opcd;
    KeyValuePair<string, float> Top(float x, float z) { return top(x, z); }
    KeyValuePair<string, float> RayTop(float x, float z)
    {
        string n = ""; float y = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 400, z), Vector3.down, 800)) { if (h.collider.transform.root != opcd) continue; if (h.point.y > y) { y = h.point.y; n = h.collider.name; } }
        return new KeyValuePair<string, float>(n, y);
    }

    public struct Read { public float worst, fit, rmsMm; }
    static readonly Vector2[] Dirs = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) };

    float Lie(float x, float z, List<Vector3> pts)
    {
        float c = Top(x, z).Value, worst = 0; if (pts != null) pts.Add(new Vector3(x, c, z));
        foreach (var d0 in Dirs)
        {
            var d = d0 * probe; float y = Top(x + d.x, z + d.y).Value; float dz = y - c; if (pts != null) pts.Add(new Vector3(x + d.x, y, z + d.y));
            float a = Mathf.Round(Mathf.Asin(Mathf.Clamp(dz / probe, -1, 1)) * Mathf.Rad2Deg * 10) / 10f; worst = Mathf.Max(worst, Mathf.Abs(a));
        }
        return worst;
    }
    public Read Area(float x, float z)
    {
        var pts = new List<Vector3>(85); float w = Lie(x, z, pts);
        foreach (var r in new[] { 0.45f, 0.9f }) for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; w = Mathf.Max(w, Lie(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r, pts)); }
        var rd = new Read { worst = w, fit = 90, rmsMm = float.NaN };
        var good = pts.Where(p => p.y > -1e5f).ToList(); float pa, pb, pc, ox, oz;
        if (good.Count == pts.Count && TierraGeom.FitPlane(good, out pa, out pb, out pc, out ox, out oz))
        {
            rd.fit = TierraGeom.SlopeDeg(pb, pc); double ss = 0; foreach (var p in good) { double e = p.y - TierraGeom.PlaneY(pa, pb, pc, ox, oz, p.x, p.z); ss += e * e; }
            rd.rmsMm = (float)(System.Math.Sqrt(ss / good.Count) * 1000);
        }
        return rd;
    }
    bool Inside(string green, float x, float z, float r)
    {
        if (Top(x, z).Key != green) return false;
        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; if (Top(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r).Key != green) return false; }
        return true;
    }

    // greedy pick of pin spots at least `sep` apart, flattest first
    public static int CountAreas(List<KeyValuePair<Vector2, float>> ok, float sep, List<Vector2> chosen = null)
    {
        var pick = chosen ?? new List<Vector2>();
        foreach (var c in ok.OrderBy(q => q.Value).ThenBy(q => q.Key.x).ThenBy(q => q.Key.y)) if (pick.All(p => (p - c.Key).magnitude >= sep)) pick.Add(c.Key);
        return pick.Count;
    }

    // the green's outline (its mesh boundary edges, xz) for edge distances
    public static TierraGeom.SegIndex Outline(Vector3[] w, int[] tris)
    {
        var s = new TierraGeom.SegIndex(2f); foreach (var e in TierraGeom.BoundaryEdges(tris)) s.Add(TierraGeom.XZ(w[e.a]), TierraGeom.XZ(w[e.b])); return s;
    }

    public string Run()
    {
        Physics.SyncTransforms(); opcd = GameObject.Find("OPCD Meshes").transform; if (top == null) top = RayTop;
        var g = JsonUtility.FromJson<G>(File.ReadAllText(gkd).TrimStart('\uFEFF'));
        var outlines = new Dictionary<string, TierraGeom.SegIndex>(); var bounds = new Dictionary<string, Bounds>();
        foreach (var mf in opcd.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh || outlines.ContainsKey(mf.name) || !mf.name.ToLower().Contains("green_-_mesh")) continue;
            var w = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray(); outlines[mf.name] = Outline(w, mf.sharedMesh.triangles);
            var b = new Bounds(w[0], Vector3.zero); foreach (var p in w) b.Encapsulate(p); bounds[mf.name] = b;
        }
        return Report(g, outlines, bounds);
    }

    string Report(G g, Dictionary<string, TierraGeom.SegIndex> outlines, Dictionary<string, Bounds> bounds)
    {
        var only = new HashSet<int>(holes.Split(',').Where(s => s.Trim().Length > 0).Select(s => int.Parse(s.Trim())));
        var o = new List<string>(); var csv = new List<string> { "hole,day,x,z,green,worst_deg,fit_deg,rms_mm,edge_m,flags" }; int fails = 0, steep = 0, nearEdge = 0, thinGreens = 0;
        foreach (var h in g.Holes)
        {
            if (only.Count > 0 && !only.Contains(h.HoleNumber)) continue;
            var pins = h.Pins.Where(p => p.Position != null).ToList(); if (pins.Count == 0) continue;
            var green = pins.Select(p => Top(p.Position.x, p.Position.z).Key).GroupBy(n => n).OrderByDescending(q => q.Count()).First().Key;
            TierraGeom.SegIndex ol; outlines.TryGetValue(green, out ol);
            var line = new List<string>();
            foreach (var p in pins)
            {
                var rd = Area(p.Position.x, p.Position.z); var on = Top(p.Position.x, p.Position.z).Key; float ed = ol != null ? ol.Dist(new Vector2(p.Position.x, p.Position.z), 20f) : float.NaN;
                bool ok = rd.worst <= limitDeg + 1e-4f && on == green; if (!ok) fails++; if (rd.fit > limitDeg + 1e-4f) steep++; if (ed < pinEdge) nearEdge++;
                var fl = new List<string>(); if (on != green) fl.Add("NOT ON " + green); if (!ok) fl.Add("FAIL"); if (rd.fit > limitDeg + 1e-4f) fl.Add("STEEP"); if (ed < pinEdge) fl.Add("EDGE");
                line.Add(p.Day.Substring(0, 3) + " " + rd.worst.ToString("F1", CI) + "deg (fit " + rd.fit.ToString("F1", CI) + ", " + rd.rmsMm.ToString("F0", CI) + " mm" + (ed < pinEdge ? ", " + ed.ToString("F1", CI) + " m from edge" : "") + ")" + (on != green ? " NOT ON " + green : "") + (ok ? "" : " FAIL"));
                csv.Add(string.Join(",", new[] { h.HoleNumber.ToString(), p.Day, p.Position.x.ToString("F2", CI), p.Position.z.ToString("F2", CI), green, rd.worst.ToString("F1", CI), rd.fit.ToString("F2", CI), rd.rmsMm.ToString("F1", CI), ed.ToString("F1", CI), string.Join(" ", fl.ToArray()) }));
            }
            float minSep = 99; for (int i = 0; i < pins.Count; i++) for (int j = i + 1; j < pins.Count; j++) minSep = Mathf.Min(minSep, new Vector2(pins[i].Position.x - pins[j].Position.x, pins[i].Position.z - pins[j].Position.z).magnitude);
            // usable area on the green under the pins, and the pin spots that pass
            int tot = 0, ok2 = 0, okFit = 0; var cand = new List<KeyValuePair<Vector2, float>>(); var candFit = new List<KeyValuePair<Vector2, float>>(); Bounds b;
            if (bounds.TryGetValue(green, out b))
                for (float x = b.min.x; x <= b.max.x; x += grid) for (float z = b.min.z; z <= b.max.z; z += grid)
                {
                    if (!Inside(green, x, z, edge)) continue; tot++; var rd = Area(x, z); bool pass = rd.worst <= limitDeg + 1e-4f, passFit = rd.fit <= limitDeg + 1e-4f; if (pass) ok2++; if (passFit) okFit++;
                    float ed = ol != null ? ol.Dist(new Vector2(x, z), pinEdge + 1) : pinEdge; if (ed < pinEdge) continue;
                    if (pass) cand.Add(new KeyValuePair<Vector2, float>(new Vector2(x, z), rd.worst)); if (passFit) candFit.Add(new KeyValuePair<Vector2, float>(new Vector2(x, z), rd.fit));
                }
            int areas = CountAreas(cand, pinSep), areasFit = CountAreas(candFit, pinSep); if (areas < minAreas) thinGreens++;
            o.Add("h" + h.HoleNumber + " " + green + ": " + string.Join(", ", line.ToArray()) + "; pins >= " + minSep.ToString("F1", CI) + " m apart" + (minSep < pinSep ? " (<" + pinSep.ToString("F0", CI) + " m)" : "")
                + "; usable " + (tot > 0 ? (100f * ok2 / tot).ToString("F0", CI) + "% of " + (tot * grid * grid).ToString("F0", CI) + " m2 (fit " + (100f * okFit / tot).ToString("F0", CI) + "%)" : "-")
                + "; pin areas " + areas + (areas < minAreas ? " (<" + minAreas + ")" : "") + " (fit " + areasFit + ")");
        }
        var head = fails + " pin areas over " + limitDeg.ToString(CI) + " deg or off the green; " + steep + " with a plane fit over " + limitDeg.ToString(CI) + " deg; " + nearEdge + " pins < " + pinEdge.ToString(CI) + " m from the edge; " + thinGreens + " greens with < " + minAreas + " pin areas";
        var txt = head + "\n" + string.Join("\n", o.ToArray());
        try { Directory.CreateDirectory("AutomationOutput"); File.WriteAllText("AutomationOutput/pin_area.txt", txt); File.WriteAllLines("AutomationOutput/pin_area.csv", csv.ToArray()); } catch (System.Exception e) { txt += "\n(could not write AutomationOutput: " + e.Message + ")"; }
        return txt;
    }

    // for tests: run on a GKD text and green outlines without a scene
    public string RunWith(string gkdText, Dictionary<string, TierraGeom.SegIndex> outlines, Dictionary<string, Bounds> bounds)
    {
        return Report(JsonUtility.FromJson<G>(gkdText.TrimStart('\uFEFF')), outlines, bounds);
    }
}
