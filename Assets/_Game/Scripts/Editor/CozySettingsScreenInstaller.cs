#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class CozySettingsScreenInstaller
{
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string ArtRoot = "Assets/_Game/Art/CozyStationery/shared";
    private const string MarkerName = "CozySettingsLayoutV2";
    private static readonly Color Cocoa = new Color32(89, 62, 48, 255);
    private static readonly Color Cream = new Color32(255, 244, 223, 255);

    [InitializeOnLoadMethod]
    private static void ScheduleInstall() => EditorApplication.delayCall += InstallIfNeeded;

    [MenuItem("Tools/Word Game/Apply Cozy Settings Screen")]
    private static void ApplyFromMenu() => Apply(true);

    public static void ApplyBatch() => Apply(true);

    private static void InstallIfNeeded() => Apply(false);

    private static void Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

        ImportArt();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        MagnetPopupUI popup = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MagnetPopupUI>(true))
            .FirstOrDefault();
        Transform screen = popup == null ? Find(scene, "SettingsScreen") : popup.SettingsScreen?.transform;
        if (screen == null)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        if (!force && FindWithin(screen, MarkerName) != null)
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        ConfigureScreen(screen);
        AssignRuntimeSprites(popup);
        if (FindWithin(screen, MarkerName) == null)
        {
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(screen, false);
            marker.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Cozy Settings screen applied from cozy-stationery-v1.1.");
    }

    private static void ConfigureScreen(Transform screen)
    {
        Stretch(screen as RectTransform);
        Image dimmer = GetImage(screen.gameObject);
        dimmer.sprite = null;
        dimmer.type = Image.Type.Simple;
        dimmer.color = new Color(0f, 0f, 0f, 0.78f);
        dimmer.raycastTarget = true;

        RectTransform panel = screen.Find("Panel") as RectTransform;
        if (panel == null) return;
        Place(panel, Vector2.zero, new Vector2(820f, 1120f));

        RectTransform body = GetRect(panel, "Body");
        Stretch(body);
        StyleImage(body, "panels/popup-cream.png", false, false);
        body.SetAsFirstSibling();

        Transform terms = panel.Find("TermsText");
        if (terms != null) terms.gameObject.SetActive(false);

        ConfigureClip(panel);
        ConfigureHeader(panel);
        ConfigureClose(panel);
        ConfigureOptions(panel);
        ConfigureResume(panel);
        ConfigureRestart(panel);
    }

    private static void ConfigureClip(RectTransform panel)
    {
        RectTransform clip = GetRect(panel, "Clip");
        Place(clip, new Vector2(0f, 548f), new Vector2(150f, 205f));
        StyleImage(clip, "decoration/clip-pink.png", true, false);
    }

    private static void ConfigureHeader(RectTransform panel)
    {
        RectTransform header = GetRect(panel, "HeaderGroup");
        Place(header, new Vector2(0f, 405f), new Vector2(650f, 180f));
        Transform oldBackground = header.Find("Background");
        if (oldBackground != null)
        {
            Image image = oldBackground.GetComponent<Image>();
            if (image != null) image.color = Color.clear;
        }

        TextMeshProUGUI title = header.GetComponentInChildren<TextMeshProUGUI>(true) ?? EnsureText(header, "Title");
        StyleText(title, "Settings", 86f, Cream, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0f, 35f), new Vector2(620f, 105f));

        TextMeshProUGUI subtitle = EnsureText(panel, "Subtitle");
        StyleText(subtitle, "Make yourself comfortable", 36f, Cocoa, TextAlignmentOptions.Center, false);
        Place(subtitle.rectTransform, new Vector2(0f, 315f), new Vector2(650f, 62f));
    }

    private static void ConfigureClose(RectTransform panel)
    {
        RectTransform close = GetRect(panel, "CloseButton");
        Place(close, new Vector2(326f, 470f), new Vector2(104f, 104f));
        Image background = StyleImage(close, "buttons/button-round-cream-normal.png", true, true);
        ConfigureButton(close, background, "buttons/button-round-cream-pressed.png", "buttons/button-round-cream-disabled.png");
        RectTransform icon = GetRect(close, "Icon");
        Place(icon, Vector2.zero, new Vector2(54f, 54f));
        StyleImage(icon, "icons/icon-close.png", true, false);
    }

    private static void ConfigureOptions(RectTransform panel)
    {
        RectTransform options = GetRect(panel, "Options");
        Place(options, new Vector2(0f, 65f), new Vector2(700f, 440f));
        ConfigureOption(options, "SoundGroup", "Sound", "icons/icon-sound.png", 145f);
        ConfigureDivider(options, "DividerSound", 72f);
        ConfigureOption(options, "MusicGroup", "Music", "icons/icon-music.png", 0f);
        ConfigureDivider(options, "DividerMusic", -72f);
        ConfigureOption(options, "HapticGroup", "Vibration", "icons/icon-vibration.png", -145f);
    }

    private static void ConfigureOption(RectTransform options, string groupName, string caption,
        string iconPath, float y)
    {
        RectTransform group = GetRect(options, groupName);
        Place(group, new Vector2(0f, y), new Vector2(660f, 130f));

        RectTransform icon = GetRect(group, "Icon");
        Place(icon, new Vector2(-270f, 0f), new Vector2(100f, 100f));
        StyleImage(icon, iconPath, true, false);

        TextMeshProUGUI label = EnsureText(group, "Label");
        StyleText(label, caption, 50f, Cocoa, TextAlignmentOptions.Left, true);
        Place(label.rectTransform, new Vector2(-65f, 0f), new Vector2(310f, 90f));

        RectTransform toggle = GetRect(group, "Background");
        Place(toggle, new Vector2(235f, 0f), new Vector2(190f, 92f));
        Image image = StyleImage(toggle, "toggles/toggle-on.png", true, true);
        ConfigureButton(toggle, image, null, "toggles/toggle-disabled.png");
    }

    private static void ConfigureDivider(RectTransform options, string name, float y)
    {
        RectTransform divider = GetRect(options, name);
        Place(divider, new Vector2(0f, y), new Vector2(610f, 8f));
        StyleImage(divider, "decoration/divider.png", false, false);
    }

    private static void ConfigureResume(RectTransform panel)
    {
        RectTransform button = GetRect(panel, "ContinueButton");
        Place(button, new Vector2(0f, -310f), new Vector2(650f, 150f));
        Image image = StyleImage(button, "buttons/button-sage-normal.png", false, true);
        ConfigureButton(button, image, "buttons/button-sage-pressed.png", "buttons/button-sage-disabled.png");
        TextMeshProUGUI label = EnsureText(button, "Label");
        StyleText(label, "Resume", 68f, Cocoa, TextAlignmentOptions.Center, true);
        Stretch(label.rectTransform, 28f);
    }

    private static void ConfigureRestart(RectTransform panel)
    {
        RectTransform group = GetRect(panel, "RestartGroup");
        Place(group, new Vector2(0f, -455f), new Vector2(540f, 112f));
        RectTransform background = GetRect(group, "Background");
        Stretch(background);
        Image image = StyleImage(background, "buttons/button-cream-normal.png", false, true);
        ConfigureButton(background, image, "buttons/button-cream-pressed.png", "buttons/button-cream-disabled.png");

        RectTransform icon = GetRect(group, "Icon");
        Place(icon, new Vector2(-178f, 0f), new Vector2(72f, 72f));
        StyleImage(icon, "icons/icon-restart.png", true, false);
        TextMeshProUGUI label = EnsureText(group, "Label");
        StyleText(label, "Restart level", 48f, Cocoa, TextAlignmentOptions.Center, true);
        Place(label.rectTransform, new Vector2(55f, 0f), new Vector2(390f, 82f));
    }

    private static void AssignRuntimeSprites(MagnetPopupUI popup)
    {
        if (popup == null) return;
        SerializedObject serialized = new SerializedObject(popup);
        SetSprite(serialized, "settingsSoundOn", "toggles/toggle-on.png");
        SetSprite(serialized, "settingsSoundOff", "toggles/toggle-off.png");
        SetSprite(serialized, "settingsMusicOn", "toggles/toggle-on.png");
        SetSprite(serialized, "settingsMusicOff", "toggles/toggle-off.png");
        SetSprite(serialized, "settingsHapticOn", "toggles/toggle-on.png");
        SetSprite(serialized, "settingsHapticOff", "toggles/toggle-off.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(popup);
    }

    private static void SetSprite(SerializedObject serialized, string property, string path)
    {
        SerializedProperty value = serialized.FindProperty(property);
        if (value != null) value.objectReferenceValue = Sprite(path);
    }

    private static void ImportArt()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Sprite) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureButton(RectTransform rect, Image target, string pressedPath, string disabledPath)
    {
        Button button = rect.GetComponent<Button>();
        if (button == null) button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = button.spriteState;
        state.highlightedSprite = null;
        state.selectedSprite = null;
        state.pressedSprite = string.IsNullOrEmpty(pressedPath) ? null : Sprite(pressedPath);
        state.disabledSprite = string.IsNullOrEmpty(disabledPath) ? null : Sprite(disabledPath);
        button.spriteState = state;
    }

    private static Image StyleImage(RectTransform rect, string path, bool preserveAspect, bool raycast)
    {
        Image image = GetImage(rect.gameObject);
        image.sprite = Sprite(path);
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.raycastTarget = raycast;
        return image;
    }

    private static Image GetImage(GameObject gameObject)
    {
        Image image = gameObject.GetComponent<Image>();
        return image != null ? image : gameObject.AddComponent<Image>();
    }

    private static RectTransform GetRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing as RectTransform;
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TextMeshProUGUI EnsureText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI text)) return text;
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<TextMeshProUGUI>();
    }

    private static void StyleText(TextMeshProUGUI text, string value, float size, Color color,
        TextAlignmentOptions alignment, bool bold)
    {
        if (text == null) return;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (font != null) text.font = font;
        text.text = value;
        text.color = color;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.62f;
        text.fontSizeMax = size;
        text.alignment = alignment;
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

    private static Sprite Sprite(string path) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{path}");

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
