using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays loot pickup notifications in a stacked, auto-expiring list.
/// Notifications for the same item stack quantity and reset the timer.
/// Rows alpha-fade out after <see cref="_displayDuration"/> seconds.
/// </summary>
public class LootNotificationUI : MonoBehaviour
{
    [SerializeField] private Transform _rowContainer;
    [SerializeField] private TextMeshProUGUI _rowPrefab;
    [SerializeField] private int _maxRows = 5;
    [SerializeField] private float _displayDuration = 5f;
    [SerializeField] private float _fadeSpeed = 2f;

    private List<NotificationEntry> _entries;

    private class NotificationEntry
    {
        public string itemName;
        public int quantity;
        public float expiryTime;   // Time.time + _displayDuration
        public TextMeshProUGUI label;
    }

    private void Awake()
    {
        _entries = new List<NotificationEntry>();

        if (_rowContainer != null)
        {
            for (int i = _rowContainer.childCount - 1; i >= 0; i--)
            {
                _rowContainer.GetChild(i).gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // Iterate backwards so RemoveAt is safe
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            NotificationEntry entry = _entries[i];

            if (Time.time >= entry.expiryTime)
            {
                Color c = entry.label.color;
                c.a = Mathf.MoveTowards(c.a, 0f, _fadeSpeed * Time.deltaTime);
                entry.label.color = c;

                if (c.a <= 0f)
                {
                    Destroy(entry.label.gameObject);
                    _entries.RemoveAt(i);
                }
            }
        }

        // Ẩn panel khi không còn notification nào
        if (_entries.Count == 0 && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Displays a pickup notification. Stacks quantity if the same item is already visible.
    /// </summary>
    /// <param name="itemName">Display name of the picked-up item.</param>
    /// <param name="quantity">Amount picked up (default 1).</param>
    public void ShowPickup(string itemName, int quantity = 1)
    {
        gameObject.SetActive(true); // Đảm bảo panel hiện khi có notification

        // Check for existing entry with the same item name
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].itemName == itemName)
            {
                _entries[i].quantity += quantity;
                _entries[i].expiryTime = Time.time + _displayDuration;

                // Reset alpha in case the row was already fading
                Color c = _entries[i].label.color;
                c.a = 1f;
                _entries[i].label.color = c;

                _entries[i].label.text = $"x{_entries[i].quantity} {_entries[i].itemName}";
                return;
            }
        }

        // No existing entry — remove oldest row if at capacity
        if (_entries.Count >= _maxRows)
        {
            Destroy(_entries[0].label.gameObject);
            _entries.RemoveAt(0);
        }

        // Instantiate new row
        TextMeshProUGUI newLabel = Instantiate(_rowPrefab, _rowContainer);
        Color labelColor = newLabel.color;
        labelColor.a = 1f;
        newLabel.color = labelColor;
        newLabel.text = $"x{quantity} {itemName}";

        _entries.Add(new NotificationEntry
        {
            itemName = itemName,
            quantity = quantity,
            expiryTime = Time.time + _displayDuration,
            label = newLabel
        });
    }
}
