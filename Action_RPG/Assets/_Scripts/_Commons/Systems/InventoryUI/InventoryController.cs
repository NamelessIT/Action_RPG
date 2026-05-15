using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý UI lưới 25 ô cố định.
/// Mỗi tab (Weapon/Shield/Artifact/Others) hiển thị 1 trang riêng biệt.
/// Trang Weapon = slot 0–24, Shield = 25–49, Artifact = 50–74, Others = 75–99.
/// Chuyển tab = rebind 25 views sang trang tương ứng → mỗi tab có layout slot độc lập.
/// Tab "All" hiển thị trang Weapon mặc định (hoặc trang cuối cùng đang xem).
/// </summary>
public class InventoryController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────

    [Header("Data")]
    [SerializeField] private InventoryRuntime _inventoryRuntime;

    [Header("UI — 25-slot grid")]
    [SerializeField] private RectTransform    _gridRoot;
    [SerializeField] private InventorySlotView _slotViewPrefab;   // prefab có InventorySlotView
    [SerializeField] private Canvas            _rootCanvas;
    [SerializeField] private TMP_InputField    _searchInput;

    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────

    private InventorySlotView[] _slotViews;   // luôn đúng 25 phần tử

    /// <summary>Offset trang hiện tại (0 / 25 / 50 / 75). -1 = All tab.</summary>
    private int _currentPageOffset = 0;

    /// <summary>True khi đang filter theo tab cụ thể, false khi All tab.</summary>
    private bool _filterActive = false;

    private InventoryItemRecord.InventoryItemType _currentTab
        = InventoryItemRecord.InventoryItemType.Weapon;

    // ─────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_inventoryRuntime == null)
            _inventoryRuntime = FindFirstObjectByType<InventoryRuntime>();

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        BuildSlotViews();
        // Mặc định hiển thị trang Weapon
        ShowWeaponsTab();
    }

    private void OnEnable()
    {
        if (_inventoryRuntime != null)
            _inventoryRuntime.OnInventoryChanged += RefreshGrid;

        if (_searchInput != null)
            _searchInput.onValueChanged.AddListener(OnSearchValueChanged);

        RefreshGrid();
    }

    private void OnDisable()
    {
        if (_inventoryRuntime != null)
            _inventoryRuntime.OnInventoryChanged -= RefreshGrid;

        if (_searchInput != null)
            _searchInput.onValueChanged.RemoveListener(OnSearchValueChanged);
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD SLOT VIEWS  (gọi 1 lần trong Start)
    // ─────────────────────────────────────────────────────────────

    private void BuildSlotViews()
    {
        if (_gridRoot == null || _slotViewPrefab == null)
        {
            Debug.LogWarning("[InventoryController] BuildSlotViews bị skip — " +
                             $"gridRoot:{_gridRoot != null}  prefab:{_slotViewPrefab != null}");
            return;
        }

        _slotViews = new InventorySlotView[InventoryRuntime.SLOTS_PER_PAGE];

        for (int i = 0; i < InventoryRuntime.SLOTS_PER_PAGE; i++)
        {
            InventorySlotView view = Instantiate(_slotViewPrefab, _gridRoot);
            // Bind lần đầu vào trang Weapon (offset 0)
            view.Bind(i, _inventoryRuntime, this);
            _slotViews[i] = view;
        }

        Debug.Log($"[InventoryController] Đã tạo {InventoryRuntime.SLOTS_PER_PAGE} InventorySlotView.");
    }

    // ─────────────────────────────────────────────────────────────
    //  TAB BUTTONS — chuyển trang
    // ─────────────────────────────────────────────────────────────

    public void ShowAllTab()
    {
        // All tab: hiển thị trang Weapon mặc định
        _filterActive = false;
        _currentTab = InventoryItemRecord.InventoryItemType.Weapon;
        _currentPageOffset = 0;
        RebindViewsToPage(0);
    }

    public void ShowWeaponsTab()
    {
        _currentTab = InventoryItemRecord.InventoryItemType.Weapon;
        _filterActive = true;
        _currentPageOffset = InventoryRuntime.GetPageOffset(_currentTab);
        RebindViewsToPage(_currentPageOffset);
    }

    public void ShowShieldsTab()
    {
        _currentTab = InventoryItemRecord.InventoryItemType.Shield;
        _filterActive = true;
        _currentPageOffset = InventoryRuntime.GetPageOffset(_currentTab);
        RebindViewsToPage(_currentPageOffset);
    }

    public void ShowArtifactsTab()
    {
        _currentTab = InventoryItemRecord.InventoryItemType.Artifact;
        _filterActive = true;
        _currentPageOffset = InventoryRuntime.GetPageOffset(_currentTab);
        RebindViewsToPage(_currentPageOffset);
    }

    public void ShowOthersTab()
    {
        _currentTab = InventoryItemRecord.InventoryItemType.Others;
        _filterActive = true;
        _currentPageOffset = InventoryRuntime.GetPageOffset(_currentTab);
        RebindViewsToPage(_currentPageOffset);
    }

    /// <summary>Backward-compat setter.</summary>
    public void SetTab(InventoryItemRecord.InventoryItemType itemType)
    {
        _currentTab = itemType;
        _filterActive = true;
        _currentPageOffset = InventoryRuntime.GetPageOffset(itemType);
        RebindViewsToPage(_currentPageOffset);
    }

    // ─────────────────────────────────────────────────────────────
    //  REBIND — gán lại 25 views sang trang mới
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebind 25 InventorySlotView sang trang mới.
    /// View[0] → slot[pageOffset + 0], View[1] → slot[pageOffset + 1], ...
    /// </summary>
    private void RebindViewsToPage(int pageOffset)
    {
        if (_slotViews == null || _inventoryRuntime == null) return;

        for (int i = 0; i < _slotViews.Length; i++)
        {
            if (_slotViews[i] == null) continue;
            _slotViews[i].Bind(pageOffset + i, _inventoryRuntime, this);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  REFRESH
    // ─────────────────────────────────────────────────────────────

    public void RefreshGrid()
    {
        if (_slotViews == null)
        {
            Debug.LogWarning("[InventoryController] RefreshGrid bị skip — _slotViews chưa được khởi tạo.");
            return;
        }

        if (_inventoryRuntime == null)
        {
            Debug.LogWarning("[InventoryController] RefreshGrid bị skip — _inventoryRuntime null.");
            return;
        }

        string search = _searchInput != null ? _searchInput.text : string.Empty;

        for (int i = 0; i < _slotViews.Length; i++)
        {
            InventorySlotView view = _slotViews[i];
            if (view == null) continue;

            // Cập nhật visual nội dung ô
            view.Refresh();

            // Search filter (nếu có gõ tìm kiếm)
            if (!string.IsNullOrEmpty(search))
            {
                InventorySlot slot = _inventoryRuntime.GetSlot(_currentPageOffset + i);
                bool matches = true;

                if (slot != null && !slot.IsEmpty)
                {
                    if (!slot.Item.MatchesSearch(search))
                        matches = false;
                }

                view.SetFilterMatch(matches);
            }
            // Không cần filter tab nữa — mỗi trang CHỈ chứa item đúng loại
        }
    }

    private void OnSearchValueChanged(string _) => RefreshGrid();

    // ─────────────────────────────────────────────────────────────
    //  INVENTORY OPERATIONS  (dùng bởi EquipmentSlotUI)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Trả item về inventory (unequip → thêm vào trang đúng loại).</summary>
    public void ReturnItemToInventory(InventoryItemRecord item)
    {
        if (_inventoryRuntime == null || item == null) return;
        _inventoryRuntime.AddItem(item);  // Tự vào đúng trang
    }

    /// <summary>Xóa 1 lượng item theo reference (legacy).</summary>
    public bool RemoveItemFromInventory(InventoryItemRecord item)
    {
        return _inventoryRuntime != null && _inventoryRuntime.RemoveItem(item);
    }

    /// <summary>Xóa item tại ô cụ thể.</summary>
    public bool RemoveFromSlot(int slotIndex, int quantity = 1)
    {
        return _inventoryRuntime != null && _inventoryRuntime.RemoveFromSlot(slotIndex, quantity);
    }

    // ─────────────────────────────────────────────────────────────
    //  UTILITIES
    // ─────────────────────────────────────────────────────────────

    public Canvas GetRootCanvas() => _rootCanvas;

    public InventoryRuntime GetRuntime() => _inventoryRuntime;
}
