using System;
using UnityEngine;

[Serializable]
public class InventoryItemRecord
{
    public enum InventoryItemType
    {
        Weapon,
        Shield,
        Artifact,
        Others
    }

    public enum EquipmentSlotKind
    {
        Weapon,
        Shield,
        CoreShard,
        MarkOfSin,
        RelicOfMemory,
        Parasite,
        Chain
    }

    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _icon;
    [SerializeField] private InventoryItemType _itemType;
    [SerializeField] private WeaponData _weaponData;
    [SerializeField] private CoreShieldData _shieldData;
    [SerializeField] private AccessoryData _accessoryData;

    public string Id => _id;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public InventoryItemType ItemType => _itemType;
    public WeaponData WeaponData => _weaponData;
    public CoreShieldData ShieldData => _shieldData;
    public AccessoryData AccessoryData => _accessoryData;

    public bool IsEquippable => _weaponData != null || _shieldData != null || _accessoryData != null;

    public bool MatchesTab(InventoryItemType tab)
    {
        return _itemType == tab;
    }

    public bool MatchesSearch(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return true;
        }

        return !string.IsNullOrEmpty(_displayName)
            && _displayName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool CanEquipTo(EquipmentSlotKind slotKind)
    {
        if (_weaponData != null)
        {
            return slotKind == EquipmentSlotKind.Weapon;
        }

        if (_shieldData != null)
        {
            return slotKind == EquipmentSlotKind.Shield;
        }

        if (_accessoryData == null)
        {
            return false;
        }

        return slotKind == ToAccessorySlotKind(_accessoryData.accessoryType);
    }

    public static EquipmentSlotKind ToAccessorySlotKind(AccessoryData.AccessoryType accessoryType)
    {
        switch (accessoryType)
        {
            case AccessoryData.AccessoryType.CoreShard:
                return EquipmentSlotKind.CoreShard;
            case AccessoryData.AccessoryType.MarkOfSin:
                return EquipmentSlotKind.MarkOfSin;
            case AccessoryData.AccessoryType.RelicOfMemory:
                return EquipmentSlotKind.RelicOfMemory;
            case AccessoryData.AccessoryType.Parasite:
                return EquipmentSlotKind.Parasite;
            case AccessoryData.AccessoryType.Chain:
                return EquipmentSlotKind.Chain;
            default:
                return EquipmentSlotKind.CoreShard;
        }
    }

    public static InventoryItemRecord FromWeapon(WeaponData data)
    {
        if (data == null)
        {
            return null;
        }

        return new InventoryItemRecord
        {
            _id = data.id,
            _displayName = data.weaponName,
            _icon = data.icon,
            _itemType = InventoryItemType.Weapon,
            _weaponData = data,
        };
    }

    public static InventoryItemRecord FromShield(CoreShieldData data)
    {
        if (data == null)
        {
            return null;
        }

        return new InventoryItemRecord
        {
            _id = data.id,
            _displayName = data.coreShieldName,
            _icon = data.icon,
            _itemType = InventoryItemType.Shield,
            _shieldData = data,
        };
    }

    public static InventoryItemRecord FromAccessory(AccessoryData data)
    {
        if (data == null)
        {
            return null;
        }

        return new InventoryItemRecord
        {
            _id = data.id,
            _displayName = data.AccessoryName,
            _icon = data.icon,
            _itemType = InventoryItemType.Artifact,
            _accessoryData = data,
        };
    }
}