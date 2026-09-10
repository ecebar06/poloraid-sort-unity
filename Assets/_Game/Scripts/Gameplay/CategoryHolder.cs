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
        holderBackground = GetComponent<Image>();
        if (holderBackground != null && !backgroundColorCached)
        {
            normalBackgroundColor = holderBackground.color;
            backgroundColorCached = true;
        }
        ResetHolder();
    }

    public bool CanAccept(LooseMagnet magnet)
    {
        if (magnet == null || IsFull) return false;
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

    public bool TryAccept(LooseMagnet magnet)
    {
        if (!CanAccept(magnet)) return false;
        if (IsEmpty)
        {
            CategoryId = magnet.CategoryId;
            categoryLabel.text = magnet.CategoryName.ToUpperInvariant();
            categoryLabel.gameObject.SetActive(true);
            Transform badge = transform.Find("CategoryBadge");
            if (badge != null) badge.gameObject.SetActive(true);
        }

        collected.Add(magnet);
        magnet.LockIntoHolder(GetSlotPosition(collected.Count - 1), content);
        UpdateCount();
        HapticFeedback.Play(HapticFeedback.Strength.Light);
        if (IsFull) StartCoroutine(CompleteAfterSnap());
        return true;
    }

    public void SetHighlighted(bool value, bool valid)
    {
        if (highlight == null) return;
        highlight.gameObject.SetActive(value);
        highlight.color = valid
            ? new Color(0.25f, 1f, 0.42f, 0.55f)
            : new Color(1f, 0.22f, 0.20f, 0.45f);
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
        }
        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.5f);
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

    private Vector2 GetSlotPosition(int index)
    {
        Rect rect = content.rect;
        // One overlapping vertical stack, with each new magnet in front.
        float step = Mathf.Min(rect.height * 0.16f, 75f);
        return new Vector2(rect.center.x, rect.yMax - rect.height * 0.24f - index * step);
    }
}
