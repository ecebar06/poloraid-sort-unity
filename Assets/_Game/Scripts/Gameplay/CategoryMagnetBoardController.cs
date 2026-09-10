using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CategoryMagnetBoardController : MonoBehaviour
{
    [SerializeField] private RectTransform bottomArea;
    [SerializeField] private CategoryHolder[] holders = new CategoryHolder[4];
    [SerializeField] private TableController legacyTableController;
    [SerializeField] private TextMeshProUGUI levelLabel;
    [SerializeField] private TextMeshProUGUI moveLabel;
    [SerializeField] private GameObject winScreen;
    [SerializeField] private TextMeshProUGUI magnetTextStyle;

    private readonly HashSet<string> completedCategories = new HashSet<string>();
    private CategoryHolder hoveredHolder;
    private LevelDatabase database;
    private WordIconLibrary iconLibrary;
    private int activeLevelIndex;
    private int totalCategories;
    private int remainingMoves;
    private bool gameEnded;
    private bool rewardDoubled;
    private Canvas canvas;
    private Sprite magnetSprite;
    private TMP_FontAsset font;
    private LevelData currentLevel;
    private int pendingTransformations;
    private Image categoryProgress;
    private RectTransform categoryKnob;
    private TextMeshProUGUI categoryProgressText;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private TextMeshProUGUI testLevelLabel;
    private Button testPrevious, testNext;
#endif

    public Canvas Canvas => canvas;
    public RectTransform BottomArea => bottomArea;
    public bool IsInputLocked => gameEnded || pendingTransformations > 0 ||
        (MagnetPopupUI.Instance != null && MagnetPopupUI.Instance.SettingsScreen != null &&
         MagnetPopupUI.Instance.SettingsScreen.activeInHierarchy);

    public bool CategoryTransforms(string categoryId)
    {
        return currentLevel != null && currentLevel.categories.Any(category =>
            category.id == categoryId && category.transformsOnComplete && category.transformResult != null);
    }

    public bool IsCategoryAssignedToAnotherHolder(string categoryId, CategoryHolder requester)
    {
        return holders.Any(holder => holder != null && holder != requester &&
            !holder.IsEmpty && holder.CategoryId == categoryId);
    }

    private void Awake()
    {
        if (legacyTableController != null)
        {
            legacyTableController.enabled = false;
            if (legacyTableController.wordContainer != null)
                legacyTableController.wordContainer.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        Transform hud = transform.parent.Find("FridgeTopHud");
        if (hud != null)
        {
            levelLabel = hud.Find("LevelLabel")?.GetComponent<TextMeshProUGUI>() ?? levelLabel;
            moveLabel = hud.Find("MovesMagnet/MoveCount")?.GetComponent<TextMeshProUGUI>() ?? moveLabel;
            categoryProgress = hud.Find("ProgressCard/ProgressTrack/Fill")?.GetComponent<Image>();
            categoryKnob = hud.Find("ProgressCard/ProgressTrack/Knob") as RectTransform;
            categoryProgressText = hud.Find("ProgressCard/ProgressTrack/Knob/Value")?.GetComponent<TextMeshProUGUI>();
        }
        database = LevelDatabase.LoadFromResources();
        iconLibrary = Resources.Load<WordIconLibrary>("Data/MainWordIconLibrary");
        magnetSprite = Resources.Load<Sprite>("Art/Fridge/Magnets/magnet_word");
        font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (magnetTextStyle != null)
        {
            if (magnetTextStyle.font != null) font = magnetTextStyle.font;
            magnetTextStyle.transform.parent.gameObject.SetActive(false);
        }
        PlayerSaveData save = SaveSystem.Load();
        WireWinScreenButtons();
        LoadLevel(save.currentLevelIndex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CreateTestNavigator();
#endif
    }

    public void LoadLevel(int index)
    {
        if (database == null || database.levels.Count == 0) return;
        activeLevelIndex = Mathf.Clamp(index, 0, database.levels.Count - 1);
        LevelData level = database.GetLevel(activeLevelIndex);
        StopAllCoroutines();
        bottomArea.gameObject.SetActive(true);
        foreach (var holder in holders) holder.gameObject.SetActive(true);
        pendingTransformations = 0;
        currentLevel = level;
        ClearLooseMagnets();
        completedCategories.Clear();
        totalCategories = level.categories.Count;
        remainingMoves = level.moveCount;
        gameEnded = false;
        rewardDoubled = false;
        if (winScreen != null) winScreen.SetActive(false);
        foreach (CategoryHolder holder in holders) holder.Configure(this);
        if (levelLabel != null) levelLabel.text = $"Level <color=#D69535>{level.levelNumber}</color>";
        RefreshCategoryProgress();
        RefreshMoves();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RefreshTestNavigator();
#endif

        // Generated results belong to their destination category but only enter
        // the board after their source category has been collected.
        List<LevelWordEntry> entries = level.categories
            .SelectMany(category => category.words.Select(word => new LevelWordEntry
                { categoryId = category.id, word = word }))
            .Where(entry => !level.categories.Any(source => source.transformsOnComplete &&
                source.transformResult != null && source.transformResultCategoryId == entry.categoryId &&
                string.Equals(source.transformResult.text, entry.word.text, System.StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Shuffle every load/restart rather than displaying category groups.
        for (int i = entries.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (entries[i], entries[j]) = (entries[j], entries[i]);
        }
        for (int i = 0; i < entries.Count; i++)
        {
            LevelWordEntry entry = entries[i];
            Category category = level.categories.FirstOrDefault(item => item.id == entry.categoryId);
            Sprite sprite = null;
            if (entry.word.useIcon && iconLibrary != null)
                iconLibrary.TryGetIcon(entry.word.spriteKey, out sprite);
            LooseMagnet magnet = CreateMagnet(entry.word.text, entry.categoryId,
                category == null ? entry.categoryId : category.name, sprite);
            magnet.RectTransform().anchoredPosition = GetScatteredPosition(i, entries.Count);
            if (level.levelNumber == 1)
                magnet.RectTransform().localRotation = Quaternion.identity;
        }
    }

    public void UpdateHoveredHolder(Vector2 screenPosition, LooseMagnet magnet)
    {
        CategoryHolder next = holders.FirstOrDefault(holder =>
            RectTransformUtility.RectangleContainsScreenPoint(holder.transform as RectTransform, screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera));
        if (next == hoveredHolder) return;
        if (hoveredHolder != null) hoveredHolder.SetHighlighted(false, true);
        hoveredHolder = next;
        if (hoveredHolder != null) hoveredHolder.SetHighlighted(true, hoveredHolder.CanAccept(magnet));
    }

    public void EndMagnetDrag(Vector2 screenPosition, LooseMagnet magnet)
    {
        UpdateHoveredHolder(screenPosition, magnet);
        CategoryHolder target = hoveredHolder;
        if (hoveredHolder != null) hoveredHolder.SetHighlighted(false, true);
        hoveredHolder = null;
        if (target == null)
        {
            magnet.ReturnInsideBottomArea();
            return;
        }
        if (!target.TryAccept(magnet))
        {
            magnet.Reject();
            magnet.ReturnInsideBottomArea();
            return;
        }

        remainingMoves = Mathf.Max(0, remainingMoves - 1);
        RefreshMoves();
    }

    public void OnHolderCompleted(CategoryHolder holder, string categoryId, LooseMagnet[] magnets)
    {
        if (!completedCategories.Add(categoryId)) return;
        RefreshCategoryProgress();
        Category source = currentLevel.categories.FirstOrDefault(category => category.id == categoryId);
        if (source != null && source.transformsOnComplete && source.transformResult != null)
        {
            pendingTransformations++;
            StartCoroutine(TransformHolder(holder, magnets, source));
            return;
        }
        if (completedCategories.Count >= totalCategories && pendingTransformations == 0)
        {
            StartCoroutine(CompleteLevel());
            return;
        }
        // Non-transforming categories remain locked in their completed holder.
    }

    public void RestartCurrentLevel()
    {
        Time.timeScale = 1f;
        LoadLevel(activeLevelIndex);
    }

    private void RefreshCategoryProgress()
    {
        float ratio = totalCategories > 0 ? completedCategories.Count / (float)totalCategories : 0f;
        if (categoryProgress != null)
        {
            categoryProgress.type = Image.Type.Filled;
            categoryProgress.fillMethod = Image.FillMethod.Horizontal;
            categoryProgress.fillOrigin = 0;
            categoryProgress.fillAmount = ratio;
        }
        if (categoryKnob != null)
        {
            categoryKnob.anchorMin = categoryKnob.anchorMax = new Vector2(ratio, 0.5f);
            categoryKnob.anchoredPosition = Vector2.zero;
        }
        if (categoryProgressText != null) categoryProgressText.text = completedCategories.Count.ToString();
    }

    private IEnumerator TransformHolder(CategoryHolder holder, LooseMagnet[] magnets, Category source)
    {
        Vector3 mergePoint = holder.transform.position;
        foreach (var magnet in magnets)
        {
            magnet.transform.DOKill();
            magnet.transform.DOMove(mergePoint, 0.3f).SetEase(Ease.InOutCubic);
            magnet.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack);
        }
        yield return new WaitForSeconds(0.32f);
        Category target = currentLevel.categories.FirstOrDefault(category => category.id == source.transformResultCategoryId);
        Sprite sprite = null;
        if (source.transformResult.useIcon && iconLibrary != null)
            iconLibrary.TryGetIcon(source.transformResult.spriteKey, out sprite);
        LooseMagnet result = CreateMagnet(source.transformResult.text, source.transformResultCategoryId,
            target == null ? source.transformResultCategoryId : target.name, sprite);
        RectTransform rect = result.RectTransform();
        rect.position = mergePoint;
        rect.localScale = Vector3.zero;
        result.BringToFront();
        holder.ClearForReuse();
        yield return rect.DOScale(1f, 0.24f).SetEase(Ease.OutBack).WaitForCompletion();
        Vector2 landing = ClampToBottomArea(new Vector2(rect.anchoredPosition.x,
            bottomArea.rect.yMax - rect.rect.height), rect.rect.size);
        yield return rect.DOAnchorPos(landing, 0.55f).SetEase(Ease.OutBounce).WaitForCompletion();
        // Keep the newest result above older magnets, including other results
        // that may have finished falling while this animation was running.
        result.BringToFront();
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        pendingTransformations--;
        if (pendingTransformations == 0 && completedCategories.Count >= totalCategories)
            StartCoroutine(CompleteLevel());
    }

    public Vector2 ClampToBottomArea(Vector2 position, Vector2 size)
    {
        Rect area = bottomArea.rect;
        Vector2 half = size * 0.5f;
        return new Vector2(
            Mathf.Clamp(position.x, area.xMin + half.x, area.xMax - half.x),
            Mathf.Clamp(position.y, area.yMin + half.y, area.yMax - half.y));
    }

    private IEnumerator CompleteLevel()
    {
        gameEnded = true;
        yield return new WaitForSeconds(0.55f);
        int next = Mathf.Min(activeLevelIndex + 1, database.levels.Count - 1);
        SaveSystem.CompleteLevel(activeLevelIndex, next);
        if (winScreen != null)
        {
            Canvas resultCanvas = winScreen.GetComponent<Canvas>();
            if (resultCanvas == null) resultCanvas = winScreen.AddComponent<Canvas>();
            resultCanvas.overrideSorting = true;
            resultCanvas.sortingOrder = 1000;
            if (winScreen.GetComponent<GraphicRaycaster>() == null)
                winScreen.AddComponent<GraphicRaycaster>();
            winScreen.SetActive(true);
            winScreen.transform.SetAsLastSibling();
            // Nested magnet canvases must not remain visible over result UI.
            bottomArea.gameObject.SetActive(false);
            foreach (var holder in holders) holder.gameObject.SetActive(false);
        }
    }

    private void WireWinScreenButtons()
    {
        if (winScreen == null) return;
        Button continueButton = winScreen.transform.Find("Panel/ContinueButton")?.GetComponent<Button>();
        Button doubleRewardButton = winScreen.transform.Find("Panel/DoubleRewardButton")?.GetComponent<Button>();
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueAfterWin);
            continueButton.onClick.AddListener(ContinueAfterWin);
        }
        if (doubleRewardButton != null)
        {
            doubleRewardButton.onClick.RemoveListener(DoubleRewardAndContinue);
            doubleRewardButton.onClick.AddListener(DoubleRewardAndContinue);
        }
    }

    private void ContinueAfterWin()
    {
        if (winScreen != null) winScreen.SetActive(false);
        LoadLevel(Mathf.Min(activeLevelIndex + 1, database.levels.Count - 1));
    }

    private void DoubleRewardAndContinue()
    {
        if (!rewardDoubled)
        {
            rewardDoubled = true;
            SaveSystem.AddGold(40);
            TextMeshProUGUI reward = winScreen.transform.Find("Panel/Reward/Value")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI balance = winScreen.transform.Find("Panel/Gold/Value")?.GetComponent<TextMeshProUGUI>();
            if (reward != null) reward.text = "+80 GOLD";
            if (balance != null) balance.text = SaveSystem.Current.gold.ToString();
        }
        ContinueAfterWin();
    }

    private LooseMagnet CreateMagnet(string text, string categoryId, string categoryName, Sprite sprite)
    {
        GameObject root = new GameObject($"Magnet - {text}", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup), typeof(LooseMagnet));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(bottomArea, false);
        rect.sizeDelta = sprite == null ? new Vector2(213f, 150f) : new Vector2(250.5f, 250.5f);
        Image background = root.GetComponent<Image>();
        background.sprite = magnetSprite;
        background.type = Image.Type.Sliced;

        Image icon = CreateImage("Icon", rect);
        Stretch(icon.rectTransform, sprite == null ? 13f : 19.5f);
        icon.preserveAspect = true;
        TextMeshProUGUI label = CreateText("Label", rect);
        Stretch(label.rectTransform, 6f);
        ApplyMagnetTextStyle(label);
        label.raycastTarget = false;

        LooseMagnet magnet = root.GetComponent<LooseMagnet>();
        magnet.ConfigureVisuals(background, icon, label);
        magnet.Initialize(this, text, categoryId, categoryName, sprite);
        return magnet;
    }

    private void ApplyMagnetTextStyle(TextMeshProUGUI label)
    {
        if (magnetTextStyle == null)
        {
            label.font = font;
            return;
        }

        label.font = magnetTextStyle.font;
        label.fontSharedMaterial = magnetTextStyle.fontSharedMaterial;
        label.color = magnetTextStyle.color;
        label.fontSize = magnetTextStyle.fontSize;
        label.enableAutoSizing = magnetTextStyle.enableAutoSizing;
        label.fontSizeMin = magnetTextStyle.fontSizeMin;
        label.fontSizeMax = magnetTextStyle.fontSizeMax;
        label.fontStyle = magnetTextStyle.fontStyle;
        label.fontWeight = magnetTextStyle.fontWeight;
        label.alignment = magnetTextStyle.alignment;
        label.characterSpacing = magnetTextStyle.characterSpacing;
        label.wordSpacing = magnetTextStyle.wordSpacing;
        label.lineSpacing = magnetTextStyle.lineSpacing;
        label.extraPadding = magnetTextStyle.extraPadding;
    }

    private Vector2 GetScatteredPosition(int index, int count)
    {
        Rect area = bottomArea.rect;
        int columns = 4;
        int rows = Mathf.CeilToInt(count / (float)columns);
        int column = index % columns;
        int row = index / columns;
        if (currentLevel != null && currentLevel.levelNumber == 1)
        {
            // Compact, aligned tutorial rows instead of stretching two rows
            // across the full board. Keep the shuffled word order intact.
            float columnStep = Mathf.Max(0f, (area.width - 260f) / 3f);
            int rowCount = Mathf.Min(columns, count - row * columns);
            float alignedX = area.center.x + (column - (rowCount - 1) * 0.5f) * columnStep;
            float rowStep = rows > 1
                ? Mathf.Min(290f, Mathf.Max(0f, area.height - 300f) / (rows - 1))
                : 0f;
            return new Vector2(alignedX, area.yMax - 150f - row * rowStep);
        }
        float x = Mathf.Lerp(area.xMin + 130f, area.xMax - 130f, columns == 1 ? 0.5f : column / 3f);
        float y = Mathf.Lerp(area.yMax - 90f, area.yMin + 90f, rows <= 1 ? 0f : row / (float)(rows - 1));
        return new Vector2(x + Random.Range(-22f, 22f), y + Random.Range(-18f, 18f));
    }

    private void ClearLooseMagnets()
    {
        if (bottomArea == null) return;
        foreach (Transform child in bottomArea.Cast<Transform>().ToArray()) Destroy(child.gameObject);
    }

    private void RefreshMoves()
    {
        if (moveLabel != null) moveLabel.text = remainingMoves.ToString();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void CreateTestNavigator()
    {
        Image panel = CreateImage("CategoryTestLevelNavigator", transform);
        panel.color = new Color(0.1f, 0.13f, 0.18f, 0.95f);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-18f, -182f);
        rect.sizeDelta = new Vector2(360f, 66f);
        Canvas overlay = panel.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 950;
        panel.gameObject.AddComponent<GraphicRaycaster>();
        var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        testPrevious = AddTestButton(panel.transform, "<", () => LoadLevel(activeLevelIndex - 1));
        testLevelLabel = CreateText("CurrentLevel", panel.transform);
        testLevelLabel.font = font;
        testLevelLabel.color = Color.white;
        testLevelLabel.fontSize = 25f;
        testLevelLabel.alignment = TextAlignmentOptions.Center;
        testLevelLabel.raycastTarget = false;
        testLevelLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 138f;
        testNext = AddTestButton(panel.transform, ">", () => LoadLevel(activeLevelIndex + 1));
        AddTestButton(panel.transform, "R", () => LoadLevel(activeLevelIndex));
        RefreshTestNavigator();
    }

    private Button AddTestButton(Transform parent, string caption, UnityEngine.Events.UnityAction action)
    {
        Image image = CreateImage(caption, parent);
        image.color = new Color(1f, 0.8f, 0.25f);
        image.gameObject.AddComponent<LayoutElement>().preferredWidth = 60f;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var label = CreateText("Label", image.transform);
        label.text = caption;
        label.richText = false;
        Stretch(label.rectTransform, 2f);
        label.font = font;
        label.fontSize = 30f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return button;
    }

    private void RefreshTestNavigator()
    {
        if (testLevelLabel == null) return;
        testLevelLabel.text = $"LEVEL {currentLevel.levelNumber}";
        testPrevious.interactable = activeLevelIndex > 0;
        testNext.interactable = activeLevelIndex < database.levels.Count - 1;
    }
#endif

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        return go.GetComponent<TextMeshProUGUI>();
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

}

internal static class LooseMagnetExtensions
{
    public static RectTransform RectTransform(this LooseMagnet magnet) => magnet.transform as RectTransform;
}
