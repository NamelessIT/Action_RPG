using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryRuntime : MonoBehaviour
{
    [Header("Mock Inventory Seeds")]
    [SerializeField] private List<WeaponData> _startingWeapons = new List<WeaponData>();
    [SerializeField] private List<CoreShieldData> _startingShields = new List<CoreShieldData>();
    [SerializeField] private List<AccessoryData> _startingAccessories = new List<AccessoryData>();

    private readonly List<InventoryItemRecord> _items = new List<InventoryItemRecord>();

    public event Action OnInventoryChanged;

    public IReadOnlyList<InventoryItemRecord> Items => _items;

    private void Awake()
    {
        BuildInitialInventory();
    }

    public void AddItem(InventoryItemRecord item)
    {
        if (item == null)
        {
            return;
        }

        _items.Add(item);
        OnInventoryChanged?.Invoke();
    }

    public bool RemoveItem(InventoryItemRecord item)
    {
        if (item == null)
        {
            return false;
        }

        bool removed = _items.Remove(item);
        if (removed)
        {
            OnInventoryChanged?.Invoke();
        }

        return removed;
    }

    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Serialises the current inventory into save-friendly records.
    /// </summary>
    public List<PlayerStateSaveData.SavedInventoryItem> GetInventorySaveData()
    {
        var saved = new List<PlayerStateSaveData.SavedInventoryItem>();
        foreach (var item in _items)
        {
            string assetId = string.Empty;
            string itemType = item.ItemType.ToString();

            if (item.WeaponData != null) assetId = item.WeaponData.id;
            else if (item.ShieldData != null) assetId = item.ShieldData.id;
            else if (item.AccessoryData != null) assetId = item.AccessoryData.id;

            if (string.IsNullOrEmpty(assetId)) continue;

            saved.Add(new PlayerStateSaveData.SavedInventoryItem
            {
                itemType = itemType,
                assetId = assetId,
                quantity = 1
            });
        }
        return saved;
    }

    /// <summary>
    /// Restores inventory from save data.
    /// <para>
    /// <paramref name="weaponDB"/> and <paramref name="accessoryDB"/> expose lightweight stat
    /// lookups (WeaponDB / stat entries) but do not hold ScriptableObject references directly.
    /// The actual WeaponData / CoreShieldData / AccessoryData assets are resolved via
    /// Resources.LoadAll — the same pattern used by GameManager.EnsureItemLookups().
    /// </para>
    /// </summary>
    public void LoadInventoryFromSave(
        List<PlayerStateSaveData.SavedInventoryItem> savedItems,
        WeaponDatabase weaponDB,
        AccessoryDatabase accessoryDB)
    {
        if (savedItems == null || savedItems.Count == 0) return;

        // WeaponDatabase.GetWeapon(id) returns WeaponDB (plain data class), not WeaponData
        // (ScriptableObject). Build lookups from Resources the same way GameManager does.
        var weaponLookup = new Dictionary<string, WeaponData>();
        foreach (var w in Resources.LoadAll<WeaponData>("Datas/Weapons"))
            if (!string.IsNullOrEmpty(w.id)) weaponLookup[w.id] = w;

        var shieldLookup = new Dictionary<string, CoreShieldData>();
        foreach (var s in Resources.LoadAll<CoreShieldData>("Datas/Core Shields"))
            if (!string.IsNullOrEmpty(s.id)) shieldLookup[s.id] = s;

        // AccessoryDatabase.GetStats(int) returns stat entries only, not AccessoryData.
        var accessoryLookup = new Dictionary<string, AccessoryData>();
        foreach (var a in Resources.LoadAll<AccessoryData>("Datas/Accessories"))
            if (!string.IsNullOrEmpty(a.id)) accessoryLookup[a.id] = a;

        foreach (var saved in savedItems)
        {
            InventoryItemRecord record = null;

            if (saved.itemType == "Weapon")
            {
                if (weaponLookup.TryGetValue(saved.assetId, out WeaponData weaponData))
                    record = InventoryItemRecord.FromWeapon(weaponData);
            }
            else if (saved.itemType == "Shield")
            {
                if (shieldLookup.TryGetValue(saved.assetId, out CoreShieldData shieldData))
                    record = InventoryItemRecord.FromShield(shieldData);
            }
            else if (saved.itemType == "Artifact" || saved.itemType == "Others")
            {
                if (accessoryLookup.TryGetValue(saved.assetId, out AccessoryData accessoryData))
                    record = InventoryItemRecord.FromAccessory(accessoryData);
            }

            if (record != null)
            {
                _items.Add(record);
            }
        }
        OnInventoryChanged?.Invoke();
    }

    private void BuildInitialInventory()
    {
        if (_items.Count > 0)
        {
            return;
        }

        for (int index = 0; index < _startingWeapons.Count; index++)
        {
            InventoryItemRecord record = InventoryItemRecord.FromWeapon(_startingWeapons[index]);
            if (record != null)
            {
                _items.Add(record);
            }
        }

        for (int index = 0; index < _startingShields.Count; index++)
        {
            InventoryItemRecord record = InventoryItemRecord.FromShield(_startingShields[index]);
            if (record != null)
            {
                _items.Add(record);
            }
        }

        for (int index = 0; index < _startingAccessories.Count; index++)
        {
            InventoryItemRecord record = InventoryItemRecord.FromAccessory(_startingAccessories[index]);
            if (record != null)
            {
                _items.Add(record);
            }
        }

        OnInventoryChanged?.Invoke();
    }
}