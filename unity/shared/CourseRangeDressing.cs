// Course Tools > Dress Driving Range
// Makes a driving range look used: a concrete hitting strip with turf mats along the hitting line,
// bag stands, ball buckets and benches, a few golfers, yellow range balls scattered down the range,
// and yardage flags. Works for any course: give it two CSVs (header x,z, Unity metres):
//   hitting line  - points along the mat line, in order
//   range outline - the range boundary, used to keep balls and flags on the range
// Everything is visual only (no colliders) and goes under RANGE_DRESSING; re-running replaces it.
// Unity 2018.2. Put this file in Assets/Editor.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CourseRangeDressing : EditorWindow
{
    const string ROOT = "RANGE_DRESSING";
    const string PREF = "CourseRange.";   // + scene name + field

    string lineCsv = "", outlineCsv = "";
    float baySpacing = 3f;       // metres between hitting spots
    float stripDepth = 4f;       // concrete strip, front to back
    int golferPercent = 35;      // share of bays with someone hitting
    int ballCount = 3000;
    float ballScale = 1.5f;      // 1 = real 42.7 mm ball; a bit bigger reads better from the tee
    float ballNear = 30f, ballFar = 230f;   // metres from the line
    bool flags = true;
    int seed = 7;

    string sceneName;
    System.Random rng;

    [MenuItem("Course Tools/Dress Driving Range")]
    static void Open() { GetWindow<CourseRangeDressing>("Driving Range"); }

    void OnEnable() { LoadPrefs(); }

    void LoadPrefs()
    {
        sceneName = EditorSceneManager.GetActiveScene().name;
        string repo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "tierra-rejada/range");
        lineCsv = EditorPrefs.GetString(PREF + sceneName + ".line", Path.Combine(repo, "range_tee_line.csv"));
        outlineCsv = EditorPrefs.GetString(PREF + sceneName + ".outline", Path.Combine(repo, "range_outline.csv"));
    }

    void OnGUI()
    {
        if (EditorSceneManager.GetActiveScene().name != sceneName) LoadPrefs();
        EditorGUILayout.LabelField("Scene '" + sceneName + "'");
        lineCsv = FileField("Hitting line CSV (x,z)", lineCsv);
        outlineCsv = FileField("Range outline CSV (x,z)", outlineCsv);
        baySpacing = EditorGUILayout.Slider("Hitting spot spacing (m)", baySpacing, 2f, 6f);
        stripDepth = EditorGUILayout.Slider("Hitting strip depth (m)", stripDepth, 2f, 8f);
        golferPercent = EditorGUILayout.IntSlider("Spots with a golfer (%)", golferPercent, 0, 100);
        ballCount = EditorGUILayout.IntSlider("Range balls", ballCount, 0, 20000);
        ballScale = EditorGUILayout.Slider("Ball size (x real)", ballScale, 1f, 4f);
        ballNear = EditorGUILayout.Slider("Balls from (m)", ballNear, 5f, 150f);
        ballFar = EditorGUILayout.Slider("Balls to (m)", ballFar, ballNear + 10f, 350f);
        flags = EditorGUILayout.Toggle("Yardage flags", flags);
        seed = EditorGUILayout.IntField("Random seed", seed);
        EditorGUILayout.HelpBox("Golfers face the middle of the range, right-handed. Balls fall thickest 100-180 m out and stay " +
            "inside the outline. Nothing has a collider, so none of it affects play. Re-running replaces " + ROOT + ".", MessageType.Info);
        if (GUILayout.Button("Build range", GUILayout.Height(28))) Build();
        if (GUILayout.Button("Remove " + ROOT)) Clear();
        if (GUILayout.Button("Show in Scene view")) Frame();
    }

    string FileField(string label, string path)
    {
        EditorGUILayout.LabelField(label);
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(path);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string f = EditorUtility.OpenFilePanel(label, Path.GetDirectoryName(path), "csv");
            if (!string.IsNullOrEmpty(f)) path = f;
        }
        EditorGUILayout.EndHorizontal();
        return path;
    }

    void Clear()
    {
        GameObject old = GameObject.Find(ROOT);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    void Frame()
    {
        GameObject root = GameObject.Find(ROOT);
        if (root == null || SceneView.lastActiveSceneView == null) return;
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        SceneView.lastActiveSceneView.Frame(b, false);
    }

    // ---------------------------------------------------------------- data

    static List<Vector2> ReadXZ(string path)
    {
        List<Vector2> pts = new List<Vector2>();
        string[] lines = File.ReadAllLines(path);
        int xi = 0, zi = 1;
        string[] head = lines.Length > 0 ? lines[0].Split(',') : new string[0];
        for (int i = 0; i < head.Length; i++)
        {
            string h = head[i].Trim().ToLowerInvariant();
            if (h == "x") xi = i;
            if (h == "z") zi = i;
        }
        for (int i = 1; i < lines.Length; i++)
        {
            string[] c = lines[i].Split(',');
            float x, z;
            if (c.Length > Math.Max(xi, zi) &&
                float.TryParse(c[xi], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                float.TryParse(c[zi], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                pts.Add(new Vector2(x, z));
        }
        return pts;
    }

    static bool Inside(List<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    // top of the playing surface: terrain, or a course mesh collider just above it (not tree canopies)
    static float Ground(float x, float z)
    {
        float ter = float.NegativeInfinity;
        Vector3 p = new Vector3(x, 0, z);
        foreach (Terrain t in Terrain.activeTerrains)
        {
            if (t == null || t.terrainData == null) continue;
            Vector3 o = t.GetPosition();
            Vector3 size = t.terrainData.size;
            if (x < o.x || z < o.z || x > o.x + size.x || z > o.z + size.z) continue;
            ter = Mathf.Max(ter, t.SampleHeight(p) + o.y);
        }
        if (float.IsNegativeInfinity(ter)) ter = 0f;
        float best = ter;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, ter + 50f, z), Vector3.down, 100f))
            if (hit.point.y > best && hit.point.y < ter + 1.5f) best = hit.point.y;
        return best;
    }

    float R() { return (float)rng.NextDouble(); }

    // ---------------------------------------------------------------- build

    void Build()
    {
        if (!File.Exists(lineCsv) || !File.Exists(outlineCsv))
        {
            EditorUtility.DisplayDialog("Driving Range", "Pick both CSV files first.", "OK");
            return;
        }
        List<Vector2> line = ReadXZ(lineCsv), outline = ReadXZ(outlineCsv);
        if (line.Count < 2 || outline.Count < 3)
        {
            EditorUtility.DisplayDialog("Driving Range", "The hitting line needs 2+ points and the outline 3+.", "OK");
            return;
        }
        EditorPrefs.SetString(PREF + sceneName + ".line", lineCsv);
        EditorPrefs.SetString(PREF + sceneName + ".outline", outlineCsv);
        rng = new System.Random(seed);

        // aim point: middle of the range outline
        Vector2 aim = Vector2.zero;
        foreach (Vector2 v in outline) aim += v;
        aim /= outline.Count;

        string dir = "Assets/CourseTools/generated/" + sceneName + "/range";
        Directory.CreateDirectory(dir);
        Clear();
        GameObject root = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(root, "Dress driving range");

        Materials m = new Materials(dir);
        Batch batch = new Batch(root.transform, dir);
        Mesh cube = PrimitiveMesh(PrimitiveType.Cube), cyl = PrimitiveMesh(PrimitiveType.Cylinder),
             cap = PrimitiveMesh(PrimitiveType.Capsule), sph = PrimitiveMesh(PrimitiveType.Sphere);
        Mesh ball = LowPolyBall();

        try
        {
            EditorUtility.DisplayProgressBar("Driving Range", "Hitting strip", 0.1f);
            // walk the line, one hitting spot every baySpacing metres
            List<Vector2> spots = new List<Vector2>();
            float carry = baySpacing * 0.5f;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                float len = Vector2.Distance(a, b);
                for (float s = carry; s < len; s += baySpacing) spots.Add(Vector2.Lerp(a, b, s / len));
                carry = baySpacing - ((len - carry) % baySpacing);
                if (carry >= baySpacing) carry -= baySpacing;
            }
            batch.Add(StripMesh(line, aim), Matrix4x4.identity, m.concrete);

            List<Vector3> starts = new List<Vector3>();
            for (int i = 0; i < spots.Count; i++)
            {
                Vector2 s2 = spots[i];
                Vector2 t2 = (aim - s2).normalized;
                Vector3 fwd = new Vector3(t2.x, 0, t2.y);            // toward the range
                Vector3 side = Vector3.Cross(Vector3.up, fwd);         // golfer faces this way (target on their left)
                Quaternion face = Quaternion.LookRotation(fwd);
                Vector3 c = new Vector3(s2.x, Ground(s2.x, s2.y), s2.y);
                starts.Add(c);

                // mat (front half of the strip), ball bucket, bag stand, bench every second spot
                Vector3 mat = c + fwd * (stripDepth * 0.15f) + Vector3.up * 0.06f;
                batch.Add(cube, Matrix4x4.TRS(mat, face, new Vector3(1.5f, 0.03f, 1.5f)), m.mat);
                batch.Add(cyl, Matrix4x4.TRS(mat + side * 0.9f + Vector3.up * 0.12f, Quaternion.identity, new Vector3(0.3f, 0.12f, 0.3f)), m.bucket);
                for (int k = 0; k < 6; k++)
                    batch.Add(ball, Matrix4x4.TRS(mat + side * 0.9f + new Vector3(R() * 0.16f - 0.08f, 0.26f, R() * 0.16f - 0.08f),
                        Quaternion.identity, Vector3.one * 0.0427f), m.ball);
                Vector3 back = c - fwd * (stripDepth * 0.3f);
                Quaternion lean = face * Quaternion.Euler(-12f, 0, 0);
                batch.Add(cyl, Matrix4x4.TRS(back - side * 0.6f + Vector3.up * 0.45f, lean, new Vector3(0.28f, 0.45f, 0.28f)), m.dark);
                if (i % 2 == 0)
                {
                    Vector3 bench = back + side * 0.9f;
                    batch.Add(cube, Matrix4x4.TRS(bench + Vector3.up * 0.45f, face, new Vector3(1.2f, 0.06f, 0.4f)), m.dark);
                    batch.Add(cube, Matrix4x4.TRS(bench + Vector3.up * 0.22f + side * 0.5f, face, new Vector3(0.06f, 0.44f, 0.4f)), m.dark);
                    batch.Add(cube, Matrix4x4.TRS(bench + Vector3.up * 0.22f - side * 0.5f, face, new Vector3(0.06f, 0.44f, 0.4f)), m.dark);
                }
                if (R() * 100f < golferPercent)
                {
                    if (R() < 0.8f) Golfer(batch, m, cube, cyl, cap, sph, mat, fwd, side, true);
                    else Golfer(batch, m, cube, cyl, cap, sph, back + side * 0.3f, fwd, side, false);
                }
            }

            EditorUtility.DisplayProgressBar("Driving Range", "Range balls", 0.5f);
            Vector3 mid = Vector3.zero;
            foreach (Vector3 s in starts) mid += s;
            mid /= starts.Count;
            int placed = 0, tries = 0;
            while (placed < ballCount && tries < ballCount * 20)
            {
                tries++;
                Vector3 from = starts[rng.Next(starts.Count)];
                Vector2 to = (aim - new Vector2(from.x, from.z)).normalized;
                // distance: mostly 100-180 m, thinning toward both ends
                float d = Mathf.Lerp(ballNear, ballFar, (R() + R() + R()) / 3f);
                float ang = (R() - 0.5f) * 30f * Mathf.Deg2Rad;
                Vector2 dir2 = new Vector2(to.x * Mathf.Cos(ang) - to.y * Mathf.Sin(ang), to.x * Mathf.Sin(ang) + to.y * Mathf.Cos(ang));
                Vector2 p = new Vector2(from.x, from.z) + dir2 * d;
                if (!Inside(outline, p)) continue;
                float r = 0.02135f * ballScale;
                batch.Add(ball, Matrix4x4.TRS(new Vector3(p.x, Ground(p.x, p.y) + r * 0.8f, p.y), Quaternion.Euler(0, R() * 360f, 0), Vector3.one * r * 2f), m.ball);
                placed++;
            }

            if (flags)
            {
                EditorUtility.DisplayProgressBar("Driving Range", "Yardage flags", 0.8f);
                Vector2 m2 = new Vector2(mid.x, mid.z);
                Vector2 to = (aim - m2).normalized;
                Material[] colours = { m.flagRed, m.flagWhite, m.flagBlue, m.flagYellow, m.flagRed };
                for (int y = 1; y <= 5; y++)
                {
                    Vector2 p = m2 + to * (y * 50f / 1.0936133f);
                    if (!Inside(outline, p)) continue;
                    Vector3 g = new Vector3(p.x, Ground(p.x, p.y), p.y);
                    batch.Add(cyl, Matrix4x4.TRS(g + Vector3.up * 1.25f, Quaternion.identity, new Vector3(0.04f, 1.25f, 0.04f)), m.pole);
                    Vector3 side = Vector3.Cross(Vector3.up, new Vector3(to.x, 0, to.y));
                    batch.Add(cube, Matrix4x4.TRS(g + Vector3.up * 2.25f + side * 0.3f, Quaternion.LookRotation(new Vector3(to.x, 0, to.y)), new Vector3(0.6f, 0.4f, 0.02f)), colours[y - 1]);
                }
            }

            EditorUtility.DisplayProgressBar("Driving Range", "Saving meshes", 0.9f);
            batch.Flush();
            Debug.Log("Driving range: " + spots.Count + " hitting spots, " + placed + " balls.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Frame();
    }

    // simple golfer from primitives: at address over the ball, or standing behind the mat watching
    static void Golfer(Batch b, Materials m, Mesh cube, Mesh cyl, Mesh cap, Mesh sph, Vector3 ball, Vector3 fwd, Vector3 side, bool hitting)
    {
        System.Random r = new System.Random(ball.GetHashCode());
        Material shirt = m.shirts[r.Next(m.shirts.Length)];
        Material pants = m.pants[r.Next(m.pants.Length)];
        Vector3 facing = hitting ? side : fwd;
        Vector3 feet = hitting ? ball - side * 0.75f : ball;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        Quaternion q = Quaternion.LookRotation(facing);
        float tilt = hitting ? 30f : 0f;
        Quaternion torsoRot = q * Quaternion.Euler(tilt, 0, 0);

        // legs, hips, torso, head, cap
        for (int s = -1; s <= 1; s += 2)
            b.Add(cap, Matrix4x4.TRS(feet + right * (0.13f * s) + Vector3.up * 0.45f, q, new Vector3(0.15f, 0.45f, 0.15f)), pants);
        Vector3 hip = feet + Vector3.up * 0.9f;
        Vector3 chest = hip + torsoRot * Vector3.up * 0.3f;
        b.Add(cap, Matrix4x4.TRS(chest, torsoRot, new Vector3(0.36f, 0.33f, 0.24f)), shirt);
        Vector3 neck = hip + torsoRot * Vector3.up * 0.6f;
        b.Add(sph, Matrix4x4.TRS(neck + torsoRot * Vector3.up * 0.13f, q, Vector3.one * 0.22f), m.skin);
        b.Add(cube, Matrix4x4.TRS(neck + torsoRot * Vector3.up * 0.23f + facing * 0.05f, torsoRot, new Vector3(0.22f, 0.06f, 0.26f)), m.dark);

        if (hitting)
        {
            // arms hang to the hands, club down to the ball
            Vector3 hands = feet + facing * 0.45f + Vector3.up * 0.7f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 shoulder = neck - torsoRot * Vector3.up * 0.06f + right * (0.19f * s);
                Vector3 mid = (shoulder + hands) * 0.5f;
                b.Add(cap, Matrix4x4.TRS(mid, Quaternion.FromToRotation(Vector3.up, shoulder - hands), new Vector3(0.09f, Vector3.Distance(shoulder, hands) * 0.5f, 0.09f)), shirt);
            }
            Vector3 head = ball - facing * 0.05f + Vector3.up * 0.02f;
            b.Add(cyl, Matrix4x4.TRS((hands + head) * 0.5f, Quaternion.FromToRotation(Vector3.up, hands - head), new Vector3(0.02f, Vector3.Distance(hands, head) * 0.5f, 0.02f)), m.pole);
        }
        else
        {
            for (int s = -1; s <= 1; s += 2)
                b.Add(cap, Matrix4x4.TRS(chest + right * (0.24f * s) - Vector3.up * 0.15f, q, new Vector3(0.09f, 0.3f, 0.09f)), shirt);
        }
    }

    // concrete ribbon along the hitting line, draped on the ground
    Mesh StripMesh(List<Vector2> line, Vector2 aim)
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        List<Vector2> uv = new List<Vector2>();
        float along = 0f;
        for (int i = 0; i < line.Count - 1; i++)
        {
            float len = Vector2.Distance(line[i], line[i + 1]);
            int n = Mathf.Max(1, Mathf.CeilToInt(len));
            for (int k = (i == 0 ? 0 : 1); k <= n; k++)
            {
                Vector2 p = Vector2.Lerp(line[i], line[i + 1], k / (float)n);
                Vector2 f = (aim - p).normalized;
                Vector2 front = p + f * (stripDepth * 0.5f), back = p - f * (stripDepth * 0.5f);
                float a = along + len * k / n;
                v.Add(new Vector3(back.x, Ground(back.x, back.y) + 0.04f, back.y));
                v.Add(new Vector3(front.x, Ground(front.x, front.y) + 0.04f, front.y));
                uv.Add(new Vector2(a / 2f, 0)); uv.Add(new Vector2(a / 2f, stripDepth / 2f));
            }
            along += len;
        }
        for (int i = 0; i + 3 < v.Count; i += 2)
        {
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i + 1); t.Add(i + 3); t.Add(i + 2);
        }
        Mesh mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        // make sure it faces up whichever way the line runs
        if (mesh.normals.Length > 0 && mesh.normals[0].y < 0)
        {
            t.Reverse();
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Mesh mesh = g.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(g);
        return mesh;
    }

    // 20-triangle ball (unit diameter), cheap enough for thousands
    static Mesh LowPolyBall()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v = {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.5f;
        int[] tri = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                      3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        Mesh mesh = new Mesh();
        mesh.vertices = v;
        mesh.triangles = tri;
        mesh.RecalculateNormals();
        return mesh;
    }

    // ---------------------------------------------------------------- materials and batching

    class Materials
    {
        public Material concrete, mat, bucket, dark, ball, skin, pole, flagRed, flagWhite, flagBlue, flagYellow;
        public Material[] shirts, pants;
        readonly string dir;

        public Materials(string dir)
        {
            this.dir = dir;
            // cart path look: CL_CartPath is 0.62 grey x its concrete texture (mean 0.578, 0.554, 0.512), gloss 0.05; this plain
            // strip takes that product so it reads the same in sun (a flat 0.62 rendered near-white next to the paths)
            concrete = Make("Range_Concrete", new Color(0.358f, 0.344f, 0.317f), 0.05f);
            mat = Make("Range_Mat", new Color(0.16f, 0.36f, 0.14f), 0.05f);
            bucket = Make("Range_Bucket", new Color(0.12f, 0.25f, 0.12f), 0.3f);
            dark = Make("Range_Dark", new Color(0.08f, 0.08f, 0.09f), 0.2f);
            ball = Make("Range_Ball", new Color(1f, 0.86f, 0.1f), 0.4f);
            skin = Make("Range_Skin", new Color(0.72f, 0.53f, 0.4f), 0.2f);
            pole = Make("Range_Pole", new Color(0.85f, 0.85f, 0.85f), 0.4f);
            flagRed = Make("Range_FlagRed", new Color(0.75f, 0.1f, 0.1f), 0.1f);
            flagWhite = Make("Range_FlagWhite", new Color(0.9f, 0.9f, 0.9f), 0.1f);
            flagBlue = Make("Range_FlagBlue", new Color(0.1f, 0.25f, 0.7f), 0.1f);
            flagYellow = Make("Range_FlagYellow", new Color(0.95f, 0.8f, 0.1f), 0.1f);
            shirts = new[] {
                Make("Range_Shirt1", new Color(0.85f, 0.85f, 0.85f), 0.1f), Make("Range_Shirt2", new Color(0.15f, 0.25f, 0.5f), 0.1f),
                Make("Range_Shirt3", new Color(0.55f, 0.12f, 0.12f), 0.1f), Make("Range_Shirt4", new Color(0.2f, 0.2f, 0.22f), 0.1f) };
            pants = new[] {
                Make("Range_Pants1", new Color(0.12f, 0.12f, 0.14f), 0.1f), Make("Range_Pants2", new Color(0.55f, 0.5f, 0.4f), 0.1f),
                Make("Range_Pants3", new Color(0.25f, 0.27f, 0.3f), 0.1f) };
        }

        Material Make(string name, Color c, float gloss)
        {
            string path = dir + "/" + name + ".mat";
            Material mt = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mt == null)
            {
                mt = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mt, path);
            }
            mt.color = c;
            mt.SetFloat("_Glossiness", gloss);
            EditorUtility.SetDirty(mt);
            return mt;
        }
    }

    // combines everything per material into meshes of up to ~60k vertices, saved as assets
    class Batch
    {
        readonly Transform root;
        readonly string dir;
        readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();
        readonly Dictionary<Material, int> verts = new Dictionary<Material, int>();
        int saved;

        public Batch(Transform root, string dir)
        {
            this.root = root;
            this.dir = dir;
            foreach (string f in Directory.GetFiles(dir, "RangeMesh_*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        }

        public void Add(Mesh mesh, Matrix4x4 m, Material mat)
        {
            if (!parts.ContainsKey(mat)) { parts[mat] = new List<CombineInstance>(); verts[mat] = 0; }
            if (verts[mat] + mesh.vertexCount > 60000) FlushOne(mat);
            CombineInstance ci = new CombineInstance();
            ci.mesh = mesh;
            ci.transform = m;
            parts[mat].Add(ci);
            verts[mat] += mesh.vertexCount;
        }

        void FlushOne(Material mat)
        {
            List<CombineInstance> list = parts[mat];
            if (list.Count == 0) return;
            Mesh mesh = new Mesh();
            mesh.name = "RangeMesh_" + saved;
            mesh.CombineMeshes(list.ToArray(), true, true);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, dir + "/RangeMesh_" + saved + ".asset");
            saved++;
            GameObject g = new GameObject(mat.name.Replace("Range_", "") + "_" + saved);
            g.transform.SetParent(root, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            list.Clear();
            verts[mat] = 0;
        }

        public void Flush()
        {
            foreach (Material mat in new List<Material>(parts.Keys)) FlushOne(mat);
        }
    }
}
