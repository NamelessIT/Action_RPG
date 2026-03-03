using UnityEngine;

public class CatalystPassive : SkillBehavior
{
    [Header("Catalyst Settings")]
    public float markDuration = 5.0f; // Tồn tại 5 giây

    protected override void OnEquip()
    {
        // Lắng nghe sự kiện mỗi khi Player đánh trúng địch
        stats.OnHitEnemy += HandleOnHitEnemy; 
    }

    protected override void OnUnequip()
    {
        // Gỡ bỏ lắng nghe khi tháo skill
        stats.OnHitEnemy -= HandleOnHitEnemy;
    }

    private void HandleOnHitEnemy(Stats target, float t, bool isCrit)
    {
        // Đánh trúng thì gắn dấu ấn
        if (target != null && target.currentHp > 0)
        {
            target.ApplyResonanceMark(markDuration);
            Debug.Log($"<color=orange>[Catalyst] Đã gắn Nội tại Cộng Hưởng lên {target.name}.</color>");
            // Bạn có thể spawn thêm VFX dấu ấn nhỏ trên đầu quái ở đây
        }
    }
}