using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Bow: Gồng 1 giây (có thể di chuyển -50% tốc độ).
/// Khi thả: bắn mũi tên xuyên thấu tối đa 3 kẻ địch.
///
/// Visual projectile được spawn (visualOnly=true) để thấy mũi tên bay.
/// Damage xử lý bằng SphereCastAll tức thì.
/// </summary>
public class BowHeavyAttack : IWeaponAttackHandler
{
    private const int   MAX_PIERCE_TARGETS = 3;
    private const float ARROW_RADIUS       = 0.15f;

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("BowHeavyShoot");

        Vector3 origin   = ctx.PlayerPos + Vector3.up * ctx.Player.projectileSpawnOffsetY;
        Vector3 dir      = ctx.FacingDir;
        float   distance = ctx.Player.attackRange;

        // ── Spawn visual projectile (visualOnly=true — không gây damage qua Trigger) ──
        UnityEngine.GameObject prefab = ctx.Weapon?.heavyProjectilePrefab ?? ctx.Player.projectilePrefab;
        if (prefab != null)
        {
            UnityEngine.GameObject projObj = UnityEngine.Object.Instantiate(
                prefab, origin, UnityEngine.Quaternion.LookRotation(dir));
            Projectile proj = projObj.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.visualOnly = true;
                proj.Setup(ctx.Player, dir, distance, true, ctx.ComboStep);
            }
        }

        // ── Damage tức thì — SphereCastAll xuyên thấu ───────────────────────
        RaycastHit[] hits = Physics.SphereCastAll(origin, ARROW_RADIUS, dir, distance, ctx.DangerLayer);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int piercedCount = 0;
        foreach (var hit in hits)
        {
            if (piercedCount >= MAX_PIERCE_TARGETS) break;

            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            ctx.ApplyDamage(enemy, true, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, true);
            piercedCount++;
        }

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        yield return new WaitForSeconds(ctx.SwingDuration);
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
