using System.Collections;
using UnityEngine;

/// <summary>
/// Handler cho Bow và Grimoire (đánh xa).
///
/// Hỗ trợ 2 chế độ qua AttackMode:
/// • Directional (mặc định): đạn bay theo hướng nhìn (stats.facingDirection).
/// • TargetSeek: tìm kẻ địch gần nhất trong tầm + góc 45°, nhắm thẳng vào.
///
/// Heavy Attack (isHeavy = true) được xử lý bởi BowHeavyAttack / GrimoireHeavyAttack
/// thông qua WeaponAttackDispatcher — handler này chỉ lo đánh thường.
/// </summary>
public class RangedAttackHandler : IWeaponAttackHandler
{
    private readonly WeaponData.AttackMode _mode;

    public RangedAttackHandler(WeaponData.AttackMode mode)
    {
        _mode = mode;
    }

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetTrigger("RangedShoot");

        Vector3 spawnPos = ctx.PlayerPos + Vector3.up * ctx.Player.projectileSpawnOffsetY;
        Vector3 fireDir  = ResolveFireDirection(ctx);

        // Đòn cường hóa (MageSkill / TricksterSkill) THAY THẾ đòn đánh thường: nếu đòn này sẽ bị
        // cường hóa tiêu hao thì KHÔNG bắn đạn thường (tránh vừa ra đạn thường vừa ra đòn cường hóa).
        bool empowerReplaces = false;
        foreach (var provider in ctx.Player.GetComponents<IEmpoweredAttackProvider>())
        {
            if (provider.WillEmpowerAttack(ctx.IsHeavy)) { empowerReplaces = true; break; }
        }

        // WPN_BW_T5_01: khi HP < 50% → đánh thường thành Tia Sáng Mặt Trời (hitscan xuyên map, True Damage).
        bool sunBeam = ctx.Stats != null && ctx.Stats.bowSunBeam
                       && ctx.Stats.currentHp < ctx.Stats.maxHp * 0.5f;
        if (!empowerReplaces && sunBeam)
        {
            WeaponEffectManager wfx = ctx.Player.GetComponent<WeaponEffectManager>();
            if (wfx != null) wfx.FireSunBeam(spawnPos, fireDir, ctx.IsHeavy, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, ctx.IsHeavy);
            yield return new WaitForSeconds(ctx.SwingDuration);
            yield break;
        }

        if (!empowerReplaces && ctx.Player.projectilePrefab != null)
        {
            GameObject projObj = Object.Instantiate(ctx.Player.projectilePrefab, spawnPos, Quaternion.identity);
            Projectile proj    = projObj.GetComponent<Projectile>();

            if (proj != null)
            {
                proj.Setup(ctx.Player, fireDir, ctx.Player.attackRange, ctx.IsHeavy, ctx.ComboStep);
                if (ctx.Stats != null && ctx.Stats.projectileSpeedBonus > 0f)
                    proj.speed *= (1f + ctx.Stats.projectileSpeedBonus);

                // WPN_BW_T5_02: đạn biến thành Chim Ánh Trăng tự bẻ cong đuổi địch.
                if (ctx.Stats != null && ctx.Stats.bowHoming) proj.EnableHoming(ctx.DangerLayer);

                // WPN_GR_T4_04: đạn Phi Dao — đòn chính tính vật lý + bonus phép.
                if (ctx.Weapon != null && ctx.Weapon.id.Trim() == "WPN_GR_T4_04") proj.grimoirePhiDao = true;
            }
        }

        // Báo "đã thực hiện 1 đòn đánh" (1 lần/swing) — để các passive theo hướng (MagePassive)
        // phóng kĩ năng đặc biệt N/SW/NW và set cooldown. Đòn xa nên fire 1 lần khi vung,
        // không phụ thuộc có trúng địch hay không.
        ctx.OnHitEvent?.Invoke(ctx.ComboStep, ctx.IsHeavy);

        yield return new WaitForSeconds(ctx.SwingDuration);
    }

    /// <summary>
    /// Directional: hướng nhìn hiện tại.
    /// TargetSeek: tìm kẻ địch gần nhất trong 45° trước mặt.
    /// </summary>
    private Vector3 ResolveFireDirection(WeaponAttackContext ctx)
    {
        if (_mode == WeaponData.AttackMode.TargetSeek)
        {
            Stats target = FindNearestTarget(ctx, seekAngle: 45f);
            if (target != null)
                return (target.transform.position - ctx.PlayerPos).normalized;
        }

        return ctx.FacingDir;
    }

    internal static Stats FindNearestTarget(WeaponAttackContext ctx, float seekAngle)
    {
        Collider[] hits = Physics.OverlapSphere(ctx.PlayerPos, ctx.Player.attackRange, ctx.DangerLayer);
        Stats best    = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Stats s = ctx.GetEnemyStats(hit);
            if (s == null) continue;

            float angle = Vector3.Angle(ctx.FacingDir,
                (s.transform.position - ctx.PlayerPos).normalized);
            if (angle > seekAngle) continue;

            float dist = Vector3.Distance(ctx.PlayerPos, s.transform.position);
            if (dist < minDist) { minDist = dist; best = s; }
        }

        return best;
    }

    /// <summary>
    /// Tìm kẻ địch gần nhất trong tầm — KHÔNG kiểm tra góc nhìn (360°).
    /// Dùng cho Grimoire Heavy: tự tìm target bất kể player quay hướng nào.
    /// </summary>
    internal static Stats FindNearestTargetAny(WeaponAttackContext ctx)
    {
        Collider[] hits = Physics.OverlapSphere(ctx.PlayerPos, ctx.Player.attackRange, ctx.DangerLayer);
        Stats best    = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Stats s = ctx.GetEnemyStats(hit);
            if (s == null) continue;

            float dist = Vector3.Distance(ctx.PlayerPos, s.transform.position);
            if (dist < minDist) { minDist = dist; best = s; }
        }

        return best;
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
