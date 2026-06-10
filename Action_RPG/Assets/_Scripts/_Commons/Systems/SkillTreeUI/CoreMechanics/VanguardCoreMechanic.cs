using UnityEngine;

/// <summary>
/// Vanguard Core Mechanic handler.
/// CM1: Giảm 10% Stamina khi Block bằng khiên Passive.
/// CM2: Tăng 20% tốc độ dựng khiên Passive (shieldSetupTime / 1.2).
/// CM3: Không còn giảm 50% moveSpeed khi Block.
/// U1 / U3: Tăng 20% Defense Value buff / scale Armor+MR của Skill.
/// U2: −1s Cooldown Skill (base xử lý ở ClassCoreMechanicBase).
/// </summary>
public class VanguardCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "Vanguard";

    private VanguardPassive _passive;

    protected override void Awake()
    {
        base.Awake();
        _passive = GetComponent<VanguardPassive>();
    }

    public override void Apply(int index)
    {
        switch (index)
        {
            case 1:
                // CM1: staminaReduction đã được đọc trong VanguardPassive.HandleShieldInput()
                if (stats != null) stats.staminaReduction += 0.10f;
                Debug.Log("[VanguardCM1] Giảm 10% Stamina khi Block.");
                break;

            case 2:
                // CM2: giảm thời gian dựng khiên (-20% = chia cho 1.2)
                if (_passive != null)
                {
                    _passive.shieldSetupTime /= 1.2f;
                    Debug.Log($"[VanguardCM2] shieldSetupTime → {_passive.shieldSetupTime:F2}s");
                }
                break;

            case 3:
                // CM3: flag được đọc trong VanguardPassive.ActivateShield()
                if (stats != null) stats.vanguardCM3_NoBlockSlow = true;
                Debug.Log("[VanguardCM3] Không giảm MoveSpeed khi Block.");
                break;
        }
    }

    public override void Revert(int index)
    {
        switch (index)
        {
            case 1:
                if (stats != null) stats.staminaReduction -= 0.10f;
                break;
            case 2:
                if (_passive != null) _passive.shieldSetupTime *= 1.2f;
                break;
            case 3:
                if (stats != null) stats.vanguardCM3_NoBlockSlow = false;
                break;
        }
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: −1s CD
        if (index == 1 && stats != null) { stats.vanguardSkillU1 += 0.20f; Debug.Log($"[VanguardU1] +20% Defense Value buff (total {stats.vanguardSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.vanguardSkillU3 += 0.20f; Debug.Log($"[VanguardU3] +20% Armor/MR scale damage (total {stats.vanguardSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.vanguardSkillU1 = Mathf.Max(0f, stats.vanguardSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.vanguardSkillU3 = Mathf.Max(0f, stats.vanguardSkillU3 - 0.20f);
    }
}
