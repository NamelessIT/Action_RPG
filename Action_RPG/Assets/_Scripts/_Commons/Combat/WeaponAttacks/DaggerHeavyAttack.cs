using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Dagger: Gồng 0.5 giây → Xoay dao 360° quanh người.
/// • AoE tròn bán kính = attackRange của Dagger.
/// • 200% sát thương, KHÔNG knockback (quay tròn không có lực đẩy hướng).
/// </summary>
public class DaggerHeavyAttack : IWeaponAttackHandler
{
    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("DaggerSpin");

        float   spinRadius = ctx.Player.attackRange;
        Vector3 center     = ctx.PlayerPos + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(center, spinRadius, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            // Dagger spin: không knockback
            ctx.Player.suppressNextKnockback = true;
            ctx.ApplyDamage(enemy, true, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, true);
        }

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        yield return new WaitForSeconds(ctx.SwingDuration);
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
