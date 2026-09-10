#if UNITY_EDITOR
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RestoreSceneTypography
{
    static RestoreSceneTypography()
    {
        EditorApplication.delayCall += Restore;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) Restore();
        };
    }

    [MenuItem("Tools/Word Game/Restore Previous Fredoka Fonts")]
    public static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Game") return;
        string yaml = File.ReadAllText(scene.path);
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.font == null || !text.font.name.Contains("Roboto")) continue;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(text).targetObjectId.ToString();
            var block = Regex.Match(yaml, @"(?ms)^--- !u!114 &" + id + @"\r?\n.*?(?=^---|\z)").Value;
            var guid = Regex.Match(block, @"m_fontAsset:.*guid: ([a-f0-9]+)").Groups[1].Value;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (font == null || font.name.Contains("Roboto"))
                font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
            Undo.RecordObject(text, "Restore previous Fredoka typography");
            text.font = font;
            text.fontSharedMaterial = font.material;
            var serialized = new SerializedObject(text);
            foreach (string key in new[] { "m_fontSize", "m_fontSizeBase", "m_fontSizeMin", "m_fontSizeMax", "m_characterSpacing" })
            {
                var value = Regex.Match(block, @"(?m)^  " + key + @": ([\d.\-]+)");
                if (value.Success) serialized.FindProperty(key).floatValue = float.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            foreach (string key in new[] { "m_fontStyle", "m_fontWeight" })
            {
                var value = Regex.Match(block, @"(?m)^  " + key + @": (\d+)");
                if (value.Success) serialized.FindProperty(key).intValue = int.Parse(value.Groups[1].Value);
            }
            serialized.ApplyModifiedProperties();
            var color = Regex.Match(block, @"m_fontColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}");
            if (color.Success) text.color = new Color(
                float.Parse(color.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(color.Groups[2].Value, CultureInfo.InvariantCulture),
                float.Parse(color.Groups[3].Value, CultureInfo.InvariantCulture),
                float.Parse(color.Groups[4].Value, CultureInfo.InvariantCulture));
            text.UpdateMeshPadding();
            text.SetAllDirty();
            EditorUtility.SetDirty(text);
            changed = true;
        }
        if (changed) EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
