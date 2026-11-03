using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Course Fixes > Calibrate Post Profile
// Sets the audit camera and scene lighting to a measured, repeatable look before audit snapshots, on the GSPro base project
// (Unity 2018.2, built-in pipeline, Post Processing Stack v2). Everything numeric comes from Assets/CourseBuild/rules/look.json
// "calibration" and "brightness"; the course frame from Assets/CourseBuild/course.json.
//  1. Tonemapping + exposure: the scene's PostProcessVolume gets a COURSE COPY of its profile (Assets/CourseBuild/<Course>_Audit.asset,
//     never the shipped asset); ColorGrading.tonemapper = rules tonemapper (Neutral by default; ACES at +2.3 EV was the washed-out look),
//     AutoExposure disabled; postExposure found by a measured loop: render the Main Camera, measure mean sky luma (top rows, sky pixels)
//     and green/turf luma (lower-middle box), step EV by damping * log2(target / measured) until both sit in their bands or max_iterations.
//  2. Turf specular + green saturation: when solar elevation > high_sun_deg, every renderer whose material name matches the turf list gets a
//     MaterialPropertyBlock clamping _Glossiness / _Metallic / _SpecColor and scaling the green channel of _OverallColor (and _Color) by
//     green_channel_scale. Scene-only: the shared material assets are untouched (course-build-look: shared materials stay shared).
//  3. Sun: NOAA solar position for course.json centre_latlon at rules sun.audit_time_local (+ tz_offset_hours): azimuth, elevation ->
//     Directional Light rotation (locked), colour temperature by elevation (kelvin_horizon..kelvin_noon), shadow bias / normal bias /
//     strength, intensity.
//  4. Log: AutomationOutput/checks/post_profile_<yyyyMMdd_HHmmss>.json (+ .md): sun angles, kelvin, EV per iteration with the measured
//     lumas, material overrides, profile paths (old and new). Reflection is used for the PPv2 types so this compiles even where the
//     PostProcessing assembly is referenced differently; if PPv2 is absent the exposure step is skipped and logged.
// Menu: Course Fixes > Calibrate Post Profile.  Bridge: invoke -Tool GSProPostProfileCalibrator -Method RunCalibration
// Batch mode has NO post-processing (Uber shader unsupported), so run this from an open editor via the bridge, never in a batch run.
// NOT YET COMPILED ON THE PC (written in the cloud 2026-11-03).
public class GSProPostProfileCalibrator : EditorWindow
{
    [Serializable] public class Exposure { public float start = 1.2f, min = -1f, max = 2.5f, damping = 0.7f; public int max_iterations = 6; }
    [Serializable] public class Targets { public float[] sky_luma_mean = { 120, 185 }; public float[] green_luma = { 100, 160 }; public float sky_rows_fraction = 0.25f; public float[] green_box_fraction = { 0.3f, 0.55f, 0.4f, 0.3f }; }
    [Serializable] public class Spec { public float glossiness_max = 0.25f, metallic_max = 0f, spec_color_max = 0.15f, green_channel_scale = 0.92f; public string[] applies_to_names = { "green", "fairway", "tee", "collar", "rough", "blend" }; }
    [Serializable] public class Sun { public string audit_time_local = "2026-05-15T10:30:00"; public float tz_offset_hours = -7, kelvin_horizon = 3400, kelvin_noon = 5600, kelvin_elevation_full_deg = 45, shadow_bias = 0.05f, shadow_normal_bias = 0.4f, shadow_strength = 0.85f, intensity = 1.25f; }
    [Serializable] public class Calibration { public string tonemapper = "Neutral"; public Exposure exposure_ev = new Exposure(); public Targets targets = new Targets(); public float high_sun_deg = 50; public Spec turf_specular_clamp = new Spec(); public Sun sun = new Sun(); }
    [Serializable] public class LookRules { public Calibration calibration = new Calibration(); }
    [Serializable] public class Frame { public float[] centre_latlon; }
    [Serializable] public class CourseJson { public string name; public Frame frame; }

    const string CB = "Assets/CourseBuild";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    string last = ""; Vector2 scroll;

    [MenuItem("Course Fixes/Calibrate Post Profile")]
    static void Open() { GetWindow<GSProPostProfileCalibrator>("Post profile"); }
    void OnGUI()
    {
        GUILayout.Label("Measured post profile + sun lock for audit snapshots (rules/look.json calibration). Editor only, not batch.", EditorStyles.boldLabel);
        if (GUILayout.Button("Run calibration")) last = RunCalibration();
        scroll = EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(last); EditorGUILayout.EndScrollView();
    }

    static T LoadJson<T>(string path, T fallback) where T : class { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path).TrimStart('﻿')) : fallback; }
    static string ProjectDir() { return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); }

    // ---------- solar position (NOAA, good to ~0.01 deg) ----------
    public static void SolarPosition(double latDeg, double lonDeg, DateTime localTime, double tzHours, out double azimuthDeg, out double elevationDeg)
    {
        var utc = localTime.AddHours(-tzHours); double jd = utc.ToOADate() + 2415018.5; double jc = (jd - 2451545.0) / 36525.0;
        double gml = (280.46646 + jc * (36000.76983 + jc * 0.0003032)) % 360; double gma = 357.52911 + jc * (35999.05029 - 0.0001537 * jc); double ecc = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);
        double r = Math.PI / 180;
        double seoc = Math.Sin(gma * r) * (1.914602 - jc * (0.004817 + 0.000014 * jc)) + Math.Sin(2 * gma * r) * (0.019993 - 0.000101 * jc) + Math.Sin(3 * gma * r) * 0.000289;
        double stl = gml + seoc; double sal = stl - 0.00569 - 0.00478 * Math.Sin((125.04 - 1934.136 * jc) * r);
        double moe = 23 + (26 + (21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813))) / 60) / 60; double oc = moe + 0.00256 * Math.Cos((125.04 - 1934.136 * jc) * r);
        double decl = Math.Asin(Math.Sin(oc * r) * Math.Sin(sal * r)) / r; double vy = Math.Tan(oc / 2 * r) * Math.Tan(oc / 2 * r);
        double eot = 4 / r * (vy * Math.Sin(2 * gml * r) - 2 * ecc * Math.Sin(gma * r) + 4 * ecc * vy * Math.Sin(gma * r) * Math.Cos(2 * gml * r) - 0.5 * vy * vy * Math.Sin(4 * gml * r) - 1.25 * ecc * ecc * Math.Sin(2 * gma * r));
        double minutes = localTime.TimeOfDay.TotalMinutes; double tst = (minutes + eot + 4 * lonDeg - 60 * tzHours) % 1440; if (tst < 0) tst += 1440;
        double ha = tst / 4 < 0 ? tst / 4 + 180 : tst / 4 - 180;
        double zen = Math.Acos(Math.Sin(latDeg * r) * Math.Sin(decl * r) + Math.Cos(latDeg * r) * Math.Cos(decl * r) * Math.Cos(ha * r)) / r;
        elevationDeg = 90 - zen;
        double az = Math.Acos(((Math.Sin(latDeg * r) * Math.Cos(zen * r)) - Math.Sin(decl * r)) / (Math.Cos(latDeg * r) * Math.Sin(zen * r))) / r;
        azimuthDeg = ha > 0 ? (az + 180) % 360 : (540 - az) % 360;
    }

    // ---------- PPv2 via reflection ----------
    static Type T(string full) { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(full)).FirstOrDefault(t => t != null); }
    static object GetParam(object settings, string field) { var f = settings.GetType().GetField(field); return f != null ? f.GetValue(settings) : null; }
    static void SetParam(object param, object value) { if (param == null) return; var t = param.GetType(); var v = t.GetProperty("value") ?? null; var fv = t.GetField("value"); var os = t.GetField("overrideState"); if (os != null) os.SetValue(param, true); if (v != null) v.SetValue(param, value, null); else if (fv != null) fv.SetValue(param, value); }
    static object GetValue(object param) { if (param == null) return null; var t = param.GetType(); var v = t.GetProperty("value"); var fv = t.GetField("value"); return v != null ? v.GetValue(param, null) : fv != null ? fv.GetValue(param) : null; }

    // ---------- measurement ----------
    static float Luma(Color c) { return 255f * (0.299f * c.r + 0.587f * c.g + 0.114f * c.b); }
    static bool Measure(Camera cam, Targets tg, out float skyLuma, out float greenLuma, out int skyPx)
    {
        skyLuma = greenLuma = 0; skyPx = 0; if (!cam) return false;
        int w = 1280, h = 720; var rt = RenderTexture.GetTemporary(w, h, 24); var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels(); double ssum = 0; int sn = 0; double gsum = 0; int gn = 0;
        int skyRows = (int)(h * tg.sky_rows_fraction); int gx0 = (int)(w * tg.green_box_fraction[0]), gy0 = (int)(h * tg.green_box_fraction[1]), gw = (int)(w * tg.green_box_fraction[2]), gh = (int)(h * tg.green_box_fraction[3]);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            var c = px[y * w + x]; int yTop = h - 1 - y;   // GetPixels is bottom-up
            if (yTop < skyRows && ((c.b > c.g + 0.03f && c.b > c.r + 0.08f) || Luma(c) > 225)) { ssum += Luma(c); sn++; }
            if (yTop >= gy0 && yTop < gy0 + gh && x >= gx0 && x < gx0 + gw) { gsum += Luma(c); gn++; }
        }
        UnityEngine.Object.DestroyImmediate(tex);
        skyPx = sn; skyLuma = sn > 0 ? (float)(ssum / sn) : float.NaN; greenLuma = gn > 0 ? (float)(gsum / gn) : float.NaN; return true;
    }

    // ---------- run ----------
    public static string RunCalibration()
    {
        var rules = LoadJson(CB + "/rules/look.json", new LookRules()).calibration; var course = LoadJson(CB + "/course.json", new CourseJson());
        var log = new List<string>(); var js = new List<string>(); string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dir = ProjectDir() + "/AutomationOutput/checks"; Directory.CreateDirectory(dir);
        // 3. sun first (exposure depends on it)
        double lat = course != null && course.frame != null && course.frame.centre_latlon != null && course.frame.centre_latlon.Length == 2 ? course.frame.centre_latlon[0] : 34.0;
        double lon = course != null && course.frame != null && course.frame.centre_latlon != null && course.frame.centre_latlon.Length == 2 ? course.frame.centre_latlon[1] : -118.0;
        DateTime when; if (!DateTime.TryParse(rules.sun.audit_time_local, CI, DateTimeStyles.None, out when)) when = new DateTime(2026, 5, 15, 10, 30, 0);
        double az, el; SolarPosition(lat, lon, when, rules.sun.tz_offset_hours, out az, out el);
        var sun = UnityEngine.Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.activeInHierarchy);
        float kelvin = Mathf.Lerp(rules.sun.kelvin_horizon, rules.sun.kelvin_noon, Mathf.Clamp01((float)el / rules.sun.kelvin_elevation_full_deg));
        if (sun)
        {
            Undo.RecordObject(sun.transform, "calibrate sun"); Undo.RecordObject(sun, "calibrate sun");
            float r = Mathf.Deg2Rad; var toSun = new Vector3(Mathf.Sin((float)az * r) * Mathf.Cos((float)el * r), Mathf.Sin((float)el * r), Mathf.Cos((float)az * r) * Mathf.Cos((float)el * r));
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
            sun.color = Mathf.CorrelatedColorTemperatureToRGB(kelvin); sun.intensity = rules.sun.intensity; sun.shadows = LightShadows.Soft;
            sun.shadowBias = rules.sun.shadow_bias; sun.shadowNormalBias = rules.sun.shadow_normal_bias; sun.shadowStrength = rules.sun.shadow_strength;
            log.Add(string.Format(CI, "sun: {0} local (tz {1:+0.0;-0.0}) at ({2:F5},{3:F5}) -> azimuth {4:F2} deg, elevation {5:F2} deg; {6:F0} K, intensity {7}, bias {8}/{9}, strength {10}", when.ToString("s"), rules.sun.tz_offset_hours, lat, lon, az, el, kelvin, rules.sun.intensity, rules.sun.shadow_bias, rules.sun.shadow_normal_bias, rules.sun.shadow_strength));
        }
        else log.Add("sun: no active Directional Light found; rotation not set");
        js.Add(string.Format(CI, "\"sun\":{{\"time_local\":\"{0}\",\"lat\":{1:F6},\"lon\":{2:F6},\"azimuth_deg\":{3:F3},\"elevation_deg\":{4:F3},\"kelvin\":{5:F0},\"light\":\"{6}\"}}", when.ToString("s"), lat, lon, az, el, kelvin, sun ? sun.name : "none"));

        // 2. turf specular clamp under high sun (MaterialPropertyBlock, scene-only)
        int clamped = 0; var names = new List<string>();
        if (el > rules.high_sun_deg)
        {
            var sp = rules.turf_specular_clamp;
            foreach (var rend in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                var m = rend.sharedMaterial; if (!m) continue; var n = m.name.ToLower(); if (!sp.applies_to_names.Any(k => n.Contains(k))) continue;
                var mpb = new MaterialPropertyBlock(); rend.GetPropertyBlock(mpb); bool any = false;
                if (m.HasProperty("_Glossiness")) { mpb.SetFloat("_Glossiness", Mathf.Min(m.GetFloat("_Glossiness"), sp.glossiness_max)); any = true; }
                if (m.HasProperty("_Metallic")) { mpb.SetFloat("_Metallic", Mathf.Min(m.GetFloat("_Metallic"), sp.metallic_max)); any = true; }
                if (m.HasProperty("_SpecColor")) { var c = m.GetColor("_SpecColor"); float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)); if (mx > sp.spec_color_max) c *= sp.spec_color_max / mx; mpb.SetColor("_SpecColor", c); any = true; }
                foreach (var prop in new[] { "_OverallColor", "_Color" }) if (m.HasProperty(prop)) { var c = m.GetColor(prop); c.g *= sp.green_channel_scale; mpb.SetColor(prop, c); any = true; }
                if (any) { rend.SetPropertyBlock(mpb); clamped++; if (!names.Contains(m.name)) names.Add(m.name); }
            }
            log.Add(string.Format(CI, "turf: sun {0:F1} deg > {1} deg: specular/green clamp on {2} renderers ({3} materials, scene-only property blocks): {4}", el, rules.high_sun_deg, clamped, names.Count, string.Join(", ", names.Take(12).ToArray())));
        }
        else log.Add(string.Format(CI, "turf: sun {0:F1} deg <= {1} deg: no specular clamp", el, rules.high_sun_deg));
        js.Add("\"turf_overrides\":{\"renderers\":" + clamped + ",\"materials\":[" + string.Join(",", names.Select(x => "\"" + x + "\"").ToArray()) + "]}");

        // 1. tonemapper + measured exposure
        var volT = T("UnityEngine.Rendering.PostProcessing.PostProcessVolume"); var cam = Camera.main; string profLine = "post: PostProcessing v2 not found; exposure step skipped"; var evLog = new List<string>();
        if (volT != null && cam)
        {
            var vol = UnityEngine.Object.FindObjectsOfType(volT).Cast<Component>().FirstOrDefault(v => v.gameObject.activeInHierarchy);
            if (vol != null)
            {
                var spField = volT.GetField("sharedProfile"); var profile = spField.GetValue(vol) as UnityEngine.Object; string oldPath = profile ? AssetDatabase.GetAssetPath(profile) : "";
                string courseName = course != null && !string.IsNullOrEmpty(course.name) ? course.name : "Course"; string newPath = CB + "/" + courseName + "_Audit.asset";
                if (profile && oldPath != newPath) { if (!AssetDatabase.IsValidFolder(CB)) AssetDatabase.CreateFolder("Assets", "CourseBuild"); AssetDatabase.CopyAsset(oldPath, newPath); profile = AssetDatabase.LoadAssetAtPath(newPath, profile.GetType()); Undo.RecordObject(vol, "calibrate profile"); spField.SetValue(vol, profile); }
                var settingsList = profile.GetType().GetField("settings").GetValue(profile) as System.Collections.IList;
                object cg = null, ae = null; foreach (var s in settingsList) { var tn = s.GetType().Name; if (tn == "ColorGrading") cg = s; if (tn == "AutoExposure") ae = s; }
                if (ae != null) { var act = ae.GetType().GetField("active"); if (act != null) act.SetValue(ae, false); }
                if (cg != null)
                {
                    var tmParam = GetParam(cg, "tonemapper"); var tmEnum = T("UnityEngine.Rendering.PostProcessing.Tonemapper");
                    if (tmParam != null && tmEnum != null) { try { SetParam(tmParam, Enum.Parse(tmEnum, rules.tonemapper)); } catch { log.Add("post: tonemapper '" + rules.tonemapper + "' unknown, left as is"); } }
                    var exParam = GetParam(cg, "postExposure"); float ev = rules.exposure_ev.start; SetParam(exParam, ev);
                    float sky, green; int skyPx; var tg = rules.targets;
                    for (int it = 0; it < rules.exposure_ev.max_iterations; it++)
                    {
                        EditorUtility.SetDirty(profile as UnityEngine.Object); Measure(cam, tg, out sky, out green, out skyPx);
                        bool skyOk = float.IsNaN(sky) || (sky >= tg.sky_luma_mean[0] && sky <= tg.sky_luma_mean[1]); bool greenOk = float.IsNaN(green) || (green >= tg.green_luma[0] && green <= tg.green_luma[1]);
                        evLog.Add(string.Format(CI, "{{\"iteration\":{0},\"ev\":{1:F3},\"sky_luma\":{2},\"sky_px\":{3},\"green_luma\":{4},\"sky_ok\":{5},\"green_ok\":{6}}}", it, ev, float.IsNaN(sky) ? "null" : sky.ToString("F1", CI), skyPx, float.IsNaN(green) ? "null" : green.ToString("F1", CI), skyOk ? "true" : "false", greenOk ? "true" : "false"));
                        log.Add(string.Format(CI, "post: iteration {0}: EV {1:+0.00;-0.00} -> sky luma {2} ({3} px) {4}, green luma {5} {6}", it, ev, float.IsNaN(sky) ? "n/a" : sky.ToString("F1", CI), skyPx, skyOk ? "ok" : "OFF", float.IsNaN(green) ? "n/a" : green.ToString("F1", CI), greenOk ? "ok" : "OFF"));
                        if (skyOk && greenOk) break;
                        // sky drives the step when it is blown (tracer visibility), green otherwise; both expressed as log2 ratio to the band centre
                        float target, measured;
                        if (!skyOk && !float.IsNaN(sky)) { target = 0.5f * (tg.sky_luma_mean[0] + tg.sky_luma_mean[1]); measured = sky; } else { target = 0.5f * (tg.green_luma[0] + tg.green_luma[1]); measured = green; }
                        if (measured <= 1) measured = 1; ev = Mathf.Clamp(ev + rules.exposure_ev.damping * Mathf.Log(target / measured, 2f), rules.exposure_ev.min, rules.exposure_ev.max); SetParam(exParam, ev);
                    }
                    EditorUtility.SetDirty(profile as UnityEngine.Object); AssetDatabase.SaveAssets();
                    profLine = string.Format(CI, "post: profile {0} (copied from {1}); tonemapper {2}; auto-exposure off; final EV {3:+0.00;-0.00}", newPath, oldPath, GetValue(tmParam), (float)GetValue(exParam));
                }
                else profLine = "post: profile has no ColorGrading block; add one to the course copy and rerun";
            }
            else profLine = "post: no active PostProcessVolume in the scene";
        }
        else if (!cam) profLine = "post: no Main Camera (tag MainCamera) to measure from";
        log.Add(profLine);
        js.Add("\"exposure\":[" + string.Join(",", evLog.ToArray()) + "]"); js.Add("\"profile\":\"" + profLine.Replace("\"", "'") + "\"");
        File.WriteAllText(dir + "/post_profile_" + stamp + ".json", "{" + string.Join(",", js.ToArray()) + "}");
        File.WriteAllLines(dir + "/post_profile_" + stamp + ".md", new[] { "# Post profile calibration " + stamp }.Concat(log).ToArray());
        var txt = string.Join("\n", log.ToArray()); Debug.Log("GSProPostProfileCalibrator:\n" + txt); return txt + "\n-> " + dir + "/post_profile_" + stamp + ".json";
    }
}
