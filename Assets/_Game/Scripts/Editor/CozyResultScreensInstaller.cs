#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time, scene-authored setup for the Cozy Stationery out-of-moves and
/// level-complete screens. After the marker is written, every RectTransform
/// remains editable in Game.unity and is not rewritten on future reloads.
/// </summary>
[InitializeOnLoad]
internal static class CozyResultScreensInstaller
{
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string ArtRoot = "Assets/_Game/Art/CozyStationery";
    private const string MarkerName = "CozyResultsLayoutV1";
    private static readonly Color Cocoa = new Color32(89, 62, 48, 255);
    private static readonly Color Cream = new Color32(255, 248, 229, 255);

    static CozyResultScreensInstaller()
    {
        EditorApplication.delayCall += InstallIfNeeded;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/Word Game/Rebuild Cozy Result Screens")]
    private static void RebuildFromMenu() => Apply(true);

    private static void InstallIfNeeded() => Apply(false);

    private static void Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        AssetDatabase.Refresh();
        ImportSprites();

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        Transform timeUp = Find(scene, "TimeUpScreen");
        Transform win = Find(scene, "WinScreen");
        if (timeUp == null || win == null)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        bool alreadyInstalled = timeUp.Find(MarkerName) != null && win.Find(MarkerName) != null;
        if (!force && alreadyInstalled)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        BuildOutOfMoves(timeUp);
        BuildWin(win);
        AssignRuntimeReferences(scene, timeUp, win);
        timeUp.gameObject.SetActive(false);
        win.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Cozy Stationery Out of Moves and Win screens installed. Scene layout remains editable.");
    }

    private static void BuildOutOfMoves(Transform screen)
    {
        ClearChildren(screen);
        ConfigureScreenRoot(screen);

        CreateGoldHud(screen, "1,200");
        RectTransform panel = ImageRect("Panel", screen, "shared/panels/popup-cream.png", Image.Type.Sliced);
        Place(panel, new Vector2(0f, -110f), new Vector2(880f, 1120f));

        RectTransform clip = ImageRect("Clip", panel, "shared/decoration/clip-pink.png");
        Place(clip, new Vector2(0f, 565f), new Vector2(180f, 230f));
        clip.GetComponent<Image>().preserveAspect = true;

        Text("Title", panel, "Out of moves", 80f, new Vector2(0f, 430f), new Vector2(720f, 110f), Cream, true);
        Text("Explanation", panel, "A few more moves?", 42f, new Vector2(0f, 355f), new Vector2(650f, 70f), Cocoa);

        RectTransform hero = ImageRect("Hero", panel, "screens/out-of-moves/illustration-more-moves.png");
        Place(hero, new Vector2(0f, 150f), new Vector2(470f, 320f));
        hero.GetComponent<Image>().preserveAspect = true;

        RectTransform goldGroup = Rect("GoldGroup", panel);
        Place(goldGroup, new Vector2(0f, -115f), new Vector2(710f, 150f));
        Button goldButton = ButtonSurface(goldGroup, "button-sage", out _);
        Text("Label", goldGroup, "+5 moves", 50f, new Vector2(0f, 25f), new Vector2(520f, 62f), Cocoa);
        RectTransform priceLine = Rect("PriceLine", goldGroup);
        Place(priceLine, new Vector2(0f, -31f), new Vector2(360f, 50f));
        RectTransform priceCoin = ImageRect("Coin", priceLine, "shared/icons/icon-coin.png");
        Place(priceCoin, new Vector2(-104f, 0f), new Vector2(46f, 46f));
        priceCoin.GetComponent<Image>().preserveAspect = true;
        Text("Price", goldGroup, "900 coins", 33f, new Vector2(28f, -31f), new Vector2(300f, 48f), Cocoa);

        RectTransform watchGroup = Rect("WatchAdGroup", panel);
        Place(watchGroup, new Vector2(0f, -300f), new Vector2(710f, 150f));
        Button watchButton = ButtonSurface(watchGroup, "button-blush", out _);
        RectTransform video = ImageRect("MovieIcon", watchGroup, "shared/icons/icon-video.png");
        Place(video, new Vector2(-190f, 9f), new Vector2(76f, 76f));
        video.GetComponent<Image>().preserveAspect = true;
        Text("Label", watchGroup, "Watch ad", 48f, new Vector2(45f, 24f), new Vector2(430f, 58f), Cocoa);
        Text("Detail", watchGroup, "+5 moves", 30f, new Vector2(45f, -30f), new Vector2(430f, 44f), Cocoa);

        RectTransform retryGroup = Rect("TryAgainGroup", panel);
        Place(retryGroup, new Vector2(0f, -467f), new Vector2(650f, 118f));
        Button retryButton = ButtonSurface(retryGroup, "button-cream", out _);
        RectTransform retry = ImageRect("RestartIcon", retryGroup, "shared/icons/icon-restart.png");
        Place(retry, new Vector2(-145f, 1f), new Vector2(65f, 65f));
        retry.GetComponent<Image>().preserveAspect = true;
        Text("Label", retryGroup, "Try again", 45f, new Vector2(55f, 2f), new Vector2(370f, 64f), Cocoa);

        Mark(screen);
        SetButtonName(goldButton, "Background");
        SetButtonName(watchButton, "Background");
        SetButtonName(retryButton, "Background");
    }

    private static void BuildWin(Transform screen)
    {
        ClearChildren(screen);
        ConfigureScreenRoot(screen);
        EnsureCanvasGroup(screen.gameObject);

        CreateGoldHud(screen, "1,240");
        RectTransform background = ImageRect("Background", screen, "shared/panels/popup-cream.png", Image.Type.Sliced);
        Place(background, new Vector2(0f, -45f), new Vector2(860f, 990f));

        RectTransform content = Rect("Content", screen);
        Place(content, background.anchoredPosition, background.sizeDelta);

        RectTransform clip = ImageRect("Clip", content, "shared/decoration/clip-pink.png");
        Place(clip, new Vector2(0f, 505f), new Vector2(180f, 230f));
        clip.GetComponent<Image>().preserveAspect = true;

        RectTransform confetti = ImageRect("Confetti", content, "screens/level-complete/confetti-overlay.png");
        Place(confetti, new Vector2(0f, 65f), new Vector2(720f, 560f));
        confetti.GetComponent<Image>().preserveAspect = true;

        Text("Title", content, "Well sorted!", 82f, new Vector2(0f, 370f), new Vector2(720f, 110f), Cream, true);
        Text("LevelText", content, "Level 1 complete", 42f, new Vector2(0f, 295f), new Vector2(650f, 65f), Cocoa);

        RectTransform hero = ImageRect("Hero", content, "screens/level-complete/illustration-checklist.png");
        Place(hero, new Vector2(0f, 100f), new Vector2(460f, 330f));
        hero.GetComponent<Image>().preserveAspect = true;

        Text("RewardCaption", content, "Your reward", 38f, new Vector2(0f, -98f), new Vector2(520f, 60f), Cocoa);
        RectTransform reward = Rect("Reward", content);
        Place(reward, new Vector2(0f, -205f), new Vector2(650f, 120f));
        RectTransform coins = ImageRect("Coins", reward, "screens/level-complete/reward-coins.png");
        Place(coins, new Vector2(-190f, 0f), new Vector2(180f, 120f));
        coins.GetComponent<Image>().preserveAspect = true;
        Text("Value", reward, "+40 coins", 60f, new Vector2(95f, 0f), new Vector2(430f, 100f), Cocoa);

        RectTransform continueRect = ImageRect("ContinueButton", content, "shared/buttons/button-sage-normal.png", Image.Type.Sliced);
        Place(continueRect, new Vector2(0f, -370f), new Vector2(690f, 145f));
        Image continueImage = continueRect.GetComponent<Image>();
        continueImage.raycastTarget = true;
        Button continueButton = continueRect.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = continueImage;
        continueButton.transition = Selectable.Transition.SpriteSwap;
        SpriteState continueState = continueButton.spriteState;
        continueState.pressedSprite = Sprite("shared/buttons/button-sage-pressed.png");
        continueState.disabledSprite = Sprite("shared/buttons/button-sage-disabled.png");
        continueButton.spriteState = continueState;
        Text("Label", continueRect, "Next level", 51f, new Vector2(-35f, 4f), new Vector2(500f, 72f), Cocoa);
        RectTransform arrow = ImageRect("Arrow", continueRect, "shared/icons/icon-arrow-right.png");
        Place(arrow, new Vector2(245f, 4f), new Vector2(62f, 62f));
        arrow.GetComponent<Image>().preserveAspect = true;

        Mark(screen);
    }

    private static void ConfigureScreenRoot(Transform screen)
    {
        Stretch(screen as RectTransform);
        Image dimmer = screen.GetComponent<Image>();
        if (dimmer == null) dimmer = screen.gameObject.AddComponent<Image>();
        dimmer.sprite = Sprite("shared/effects/dimmer-warm.png");
        dimmer.type = Image.Type.Simple;
        dimmer.preserveAspect = false;
        dimmer.color = Color.white;
        dimmer.raycastTarget = true;
    }

    private static RectTransform CreateGoldHud(Transform screen, string value)
    {
        RectTransform gold = ImageRect("Gold", screen, "shared/panels/gold-balance-panel.png", Image.Type.Sliced);
        gold.anchorMin = gold.anchorMax = new Vector2(0f, 1f);
        gold.pivot = new Vector2(0f, 1f);
        gold.anchoredPosition = new Vector2(24f, -28f);
        gold.sizeDelta = new Vector2(292f, 110f);
        RectTransform icon = ImageRect("Icon", gold, "shared/icons/icon-coin.png");
        Place(icon, new Vector2(-88f, 3f), new Vector2(82f, 82f));
        icon.GetComponent<Image>().preserveAspect = true;
        Text("Value", gold, value, 48f, new Vector2(43f, 3f), new Vector2(170f, 72f), Cocoa);
        return gold;
    }

    private static Button ButtonSurface(RectTransform group, string family, out Image image)
    {
        RectTransform surface = ImageRect("Background", group, $"shared/buttons/{family}-normal.png", Image.Type.Sliced);
        Stretch(surface);
        image = surface.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = surface.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        SpriteState state = button.spriteState;
        state.pressedSprite = Sprite($"shared/buttons/{family}-pressed.png");
        state.disabledSprite = Sprite($"shared/buttons/{family}-disabled.png");
        button.spriteState = state;
        return button;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size,
        Vector2 position, Vector2 dimensions, Color color, bool outline = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        Place(text.rectTransform, position, dimensions);
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Resources/Fonts/Fredoka-Variable SDF.asset")
                    ?? TMP_Settings.defaultFontAsset;
        text.text = value;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.60f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        if (outline)
        {
            text.outlineColor = Cocoa;
            text.outlineWidth = 0.22f;
            text.extraPadding = true;
        }
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static RectTransform ImageRect(string name, Transform parent, string relativePath,
        Image.Type type = Image.Type.Simple)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = Sprite(relativePath);
        image.type = type;
        image.color = Color.white;
        image.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private static void AssignRuntimeReferences(Scene scene, Transform timeUp, Transform win)
    {
        MagnetPopupUI popup = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MagnetPopupUI>(true)).FirstOrDefault();
        if (popup != null)
        {
            SerializedObject serialized = new SerializedObject(popup);
            SetObject(serialized, "timeUpScreen", timeUp.gameObject);
            SetObject(serialized, "watchAdButton", timeUp.Find("Panel/WatchAdGroup/Background")?.GetComponent<Button>());
            SetObject(serialized, "spendGoldButton", timeUp.Find("Panel/GoldGroup/Background")?.GetComponent<Button>());
            SetObject(serialized, "tryAgainButton", timeUp.Find("Panel/TryAgainGroup/Background")?.GetComponent<Button>());
            SetObject(serialized, "explanationText", timeUp.Find("Panel/Explanation")?.GetComponent<TextMeshProUGUI>());
            SetObject(serialized, "goldPriceText", timeUp.Find("Panel/GoldGroup/Price")?.GetComponent<TextMeshProUGUI>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(popup);
        }

        CategoryMagnetBoardController board = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CategoryMagnetBoardController>(true)).FirstOrDefault();
        if (board != null)
        {
            SerializedObject serialized = new SerializedObject(board);
            SetObject(serialized, "winScreen", win.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);
        }
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void Mark(Transform parent)
    {
        GameObject marker = new GameObject(MarkerName);
        marker.transform.SetParent(parent, false);
        marker.SetActive(false);
    }

    private static void SetButtonName(Button button, string name)
    {
        if (button != null) button.gameObject.name = name;
    }

    private static void EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private static void ClearChildren(Transform root)
    {
        for (int index = root.childCount - 1; index >= 0; index--)
            Object.DestroyImmediate(root.GetChild(index).gameObject);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static Sprite Sprite(string relativePath) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{relativePath}");

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeep(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDeep(root.GetChild(index), name);
            if (found != null) return found;
        }
        return null;
    }

    private static void ImportSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            Vector4 border = BorderFor(path);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.mipmapEnabled || importer.alphaIsTransparency == false ||
                           importer.wrapMode != TextureWrapMode.Clamp ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed ||
                           importer.spritePixelsPerUnit != 100f || importer.spriteBorder != border;
            if (!changed) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    private static Vector4 BorderFor(string path)
    {
        if (path.EndsWith("popup-cream.png")) return new Vector4(144f, 160f, 144f, 144f);
        if (path.EndsWith("gold-balance-panel.png")) return new Vector4(128f, 0f, 128f, 0f);
        if (path.Contains("/buttons/")) return new Vector4(130f, 72f, 130f, 72f);
        return Vector4.zero;
    }
}
#endif
