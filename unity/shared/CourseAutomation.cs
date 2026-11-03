// Course Tools > Automation
// Runs a course's editor tools in order from a JSON config, saves the scene, checks what got built, renders screenshots
// from fixed camera spots and writes a summary. Nothing in here is Tierra specific: each course has its own config
// (Assets/CourseAutomation/<scene>.json, or -automationConfig <file>), see automation/README.md.
//
// Three ways to run it:
//   1. Batch, Unity closed:  Unity.exe -batchmode -projectPath <project> -executeMethod CourseAutomation.RunBatch
//                            -automationConfig <config.json> [-automationSteps "Plant shrubs,Paint grass"] [-automationNoShots]
//                            (automation/Run-CourseAutomation.ps1 does this and prints the summary)
//   2. Menu, Unity open:     Course Tools > Automation > Run pipeline / Screenshots only / Inspect scene
//   3. Live bridge, Unity open: drop a command JSON in <project>/Automation/inbox, the answer lands in
//                            <project>/Automation/outbox (automation/Send-UnityCommand.ps1). Lets a Claude session on the
//                            PC drive the open editor: run steps, call any tool method, open menus, take screenshots.
// Output: <project>/AutomationOutput/<yyyyMMdd_HHmmss>/summary.md, summary.json, shots/*.png, index.html.
// Unity 2018.2. Put this file in Assets/Editor.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CourseAutomation
{
    // ------------------------------------------------------------------ config (JsonUtility, so arrays not dictionaries)
    [Serializable] public class FieldSet { public string field; public string value; }

    [Serializable]
    public class Step
    {
        public string name;
        public bool disabled;            // leave in the config but skip it
        public bool optional;            // a failure is a warning, the pipeline carries on
        public string tool;              // class name, e.g. TierraPlantShrubs (any assembly)
        public string method;            // method on it, private is fine; static or on the tool's window
        public FieldSet[] set;           // fields to set on the tool window before the call
        public string menu;              // or: a menu item to run, e.g. "Tierra/Build Cart Paths"
        public string[] requireFiles;    // fail early (no dialog) if one of these is missing
        public string expectObject;      // fail if this scene object doesn't exist after the step
        public int expectMinChildren;    // ... or has fewer descendants than this
        public string expectTerrainDetails; // fail if this terrain (name) has no detail prototypes after the step
    }

    [Serializable]
    public class Shot
    {
        public string name;
        public float[] pos;              // camera position (x, y, z)
        public float[] lookAt;           // target (x, y, z)
        public bool aboveGround;         // y values are metres above the terrain, not absolute
        public string frameObject;       // or: frame this scene object (name or path) from bounds
        public bool eachChild;           // frame each direct child of frameObject on its own
        public float[] viewDir;          // direction the camera looks when framing (default from the south-west, down 30 deg)
        public float fov;                // default 50
    }

    [Serializable]
    public class Config
    {
        public string course;            // label for the summary
        public string scene;             // scene name or path to open (batch); empty = keep the open scene
        public string repo;              // {Repo} token, default {Documents}/<course>
        public bool saveScene = true;
        public Step[] steps;
        public string[] inspect;         // scene objects to check after the steps (counts, colliders, physics materials)
        public Shot[] shots;
        public string gkd;               // Greenkeeper file for automatic tee and green shots per hole (empty = none)
        public string teeType;           // tee used for hole shots, e.g. Blue (falls back to any enabled tee)
        public bool teeShots = true, greenShots = true;
        public int shotWidth = 1600, shotHeight = 900;
    }

    // ------------------------------------------------------------------ report
    [Serializable] public class StepResult { public string name; public string status; public float seconds; public string message; public List<string> log = new List<string>(); }
    [Serializable]
    public class ObjectReport
    {
        public string name; public bool found, active; public int descendants, renderers, missingMaterials, colliders, collidersNoPhysics, meshColliders, staticObjects;
        public string layers, physicsMaterials, boundsMin, boundsMax;
    }
    [Serializable]
    public class Report
    {
        public string course, scene, started, finished, outputDir; public bool ok; public int errors, warnings;
        public List<StepResult> steps = new List<StepResult>();
        public List<ObjectReport> objects = new List<ObjectReport>();
        public List<string> sceneRoots = new List<string>();
        public List<string> shots = new List<string>();
        public List<string> problems = new List<string>();
    }

    const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public const string CONFIG_DIR = "Assets/CourseAutomation";
    static StepResult current;
    static Report report;

    // ------------------------------------------------------------------ entry points
    // Unity.exe -batchmode -executeMethod CourseAutomation.RunBatch -automationConfig <file> [-automationSteps a,b] [-automationNoShots]
    public static void RunBatch()
    {
        int code = 1;
        try
        {
            string cfgPath = Arg("-automationConfig");
            string steps = Arg("-automationSteps");
            bool shots = !HasArg("-automationNoShots");
            Config c = LoadConfig(cfgPath);
            if (c == null) { Debug.LogError("CourseAutomation: no config (" + (cfgPath ?? "none given") + ")"); }
            else
            {
                if (!string.IsNullOrEmpty(c.scene) && !OpenScene(c.scene)) Debug.LogError("CourseAutomation: scene not found: " + c.scene);
                else { Report r = Run(c, Split(steps), shots); code = r.ok ? 0 : 2; }
            }
        }
        catch (Exception e) { Debug.LogError("CourseAutomation: " + e); }
        EditorApplication.Exit(code);
    }

    [MenuItem("Course Tools/Automation/Run pipeline")]
    static void MenuRun() { Report r = RunForScene(null, true); if (r != null) Reveal(r); }

    [MenuItem("Course Tools/Automation/Screenshots only")]
    static void MenuShots() { Report r = RunForScene(new string[0], true); if (r != null) Reveal(r); }

    [MenuItem("Course Tools/Automation/Inspect scene")]
    static void MenuInspect() { Report r = RunForScene(new string[0], false); if (r != null) Reveal(r); }

    [MenuItem("Course Tools/Automation/Open config")]
    static void MenuConfig()
    {
        string p = SceneConfigPath();
        if (!File.Exists(p)) { EditorUtility.DisplayDialog("Automation", "No config for this scene:\n" + p + "\n\nCopy one from automation/configs in the repo.", "OK"); return; }
        EditorUtility.RevealInFinder(p);
    }

    static Report RunForScene(string[] only, bool shots)
    {
        Config c = LoadConfig(SceneConfigPath());
        if (c == null) { EditorUtility.DisplayDialog("Automation", "No config for this scene:\n" + SceneConfigPath(), "OK"); return null; }
        return Run(c, only, shots);
    }

    static void Reveal(Report r)
    {
        Debug.Log("CourseAutomation: " + (r.ok ? "OK" : "FAILED") + ", summary in " + r.outputDir);
        EditorUtility.RevealInFinder(Path.Combine(r.outputDir, "summary.md"));
    }

    public static string SceneConfigPath() { return CONFIG_DIR + "/" + EditorSceneManager.GetActiveScene().name + ".json"; }

    public static Config LoadConfig(string path)
    {
        if (string.IsNullOrEmpty(path)) path = SceneConfigPath();
        if (!File.Exists(path)) return null;
        Config c = JsonUtility.FromJson<Config>(File.ReadAllText(path));
        if (c != null && c.steps == null) c.steps = new Step[0];
        return c;
    }

    // ------------------------------------------------------------------ the pipeline
    // only: step names to run (null = all enabled steps, empty = none, just inspect and shots)
    public static Report Run(Config c, string[] only, bool shots)
    {
        report = new Report();
        report.course = string.IsNullOrEmpty(c.course) ? EditorSceneManager.GetActiveScene().name : c.course;
        report.scene = EditorSceneManager.GetActiveScene().path;
        report.started = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        report.outputDir = Path.Combine(ProjectDir(), "AutomationOutput/" + DateTime.Now.ToString("yyyyMMdd_HHmmss")).Replace('\\', '/');
        Directory.CreateDirectory(report.outputDir);
        Application.logMessageReceived += OnLog;
        bool changed = false;
        try
        {
            foreach (Step st in c.steps)
            {
                if (only != null ? !Contains(only, st.name) : st.disabled) continue;
                StepResult r = RunStep(c, st);
                report.steps.Add(r);
                if (r.status != "skipped") changed = true;
                if (r.status == "failed" && !st.optional) { report.problems.Add("Stopped after failed step: " + st.name); break; }
            }
            current = null;
            if (changed && c.saveScene)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene())) report.problems.Add("Scene save failed");
                AssetDatabase.SaveAssets();
            }
            Inspect(c);
            if (shots) Shots(c);
        }
        catch (Exception e) { report.problems.Add("Pipeline error: " + e.Message); Debug.LogException(e); }
        finally
        {
            Application.logMessageReceived -= OnLog;
            current = null;
            EditorUtility.ClearProgressBar();
        }
        report.ok = report.problems.Count == 0;
        foreach (StepResult r in report.steps) if (r.status == "failed" && !IsOptional(c, r.name)) report.ok = false;
        report.finished = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        WriteReport(report);
        Debug.Log("CourseAutomation: " + (report.ok ? "OK" : "FAILED") + ", " + report.outputDir);
        return report;
    }

    static bool IsOptional(Config c, string name)
    {
        foreach (Step s in c.steps) if (s.name == name) return s.optional;
        return false;
    }

    static StepResult RunStep(Config c, Step st)
    {
        StepResult r = new StepResult();
        r.name = string.IsNullOrEmpty(st.name) ? (st.menu ?? st.tool + "." + st.method) : st.name;
        current = r;
        float t0 = (float)EditorApplication.timeSinceStartup;
        try
        {
            if (st.requireFiles != null)
                foreach (string f in st.requireFiles)
                {
                    string p = Expand(c, f);
                    if (!File.Exists(p) && !Directory.Exists(p)) throw new Exception("missing " + p);
                }
            Selection.objects = new Object[0];   // CourseLook and others act on the selection when there is one
            if (!string.IsNullOrEmpty(st.menu))
            {
                if (!EditorApplication.ExecuteMenuItem(st.menu)) throw new Exception("menu item not found: " + st.menu);
            }
            else Invoke(c, st.tool, st.method, st.set);
            Check(st);
            r.status = "ok";
        }
        catch (Exception e)
        {
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            r.status = "failed";
            r.message = e.Message;
            Debug.LogError("CourseAutomation step '" + r.name + "' failed: " + e.Message);
        }
        r.seconds = (float)EditorApplication.timeSinceStartup - t0;
        current = null;
        EditorUtility.ClearProgressBar();
        return r;
    }

    // The tools return quietly (after a dialog) when something is missing, so check that they made something.
    static void Check(Step st)
    {
        if (!string.IsNullOrEmpty(st.expectObject))
        {
            GameObject g = FindObject(st.expectObject);
            if (g == null) throw new Exception("nothing built: no " + st.expectObject + " in the scene (check the log above for the reason)");
            int n = g.GetComponentsInChildren<Transform>(true).Length - 1;
            if (n < st.expectMinChildren) throw new Exception(st.expectObject + " has only " + n + " objects under it (expected " + st.expectMinChildren + "+)");
            current.message = st.expectObject + ": " + n + " objects";
        }
        if (!string.IsNullOrEmpty(st.expectTerrainDetails))
        {
            GameObject g = FindObject(st.expectTerrainDetails);
            Terrain t = g != null ? g.GetComponent<Terrain>() : null;
            if (t == null) throw new Exception("terrain not found: " + st.expectTerrainDetails);
            int n = t.terrainData.detailPrototypes.Length;
            if (n == 0) throw new Exception(st.expectTerrainDetails + " has no detail layers after painting");
            current.message = st.expectTerrainDetails + ": " + n + " detail layers";
        }
    }

    // ------------------------------------------------------------------ calling tools by reflection
    public static object Invoke(Config c, string tool, string method, FieldSet[] set)
    {
        Type t = FindType(tool);
        if (t == null) throw new Exception("tool " + tool + " isn't compiled in this project (is the .cs in Assets/Editor?)");
        MethodInfo m = FindMethod(t, method);
        if (m == null) throw new Exception(tool + " has no method '" + method + "'. It has: " + MethodList(t));
        if (m.GetParameters().Length > 0) throw new Exception(tool + "." + method + " takes parameters, call a no-argument one");
        object target = null;
        if (!m.IsStatic || (set != null && set.Length > 0)) target = Instance(t);
        if (set != null) foreach (FieldSet f in set) SetField(c, target, f.field, f.value);
        return m.Invoke(m.IsStatic ? null : target, null);
    }

    // The tools are EditorWindows that keep their settings in fields (filled in OnEnable, or by another tool, e.g.
    // Vegetation > Load shrubs into Plant Shrubs). Use the open window if there is one, so those settings carry over.
    public static object Instance(Type t)
    {
        if (typeof(ScriptableObject).IsAssignableFrom(t))
        {
            Object[] open = Resources.FindObjectsOfTypeAll(t);
            if (open.Length > 0) return open[0];
            if (typeof(EditorWindow).IsAssignableFrom(t))
            {
                try { return EditorWindow.GetWindow(t, false, t.Name, false); }
                catch (Exception e) { Debug.LogWarning("CourseAutomation: GetWindow(" + t.Name + ") failed (" + e.Message + "), using a hidden instance"); }
            }
            return ScriptableObject.CreateInstance(t);
        }
        return Activator.CreateInstance(t);
    }

    public static Type FindType(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    static MethodInfo FindMethod(Type t, string name)
    {
        for (Type b = t; b != null && b != typeof(object); b = b.BaseType)
            foreach (MethodInfo m in b.GetMethods(ALL | BindingFlags.DeclaredOnly))
                if (m.Name == name && m.GetParameters().Length == 0) return m;
        for (Type b = t; b != null && b != typeof(object); b = b.BaseType)
            foreach (MethodInfo m in b.GetMethods(ALL | BindingFlags.DeclaredOnly))
                if (m.Name == name) return m;
        return null;
    }

    static bool IsUnityMessage(string n)
    {
        return n == "OnGUI" || n == "OnEnable" || n == "OnDisable" || n == "OnDestroy" || n == "Update" || n == "OnSceneGUI" ||
               n == "OnFocus" || n == "OnLostFocus" || n == "OnSelectionChange" || n == "OnHierarchyChange" || n == "OnInspectorUpdate";
    }

    static string MethodList(Type t)
    {
        List<string> names = new List<string>();
        foreach (MethodInfo m in t.GetMethods(ALL | BindingFlags.DeclaredOnly))
            if (m.GetParameters().Length == 0 && !m.IsSpecialName && !m.Name.StartsWith("<") && !IsUnityMessage(m.Name)) names.Add(m.Name);
        return string.Join(", ", names.ToArray());
    }

    // Everything a tool has: fields with their current values and the no-argument methods (bridge "describe").
    public static string Describe(string tool)
    {
        Type t = FindType(tool);
        if (t == null) return "tool " + tool + " not found";
        object inst = null;
        if (!t.IsAbstract) { try { inst = Instance(t); } catch { } }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(t.FullName + " : " + t.BaseType.Name);
        foreach (MethodInfo m in t.GetMethods(ALL))
        {
            foreach (object at in m.GetCustomAttributes(typeof(MenuItem), false)) sb.AppendLine("menu: " + ((MenuItem)at).menuItem + " -> " + m.Name);
        }
        sb.AppendLine("fields:");
        foreach (FieldInfo f in t.GetFields(ALL | BindingFlags.DeclaredOnly))
        {
            if (f.Name.Contains("<")) continue;
            object v = null;
            try { v = f.IsStatic ? f.GetValue(null) : (inst != null ? f.GetValue(inst) : null); } catch { }
            sb.AppendLine("  " + f.FieldType.Name + " " + f.Name + " = " + ValueText(v));
        }
        sb.AppendLine("methods (no arguments): " + MethodList(t));
        return sb.ToString();
    }

    static string ValueText(object v)
    {
        if (v == null) return "null";
        Object o = v as Object;
        if (o != null) { string p = AssetDatabase.GetAssetPath(o); return o.name + (string.IsNullOrEmpty(p) ? "" : " (" + p + ")"); }
        Array arr = v as Array;
        if (arr != null) return arr.GetType().GetElementType().Name + "[" + arr.Length + "]";
        if (v is Vector3 || v is Color || v is Vector2) return v.ToString();
        return System.Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    static void SetField(Config c, object target, string name, string value)
    {
        if (target == null) throw new Exception("no instance to set " + name + " on");
        Type t = target.GetType();
        FieldInfo f = null;
        for (Type b = t; b != null && f == null; b = b.BaseType) f = b.GetField(name, ALL | BindingFlags.DeclaredOnly);
        if (f != null) { f.SetValue(target, ConvertValue(c, f.FieldType, value)); return; }
        PropertyInfo p = t.GetProperty(name, ALL);
        if (p != null && p.CanWrite) { p.SetValue(target, ConvertValue(c, p.PropertyType, value), null); return; }
        throw new Exception(t.Name + " has no field " + name);
    }

    // value formats: plain text, numbers, true/false, enum names, "x,y,z" for vectors,
    // asset path for assets (Assets/...), "scene:Name" for scene objects, ";" between items for arrays
    static object ConvertValue(Config c, Type type, string value)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (type == typeof(string)) return Expand(c, value);
        if (type == typeof(float)) return float.Parse(value, ci);
        if (type == typeof(int)) return int.Parse(value, ci);
        if (type == typeof(double)) return double.Parse(value, ci);
        if (type == typeof(bool)) return bool.Parse(value);
        if (type.IsEnum) return Enum.Parse(type, value, true);
        if (type == typeof(Vector3)) { float[] v = Floats(value); return new Vector3(v[0], v[1], v[2]); }
        if (type == typeof(Vector2)) { float[] v = Floats(value); return new Vector2(v[0], v[1]); }
        if (type.IsArray)
        {
            string[] parts = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Array arr = Array.CreateInstance(type.GetElementType(), parts.Length);
            for (int i = 0; i < parts.Length; i++) arr.SetValue(ConvertValue(c, type.GetElementType(), parts[i].Trim()), i);
            return arr;
        }
        if (typeof(Object).IsAssignableFrom(type))
        {
            if (string.IsNullOrEmpty(value) || value == "null") return null;
            Object o;
            if (value.StartsWith("scene:"))
            {
                GameObject g = FindObject(value.Substring(6));
                if (g == null) throw new Exception("scene object not found: " + value);
                o = type == typeof(GameObject) ? (Object)g : g.GetComponent(type);
            }
            else if (value.StartsWith("resources:")) o = Resources.Load(value.Substring(10), type);
            else if (value.Contains("#"))       // sub-asset by name, e.g. a material inside an FBX: path#Material Name
            {
                string path = Expand(c, value.Substring(0, value.IndexOf('#'))), name = value.Substring(value.IndexOf('#') + 1);
                o = AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(a => a != null && type.IsInstanceOfType(a) && a.name == name);
            }
            else o = AssetDatabase.LoadAssetAtPath(Expand(c, value), type);
            if (o == null) throw new Exception("couldn't load " + type.Name + " from " + value);
            return o;
        }
        throw new Exception("can't set a " + type.Name + " from text");
    }

    // ------------------------------------------------------------------ checking what was built
    static void Inspect(Config c)
    {
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            report.sceneRoots.Add(g.name + (g.activeSelf ? "" : " (inactive)") + ": " + (g.GetComponentsInChildren<Transform>(true).Length - 1) + " under it");
        if (c.inspect == null) return;
        foreach (string name in c.inspect)
        {
            ObjectReport o = new ObjectReport();
            o.name = name;
            GameObject g = FindObject(name);
            report.objects.Add(o);
            if (g == null) { report.problems.Add("Not in the scene: " + name); continue; }
            o.found = true;
            o.active = g.activeInHierarchy;
            if (!o.active) report.problems.Add(name + " is inactive (it won't be in the course)");
            Transform[] all = g.GetComponentsInChildren<Transform>(true);
            o.descendants = all.Length - 1;
            HashSet<string> layers = new HashSet<string>(), phys = new HashSet<string>();
            foreach (Transform t in all)
            {
                layers.Add(LayerMask.LayerToName(t.gameObject.layer));
                if (t.gameObject.isStatic) o.staticObjects++;
            }
            Bounds b = new Bounds(); bool any = false;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                o.renderers++;
                foreach (Material m in r.sharedMaterials) if (m == null) { o.missingMaterials++; break; }
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            foreach (Collider col in g.GetComponentsInChildren<Collider>(true))
            {
                o.colliders++;
                if (col is MeshCollider) o.meshColliders++;
                if (col.sharedMaterial == null) o.collidersNoPhysics++; else phys.Add(col.sharedMaterial.name);
            }
            o.layers = string.Join(", ", new List<string>(layers).ToArray());
            o.physicsMaterials = string.Join(", ", new List<string>(phys).ToArray());
            if (any) { o.boundsMin = V3(b.min); o.boundsMax = V3(b.max); }
            if (o.missingMaterials > 0) report.problems.Add(name + ": " + o.missingMaterials + " renderers with a missing material (magenta)");
            if (o.collidersNoPhysics > 0) report.problems.Add(name + ": " + o.collidersNoPhysics + " colliders without a physics material (the GSP build warns or fails)");
        }
    }

    // ------------------------------------------------------------------ screenshots
    static void Shots(Config c)
    {
        string dir = Path.Combine(report.outputDir, "shots");
        Directory.CreateDirectory(dir);
        GameObject go = new GameObject("CourseAutomationCamera");
        go.hideFlags = HideFlags.HideAndDontSave;
        Camera cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 6000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        RenderTexture rt = new RenderTexture(c.shotWidth > 0 ? c.shotWidth : 1600, c.shotHeight > 0 ? c.shotHeight : 900, 24);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        try
        {
            if (c.shots != null)
                foreach (Shot s in c.shots)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(s.frameObject))
                        {
                            GameObject g = FindObject(s.frameObject);
                            if (g == null) { report.problems.Add("Shot " + s.name + ": no " + s.frameObject + " in the scene"); continue; }
                            if (s.eachChild)
                            {
                                int i = 0;
                                foreach (Transform ch in g.transform) Frame(cam, rt, dir, s, ch.gameObject, (s.name ?? g.name) + "_" + (i++).ToString("00") + "_" + Safe(ch.name));
                            }
                            else Frame(cam, rt, dir, s, g, s.name ?? g.name);
                        }
                        else
                        {
                            Vector3 p = Vec(s.pos), l = Vec(s.lookAt);
                            if (s.aboveGround) { p.y += Ground(p); l.y += Ground(l); }
                            Render(cam, rt, dir, s.name, p, l, s.fov);
                        }
                    }
                    catch (Exception e) { report.problems.Add("Shot " + s.name + ": " + e.Message); }
                }
            HoleShots(c, cam, rt, dir);
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }

    static void Frame(Camera cam, RenderTexture rt, string dir, Shot s, GameObject g, string name)
    {
        Bounds b = new Bounds(g.transform.position, Vector3.one * 4f); bool any = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        Vector3 look = s.viewDir != null && s.viewDir.Length == 3 ? Vec(s.viewDir).normalized : new Vector3(0.61f, -0.5f, 0.61f).normalized;
        float fov = s.fov > 0 ? s.fov : 50f;
        float radius = Mathf.Max(b.extents.magnitude, 5f);
        float dist = radius / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * 1.05f;
        Vector3 p = b.center - look * dist;
        float floor = Ground(p) + 2f;
        if (p.y < floor) p.y = floor;
        Render(cam, rt, dir, name, p, b.center, fov);
    }

    // a golfer's view from behind each tee and a high view onto each green, from the Greenkeeper file
    static void HoleShots(Config c, Camera cam, RenderTexture rt, string dir)
    {
        string path = Expand(c, c.gkd);
        if (string.IsNullOrEmpty(c.gkd)) return;
        if (!File.Exists(path)) { report.problems.Add("GKD not found for hole shots: " + path); return; }
        GkCourse course = JsonUtility.FromJson<GkCourse>(File.ReadAllText(path));
        if (course == null || course.Holes == null) { report.problems.Add("No holes in " + path); return; }
        foreach (GkHole h in course.Holes)
        {
            if (!h.Enabled || h.Tees == null || h.Pins == null || h.Pins.Length == 0 || !Has(h.Pins[0].Position)) continue;
            GkTee tee = null;
            foreach (GkTee t in h.Tees) if (t.Enabled && Has(t.Position) && t.TeeType == c.teeType) tee = t;
            if (tee == null) foreach (GkTee t in h.Tees) if (t.Enabled && Has(t.Position)) { tee = t; break; }
            if (tee == null) continue;
            Vector3 a = V(tee.Position), pin = V(h.Pins[0].Position);
            a.y = Ground(a); pin.y = Ground(pin);
            Vector3 d = pin - a; d.y = 0; d.Normalize();
            string n = CourseHoleNames.FileTag(course.CourseName, h.HoleNumber, course.Holes.Length);   // "Ridgeline_1" for named nines
            if (c.teeShots) Render(cam, rt, dir, n + "_tee", a - d * 6f + Vector3.up * 2.2f, pin + Vector3.up * 1f, 45f);
            if (c.greenShots) Render(cam, rt, dir, n + "_green", pin - d * 50f + Vector3.up * 28f, pin, 40f);
        }
    }

    static void Render(Camera cam, RenderTexture rt, string dir, string name, Vector3 pos, Vector3 look, float fov)
    {
        cam.fieldOfView = fov > 0 ? fov : 50f;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized == Vector3.zero ? Vector3.forward : (look - pos).normalized);
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        img.Apply();
        RenderTexture.active = prev;
        string file = Safe(name) + ".png";
        File.WriteAllBytes(Path.Combine(dir, file), img.EncodeToPNG());
        Object.DestroyImmediate(img);
        report.shots.Add("shots/" + file + "  camera " + V3(pos) + " looking at " + V3(look));
    }

    [Serializable] public class GkVec { public float x, y, z; }
    [Serializable] public class GkTee { public string TeeType; public bool Enabled; public GkVec Position; }
    [Serializable] public class GkPin { public string Day; public GkVec Position; }
    [Serializable] public class GkHole { public bool Enabled; public int HoleNumber; public int Par; public GkTee[] Tees; public GkPin[] Pins; }
    [Serializable] public class GkCourse { public string CourseName; public GkHole[] Holes; }
    static bool Has(GkVec v) { return v != null && !(v.x == 0f && v.y == 0f && v.z == 0f); }
    static Vector3 V(GkVec v) { return new Vector3(v.x, v.y, v.z); }

    // highest terrain under a point (the outer flat terrain sits below the course terrain)
    static float Ground(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (Terrain t in Terrain.activeTerrains)
        {
            if (t == null || t.terrainData == null) continue;
            Vector3 o = t.GetPosition(), size = t.terrainData.size;
            if (p.x < o.x || p.z < o.z || p.x > o.x + size.x || p.z > o.z + size.z) continue;
            best = Mathf.Max(best, t.SampleHeight(p) + o.y);
        }
        return float.IsNegativeInfinity(best) ? 0f : best;
    }

    // ------------------------------------------------------------------ summary
    static void WriteReport(Report r)
    {
        File.WriteAllText(Path.Combine(r.outputDir, "summary.json"), JsonUtility.ToJson(r, true));
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# " + r.course + ": automation " + (r.ok ? "OK" : "FAILED"));
        sb.AppendLine();
        sb.AppendLine("Scene `" + r.scene + "`, " + r.started + " to " + r.finished + ". Errors logged: " + r.errors + ", warnings: " + r.warnings + ".");
        sb.AppendLine();
        sb.AppendLine("## Steps");
        if (r.steps.Count == 0) sb.AppendLine("(none run)");
        foreach (StepResult s in r.steps)
        {
            sb.AppendLine("- **" + s.name + "**: " + s.status + " (" + s.seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s)" + (string.IsNullOrEmpty(s.message) ? "" : ": " + s.message));
            foreach (string l in s.log) sb.AppendLine("  - " + l);
        }
        sb.AppendLine();
        sb.AppendLine("## Problems");
        if (r.problems.Count == 0) sb.AppendLine("None found.");
        foreach (string p in r.problems) sb.AppendLine("- " + p);
        sb.AppendLine();
        sb.AppendLine("## Checked objects");
        sb.AppendLine("| Object | Found | Objects | Renderers | Missing mat | Colliders | No physics | Mesh col | Static | Layers | Physics materials |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (ObjectReport o in r.objects)
            sb.AppendLine("| " + o.name + " | " + (o.found ? (o.active ? "yes" : "inactive") : "**no**") + " | " + o.descendants + " | " + o.renderers + " | " + o.missingMaterials +
                          " | " + o.colliders + " | " + o.collidersNoPhysics + " | " + o.meshColliders + " | " + o.staticObjects + " | " + o.layers + " | " + o.physicsMaterials + " |");
        sb.AppendLine();
        sb.AppendLine("## Scene root objects");
        foreach (string s in r.sceneRoots) sb.AppendLine("- " + s);
        sb.AppendLine();
        sb.AppendLine("## Screenshots");
        foreach (string s in r.shots) sb.AppendLine("- " + s);
        File.WriteAllText(Path.Combine(r.outputDir, "summary.md"), sb.ToString());

        StringBuilder html = new StringBuilder("<!doctype html><meta charset=utf-8><title>" + r.course + " shots</title><body style='background:#111;color:#ddd;font:14px sans-serif'>");
        foreach (string s in r.shots)
        {
            string f = s.Split(' ')[0];
            html.Append("<figure style='display:inline-block;margin:6px'><img src='" + f + "' style='width:480px'><figcaption>" + Path.GetFileNameWithoutExtension(f) + "</figcaption></figure>");
        }
        File.WriteAllText(Path.Combine(r.outputDir, "index.html"), html.ToString());
        File.WriteAllText(Path.Combine(ProjectDir(), "AutomationOutput/latest.txt"), r.outputDir);
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (report == null || msg.StartsWith("CourseAutomation: ")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors++;
        else if (type == LogType.Warning) report.warnings++;
        if (current == null || current.log.Count >= 40) return;
        string first = msg.Split('\n')[0];
        current.log.Add((type == LogType.Log ? "" : type.ToString().ToLower() + ": ") + (first.Length > 300 ? first.Substring(0, 300) + "..." : first));
    }

    // ------------------------------------------------------------------ helpers
    public static string ProjectDir() { return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); }

    public static bool OpenScene(string scene)
    {
        string path = scene;
        if (!scene.EndsWith(".unity"))
        {
            path = null;
            foreach (string guid in AssetDatabase.FindAssets(scene + " t:Scene"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == scene) { path = p; break; }
            }
        }
        if (path == null || !File.Exists(path)) return false;
        if (EditorSceneManager.GetActiveScene().path == path) return true;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    // name or path ("A/B/C"), inactive objects included
    public static GameObject FindObject(string name)
    {
        GameObject g = GameObject.Find(name);
        if (g != null) return g;
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            Transform t = name.Contains("/") && name.StartsWith(root.name + "/") ? root.transform.Find(name.Substring(root.name.Length + 1)) : null;
            if (t != null) return t.gameObject;
            foreach (Transform ch in root.GetComponentsInChildren<Transform>(true)) if (ch.name == name) return ch.gameObject;
        }
        return null;
    }

    public static string Expand(Config c, string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).Replace('\\', '/');
        string repo = c != null && !string.IsNullOrEmpty(c.repo) ? c.repo : docs + "/tierra-rejada";
        repo = repo.Replace("{Documents}", docs).Replace("{Project}", ProjectDir());
        string scene = EditorSceneManager.GetActiveScene().name;
        return s.Replace("{Documents}", docs).Replace("{Project}", ProjectDir()).Replace("{Repo}", repo).Replace("{Scene}", scene)
                .Replace("{Course}", c != null && !string.IsNullOrEmpty(c.course) ? c.course : scene);
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
    static bool HasArg(string name) { return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0; }

    public static string[] Split(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        List<string> l = new List<string>();
        foreach (string p in s.Split(',')) if (p.Trim().Length > 0) l.Add(p.Trim());
        return l.ToArray();
    }
    static bool Contains(string[] a, string s)
    {
        foreach (string x in a) if (string.Equals(x, s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    static float[] Floats(string s)
    {
        string[] p = s.Split(',');
        float[] v = new float[p.Length];
        for (int i = 0; i < p.Length; i++) v[i] = float.Parse(p[i].Trim(), CultureInfo.InvariantCulture);
        return v;
    }
    static Vector3 Vec(float[] v) { return v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.zero; }
    static string V3(Vector3 v) { return "(" + v.x.ToString("0.0", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.0", CultureInfo.InvariantCulture) + ", " + v.z.ToString("0.0", CultureInfo.InvariantCulture) + ")"; }
    static string Safe(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char ch in s ?? "shot") sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
        return sb.ToString();
    }
}

// ---------------------------------------------------------------------- live bridge
// While the editor is open, every second it picks up <project>/Automation/inbox/*.json and writes the answer to
// <project>/Automation/outbox/<id>.json. Commands (field "cmd"):
//   ping                                  is the editor there, which scene is open
//   run      [steps "a,b"] [shots] [config]  the pipeline (all enabled steps, or just the named ones)
//   shots    [config]                     screenshots and the object check only
//   invoke   tool, method, [set]          call any tool method, e.g. tool TierraPlantShrubs, method Plant
//   describe tool                         list a tool's menu items, fields (with values) and methods
//   menu     path                         run a menu item, e.g. "GSP/Build Course" (opens its window)
//   openScene scene | save | refresh      open a scene / save the open scene / reimport changed scripts and assets
// Turn it off with Course Tools > Automation > Live bridge.
[InitializeOnLoad]
public static class CourseAutomationBridge
{
    [Serializable]
    public class Command
    {
        public string id, cmd, config, steps, tool, method, path, scene;
        public bool shots = true;
        public CourseAutomation.FieldSet[] set;
    }
    [Serializable] public class Answer { public string id, cmd, status, message, outputDir, scene, finished; }

    const string PREF = "CourseAutomation.Bridge";
    const string MENU = "Course Tools/Automation/Live bridge";
    static double next;

    static CourseAutomationBridge()
    {
        if (InternalEditorUtility.inBatchMode) return;
        EditorApplication.update += Poll;
    }

    static bool On { get { return EditorPrefs.GetBool(PREF, true); } }

    [MenuItem(MENU)]
    static void Toggle() { EditorPrefs.SetBool(PREF, !On); Debug.Log("CourseAutomation: live bridge " + (On ? "on" : "off") + ", inbox " + Inbox()); }
    [MenuItem(MENU, true)]
    static bool ToggleCheck() { Menu.SetChecked(MENU, On); return true; }

    static string Inbox() { return CourseAutomation.ProjectDir() + "/Automation/inbox"; }
    static string Outbox() { return CourseAutomation.ProjectDir() + "/Automation/outbox"; }

    static void Poll()
    {
        if (!On || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating) return;
        string inbox = Inbox();
        if (!Directory.Exists(inbox)) { Directory.CreateDirectory(inbox); Directory.CreateDirectory(Outbox()); return; }
        string[] files = Directory.GetFiles(inbox, "*.json");
        if (files.Length == 0) return;
        Array.Sort(files);
        string file = files[0];
        string text;
        try { text = File.ReadAllText(file); File.Delete(file); }
        catch (IOException) { return; }   // still being written, next time
        Answer a = new Answer();
        a.id = Path.GetFileNameWithoutExtension(file);
        try
        {
            Command c = JsonUtility.FromJson<Command>(text);
            if (!string.IsNullOrEmpty(c.id)) a.id = c.id;
            a.cmd = c.cmd;
            Handle(c, a);
        }
        catch (Exception e)
        {
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            a.status = "error"; a.message = e.Message;
        }
        a.scene = EditorSceneManager.GetActiveScene().path;
        a.finished = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Directory.CreateDirectory(Outbox());
        File.WriteAllText(Path.Combine(Outbox(), a.id + ".json"), JsonUtility.ToJson(a, true));
    }

    static void Handle(Command c, Answer a)
    {
        a.status = "ok";
        switch ((c.cmd ?? "").ToLower())
        {
            case "ping":
                a.message = "Unity " + Application.unityVersion + ", project " + CourseAutomation.ProjectDir();
                break;
            case "run":
            case "shots":
            {
                CourseAutomation.Config cfg = CourseAutomation.LoadConfig(c.config);
                if (cfg == null) throw new Exception("no config (" + (c.config ?? CourseAutomation.SceneConfigPath()) + ")");
                string[] only = c.cmd.ToLower() == "shots" ? new string[0] : CourseAutomation.Split(c.steps);
                CourseAutomation.Report r = CourseAutomation.Run(cfg, only, c.shots);
                a.status = r.ok ? "ok" : "failed";
                a.outputDir = r.outputDir;
                a.message = File.ReadAllText(Path.Combine(r.outputDir, "summary.md"));
                break;
            }
            case "invoke":
            {
                CourseAutomation.Config cfg = CourseAutomation.LoadConfig(c.config) ?? new CourseAutomation.Config();
                object result = CourseAutomation.Invoke(cfg, c.tool, c.method, c.set);
                a.message = c.tool + "." + c.method + " done" + (result != null ? ", returned " + result : "");
                break;
            }
            case "describe":
                a.message = CourseAutomation.Describe(c.tool);
                break;
            case "menu":
                if (!EditorApplication.ExecuteMenuItem(c.path)) throw new Exception("menu item not found: " + c.path);
                a.message = "ran " + c.path;
                break;
            case "openscene":
                if (!CourseAutomation.OpenScene(c.scene)) throw new Exception("scene not found: " + c.scene);
                a.message = "opened " + c.scene;
                break;
            case "save":
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene())) throw new Exception("save failed");
                AssetDatabase.SaveAssets();
                a.message = "saved";
                break;
            case "refresh":
                AssetDatabase.Refresh();
                a.message = "refresh started (scripts recompile if they changed; ping again until it answers)";
                break;
            default:
                throw new Exception("unknown cmd '" + c.cmd + "' (ping, run, shots, invoke, describe, menu, openScene, save, refresh)");
        }
    }
}
