using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    protected EnemyStats stats; // Dùng 'protected' để lớp con (Boss) có thể dùng
    protected Transform target; // Player

    [Header("Combat Settings")]
    public float attackCooldown = 2.0f;
    protected float lastAttackTime = -10f;

    // Tầm đánh cơ bản (nếu chưa có skill)
    public float basicAttackRange = 2.0f;

    public virtual void Setup(EnemyStats _stats, Transform _target)
    {
        stats = _stats;
        target = _target;
    }

    // Hàm Update để quản lý Cooldown skill (nếu có)
    public virtual void HandleCombatUpdate()
    {
        // Boss sẽ override hàm này để tính toán combo skill
    }

    // Hàm tấn công cơ bản (Đánh thường)
    public virtual void PerformBasicAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown) return;

        // Logic đánh (Animation, Deal Damage)
        // Animator.SetTrigger("Attack"); (Cần tham chiếu Animator)

        Debug.Log($"{gameObject.name} thực hiện ĐÁNH THƯỜNG vào {target.name}");
        lastAttackTime = Time.time;

        // Tính toán damage (Gọi CombatMath)
        Stats playerStats = target.GetComponent<Stats>();
        if (playerStats != null)
        {
            // [CẬP NHẬT] Truyền playerStats vào đây
            float t = CombatMath.CalculateDirectionFactor(transform, playerStats);
            // Giả sử đánh thường không crit
            float damage = CombatMath.CalculateFullDamage(stats, playerStats, t, false);
            playerStats.TakeDamage(damage);
        }
    }

    // Sau này sẽ có thêm: PerformSkill(string skillID) ...
}