using UnityEngine;
using TMPro;

/// <summary>
/// Attach to the Player GameObject.
/// Mỗi frame scan DroppedItemBehaviour gần nhất trong _pickupScanRadius.
/// Gọi TryPickupNearest() từ input handler để nhặt.
///
/// Bug fix: Unity override == operator cho MonoBehaviour đã bị Destroy() trả về true khi so sánh với null.
/// Dùng System.Object.ReferenceEquals để phân biệt C# null thật vs Unity-null (destroyed).
/// </summary>
public class ItemPickupManager : MonoBehaviour
{
    [SerializeField] private float _pickupScanRadius = 2f;
    [SerializeField] private LayerMask _droppedItemLayer;
    [SerializeField] private InventoryRuntime _inventoryRuntime;

    [SerializeField] private LootNotificationUI _notificationUI;

    [Header("Pickup Prompt UI")]
    [SerializeField] private GameObject _promptPanel;
    [SerializeField] private TextMeshProUGUI _promptItemNameLabel;

    private DroppedItemBehaviour _nearestItem;  // C# null hoặc object còn sống

    public event System.Action<DroppedItemBehaviour> OnNearbyItemChanged;

    private void Awake()
    {
        if (_inventoryRuntime == null)
            _inventoryRuntime = InventoryRuntime.Current;
    }

    private void Update()
    {
        DroppedItemBehaviour nearest = FindNearest();

        // ── Quan trọng: dùng ReferenceEquals thay vì != ────────────────────
        // Unity override != sẽ coi destroyed MonoBehaviour == null (true),
        // nên nếu _nearestItem đang trỏ tới object đã bị Destroy() và nearest là null thật,
        // điều kiện _nearestItem != nearest = false → panel không bao giờ được ẩn.
        // ReferenceEquals không bị override → phân biệt đúng.
        if (!System.Object.ReferenceEquals(_nearestItem, nearest))
        {
            _nearestItem = nearest;

            bool hasItem = _nearestItem != null;   // Unity null check — ok để check trạng thái

            if (_promptPanel != null)
                _promptPanel.SetActive(hasItem);

            if (_promptItemNameLabel != null)
                _promptItemNameLabel.text = hasItem ? _nearestItem.Entry.displayName : string.Empty;

            OnNearbyItemChanged?.Invoke(_nearestItem);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PICKUP
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Nhặt item gần nhất. Ẩn prompt ngay từ đầu để tránh race condition.
    /// </summary>
    public void TryPickupNearest()
    {
        if (_nearestItem == null) return;

        // Lưu reference trước khi clear, tránh double-pickup
        DroppedItemBehaviour toPickup = _nearestItem;
        _nearestItem = null;

        // Ẩn prompt NGAY — không chờ Update() của frame sau
        HidePrompt();
        OnNearbyItemChanged?.Invoke(null);

        // ── Tạo record ────────────────────────────────────────────
        LootEntry entry = toPickup.Entry;
        InventoryItemRecord record = null;

        if (entry.weaponData != null)
            record = InventoryItemRecord.FromWeapon(entry.weaponData);
        else if (entry.accessoryData != null)
            record = InventoryItemRecord.FromAccessory(entry.accessoryData);

        if (record == null)
        {
            Debug.LogWarning($"[ItemPickupManager] Không có weaponData/accessoryData cho '{entry.displayName}'. Item bị bỏ qua.");
            Destroy(toPickup.gameObject);
            return;
        }

        // ── Thêm vào inventory ────────────────────────────────────
        // [FIX MẤT ITEM] Chỉ Destroy khỏi world khi add THÀNH CÔNG. Nếu trang đầy (AddItem=false)
        // mà vẫn Destroy thì item biến mất vĩnh viễn → giữ lại world để player dọn túi rồi nhặt lại.
        if (!_inventoryRuntime.AddItem(record))
        {
            Debug.LogWarning($"[ItemPickupManager] Túi (trang {record.ItemType}) đầy — KHÔNG nhặt được '{record.DisplayName}'. Giữ item dưới đất.");
            return;
        }
        Debug.Log($"[ItemPickupManager] ✅ Nhặt '{record.DisplayName}' (type: {record.ItemType}).");

        // ── Notification ──────────────────────────────────────────
        if (_notificationUI != null)
            _notificationUI.ShowPickup(entry.displayName, entry.quantity);

        // ── Xóa khỏi world ───────────────────────────────────────
        Destroy(toPickup.gameObject);
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private DroppedItemBehaviour FindNearest()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _pickupScanRadius, _droppedItemLayer);

        DroppedItemBehaviour nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            // GetComponent trả về null nếu object đã bị Destroy() → skip
            DroppedItemBehaviour candidate = hit.GetComponent<DroppedItemBehaviour>();
            if (candidate == null) continue;

            float dist = Vector3.Distance(transform.position, candidate.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest     = candidate;
            }
        }

        return nearest;
    }

    private void HidePrompt()
    {
        if (_promptPanel != null)      _promptPanel.SetActive(false);
        if (_promptItemNameLabel != null) _promptItemNameLabel.text = string.Empty;
    }
}
