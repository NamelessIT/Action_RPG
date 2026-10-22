using UnityEngine;

/// <summary>
/// Duelist Core Mechanic handler.
/// CM1: Hồi 10 Stamina mỗi khi Parry thành công.
/// CM2: Kẻ địch bị Parry giảm 20 Defense Value trong 3 giây.
/// CM3: Tự động Parry 1 đòn nếu quên/parry hụt. Cooldown 10 giây.
/// U1: +20% thời gian stun của Skill.
/// U2: −1s Cooldown.
/// U3: +20% scale damage của Skill.
/// </summary>
public class DuelistCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "Duelist";

    private bool _cm1Active;
    private bool _cm2Active;
    private bool _cm3Active;

    private DuelistPassive _passive;

    // CM3 auto-parry
    private float _autoParryCooldownTimer;
    private const float AutoParryCD = 10f;
    private int _cm2DebuffSequence;

    protected override void Awake()
    {
        base.Awake();
        _passive = GetComponent<DuelistPassive>();
    }

    // DuelistPassive is added dynamically by SkillFactory after the Player root has already Awake'd.
    // Never rely solely on the Awake cache when Core Mechanic nodes are applied/reverted later.
    private DuelistPassive ResolvePassive()
    {
        if (_passive == null) _passive = GetComponent<DuelistPassive>();
        return _passive;
    }

    public override void Apply(int index)
    {
        switch (index)
        {
            case 1:
                _cm1Active = true;
                stats.duelistCM1_ParryRestoresStamina = true;
                DuelistPassive passive1 = ResolvePassive();
                if (passive1 != null) passive1.OnParrySuccessEvent += OnParrySuccess_CM1;
                Debug.Log("[DuelistCM1] Parry hồi 10 Stamina.");
                break;

            case 2:
                _cm2Active = true;
                stats.duelistCM2_ParryBreaksDefense = true;
                DuelistPassive passive2 = ResolvePassive();
                if (passive2 != null) passive2.OnParrySuccessEvent += OnParrySuccess_CM2;
                Debug.Log("[DuelistCM2] Parry → địch mất 20 Defense Value trong 3s.");
                break;

            case 3:
                _cm3Active = true;
                stats.duelistCM3_AutoParry = true;
                Debug.Log("[DuelistCM3] Auto Parry mỗi 10s.");
                break;
        }
    }

    public override void Revert(int index)
    {
        switch (index)
        {
            case 1:
                _cm1Active = false;
                stats.duelistCM1_ParryRestoresStamina = false;
                DuelistPassive passive1 = ResolvePassive();
                if (passive1 != null) passive1.OnParrySuccessEvent -= OnParrySuccess_CM1;
                break;
            case 2:
                _cm2Active = false;
                stats.duelistCM2_ParryBreaksDefense = false;
                DuelistPassive passive2 = ResolvePassive();
                if (passive2 != null) passive2.OnParrySuccessEvent -= OnParrySuccess_CM2;
                break;
            case 3:
                _cm3Active = false;
                stats.duelistCM3_AutoParry = false;
                break;
        }
    }

    void Update()
    {
        // CM3: đếm cooldown auto-parry
        if (_cm3Active && _autoParryCooldownTimer > 0f)
            _autoParryCooldownTimer -= Time.deltaTime;
    }

    // CM1
    private void OnParrySuccess_CM1(bool isPerfect, Stats attacker)
    {
        if (stats == null) return;
        stats.currentStamina = Mathf.Min(stats.maxStamina, stats.currentStamina + 10f);
        Debug.Log("[DuelistCM1] Parry → hồi 10 Stamina.");
    }

    // CM2
    private void OnParrySuccess_CM2(bool isPerfect, Stats attacker)
    {
        if (attacker == null) return;
        const float debuff = 20f;
        string sourceKey = $"DuelistCM2.DefenseValueDown:{GetInstanceID()}:{++_cm2DebuffSequence}";
        attacker.ApplyDefenseValueDown(
            "DuelistCM2.DefenseValueDown", debuff, 3f, stats, sourceKey,
            EffectStackPolicy.IndependentPerSource, 1, debuff,
            "Duelist CM2 Parry -20 DefenseValue / independent 3s");
        Debug.Log($"[DuelistCM2] Parry → {attacker.name} mất {debuff} Effective Defense Value trong 3s.");
    }

    // CM3: gọi từ combat system khi đòn đánh đánh trúng player và không có parry active
    public bool TryAutoParry()
    {
        if (!_cm3Active || _autoParryCooldownTimer > 0f) return false;
        _autoParryCooldownTimer = AutoParryCD;
        Debug.Log("[DuelistCM3] Auto Parry kích hoạt!");
        return true;
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.duelistSkillU1 += 0.20f; Debug.Log($"[DuelistU1] +20% stun duration (total {stats.duelistSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.duelistSkillU3 += 0.20f; Debug.Log($"[DuelistU3] +20% scale damage (total {stats.duelistSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.duelistSkillU1 = Mathf.Max(0f, stats.duelistSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.duelistSkillU3 = Mathf.Max(0f, stats.duelistSkillU3 - 0.20f);
    }
}
