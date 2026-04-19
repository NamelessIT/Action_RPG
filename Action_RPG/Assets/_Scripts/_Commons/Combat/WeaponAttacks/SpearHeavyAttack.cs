using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Spear: Gồng 1 giây → Đâm thẳng tới + bước lướt ngắn về phía trước.
/// • Player lướt LUNGE_DISTANCE mét theo hướng nhìn.
/// • OverlapCapsule dọc đường đâm → xuyên thấu MỌI kẻ địch trên đường (kể cả đứng gần).
/// • 150% sát thương + Stun nhẹ 0.1 giây, KHÔNG knockback.
///
/// NOTE: Dùng OverlapCapsule thay SphereCastAll để tránh lỗi Unity:
///       SphereCastAll bỏ qua collider đang overlap với sphere tại origin.
/// </summary>
public class SpearHeavyAttack : IWeaponAttackHandler
{
    private const float THRUST_RADIUS  = 0.4f;   // Bán kính capsule đâm
    private const float LUNGE_DISTANCE = 1.5f;   // Khoảng lướt về phía trước
    private const float LUNGE_DURATION = 0.15f;  // Thời gian lướt (giây)
    private const float STUN_DURATION  = 0.1f;   // Stun nhẹ

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("SpearHeavyThrust");

        Vector3 dir = ctx.FacingDir;

        // ── Lướt về phía trước ──────────────────────────────────────────────
        Rigidbody rb = ctx.Player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float   elapsed   = 0f;
            Vector3 startPos  = ctx.Player.transform.position;
            Vector3 targetPos = new Vector3(
                startPos.x + dir.x * LUNGE_DISTANCE,
                startPos.y,                          // giữ nguyên độ cao
                startPos.z + dir.z * LUNGE_DISTANCE);

            while (elapsed < LUNGE_DURATION)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / LUNGE_DURATION);
                rb.MovePosition(Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t)));
                yield return new WaitForFixedUpdate();
            }
        }

        // ── Damage: OverlapCapsule từ gần đất → đầu mũi thương ──────────────
        // Dùng OverlapCapsule thay SphereCastAll để bắt cả enemy đứng sát player
        Vector3 origin = ctx.Player.transform.position + Vector3.up * 0.15f;
        Vector3 tip    = origin + dir * ctx.Player.attackRange;

        Collider[] hits = Physics.OverlapCapsule(origin, tip, THRUST_RADIUS, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            // Xuyên thấu: stun nhẹ, KHÔNG knockback
            ctx.Player.suppressNextKnockback = true;
            ctx.Player.nextHitStun           = true;
            ctx.Player.nextHitStunDuration   = STUN_DURATION;
            ctx.ApplyDamage(enemy, true, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, true);
        }

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        float remaining = ctx.SwingDuration - LUNGE_DURATION;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
