// Course Tools > Flyover Preview
// Works for any GSPro course: it reads the GKD that belongs to the open scene.
// Flies the Scene view (or a temporary Game view camera) down a hole the way GSPro's flyover does:
// from behind the tee, over the aim points, down to the pin. Tee, aim point and pin positions are read
// from the course GKD (Greenkeeper file), which uses the same Unity metres as the scene.
// Nothing is added to the scene that gets saved or built. Unity 2018.2. Put this file in Assets/Editor.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CourseFlyoverPreview : EditorWindow
{
    [Serializable] public class GkVec { public float x, y, z; }
    [Serializable] public class GkTee { public string TeeType; public bool Enabled; public GkVec Position; }
    [Serializable] public class GkPin { public string Day; public GkVec Position; }
    [Serializable] public class GkHole { public bool Enabled; public int HoleNumber; public int Par; public GkTee[] Tees; public GkPin[] Pins; }
    [Serializable] public class GkCourse { public string CourseName; public GkHole[] Holes; }

    // GKD tee slots, in the order GSPro lists them
    static readonly string[] TEE_SLOT = { "Black", "Green", "Blue", "White", "Yellow", "Red", "Junior", "Par3" };
    const string COURSES_DIR = @"C:\GSProV1\Core\GSP\Courses";
    const string PREF_GKD = "CourseFlyover.gkd.";   // + scene name
    const string CAM_NAME = "FLYOVER_PREVIEW_CAM";
    const float YARDS = 1.0936133f;

    string gkdPath = "";
    string loadedScene;
    GkCourse course;
    string loadError;
    int holeIdx, teeIdx, pinIdx;
    bool useGameView = true;   // Game view: real lighting and shadows, no editor icons
    bool playAll;
    bool showPath = true;
    bool showSettings;
    // low, shallow "drone" view so slopes and hills read, like GSPro's flyover
    float speed = 30f;          // average ground speed, m/s
    float startHeight = 12f;    // metres above the tee line at the start
    float endHeight = 9f;       // metres above the ground line at the end
    float lookAhead = 180f;     // camera looks at the ground this far ahead on the line
    float behindTee = 20f;      // start this far behind the tee
    float stopShort = 45f;      // stop this far short of the pin
    float minClear = 6f;        // never closer than this to the terrain, trees or shrubs (anywhere 12 m either side, 30 m ahead or behind)
    float fov = 55f;            // Game view camera only

    // ground line through tee, aim points and pin (control elevations, smoothed)
    readonly List<Vector3> path = new List<Vector3>();
    readonly List<float> along = new List<float>();
    float pathLen;
    readonly List<Vector3> marks = new List<Vector3>();
    readonly List<string> markNames = new List<string>();
    readonly List<float> markAlong = new List<float>();
    Vector3[] drawPath;
    string teeNote;

    float progress;
    bool playing;
    double lastTime, holdUntil;
    Camera previewCam;
    Vector2 scroll;

    [MenuItem("Course Tools/Flyover Preview")]
    static void Open() { GetWindow<CourseFlyoverPreview>("Flyover"); }

    void OnEnable()
    {
        EditorApplication.update += Tick;
        SceneView.onSceneGUIDelegate += OnSceneGUI;
        PickForScene();
    }

    static string SceneName() { return EditorSceneManager.GetActiveScene().name; }

    // the GKD last used with this scene, else the one in GSPro's folder for a course of the same name
    void PickForScene()
    {
        loadedScene = SceneName();
        gkdPath = EditorPrefs.GetString(PREF_GKD + loadedScene, "");
        if (!File.Exists(gkdPath))
        {
            gkdPath = "";
            string dir = Path.Combine(COURSES_DIR, loadedScene);
            if (!string.IsNullOrEmpty(loadedScene) && Directory.Exists(dir))
            {
                string[] found = Directory.GetFiles(dir, "*.GKD");
                if (found.Length > 0) gkdPath = found[0];
            }
        }
        Load();
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        RemoveCam();
        SceneView.RepaintAll();
    }

    // ---------------------------------------------------------------- data

    void Load()
    {
        course = null;
        loadError = null;
        path.Clear(); drawPath = null; marks.Clear();
        try
        {
            if (string.IsNullOrEmpty(gkdPath))
            {
                loadError = "No GKD found for scene '" + loadedScene + "' in " + COURSES_DIR + ". Click Browse and pick the course's .GKD.";
                return;
            }
            if (!File.Exists(gkdPath)) { loadError = "File not found:\n" + gkdPath; return; }
            GkCourse c = JsonUtility.FromJson<GkCourse>(File.ReadAllText(gkdPath));
            if (c == null || c.Holes == null || c.Holes.Length == 0) { loadError = "No holes found in " + gkdPath; return; }
            Array.Sort(c.Holes, (a, b) => a.HoleNumber.CompareTo(b.HoleNumber));
            course = c;
            EditorPrefs.SetString(PREF_GKD + loadedScene, gkdPath);
        }
        catch (Exception e) { loadError = "Could not read GKD: " + e.Message; return; }
        holeIdx = Mathf.Clamp(holeIdx, 0, course.Holes.Length - 1);
        BuildPath();
    }

    static bool Has(GkVec v) { return v != null && !(v.x == 0f && v.y == 0f && v.z == 0f); }
    static Vector3 V(GkVec v) { return new Vector3(v.x, v.y, v.z); }

    static GkTee FindTee(GkHole h, string type, bool needEnabled)
    {
        if (h.Tees == null) return null;
        foreach (GkTee t in h.Tees)
            if (t.TeeType == type && (t.Enabled || !needEnabled) && Has(t.Position)) return t;
        return null;
    }

    GkHole Hole { get { return course.Holes[holeIdx]; } }

    void BuildPath()
    {
        path.Clear(); along.Clear(); marks.Clear(); markNames.Clear(); markAlong.Clear();
        pathLen = 0f; drawPath = null; teeNote = null;
        progress = 0f; holdUntil = 0;
        if (course == null) return;
        GkHole h = Hole;

        GkTee tee = FindTee(h, TEE_SLOT[teeIdx], true);
        if (tee == null)
        {
            for (int i = 0; i < TEE_SLOT.Length && tee == null; i++) tee = FindTee(h, TEE_SLOT[i], true);
            if (tee != null) teeNote = "No " + TEE_SLOT[teeIdx] + " tee on this hole, using " + tee.TeeType + ".";
        }
        int pinCount = h.Pins != null ? h.Pins.Length : 0;
        pinIdx = pinCount > 0 ? Mathf.Clamp(pinIdx, 0, pinCount - 1) : 0;
        Vector3 pin;
        string pinName;
        if (pinCount > 0 && Has(h.Pins[pinIdx].Position)) { pin = V(h.Pins[pinIdx].Position); pinName = "Pin (" + h.Pins[pinIdx].Day + ")"; }
        else
        {
            GkTee gc = FindTee(h, "GreenCenterPoint", false);
            if (gc == null) { teeNote = CourseHoleNames.Label(course.CourseName, h.HoleNumber, course.Holes.Length) + " has no pin or green centre."; return; }
            pin = V(gc.Position); pinName = "Green centre";
        }
        if (tee == null) { teeNote = CourseHoleNames.Label(course.CourseName, h.HoleNumber, course.Holes.Length) + " has no tees."; return; }

        Vector3 teePos = V(tee.Position);
        List<Vector3> ctrl = new List<Vector3>();
        ctrl.Add(teePos); marks.Add(teePos); markNames.Add(tee.TeeType + " tee");
        float teeToPin = Flat(teePos, pin);
        for (int i = 1; i <= 9; i++)
        {
            GkTee a = FindTee(h, "AimPoint" + i, true);
            if (a == null) continue;
            Vector3 p = V(a.Position);
            if (Flat(p, pin) >= teeToPin - 10f) continue;   // aim point is behind this tee (forward tees)
            ctrl.Add(p); marks.Add(p); markNames.Add("Aim " + i);
        }
        ctrl.Add(pin); marks.Add(pin); markNames.Add(pinName);

        // Catmull-Rom through the control points, about one sample per 2 m
        for (int i = 0; i < ctrl.Count - 1; i++)
        {
            Vector3 p0 = ctrl[Mathf.Max(i - 1, 0)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(i + 2, ctrl.Count - 1)];
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 2f));
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n;
                Vector3 q = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                AddPathPoint(q);
            }
        }
        AddPathPoint(pin);

        // where each marker sits along the line
        for (int m = 0; m < marks.Count; m++)
        {
            float best = float.MaxValue, at = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                float d = Flat(path[i], marks[m]);
                if (d < best) { best = d; at = along[i]; }
            }
            markAlong.Add(at);
        }

        drawPath = new Vector3[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = path[i];
            float ter = TerrainHeight(p);
            if (!float.IsNegativeInfinity(ter)) p.y = ter;
            drawPath[i] = p + Vector3.up * 1f;
        }
        BuildHeights();
        ApplyAt(progress);
        SceneView.RepaintAll();
    }

    // camera height along the flight, every 2 m: the planned height over the line, raised wherever the ground, a tree or a
    // shrub (topmost collider) within 12 m either side comes within minClear, starting 30 m before the obstacle and eased,
    // so the camera never skims a hillside or flies through a canopy
    readonly List<float> camY = new List<float>();
    const float CamStep = 2f;

    // tree canopies (their colliders are only trunks): centre, radius and top of every tree's renderers, on a 20 m grid
    static Dictionary<long, List<Vector4>> canopies;
    static long CKey(int x, int z) { return ((long)x << 32) ^ (uint)z; }
    static void LoadCanopies()
    {
        canopies = new Dictionary<long, List<Vector4>>();
        foreach (var root in new[] { "LIDAR_TREES", "TREES" })
        {
            var g = GameObject.Find(root); if (g == null) continue;
            foreach (Transform t in g.transform)
            {
                var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float rad = Mathf.Max(b.extents.x, b.extents.z);
                var v = new Vector4(b.center.x, b.center.z, rad, b.max.y);
                for (int x = Mathf.FloorToInt((v.x - rad) / 20f); x <= Mathf.FloorToInt((v.x + rad) / 20f); x++)
                    for (int z = Mathf.FloorToInt((v.y - rad) / 20f); z <= Mathf.FloorToInt((v.y + rad) / 20f); z++)
                    {
                        List<Vector4> l; if (!canopies.TryGetValue(CKey(x, z), out l)) canopies[CKey(x, z)] = l = new List<Vector4>(); l.Add(v);
                    }
            }
        }
    }

    static float TopAt(Vector3 p)
    {
        float best = TerrainHeight(p);
        if (canopies == null) LoadCanopies();
        List<Vector4> cl;
        if (canopies.TryGetValue(CKey(Mathf.FloorToInt(p.x / 20f), Mathf.FloorToInt(p.z / 20f)), out cl))
            foreach (var c in cl)
                if ((p.x - c.x) * (p.x - c.x) + (p.z - c.y) * (p.z - c.y) <= c.z * c.z) best = Mathf.Max(best, c.w);
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 3000f, p.z), Vector3.down, out hit, 6000f, ~0, QueryTriggerInteraction.Ignore))
            best = Mathf.Max(best, hit.point.y);
        return best;
    }

    void BuildHeights()
    {
        camY.Clear(); canopies = null;
        if (path.Count < 2) return;
        int n = Mathf.Max(2, Mathf.CeilToInt((EndS - StartS) / CamStep) + 1);
        var plan = new float[n]; var need = new float[n];
        for (int i = 0; i < n; i++)
        {
            float s = StartS + i * CamStep;
            Vector3 g = GroundAt(s);
            Vector3 dir = GroundAt(s + 2f) - GroundAt(s - 2f); dir.y = 0;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Vector3 side = new Vector3(dir.z, 0, -dir.x);
            float ter = TerrainHeight(g);
            float k = Mathf.InverseLerp(StartS, EndS, s);
            plan[i] = Mathf.Max(g.y, float.IsNegativeInfinity(ter) ? g.y : ter) + Mathf.Lerp(startHeight, endHeight, k);
            float top = float.NegativeInfinity;
            foreach (float o in new[] { -12f, -6f, 0f, 6f, 12f }) top = Mathf.Max(top, TopAt(g + side * o));
            need[i] = float.IsNegativeInfinity(top) ? plan[i] : top + minClear;
        }
        int w = Mathf.CeilToInt(30f / CamStep), r = Mathf.CeilToInt(20f / CamStep);
        var y = new float[n];
        for (int i = 0; i < n; i++)
        {
            float m = plan[i];
            for (int j = Mathf.Max(0, i - w); j <= Mathf.Min(n - 1, i + w); j++) m = Mathf.Max(m, need[j]);
            y[i] = m;
        }
        for (int i = 0; i < n; i++)
        {
            float sum = 0f; int c = 0;
            for (int j = Mathf.Max(0, i - r); j <= Mathf.Min(n - 1, i + r); j++) { sum += y[j]; c++; }
            camY.Add(Mathf.Max(sum / c, need[i], plan[i]));
        }
    }

    // automation check: for every hole of the scene's GKD (Black tee), the lowest clearance of the camera over the ground,
    // trees and shrubs along the flight, new path vs the old terrain-only clamp; frames of one hole into outDir
    public static int AuditHole = 9;
    public static string AuditDir = "C:/Users/user/Documents/moorpark/previews/flyover";
    public static string Audit()
    {
        var w = CreateInstance<CourseFlyoverPreview>();
        var sb = new System.Text.StringBuilder();
        try
        {
            if (w.course == null) return "no course: " + w.loadError;
            sb.AppendLine(w.gkdPath);
            for (int h = 0; h < w.course.Holes.Length; h++)
            {
                if (!w.course.Holes[h].Enabled) continue;
                w.holeIdx = h; w.teeIdx = 0; w.BuildPath();
                if (w.path.Count < 2) continue;
                float worstNew = float.MaxValue, worstOld = float.MaxValue;
                for (int i = 0; i <= 300; i++)
                {
                    Vector3 pos; Quaternion rot; w.Evaluate(i / 300f, out pos, out rot);
                    worstNew = Mathf.Min(worstNew, pos.y - TopAt(pos));
                    float s = w.SAt(i / 300f), k = Mathf.InverseLerp(w.StartS, w.EndS, s);
                    Vector3 old = w.GroundAt(s) + Vector3.up * Mathf.Lerp(w.startHeight, w.endHeight, k);
                    float ter = TerrainHeight(old); if (!float.IsNegativeInfinity(ter)) old.y = Mathf.Max(old.y, ter + 5f);
                    worstOld = Mathf.Min(worstOld, old.y - TopAt(old));
                }
                sb.AppendLine(CourseHoleNames.Label(w.course.CourseName, w.course.Holes[h].HoleNumber, w.course.Holes.Length) +
                              ": lowest clearance over ground/trees/shrubs " + worstNew.ToString("0.0") + " m (was " + worstOld.ToString("0.0") + " m)");
                if (w.course.Holes[h].HoleNumber == AuditHole)
                {
                    Directory.CreateDirectory(AuditDir);
                    var go = new GameObject("AUDIT_CAM"); go.hideFlags = HideFlags.HideAndDontSave;
                    var cam = go.AddComponent<Camera>(); cam.fieldOfView = w.fov; cam.farClipPlane = 6000f;
                    var rt = new RenderTexture(1280, 720, 24); rt.antiAliasing = 4; cam.targetTexture = rt;
                    foreach (float p in new[] { 0f, 0.15f, 0.35f, 0.6f, 0.9f })
                    {
                        Vector3 pos; Quaternion rot; w.Evaluate(p, out pos, out rot);
                        go.transform.position = pos; go.transform.rotation = rot; cam.Render();
                        RenderTexture.active = rt; var img = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                        img.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); img.Apply(); RenderTexture.active = null;
                        File.WriteAllBytes(AuditDir + "/" + CourseHoleNames.FileTag(w.course.CourseName, w.course.Holes[h].HoleNumber, w.course.Holes.Length) + "_" + w.FindTeeName() + "_" + (int)(p * 100) + ".png", img.EncodeToPNG());
                        DestroyImmediate(img);
                    }
                    cam.targetTexture = null; DestroyImmediate(rt); DestroyImmediate(go);
                }
            }
        }
        finally { DestroyImmediate(w); }
        return sb.ToString();
    }
    string FindTeeName() { return marks.Count > 0 && markNames.Count > 0 ? markNames[0].Replace(" tee", "") : "tee"; }

    float CamHeightAt(float s)
    {
        if (camY.Count == 0) return float.NegativeInfinity;
        float f = Mathf.Clamp((s - StartS) / CamStep, 0f, camY.Count - 1);
        int i = Mathf.FloorToInt(f); int j = Mathf.Min(i + 1, camY.Count - 1);
        return Mathf.Lerp(camY[i], camY[j], f - i);
    }

    void AddPathPoint(Vector3 q)
    {
        if (path.Count > 0)
        {
            float d = Vector3.Distance(path[path.Count - 1], q);
            if (d < 0.01f) return;
            pathLen += d;
        }
        path.Add(q);
        along.Add(pathLen);
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

    // point on the ground line at distance s (extends straight back behind the tee when s < 0)
    Vector3 GroundAt(float s)
    {
        if (path.Count == 0) return Vector3.zero;
        if (path.Count == 1) return path[0];
        if (s <= 0f)
        {
            Vector3 dir = path[Mathf.Min(5, path.Count - 1)] - path[0];
            dir.y = 0;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            return path[0] + dir * s;
        }
        if (s >= pathLen) return path[path.Count - 1];
        int lo = 0, hi = along.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (along[mid] < s) lo = mid; else hi = mid;
        }
        float seg = along[hi] - along[lo];
        return Vector3.Lerp(path[lo], path[hi], seg > 0 ? (s - along[lo]) / seg : 0f);
    }

    static float TerrainHeight(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (Terrain t in Terrain.activeTerrains)
        {
            if (t == null || t.terrainData == null) continue;
            Vector3 o = t.GetPosition();
            Vector3 size = t.terrainData.size;
            if (p.x < o.x || p.z < o.z || p.x > o.x + size.x || p.z > o.z + size.z) continue;
            float y = t.SampleHeight(p) + o.y;
            if (y > best) best = y;
        }
        return best;
    }

    // ---------------------------------------------------------------- camera

    float StartS { get { return -behindTee; } }
    float EndS { get { return Mathf.Max(0f, pathLen - stopShort); } }

    // progress 0..1 is eased so the flight starts and ends gently
    float SAt(float p)
    {
        p = Mathf.Clamp01(p);
        return Mathf.Lerp(StartS, EndS, p * p * (3f - 2f * p));
    }

    float ProgressAtS(float s)
    {
        float k = Mathf.Clamp01(Mathf.InverseLerp(StartS, EndS, s)), lo = 0f, hi = 1f;
        for (int i = 0; i < 24; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (mid * mid * (3f - 2f * mid) < k) lo = mid; else hi = mid;
        }
        return 0.5f * (lo + hi);
    }

    void Evaluate(float p, out Vector3 pos, out Quaternion rot)
    {
        float s = SAt(p);
        float k = Mathf.InverseLerp(StartS, EndS, s);
        pos = GroundAt(s) + Vector3.up * Mathf.Lerp(startHeight, endHeight, k);
        float ch = CamHeightAt(s);
        if (!float.IsNegativeInfinity(ch)) pos.y = ch;
        float ter = TerrainHeight(pos);
        if (!float.IsNegativeInfinity(ter)) pos.y = Mathf.Max(pos.y, ter + minClear);
        Vector3 target = GroundAt(Mathf.Min(s + lookAhead, pathLen));
        Vector3 dir = target - pos;
        rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
    }

    void ApplyAt(float p)
    {
        if (path.Count < 2) return;
        Vector3 pos;
        Quaternion rot;
        Evaluate(p, out pos, out rot);
        if (useGameView)
        {
            EnsureCam();
            previewCam.fieldOfView = fov;
            previewCam.transform.position = pos;
            previewCam.transform.rotation = rot;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        else
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            sv.in2DMode = false;
            sv.orthographic = false;
            // the Scene view orbits a pivot; its perspective camera sits 'size' metres behind it
            const float size = 2f;
            sv.LookAtDirect(pos + rot * Vector3.forward * size, rot, size);
            sv.Repaint();
        }
    }

    void EnsureCam()
    {
        if (previewCam != null) return;
        GameObject old = GameObject.Find(CAM_NAME);
        if (old != null) DestroyImmediate(old);
        GameObject go = new GameObject(CAM_NAME);
        go.hideFlags = HideFlags.DontSave;   // never saved into the scene or the course build
        previewCam = go.AddComponent<Camera>();
        previewCam.depth = 100;
        previewCam.nearClipPlane = 0.3f;
        previewCam.farClipPlane = 5000f;
    }

    void RemoveCam()
    {
        if (previewCam != null) DestroyImmediate(previewCam.gameObject);
        previewCam = null;
        GameObject old = GameObject.Find(CAM_NAME);
        if (old != null) DestroyImmediate(old);
    }

    void Tick()
    {
        if (!playing || path.Count < 2) return;
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastTime), 0.1f);
        lastTime = now;
        if (holdUntil > 0)
        {
            if (now < holdUntil) return;
            holdUntil = 0;
            if (playAll && holeIdx < course.Holes.Length - 1) { holeIdx++; BuildPath(); lastTime = now; }
            else { playing = false; }
            Repaint();
            return;
        }
        float span = Mathf.Max(1f, EndS - StartS);
        progress = Mathf.Min(1f, progress + dt * speed / span);
        ApplyAt(progress);
        if (progress >= 1f) holdUntil = now + 2.0;
        Repaint();
    }

    void Play()
    {
        if (useGameView)
        {
            Type gv = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gv != null) GetWindow(gv, false, null, true);
        }
        if (progress >= 1f) progress = 0f;
        playing = true;
        holdUntil = 0;
        lastTime = EditorApplication.timeSinceStartup;
        SceneView.RepaintAll();
    }

    void JumpTo(float p)
    {
        playing = false;
        holdUntil = 0;
        progress = p;
        ApplyAt(progress);
        SceneView.RepaintAll();
    }

    // ---------------------------------------------------------------- UI

    void OnGUI()
    {
        if (SceneName() != loadedScene) { playing = false; PickForScene(); }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Scene '" + loadedScene + "': course file (GKD from Greenkeeper)");
        EditorGUILayout.BeginHorizontal();
        gkdPath = EditorGUILayout.TextField(gkdPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string f = EditorUtility.OpenFilePanel("Course GKD", string.IsNullOrEmpty(gkdPath) ? COURSES_DIR : Path.GetDirectoryName(gkdPath), "GKD");
            if (!string.IsNullOrEmpty(f)) { gkdPath = f; Load(); }
        }
        if (GUILayout.Button("Reload", GUILayout.Width(60))) Load();
        EditorGUILayout.EndHorizontal();

        if (loadError != null) EditorGUILayout.HelpBox(loadError, MessageType.Error);
        if (course == null) { EditorGUILayout.EndScrollView(); return; }

        EditorGUILayout.Space();
        string[] holeNames = new string[course.Holes.Length];
        for (int i = 0; i < holeNames.Length; i++) holeNames[i] = CourseHoleNames.Label(course.CourseName, course.Holes[i].HoleNumber, course.Holes.Length) + "  (par " + course.Holes[i].Par + ")";
        int oldHole = holeIdx, oldTee = teeIdx, oldPin = pinIdx;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(28)) && holeIdx > 0) holeIdx--;
        holeIdx = EditorGUILayout.Popup(holeIdx, holeNames);
        if (GUILayout.Button(">", GUILayout.Width(28)) && holeIdx < holeNames.Length - 1) holeIdx++;
        EditorGUILayout.EndHorizontal();
        teeIdx = EditorGUILayout.Popup("Tee (GKD slot)", teeIdx, TEE_SLOT);
        GkHole h = Hole;
        if (h.Pins != null && h.Pins.Length > 0)
        {
            string[] pinNames = new string[h.Pins.Length];
            for (int i = 0; i < pinNames.Length; i++) pinNames[i] = h.Pins[i].Day;
            pinIdx = EditorGUILayout.Popup("Pin", Mathf.Clamp(pinIdx, 0, pinNames.Length - 1), pinNames);
        }
        if (holeIdx != oldHole || teeIdx != oldTee || pinIdx != oldPin) { playing = false; BuildPath(); }

        if (teeNote != null) EditorGUILayout.HelpBox(teeNote, MessageType.Warning);
        if (marks.Count >= 2)
            EditorGUILayout.LabelField(markNames[0] + " to pin: " + Mathf.RoundToInt(Flat(marks[0], marks[marks.Count - 1]) * YARDS) +
                " yds, " + (marks.Count - 2) + " aim point(s)");

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        int view = GUILayout.Toolbar(useGameView ? 1 : 0, new[] { "Scene view", "Game view camera" });
        if (EditorGUI.EndChangeCheck())
        {
            useGameView = view == 1;
            if (!useGameView) RemoveCam();
            ApplyAt(progress);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(playing ? "Pause" : "Play flyover", GUILayout.Height(28)))
        {
            if (playing) playing = false; else Play();
        }
        if (GUILayout.Button("Back to tee", GUILayout.Height(28))) JumpTo(0f);
        EditorGUILayout.EndHorizontal();
        playAll = EditorGUILayout.ToggleLeft("Keep going to the next hole (play the round)", playAll);

        EditorGUI.BeginChangeCheck();
        float p = EditorGUILayout.Slider("Scrub", progress, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) JumpTo(p);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Tee view")) JumpTo(0f);
        if (marks.Count > 2 && GUILayout.Button("Landing view")) JumpTo(ProgressAtS(markAlong[1] - 60f));
        if (GUILayout.Button("Green view")) JumpTo(1f);
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        showPath = EditorGUILayout.ToggleLeft("Show the line, tee, aim points and pin in the Scene view", showPath);
        showSettings = EditorGUILayout.Foldout(showSettings, "Camera settings");
        if (showSettings)
        {
            speed = EditorGUILayout.Slider("Speed (m/s)", speed, 5f, 120f);
            startHeight = EditorGUILayout.Slider("Height at start (m)", startHeight, 2f, 120f);
            endHeight = EditorGUILayout.Slider("Height at end (m)", endHeight, 2f, 80f);
            lookAhead = EditorGUILayout.Slider("Look ahead (m)", lookAhead, 20f, 300f);
            behindTee = EditorGUILayout.Slider("Start behind tee (m)", behindTee, 0f, 100f);
            stopShort = EditorGUILayout.Slider("Stop short of pin (m)", stopShort, 0f, 120f);
            minClear = EditorGUILayout.Slider("Min height over ground (m)", minClear, 1f, 40f);
            if (useGameView) fov = EditorGUILayout.Slider("Field of view", fov, 20f, 90f);
        }
        if (EditorGUI.EndChangeCheck()) { BuildHeights(); if (!playing) ApplyAt(progress); SceneView.RepaintAll(); }

        EditorGUILayout.HelpBox(useGameView
            ? "Game view: open the Game tab to watch. A temporary camera is used and removed when this window closes; it is never saved or built."
            : "Scene view: click into the Scene tab once so it is the active view, then press Play flyover. Scrub or use the view buttons to stop anywhere.",
            MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    void OnSceneGUI(SceneView sv)
    {
        if (!showPath || playing || drawPath == null || drawPath.Length < 2) return;
        if (Event.current.type != EventType.Repaint) return;
        Handles.color = new Color(1f, 0.85f, 0.1f);
        Handles.DrawAAPolyLine(4f, drawPath);
        for (int i = 0; i < marks.Count; i++)
        {
            Vector3 m = marks[i];
            Handles.color = i == 0 ? Color.cyan : (i == marks.Count - 1 ? Color.red : Color.white);
            Handles.SphereHandleCap(0, m + Vector3.up, Quaternion.identity, 1.5f, EventType.Repaint);
            Handles.Label(m + Vector3.up * 5f, markNames[i]);
        }
    }
}
