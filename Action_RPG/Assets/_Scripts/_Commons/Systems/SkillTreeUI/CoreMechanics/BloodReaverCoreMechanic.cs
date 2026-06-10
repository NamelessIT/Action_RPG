using UnityEngine;
using System.Collections;

/// <summary>
/// BloodReaver Core Mechanic handler.
/// CM1: HP &lt; 50% → giảm 20% Stamina tiêu hao (dynamic, cập nhật mỗi frame).
/// CM2: HP &lt; 30% → miễn nhiễm với hiệu ứng làm chậm.
/// CM3: Mỗi khi tự tiêu hao máu → +1% moveSpeed và +1% CritDamage (5s, stack 20).
/// U1: +20% bonusMoveSpeed buff của Skill.
/// U2: −1s Cooldown.
/// U3: +20% scale damage của Skill.
/// </summary>
public class BloodReaverCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "BloodReaver";

    private bool _cm1Active;
    private bool _cm2Active;
    private bool _cm3Active;

    // CM1 dynamic tracking
    private bool _cm1StaminaBonusApplied;

    // CM3 stacking buff
    private int   _cm3StackCount;
    private const int   MaxStacks   = 20;
    private const float StackBonusPct = 0.01f;
    private const float StackDuration = 5f;
    private float _addedMoveSpeed;
    private float _addedCritDmg;

    private BloodReaverPassive _passive;

    protected override void Awake()
    {
        base.Awake();
        _passive = GetComponent<BloodReaverPassive>();
    }

    public override void Apply(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.bloodReaverCM1_LowHpStamina = true;
                _cm1Active = true;
                Debug.Log("[BloodReaverCM1] HP < 50% giảm 20% Stamina.");
                break;
            case 2:
                stats.bloodReaverCM2_SlowImmuneVeryLow = true;
                _cm2Active = true;
                Debug.Log("[BloodReaverCM2] HP < 30% miễn nhiễm slow.");
                break;
            case 3:
                stats.bloodReaverCM3_SelfDmgStacks = true;
                _cm3Active = true;
                // Hook vào OnHitEnemy — BloodReaverPassive tiêu hao máu tại đây
                stats.OnHitEnemy += OnHitEnemy_CM3;
                Debug.Log("[BloodReaverCM3] Tự tiêu hao máu → stack buff.");
                break;
        }
    }

    public override void Revert(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.bloodReaverCM1_LowHpStamina = false;
                _cm1Active = false;
                if (_cm1StaminaBonusApplied) { stats.staminaReduction -= 0.20f; _cm1StaminaBonusApplied = false; }
                break;
            case 2:
                stats.bloodReaverCM2_SlowImmuneVeryLow = false;
                _cm2Active = false;
                break;
            case 3:
                stats.bloodReaverCM3_SelfDmgStacks = false;
                _cm3Active = false;
                stats.OnHitEnemy -= OnHitEnemy_CM3;
                ClearCM3Buff();
                break;
        }
    }

    void Update()
    {
        if (stats == null) return;

        // CM1: dynamic stamina reduction dựa trên HP
        if (_cm1Active)
        {
            bool shouldApply = stats.currentHp < stats.maxHp * 0.5f;
            if (shouldApply && !_cm1StaminaBonusApplied)
            {
                stats.staminaReduction += 0.20f;
                _cm1StaminaBonusApplied = true;
            }
            else if (!shouldApply && _cm1StaminaBonusApplied)
            {
                stats.staminaReduction -= 0.20f;
                _cm1StaminaBonusApplied = false;
            }
        }
    }

    // CM3: mỗi lần BloodReaverPassive gây self-damage (có hit enemy)
    private void OnHitEnemy_CM3(Stats target, float t, bool isCrit)
    {
        if (!_cm3Active) return;
        // BloodReaverPassive tự tiêu hao máu khi OnHitEnemy
        if (_cm3StackCount < MaxStacks)
        {
            _cm3StackCount++;
            _addedMoveSpeed += StackBonusPct;
            _addedCritDmg   += StackBonusPct;
            stats.bonusMoveSpeed    += StackBonusPct;
            stats.bonusCritMultiplier += StackBonusPct;
            StopAllCoroutines();
            StartCoroutine(ResetStacksAfterDelay(StackDuration));
        }
    }

    private IEnumerator ResetStacksAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearCM3Buff();
    }

    private void ClearCM3Buff()
    {
        stats.bonusMoveSpeed      -= _addedMoveSpeed;
        stats.bonusCritMultiplier -= _addedCritDmg;
        _addedMoveSpeed  = 0f;
        _addedCritDmg    = 0f;
        _cm3StackCount   = 0;
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.bloodReaverSkillU1 += 0.20f; Debug.Log($"[BloodReaverU1] +20% moveSpeed buff (total {stats.bloodReaverSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.bloodReaverSkillU3 += 0.20f; Debug.Log($"[BloodReaverU3] +20% scale damage (total {stats.bloodReaverSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.bloodReaverSkillU1 = Mathf.Max(0f, stats.bloodReaverSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.bloodReaverSkillU3 = Mathf.Max(0f, stats.bloodReaverSkillU3 - 0.20f);
    }
}
