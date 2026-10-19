#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class GeneratedToyIconImporter : AssetPostprocessor
{
    const string Folder = "Assets/_Game/Resources/Icons/Generated/";
    const string LibraryPath = "Assets/_Game/Resources/Data/MainWordIconLibrary.asset";

    [InitializeOnLoadMethod]
    static void ScheduleMissingIconLink()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                LinkGeneratedIcons(false);
        };
    }

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
    static void Link() => LinkGeneratedIcons(true);

    static void LinkGeneratedIcons(bool alwaysLog)
    {
        var library = AssetDatabase.LoadAssetAtPath<WordIconLibrary>(LibraryPath);
        if (library == null || !Directory.Exists(Folder)) return;

        int linked = 0;
        foreach (var file in Directory.GetFiles(Folder, "*.png"))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
            if (sprite == null) continue;

            string key = Path.GetFileNameWithoutExtension(file);
            if (library.TryGetIcon(key, out Sprite current) && current == sprite) continue;
            if (linked == 0) Undo.RecordObject(library, "Link generated toy icons");
            library.SetIcon(key, sprite);
            linked++;
        }

        if (linked == 0)
        {
            if (alwaysLog) Debug.Log("All generated toy icons are already linked.");
            return;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Linked {linked} missing generated toy icons.");
    }
}
#endif
