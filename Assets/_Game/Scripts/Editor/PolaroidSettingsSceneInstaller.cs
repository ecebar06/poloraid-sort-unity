#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the supplied Polaroid settings artwork once. Afterwards every
/// RectTransform remains Scene-authored and can be adjusted normally.
/// </summary>
internal static class PolaroidSettingsSceneInstaller
{
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string ArtRoot = "Assets/_Game/Art/PolaroidTheme/UI/settings";
    private const string MarkerName = "PolaroidSettingsLayoutV1";

    [InitializeOnLoadMethod]
    private static void ScheduleInstall()
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

    [MenuItem("Tools/Word Game/Apply Polaroid Settings Layout")]
    private static void ApplyFromMenu() => Apply(true);

    private static void InstallIfNeeded() => Apply(false);

    private static void Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        Transform screen = Find(scene, "SettingsScreen");
        if (screen == null)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        // Persist the popup canvas in the Scene as well as creating it at
        // runtime. Category ribbons use their own sorting canvases, so without
        // this the ribbons can appear above the settings dimmer in Scene view.
        // This changes no popup or ribbon RectTransform values.
        bool sortingChanged = EnsurePopupSorting(screen.parent);
        if (!force && FindWithin(screen, MarkerName) != null)
        {
            if (sortingChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        ConfigureScreen(screen);
        AssignRuntimeSprites(scene);

        if (FindWithin(screen, MarkerName) == null)
        {
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(screen, false);
            marker.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Polaroid settings screen layout applied. Scene RectTransforms remain editable.");
    }

    private static bool EnsurePopupSorting(Transform popupRoot)
    {
        if (popupRoot == null) return false;
        bool changed = false;
        Canvas canvas = popupRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = popupRoot.gameObject.AddComponent<Canvas>();
            changed = true;
        }
        if (!canvas.overrideSorting)
        {
            canvas.overrideSorting = true;
            changed = true;
        }
        if (canvas.sortingOrder != 2000)
        {
            canvas.sortingOrder = 2000;
            changed = true;
        }
        if (popupRoot.GetComponent<GraphicRaycaster>() == null)
        {
            popupRoot.gameObject.AddComponent<GraphicRaycaster>();
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(popupRoot.gameObject);
        return changed;
    }

    private static void ConfigureScreen(Transform screen)
    {
        RectTransform screenRect = screen as RectTransform;
        Stretch(screenRect);
        Image dimmer = screen.GetComponent<Image>();
        if (dimmer != null)
        {
            dimmer.color = new Color(0f, 0f, 0f, 0.72f);
            dimmer.raycastTarget = true;
        }

        RectTransform panel = screen.Find("Panel") as RectTransform;
        if (panel == null) return;
        Place(panel, Vector2.zero, new Vector2(820f, 900f));

        RectTransform body = panel.Find("Body") as RectTransform;
        if (body != null)
        {
            Stretch(body);
            StyleImage(body, "popup_bg.png", false, false);
            body.SetAsFirstSibling();
        }

        Transform goldDisplay = panel.Find("GoldDisplay");
        if (goldDisplay != null) goldDisplay.gameObject.SetActive(false);

        ConfigureHeader(panel);
        ConfigureClose(panel);
        ConfigureWideButton(panel.Find("RestartGroup") as RectTransform, "orange_rectangle.png",
            new Vector2(0f, 165f), new Vector2(390f, 166f), "restart", 62f);
        ConfigureContinue(panel);
        ConfigureOptions(panel);
        ConfigureFooter(panel);
    }

    private static void ConfigureHeader(RectTransform panel)
    {
        RectTransform header = panel.Find("HeaderGroup") as RectTransform;
        if (header == null) return;
        Place(header, new Vector2(0f, 350f), new Vector2(470f, 120f));
        RectTransform background = header.Find("Background") as RectTransform;
        if (background != null)
        {
            Stretch(background);
            Image image = background.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = Color.clear;
                image.raycastTarget = false;
            }
        }
        TextMeshProUGUI title = header.GetComponentInChildren<TextMeshProUGUI>(true);
        StyleText(title, "settings", 92f, new Color32(137, 74, 34, 255));
        if (title != null) Stretch(title.rectTransform);
    }

    private static void ConfigureClose(RectTransform panel)
    {
        RectTransform close = panel.Find("CloseButton") as RectTransform;
        if (close == null) return;
        Place(close, new Vector2(325f, 350f), new Vector2(100f, 102f));
        Image image = StyleImage(close, "buttons/exit.png", true, true);
        EnsureButton(close, image);
    }

    private static void ConfigureContinue(RectTransform panel)
    {
        RectTransform rect = panel.Find("ContinueButton") as RectTransform;
        if (rect == null)
        {
            GameObject go = new GameObject("ContinueButton", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
        }
        Place(rect, new Vector2(0f, -15f), new Vector2(350f, 149f));
        Image image = StyleImage(rect, "buttons/green_rectangle.png", false, true);
        EnsureButton(rect, image);
        TextMeshProUGUI label = EnsureText(rect, "Label");
        StyleText(label, "continue", 57f, Color.white);
        Stretch(label.rectTransform, 20f);
    }

    private static void ConfigureWideButton(RectTransform group, string spritePath, Vector2 position,
        Vector2 size, string caption, float fontSize)
    {
        if (group == null) return;
        Place(group, position, size);
        RectTransform background = group.Find("Background") as RectTransform;
        if (background != null)
        {
            Stretch(background);
            Image image = StyleImage(background, "buttons/" + spritePath, false, true);
            EnsureButton(background, image);
        }
        TextMeshProUGUI label = group.Find("Label")?.GetComponent<TextMeshProUGUI>() ?? EnsureText(group, "Label");
        StyleText(label, caption, fontSize, Color.white);
        Stretch(label.rectTransform, 22f);
    }

    private static void ConfigureOptions(RectTransform panel)
    {
        RectTransform options = panel.Find("Options") as RectTransform;
        if (options == null) return;
        Place(options, new Vector2(0f, -235f), new Vector2(670f, 190f));
        ConfigureOption(options, "SoundGroup", "buttons/sound.png", -230f);
        ConfigureOption(options, "HapticGroup", "buttons/vibration_on.png", 0f);
        ConfigureOption(options, "MusicGroup", "buttons/music_on.png", 230f);
    }

    private static void ConfigureOption(RectTransform options, string name, string spritePath, float x)
    {
        RectTransform group = options.Find(name) as RectTransform;
        if (group == null) return;
        Place(group, new Vector2(x, 0f), new Vector2(180f, 180f));
        RectTransform background = group.Find("Background") as RectTransform;
        if (background != null)
        {
            Stretch(background);
            Image image = StyleImage(background, spritePath, true, true);
            EnsureButton(background, image);
        }
        Transform legacyIcon = group.Find("Icon");
        if (legacyIcon != null) legacyIcon.gameObject.SetActive(false);
        Transform legacyLabel = group.Find("Label");
        if (legacyLabel != null) legacyLabel.gameObject.SetActive(false);
    }

    private static void ConfigureFooter(RectTransform panel)
    {
        TextMeshProUGUI footer = EnsureText(panel, "TermsText");
        StyleText(footer, "terms conditions etc.", 34f, new Color32(137, 74, 34, 255));
        Place(footer.rectTransform, new Vector2(0f, -375f), new Vector2(600f, 65f));
    }

    private static void AssignRuntimeSprites(Scene scene)
    {
        MagnetPopupUI popup = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MagnetPopupUI>(true)).FirstOrDefault();
        if (popup == null) return;
        SerializedObject serialized = new SerializedObject(popup);
        SetSprite(serialized, "settingsSoundOn", "buttons/sound.png");
        SetSprite(serialized, "settingsSoundOff", "buttons/sound_off.png");
        SetSprite(serialized, "settingsMusicOn", "buttons/music_on.png");
        SetSprite(serialized, "settingsMusicOff", "buttons/music_off.png");
        SetSprite(serialized, "settingsHapticOn", "buttons/vibration_on.png");
        SetSprite(serialized, "settingsHapticOff", "buttons/vibration_off.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(popup);
    }

    private static void SetSprite(SerializedObject serialized, string property, string relativePath)
    {
        SerializedProperty value = serialized.FindProperty(property);
        if (value != null) value.objectReferenceValue = Sprite(relativePath);
    }

    private static Image StyleImage(RectTransform rect, string relativePath, bool preserveAspect, bool raycast)
    {
        if (rect == null) return null;
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = Sprite(relativePath);
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.raycastTarget = raycast;
        return image;
    }

    private static void EnsureButton(RectTransform rect, Image target)
    {
        Button button = rect.GetComponent<Button>();
        if (button == null) button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static TextMeshProUGUI EnsureText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI text)) return text;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        return go.GetComponent<TextMeshProUGUI>();
    }

    private static void StyleText(TextMeshProUGUI text, string value, float size, Color color)
    {
        if (text == null) return;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (font != null) text.font = font;
        text.text = value;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.62f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static Sprite Sprite(string relativePath) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{relativePath}");

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
