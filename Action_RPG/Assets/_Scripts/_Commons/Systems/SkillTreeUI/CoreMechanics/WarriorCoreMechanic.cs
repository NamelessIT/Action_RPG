using UnityEngine;

/// <summary>
/// Warrior Core Mechanic handler.
/// CM1: Giảm 20% thời gian gồng Heavy Attack (heavyAttackWindupMult = 0.8).
/// CM2: Heavy Attack khó bị ngắt hơn (+2 Heavy Armor — heavyArmorBonus += 2).
/// CM3: Heavy Attack gây thêm 20% sát thương (heavyAttackDamageMult = 1.2).
/// U1: Tăng 20% thời gian stun của Skill.
/// U2: −1s Cooldown Skill.
/// U3: Tăng 20% scale damage của Skill.
/// PlayerController / combat system phải đọc các field trong AllyStats để áp dụng.
/// </summary>
public class WarriorCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "Warrior";

    public override void Apply(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.heavyAttackWindupMult = 0.8f;
                Debug.Log("[WarriorCM1] Windup Heavy Attack giảm 20%.");
                break;
            case 2:
                stats.heavyArmorBonus += 2;
                Debug.Log($"[WarriorCM2] Heavy Armor Bonus → {stats.heavyArmorBonus}");
                break;
            case 3:
                stats.heavyAttackDamageMult = 1.2f;
                Debug.Log("[WarriorCM3] Heavy Attack +20% sát thương.");
                break;
        }
    }

    public override void Revert(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1: stats.heavyAttackWindupMult  = 1f;  break;
            case 2: stats.heavyArmorBonus        -= 2;  break;
            case 3: stats.heavyAttackDamageMult  = 1f;  break;
        }
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.warriorSkillU1 += 0.20f; Debug.Log($"[WarriorU1] +20% stun duration (total {stats.warriorSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.warriorSkillU3 += 0.20f; Debug.Log($"[WarriorU3] +20% scale damage (total {stats.warriorSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.warriorSkillU1 = Mathf.Max(0f, stats.warriorSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.warriorSkillU3 = Mathf.Max(0f, stats.warriorSkillU3 - 0.20f);
    }
}
