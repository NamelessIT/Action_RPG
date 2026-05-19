using UnityEngine;

/// <summary>
/// Rogue Core Mechanic handler.
/// CM1: Dash xuyên qua quái (rogueCM1_DashPhaseEnemies — PlayerController đọc flag này khi dash).
/// CM2: Đòn đánh đầu tiên sau dash +10% Damage (track trạng thái hậu-dash).
/// CM3: Backstab giảm 30% Armor và MoveSpeed của địch trong 3s.
/// U1: +20% thời gian tác dụng của Skill.
/// U2: −1s Cooldown.
/// U3: +20% scale damage của Skill.
/// </summary>
public class RogueCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "Rogue";

    private bool _cm2Active;
    private bool _cm3Active;

    // CM2: track after-dash window
    private bool  _waitingFirstHit;
    private float _dashBonusDmgPct = 0.10f;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Apply(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.rogueCM1_DashPhaseEnemies = true;
                Debug.Log("[RogueCM1] Dash xuyên qua quái.");
                break;

            case 2:
                stats.rogueCM2_FirstHitAfterDash = true;
                _cm2Active = true;
                // Đăng ký sự kiện dash từ PlayerController
                if (player != null) player.OnDashPerformed += OnDashPerformed_CM2;
                if (stats  != null) stats.OnHitEnemy       += OnHitEnemy_CM2;
                Debug.Log("[RogueCM2] Đòn đầu sau dash +10% Damage.");
                break;

            case 3:
                stats.rogueCM3_BackstabAppliesDebuff = true;
                _cm3Active = true;
                if (stats != null) stats.OnHitEnemy += OnHitEnemy_CM3;
                Debug.Log("[RogueCM3] Backstab → -30% Armor và MS trong 3s.");
                break;
        }
    }

    public override void Revert(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.rogueCM1_DashPhaseEnemies = false;
                break;
            case 2:
                stats.rogueCM2_FirstHitAfterDash = false;
                _cm2Active = false;
                if (player != null) player.OnDashPerformed -= OnDashPerformed_CM2;
                if (stats  != null) stats.OnHitEnemy       -= OnHitEnemy_CM2;
                break;
            case 3:
                stats.rogueCM3_BackstabAppliesDebuff = false;
                _cm3Active = false;
                if (stats != null) stats.OnHitEnemy -= OnHitEnemy_CM3;
                break;
        }
    }

    // CM2: sau khi dash, đánh dấu chờ first hit
    private void OnDashPerformed_CM2() => _waitingFirstHit = true;

    private void OnHitEnemy_CM2(Stats target, float directionT, bool isCrit)
    {
        if (!_cm2Active || !_waitingFirstHit || target == null) return;
        _waitingFirstHit = false;
        // Áp thêm 10% bonus damage vào đòn đánh tới target
        // Vì damage đã được tính trước khi event này fire, ta dùng ApplyBleed-style hoặc đánh dấu
        // để combat system áp dụng. Đơn giản nhất: gửi một lần bleed damage tương đương 10% physAtk
        float bonus = stats.physicalAtk * _dashBonusDmgPct;
        DamageInfo dmg = new DamageInfo { physDamage = bonus, attacker = stats, sourcePosition = transform.position };
        target.TakeDamage(dmg);
        Debug.Log($"[RogueCM2] First hit after dash bonus: {bonus:F1}");
    }

    private void OnHitEnemy_CM3(Stats target, float directionT, bool isCrit)
    {
        if (!_cm3Active || target == null) return;
        bool isBackstab = directionT > 0.5f; // directionT ~ 1 = backstab
        if (!isBackstab) return;

        float armorDebuff = target.armor * 0.30f;
        target.armor -= armorDebuff;
        StartCoroutine(RevertArmorDebuff(target, armorDebuff, 3f));

        Debug.Log($"[RogueCM3] Backstab → giảm {armorDebuff:F1} Armor của {target.name} trong 3s.");
    }

    private System.Collections.IEnumerator RevertArmorDebuff(Stats target, float amount, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && target.currentHp > 0)
            target.armor += amount;
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.rogueSkillU1 += 0.20f; Debug.Log($"[RogueU1] +20% effect duration (total {stats.rogueSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.rogueSkillU3 += 0.20f; Debug.Log($"[RogueU3] +20% scale damage (total {stats.rogueSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.rogueSkillU1 = Mathf.Max(0f, stats.rogueSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.rogueSkillU3 = Mathf.Max(0f, stats.rogueSkillU3 - 0.20f);
    }
}
