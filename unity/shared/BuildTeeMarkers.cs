using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Places Tierra's TR shield tee markers (BoJa's 10/29 photos, holes 1 and 12) at every enabled tee in the GKD: a pair per tee,
// 1 m in front of the tee point and 2.5 m either side of the line to AimPoint1, face toward the golfer. Each marker is one
// thick shield plate (flat top with notched corners, chamfered bottom) on a short stake, face textures from TM_<tee>_face.png.
// Colours per tee: Black = black/white TR, Green (Grey on the card) = grey/black, Blue = blue/white, White = white/black,
// Yellow (Forward) = gold/black. Everything goes under TEE_MARKERS; re-running replaces it. No colliders, so play is unchanged.
public static class BuildTeeMarkers
{
    const string ROOT = "TEE_MARKERS", DIR = "Assets/TierraRejada/generated/tee_markers";
    static string Gkd { get { return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "/tierra-rejada/greenkeeper/TierraRejada.GKD"; } }

    // shield outline (shield_outline.txt), x -0.5..0.5 across, y 0..1 up; real markers are about 0.22 m wide x 0.24 m tall on a 0.07 m stake; built at 2x (BoJa: bigger so they read in the sim)
    static readonly Vector2[] SHIELD = { new Vector2(-0.3000f, 0.0000f), new Vector2(0.3000f, 0.0000f), new Vector2(0.5000f, 0.2400f), new Vector2(0.5000f, 0.8700f), new Vector2(0.5000f, 0.8700f), new Vector2(0.4689f, 0.8744f), new Vector2(0.4400f, 0.8874f), new Vector2(0.4151f, 0.9081f), new Vector2(0.3961f, 0.9350f), new Vector2(0.3841f, 0.9664f), new Vector2(0.3800f, 1.0000f), new Vector2(-0.3800f, 1.0000f), new Vector2(-0.3841f, 0.9664f), new Vector2(-0.3961f, 0.9350f), new Vector2(-0.4151f, 0.9081f), new Vector2(-0.4400f, 0.8874f), new Vector2(-0.4689f, 0.8744f), new Vector2(-0.5000f, 0.8700f), new Vector2(-0.5000f, 0.2400f) };
    const float SCALE = 2f, W = 0.22f * SCALE, H = 0.24f * SCALE, STAKE = 0.07f * SCALE, THICK = 0.03f * SCALE, LIFT = 0.02f;
    static string TexDir { get { return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "/tierra-rejada/course_edits/visit_2026-10-29/tee_markers"; } }

    [System.Serializable] class V3 { public float x, y, z; }
    [System.Serializable] class Tee { public string TeeType; public bool Enabled; public float Distance; public V3 Position; }
    [System.Serializable] class Hole { public bool Enabled; public int HoleNumber; public Tee[] Tees; }
    [System.Serializable] class Course { public Hole[] Holes; }

    [MenuItem("Course Tools/Build Tee Markers")]
    public static void Build()
    {
        var course = JsonUtility.FromJson<Course>(File.ReadAllText(Gkd));
        var old = GameObject.Find(ROOT); if (old != null) Undo.DestroyObjectImmediate(old);
        Directory.CreateDirectory(DIR);
        var root = new GameObject(ROOT); Undo.RegisterCreatedObjectUndo(root, "Build tee markers");

        var colours = new Dictionary<string, Color[]> {
            { "Black", new[] { new Color(0.05f, 0.05f, 0.05f), new Color(0.95f, 0.95f, 0.95f) } },
            { "Green", new[] { new Color(0.55f, 0.56f, 0.57f), new Color(0.05f, 0.05f, 0.05f) } },
            { "Blue", new[] { new Color(0.07f, 0.24f, 0.52f), new Color(0.95f, 0.95f, 0.95f) } },
            { "White", new[] { new Color(0.92f, 0.92f, 0.90f), new Color(0.05f, 0.05f, 0.05f) } },
            { "Yellow", new[] { new Color(0.83f, 0.66f, 0.18f), new Color(0.05f, 0.05f, 0.05f) } } };
        var faceMats = new Dictionary<string, Material>(); var backMats = new Dictionary<string, Material>();
        foreach (var kv in colours)
        {
            faceMats[kv.Key] = Make("TM_" + kv.Key + "_face", Color.white, 0.3f, FaceTex("TM_" + kv.Key + "_face.png"));
            backMats[kv.Key] = Make("TM_" + kv.Key + "_plate", kv.Value[1], 0.3f, null);   // rim, back and stake
        }

        // two meshes shared by every marker: the textured front face, and the rim + back + stake
        Mesh front, body; Plate(out front, out body); Save(front, "TM_front"); Save(body, "TM_body");

        int n = 0;
        foreach (var h in course.Holes)
        {
            if (!h.Enabled || h.Tees == null) continue;
            Vector3? aim = null; foreach (var t in h.Tees) if (t.TeeType == "AimPoint1" && t.Position != null && (t.Position.x != 0 || t.Position.z != 0)) aim = new Vector3(t.Position.x, 0, t.Position.z);
            if (aim == null) foreach (var t in h.Tees) if (t.TeeType == "GreenCenterPoint" && t.Position != null && (t.Position.x != 0 || t.Position.z != 0)) aim = new Vector3(t.Position.x, 0, t.Position.z);
            var hg = new GameObject("Hole" + h.HoleNumber.ToString("00")); hg.transform.SetParent(root.transform);
            foreach (var t in h.Tees)
            {
                if (!t.Enabled || t.Position == null || (t.Position.x == 0 && t.Position.z == 0) || !colours.ContainsKey(t.TeeType)) continue;
                var p = new Vector3(t.Position.x, 0, t.Position.z);
                var dir = aim.HasValue ? (aim.Value - p) : Vector3.forward; dir.y = 0; dir.Normalize();
                var right = Vector3.Cross(Vector3.up, dir);
                foreach (int side in new[] { -1, 1 })
                {
                    var q = p + dir * 1.0f + right * (2.5f * side);
                    q.y = Ground(q.x, q.z) - LIFT;
                    var g = new GameObject(t.TeeType + (side < 0 ? "_L" : "_R")); g.transform.SetParent(hg.transform);
                    // the monogram faces the golfer, so the plate's +z (front normal) points back down the line
                    g.transform.position = q; g.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                    Child(g, "Face", front, faceMats[t.TeeType]); Child(g, "Body", body, backMats[t.TeeType]);
                    n++;
                }
            }
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[TeeMarkers] placed " + n + " markers from " + Gkd);
    }

    static void Child(GameObject parent, string name, Mesh mesh, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(parent.transform, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    // front face at z = 0 (+z normal, UVs over the outline's box), back face at z = -THICK, edge strip, and a stake under the bottom
    static void Plate(out Mesh front, out Mesh body)
    {
        int k = SHIELD.Length; var o = new Vector3[k];
        for (int i = 0; i < k; i++) o[i] = new Vector3(SHIELD[i].x * W, STAKE + SHIELD[i].y * H, 0);
        var cen = new Vector3(0, STAKE + 0.5f * H, 0);
        var fv = new List<Vector3> { cen }; var fuv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var ft = new List<int>();
        for (int i = 0; i < k; i++) { fv.Add(o[i]); fuv.Add(new Vector2(SHIELD[i].x + 0.5f, SHIELD[i].y)); }
        for (int i = 0; i < k; i++) Tri(fv, ft, 0, 1 + i, 1 + (i + 1) % k, Vector3.forward);
        front = Finish(fv, fuv, ft);

        var bv = new List<Vector3>(); var bt = new List<int>();
        var back = new Vector3(0, 0, -THICK); bv.Add(cen + back); for (int i = 0; i < k; i++) bv.Add(o[i] + back);
        for (int i = 0; i < k; i++) Tri(bv, bt, 0, 1 + i, 1 + (i + 1) % k, Vector3.back);
        for (int i = 0; i < k; i++) Box(bv, bt, o[i], o[(i + 1) % k], back, cen);
        // stake: a 2 x 1.5 cm post from the ground up into the plate
        float sx = 0.01f * SCALE, sz = 0.0075f * SCALE, zc = -THICK / 2;
        var c = new[] { new Vector3(-sx, -0.02f, zc - sz), new Vector3(sx, -0.02f, zc - sz), new Vector3(sx, -0.02f, zc + sz), new Vector3(-sx, -0.02f, zc + sz) };
        var up = new Vector3(0, STAKE + 0.03f, 0); var sc = new Vector3(0, STAKE / 2, zc);
        for (int i = 0; i < 4; i++) Quad(bv, bt, c[i], c[(i + 1) % 4], c[(i + 1) % 4] + up, c[i] + up, sc);
        body = Finish(bv, null, bt);
    }
    static void Box(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 back, Vector3 cen) { Quad(v, t, a, b, b + back, a + back, cen + back / 2); }
    static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
    {
        var outward = (a + b + c + d) / 4 - inside; int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        Tri(v, t, i, i + 1, i + 2, outward); Tri(v, t, i, i + 2, i + 3, outward);
    }
    // adds triangle (a, b, c) wound so its face normal points along 'want' (Unity front faces are clockwise from the viewer)
    static void Tri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 want)
    {
        var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, want) >= 0) t.AddRange(new[] { a, b, c }); else t.AddRange(new[] { a, c, b });
    }
    static Mesh Finish(List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        // flat-shaded: split every triangle so the edge strip and caps get their own normals
        var nv = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<int>();
        for (int i = 0; i < t.Count; i++) { nv.Add(v[t[i]]); nuv.Add(uv != null ? uv[t[i]] : Vector2.zero); nt.Add(i); }
        var m = new Mesh(); m.SetVertices(nv); m.SetUVs(0, nuv); m.SetTriangles(nt, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static Mesh Save(Mesh mesh, string name)
    {
        mesh.name = name; string path = DIR + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    // top of the course surface, ignoring trees, shrubs and the markers themselves
    static float Ground(float x, float z)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 1000, z), Vector3.down, 2000))
        {
            var r = h.collider.transform.root.name; if (r == ROOT || r == "LIDAR_TREES" || r == "SHRUBS" || r == "NETS" || r == "FENCES" || r == "CLUBHOUSE") continue;
            var pm = h.collider.sharedMaterial; if (pm != null && pm.name.StartsWith("GSPleaves")) continue;
            best = Mathf.Max(best, h.point.y);
        }
        if (float.IsNegativeInfinity(best)) foreach (var t in Terrain.activeTerrains) best = Mathf.Max(best, t.SampleHeight(new Vector3(x, 0, z)) + t.GetPosition().y);
        return best;
    }

    static Material Make(string n, Color c, float gloss, Texture2D tex)
    {
        string p = DIR + "/" + n + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.mainTexture = tex; EditorUtility.SetDirty(m); return m;
    }

    // copies TM_<tee>_face.png (made from BoJa's marker photos) into the project and returns it
    static Texture2D FaceTex(string file)
    {
        string p = DIR + "/" + file; File.Copy(TexDir + "/" + file, p, true); AssetDatabase.ImportAsset(p);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }
}
