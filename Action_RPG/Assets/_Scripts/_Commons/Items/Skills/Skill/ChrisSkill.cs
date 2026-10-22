using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChrisSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float dashSpeed = 3f;
    public float dashDuration = 1.5f;

    [Tooltip("Bán kính quét va chạm")]
    public float hitRadius = 0.3f;

    [Tooltip("Nâng tâm va chạm")]
    public float hitOffsetY = 0f;

    [Tooltip("Khoảng cách đẩy Enemy ra xa khỏi người")]
    public float pushOffsetDistance = 1f;

    public float knockbackForce = 15f;
    public float headOnAngle = 45f;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;
    private DrivenKnockbackSourceController drivenKnockback;

    // [MỚI] Tham chiếu Animator để chơi animation ủi
    private Animator animator;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        drivenKnockback = myPlayer.GetComponent<DrivenKnockbackSourceController>();
        if (drivenKnockback == null) drivenKnockback = myPlayer.gameObject.AddComponent<DrivenKnockbackSourceController>();

        // [MỚI] Lấy Animator
        animator = myPlayer.GetComponentInChildren<Animator>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(DashAndBashRoutine());
        return true;
    }

    private IEnumerator DashAndBashRoutine()
    {
        // --- 1. SETUP ---
        player.isUsingSpecialSkill = true; // Khóa Input

        // [ANIMATION] Bắt đầu Animation Push (Bỏ comment khi có Animation)
        /* if (animator != null) 
        {
            animator.SetBool("IsPushing", true); 
        } */

        Vector3 dashDir = stats.facingDirection;
        if (dashDir == Vector3.zero) dashDir = player.transform.forward;
        dashDir.y = 0;
        dashDir.Normalize();

        float timer = 0f;
        List<GameObject> alreadyDamagedList = new List<GameObject>();

        bool originalUseGravity = rb.useGravity;

        // [QUAN TRỌNG] Đảm bảo Physics sạch sẽ trước khi bắt đầu
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        drivenKnockback.BeginSession(stats, $"CHRIS_CHARGE_{GetInstanceID()}", dashDir, pushOffsetDistance, dashDuration + 0.5f);

        try
        {
            // [FIX] Dùng WaitForFixedUpdate để đồng bộ hoàn toàn với Physics
            WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

            while (timer < dashDuration)
            {
                float dt = Time.fixedDeltaTime;
                drivenKnockback.PruneInvalidTargets();

                // Quét target TRƯỚC khi tính speed để resistance có hiệu lực ngay tick player chạm enemy.
                Vector3 centerPoint = transform.position + Vector3.up * hitOffsetY;
                Collider[] hits = Physics.OverlapSphere(centerPoint, hitRadius, player.dangerLayer);

                foreach (var hit in hits)
                {
                    Stats enemyStats = hit.GetComponentInParent<Stats>();
                    if (enemyStats == null || enemyStats.currentHp <= 0) continue;

                    int impactLevel = ComputeImpactLevel(enemyStats, dashDir);
                    drivenKnockback.TryCapture(enemyStats, impactLevel);

                    if (!alreadyDamagedList.Contains(enemyStats.gameObject))
                    {
                        alreadyDamagedList.Add(enemyStats.gameObject);
                        ApplyDamage(enemyStats, dashDir);
                    }
                }

                // Driven KB: target càng nặng (KB resistance cao) thì chính SOURCE bị chậm.
                // Duration charge giữ nguyên => tổng quãng đường charge tự ngắn đi.
                float effectiveDashSpeed = dashSpeed * drivenKnockback.CurrentSpeedMultiplier;
                Vector3 nextFramePos = rb.position + (dashDir * effectiveDashSpeed * dt);
                drivenKnockback.SetPredictedAnchor(nextFramePos, dashDir);

                rb.linearVelocity = dashDir * effectiveDashSpeed;
                rb.angularVelocity = Vector3.zero;

                timer += dt;
                yield return waitFixed;
            }
            // --- [MỚI] TÍNH TOÁN SIN SAU KHI KẾT THÚC ỦI ---
            // Lúc này alreadyDamagedList đã chứa TẤT CẢ những con quái bị tông trúng trong 1.5s qua
            if (stats != null && alreadyDamagedList.Count > 0)
            {
                stats.GainSinFromAttack(alreadyDamagedList.Count);
                Debug.Log($"[ChrisSkill] Kết thúc ủi. Tông trúng tổng cộng {alreadyDamagedList.Count} mục tiêu -> Tích Sin!");
            }
            // -----------------------------------------------
        }
        finally
        {
            // --- 3. CLEANUP ---
            drivenKnockback.EndSession();
            rb.useGravity = originalUseGravity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // [ANIMATION] Tắt Animation
            /* if (animator != null) 
            {
                animator.SetBool("IsPushing", false); 
            } */
        }

        // --- 4. POST-CLEANUP ---
        // Đợi thêm 1 nhịp để đảm bảo Player dừng hẳn rồi mới trả quyền điều khiển
        yield return new WaitForFixedUpdate();
        player.isUsingSpecialSkill = false;
    }

    private int ComputeImpactLevel(Stats enemyStats, Vector3 dashDir)
    {
        Vector3 impactDir = enemyStats.transform.position - transform.position;
        impactDir.y = 0f;
        if (impactDir.sqrMagnitude <= 0.0001f) return 2;
        impactDir.Normalize();
        float angle = Vector3.Angle(dashDir, impactDir);
        return angle <= headOnAngle ? 2 : 1;
    }

    private void ApplyDamage(Stats enemyStats, Vector3 dashDir)
    {
        stats.EnterCombat();

        int impactLevel = ComputeImpactLevel(enemyStats, dashDir);

        DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, 1f, data, equipmentManager != null ? equipmentManager.currentWeapon : null, impactLevel, true, dashDuration + 0.2f);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * hitOffsetY, hitRadius);
    }
}