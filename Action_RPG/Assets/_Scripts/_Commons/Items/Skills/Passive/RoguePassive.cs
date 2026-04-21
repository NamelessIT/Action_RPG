using UnityEngine;

public class RoguePassive : SkillBehavior
{

    protected override void OnEquip()
    {
        // 1. Tăng hiệu quả xuyên giáp khi Backstab lên 100% (1.0f)
        stats.armorBackstabReduce += 0.5f;
        stats.magicResistBackstabReduce = 0.5f;

        // 2. Đăng ký sự kiện Kill
        stats.OnKillEnemy += HandleKillEnemy;

        Debug.Log($"[Rogue] Backstab Penetration: 100%");
    }

    protected override void OnUnequip()
    {
        // 1. Trả lại giá trị cũ
        stats.armorBackstabReduce -= 0.5f;
        stats.magicResistBackstabReduce -= 0.5f;

        // 2. Hủy đăng ký
        stats.OnKillEnemy -= HandleKillEnemy;

        Debug.Log("[Rogue] Gỡ bỏ hiệu ứng.");
    }

    private void HandleKillEnemy(Stats victim, bool isBackstab)
    {
        // Điều kiện: Phải kết liễu từ phía sau
        if (isBackstab)
        {
            // Hồi lại 1 lượt Dash (bằng đúng dashCost hiện tại)
            float staminaToRestore = stats.dashCost;

            if (stats.currentStamina < stats.maxStamina)
            {
                stats.currentStamina += staminaToRestore;

                // Kẹp trần
                if (stats.currentStamina > stats.maxStamina)
                    stats.currentStamina = stats.maxStamina;

                Debug.Log($"<color=red>[Rogue]</color> Backstab Kill! Hồi {staminaToRestore} Stamina.");
            }
        }
    }
}