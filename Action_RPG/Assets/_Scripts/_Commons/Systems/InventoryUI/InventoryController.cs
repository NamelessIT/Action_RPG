using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private InventoryRuntime _inventoryRuntime;

    [Header("UI")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _gridRoot;
    [SerializeField] private DraggableItem _itemViewPrefab;
    [SerializeField] private TMP_InputField _searchInput;
    [SerializeField] private Canvas _rootCanvas;

    private readonly List<DraggableItem> _pooledViews = new List<DraggableItem>();
    private readonly List<InventoryItemRecord> _filteredItems = new List<InventoryItemRecord>();

    private InventoryItemRecord.InventoryItemType _currentTab = InventoryItemRecord.InventoryItemType.Weapon;

    private void Awake()
    {
        if (_inventoryRuntime == null)
        {
            _inventoryRuntime = FindFirstObjectByType<InventoryRuntime>();
        }

        if (_rootCanvas == null)
        {
            _rootCanvas = GetComponentInParent<Canvas>();
        }
    }

    private void OnEnable()
    {
        if (_inventoryRuntime != null)
        {
            _inventoryRuntime.OnInventoryChanged += RefreshGrid;
        }

        if (_searchInput != null)
        {
            _searchInput.onValueChanged.AddListener(OnSearchValueChanged);
        }

        RefreshGrid();
    }

    private void OnDisable()
    {
        if (_inventoryRuntime != null)
        {
            _inventoryRuntime.OnInventoryChanged -= RefreshGrid;
        }

        if (_searchInput != null)
        {
            _searchInput.onValueChanged.RemoveListener(OnSearchValueChanged);
        }
    }

    public Canvas GetRootCanvas()
    {
        return _rootCanvas;
    }

    public void ShowWeaponsTab()
    {
        SetTab(InventoryItemRecord.InventoryItemType.Weapon);
    }

    public void ShowShieldsTab()
    {
        SetTab(InventoryItemRecord.InventoryItemType.Shield);
    }

    public void ShowArtifactsTab()
    {
        SetTab(InventoryItemRecord.InventoryItemType.Artifact);
    }

    public void ShowOthersTab()
    {
        SetTab(InventoryItemRecord.InventoryItemType.Others);
    }

    public void SetTab(InventoryItemRecord.InventoryItemType itemType)
    {
        _currentTab = itemType;
        RefreshGrid();
    }

    public void ReturnItemToInventory(InventoryItemRecord item)
    {
        if (_inventoryRuntime == null || item == null)
        {
            return;
        }

        _inventoryRuntime.AddItem(item);
    }

    public bool RemoveItemFromInventory(InventoryItemRecord item)
    {
        return _inventoryRuntime != null && _inventoryRuntime.RemoveItem(item);
    }

    public void RefreshGrid()
    {
        if (_inventoryRuntime == null || _gridRoot == null || _itemViewPrefab == null)
        {
            return;
        }

        BuildFilteredItems();

        for (int index = 0; index < _filteredItems.Count; index++)
        {
            DraggableItem itemView = GetOrCreateView(index);
            itemView.gameObject.SetActive(true);
            itemView.Bind(_filteredItems[index], this);
        }

        for (int index = _filteredItems.Count; index < _pooledViews.Count; index++)
        {
            _pooledViews[index].gameObject.SetActive(false);
        }

        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void OnSearchValueChanged(string _)
    {
        RefreshGrid();
    }

    private void BuildFilteredItems()
    {
        _filteredItems.Clear();
        string currentSearch = _searchInput != null ? _searchInput.text : string.Empty;
        IReadOnlyList<InventoryItemRecord> items = _inventoryRuntime.Items;

        for (int index = 0; index < items.Count; index++)
        {
            InventoryItemRecord item = items[index];
            if (item == null)
            {
                continue;
            }

            if (!item.MatchesTab(_currentTab))
            {
                continue;
            }

            if (!item.MatchesSearch(currentSearch))
            {
                continue;
            }

            _filteredItems.Add(item);
        }
    }

    private DraggableItem GetOrCreateView(int index)
    {
        while (_pooledViews.Count <= index)
        {
            DraggableItem itemView = Instantiate(_itemViewPrefab, _gridRoot);
            itemView.gameObject.SetActive(false);
            _pooledViews.Add(itemView);
        }

        return _pooledViews[index];
    }
}