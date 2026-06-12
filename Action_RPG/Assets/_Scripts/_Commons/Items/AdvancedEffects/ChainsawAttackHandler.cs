using System.Collections;
using UnityEngine;

public class ChainsawAttackHandler : IWeaponAttackHandler
{
    private const float TICK_INTERVAL = 0.2f; // Thời gian gồng/nhịp chém
    private Coroutine _spinCoroutine;
    private bool _isSpinning;
    private int _tickCount = 0; // Đếm số nhịp chém

    public bool IsChanneled => true;

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner)
    {
        _isSpinning = true;
        _tickCount = 0;
        _spinCoroutine = owner.StartCoroutine(SpinRoutine(ctx));
        return _spinCoroutine;
    }

    public void StopChanneled() { _isSpinning = false; }
    public IEnumerator ExecuteSwing(WeaponAttackContext ctx) { yield break; }

    private IEnumerator SpinRoutine(WeaponAttackContext ctx)
    {
        float tickTimer = 0f;
        while (_isSpinning)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= TICK_INTERVAL)
            {
                tickTimer = 0f;
                ctx.HitTargets.Clear();
                DealChainsawDamage(ctx);
            }
            yield return null;
        }
        _isSpinning = false;
    }

    private void DealChainsawDamage(WeaponAttackContext ctx)
    {
        _tickCount++;
        bool triggerOnHitEvent = (_tickCount % 3 == 0); // Mỗi 3 hit thì trigger OnHit

        // HITBOX: Dài 2f, Rộng 0.4f. Tâm (center) nằm cách người 1f.
        Vector3 center = ctx.PlayerPos + Vector3.up * 0.5f + ctx.FacingDir * 1.0f;
        Vector3 halfExtents = new Vector3(0.2f, 0.5f, 1.0f); // Rộng tổng 0.4, Dài tổng 2.0

        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.LookRotation(ctx.FacingDir), ctx.DangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null) continue;
            if (!ctx.TryAddHitTarget(enemy.transform)) continue;

            // TÍNH TOÁN COMBATMATH CHUẨN (HƯỚNG + CRIT)
            float t = CombatMath.CalculateDirectionFactor(ctx.Player.transform, enemy);
            float totalCrit = ctx.Stats.critChance + (ctx.Weapon != null ? ctx.Weapon.bonusCritChance : 0);
            bool isC = CombatMath.CheckIsCrit(totalCrit);

            var dmgTuple = CombatMath.CalculateFullDamage(ctx.Stats, enemy, t, isC, null, ctx.Weapon, 0.2f); // Base 20%

            DamageInfo info = new DamageInfo
            {
                sourcePosition = ctx.PlayerPos,
                attacker = ctx.Stats,
                physDamage = dmgTuple.phys,
                magicDamage = dmgTuple.magic + (ctx.Stats.magicAtk * 0.5f), // Gây thêm 50% MagicAtk
                trueDamage = dmgTuple.trueDmg,
                isCrit = isC
            };
            enemy.TakeDamage(info);

            // Kích hoạt Effect OnHit của khiên/passive mỗi 3 hit
            if (triggerOnHitEvent)
            {
                ctx.OnHitEvent?.Invoke(ctx.ComboStep, false);
            }
        }

        // Hồi 20% Sin Gain (Không vượt qua Max)
        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
        {
            float sinGainAmount = ctx.Stats.sinGain * 0.2f;
            ctx.Stats.currentSin = Mathf.Min(ctx.Stats.maxSin, ctx.Stats.currentSin + sinGainAmount);
        }

        // Hiển thị cục Visual để dễ test Hitbox cưa xích
        VisualDebugHelper.DrawBox(center, halfExtents * 2f, Quaternion.LookRotation(ctx.FacingDir), Color.red, 0.2f);
    }
}