using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Course Checks > Physics: audit / apply
// GSPro's ball physics come from the physic material on each collider (GSPgreen, GSPfairway, GSPrough, GSPdeeprough, TVGsand,
// GSPconcrete, GSPwater, GSPtimber, GSPleaves, GSPstone, GSPwood). A mesh with the wrong one, or none, plays wrong even when it looks right.
// This tool: (1) names every collider's class from its name using rules/physics.json name_rules; (2) compares with the physic material it
// carries; (3) writes AutomationOutput/checks/physics_audit.csv with one row per collider: current, expected, action (ok / fix / unsure /
// missing); (4) with Apply ticked, sets the expected material where the name rule matched and the material asset exists, never on tree
// LODGroups and never on unmatched names; (5) optional census: a grid over the scene, top collider's class at each point, written to
// physics_census.csv so tools/physics_check.py can compare it to the SVG ground truth headless; (6) physics_unsure.csv: the colliders with
// no rule match or no material asset found, with world position, for a human to label in the Inspector.
// Inputs: Assets/CourseBuild/rules/physics.json (synced by Run-CourseAutomation.ps1 -SyncTools). Materials found by name anywhere in Assets.
// NOT YET COMPILED ON THE PC (written in the cloud 2026-11-03).
public class CoursePhysicsAudit : EditorWindow
{
    [Serializable] public class NameRule { public string match; public string @class; }
    [Serializable] public class Census { public float grid_m = 2f; }
    [Serializable] public class Rules { public NameRule[] name_rules = new NameRule[0]; public Census census = new Census(); }
    const string CB = "Assets/CourseBuild";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    // class -> physic material name (mirrors rules/physics.json "materials"; JsonUtility cannot read a dictionary, so keep this list in step)
    static readonly string[,] MAT = { { "green", "GSPgreen" }, { "collar", "GSPfairway" }, { "fairway", "GSPfairway" }, { "tee", "GSPtee" }, { "rough", "GSPrough" }, { "deep_rough", "GSPdeeprough" },
                                      { "sand", "GSPsand" }, { "concrete", "GSPconcrete" }, { "water", "GSPwater" }, { "timber", "GSPtimber" }, { "leaves", "GSPleaves" }, { "stone", "GSPstone" }, { "wood", "GSPwood" } };
    public bool apply = false, census = true, includeOverlays = true;
    string last = ""; Vector2 scroll;

    [MenuItem("Course Checks/Physics: audit / apply")]
    static void Open() { GetWindow<CoursePhysicsAudit>("Physics audit"); }

    void OnGUI()
    {
        GUILayout.Label("Physic materials vs mesh names (rules/physics.json). Dry run unless Apply is ticked.", EditorStyles.boldLabel);
        apply = EditorGUILayout.Toggle("Apply fixes (name-matched only)", apply);
        census = EditorGUILayout.Toggle("Write 2 m census grid", census);
        includeOverlays = EditorGUILayout.Toggle("Include non-OPCD roots (paths, water, props)", includeOverlays);
        if (GUILayout.Button("Run")) last = Run(apply, census, includeOverlays);
        scroll = EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(last); EditorGUILayout.EndScrollView();
    }

    public static string Audit() { return Run(false, true, true); }     // bridge: invoke -Tool CoursePhysicsAudit -Method Audit
    public static string ApplyFixes() { return Run(true, false, true); } // bridge: invoke -Tool CoursePhysicsAudit -Method ApplyFixes

    static string MatName(string cls) { for (int i = 0; i < MAT.GetLength(0); i++) if (MAT[i, 0] == cls) return MAT[i, 1]; return null; }

    static string ClassOf(string name, NameRule[] rules)
    {
        var n = name.ToLower();
        foreach (var r in rules) if (!string.IsNullOrEmpty(r.match) && n.Contains(r.match.ToLower())) return r.@class;
        return null;
    }

    static PhysicMaterial FindMat(string name, Dictionary<string, PhysicMaterial> cache)
    {
        PhysicMaterial m; if (cache.TryGetValue(name, out m)) return m;
        foreach (var g in AssetDatabase.FindAssets(name + " t:PhysicMaterial"))
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(AssetDatabase.GUIDToAssetPath(g));
            if (pm && pm.name == name) { cache[name] = pm; return pm; }
        }
        cache[name] = null; return null;
    }

    public static string Run(bool apply, bool census, bool includeOverlays)
    {
        Physics.SyncTransforms();
        var rp = CB + "/rules/physics.json";
        var R = File.Exists(rp) ? JsonUtility.FromJson<Rules>(File.ReadAllText(rp).TrimStart('﻿')) : new Rules();
        if (R.name_rules.Length == 0) return "no name_rules in " + rp + " (run Run-CourseAutomation.ps1 -SyncTools)";
        string outDir = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/AutomationOutput/checks"; Directory.CreateDirectory(outDir);
        var opcd = GameObject.Find("OPCD Meshes"); var cache = new Dictionary<string, PhysicMaterial>();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.activeInHierarchy && (includeOverlays || g == opcd)).ToList();
        var audit = new List<string> { "root,collider,current,expected_class,expected,action,x,z" }; var unsure = new List<string> { "root,collider,current,x,z,reason" };
        int ok = 0, fixes = 0, missing = 0, uns = 0, applied = 0;
        foreach (var root in roots)
            foreach (var c in root.GetComponentsInChildren<Collider>(false))
            {
                if (c.GetComponentInParent<LODGroup>()) continue;   // trees: their tools own GSPtimber / GSPleaves
                var p = c.bounds.center; string cur = c.sharedMaterial ? c.sharedMaterial.name : "";
                if (cur == "GSPleaves" || cur == "GSPtimber") { ok++; audit.Add(string.Join(",", new[] { root.name, c.name.Replace(',', ';'), cur, "plant", cur, "ok", p.x.ToString("F1", CI), p.z.ToString("F1", CI) })); continue; }   // shrubs / props on plant physics: the tree tools own these
                string cls = ClassOf(c.name, R.name_rules) ?? ClassOf(c.transform.parent ? c.transform.parent.name : "", R.name_rules);
                string exp = cls != null ? MatName(cls) : null; string action;
                if (cls == null && !string.IsNullOrEmpty(cur)) { action = "unmatched"; ok++; }   // no rule for the name, but it carries a material: glance, usually fine
                else if (cls == null) { action = "unsure"; uns++; unsure.Add(string.Join(",", new[] { root.name, c.name, cur, p.x.ToString("F1", CI), p.z.ToString("F1", CI), "no name rule and no material" })); }
                else if (exp == null) { action = "unsure"; uns++; unsure.Add(string.Join(",", new[] { root.name, c.name, cur, p.x.ToString("F1", CI), p.z.ToString("F1", CI), "class " + cls + " has no material in this tool's table" })); }
                else if (cur == exp) { action = "ok"; ok++; }
                else
                {
                    var pm = FindMat(exp, cache);
                    if (!pm) { action = "missing-asset"; missing++; unsure.Add(string.Join(",", new[] { root.name, c.name, cur, p.x.ToString("F1", CI), p.z.ToString("F1", CI), "physic material asset " + exp + " not found in Assets" })); }
                    else
                    {
                        action = string.IsNullOrEmpty(cur) ? "fix(missing)" : "fix"; fixes++;
                        if (apply) { Undo.RecordObject(c, "physics audit"); c.sharedMaterial = pm; EditorUtility.SetDirty(c); applied++; }
                    }
                }
                audit.Add(string.Join(",", new[] { root.name, c.name.Replace(',', ';'), cur, cls ?? "", exp ?? "", action, p.x.ToString("F1", CI), p.z.ToString("F1", CI) }));
            }
        File.WriteAllLines(outDir + "/physics_audit.csv", audit.ToArray()); File.WriteAllLines(outDir + "/physics_unsure.csv", unsure.ToArray());
        if (apply && applied > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        string cen = "";
        if (census)
        {
            float g = Mathf.Max(0.5f, R.census.grid_m); var b = new Bounds(); bool any = false;
            foreach (var root in roots) foreach (var r in root.GetComponentsInChildren<Renderer>(false)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            var rows = new List<string> { "x,z,y,collider,material" }; int n = 0;
            if (any)
                for (float x = Mathf.Floor(b.min.x); x <= b.max.x; x += g) for (float z = Mathf.Floor(b.min.z); z <= b.max.z; z += g)
                {
                    RaycastHit best = new RaycastHit(); bool hit = false;
                    foreach (var h in Physics.RaycastAll(new Vector3(x, 600, z), Vector3.down, 1200))
                    {
                        if (h.collider.GetComponentInParent<LODGroup>()) continue;
                        if (!hit || h.point.y > best.point.y) { best = h; hit = true; }
                    }
                    if (!hit) continue;
                    rows.Add(x.ToString("F1", CI) + "," + z.ToString("F1", CI) + "," + best.point.y.ToString("F2", CI) + "," + best.collider.name.Replace(',', ';') + "," + (best.collider.sharedMaterial ? best.collider.sharedMaterial.name : "")); n++;
                }
            File.WriteAllLines(outDir + "/physics_census.csv", rows.ToArray()); cen = "; census " + n + " points at " + g + " m -> physics_census.csv (run tools/physics_check.py against the SVG)";
        }
        var head = "physics audit: " + ok + " ok, " + fixes + " to fix" + (apply ? " (" + applied + " applied)" : " (dry run)") + ", " + missing + " missing material asset, " + uns + " unsure -> " + outDir + "/physics_audit.csv, physics_unsure.csv" + cen;
        Debug.Log(head); return head;
    }
}
