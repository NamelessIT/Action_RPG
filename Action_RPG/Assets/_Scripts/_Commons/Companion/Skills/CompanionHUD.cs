using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD của Companion. Dùng ĐÚNG component như UI Player (UIStats):
///  - HP & SinCharge: Slider (maxValue/value + Lerp) — copy Slider_HP / Slider_Sin của Player rồi gán vào đây.
///  - Skill / Signature: Image cooldown-overlay (Filled, Radial360) + Text + Icon — copy ô Skill E / Skill Q của Player.
///  - Protocol/Matrix icon lấy từ module.icon; tên/avatar/3 icon nguyên mẫu lấy từ _visuals.
/// </summary>
public class CompanionHUD : MonoBehaviour
{
    [Header("Thông tin (40% trái)")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _avatar;
    [SerializeField] private Image _protocolIcon;   // chế độ tấn công (Protocol)
    [SerializeField] private Image _matrixIcon;     // chế độ phòng thủ (Matrix)

    [Header("Thanh — COPY Slider_HP / Slider_Sin của Player")]
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private Slider _sinSlider;
    [SerializeField] private TMP_Text _sinText;

    [Header("Shield Bar — COPY 2 Image shield của Player (giống UIStats)")]
    [Tooltip("Image fill bạc — lấp phần HP trống (Fill Origin = Left)")]
    [SerializeField] private Image _shieldFillImage;
    [Tooltip("Image fill bạc mờ — đè lên HP khi shield vượt max (Fill Origin = Right)")]
    [SerializeField] private Image _shieldOverlayImage;

    [Header("Passive (Image icon, không cooldown)")]
    [SerializeField] private Image _passiveIcon;

    [Header("Skill — COPY ô Skill E của Player")]
    [SerializeField] private Image _skillIcon;
    [SerializeField] private Image _skillCooldownFill;   // Image Type = Filled, Radial360
    [SerializeField] private TMP_Text _skillCooldownText;

    [Header("Signature — COPY ô Skill Q của Player")]
    [SerializeField] private Image _signatureIcon;
    [SerializeField] private Image _signatureCooldownFill;
    [SerializeField] private TMP_Text _signatureCooldownText;
    [SerializeField] private Color _notEnoughSinColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Hiển thị theo nguyên mẫu")]
    [SerializeField] private CompanionVisual[] _visuals;

    [System.Serializable]
    public struct CompanionVisual
    {
        public CompanionArchetype archetype;
        public string displayName;
        public Sprite avatar;
        public Sprite passiveIcon;
        public Sprite skillIcon;
        public Sprite signatureIcon;
    }

    [Header("Tùy chọn")]
    [SerializeField] private bool _hideWhenNoCompanion = true;

    // runtime
    private CanvasGroup _cg;
    private AllyStats _stats;
    private CompanionEquipmentManager _equip;
    private CompanionSkillController _ctrl;
    private CompanionArchetype _lastArch = (CompanionArchetype)(-1);
    private float _resolveTimer;

    private void Awake()
    {
        // Dùng CanvasGroup để ẩn/hiện (không SetActive GameObject → script vẫn chạy được).
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.interactable = false;   // HUD không tương tác
        _cg.blocksRaycasts = false; // KHÔNG chặn click sang tab/UI khác
    }

    private void Update()
    {
        // Ẩn khi đang mở menu chặn (Inventory / SkillTree / DevTool) hoặc không có Companion.
        bool hasCompanion = Resolve();
        bool show = !UIPauseManager.IsPaused && (hasCompanion || !_hideWhenNoCompanion);
        _cg.alpha = show ? 1f : 0f;
        if (!show || !hasCompanion) return;

        // ── HP (giống Player) ──
        if (_hpSlider != null)
        {
            _hpSlider.maxValue = _stats.maxHp;
            _hpSlider.value = Mathf.Lerp(_hpSlider.value, _stats.currentHp, Time.deltaTime * 10f);
        }
        if (_hpText != null) _hpText.text = $"{Mathf.Ceil(_stats.currentHp)} / {_stats.maxHp}";

        UpdateShieldBar();

        // ── SinCharge (giống Player) ──
        if (_sinSlider != null)
        {
            _sinSlider.maxValue = _stats.maxSin;
            _sinSlider.value = Mathf.Lerp(_sinSlider.value, _stats.currentSin, Time.deltaTime * 10f);
        }
        if (_sinText != null) _sinText.text = $"{Mathf.Ceil(_stats.currentSin)} / {_stats.maxSin}";

        // ── Protocol / Matrix icon ──
        SetIcon(_protocolIcon, _equip != null && _equip.Protocol != null ? _equip.Protocol.icon : null);
        SetIcon(_matrixIcon,   _equip != null && _equip.Matrix   != null ? _equip.Matrix.icon   : null);

        // ── Visual theo nguyên mẫu (chỉ đổi khi archetype đổi) ──
        if (_ctrl != null && _ctrl.Archetype != _lastArch)
        {
            _lastArch = _ctrl.Archetype;
            ApplyVisual(_lastArch);
        }

        // ── Cooldown Skill / Signature (giống ô Skill E/Q của Player) ──
        if (_ctrl != null)
        {
            UpdateCd(_skillIcon, _skillCooldownFill, _skillCooldownText,
                     _ctrl.SkillCdNormalized, _ctrl.SkillCdRemaining, true);
            UpdateCd(_signatureIcon, _signatureCooldownFill, _signatureCooldownText,
                     _ctrl.SignatureCdNormalized, _ctrl.SignatureCdRemaining, _ctrl.SignatureSinReady);
        }
    }

    // Thanh shield — logic giống UIStats.UpdateShieldBar.
    private void UpdateShieldBar()
    {
        float shield = _stats.currentShield;
        float curHp = _stats.currentHp;
        float maxHp = _stats.maxHp;

        bool hasShield = shield > 0f && maxHp > 0f;
        if (_shieldFillImage)    _shieldFillImage.gameObject.SetActive(hasShield);
        if (_shieldOverlayImage) _shieldOverlayImage.gameObject.SetActive(hasShield);
        if (!hasShield) return;

        float emptySpace  = Mathf.Max(0f, maxHp - curHp);
        float fillPart    = Mathf.Min(shield, emptySpace);
        float overlayPart = Mathf.Max(0f, shield - emptySpace);

        if (_shieldFillImage)    _shieldFillImage.fillAmount = (curHp + fillPart) / maxHp;
        if (_shieldOverlayImage) _shieldOverlayImage.fillAmount = overlayPart / maxHp;
    }

    private bool Resolve()
    {
        if (_stats != null && _ctrl != null) return true;
        _resolveTimer -= Time.deltaTime;
        if (_resolveTimer > 0f && _stats == null) return false;
        _resolveTimer = 0.5f;

        CompanionAI c = CompanionAI.Current;
        if (c == null) { _stats = null; _equip = null; _ctrl = null; return false; }
        _stats = c.GetComponent<AllyStats>();
        _equip = c.GetComponent<CompanionEquipmentManager>();
        _ctrl  = c.GetComponent<CompanionSkillController>();
        return _stats != null && _ctrl != null;
    }

    private void ApplyVisual(CompanionArchetype arch)
    {
        if (_visuals == null) return;
        foreach (var v in _visuals)
        {
            if (v.archetype != arch) continue;
            if (_nameText != null && !string.IsNullOrEmpty(v.displayName)) _nameText.text = v.displayName;
            SetIcon(_avatar, v.avatar);
            SetIcon(_passiveIcon, v.passiveIcon);
            SetIcon(_skillIcon, v.skillIcon);
            SetIcon(_signatureIcon, v.signatureIcon);
            return;
        }
    }

    private static void SetIcon(Image img, Sprite s)
    {
        if (img == null) return;
        img.sprite = s;
        img.enabled = s != null;
    }

    // Mô phỏng UIStats.UpdateSingleSkillSlot: overlay fill = phần CD còn lại, text = giây, icon mờ khi CD hoặc thiếu Sin.
    private void UpdateCd(Image icon, Image cooldownFill, TMP_Text text, float normalized, float remaining, bool resourceReady)
    {
        if (cooldownFill != null)
        {
            cooldownFill.fillAmount = normalized;
            cooldownFill.color = new Color(0.4f, 0.4f, 0.4f, 0.7f); // lớp phủ XÁM khi đang hồi
        }
        bool onCd = remaining > 0.05f;
        if (text != null)
        {
            if (onCd) { text.text = remaining.ToString("F1"); text.gameObject.SetActive(true); }
            else text.gameObject.SetActive(false);
        }
        if (icon != null && icon.enabled)
        {
            if (onCd) icon.color = Color.gray;
            else icon.color = resourceReady ? Color.white : _notEnoughSinColor;
        }
    }
}
