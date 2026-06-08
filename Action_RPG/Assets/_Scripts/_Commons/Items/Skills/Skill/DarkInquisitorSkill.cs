using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

/// <summary>
/// DarkInquisitorSkill — "Gravity Guillotine".
/// Tạo một chiếc lồng hình chữ nhật trước mặt, hút kẻ địch gần đó vào trong (~1s),
/// đóng lồng giữ chúng lại, giảm 50% giáp/kháng phép, rồi bổ xuống một nhát cực mạnh
/// gây 200% physicalAtk + 200% magicAtk, làm choáng, hồi máu theo số kẻ địch trúng đòn.
/// </summary>
public class DarkInquisitorSkill : SkillBehavior
{
    [Header("Lồng (hình chữ nhật, dài x rộng)")]
    public float boxLength = 3.0f;       // dài (dọc hướng nhìn)
    public float boxWidth = 2.0f;        // rộng
    public float captureRadius = 2f;   // bán kính hút kẻ địch gần đó vào lồng

    [Header("Phase 1: Hút vào")]
    public float pullDuration = 1.0f;
    public float pullSpeed = 8.0f;       // tốc độ kéo về tâm lồng
    public float cageHoldDuration = 0.3f; // đóng lồng giữ địch trước khi bổ

    [Header("Phase 2: Bổ xuống (lai vật lý + phép)")]
    public float physicalScale = 2.0f;   // 200% physicalAtk
    public float magicScale = 2.0f;      // 200% magicAtk
    public float stunDuration = 1.5f;
    public int impactLevel = 2;          // bổ mạnh, phá siêu giáp

    [Header("Giảm giáp / kháng phép")]
    public float shredPercent = 0.5f;    // 50%
    public float shredDuration = 5.0f;

    [Header("Hồi máu")]
    public float healPerHitPercent = 0.02f; // 2% / kẻ địch
    public float maxHealPercent = 0.20f;    // tối đa 20%

    [Header("VFX (tuỳ chọn)")]
    public GameObject cageVfxPrefab;
    public GameObject slashVfxPrefab;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    // Vũ khí/SkillData "ảo" để ép 200% phys + 200% magic trong MỘT lần gây sát thương,
    // không phụ thuộc vũ khí đang cầm và không lệ thuộc cấu hình asset SkillData.
    private SkillData _hybridProxy;
    private SkillData HybridProxy
    {
        get
        {
            if (_hybridProxy == null)
            {
                _hybridProxy = ScriptableObject.CreateInstance<SkillData>();
                _hybridProxy.skillType = SkillData.SkillType.Skill; // KHÔNG phải Signature → không dính signatureDamageBonus
                _hybridProxy.skillName = "DarkInquisitor_Hybrid";
            }
            return _hybridProxy;
        }
    }

    private class StatBackup
    {
        public float originalArmor;
        public float originalMagicResist;
        public Coroutine revertCoroutine;
    }
    private Dictionary<Stats, StatBackup> activeDebuffs = new Dictionary<Stats, StatBackup>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { activeDebuffs.Clear(); }

    protected override void OnUnequip()
    {
        foreach (var kvp in activeDebuffs)
        {
            Stats target = kvp.Key;
            StatBackup backup = kvp.Value;
            if (target != null)
            {
                if (backup.revertCoroutine != null) StopCoroutine(backup.revertCoroutine);
                target.armor = backup.originalArmor;
                target.magicResist = backup.originalMagicResist;
            }
        }
        activeDebuffs.Clear();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(ComboRoutine());
        return true;
    }

    private IEnumerator ComboRoutine()
    {
        // Warrior U1: +20% stun | Warrior U3: +20% sát thương | BattleMage U1: +20% tầm | BattleMage U3: +20% hồi máu
        float wU1  = stats != null ? stats.warriorSkillU1    : 0f;
        float wU3  = stats != null ? stats.warriorSkillU3    : 0f;
        float bmU1 = stats != null ? stats.battleMageSkillU1 : 0f;
        float bmU3 = stats != null ? stats.battleMageSkillU3 : 0f;

        float effStun         = stunDuration      * (1f + wU1);
        float effPhysScale     = physicalScale     * (1f + wU3);
        float effMagScale      = magicScale        * (1f + wU3);
        float effCaptureRadius = captureRadius     * (1f + bmU1);
        float effHealPerHit    = healPerHitPercent * (1f + bmU3);

        player.isAttacking = true;
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // Lồng đặt cố định tại vị trí thế giới (như "cắm" xuống đất) theo hướng nhìn lúc bấm.
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0f; forward.Normalize();

        Vector3 cageCenter = transform.position + forward * (boxLength * 0.5f);
        Quaternion cageRot = Quaternion.LookRotation(forward);

        Vector3 cageSize = new Vector3(boxWidth, 2f, boxLength);
        float total = pullDuration + cageHoldDuration;

        // VFX (tạm): TẠO LỒNG — khung tím trong suốt tồn tại suốt skill.
        VisualDebugHelper.DrawBox(cageCenter, cageSize, cageRot, new Color(0.55f, 0.1f, 0.9f, 0.22f), total + 0.3f);
        // VFX (tạm): VÙNG HÚT — quả cầu mờ trong lúc hút.
        VisualDebugHelper.DrawSphere(cageCenter, effCaptureRadius, new Color(0.6f, 0.2f, 1f, 0.12f), pullDuration);

        GameObject cageVfx = null;
        if (cageVfxPrefab) cageVfx = Instantiate(cageVfxPrefab, cageCenter, cageRot);

        // ---------- PHASE 1: HÚT ----------
        float timer = 0f;
        while (timer < pullDuration)
        {
            PullEnemiesToCage(cageCenter, effCaptureRadius);
            timer += Time.deltaTime;
            yield return null;
        }

        // ---------- PHASE 1.5: ĐÓNG LỒNG (giữ địch lại) ----------
        // VFX (tạm): ĐÓNG LỒNG — khung đậm hơn, báo hiệu lồng đã khép.
        VisualDebugHelper.DrawBox(cageCenter, cageSize, cageRot, new Color(0.35f, 0f, 0.6f, 0.45f), cageHoldDuration + 0.1f);
        timer = 0f;
        while (timer < cageHoldDuration)
        {
            PullEnemiesToCage(cageCenter, effCaptureRadius);
            timer += Time.deltaTime;
            yield return null;
        }

        // ---------- PHASE 2: BỔ XUỐNG ----------
        Debug.Log("<color=red>INQUISITOR: GUILLOTINE SLAM!</color>");
        // VFX (tạm): NHÁT BỔ — chớp đỏ toàn lồng + "lưỡi dao" vàng dọc rơi xuống.
        VisualDebugHelper.DrawBox(cageCenter, cageSize, cageRot, new Color(1f, 0.1f, 0.1f, 0.5f), 0.25f);
        VisualDebugHelper.DrawBox(cageCenter + Vector3.up * 1.5f, new Vector3(boxWidth, 3f, 0.3f), cageRot,
            new Color(1f, 0.85f, 0.2f, 0.75f), 0.2f);
        if (slashVfxPrefab) Instantiate(slashVfxPrefab, cageCenter, cageRot);

        Collider[] hits = Physics.OverlapBox(cageCenter, cageSize * 0.5f, cageRot, player.dangerLayer);

        // Cấu hình proxy lai cho cú bổ này (đã tính node nâng cấp).
        HybridProxy.skillPhysicalMultiplier = effPhysScale;
        HybridProxy.skillMagicMultiplier = effMagScale;

        stats.EnterCombat();
        HashSet<Stats> done = new HashSet<Stats>();
        int hitCount = 0;
        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !done.Add(e)) continue;

            hitCount++;
            ApplyDualShred(e);  // giảm giáp/kháng phép TRƯỚC để CombatMath tính trên giá trị đã giảm
            DamageHelper.ApplyStandardDamage(stats, e, transform, 1f, HybridProxy, null, impactLevel, true, effStun);
        }

        if (hitCount > 0)
        {
            float healPercent = Mathf.Min(hitCount * effHealPerHit, maxHealPercent);
            float healAmount = stats.maxHp * healPercent;
            stats.Heal(healAmount);
            Debug.Log($"<color=green>Inquisitor Heal:</color> +{healAmount:F0} HP ({hitCount} kẻ địch)");
        }

        if (cageVfx != null) Destroy(cageVfx);

        yield return new WaitForSeconds(0.4f);
        player.isAttacking = false;
    }

    // Kéo kẻ địch trong bán kính về tâm lồng. MoveTowards: ai ở ngoài bị hút vào,
    // ai đã ở trong bị giữ quanh tâm (đóng lồng). Warp NavMeshAgent để không bị giật về.
    private void PullEnemiesToCage(Vector3 cageCenter, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(cageCenter, radius, player.dangerLayer);
        HashSet<Stats> moved = new HashSet<Stats>();

        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !moved.Add(e)) continue;

            Transform et = e.transform;
            Vector3 next = Vector3.MoveTowards(et.position, cageCenter, pullSpeed * Time.deltaTime);

            NavMeshAgent agent = e.GetComponentInParent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.Warp(next);
            }
            else
            {
                et.position = next;
            }
        }
    }

    // ==========================================================
    // GIẢM GIÁP / KHÁNG PHÉP (50%, 5s, tự khôi phục)
    // ==========================================================
    private void ApplyDualShred(Stats target)
    {
        if (!activeDebuffs.ContainsKey(target))
        {
            StatBackup backup = new StatBackup
            {
                originalArmor = target.armor,
                originalMagicResist = target.magicResist
            };
            target.armor *= (1.0f - shredPercent);
            target.magicResist *= (1.0f - shredPercent);
            backup.revertCoroutine = StartCoroutine(RevertShredRoutine(target, shredDuration));
            activeDebuffs.Add(target, backup);
        }
        else
        {
            // Đã dính → chỉ làm mới thời gian, KHÔNG giảm chồng.
            StatBackup backup = activeDebuffs[target];
            if (backup.revertCoroutine != null) StopCoroutine(backup.revertCoroutine);
            backup.revertCoroutine = StartCoroutine(RevertShredRoutine(target, shredDuration));
        }
    }

    private IEnumerator RevertShredRoutine(Stats target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && activeDebuffs.ContainsKey(target))
        {
            StatBackup backup = activeDebuffs[target];
            target.armor = backup.originalArmor;
            target.magicResist = backup.originalMagicResist;
            activeDebuffs.Remove(target);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        forward.y = 0f;
        if (forward == Vector3.zero) return;
        forward.Normalize();

        Vector3 cageCenter = transform.position + forward * (boxLength * 0.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(cageCenter, captureRadius);

        Gizmos.color = Color.red;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(cageCenter, Quaternion.LookRotation(forward), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxWidth, 2f, boxLength));
        Gizmos.matrix = old;
    }
}
