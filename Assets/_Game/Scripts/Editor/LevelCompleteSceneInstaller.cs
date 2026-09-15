using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LevelCompleteSceneInstaller
{
    private const string PopupPath = "Assets/_Game/Resources/Art/UI/Popups/";

    static LevelCompleteSceneInstaller() => EditorApplication.delayCall += EnsureAuthoredScreen;

    [MenuItem("Tools/Word Game/Build Authored Level Complete Screen")]
    private static void RebuildFromMenu()
    {
        Transform panel = FindPanel();
        if (panel == null) return;
        Transform marker = panel.parent.Find("Content/LevelCompleteLayout") ?? panel.Find("LevelCompleteLayout");
        if (marker != null) Object.DestroyImmediate(marker.gameObject);
        EnsureAuthoredScreen();
    }

    private static void EnsureAuthoredScreen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        RemovePopupCanvasGroups();
        Transform panel = FindPanel();
        if (panel == null) return;
        Transform existingLayout = panel.parent.Find("Content/LevelCompleteLayout") ?? panel.Find("LevelCompleteLayout");
        if (existingLayout != null && existingLayout.Find("RewardCoins") != null)
        {
            EnsureSeparateContent(panel);
            ConfigurePresenter(panel.parent.gameObject);
            panel.parent.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            EditorSceneManager.SaveScene(panel.gameObject.scene);
            return;
        }
        if (existingLayout != null)
            Object.DestroyImmediate(existingLayout.gameObject);
        GameObject winScreen = panel.parent.gameObject;
        ConfigurePresenter(winScreen);

        foreach (Transform child in panel)
        {
            child.name = "Legacy_" + child.name;
            child.gameObject.SetActive(false);
        }

        RectTransform panelRect = (RectTransform)panel;
        SetAnchors(panelRect, new Vector2(0.09f, 0.17f), new Vector2(0.91f, 0.83f));
        Image body = panel.GetComponent<Image>();
        body.sprite = Sprite("card_vertical_purple_tall 6");
        body.type = Image.Type.Sliced;
        body.color = Color.white;

        RectTransform layout = Rect("LevelCompleteLayout", panel);
        Stretch(layout);

        RectTransform header = ImageObject("Header", layout, "card_header", Image.Type.Sliced);
        SetAnchors(header, new Vector2(0.16f, 0.86f), new Vector2(0.84f, 1.10f));
        Label("Title", header, "LEVEL 1", 62f, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.86f));
        Label("RewardCaption", layout, "REWARDS", 30f, new Vector2(0.26f, 0.68f), new Vector2(0.74f, 0.75f));

        RectTransform effect = ImageObject("RewardEffect", layout, "level_complete_gold_effect");
        SetAnchors(effect, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.72f));
        effect.GetComponent<Image>().preserveAspect = true;

        RectTransform platform = ImageObject("RewardPlatform", layout, "level_complete_platform");
        SetAnchors(platform, new Vector2(0.17f, 0.34f), new Vector2(0.83f, 0.49f));
        platform.GetComponent<Image>().preserveAspect = true;

        RectTransform coins = ImageObject("RewardCoins", layout, "golds_1");
        SetAnchors(coins, new Vector2(0.23f, 0.42f), new Vector2(0.77f, 0.68f));
        coins.GetComponent<Image>().preserveAspect = true;

        RectTransform reward = Rect("Reward", panel);
        SetAnchors(reward, new Vector2(0.31f, 0.29f), new Vector2(0.69f, 0.39f));
        Label("Value", reward, "40", 54f, Vector2.zero, Vector2.one);

        RectTransform continueButton = ImageObject("ContinueButton", panel, "Button_Green", Image.Type.Sliced);
        SetAnchors(continueButton, new Vector2(0.23f, 0.08f), new Vector2(0.77f, 0.21f));
        continueButton.gameObject.AddComponent<Button>().targetGraphic = continueButton.GetComponent<Image>();
        Label("Label", continueButton, "CONTINUE", 42f, Vector2.zero, Vector2.one);

        EnsureSeparateContent(panel);
        // Like SettingsScreen, this stays authored but closed in the editor.
        // TableController activates it only after a successful level.
        winScreen.SetActive(false);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        EditorSceneManager.SaveScene(panel.gameObject.scene);
    }

    private static void EnsureSeparateContent(Transform backgroundTransform)
    {
        RectTransform background = (RectTransform)backgroundTransform;
        Transform winScreen = background.parent;
        RectTransform content = winScreen.Find("Content") as RectTransform;
        if (content == null)
        {
            content = Rect("Content", winScreen);
            content.pivot = background.pivot;
            content.anchorMin = background.anchorMin;
            content.anchorMax = background.anchorMax;
            content.sizeDelta = background.sizeDelta;
            content.anchoredPosition3D = background.anchoredPosition3D;
            content.localRotation = background.localRotation;
            content.localScale = background.localScale;
        }

        foreach (string childName in new[] { "LevelCompleteLayout", "Reward", "ContinueButton" })
        {
            Transform child = background.Find(childName);
            if (child != null) child.SetParent(content, false);
        }

        background.name = "Background";
    }

    private static void RemovePopupCanvasGroups()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("Game.unity")) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (string screenName in new[] { "SettingsScreen", "TimeUpScreen" })
            {
                Transform screen = FindDeep(root.transform, screenName);
                if (screen == null) continue;
                CanvasGroup group = screen.GetComponent<CanvasGroup>();
                if (group != null) Object.DestroyImmediate(group);
            }
        }
    }

    private static void ConfigurePresenter(GameObject winScreen)
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(winScreen);
        CanvasGroup group = winScreen.GetComponent<CanvasGroup>();
        if (group == null) group = winScreen.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    private static Transform FindPanel()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("Game.unity")) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform win = FindDeep(root.transform, "WinScreen");
            Transform panel = win?.Find("Background") ?? win?.Find("Panel");
            if (panel != null) return panel;
        }
        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static RectTransform ImageObject(string name, Transform parent, string spriteName, Image.Type type = Image.Type.Simple)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = Sprite(spriteName);
        image.type = type;
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string value, float size, Vector2 min, Vector2 max)
    {
        RectTransform rect = Rect(name, parent);
        SetAnchors(rect, min, max);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Resources/Fonts/Fredoka-Variable SDF.asset")
                    ?? TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.65f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.18f;
        text.outlineColor = Color.black;
        return text;
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(PopupPath + name + ".png");

    private static void Stretch(RectTransform rect) => SetAnchors(rect, Vector2.zero, Vector2.one);

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
