// Moorpark: GSP > Build Course without the window, once per GSPro layout. GSPro names a course after its scene, so each
// layout gets a copy of Moorpark.unity called after its course folder (Moorpark_RidgelineCreekside, Moorpark_Ridgeline, ...).
// Builds into OutDir (a staging folder), with the collider of every terrain disabled, as on Tierra.
// Unity 2018.2 compatible.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkBuild
{
    public static string OutDir = "C:/Users/user/Documents/moorpark/build/gspro_bundles";
    public static string Courses = "Moorpark_RidgelineCreekside,Moorpark_CreeksideCanyonCrest,Moorpark_CanyonCrestRidgeline";
    const string Source = "Assets/Moorpark/Moorpark.unity";

    // one layout per call, so the bridge can report between builds
    public static string Build_RidgelineCreekside() { return BuildList("Moorpark_RidgelineCreekside"); }
    public static string Build_CreeksideCanyonCrest() { return BuildList("Moorpark_CreeksideCanyonCrest"); }
    public static string Build_CanyonCrestRidgeline() { return BuildList("Moorpark_CanyonCrestRidgeline"); }
    public static string Build_Ridgeline() { return BuildList("Moorpark_Ridgeline"); }
    public static string Build_Creekside() { return BuildList("Moorpark_Creekside"); }
    public static string Build_CanyonCrest() { return BuildList("Moorpark_CanyonCrest"); }

    public static string BuildAll() { return BuildList(Courses); }

    static string BuildList(string list)
    {
        var sb = new System.Text.StringBuilder();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        if (!AssetDatabase.IsValidFolder("Assets/Moorpark/Courses")) AssetDatabase.CreateFolder("Assets/Moorpark", "Courses");
        foreach (string course in list.Split(','))
        {
            string scenePath = "Assets/Moorpark/Courses/" + course + ".unity";
            AssetDatabase.DeleteAsset(scenePath);
            AssetDatabase.CopyAsset(Source, scenePath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var terrains = Object.FindObjectsOfType<Terrain>();
            var toggles = new bool[terrains.Length];
            for (int i = 0; i < toggles.Length; i++) toggles[i] = true;      // "Disable ... Collider" ticked for every terrain
            var t0 = System.DateTime.Now;
            Directory.CreateDirectory(OutDir);
            BuildCourseDLL.Encryption.MyScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            BuildCourseDLL.Encryption.gspdir = OutDir;
            BuildCourseDLL.Encryption.TerrainToggles = toggles;
            BuildCourseDLL.Encryption.Terrains = terrains;
            try
            {
                BuildCourseDLL.Encryption.DisableTerrains();
                BuildCourseDLL.Encryption.BuildBundle();
                string f = Path.Combine(OutDir, course + "/" + course + ".gspcrse");
                sb.AppendLine(course + ": " + (File.Exists(f) ? (new FileInfo(f).Length / 1048576) + " MB" : "no .gspcrse found in " + Path.Combine(OutDir, course)) +
                              " in " + (System.DateTime.Now - t0).TotalMinutes.ToString("0.0") + " min");
            }
            catch (System.Exception e) { sb.AppendLine(course + ": FAILED " + e); }
            EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);   // back to the working scene, unsaved changes dropped
        }
        return sb.ToString();
    }
}
