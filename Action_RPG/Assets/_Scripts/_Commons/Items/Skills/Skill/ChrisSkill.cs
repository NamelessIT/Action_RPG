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

    // [MỚI] Tham chiếu Animator để chơi animation ủi
    private Animator animator;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();

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

        try
        {
            // [FIX] Dùng WaitForFixedUpdate để đồng bộ hoàn toàn với Physics
            WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

            while (timer < dashDuration)
            {
                // Dùng fixedDeltaTime cho chính xác
                float dt = Time.fixedDeltaTime;

                // A. Ép vận tốc Player (Ghi đè hoàn toàn vận tốc cũ -> Tốc độ luôn chuẩn là 3)
                rb.linearVelocity = dashDir * dashSpeed;
                rb.angularVelocity = Vector3.zero;

                // B. Quét va chạm
                Vector3 centerPoint = transform.position + Vector3.up * hitOffsetY;
                Collider[] hits = Physics.OverlapSphere(centerPoint, hitRadius, player.dangerLayer);

                // Tính trước vị trí frame tiếp theo để Enemy bám dính tốt hơn
                Vector3 nextFramePos = rb.position + (dashDir * dashSpeed * dt);

                foreach (var hit in hits)
                {
                    Stats enemyStats = hit.GetComponent<Stats>();
                    if (enemyStats != null && enemyStats.currentHp > 0)
                    {
                        // Logic 1: Đẩy
                        HandleContinuousPush(hit.gameObject, dashDir, nextFramePos);

                        // Logic 2: Damage
                        if (!alreadyDamagedList.Contains(hit.gameObject))
                        {
                            alreadyDamagedList.Add(hit.gameObject);
                            ApplyDamage(enemyStats, dashDir);
                        }
                    }
                }

                timer += dt;

                // [FIX] Chờ nhịp vật lý tiếp theo thay vì chờ Frame màn hình
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

    void HandleContinuousPush(GameObject enemyObj, Vector3 dashDir, Vector3 playerNextPos)
    {
        Rigidbody enemyRb = enemyObj.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            // Điểm neo lý tưởng trước mặt Player
            Vector3 idealPos = playerNextPos + (dashDir * pushOffsetDistance);
            idealPos.y = enemyRb.position.y;

            // Dùng MovePosition để ép Enemy vào vị trí đó
            // Lưu ý: MovePosition hoạt động tốt cho cả Kinematic và Dynamic khi muốn teleport mượt
            enemyRb.MovePosition(idealPos);

            // Nếu là Dynamic, reset velocity để nó không bị nảy lung tung
            if (!enemyRb.isKinematic)
            {
                enemyRb.linearVelocity = Vector3.zero;
            }
        }
    }

    private void ApplyDamage(Stats enemyStats, Vector3 dashDir)
    {
        stats.EnterCombat();

        // Tính góc va chạm để xác định impactLevel
        Vector3 impactDir = (enemyStats.transform.position - transform.position);
        impactDir.y = 0;
        impactDir.Normalize();
        float angle = Vector3.Angle(dashDir, impactDir);
        int impactLevel = (angle <= headOnAngle) ? 2 : 1;

        DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, 1f, data, equipmentManager != null ? equipmentManager.currentWeapon : null, impactLevel, true, dashDuration + 0.2f);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * hitOffsetY, hitRadius);
    }
}