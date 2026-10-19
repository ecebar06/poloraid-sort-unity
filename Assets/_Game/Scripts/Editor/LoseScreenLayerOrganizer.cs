#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cleans only the hierarchy of the artist-authored LoseScreen. It deliberately
/// does not alter any visual component, sprite, RectTransform value or text.
/// </summary>
[InitializeOnLoad]
internal static class LoseScreenLayerOrganizer
{
    private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
    private const string MarkerName = "LoseScreenLayersOrganizedV1";

    static LoseScreenLayerOrganizer()
    {
        EditorApplication.delayCall += OrganizeIfNeeded;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += OrganizeIfNeeded;
    }

    [MenuItem("Tools/Word Game/Organize Lose Screen Layers")]
    private static void OrganizeFromMenu() => Organize(true);

    private static void OrganizeIfNeeded() => Organize(false);

    private static void Organize(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        MagnetPopupUI popup = FindComponent<MagnetPopupUI>(scene);
        Transform lose = FindAuthoredLoseScreen(popup == null ? null : popup.transform);
        if (popup == null || lose == null || (!force && lose.Find(MarkerName) != null))
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        Transform content = lose.Find("Content");
        Transform goldButton = FindEither(content, "GoldMovesButton", "ContinueButton");
        Transform tryAgainButton = FindEither(content, "TryAgainButton", "ContinueButton (1)");
        Transform watchButton = FindEither(content, "WatchAdButton", "ContinueButton (2)");

        if (goldButton != null) goldButton.name = "GoldMovesButton";
        if (tryAgainButton != null) tryAgainButton.name = "TryAgainButton";
        if (watchButton != null)
        {
            watchButton.name = "WatchAdButton";
            FlattenWatchLayersWithoutMoving(watchButton);
        }

        Transform subtitle = content == null ? null : content.Find("LevelText");
        if (subtitle != null) subtitle.name = "Subtitle";

        SerializedObject serialized = new SerializedObject(popup);
        Assign(serialized, "timeUpScreen", lose.gameObject);
        Assign(serialized, "watchAdButton", watchButton == null ? null : watchButton.GetComponent<Button>());
        Assign(serialized, "spendGoldButton", goldButton == null ? null : goldButton.GetComponent<Button>());
        Assign(serialized, "tryAgainButton", tryAgainButton == null ? null : tryAgainButton.GetComponent<Button>());
        Assign(serialized, "explanationText", subtitle == null ? null : subtitle.GetComponent<TextMeshProUGUI>());
        Assign(serialized, "goldPriceText", goldButton == null ? null : goldButton.Find("Price")?.GetComponent<TextMeshProUGUI>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(popup);

        if (lose.Find(MarkerName) == null)
        {
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(lose, false);
            marker.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("LoseScreen layers organized without changing the saved visual layout.", lose);
    }

    private static void FlattenWatchLayersWithoutMoving(Transform watchButton)
    {
        Transform nested = watchButton.Find("WatchAdGroup") ?? watchButton.Find("UnusedWatchBackground");
        if (nested == null) return;

        Transform primary = watchButton.Find("Label");
        if (primary != null) primary.name = "PrimaryLabel";

        Transform movie = nested.Find("MovieIcon");
        Transform secondary = nested.Find("Label");
        Transform detail = nested.Find("Detail");
        if (secondary != null) secondary.name = "SecondaryLabel";

        // worldPositionStays keeps the exact appearance the artist saved.
        if (movie != null) movie.SetParent(watchButton, true);
        if (secondary != null) secondary.SetParent(watchButton, true);
        if (detail != null) detail.SetParent(watchButton, true);
        nested.name = "UnusedWatchBackground";

        // Preserve the previous draw order: nested background first, then its
        // icon/text layers, with the artist's primary label still on top.
        nested.SetSiblingIndex(0);
        if (movie != null) movie.SetSiblingIndex(1);
        if (secondary != null) secondary.SetSiblingIndex(2);
        if (detail != null) detail.SetSiblingIndex(3);
        if (primary != null) primary.SetAsLastSibling();
    }

    private static Transform FindAuthoredLoseScreen(Transform popupRoot)
    {
        if (popupRoot == null) return null;
        for (int index = 0; index < popupRoot.childCount; index++)
        {
            Transform child = popupRoot.GetChild(index);
            if (child.name == "LoseScreen" && child.Find("Content") != null && child.Find("Background") != null)
                return child;
        }
        return null;
    }

    private static Transform FindEither(Transform parent, string preferred, string fallback)
    {
        if (parent == null) return null;
        return parent.Find(preferred) ?? parent.Find(fallback);
    }

    private static void Assign(SerializedObject serialized, string name, Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }
}
#endif
