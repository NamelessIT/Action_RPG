using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component cho 1 node skill trong Skill Tree.
/// Mỗi node hiển thị: icon, tên, trạng thái (locked/unlocked/equipped), level upgrade.
/// Designer tạo prefab với component này, SkillTreeController sẽ spawn nó.
/// </summary>
public class SkillNodeUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────

    [Header("Visual")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _frameBorder;           // Viền frame (đổi màu theo state)
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;   // "Lv 2/3"
    [SerializeField] private TextMeshProUGUI _costText;    // "1 SP"

    [Header("State Colors")]
    [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color _availableColor = new Color(1f, 0.85f, 0f, 1f);    // Vàng: đủ điều kiện
    [SerializeField] private Color _unlockedColor = new Color(0.2f, 0.8f, 0.2f, 1f);  // Xanh lá: đã unlock
    [SerializeField] private Color _equippedColor = new Color(0f, 0.6f, 1f, 1f);      // Xanh dương: đang equip
    [SerializeField] private Color _maxedColor = new Color(0.9f, 0.3f, 0.9f, 1f);     // Tím: max level

    [Header("Buttons")]
    [SerializeField] private Button _unlockButton;
    [SerializeField] private Button _equipButton;

    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────

    private SkillNodeData _nodeData;
    private SkillTreeRuntime _runtime;
    private SkillTreeController _controller;

    // ─────────────────────────────────────────────────────────────
    //  BINDING
    // ─────────────────────────────────────────────────────────────

    /// <summary>Gán dữ liệu cho node UI này.</summary>
    public void Bind(SkillNodeData nodeData, SkillTreeRuntime runtime, SkillTreeController controller)
    {
        _nodeData = nodeData;
        _runtime = runtime;
        _controller = controller;

        // Gán button listeners
        if (_unlockButton != null)
        {
            _unlockButton.onClick.RemoveAllListeners();
            _unlockButton.onClick.AddListener(OnUnlockClicked);
        }
        if (_equipButton != null)
        {
            _equipButton.onClick.RemoveAllListeners();
            _equipButton.onClick.AddListener(OnEquipClicked);
        }

        Refresh();
    }

    /// <summary>Cập nhật visual theo state hiện tại.</summary>
    public void Refresh()
    {
        if (_nodeData == null || _nodeData.skillData == null || _runtime == null) return;

        SkillData skill = _nodeData.skillData;
        string id = skill.id;
        int currentLevel = _runtime.GetUpgradeLevel(id);
        bool isUnlocked = currentLevel > 0;
        bool isMaxed = currentLevel >= _nodeData.maxUpgradeLevel;
        bool canUpgrade = _runtime.CanUnlockOrUpgrade(_nodeData);
        bool isEquipped = _runtime.IsEquipped(skill);

        // --- Icon ---
        if (_iconImage != null)
        {
            _iconImage.sprite = skill.icon;
            _iconImage.color = isUnlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.8f);
        }

        // --- Name ---
        if (_nameText != null)
            _nameText.text = skill.skillName;

        // --- Level Text ---
        if (_levelText != null)
        {
            if (!isUnlocked)
                _levelText.text = "Locked";
            else if (isMaxed)
                _levelText.text = $"MAX ({currentLevel}/{_nodeData.maxUpgradeLevel})";
            else
                _levelText.text = $"Lv {currentLevel}/{_nodeData.maxUpgradeLevel}";
        }

        // --- Cost Text ---
        if (_costText != null)
        {
            if (isMaxed)
                _costText.text = "";
            else
            {
                int cost = _runtime.GetNextCost(_nodeData);
                _costText.text = $"{cost} SP";
            }
        }

        // --- Frame Color ---
        if (_frameBorder != null)
        {
            if (isEquipped)
                _frameBorder.color = _equippedColor;
            else if (isMaxed)
                _frameBorder.color = _maxedColor;
            else if (isUnlocked)
                _frameBorder.color = _unlockedColor;
            else if (canUpgrade)
                _frameBorder.color = _availableColor;
            else
                _frameBorder.color = _lockedColor;
        }

        // --- Buttons ---
        if (_unlockButton != null)
        {
            // Hiện nút Unlock/Upgrade khi chưa max
            _unlockButton.gameObject.SetActive(!isMaxed);
            _unlockButton.interactable = canUpgrade;

            // Đổi text nút
            TextMeshProUGUI btnText = _unlockButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = isUnlocked ? "Upgrade" : "Unlock";
        }

        if (_equipButton != null)
        {
            // Chỉ hiện nút Equip cho skill type Skill/Signature đã unlock
            bool isEquippableType = (skill.skillType == SkillData.SkillType.Skill
                                  || skill.skillType == SkillData.SkillType.Signature);
            _equipButton.gameObject.SetActive(isUnlocked && isEquippableType);
            _equipButton.interactable = isUnlocked && !isEquipped;

            TextMeshProUGUI btnText = _equipButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = isEquipped ? "Equipped" : "Equip";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  BUTTON HANDLERS
    // ─────────────────────────────────────────────────────────────

    private void OnUnlockClicked()
    {
        if (_runtime == null || _nodeData == null) return;

        bool success = _runtime.UnlockOrUpgrade(_nodeData);
        if (success)
        {
            // Refresh toàn bộ tree (vì prerequisite có thể mở node khác)
            if (_controller != null) _controller.RefreshAllNodes();
        }
    }

    private void OnEquipClicked()
    {
        if (_runtime == null || _nodeData == null || _nodeData.skillData == null) return;

        SkillData skill = _nodeData.skillData;

        if (_runtime.IsEquipped(skill))
        {
            // Đang equip → unequip
            _runtime.UnequipSkillFromTree(skill.skillType);
        }
        else
        {
            // Equip mới
            _runtime.EquipSkillFromTree(skill);
        }

        if (_controller != null) _controller.RefreshAllNodes();
    }

    // ─────────────────────────────────────────────────────────────
    //  PROPERTIES
    // ─────────────────────────────────────────────────────────────

    public SkillNodeData NodeData => _nodeData;
}
