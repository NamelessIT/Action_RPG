using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Controller chính cho Skill Tree UI.
/// - Mở/đóng bằng phím C (xử lý ở PlayerController, gọi Toggle từ đây)
/// - Hiển thị skill tree theo class hiện tại
/// - Hỗ trợ dual-class: tab chuyển giữa 2 class + tab Fusion
/// - Quản lý spawn/despawn SkillNodeUI
/// </summary>
public class SkillTreeController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────

    [Header("Data")]
    [SerializeField] private SkillTreeDatabase _database;

    [Header("UI — Panel")]
    [SerializeField] private GameObject _skillTreePanel;         // Root panel (bật/tắt)
    [SerializeField] private RectTransform _nodeContainer;       // Container spawn các SkillNodeUI
    [SerializeField] private SkillNodeUI _skillNodePrefab;       // Prefab 1 node

    [Header("UI — Header")]
    [SerializeField] private TextMeshProUGUI _classNameText;     // Tên class đang xem
    [SerializeField] private TextMeshProUGUI _skillPointText;    // "SP: 5"
    [SerializeField] private Image _classIcon;                   // Icon class

    [Header("UI — Class Tabs")]
    [SerializeField] private Button _classTab1Button;            // Tab class chính
    [SerializeField] private Button _classTab2Button;            // Tab class phụ (ẩn nếu single class)
    [SerializeField] private Button _fusionTabButton;            // Tab fusion (ẩn nếu single class)
    [SerializeField] private TextMeshProUGUI _classTab1Text;
    [SerializeField] private TextMeshProUGUI _classTab2Text;

    [Header("UI — Info Panel (Skill Detail)")]
    [SerializeField] private GameObject _detailPanel;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TextMeshProUGUI _detailName;
    [SerializeField] private TextMeshProUGUI _detailDesc;
    [SerializeField] private TextMeshProUGUI _detailType;

    [Header("UI — Equipped Slots Display")]
    [SerializeField] private Image _equippedSkillIcon;           // Icon skill đang equip (slot E)
    [SerializeField] private Image _equippedSignatureIcon;       // Icon signature đang equip (slot R)
    [SerializeField] private TextMeshProUGUI _equippedSkillName;
    [SerializeField] private TextMeshProUGUI _equippedSignatureName;

    [Header("UI — Refund")]
    [SerializeField] private Button _refundButton;

    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────

    public static bool IsSkillTreeOpen { get; private set; }

    private SkillTreeRuntime _runtime;
    private PlayerStats _playerStats;

    private List<SkillNodeUI> _spawnedNodes = new List<SkillNodeUI>();
    private ClassSkillTreeData _currentViewingTree;

    // Dual class support
    private string _primaryClass = "";
    private string _secondaryClass = "";  // Rỗng nếu chưa mở khóa class 2

    private enum ViewTab { Primary, Secondary, Fusion }
    private ViewTab _currentTab = ViewTab.Primary;

    // ─────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        SetPanelVisible(false);
    }

    private void Start()
    {
        // Tìm runtime trên Player
        _runtime = FindFirstObjectByType<SkillTreeRuntime>();
        _playerStats = FindFirstObjectByType<PlayerStats>();

        if (_runtime == null)
            Debug.LogWarning("[SkillTreeController] Không tìm thấy SkillTreeRuntime trên scene!");

        // Setup button listeners
        if (_classTab1Button != null)
            _classTab1Button.onClick.AddListener(() => SwitchTab(ViewTab.Primary));
        if (_classTab2Button != null)
            _classTab2Button.onClick.AddListener(() => SwitchTab(ViewTab.Secondary));
        if (_fusionTabButton != null)
            _fusionTabButton.onClick.AddListener(() => SwitchTab(ViewTab.Fusion));
        if (_refundButton != null)
            _refundButton.onClick.AddListener(OnRefundClicked);
    }

    private void OnEnable()
    {
        if (_runtime != null)
            _runtime.OnSkillTreeChanged += RefreshHeader;
    }

    private void OnDisable()
    {
        if (_runtime != null)
            _runtime.OnSkillTreeChanged -= RefreshHeader;
    }

    // ─────────────────────────────────────────────────────────────
    //  TOGGLE (gọi từ PlayerController khi bấm C)
    // ─────────────────────────────────────────────────────────────

    public void ToggleSkillTree()
    {
        SetPanelVisible(!IsSkillTreeOpen);
    }

    public void SetPanelVisible(bool isVisible)
    {
        IsSkillTreeOpen = isVisible;

        if (_skillTreePanel != null)
            _skillTreePanel.SetActive(isVisible);

        Time.timeScale = isVisible ? 0f : 1f;
        Cursor.visible = isVisible;
        Cursor.lockState = isVisible ? CursorLockMode.None : CursorLockMode.Locked;

        if (isVisible)
        {
            // Refresh khi mở
            DetectClasses();
            BuildTree();
            RefreshHeader();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  CLASS DETECTION
    // ─────────────────────────────────────────────────────────────

    /// <summary>Xác định class chính/phụ từ AllyStats.</summary>
    private void DetectClasses()
    {
        if (_runtime == null) return;

        _primaryClass = _runtime.CurrentClassName;
        // TODO: Khi implement dual-class, lấy secondaryClass từ AllyStats
        _secondaryClass = ""; // Chưa có dual-class

        // Cập nhật tab visibility
        bool hasDualClass = !string.IsNullOrEmpty(_secondaryClass);

        if (_classTab1Text != null) _classTab1Text.text = _primaryClass;
        if (_classTab2Text != null) _classTab2Text.text = _secondaryClass;

        if (_classTab2Button != null) _classTab2Button.gameObject.SetActive(hasDualClass);
        if (_fusionTabButton != null) _fusionTabButton.gameObject.SetActive(hasDualClass);

        // Default về tab Primary
        _currentTab = ViewTab.Primary;
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD TREE
    // ─────────────────────────────────────────────────────────────

    /// <summary>Spawn các SkillNodeUI theo class đang xem.</summary>
    private void BuildTree()
    {
        ClearNodes();

        if (_database == null || _runtime == null) return;

        ClassSkillTreeData treeData = null;

        switch (_currentTab)
        {
            case ViewTab.Primary:
                treeData = _database.GetTreeByClassName(_primaryClass);
                break;
            case ViewTab.Secondary:
                treeData = _database.GetTreeByClassName(_secondaryClass);
                break;
            case ViewTab.Fusion:
                BuildFusionTree();
                return;
        }

        if (treeData == null)
        {
            Debug.LogWarning($"[SkillTreeController] Không tìm thấy tree cho class '{_primaryClass}'");
            return;
        }

        _currentViewingTree = treeData;

        // Cập nhật header
        if (_classNameText != null) _classNameText.text = treeData.className;
        if (_classIcon != null && treeData.classIcon != null)
        {
            _classIcon.sprite = treeData.classIcon;
            _classIcon.enabled = true;
        }

        // Spawn nodes
        foreach (var nodeData in treeData.skillNodes)
        {
            if (nodeData.skillData == null) continue;

            SkillNodeUI nodeUI = Instantiate(_skillNodePrefab, _nodeContainer);

            // Đặt vị trí theo uiPosition (designer config trong Inspector)
            RectTransform rect = nodeUI.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = nodeData.uiPosition;

            nodeUI.Bind(nodeData, _runtime, this);
            _spawnedNodes.Add(nodeUI);
        }
    }

    /// <summary>Build fusion skill tree (dual-class).</summary>
    private void BuildFusionTree()
    {
        if (_database == null || _database.fusionSkills == null) return;

        foreach (var fusion in _database.fusionSkills)
        {
            // Tìm fusion phù hợp với 2 class hiện tại
            bool match = (fusion.classA == _primaryClass && fusion.classB == _secondaryClass)
                      || (fusion.classA == _secondaryClass && fusion.classB == _primaryClass);

            if (!match) continue;

            if (_classNameText != null)
                _classNameText.text = $"Fusion: {fusion.classA} + {fusion.classB}";

            foreach (var nodeData in fusion.fusionNodes)
            {
                if (nodeData.skillData == null) continue;

                SkillNodeUI nodeUI = Instantiate(_skillNodePrefab, _nodeContainer);
                RectTransform rect = nodeUI.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = nodeData.uiPosition;

                nodeUI.Bind(nodeData, _runtime, this);
                _spawnedNodes.Add(nodeUI);
            }
            break; // Chỉ 1 fusion set per 2 classes
        }
    }

    /// <summary>Hủy tất cả node UI đang hiển thị.</summary>
    private void ClearNodes()
    {
        foreach (var node in _spawnedNodes)
        {
            if (node != null) Destroy(node.gameObject);
        }
        _spawnedNodes.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    //  REFRESH
    // ─────────────────────────────────────────────────────────────

    /// <summary>Refresh tất cả node (gọi sau khi unlock/equip).</summary>
    public void RefreshAllNodes()
    {
        foreach (var node in _spawnedNodes)
        {
            if (node != null) node.Refresh();
        }
        RefreshHeader();
        RefreshEquippedDisplay();
    }

    /// <summary>Cập nhật header (SP còn lại).</summary>
    private void RefreshHeader()
    {
        if (_skillPointText != null && _runtime != null)
            _skillPointText.text = $"SP: {_runtime.SkillPointRemain}";
    }

    /// <summary>Cập nhật hiển thị slot equip.</summary>
    private void RefreshEquippedDisplay()
    {
        if (_runtime == null) return;

        // Skill slot (E)
        SkillData equippedSkill = _runtime.EquippedSkill;
        if (_equippedSkillIcon != null)
        {
            bool hasSkill = equippedSkill != null && equippedSkill.icon != null;
            _equippedSkillIcon.sprite = hasSkill ? equippedSkill.icon : null;
            _equippedSkillIcon.color = hasSkill ? Color.white : Color.clear;
        }
        if (_equippedSkillName != null)
            _equippedSkillName.text = equippedSkill != null ? equippedSkill.skillName : "Empty";

        // Signature slot (R)
        SkillData equippedSig = _runtime.EquippedSignature;
        if (_equippedSignatureIcon != null)
        {
            bool hasSig = equippedSig != null && equippedSig.icon != null;
            _equippedSignatureIcon.sprite = hasSig ? equippedSig.icon : null;
            _equippedSignatureIcon.color = hasSig ? Color.white : Color.clear;
        }
        if (_equippedSignatureName != null)
            _equippedSignatureName.text = equippedSig != null ? equippedSig.skillName : "Empty";
    }

    // ─────────────────────────────────────────────────────────────
    //  TAB SWITCH
    // ─────────────────────────────────────────────────────────────

    private void SwitchTab(ViewTab tab)
    {
        _currentTab = tab;
        BuildTree();
        RefreshHeader();
        RefreshEquippedDisplay();
    }

    // ─────────────────────────────────────────────────────────────
    //  REFUND
    // ─────────────────────────────────────────────────────────────

    private void OnRefundClicked()
    {
        if (_runtime == null || _database == null) return;

        _runtime.RefundAllWithDatabase(_database);
        BuildTree();
        RefreshHeader();
        RefreshEquippedDisplay();
    }

    // ─────────────────────────────────────────────────────────────
    //  SKILL DETAIL PANEL (Hover/Click)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Hiển thị thông tin chi tiết skill (gọi từ SkillNodeUI khi hover/click).</summary>
    public void ShowSkillDetail(SkillData skill)
    {
        if (_detailPanel == null || skill == null) return;

        _detailPanel.SetActive(true);

        if (_detailIcon != null)
        {
            _detailIcon.sprite = skill.icon;
            _detailIcon.color = skill.icon != null ? Color.white : Color.clear;
        }
        if (_detailName != null) _detailName.text = skill.skillName;
        if (_detailDesc != null) _detailDesc.text = skill.skilDesc;
        if (_detailType != null) _detailType.text = skill.skillType.ToString();
    }

    public void HideSkillDetail()
    {
        if (_detailPanel != null)
            _detailPanel.SetActive(false);
    }
}
