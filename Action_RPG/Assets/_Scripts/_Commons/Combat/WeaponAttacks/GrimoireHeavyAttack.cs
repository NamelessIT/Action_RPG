using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Grimoire: Gồng 1 giây → Tìm kẻ địch gần nhất (360°, không cần góc nhìn),
/// giáng sét AoE từ trên trời xuống vị trí đó.
/// • Bán kính AoE: 1f quanh điểm giáng.
/// • 150% sát thương + Stun 1.5s, KHÔNG knockback.
/// </summary>
public class GrimoireHeavyAttack : IWeaponAttackHandler
{
    private const float AOE_RADIUS    = 1.0f;
    private const float STUN_DURATION = 1.5f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("GrimoireHeavy");

        // Tìm mục tiêu gần nhất 360° (không cần góc nhìn)
        Stats primary = RangedAttackHandler.FindNearestTargetAny(ctx);

        Vector3 strikePos = primary != null
            ? primary.transform.position
            : ctx.PlayerPos + ctx.FacingDir * (ctx.Player.attackRange * 0.5f);

        // VFX: // Object.Instantiate(lightningVfxPrefab, strikePos + Vector3.up * 5f, Quaternion.identity);

        yield return new WaitForSeconds(0.1f);

        Collider[] hits = Physics.OverlapSphere(strikePos, AOE_RADIUS, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            // Stun thay vì knockback
            ctx.Player.suppressNextKnockback = true;
            ctx.Player.nextHitStun           = true;
            ctx.Player.nextHitStunDuration   = STUN_DURATION;
            ctx.ApplyDamage(enemy, true, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, true);
        }

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        yield return new WaitForSeconds(Mathf.Max(0f, ctx.SwingDuration - 0.1f));
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
