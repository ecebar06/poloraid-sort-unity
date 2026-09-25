using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameAudioCue
{
    Button,
    Settings,
    Restart,
    PopupOpen,
    MagnetPlace,
    Error,
    CategoryComplete,
    Transformation,
    Win,
    Lose
}

/// <summary>
/// Central audio gateway for the game. Clips are loaded from Resources so the
/// sound settings work in both the editor and Android builds without scene
/// references that can be lost when the HUD is rebuilt.
/// </summary>
public sealed class GameAudio : MonoBehaviour
{
    private static GameAudio instance;
    private AudioSource sfxSource;
    private AudioSource musicSource;
    private AudioClip uiClick;
    private AudioClip settings;
    private AudioClip restart;
    private AudioClip popupOpen;
    private AudioClip magnetPlace;
    private AudioClip error;
    private AudioClip categoryComplete;
    private AudioClip transformation;
    private AudioClip win;
    private AudioClip lose;
    private AudioClip gameplayMusic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameAudio Instance => EnsureInstance();

    private static GameAudio EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindFirstObjectByType<GameAudio>();
        if (instance != null) return instance;
        GameObject audioObject = new GameObject(nameof(GameAudio));
        instance = audioObject.AddComponent<GameAudio>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        sfxSource = CreateSource("SFX");
        musicSource = CreateSource("Music");
        musicSource.loop = true;
        LoadClips();
        ApplySettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallButtonAudio();
        ApplySettings();
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void LoadClips()
    {
        uiClick = Resources.Load<AudioClip>("Audio/ui_click");
        settings = Resources.Load<AudioClip>("Audio/settings_click");
        restart = Resources.Load<AudioClip>("Audio/restart");
        popupOpen = Resources.Load<AudioClip>("Audio/popup_open");
        magnetPlace = Resources.Load<AudioClip>("Audio/magnet_place");
        error = Resources.Load<AudioClip>("Audio/error");
        categoryComplete = Resources.Load<AudioClip>("Audio/category_complete");
        transformation = Resources.Load<AudioClip>("Audio/transformation");
        win = Resources.Load<AudioClip>("Audio/win");
        lose = Resources.Load<AudioClip>("Audio/lose");
        gameplayMusic = Resources.Load<AudioClip>("Audio/gameplay_music");
    }

    public static void Play(GameAudioCue cue)
    {
        GameAudio audio = EnsureInstance();
        if (!SaveSystem.Current.soundEnabled || audio.sfxSource == null) return;
        AudioClip clip = audio.GetClip(cue);
        if (clip != null)
        {
            float volume = cue == GameAudioCue.Button ? 0.45f :
                cue == GameAudioCue.Settings ? 0.35f :
                cue == GameAudioCue.Restart ? 0.65f : 1f;
            audio.sfxSource.PlayOneShot(clip, volume);
        }
    }

    public static void RefreshFromSettings()
    {
        EnsureInstance().ApplySettings();
    }

    private AudioClip GetClip(GameAudioCue cue)
    {
        switch (cue)
        {
            case GameAudioCue.Button: return uiClick;
            case GameAudioCue.Settings: return settings;
            case GameAudioCue.Restart: return restart;
            case GameAudioCue.PopupOpen: return popupOpen;
            case GameAudioCue.MagnetPlace: return magnetPlace;
            case GameAudioCue.Error: return error;
            case GameAudioCue.CategoryComplete: return categoryComplete;
            case GameAudioCue.Transformation: return transformation;
            case GameAudioCue.Win: return win;
            case GameAudioCue.Lose: return lose;
            default: return null;
        }
    }

    private void ApplySettings()
    {
        PlayerSaveData data = SaveSystem.Current;
        if (sfxSource != null) sfxSource.volume = data.soundEnabled ? 0.9f : 0f;
        if (musicSource != null)
        {
            musicSource.volume = data.musicEnabled ? 0.30f : 0f;
            if (gameplayMusic != null && musicSource.clip != gameplayMusic)
            {
                musicSource.clip = gameplayMusic;
                if (data.musicEnabled) musicSource.Play();
            }
            else if (data.musicEnabled && !musicSource.isPlaying && gameplayMusic != null)
            {
                musicSource.Play();
            }
        }
        // Keep the listener unmuted; the two settings are controlled
        // independently by the dedicated sources above.
        AudioListener.volume = 1f;
    }

    private static void InstallButtonAudio()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (button.GetComponent<GameAudioButtonRelay>() == null)
                button.gameObject.AddComponent<GameAudioButtonRelay>();
        }
    }
}

public sealed class GameAudioButtonRelay : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Play);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Play);
    }

    private void Play()
    {
        bool isSettings = button != null && button.name == "SettingsButton";
        bool isRestart = button != null && button.name == "Background" &&
            button.transform.parent != null && button.transform.parent.name == "RestartGroup";
        if (isSettings) return;
        GameAudio.Play(isRestart ? GameAudioCue.Restart : GameAudioCue.Button);
    }
}
