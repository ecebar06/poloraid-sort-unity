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
    private Color normalBackgroundColor = Color.white;
    private bool backgroundColorCached;
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
        ResolveLocalCategoryLabel();
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

    private void ResolveLocalCategoryLabel()
    {
        // Always use this holder's own label. This also repairs missing or
        // accidentally cross-wired Inspector references on holders 2-4 while
        // leaving every Scene-authored ribbon transform untouched.
        Transform localLabel = transform.Find("CategoryBadge/CategoryLabel");
        if (localLabel != null)
            categoryLabel = localLabel.GetComponent<TextMeshProUGUI>();
    }

    public bool TryAccept(LooseMagnet magnet)
    {
        if (!CanAccept(magnet)) return false;
        if (IsEmpty)
        {
            ResolveLocalCategoryLabel();
            CategoryId = magnet.CategoryId;
            // The Polaroid holder art does not require a category label.
            // Some Scene holders intentionally leave this reference empty;
            // accepting a card must still continue and snap it into its slot.
            if (categoryLabel != null)
            {
                string displayName = string.IsNullOrWhiteSpace(magnet.CategoryName)
                    ? magnet.CategoryId
                    : magnet.CategoryName;
                categoryLabel.text = CategoryText.ToLowerEnglish(displayName);
                categoryLabel.color = Color.white;
                categoryLabel.gameObject.SetActive(true);
            }
            Transform badge = transform.Find("CategoryBadge");
            if (badge != null) badge.gameObject.SetActive(true);
        }

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
        // Dragging over a holder must stay visually neutral. Feedback is shown
        // only after the player releases the card.
        if (highlight != null) highlight.gameObject.SetActive(false);
        if (holderBackground == null) return;
        holderBackground.DOKill();
        holderBackground.color = normalBackgroundColor;
    }

    public void ShowDropFeedback(bool valid)
    {
        if (highlight != null) highlight.gameObject.SetActive(false);
        if (holderBackground == null) return;

        holderBackground.DOKill();
        Color feedbackColor = Color.Lerp(normalBackgroundColor,
            valid ? new Color(0.10f, 1f, 0.24f, 1f) : new Color(1f, 0.08f, 0.12f, 1f), 0.72f);
        DOTween.Sequence()
            .SetTarget(holderBackground)
            .Append(holderBackground.DOColor(feedbackColor, 0.10f).SetEase(Ease.OutCubic))
            .AppendInterval(0.16f)
            .Append(holderBackground.DOColor(normalBackgroundColor, 0.18f).SetEase(Ease.OutCubic));
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
        if (holderBackground != null)
        {
            holderBackground.DOKill();
            holderBackground.color = normalBackgroundColor;
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
