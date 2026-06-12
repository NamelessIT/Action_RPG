using System;
using UnityEngine;

/// <summary>
/// Đại diện cho 1 ô trong inventory (25 ô cố định).
/// Mỗi ô có thể trống (IsEmpty=true) hoặc chứa 1 loại item với số lượng.
/// </summary>
[Serializable]
public class InventorySlot
{
    private InventoryItemRecord _item;
    private int _quantity;

    public InventoryItemRecord Item     => _item;
    public int                 Quantity => _quantity;
    public bool                IsEmpty  => _item == null || _quantity <= 0;

    /// <summary>Gán item + số lượng vào slot.</summary>
    public void Set(InventoryItemRecord item, int quantity = 1)
    {
        _item     = item;
        _quantity = item != null ? Mathf.Max(1, quantity) : 0;
    }

    /// <summary>Xóa slot về trạng thái rỗng.</summary>
    public void Clear()
    {
        _item     = null;
        _quantity = 0;
    }

    /// <summary>
    /// Kiểm tra item này có thể stack với item khác không.
    /// Stack được khi cùng id, cùng loại và id không rỗng.
    /// </summary>
    public bool CanStackWith(InventoryItemRecord other)
    {
        if (_item == null || other == null) return false;
        if (string.IsNullOrEmpty(_item.Id) || string.IsNullOrEmpty(other.Id)) return false;
        return _item.Id == other.Id && _item.ItemType == other.ItemType;
    }

    /// <summary>Cộng thêm (hoặc trừ nếu âm) số lượng. Tự clear nếu về 0.</summary>
    public void AddQuantity(int delta)
    {
        _quantity = Mathf.Max(0, _quantity + delta);
        if (_quantity == 0) Clear();
    }
}
