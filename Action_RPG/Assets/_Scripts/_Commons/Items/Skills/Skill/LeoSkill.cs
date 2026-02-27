using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LeoSkill : SkillBehavior
{
    [Header("Distance Settings")]
    public float maxCastRange = 4.0f;    // Tầm tìm địch
    public float defaultDashDist = 4.0f; // Lướt đi đâu nếu không có địch
    public float sideOffset = 0.3f;      // Lướt cách hông địch bao xa (1.5m là vừa tầm kiếm)

    [Header("Speed & Detection")]
    public float dashSpeed = 5f;        // Tốc độ lướt (Nhanh)
    public float detectionAngle = 120f;  // Góc quét địch

    [Header("Hit Settings")]
    public float hitRadius = 1.0f;       // Bán kính vùng chém

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // 1. Tìm mục tiêu
        Stats target = FindBestTarget();

        // 2. Tính toán điểm đến
        Vector3 startPos = transform.position;
        Vector3 targetPos;
        bool isTargetedDash = false;

        if (target != null)
        {
            // --- CÓ MỤC TIÊU: LƯỚT SANG HÔNG ---
            isTargetedDash = true;

            // Random chọn Trái (-1) hoặc Phải (1) của kẻ địch
            float sideDir = (Random.value > 0.5f) ? 1f : -1f;

            // Công thức: Vị trí địch + (Vector Phải của địch * Khoảng cách * Hướng)
            // target.transform.right là hướng bên phải của địch
            Vector3 flankPos = target.transform.position + (target.transform.right * sideOffset * sideDir);

            // Giữ nguyên độ cao Y của địch (hoặc của mình)
            flankPos.y = target.transform.position.y;

            targetPos = flankPos;

            // [Mẹo] Xoay mặt Player về phía Enemy ngay lập tức để chém cho chuẩn visual
            Vector3 lookAtEnemy = (target.transform.position - targetPos).normalized;
            if (lookAtEnemy != Vector3.zero) stats.facingDirection = lookAtEnemy;
        }
        else
        {
            // --- KHÔNG MỤC TIÊU: LƯỚT THẲNG ---
            isTargetedDash = false;

            Vector3 dashDir = stats.facingDirection;
            if (dashDir == Vector3.zero) dashDir = player.transform.forward;
            dashDir.y = 0; dashDir.Normalize();

            targetPos = startPos + (dashDir * defaultDashDist);
        }

        // 3. Thực hiện lướt
        StartCoroutine(ExecuteDash(targetPos, target, isTargetedDash));
        return true;
    }

    private Stats FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, maxCastRange, player.dangerLayer);
        Stats bestTarget = null;
        float closestDist = Mathf.Infinity;
        Vector3 currentDir = stats.facingDirection;
        if (currentDir == Vector3.zero) currentDir = transform.forward;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;

                // Chỉ lấy địch trong góc nhìn
                if (Vector3.Angle(currentDir, dirToTarget) < detectionAngle / 2)
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = enemyStats;
                    }
                }
            }
        }
        return bestTarget;
    }

    private IEnumerator ExecuteDash(Vector3 destination, Stats lockedTarget, bool isTargeted)
    {
        // Setup Ghosting & I-Frame
        bool originalKinematic = rb.isKinematic;
        rb.isKinematic = true;
        stats.isInvincible = true;

        Vector3 startPos = transform.position;
        float distanceTotal = Vector3.Distance(startPos, destination);

        // Thời gian lướt tối thiểu 0.1s để không bị giật
        float duration = Mathf.Max(distanceTotal / dashSpeed, 0.1f);
        float timer = 0f;

        List<GameObject> damagedList = new List<GameObject>();

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float percent = timer / duration;

            // Di chuyển
            rb.MovePosition(Vector3.Lerp(startPos, destination, percent));

            // Quét Damage
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up, hitRadius, player.dangerLayer);
            foreach (var hit in hits)
            {
                if (!damagedList.Contains(hit.gameObject))
                {
                    Stats enemyStats = hit.GetComponent<Stats>();
                    if (enemyStats != null && enemyStats.currentHp > 0)
                    {
                        damagedList.Add(hit.gameObject);

                        // Nếu là mục tiêu bị khóa -> Ép buộc tính là Side Hit (Flank)
                        bool isFlankHit = (isTargeted && enemyStats == lockedTarget);

                        DealDamage(enemyStats, isFlankHit);
                    }
                }
            }

            yield return null;
        }

        // Kết thúc
        rb.isKinematic = originalKinematic;
        stats.isInvincible = false;
        if (!originalKinematic) rb.linearVelocity = Vector3.zero;
    }

    private void DealDamage(Stats enemyStats, bool isFlankHit)
    {
        bool wasAlive = enemyStats.currentHp > 0;
        stats.EnterCombat();

        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        // [QUAN TRỌNG] Tính hệ số hướng t
        float t = 0f;
        if (isFlankHit)
        {
            t = 0.5f; // Ép buộc t = 0.5 (Side Hit / Bên hông)
            Debug.Log($"<color=cyan>Flank Attack!</color> {enemyStats.name}");
        }
        else
        {
            // Tính toán bình thường cho kẻ địch khác xung quanh
            t = CombatMath.CalculateDirectionFactor(transform, enemyStats);
        }

        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, 1f
        );

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.isKnockback = false;
        info.damageAmount = damage;
        info.attacker = stats;

        enemyStats.TakeDamage(info);

        if (wasAlive && enemyStats.currentHp <= 0)
        {
            // Side Hit (0.5) không kích hoạt AssassinPassive (cần 0.85), trừ khi bạn muốn sửa logic đó sau
            if (stats != null) stats.NotifyKillEnemy(enemyStats, (t >= 0.85f));
        }
        if (stats != null) stats.NotifyOnHitEnemy(enemyStats, t, isCrit);
    }
}