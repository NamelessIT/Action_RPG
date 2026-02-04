using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RougeSkill : SkillBehavior
{
    [Header("Distance Settings")]
    public float maxCastRange = 4.0f;  // Khoảng cách tối đa để tìm kẻ địch
    public float defaultDashDist = 4.0f; // Khoảng cách lướt nếu KHÔNG có địch
    public float overshootDist = 0.3f;   // Khoảng cách lướt xuyên ra sau lưng địch (bao nhiêu mét)

    [Header("Speed & Detection")]
    public float dashSpeed = 100f;      // Tốc độ lướt (Rất nhanh)
    public float detectionAngle = 120f; // Chỉ tìm địch ở góc nhìn phía trước (120 độ)

    [Header("Hit Settings")]
    public float hitRadius = 1.0f;     // Bán kính gây damage khi lướt qua

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

        // 1. Tìm mục tiêu ngon ăn nhất
        Stats target = FindBestTarget();

        // 2. Tính toán điểm đến
        Vector3 startPos = transform.position;
        Vector3 targetPos;
        bool isTargetedDash = false;

        if (target != null)
        {
            // --- CÓ MỤC TIÊU ---
            isTargetedDash = true;

            // Tính hướng từ Player -> Enemy
            Vector3 dirToEnemy = (target.transform.position - startPos).normalized;
            dirToEnemy.y = 0;

            // Điểm đến = Vị trí Enemy + (Hướng * Khoảng cách xuyên ra sau)
            targetPos = target.transform.position + (dirToEnemy * overshootDist);

            // [Option] Xoay mặt Player về phía Enemy luôn cho ngầu
            stats.facingDirection = dirToEnemy;
        }
        else
        {
            // --- KHÔNG MỤC TIÊU ---
            isTargetedDash = false;

            // Lướt theo hướng đang nhìn
            Vector3 dashDir = stats.facingDirection;
            if (dashDir == Vector3.zero) dashDir = player.transform.forward;
            dashDir.y = 0; dashDir.Normalize();

            targetPos = startPos + (dashDir * defaultDashDist);
        }

        // 3. Thực hiện lướt
        StartCoroutine(ExecuteDash(targetPos, target, isTargetedDash));
        return true;
    }

    // Hàm tìm mục tiêu gần nhất trong góc nhìn
    private Stats FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, maxCastRange, player.enemyLayer);
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

                // 1. Kiểm tra góc (Chỉ lấy địch trước mặt)
                if (Vector3.Angle(currentDir, dirToTarget) < detectionAngle / 2)
                {
                    // 2. Kiểm tra khoảng cách (Lấy đứa gần nhất)
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
        // Setup trạng thái
        bool originalKinematic = rb.isKinematic;
        rb.isKinematic = true; // Ghosting: Đi xuyên vật thể
        stats.isInvincible = true; // Bất tử

        Vector3 startPos = transform.position;
        float distanceTotal = Vector3.Distance(startPos, destination);

        // Tính thời gian dựa trên tốc độ (Quãng đường / Vận tốc)
        // Kẹp thời gian tối thiểu để không bị teleport tức thì nếu địch quá gần
        float duration = Mathf.Max(distanceTotal / dashSpeed, 0.1f);

        float timer = 0f;

        // Danh sách đã chém (để lướt qua đám đông thì chém hết, nhưng chém mục tiêu chính 1 lần thôi)
        List<GameObject> damagedList = new List<GameObject>();

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float percent = timer / duration;

            // Di chuyển nội suy (Lerp) tới điểm đích
            // Dùng Lerp giúp chuyển động chính xác tới điểm cuối cùng 100%
            Vector3 nextPos = Vector3.Lerp(startPos, destination, percent);
            rb.MovePosition(nextPos);

            // --- QUÉT DAMAGE DỌC ĐƯỜNG ---
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up, hitRadius, player.enemyLayer);
            foreach (var hit in hits)
            {
                if (!damagedList.Contains(hit.gameObject))
                {
                    Stats enemyStats = hit.GetComponent<Stats>();
                    if (enemyStats != null && enemyStats.currentHp > 0)
                    {
                        damagedList.Add(hit.gameObject);

                        // LOGIC ĐẶC BIỆT:
                        // Nếu đây là "Locked Target" (Mục tiêu bị khóa) -> Ép buộc tính là Backstab (100% xuyên giáp)
                        // Nếu là địch rác đứng chắn đường -> Tính damage bình thường
                        bool forceBackstab = (isTargeted && enemyStats == lockedTarget);

                        DealDamage(enemyStats, forceBackstab);
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

    private void DealDamage(Stats enemyStats, bool forceBackstab)
    {
        bool wasAlive = enemyStats.currentHp > 0;
        stats.EnterCombat();

        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        // [QUAN TRỌNG] Tính hệ số hướng t
        float t = 0f;
        if (forceBackstab)
        {
            t = 1.0f; // Ép buộc là sau lưng (Backstab chuẩn 100%)
            Debug.Log($"<color=red>Rouge!</color> {enemyStats.name}");
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

        enemyStats.TakeDamage(info);

        if (wasAlive && enemyStats.currentHp <= 0)
        {
            // Nếu forceBackstab thì gửi t=1 để kích hoạt AssassinPassive hồi Dash
            if (stats != null) stats.NotifyKillEnemy(enemyStats, (t >= 0.85f));
        }
        if (stats != null) stats.NotifyOnHitEnemy(enemyStats, t, isCrit);
    }
}