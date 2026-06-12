using System.Collections;
using UnityEngine;

/// <summary>
/// Handler cho Spear — đâm thẳng về phía trước (thrust).
/// Sử dụng OverlapCapsule hẹp dọc theo trục nhìn để giống hình cây thương.
/// Gây sát thương 1 lần lên tất cả kẻ địch trên đường đâm.
/// </summary>
public class SpearAttackHandler : IWeaponAttackHandler
{
    // Bán kính capsule — đủ rộng để bắt cả sprite enemy với collider mỏng
    private const float THRUST_RADIUS = 0.4f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ── Hình dạng đâm: Capsule từ gần mặt đất → đầu mũi thương ──────────
        float   range  = ctx.Player.attackRange;
        Vector3 dir    = ctx.FacingDir;
        // origin gần mặt đất để bắt cả enemy đứng thấp (sprite 2D dạng nằm)
        Vector3 origin = ctx.PlayerPos + Vector3.up * 0.15f;
        Vector3 tip    = origin + dir * range;

        // ANIMATOR: // animator.SetTrigger("SpearThrust");

        Collider[] hits = Physics.OverlapCapsule(origin, tip, THRUST_RADIUS, ctx.DangerLayer);
        bool hitAny = false;

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            ctx.ApplyDamage(enemy, ctx.IsHeavy, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, ctx.IsHeavy);
            hitAny = true;
        }

        if (hitAny && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        yield return new WaitForSeconds(ctx.SwingDuration);
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
