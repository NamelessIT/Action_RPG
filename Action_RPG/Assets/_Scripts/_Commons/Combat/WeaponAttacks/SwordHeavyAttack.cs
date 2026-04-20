using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Sword: 2 nhát chém liên tiếp bằng sweep.
/// • Đòn 1 (BETWEEN_SLASH_DELAY giây đầu): 1.5x sát thương, KHÔNG knockback.
/// • Đòn 2 (sau delay): 1.5x sát thương + Knockback.
/// • Dùng cùng OverlapSphere sweep như DefaultMeleeAttackHandler (đã hoạt động).
/// </summary>
public class SwordHeavyAttack : IWeaponAttackHandler
{
    private const float BETWEEN_SLASH_DELAY = 0.2f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("SwordHeavyDouble");

        // ── Đòn 1: sweep toàn bộ góc, không knockback ───────────────────────
        ctx.HitTargets.Clear();
        SweepArc(ctx, withKnockback: false);

        yield return new WaitForSeconds(BETWEEN_SLASH_DELAY);

        // ── Đòn 2: sweep lại, có knockback ──────────────────────────────────
        ctx.HitTargets.Clear(); // Reset để trúng lại cùng enemy
        SweepArc(ctx, withKnockback: true);

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        float remaining = ctx.SwingDuration - BETWEEN_SLASH_DELAY;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    /// <summary>
    /// Sweep toàn bộ góc attackAngle — geometry giống DefaultMeleeAttackHandler gốc.
    /// Sphere tại player position (không offset forward), bán kính = full attackRange.
    /// </summary>
    private void SweepArc(WeaponAttackContext ctx, bool withKnockback)
    {
        float   range    = ctx.Player.attackRange;
        Vector3 center   = ctx.PlayerPos + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(center, range, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;

            // Angle check hình quạt — dùng vị trí ROOT
            float angleToEnemy = Vector3.Angle(ctx.FacingDir,
                (enemy.transform.position - ctx.PlayerPos).normalized);
            if (angleToEnemy > ctx.Player.attackAngle / 2f) continue;

            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            if (!withKnockback)
                ctx.Player.suppressNextKnockback = true;

            ctx.ApplyDamage(enemy, true, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, true);
        }
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
