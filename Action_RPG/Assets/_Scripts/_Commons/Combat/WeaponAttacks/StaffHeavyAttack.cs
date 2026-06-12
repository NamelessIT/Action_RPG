using System.Collections;
using UnityEngine;

/// <summary>
/// Heavy Attack — Staff: Channeled spin.
/// • Gồng ≥ 0.5 giây → vào trạng thái xoay trượng.
/// • Gây 150% sát thương mỗi 0.5 giây, bán kính 1.5f.
/// • Tốn 20 stamina/giây; tự dừng khi hết stamina.
/// • Dừng khi thả chuột (WeaponAttackDispatcher.StopChanneled).
/// </summary>
public class StaffHeavyAttack : IWeaponAttackHandler
{
    private const float SPIN_RADIUS        = 1.5f;
    private const float TICK_INTERVAL      = 0.5f;
    private const float STAMINA_COST_PER_S = 20f;

    private Coroutine _spinCoroutine;
    private bool      _isSpinning;

    public bool IsChanneled => true;

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner)
    {
        _isSpinning    = true;
        _spinCoroutine = owner.StartCoroutine(SpinRoutine(ctx));
        return _spinCoroutine;
    }

    public void StopChanneled()
    {
        _isSpinning = false;
        // WeaponAttackDispatcher.StopChanneled() reset isAttacking sau khi StopCoroutine
    }

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        yield break; // Channeled — không dùng ExecuteSwing
    }

    private IEnumerator SpinRoutine(WeaponAttackContext ctx)
    {
        // ANIMATOR: // ctx.Animator.SetBool("StaffSpin", true);
        ctx.Player.isAttacking    = true;
        ctx.Player.isStaffSpinning = true; // Cho phép di chuyển 50% tốc độ khi spin

        float tickTimer = 0f;

        while (_isSpinning)
        {
            bool hasStamina = ctx.Stats.TryConsumeStamina(STAMINA_COST_PER_S * Time.deltaTime);
            if (!hasStamina) break; // Hết stamina → tự dừng

            tickTimer += Time.deltaTime;
            if (tickTimer >= TICK_INTERVAL)
            {
                tickTimer = 0f;
                ctx.HitTargets.Clear(); // Reset để tick tiếp theo trúng lại kẻ cũ
                DealSpinDamage(ctx);
            }

            yield return null;
        }

        // ANIMATOR: // ctx.Animator.SetBool("StaffSpin", false);
        // Lưu ý: nếu coroutine bị StopCoroutine, đoạn này không chạy.
        // WeaponAttackDispatcher.StopChanneled() đã xử lý reset isAttacking + isStaffSpinning.
        ctx.Player.isAttacking     = false;
        ctx.Player.isStaffSpinning = false;
        _isSpinning = false;
    }

    private void DealSpinDamage(WeaponAttackContext ctx)
    {
        Vector3    center = ctx.PlayerPos + Vector3.up * 0.5f;
        Collider[] hits   = Physics.OverlapSphere(center, SPIN_RADIUS, ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            ctx.Player.nextHitKnockbackForce = 5f; // Spin nhẹ hơn đòn heavy thường (15f → 5f)
            ctx.ApplyDamage(enemy, true, ctx.ComboStep);
            ctx.OnHitEvent?.Invoke(ctx.ComboStep, true);
        }

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);
    }
}
