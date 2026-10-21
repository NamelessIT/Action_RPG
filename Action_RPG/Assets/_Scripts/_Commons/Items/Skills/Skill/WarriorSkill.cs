using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WarriorSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float chargeTime = 1.0f; // Thời gian gồng (1s)

    [Header("AOE Settings (Rectangular)")]
    public float boxWidth = 1.5f;   // Chiều ngang vùng đập
    public float boxLength = 3.0f;  // Chiều dài vùng đập

    [Header("Effects")]
    public float stunDuration = 2.0f;     // Choáng 2s
    public float armorBreakPercent = 0.5f;// Giảm 50% giáp
    public float armorBreakDuration = 5f; // Trong 5s

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    // F2-09: registry owns ArmorBreak lifecycle; dictionary only tracks this Skill's active handle per target
    // to preserve legacy no-stack / no-refresh behavior and immediate OnUnequip cleanup.
    private readonly Dictionary<Stats, EffectHandle> activeArmorBreaks = new Dictionary<Stats, EffectHandle>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip()
    {
        activeArmorBreaks.Clear();
    }

    protected override void OnUnequip()
    {
        // Preserve legacy behavior: unequip immediately removes ArmorBreaks created by this Skill.
        var snapshot = new List<KeyValuePair<Stats, EffectHandle>>(activeArmorBreaks);
        activeArmorBreaks.Clear();
        foreach (var kv in snapshot)
        {
            if (kv.Key != null && kv.Value.IsValid)
                kv.Key.StatusEffects.RemoveEffect(kv.Value, EffectRemoveReason.Manual);
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(SlamRoutine());
        return true;
    }

    private IEnumerator SlamRoutine()
    {
        // 1. KHÓA DI CHUYỂN
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        // [Optional] Play Animation Gồng
        // animator.SetTrigger("WarriorCharge");
        Debug.Log("Warrior: Đang gồng...");

        // 2. GỒNG (CHARGE)
        yield return new WaitForSeconds(chargeTime);

        // 3. ĐẬP (SLAM)
        // Play Animation Đập
        // animator.SetTrigger("WarriorSlam");

        // 4. XỬ LÝ VA CHẠM
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;

        Vector3 center = transform.position + (forward * boxLength * 0.5f);
        Vector3 size = new Vector3(boxWidth, 2f, boxLength);
        Quaternion orientation = Quaternion.LookRotation(forward);

        Collider[] hits = Physics.OverlapBox(center, size * 0.5f, orientation, player.dangerLayer);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                ApplySlamEffect(enemyStats);
            }
        }

        // Chờ animation đập xong (0.5s)
        yield return new WaitForSeconds(0.5f);

        // 5. MỞ KHÓA
        player.isAttacking = false;
        Debug.Log("Warrior: Đập xong!");
    }

    private void ApplySlamEffect(Stats enemyStats)
    {
        stats.EnterCombat();

        // 1. Gây Sát Thương & Stun
        DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, 1f, data, equipmentManager.currentWeapon, 0, true, stunDuration);

        // 2. Phá Giáp (Sử dụng hàm của bạn)
        ApplyDefenseReduction(enemyStats, armorBreakPercent, armorBreakDuration);
    }

    // --- F2-09: MANAGED ARMOR BREAK ---
    private void ApplyDefenseReduction(Stats target, float reductionPercent, float duration)
    {
        if (target == null || target.currentHp <= 0) return;

        if (activeArmorBreaks.TryGetValue(target, out EffectHandle activeHandle))
        {
            // Legacy semantics: while active, another Slam does NOT stack and does NOT refresh.
            if (activeHandle.IsValid && target.StatusEffects.IsEffectActive(activeHandle)) return;
            activeArmorBreaks.Remove(target);
        }

        float armorAmount = Mathf.Max(0f, target.EffectiveArmor * Mathf.Clamp01(reductionPercent));
        if (armorAmount <= 0f) return;

        const string effectId = "WarriorSkill.Slam.ArmorBreak";
        string sourceKey = $"{effectId}:{GetInstanceID()}";
        EffectApplyResult result = target.ApplyArmorMagicResistDown(
            effectId, armorAmount, 0f, duration, stats, sourceKey,
            EffectStackPolicy.RefreshSameSource, 1, reductionPercent,
            "Warrior Slam ArmorBreak 50% / legacy no-stack no-refresh",
            (snapshot, reason) =>
            {
                if (target != null) activeArmorBreaks.Remove(target);
            });

        if (result.Accepted)
        {
            activeArmorBreaks[target] = result.handle;
            Debug.Log($"<color=orange>{target.name} bị Phá Giáp! Effective Armor -{armorAmount:0.##} trong {duration:0.##}s.</color>");
        }
    }
    // -----------------------------------
    // Vẽ Gizmos để căn chỉnh vùng va chạm (Slam Area)
    void OnDrawGizmosSelected()
    {
        // 1. Xác định hướng (Forward) giống như trong SlamRoutine
        // Nếu game đang chạy, ưu tiên facingDirection của stats
        Vector3 forward = transform.forward;
        if (Application.isPlaying && stats != null && stats.facingDirection != Vector3.zero)
        {
            forward = stats.facingDirection;
        }

        // 2. Tính toán vị trí tâm của Box (Center)
        // Tâm của OverlapBox nằm ở phía trước nhân vật một khoảng bằng nửa chiều dài box
        Vector3 center = transform.position + (forward * boxLength * 0.5f);

        // 3. Thiết lập ma trận vẽ để xoay Box theo hướng nhân vật
        Gizmos.color = Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(forward), Vector3.one);

        // 4. Vẽ Box (Size khớp với size trong OverlapBox)
        // Lưu ý: Chiều cao (y) để là 2f như trong code xử lý va chạm của bạn
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxWidth, 2f, boxLength));

        // Khôi phục lại ma trận mặc định
        Gizmos.matrix = oldMatrix;
    }
}