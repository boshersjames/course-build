using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Green colour test (Oct 29): duplicate the green and collar materials for the hole 11 green only and tint them toward the photos'
// yellow-green. Never touches the shared materials; Revert puts the originals back.
public static class TierraGreenTest
{
    static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;
    const string Dir = "AutomationOutput/vegetation/";
    const string MatDir = "Assets/TierraRejada/GreenTest1029";
    static readonly Vector2 C11 = new Vector2(1707.954f, 1003.228f);

    static IEnumerable<Renderer> Near(float r)
    {
        foreach (var rd in Object.FindObjectsOfType<Renderer>())
        {
            if (!(rd is MeshRenderer)) continue; var b = rd.bounds;
            if (b.size.x > 400 || b.size.z > 400) continue;
            var c = new Vector2(Mathf.Clamp(C11.x, b.min.x, b.max.x), Mathf.Clamp(C11.y, b.min.z, b.max.z));
            if ((c - C11).magnitude < r) yield return rd;
        }
    }

    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    public static string Report()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var rd in Near(45))
        {
            var col = rd.GetComponent<Collider>();
            sb.AppendLine(string.Format(CI, "{0} | phys {1} | bounds {2}", Path(rd.transform), col && col.sharedMaterial ? col.sharedMaterial.name : "-", rd.bounds.size));
            foreach (var m in rd.sharedMaterials.Where(m => m))
            {
                sb.AppendLine("   mat " + m.name + " (" + AssetDatabase.GetAssetPath(m) + ") shader " + m.shader.name);
                for (int i = 0; i < ShaderUtil.GetPropertyCount(m.shader); i++)
                {
                    var n = ShaderUtil.GetPropertyName(m.shader, i); var ty = ShaderUtil.GetPropertyType(m.shader, i);
                    if (ty == ShaderUtil.ShaderPropertyType.Color) sb.AppendLine("      " + n + " = " + m.GetColor(n));
                    else if (ty == ShaderUtil.ShaderPropertyType.TexEnv) { var t = m.GetTexture(n); sb.AppendLine("      " + n + " = " + (t ? AssetDatabase.GetAssetPath(t) : "-") + " tiling " + m.GetTextureScale(n)); }
                    else if (ty == ShaderUtil.ShaderPropertyType.Float || ty == ShaderUtil.ShaderPropertyType.Range) sb.AppendLine("      " + n + " = " + m.GetFloat(n).ToString(CI));
                }
            }
        }
        File.WriteAllText(Dir + "green_test_report.txt", sb.ToString()); return "green test report written";
    }

    // green_test.json: { "targets": ["object name", ...], "props": { "matName|_Prop": [r,g,b,a], ... } }
    public static string Apply()
    {
        var J = TierraMiniJson.Parse(File.ReadAllText(Dir + "green_test.json")) as Dictionary<string, object>;
        var targets = ((List<object>)J["targets"]).Select(o => (string)o).ToList();
        var props = (Dictionary<string, object>)J["props"]; var log = new List<string>(); var made = new Dictionary<Material, Material>();
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/TierraRejada", "GreenTest1029");
        var orig = new System.Text.StringBuilder();
        foreach (var rd in Near(60).Where(r => targets.Contains(r.name)))
        {
            var ms = rd.sharedMaterials; orig.AppendLine(Path(rd.transform) + "\t" + string.Join(";", ms.Select(m => m ? AssetDatabase.GetAssetPath(m) + ":" + m.name : "").ToArray()));
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i]; if (!m || m.name.EndsWith("_H11Test")) continue;
                Material d;
                if (!made.TryGetValue(m, out d))
                {
                    d = new Material(m) { name = m.name + "_H11Test" };
                    foreach (var kv in props)
                    {
                        var k = kv.Key.Split('|'); if (k[0] != m.name || !d.HasProperty(k[1])) continue;
                        var a = ((List<object>)kv.Value).Select(o => System.Convert.ToSingle(o, CI)).ToArray();
                        if (a.Length == 1) d.SetFloat(k[1], a[0]); else d.SetColor(k[1], new Color(a[0], a[1], a[2], a.Length > 3 ? a[3] : 1));
                        log.Add(d.name + " " + k[1] + " = " + string.Join(",", a.Select(x => x.ToString(CI)).ToArray()));
                    }
                    if (J.ContainsKey("scale"))
                    { // multiply every channel's overall colour, so the shared originals' values carry over
                        var s = ((List<object>)J["scale"]).Select(o => System.Convert.ToSingle(o, CI)).ToArray();
                        foreach (var ch in new[] { "_Red", "_Green", "_Blue", "_Black" })
                        {
                            var pn = ch + "OverallColor"; if (!d.HasProperty(pn)) continue; var c = d.GetColor(pn);
                            d.SetColor(pn, new Color(c.r * s[0], c.g * s[1], c.b * s[2], c.a)); log.Add(d.name + " " + pn + " " + c + " -> " + d.GetColor(pn));
                        }
                    }
                    if (J.ContainsKey("desaturate") && d.HasProperty("_OverallDesaturate")) d.SetFloat("_OverallDesaturate", System.Convert.ToSingle(J["desaturate"], CI));
                    AssetDatabase.CreateAsset(d,MatDir + "/" + d.name.Replace(" ", "_") + ".mat"); made[m] = d;
                }
                ms[i] = d;
            }
            rd.sharedMaterials = ms; log.Add("test materials on " + Path(rd.transform));
        }
        File.AppendAllText(Dir + "green_test_original.txt", orig.ToString());
        AssetDatabase.SaveAssets(); File.WriteAllText(Dir + "green_test_log.txt", string.Join(System.Environment.NewLine, log.ToArray()));
        return log.Count + " changes";
    }

    // Course-wide roll-out (BoJa, Oct 29: "yes brighten them"): one tinted copy per source material, shared by every green, green-edge
    // blend and 1029 collar patch on the course (collars get their own copy of the fairway material so fairways stay as they are).
    // Renderers on the 11 test copies go to the course-wide copies. green_rollout_original.txt lists every renderer's old materials.
    public static string RollOut()
    {
        var J = TierraMiniJson.Parse(File.ReadAllText(Dir + "green_test.json")) as Dictionary<string, object>;
        var s = ((List<object>)J["scale"]).Select(o => System.Convert.ToSingle(o, CI)).ToArray(); float desat = System.Convert.ToSingle(J["desaturate"], CI);
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/TierraRejada", "GreenTest1029");
        var made = new Dictionary<string, Material>(); var orig = new System.Text.StringBuilder(); var count = new Dictionary<string, int>(); var other = new HashSet<string>();
        // green-edge blends take the fairway tone (edgeScale), not the green one, so no lime ring shows round the greens
        var es = J.ContainsKey("edgeScale") ? ((List<object>)J["edgeScale"]).Select(o => System.Convert.ToSingle(o, CI)).ToArray() : s;
        // collars copy the shared fairway material, which is already toned (TintShared): collarScale = green tone / fairway tone
        var cs = J.ContainsKey("collarScale") ? ((List<object>)J["collarScale"]).Select(o => System.Convert.ToSingle(o, CI)).ToArray() : null;
        System.Func<Material, bool, Material> tinted = (m, edge) =>
        {
            // the 11 test copies were saved with underscores for spaces, so their names are normalised back to the source material's
            var baseName = m.name.EndsWith("_H11Test") ? m.name.Substring(0, m.name.Length - 8).Replace("_", " ").Replace("Green Sat", "Green_Sat").Replace("Fairway Sat", "Fairway_Sat").Replace("Blend OPCD", "Blend_OPCD") : m.name; Material d; var f = edge ? es : (baseName == "OPCD MAHS Fairway default" && cs != null ? cs : s);
            var key = baseName + (edge ? "|edge" : ""); if (made.TryGetValue(key, out d)) return d;
            var src = m.name.EndsWith("_H11Test") ? AssetDatabase.FindAssets("t:Material " + baseName).Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x && x.name == baseName) : m;
            d = new Material(src ?? m) { name = baseName + (edge ? "_TierraEdge" : "_Tierra") };
            foreach (var ch in new[] { "_Red", "_Green", "_Blue", "_Black" }) { var pn = ch + "OverallColor"; if (!d.HasProperty(pn)) continue; var c = d.GetColor(pn); d.SetColor(pn, new Color(c.r * f[0], c.g * f[1], c.b * f[2], c.a)); }
            if (d.HasProperty("_OverallDesaturate") && !edge) d.SetFloat("_OverallDesaturate", desat);
            // unique path: two source materials can differ only by space vs underscore or by case, and overwriting an asset destroys
            // the copy already assigned to earlier renderers
            AssetDatabase.CreateAsset(d, AssetDatabase.GenerateUniqueAssetPath(MatDir + "/" + d.name.Replace(" ", "_") + ".mat")); made[key] = d; return d;
        };
        var opcd = GameObject.Find("OPCD Meshes");
        foreach (var rd in opcd.GetComponentsInChildren<MeshRenderer>(true))
        {
            var n = rd.name; var col = rd.GetComponent<Collider>(); bool greenPhys = col && col.sharedMaterial && col.sharedMaterial.name == "GSPgreen";
            // greens by physics (GSPgreen) or the OPCD green spline name; tee boxes are excluded even when named for the Green tee colour
            bool tee = n.ToLowerInvariant().Contains("tee");
            bool isGreen = !tee && !n.Contains("Blend") && (greenPhys || n.StartsWith("Spline_green")), isEdge = !tee && n.StartsWith("Spline_green") && n.Contains("Blend"), isCollar = n.StartsWith("SPatch_collar1029");
            if (!(isGreen || isEdge || isCollar)) continue;
            var ms = rd.sharedMaterials; orig.AppendLine(n + "\t" + string.Join(";", ms.Select(m => m ? m.name : "-").ToArray()));
            for (int i = 0; i < ms.Length; i++) { if (!ms[i] || ms[i].name.EndsWith("_Tierra") || ms[i].name.EndsWith("_TierraEdge")) continue; if (!ms[i].shader.name.Contains("Vert Paint")) { other.Add(n + ": " + ms[i].name); continue; } ms[i] = tinted(ms[i], isEdge); }
            rd.sharedMaterials = ms; var key = isCollar ? "collars" : isEdge ? "green edges" : "greens"; count[key] = (count.ContainsKey(key) ? count[key] : 0) + 1;
        }
        File.WriteAllText(Dir + "green_rollout_original.txt", orig.ToString()); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(opcd.scene);
        var msg = string.Join(", ", count.Select(k => k.Value + " " + k.Key).ToArray()) + "; materials: " + string.Join(", ", made.Values.Select(m => m.name).ToArray()) + (other.Count > 0 ? "; skipped non-OPCD: " + string.Join(", ", other.ToArray()) : "");
        File.WriteAllText(Dir + "green_rollout_log.txt", msg); return msg;
    }

    // Fairway / rough tone toward the photos (Oct 29): multiply the four channel OverallColors of shared materials in place.
    // green_tone.json { "materials": { "<material name>": [r, g, b] or [r, g, b, desaturate] } }. Each .mat is copied to
    // Backups/green_rollout_1029/ first; a material already toned (tone_done.txt) is skipped so a rerun never compounds.
    public static string TintShared()
    {
        var J = (Dictionary<string, object>)TierraMiniJson.Parse(File.ReadAllText(Dir + "green_tone.json")); var mats = (Dictionary<string, object>)J["materials"];
        var donePath = Dir + "tone_done.txt"; var done = new HashSet<string>(File.Exists(donePath) ? File.ReadAllLines(donePath) : new string[0]); var log = new List<string>();
        Directory.CreateDirectory("Backups/green_rollout_1029");
        foreach (var kv in mats)
        {
            if (done.Contains(kv.Key)) { log.Add(kv.Key + ": already toned, skipped"); continue; }
            var m = AssetDatabase.FindAssets("t:Material " + kv.Key).Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x && x.name == kv.Key);
            if (!m) { log.Add(kv.Key + ": not found"); continue; }
            var p = AssetDatabase.GetAssetPath(m); File.Copy(p, "Backups/green_rollout_1029/" + System.IO.Path.GetFileName(p), true);
            var a = ((List<object>)kv.Value).Select(o => System.Convert.ToSingle(o, CI)).ToArray();
            foreach (var ch in new[] { "_Red", "_Green", "_Blue", "_Black" }) { var pn = ch + "OverallColor"; if (!m.HasProperty(pn)) continue; var c = m.GetColor(pn); m.SetColor(pn, new Color(c.r * a[0], c.g * a[1], c.b * a[2], c.a)); }
            if (a.Length > 3 && m.HasProperty("_OverallDesaturate")) m.SetFloat("_OverallDesaturate", a[3]);
            EditorUtility.SetDirty(m); done.Add(kv.Key); log.Add(kv.Key + " (" + p + ") x " + string.Join(",", a.Select(x => x.ToString(CI)).ToArray()));
        }
        AssetDatabase.SaveAssets(); File.WriteAllLines(donePath, done.ToArray()); File.WriteAllText(Dir + "green_tone_log.txt", string.Join(System.Environment.NewLine, log.ToArray()));
        return string.Join("; ", log.ToArray());
    }

    // render-only raking light for the putter-height green views: sun 9 degrees up from the west-south-west (scene not saved)
    public static string LowSun()
    {
        var l = Object.FindObjectsOfType<Light>().Where(x => x.type == LightType.Directional).OrderByDescending(x => x.intensity).FirstOrDefault();
        if (!l) return "no directional light"; var was = l.transform.eulerAngles; l.transform.rotation = Quaternion.Euler(9f, 70f, 0f); return "sun " + was + " -> " + l.transform.eulerAngles;
    }

    // flyover renders only (not saved): a mid-afternoon sun, 26 deg up from the west-south-west, rakes across the greens so their
    // undulation reads (the scene's high sun flattens 3-6 % slopes; BoJa Oct 30: "15 green looks flat against the fairway")
    public static string FlyoverSun()
    {
        var l = Object.FindObjectsOfType<Light>().Where(x => x.type == LightType.Directional).OrderByDescending(x => x.intensity).FirstOrDefault();
        if (!l) return "no directional light"; var was = l.transform.eulerAngles; l.transform.rotation = Quaternion.Euler(26f, 70f, 0f); return "sun " + was + " -> " + l.transform.eulerAngles;
    }

    // Slope shading on the putting surfaces (the OPCD shader darkens toward _SlopeShadeTint by slope angle x _SlopePower; default power 1
    // is ~3 % darker on a 3 % slope, i.e. invisible). Test values from AutomationOutput/vegetation/slope_look.json {"power": 4, "tint": [r,g,b]}.
    // Applies to the *_Tierra green materials only (not edges/collars, not fairways). Material assets change: run Revert-free by saving power 1.
    public static string SlopeLook()
    {
        var j = File.ReadAllText("AutomationOutput/vegetation/slope_look.json"); var J = (Dictionary<string, object>)TierraMiniJson.Parse(j);
        float pw = System.Convert.ToSingle(J["power"], System.Globalization.CultureInfo.InvariantCulture); var tl = ((List<object>)J["tint"]).Select(o => System.Convert.ToSingle(o, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        int n = 0; foreach (var g in AssetDatabase.FindAssets("t:Material _Tierra"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (!m || !m.name.Contains("Green") || m.name.Contains("Edge") || m.name.StartsWith("Blend")) continue;
            m.SetFloat("_SlopeShading", 1); m.SetFloat("_SlopePower", pw); m.SetColor("_SlopeShadeTint", new Color(tl[0], tl[1], tl[2], 1)); EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets(); return n + " green materials: slope power " + pw;
    }

    public static string SlopeLookOff()
    {   // back to the shipped look (power 1, black tint) after a flyover render
        int n = 0; foreach (var g in AssetDatabase.FindAssets("t:Material _Tierra"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (!m || !m.name.Contains("Green") || m.name.Contains("Edge") || m.name.StartsWith("Blend")) continue;
            m.SetFloat("_SlopePower", 1); m.SetColor("_SlopeShadeTint", new Color(0, 0, 0, 0)); EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets(); return n + " green materials back to slope power 1";
    }

    // BoJa Oct 30: the green-to-collar line read as a dark band. The green-edge blend's Red/Black channels were toned to 0.93-1.03, darker than
    // both the green (1.31) and the collar (1.19): set them to the collar tone so the edge is a soft change. Shapes untouched; material only.
    public static string SoftenEdge()
    {
        var p = MatDir + "/Blend_OPCD_MAHS_Green_Sat_to_OPCD_MAHS_Fairway_Sat_TierraEdge.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (!m) return "edge material missing";
        var col = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/OPCD_MAHS_Fairway_default_Tierra.mat"); var c = col ? col.GetColor("_RedOverallColor") : new Color(1.187f, 1.077f, 0.911f);
        var was = m.GetColor("_RedOverallColor") + " / " + m.GetColor("_BlackOverallColor");
        m.SetColor("_RedOverallColor", new Color(c.r, c.g, c.b, 1)); m.SetColor("_BlackOverallColor", new Color(c.r, c.g, c.b, 1)); m.SetFloat("_BlackOverallColorStrength", 1f);
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); return "green edge red/black " + was + " -> " + c;
    }

    public static string Revert()
    {
        int n = 0;
        foreach (var rd in Near(60))
        {
            var ms = rd.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (!ms[i] || !ms[i].name.EndsWith("_H11Test")) continue;
                var on = ms[i].name.Substring(0, ms[i].name.Length - 8);
                var o = AssetDatabase.FindAssets("t:Material " + on).Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x && x.name == on);
                if (o) { ms[i] = o; ch = true; }
            }
            if (ch) { rd.sharedMaterials = ms; n++; }
        }
        return n + " renderers reverted";
    }
}
