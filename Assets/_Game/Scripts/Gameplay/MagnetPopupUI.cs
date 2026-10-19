using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MagnetPopupUI : MonoBehaviour
{
    public static MagnetPopupUI Instance { get; private set; }
    private const string ArtPath = "Art/UI/Popups/";

    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private GameObject timeUpScreen;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button spendGoldButton;
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private TextMeshProUGUI explanationText;
    [SerializeField] private TextMeshProUGUI goldPriceText;
    [Header("Polaroid Settings Toggle Art")]
    [SerializeField] private Sprite settingsSoundOn;
    [SerializeField] private Sprite settingsSoundOff;
    [SerializeField] private Sprite settingsMusicOn;
    [SerializeField] private Sprite settingsMusicOff;
    [SerializeField] private Sprite settingsHapticOn;
    [SerializeField] private Sprite settingsHapticOff;
    private TextMeshProUGUI settingsGoldText;

    public GameObject SettingsScreen => settingsScreen;
    public GameObject TimeUpScreen => timeUpScreen;
    public Button RestartButton => restartButton;
    public Button WatchAdButton => watchAdButton;
    public Button SpendGoldButton => spendGoldButton;
    public Button TryAgainButton => tryAgainButton;
    public TextMeshProUGUI ExplanationText => explanationText;
    public TextMeshProUGUI GoldPriceText => goldPriceText;

    private Image soundBackground;
    private Image musicBackground;
    private Image hapticBackground;

    private void Awake()
    {
        Instance = this;
        ResolveSceneReferences();
        EnsurePopupCanvas();
        if (settingsScreen != null) settingsScreen.SetActive(false);
        if (timeUpScreen != null) timeUpScreen.SetActive(false);
        WireSettingsButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsurePopupCanvas()
    {
        // MagnetPopupUI is authored under the portrait GameHUD. Detach it to
        // the actual root canvas before sizing it; otherwise a ScreenSpace
        // overlay can still inherit the HUD's narrow rect and leave the
        // dimmer/background cut off on the sides.
        Canvas parentCanvas = transform.parent != null
            ? transform.parent.GetComponentInParent<Canvas>()
            : null;
        Canvas rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
        if (rootCanvas != null && rootCanvas.transform != transform.parent)
            transform.SetParent(rootCanvas.transform, false);

        Canvas popupCanvas = GetComponent<Canvas>();
        if (popupCanvas == null) popupCanvas = gameObject.AddComponent<Canvas>();
        // This is a separate overlay canvas. Its root and dimmer must follow
        // the real screen bounds, not the authored phone-sized GameHUD rect.
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 2000;

        RectTransform root = transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.localScale = Vector3.one;
        }

        StretchToScreen(settingsScreen);
        StretchToScreen(timeUpScreen);

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private static void StretchToScreen(GameObject screen)
    {
        if (screen == null) return;
        RectTransform rect = screen.transform as RectTransform;
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void Start()
    {
        ResolveSceneReferences();
        WireSettingsButtons();
        WireSettingsLaunchers();
    }

    private void WireSettingsLaunchers()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.name != "SettingsButton" || button.gameObject.scene != gameObject.scene) continue;
            button.onClick.RemoveListener(ShowSettings);
            button.onClick.AddListener(ShowSettings);
        }
    }

    private void WireSettingsButtons()
    {
        if (settingsScreen == null) return;
        Transform panel = settingsScreen.transform.Find("Panel");
        Button close = panel?.Find("CloseButton")?.GetComponent<Button>();
        Button continueButton = panel?.Find("ContinueButton")?.GetComponent<Button>();
        Button sound = panel?.Find("Options/SoundGroup/Background")?.GetComponent<Button>();
        Button music = panel?.Find("Options/MusicGroup/Background")?.GetComponent<Button>();
        Button haptic = panel?.Find("Options/HapticGroup/Background")?.GetComponent<Button>();
        Wire(close, HideSettings);
        Wire(continueButton, HideSettings);
        Wire(sound, ToggleSound);
        Wire(music, ToggleMusic);
        Wire(haptic, ToggleHaptic);
        if (FindFirstObjectByType<CategoryMagnetBoardController>() != null)
            Wire(restartButton, RestartCategoryLevel);
    }

    private void RestartCategoryLevel()
    {
        var board = FindFirstObjectByType<CategoryMagnetBoardController>();
        if (board == null) return;
        HideSettings();
        board.RestartCurrentLevel();
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ResolveSceneReferences()
    {
        if (settingsScreen == null)
            settingsScreen = transform.Find("SettingsScreen")?.gameObject;
        if (timeUpScreen == null)
            timeUpScreen = transform.Find("TimeUpScreen")?.gameObject ??
                           transform.Find("LoseScreen")?.gameObject;
        if (settingsScreen != null)
        {
            Transform panel = settingsScreen.transform.Find("Panel");
            settingsGoldText = settingsScreen.transform.Find("GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>() ??
                               panel?.Find("GoldDisplay/GoldText")?.GetComponent<TextMeshProUGUI>();
            soundBackground = panel?.Find("Options/SoundGroup/Background")?.GetComponent<Image>();
            musicBackground = panel?.Find("Options/MusicGroup/Background")?.GetComponent<Image>();
            hapticBackground = panel?.Find("Options/HapticGroup/Background")?.GetComponent<Image>();
            if (restartButton == null)
                restartButton = panel?.Find("RestartGroup/Background")?.GetComponent<Button>();
        }
        if (timeUpScreen != null)
        {
            Transform panel = timeUpScreen.transform.Find("Panel") ??
                              timeUpScreen.transform.Find("Content");
            if (watchAdButton == null)
                watchAdButton = panel?.Find("WatchAdGroup/Background")?.GetComponent<Button>() ??
                                panel?.Find("WatchAdButton")?.GetComponent<Button>();
            if (spendGoldButton == null)
                spendGoldButton = panel?.Find("GoldGroup/Background")?.GetComponent<Button>() ??
                                  panel?.Find("GoldMovesButton")?.GetComponent<Button>();
            if (tryAgainButton == null)
                tryAgainButton = panel?.Find("TryAgainGroup/Background")?.GetComponent<Button>() ??
                                 panel?.Find("TryAgainButton")?.GetComponent<Button>();
            if (explanationText == null)
                explanationText = panel?.Find("Explanation")?.GetComponent<TextMeshProUGUI>() ??
                                  panel?.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
            if (goldPriceText == null)
                goldPriceText = panel?.Find("GoldGroup/Price")?.GetComponent<TextMeshProUGUI>() ??
                                panel?.Find("GoldMovesButton/Price")?.GetComponent<TextMeshProUGUI>();
        }
    }

    public static MagnetPopupUI CreateAuthored(RectTransform parent)
    {
        if (Application.isPlaying)
            throw new System.InvalidOperationException("Popup authoring is only available outside Play Mode. Use the existing scene popups.");

        GameObject root = Rect("MagnetPopupUI", parent);
        Stretch(root.GetComponent<RectTransform>());
        MagnetPopupUI popup = root.AddComponent<MagnetPopupUI>();
        popup.Build();
        return popup;
    }

    private void Build()
    {
        settingsScreen = CreateScreen("SettingsScreen");
        BuildSettings(settingsScreen.transform);
        settingsScreen.SetActive(false);
        timeUpScreen = CreateScreen("TimeUpScreen");
        BuildTimeUp(timeUpScreen.transform);
        timeUpScreen.SetActive(false);
    }

    private GameObject CreateScreen(string screenName)
    {
        GameObject screen = Rect(screenName, transform);
        Stretch(screen.GetComponent<RectTransform>());
        Image dimmer = screen.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.66f);
        dimmer.raycastTarget = true;
        return screen;
    }

    private void BuildSettings(Transform screen)
    {
        RectTransform panel = Rect("Panel", screen).GetComponent<RectTransform>();
        SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.88f, 0.62f));
        RectTransform body = ImageObject("Body", panel, "card_vertical_purple_tall 6", false);
        Stretch(body);
        body.SetAsFirstSibling();
        RectTransform header = Rect("HeaderGroup", panel).GetComponent<RectTransform>();
        SetRect(header, new Vector2(0.5f, 0.965f), new Vector2(0.72f, 0.25f));
        RectTransform headerBackground = ImageObject("Background", header, "card_header", false);
        Stretch(headerBackground);
        AddLabel("Title", header, "SETTINGS", 72f, new Vector2(0.16f, 0.18f), new Vector2(0.94f, 0.84f));
        RectTransform headerIcon = ImageObject("GearIcon", header, "PictoIcon_Phone", true);
        headerIcon.GetComponent<Image>().sprite = TableController.GetGearIconSprite();
        SetAnchors(headerIcon, new Vector2(0.05f, 0.23f), new Vector2(0.25f, 0.77f));

        Button close = CreateSpriteButton("CloseButton", panel, "exit", "exit");
        SetRect(close.GetComponent<RectTransform>(), new Vector2(0.96f, 0.94f), new Vector2(0.18f, 0.18f));
        close.onClick.AddListener(HideSettings);

        RectTransform options = Rect("Options", panel).GetComponent<RectTransform>();
        SetAnchors(options, new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.73f));
        Button sound = CreateOption(options, "Sound", "PictoIcon_Sound", 0.17f, out soundBackground);
        Button music = CreateOption(options, "Music", "PictoIcon_Music_2", 0.50f, out musicBackground);
        Button haptic = CreateOption(options, "Haptic", "PictoIcon_Phone", 0.83f, out hapticBackground);
        sound.onClick.AddListener(ToggleSound);
        music.onClick.AddListener(ToggleMusic);
        haptic.onClick.AddListener(ToggleHaptic);

        RectTransform restartGroup = Rect("RestartGroup", panel).GetComponent<RectTransform>();
        SetRect(restartGroup, new Vector2(0.5f, 0.20f), new Vector2(0.55f, 0.18f));
        restartButton = CreateSpriteButton("Background", restartGroup, "Button_Green", "Button_Green_Pushed");
        Stretch(restartButton.GetComponent<RectTransform>());
        AddLabel("Label", restartGroup, "RESTART", 58f, Vector2.zero, Vector2.one);
    }

    private Button CreateOption(RectTransform parent, string label, string iconName, float x, out Image background)
    {
        RectTransform group = Rect(label + "Group", parent).GetComponent<RectTransform>();
        SetRect(group, new Vector2(x, 0.58f), new Vector2(0.28f, 0.72f));
        Button button = CreateSpriteButton("Background", group, "button_purple_framed", "button_purple_framed_pushed");
        Stretch(button.GetComponent<RectTransform>());
        background = button.GetComponent<Image>();
        RectTransform icon = ImageObject("Icon", group, iconName, true);
        SetAnchors(icon, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.80f));
        AddLabel("Label", group, label.ToUpperInvariant(), 34f,
            new Vector2(-0.18f, -0.28f), new Vector2(1.18f, 0.06f));
        return button;
    }

    private void BuildTimeUp(Transform screen)
    {
        RectTransform panel = Rect("Panel", screen).GetComponent<RectTransform>();
        SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.88f, 0.64f));
        RectTransform body = ImageObject("Body", panel, "card_vertical_purple_tall 6", false);
        Stretch(body);
        body.SetAsFirstSibling();
        RectTransform header = Rect("HeaderGroup", panel).GetComponent<RectTransform>();
        SetRect(header, new Vector2(0.5f, 0.965f), new Vector2(0.72f, 0.25f));
        RectTransform headerBackground = ImageObject("Background", header, "card_header", false);
        Stretch(headerBackground);
        AddLabel("Title", header, "OUT OF MOVES!", 64f, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.84f));
        explanationText = AddLabel("Explanation", panel, "Add extra moves to continue.", 38f,
            new Vector2(0.10f, 0.67f), new Vector2(0.90f, 0.78f));
        RectTransform hourglassGroup = Rect("HourglassGroup", panel).GetComponent<RectTransform>();
        SetRect(hourglassGroup, new Vector2(0.5f, 0.54f), new Vector2(0.28f, 0.25f));
        RectTransform hourglassRing = ImageObject("Ring", hourglassGroup, "button_circle_framed", true);
        Stretch(hourglassRing);
        RectTransform hourglass = ImageObject("Icon", hourglassGroup, "PictoIcon_Hourglass", true);
        SetAnchors(hourglass, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f));

        RectTransform watchGroup = Rect("WatchAdGroup", panel).GetComponent<RectTransform>();
        SetRect(watchGroup, new Vector2(0.30f, 0.25f), new Vector2(0.40f, 0.20f));
        watchAdButton = CreateSpriteButton("Background", watchGroup, "Button_V3_Red", "Button_orange_Pushed");
        Stretch(watchAdButton.GetComponent<RectTransform>());
        RectTransform movie = ImageObject("MovieIcon", watchGroup, "PictoIcon_Movie", true);
        SetAnchors(movie, new Vector2(-0.07f, 0.54f), new Vector2(0.25f, 1.18f));
        AddLabel("Label", watchGroup, "WATCH AD", 34f, new Vector2(0.16f, 0.38f), new Vector2(0.96f, 0.84f));
        AddLabel("Detail", watchGroup, "+5 MOVES", 24f, new Vector2(0.16f, 0.10f), new Vector2(0.96f, 0.42f));

        RectTransform goldGroup = Rect("GoldGroup", panel).GetComponent<RectTransform>();
        SetRect(goldGroup, new Vector2(0.72f, 0.25f), new Vector2(0.40f, 0.20f));
        spendGoldButton = CreateSpriteButton("Background", goldGroup, "Button_Green", "Button_Green_Pushed");
        Stretch(spendGoldButton.GetComponent<RectTransform>());
        goldPriceText = AddLabel("Price", goldGroup, "900", 46f,
            new Vector2(0.18f, 0.37f), new Vector2(0.94f, 0.88f));
        AddLabel("Detail", goldGroup, "+5 MOVES", 24f,
            new Vector2(0.18f, 0.09f), new Vector2(0.94f, 0.40f));
    }


    public void ShowSettings()
    {
        ResolveSceneReferences();
        EnsurePopupCanvas();
        if (settingsScreen == null)
        {
            Debug.LogError("SettingsScreen bulunamadi. MagnetPopupUI hiyerarsisini kontrol et.", this);
            return;
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Prepare every saved value while the popup is still invisible. If we
        // activate it first, the Scene-authored (all-off) sprites can be drawn
        // for a frame before the saved settings replace them.
        CanvasGroup settingsGroup = GetOrAddCanvasGroup(settingsScreen);
        settingsGroup.alpha = 0f;
        settingsGroup.blocksRaycasts = false;
        settingsGroup.interactable = false;
        settingsScreen.transform.SetAsLastSibling();
        if (settingsGoldText != null)
            settingsGoldText.text = SaveSystem.Current.gold.ToString();
        RefreshSettingsVisuals();

        settingsScreen.SetActive(true);
        Canvas.ForceUpdateCanvases();
        AnimateSettingsPopupIn(settingsScreen);
        Debug.Log("Settings popup acildi.", this);
    }

    public void HideSettings()
    {
        AnimatePopupOut(settingsScreen);
    }

    public void ShowTimeUp()
    {
        ResolveSceneReferences();
        EnsurePopupCanvas();
        if (timeUpScreen == null)
        {
            Debug.LogError("TimeUpScreen bulunamadi. MagnetPopupUI hiyerarsisini kontrol et.", this);
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        timeUpScreen.SetActive(true);
        if (explanationText != null)
            explanationText.text = "A few more moves?";
        GameAudio.Play(GameAudioCue.Lose);
        HapticFeedback.Play(HapticFeedback.Strength.Medium);
        AnimatePopupIn(timeUpScreen, GameAudioCue.PopupOpen);
        timeUpScreen.transform.SetAsLastSibling();
        Transform goldValue = timeUpScreen.transform.Find("Gold/Value");
        if (goldValue != null && goldValue.TryGetComponent(out TextMeshProUGUI balance))
            balance.text = SaveSystem.Current.gold.ToString("N0");
        CategoryMagnetBoardController board = FindFirstObjectByType<CategoryMagnetBoardController>();
        if (board != null)
            board.BindOutOfMovesButtons(this);
        if (spendGoldButton != null)
            spendGoldButton.interactable = true;
        Canvas.ForceUpdateCanvases();
        Debug.Log("TimeUp popup acildi.", this);
    }

    public void ShowNotEnoughGold()
    {
        if (explanationText == null) return;
        explanationText.text = "Not enough coins";
        explanationText.transform.DOKill();
        explanationText.transform.localScale = Vector3.one;
        explanationText.transform.DOPunchScale(Vector3.one * 0.12f, 0.28f, 6, 0.45f)
            .SetUpdate(true);
    }

    public void HideTimeUp()
    {
        AnimatePopupOut(timeUpScreen);
    }

    private static void AnimatePopupIn(GameObject screen, GameAudioCue openCue)
    {
        if (screen == null) return;
        CanvasGroup group = GetOrAddCanvasGroup(screen);
        RectTransform panel = FindPanel(screen);
        screen.transform.localScale = Vector3.one;
        panel.localScale = Vector3.one * 0.86f;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        screen.transform.SetAsLastSibling();
        GameAudio.Play(openCue);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(group.DOFade(1f, 0.16f).SetEase(Ease.OutCubic));
        sequence.Join(panel.DOScale(1f, 0.34f).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            group.blocksRaycasts = true;
            group.interactable = true;
        });
    }

    private static void AnimateSettingsPopupIn(GameObject screen)
    {
        if (screen == null) return;
        CanvasGroup group = GetOrAddCanvasGroup(screen);
        RectTransform panel = FindPanel(screen);
        panel.DOKill();
        group.DOKill();
        screen.transform.localScale = Vector3.one;
        panel.localScale = Vector3.one * 0.86f;

        // The saved toggle sprites must be visible at full colour from the
        // first rendered frame. Fading the entire screen made every green ON
        // toggle briefly resemble its muted OFF state.
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;
        screen.transform.SetAsLastSibling();
        GameAudio.Play(GameAudioCue.Settings);
        panel.DOScale(1f, 0.34f).SetEase(Ease.OutBack).SetUpdate(true).OnComplete(() =>
        {
            group.blocksRaycasts = true;
            group.interactable = true;
        });
    }

    private static void AnimatePopupOut(GameObject screen)
    {
        if (screen == null || !screen.activeSelf) return;
        CanvasGroup group = GetOrAddCanvasGroup(screen);
        RectTransform panel = FindPanel(screen);
        group.blocksRaycasts = false;
        group.interactable = false;
        panel.DOKill();
        group.DOKill();
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(panel.DOScale(0.86f, 0.18f).SetEase(Ease.InBack));
        sequence.Join(group.DOFade(0f, 0.14f).SetEase(Ease.InCubic));
        sequence.OnComplete(() =>
        {
            panel.localScale = Vector3.one;
            screen.SetActive(false);
        });
    }

    private static RectTransform FindPanel(GameObject screen)
    {
        return screen.transform.Find("Panel") as RectTransform ?? screen.transform as RectTransform;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject screen)
    {
        CanvasGroup group = screen.GetComponent<CanvasGroup>();
        return group != null ? group : screen.AddComponent<CanvasGroup>();
    }

    private void ToggleSound()
    {
        PlayerSaveData data = SaveSystem.Current;
        SaveSystem.SetAudioSettings(!data.soundEnabled, data.musicEnabled);
        GameAudio.RefreshFromSettings();
        RefreshSettingsVisuals();
    }

    private void ToggleMusic()
    {
        PlayerSaveData data = SaveSystem.Current;
        SaveSystem.SetAudioSettings(data.soundEnabled, !data.musicEnabled);
        GameAudio.RefreshFromSettings();
        RefreshSettingsVisuals();
    }

    private void ToggleHaptic()
    {
        HapticFeedback.Enabled = !HapticFeedback.Enabled;
        if (HapticFeedback.Enabled) HapticFeedback.Play(HapticFeedback.Strength.Light);
        RefreshSettingsVisuals();
    }

    private void RefreshSettingsVisuals()
    {
        PlayerSaveData data = SaveSystem.Current;
        SetToggleVisual(soundBackground, data.soundEnabled, settingsSoundOn, settingsSoundOff);
        SetToggleVisual(musicBackground, data.musicEnabled, settingsMusicOn, settingsMusicOff);
        SetToggleVisual(hapticBackground, HapticFeedback.Enabled, settingsHapticOn, settingsHapticOff);
        StabilizeSettingsActionButtons();
    }

    private void StabilizeSettingsActionButtons()
    {
        if (settingsScreen == null) return;
        Transform panel = settingsScreen.transform.Find("Panel");
        StabilizeButtonVisual(panel?.Find("ContinueButton")?.GetComponent<Button>());
        StabilizeButtonVisual(panel?.Find("RestartGroup/Background")?.GetComponent<Button>());
    }

    private static void StabilizeButtonVisual(Button button)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image == null) return;

        // These buttons already use their authored normal sprite. Disabling
        // Selectable's delayed state transition prevents the colour/sprite
        // from being replaced for a moment when the popup becomes active.
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        image.color = Color.white;
        image.enabled = true;
        image.SetAllDirty();
    }

    private static void SetToggleVisual(Image image, bool enabled, Sprite enabledSprite, Sprite disabledSprite)
    {
        if (image == null) return;
        Button button = image.GetComponent<Button>();
        if (button != null)
        {
            // The Image itself represents the saved toggle value. A SpriteSwap
            // transition can restore the Scene sprite on enable/selection and
            // overwrite that value a frame later.
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
        }
        if (enabledSprite != null && disabledSprite != null)
        {
            image.sprite = enabled ? enabledSprite : disabledSprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return;
        }
        image.sprite = Load(enabled ? "button_purple_framed" : "button_purple_framed_dark");
        image.color = enabled ? Color.white : new Color(0.68f, 0.68f, 0.72f, 1f);
    }

    private static Button CreateSpriteButton(string name, Transform parent, string normal, string pressed)
    {
        RectTransform rect = ImageObject(name, parent, normal, false);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SpriteState state = button.spriteState;
        state.pressedSprite = Load(pressed);
        state.selectedSprite = Load(normal);
        button.spriteState = state;
        button.transition = Selectable.Transition.SpriteSwap;
        return button;
    }

    private static RectTransform ImageObject(string name, Transform parent, string spriteName, bool preserveAspect)
    {
        GameObject go = Rect(name, parent);
        Image image = go.AddComponent<Image>();
        image.sprite = Load(spriteName);
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI AddLabel(string name, Transform parent, string value, float size, Vector2 min, Vector2 max)
    {
        GameObject go = Rect(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, size * 0.55f);
        text.fontSizeMax = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.22f;
        text.raycastTarget = false;
        SetAnchors(text.rectTransform, min, max);
        return text;
    }

    private static GameObject Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Sprite Load(string name)
    {
        Sprite sprite = Resources.Load<Sprite>(ArtPath + name);
        if (sprite != null) return sprite;
        Texture2D texture = Resources.Load<Texture2D>(ArtPath + name);
        return texture == null
            ? null
            : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 center, Vector2 normalizedSize)
    {
        rect.anchorMin = center - normalizedSize * 0.5f;
        rect.anchorMax = center + normalizedSize * 0.5f;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
