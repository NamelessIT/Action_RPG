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