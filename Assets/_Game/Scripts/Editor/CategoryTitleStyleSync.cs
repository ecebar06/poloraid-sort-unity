#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CategoryTitleStyleSync
{
    static double nextUpdate;
    static CategoryTitleStyleSync() => EditorApplication.update += Sync;

    static void Sync()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextUpdate) return;
        nextUpdate = EditorApplication.timeSinceStartup + 0.4;
        foreach (var board in Object.FindObjectsByType<CategoryMagnetBoardController>(FindObjectsInactive.Include))
        {
            var area = board.transform.Find("CategoryHolders");
            if (area == null || area.childCount == 0) continue;
            var sourceTransform = area.GetChild(0).Find("CategoryLabel");
            if (sourceTransform == null) continue;
            var source = sourceTransform.GetComponent<TextMeshProUGUI>();
            if (source == null) continue;
            for (int i = 1; i < area.childCount; i++)
            {
                var title = area.GetChild(i).Find("CategoryLabel");
                if (title == null) continue;
                var target = title.GetComponent<TextMeshProUGUI>();
                if (target == null) continue;
                if (target.font == source.font && target.fontSharedMaterial == source.fontSharedMaterial &&
                    target.color == source.color && target.fontStyle == source.fontStyle &&
                    target.fontWeight == source.fontWeight && target.fontSize == source.fontSize &&
                    target.enableAutoSizing == source.enableAutoSizing && target.fontSizeMin == source.fontSizeMin &&
                    target.fontSizeMax == source.fontSizeMax && target.characterSpacing == source.characterSpacing) continue;
                target.font = source.font;
                target.fontSharedMaterial = source.fontSharedMaterial;
                target.color = source.color;
                target.fontStyle = source.fontStyle;
                target.fontWeight = source.fontWeight;
                target.fontSize = source.fontSize;
                target.enableAutoSizing = source.enableAutoSizing;
                target.fontSizeMin = source.fontSizeMin;
                target.fontSizeMax = source.fontSizeMax;
                target.characterSpacing = source.characterSpacing;
                target.UpdateMeshPadding();
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            }
        }
    }
}
#endif
