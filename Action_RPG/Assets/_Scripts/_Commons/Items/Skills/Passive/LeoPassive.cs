using UnityEngine;

public class LeoPassive : SkillBehavior
{
    private float cooldownTimer = 0f;

    protected override void OnEquip()
    {
        // 1. Đăng ký sự kiện: Khi đánh trúng địch
        stats.OnHitEnemy += HandleOnHitEnemy;

        // 2. Cộng chỉ số tĩnh (Chỉ số thời gian buff)
        // (Còn 5% Bonus Atk/Magic bạn cấu hình trong List Passive Stats của SkillData nhé)
        stats.buffDurationBonus += 0.15f;

        Debug.Log($"[Leo Passive] Kích hoạt: Tăng 15% thời gian Buff");
    }

    protected override void OnUnequip()
    {
        stats.OnHitEnemy -= HandleOnHitEnemy;
        stats.buffDurationBonus -= 0.15f;
    }

    // Logic xử lý khi đánh trúng
    private void HandleOnHitEnemy(Stats target, float t, bool isCrit)
    {
        // Điều kiện: Backstab 
        // Hoặc là Crit
        if (t == 1f || isCrit)
        {
            // Check Cooldown 1s
            if (Time.time < cooldownTimer) return;

            // Hồi 2 Sin
            // Kiểm tra xem Sin đã đầy chưa
            if (stats.currentSin < stats.maxSin)
            {
                stats.currentSin += 2f;
                // Kẹp trần
                if (stats.currentSin > stats.maxSin) stats.currentSin = stats.maxSin;

                Debug.Log($"<color=cyan>[Leo Passive]</color> Backstab/Crit! Hồi 2 Sin Charge.");
            }

            // Set cooldown
            cooldownTimer = Time.time + 1.0f;
        }
    }
}