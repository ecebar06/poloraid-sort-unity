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
    private GameObject outOfMovesScreen;
    private bool gameEnded;
    private bool rewardDoubled;
    private Canvas canvas;
    private Sprite magnetSprite;
    private Image.Type magnetImageType = Image.Type.Sliced;
    private Vector2 magnetSceneSize = new Vector2(245f, 165f);
    private TMP_FontAsset font;
    private LevelData currentLevel;
    private int pendingTransformations;
    private int creditedCategories;
    private bool isRefilling;
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
    private RectTransform categoryProgressTarget;
    private RectTransform printerRect;
    private TextMeshProUGUI categoryProgressText;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private TextMeshProUGUI testLevelLabel;
    private Button testPrevious, testNext;
    private TextMeshProUGUI testTimerLabel;
    private TextMeshProUGUI testResultTimeLabel;
    private bool testTimerRunning;
    private float testTimerStartedAt;
    private float testTimerElapsed;
    private float testTimerNextRefreshAt;
#endif

    public Canvas Canvas => canvas;
    public RectTransform BottomArea => bottomArea;
    public bool IsInputLocked => gameEnded || remainingMoves <= 0 || pendingTransformations > 0 || isRefilling ||
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
        printerRect = transform.Find("PolaroidPrinter") as RectTransform;

        if (legacyTableController != null)
        {
            legacyTableController.enabled = false;
            if (legacyTableController.wordContainer != null)
                legacyTableController.wordContainer.gameObject.SetActive(false);
        }

        // The Polaroid scene is artist-authored. Legacy fridge objects are
        // kept only as inactive migration data and must never cover it in Play Mode.
        Canvas sceneCanvas = GetComponentInParent<Canvas>();
        if (sceneCanvas != null && sceneCanvas.transform.Find("PolaroidThemeSceneLayoutV2") != null)
        {
            SetSceneChildActive(sceneCanvas.transform, "GameHUD/FridgeTopHud", false);
            SetSceneChildActive(sceneCanvas.transform, "background", false);
            SetSceneChildActive(sceneCanvas.transform, "PolaroidThemeBackground", true);
            SetSceneChildActive(sceneCanvas.transform, "PolaroidTopHud", true);
        }
    }

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        Transform hud = canvas == null ? null : canvas.transform.Find("PolaroidTopHud");
        if (hud == null) hud = transform.parent.Find("FridgeTopHud");
        if (hud != null)
        {
            RectTransform progressTrack = hud.Find("ProgressTrack") as RectTransform ??
                                          hud.Find("ProgressCard/ProgressTrack") as RectTransform;
            levelLabel = hud.Find("LevelCard/LevelLabel")?.GetComponent<TextMeshProUGUI>() ??
                         hud.Find("LevelLabel")?.GetComponent<TextMeshProUGUI>() ?? levelLabel;
            moveLabel = hud.Find("MovesCard/MoveCount")?.GetComponent<TextMeshProUGUI>() ??
                        hud.Find("MovesMagnet/MoveCount")?.GetComponent<TextMeshProUGUI>() ?? moveLabel;
            categoryProgress = hud.Find("ProgressTrack/Fill")?.GetComponent<Image>() ??
                               hud.Find("ProgressCard/ProgressTrack/Fill")?.GetComponent<Image>();
            categoryKnob = hud.Find("ProgressTrack/Knob") as RectTransform ??
                           hud.Find("ProgressCard/ProgressTrack/Knob") as RectTransform;
            categoryProgressText = hud.Find("ProgressTrack/Progress")?.GetComponent<TextMeshProUGUI>() ??
                                   hud.Find("ProgressCard/ProgressTrack/Knob/Value")?.GetComponent<TextMeshProUGUI>();
            // The new Polaroid HUD has no Knob object. Keep a separate, always
            // valid flight target so completion labels actually travel to the
            // visible progress track instead of shrinking in place.
            categoryProgressTarget = categoryKnob != null ? categoryKnob : progressTrack;
            if (categoryProgressTarget == null && categoryProgress != null)
                categoryProgressTarget = categoryProgress.rectTransform;
        }
        database = LevelDatabase.LoadFromResources();
        iconLibrary = Resources.Load<WordIconLibrary>("Data/MainWordIconLibrary");
        font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (magnetTextStyle != null)
        {
            if (magnetTextStyle.font != null) font = magnetTextStyle.font;
            Transform template = magnetTextStyle.transform.parent;
            if (template is RectTransform templateRect)
            {
                Vector2 authoredSize = templateRect.sizeDelta;
                if (authoredSize.x > 0f && authoredSize.y > 0f)
                    magnetSceneSize = authoredSize;
            }
            Image templateBackground = template.Find("Paper")?.GetComponent<Image>() ??
                                       template.GetComponent<Image>();
            if (templateBackground != null && templateBackground.sprite != null)
            {
                // Scene-authored card art is the source of truth. Do not replace
                // it with the legacy fridge magnet when Play Mode starts.
                magnetSprite = templateBackground.sprite;
                magnetImageType = templateBackground.type;
            }
            magnetTextStyle.transform.parent.gameObject.SetActive(false);
        }
        if (magnetSprite == null)
            magnetSprite = Resources.Load<Sprite>("Art/Fridge/Magnets/magnet_word");
        PlayerSaveData save = SaveSystem.Load();
        MagnetPopupUI popup = FindFirstObjectByType<MagnetPopupUI>(FindObjectsInactive.Include);
        outOfMovesScreen = popup == null ? null : popup.TimeUpScreen;
        BindOutOfMovesButtons(popup);
        WireWinScreenButtons();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EnsureTestTimerUi();
#endif
        LoadLevel(save.currentLevelIndex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CreateTestNavigator();
#endif
    }

    private static void SetSceneChildActive(Transform root, string path, bool active)
    {
        Transform child = root.Find(path);
        if (child != null) child.gameObject.SetActive(active);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!testTimerRunning) return;
        float now = Time.realtimeSinceStartup;
        if (now < testTimerNextRefreshAt) return;
        // The timer is for human-readable test feedback; refreshing its TMP
        // mesh every frame creates avoidable allocations and canvas rebuilds.
        testTimerNextRefreshAt = now + 0.05f;
        testTimerElapsed = Mathf.Max(0f, now - testTimerStartedAt);
        RefreshTestTimerLabel();
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
        isRefilling = false;
        currentLevel = level;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EnsureTestTimerUi();
        testTimerStartedAt = Time.realtimeSinceStartup;
        testTimerNextRefreshAt = testTimerStartedAt;
        testTimerElapsed = 0f;
        testTimerRunning = true;
        RefreshTestTimerLabel();
#endif
        stockCursor = 0;
        looseSlots.Clear();
        reservedLandings.Clear();
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
        RefillStock(false);
    }

    private void RefillStock(bool sequential)
    {
        if (gameEnded || isRefilling) return;
        if (pendingTransformations > 0) return;
        LevelData level = currentLevel;
        // Consume a fixed mixed queue: 4 replacement words for a final, 3 plus
        // the generated output for a transformation. Holder-contained words
        // still count toward this logical budget, avoiding hidden over-deals.
        int outputs = level.categories.Count(c => c.transformsOnComplete && completedCategories.Contains(c.id));
        int targetCursor = Mathf.Min(mixedStock.Count, 16 + completedCategories.Count * 4 - outputs);
        List<LevelWordEntry> entries = mixedStock.Skip(stockCursor).Take(targetCursor - stockCursor).ToList();
        stockCursor = targetCursor;
        if (entries.Count == 0) return;
        if (!sequential)
        {
            foreach (LevelWordEntry entry in entries)
                SpawnRefillEntry(entry, false);
            return;
        }
        isRefilling = true;
        StartCoroutine(SpawnRefillEntries(entries));
    }

    private IEnumerator SpawnRefillEntries(List<LevelWordEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SpawnRefillEntry(entries[i], true);

            // Reveal replacement magnets one at a time so a completed
            // category feels like a deliberate refill instead of a single
            // frame spawning an entire new row.
            if (i < entries.Count - 1)
                yield return new WaitForSecondsRealtime(0.90f);
        }
        // Keep input locked until the final printed card reaches the table.
        yield return new WaitForSecondsRealtime(0.68f);
        isRefilling = false;
    }

    private void SpawnRefillEntry(LevelWordEntry entry, bool animateFromPrinter)
    {
        Category category = currentLevel.categories.FirstOrDefault(item => item.id == entry.categoryId);
        Sprite sprite = null;
        if (entry.word.useIcon && iconLibrary != null)
            iconLibrary.TryGetIcon(entry.word.spriteKey, out sprite);
        LooseMagnet magnet = CreateMagnet(entry.word.text, entry.categoryId,
            category == null ? entry.categoryId : category.name, sprite);
        PlaceInFreeSlot(magnet);
        magnet.RectTransform().localEulerAngles = new Vector3(0f, 0f, Random.Range(-7f, 7f));
        if (animateFromPrinter) AnimatePrintedMagnet(magnet);
        else
        {
            magnet.RectTransform().localScale = Vector3.one * 1.18f;
            magnet.RectTransform().DOScale(1f, 0.28f).SetEase(Ease.OutCubic);
        }
    }

    private void AnimatePrintedMagnet(LooseMagnet magnet)
    {
        RectTransform rect = magnet.RectTransform();
        Vector2 landing = rect.anchoredPosition;
        Vector3 landingRotation = rect.localEulerAngles;
        Rect printerBounds = printerRect != null
            ? GetVisiblePrinterBounds(0f)
            : new Rect(landing.x, landing.y, 0f, 0f);
        Vector2 mouth = new Vector2(printerBounds.center.x,
            printerBounds.yMin - rect.rect.height * 0.08f);
        Vector2 ejected = mouth + Vector2.down * Mathf.Min(72f, rect.rect.height * 0.30f);

        CanvasGroup group = magnet.GetComponent<CanvasGroup>();
        if (group != null) group.blocksRaycasts = false;
        rect.DOKill();
        rect.SetAsLastSibling();
        rect.anchoredPosition = mouth;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.22f;

        float jumpPower = Mathf.Clamp(Vector2.Distance(ejected, landing) * 0.16f, 42f, 105f);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(rect.DOAnchorPos(ejected, 0.18f).SetEase(Ease.OutQuad));
        sequence.Join(rect.DOScale(0.72f, 0.18f).SetEase(Ease.OutCubic));
        sequence.Append(rect.DOJumpAnchorPos(landing, jumpPower, 1, 0.48f).SetEase(Ease.InOutQuad));
        sequence.Join(rect.DOScale(1f, 0.38f).SetEase(Ease.OutBack));
        sequence.Join(rect.DORotate(landingRotation, 0.44f).SetEase(Ease.OutCubic));
        sequence.OnComplete(() =>
        {
            if (group != null) group.blocksRaycasts = true;
        });
    }

    private void PlaceInFreeSlot(LooseMagnet magnet)
    {
        int placedCount = looseSlots.Count;
        var free = Enumerable.Range(0, SlotCount).Where(i => !looseSlots.ContainsValue(i)).ToList();
        int slot = free[Random.Range(0, free.Count)];
        looseSlots.Add(magnet, slot);
        var size = magnet.RectTransform().rect.size;
        Rect bounds = GetLooseCenterBounds(size);

        // Always occupy the two narrow tabletop pockets beside the printer.
        // A generic farthest-point search tends to prefer the much larger
        // lower area and can leave these valid pockets empty forever.
        if (placedCount < 2 && TryGetPrinterSidePosition(placedCount == 0, size, bounds, out Vector2 sidePosition))
        {
            magnet.RectTransform().anchoredPosition = sidePosition;
            return;
        }

        var candidates = new List<Vector2>();

        // Search the entire visible tabletop, not a handful of fixed rows.
        // A dense regular sample guarantees that narrow areas beside the
        // printer are represented; random samples keep the result organic.
        const int columns = 12;
        const int rows = 16;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector2 candidate = new Vector2(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, column / (float)(columns - 1)),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, row / (float)(rows - 1)));
                if (!OverlapsPrinter(candidate, size)) candidates.Add(candidate);
            }
        }
        for (int i = 0; i < 300; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax));
            if (!OverlapsPrinter(candidate, size)) candidates.Add(candidate);
        }
        if (candidates.Count == 0) candidates.Add(bounds.center);

        // Placement consults actual positions, not old grid slots: free drags may overlap.
        float Clearance(Vector2 p) => looseSlots.Keys.Where(m => m != magnet && m != null)
            .Select(m => {
                Vector2 delta = p - (reservedLandings.TryGetValue(m, out Vector2 landing) ? landing : m.RectTransform().anchoredPosition);
                Vector2 half = (size + m.RectTransform().rect.size) * 0.5f + Vector2.one * 8f;
                return Mathf.Max(Mathf.Abs(delta.x) - half.x, Mathf.Abs(delta.y) - half.y);
            }).DefaultIfEmpty(float.MaxValue).Min();
        Vector2 best = candidates.OrderByDescending(Clearance).First();
        magnet.RectTransform().anchoredPosition = best;
    }

    private Vector2 ClampToLooseSpawnArea(Vector2 position, Vector2 size)
    {
        Rect bounds = GetLooseCenterBounds(size);
        return new Vector2(Mathf.Clamp(position.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(position.y, bounds.yMin, bounds.yMax));
    }

    private Rect GetLooseCenterBounds(Vector2 size)
    {
        Rect area = bottomArea.rect;
        Vector2 half = size * 0.5f;
        // Use the full visible table width and height. Only points whose card
        // rectangle actually intersects the printer are excluded separately.
        var canvasCorners = new Vector3[4];
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(canvasCorners);
        float canvasLeft = canvasCorners.Select(corner => bottomArea.InverseTransformPoint(corner).x).Min();
        float canvasRight = canvasCorners.Select(corner => bottomArea.InverseTransformPoint(corner).x).Max();
        float canvasBottom = canvasCorners.Select(corner => bottomArea.InverseTransformPoint(corner).y).Min();
        float canvasTop = canvasCorners.Select(corner => bottomArea.InverseTransformPoint(corner).y).Max();
        float sideMargin = 4f;
        float minX = canvasLeft + half.x + sideMargin;
        float maxX = canvasRight - half.x - sideMargin;
        float minY = canvasBottom + half.y + (canvasTop - canvasBottom) * 0.025f;

        // The loose-card surface starts immediately below the holders, not at
        // BottomMagnetArea's old upper anchor. This exposes the large tabletop
        // area beside and above the printer; printer overlap is rejected by
        // OverlapsPrinter for each individual candidate.
        float holderBottom = float.MaxValue;
        foreach (CategoryHolder holder in holders)
        {
            if (holder == null) continue;
            var holderCorners = new Vector3[4];
            ((RectTransform)holder.transform).GetWorldCorners(holderCorners);
            holderBottom = Mathf.Min(holderBottom,
                holderCorners.Min(corner => bottomArea.InverseTransformPoint(corner).y));
        }
        float tableTop = holderBottom < float.MaxValue ? holderBottom : area.yMax;
        float maxY = tableTop - half.y - 12f;
        if (maxX < minX) minX = maxX = area.center.x;
        if (maxY < minY)
        {
            float middle = (minY + maxY) * 0.5f;
            minY = maxY = middle;
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private bool OverlapsPrinter(Vector2 center, Vector2 size)
    {
        if (printerRect == null || !printerRect.gameObject.activeInHierarchy) return false;
        Rect printerBounds = GetVisiblePrinterBounds(4f);
        Rect cardBounds = new Rect(center - size * 0.5f, size);
        return cardBounds.Overlaps(printerBounds);
    }

    private bool TryGetPrinterSidePosition(bool leftSide, Vector2 size, Rect tableBounds, out Vector2 position)
    {
        position = default;
        if (printerRect == null || !printerRect.gameObject.activeInHierarchy) return false;
        Rect printerBounds = GetVisiblePrinterBounds(4f);
        float x = leftSide ? tableBounds.xMin : tableBounds.xMax;
        float y = Mathf.Clamp(printerBounds.center.y, tableBounds.yMin, tableBounds.yMax);
        Vector2 candidate = new Vector2(x, y);
        if (OverlapsPrinter(candidate, size)) return false;
        position = candidate;
        return true;
    }

    private Rect GetVisiblePrinterBounds(float gap)
    {
        var corners = new Vector3[4];
        printerRect.GetWorldCorners(corners);
        var local = corners.Select(corner => (Vector2)bottomArea.InverseTransformPoint(corner)).ToArray();
        float left = local.Min(p => p.x);
        float right = local.Max(p => p.x);
        float bottom = local.Min(p => p.y);
        float top = local.Max(p => p.y);

        // Image.preserveAspect draws inside the RectTransform. Use that visible
        // sprite rectangle instead of treating its transparent letterbox as
        // part of the printer collision area.
        Image printerImage = printerRect.GetComponent<Image>();
        if (printerImage != null && printerImage.preserveAspect && printerImage.sprite != null)
        {
            float width = right - left;
            float height = top - bottom;
            float spriteAspect = printerImage.sprite.rect.width / printerImage.sprite.rect.height;
            float rectAspect = width / height;
            if (spriteAspect > rectAspect)
            {
                float visibleHeight = width / spriteAspect;
                float centerY = (bottom + top) * 0.5f;
                bottom = centerY - visibleHeight * 0.5f;
                top = centerY + visibleHeight * 0.5f;
            }
            else
            {
                float visibleWidth = height * spriteAspect;
                float centerX = (left + right) * 0.5f;
                left = centerX - visibleWidth * 0.5f;
                right = centerX + visibleWidth * 0.5f;
            }
        }
        return Rect.MinMaxRect(left - gap, bottom - gap, right + gap, top + gap);
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
        CategoryHolder target = hoveredHolder ?? FindOverlappingHolder(magnet);
        if (hoveredHolder != null) hoveredHolder.SetHighlighted(false, true);
        hoveredHolder = null;
        if (target == null)
        {
            magnet.ReturnInsideBottomArea();
            return;
        }
        if (!target.TryAccept(magnet))
        {
            // Every invalid holder drop is a spent move, including a full
            // holder, a completed category or a category already assigned to
            // another holder.
            target.ShowDropFeedback(false);
            SpendMove();
            magnet.Reject();
            return;
        }

        target.ShowDropFeedback(true);
        SpendMove();
        looseSlots.Remove(magnet);
    }

    private void SpendMove()
    {
        remainingMoves = Mathf.Max(0, remainingMoves - 1);
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

    private CategoryHolder FindOverlappingHolder(LooseMagnet magnet)
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
        CategoryHolder best = null;
        float bestArea = 0f;
        foreach (CategoryHolder holder in holders)
        {
            if (holder == null || !holder.gameObject.activeInHierarchy) continue;
            Rect holderBounds = LocalBounds((RectTransform)holder.transform);
            float overlapWidth = Mathf.Max(0f,
                Mathf.Min(magnetBounds.xMax, holderBounds.xMax) - Mathf.Max(magnetBounds.xMin, holderBounds.xMin));
            float overlapHeight = Mathf.Max(0f,
                Mathf.Min(magnetBounds.yMax, holderBounds.yMax) - Mathf.Max(magnetBounds.yMin, holderBounds.yMin));
            float overlapArea = overlapWidth * overlapHeight;
            if (overlapArea <= bestArea) continue;
            bestArea = overlapArea;
            best = holder;
        }
        float requiredArea = magnetBounds.width * magnetBounds.height * 0.10f;
        return bestArea >= requiredArea ? best : null;
    }

    public void OnHolderCompleted(CategoryHolder holder, string categoryId, LooseMagnet[] magnets)
    {
        if (!completedCategories.Add(categoryId)) return;
        Category source = currentLevel.categories.FirstOrDefault(category => category.id == categoryId);
        bool transforms = source != null && source.transformsOnComplete && source.transformResult != null;
        // Every terminal category follows the same readable finish: show its
        // name at the holder, fly that badge to the progress bar, then credit it.
        if (!transforms)
        {
            bool levelHasTransformations = currentLevel.categories.Any(category =>
                category.transformsOnComplete && category.transformResult != null);
            int visibleHolderCount = holders.Count(holderItem => holderItem != null && holderItem.gameObject.activeSelf);
            bool isOneOfFinalVisibleCategories =
                completedCategories.Count > totalCategories - Mathf.Max(1, visibleHolderCount);
            bool keepCompletedWords = !levelHasTransformations && isOneOfFinalVisibleCategories;
            pendingTransformations++;
            StartCoroutine(FlyFinalToProgress(holder, magnets, source, keepCompletedWords));
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
        holder.ClearForReuse();
        RefillStock(true);
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

    private IEnumerator FlyFinalToProgress(CategoryHolder holder, LooseMagnet[] magnets, Category category,
        bool keepCompletedWords)
    {
        Vector3 center = holder.transform.position;
        if (!keepCompletedWords)
        {
            foreach (var magnet in magnets)
            {
                magnet.transform.DOKill();
                magnet.transform.DOMove(center, 0.3f).SetEase(Ease.InOutCubic);
                magnet.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack);
            }
            yield return new WaitForSeconds(0.32f);
        }

        Image badge = CreateImage("CompletedCategoryFlight", transform);
        RectTransform rect = badge.rectTransform;
        completionEffects.Add(rect);
        Image sourceBadge = holder.transform.Find("CategoryBadge")?.GetComponent<Image>();
        // Match the Scene-authored ribbon instead of creating an oversized
        // fixed badge during the merge/completion animation.
        RectTransform sourceBadgeRect = sourceBadge != null ? sourceBadge.rectTransform : null;
        rect.sizeDelta = sourceBadgeRect != null && sourceBadgeRect.rect.size.sqrMagnitude > 0f
            ? sourceBadgeRect.rect.size
            : new Vector2(280f, 95f);
        rect.position = center;
        badge.sprite = sourceBadge != null ? sourceBadge.sprite : magnetSprite;
        badge.color = sourceBadge != null ? sourceBadge.color : Color.white;
        badge.type = Image.Type.Sliced;
        badge.raycastTarget = false;
        Canvas effectCanvas = badge.gameObject.AddComponent<Canvas>();
        effectCanvas.overrideSorting = true;
        effectCanvas.sortingOrder = 900;
        TextMeshProUGUI title = CreateText("CategoryName", rect);
        // Ribbon tails are decorative and not safe text space. Keep the name
        // inside the wide centre section, including long category names.
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(42f, 8f);
        titleRect.offsetMax = new Vector2(-42f, -8f);
        TextMeshProUGUI sourceTitle = holder.transform.Find("CategoryBadge/CategoryLabel")?.GetComponent<TextMeshProUGUI>() ??
                                      holder.transform.Find("CategoryLabel")?.GetComponent<TextMeshProUGUI>();
        title.font = sourceTitle != null ? sourceTitle.font : font;
        if (sourceTitle != null) title.fontSharedMaterial = sourceTitle.fontSharedMaterial;
        // Do not inherit the Scene label's former black tint: the flying title
        // needs strong contrast over the coloured completion badge.
        title.color = Color.white;
        title.fontStyle = sourceTitle != null ? sourceTitle.fontStyle : FontStyles.Bold;
        title.text = CategoryText.ToLowerEnglish(category != null ? category.name : holder.CategoryId);
        title.alignment = TextAlignmentOptions.Center;
        title.enableAutoSizing = true;
        title.fontSizeMin = 10f;
        title.fontSizeMax = 40f;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.raycastTarget = false;
        rect.localScale = Vector3.zero;
        yield return rect.DOScale(1f, 0.22f).SetEase(Ease.OutBack).WaitForCompletion();
        yield return new WaitForSeconds(0.35f);
        Vector3 start = rect.position;
        Vector3 target = categoryProgressTarget != null ? categoryProgressTarget.position : start;
        float arcHeight = Mathf.Max(70f, Vector3.Distance(start, target) * 0.12f);
        Vector3 control = (start + target) * 0.5f + Vector3.up * arcHeight;
        Sequence flight = DOTween.Sequence().SetTarget(rect);
        flight.Join(DOVirtual.Float(0f, 1f, 0.68f, t =>
        {
            // Quadratic Bezier path makes the destination unmistakable while
            // keeping the label readable through most of the flight.
            float inverse = 1f - t;
            rect.position = inverse * inverse * start + 2f * inverse * t * control + t * t * target;
        }).SetEase(Ease.InOutCubic));
        flight.Insert(0.34f, rect.DOScale(0.30f, 0.34f).SetEase(Ease.InCubic));
        yield return flight.WaitForCompletion();
        completionEffects.Remove(rect);
        rect.DOKill();
        Destroy(badge.gameObject);
        creditedCategories++;
        RefreshCategoryProgress(true);
        RectTransform progressImpact = categoryKnob != null ? categoryKnob : categoryProgressTarget;
        if (progressImpact != null)
        {
            progressImpact.DOKill();
            progressImpact.localScale = Vector3.one;
            progressImpact.DOPunchScale(Vector3.one * 0.12f, 0.3f, 4, 0.5f);
        }
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        if (!keepCompletedWords)
            holder.ClearForReuse();
        pendingTransformations--;
        if (!keepCompletedWords)
            RefillStock(true);
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
        GameAudio.Play(GameAudioCue.Transformation);
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        pendingTransformations--;
        RefillStock(true);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StopTestTimer();
#endif
        yield return new WaitForSeconds(0.55f);
        int next = Mathf.Min(activeLevelIndex + 1, database.levels.Count - 1);
        SaveSystem.CompleteLevel(activeLevelIndex, next);
        if (winScreen != null)
        {
            // WinScreen is authored below MagnetPopupUI, whose root starts
            // inactive. If Settings/TimeUp was never opened during the level,
            // activating only the child leaves the win popup invisible while
            // the board is hidden.
            MagnetPopupUI popup = winScreen.GetComponentInParent<MagnetPopupUI>(true);
            if (popup != null && !popup.gameObject.activeSelf)
                popup.gameObject.SetActive(true);

            Transform resultContent = winScreen.transform.Find("Content") ?? winScreen.transform.Find("Panel");
            TextMeshProUGUI title = resultContent?.Find("Title")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI level = resultContent?.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI reward = resultContent?.Find("Reward/Value")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI balance = FindWinGoldBalance();
            if (title != null) title.text = "Well sorted!";
            if (level != null) level.text = $"Level {currentLevel.levelNumber} complete";
            if (reward != null) reward.text = $"+{SaveSystem.LevelCompletionGold} coins";
            if (balance != null) balance.text = SaveSystem.Current.gold.ToString("N0");
            Canvas resultCanvas = winScreen.GetComponent<Canvas>();
            if (resultCanvas == null) resultCanvas = winScreen.AddComponent<Canvas>();
            resultCanvas.overrideSorting = true;
            resultCanvas.sortingOrder = 1000;
            if (winScreen.GetComponent<GraphicRaycaster>() == null)
                winScreen.AddComponent<GraphicRaycaster>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshTestTimerLabel();
#endif
            winScreen.SetActive(true);
            winScreen.transform.SetAsLastSibling();
            GameAudio.Play(GameAudioCue.Win);
            AnimateWinScreenIn();
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

    private TextMeshProUGUI FindWinGoldBalance()
    {
        if (winScreen == null) return null;

        TextMeshProUGUI balance = winScreen.transform.Find("Gold/Value")?.GetComponent<TextMeshProUGUI>() ??
                                  winScreen.transform.Find("Gold (1)/Value")?.GetComponent<TextMeshProUGUI>() ??
                                  winScreen.transform.Find("Content/Gold/Value")?.GetComponent<TextMeshProUGUI>() ??
                                  winScreen.transform.Find("Panel/Gold/Value")?.GetComponent<TextMeshProUGUI>();
        if (balance != null) return balance;

        foreach (TextMeshProUGUI label in winScreen.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Transform parent = label.transform.parent;
            if (label.name == "Value" && parent != null && parent.name.StartsWith("Gold"))
                return label;
        }

        return null;
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    public void BindOutOfMovesButtons(MagnetPopupUI popup)
    {
        if (popup == null) return;
        outOfMovesScreen = popup.TimeUpScreen;
        Wire(popup.WatchAdButton, ContinueWithAd);
        Wire(popup.SpendGoldButton, ContinueWithGold);
        Wire(popup.TryAgainButton, RestartCurrentLevel);
    }

    private void ContinueWithAd()
    {
        ResumeAfterOutOfMoves(5);
    }

    private void ContinueWithGold()
    {
        if (!SaveSystem.TrySpendGold(900))
        {
            if (MagnetPopupUI.Instance != null)
                MagnetPopupUI.Instance.ShowNotEnoughGold();
            GameAudio.Play(GameAudioCue.Error);
            HapticFeedback.Play(HapticFeedback.Strength.Light);
            return;
        }
        ResumeAfterOutOfMoves(5);
    }

    private void ResumeAfterOutOfMoves(int extraMoves)
    {
        remainingMoves = Mathf.Max(1, extraMoves);
        gameEnded = false;
        if (MagnetPopupUI.Instance != null) MagnetPopupUI.Instance.HideTimeUp();
        else if (outOfMovesScreen != null) outOfMovesScreen.SetActive(false);
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

    private void AnimateWinScreenIn()
    {
        if (winScreen == null) return;
        CanvasGroup group = winScreen.GetComponent<CanvasGroup>();
        if (group == null) group = winScreen.AddComponent<CanvasGroup>();
        // The win screen has a full-screen dimmer on its root and two separate
        // popup visuals below it: the rounded Background card and the Content
        // (header, rewards and continue button). Animate both so the complete
        // popup enters together instead of only the inner content scaling.
        Transform background = winScreen.transform.Find("Background");
        Transform content = winScreen.transform.Find("Content") ?? winScreen.transform.Find("Panel");
        RectTransform backgroundRect = background as RectTransform;
        RectTransform contentRect = content as RectTransform;
        group.DOKill();
        backgroundRect?.DOKill();
        contentRect?.DOKill();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        if (backgroundRect != null) backgroundRect.localScale = Vector3.one * 0.86f;
        if (contentRect != null && contentRect.parent != backgroundRect)
            contentRect.localScale = Vector3.one * 0.86f;
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(group.DOFade(1f, 0.16f).SetEase(Ease.OutCubic));
        if (backgroundRect != null)
            sequence.Join(backgroundRect.DOScale(1f, 0.34f).SetEase(Ease.OutBack));
        if (contentRect != null && contentRect.parent != backgroundRect)
            sequence.Join(contentRect.DOScale(1f, 0.34f).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            group.blocksRaycasts = true;
            group.interactable = true;
        });
    }

    private void DoubleRewardAndContinue()
    {
        if (!rewardDoubled)
        {
            rewardDoubled = true;
            SaveSystem.AddGold(40);
            Transform content = winScreen.transform.Find("Content") ?? winScreen.transform.Find("Panel");
            TextMeshProUGUI reward = content?.Find("Reward/Value")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI balance = FindWinGoldBalance();
            if (reward != null) reward.text = "+80 GOLD";
            if (balance != null) balance.text = SaveSystem.Current.gold.ToString("N0");
        }
        ContinueAfterWin();
    }

    private LooseMagnet CreateMagnet(string text, string categoryId, string categoryName, Sprite sprite)
    {
        GameObject root = new GameObject($"Magnet - {text}", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup), typeof(LooseMagnet));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(bottomArea, false);
        // The Scene-authored MagnetStyleTemplate is the single source of truth
        // for card width and height. Changing its RectTransform in Game.unity
        // now changes every runtime magnet without another code edit.
        rect.sizeDelta = magnetSceneSize;
        Image background = root.GetComponent<Image>();
        background.sprite = magnetSprite;
        background.type = magnetImageType;

        GameObject viewportObject = new GameObject("IconViewport", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(rect, false);
        // The polaroid sprite has transparent pixels around its visible paper.
        // Keep icons inside the actual pink paper and clip unusually tall or
        // wide source art so it can never protrude beyond that safe region.
        viewport.anchorMin = new Vector2(0.10f, 0.12f);
        viewport.anchorMax = new Vector2(0.90f, 0.88f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        Image icon = CreateImage("Icon", viewport);
        Stretch(icon.rectTransform, 0f);
        icon.rectTransform.localScale = Vector3.one;
        icon.preserveAspect = true;
        TextMeshProUGUI label = CreateText("Label", rect);
        label.rectTransform.anchorMin = new Vector2(0.08f, 0.12f);
        label.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
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
        // Long words must stay on one line inside the visible pink paper.
        // Preserve the Scene-authored size as the maximum and shrink only
        // when the preferred width would otherwise overflow the card.
        label.enableAutoSizing = true;
        label.fontSizeMax = Mathf.Max(magnetTextStyle.fontSize, magnetTextStyle.fontSizeMax);
        label.fontSizeMin = Mathf.Min(label.fontSizeMax, 10f);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
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
    private void EnsureTestTimerUi()
    {
        Transform hud = transform.parent == null ? null : transform.parent.Find("FridgeTopHud");
        if (hud != null && testTimerLabel == null)
        {
            Transform existing = hud.Find("TestLevelTimer/Value");
            testTimerLabel = existing == null ? null : existing.GetComponent<TextMeshProUGUI>();
            if (testTimerLabel == null)
            {
                Image timerBackground = CreateImage("TestLevelTimer", hud);
                timerBackground.color = new Color(0.08f, 0.10f, 0.22f, 0.90f);
                RectTransform backgroundRect = timerBackground.rectTransform;
                backgroundRect.anchorMin = new Vector2(0.02f, 0.765f);
                backgroundRect.anchorMax = new Vector2(0.22f, 0.805f);
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;

                testTimerLabel = CreateText("Value", timerBackground.transform);
                testTimerLabel.font = font;
                testTimerLabel.fontSize = 22f;
                testTimerLabel.color = Color.white;
                testTimerLabel.alignment = TextAlignmentOptions.Center;
                testTimerLabel.raycastTarget = false;
                Stretch(testTimerLabel.rectTransform, 4f);
            }
        }

        if (winScreen == null || testResultTimeLabel != null) return;
        Transform panel = winScreen.transform.Find("Panel") ?? winScreen.transform.Find("Content");
        if (panel == null) panel = winScreen.transform;
        testResultTimeLabel = panel.Find("TestLevelTime")?.GetComponent<TextMeshProUGUI>();
        if (testResultTimeLabel == null)
        {
            testResultTimeLabel = CreateText("TestLevelTime", panel);
            testResultTimeLabel.font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Bold SDF") ?? font;
            testResultTimeLabel.fontSize = 35f;
            testResultTimeLabel.fontStyle = FontStyles.Bold;
            testResultTimeLabel.color = new Color(0.12f, 0.10f, 0.30f, 1f);
            testResultTimeLabel.alignment = TextAlignmentOptions.Center;
            testResultTimeLabel.enableAutoSizing = false;
            testResultTimeLabel.extraPadding = true;
            testResultTimeLabel.outlineWidth = 0.18f;
            testResultTimeLabel.outlineColor = new Color(1f, 1f, 1f, 0.95f);
            testResultTimeLabel.raycastTarget = false;
            RectTransform resultRect = testResultTimeLabel.rectTransform;
            resultRect.anchorMin = new Vector2(0.18f, 0.32f);
            resultRect.anchorMax = new Vector2(0.82f, 0.39f);
            resultRect.offsetMin = Vector2.zero;
            resultRect.offsetMax = Vector2.zero;
        }
    }

    private void StopTestTimer()
    {
        if (!testTimerRunning) return;
        testTimerElapsed = Mathf.Max(0f, Time.realtimeSinceStartup - testTimerStartedAt);
        testTimerRunning = false;
        RefreshTestTimerLabel();
    }

    private void RefreshTestTimerLabel()
    {
        int wholeSeconds = Mathf.FloorToInt(Mathf.Max(0f, testTimerElapsed));
        int minutes = wholeSeconds / 60;
        int remainingSeconds = wholeSeconds % 60;
        int hundredths = Mathf.Clamp(Mathf.FloorToInt((testTimerElapsed - wholeSeconds) * 100f), 0, 99);
        const string format = "TIME  {0:00}:{1:00}.{2:00}";
        if (testTimerLabel != null)
            testTimerLabel.SetText(format, (float)minutes, (float)remainingSeconds, (float)hundredths);
        // The result label is hidden during play. Updating it only when the
        // timer stops avoids rebuilding another TMP mesh on every tick.
        if (!testTimerRunning && testResultTimeLabel != null)
            testResultTimeLabel.SetText(format, (float)minutes, (float)remainingSeconds, (float)hundredths);
    }

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
