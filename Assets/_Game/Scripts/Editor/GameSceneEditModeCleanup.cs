#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameSceneEditModeCleanup
{
    static GameSceneEditModeCleanup()
    {
        EditorApplication.delayCall += CleanLoadedGameScene;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += CleanLoadedGameScene;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += CleanLoadedGameScene;
    }

    private static void CleanLoadedGameScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        TableController[] controllers = Object.FindObjectsByType<TableController>(
            FindObjectsInactive.Include);
        foreach (TableController controller in controllers)
        {
            if (controller == null || controller.wordContainer == null) continue;

            Transform board = controller.wordContainer;
            Canvas canvas = board.GetComponentInParent<Canvas>(true);
            bool categoryMode = canvas != null &&
                canvas.GetComponentInChildren<CategoryMagnetBoardController>(true) != null;
            if (board.gameObject.activeSelf == categoryMode)
            {
                board.gameObject.SetActive(!categoryMode);
                EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            }
            // Rows are authored prefab instances in Game.unity now. Do not
            // delete them in edit mode; Play Mode only changes their data and
            // active state.

            if (canvas != null)
            {
                bool polaroidTheme = canvas.transform.Find("PolaroidThemeSceneLayoutV2") != null;
                RectTransform gameHud = canvas.transform.Find("GameHUD") as RectTransform;
                if (gameHud != null)
                {
                    if (!polaroidTheme) controller.ApplyEditorPreviewSafeArea();

                    Transform fridgeHud = gameHud.Find("FridgeTopHud");
                    if (fridgeHud != null) fridgeHud.gameObject.SetActive(!polaroidTheme);
                }

                Transform polaroidHud = canvas.transform.Find("PolaroidTopHud");
                if (polaroidHud != null) polaroidHud.gameObject.SetActive(polaroidTheme);

                DestroyChild(canvas.transform, "ResultOverlay");
            }

        }

        SceneView.RepaintAll();
    }

    private static void DestroyChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }
}
#endif
