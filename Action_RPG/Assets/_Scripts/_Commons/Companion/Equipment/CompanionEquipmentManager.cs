using UnityEngine;

/// <summary>
/// Hệ trang bị RIÊNG của Companion — KHÔNG dùng EquipmentManager của Player.
/// Đúng 3 slot: Attack Module, Defense Core, Bond.
/// Gắn trên Companion (cùng GameObject với CompanionAI + AllyStats).
///
/// Giai đoạn nền:
///  - Apply/Revert stat modifier (CompanionStatMod) vào AllyStats khi đổi slot.
///  - Sinh CompanionAttackBehavior theo role của Attack Module để CompanionAI dùng.
///  - Có thể gán module trực tiếp trong Inspector để test, hoặc EquipX() từ code.
/// </summary>
[DisallowMultipleComponent]
public class CompanionEquipmentManager : MonoBehaviour
{
    [Header("Slots (gán trực tiếp để test trong Inspector)")]
    [SerializeField] private CompanionAttackModuleData _attackModule;
    [SerializeField] private CompanionDefenseCoreData  _defenseCore;
    [SerializeField] private CompanionBondData         _bond;

    public CompanionAttackModuleData AttackModule => _attackModule;
    public CompanionDefenseCoreData  DefenseCore  => _defenseCore;
    public CompanionBondData         Bond         => _bond;

    /// <summary>Behavior AI hiện tại theo Attack Module (null nếu chưa gắn → CompanionAI dùng fallback).</summary>
    public CompanionAttackBehavior CurrentBehavior { get; private set; }

    private AllyStats _stats;

    private void Awake()
    {
        _stats = GetComponent<AllyStats>();
        if (_stats == null)
            Debug.LogError($"[CompanionEquip] '{gameObject.name}' thiếu AllyStats — companion equipment cần AllyStats.");
    }

    private void Start()
    {
        // Áp các module đã gán sẵn trong Inspector.
        ApplyModuleStats(_attackModule);
        ApplyModuleStats(_defenseCore);
        ApplyModuleStats(_bond);
        RebuildBehavior();
        if (_stats != null) _stats.RecalculateStats();
    }

    // ── EQUIP API (gọi từ code/UI sau này) ──────────────────────────────────
    public void EquipAttackModule(CompanionAttackModuleData m)
    {
        RemoveModuleStats(_attackModule);
        _attackModule = m;
        ApplyModuleStats(_attackModule);
        RebuildBehavior();
        if (_stats != null) _stats.RecalculateStats();
    }

    public void EquipDefenseCore(CompanionDefenseCoreData m)
    {
        RemoveModuleStats(_defenseCore);
        _defenseCore = m;
        ApplyModuleStats(_defenseCore);
        if (_stats != null) _stats.RecalculateStats();
    }

    public void EquipBond(CompanionBondData m)
    {
        RemoveModuleStats(_bond);
        _bond = m;
        ApplyModuleStats(_bond);
        if (_stats != null) _stats.RecalculateStats();
    }

    private void RebuildBehavior()
    {
        CurrentBehavior = CompanionAttackBehavior.Create(_attackModule);
        if (_attackModule != null)
            Debug.Log($"[CompanionEquip] Attack Module = {_attackModule.role} ('{_attackModule.moduleName}').");
    }

    // ── STAT MOD apply/revert (đối xứng) ────────────────────────────────────
    private void ApplyModuleStats(CompanionModuleData m)  => AddModuleStats(m, +1f);
    private void RemoveModuleStats(CompanionModuleData m) => AddModuleStats(m, -1f);

    private void AddModuleStats(CompanionModuleData m, float sign)
    {
        if (m == null || _stats == null || m.statMods == null) return;
        foreach (var mod in m.statMods)
        {
            float v = mod.value * sign;
            switch (mod.field)
            {
                case CompanionStatMod.Field.BonusHp:          _stats.bonusHp           += v; break;
                case CompanionStatMod.Field.Armor:            _stats.armor             += v; break;
                case CompanionStatMod.Field.MagicResist:      _stats.magicResist       += v; break;
                case CompanionStatMod.Field.BonusPhysicalAtk: _stats.bonusPhysicalAtk  += v; break;
                case CompanionStatMod.Field.BonusMagicAtk:    _stats.bonusMagicAtk     += v; break;
                case CompanionStatMod.Field.BonusAttackSpeed: _stats.bonusAttackSpeed  += v; break;
                case CompanionStatMod.Field.BonusMoveSpeed:   _stats.bonusMoveSpeed    += v; break;
                case CompanionStatMod.Field.BonusCritChance:  _stats.bonusCritChance   += v; break;
            }
        }
    }
}
