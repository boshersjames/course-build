using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Raises the terrain back up towards the OPCD meshes after GSP's "Lower Terrain Under Meshes" (which drops it 1 m), so
// terrain detail grass grows at the visible surface instead of 1 m under it. Each heightmap vertex goes up by
// min(maxRaise, gap - clearance), where gap is the smallest distance to the lowest non-water OPCD surface over its 3x3
// neighbours. Vertices with no mesh above, and under water planes / lake beds / STREAMS (+ buffer), are left alone.
// The original heights are written next to the terrain asset (<asset>.original_heights.bin) the first time; Apply always
// starts from them, and Restore puts them back. Distant terrain LOD can lift it a few dm, so heightmapPixelError is set to 1.
public class RaiseTerrainUnderMeshes : EditorWindow
{
    Terrain terrain;
    float maxRaise = 0.95f, clearance = 0.25f;
    int waterBuffer = 2;   // vertices
    bool setPixelError = true;

    [MenuItem("Tierra/Raise Terrain Under Meshes")]
    static void Open() { GetWindow<RaiseTerrainUnderMeshes>("Raise Terrain"); }

    void OnGUI()
    {
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        maxRaise = EditorGUILayout.FloatField("Max raise (m)", maxRaise);
        clearance = EditorGUILayout.FloatField("Clearance under meshes (m)", clearance);
        waterBuffer = EditorGUILayout.IntField("Water buffer (vertices)", waterBuffer);
        setPixelError = EditorGUILayout.Toggle("Set pixel error to 1", setPixelError);
        if (GUILayout.Button("Apply")) Apply();
        if (GUILayout.Button("Restore original heights")) Restore();
    }

    Terrain T() { if (terrain == null) terrain = Object.FindObjectsOfType<Terrain>().FirstOrDefault(t => t.name == "Terrain (Inner)"); return terrain; }
    string BinPath(Terrain t) { return AssetDatabase.GetAssetPath(t.terrainData) + ".original_heights.bin"; }

    static void Raster(Mesh m, Transform tr, int res, float cell, float[,] grid, bool[,] mark)
    {
        var v = m.vertices; var tris = m.triangles; var w = v.Select(p => tr.TransformPoint(p)).ToArray();
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = w[tris[i]], b = w[tris[i + 1]], c = w[tris[i + 2]];
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / cell)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / cell)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / cell));
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z); if (Mathf.Abs(d) < 1e-9f) continue;
            for (int j = z0; j <= z1; j++) for (int k = x0; k <= x1; k++)
                {
                    float px = k * cell, pz = j * cell;
                    float l1 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / d, l2 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / d, l3 = 1 - l1 - l2;
                    if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                    if (mark != null) { mark[j, k] = true; continue; }
                    float y = l1 * a.y + l2 * b.y + l3 * c.y;
                    grid[j, k] = float.IsNaN(grid[j, k]) ? y : Mathf.Min(grid[j, k], y);
                }
        }
    }

    public void Apply()
    {
        var t = T(); if (t == null) { Debug.LogError("RaiseTerrainUnderMeshes: no terrain"); return; }
        var opcd = GameObject.Find("OPCD Meshes"); if (opcd == null) { Debug.LogError("RaiseTerrainUnderMeshes: no 'OPCD Meshes'"); return; }
        var td = t.terrainData; int res = td.heightmapResolution; float cell = td.size.x / (res - 1), sy = td.size.y, oy = t.GetPosition().y;
        var h = td.GetHeights(0, 0, res, res); string bin = BinPath(t);
        if (!File.Exists(bin)) using (var bw = new BinaryWriter(File.Create(bin))) { bw.Write(res); for (int j = 0; j < res; j++) for (int i = 0; i < res; i++) bw.Write(h[j, i]); }
        else using (var br = new BinaryReader(File.OpenRead(bin))) { br.ReadInt32(); for (int j = 0; j < res; j++) for (int i = 0; i < res; i++) h[j, i] = br.ReadSingle(); }
        var surf = new float[res, res]; for (int j = 0; j < res; j++) for (int i = 0; i < res; i++) surf[j, i] = float.NaN;
        var water = new bool[res, res];
        foreach (var mc in opcd.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc.sharedMesh == null || !mc.sharedMesh.isReadable) continue;
            if (mc.name.Contains("Water")) { if (mc.gameObject.activeInHierarchy || mc.name.Contains("Base")) Raster(mc.sharedMesh, mc.transform, res, cell, null, water); continue; }
            if (mc.gameObject.activeInHierarchy) Raster(mc.sharedMesh, mc.transform, res, cell, surf, null);
        }
        var st = GameObject.Find("STREAMS"); if (st != null) foreach (var mc in st.GetComponentsInChildren<MeshCollider>()) Raster(mc.sharedMesh, mc.transform, res, cell, null, water);
        var wd = (bool[,])water.Clone();
        for (int j = 0; j < res; j++) for (int i = 0; i < res; i++) if (water[j, i])
                    for (int dj = -waterBuffer; dj <= waterBuffer; dj++) for (int di = -waterBuffer; di <= waterBuffer; di++) { int a = j + dj, b = i + di; if (a >= 0 && b >= 0 && a < res && b < res) wd[a, b] = true; }
        var nh = (float[,])h.Clone(); int raised = 0; double sum = 0;
        for (int j = 0; j < res; j++) for (int i = 0; i < res; i++)
            {
                if (wd[j, i]) continue;
                float g = float.PositiveInfinity; bool ok = true;
                for (int dj = -1; dj <= 1 && ok; dj++) for (int di = -1; di <= 1; di++)
                    {
                        int a = j + dj, b = i + di; if (a < 0 || b < 0 || a >= res || b >= res) continue;
                        if (float.IsNaN(surf[a, b])) { ok = false; break; }
                        g = Mathf.Min(g, surf[a, b] - (h[a, b] * sy + oy));
                    }
                if (!ok) continue;
                float d = Mathf.Clamp(g - clearance, 0f, maxRaise); if (d <= 0) continue;
                nh[j, i] = h[j, i] + d / sy; raised++; sum += d;
            }
        Undo.RegisterCompleteObjectUndo(td, "Raise terrain under meshes");
        td.SetHeights(0, 0, nh); if (setPixelError) t.heightmapPixelError = 1f; t.Flush();
        EditorUtility.SetDirty(td); EditorUtility.SetDirty(t); EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        Debug.Log(string.Format("RaiseTerrainUnderMeshes: raised {0} of {1} vertices, mean {2:F3} m (clearance {3}, max {4}); originals in {5}", raised, res * res, raised > 0 ? sum / raised : 0, clearance, maxRaise, bin));
    }

    public void Restore()
    {
        var t = T(); var td = t.terrainData; string bin = BinPath(t);
        if (!File.Exists(bin)) { Debug.LogError("RaiseTerrainUnderMeshes: no " + bin); return; }
        int res = td.heightmapResolution; var h = new float[res, res];
        using (var br = new BinaryReader(File.OpenRead(bin))) { if (br.ReadInt32() != res) { Debug.LogError("resolution differs"); return; } for (int j = 0; j < res; j++) for (int i = 0; i < res; i++) h[j, i] = br.ReadSingle(); }
        Undo.RegisterCompleteObjectUndo(td, "Restore terrain heights"); td.SetHeights(0, 0, h); t.Flush(); EditorUtility.SetDirty(td);
        EditorSceneManager.MarkSceneDirty(t.gameObject.scene); Debug.Log("RaiseTerrainUnderMeshes: restored original heights");
    }
}
