using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Controller chính cho Skill Tree UI.
/// - Mở/đóng bằng phím C
/// - Hiển thị toàn bộ 125 node cùng lúc:
///     T1 (5 node chung) ở cột giữa phía trên
///     4 class tree (30 node mỗi cây) dạng 4 cột dọc bên dưới
///     T4 (15 node) dạng grid 3×5 trong mỗi cột class
/// - Không có tab, không chuyển view
/// </summary>
public class SkillTreeController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR — DATA
    // ─────────────────────────────────────────────────────────────

    [Header("Data — Per Character")]
    [SerializeField] private SkillTreeDatabase _chrisDatabase;
    [SerializeField] private SkillTreeDatabase _leoDatabase;

    private SkillTreeDatabase ActiveDatabase =>
        (_playerStats != null && _playerStats.characterId == "Leo") ? _leoDatabase : _chrisDatabase;

    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR — UI
    // ─────────────────────────────────────────────────────────────

    [Header("UI — Panel")]
    [SerializeField] private GameObject _skillTreePanel;
    [SerializeField] private RectTransform _nodeContainer;
    [Tooltip("ScrollRect bao NodeContainer. Khi mở panel sẽ tự cuộn lên vùng 5 node T1 (top).")]
    [SerializeField] private UnityEngine.UI.ScrollRect _scrollRect;
    [SerializeField] private SkillNodeUI _skillNodePrefab;
    [SerializeField] private float _nodeScale = 0.2f;
    [Tooltip("Kích thước gốc mỗi node (px) trước khi nhân Node Scale. Tránh để node anchor-stretch phình to che node khác.")]
    [SerializeField] private Vector2 _nodeSize = new Vector2(100f, 100f);

    [Header("UI — Header (Optional)")]
    [SerializeField] private TextMeshProUGUI _skillPointText;
    [SerializeField] private TextMeshProUGUI _classNameText;

    [Header("UI — Skill Detail Panel (Optional)")]
    [SerializeField] private GameObject _detailPanel;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TextMeshProUGUI _detailName;
    [SerializeField] private TextMeshProUGUI _detailDesc;
    [SerializeField] private TextMeshProUGUI _detailType;

    [Header("UI — Equipped Slots (Optional)")]
    [SerializeField] private Image _equippedSkillIcon;
    [SerializeField] private Image _equippedSignatureIcon;
    [SerializeField] private TextMeshProUGUI _equippedSkillName;
    [SerializeField] private TextMeshProUGUI _equippedSignatureName;

    [Header("UI — Refund (Optional)")]
    [SerializeField] private Button _refundButton;

    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR — LAYOUT POSITIONS
    //  Điều chỉnh các giá trị này nếu node bị lệch / chồng nhau
    // ─────────────────────────────────────────────────────────────

    [Header("Layout — Vertical Y positions (canvas units)")]
    [Tooltip("Y của node T1 cơ bản đầu tiên (trên cùng). 5 node T1 xếp dọc xuống từ đây.")]
    [SerializeField] private float _t1TopY = 500f;
    [Tooltip("Khoảng trống (px) giữa node T1 CUỐI và tier đầu (T2) của 4 class. " +
             "Vị trí class được TÍNH TỪ đáy cột T1, không phải toạ độ tuyệt đối.")]
    [SerializeField] private float _gapAfterCommonNodes = 140f;
    [Tooltip("Khoảng cách Y giữa các TIER trong 1 class (T2→T3→T4→T5).")]
    [SerializeField] private float _classTierSpacingY = 420f;
    [Tooltip("Khoảng trống tối thiểu giữa đáy tier trước và đỉnh tier sau. Code sẽ tự cộng thêm chiều cao 5 node để tránh đè node.")]
    [SerializeField] private float _gapBetweenClassTiers = 160f;
    [Tooltip("Khoảng cách Y giữa các node trong cùng cột (cùng tier).")]
    [SerializeField] private float _nodeSpacingY = 80f;

    // Y bắt đầu của tier T2 (class), tính runtime trong BuildAllTrees từ đáy cột T1.
    private float _classStartY;

    [Header("Layout — Horizontal positions")]
    [Tooltip("Khoảng cách X giữa 2 class tree kề nhau")]
    [SerializeField] private float _classColumnSpacingX = 220f;
    [Tooltip("Khoảng cách X giữa 3 sub-column trong T4 grid")]
    [SerializeField] private float _t4SubColSpacingX = 65f;
    [Tooltip("[KHÔNG DÙNG] T1 hiện xếp DỌC (x=0) theo _nodeSpacingY. Giữ field để tương thích, " +
             "không ảnh hưởng layout.")]
    [SerializeField] private float _t1SpacingX = 220f;
    [Tooltip("Lề (px) thêm vào quanh cây khi tự tính size cho NodeContainer (để scroll có khoảng thở)")]
    [SerializeField] private Vector2 _contentPadding = new Vector2(200f, 200f);

    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────

    public static bool IsSkillTreeOpen { get; private set; }

    private SkillTreeRuntime _runtime;
    private PlayerStats _playerStats;
    private List<SkillNodeUI> _spawnedNodes = new List<SkillNodeUI>();

    // ─────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        MakeDetailPanelNonBlocking();

        // Sap lai bo cuc: Icon/NameText/DescText/TypeText deu dang nam o (0,0) tuc chong
        // dung len nhau giua panel, va BTN_Refund cung o (0,0) -> chinh la chu "Button" troi
        // giua man hinh. Controller nay chi set sprite/text nen khong da nhau.
        //
        // Truyen THANG tham chieu da serialize. Component nay nam tren GameUI_MainLayout chu
        // KHONG phai tren SkillTreePanel, nen moi cach tim theo ten tu gameObject deu tra ve
        // null va bo cuc se im lang khong duoc sua.
        Systems.SkillTreeLayoutRepair.Apply(_detailPanel, _refundButton);

        // Khoac giao dien cho DUNG panel skill tree (lay tu cha cua DetailPanel), khong phai
        // cho GameUI_MainLayout ma component nay tinh co nam tren.
        GameObject skillTreeRoot = _detailPanel != null && _detailPanel.transform.parent != null
            ? _detailPanel.transform.parent.gameObject
            : null;
        if (skillTreeRoot != null)
        {
            Systems.UILayoutTidy.Apply(skillTreeRoot);
            Systems.ArcaneUISkin.Apply(skillTreeRoot);
        }
        SetPanelVisible(false);
    }

    /// <summary>
    /// Detail panel KHÔNG được nhận raycast — nếu nó che con trỏ thì node sẽ nhận PointerExit
    /// ngay khi panel hiện ra → nhấp nháy. Ép CanvasGroup.blocksRaycasts=false + tắt raycastTarget
    /// mọi Graphic con. Chạy bằng code nên không cần user setup trong Editor.
    /// </summary>
    private void MakeDetailPanelNonBlocking()
    {
        if (_detailPanel == null) return;

        var cg = _detailPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = _detailPanel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        // Tắt raycastTarget cho toàn bộ Graphic con (Image/Text) để panel "trong suốt" với chuột.
        foreach (var g in _detailPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            g.raycastTarget = false;
    }

    private void Start()
    {
        _runtime      = FindFirstObjectByType<SkillTreeRuntime>();
        _playerStats  = PlayerStats.Current;

        if (_runtime == null)
            Debug.LogWarning("[SkillTreeController] Không tìm thấy SkillTreeRuntime!");
        else
            _runtime.SetDatabase(ActiveDatabase); // TRUYỀN DATABASE NGAY LẬP TỨC ĐỂ RUNTIME CÓ DỮ LIỆU TÍNH TOÁN

        if (_refundButton != null)
            _refundButton.onClick.AddListener(OnRefundClicked);

        // Delay 2 frame: PlayerStats.Start() + GameManager.AssignStatsNextFrame() xong hết
        StartCoroutine(LateLoadAndReapply());
    }

    private System.Collections.IEnumerator LateLoadAndReapply()
    {
        yield return null; // frame 1
        yield return null; // frame 2
        _runtime?.LoadAndReapply(ActiveDatabase);
    }

    private void OnEnable()  { if (_runtime != null) _runtime.OnSkillTreeChanged += RefreshHeader; }
    private void OnDisable() { if (_runtime != null) _runtime.OnSkillTreeChanged -= RefreshHeader; }

    // ─────────────────────────────────────────────────────────────
    //  TOGGLE
    // ─────────────────────────────────────────────────────────────

    public void ToggleSkillTree() => SetPanelVisible(!IsSkillTreeOpen);

    public void SetPanelVisible(bool isVisible)
    {
        // [GUARD] Chặn ngay trong controller (không chỉ ở phím C của PlayerController),
        // phòng khi UI/Button gọi thẳng. Chỉ MỞ khi không có panel chặn KHÁC đang bật.
        // Lúc này lock "SkillTree" chưa đặt → IsPaused=true nghĩa là panel khác (Inventory/DevTool) đang mở.
        if (isVisible && !IsSkillTreeOpen && UIPauseManager.IsPaused)
        {
            Debug.LogWarning("[SkillTreeController] Không mở Skill Tree: đang có UI chặn khác mở.");
            return;
        }

        IsSkillTreeOpen = isVisible;

        if (_skillTreePanel != null)
            _skillTreePanel.SetActive(isVisible);

        // Pause/cursor do UIPauseManager quản lý tập trung
        UIPauseManager.SetLock("SkillTree", isVisible);

        if (isVisible)
        {
            BuildAllTrees();
            RefreshHeader();
            RefreshEquippedDisplay();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD — hiển thị toàn bộ 125 node cùng lúc
    // ─────────────────────────────────────────────────────────────

    private void BuildAllTrees()
    {
        ClearNodes();

        var db = ActiveDatabase;
        if (db == null || _runtime == null) return;

        // ── T1 Common tree — CỘT DỌC ở giữa, phía trên (4 nhánh class nằm ngay dưới) ──
        if (db.commonTree != null)
        {
            var nodes = db.commonTree.skillNodes;
            // Đếm số node hợp lệ để căn giữa hàng ngang T1
            int t1Count = 0;
            foreach (var nd in nodes) if (!string.IsNullOrEmpty(nd.nodeId)) t1Count++;

            int t1Idx = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (string.IsNullOrEmpty(nodes[i].nodeId)) continue;
                SpawnNode(nodes[i], T1Position(t1Idx, t1Count));
                t1Idx++;
            }

            // Tier đầu của 4 class bắt đầu NGAY DƯỚI node T1 cuối + khoảng trống cấu hình.
            float t1BottomY = _t1TopY - Mathf.Max(0, t1Count - 1) * _nodeSpacingY;
            _classStartY = t1BottomY - _gapAfterCommonNodes;
        }
        else
        {
            _classStartY = _t1TopY - _gapAfterCommonNodes; // fallback nếu không có commonTree
        }

        // ── 4 Class trees — 4 cột dọc bên dưới ─────────────────
        int classCount = db.allClassTrees.Count;
        for (int c = 0; c < classCount; c++)
        {
            var tree = db.allClassTrees[c];
            if (tree == null) continue;

            float colX = ClassColumnX(c, classCount);
            var nodes = tree.skillNodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (string.IsNullOrEmpty(nodes[i].nodeId)) continue;
                SpawnNode(nodes[i], ClassNodePosition(i, colX));
            }
        }

        if (_classNameText != null)
            _classNameText.text = "SKILL TREE";

        // Sau khi spawn xong → tự co giãn NodeContainer ôm trọn mọi node (để ScrollRect kéo hết).
        FitContainerToNodes();
    }

    /// <summary>
    /// Tự tính bounding box của toàn bộ node (theo anchoredPosition × scale) rồi set sizeDelta
    /// cho NodeContainer cho đủ lớn + đẩy node về tâm. Đây là cách thay thế Content Size Fitter —
    /// vì node đặt thủ công bằng anchoredPosition nên Content Size Fitter (vốn chỉ đọc Layout Group)
    /// sẽ co Content về 0 và làm mất scroll. KHÔNG dùng Content Size Fitter / Layout Group ở đây.
    /// </summary>
    private void FitContainerToNodes()
    {
        if (_nodeContainer == null || _spawnedNodes.Count == 0) return;

        // Tắt mọi component layout gây xung đột (nếu lỡ gắn trong Editor)
        var fitter = _nodeContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (fitter != null && fitter.enabled)
        {
            fitter.enabled = false;
            Debug.LogWarning("[SkillTreeController] Đã tắt Content Size Fitter trên NodeContainer " +
                             "(không tương thích node đặt thủ công — gây mất scroll). Size do code tự tính.");
        }
        var layoutGroup = _nodeContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
        if (layoutGroup != null && layoutGroup.enabled)
        {
            layoutGroup.enabled = false;
            Debug.LogWarning("[SkillTreeController] Đã tắt Layout Group trên NodeContainer " +
                             "(ghi đè vị trí node thủ công). Size/vị trí do code tự quản lý.");
        }

        // Bounding box theo tâm từng node (anchoredPosition) + nửa kích thước thật (size × scale)
        Vector2 halfNode = _nodeSize * (0.5f * _nodeScale);
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var node in _spawnedNodes)
        {
            if (node == null) continue;
            var rt = node.GetComponent<RectTransform>();
            if (rt == null) continue;
            Vector2 p = rt.anchoredPosition;
            minX = Mathf.Min(minX, p.x - halfNode.x);
            maxX = Mathf.Max(maxX, p.x + halfNode.x);
            minY = Mathf.Min(minY, p.y - halfNode.y);
            maxY = Mathf.Max(maxY, p.y + halfNode.y);
        }

        if (minX > maxX || minY > maxY) return; // không có node hợp lệ

        float width  = (maxX - minX) + _contentPadding.x * 2f;
        float height = (maxY - minY) + _contentPadding.y * 2f;

        // Đảm bảo NodeContainer dùng anchor/pivot tâm để sizeDelta = kích thước thật vùng cuộn
        _nodeContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _nodeContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _nodeContainer.pivot     = new Vector2(0.5f, 0.5f);
        _nodeContainer.sizeDelta = new Vector2(width, height);

        // Tâm bounding box (thường lệch xuống do cây trải từ +Y xuống -Y). Dời mọi node để
        // bounding box căn giữa NodeContainer → không thừa khoảng trống 1 phía, kéo cân đối.
        Vector2 boxCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        if (boxCenter.sqrMagnitude > 0.01f)
        {
            foreach (var node in _spawnedNodes)
            {
                if (node == null) continue;
                var rt = node.GetComponent<RectTransform>();
                if (rt == null) continue;
                rt.anchoredPosition -= boxCenter;
            }
        }

        Debug.Log($"[SkillTreeController] NodeContainer fit: size={width:F0}x{height:F0}, " +
                  $"recenter offset={boxCenter}");

        // Sau khi recenter toàn cây, đưa view về TOP để mở panel thấy 5 node T1 trước
        // (T1 ở trên cùng theo trục Y). Cuộn ngang về giữa.
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases(); // đảm bảo ScrollRect cập nhật content size mới
            if (_scrollRect.vertical)   _scrollRect.verticalNormalizedPosition = 1f;   // 1 = top
            if (_scrollRect.horizontal) _scrollRect.horizontalNormalizedPosition = 0.5f; // giữa
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  POSITION CALCULATORS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Vị trí node T1 — CỘT DỌC ở giữa (x=0), xếp từ trên xuống. 4 nhánh class nằm ngay
    /// DƯỚI node T1 cuối (_t2TopY phải thấp hơn đáy cột T1). `total` không dùng (giữ cho call-site).
    /// </summary>
    private Vector2 T1Position(int nodeIdx, int total)
    {
        return new Vector2(0f, _t1TopY - nodeIdx * _nodeSpacingY);
    }

    /// <summary>X của cột class thứ c (căn giữa tổng thể).</summary>
    private float ClassColumnX(int classIdx, int totalClasses)
    {
        float totalWidth = (totalClasses - 1) * _classColumnSpacingX;
        return -totalWidth * 0.5f + classIdx * _classColumnSpacingX;
    }

    /// <summary>
    /// Vị trí node trong class tree. Mọi tier tính TỪ _classStartY (đáy cột T1 + gap),
    /// nên 4 nhánh class luôn bắt đầu ngay dưới node T1 cuối dù đổi số node/spacing.
    /// Nodes 0-4   → T2 (single column)
    /// Nodes 5-9   → T3 (single column)
    /// Nodes 10-24 → T4 (3×5 grid: col = idx/5, row = idx%5)
    /// Nodes 25-29 → T5 (single column)
    /// </summary>
    private Vector2 ClassNodePosition(int nodeIdx, float colX)
    {
        float t2TopY = _classStartY;
        float tierStepY = ClassTierStepY();
        float t3TopY = t2TopY - tierStepY;
        float t4TopY = t3TopY - tierStepY;
        float t5TopY = t4TopY - tierStepY;

        if (nodeIdx < 5)
        {
            return new Vector2(colX, t2TopY - nodeIdx * _nodeSpacingY);
        }
        else if (nodeIdx < 10)
        {
            return new Vector2(colX, t3TopY - (nodeIdx - 5) * _nodeSpacingY);
        }
        else if (nodeIdx < 25)
        {
            // T4 — grid 3×5
            int t4Idx = nodeIdx - 10;
            int col   = t4Idx / 5;
            int row   = t4Idx % 5;
            float xOff = (col - 1) * _t4SubColSpacingX; // -spacing, 0, +spacing
            return new Vector2(colX + xOff, t4TopY - row * _nodeSpacingY);
        }
        else
        {
            return new Vector2(colX, t5TopY - (nodeIdx - 25) * _nodeSpacingY);
        }
    }

    /// <summary>
    /// Một tier T2/T3/T5 có 5 node dọc, T4 có grid 5 hàng. Nếu chỉ dùng khoảng cách
    /// cố định nhỏ hơn 4 * _nodeSpacingY, tier kế tiếp sẽ đè lên node 3/4/5 của tier trước.
    /// </summary>
    private float ClassTierStepY()
    {
        float fiveNodeTierHeight = 4f * Mathf.Max(1f, _nodeSpacingY);
        float safeStep = fiveNodeTierHeight + Mathf.Max(0f, _gapBetweenClassTiers);
        return Mathf.Max(_classTierSpacingY, safeStep);
    }

    // ─────────────────────────────────────────────────────────────
    //  SPAWN HELPER
    // ─────────────────────────────────────────────────────────────

    private void SpawnNode(SkillNodeData nodeData, Vector2 position)
    {
        SkillNodeUI nodeUI = Instantiate(_skillNodePrefab, _nodeContainer);
        RectTransform rect = nodeUI.GetComponent<RectTransform>();
        if (rect != null)
        {
            // Ép anchor/pivot về tâm + kích thước cố định. Nếu prefab để anchor stretch
            // (0,0)-(1,1) + sizeDelta (0,0), node sẽ phình to bằng cả NodeContainer → background
            // trắng phủ lên và chặn click các node khác. Ép cố định để mỗi node là 1 ô _nodeSize.
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.sizeDelta        = _nodeSize;
            rect.anchoredPosition = position;
            rect.localScale       = Vector3.one * _nodeScale;
        }
        nodeUI.Bind(nodeData, _runtime, this);
        _spawnedNodes.Add(nodeUI);
    }

    private void ClearNodes()
    {
        foreach (var n in _spawnedNodes)
            if (n != null) Destroy(n.gameObject);
        _spawnedNodes.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    //  REFRESH
    // ─────────────────────────────────────────────────────────────

    public void RefreshAllNodes()
    {
        foreach (var n in _spawnedNodes)
            if (n != null) n.Refresh();
        RefreshHeader();
        RefreshEquippedDisplay();
    }

    private void RefreshHeader()
    {
        if (_runtime == null) return;

        if (_skillPointText != null && _runtime != null)
            _skillPointText.text = $"SP: {_runtime.SkillPointRemain}";
        
        if (_classNameText != null)
            _classNameText.text = _runtime.CurrentClassName; // Lấy tên thật (Class kết hợp) từ Runtime
    }

    private void RefreshEquippedDisplay()
    {
        if (_runtime == null) return;

        var skill = _runtime.EquippedSkill;
        if (_equippedSkillIcon != null)
        {
            bool has = skill != null && skill.icon != null;
            _equippedSkillIcon.sprite = has ? skill.icon : null;
            _equippedSkillIcon.color  = has ? Color.white : Color.clear;
        }
        if (_equippedSkillName != null)
            _equippedSkillName.text = skill != null ? skill.skillName : "Empty";

        var sig = _runtime.EquippedSignature;
        if (_equippedSignatureIcon != null)
        {
            bool has = sig != null && sig.icon != null;
            _equippedSignatureIcon.sprite = has ? sig.icon : null;
            _equippedSignatureIcon.color  = has ? Color.white : Color.clear;
        }
        if (_equippedSignatureName != null)
            _equippedSignatureName.text = sig != null ? sig.skillName : "Empty";
    }

    // ─────────────────────────────────────────────────────────────
    //  REFUND
    // ─────────────────────────────────────────────────────────────

    public void OnRefundClicked()
    {
        var db = ActiveDatabase;
        if (_runtime == null || db == null) return;
        _runtime.RefundAllWithDatabase(db);
        BuildAllTrees();
        RefreshHeader();
        RefreshEquippedDisplay();
    }

    // ─────────────────────────────────────────────────────────────
    //  SKILL DETAIL PANEL
    // ─────────────────────────────────────────────────────────────

    public void ShowNodeDetail(SkillNodeData node)
    {
        if (_detailPanel == null || node == null) return;
        _detailPanel.SetActive(true);

        if (node.skillData != null)
        {
            if (_detailIcon != null) { _detailIcon.sprite = node.skillData.icon; _detailIcon.color = node.skillData.icon != null ? Color.white : Color.clear; }
            if (_detailName != null) _detailName.text = node.skillData.skillName;
            if (_detailDesc != null) _detailDesc.text = node.skillData.skilDesc;
            if (_detailType != null) _detailType.text = node.skillData.skillType.ToString();
        }
        else
        {
            if (_detailIcon != null) _detailIcon.color = Color.clear;
            if (_detailName != null) _detailName.text = node.nodeName;
            if (_detailDesc != null) _detailDesc.text = node.nodeDescription;
            if (_detailType != null) _detailType.text = node.nodeType.ToString();
        }
    }

    public void ShowSkillDetail(SkillData skill)
    {
        if (_detailPanel == null || skill == null) return;
        _detailPanel.SetActive(true);
        if (_detailIcon != null) { _detailIcon.sprite = skill.icon; _detailIcon.color = skill.icon != null ? Color.white : Color.clear; }
        if (_detailName != null) _detailName.text = skill.skillName;
        if (_detailDesc != null) _detailDesc.text = skill.skilDesc;
        if (_detailType != null) _detailType.text = skill.skillType.ToString();
    }

    public void HideSkillDetail()
    {
        if (_detailPanel != null) _detailPanel.SetActive(false);
    }
}
