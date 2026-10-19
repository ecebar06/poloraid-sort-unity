#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class SettingsGoldDisplayInstaller
{
    private const string PanelPath = "Assets/_Game/Art/CozyStationery/shared/panels/gold-balance-panel.png";
    private const string CoinPath = "Assets/_Game/Art/CozyStationery/shared/icons/icon-coin.png";

    static SettingsGoldDisplayInstaller() => EditorApplication.delayCall += Install;

    [MenuItem("Tools/Word Game/Refresh Settings Gold Display")]
    private static void ApplyFromMenu() => Install();

    private static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        MagnetPopupUI popup = Object.FindFirstObjectByType<MagnetPopupUI>(FindObjectsInactive.Include);
        if (popup == null || popup.SettingsScreen == null) return;

        Transform screen = popup.SettingsScreen.transform;
        Transform panel = screen.Find("Panel");
        if (panel == null) return;
        Transform existing = screen.Find("GoldDisplay") ?? panel.Find("GoldDisplay");
        GameObject display = existing == null ? new GameObject("GoldDisplay", typeof(RectTransform)) : existing.gameObject;
        RectTransform displayRect = display.GetComponent<RectTransform>();
        displayRect.SetParent(screen, false);
        displayRect.anchorMin = displayRect.anchorMax = new Vector2(0f, 1f);
        displayRect.pivot = new Vector2(0f, 1f);
        displayRect.anchoredPosition = new Vector2(23f, -28f);
        displayRect.sizeDelta = new Vector2(474.3857f, 233.836f);
        displayRect.localScale = Vector3.one;

        Image background = display.GetComponent<Image>();
        if (background == null) background = display.AddComponent<Image>();
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        background.raycastTarget = false;

        RectTransform coinRect = ChildRect(displayRect, "GoldIcon");
        Place(coinRect, new Vector2(-93.8768f, 3.2512f), new Vector2(128.2464f, 109.4978f));
        Image coin = coinRect.GetComponent<Image>();
        if (coin == null) coin = coinRect.gameObject.AddComponent<Image>();
        coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath);
        coin.preserveAspect = true;
        coin.raycastTarget = false;

        RectTransform textRect = ChildRect(displayRect, "GoldText");
        Place(textRect, new Vector2(55.246f, 3.2513f), new Vector2(170f, 72f));
        TextMeshProUGUI text = textRect.GetComponent<TextMeshProUGUI>();
        if (text == null) text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = SaveSystem.Current.gold.ToString("N0");
        text.fontSize = 60f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28f;
        text.fontSizeMax = 90f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(218, 112, 214, 255);
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        TextMeshProUGUI source = panel.Find("HeaderGroup/Title")?.GetComponent<TextMeshProUGUI>();
        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
        }

        display.SetActive(true);
        EditorUtility.SetDirty(display);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Settings gold display refreshed without changing the popup layout.", display);
    }

    private static RectTransform ChildRect(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing as RectTransform;
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
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
}
#endif
