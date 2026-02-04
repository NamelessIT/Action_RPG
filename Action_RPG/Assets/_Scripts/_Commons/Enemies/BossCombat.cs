using UnityEngine;
using UnityEngine.AI; // Cần dùng để điều khiển Agent lướt
using System.Collections;

// Kế thừa từ EnemyCombat để có mọi tính năng cũ
public class BossCombat : EnemyCombat
{
    [Header("--- Boss Skills ---")]
    public float dashCooldown = 5.0f;
    public float dashSpeedMultiplier = 5.0f; // Lướt nhanh gấp 5 lần đi bộ
    public float dashDuration = 0.2f;

    private float lastDashTime = -10f;
    private bool isDashing = false;
    private NavMeshAgent agent; // Boss cần tham chiếu Agent để lướt

    // Override Setup để lấy thêm NavMeshAgent
    public override void Setup(EnemyStats _stats, Transform _target, Animator _animator)
    {
        base.Setup(_stats, _target, _animator);
        agent = GetComponent<NavMeshAgent>();
    }

    // Hàm Dash đặc biệt của Boss
    public void PerformBossDash(Vector3 dashDirection)
    {
        // 1. Check Cooldown Dash
        if (Time.time < lastDashTime + dashCooldown) return;
        if (isDashing) return;

        // 2. --- LOGIC CANCEL ATTACK (Giống Player) ---
        // Nếu Boss đang vung tay đánh mà quyết định lướt -> Hủy đánh
        if (isAttacking)
        {
            CancelAttack(); // Gọi hàm ta vừa viết ở EnemyCombat

            // Effect hình ảnh khi hủy chiêu (nếu có)
            // Instantiate(vfxCancel, transform.position, Quaternion.identity);
        }
        // ---------------------------------------------

        // 3. Thực hiện Dash
        StartCoroutine(DashRoutine(dashDirection));
    }

    IEnumerator DashRoutine(Vector3 dir)
    {
        isDashing = true;
        lastDashTime = Time.time;
        stats.isInvincible = true; // Boss lướt cũng bất tử như Player

        // Setup Animation Dash cho Boss
        if (animator != null) animator.SetTrigger("Dash");

        // Logic Lướt bằng NavMeshAgent
        if (agent != null)
        {
            float originalSpeed = agent.speed;
            float originalAccel = agent.acceleration;

            // Tăng tốc cực đại để lướt
            agent.speed = stats.baseMoveSpeed * dashSpeedMultiplier;
            agent.acceleration = 1000f; // Tăng tốc tức thì
            agent.isStopped = false; // Đảm bảo agent được chạy

            // Set điểm đến là vị trí cách đó 1 đoạn theo hướng Dash
            agent.SetDestination(transform.position + dir * 5.0f);

            yield return new WaitForSeconds(dashDuration);

            // Trả lại tốc độ cũ
            agent.speed = stats.baseMoveSpeed;
            agent.acceleration = originalAccel;
            agent.velocity = Vector3.zero; // Dừng lại

            // Nếu muốn boss đứng lại 1 chút sau khi Dash
            agent.isStopped = true;
        }

        stats.isInvincible = false;
        isDashing = false;
    }
}