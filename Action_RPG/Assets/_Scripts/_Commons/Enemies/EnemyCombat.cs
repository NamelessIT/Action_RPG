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
            DealDamage();
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

    void DealDamage()
    {
        Debug.Log($"{gameObject.name} thực hiện ĐÁNH THƯỜNG vào {target.name}");

        Stats playerStats = target.GetComponent<Stats>();
        if (playerStats != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, playerStats);
            // Enemy thường mặc định không crit (hoặc thêm logic crit sau)
            float damage = CombatMath.CalculateFullDamage(
                stats,           // Attacker 
                playerStats,     // Target 
                t,               // Direction Factor 
                false,           // IsCrit (Enemy thường mặc định false)
                null,            // SkillData: Để null (Nếu enemy đánh thường)
                null,            // WeaponData: Để null (Logic sẽ tự hiểu là Physical)
                1.0f             // ExternalMult: Mặc định là 1
            );
            playerStats.TakeDamage(damage);
        }
    }

    // Sau này sẽ có thêm: PerformSkill(string skillID) ...
}