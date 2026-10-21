using System.Collections;
using UnityEngine;

/// <summary>
/// Handler đánh thường cho Staff.
/// Tự tìm kẻ địch gần nhất trong tầm VÀ trong góc 45° trước mặt.
/// Gây sát thương đơn mục tiêu.
/// </summary>
public class StaffNormalAttackHandler : IWeaponAttackHandler
{
    private const float SEEK_ANGLE = 45f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("StaffStrike");

        // Phase E2.1: dưới Taunt, Staff không được target-seek sang enemy gần hơn; primary target là Taunter.
        Stats target = ctx.HasForcedPrimaryTarget
            ? ctx.ForcedPrimaryTargetStats
            : RangedAttackHandler.FindNearestTarget(ctx, SEEK_ANGLE);

        if (target != null && !target.isDead && ctx.TryAddHitTarget(target.transform))
        {
            ctx.ApplyDamage(target, ctx.IsHeavy, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, ctx.IsHeavy);

            if (ctx.Stats != null)
                ctx.Stats.GainSinFromAttack(1);
        }

        yield return new WaitForSeconds(ctx.SwingDuration);
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
