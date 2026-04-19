using System.Collections;
using UnityEngine;

/// <summary>
/// Handler mặc định cho Sword, Greatsword, Dagger (đánh thường).
/// Logic sweep GIỐNG HỆT PerformPlayerSweep gốc — đã hoạt động với mọi loại enemy.
/// Chỉ thay CompareTag + GetComponent bằng ctx.GetEnemyStats để hỗ trợ child collider.
/// </summary>
public class DefaultMeleeAttackHandler : IWeaponAttackHandler
{
    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        float elapsed    = 0f;
        float startAngle =  ctx.Player.attackAngle / 2f;
        float endAngle   = -ctx.Player.attackAngle / 2f;
        bool  hitAny     = false;

        while (elapsed < ctx.SwingDuration)
        {
            elapsed += Time.deltaTime;
            float t            = elapsed / ctx.SwingDuration;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

            if (SweepAtAngle(ctx, currentAngle))
                hitAny = true;

            yield return null;
        }

        if (hitAny && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);
    }

    /// <summary>
    /// Quét tại 1 góc — GIỐNG HỆT PerformPlayerSweep gốc về geometry.
    /// checkPos = playerPos + sweepDir * (range * 0.8f), radius = range * 0.5f.
    /// </summary>
    private bool SweepAtAngle(WeaponAttackContext ctx, float angle)
    {
        Quaternion rot      = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3    sweepDir = rot * ctx.FacingDir;
        float      range    = ctx.Player.attackRange;
        float      radius   = range * 0.5f;
        // + Vector3.up * 0.5f để bắt được sprite enemy có collider trên child (giống Dagger)
        Vector3    checkPos = ctx.PlayerPos + sweepDir * (range * 0.8f) + Vector3.up * 0.5f;

        Collider[] hits  = Physics.OverlapSphere(checkPos, radius, ctx.DangerLayer);
        bool       hitNew = false;

        foreach (var hit in hits)
        {
            // GetEnemyStats: hỗ trợ cả collider-trên-child (không cần CompareTag trực tiếp)
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;

            // Angle check dùng vị trí của collider thực tế (như PerformPlayerSweep gốc)
            float angleToEnemy = Vector3.Angle(ctx.FacingDir,
                (hit.transform.position - ctx.PlayerPos).normalized);
            if (angleToEnemy > ctx.Player.attackAngle / 2f) continue;

            // Dedup theo enemy root transform (tránh double-hit nếu enemy có nhiều collider)
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            ctx.ApplyDamage(enemy, ctx.IsHeavy, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, ctx.IsHeavy);
            hitNew = true;
        }
        return hitNew;
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
