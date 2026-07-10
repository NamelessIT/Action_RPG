using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatAllocationUI : MonoBehaviour
{
    private enum AttributeType
    {
        STR,
        INT,
        DEX,
        AGI,
        VIT,
    }

    [Serializable]
    private class StatRow
    {
        public AttributeType attributeType;
        public TextMeshProUGUI baseValueText;
        public TextMeshProUGUI totalValueText;
        public TextMeshProUGUI capText;
        public Button addButton;
    }

    [SerializeField] private AllyStats _allyStats;
    [SerializeField] private TextMeshProUGUI _statPointsText;
    [SerializeField] private StatRow[] _rows;

    private EquipmentManager _equipmentManager;

    private void Awake()
    {
        if (_allyStats == null)
        {
            _allyStats = FindFirstObjectByType<PlayerStats>();
        }

        _equipmentManager = FindFirstObjectByType<EquipmentManager>();
        if (_equipmentManager != null)
            _equipmentManager.OnEquipmentChanged += RefreshAll;
    }

    private void OnDestroy()
    {
        if (_equipmentManager != null)
            _equipmentManager.OnEquipmentChanged -= RefreshAll;
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    public void AddStrengthPoint()
    {
        TryAllocatePoint(AttributeType.STR);
    }

    public void AddIntelligencePoint()
    {
        TryAllocatePoint(AttributeType.INT);
    }

    public void AddDexterityPoint()
    {
        TryAllocatePoint(AttributeType.DEX);
    }

    public void AddAgilityPoint()
    {
        TryAllocatePoint(AttributeType.AGI);
    }

    public void AddVitalityPoint()
    {
        TryAllocatePoint(AttributeType.VIT);
    }

    public void RefreshAll()
    {
        if (_allyStats == null)
        {
            return;
        }

        if (_statPointsText != null)
        {
            _statPointsText.text = _allyStats.attributePointRemain.ToString();
        }

        int maxInvestedPoints = GetMaxInvestedPoints();

        for (int index = 0; index < _rows.Length; index++)
        {
            StatRow row = _rows[index];
            float baseValue = GetBaseAttributeValue(row.attributeType);
            float totalValue = GetTotalAttributeValue(row.attributeType);

            if (row.baseValueText != null)
            {
                row.baseValueText.text = Mathf.RoundToInt(baseValue).ToString();
            }

            if (row.totalValueText != null)
            {
                row.totalValueText.text = Mathf.RoundToInt(totalValue).ToString();
            }

            if (row.capText != null)
            {
                row.capText.text = $"Cap: {maxInvestedPoints}";
            }

            if (row.addButton != null)
            {
                row.addButton.interactable = CanAllocatePoint(row.attributeType, maxInvestedPoints);
            }
        }
    }

    private void TryAllocatePoint(AttributeType attributeType)
    {
        if (_allyStats == null)
        {
            return;
        }

        int maxInvestedPoints = GetMaxInvestedPoints();
        if (!CanAllocatePoint(attributeType, maxInvestedPoints))
        {
            return;
        }

        _allyStats.attributePointRemain -= 1;

        switch (attributeType)
        {
            case AttributeType.STR:
                _allyStats.AddBaseAttribute(Stats.BaseAttribute.STR, 1f);
                break;
            case AttributeType.INT:
                _allyStats.AddBaseAttribute(Stats.BaseAttribute.INT, 1f);
                break;
            case AttributeType.DEX:
                _allyStats.AddBaseAttribute(Stats.BaseAttribute.DEX, 1f);
                break;
            case AttributeType.AGI:
                _allyStats.AddBaseAttribute(Stats.BaseAttribute.AGI, 1f);
                break;
            case AttributeType.VIT:
                _allyStats.AddBaseAttribute(Stats.BaseAttribute.VIT, 1f);
                break;
        }

        // Stat cap is calculated against base attributes only.
        // This avoids equipment buffs or temporary effects blocking stat investment.
        _allyStats.RecalculateStats();
        RefreshAll();
    }

    private bool CanAllocatePoint(AttributeType attributeType, int maxInvestedPoints)
    {
        if (_allyStats.attributePointRemain <= 0)
        {
            return false;
        }

        // Compare against base attributes only.
        // STR/INT/DEX/AGI/VIT totals can be inflated by gear, buffs, or passives,
        // but the design requirement is to invest into baseSTR/baseINT/... directly.
        return GetBaseAttributeValue(attributeType) < maxInvestedPoints;
    }

    private int GetMaxInvestedPoints()
    {
        return (_allyStats.level * 3) + 10;
    }

    private float GetBaseAttributeValue(AttributeType attributeType)
    {
        switch (attributeType)
        {
            case AttributeType.STR:
                return _allyStats.baseSTR;
            case AttributeType.INT:
                return _allyStats.baseINT;
            case AttributeType.DEX:
                return _allyStats.baseDEX;
            case AttributeType.AGI:
                return _allyStats.baseAGI;
            case AttributeType.VIT:
                return _allyStats.baseVIT;
            default:
                return 0f;
        }
    }

    private float GetTotalAttributeValue(AttributeType attributeType)
    {
        switch (attributeType)
        {
            case AttributeType.STR:
                return _allyStats.STR;
            case AttributeType.INT:
                return _allyStats.INT;
            case AttributeType.DEX:
                return _allyStats.DEX;
            case AttributeType.AGI:
                return _allyStats.AGI;
            case AttributeType.VIT:
                return _allyStats.VIT;
            default:
                return 0f;
        }
    }
}