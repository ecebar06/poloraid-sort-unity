using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CategoryHolder : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private TextMeshProUGUI categoryLabel;
    [SerializeField] private TextMeshProUGUI countLabel;
    [SerializeField] private Image highlight;

    private readonly List<LooseMagnet> collected = new List<LooseMagnet>(4);
    private CategoryMagnetBoardController owner;
    private Image holderBackground;
    private Image completionTint;
    private Color normalBackgroundColor = Color.white;
    private Color assignedBackgroundColor = Color.white;
    private bool backgroundColorCached;
    private bool hasAssignedBackgroundColor;
    public string CategoryId { get; private set; }
    public bool IsEmpty => string.IsNullOrEmpty(CategoryId);
    public bool IsFull => collected.Count >= 4;

    public void Configure(CategoryMagnetBoardController controller)
    {
        owner = controller;
        StopAllCoroutines();
        foreach (LooseMagnet magnet in collected)
            if (magnet != null) Destroy(magnet.gameObject);
        ApplyReadableCountStyle();
        ApplyReadableCategoryStyle();
        holderBackground = GetComponent<Image>();
        if (holderBackground != null && !backgroundColorCached)
        {
            normalBackgroundColor = holderBackground.color;
            backgroundColorCached = true;
        }
        EnsureCompletionTint();
        ResetHolder();
    }

    public bool CanAccept(LooseMagnet magnet)
    {
        if (magnet == null || IsFull) return false;
        if (owner != null && !owner.CanPlayCategory(magnet.CategoryId)) return false;
        if (!IsEmpty) return CategoryId == magnet.CategoryId;

        // Once a category has claimed a holder, its remaining magnets must all
        // go to that same holder instead of splitting across empty holders.
        return owner == null ||
               !owner.IsCategoryAssignedToAnotherHolder(magnet.CategoryId, this);
    }

    public void ApplyReadableCountStyle()
    {
        if (countLabel == null) return;
        var readableFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-Variable SDF");
        if (readableFont != null) countLabel.font = readableFont;
        countLabel.color = Color.white;
        countLabel.fontStyle = FontStyles.Bold;
        countLabel.enableAutoSizing = true;
        countLabel.fontSize = 36f;
        countLabel.fontSizeMin = 30f;
        countLabel.fontSizeMax = 36f;
        countLabel.extraPadding = true;
        // Use a local material so HUD and magnet font assets are unaffected.
        Material material = countLabel.fontMaterial;
        material.SetColor("_FaceColor", Color.white);
        material.SetFloat("_FaceDilate", 0f);
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0f);
        material.SetFloat("_OutlineSoftness", 0f);
        material.DisableKeyword("OUTLINE_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        material.DisableKeyword("UNDERLAY_ON");
        countLabel.UpdateMeshPadding();
        countLabel.SetAllDirty();
    }

    private void ApplyReadableCategoryStyle()
    {
        if (categoryLabel == null) return;
        // Typography is intentionally left untouched here. The CategoryLabel
        // values from the authored Scene Inspector must remain editable by the
        // designer instead of being overwritten whenever a level loads.

        RectTransform categoryRect = categoryLabel.rectTransform;
        categoryRect.anchorMin = new Vector2(0.06f, 1.04f);
        categoryRect.anchorMax = new Vector2(0.94f, 1.34f);
        categoryRect.offsetMin = categoryRect.offsetMax = Vector2.zero;
        Transform categoryBadge = transform.Find("CategoryBadge");
        RectTransform categoryBadgeRect = categoryBadge as RectTransform;
        if (categoryBadgeRect != null)
        {
            categoryBadgeRect.anchorMin = new Vector2(0.02f, 1.04f);
            categoryBadgeRect.anchorMax = new Vector2(0.98f, 1.34f);
            categoryBadgeRect.offsetMin = categoryBadgeRect.offsetMax = Vector2.zero;
        }

        RectTransform countRect = countLabel.rectTransform;
        countRect.anchorMin = new Vector2(0.23f, 0.95f);
        countRect.anchorMax = new Vector2(0.77f, 1.065f);
        countRect.offsetMin = countRect.offsetMax = Vector2.zero;
        Transform countBadge = transform.Find("CountBadge");
        RectTransform countBadgeRect = countBadge as RectTransform;
        if (countBadgeRect != null)
        {
            countBadgeRect.anchorMin = new Vector2(0.22f, 0.95f);
            countBadgeRect.anchorMax = new Vector2(0.78f, 1.065f);
            countBadgeRect.offsetMin = countBadgeRect.offsetMax = Vector2.zero;
        }
    }

    public bool TryAccept(LooseMagnet magnet)
    {
        if (!CanAccept(magnet)) return false;
        if (IsEmpty)
        {
            CategoryId = magnet.CategoryId;
            categoryLabel.text = CategoryText.ToLowerEnglish(magnet.CategoryName);
            categoryLabel.gameObject.SetActive(true);
            Transform badge = transform.Find("CategoryBadge");
            if (badge != null) badge.gameObject.SetActive(true);
            ApplyAssignedCategoryColor();
        }

        // Every card in the claimed holder should carry the same category
        // tint, not only the first card that assigned the holder.
        magnet.SetAssignedCategoryAppearance(GetCategoryBadgeColor());
        collected.Add(magnet);
        LayoutCollectedMagnets();
        UpdateCount();
        GameAudio.Play(GameAudioCue.MagnetPlace);
        HapticFeedback.Play(HapticFeedback.Strength.Light);
        if (IsFull) StartCoroutine(CompleteAfterSnap());
        return true;
    }

    public void SetHighlighted(bool value, bool valid)
    {
        // Highlight the holder image itself. The former overlay could extend
        // beyond the art's rounded outline and looked like a detached glow.
        if (highlight != null) highlight.gameObject.SetActive(false);
        if (holderBackground == null) return;
        holderBackground.DOKill();
        // Use a strong, unambiguous target color while dragging. Keeping a
        // small amount of the original gray preserves the holder artwork.
        Color baseColor = hasAssignedBackgroundColor ? assignedBackgroundColor : normalBackgroundColor;
        Color target = !value ? baseColor : Color.Lerp(baseColor,
            valid ? new Color(0.10f, 1f, 0.24f, 1f) : new Color(1f, 0.08f, 0.12f, 1f), 0.72f);
        holderBackground.DOColor(target, 0.10f).SetEase(Ease.OutCubic);
    }

    private void ApplyAssignedCategoryColor()
    {
        if (holderBackground == null) return;

        Color badgeColor = GetCategoryBadgeColor();
        // Keep the holder artwork readable while making the category color
        // clearly visible as soon as its first card claims the holder.
        // Use a stronger pastel tint so the holder does not wash back into
        // gray while still retaining the original artwork and contrast.
        assignedBackgroundColor = Color.Lerp(normalBackgroundColor, badgeColor, 0.78f);
        assignedBackgroundColor.a = normalBackgroundColor.a;
        hasAssignedBackgroundColor = true;
        holderBackground.DOKill();
        holderBackground.DOColor(assignedBackgroundColor, 0.18f).SetEase(Ease.OutCubic);
    }

    private Color GetCategoryBadgeColor()
    {
        Image categoryBadge = transform.Find("CategoryBadge")?.GetComponent<Image>();
        return categoryBadge != null ? categoryBadge.color : normalBackgroundColor;
    }

    private IEnumerator CompleteAfterSnap()
    {
        // Let the fourth magnet finish snapping before showing completion.
        yield return new WaitForSeconds(0.22f);
        if (categoryLabel != null)
        {
            categoryLabel.transform.DOKill();
            categoryLabel.transform.DOPunchScale(Vector3.one * 0.2f, 0.45f, 4, 0.5f);
        }
        if (holderBackground != null && !owner.CategoryTransforms(CategoryId))
        {
            holderBackground.DOKill();
            Image categoryBadge = transform.Find("CategoryBadge")?.GetComponent<Image>();
            Color completedColor = categoryBadge != null ? categoryBadge.color : normalBackgroundColor;
            completedColor.a = 1f;
            holderBackground.DOColor(completedColor, 0.22f);
            ShowCompletionTint(completedColor);
            foreach (LooseMagnet magnet in collected)
                if (magnet != null) magnet.SetCompletedAppearance(completedColor);
        }
        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.5f);
        GameAudio.Play(GameAudioCue.CategoryComplete);
        HapticFeedback.Play(HapticFeedback.Strength.Success);
        // Hold the completed state long enough to read it before recycling.
        yield return new WaitForSeconds(1.1f);
        owner.OnHolderCompleted(this, CategoryId, collected.ToArray());
    }

    public void ClearForReuse()
    {
        foreach (LooseMagnet magnet in collected)
            if (magnet != null) Destroy(magnet.gameObject);
        collected.Clear();
        ResetHolder();
    }

    private void ResetHolder()
    {
        CategoryId = string.Empty;
        collected.Clear();
        hasAssignedBackgroundColor = false;
        assignedBackgroundColor = normalBackgroundColor;
        if (holderBackground != null)
        {
            holderBackground.DOKill();
            holderBackground.color = normalBackgroundColor;
        }
        if (completionTint != null)
        {
            completionTint.DOKill();
            completionTint.gameObject.SetActive(false);
        }
        if (categoryLabel != null) categoryLabel.gameObject.SetActive(false);
        Transform badge = transform.Find("CategoryBadge");
        if (badge != null) badge.gameObject.SetActive(false);
        SetHighlighted(false, true);
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (countLabel != null) countLabel.text = $"{collected.Count}/4";
    }

    private void EnsureCompletionTint()
    {
        if (completionTint != null || holderBackground == null) return;
        Transform existing = transform.Find("CompletedHolderTint");
        if (existing != null) completionTint = existing.GetComponent<Image>();
        else
        {
            GameObject tint = new GameObject("CompletedHolderTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tint.transform.SetParent(transform, false);
            completionTint = tint.GetComponent<Image>();
            RectTransform rect = completionTint.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            completionTint.sprite = holderBackground.sprite;
            completionTint.type = holderBackground.type;
            completionTint.raycastTarget = false;
            // Render after the gray holder image but before Content/magnets.
            completionTint.transform.SetAsFirstSibling();
        }
        completionTint.gameObject.SetActive(false);
    }

    private void ShowCompletionTint(Color color)
    {
        EnsureCompletionTint();
        if (completionTint == null) return;
        color.a = 0f;
        completionTint.color = color;
        completionTint.gameObject.SetActive(true);
        completionTint.DOFade(1f, 0.22f).SetEase(Ease.OutCubic);
    }

    private Vector2 GetSlotPosition(int index)
    {
        Rect rect = content.rect;
        // Preserve the original accordion behavior while giving each card a
        // little more vertical breathing room in the holder.
        float step = Mathf.Clamp(rect.height * 0.18f, 48f, 74f);
        return new Vector2(rect.center.x, rect.yMax - rect.height * 0.24f - index * step);
    }

    private void LayoutCollectedMagnets()
    {
        LooseMagnet newest = collected[collected.Count - 1];
        // Only cards covered by a newer card move their labels upward. The
        // newest/front card always keeps its word centred.
        for (int index = 0; index < collected.Count - 1; index++)
        {
            LooseMagnet existing = collected[index];
            if (existing != null) existing.SetHolderStackMode(true);
        }
        if (newest != null) newest.SetHolderStackMode(false);
        if (newest != null)
            newest.LockIntoHolder(GetSlotPosition(collected.Count - 1), content, collected.Count - 1);
    }
}
