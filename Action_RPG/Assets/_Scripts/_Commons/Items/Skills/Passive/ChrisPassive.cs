using UnityEngine;

public class ChrisPassive : SkillBehavior
{
    private float cooldownTimer = 0f;

    protected override void OnEquip()
    {
        // 1. Logic riêng: Đăng ký sự kiện bị đánh
        stats.OnTakeDamage += HandleOnTakeDamage;

        // 2. Cộng chỉ số riêng (Kháng hiệu ứng)
        stats.resistanceEffect += 0.15f;

        Debug.Log($"[Skill System] Đã kích hoạt logic: {data.skillName}");
    }

    protected override void OnUnequip()
    {
        // 1. Hủy đăng ký (Quan trọng: Không hủy là lỗi game)
        stats.OnTakeDamage -= HandleOnTakeDamage;

        // 2. Trừ lại chỉ số
        stats.resistanceEffect -= 0.15f;

        Debug.Log($"[Skill System] Đã gỡ bỏ logic: {data.skillName}");
    }

    // Logic xử lý khi bị đánh
    private void HandleOnTakeDamage(float damage)
    {
        if (Time.time < cooldownTimer) return;

        // Hồi 3 Stamina
        float restoreAmount = 3f;
        if (stats.currentStamina < stats.maxStamina)
        {
            stats.currentStamina += restoreAmount;
            if (stats.currentStamina > stats.maxStamina) stats.currentStamina = stats.maxStamina;

            Debug.Log($"<color=green>Battle Hardened:</color> Hồi {restoreAmount} Stamina!");
        }

        // Set cooldown 1s
        cooldownTimer = Time.time + 1.0f;
    }
}