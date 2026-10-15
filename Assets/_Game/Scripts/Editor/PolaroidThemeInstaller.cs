#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PolaroidThemeInstaller
{
    private const string Root = "Assets/_Game/Art/PolaroidTheme";
    private const string GameScene = "Assets/_Game/Scenes/Game.unity";
    private const string SplashScene = "Assets/_Game/Scenes/Splash.unity";
    private const string WordPrefab = "Assets/_Game/Prefabs/Gameplay/WordButtonPrefab.prefab";
    private const string EditableSlotSizeMarker = "PolaroidEditableSlotSize236x182V1";

    [InitializeOnLoadMethod]
    private static void ScheduleFirstInstall()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= ApplyWhenEditMode;
        EditorApplication.update += ApplyWhenEditMode;
    }

    private static void ApplyWhenEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= ApplyWhenEditMode;
        EditorApplication.delayCall += ApplyIfNeeded;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyIfNeeded;
    }

    private static void ApplyIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) == null) return;
        Scene scene = SceneManager.GetSceneByPath(GameScene);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
        bool alreadyInstalled = Find(scene, "PolaroidThemeSceneLayoutV8") != null;
        bool editableSlotsSized = Find(scene, EditableSlotSizeMarker) != null;
        if (opened) EditorSceneManager.CloseScene(scene, true);
        if (!alreadyInstalled) EditorApplication.delayCall += ApplyTheme;
        if (!editableSlotsSized) EditorApplication.delayCall += ApplyEditableSlotSizes;
    }

    private static void ApplyEditableSlotSizes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(GameScene);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
        if (Find(scene, EditableSlotSizeMarker) != null)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        float[] centers = { 0.875f, 0.625f, 0.375f, 0.125f };
        foreach (CategoryHolder holder in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<CategoryHolder>(true)))
        {
            Transform content = holder.transform.Find("Content");
            if (content == null) continue;
            for (int index = 0; index < centers.Length; index++)
            {
                RectTransform slot = content.Find($"Slot {index + 1}") as RectTransform;
                if (slot == null) continue;
                Vector2 anchor = new Vector2(0.5f, centers[index]);
                slot.anchorMin = anchor;
                slot.anchorMax = anchor;
                slot.anchoredPosition = Vector2.zero;
                slot.sizeDelta = new Vector2(236.099f, 182.2816f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.localRotation = Quaternion.identity;
                slot.localScale = Vector3.one;
            }
        }

        Canvas canvas = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
        if (canvas != null)
        {
            GameObject marker = new GameObject(EditableSlotSizeMarker);
            marker.transform.SetParent(canvas.transform, false);
            marker.SetActive(false);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    [MenuItem("Tools/Word Game/Apply Polaroid Theme")]
    public static void ApplyTheme()
    {
        ImportSprites();
        ApplyGameScene();
        ApplySplashScene();
        ApplyWordPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("Polaroid Sort theme applied to the game, splash, settings and word-card prefab.");
    }

    public static void ApplyThemeBatch()
    {
        ApplyTheme();
    }

    private static void ImportSprites()
    {
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { Root })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 4096;
            if (changed) importer.SaveAndReimport();
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ApplyGameScene()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScene);
        bool openedForInstall = !scene.IsValid() || !scene.isLoaded;
        if (openedForInstall) scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
        Canvas canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
        if (canvas == null) throw new InvalidOperationException("Game scene canvas was not found.");

        Transform oldBackground = Find(scene, "Background");
        if (oldBackground != null) oldBackground.gameObject.SetActive(false);
        SetActive(scene, "background", false);
        SetActive(scene, "StaticFridgeSceneV2", false);
        SetActive(scene, "BubbleLeft", false);
        SetActive(scene, "BubbleRight", false);
        SetActive(scene, "RibbonLeft", false);
        SetActive(scene, "RibbonRight", false);

        Image background = EnsureImage(canvas.transform, "PolaroidThemeBackground");
        Stretch(background.rectTransform);
        background.sprite = Sprite("game_art/background.png");
        background.color = Color.white;
        background.preserveAspect = false;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();

        Transform board = Find(scene, "CategoryMagnetBoard");
        if (board is RectTransform boardRect) Stretch(boardRect);

        Transform holderArea = Find(scene, "CategoryHolders");
        if (holderArea is RectTransform holderAreaRect)
        {
            holderAreaRect.anchorMin = new Vector2(0.015f, 0.54f);
            holderAreaRect.anchorMax = new Vector2(0.985f, 0.84f);
            Zero(holderAreaRect);
            foreach (LayoutGroup layout in holderArea.GetComponents<LayoutGroup>()) layout.enabled = false;
            ConfigureHolders(holderArea);

            Image rope = EnsureImage(holderArea.parent, "PolaroidRope");
            rope.sprite = Sprite("game_art/rope.png");
            rope.color = Color.white;
            rope.preserveAspect = false;
            rope.raycastTarget = false;
            rope.rectTransform.anchorMin = new Vector2(0.01f, 0.825f);
            rope.rectTransform.anchorMax = new Vector2(0.99f, 0.84f);
            Zero(rope.rectTransform);
            rope.transform.SetSiblingIndex(Mathf.Max(0, holderArea.GetSiblingIndex()));
        }

        Transform bottom = Find(scene, "BottomMagnetArea");
        if (bottom is RectTransform bottomRect)
        {
            bottomRect.anchorMin = new Vector2(0.04f, 0.15f);
            bottomRect.anchorMax = new Vector2(0.96f, 0.43f);
            Zero(bottomRect);
        }

        Transform printerParent = board != null ? board : canvas.transform;
        Image printer = EnsureImage(printerParent, "PolaroidPrinter");
        printer.sprite = Sprite("game_art/printer.png");
        printer.color = Color.white;
        printer.preserveAspect = true;
        printer.raycastTarget = false;
        printer.rectTransform.anchorMin = new Vector2(0.35f, 0.43f);
        printer.rectTransform.anchorMax = new Vector2(0.65f, 0.535f);
        Zero(printer.rectTransform);

        StyleTopHud(scene);
        StyleSettings(scene);
        StyleMagnetTemplate(scene);
        StyleBottomButtons(scene);

        if (Find(scene, "PolaroidThemeSceneLayoutV8") == null)
        {
            GameObject marker = new GameObject("PolaroidThemeSceneLayoutV8");
            marker.transform.SetParent(canvas.transform, false);
            marker.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForInstall) EditorSceneManager.CloseScene(scene, true);
    }

    private static void ConfigureHolders(Transform holderArea)
    {
        string[] spriteNames =
        {
            "game_art/frames and pegs/4x1_polaroid_orange.png",
            "game_art/frames and pegs/4x1_polaroid_blue.png",
            "game_art/frames and pegs/4x1_polaroid_purple.png",
            "game_art/frames and pegs/4x1_polaroid_green.png"
        };
        string[] pegSpriteNames =
        {
            "game_art/frames and pegs/peg_orange.png",
            "game_art/frames and pegs/peg_blue.png",
            "game_art/frames and pegs/peg_purple.png",
            "game_art/frames and pegs/peg_green.png"
        };
        List<CategoryHolder> holders = holderArea.GetComponentsInChildren<CategoryHolder>(true)
            .OrderBy(x => x.name).Take(4).ToList();
        const float gap = 0.012f;
        float width = (1f - gap * 5f) / 4f;
        for (int i = 0; i < holders.Count; i++)
        {
            CategoryHolder holder = holders[i];
            Image image = holder.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = Sprite(spriteNames[i]);
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.preserveAspect = false;
            }
            if (holder.transform is RectTransform rect)
            {
                float left = gap + i * (width + gap);
                rect.anchorMin = new Vector2(left, 0f);
                rect.anchorMax = new Vector2(left + width, 1f);
                Zero(rect);
            }

            Image peg = EnsureImage(holder.transform, "PolaroidPeg");
            peg.sprite = Sprite(pegSpriteNames[i]);
            peg.type = Image.Type.Simple;
            peg.color = Color.white;
            peg.preserveAspect = true;
            peg.raycastTarget = false;
            peg.rectTransform.anchorMin = new Vector2(0.30f, 0.91f);
            peg.rectTransform.anchorMax = new Vector2(0.70f, 1.13f);
            Zero(peg.rectTransform);
            peg.transform.SetAsLastSibling();

            Transform content = holder.transform.Find("Content");
            if (content != null)
            {
                // Calibrated to the four cream openings in the supplied
                // 4x1 Polaroid holder artwork, from top to bottom.
                float[] slotCenters = { 0.875f, 0.625f, 0.375f, 0.125f };
                for (int slotIndex = 0; slotIndex < slotCenters.Length; slotIndex++)
                {
                    string slotName = $"Slot {slotIndex + 1}";
                    Transform existingSlot = content.Find(slotName);
                    RectTransform slot = existingSlot as RectTransform;
                    bool created = false;
                    if (slot == null)
                    {
                        GameObject slotObject = new GameObject(slotName, typeof(RectTransform));
                        slotObject.transform.SetParent(content, false);
                        slot = slotObject.GetComponent<RectTransform>();
                        created = true;
                    }
                    // Existing slots belong to the Scene designer. Defaults
                    // are applied only when a slot is first created.
                    if (created)
                    {
                        float center = slotCenters[slotIndex];
                        slot.anchorMin = new Vector2(0.04f, center - 0.115f);
                        slot.anchorMax = new Vector2(0.96f, center + 0.115f);
                        Zero(slot);
                    }
                }
            }
            SetActive(holder.gameObject.scene, "DropHighlight", false);
        }
    }

    private static void StyleTopHud(Scene scene)
    {
        SetActive(scene, "FridgeTopHud", false);
        SetActive(scene, "TopBar", false);
        SetActive(scene, "GoldDisplay", false);
        SetActive(scene, "LevelRibbon", false);

        Canvas canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).First();
        Transform existing = Find(scene, "PolaroidTopHud");
        if (existing != null)
        {
            // The HUD is a scene-authored object after the first install.  Do not
            // recreate it or touch any RectTransforms here: artists must be able
            // to adjust its layout directly in the Scene/Inspector.
            BindTopHud(scene, existing);
            return;
        }

        GameObject hudObject = new GameObject("PolaroidTopHud", typeof(RectTransform));
        hudObject.transform.SetParent(canvas.transform, false);
        RectTransform hud = hudObject.GetComponent<RectTransform>();
        hud.anchorMin = new Vector2(0f, 0.855f);
        hud.anchorMax = Vector2.one;
        Zero(hud);

        Image moves = ChildImage(hud, "MovesCard", "UI/top_ui/move_counter.png",
            new Vector2(0.02f, 0.18f), new Vector2(0.20f, 0.94f));
        TextMeshProUGUI movesTitle = ChildText(moves.transform, "MovesTitle", "moves", 25f,
            new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.96f));
        TextMeshProUGUI moveCount = ChildText(moves.transform, "MoveCount", "0", 34f,
            new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.60f));

        Image level = ChildImage(hud, "LevelCard", "UI/top_ui/level_bar.png",
            new Vector2(0.25f, 0.57f), new Vector2(0.75f, 0.98f));
        TextMeshProUGUI levelLabel = ChildText(level.transform, "LevelLabel", "level 1", 31f,
            new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.98f));

        Image progressTrack = ChildImage(hud, "ProgressTrack", "UI/top_ui/progress_bar.png",
            new Vector2(0.25f, 0.16f), new Vector2(0.75f, 0.51f));
        Image fill = ChildImage(progressTrack.transform, "Fill", "UI/top_ui/progresss_green.png",
            new Vector2(0.035f, 0.19f), new Vector2(0.965f, 0.81f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        TextMeshProUGUI progressText = ChildText(progressTrack.transform, "Progress", "0/4", 18f,
            new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.98f));
        progressText.gameObject.SetActive(false);

        Image pause = ChildImage(hud, "SettingsButton", "UI/top_ui/pause_button.png",
            new Vector2(0.82f, 0.18f), new Vector2(0.98f, 0.94f));
        Button pauseButton = pause.gameObject.AddComponent<Button>();
        pauseButton.targetGraphic = pause;

        Color brown = new Color32(117, 65, 36, 255);
        foreach (TextMeshProUGUI text in new[] { movesTitle, moveCount, levelLabel })
        {
            text.color = brown;
            text.fontStyle = FontStyles.Bold;
        }

        BindTopHud(scene, hud);
    }

    private static void BindTopHud(Scene scene, Transform hud)
    {
        TextMeshProUGUI moveCount = FindWithin(hud, "MoveCount")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI levelLabel = FindWithin(hud, "LevelLabel")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI progressText = FindWithin(hud, "Progress")?.GetComponent<TextMeshProUGUI>();
        Image fill = FindWithin(hud, "Fill")?.GetComponent<Image>();

        TableController controller = scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<TableController>(true)).FirstOrDefault();
        if (controller != null)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("moveCountText").objectReferenceValue = moveCount;
            serialized.FindProperty("levelLabel").objectReferenceValue = levelLabel;
            serialized.FindProperty("progressText").objectReferenceValue = progressText;
            serialized.FindProperty("progressFill").objectReferenceValue = fill;
            serialized.FindProperty("progressKnob").objectReferenceValue = null;
            serialized.FindProperty("progressKnobText").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
    }

    private static void StyleSettings(Scene scene)
    {
        Transform screen = Find(scene, "SettingsScreen");
        if (screen == null) return;
        Transform panel = FindWithin(screen, "Panel");
        SetImage(panel, "UI/settings/popup_bg.png");
        SetRect(panel, new Vector2(0.14f, 0.18f), new Vector2(0.86f, 0.82f));
        SetImage(FindWithin(screen, "CloseButton"), "UI/settings/buttons/exit.png");
        SetImage(FindWithin(screen, "RestartGroup"), "UI/settings/buttons/orange_rectangle.png");
        SetImage(FindWithin(screen, "ContinueButton"), "UI/settings/buttons/green_rectangle.png");
        SetImage(FindWithin(screen, "SoundGroup"), "UI/settings/buttons/sound.png");
        SetImage(FindWithin(screen, "HapticGroup"), "UI/settings/buttons/vibration_on.png");
        SetImage(FindWithin(screen, "MusicGroup"), "UI/settings/buttons/music_on.png");
    }

    private static void StyleMagnetTemplate(Scene scene)
    {
        Transform template = Find(scene, "MagnetStyleTemplate");
        if (template == null) return;
        Transform paper = FindWithin(template, "Paper") ?? template;
        SetImage(paper, "game_art/polariod_print.png");
        Transform body = FindWithin(template, "Body");
        if (body != null)
        {
            Image bodyImage = body.GetComponent<Image>();
            if (bodyImage != null) bodyImage.color = Color.clear;
        }
    }

    private static void StyleBottomButtons(Scene scene)
    {
        Transform booster = Find(scene, "BoosterArea");
        if (booster == null) return;
        SetRect(booster, new Vector2(0.08f, 0.025f), new Vector2(0.92f, 0.12f));
        Transform hint = FindWithin(booster, "HintButton");
        SetImage(hint, "game_art/blue_square.png");
        SetRect(hint, new Vector2(0.40f, 0f), new Vector2(0.60f, 1f));
        EnsureDecorativeButton(booster, "PolaroidBottomLeft", new Vector2(0.08f, 0f), new Vector2(0.28f, 1f));
        EnsureDecorativeButton(booster, "PolaroidBottomRight", new Vector2(0.72f, 0f), new Vector2(0.92f, 1f));
        SetActive(scene, "HintLabel", false);
    }

    private static void EnsureDecorativeButton(Transform parent, string name, Vector2 min, Vector2 max)
    {
        Image image = EnsureImage(parent, name);
        image.sprite = Sprite("game_art/blue_square.png");
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        Zero(image.rectTransform);
    }

    private static void ApplySplashScene()
    {
        Scene scene = SceneManager.GetSceneByPath(SplashScene);
        bool openedForInstall = !scene.IsValid() || !scene.isLoaded;
        if (openedForInstall) scene = EditorSceneManager.OpenScene(SplashScene, OpenSceneMode.Additive);
        Transform background = Find(scene, "Pastel Background");
        SetImage(background, "splash/splash_background.png");
        if (background is RectTransform backgroundRect) Stretch(backgroundRect);
        Transform logo = Find(scene, "Logo Card");
        SetImage(logo, "splash/title.png");
        SetRect(logo, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.83f));
        SetActive(scene, "Soft Tint", false);
        SetActive(scene, "Title", false);
        SetActive(scene, "Studio", false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForInstall) EditorSceneManager.CloseScene(scene, true);
    }

    private static void ApplyWordPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WordPrefab);
        try
        {
            Image image = root.GetComponent<Image>() ?? root.GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.sprite = Sprite("game_art/polariod_print.png");
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.preserveAspect = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, WordPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Sprite Sprite(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{relative}");

    private static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindWithin(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindWithin(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindWithin(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetActive(Scene scene, string name, bool active)
    {
        Transform transform = Find(scene, name);
        if (transform != null) transform.gameObject.SetActive(active);
    }

    private static void SetImage(Scene scene, string name, string spritePath) => SetImage(Find(scene, name), spritePath);

    private static void SetImage(Transform transform, string spritePath)
    {
        if (transform == null) return;
        Image image = transform.GetComponent<Image>() ?? transform.GetComponentInChildren<Image>(true);
        if (image == null) return;
        image.sprite = Sprite(spritePath);
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;
    }

    private static Image EnsureImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Image image)) return image;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static Image ChildImage(Transform parent, string name, string spritePath, Vector2 min, Vector2 max)
    {
        Image image = EnsureImage(parent, name);
        image.sprite = Sprite(spritePath);
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        Zero(image.rectTransform);
        return image;
    }

    private static TextMeshProUGUI ChildText(Transform parent, string name, string value, float size,
        Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.55f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (font != null) text.font = font;
        text.rectTransform.anchorMin = min;
        text.rectTransform.anchorMax = max;
        Zero(text.rectTransform);
        return text;
    }

    private static void SetRect(Transform transform, Vector2 min, Vector2 max)
    {
        if (!(transform is RectTransform rect)) return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        Zero(rect);
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    private static void Zero(RectTransform rect)
    {
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
#endif
