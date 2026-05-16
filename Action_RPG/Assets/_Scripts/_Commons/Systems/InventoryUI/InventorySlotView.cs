using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI component cho mỗi ô inventory trong lưới 25 ô.
/// Thay thế DraggableItem — hỗ trợ:
///   • Kéo thả giữa các ô (swap / stack)
///   • Kéo thả vào EquipmentSlotUI để trang bị
///   • Right-click x2 (trong 0.4s) để gộp tất cả item cùng loại
///
/// Multi-page: Bind() nhận absolute slot index (0-99).
/// Khi chuyển tab, InventoryController gọi Bind() lại với offset mới.
/// </summary>
public class InventorySlotView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{
    [Header("Visual")]
    [SerializeField] private Image            _iconImage;
    [SerializeField] private TextMeshProUGUI  _nameText;
    [SerializeField] private TextMeshProUGUI  _quantityText;
    [SerializeField] private Image            _emptyBackground;   // ô xám khi trống (tùy chọn)
    [SerializeField] private CanvasGroup      _canvasGroup;

    // Runtime binding (gán bởi InventoryController)
    private int              _slotIndex  = -1;   // absolute index (0-99)
    private InventoryRuntime _runtime;
    private InventoryController _owner;

    // Ghost khi kéo
    private RectTransform _ghostRect;

    // Double right-click
    private float _lastRightClickTime = -999f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.4f;

    // ─────────────────────────────────────────────────
    //  BINDING
    // ─────────────────────────────────────────────────

    /// <summary>Gán slot index (absolute 0-99) + runtime cho view này.</summary>
    public void Bind(int slotIndex, InventoryRuntime runtime, InventoryController owner)
    {
        _slotIndex = slotIndex;
        _runtime   = runtime;
        _owner     = owner;
        Refresh();
    }

    /// <summary>Cập nhật visual theo trạng thái slot hiện tại.</summary>
    public void Refresh()
    {
        if (_runtime == null || _slotIndex < 0) return;

        InventorySlot slot = _runtime.GetSlot(_slotIndex);
        bool hasItem       = slot != null && !slot.IsEmpty;
        bool hasIcon       = hasItem && slot.Item.Icon != null;

        // Icon
        if (_iconImage != null)
        {
            _iconImage.enabled = true;
            _iconImage.sprite  = hasIcon ? slot.Item.Icon : null;
            _iconImage.color   = hasItem
                ? (hasIcon ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.6f))
                : Color.clear;
        }

        // Tên
        if (_nameText != null)
            _nameText.text = hasItem ? slot.Item.DisplayName : string.Empty;

        // Số lượng (ẩn khi = 1)
        if (_quantityText != null)
            _quantityText.text = (hasItem && slot.Quantity > 1) ? $"x{slot.Quantity}" : string.Empty;

        // Nền ô rỗng
        if (_emptyBackground != null)
            _emptyBackground.enabled = !hasItem;

        gameObject.name = hasItem ? $"Slot{_slotIndex}_{slot.Item.DisplayName}" : $"Slot{_slotIndex}_Empty";
    }

    // ─────────────────────────────────────────────────
    //  DRAG (bắt đầu kéo từ ô này)
    // ─────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_runtime == null || _slotIndex < 0) return;

        InventorySlot slot = _runtime.GetSlot(_slotIndex);
        if (slot == null || slot.IsEmpty) return;

        InventoryDragContext.BeginFromInventorySlot(slot.Item, _slotIndex, this);

        // Mờ ô nguồn
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha        = 0.35f;
            _canvasGroup.blocksRaycasts = false;
        }

        CreateGhost(slot.Item.Icon);
        UpdateGhost(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        UpdateGhost(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        DestroyGhost();
        InventoryDragContext.Clear();
        Refresh();
    }

    // ─────────────────────────────────────────────────
    //  DROP (nhận kéo từ ô khác vào ô này)
    // ─────────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        if (_runtime == null || _slotIndex < 0) return;

        // Kéo từ ô inventory sang ô inventory (cùng trang — vì cả 2 view đều bind cùng page)
        if (InventoryDragContext.ActiveInventorySlotSource >= 0
            && InventoryDragContext.ActiveInventorySlotSource != _slotIndex)
        {
            _runtime.MoveSlot(InventoryDragContext.ActiveInventorySlotSource, _slotIndex);
            return;
        }

        // Kéo từ EquipmentSlot về inventory (unequip → vào ô này)
        if (InventoryDragContext.ActiveEquipmentSource != null)
        {
            InventoryItemRecord item = InventoryDragContext.ActiveItem;
            if (item == null) return;

            InventoryDragContext.ActiveEquipmentSource.ClearSlotWithoutReturningToInventory();
            _runtime.AddItemToSlot(_slotIndex, item, 1);
        }
    }

    // ─────────────────────────────────────────────────
    //  RIGHT-CLICK x2 → MERGE
    // ─────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (_runtime == null || _slotIndex < 0) return;

        InventorySlot slot = _runtime.GetSlot(_slotIndex);
        if (slot == null || slot.IsEmpty) return;

        // Dùng unscaledTime vì timeScale có thể = 0 khi inventory mở
        float now = Time.unscaledTime;
        if (now - _lastRightClickTime <= DOUBLE_CLICK_THRESHOLD)
        {
            // Double right-click: gộp tất cả item cùng id
            string itemId = slot.Item.Id;
            if (!string.IsNullOrEmpty(itemId))
            {
                _runtime.MergeAllSame(itemId);
                Debug.Log($"[InventorySlotView] Merge tất cả '{slot.Item.DisplayName}' vào 1 ô.");
            }
            _lastRightClickTime = -999f;
        }
        else
        {
            _lastRightClickTime = now;
        }
    }

    // ─────────────────────────────────────────────────
    //  GHOST ICON
    // ─────────────────────────────────────────────────

    private void CreateGhost(Sprite icon)
    {
        Canvas rootCanvas = _owner != null ? _owner.GetRootCanvas() : null;
        if (rootCanvas == null) return;

        GameObject g = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        g.transform.SetParent(rootCanvas.transform, false);

        _ghostRect              = g.GetComponent<RectTransform>();
        _ghostRect.sizeDelta    = new Vector2(72f, 72f);

        Image img               = g.GetComponent<Image>();
        img.sprite              = icon;
        img.preserveAspect      = true;
        img.raycastTarget       = false;
        img.color               = icon != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);

        CanvasGroup cg          = g.GetComponent<CanvasGroup>();
        cg.alpha                = 0.85f;
        cg.blocksRaycasts       = false;
    }

    private void UpdateGhost(Vector2 screenPos)
    {
        if (_ghostRect != null) _ghostRect.position = screenPos;
    }

    private void DestroyGhost()
    {
        if (_ghostRect != null) { Destroy(_ghostRect.gameObject); _ghostRect = null; }
    }

    // ─────────────────────────────────────────────────
    //  FILTER VISUAL (giữ cho search filter dùng)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Ẩn visual khi search không khớp.
    /// Với multi-page, tab filter không cần hàm này nữa (mỗi trang chỉ chứa item đúng loại).
    /// Chỉ dùng cho search text filter.
    /// </summary>
    public void SetFilterMatch(bool matches)
    {
        if (matches) return;  // Khớp → giữ nguyên visual từ Refresh()

        // Ẩn visual
        if (_iconImage != null) _iconImage.color = Color.clear;
        if (_nameText != null) _nameText.text = string.Empty;
        if (_quantityText != null) _quantityText.text = string.Empty;
        if (_emptyBackground != null) _emptyBackground.enabled = true;
    }

    // ─────────────────────────────────────────────────
    //  PROPERTIES
    // ─────────────────────────────────────────────────

    public int SlotIndex => _slotIndex;

    public InventoryItemRecord CurrentItem
        => (_runtime != null && _slotIndex >= 0) ? _runtime.GetSlot(_slotIndex)?.Item : null;
}
