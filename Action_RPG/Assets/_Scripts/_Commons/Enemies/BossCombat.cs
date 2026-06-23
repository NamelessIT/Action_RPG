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
    private EnemyAI _enemyAI;   // để mượn external move override (tránh đánh nhau agent.speed với EnemyAI.Update)

    // Trạng thái dash cần restore idempotent (mọi exit path + disable)
    private float _dashOriginalAccel;
    private bool _dashAccelStored;

    // Override Setup để lấy thêm NavMeshAgent
    public override void Setup(EnemyStats _stats, Transform _target, Animator _animator)
    {
        base.Setup(_stats, _target, _animator);
        agent = GetComponent<NavMeshAgent>();
        _enemyAI = GetComponent<EnemyAI>();
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
            // Lưu acceleration gốc (idempotent) để EndDash trả lại đúng.
            if (!_dashAccelStored) { _dashOriginalAccel = agent.acceleration; _dashAccelStored = true; }
            agent.acceleration = 1000f; // Tăng tốc tức thì
            agent.isStopped = false;    // Đảm bảo agent được chạy

            // [OWNERSHIP] Mượn override của EnemyAI: tốc độ = base × dashMult × Slow, AI.Update không ghi đè
            // SetDestination/agent.speed trong lúc dash. Fallback set trực tiếp nếu không có EnemyAI.
            if (_enemyAI != null) _enemyAI.BeginExternalMoveOverride(dashSpeedMultiplier);
            else agent.speed = stats.baseMoveSpeed * dashSpeedMultiplier;

            // Set điểm đến là vị trí cách đó 1 đoạn theo hướng Dash
            agent.SetDestination(transform.position + dir * 5.0f);

            yield return new WaitForSeconds(dashDuration);

            if (agent.isOnNavMesh) { agent.velocity = Vector3.zero; agent.isStopped = true; }
        }

        EndDash(); // restore tốc độ/accel/invincibility/override idempotent
    }

    /// <summary>Kết thúc dash idempotent: trả override, acceleration, invincibility, cờ. Gọi được nhiều lần an toàn.</summary>
    private void EndDash()
    {
        if (_enemyAI != null) _enemyAI.EndExternalMoveOverride();
        else if (agent != null && stats != null) agent.speed = stats.baseMoveSpeed;
        if (agent != null && _dashAccelStored) { agent.acceleration = _dashOriginalAccel; _dashAccelStored = false; }
        if (stats != null) stats.isInvincible = false;
        isDashing = false;
    }

    // [CLEANUP] Bị disable/destroy giữa lúc dash → coroutine dừng; vẫn phải trả mọi trạng thái (idempotent).
    protected override void OnDisable()
    {
        base.OnDisable();
        if (isDashing) EndDash();
    }
}
