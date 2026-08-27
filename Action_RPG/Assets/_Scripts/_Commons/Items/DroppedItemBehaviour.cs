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
    /// Returns a colour representing the rarity of the weapon in the entry.
    /// Falls back to white if no weapon data is present.
    /// </summary>
    private Color GetRarityColor(LootEntry entry)
    {
        if (entry.weaponData != null)
        {
            switch (entry.weaponData.rarity)
            {
                case Rarity.Residual_1:  return new Color(0.70f, 0.70f, 0.70f); // grey
                case Rarity.Stained_2:   return new Color(0.30f, 0.80f, 0.30f); // green
                case Rarity.Corrupted_3: return new Color(0.30f, 0.50f, 1.00f); // blue
                case Rarity.Condemned_4: return new Color(0.60f, 0.20f, 0.90f); // purple
                case Rarity.Anomalous_5: return new Color(1.00f, 0.50f, 0.10f); // orange
                default:                            return Color.white;
            }
        }

        return Color.white;
    }
}
