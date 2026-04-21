using GLTFast.Schema;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    protected EnemyStats stats; // Dùng 'protected' để lớp con (Boss) có thể dùng
    protected Transform target; // Player
    protected Animator animator;

    [Header("Combat Settings")]
    public bool isAttacking = false; // [MỚI] Trạng thái đang đánh
    protected float lastAttackTime = -10f;
    // Tầm đánh cơ bản (nếu chưa có skill)
    public float basicAttackRange = 2.0f;
    [Range(0, 360)] public float attackAngle = 90f; // [MỚI] Góc đánh
    private int currentComboStep = 0;
    private int maxCombo = 2;


    // [MỚI] Biến này để lưu Coroutine đánh, giúp Boss có thể Cancel
    protected Coroutine currentAttackCoroutine;

    private List<Transform> hitTargets = new List<Transform>();

    public virtual void Setup(EnemyStats _stats, Transform _target, Animator _animator)
    {
        stats = _stats;
        target = _target;
        animator = _animator;
    }


    // Hàm Update để quản lý Cooldown skill (nếu có)
    public virtual void HandleCombatUpdate()
    {
        // Boss sẽ override hàm này để tính toán combo skill
    }


    public virtual void PerformBasicAttack()
    {
        if (stats == null || target == null) return;
        if (isAttacking) return;

        // Tính Cooldown
        float speed = stats.baseAttackSpeed;
        if (speed <= 0) speed = 0.25f;
        float cooldownTime = 1.0f / speed;

        if (Time.time < lastAttackTime + cooldownTime) return;

        StartCoroutine(EnemyAttackRoutine());
    }

protected IEnumerator EnemyAttackRoutine()
    {
        // 1. Setup ban đầu
        isAttacking = true;
        lastAttackTime = Time.time;
        hitTargets.Clear(); // [QUAN TRỌNG] Reset danh sách nạn nhân mới
        if (stats != null) stats.EnterCombat();

        // Xoay mặt về hướng Target
        if (target != null)
        {
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            dirToTarget.y = 0;
            if (dirToTarget != Vector3.zero) stats.facingDirection = dirToTarget;
        }

        // 2. Trigger Animation
        if (animator != null)
        {
            animator.SetFloat("AttackSpeedMultiplier", stats.baseAttackSpeed);
            animator.SetTrigger("Attack");
        }

        // --- [LOGIC MỚI] CẤU HÌNH TIMING CHO ĐÒN QUÉT ---
        float baseAnimDuration = 0.5f; // Thời lượng anim gốc (Ví dụ)
        float realAnimDuration = baseAnimDuration / stats.baseAttackSpeed;

        // Định nghĩa giai đoạn chém:
        // - Wind-up (Giơ tay): 30% đầu
        // - Active Swing (Vung kiếm gây damage): Từ 30% đến 60%
        // - Recovery (Thu tay): 40% còn lại
        float startDamageTime = realAnimDuration * 0.3f; 
        float endDamageTime   = realAnimDuration * 0.6f;
        float swingDuration   = endDamageTime - startDamageTime;

        // Góc chém: Quét từ Trái (-Góc/2) sang Phải (+Góc/2)
        // (Hoặc ngược lại tùy animation, ở đây giả sử chém từ trái sang phải)
        float startAngle = -attackAngle / 2f; 
        float endAngle   = attackAngle / 2f;

        // 3. Chờ giai đoạn Wind-up (Giơ tay lên - Chưa gây damage)
        // Đây là lúc người chơi nhìn thấy để chuẩn bị Parry
        yield return new WaitForSeconds(startDamageTime);

        // 4. [QUAN TRỌNG] VÒNG LẶP QUÉT (SWEEPING LOOP)
        float currentSweepTime = 0f;

        // Chạy vòng lặp trong suốt thời gian vung kiếm
        while (currentSweepTime < swingDuration)
        {
            currentSweepTime += Time.deltaTime;
            
            // Tính phần trăm tiến trình chém (0.0 -> 1.0)
            float t = currentSweepTime / swingDuration;

            // Tính góc hiện tại của cây kiếm theo t (Lerp từ góc bắt đầu đến kết thúc)
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

            // Thực hiện kiểm tra va chạm tại góc này
            PerformSweepCheck(currentAngle);

            // Chờ Frame tiếp theo để quét tiếp
            yield return null; 
        }

        // 5. Recovery (Chờ nốt animation)
        // (realDuration - endDamageTime) là thời gian còn lại
        yield return new WaitForSeconds(realAnimDuration - endDamageTime);

        isAttacking = false;
        
        // Tăng combo
        currentComboStep++;
        if (currentComboStep >= maxCombo) currentComboStep = 0;
    }

    // [MỚI] Hàm hỗ trợ Cancel Attack (Để Boss dùng)
    public void CancelAttack()
    {
        if (isAttacking && currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            isAttacking = false;

            // Reset Animation trigger nếu cần
            if (animator != null) animator.ResetTrigger("Attack");

            Debug.Log($"{gameObject.name} đã HỦY đòn đánh!");
        }
    }

    // Hàm kiểm tra va chạm và gây damage
    //void CheckHitAndDealDamage()
    //{
    //    // Tìm tất cả đối tượng trong tầm đánh (Sphere)
    //    Collider[] hits = Physics.OverlapSphere(transform.position, basicAttackRange);

    //    foreach (var hit in hits)
    //    {
    //        // Chỉ quan tâm đến Player hoặc Ally
    //        if (hit.CompareTag("Player") || hit.CompareTag("Ally"))
    //        {
    //            // [MỚI] CHECK GÓC (CONE CHECK)
    //            Vector3 dirToHit = (hit.transform.position - transform.position).normalized;
    //            Vector3 facingDir = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;

    //            float angle = Vector3.Angle(facingDir, dirToHit);

    //            // Nếu nằm ngoài góc đánh -> Bỏ qua
    //            if (angle > attackAngle / 2f) continue;

    //            // Nếu thỏa mãn -> Gây damage
    //            DealDamageToTarget(hit.transform, currentComboStep);
    //        }
    //    }
    //}

    // Hàm quét tại một góc cụ thể (Thay thế CheckHitAndDealDamage)
    void PerformSweepCheck(float angle)
    {
        // 1. Xác định hướng mặt của Enemy (Trục giữa của hình quạt)
        Vector3 enemyFacingDir = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;

        // 2. Tính hướng của "lưỡi kiếm" tại thời điểm quét này
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 dirOfSword = rotation * enemyFacingDir;

        // 3. Vị trí quét
        Vector3 checkPos = transform.position + dirOfSword * (basicAttackRange * 0.8f);

        // [TINH CHỈNH] Nên để bán kính phụ thuộc vào tầm đánh để không bị quá to
        float checkRadius = basicAttackRange * 0.5f; // Hoặc để 1.0f nếu tầm đánh của quái luôn lớn

        // 4. Kiểm tra va chạm
        Collider[] hits = Physics.OverlapSphere(checkPos, checkRadius);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") || hit.CompareTag("Ally"))
            {
                // Kiểm tra xem nạn nhân này đã bị chém trúng trong lần vung này chưa?
                if (!hitTargets.Contains(hit.transform))
                {
                    // --- [FIX QUAN TRỌNG] THÊM LẠI CHECK GÓC CHO ENEMY ---
                    // Ngăn chặn việc đánh trúng sau lưng do Sphere quá to

                    Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;

                    // Tính góc giữa "Mặt Enemy" và "Mục tiêu"
                    float angleToTarget = Vector3.Angle(enemyFacingDir, dirToTarget);

                    // Nếu góc lệch lớn hơn một nửa góc đánh -> Nằm ngoài hình quạt -> Bỏ qua
                    if (angleToTarget > attackAngle / 2f)
                    {
                        continue;
                    }
                    // ----------------------------------------------------

                    hitTargets.Add(hit.transform); // Đánh dấu đã trúng

                    // Gây damage ngay lập tức
                    DealDamageToTarget(hit.transform, currentComboStep);
                }
            }
        }
    }

    // Hàm tính toán và gửi damage
    void DealDamageToTarget(Transform victim, int step)
    {
        Debug.Log($"{gameObject.name} CHÉM TRÚNG {victim.name}");

        Stats victimStats = victim.GetComponent<Stats>();
        if (victimStats != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, victimStats);

            // --- BƯỚC 1: INFO ---
            DamageInfo info = new DamageInfo();
            info.sourcePosition = transform.position;
            info.attacker = stats;
            info.impactLevel = stats.monsterRank;

            // --- BƯỚC 2: HIỆU ỨNG (CC) ---
            // Boss Golem
            if (stats.enemyID == "Boss_Golem")
            {
                info.isKnockback = true;
                info.knockbackForce = 12f;
            }
            // Orc Warrior (Đòn cuối combo)
            if (stats.enemyID == "Orc_Warrior" && step == 2)
            {
                info.isStun = true;
                info.stunDuration = 1.0f;
                info.isKnockback = true;
                info.knockbackForce = 8f;
            }
            // Odo (Đòn 2)
            if (stats.enemyID == "Odo" && step == 1)
            {
                info.isKnockback = true;
                info.knockbackForce = 8f;
            }

            // --- BƯỚC 3: TÍNH DAMAGE ---
            bool isCrit = CombatMath.CheckIsCrit(stats.baseCritChance);
            info.isCrit = isCrit;
            if (isCrit) Debug.Log($"<color=orange>{gameObject.name} CRITS!</color>");

            // Gọi CombatMath (Nhớ cập nhật tham số ignoreReduction nếu cần, ở đây Enemy thường ko có True Damage nên để false)
            var dmgTuple = CombatMath.CalculateFullDamage(
                stats,
                victimStats,
                t,
                isCrit,
                null,
                null,
                1.0f,
                false // Enemy đánh thường không xuyên giáp
            );

            info.physDamage = dmgTuple.phys;
            info.magicDamage = dmgTuple.magic;
            info.trueDamage = dmgTuple.trueDmg;

            // --- BƯỚC 4: GỬI ---
            victimStats.TakeDamage(info);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Vẽ Gizmos để debug tầm đánh của Enemy
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, basicAttackRange);

        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        Vector3 leftRay = Quaternion.AngleAxis(-attackAngle / 2, Vector3.up) * forward;
        Vector3 rightRay = Quaternion.AngleAxis(attackAngle / 2, Vector3.up) * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, leftRay * basicAttackRange);
        Gizmos.DrawRay(transform.position, rightRay * basicAttackRange);
    }

    // Sau này sẽ có thêm: PerformSkill(string skillID) ...
}