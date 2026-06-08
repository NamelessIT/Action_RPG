using UnityEngine;

public class InventoryUIManager : MonoBehaviour
{
    [SerializeField] private GameObject _inventoryPanel;
    [Tooltip("Tùy chọn — panel chi tiết chỉ số. Tự đóng theo khi đóng Inventory. Bỏ trống sẽ tự tìm trong scene.")]
    [SerializeField] private StatDetailUI _statDetailUI;
#pragma warning disable 0414
    [SerializeField] private KeyCode _toggleKey = KeyCode.B;
#pragma warning restore 0414

    public static bool IsInventoryOpen { get; private set; }

    private void Awake()
    {
        if (_statDetailUI == null)
            _statDetailUI = FindFirstObjectByType<StatDetailUI>(FindObjectsInactive.Include);

        SetInventoryVisible(false);
    }

    // Toggle key (B) is handled by PlayerController.Update() to ensure
    // it fires before the IsInventoryOpen early-return guard.

    public void ToggleInventory()
    {
        SetInventoryVisible(!IsInventoryOpen);
    }

    public void SetInventoryVisible(bool isVisible)
    {
        IsInventoryOpen = isVisible;

        if (_inventoryPanel != null)
        {
            _inventoryPanel.SetActive(isVisible);
        }

        // Đóng Inventory → đóng luôn StatDetail (nó là panel riêng, không phải con của Inventory
        // nên không tự tắt theo). Tránh tình trạng StatDetailPanel lơ lửng sau khi bấm B.
        if (!isVisible && _statDetailUI != null)
            _statDetailUI.Hide();

        // Pause/cursor do UIPauseManager quản lý tập trung (tránh panel này unpause khi panel khác còn mở)
        UIPauseManager.SetLock("Inventory", isVisible);
    }
}