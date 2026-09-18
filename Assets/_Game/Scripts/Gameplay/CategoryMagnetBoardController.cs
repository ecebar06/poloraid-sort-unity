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
    private readonly HashSet<string> retainedFinalCategories = new HashSet<string>();
    private GameObject outOfMovesScreen;
    private bool gameEnded;
    private bool rewardDoubled;
    private Canvas canvas;
    private Sprite magnetSprite;
    private TMP_FontAsset font;
    private LevelData currentLevel;
    private int pendingTransformations;
    private int creditedCategories;
    private Tween progressTween;
    private readonly List<RectTransform> completionEffects = new List<RectTransform>();
    private List<LevelWordEntry> mixedStock;
    private int stockCursor;
    private readonly Dictionary<LooseMagnet, int> looseSlots = new Dictionary<LooseMagnet, int>();
    private readonly Dictionary<LooseMagnet, Vector2> reservedLandings = new Dictionary<LooseMagnet, Vector2>();
    public bool CanPlayCategory(string id) => !completedCategories.Contains(id);
    private int SlotCount => currentLevel.categories.Count <= 4 &&
        currentLevel.categories.All(c => !c.transformsOnComplete)
        ? currentLevel.categories.Count * 4 : MagnetDealPlan.Capacity;
    private Image categoryProgress;
    private RectTransform categoryKnob;
    private TextMeshProUGUI categoryProgressText;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private TextMeshProUGUI testLevelLabel;
    private Button testPrevious, testNext;
#endif

    public Canvas Canvas => canvas;
    public RectTransform BottomArea => bottomArea;
    public bool IsInputLocked => gameEnded || remainingMoves <= 0 || pendingTransformations > 0 ||
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
        MagnetPopupUI popup = FindFirstObjectByType<MagnetPopupUI>(FindObjectsInactive.Include);
        outOfMovesScreen = popup == null ? null : popup.TimeUpScreen;
        if (popup != null)
        {
            Wire(popup.WatchAdButton, ContinueWithAd);
            Wire(popup.SpendGoldButton, ContinueWithGold);
        }
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
        if (outOfMovesScreen != null) outOfMovesScreen.SetActive(false);
        ClearCompletionEffects();
        progressTween?.Kill();
        creditedCategories = 0;
        if (categoryKnob != null)
        {
            categoryKnob.DOKill();
            categoryKnob.localScale = Vector3.one;
        }
        bottomArea.gameObject.SetActive(true);
        foreach (var holder in holders) holder.gameObject.SetActive(true);
        pendingTransformations = 0;
        currentLevel = level;
        stockCursor = 0;
        looseSlots.Clear();
        reservedLandings.Clear();
        ClearLooseMagnets();
        completedCategories.Clear();
        retainedFinalCategories.Clear();
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

        try
        {
            mixedStock = MixedMagnetStock.IsSafe(level, level.orderedWords)
                ? level.orderedWords.ToList() : MixedMagnetStock.Generate(level);
        }
        catch (System.InvalidOperationException error)
        {
            gameEnded = true;
            Debug.LogError(error.Message);
            return;
        }
        RefillStock();
    }

    private void RefillStock()
    {
        if (gameEnded) return;
        if (pendingTransformations > 0) return;
        LevelData level = currentLevel;
        // Consume a fixed mixed queue: 4 replacement words for a final, 3 plus
        // the generated output for a transformation. Holder-contained words
        // still count toward this logical budget, avoiding hidden over-deals.
        int outputs = level.categories.Count(c => c.transformsOnComplete && completedCategories.Contains(c.id));
        int targetCursor = Mathf.Min(mixedStock.Count, 16 + completedCategories.Count * 4 - outputs);
        List<LevelWordEntry> entries = mixedStock.Skip(stockCursor).Take(targetCursor - stockCursor).ToList();
        stockCursor = targetCursor;
        for (int i = 0; i < entries.Count; i++)
        {
            LevelWordEntry entry = entries[i];
            Category category = level.categories.FirstOrDefault(item => item.id == entry.categoryId);
            Sprite sprite = null;
            if (entry.word.useIcon && iconLibrary != null)
                iconLibrary.TryGetIcon(entry.word.spriteKey, out sprite);
            LooseMagnet magnet = CreateMagnet(entry.word.text, entry.categoryId,
                category == null ? entry.categoryId : category.name, sprite);
            PlaceInFreeSlot(magnet);
            magnet.RectTransform().localScale = Vector3.one * 1.18f;
            magnet.RectTransform().DOScale(1f, 0.28f).SetEase(Ease.OutCubic);
        }
    }

    private Vector2 SlotPosition(int slot)
    {
        Rect area = bottomArea.rect;
        float cellWidth = area.width / 4f;
        float cellHeight = Mathf.Min(280f, area.height / 4f);
        return new Vector2(area.xMin + cellWidth * (slot % 4 + 0.5f),
            area.yMax - cellHeight * (slot / 4 + 0.5f));
    }

    private void PlaceInFreeSlot(LooseMagnet magnet)
    {
        var free = Enumerable.Range(0, SlotCount).Where(i => !looseSlots.ContainsValue(i)).ToList();
        int slot = free[Random.Range(0, free.Count)];
        looseSlots.Add(magnet, slot);
        var size = magnet.RectTransform().rect.size;
        var candidates = new List<Vector2>();
        for (int i = 0; i < 400; i++)
            candidates.Add(ClampToBottomArea(new Vector2(Random.Range(bottomArea.rect.xMin, bottomArea.rect.xMax),
                Random.Range(bottomArea.rect.yMin, bottomArea.rect.yMax)), size));
        // Grid locations are only a last resort, never the preferred layout.
        candidates.AddRange(Enumerable.Range(0, SlotCount).Select(SlotPosition).OrderBy(_ => Random.value));
        // Placement consults actual positions, not old grid slots: free drags may overlap.
        float Clearance(Vector2 p) => looseSlots.Keys.Where(m => m != magnet && m != null)
            .Select(m => {
                Vector2 delta = p - (reservedLandings.TryGetValue(m, out Vector2 landing) ? landing : m.RectTransform().anchoredPosition);
                Vector2 half = (size + m.RectTransform().rect.size) * 0.5f + Vector2.one * 8f;
                return Mathf.Max(Mathf.Abs(delta.x) - half.x, Mathf.Abs(delta.y) - half.y);
            }).DefaultIfEmpty(float.MaxValue).Min();
        Vector2 best = candidates.OrderByDescending(Clearance).First();
        foreach (Vector2 candidate in candidates)
            if (Clearance(candidate) >= 0f) { best = candidate; break; }
        magnet.RectTransform().anchoredPosition = best;
    }

    public Vector2 GetLooseLanding(LooseMagnet magnet)
    {
        // No grid snap: preserve the drop point, including deliberate overlaps.
        var corners = new Vector3[4];
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(corners);
        var localCorners = corners.Select(p => (Vector2)bottomArea.InverseTransformPoint(p)).ToArray();
        Vector2 half = magnet.RectTransform().rect.size * 0.5f;
        Vector2 position = magnet.RectTransform().anchoredPosition;
        return new Vector2(Mathf.Clamp(position.x, localCorners.Min(p => p.x) + half.x, localCorners.Max(p => p.x) - half.x),
            Mathf.Clamp(position.y, localCorners.Min(p => p.y) + half.y, localCorners.Max(p => p.y) - half.y));
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
            // Pointer can be outside while the dragged card still covers a holder.
            // Reject that overlap as well, rather than leaving a fake collected card.
            if (OverlapsHolder(magnet)) magnet.Reject();
            else magnet.ReturnInsideBottomArea();
            return;
        }
        if (!target.TryAccept(magnet))
        {
            // Dropping onto a holder that already belongs to another
            // category is still a spent move, even though the magnet is
            // rejected and returned to the loose area.
            if (!target.IsEmpty && target.CategoryId != magnet.CategoryId)
            {
                remainingMoves = Mathf.Max(0, remainingMoves - 1);
                RefreshMoves();
            }
            magnet.Reject();
            return;
        }

        remainingMoves = Mathf.Max(0, remainingMoves - 1);
        looseSlots.Remove(magnet);
        RefreshMoves();
    }

    private void LateUpdate()
    {
        if (currentLevel == null || gameEnded || remainingMoves > 0 || pendingTransformations > 0) return;
        // The last legal move may complete a category. Let its celebration and
        // transformations finish before choosing win versus out-of-moves.
        if (holders.Any(h => h.IsFull && !completedCategories.Contains(h.CategoryId))) return;
        if (completedCategories.Count >= totalCategories) return;
        gameEnded = true;
        if (outOfMovesScreen == null)
        {
            Debug.LogError("TimeUpScreen is missing from the authored MagnetPopupUI.", this);
            return;
        }
        MagnetPopupUI popupScreen = FindFirstObjectByType<MagnetPopupUI>(FindObjectsInactive.Include);
        if (popupScreen != null)
        {
            popupScreen.ShowTimeUp();
            return;
        }
        outOfMovesScreen.SetActive(true);
    }

    private bool OverlapsHolder(LooseMagnet magnet)
    {
        Rect LocalBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var points = corners.Select(p => (Vector2)bottomArea.InverseTransformPoint(p)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y),
                points.Max(p => p.x), points.Max(p => p.y));
        }
        Rect magnetBounds = LocalBounds(magnet.RectTransform());
        return holders.Any(h => h != null && h.gameObject.activeInHierarchy &&
            magnetBounds.Overlaps(LocalBounds((RectTransform)h.transform)));
    }

    public void OnHolderCompleted(CategoryHolder holder, string categoryId, LooseMagnet[] magnets)
    {
        if (!completedCategories.Add(categoryId)) return;
        Category source = currentLevel.categories.FirstOrDefault(category => category.id == categoryId);
        // Conservatively reserve a holder for every unfinished category,
        // including transformation stages. Never retain while stock remains.
        bool keepFinalHolder = mixedStock != null && stockCursor >= mixedStock.Count &&
            totalCategories - completedCategories.Count <= holders.Length - retainedFinalCategories.Count - 1;
        bool transforms = source != null && source.transformsOnComplete && source.transformResult != null;
        if (!transforms && keepFinalHolder) retainedFinalCategories.Add(categoryId);
        if (!transforms && !keepFinalHolder)
        {
            pendingTransformations++;
            StartCoroutine(FlyFinalToProgress(holder, magnets, source));
            return;
        }
        creditedCategories++;
        RefreshCategoryProgress(true);
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
        // This completion was one of the last four categories (including itself).
        // Keep terminal holders filled and badge-colored through the finish.
        // Transformations above still recycle, since their output is required.
        if (!keepFinalHolder) holder.ClearForReuse();
        RefillStock();
    }

    public void RestartCurrentLevel()
    {
        Time.timeScale = 1f;
        LoadLevel(activeLevelIndex);
    }

    private void RefreshCategoryProgress(bool animate = false)
    {
        float ratio = totalCategories > 0 ? creditedCategories / (float)totalCategories : 0f;
        progressTween?.Kill();
        if (categoryProgressText != null) categoryProgressText.text = creditedCategories.ToString();
        if (animate && categoryProgress != null)
            progressTween = DOTween.To(() => categoryProgress.fillAmount, ApplyProgressRatio, ratio, 0.3f).SetEase(Ease.OutCubic);
        else ApplyProgressRatio(ratio);
    }

    private void ApplyProgressRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        // The counter badge has a visible width. Pinning its centre directly
        // to 0/1 makes the first and last progress slices look shorter because
        // half of the badge hangs outside the track. Keep its centre inset by
        // half its width so every category step occupies the same visual space.
        float visualRatio = GetProgressVisualRatio(ratio);
        if (categoryProgress != null)
        {
            categoryProgress.type = Image.Type.Filled;
            categoryProgress.fillMethod = Image.FillMethod.Horizontal;
            categoryProgress.fillOrigin = 0;
            categoryProgress.fillAmount = visualRatio;
        }
        if (categoryKnob != null)
        {
            categoryKnob.anchorMin = categoryKnob.anchorMax = new Vector2(visualRatio, 0.5f);
            categoryKnob.anchoredPosition = Vector2.zero;
        }
    }

    private float GetProgressVisualRatio(float ratio)
    {
        if (categoryKnob == null || categoryKnob.parent is not RectTransform track || track.rect.width <= 0f)
            return ratio;

        float halfBadgeWidth = categoryKnob.rect.width * 0.5f;
        float inset = Mathf.Clamp(halfBadgeWidth / track.rect.width, 0f, 0.45f);
        return Mathf.Lerp(inset, 1f - inset, ratio);
    }

    private IEnumerator FlyFinalToProgress(CategoryHolder holder, LooseMagnet[] magnets, Category category)
    {
        Vector3 center = holder.transform.position;
        foreach (var magnet in magnets)
        {
            magnet.transform.DOKill();
            magnet.transform.DOMove(center, 0.3f).SetEase(Ease.InOutCubic);
            magnet.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack);
        }
        yield return new WaitForSeconds(0.32f);

        Image badge = CreateImage("CompletedCategoryFlight", transform);
        RectTransform rect = badge.rectTransform;
        completionEffects.Add(rect);
        rect.sizeDelta = new Vector2(280f, 95f);
        rect.position = center;
        Image sourceBadge = holder.transform.Find("CategoryBadge")?.GetComponent<Image>();
        badge.sprite = sourceBadge != null ? sourceBadge.sprite : magnetSprite;
        badge.color = sourceBadge != null ? sourceBadge.color : Color.white;
        badge.type = Image.Type.Sliced;
        badge.raycastTarget = false;
        Canvas effectCanvas = badge.gameObject.AddComponent<Canvas>();
        effectCanvas.overrideSorting = true;
        effectCanvas.sortingOrder = 900;
        TextMeshProUGUI title = CreateText("CategoryName", rect);
        Stretch(title.rectTransform, 8f);
        TextMeshProUGUI sourceTitle = holder.transform.Find("CategoryLabel")?.GetComponent<TextMeshProUGUI>();
        title.font = sourceTitle != null ? sourceTitle.font : font;
        if (sourceTitle != null) title.fontSharedMaterial = sourceTitle.fontSharedMaterial;
        title.color = sourceTitle != null ? sourceTitle.color : Color.black;
        title.fontStyle = sourceTitle != null ? sourceTitle.fontStyle : FontStyles.Bold;
        title.text = category != null ? category.name : holder.CategoryId;
        title.alignment = TextAlignmentOptions.Center;
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = 40f;
        title.raycastTarget = false;
        rect.localScale = Vector3.zero;
        yield return rect.DOScale(1f, 0.22f).SetEase(Ease.OutBack).WaitForCompletion();
        yield return new WaitForSeconds(0.35f);
        Vector3 start = rect.position;
        rect.DOScale(0.18f, 0.55f).SetEase(Ease.InCubic);
        yield return DOVirtual.Float(0f, 1f, 0.55f, t => {
            Vector3 target = categoryKnob != null ? categoryKnob.position : start;
            rect.position = Vector3.Lerp(start, target, t);
        }).SetEase(Ease.InOutCubic).SetTarget(rect).WaitForCompletion();
        completionEffects.Remove(rect);
        rect.DOKill();
        Destroy(badge.gameObject);
        creditedCategories++;
        RefreshCategoryProgress(true);
        if (categoryKnob != null)
        {
            categoryKnob.DOKill();
            categoryKnob.localScale = Vector3.one;
            categoryKnob.DOPunchScale(Vector3.one * 0.2f, 0.3f, 4, 0.5f);
        }
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        holder.ClearForReuse();
        pendingTransformations--;
        RefillStock();
        if (pendingTransformations == 0 && completedCategories.Count >= totalCategories)
            StartCoroutine(CompleteLevel());
    }

    private void ClearCompletionEffects()
    {
        foreach (RectTransform effect in completionEffects)
        {
            if (effect == null) continue;
            effect.DOKill();
            Destroy(effect.gameObject);
        }
        completionEffects.Clear();
    }

    private void OnDestroy()
    {
        progressTween?.Kill();
        ClearCompletionEffects();
    }

    private IEnumerator TransformHolder(CategoryHolder holder, LooseMagnet[] magnets, Category source)
    {
        Vector3 mergePoint = holder.transform.position;
        foreach (var magnet in magnets)
        {
            magnet.transform.DOKill();
            magnet.transform.DOMove(mergePoint, 0.3f).SetEase(Ease.InOutCubic);
            // Keep the four cards visible as a tight stack while the
            // transformation result is revealed at the holder.
            magnet.transform.DOScale(0.28f, 0.3f).SetEase(Ease.InBack);
        }
        yield return new WaitForSeconds(0.32f);
        Category target = currentLevel.categories.FirstOrDefault(category => category.id == source.transformResultCategoryId);
        Sprite sprite = null;
        if (source.transformResult.useIcon && iconLibrary != null)
            iconLibrary.TryGetIcon(source.transformResult.spriteKey, out sprite);
        LooseMagnet result = CreateMagnet(source.transformResult.text, source.transformResultCategoryId,
            target == null ? source.transformResultCategoryId : target.name, sprite);
        RectTransform rect = result.RectTransform();
        PlaceInFreeSlot(result);
        Vector2 landing = rect.anchoredPosition;
        reservedLandings[result] = landing;
        rect.position = mergePoint;
        rect.localScale = Vector3.zero;
        result.BringToFront();
        // Show the result at the merge point first. Only after it is visible
        // do we release the holder and send the new magnet to its landing.
        yield return rect.DOScale(1f, 0.24f).SetEase(Ease.OutBack).WaitForCompletion();
        yield return new WaitForSeconds(0.28f);
        holder.ClearForReuse();
        // A transformation result should settle cleanly; a bounce makes it
        // look as if it is being rejected by the board.
        yield return rect.DOAnchorPos(landing, 0.55f).SetEase(Ease.OutCubic).WaitForCompletion();
        reservedLandings.Remove(result);
        // Keep the newest result above older magnets, including other results
        // that may have finished falling while this animation was running.
        result.BringToFront();
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        pendingTransformations--;
        RefillStock();
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
            TextMeshProUGUI[] resultLabels = winScreen.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in resultLabels)
            {
                if (label != null && label.name == "Title")
                {
                    label.text = $"LEVEL {currentLevel.levelNumber}";
                    break;
                }
            }
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
        Transform content = winScreen.transform.Find("Content") ?? winScreen.transform.Find("Panel");
        Button continueButton = content?.Find("ContinueButton")?.GetComponent<Button>();
        Button doubleRewardButton = content?.Find("DoubleRewardButton")?.GetComponent<Button>();
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

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ContinueWithAd()
    {
        ResumeAfterOutOfMoves(5);
    }

    private void ContinueWithGold()
    {
        if (!SaveSystem.TrySpendGold(900)) return;
        ResumeAfterOutOfMoves(5);
    }

    private void ResumeAfterOutOfMoves(int extraMoves)
    {
        remainingMoves = Mathf.Max(1, extraMoves);
        gameEnded = false;
        if (outOfMovesScreen != null) outOfMovesScreen.SetActive(false);
        bottomArea.gameObject.SetActive(true);
        foreach (CategoryHolder holder in holders)
            if (holder != null) holder.gameObject.SetActive(true);
        RefreshMoves();
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
            Transform content = winScreen.transform.Find("Content") ?? winScreen.transform.Find("Panel");
            TextMeshProUGUI reward = content?.Find("Reward/Value")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI balance = content?.Find("Gold/Value")?.GetComponent<TextMeshProUGUI>();
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
        // Icons belong inside the same readable magnet footprint as text.
        // A shared size also prevents square icon assets from crowding nearby magnets.
        // Slightly wider magnets improve label readability without making the
        // 16-magnet board feel crowded.
        rect.sizeDelta = new Vector2(223f, 150f);
        float fit = Mathf.Min(1f, bottomArea.rect.width / 4f / 280f,
            Mathf.Min(280f, bottomArea.rect.height / 4f) / 280f);
        rect.sizeDelta *= fit;
        Image background = root.GetComponent<Image>();
        background.sprite = magnetSprite;
        background.type = Image.Type.Sliced;

        Image icon = CreateImage("Icon", rect);
        // Generated icons contain some transparent breathing room themselves;
        // use a small UI inset so the actual artwork stays legible while still
        // remaining completely inside the magnet.
        Stretch(icon.rectTransform, 4f);
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
