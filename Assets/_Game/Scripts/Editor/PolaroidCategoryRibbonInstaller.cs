#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the generated, tintable category ribbon to all four Scene holders once.
/// The resulting RectTransforms and colours remain fully editable in the Scene.
/// </summary>
internal static class PolaroidCategoryRibbonInstaller
{
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string RibbonPath = "Assets/_Game/Art/PolaroidTheme/UI/category_ribbon_v2.png";
    private const string MarkerName = "PolaroidCategoryRibbonEditablePlaceholderV6";

    private static readonly Color32[] RibbonColors =
    {
        new Color32(242, 118, 32, 255),
        new Color32(31, 183, 181, 255),
        new Color32(166, 67, 166, 255),
        new Color32(39, 183, 56, 255)
    };

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Word Game/Apply Category Ribbons")]
    private static void ApplyFromMenu() => Apply(true);

    private static void ApplyIfNeeded() => Apply(false);

    private static void Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        ImportRibbon();
        Sprite ribbon = AssetDatabase.LoadAssetAtPath<Sprite>(RibbonPath);
        if (ribbon == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (!force && Find(scene, MarkerName) != null)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        CategoryHolder[] holders = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CategoryHolder>(true))
            .OrderBy(holder => ((RectTransform)holder.transform).anchoredPosition.x)
            .ToArray();
        for (int index = 0; index < holders.Length; index++)
            ConfigureHolder(holders[index], ribbon, RibbonColors[Mathf.Min(index, RibbonColors.Length - 1)]);

        Canvas canvas = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
        if (canvas != null && Find(scene, MarkerName) == null)
        {
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(canvas.transform, false);
            marker.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Generated category ribbon added to all holders. Scene placement remains editable.");
    }

    private static void ConfigureHolder(CategoryHolder holder, Sprite ribbon, Color color)
    {
        Transform existing = holder.transform.Find("CategoryBadge");
        GameObject badgeObject;
        if (existing == null)
        {
            badgeObject = new GameObject("CategoryBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObject.transform.SetParent(holder.transform, false);
        }
        else badgeObject = existing.gameObject;

        RectTransform badge = badgeObject.GetComponent<RectTransform>();
        badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 1f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        // Match the holder width: compact folded corners stay inside their own
        // column instead of overlapping the neighbouring ribbon.
        badge.anchoredPosition = new Vector2(0f, 20f);
        badge.sizeDelta = new Vector2(238f, 68f);
        badge.localRotation = Quaternion.identity;
        badge.localScale = Vector3.one;
        badge.SetAsLastSibling();

        Image image = badgeObject.GetComponent<Image>();
        if (image == null) image = badgeObject.AddComponent<Image>();
        image.sprite = ribbon;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;

        // Placed cards use nested override-sorting canvases. Sibling order is
        // therefore insufficient: give the ribbon its own higher canvas so it
        // always renders in front of the first card.
        Canvas ribbonCanvas = badgeObject.GetComponent<Canvas>();
        if (ribbonCanvas == null) ribbonCanvas = badgeObject.AddComponent<Canvas>();
        ribbonCanvas.overrideSorting = true;
        ribbonCanvas.sortingOrder = 500;

        TextMeshProUGUI label = EnsureLabel(badge);
        // Keep an editable placeholder visible in Scene view. At runtime,
        // CategoryHolder replaces it with the actual category name as soon as
        // the first card claims this holder.
        label.text = "Category";
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 38f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 38f;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (font != null) label.font = font;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.14f, 0.23f);
        labelRect.anchorMax = new Vector2(0.86f, 0.82f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        labelRect.localRotation = Quaternion.identity;
        labelRect.localScale = Vector3.one;

        SerializedObject serialized = new SerializedObject(holder);
        SerializedProperty property = serialized.FindProperty("categoryLabel");
        if (property != null) property.objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(holder);
        badgeObject.SetActive(true);
    }

    private static TextMeshProUGUI EnsureLabel(RectTransform badge)
    {
        Transform existing = badge.Find("CategoryLabel");
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI label)) return label;
        GameObject go = new GameObject("CategoryLabel", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(badge, false);
        return go.GetComponent<TextMeshProUGUI>();
    }

    private static void ImportRibbon()
    {
        AssetDatabase.ImportAsset(RibbonPath, ImportAssetOptions.ForceSynchronousImport);
        if (!(AssetImporter.GetAtPath(RibbonPath) is TextureImporter importer)) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindWithin(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }

    private static Transform FindWithin(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindWithin(root.GetChild(index), name);
            if (result != null) return result;
        }
        return null;
    }
}
#endif
