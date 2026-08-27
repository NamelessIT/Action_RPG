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

    private InventorySlotView[] _slotViews;   // đúng 100 phần tử (TOTAL_SLOTS), bind cố định slot 0-99

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
            _inventoryRuntime = InventoryRuntime.Current;

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        BuildSlotViews();
        // Mặc định hiển thị trang Weapon
        ShowWeaponsTab();

        // Khoác tông arcane cho toàn bộ control mặc định trong panel (nút tab, dropdown,
        // thanh cuộn, chữ). Chạy sau BuildSlotViews để các ô vừa sinh cũng được tính.
        Systems.ArcaneUISkin.Apply(gameObject);
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

        // Tạo đủ 100 view (TOTAL_SLOTS), bind CỐ ĐỊNH vào slot 0-99.
        // Tab cụ thể → chỉ hiện 25 ô của trang đó; tab All → hiện cả 100.
        _slotViews = new InventorySlotView[InventoryRuntime.TOTAL_SLOTS];

        for (int i = 0; i < InventoryRuntime.TOTAL_SLOTS; i++)
        {
            InventorySlotView view = Instantiate(_slotViewPrefab, _gridRoot);
            view.Bind(i, _inventoryRuntime, this); // index tuyệt đối, không đổi nữa
            _slotViews[i] = view;
        }

        Debug.Log($"[InventoryController] Đã tạo {InventoryRuntime.TOTAL_SLOTS} InventorySlotView (cố định slot 0-99).");
    }

    /// <summary>
    /// Hiện/ẩn các view theo trang. pageOffset = -1 → hiện toàn bộ 100 ô (tab All).
    /// Ngược lại chỉ hiện 25 ô [pageOffset .. pageOffset+24].
    /// </summary>
    private void ApplyPageVisibility(int pageOffset)
    {
        if (_slotViews == null) return;

        bool showAll = pageOffset < 0;
        for (int i = 0; i < _slotViews.Length; i++)
        {
            if (_slotViews[i] == null) continue;
            bool visible = showAll || (i >= pageOffset && i < pageOffset + InventoryRuntime.SLOTS_PER_PAGE);
            if (_slotViews[i].gameObject.activeSelf != visible)
                _slotViews[i].gameObject.SetActive(visible);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  TAB BUTTONS — chuyển trang
    // ─────────────────────────────────────────────────────────────

    public void ShowAllTab()
    {
        // All tab: hiển thị TOÀN BỘ 100 ô (4 trang × 25)
        _filterActive = false;
        _currentTab = InventoryItemRecord.InventoryItemType.Weapon;
        _currentPageOffset = -1; // -1 = All
        ApplyPageVisibility(-1);
        RefreshGrid();
    }

    public void ShowWeaponsTab()  => ShowTabInternal(InventoryItemRecord.InventoryItemType.Weapon);
    public void ShowShieldsTab()  => ShowTabInternal(InventoryItemRecord.InventoryItemType.Shield);
    public void ShowArtifactsTab() => ShowTabInternal(InventoryItemRecord.InventoryItemType.Artifact);
    public void ShowOthersTab()   => ShowTabInternal(InventoryItemRecord.InventoryItemType.Others);

    /// <summary>Backward-compat setter.</summary>
    public void SetTab(InventoryItemRecord.InventoryItemType itemType) => ShowTabInternal(itemType);

    private void ShowTabInternal(InventoryItemRecord.InventoryItemType itemType)
    {
        _currentTab = itemType;
        _filterActive = true;
        _currentPageOffset = InventoryRuntime.GetPageOffset(itemType);
        ApplyPageVisibility(_currentPageOffset);
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

            // View bind cố định slot tuyệt đối = chỉ số mảng (0-99)
            if (!view.gameObject.activeSelf) continue; // ô đang ẩn (khác trang) → bỏ qua

            // Cập nhật visual nội dung ô
            view.Refresh();

            // Search filter (nếu có gõ tìm kiếm)
            if (!string.IsNullOrEmpty(search))
            {
                InventorySlot slot = _inventoryRuntime.GetSlot(view.SlotIndex);
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
    /// <returns>true nếu thêm thành công; false nếu trang đầy (item KHÔNG được thêm).</returns>
    public bool ReturnItemToInventory(InventoryItemRecord item)
    {
        if (_inventoryRuntime == null || item == null) return false;
        return _inventoryRuntime.AddItem(item);  // Tự vào đúng trang
    }

    /// <summary>True nếu túi còn chỗ cho item (trang đúng loại). Dùng để check trước khi unequip.</summary>
    public bool CanAccept(InventoryItemRecord item)
        => _inventoryRuntime != null && _inventoryRuntime.HasRoomFor(item);

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
