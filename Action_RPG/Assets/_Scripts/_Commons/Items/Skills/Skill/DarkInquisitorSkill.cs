using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class DarkInquisitorSkill : SkillBehavior
{
    [Header("Phase 1: The Pull (Hút Địch)")]
    public float pullDuration = 0.6f; // Hút trong 0.6s
    public float pullSpeed = 15f;     // Tốc độ bay vào
    public float coneLength = 5.0f;   // Tầm hút xa (Hình nón)
    public float coneAngle = 60f;     // Góc hút

    [Header("Phase 2: The Execution (Chém)")]
    public float slashDelay = 0.2f;   // Thời gian khựng lại trước khi chém (tạo lực)
    public float boxWidth = 2f;     // Rộng 1.5m
    public float boxLength = 3.0f;    // Dài 3m

    [Header("Effects")]
    public float stunDuration = 1.5f;
    public float shredPercent = 0.5f; // Giảm 30% Armor & MagicResist
    public float shredDuration = 5.0f;

    [Header("Healing")]
    public float healPerHitPercent = 0.02f; // Hồi 2% máu mỗi kẻ địch
    public float maxHealPercent = 0.20f;    // Tối đa 20%

    [Header("VFX")]
    public GameObject pullVfxPrefab;   // VFX Gió/Hố đen hút vào
    public GameObject slashVfxPrefab;  // VFX Kiếm khí chém xuống

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    // Struct để lưu trữ chỉ số gốc trước khi bị trừ
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
        // Trả lại chỉ số khi tháo skill
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
        // 1. KHÓA DI CHUYỂN
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        // Xác định hướng và tâm điểm hút (Sweet Spot)
        // Tâm hút = Vị trí chém xuống (Cách người chơi boxLength/2)
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        Vector3 pullCenter = transform.position + (forward * boxLength * 0.5f);

        // --- GIAI ĐOẠN 1: HÚT (PULL) ---
        Debug.Log("Inquisitor: Đang hút...");

        // Spawn VFX Hút
        //if (pullVfxPrefab)
        //{
        //    GameObject vfx = Instantiate(pullVfxPrefab, pullCenter, Quaternion.identity);
        //    Destroy(vfx, pullDuration);
        //}

        float timer = 0f;
        while (timer < pullDuration)
        {
            PullEnemies(pullCenter, forward);
            timer += Time.deltaTime;
            yield return null;
        }

        // --- GIAI ĐOẠN 2: CHÉM (EXECUTE) ---
        // Khựng lại 1 nhịp để tạo cảm giác nặng
        yield return new WaitForSeconds(slashDelay);

        Debug.Log("Inquisitor: TRẢM!");
        // Animator.SetTrigger("HeavySlash");

        // Spawn VFX Chém
        //if (slashVfxPrefab)
        //{
        //    Instantiate(slashVfxPrefab, pullCenter, Quaternion.LookRotation(forward));
        //}

        // Tính toán va chạm hình hộp
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

        // Hồi máu
        if (hitCount > 0)
        {
            float healPercent = Mathf.Min(hitCount * healPerHitPercent, maxHealPercent);
            float healAmount = stats.maxHp * healPercent;
            stats.currentHp += healAmount;
            if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;
            Debug.Log($"<color=green>Inquisitor Heal:</color> +{healAmount} HP ({hitCount} enemies)");
        }

        // Delay animation chém xong
        yield return new WaitForSeconds(0.4f);

        player.isAttacking = false;
    }

    private void PullEnemies(Vector3 centerPoint, Vector3 forwardDir)
    {
        // Quét tất cả địch trong tầm xa (coneLength)
        Collider[] hits = Physics.OverlapSphere(transform.position, coneLength, player.dangerLayer);

        foreach (var hit in hits)
        {
            Rigidbody enemyRb = hit.GetComponent<Rigidbody>();
            if (enemyRb == null) continue;

            Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;

            // 1. Kiểm tra góc (Hình nón)
            if (Vector3.Angle(forwardDir, dirToEnemy) < coneAngle / 2)
            {
                // 2. Tính hướng kéo về tâm (Sweet Spot)
                Vector3 pullDirection = (centerPoint - hit.transform.position).normalized;
                float distToCenter = Vector3.Distance(hit.transform.position, centerPoint);

                // [LOGIC CHỐNG CHỒNG LẤP]
                // Nếu địch đã quá gần tâm (0.5m) -> Ngừng kéo để tránh chúng chui tọt vào nhau
                if (distToCenter < 0.8f)
                {
                    enemyRb.linearVelocity = Vector3.zero;
                    continue;
                }

                // Kéo địch
                if (!enemyRb.isKinematic)
                {
                    // Vật lý thường: Gán vận tốc bay về tâm
                    enemyRb.linearVelocity = pullDirection * pullSpeed;
                }
                else
                {
                    // Kinematic (Boss/NavMesh): Dịch chuyển
                    NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled) agent.velocity = Vector3.zero;

                    hit.transform.position += pullDirection * pullSpeed * Time.deltaTime;
                }
            }
        }
    }

    private void ApplyExecuteEffect(Stats enemyStats)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        // Tính Damage
        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, 1f
        );

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.damageAmount = damage;
        info.isStun = true;
        info.stunDuration = stunDuration;
        info.isKnockback = false; // Đang gom lại chém thì không nên đẩy ra, hoặc đẩy rất nhẹ
        info.attacker = stats;

        enemyStats.TakeDamage(info);

        // Áp dụng Debuff Giảm Armor & MagicResist
        ApplyDualShred(enemyStats);
    }

    private void ApplyDualShred(Stats target)
    {
        // Nếu đã bị debuff rồi thì refresh thời gian (hoặc bỏ qua tùy logic)
        // Ở đây mình làm logic: Nếu chưa bị thì debuff, nếu bị rồi thì thôi (để đơn giản)
        if (!activeDebuffs.ContainsKey(target))
        {
            StatBackup backup = new StatBackup();
            backup.originalArmor = target.armor;
            backup.originalMagicResist = target.magicResist;

            // Trừ chỉ số
            target.armor *= (1.0f - shredPercent);
            target.magicResist *= (1.0f - shredPercent);

            Debug.Log($"<color=purple>Shredded {target.name}:</color> Arm/MR -{shredPercent * 100}%");

            // Start Restore Coroutine
            backup.revertCoroutine = StartCoroutine(RevertShredRoutine(target, shredDuration));

            activeDebuffs.Add(target, backup);
        }
    }

    private IEnumerator RevertShredRoutine(Stats target, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (target != null && activeDebuffs.ContainsKey(target))
        {
            StatBackup backup = activeDebuffs[target];

            // Trả lại chỉ số gốc
            target.armor = backup.originalArmor;
            target.magicResist = backup.originalMagicResist;

            activeDebuffs.Remove(target);
            Debug.Log($"<color=white>Debuff ended for {target.name}</color>");
        }
    }

    // Vẽ Gizmos Debug
    void OnDrawGizmosSelected()
    {
        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;

        // 1. Vẽ Cone Hút (Màu Vàng)
        Gizmos.color = Color.yellow;
        // Vẽ 2 đường biên của nón
        Vector3 leftRay = Quaternion.Euler(0, -coneAngle / 2, 0) * forward;
        Vector3 rightRay = Quaternion.Euler(0, coneAngle / 2, 0) * forward;
        Gizmos.DrawRay(transform.position, leftRay * coneLength);
        Gizmos.DrawRay(transform.position, rightRay * coneLength);
        // Vẽ cung tròn (tương đối)
        Gizmos.DrawWireSphere(transform.position, coneLength);

        // 2. Vẽ Box Chém (Màu Đỏ)
        Gizmos.color = Color.red;
        Vector3 boxCenter = transform.position + (forward * boxLength * 0.5f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, Quaternion.LookRotation(forward), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxWidth, 2f, boxLength));
        Gizmos.matrix = oldMatrix;
    }
}