using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentSlotUI : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private InventoryItemRecord.EquipmentSlotKind _slotKind;
    [SerializeField] private EquipmentManager _equipmentManager;
    [SerializeField] private InventoryController _inventoryController;
    [SerializeField] private Image _slotIcon;
    [SerializeField] private Image _slotBackground;    // (tuỳ chọn) nền đổi màu khi equipped
    [SerializeField] private CanvasGroup _slotCanvasGroup;

    // Màu nền: xanh lá mờ khi đang trang bị, tối khi trống
    private static readonly Color COLOR_EQUIPPED_BG = new Color(0.15f, 0.55f, 0.15f, 0.45f);
    private static readonly Color COLOR_EMPTY_BG    = new Color(0.08f, 0.08f, 0.08f, 0.30f);
    // Màu icon: vàng placeholder khi equipped nhưng không có sprite
    private static readonly Color COLOR_ICON_NO_SPRITE = new Color(0.95f, 0.78f, 0.10f, 0.90f);

    private RectTransform _ghostRect;
    private InventoryItemRecord _lastBoundItem;

    private void Awake()
    {
        if (_equipmentManager == null)
        {
            _equipmentManager = EquipmentManager.Current;
        }

        if (_inventoryController == null)
        {
            _inventoryController = FindFirstObjectByType<InventoryController>();
        }
    }

    private void OnEnable()
    {
        RefreshSlotVisual();
    }

    private void Update()
    {
        InventoryItemRecord currentItem = GetCurrentItem();
        if (!ReferenceEquals(currentItem?.WeaponData, _lastBoundItem?.WeaponData)
            || !ReferenceEquals(currentItem?.ShieldData, _lastBoundItem?.ShieldData)
            || !ReferenceEquals(currentItem?.AccessoryData, _lastBoundItem?.AccessoryData))
        {
            RefreshSlotVisual();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        InventoryItemRecord draggedItem = InventoryDragContext.ActiveItem;
        if (draggedItem == null || !draggedItem.CanEquipTo(_slotKind))
        {
            return;
        }

        // Drag-and-drop flow:
        // 1. Validate item type against the slot.
        // 2. Equip the new item through EquipmentManager.
        // 3. Remove the dragged item from the bag (or clear previous slot source).
        // 4. If the slot already had an item, push that old item back into inventory.
        InventoryItemRecord previousItem = GetCurrentItem();

        // [FIX MẤT ITEM] Nếu slot đang có món cũ phải trả về túi, kiểm tra túi còn chỗ TRƯỚC khi equip.
        // Khi kéo từ inventory: ô nguồn sẽ giải phóng cùng trang nên luôn đủ chỗ → bỏ qua check.
        // Khi kéo từ equipment slot khác (không giải phóng ô túi nào): phải đảm bảo có chỗ, nếu
        // không thì huỷ thao tác để không làm bay mất món cũ.
        bool sourceFreesInventorySlot = InventoryDragContext.ActiveInventorySlotSource >= 0
                                        || InventoryDragContext.ActiveInventorySource != null;
        if (previousItem != null && !sourceFreesInventorySlot
            && _inventoryController != null && !_inventoryController.CanAccept(previousItem))
        {
            Debug.LogWarning($"[EquipmentSlotUI] Huỷ trang bị: túi đầy, không thể trả '{previousItem.DisplayName}' về.");
            return;
        }

        if (!TryEquip(draggedItem))
        {
            return;
        }

        // ── Xóa item khỏi nguồn kéo ──────────────────────────────────────
        if (InventoryDragContext.ActiveInventorySlotSource >= 0)
        {
            // Kéo từ InventorySlotView (hệ thống 25 ô mới)
            _inventoryController.RemoveFromSlot(InventoryDragContext.ActiveInventorySlotSource, 1);
        }
        else if (InventoryDragContext.ActiveInventorySource != null)
        {
            // Kéo từ DraggableItem (hệ thống cũ — backward compat)
            _inventoryController.RemoveItemFromInventory(draggedItem);
        }
        else if (InventoryDragContext.ActiveEquipmentSource != null && InventoryDragContext.ActiveEquipmentSource != this)
        {
            InventoryDragContext.ActiveEquipmentSource.ClearSlotWithoutReturningToInventory();
        }

        if (previousItem != null)
        {
            // Đã kiểm tra CanAccept ở trên (hoặc nguồn vừa giải phóng 1 ô) → add gần như chắc chắn thành công.
            if (!_inventoryController.ReturnItemToInventory(previousItem))
                Debug.LogWarning($"[EquipmentSlotUI] Không trả được '{previousItem.DisplayName}' về túi (trang đầy).");
        }

        RefreshSlotVisual();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        InventoryItemRecord currentItem = GetCurrentItem();
        if (currentItem == null || _inventoryController == null)
        {
            return;
        }

        InventoryDragContext.BeginFromEquipment(currentItem, this);
        CreateGhostIcon(currentItem.Icon);

        if (_slotCanvasGroup != null)
        {
            _slotCanvasGroup.alpha = 0.35f;
            _slotCanvasGroup.blocksRaycasts = false;
        }

        _ghostRect.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_ghostRect != null)
        {
            _ghostRect.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        bool endedOverSlot = eventData.pointerEnter != null
            && eventData.pointerEnter.GetComponentInParent<EquipmentSlotUI>() != null;

        // Dragging an equipped item to empty space means unequip and return it to the bag.
        // If the pointer ended over another equipment slot, let that slot decide whether it accepts the drop.
        if (!endedOverSlot && InventoryDragContext.ActiveEquipmentSource == this)
        {
            UnequipCurrentToInventory();
        }

        if (_slotCanvasGroup != null)
        {
            _slotCanvasGroup.alpha = 1f;
            _slotCanvasGroup.blocksRaycasts = true;
        }

        DestroyGhostIcon();
        InventoryDragContext.Clear();
        RefreshSlotVisual();
    }

    public void RefreshSlotVisual()
    {
        InventoryItemRecord currentItem = GetCurrentItem();
        _lastBoundItem = currentItem;

        bool isEquipped = currentItem != null;
        bool hasIcon    = isEquipped && currentItem.Icon != null;

        // ── Icon ────────────────────────────────────────────────────────────
        if (_slotIcon != null)
        {
            _slotIcon.enabled = true;
            _slotIcon.sprite  = hasIcon ? currentItem.Icon : null;
            _slotIcon.color   = hasIcon    ? Color.white          // có sprite
                              : isEquipped ? COLOR_ICON_NO_SPRITE  // equipped nhưng không có icon → vàng
                                           : Color.clear;          // trống → trong suốt
        }

        // ── Background ──────────────────────────────────────────────────────
        if (_slotBackground != null)
        {
            _slotBackground.color = isEquipped ? COLOR_EQUIPPED_BG : COLOR_EMPTY_BG;
        }
    }

    public void ClearSlotWithoutReturningToInventory()
    {
        UnequipInternal();
        RefreshSlotVisual();
    }

    private void UnequipCurrentToInventory()
    {
        InventoryItemRecord currentItem = GetCurrentItem();
        if (currentItem != null)
        {
            // [FIX MẤT ITEM] Chỉ tháo trang bị khi đã trả được về túi thành công.
            // Nếu trang túi đầy (ReturnItemToInventory=false) mà vẫn UnequipInternal thì item biến mất.
            if (!_inventoryController.ReturnItemToInventory(currentItem))
            {
                Debug.LogWarning($"[EquipmentSlotUI] Túi đầy — không thể tháo '{currentItem.DisplayName}'. Giữ nguyên trang bị.");
                RefreshSlotVisual();
                return;
            }
        }

        UnequipInternal();
        RefreshSlotVisual();
    }

    private bool TryEquip(InventoryItemRecord item)
    {
        if (_equipmentManager == null || item == null)
        {
            return false;
        }

        // Defense-in-depth: chặn sai loại slot kể cả khi không đi qua OnDrop (guard của _slotKind)
        if (!item.CanEquipTo(_slotKind))
        {
            return false;
        }

        if (item.WeaponData != null)
        {
            _equipmentManager.EquipWeapon(item.WeaponData);
            return true;
        }

        if (item.ShieldData != null)
        {
            _equipmentManager.EquipCoreShield(item.ShieldData);
            return true;
        }

        if (item.AccessoryData != null)
        {
            _equipmentManager.EquipAccessory(item.AccessoryData);
            return true;
        }

        return false;
    }

    private void UnequipInternal()
    {
        if (_equipmentManager == null)
        {
            return;
        }

        switch (_slotKind)
        {
            case InventoryItemRecord.EquipmentSlotKind.Weapon:
                _equipmentManager.UnequipWeapon();
                break;
            case InventoryItemRecord.EquipmentSlotKind.Shield:
                _equipmentManager.UnequipCoreShield();
                break;
            case InventoryItemRecord.EquipmentSlotKind.CoreShard:
            case InventoryItemRecord.EquipmentSlotKind.MarkOfSin:
            case InventoryItemRecord.EquipmentSlotKind.RelicOfMemory:
            case InventoryItemRecord.EquipmentSlotKind.Parasite:
            case InventoryItemRecord.EquipmentSlotKind.Chain:
                AccessoryData accessory = _equipmentManager.GetAccessoryInSlot(_slotKind);
                if (accessory != null)
                {
                    _equipmentManager.UnequipAccessory(accessory);
                }
                break;
        }
    }

    private InventoryItemRecord GetCurrentItem()
    {
        if (_equipmentManager == null)
        {
            return null;
        }

        switch (_slotKind)
        {
            case InventoryItemRecord.EquipmentSlotKind.Weapon:
                return InventoryItemRecord.FromWeapon(_equipmentManager.GetVisibleEquippedWeapon());
            case InventoryItemRecord.EquipmentSlotKind.Shield:
                return InventoryItemRecord.FromShield(_equipmentManager.currentCoreShield);
            case InventoryItemRecord.EquipmentSlotKind.CoreShard:
            case InventoryItemRecord.EquipmentSlotKind.MarkOfSin:
            case InventoryItemRecord.EquipmentSlotKind.RelicOfMemory:
            case InventoryItemRecord.EquipmentSlotKind.Parasite:
            case InventoryItemRecord.EquipmentSlotKind.Chain:
                return InventoryItemRecord.FromAccessory(_equipmentManager.GetAccessoryInSlot(_slotKind));
            default:
                return null;
        }
    }

    private void CreateGhostIcon(Sprite icon)
    {
        Canvas rootCanvas = _inventoryController != null ? _inventoryController.GetRootCanvas() : null;
        if (rootCanvas == null)
        {
            return;
        }

        GameObject ghostObject = new GameObject("EquippedDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghostObject.transform.SetParent(rootCanvas.transform, false);
        _ghostRect = ghostObject.GetComponent<RectTransform>();
        _ghostRect.sizeDelta = new Vector2(72f, 72f);

        Image ghostImage = ghostObject.GetComponent<Image>();
        ghostImage.sprite = icon;
        ghostImage.preserveAspect = true;
        ghostImage.raycastTarget = false;

        CanvasGroup ghostCanvasGroup = ghostObject.GetComponent<CanvasGroup>();
        ghostCanvasGroup.alpha = 0.85f;
        ghostCanvasGroup.blocksRaycasts = false;
    }

    private void DestroyGhostIcon()
    {
        if (_ghostRect != null)
        {
            Destroy(_ghostRect.gameObject);
            _ghostRect = null;
        }
    }
}