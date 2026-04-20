using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Greatsword: Gồng 1.5 giây → Bổ thẳng trước mặt.
/// • OverlapSphere tại player + angle check (cùng pattern DefaultMeleeAttackHandler).
/// • Góc hẹp hơn attackAngle thông thường để giống cảm giác bổ thẳng.
/// • 200% sát thương + Knockback mạnh.
/// </summary>
public class GreatswordHeavyAttack : IWeaponAttackHandler
{
    // Greatsword slam: góc 60° (hẹp hơn normal attack) — đòn thẳng, uy lực cao
    private const float SLAM_ANGLE = 60f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("GreatswordSlam");

        float   range  = ctx.Player.attackRange;
        Vector3 center = ctx.PlayerPos + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(center, range, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;

            // Góc hẹp SLAM_ANGLE để tạo cảm giác bổ thẳng
            float angleToEnemy = Vector3.Angle(ctx.FacingDir,
                (enemy.transform.position - ctx.PlayerPos).normalized);
            if (angleToEnemy > SLAM_ANGLE / 2f) continue;

            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

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
