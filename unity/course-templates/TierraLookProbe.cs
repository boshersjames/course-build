using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only: what makes the scene Main Camera look different from the automation render camera (post-processing, gradient fog,
// volumetric light), and a 4651 render through a hidden clone of the Main Camera (destroyed after; the scene is not changed).
public class TierraLookProbe
{
    public float px, py, pz, lx, ly, lz, fov = 52; public string path = ""; public bool above;
    public static string Effects()
    {
        var o = new List<string>();
        foreach (var c in Resources.FindObjectsOfTypeAll<Component>().Where(c => c && c.gameObject.scene.IsValid()))
        {
            var tn = c.GetType().Name; if (!(tn.Contains("PostProcess") || tn.Contains("GradientFog") || tn.Contains("Volumetric") || tn.Contains("Fog"))) continue;
            var s = new List<string>(); foreach (var f in c.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) { object v = null; try { v = f.GetValue(c); } catch { } if (v is Object && (Object)v) v = ((Object)v).name; s.Add(f.Name + "=" + (v == null ? "null" : v.ToString())); }
            o.Add(c.gameObject.name + " [" + tn + "] active " + c.gameObject.activeInHierarchy + (c is Behaviour ? " enabled " + ((Behaviour)c).enabled : "") + " :: " + string.Join("; ", s.Take(25).ToArray()));
        }
        o.Add("RenderSettings fog " + RenderSettings.fog + " mode " + RenderSettings.fogMode + " colour " + RenderSettings.fogColor + " start/end " + RenderSettings.fogStartDistance + "/" + RenderSettings.fogEndDistance + " density " + RenderSettings.fogDensity + "; skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name : "none"));
        return string.Join("\n", o.ToArray());
    }
    static float G(Vector3 p) { float b = float.NegativeInfinity; foreach (var tr in Terrain.activeTerrains) { var o = tr.GetPosition(); var sz = tr.terrainData.size; if (p.x < o.x || p.z < o.z || p.x > o.x + sz.x || p.z > o.z + sz.z) continue; b = Mathf.Max(b, tr.SampleHeight(p) + o.y); } return float.IsInfinity(b) ? 0 : b; }
    public string Render()
    {
        var main = Resources.FindObjectsOfTypeAll<Camera>().FirstOrDefault(c => c.gameObject.scene.IsValid() && c.name == "Main Camera"); if (!main) return "no Main Camera";
        var P = new Vector3(px, py, pz); var L = new Vector3(lx, ly, lz); if (above) { P.y += G(P); L.y += G(L); }
        var outp = new List<string>();
        foreach (var mode in new[] { "main", "plain" })
        {
            GameObject go; Camera cam;
            if (mode == "main") { go = Object.Instantiate(main.gameObject); go.hideFlags = HideFlags.HideAndDontSave; cam = go.GetComponent<Camera>(); foreach (var b in go.GetComponents<MonoBehaviour>()) if (b.GetType().Name.Contains("CameraControl")) Object.DestroyImmediate(b); var rb = go.GetComponent<Rigidbody>(); if (rb) Object.DestroyImmediate(rb); var al = go.GetComponent<AudioListener>(); if (al) Object.DestroyImmediate(al); cam.enabled = true; }
            else { go = new GameObject("probe_plain") { hideFlags = HideFlags.HideAndDontSave }; cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.3f; cam.farClipPlane = 6000f; cam.clearFlags = CameraClearFlags.Skybox; }
            cam.fieldOfView = fov; go.transform.position = P; go.transform.LookAt(L);
            var rt = new RenderTexture(1280, 960, 24) { antiAliasing = mode == "plain" ? 4 : 1 }; cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; var tx = new Texture2D(1280, 960, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); tx.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            var fp = path.Replace(".png", "_" + mode + ".png"); System.IO.File.WriteAllBytes(fp, tx.EncodeToPNG()); Object.DestroyImmediate(tx); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); outp.Add(fp);
        }
        return string.Join(" ", outp.ToArray());
    }
}
