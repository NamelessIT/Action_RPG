using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private bool _useMockExperienceData;
    [SerializeField] private float _mockCurrentExp;
    [SerializeField] private float _mockMaxExpForCurrentLevel = 100f;

    [Header("Stat Detail Panel")]
    [SerializeField] private StatDetailUI _statDetailUI;

    [Header("HP")]
    [SerializeField] private Slider _sliderHP;
    [SerializeField] private TextMeshProUGUI _textHP;

    [Header("Experience")]
    [SerializeField] private Slider _sliderExperience;
    [SerializeField] private TextMeshProUGUI _textExperience;

    private void Awake()
    {
        if (_playerStats == null)
        {
            _playerStats = PlayerStats.Current;
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (_playerStats == null)
        {
            return;
        }

        RefreshHp();
        RefreshExperience();
    }

    public void OnClick_ShowDetailedStats()
    {
        if (_statDetailUI != null)
        {
            _statDetailUI.Toggle();
        }
    }

    private void RefreshHp()
    {
        if (_sliderHP != null)
        {
            _sliderHP.maxValue = Mathf.Max(1f, _playerStats.maxHp);
            _sliderHP.value = Mathf.Lerp(_sliderHP.value, _playerStats.currentHp, Time.unscaledDeltaTime * 12f);
        }

        if (_textHP != null)
        {
            _textHP.text = $"{Mathf.CeilToInt(_playerStats.currentHp)} / {Mathf.CeilToInt(_playerStats.maxHp)}";
        }
    }

    private void RefreshExperience()
    {
        float currentExp = _useMockExperienceData ? _mockCurrentExp : _playerStats.exp;
        float maxExpForCurrentLevel = _useMockExperienceData
            ? Mathf.Max(1f, _mockMaxExpForCurrentLevel)
            : Mathf.Max(1f, _playerStats.maxExpForCurrentLevel);

        if (_sliderExperience != null)
        {
            _sliderExperience.maxValue = maxExpForCurrentLevel;
            _sliderExperience.value = Mathf.Lerp(_sliderExperience.value, currentExp, Time.unscaledDeltaTime * 12f);
        }

        if (_textExperience != null)
        {
            _textExperience.text = $"Lv {_playerStats.level}  {Mathf.FloorToInt(currentExp)} / {Mathf.FloorToInt(maxExpForCurrentLevel)}";
        }
    }
}