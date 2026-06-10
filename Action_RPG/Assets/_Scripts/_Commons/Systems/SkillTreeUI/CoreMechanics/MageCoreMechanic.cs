using UnityEngine;

/// <summary>
/// Mage Core Mechanic handler.
/// CM1: Hồi 10 Stamina nếu đòn đánh khác nguyên tố với đòn trước đó.
///      MagePassive cần đọc stats.mageCM1_ElementSwitchStamina và gọi TryGrantStaminaOnElementSwitch().
/// CM2: Đòn đánh gây 200% sát thương nếu nguyên tố đối diện với đòn trước (cùng target, ≤2s).
///      MagePassive cần đọc stats.mageCM2_OppositeElementBurst.
/// CM3: Đòn đánh xuyên tường (projectile).
///      Projectile spawn code cần kiểm tra stats.mageCM3_ProjectilePhaseWalls.
/// U1: +20% thời gian hiệu ứng của Skill.
/// U2: −1s Cooldown.
/// U3: +20% scale damage của Skill.
/// </summary>
public class MageCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "Mage";

    private MagePassive _passive;

    protected override void Awake()
    {
        base.Awake();
        _passive = GetComponent<MagePassive>();
    }

    public override void Apply(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.mageCM1_ElementSwitchStamina = true;
                // MagePassive đọc flag này và gọi TryGrantStaminaOnElementSwitch()
                Debug.Log("[MageCM1] Đổi nguyên tố → hồi 10 Stamina. (MagePassive cần đọc flag này)");
                break;

            case 2:
                stats.mageCM2_OppositeElementBurst = true;
                Debug.Log("[MageCM2] Nguyên tố đối diện → 200% sát thương. (MagePassive cần implement)");
                break;

            case 3:
                stats.mageCM3_ProjectilePhaseWalls = true;
                Debug.Log("[MageCM3] Đạn xuyên tường. (Projectile script cần đọc flag này)");
                break;
        }
    }

    public override void Revert(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1: stats.mageCM1_ElementSwitchStamina = false; break;
            case 2: stats.mageCM2_OppositeElementBurst  = false; break;
            case 3: stats.mageCM3_ProjectilePhaseWalls   = false; break;
        }
    }

    /// <summary>
    /// Gọi từ MagePassive khi phát hiện nguyên tố thay đổi.
    /// Hồi 10 Stamina nếu CM1 đang active.
    /// </summary>
    public void TryGrantStaminaOnElementSwitch()
    {
        if (!stats.mageCM1_ElementSwitchStamina || stats == null) return;
        stats.currentStamina = Mathf.Min(stats.maxStamina, stats.currentStamina + 10f);
        Debug.Log("[MageCM1] Đổi nguyên tố → hồi 10 Stamina.");
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.mageSkillU1 += 0.20f; Debug.Log($"[MageU1] +20% effect duration (total {stats.mageSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.mageSkillU3 += 0.20f; Debug.Log($"[MageU3] +20% scale damage (total {stats.mageSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.mageSkillU1 = Mathf.Max(0f, stats.mageSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.mageSkillU3 = Mathf.Max(0f, stats.mageSkillU3 - 0.20f);
    }
}
