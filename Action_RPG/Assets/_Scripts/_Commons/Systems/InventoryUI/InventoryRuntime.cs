using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý 25 ô inventory cố định.
/// Hỗ trợ: thêm item (auto stack / auto slot), di chuyển giữa ô, merge same items.
/// </summary>
public class InventoryRuntime : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  CONSTANTS
    // ─────────────────────────────────────────────────────────────
    public const int SLOT_COUNT = 25;

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
        _slots = new InventorySlot[SLOT_COUNT];
        for (int i = 0; i < SLOT_COUNT; i++)
            _slots[i] = new InventorySlot();

        BuildInitialInventory();
    }

    // ─────────────────────────────────────────────────────────────
    //  READ
    // ─────────────────────────────────────────────────────────────

    public InventorySlot GetSlot(int index)
    {
        if (_slots == null || index < 0 || index >= SLOT_COUNT) return null;
        return _slots[index];
    }

    /// <summary>Legacy — trả về list các item không rỗng (dùng cho code cũ).</summary>
    public IReadOnlyList<InventoryItemRecord> Items
    {
        get
        {
            var list = new List<InventoryItemRecord>();
            for (int i = 0; i < SLOT_COUNT; i++)
                if (!_slots[i].IsEmpty) list.Add(_slots[i].Item);
            return list;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  ADD
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Thêm item vào inventory:
    /// 1. Tìm ô có cùng item → stack số lượng.
    /// 2. Nếu không → tìm ô trống đầu tiên.
    /// 3. Nếu hết ô → log warning.
    /// </summary>
    public void AddItem(InventoryItemRecord item, int quantity = 1)
    {
        if (item == null) return;

        // Tìm stack cùng loại
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (!_slots[i].IsEmpty && _slots[i].CanStackWith(item))
            {
                _slots[i].AddQuantity(quantity);
                Debug.Log($"[InventoryRuntime] Stack '{item.DisplayName}' x{quantity} vào slot {i} " +
                          $"(tổng: {_slots[i].Quantity})");
                OnInventoryChanged?.Invoke();
                return;
            }
        }

        // Tìm ô trống
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (_slots[i].IsEmpty)
            {
                _slots[i].Set(item, quantity);
                Debug.Log($"[InventoryRuntime] Thêm '{item.DisplayName}' x{quantity} vào slot {i}");
                OnInventoryChanged?.Invoke();
                return;
            }
        }

        Debug.LogWarning($"[InventoryRuntime] Inventory đầy ({SLOT_COUNT} slots) — không thể thêm '{item.DisplayName}'.");
    }

    /// <summary>Thêm item vào ô cụ thể (dùng khi kéo từ equipment về).</summary>
    public void AddItemToSlot(int slotIndex, InventoryItemRecord item, int quantity = 1)
    {
        if (item == null || slotIndex < 0 || slotIndex >= SLOT_COUNT) return;

        InventorySlot target = _slots[slotIndex];
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
            // Ô bị chiếm bởi item khác → tìm ô khác
            AddItem(item, quantity);
            return;
        }
        OnInventoryChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  REMOVE
    // ─────────────────────────────────────────────────────────────

    /// <summary>Xóa 1 quantity của item (tìm theo reference). Legacy compat.</summary>
    public bool RemoveItem(InventoryItemRecord item)
    {
        for (int i = 0; i < SLOT_COUNT; i++)
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
    public bool RemoveFromSlot(int slotIndex, int quantity = 1)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return false;
        if (_slots[slotIndex].IsEmpty) return false;

        _slots[slotIndex].AddQuantity(-quantity);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();

    // ─────────────────────────────────────────────────────────────
    //  MOVE / SWAP / STACK
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Di chuyển item từ ô from → to:
    /// • Cùng item: stack số lượng, xóa from.
    /// • Khác item: swap 2 ô.
    /// • To trống: di chuyển đơn giản.
    /// </summary>
    public void MoveSlot(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= SLOT_COUNT) return;
        if (toIndex   < 0 || toIndex   >= SLOT_COUNT) return;

        InventorySlot from = _slots[fromIndex];
        InventorySlot to   = _slots[toIndex];

        if (from.IsEmpty) return;

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
    //  MERGE
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

        for (int i = 0; i < SLOT_COUNT; i++)
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

        for (int i = 0; i < SLOT_COUNT; i++)
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
                slotIndex = i
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
        for (int i = 0; i < SLOT_COUNT; i++) _slots[i].Clear();

        var weaponLookup    = new System.Collections.Generic.Dictionary<string, WeaponData>();
        var shieldLookup    = new System.Collections.Generic.Dictionary<string, CoreShieldData>();
        var accessoryLookup = new System.Collections.Generic.Dictionary<string, AccessoryData>();

        foreach (var w in Resources.LoadAll<WeaponData>("Datas/Weapons"))
            if (!string.IsNullOrEmpty(w.id)) weaponLookup[w.id]    = w;
        foreach (var s in Resources.LoadAll<CoreShieldData>("Datas/Core Shields"))
            if (!string.IsNullOrEmpty(s.id)) shieldLookup[s.id]    = s;
        foreach (var a in Resources.LoadAll<AccessoryData>("Datas/Accessories"))
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

            // Dùng đúng slot nếu hợp lệ và trống
            if (targetIdx >= 0 && targetIdx < SLOT_COUNT && _slots[targetIdx].IsEmpty)
                _slots[targetIdx].Set(record, qty);
            else
                AddItem(record, qty);  // fallback: tìm ô trống
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
            if (r != null) AddItem(r);
        }
        foreach (var s in _startingShields)
        {
            var r = InventoryItemRecord.FromShield(s);
            if (r != null) AddItem(r);
        }
        foreach (var a in _startingAccessories)
        {
            var r = InventoryItemRecord.FromAccessory(a);
            if (r != null) AddItem(r);
        }
    }
}
