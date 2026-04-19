using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Greatsword: Gồng 1.5 giây → Bổ từ trên cao xuống.
/// • Hitbox: hộp chữ nhật dài 2f, rộng 1f, theo hướng nhìn.
/// • 200% sát thương + Knockback mạnh.
/// </summary>
public class GreatswordHeavyAttack : IWeaponAttackHandler
{
    // Kích thước hộp hit (halfExtents)
    private const float BOX_LENGTH      = 1.0f;   // 2f tổng / 2
    private const float BOX_WIDTH_HALF  = 0.5f;   // 1f tổng / 2
    private const float BOX_HEIGHT_HALF = 0.6f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("GreatswordSlam");

        Vector3    dir      = ctx.FacingDir;
        // + Vector3.up * 0.5f để bắt sprite enemy có collider trên child (giống Dagger)
        Vector3    center   = ctx.PlayerPos + dir * BOX_LENGTH + Vector3.up * (BOX_HEIGHT_HALF + 0.5f);
        Quaternion rotation = Quaternion.LookRotation(dir);
        Vector3    halfExts = new Vector3(BOX_WIDTH_HALF, BOX_HEIGHT_HALF, BOX_LENGTH);

        Collider[] hits = Physics.OverlapBox(center, halfExts, rotation, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
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
