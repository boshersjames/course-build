using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Course Look > Apply Tint JSON
// Reads a tint file written by tools/colour_match.py (or by hand) and multiplies OverallColor on the named material copies.
// It never touches a shared material: the target must be a per-course or per-hole copy (name contains "_" + course tag).
// File format (Assets/CourseLook/tint.json or any path):
// { "tints": [ {"material": "GSPgreen_Tierra", "mul": [1.85, 1.68, 1.42], "desaturate": 0.2},
//              {"material": "Rough_h11edge",   "mul": [1.4, 1.22, 1.08]} ],
//   "done_marker": "Assets/CourseLook/tone_done_<name>.txt" }
// A done marker, when given, stops a rerun from compounding the multiply (course-build-rough: tone_done.txt rule).
public class ApplyTintJson
{
    [System.Serializable] public class Tint { public string material; public float[] mul; public float desaturate; }
    [System.Serializable] public class File_ { public Tint[] tints; public string done_marker; }
    static readonly string[] Channels = { "_OverallColor", "_GreenOverallColor", "_BlueOverallColor", "_BlackOverallColor" };

    [MenuItem("Course Look/Apply Tint JSON")]
    static void Menu()
    {
        var p = EditorUtility.OpenFilePanel("tint.json", "Assets/CourseLook", "json");
        if (!string.IsNullOrEmpty(p)) Apply(p);
    }

    public static string Apply(string path)
    {
        var f = JsonUtility.FromJson<File_>(File.ReadAllText(path));
        if (!string.IsNullOrEmpty(f.done_marker) && File.Exists(f.done_marker))
            return "skip: done marker exists " + f.done_marker;
        var log = new List<string>();
        foreach (var t in f.tints)
        {
            var guids = AssetDatabase.FindAssets(t.material + " t:Material");
            Material m = null;
            foreach (var g in guids) { var mm = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (mm && mm.name == t.material) { m = mm; break; } }
            if (!m) { log.Add("MISSING " + t.material); continue; }
            if (!m.name.Contains("_")) { log.Add("REFUSED shared material " + m.name + " (tint a copy)"); continue; }
            int n = 0;
            foreach (var ch in Channels)
            {
                if (!m.HasProperty(ch)) continue;
                var c = m.GetColor(ch);
                var r = new Color(c.r * t.mul[0], c.g * t.mul[1], c.b * t.mul[2], c.a);
                if (t.desaturate > 0) { float l = 0.299f * r.r + 0.587f * r.g + 0.114f * r.b; r = Color.Lerp(r, new Color(l, l, l, r.a), t.desaturate); }
                m.SetColor(ch, r); n++;
            }
            EditorUtility.SetDirty(m);
            log.Add(m.name + " x(" + t.mul[0] + "," + t.mul[1] + "," + t.mul[2] + ") desat " + t.desaturate + " on " + n + " channels");
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(f.done_marker)) File.WriteAllText(f.done_marker, System.DateTime.Now.ToString("s") + " " + path);
        var s = string.Join("\n", log.ToArray()); Debug.Log("ApplyTintJson\n" + s); return s;
    }
}
