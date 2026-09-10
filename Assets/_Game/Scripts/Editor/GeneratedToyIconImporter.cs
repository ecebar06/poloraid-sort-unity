#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class GeneratedToyIconImporter : AssetPostprocessor
{
    const string Folder = "Assets/_Game/Resources/Icons/Generated/";
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    [MenuItem("Tools/Word Game/Link Generated Toy Icons")]
    static void Link()
    {
        const string path = "Assets/_Game/Resources/Data/MainWordIconLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<WordIconLibrary>(path);
        if (library == null || !Directory.Exists(Folder)) return;
        Undo.RecordObject(library, "Link generated toy icons");
        int linked = 0;
        foreach (var file in Directory.GetFiles(Folder, "*.png"))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
            if (sprite == null) continue;
            library.SetIcon(Path.GetFileNameWithoutExtension(file), sprite);
            linked++;
        }
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Linked {linked} generated toy icons.");
    }
}
#endif
