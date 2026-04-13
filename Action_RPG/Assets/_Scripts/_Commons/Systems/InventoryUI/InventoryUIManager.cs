using UnityEngine;

public class InventoryUIManager : MonoBehaviour
{
    [SerializeField] private GameObject _inventoryPanel;
#pragma warning disable 0414
    [SerializeField] private KeyCode _toggleKey = KeyCode.B;
#pragma warning restore 0414

    public static bool IsInventoryOpen { get; private set; }

    private void Awake()
    {
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

        Time.timeScale = isVisible ? 0f : 1f;
        Cursor.visible = isVisible;
        Cursor.lockState = isVisible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}