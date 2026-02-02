using GLTFast.Schema;
using System.Collections;
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

    private int currentComboStep = 0;
    private int maxCombo = 2;


    // [MỚI] Biến này để lưu Coroutine đánh, giúp Boss có thể Cancel
    protected Coroutine currentAttackCoroutine;

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

    // Hàm tấn công cơ bản (Đánh thường)


    public virtual void PerformBasicAttack()
    {
        if (stats == null || target == null) return;
        if (isAttacking) return; // Đang đánh thì không đánh đè

        // [LOGIC MỚI] Tính Cooldown dựa trên Attack Speed của Enemy
        // Công thức giống Player: 1 / Speed
        float speed = stats.baseAttackSpeed;
        if (speed <= 0) speed = 0.25f;
        float cooldownTime = 1.0f / speed;

        if (Time.time < lastAttackTime + cooldownTime) return;

        // Bắt đầu chuỗi hành động đánh
        currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine());
    }

    // [MỚI] Coroutine xử lý Animation và Gây Damage (Giống Player)
    protected IEnumerator EnemyAttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        if (stats != null) stats.EnterCombat();


        // 1. Xoay mặt về phía Player ngay trước khi đánh để chính xác hơn
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        dirToTarget.y = 0; // Giữ thăng bằng
        if (dirToTarget != Vector3.zero)
        {
            stats.facingDirection = dirToTarget;
            // Nếu muốn xoay model ngay lập tức:
            // transform.rotation = Quaternion.LookRotation(dirToTarget);
        }

        // 2. Chạy Animation
        if (animator != null)
        {
            // Set tốc độ animation khớp với tốc độ đánh
            animator.SetFloat("AttackSpeedMultiplier", stats.baseAttackSpeed);
            animator.SetTrigger("Attack");
        }

        // 3. Chờ giai đoạn "Vung tay" (Wind-up)
        // Giả sử animation chuẩn dài 0.5s. Ta chờ khoảng 30-40% thời gian để đòn đánh chạm mục tiêu
        float baseAnimDuration = 0.5f;
        float realDuration = baseAnimDuration / stats.baseAttackSpeed;

        // Chờ 40% thời gian animation rồi mới gây damage (tùy chỉnh số này cho khớp hình ảnh)
        yield return new WaitForSeconds(realDuration * 0.4f);

        // 4. Gây Damage (Kiểm tra lại khoảng cách cho chắc ăn)
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= basicAttackRange + 0.5f) // +0.5f du di một chút
        {
            currentComboStep++;
            if (currentComboStep >= maxCombo) currentComboStep = 0;
            DealDamage(currentComboStep);
        }

        // 5. Chờ nốt phần còn lại của Animation (Recovery)
        yield return new WaitForSeconds(realDuration * 0.6f);

        isAttacking = false;
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

    // Thay thế hàm DealDamage cũ
    void DealDamage(int step)
    {
        Debug.Log($"{gameObject.name} thực hiện ĐÁNH THƯỜNG vào {target.name}");

        Stats playerStats = target.GetComponent<Stats>();
        if (playerStats != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, playerStats);

            // --- BƯỚC 1: KHỞI TẠO DAMAGE INFO ---
            DamageInfo info = new DamageInfo();
            info.sourcePosition = transform.position;
            info.isCrit = false;
            info.isKnockback = false;
            info.isStun = false;

            // --- BƯỚC 2: CẤU HÌNH HIỆU ỨNG CỦA ENEMY ---
            // Cách đơn giản: Check tên hoặc ID của Enemy để gán hiệu ứng

            // Ví dụ 1: Boss Golem -> Mọi đòn đánh đều gây Knockback
            if (stats.enemyID == "Boss_Golem")
            {
                info.isKnockback = true;
                info.knockbackForce = 12f; // Lực đẩy của Boss
            }

            // Ví dụ 2: Orc -> Đòn thứ 3 (kết thúc combo) gây Stun + Knockback
            // currentComboStep được tính ở Coroutine EnemyAttackRoutine
            if (stats.enemyID == "Orc_Warrior" && currentComboStep == 2) // Giả sử maxCombo=3, index 0,1,2
            {
                info.isStun = true;
                info.stunDuration = 1.0f;
                info.isKnockback = true;
                info.knockbackForce = 8f;
                Debug.Log("Orc thực hiện đòn đập mạnh gây choáng!");
            }

            // --- BƯỚC 3: TÍNH CRIT & DAMAGE ---
            bool isCrit = CombatMath.CheckIsCrit(stats.baseCritChance);
            if (isCrit) Debug.Log($"<color=orange>{gameObject.name} CRITS!</color>");

            info.isCrit = isCrit;

            float damage = CombatMath.CalculateFullDamage(
                stats,
                playerStats,
                t,
                isCrit,
                null,
                null,
                1.0f
            );

            info.damageAmount = damage;

            // --- BƯỚC 4: GỬI GÓI TIN ---
            playerStats.TakeDamage(info);
        }
    }

    // Sau này sẽ có thêm: PerformSkill(string skillID) ...
}