using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class LooseMagnet : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;

    private CategoryMagnetBoardController owner;
    private Canvas canvas;
    private Canvas magnetCanvas;
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector2 pointerOffset;
    private bool locked;
    private bool dragging;
    private Tween scaleTween;

    private void AnimateScale(float scale, float duration, Ease ease)
    {
        scaleTween?.Kill();
        scaleTween = rect.DOScale(scale, duration).SetEase(ease);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (locked || owner.IsInputLocked) return;
        // A small scale lift also responds to a press before dragging starts.
        AnimateScale(1.08f, 0.16f, Ease.OutCubic);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (locked || dragging) return;
        AnimateScale(1f, 0.24f, Ease.OutBack);
    }

    public string CategoryId { get; private set; }
    public string CategoryName { get; private set; }

    public void ConfigureVisuals(Image backgroundImage, Image iconImage, TextMeshProUGUI textLabel)
    {
        background = backgroundImage;
        icon = iconImage;
        label = textLabel;
    }

    public void Initialize(CategoryMagnetBoardController controller, string text, string categoryId,
        string categoryName, Sprite sprite)
    {
        owner = controller;
        canvas = controller.Canvas;
        rect = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        magnetCanvas = GetComponent<Canvas>();
        if (magnetCanvas == null) magnetCanvas = gameObject.AddComponent<Canvas>();
        magnetCanvas.overrideSorting = true;
        magnetCanvas.sortingOrder = 10;
        // A nested override-sorting Canvas needs its own raycaster; otherwise
        // the magnet renders correctly but no longer receives pointer events.
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        CategoryId = categoryId;
        CategoryName = categoryName;
        label.text = text;
        bool useIcon = sprite != null;
        if (background != null)
        {
            // Keep the transparent graphic as a generous touch target.
            Color backgroundColor = background.color;
            backgroundColor.a = useIcon ? 0f : 1f;
            background.color = backgroundColor;
            background.raycastTarget = true;
        }
        icon.sprite = sprite;
        icon.gameObject.SetActive(useIcon);
        label.gameObject.SetActive(!useIcon);
        float angle = Random.Range(-9f, 9f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void BringToFront()
    {
        transform.SetAsLastSibling();
        // Each magnet has its own Canvas, so sibling order alone is not enough.
        // Retain this stack order after dropping as well.
        int stackOrder = 110;
        foreach (Transform child in owner.BottomArea)
        {
            Canvas childCanvas = child.GetComponent<Canvas>();
            if (childCanvas != null)
                childCanvas.sortingOrder = Mathf.Min(stackOrder++, 800);
        }
        magnetCanvas.sortingOrder = 900;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (locked || owner.IsInputLocked) return;
        dragging = true;
        BringToFront();
        canvasGroup.blocksRaycasts = false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.BottomArea, eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 point);
        pointerOffset = rect.anchoredPosition - point;
        rect.DOKill();
        AnimateScale(1.08f, 0.16f, Ease.OutCubic);
        HapticFeedback.Play(HapticFeedback.Strength.Light);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (locked || owner.IsInputLocked) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.BottomArea, eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 point))
            rect.anchoredPosition = point + pointerOffset;
        owner.UpdateHoveredHolder(eventData.position, this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;
        canvasGroup.blocksRaycasts = true;
        // Keep the last manipulated magnet on top until another is picked up.
        magnetCanvas.sortingOrder = 800;
        if (locked) return;
        if (owner.IsInputLocked)
        {
            ReturnInsideBottomArea();
            return;
        }
        owner.EndMagnetDrag(eventData.position, this);
    }

    public void LockIntoHolder(Vector2 localPosition, RectTransform holderContent)
    {
        locked = true;
        canvasGroup.blocksRaycasts = false;
        rect.SetParent(holderContent, true);
        rect.SetAsLastSibling();
        // Holders are siblings, so a later holder could otherwise render over
        // a magnet belonging to an earlier holder. Give collected magnets an
        // explicit nested-canvas order above every holder background.
        magnetCanvas.overrideSorting = true;
        magnetCanvas.sortingOrder = 100 + rect.GetSiblingIndex();
        rect.DOKill();
        rect.localRotation = Quaternion.identity;
        rect.DOAnchorPos(localPosition, 0.24f).SetEase(Ease.OutCubic);
        AnimateScale(0.72f, 0.24f, Ease.OutBack);
    }

    public void Reject()
    {
        rect.DOKill();
        rect.DOShakeRotation(0.22f, new Vector3(0f, 0f, 12f), 10, 60f);
        HapticFeedback.Play(HapticFeedback.Strength.Medium);
    }

    public void ReturnInsideBottomArea()
    {
        AnimateScale(1f, 0.24f, Ease.OutBack);
        rect.DOAnchorPos(owner.ClampToBottomArea(rect.anchoredPosition, rect.rect.size), 0.24f)
            .SetEase(Ease.OutCubic);
    }

    private void OnDestroy()
    {
        if (rect != null) rect.DOKill();
    }
}
