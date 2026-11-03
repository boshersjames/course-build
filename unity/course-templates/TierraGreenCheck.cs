using System.Linq;
using System.IO;
using UnityEngine;
// Report: green / collar meshes near a point (holes 12-18 green centres): vertex count, y range, normal tilt spread, material and shader.
public static class TierraGreenCheck
{
    public static string Run()
    {
        var sb = new System.Text.StringBuilder();
        var g = Newtonsoft(); 
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            if (!mf.sharedMesh || !(mf.name.Contains("Green") || mf.name.Contains("green") || mf.name.Contains("Collar") || mf.name.Contains("Fringe"))) continue;
            var b = mf.GetComponent<Renderer>().bounds; var c = b.center;
            if (!g.Any(p => Mathf.Abs(p.x - c.x) < 40 && Mathf.Abs(p.z - c.z) < 40)) continue;
            var m = mf.sharedMesh; var v = m.vertices; var n = m.normals; var tr = mf.transform;
            float ymin = v.Min(p => tr.TransformPoint(p).y), ymax = v.Max(p => tr.TransformPoint(p).y);
            float tilt = n.Length > 0 ? n.Select(q => Vector3.Angle(tr.TransformDirection(q), Vector3.up)).OrderBy(a => a).ElementAt((int)(n.Length * 0.9f)) : -1;
            var r = mf.GetComponent<Renderer>();
            sb.AppendLine(string.Format("{0} [{1}] verts {2} y {3:F2}-{4:F2} normal tilt p90 {5:F1} deg, mat {6} / {7}, mesh {8}", mf.name, mf.transform.root.name, v.Length, ymin, ymax, tilt, r.sharedMaterial ? r.sharedMaterial.name : "-", r.sharedMaterial ? r.sharedMaterial.shader.name : "-", m.name));
        }
        File.WriteAllText("AutomationOutput/vegetation/green_check.txt", sb.ToString()); return sb.Length + " chars";
    }
    static Vector3[] Newtonsoft() { return new[] { new Vector3(1245.7f, 0, 848.5f),new Vector3(827.9f, 0, 893.8f),new Vector3(737.5f, 0, 713.9f),new Vector3(1173.3f, 0, 826.7f),new Vector3(1689.0f, 0, 816.8f),new Vector3(1632.0f, 0, 898.0f),new Vector3(1271.7f, 0, 1063.1f) }; }
}
