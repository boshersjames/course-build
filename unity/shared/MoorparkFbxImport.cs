// Moorpark course FBX (Assets/Moorpark/FBX): readable meshes, no imported placeholder materials (MoorparkLook.Apply sets them).
// Unity 2018.2 compatible.
using UnityEditor;

public class MoorparkFbxImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Moorpark/FBX/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.isReadable = true;
        mi.importMaterials = false;
        mi.importAnimation = false;
    }
}
