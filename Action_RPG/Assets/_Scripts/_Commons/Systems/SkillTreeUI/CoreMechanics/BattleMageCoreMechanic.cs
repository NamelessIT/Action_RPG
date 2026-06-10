using UnityEngine;

/// <summary>
/// BattleMage Core Mechanic handler.
/// CM1: Mỗi lần HP thay đổi 20% máu tối đa, hồi 10 Sin Charge.
/// CM2: 30% máu hồi phụ trội → currentShield trong 3 giây.
/// CM3: Companion hồi máu bằng 5% lượng máu BattleMage được hồi.
/// U1: +20% range của Skill.
/// U2: −1s Cooldown.
/// U3: +20% scale heal của Skill.
/// </summary>
public class BattleMageCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "BattleMage";

    // CM1: track HP snapshot (mỗi khi thay đổi 20% maxHp → hồi sin)
    private float _lastHpSnapshot = -1f;
    private bool  _cm1Active;

    public override void Apply(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.battleMageCM1_SinOnHpThreshold = true;
                _cm1Active = true;
                _lastHpSnapshot = stats.currentHp;
                Debug.Log("[BattleMageCM1] Hồi 10 Sin mỗi khi HP thay đổi 20%.");
                break;
            case 2:
                stats.battleMageCM2_ExcessHealShield = true;
                Debug.Log("[BattleMageCM2] 30% heal phụ trội → Shield 3s.");
                break;
            case 3:
                stats.battleMageCM3_CompanionHealShare = true;
                Debug.Log("[BattleMageCM3] Companion nhận 5% lượng máu player hồi.");
                break;
        }
    }

    public override void Revert(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1: stats.battleMageCM1_SinOnHpThreshold = false; _cm1Active = false; break;
            case 2: stats.battleMageCM2_ExcessHealShield  = false; break;
            case 3: stats.battleMageCM3_CompanionHealShare = false; break;
        }
    }

    void Update()
    {
        if (!_cm1Active || stats == null) return;
        if (_lastHpSnapshot < 0f) { _lastHpSnapshot = stats.currentHp; return; }

        float threshold = stats.maxHp * 0.20f;
        float delta = Mathf.Abs(stats.currentHp - _lastHpSnapshot);
        if (delta >= threshold)
        {
            stats.currentSin = Mathf.Min(stats.maxSin, stats.currentSin + 10f);
            _lastHpSnapshot  = stats.currentHp;
            Debug.Log("[BattleMageCM1] HP thay đổi 20% → hồi 10 Sin.");
        }
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.battleMageSkillU1 += 0.20f; Debug.Log($"[BattleMageU1] +20% range (total {stats.battleMageSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.battleMageSkillU3 += 0.20f; Debug.Log($"[BattleMageU3] +20% heal (total {stats.battleMageSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.battleMageSkillU1 = Mathf.Max(0f, stats.battleMageSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.battleMageSkillU3 = Mathf.Max(0f, stats.battleMageSkillU3 - 0.20f);
    }
}
