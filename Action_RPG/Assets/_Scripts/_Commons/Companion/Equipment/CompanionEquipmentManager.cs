using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hệ trang bị RIÊNG của Companion (KHÔNG dùng EquipmentManager của Player).
/// 3 slot: Protocol (Slot1), Matrix (Slot2), Sync Core (Slot3).
/// Gắn cùng GameObject với CompanionAI + AllyStats.
///
/// Trách nhiệm:
///  - Apply/Revert chỉ số của 3 slot vào AllyStats (đối xứng khi đổi đồ).
///  - Dựng CompanionAttackBehavior theo Protocol cho CompanionAI dùng.
///  - Lộ DodgeChance / AggroWeight (từ Matrix) cho AI & EnemyAI.
///  - Nạp module cho 3 EffectManager (Rarity 3+).
/// </summary>
[DisallowMultipleComponent]
public class CompanionEquipmentManager : MonoBehaviour
{
    [Header("Slots (gán trực tiếp để test trong Inspector)")]
    [SerializeField] private CompanionProtocolData _protocol;
    [SerializeField] private CompanionMatrixData   _matrix;
    [SerializeField] private CompanionSyncCoreData _syncCore;

    public CompanionProtocolData Protocol => _protocol;
    public CompanionMatrixData   Matrix   => _matrix;
    public CompanionSyncCoreData SyncCore => _syncCore;

    /// <summary>Behavior AI theo Protocol (luôn khác null — không có Protocol thì là Carnage tay không).</summary>
    public CompanionAttackBehavior CurrentBehavior { get; private set; }

    /// <summary>Tỉ lệ né đòn (theo Matrix). Không có Matrix ~ Regen.</summary>
    public float DodgeChance => CompanionMatrixProfile.DodgeChance(_matrix != null ? _matrix.matrixType : (CompanionMatrixType?)null);
    /// <summary>Xác suất kẻ địch chọn đánh Companion thay vì Player (theo Matrix).</summary>
    public float AggroWeight => CompanionMatrixProfile.AggroWeight(_matrix != null ? _matrix.matrixType : (CompanionMatrixType?)null);

    private AllyStats _stats;
    private ProtocolEffectManager _protocolFx;
    private MatrixEffectManager   _matrixFx;
    private SyncCoreEffectManager _syncFx;

    private void Awake()
    {
        _stats = GetComponent<AllyStats>();
        if (_stats == null)
            Debug.LogError($"[CompanionEquip] '{gameObject.name}' thiếu AllyStats.");

        _protocolFx = GetComponent<ProtocolEffectManager>() ?? gameObject.AddComponent<ProtocolEffectManager>();
        _matrixFx   = GetComponent<MatrixEffectManager>()   ?? gameObject.AddComponent<MatrixEffectManager>();
        _syncFx     = GetComponent<SyncCoreEffectManager>() ?? gameObject.AddComponent<SyncCoreEffectManager>();
    }

    private void Start()
    {
        ApplyProtocolStats(_protocol);
        ApplyMatrixStats(_matrix);
        ApplySyncStats(_syncCore);
        RebuildBehavior();
        ReloadEffects();
        if (_stats != null) _stats.RecalculateStats();
    }

    // ── EQUIP API ────────────────────────────────────────────────────────────
    public void EquipProtocol(CompanionProtocolData m)
    {
        RemoveProtocolStats(_protocol);
        _protocol = m;
        ApplyProtocolStats(_protocol);
        RebuildBehavior();
        _protocolFx?.SetModule(_protocol);
        if (_stats != null) _stats.RecalculateStats();
    }

    public void EquipMatrix(CompanionMatrixData m)
    {
        RemoveMatrixStats(_matrix);
        _matrix = m;
        ApplyMatrixStats(_matrix);
        _matrixFx?.SetModule(_matrix);
        if (_stats != null) _stats.RecalculateStats();
    }

    public void EquipSyncCore(CompanionSyncCoreData m)
    {
        RemoveSyncStats(_syncCore);
        _syncCore = m;
        ApplySyncStats(_syncCore);
        _syncFx?.SetModule(_syncCore);
        if (_stats != null) _stats.RecalculateStats();
    }

    private void RebuildBehavior()
    {
        CurrentBehavior = CompanionAttackBehavior.Create(_protocol);
        Debug.Log($"[CompanionEquip] Protocol = {CurrentBehavior.ProtocolType}" +
                  (_protocol != null ? $" ('{_protocol.protocolName}')" : " (tay không)"));
    }

    private void ReloadEffects()
    {
        _protocolFx?.SetModule(_protocol);
        _matrixFx?.SetModule(_matrix);
        _syncFx?.SetModule(_syncCore);
    }

    // ── APPLY/REVERT CHỈ SỐ (đối xứng) ───────────────────────────────────────
    private void ApplyProtocolStats(CompanionProtocolData p)  => AddProtocolStats(p, +1f);
    private void RemoveProtocolStats(CompanionProtocolData p) => AddProtocolStats(p, -1f);
    private void AddProtocolStats(CompanionProtocolData p, float sign)
    {
        if (p == null || _stats == null) return;
        if (p.atkType == CompanionAtkType.Physical) _stats.flatPhysicalAtk += p.baseAtk * sign;
        else                                        _stats.flatMagicAtk    += p.baseAtk * sign;
        _stats.defenseValue    += p.defenseValue    * sign;
        _stats.bonusCritChance += p.bonusCritChance * sign;
        _stats.baseAttackSpeed += p.baseAttackSpeed * sign;
    }

    private void ApplyMatrixStats(CompanionMatrixData m)  => AddMatrixStats(m, +1f);
    private void RemoveMatrixStats(CompanionMatrixData m) => AddMatrixStats(m, -1f);
    private void AddMatrixStats(CompanionMatrixData m, float sign)
    {
        if (m == null || _stats == null) return;
        _stats.armor       += m.armor       * sign;
        _stats.magicResist += m.magicResist * sign;
        _stats.flatHp      += m.flatHp      * sign;
        _stats.bonusHp     += m.bonusHp     * sign;
    }

    private void ApplySyncStats(CompanionSyncCoreData s)  => AddSyncStats(s, +1f);
    private void RemoveSyncStats(CompanionSyncCoreData s) => AddSyncStats(s, -1f);
    private void AddSyncStats(CompanionSyncCoreData s, float sign)
    {
        if (s == null || _stats == null || s.statModifiers == null) return;
        foreach (var mod in s.statModifiers)
            if (mod != null) CompanionStatApplier.Apply(_stats, mod, sign);
    }
}

/// <summary>
/// Áp dụng StatModifier (substat của Sync Core) lên AllyStats — mirror EquipmentManager.ApplyModifier.
/// sign = +1 cộng vào, -1 trừ ra.
/// </summary>
public static class CompanionStatApplier
{
    public static void Apply(AllyStats a, StatModifier mod, float sign)
    {
        if (a == null || mod == null) return;
        float v = mod.GetFinalValue() * sign;
        switch (mod.stat)
        {
            case StatModifier.StatType.STR: a.flatSTR += v; break;
            case StatModifier.StatType.DEX: a.flatDEX += v; break;
            case StatModifier.StatType.INT: a.flatINT += v; break;
            case StatModifier.StatType.VIT: a.flatVIT += v; break;
            case StatModifier.StatType.AGI: a.flatAGI += v; break;
            case StatModifier.StatType.BonusSTR: a.bonusSTR += v; break;
            case StatModifier.StatType.BonusDEX: a.bonusDEX += v; break;
            case StatModifier.StatType.BonusINT: a.bonusINT += v; break;
            case StatModifier.StatType.BonusVIT: a.bonusVIT += v; break;
            case StatModifier.StatType.BonusAGI: a.bonusAGI += v; break;
            case StatModifier.StatType.FlatHP: a.flatHp += v; break;
            case StatModifier.StatType.BonusHP: a.bonusHp += v; break;
            case StatModifier.StatType.Armor: a.armor += v; break;
            case StatModifier.StatType.MagicResist: a.magicResist += v; break;
            case StatModifier.StatType.DefenseValue: a.defenseValue += v; break;
            case StatModifier.StatType.FlatPhysicalAtk: a.flatPhysicalAtk += v; break;
            case StatModifier.StatType.FlatMagicAtk: a.flatMagicAtk += v; break;
            case StatModifier.StatType.BonusPhysicalAtk: a.bonusPhysicalAtk += v; break;
            case StatModifier.StatType.BonusMagicAtk: a.bonusMagicAtk += v; break;
            case StatModifier.StatType.CritChance: a.bonusCritChance += v; break;
            case StatModifier.StatType.CritMultiplier: a.bonusCritMultiplier += v; break;
            case StatModifier.StatType.BonusAttackSpeed: a.bonusAttackSpeed += v; break;
            case StatModifier.StatType.BonusMoveSpeed: a.bonusMoveSpeed += v; break;
            case StatModifier.StatType.BonusCDR: a.bonusCdr += v; break;
            case StatModifier.StatType.PhysicalLifeSteal: a.physicalLifeSteal += v; break;
            case StatModifier.StatType.MagicLifeSteal: a.magicLifeSteal += v; break;
            case StatModifier.StatType.KnockBackRes: a.knockbackResistance += v; break;
            case StatModifier.StatType.EffectRes: a.resistanceEffect += v; break;
            case StatModifier.StatType.FlatHpGain: a.flatHpGain += v; break;
            case StatModifier.StatType.FlatSinGain: /* companion không dùng Sin */ break;
        }
    }
}
