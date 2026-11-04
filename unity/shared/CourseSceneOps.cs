// CourseSceneOps: small, logged scene operations through the bridge (course-build-unity-tools). No course names, no constants.
//   Find          -Set @{ name = "<substring>" }   lists active and inactive objects (any root) whose name contains it
//   FindPlants    -Set @{ px = "1688"; pz = "815"; pr = "15" }  LODGroup plants within pr m of a point: name, root, height, crown: active, position, collider
//                                                  vs render dy (MeshCollider raycast at 1/50 of the vertices), physic material
//   SetOff/SetOn  -Set @{ name = "<exact name>" }  SetActive(false/true) on that object; logged "active|<name>|<was>" in COURSEBUILD_LOG
//   RebakeCollider -Set @{ name = "<exact name>" } MeshCollider.sharedMesh re-pointed at the MeshFilter's mesh (dy back to 0 when the
//                                                  render mesh was swapped under a stale collider); reports dy before and after
//   UndoActive                                     every "active|" log entry put back, newest first
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CourseSceneOps : EditorWindow
{
    const string LOG = "COURSEBUILD_LOG"; static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public static string name = ""; public static float px = 0, pz = 0, pr = 15f;   // FindPlants: LODGroup plants within pr m of (px, pz), any root

    // every transform in the scene (all roots), so plants, props and course meshes can all be named, switched and listed
    static IEnumerable<Transform> All() { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)); }

    // plants near a point: every LODGroup within pr m of (px, pz), with its root, height, crown radius (renderer bounds) and state
    public static string FindPlants()
    {
        var c = new Vector2(px, pz); var o = new List<string>();
        foreach (var lg in UnityEngine.Object.FindObjectsOfType<LODGroup>().Concat(All().Select(t => t.GetComponent<LODGroup>()).Where(l => l)).Distinct())
        {
            var p = lg.transform.position; float d = (new Vector2(p.x, p.z) - c).magnitude; if (d > pr) continue;
            var rs = lg.GetComponentsInChildren<Renderer>(true); Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(p, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds);
            o.Add(lg.name + "  root " + lg.transform.root.name + (lg.gameObject.activeInHierarchy ? "  active" : "  OFF") + "  at (" + p.x.ToString("F1", CI) + ", " + p.z.ToString("F1", CI) + ")  " + d.ToString("F1", CI) + " m away, height " + b.size.y.ToString("F1", CI) + " m, crown r " + (Mathf.Max(b.extents.x, b.extents.z)).ToString("F1", CI) + " m");
        }
        return o.Count + " plants within " + pr.ToString(CI) + " m of (" + px.ToString("F1", CI) + ", " + pz.ToString("F1", CI) + ")\n" + string.Join("\n", o.OrderBy(x => x).ToArray());
    }

    static float ColliderDy(MeshFilter f)
    {
        var mc = f ? f.GetComponent<MeshCollider>() : null; if (!f || !mc || !f.sharedMesh || !mc.sharedMesh) return float.NaN;
        var ra = f.sharedMesh.vertices; float worst = 0; int step = Mathf.Max(1, ra.Length / 50);
        for (int i = 0; i < ra.Length; i += step) { var wp = f.transform.TransformPoint(ra[i]); RaycastHit h; if (mc.Raycast(new Ray(wp + Vector3.up * 5, Vector3.down), out h, 20)) worst = Mathf.Max(worst, Mathf.Abs(h.point.y - wp.y)); }
        return worst;
    }

    public static string Find()
    {
        if (string.IsNullOrEmpty(name)) return "set name = <substring>"; var o = new List<string>(); string n = name.ToLower();
        foreach (var t in All().Where(t => t.name.ToLower().Contains(n)).OrderBy(t => t.name))
        {
            var f = t.GetComponent<MeshFilter>(); var c = t.GetComponent<Collider>(); float dy = ColliderDy(f);
            o.Add(t.name + (t.gameObject.activeInHierarchy ? "  active" : "  OFF") + "  at (" + t.position.x.ToString("F1", CI) + ", " + t.position.z.ToString("F1", CI) + ")" + (f && f.sharedMesh ? "  mesh " + f.sharedMesh.name + " (" + f.sharedMesh.vertexCount + " v)" : "  no mesh") + (c ? "  collider " + (c.sharedMaterial ? c.sharedMaterial.name : "no material") + (float.IsNaN(dy) ? "" : " dy " + (dy * 100).ToString("F1", CI) + " cm") : "  no collider"));
        }
        return o.Count + " objects\n" + string.Join("\n", o.ToArray());
    }

    static string SetActive(bool on)
    {
        var t = All().FirstOrDefault(x => x.name == name); if (!t) return "no object named " + name;
        bool was = t.gameObject.activeSelf; if (was == on) return name + " already " + (on ? "active" : "off");
        var log = GameObject.Find(LOG) ?? new GameObject(LOG); new GameObject("active|" + name + "|" + (was ? "1" : "0")).transform.SetParent(log.transform, false);
        Undo.RecordObject(t.gameObject, "scene ops"); t.gameObject.SetActive(on); EditorUtility.SetDirty(t.gameObject); Physics.SyncTransforms();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene); return name + (on ? " active" : " OFF") + " (was " + (was ? "active" : "off") + "; UndoActive puts it back)";
    }
    public static string SetOff() { return SetActive(false); }
    public static string SetOn() { return SetActive(true); }

    public static string RebakeCollider()
    {
        var t = All().FirstOrDefault(x => x.name == name); if (!t) return "no OPCD object named " + name;
        var f = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>(); if (!f || !f.sharedMesh) return name + ": no mesh"; if (!mc) return name + ": no MeshCollider";
        float before = ColliderDy(f); string was = mc.sharedMesh ? mc.sharedMesh.name : "none";
        Undo.RecordObject(mc, "rebake collider"); mc.sharedMesh = null; mc.sharedMesh = f.sharedMesh; EditorUtility.SetDirty(mc); Physics.SyncTransforms();
        float after = ColliderDy(f); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        return name + ": collider was " + was + ", now " + f.sharedMesh.name + "; dy " + (before * 100).ToString("F1", CI) + " cm -> " + (after * 100).ToString("F1", CI) + " cm";
    }

    public static string UndoActive()
    {
        var log = GameObject.Find(LOG); if (!log) return "no " + LOG; int n = 0; var o = new List<string>();
        foreach (var e in log.transform.Cast<Transform>().Reverse().ToList())
        {
            var p = e.name.Split('|'); if (p.Length < 3 || p[0] != "active") continue; var t = All().FirstOrDefault(x => x.name == p[1]); if (!t) { o.Add(p[1] + " not found"); continue; }
            t.gameObject.SetActive(p[2] == "1"); EditorUtility.SetDirty(t.gameObject); UnityEngine.Object.DestroyImmediate(e.gameObject); n++;
        }
        Physics.SyncTransforms(); return n + " objects put back" + (o.Count > 0 ? "\n" + string.Join("\n", o.ToArray()) : "");
    }

    void OnGUI() { name = EditorGUILayout.TextField("Name", name); if (GUILayout.Button("Find")) Debug.Log(Find()); if (GUILayout.Button("Set off")) Debug.Log(SetOff()); if (GUILayout.Button("Set on")) Debug.Log(SetOn()); if (GUILayout.Button("Rebake collider")) Debug.Log(RebakeCollider()); if (GUILayout.Button("Undo active changes")) Debug.Log(UndoActive()); }
    [MenuItem("Course Tools/Scene ops")] static void Open() { GetWindow<CourseSceneOps>("Scene ops"); }
}
