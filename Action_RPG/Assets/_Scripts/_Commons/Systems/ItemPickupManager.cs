using UnityEngine;
using TMPro;

/// <summary>
/// Attach to the Player GameObject.
/// Each frame, scans for the nearest DroppedItemBehaviour within _pickupScanRadius
/// using Physics.OverlapSphere. Raises OnNearbyItemChanged whenever the nearest
/// item changes. Call TryPickupNearest() from an input handler to pick up.
/// </summary>
public class ItemPickupManager : MonoBehaviour
{
    [SerializeField] private float _pickupScanRadius = 2f;
    [SerializeField] private LayerMask _droppedItemLayer;
    [SerializeField] private InventoryRuntime _inventoryRuntime;

    /// <summary>Inject via Inspector. May be null — notification is optional.</summary>
    [SerializeField] private LootNotificationUI _notificationUI;

    [Header("Pickup Prompt UI")]
    [SerializeField] private GameObject _promptPanel;
    [SerializeField] private TextMeshProUGUI _promptItemNameLabel;

    private DroppedItemBehaviour _nearestItem;

    /// <summary>
    /// Fires whenever the nearest dropped item changes (including becoming null).
    /// </summary>
    public event System.Action<DroppedItemBehaviour> OnNearbyItemChanged;

    private void Awake()
    {
        if (_inventoryRuntime == null)
        {
            _inventoryRuntime = FindFirstObjectByType<InventoryRuntime>();
        }
    }

    private void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _pickupScanRadius, _droppedItemLayer);

        DroppedItemBehaviour nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            DroppedItemBehaviour candidate = hit.GetComponent<DroppedItemBehaviour>();
            if (candidate == null) continue;

            float dist = Vector3.Distance(transform.position, candidate.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = candidate;
            }
        }

        if (_nearestItem != nearest)
        {
            _nearestItem = nearest;

            // Toggle prompt UI
            if (_promptPanel != null)
            {
                bool hasItem = _nearestItem != null;
                _promptPanel.SetActive(hasItem);

                if (hasItem && _promptItemNameLabel != null)
                {
                    _promptItemNameLabel.text = _nearestItem.Entry.displayName;
                }
            }

            OnNearbyItemChanged?.Invoke(_nearestItem);
        }
    }

    /// <summary>
    /// Attempts to pick up the nearest detected item and add it to the inventory.
    /// Does nothing if no item is in range.
    /// </summary>
    public void TryPickupNearest()
    {
        if (_nearestItem == null) return;

        LootEntry entry = _nearestItem.Entry;
        InventoryItemRecord record = null;

        if (entry.weaponData != null)
        {
            record = InventoryItemRecord.FromWeapon(entry.weaponData);
        }
        else if (entry.accessoryData != null)
        {
            record = InventoryItemRecord.FromAccessory(entry.accessoryData);
        }
        else
        {
            // Fallback: entry has no weapon or accessory data reference.
            // InventoryItemRecord fields are private; no public factory exists for
            // a display-name-only record. Log and discard the item.
            Debug.LogWarning($"[ItemPickupManager] No weapon/accessory data for '{entry.displayName}'. Item discarded.");
            Destroy(_nearestItem.gameObject);
            _nearestItem = null;
            return;
        }

        _inventoryRuntime.AddItem(record);

        if (_notificationUI != null)
        {
            _notificationUI.ShowPickup(entry.displayName, entry.quantity);
        }

        Destroy(_nearestItem.gameObject);
        _nearestItem = null;
    }
}
