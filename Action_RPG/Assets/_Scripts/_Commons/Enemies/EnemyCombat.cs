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
        hitTargets.Clear();
        if (stats != null) stats.EnterCombat();

        // Khóa hướng tấn công ngay lúc bắt đầu đánh — enemy commit vào hướng này
        // Nếu player dash ra sau lưng trong lúc wind-up, đòn sẽ trượt
        Vector3 lockedFacingDir = stats != null ? stats.facingDirection : transform.forward;
        if (target != null)
        {
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            dirToTarget.y = 0;
            if (dirToTarget != Vector3.zero)
            {
                lockedFacingDir = dirToTarget;
                if (stats != null) stats.facingDirection = lockedFacingDir;
            }
        }

        // 2. Trigger Animation
        if (animator != null)
        {
            animator.SetFloat("AttackSpeedMultiplier", stats.baseAttackSpeed);
            animator.SetTrigger("Attack");
        }

        float baseAnimDuration = 0.5f;
        float realAnimDuration = baseAnimDuration / Mathf.Max(stats.baseAttackSpeed, 0.1f);

        // Wind-up 50%: player có đủ thời gian nhìn thấy animation và bấm dash thoát
        // Active 50%-75%: hitbox quét trong cửa sổ hẹp, đòn đánh phải đúng timing
        // Recovery 75%-100%: enemy bị hở sườn, player có thể phản công
        float startDamageTime = realAnimDuration * 0.50f;
        float endDamageTime   = realAnimDuration * 0.75f;
        float swingDuration   = endDamageTime - startDamageTime;

        float startAngle = -attackAngle / 2f;
        float endAngle   = attackAngle / 2f;

        // 3. Wind-up — player nhìn thấy animation và bấm dash trong giai đoạn này
        yield return new WaitForSeconds(startDamageTime);

        // 4. Sweep — dùng lockedFacingDir, không cập nhật theo target nữa
        float currentSweepTime = 0f;
        while (currentSweepTime < swingDuration)
        {
            currentSweepTime += Time.deltaTime;
            float t = currentSweepTime / swingDuration;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
            PerformSweepCheck(currentAngle, lockedFacingDir);
            yield return null;
        }

        // 5. Recovery — enemy hở sườn trong giai đoạn này
        yield return new WaitForSeconds(realAnimDuration - endDamageTime);

        isAttacking = false;
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

    // Dùng lockedFacingDir (hướng đã commit khi bắt đầu đánh) — không đọc stats.facingDirection
    // để tránh hitbox theo dõi player sau khi họ đã dash ra ngoài góc đánh
    void PerformSweepCheck(float angle, Vector3 lockedFacingDir)
    {
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 dirOfSword = rotation * lockedFacingDir;

        Vector3 checkPos = transform.position + dirOfSword * (basicAttackRange * 0.8f);
        float checkRadius = basicAttackRange * 0.4f;

        Collider[] hits = Physics.OverlapSphere(checkPos, checkRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") || hit.CompareTag("Ally"))
            {
                if (!hitTargets.Contains(hit.transform))
                {
                    // Dùng lockedFacingDir để check góc — nếu player dash ra sau lưng, miss
                    Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
                    if (Vector3.Angle(lockedFacingDir, dirToTarget) > attackAngle / 2f) continue;

                    hitTargets.Add(hit.transform);
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