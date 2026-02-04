using UnityEngine;

public class WarriorPassive : SkillBehavior
{
    [Header("Settings")]
    public float damageReduction = 0.3f; // Giảm 30% sát thương

    protected override void OnEquip()
    {
        // Gán chỉ số giảm thương vào Stats
        stats.momentumReduction = damageReduction;
        Debug.Log($"[Warrior] Momentum Armor Ready: -{damageReduction * 100}% Damage khi tấn công.");
    }

    protected override void OnUnequip()
    {
        // Reset về 0
        stats.momentumReduction = 0f;
        stats.isMomentumActive = false;
        Debug.Log("[Warrior] Gỡ bỏ Momentum Armor.");
    }

    void Update()
    {
        // Kiểm tra trạng thái tấn công từ PlayerController
        // PlayerController.cs có biến isAttacking
        if (player.isAttacking)
        {
            if (!stats.isMomentumActive)
            {
                stats.isMomentumActive = true;
                //stats.resistanceKnockBack += 0.2;
                // Debug.Log(">> Momentum Armor: ON");
            }
        }
        else
        {
            if (stats.isMomentumActive)
            {
                stats.isMomentumActive = false;
                //stats.resistanceKnockBack -= 0.2;
                // Debug.Log(">> Momentum Armor: OFF");
            }
        }
    }
}