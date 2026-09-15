#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CategoryMagnetBoardSceneSetup
{
    // The plain holder artwork used by the four category slots.
    private const string HolderSpritePath = "Assets/_Game/Resources/Art/Fridge/Rows/magnet_holder_empty.png";
    private const string MagnetSpritePath = "Assets/_Game/Resources/Art/Fridge/Magnets/magnet_word.png";

    static CategoryMagnetBoardSceneSetup() => EditorApplication.delayCall += EnsureBoard;

    [MenuItem("Tools/Word Game/Build Category Magnet Board")]
    public static void EnsureBoard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Game") return;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        TableController table = Object.FindFirstObjectByType<TableController>(FindObjectsInactive.Include);
        if (canvas == null || table == null) return;
        Transform gameHud = canvas.transform.Find("GameHUD");
        if (gameHud == null) return;
        Transform existingBoard = gameHud.Find("CategoryMagnetBoard");
        if (existingBoard != null)
        {
            EnsureEditableMagnetStyle(existingBoard, table);
            return;
        }

        GameObject board = Rect("CategoryMagnetBoard", gameHud);
        Stretch(board.GetComponent<RectTransform>());
        CategoryMagnetBoardController controller = board.AddComponent<CategoryMagnetBoardController>();

        RectTransform bottom = Rect("BottomMagnetArea", board.transform).GetComponent<RectTransform>();
        SetAnchors(bottom, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.61f));

        RectTransform holderArea = Rect("CategoryHolders", board.transform).GetComponent<RectTransform>();
        SetAnchors(holderArea, new Vector2(0.015f, 0.61f), new Vector2(0.985f, 0.84f));
        CategoryHolder[] holders = new CategoryHolder[4];
        for (int index = 0; index < holders.Length; index++)
            holders[index] = CreateHolder(holderArea, index);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("bottomArea").objectReferenceValue = bottom;
        SerializedProperty holderProperty = serialized.FindProperty("holders");
        holderProperty.arraySize = holders.Length;
        for (int i = 0; i < holders.Length; i++)
            holderProperty.GetArrayElementAtIndex(i).objectReferenceValue = holders[i];
        serialized.FindProperty("legacyTableController").objectReferenceValue = table;
        SerializedObject tableData = new SerializedObject(table);
        serialized.FindProperty("levelLabel").objectReferenceValue = tableData.FindProperty("levelLabel").objectReferenceValue;
        serialized.FindProperty("moveLabel").objectReferenceValue = tableData.FindProperty("moveCountText").objectReferenceValue;
        serialized.FindProperty("winScreen").objectReferenceValue = tableData.FindProperty("winScreen").objectReferenceValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnsureEditableMagnetStyle(board.transform, table);

        board.transform.SetSiblingIndex(Mathf.Max(0, gameHud.childCount - 2));
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = board;
        Debug.Log("Category magnet board sahneye eklendi. Yerlesimi Scene'den duzenleyip Ctrl+S ile kaydet.", board);
    }

    private static void EnsureEditableMagnetStyle(Transform board, TableController table)
    {
        CategoryMagnetBoardController controller = board.GetComponent<CategoryMagnetBoardController>();
        if (controller == null) return;
        foreach (CategoryHolder holder in board.GetComponentsInChildren<CategoryHolder>(true))
        {
            holder.ApplyReadableCountStyle();
            AddHeaderBadges(holder);
            Transform title = holder.transform.Find("CategoryLabel");
            if (title == null) continue;
            var label = title.GetComponent<TextMeshProUGUI>();
            if (label == null || label.fontSharedMaterial == null) continue;
            const string categoryMaterialPath = "Assets/_Game/Resources/Fonts/CategoryTitleIndependent.mat";
            Material independent = AssetDatabase.LoadAssetAtPath<Material>(categoryMaterialPath);
            if (independent == null)
            {
                // Copy properties into a standalone material (not a variant
                // inheriting changes from the Level / Moves material).
                independent = new Material(label.fontSharedMaterial.shader);
                independent.CopyPropertiesFromMaterial(label.fontSharedMaterial);
                independent.name = "CategoryTitleIndependent";
                AssetDatabase.CreateAsset(independent, categoryMaterialPath);
            }
            if (label.fontSharedMaterial != independent)
            {
                Undo.RecordObject(label, "Separate category title material");
                label.fontSharedMaterial = independent;
                label.UpdateMeshPadding();
                EditorUtility.SetDirty(label);
            }
        }

        // The legacy pooled rows only clutter the authoring view in this mode.
        if (table.wordContainer != null && table.wordContainer.gameObject.activeSelf)
        {
            Undo.RecordObject(table.wordContainer.gameObject, "Hide legacy word rows");
            table.wordContainer.gameObject.SetActive(false);
        }

        Transform template = board.Find("MagnetStyleTemplate");
        TextMeshProUGUI styleLabel;
        if (template == null)
        {
            GameObject sample = Rect("MagnetStyleTemplate", board);
            RectTransform sampleRect = sample.GetComponent<RectTransform>();
            sampleRect.anchorMin = sampleRect.anchorMax = new Vector2(0.5f, 0.48f);
            sampleRect.pivot = new Vector2(0.5f, 0.5f);
            sampleRect.anchoredPosition = Vector2.zero;
            sampleRect.sizeDelta = new Vector2(260f, 120f);
            Image image = sample.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MagnetSpritePath);
            image.type = image.sprite != null && image.sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;

            styleLabel = Text("MagnetTextStyle", sampleRect, "WordButton", 40f);
            Stretch(styleLabel.rectTransform);
            styleLabel.rectTransform.offsetMin = new Vector2(8f, 6f);
            styleLabel.rectTransform.offsetMax = new Vector2(-8f, -6f);
            styleLabel.enableAutoSizing = true;
            styleLabel.fontSizeMin = 22f;
            styleLabel.fontSizeMax = 40f;
            styleLabel.color = Color.white;
            styleLabel.outlineColor = Color.black;
            styleLabel.outlineWidth = 0.14f;
        }
        else
        {
            styleLabel = template.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Apply this preset once; later Inspector edits remain user-controlled.
        const string readableMaterialPath = "Assets/_Game/Resources/Fonts/MagnetReadable.mat";
        if (template == null && styleLabel != null && AssetDatabase.LoadAssetAtPath<Material>(readableMaterialPath) == null)
        {
            Material material = new Material(styleLabel.fontSharedMaterial);
            material.name = "MagnetReadable";
            material.SetColor("_FaceColor", Color.white);
            material.SetFloat("_FaceDilate", 0.08f);
            material.SetColor("_OutlineColor", Color.black);
            material.SetFloat("_OutlineWidth", 0.09f);
            material.SetFloat("_OutlineSoftness", 0f);
            material.EnableKeyword("OUTLINE_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.45f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.25f);
            material.SetFloat("_UnderlayDilate", 0f);
            material.SetFloat("_UnderlaySoftness", 0.12f);
            AssetDatabase.CreateAsset(material, readableMaterialPath);
            Undo.RecordObject(styleLabel, "Improve magnet readability");
            styleLabel.fontSharedMaterial = material;
            styleLabel.color = Color.white;
            styleLabel.fontStyle = FontStyles.Bold;
            styleLabel.fontSize = 44f;
            styleLabel.fontSizeMin = 26f;
            styleLabel.fontSizeMax = 44f;
            styleLabel.enableAutoSizing = true;
            styleLabel.extraPadding = true;
            EditorUtility.SetDirty(styleLabel);
        }

        SerializedObject serialized = new SerializedObject(controller);
        const string wordMaterialPath = "Assets/_Game/Resources/Fonts/WordButtonIndependent.mat";
        if (template == null && styleLabel != null && styleLabel.fontSharedMaterial != null)
        {
            Material wordMaterial = AssetDatabase.LoadAssetAtPath<Material>(wordMaterialPath);
            if (wordMaterial == null)
            {
                wordMaterial = new Material(styleLabel.fontSharedMaterial.shader);
                wordMaterial.CopyPropertiesFromMaterial(styleLabel.fontSharedMaterial);
                wordMaterial.name = "WordButtonIndependent";
                AssetDatabase.CreateAsset(wordMaterial, wordMaterialPath);
            }
            Undo.RecordObject(styleLabel, "Separate WordButton material");
            styleLabel.fontSharedMaterial = wordMaterial;
            styleLabel.UpdateMeshPadding();
            EditorUtility.SetDirty(styleLabel);
        }
        SerializedProperty styleProperty = serialized.FindProperty("magnetTextStyle");
        if (styleProperty.objectReferenceValue != styleLabel)
        {
            styleProperty.objectReferenceValue = styleLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
        Selection.activeGameObject = template == null ? board.Find("MagnetStyleTemplate").gameObject : template.gameObject;
    }

    private static void AddHeaderBadges(CategoryHolder holder)
    {
        Color[] colors = { new Color(0.27f, 0.79f, 0.85f), new Color(1f, 0.79f, 0.29f),
            new Color(0.69f, 0.48f, 0.87f), new Color(0.48f, 0.83f, 0.48f) };
        var title = holder.transform.Find("CategoryLabel") as RectTransform;
        var count = holder.transform.Find("Count") as RectTransform;
        if (title == null || count == null) return;
        bool firstSetup = holder.transform.Find("CategoryBadge") == null;
        var badge = Badge("CategoryBadge", holder.transform, colors[holder.transform.GetSiblingIndex() % 4]);
        SetAnchors(badge.rectTransform, new Vector2(0.02f, 1.04f), new Vector2(0.98f, 1.23f));
        SetAnchors(title, new Vector2(0.06f, 1.04f), new Vector2(0.94f, 1.23f));
        var counter = Badge("CountBadge", holder.transform, new Color(0.28f, 0.24f, 0.19f));
        SetAnchors(counter.rectTransform, new Vector2(0.22f, 0.95f), new Vector2(0.78f, 1.065f));
        SetAnchors(count, new Vector2(0.23f, 0.95f), new Vector2(0.77f, 1.065f));
        badge.transform.SetAsLastSibling();
        title.SetAsLastSibling();
        counter.transform.SetAsLastSibling();
        count.SetAsLastSibling();
        if (firstSetup)
        {
            var label = title.GetComponent<TextMeshProUGUI>();
            label.fontSize = label.fontSizeMax = 28f;
            label.fontSizeMin = 18f;
            label.enableAutoSizing = true;
        }
    }

    private static Image Badge(string name, Transform parent, Color color)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Image>();
        var image = Rect(name, parent).AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        var shadow = image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(0f, -3f);
        return image;
    }

    private static CategoryHolder CreateHolder(RectTransform parent, int index)
    {
        GameObject holderObject = Rect($"CategoryHolder {index + 1}", parent);
        RectTransform rect = holderObject.GetComponent<RectTransform>();
        float left = index * 0.25f + 0.01f;
        // Slightly taller holders create room for the four-card stack while
        // keeping a small safe gap above the lower magnet area.
        SetAnchors(rect, new Vector2(left, 0.02f), new Vector2(left + 0.23f, 0.91f));
        Image holderImage = holderObject.AddComponent<Image>();
        holderImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HolderSpritePath);
        holderImage.type = holderImage.sprite != null && holderImage.sprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced : Image.Type.Simple;

        RectTransform content = Rect("Content", rect).GetComponent<RectTransform>();
        Stretch(content);

        Image highlight = Rect("DropHighlight", rect).AddComponent<Image>();
        Stretch(highlight.rectTransform);
        highlight.raycastTarget = false;
        // Runtime tints the holder itself, so this legacy child remains inert.
        // Keeping it inside the holder prevents old scene instances from
        // showing any overflow if manually enabled in the editor.
        highlight.rectTransform.offsetMin = Vector2.one * 6f;
        highlight.rectTransform.offsetMax = Vector2.one * -6f;
        highlight.color = Color.clear;
        highlight.gameObject.SetActive(false);

        TextMeshProUGUI category = Text("CategoryLabel", rect, "CATEGORY", 25f);
        category.rectTransform.anchorMin = new Vector2(-0.04f, 1.01f);
        category.rectTransform.anchorMax = new Vector2(1.04f, 1.22f);
        category.rectTransform.offsetMin = category.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI count = Text("Count", rect, "0/4", 22f);
        count.rectTransform.anchorMin = new Vector2(0.25f, 0.04f);
        count.rectTransform.anchorMax = new Vector2(0.75f, 0.22f);
        count.rectTransform.offsetMin = count.rectTransform.offsetMax = Vector2.zero;

        CategoryHolder holder = holderObject.AddComponent<CategoryHolder>();
        SerializedObject serialized = new SerializedObject(holder);
        serialized.FindProperty("content").objectReferenceValue = content;
        serialized.FindProperty("categoryLabel").objectReferenceValue = category;
        serialized.FindProperty("countLabel").objectReferenceValue = count;
        serialized.FindProperty("highlight").objectReferenceValue = highlight;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return holder;
    }

    private static GameObject Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build category magnet board");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Resources/Fonts/Fredoka-Variable SDF.asset");
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.18f, 0.08f, 0.22f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
#endif
