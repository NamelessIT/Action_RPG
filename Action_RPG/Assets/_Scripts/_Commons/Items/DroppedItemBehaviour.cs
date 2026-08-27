using UnityEngine;

/// <summary>
/// Represents a physical item dropped in the world.
/// Attach to a prefab with a SpriteRenderer.
/// ItemPickupManager uses OverlapSphere to detect nearby instances.
/// </summary>
public class DroppedItemBehaviour : MonoBehaviour
{
    private LootEntry _entry;
    public LootEntry Entry => _entry;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private float _pickupRadius = 1.5f;

    public float PickupRadius => _pickupRadius;

    /// <summary>
    /// Initialises the dropped item with loot data and applies rarity colour.
    /// </summary>
    public void Initialize(LootEntry entry)
    {
        _entry = entry;

        if (entry.icon != null && _spriteRenderer != null)
        {
            _spriteRenderer.color = GetRarityColor(entry);
        }

        Debug.Log($"[DroppedItem] Spawned: {entry.displayName}");
    }

    /// <summary>
    /// Màu rarity của item rơi, lấy từ bảng dùng chung <see cref="RarityColors"/>
    /// (không tự giữ bảng màu riêng nữa — trước đây hai bảng lệch nhau ở bậc Stained).
    /// Trắng nếu entry không mang data có rarity.
    /// </summary>
    private Color GetRarityColor(LootEntry entry)
    {
        if (entry.weaponData != null)    return RarityColors.Get(entry.weaponData.rarity);
        if (entry.accessoryData != null) return RarityColors.Get(entry.accessoryData.rarity);

        return Color.white;
    }
}
