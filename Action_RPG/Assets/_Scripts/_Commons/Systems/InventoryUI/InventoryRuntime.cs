using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý inventory theo 4 trang riêng biệt (Weapon / Shield / Artifact / Others).
/// Mỗi trang có 25 ô cố định — hoàn toàn độc lập, không chia sẻ slot.
/// Tổng cộng 100 slot vật lý (4 × 25).
///
/// Layout:
///   Page 0 (Weapon)   → slot  0 – 24
///   Page 1 (Shield)   → slot 25 – 49
///   Page 2 (Artifact) → slot 50 – 74
///   Page 3 (Others)   → slot 75 – 99
/// </summary>
public class InventoryRuntime : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  CONSTANTS
    // ─────────────────────────────────────────────────────────────
    public const int SLOTS_PER_PAGE = 25;
    public const int PAGE_COUNT     = 4;
    public const int TOTAL_SLOTS    = SLOTS_PER_PAGE * PAGE_COUNT; // 100

    /// <summary>Backward-compat: code cũ dùng SLOT_COUNT → giờ trả về 25 (1 trang).</summary>
    public const int SLOT_COUNT = SLOTS_PER_PAGE;

    // ─────────────────────────────────────────────────────────────
    //  SEED (mock data khi không có save)
    // ─────────────────────────────────────────────────────────────
    [Header("Mock Inventory Seeds (Editor / First Launch)")]
    [SerializeField] private List<WeaponData>    _startingWeapons     = new List<WeaponData>();
    [SerializeField] private List<CoreShieldData> _startingShields    = new List<CoreShieldData>();
    [SerializeField] private List<AccessoryData>  _startingAccessories = new List<AccessoryData>();

    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────
    private InventorySlot[] _slots;

    public event Action OnInventoryChanged;

    // ─────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        _slots = new InventorySlot[TOTAL_SLOTS];
        for (int i = 0; i < TOTAL_SLOTS; i++)
            _slots[i] = new InventorySlot();

        BuildInitialInventory();
    }

    // ─────────────────────────────────────────────────────────────
    //  PAGE HELPERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>Trả về offset đầu trang cho loại item.</summary>
    public static int GetPageOffset(InventoryItemRecord.InventoryItemType itemType)
    {
        switch (itemType)
        {
            case InventoryItemRecord.InventoryItemType.Weapon:   return 0;
            case InventoryItemRecord.InventoryItemType.Shield:   return SLOTS_PER_PAGE;
            case InventoryItemRecord.InventoryItemType.Artifact: return SLOTS_PER_PAGE * 2;
            case InventoryItemRecord.InventoryItemType.Others:   return SLOTS_PER_PAGE * 3;
            default: return 0;
        }
    }

    /// <summary>Trả về index tuyệt đối từ local index (0-24) + itemType.</summary>
    public static int ToAbsoluteIndex(int localIndex, InventoryItemRecord.InventoryItemType itemType)
    {
        return GetPageOffset(itemType) + localIndex;
    }

    /// <summary>True nếu absoluteIndex nằm đúng trang (page) của itemType.</summary>
    public static bool IsIndexValidForItemType(int absoluteIndex, InventoryItemRecord.InventoryItemType itemType)
    {
        int pageStart = GetPageOffset(itemType);
        return absoluteIndex >= pageStart && absoluteIndex < pageStart + SLOTS_PER_PAGE;
    }

    /// <summary>True nếu trang của item còn chỗ chứa (ô trống hoặc ô stack được). Dùng để
    /// kiểm tra TRƯỚC khi unequip/clear nguồn → tránh mất item khi trang đầy.</summary>
    public bool HasRoomFor(InventoryItemRecord item)
    {
        if (item == null || _slots == null) return false;
        int pageStart = GetPageOffset(item.ItemType);
        int pageEnd   = pageStart + SLOTS_PER_PAGE;
        for (int i = pageStart; i < pageEnd; i++)
        {
            if (_slots[i].IsEmpty) return true;
            if (_slots[i].CanStackWith(item)) return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  READ
    // ─────────────────────────────────────────────────────────────

    public InventorySlot GetSlot(int absoluteIndex)
    {
        if (_slots == null || absoluteIndex < 0 || absoluteIndex >= TOTAL_SLOTS) return null;
        return _slots[absoluteIndex];
    }

    /// <summary>Legacy — trả về list các item không rỗng (dùng cho code cũ).</summary>
    public IReadOnlyList<InventoryItemRecord> Items
    {
        get
        {
            var list = new List<InventoryItemRecord>();
            for (int i = 0; i < TOTAL_SLOTS; i++)
                if (!_slots[i].IsEmpty) list.Add(_slots[i].Item);
            return list;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  ADD
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Thêm item vào đúng trang dựa theo ItemType:
    /// 1. Tìm ô có cùng item trong trang → stack.
    /// 2. Nếu không → tìm ô trống đầu tiên trong trang.
    /// 3. Nếu hết ô → log warning, trả về FALSE (caller cần xử lý để không mất item).
    /// </summary>
    /// <returns>true nếu thêm/stack thành công; false nếu trang đầy (item KHÔNG được thêm).</returns>
    public bool AddItem(InventoryItemRecord item, int quantity = 1)
    {
        if (item == null) return false;

        int pageStart = GetPageOffset(item.ItemType);
        int pageEnd   = pageStart + SLOTS_PER_PAGE;

        // Tìm stack cùng loại trong đúng trang
        for (int i = pageStart; i < pageEnd; i++)
        {
            if (!_slots[i].IsEmpty && _slots[i].CanStackWith(item))
            {
                _slots[i].AddQuantity(quantity);
                Debug.Log($"[InventoryRuntime] Stack '{item.DisplayName}' x{quantity} vào slot {i} " +
                          $"(tổng: {_slots[i].Quantity})");
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        // Tìm ô trống trong đúng trang
        for (int i = pageStart; i < pageEnd; i++)
        {
            if (_slots[i].IsEmpty)
            {
                _slots[i].Set(item, quantity);
                Debug.Log($"[InventoryRuntime] Thêm '{item.DisplayName}' x{quantity} vào slot {i} " +
                          $"(page: {item.ItemType})");
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        Debug.LogWarning($"[InventoryRuntime] Trang {item.ItemType} đầy ({SLOTS_PER_PAGE} slots) " +
                         $"— không thể thêm '{item.DisplayName}'.");
        return false;
    }

    /// <summary>Thêm item vào ô cụ thể (dùng khi kéo từ equipment về).</summary>
    /// <returns>true nếu thêm thành công; false nếu thất bại (item KHÔNG được thêm).</returns>
    public bool AddItemToSlot(int absoluteIndex, InventoryItemRecord item, int quantity = 1)
    {
        if (item == null || absoluteIndex < 0 || absoluteIndex >= TOTAL_SLOTS) return false;

        // Slot phải thuộc đúng trang của loại item; nếu không, để AddItem tự tìm ô đúng trang
        if (!IsIndexValidForItemType(absoluteIndex, item.ItemType))
        {
            Debug.LogWarning($"[InventoryRuntime] Slot {absoluteIndex} không thuộc trang {item.ItemType} " +
                             $"của '{item.DisplayName}' — chuyển sang tìm ô đúng trang.");
            return AddItem(item, quantity);
        }

        InventorySlot target = _slots[absoluteIndex];
        if (!target.IsEmpty && target.CanStackWith(item))
        {
            target.AddQuantity(quantity);
        }
        else if (target.IsEmpty)
        {
            target.Set(item, quantity);
        }
        else
        {
            // Ô bị chiếm bởi item khác → tìm ô trống trong đúng trang
            return AddItem(item, quantity);
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    //  REMOVE
    // ─────────────────────────────────────────────────────────────

    /// <summary>Xóa 1 quantity của item (tìm theo reference). Legacy compat.</summary>
    public bool RemoveItem(InventoryItemRecord item)
    {
        for (int i = 0; i < TOTAL_SLOTS; i++)
        {
            if (!_slots[i].IsEmpty && ReferenceEquals(_slots[i].Item, item))
            {
                _slots[i].AddQuantity(-1); // tự clear nếu về 0
                OnInventoryChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    /// <summary>Xóa 1 quantity từ ô cụ thể.</summary>
    public bool RemoveFromSlot(int absoluteIndex, int quantity = 1)
    {
        if (absoluteIndex < 0 || absoluteIndex >= TOTAL_SLOTS) return false;
        if (_slots[absoluteIndex].IsEmpty) return false;

        _slots[absoluteIndex].AddQuantity(-quantity);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();

    // ─────────────────────────────────────────────────────────────
    //  MOVE / SWAP / STACK (chỉ trong cùng trang)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Di chuyển item từ ô from → to:
    /// • Cùng item: stack số lượng, xóa from.
    /// • Khác item: swap 2 ô.
    /// • To trống: di chuyển đơn giản.
    /// Hoạt động với absolute index (0-99).
    /// </summary>
    public void MoveSlot(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= TOTAL_SLOTS) return;
        if (toIndex   < 0 || toIndex   >= TOTAL_SLOTS) return;

        InventorySlot from = _slots[fromIndex];
        InventorySlot to   = _slots[toIndex];

        if (from.IsEmpty) return;

        // Chỉ cho phép di chuyển trong đúng trang của loại item (không cho lệch trang)
        if (!IsIndexValidForItemType(toIndex, from.Item.ItemType))
        {
            Debug.LogWarning($"[InventoryRuntime] Không thể chuyển '{from.Item.DisplayName}' " +
                             $"sang slot {toIndex} — khác trang ({from.Item.ItemType}).");
            return;
        }

        if (!to.IsEmpty && from.CanStackWith(to.Item))
        {
            // ── Stack ──────────────────────────────────────────
            to.AddQuantity(from.Quantity);
            from.Clear();
            Debug.Log($"[InventoryRuntime] Stack slot {fromIndex} → slot {toIndex} " +
                      $"(tổng: {to.Quantity})");
        }
        else
        {
            // ── Swap ───────────────────────────────────────────
            InventoryItemRecord tmpItem = to.Item;
            int                 tmpQty  = to.Quantity;

            to.Set(from.Item, from.Quantity);

            if (tmpItem != null)
                from.Set(tmpItem, tmpQty);
            else
                from.Clear();

            Debug.Log($"[InventoryRuntime] Swap slot {fromIndex} ↔ slot {toIndex}");
        }

        OnInventoryChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  MERGE (trong toàn bộ 100 slot)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gộp tất cả item có cùng id vào ô xuất hiện đầu tiên.
    /// Triggered bởi right-click x2 trên InventorySlotView.
    /// </summary>
    public void MergeAllSame(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        int totalQty           = 0;
        int firstSlotIndex     = -1;
        InventoryItemRecord ref_item = null;

        for (int i = 0; i < TOTAL_SLOTS; i++)
        {
            if (_slots[i].IsEmpty) continue;
            if (_slots[i].Item.Id != itemId) continue;

            if (firstSlotIndex < 0)
            {
                firstSlotIndex = i;
                ref_item       = _slots[i].Item;
            }
            else
            {
                totalQty += _slots[i].Quantity;
                _slots[i].Clear();
            }
        }

        if (firstSlotIndex >= 0 && totalQty > 0)
        {
            _slots[firstSlotIndex].AddQuantity(totalQty);
            Debug.Log($"[InventoryRuntime] Merge '{ref_item?.DisplayName}': " +
                      $"tổng {_slots[firstSlotIndex].Quantity} vào slot {firstSlotIndex}");
        }

        OnInventoryChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  SAVE / LOAD
    // ─────────────────────────────────────────────────────────────

    public List<PlayerStateSaveData.SavedInventoryItem> GetInventorySaveData()
    {
        var saved = new List<PlayerStateSaveData.SavedInventoryItem>();

        for (int i = 0; i < TOTAL_SLOTS; i++)
        {
            if (_slots[i].IsEmpty) continue;

            InventoryItemRecord item = _slots[i].Item;
            string assetId = string.Empty;

            if (item.WeaponData    != null) assetId = item.WeaponData.id;
            else if (item.ShieldData    != null) assetId = item.ShieldData.id;
            else if (item.AccessoryData != null) assetId = item.AccessoryData.id;

            if (string.IsNullOrEmpty(assetId)) continue;

            saved.Add(new PlayerStateSaveData.SavedInventoryItem
            {
                itemType  = item.ItemType.ToString(),
                assetId   = assetId,
                quantity  = _slots[i].Quantity,
                slotIndex = i  // Giờ lưu absolute index (0-99)
            });
        }

        return saved;
    }

    public void LoadInventoryFromSave(
        List<PlayerStateSaveData.SavedInventoryItem> savedItems,
        WeaponDatabase    weaponDB,
        AccessoryDatabase accessoryDB)
    {
        if (savedItems == null || savedItems.Count == 0) return;

        // Clear toàn bộ slots trước
        for (int i = 0; i < TOTAL_SLOTS; i++) _slots[i].Clear();

        var weaponLookup    = new System.Collections.Generic.Dictionary<string, WeaponData>();
        var shieldLookup    = new System.Collections.Generic.Dictionary<string, CoreShieldData>();
        var accessoryLookup = new System.Collections.Generic.Dictionary<string, AccessoryData>();

        foreach (var w in Resources.LoadAll<WeaponData>(ResourcePaths.Weapons))
            if (!string.IsNullOrEmpty(w.id)) weaponLookup[w.id]    = w;
        foreach (var s in Resources.LoadAll<CoreShieldData>(ResourcePaths.CoreShields))
            if (!string.IsNullOrEmpty(s.id)) shieldLookup[s.id]    = s;
        foreach (var a in Resources.LoadAll<AccessoryData>(ResourcePaths.Accessories))
            if (!string.IsNullOrEmpty(a.id)) accessoryLookup[a.id] = a;

        foreach (var saved in savedItems)
        {
            InventoryItemRecord record = null;

            if (saved.itemType == "Weapon" && weaponLookup.TryGetValue(saved.assetId, out var w))
                record = InventoryItemRecord.FromWeapon(w);
            else if (saved.itemType == "Shield" && shieldLookup.TryGetValue(saved.assetId, out var s))
                record = InventoryItemRecord.FromShield(s);
            else if ((saved.itemType == "Artifact" || saved.itemType == "Others")
                     && accessoryLookup.TryGetValue(saved.assetId, out var a))
                record = InventoryItemRecord.FromAccessory(a);

            if (record == null) continue;

            int qty       = Mathf.Max(1, saved.quantity);
            int targetIdx = saved.slotIndex;

            // Dùng đúng slot nếu hợp lệ, ĐÚNG TRANG và trống
            if (targetIdx >= 0 && targetIdx < TOTAL_SLOTS
                && IsIndexValidForItemType(targetIdx, record.ItemType)
                && _slots[targetIdx].IsEmpty)
                _slots[targetIdx].Set(record, qty);
            else
                AddItem(record, qty);  // fallback: tìm ô trống đúng trang
        }

        OnInventoryChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  INITIAL SEED
    // ─────────────────────────────────────────────────────────────

    private void BuildInitialInventory()
    {
        foreach (var w in _startingWeapons)
        {
            var r = InventoryItemRecord.FromWeapon(w);
            if (r != null) AddItem(r); // Tự vào trang Weapon (slot 0-24)
        }
        foreach (var s in _startingShields)
        {
            var r = InventoryItemRecord.FromShield(s);
            if (r != null) AddItem(r); // Tự vào trang Shield (slot 25-49)
        }
        foreach (var a in _startingAccessories)
        {
            var r = InventoryItemRecord.FromAccessory(a);
            if (r != null) AddItem(r); // Tự vào trang Artifact (slot 50-74)
        }
    }
}
