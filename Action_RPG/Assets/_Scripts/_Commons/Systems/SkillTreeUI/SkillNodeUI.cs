using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SkillNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual")]
    [SerializeField] private Image              _iconImage;
    [SerializeField] private Image              _frameBorder;
    [SerializeField] private TextMeshProUGUI    _nameText;
    [SerializeField] private TextMeshProUGUI    _costText;       // "1 SP"

    // Mặc định lấy từ UIPalette. LƯU Ý: đây là [SerializeField] nên prefab/scene đã lưu giá trị
    // cũ sẽ ĐÈ LÊN mặc định này — phải Reset component (hoặc sửa tay) trong Editor mới thấy đổi.
    [Header("State Colors")]
    [SerializeField] private Color _lockedColor    = new Color(0.290f, 0.267f, 0.353f, 1f); // UIPalette.StateLocked
    [SerializeField] private Color _availableColor = new Color(1.000f, 0.780f, 0.180f, 1f); // UIPalette.StateWarn
    [SerializeField] private Color _unlockedColor  = new Color(0.298f, 0.831f, 0.365f, 1f); // UIPalette.StateGood
    [SerializeField] private Color _equippedColor  = new Color(0.545f, 0.361f, 0.965f, 1f); // UIPalette.ArcaneCore

    [Header("Buttons")]
    [SerializeField] private Button _unlockButton;
    [SerializeField] private Button _equipButton;

    // ── Default icon cho node stat/core/skill (khi không có SkillData.icon) ──
    [Header("Default Icons")]
    [SerializeField] private Sprite _statNodeIcon;
    [SerializeField] private Sprite _coreNodeIcon;
    [Tooltip("Icon mặc định cho node SKILL khi skillData.icon trống — KHÔNG mượn icon Stat (thanh kiếm).")]
    [SerializeField] private Sprite _skillNodeDefaultIcon;

    private SkillNodeData       _nodeData;
    private SkillTreeRuntime    _runtime;
    private SkillTreeController _controller;
    private Coroutine           _hoverRoutine;

    public SkillNodeData NodeData => _nodeData;
    private const float HoverDetailDelaySeconds = 2f;

    // ─────────────────────────────────────────────────────────────
    //  BINDING
    // ─────────────────────────────────────────────────────────────

    public void Bind(SkillNodeData nodeData, SkillTreeRuntime runtime, SkillTreeController controller)
    {
        _nodeData   = nodeData;
        _runtime    = runtime;
        _controller = controller;

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

    /// <summary>
    /// Dat nhan mac dinh cho hai nut NGAY khi node ton tai.
    ///
    /// Vi sao can: ca hai nut trong Node.prefab deu ten "Button" va nhan mac dinh cung la
    /// "Button". Refresh() co dat nhan that ("Unlock"/"Equip"), NHUNG no early-return ngay
    /// dong dau khi _nodeData hoac _runtime con null. Node nao chua duoc Bind thi hai nut
    /// giu nguyen chu "Button" — 2 nut x ~100 node = ~200 chu "Button" rai khap cay skill.
    ///
    /// Awake chay vo dieu kien nen bit duoc ca truong hop node khong bao gio duoc Bind.
    /// </summary>
    private void Awake()
    {
        SetButtonLabel(_unlockButton, "Unlock");
        SetButtonLabel(_equipButton,  "Equip");
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        var t = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null) t.text = label;
    }

    // ─────────────────────────────────────────────────────────────
    //  REFRESH
    // ─────────────────────────────────────────────────────────────

    public void Refresh()
    {
        if (_nodeData == null || _runtime == null) return;

        string id         = _nodeData.nodeId;
        bool   isUnlocked = _runtime.IsUnlocked(id);
        bool   canUnlock  = _runtime.CanUnlockNode(_nodeData);

        // ── Icon ──────────────────────────────────────────────────
        if (_iconImage != null)
        {
            // Chọn icon ĐÚNG theo nodeType (không để Skill mượn icon Stat):
            //   Stat         → statNodeIcon
            //   CoreMechanic → coreNodeIcon
            //   Skill        → skillData.icon nếu có, không thì skillNodeDefaultIcon
            Sprite icon;
            switch (_nodeData.nodeType)
            {
                case SkillNodeType.CoreMechanic:
                    icon = _coreNodeIcon;
                    break;
                case SkillNodeType.Skill:
                    icon = (_nodeData.skillData != null && _nodeData.skillData.icon != null)
                           ? _nodeData.skillData.icon : _skillNodeDefaultIcon;
                    break;
                default: // Stat
                    icon = _statNodeIcon;
                    break;
            }

            _iconImage.sprite         = icon;
            _iconImage.preserveAspect = true;
            // Có sprite → hiện; nếu vẫn null (chưa gán default) → ẩn để tránh ô vuông trắng.
            _iconImage.enabled = icon != null;
            // Locked: tint xám nhưng VẪN NHÌN THẤY (không trong suốt hẳn).
            _iconImage.color   = isUnlocked ? Color.white : UIPalette.IconDimmed;
        }

        // ── Name + Cost ──────────────────────────────
        // Node.prefab chi co DUNG MOT label tu do ("Text (TMP)" o goc node); hai Text (TMP)
        // con lai la nhan ben trong hai Button. Vi vay _nameText va _costText dang tro
        // CUNG MOT doi tuong — ghi lan luot thi dong sau de len dong truoc, va TEN NODE
        // KHONG BAO GIO HIEN, chi con lai chu cost.
        //
        // Day KHONG phai loi keo-tha: khong co o thu hai nao de keo _costText sang.
        // Nen xu ly o code — phat hien tro trung thi GOP hai thong tin vao mot label.
        string costLabel = isUnlocked ? string.Empty : $"{_nodeData.unlockCost} SP";

        if (_nameText != null && ReferenceEquals(_nameText, _costText))
        {
            // Dung Environment.NewLine chu khong phai escape trong chuoi — tranh
            // rui ro dau backslash bi nuot khi sinh code qua pipeline.
            _nameText.text = string.IsNullOrEmpty(costLabel)
                ? _nodeData.nodeName
                : _nodeData.nodeName + System.Environment.NewLine + costLabel;
        }
        else
        {
            if (_nameText != null) _nameText.text = _nodeData.nodeName;
            if (_costText != null) _costText.text = costLabel;
        }

        // ── Frame Color ───────────────────────────────────────────
        if (_frameBorder != null)
        {
            bool isEquipped = _nodeData.skillData != null && _runtime.IsEquipped(_nodeData.skillData);

            if (isEquipped)      _frameBorder.color = _equippedColor;
            else if (isUnlocked) _frameBorder.color = _unlockedColor;
            else if (canUnlock)  _frameBorder.color = _availableColor;
            else                 _frameBorder.color = _lockedColor;
        }

        // ── Unlock Button ─────────────────────────────────────────
        if (_unlockButton != null)
        {
            _unlockButton.gameObject.SetActive(!isUnlocked);
            _unlockButton.interactable = canUnlock;

            SetButtonLabel(_unlockButton, "Unlock");
        }

        // ── Equip Button ─────────────────────────────────────────
        if (_equipButton != null)
        {
            bool isSkillNode = _nodeData.nodeType == SkillNodeType.Skill && _nodeData.skillData != null;

            // Lấy Skill thực tế (Sẽ tự động trả về skill kết hợp nếu đạt level 23)
            SkillData actualSkill = isSkillNode ? _runtime.GetActualSkillToEquip(_nodeData) : null;

            bool isEquippable = actualSkill != null &&
                               (actualSkill.skillType == SkillData.SkillType.Skill ||
                                actualSkill.skillType == SkillData.SkillType.Signature);

            bool isBanned = _runtime.IsNodeEquipBanned(_nodeData);
            bool isEquipped = actualSkill != null && _runtime.IsEquipped(actualSkill);

            // Hiện nút trang bị nếu node đã unlock và chứa kỹ năng hợp lệ
            _equipButton.gameObject.SetActive(isUnlocked && isEquippable);

            // Chỉ cho bấm trang bị nếu chưa được gán VÀ không nằm trong danh sách cấm
            _equipButton.interactable = !isEquipped && !isBanned;

            SetButtonLabel(_equipButton, isEquipped ? "Equipped" : isBanned ? "Locked" : "Equip");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  BUTTON HANDLERS
    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    //  HOVER → hiện detail/description (kể cả khi node CHƯA unlock)
    // ─────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_hoverRoutine != null)
            StopCoroutine(_hoverRoutine);

        if (_controller != null && _nodeData != null)
            _hoverRoutine = StartCoroutine(ShowDetailAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_hoverRoutine != null)
        {
            StopCoroutine(_hoverRoutine);
            _hoverRoutine = null;
        }

        if (_controller != null)
            _controller.HideSkillDetail();
    }

    private IEnumerator ShowDetailAfterDelay()
    {
        // SkillTree đang pause timeScale, nên phải dùng realtime.
        yield return new WaitForSecondsRealtime(HoverDetailDelaySeconds);
        _hoverRoutine = null;

        if (_controller != null && _nodeData != null && isActiveAndEnabled)
            _controller.ShowNodeDetail(_nodeData);
    }

    private void OnDisable()
    {
        if (_hoverRoutine != null)
        {
            StopCoroutine(_hoverRoutine);
            _hoverRoutine = null;
        }
    }

    private void OnUnlockClicked()
    {
        if (_runtime == null || _nodeData == null) return;

        if (_runtime.UnlockNode(_nodeData))
        {
            if (_controller != null) _controller.RefreshAllNodes();
        }
    }

    private void OnEquipClicked()
    {
        if (_runtime == null || _nodeData?.skillData == null) return;

        SkillData actualSkill = _runtime.GetActualSkillToEquip(_nodeData);

        if (_runtime.IsEquipped(actualSkill))
            _runtime.UnequipSkillFromTree(actualSkill.skillType);
        else
            _runtime.EquipSkillFromNode(_nodeData); // Gọi hàm mới bên Runtime

        if (_controller != null) _controller.RefreshAllNodes();
    }
}
