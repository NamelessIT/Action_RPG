using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị chi tiết toàn bộ chỉ số nhân vật.
/// Mở/đóng bằng PlayerStatsUI.OnClick_ShowDetailedStats().
/// </summary>
public class StatDetailUI : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private PlayerStats _playerStats;

    [Header("Panel Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI _textName;
    [SerializeField] private TextMeshProUGUI _textLevelClass;

    [Header("Resources")]
    [SerializeField] private TextMeshProUGUI _textHp;
    [SerializeField] private TextMeshProUGUI _textStamina;
    [SerializeField] private TextMeshProUGUI _textSin;

    [Header("Attributes (base + bonus)")]
    [SerializeField] private TextMeshProUGUI _textSTR;
    [SerializeField] private TextMeshProUGUI _textDEX;
    [SerializeField] private TextMeshProUGUI _textINT;
    [SerializeField] private TextMeshProUGUI _textVIT;
    [SerializeField] private TextMeshProUGUI _textAGI;

    [Header("Offense")]
    [SerializeField] private TextMeshProUGUI _textPhysicalAtk;
    [SerializeField] private TextMeshProUGUI _textMagicAtk;
    [SerializeField] private TextMeshProUGUI _textAttackSpeed;
    [SerializeField] private TextMeshProUGUI _textCritChance;
    [SerializeField] private TextMeshProUGUI _textCritMultiplier;

    [Header("Defense")]
    [SerializeField] private TextMeshProUGUI _textArmor;
    [SerializeField] private TextMeshProUGUI _textMagicResist;
    [SerializeField] private TextMeshProUGUI _textDefenseValue;
    [SerializeField] private TextMeshProUGUI _textBackstabReduce;

    [Header("Mobility")]
    [SerializeField] private TextMeshProUGUI _textMoveSpeed;
    [SerializeField] private TextMeshProUGUI _textDashDistance;
    [SerializeField] private TextMeshProUGUI _textDashCost;
    [SerializeField] private TextMeshProUGUI _textCooldownReduction;

    [Header("Close Button")]
    [SerializeField] private Button _closeButton;

    private void Awake()
    {
        if (_playerStats == null)
        {
            _playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// Hiển thị panel và cập nhật toàn bộ chỉ số.
    /// Gọi bởi PlayerStatsUI.OnClick_ShowDetailedStats().
    /// </summary>
    public void Show()
    {
        if (_panelRoot != null)
        {
            _panelRoot.SetActive(true);
        }
        Refresh();
    }

    /// <summary>
    /// Ẩn panel. Gọi bởi nút [X] hoặc phím tắt.
    /// </summary>
    public void Hide()
    {
        if (_panelRoot != null)
        {
            _panelRoot.SetActive(false);
        }
    }

    public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Show();
    }

    /// <summary>
    /// Cập nhật toàn bộ Text UI từ PlayerStats hiện tại.
    /// </summary>
    public void Refresh()
    {
        if (_playerStats == null) return;

        RefreshHeader();
        RefreshResources();
        RefreshAttributes();
        RefreshOffense();
        RefreshDefense();
        RefreshMobility();
    }

    private void RefreshHeader()
    {
        SetText(_textName, _playerStats.characterName);
        SetText(_textLevelClass, $"Lv {_playerStats.level}  {_playerStats.className}");
    }

    private void RefreshResources()
    {
        SetText(_textHp, $"{Ceil(_playerStats.currentHp)} / {Ceil(_playerStats.maxHp)}");
        SetText(_textStamina, $"{Ceil(_playerStats.currentStamina)} / {Ceil(_playerStats.maxStamina)}");
        SetText(_textSin, $"{Ceil(_playerStats.currentSin)} / {Ceil(_playerStats.maxSin)}");
    }

    private void RefreshAttributes()
    {
        SetAttributeText(_textSTR, _playerStats.baseSTR, _playerStats.STR);
        SetAttributeText(_textDEX, _playerStats.baseDEX, _playerStats.DEX);
        SetAttributeText(_textINT, _playerStats.baseINT, _playerStats.INT);
        SetAttributeText(_textVIT, _playerStats.baseVIT, _playerStats.VIT);
        SetAttributeText(_textAGI, _playerStats.baseAGI, _playerStats.AGI);
    }

    private void RefreshOffense()
    {
        SetText(_textPhysicalAtk, F1(_playerStats.physicalAtk));
        SetText(_textMagicAtk, F1(_playerStats.magicAtk));
        SetText(_textAttackSpeed, F2(_playerStats.attackSpeed));
        SetText(_textCritChance, Pct(_playerStats.critChance));
        SetText(_textCritMultiplier, $"{_playerStats.critMultiplier:F2}x");
    }

    private void RefreshDefense()
    {
        SetText(_textArmor, F1(_playerStats.armor));
        SetText(_textMagicResist, F1(_playerStats.magicResist));
        SetText(_textDefenseValue, F1(_playerStats.defenseValue));
        SetText(_textBackstabReduce, Pct(_playerStats.armorBackstabReduce));
    }

    private void RefreshMobility()
    {
        SetText(_textMoveSpeed, F1(_playerStats.moveSpeed));
        SetText(_textDashDistance, F1(_playerStats.dashDistance));
        SetText(_textDashCost, Ceil(_playerStats.dashCost).ToString());
        SetText(_textCooldownReduction, Pct(_playerStats.cooldownReduction));
    }

    // ── Utility ──

    private void SetText(TextMeshProUGUI textComponent, string value)
    {
        if (textComponent != null)
        {
            textComponent.text = value;
        }
    }

    /// <summary>
    /// Hiển thị dạng "20 (+5)" khi có bonus, hoặc "20" khi không có.
    /// </summary>
    private void SetAttributeText(TextMeshProUGUI textComponent, float baseValue, float totalValue)
    {
        if (textComponent == null) return;

        int baseInt = Mathf.RoundToInt(baseValue);
        int bonus = Mathf.RoundToInt(totalValue - baseValue);

        if (bonus > 0)
            textComponent.text = $"{baseInt} <color=#00FF88>(+{bonus})</color>";
        else if (bonus < 0)
            textComponent.text = $"{baseInt} <color=#FF4444>({bonus})</color>";
        else
            textComponent.text = baseInt.ToString();
    }

    private static int Ceil(float value) => Mathf.CeilToInt(value);
    private static string F1(float value) => value.ToString("F1");
    private static string F2(float value) => value.ToString("F2");
    private static string Pct(float value) => $"{value * 100f:F1}%";
}
