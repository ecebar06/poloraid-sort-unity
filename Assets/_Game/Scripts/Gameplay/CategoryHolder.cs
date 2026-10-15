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
    private bool completionPending;
    public string CategoryId { get; private set; }
    public bool IsEmpty => string.IsNullOrEmpty(CategoryId);
    public bool IsFull => collected.Count >= 4;

    public void Configure(CategoryMagnetBoardController controller)
    {
        owner = controller;
        StopAllCoroutines();
        DestroyAllMagnetChildren();
        collected.Clear();
        ApplyReadableCountStyle();
        ApplyReadableCategoryStyle();
        holderBackground = GetComponent<Image>();
        if (holderBackground != null && !backgroundColorCached)
        {
            normalBackgroundColor = holderBackground.color;
            backgroundColorCached = true;
        }
        // Slot 1-4 are authored in the Scene. Runtime must not rewrite their
        // anchors, positions or sizes; placed cards follow those RectTransforms.
        EnsureCompletionTint();
        ResetHolder();
    }

    public bool CanAccept(LooseMagnet magnet)
    {
        if (magnet == null || IsFull || completionPending) return false;
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
        // Position and size belong to the Scene-authored ribbon. Runtime only
        // supplies the category text and its readable colour.
        categoryLabel.color = Color.white;
    }

    public bool TryAccept(LooseMagnet magnet)
    {
        if (!CanAccept(magnet)) return false;
        if (IsEmpty)
        {
            CategoryId = magnet.CategoryId;
            // The Polaroid holder art does not require a category label.
            // Some Scene holders intentionally leave this reference empty;
            // accepting a card must still continue and snap it into its slot.
            if (categoryLabel != null)
            {
                categoryLabel.text = CategoryText.ToLowerEnglish(magnet.CategoryName);
                categoryLabel.color = Color.white;
                categoryLabel.gameObject.SetActive(true);
            }
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
        if (IsFull && !completionPending)
        {
            // Lock this holder immediately. The celebration coroutine waits
            // before recycling it, and accepting another magnet during that
            // window can leave a visual orphan when the holder is cleared.
            completionPending = true;
            StartCoroutine(CompleteAfterSnap());
        }
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
        DestroyAllMagnetChildren();
        collected.Clear();
        ResetHolder();
    }

    private void DestroyAllMagnetChildren()
    {
        if (content == null) return;
        foreach (LooseMagnet magnet in content.GetComponentsInChildren<LooseMagnet>(true))
            if (magnet != null) Destroy(magnet.gameObject);
    }

    private void ResetHolder()
    {
        CategoryId = string.Empty;
        completionPending = false;
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

    private RectTransform GetSlot(int index)
    {
        if (content == null) return null;
        Transform slot = content.Find($"Slot {index + 1}");
        return slot as RectTransform;
    }

    private void EnsurePolaroidSlotLayout()
    {
        if (content == null || holderBackground == null || holderBackground.sprite == null ||
            !holderBackground.sprite.name.Contains("4x1_polaroid")) return;

        // Exact centres of the four cream openings in the supplied holder art.
        // Keep these as real RectTransforms so designers can still inspect and
        // fine-tune them in Scene view after the V6 installer persists them.
        float[] centers = { 0.875f, 0.625f, 0.375f, 0.125f };
        for (int index = 0; index < centers.Length; index++)
        {
            RectTransform slot = GetSlot(index);
            if (slot == null) continue;
            float center = centers[index];
            slot.anchorMin = new Vector2(0.04f, center - 0.115f);
            slot.anchorMax = new Vector2(0.96f, center + 0.115f);
            slot.offsetMin = Vector2.zero;
            slot.offsetMax = Vector2.zero;
            slot.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void LayoutCollectedMagnets()
    {
        int index = collected.Count - 1;
        LooseMagnet newest = collected[index];
        if (newest == null) return;
        newest.SetHolderStackMode(false);

        RectTransform slot = GetSlot(index);
        if (slot != null)
            newest.LockIntoHolder(slot, index);
        else
        {
            // Backward-compatible fallback for scenes that have not yet been
            // upgraded with editable Slot 1-4 RectTransforms.
            Rect rect = content.rect;
            float[] slotCenters = { 0.875f, 0.625f, 0.375f, 0.125f };
            float normalizedY = slotCenters[Mathf.Clamp(index, 0, slotCenters.Length - 1)];
            Vector2 position = new Vector2(rect.center.x, Mathf.Lerp(rect.yMin, rect.yMax, normalizedY));
            newest.LockIntoHolder(position, content, index);
        }
    }
}
