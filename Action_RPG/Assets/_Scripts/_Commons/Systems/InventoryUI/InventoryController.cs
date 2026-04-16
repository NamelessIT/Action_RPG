using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý UI lưới 25 ô cố định.
/// • Khởi tạo đúng 25 InventorySlotView khi Awake.
/// • RefreshGrid(): cập nhật tất cả slot views, áp filter tab / search.
/// • Các tab button (Weapon/Shield/Artifact/Others) làm mờ ô không khớp thay vì ẩn.
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

    private InventoryItemRecord.InventoryItemType _currentTab
        = InventoryItemRecord.InventoryItemType.Weapon;

    private bool _filterActive = false;   // false = hiện tất cả (no tab selected)

    // ─────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_inventoryRuntime == null)
            _inventoryRuntime = FindFirstObjectByType<InventoryRuntime>();

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();

        // KHÔNG gọi BuildSlotViews() ở đây!
        // Unity không đảm bảo thứ tự Awake() giữa các GameObject.
        // InventoryRuntime._slots có thể chưa được khởi tạo → NullRef.
    }

    /// <summary>
    /// Start() được gọi SAU KHI tất cả Awake() đã hoàn thành.
    /// Lúc này InventoryRuntime._slots đã được khởi tạo an toàn.
    /// </summary>
    private void Start()
    {
        BuildSlotViews();
        RefreshGrid();   // lần đầu load đúng data
    }

    private void OnEnable()
    {
        if (_inventoryRuntime != null)
            _inventoryRuntime.OnInventoryChanged += RefreshGrid;

        if (_searchInput != null)
            _searchInput.onValueChanged.AddListener(OnSearchValueChanged);

        // RefreshGrid() an toàn: nếu _slotViews chưa build (trước Start) thì return sớm
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
    //  BUILD SLOT VIEWS  (gọi 1 lần trong Awake)
    // ─────────────────────────────────────────────────────────────

    private void BuildSlotViews()
    {
        if (_gridRoot == null || _slotViewPrefab == null)
        {
            Debug.LogWarning("[InventoryController] BuildSlotViews bị skip — " +
                             $"gridRoot:{_gridRoot != null}  prefab:{_slotViewPrefab != null}");
            return;
        }

        _slotViews = new InventorySlotView[InventoryRuntime.SLOT_COUNT];

        for (int i = 0; i < InventoryRuntime.SLOT_COUNT; i++)
        {
            InventorySlotView view = Instantiate(_slotViewPrefab, _gridRoot);
            view.Bind(i, _inventoryRuntime, this);
            _slotViews[i] = view;
        }

        Debug.Log($"[InventoryController] Đã tạo {InventoryRuntime.SLOT_COUNT} InventorySlotView.");
    }

    // ─────────────────────────────────────────────────────────────
    //  TAB BUTTONS
    // ─────────────────────────────────────────────────────────────

    public void ShowAllTab()
    {
        _filterActive = false;
        RefreshGrid();
    }

    public void ShowWeaponsTab()
    {
        _currentTab   = InventoryItemRecord.InventoryItemType.Weapon;
        _filterActive = true;
        RefreshGrid();
    }

    public void ShowShieldsTab()
    {
        _currentTab   = InventoryItemRecord.InventoryItemType.Shield;
        _filterActive = true;
        RefreshGrid();
    }

    public void ShowArtifactsTab()
    {
        _currentTab   = InventoryItemRecord.InventoryItemType.Artifact;
        _filterActive = true;
        RefreshGrid();
    }

    public void ShowOthersTab()
    {
        _currentTab   = InventoryItemRecord.InventoryItemType.Others;
        _filterActive = true;
        RefreshGrid();
    }

    /// <summary>Backward-compat setter.</summary>
    public void SetTab(InventoryItemRecord.InventoryItemType itemType)
    {
        _currentTab   = itemType;
        _filterActive = true;
        RefreshGrid();
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

            // Áp filter: ô trống hoặc item không khớp → làm mờ
            InventorySlot slot = _inventoryRuntime.GetSlot(i);
            bool matches = true;

            if (slot != null && !slot.IsEmpty)
            {
                if (_filterActive && !slot.Item.MatchesTab(_currentTab))
                    matches = false;

                if (!string.IsNullOrEmpty(search) && !slot.Item.MatchesSearch(search))
                    matches = false;
            }

            view.SetFilterMatch(matches);
        }
    }

    private void OnSearchValueChanged(string _) => RefreshGrid();

    // ─────────────────────────────────────────────────────────────
    //  INVENTORY OPERATIONS  (dùng bởi EquipmentSlotUI)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Trả item về inventory (unequip → thêm vào ô trống đầu tiên).</summary>
    public void ReturnItemToInventory(InventoryItemRecord item)
    {
        if (_inventoryRuntime == null || item == null) return;
        _inventoryRuntime.AddItem(item);
    }

    /// <summary>Xóa 1 lượng item theo reference (legacy — dùng khi kéo từ DraggableItem cũ).</summary>
    public bool RemoveItemFromInventory(InventoryItemRecord item)
    {
        return _inventoryRuntime != null && _inventoryRuntime.RemoveItem(item);
    }

    /// <summary>Xóa item tại ô cụ thể (dùng khi kéo từ InventorySlotView mới).</summary>
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
