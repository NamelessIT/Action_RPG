using UnityEngine;

public class InventoryUIManager : MonoBehaviour
{
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private KeyCode _toggleKey = KeyCode.B;

    public static bool IsInventoryOpen { get; private set; }

    private void Awake()
    {
        SetInventoryVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            ToggleInventory();
        }
    }

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