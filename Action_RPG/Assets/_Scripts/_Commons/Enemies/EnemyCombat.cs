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
    public bool isAttacking = false;
    protected float lastAttackTime = -10f;
    // Expose để EnemyAI đọc cooldown state
    public float LastAttackTime => lastAttackTime;

    public float basicAttackRange = 2.0f;
    [Range(0, 360)] public float attackAngle = 90f;

    [Header("Sweep Angles (tính từ hướng nhìn, âm=trái, dương=phải)")]
    [Tooltip("Góc bắt đầu quét. Ví dụ: 0 = từ thẳng trước, -45 = từ trái")]
    public float sweepStartAngle = -45f;
    [Tooltip("Góc kết thúc quét. Ví dụ: 45 = ra phải. Đặt 0→45 cho chém một chiều")]
    public float sweepEndAngle = 45f;

    [Header("Telegraph — Báo hiệu trước khi đánh")]
    [Tooltip("Bật/tắt animation báo hiệu. Tắt = tấn công ngay không cảnh báo.")]
    public bool useTelegraph = true;

    [Tooltip("Thời gian thực hiện animation báo hiệu (giây). Đây là window để player né đòn.")]
    public float telegraphDuration = 1.0f;

    [Tooltip("Tên Animator Trigger để kích hoạt animation báo hiệu.\n" +
             "Ví dụ: 'WindUp' (kiếm sĩ giơ tay), 'ChargeUp' (pháp sư tụ lực), 'Roar' (boss gầm).\n" +
             "Để TRỐNG nếu chỉ muốn đứng im không animation.")]
    public string telegraphAnimTrigger = "WindUp";

    [Tooltip("Tên Animator Bool để set TRUE suốt thời gian telegraph (optional).\n" +
             "Dùng khi animation báo hiệu dài/blend (VD: 'IsCharging').\n" +
             "Để TRỐNG nếu dùng Trigger.")]
    public string telegraphAnimBool = "";

    [Header("Attack Timing — chỉnh cảm giác đòn đánh")]
    [Tooltip("Độ dài animation đánh gốc (giây) trước khi chia cho attackSpeed.")]
    public float baseAttackAnimDuration = 0.5f;
    [Tooltip("Mốc BẮT ĐẦU cửa sổ gây damage (0-1 theo độ dài anim). VD 0.5 = nửa sau.")]
    [Range(0f, 1f)] public float damageWindowStartNormalized = 0.50f;
    [Tooltip("Mốc KẾT THÚC cửa sổ gây damage (0-1). Phải > start.")]
    [Range(0f, 1f)] public float damageWindowEndNormalized = 0.75f;

    // Trạng thái telegraph — EnemyAI đọc để block movement
    [HideInInspector] public bool isTelegraphing = false;

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

        currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine());
    }

protected IEnumerator EnemyAttackRoutine()
    {
        // 1. Setup ban đầu
        isAttacking = true;
        lastAttackTime = Time.time;
        hitTargets.Clear();
        if (stats != null) stats.EnterCombat();

        // Khóa hướng ngay khi bắt đầu chuỗi tấn công — commit từ pha telegraph
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

        // ── TELEGRAPH PHASE ───────────────────────────────────────────────────
        // Enemy gồng / tụ lực / làm động tác báo hiệu — player có window để né
        // isAttacking đã = true → EnemyAI sẽ stop movement trong giai đoạn này
        if (useTelegraph && telegraphDuration > 0f)
        {
            isTelegraphing = true;

            if (animator != null)
            {
                // Set THẬT nếu Animator có parameter tương ứng (safe-set, không crash nếu thiếu).
                // Ưu tiên Bool (animation blend dài), nếu không thì dùng Trigger.
                if (!string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                    animator.SetBool(telegraphAnimBool, true);
                else if (!string.IsNullOrEmpty(telegraphAnimTrigger) && HasParameter(animator, telegraphAnimTrigger))
                    animator.SetTrigger(telegraphAnimTrigger);
            }

            yield return new WaitForSeconds(telegraphDuration);

            isTelegraphing = false;

            // Tắt Bool nếu đã bật (safe-set)
            if (animator != null && !string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                animator.SetBool(telegraphAnimBool, false);
        }
        // ─────────────────────────────────────────────────────────────────────

        // 2. Trigger Attack Animation (sau khi telegraph xong)
        if (animator != null)
        {
            animator.SetFloat("AttackSpeedMultiplier", stats.baseAttackSpeed);
            animator.SetTrigger("Attack");
        }

        float realAnimDuration = baseAttackAnimDuration / Mathf.Max(stats.baseAttackSpeed, 0.1f);

        // Cửa sổ gây damage (chỉnh qua Inspector). Clamp hợp lệ: 0 <= start < end <= 1.
        float startN = Mathf.Clamp01(damageWindowStartNormalized);
        float endN   = Mathf.Clamp01(damageWindowEndNormalized);
        if (endN <= startN) endN = Mathf.Min(1f, startN + 0.05f); // đảm bảo end > start

        // Wind-up [0..start]: player nhìn thấy & né. Active [start..end]: hitbox quét. Recovery [end..1]: hở sườn.
        float startDamageTime = realAnimDuration * startN;
        float endDamageTime   = realAnimDuration * endN;
        float swingDuration   = endDamageTime - startDamageTime;

        // 3. Wind-up — player nhìn thấy animation và bấm dash trong giai đoạn này
        yield return new WaitForSeconds(startDamageTime);

        // 4. Sweep — quét từ sweepStartAngle đến sweepEndAngle theo từng frame
        // Hit chỉ register khi góc quét "vượt qua" đúng góc mà player đứng
        float currentSweepTime = 0f;
        float prevAngle = sweepStartAngle;
        while (currentSweepTime < swingDuration)
        {
            currentSweepTime += Time.deltaTime;
            float t = Mathf.Clamp01(currentSweepTime / swingDuration);
            float currentAngle = Mathf.Lerp(sweepStartAngle, sweepEndAngle, t);
            PerformSweepCheck(currentAngle, prevAngle, lockedFacingDir);
            prevAngle = currentAngle;
            yield return null;
        }

        // 5. Recovery — enemy hở sườn trong giai đoạn này
        yield return new WaitForSeconds(realAnimDuration - endDamageTime);

        isAttacking = false;
        currentComboStep++;
        if (currentComboStep >= maxCombo) currentComboStep = 0;
        currentAttackCoroutine = null; // routine kết thúc bình thường
    }

    // [MỚI] Hàm hỗ trợ Cancel Attack (Để Boss dùng)
    public void CancelAttack()
    {
        // Không có gì để hủy
        if (currentAttackCoroutine == null && !isAttacking && !isTelegraphing) return;

        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }

        isAttacking = false;

        // Đang trong pha telegraph thì StopCoroutine không tự dọn — reset thủ công (safe-set)
        if (isTelegraphing)
        {
            isTelegraphing = false;
            if (animator != null && !string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                animator.SetBool(telegraphAnimBool, false);
        }

        // Reset Animation trigger nếu cần
        if (animator != null) animator.ResetTrigger("Attack");

        Debug.Log($"{gameObject.name} đã HỦY đòn đánh!");
    }

    /// <summary>True nếu Animator có parameter tên paramName (an toàn — set vào param không tồn tại sẽ spam warning).</summary>
    protected static bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in anim.parameters)
            if (p.name == paramName) return true;
        return false;
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

    // Sweep-past detection: hit chỉ register khi góc quét đi qua đúng góc mà target đứng.
    // Ví dụ: player ở 30°, sweep từ 20°→32° trong frame này → HIT.
    // Nếu player dash ra khỏi range trước frame đó → MISS (vì dist check fail).
    void PerformSweepCheck(float currentAngle, float prevAngle, Vector3 lockedFacingDir)
    {
        // Quét toàn bộ range một lần, check từng target
        Collider[] hits = Physics.OverlapSphere(transform.position, basicAttackRange);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player") && !hit.CompareTag("Ally")) continue;
            if (hitTargets.Contains(hit.transform)) continue;

            // 1. Phải trong tầm đánh thực tế
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist > basicAttackRange) continue;

            // 2. Tính góc SIGNED của target so với lockedFacingDir (trong mặt phẳng XZ)
            Vector3 toTarget = (hit.transform.position - transform.position);
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) continue;
            float targetAngle = Vector3.SignedAngle(lockedFacingDir, toTarget.normalized, Vector3.up);

            // 3. Hit khi sweep vượt qua góc của target trong frame này
            // Cả 2 chiều (prevAngle→currentAngle âm hoặc dương)
            bool sweptPast;
            if (currentAngle >= prevAngle)
                sweptPast = targetAngle > prevAngle && targetAngle <= currentAngle;
            else
                sweptPast = targetAngle < prevAngle && targetAngle >= currentAngle;

            if (!sweptPast) continue;

            hitTargets.Add(hit.transform);
            DealDamageToTarget(hit.transform, currentComboStep);
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

            // [COMBAT FEEL] Enemy đánh trúng Player/Ally → feedback 2 chiều.
            // Chỉ kích khi victim là Player/Ally (không rung khi đánh quái khác).
            if (victim.CompareTag("Player") || victim.CompareTag("Ally"))
            {
                CombatFeel.HitStrength strength =
                    isCrit ? CombatFeel.HitStrength.Crit :
                    (info.isKnockback || info.isStun || stats.monsterRank >= 1) ? CombatFeel.HitStrength.Heavy :
                    CombatFeel.HitStrength.Normal;
                CombatFeel.OnHit(strength, victim.name);
                // Hit flash do HitFlash tự lắng nghe OnDamageReceived (1 nguồn) — không gọi Flash() ở đây.
            }
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