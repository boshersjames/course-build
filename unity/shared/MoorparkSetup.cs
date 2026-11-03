// Moorpark Country Club: build the base scene from Assets/Moorpark (course.json, heightmap_2049.raw, satellite.jpg).
// Menu  Moorpark > Set Up Scene  writes Assets/Moorpark/Moorpark.unity with the terrain, satellite splat, light and camera.
// Running it again rebuilds the terrain and scene from the files.  Unity 2018.2 compatible (SplatPrototype, not TerrainLayer).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MoorparkSetup
{
    const string Folder = "Assets/Moorpark";
    const string ScenePath = Folder + "/Moorpark.unity";
    const string TerrainDataPath = Folder + "/Moorpark_TerrainData.asset";
    const string RawPath = Folder + "/heightmap_2049.raw";
    const string SatellitePath = Folder + "/satellite.jpg";
    const string ConfigPath = Folder + "/course.json";

    [System.Serializable]
    class CourseConfig
    {
        public float size = 3000f;
        public float terrain_height = 151f;
        public float y_offset = 156f;
    }

    [MenuItem("Moorpark/Set Up Scene")]
    public static void SetUpScene()
    {
        if (!File.Exists(ConfigPath) || !File.Exists(RawPath) || !File.Exists(SatellitePath))
        {
            Debug.LogError("Moorpark setup: need " + ConfigPath + ", " + RawPath + " and " + SatellitePath);
            return;
        }
        var cfg = JsonUtility.FromJson<CourseConfig>(File.ReadAllText(ConfigPath));

        // heightmap: 2049 x 2049, 16-bit little-endian (Windows byte order), first row = south edge (z = 0)
        const int res = 2049;
        byte[] bytes = File.ReadAllBytes(RawPath);
        if (bytes.Length != res * res * 2)
        {
            Debug.LogError("Moorpark setup: " + RawPath + " is " + bytes.Length + " bytes, expected " + (res * res * 2));
            return;
        }
        var heights = new float[res, res];
        for (int z = 0, i = 0; z < res; z++)
            for (int x = 0; x < res; x++, i += 2)
                heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;

        var td = new TerrainData();
        td.heightmapResolution = res;
        td.size = new Vector3(cfg.size, cfg.terrain_height, cfg.size);   // size after resolution, which resets it
        td.SetHeights(0, 0, heights);

        // satellite as the only splat, stretched once over the whole square
        var sat = PrepareSatellite();
        td.splatPrototypes = new[] { new SplatPrototype { texture = sat, tileSize = new Vector2(cfg.size, cfg.size), tileOffset = Vector2.zero } };
        td.alphamapResolution = 512;
        td.baseMapResolution = 2048;
        var alpha = new float[td.alphamapHeight, td.alphamapWidth, 1];
        for (int z = 0; z < td.alphamapHeight; z++)
            for (int x = 0; x < td.alphamapWidth; x++)
                alpha[z, x, 0] = 1f;
        td.SetAlphamaps(0, 0, alpha);

        AssetDatabase.DeleteAsset(TerrainDataPath);
        AssetDatabase.CreateAsset(td, TerrainDataPath);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var terrainGo = Terrain.CreateTerrainGameObject(td);
        terrainGo.name = "Terrain (Inner)";   // the OPCD / GSP and vegetation tools look the terrain up by this name
        terrainGo.transform.position = Vector3.zero;
        var terrain = terrainGo.GetComponent<Terrain>();
        terrain.heightmapPixelError = 2f;
        terrain.basemapDistance = 4000f;   // keep the full-resolution satellite at any distance
        TerrainPhysics();

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = light;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 6000f;
        cam.fieldOfView = 55f;
        camGo.AddComponent<AudioListener>();
        float mid = cfg.size * 0.5f;
        camGo.transform.position = new Vector3(mid, terrain.SampleHeight(new Vector3(mid, 0f, mid - 900f)) + 500f, mid - 900f);
        camGo.transform.LookAt(new Vector3(mid, terrain.SampleHeight(new Vector3(mid, 0f, mid)), mid));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrainGo;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Moorpark: scene saved to " + ScenePath + ". Terrain " + cfg.size + " x " + cfg.terrain_height + " x " + cfg.size +
                  " m (y = elevation - " + cfg.y_offset + " m), satellite " + sat.width + " px.");
    }

    // Same as Tierra: terrain on the Terrain layer, ball physics GSPdeeprough (the GSP build warns without one)
    public static void TerrainPhysics()
    {
        var go = GameObject.Find("Terrain (Inner)");
        if (go == null) { Debug.LogError("Moorpark: no Terrain (Inner) in the scene"); return; }
        int layer = LayerMask.NameToLayer("Terrain");
        if (layer >= 0) go.layer = layer;
        var col = go.GetComponent<TerrainCollider>();
        col.sharedMaterial = Resources.Load<PhysicMaterial>("Physics/GSPdeeprough");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("Moorpark: terrain layer " + LayerMask.LayerToName(go.layer) + ", physics " + (col.sharedMaterial != null ? col.sharedMaterial.name : "none"));
    }

    static Texture2D PrepareSatellite()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(SatellitePath);
        int max = 8192;   // 4096 for the current NAIP image; a larger satellite.jpg keeps its full size
        if (imp.maxTextureSize != max || imp.wrapMode != TextureWrapMode.Clamp || imp.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            imp.maxTextureSize = max;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SatellitePath);
    }
}
