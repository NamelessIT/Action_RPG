using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lớp nền dùng chung cho 3 EffectManager của Companion (Protocol / Matrix / Sync Core).
/// Cung cấp tham chiếu (Companion stats / AI / Equip / Player) + các tiện ích sát thương & khống chế,
/// dùng lại đúng pipeline của CompanionSkillBehavior (DamageHelper.ApplyStandardDamage + CompanionCombat).
///
/// GHI CHÚ THIẾT KẾ (theo 3 file "Tổng hợp Effect ..."):
///  • Chỉ xử lý module Rarity 3+ (CompanionModuleData.HasEffect).
///  • True Damage không bị giảm trừ và không ăn modifier (Global Rule 4).
///  • "Mất máu" = chạm currentHp (Global Rule 3) — các effect liên quan dùng OnDamageTakenHp.
/// </summary>
[RequireComponent(typeof(AllyStats))]
public abstract class CompanionEffectManagerBase : MonoBehaviour
{
    protected AllyStats stats;                       // stats của Companion
    protected CompanionAI ai;                        // để biết currentTarget
    protected CompanionEquipmentManager equip;       // để biết Protocol atkType

    protected Transform playerTf;
    protected AllyStats playerStats;
    protected PlayerController playerController;

    protected virtual void Awake()
    {
        stats = GetComponent<AllyStats>();
        ai = GetComponent<CompanionAI>();
        equip = GetComponent<CompanionEquipmentManager>();
        ResolvePlayer();
    }

    protected void ResolvePlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        playerTf = p.transform;
        playerStats = p.GetComponent<AllyStats>();
        playerController = p.GetComponent<PlayerController>();
    }

    // ───────────────────────── TÌM ĐỊCH ─────────────────────────
    protected int EnemyMask()
    {
        var pc = playerController != null ? playerController : (playerTf != null ? playerTf.GetComponent<PlayerController>() : null);
        return pc != null ? pc.dangerLayer.value : ~0;
    }

    protected List<Stats> EnemiesInRadius(Vector3 center, float radius)
    {
        var list = new List<Stats>();
        var seen = new HashSet<Stats>();
        foreach (var h in Physics.OverlapSphere(center, radius, EnemyMask()))
        {
            Stats e = CompanionCombat.GetEnemy(h);
            if (e != null && e.currentHp > 0 && seen.Add(e)) list.Add(e);
        }
        return list;
    }

    protected Stats NearestEnemy(Vector3 center, float radius)
    {
        Stats best = null; float min = float.MaxValue;
        foreach (var e in EnemiesInRadius(center, radius))
        {
            float d = Vector3.SqrMagnitude(e.transform.position - center);
            if (d < min) { min = d; best = e; }
        }
        return best;
    }

    protected Stats HighestHpEnemy(Vector3 center, float radius)
    {
        Stats best = null; float max = -1f;
        foreach (var e in EnemiesInRadius(center, radius))
            if (e.currentHp > max) { max = e.currentHp; best = e; }
        return best;
    }

    protected Stats CurrentTargetStats()
    {
        if (ai != null && ai.currentTarget != null)
        {
            Stats s = ai.currentTarget.GetComponentInParent<Stats>();
            if (s != null && s.currentHp > 0) return s;
        }
        return null;
    }

    // ───────────────────────── SÁT THƯƠNG ─────────────────────────
    protected void DealPhysical(Stats target, float mult, int impact = 0, bool stun = false, float stunTime = 0f, bool knockback = false, float kbForce = 0f)
    {
        if (target == null || target.currentHp <= 0) return;
        DamageHelper.ApplyStandardDamage(stats, target, transform, mult, null, null, impact, stun, stunTime, knockback, kbForce, sourceType: DamageSourceType.Ranged);
    }

    protected void DealMagic(Stats target, float mult, int impact = 0, bool stun = false, float stunTime = 0f, bool knockback = false, float kbForce = 0f)
    {
        if (target == null || target.currentHp <= 0) return;
        DamageHelper.ApplyStandardDamage(stats, target, transform, mult, null, CompanionCombat.MagicProxy, impact, stun, stunTime, knockback, kbForce, sourceType: DamageSourceType.Ranged);
    }

    /// <summary>Sát thương theo atkType của Protocol đang trang bị (mặc định vật lý).</summary>
    protected void DealByProtocol(Stats target, float mult, int impact = 0, bool stun = false, float stunTime = 0f)
    {
        bool magic = equip != null && equip.Protocol != null && equip.Protocol.atkType == CompanionAtkType.Magic;
        if (magic) DealMagic(target, mult, impact, stun, stunTime);
        else DealPhysical(target, mult, impact, stun, stunTime);
    }

    protected void DealTrue(Stats target, float amount, bool stun = false, float stunTime = 0f)
    {
        if (target == null || target.currentHp <= 0 || amount <= 0f) return;
        target.TakeDamage(new DamageInfo { trueDamage = amount, attacker = stats, sourcePosition = transform.position, isStun = stun, stunDuration = stunTime, sourceType = DamageSourceType.Other });
    }

    // ───────────────────────── KHỐNG CHẾ / DEBUFF (tự khôi phục) ─────────────────────────
    protected void StunEnemy(Stats e, float dur, int impact = 0)
    {
        if (e == null || e.currentHp <= 0) return;
        e.TakeDamage(new DamageInfo { isStun = true, stunDuration = dur, impactLevel = impact, attacker = stats, sourcePosition = transform.position });
    }

    protected void SlowEnemy(Stats target, float percent, float dur)
    {
        if (target == null || percent <= 0f) return;
        StartCoroutine(SlowRoutine(target, percent, dur));
    }
    private IEnumerator SlowRoutine(Stats target, float percent, float dur)
    {
        target.baseMoveSpeed *= (1f - percent);
        yield return new WaitForSeconds(dur);
        if (target != null) target.baseMoveSpeed /= (1f - percent);
    }

    /// <summary>Giảm Armor & MR theo lượng tuyệt đối trong dur giây.</summary>
    protected void ReduceArmorMR(Stats target, float armorAmt, float mrAmt, float dur)
    {
        if (target == null) return;
        StartCoroutine(ReduceStatRoutine(target, armorAmt, mrAmt, dur));
    }
    private IEnumerator ReduceStatRoutine(Stats target, float armorAmt, float mrAmt, float dur)
    {
        if (target != null) { target.armor -= armorAmt; target.magicResist -= mrAmt; }
        yield return new WaitForSeconds(dur);
        if (target != null) { target.armor += armorAmt; target.magicResist += mrAmt; }
    }

    /// <summary>Giảm % sát thương kẻ địch gây ra (damageOutputMultiplier) trong dur giây.</summary>
    protected void WeakenEnemyDamage(Stats target, float percent, float dur)
    {
        if (target == null || percent <= 0f) return;
        StartCoroutine(WeakenRoutine(target, percent, dur));
    }
    private IEnumerator WeakenRoutine(Stats target, float percent, float dur)
    {
        target.damageOutputMultiplier -= percent;
        yield return new WaitForSeconds(dur);
        if (target != null) target.damageOutputMultiplier += percent;
    }

    /// <summary>Tăng % "dễ tổn thương" (damageTakenMultiplier) cho địch trong dur giây.</summary>
    protected void VulnEnemy(Stats target, float percent, float dur)
    {
        if (target == null || percent == 0f) return;
        StartCoroutine(VulnRoutine(target, percent, dur));
    }
    private IEnumerator VulnRoutine(Stats target, float percent, float dur)
    {
        target.damageTakenMultiplier += percent;
        yield return new WaitForSeconds(dur);
        if (target != null) target.damageTakenMultiplier -= percent;
    }

    /// <summary>
    /// Khiêu khích (Taunt): ép kẻ địch chuyển mục tiêu sang Companion + dồn aggro tối đa.
    /// Dùng API có sẵn (EnemyCombat.SetTarget + EnemyStats.AddAggro).
    /// </summary>
    protected void TauntToCompanion(Stats enemy)
    {
        if (enemy == null) return;
        var ec = enemy.GetComponent<EnemyCombat>();
        if (ec != null) ec.SetTarget(transform);
        var es = enemy as EnemyStats;
        if (es != null) es.AddAggro(99999f);
    }

    // ───────────────────────── VÙNG / BUFF TẠM ─────────────────────────
    protected void SpawnGroundZone(Vector3 center, float radius, float duration, float tickInterval, System.Action<Stats> onTick, Color color)
    {
        StartCoroutine(ZoneRoutine(center, radius, duration, tickInterval, onTick, color));
    }
    private IEnumerator ZoneRoutine(Vector3 center, float radius, float duration, float tickInterval, System.Action<Stats> onTick, Color color)
    {
        float t = 0f;
        while (t < duration)
        {
            foreach (var e in EnemiesInRadius(center, radius)) onTick?.Invoke(e);
            VisualDebugHelper.DrawSphere(center, radius, color, tickInterval);
            t += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }
    }

    protected void TimedBuff(System.Action apply, System.Action revert, float dur)
        => StartCoroutine(TimedBuffRoutine(apply, revert, dur));
    private IEnumerator TimedBuffRoutine(System.Action apply, System.Action revert, float dur)
    {
        apply?.Invoke();
        yield return new WaitForSeconds(dur);
        revert?.Invoke();
    }
}
