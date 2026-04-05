using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class DarkInquisitorSkill : SkillBehavior
{
    [Header("Phase 1: The Pull (Hút Địch)")]
    public float pullDuration = 0.6f;
    public float pullSpeed = 15f;
    public float coneLength = 5.0f;
    public float coneAngle = 60f;

    [Header("Phase 2: The Execution (Chém)")]
    public float slashDelay = 0.2f;
    public float boxWidth = 2f;
    public float boxLength = 3.0f;

    [Header("Damage Scaling (Hybrid)")]
    public float physicalScale = 2.0f; // Scale 200% Sát thương Vật lý
    public float magicScale = 2.0f;    // Scale 200% Sát thương Phép thuật

    [Header("Effects")]
    public float stunDuration = 1.5f;
    public float shredPercent = 0.5f; // Giảm 50% Armor & MagicResist
    public float shredDuration = 5.0f;

    [Header("Healing")]
    public float healPerHitPercent = 0.02f;
    public float maxHealPercent = 0.20f;

    [Header("VFX")]
    public GameObject pullVfxPrefab;
    public GameObject slashVfxPrefab;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    private class StatBackup
    {
        public float originalArmor;
        public float originalMagicResist;
        public Coroutine revertCoroutine;
    }
    private Dictionary<Stats, StatBackup> activeDebuffs = new Dictionary<Stats, StatBackup>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
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
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        Vector3 pullCenter = transform.position + (forward * boxLength * 0.5f);

        Debug.Log("Inquisitor: Đang hút...");

        if (pullVfxPrefab) Destroy(Instantiate(pullVfxPrefab, pullCenter, Quaternion.identity), pullDuration);

        float timer = 0f;
        while (timer < pullDuration)
        {
            PullEnemies(pullCenter, forward);
            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(slashDelay);
        Debug.Log("Inquisitor: TRẢM!");

        if (slashVfxPrefab) Instantiate(slashVfxPrefab, pullCenter, Quaternion.LookRotation(forward));

        Vector3 boxCenter = transform.position + (forward * boxLength * 0.5f);
        Vector3 boxSize = new Vector3(boxWidth, 2f, boxLength);
        Quaternion orientation = Quaternion.LookRotation(forward);

        Collider[] hits = Physics.OverlapBox(boxCenter, boxSize * 0.5f, orientation, player.dangerLayer);

        int hitCount = 0;
        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                hitCount++;
                ApplyExecuteEffect(enemyStats);
            }
        }

        if (hitCount > 0)
        {
            float healPercent = Mathf.Min(hitCount * healPerHitPercent, maxHealPercent);
            float healAmount = stats.maxHp * healPercent;
            stats.Heal(healAmount); // Sử dụng hàm Heal an toàn
            Debug.Log($"<color=green>Inquisitor Heal:</color> +{healAmount} HP ({hitCount} enemies)");
        }

        yield return new WaitForSeconds(0.4f);
        player.isAttacking = false;
    }

    private void PullEnemies(Vector3 centerPoint, Vector3 forwardDir)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, coneLength, player.dangerLayer);

        foreach (var hit in hits)
        {
            Rigidbody enemyRb = hit.GetComponent<Rigidbody>();
            if (enemyRb == null) continue;

            Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;

            if (Vector3.Angle(forwardDir, dirToEnemy) < coneAngle / 2)
            {
                Vector3 pullDirection = (centerPoint - hit.transform.position).normalized;
                float distToCenter = Vector3.Distance(hit.transform.position, centerPoint);

                if (distToCenter < 0.8f)
                {
                    enemyRb.linearVelocity = Vector3.zero;
                    continue;
                }

                if (!enemyRb.isKinematic)
                {
                    enemyRb.linearVelocity = pullDirection * pullSpeed;
                }
                else
                {
                    NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled) agent.velocity = Vector3.zero;
                    hit.transform.position += pullDirection * pullSpeed * Time.deltaTime;
                }
            }
        }
    }

    // ==========================================================
    // TÍNH TOÁN SÁT THƯƠNG HỖN HỢP (HYBRID DAMAGE)
    // ==========================================================
    private void ApplyExecuteEffect(Stats enemyStats)
    {
        stats.EnterCombat();

        // 1. GỌI HÀM PHÁ GIÁP TRƯỚC
        // Kẻ địch bị trừ 50% Giáp/Kháng phép ngay lập tức để đòn chém đau hơn!
        ApplyDualShred(enemyStats);

        // 2. TÍNH TOÁN RAW DAMAGE
        // Lấy 200% Vật lý và 200% Phép thuật
        float rawPhysDmg = stats.physicalAtk * physicalScale;
        float rawMagDmg = stats.magicAtk * magicScale;

        // 3. TÍNH CHỈ SỐ XUYÊN THẤU TỪ BACKSTAB
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);
        float currentEnemyArmor = enemyStats.armor;
        float currentEnemyMR = enemyStats.magicResist;

        if (t == 1.0f) // Nếu đánh từ sau lưng -> Áp dụng xuyên giáp của hệ thống
        {
            currentEnemyArmor *= (1f - stats.armorBackstabReduce);
            currentEnemyMR *= (1f - stats.magicResistBackstabReduce);
        }

        // 4. TRỪ PHÒNG NGỰ ĐỘC LẬP
        // Giáp chỉ đỡ Vật lý, Kháng Phép chỉ đỡ Phép
        float reducedPhysDmg = rawPhysDmg * (100f / (100f + Mathf.Max(0, currentEnemyArmor)));
        float reducedMagDmg = rawMagDmg * (100f / (100f + Mathf.Max(0, currentEnemyMR)));

        // Gộp sát thương thực tế lại
        float baseTotalDamage = reducedPhysDmg + reducedMagDmg;

        // 5. TÍNH CHÍ MẠNG & HỆ SỐ TỔNG
        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        float critMult = isCrit ? stats.critMultiplier : 1.0f;

        // Sát thương cuối cùng (Nhân thêm DamageOutputMultiplier của Stats)
        float finalDamage = baseTotalDamage * critMult * stats.damageOutputMultiplier;

        // 6. GỬI SÁT THƯƠNG
        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.damageAmount = finalDamage;
        info.isStun = true;
        info.stunDuration = stunDuration;
        info.isKnockback = false;
        info.attacker = stats;
        info.impactLevel = 1;

        enemyStats.TakeDamage(info);
        Debug.Log($"<color=red>Hybrid Strike:</color> Gây {finalDamage:F1} sát thương (Phys: {reducedPhysDmg:F1} | Mag: {reducedMagDmg:F1})");
    }

    private void ApplyDualShred(Stats target)
    {
        if (!activeDebuffs.ContainsKey(target))
        {
            StatBackup backup = new StatBackup();
            backup.originalArmor = target.armor;
            backup.originalMagicResist = target.magicResist;

            target.armor *= (1.0f - shredPercent);
            target.magicResist *= (1.0f - shredPercent);

            Debug.Log($"<color=purple>Shredded {target.name}:</color> Arm/MR -{shredPercent * 100}%");

            backup.revertCoroutine = StartCoroutine(RevertShredRoutine(target, shredDuration));
            activeDebuffs.Add(target, backup);
        }
        else
        {
            // Làm mới thời gian nếu đã bị dính
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
            Debug.Log($"<color=white>Debuff ended for {target.name}</color>");
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;

        Gizmos.color = Color.yellow;
        Vector3 leftRay = Quaternion.Euler(0, -coneAngle / 2, 0) * forward;
        Vector3 rightRay = Quaternion.Euler(0, coneAngle / 2, 0) * forward;
        Gizmos.DrawRay(transform.position, leftRay * coneLength);
        Gizmos.DrawRay(transform.position, rightRay * coneLength);
        Gizmos.DrawWireSphere(transform.position, coneLength);

        Gizmos.color = Color.red;
        Vector3 boxCenter = transform.position + (forward * boxLength * 0.5f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, Quaternion.LookRotation(forward), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxWidth, 2f, boxLength));
        Gizmos.matrix = oldMatrix;
    }
}