using System.Collections;
using UnityEngine;

/// <summary>
/// Handler mặc định cho Sword, Greatsword, Dagger (đánh thường).
/// Geometry khớp 100% với PerformPlayerSweep gốc:
///   - OverlapSphere tại VỊ TRÍ PLAYER, bán kính = FULL attackRange
///   - Angle check hình quạt dùng vị trí ROOT của enemy
///   - Không offset sphere về phía trước (tránh dead-zone sát người)
/// </summary>
public class DefaultMeleeAttackHandler : IWeaponAttackHandler
{
    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        float   range  = ctx.Player.attackRange;
        // Sphere tại player + offset height — giống hệt original PerformPlayerSweep
        // (Physics.OverlapSphere(transform.position, attackRange, ...))
        Vector3 center = ctx.PlayerPos + Vector3.up * 0.5f;

        Collider[] hits  = Physics.OverlapSphere(center, range, ctx.DangerLayer);
        bool       hitAny = false;

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;

            // Angle check hình quạt — dùng vị trí ROOT của enemy
            float angleToEnemy = Vector3.Angle(ctx.FacingDir,
                (enemy.transform.position - ctx.PlayerPos).normalized);
            if (angleToEnemy > ctx.Player.attackAngle / 2f) continue;

            // Dedup: tránh double-hit nếu enemy có nhiều collider
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
