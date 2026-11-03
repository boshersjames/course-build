// Moorpark water planes, as on Tierra: OPCD-WaterPlane_AquasLite material and GSPwater ball physics. The lake and the
// pond get one flat surface (their LiDAR "surface" wanders 1.5 m with the banks); the stream keeps its sloping surface,
// like Tierra's upper and lower range ponds. Unity 2018.2 compatible.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkWater
{
    const string Dir = "Assets/Moorpark/generated/water";

    public static string Planes()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Object Materials/OPCD-WaterPlane_AquasLite.mat");
        var phys = Resources.Load<PhysicMaterial>("Physics/GSPwater");
        Directory.CreateDirectory(Dir);
        var sb = new System.Text.StringBuilder();
        foreach (var mf in GameObject.Find("OPCD Meshes").GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.Contains("WaterPlane")) continue;
            // always start from the imported FBX mesh (re-runs don't stack)
            var src = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Moorpark/FBX/" + mf.name.Replace(' ', '_') + ".fbx") ?? mf.sharedMesh;
            var v = src.vertices;
            var b = src.bounds;
            string note = "sloping (kept)";
            float area = 0f; var tri = src.triangles;
            for (int i = 0; i < tri.Length; i += 3) area += 0.5f * Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]).magnitude;
            if (area > 2000f)                                       // lake and pond; the stream plane is a few hundred m2
            {
                var ys = new List<float>(); foreach (var p in v) ys.Add(p.y); ys.Sort();
                float level = ys[ys.Count / 10];                   // 10th percentile: the water, not the banks
                for (int i = 0; i < v.Length; i++) v[i].y = level;
                note = "flat at " + (level + mf.transform.position.y).ToString("0.00") + " m";
            }
            var m = Object.Instantiate(src); m.name = mf.name.Replace(' ', '_'); m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds();
            string path = Dir + "/" + m.name + ".asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
            mf.sharedMesh = m;
            var r = mf.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = mf.GetComponent<MeshCollider>() ?? mf.gameObject.AddComponent<MeshCollider>();
            c.sharedMesh = m; c.sharedMaterial = phys;
            mf.gameObject.layer = LayerMask.NameToLayer("Water") >= 0 ? LayerMask.NameToLayer("Water") : 0;
            sb.AppendLine(mf.name + ": " + note);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return sb.ToString();
    }
}
