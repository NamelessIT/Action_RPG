using System.Collections.Generic;
using UnityEngine;

/// <summary>5 nguyên mẫu Companion.</summary>
public enum CompanionArchetype
{
    Debuffer,      // 1 - phá giáp / điểm yếu
    Sustain,       // 2 - hồi máu / chống chịu
    Controller,    // 3 - khống chế
    Buffer,        // 4 - buff Player
    DamageDealer   // 5 - sát thương
}

/// <summary>
/// Lớp trừu tượng cho bộ Kỹ năng của 1 nguyên mẫu Companion (Passive + Skill + Signature).
/// Skill/Signature BIẾN DỊ theo Protocol đang trang bị (Basic = không Protocol, hoặc Artillery/Carnage/Suppression/Aegis).
/// Toàn bộ sát thương đi qua DamageHelper.ApplyStandardDamage (GDD mục 3.3).
///
/// Cooldown/Sin do CompanionSkillController quản lý; lớp này chỉ THỰC THI hiệu ứng + giữ Passive.
/// </summary>
[RequireComponent(typeof(AllyStats))]
[RequireComponent(typeof(CompanionAI))]
public abstract class CompanionSkillBehavior : MonoBehaviour
{
    protected AllyStats stats;
    protected CompanionAI ai;
    protected CompanionEquipmentManager equip;
    protected Transform player;

    // ── Thông số/định danh (mỗi nguyên mẫu override) ──
    public abstract CompanionArchetype Archetype { get; }
    public abstract float SkillCooldown { get; }
    public abstract float SignatureCooldown { get; }
    public abstract float SignatureSinCost { get; }

    // ── Thực thi (switch theo Protocol; null = Basic) ──
    public abstract void ExecuteSkill(CompanionProtocolType? protocol);
    public abstract void ExecuteSignature(CompanionProtocolType? protocol);

    protected virtual void Awake()
    {
        stats = GetComponent<AllyStats>();
        ai    = GetComponent<CompanionAI>();
        equip = GetComponent<CompanionEquipmentManager>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        OnPassiveEnable();
    }

    protected virtual void OnDestroy() => OnPassiveDisable();

    // Passive hooks — nguyên mẫu nào cần thì override (subscribe event / Update).
    protected virtual void OnPassiveEnable() { }
    protected virtual void OnPassiveDisable() { }

    // ───────────────────────── TIỆN ÍCH DÙNG CHUNG ─────────────────────────

    /// <summary>Mục tiêu Companion đang nhắm (Stats), null nếu không có.</summary>
    protected Stats CurrentTargetStats()
    {
        if (ai != null && ai.currentTarget != null)
        {
            Stats s = ai.currentTarget.GetComponentInParent<Stats>();
            if (s != null && s.currentHp > 0) return s;
        }
        return null;
    }

    /// <summary>Hướng Companion đang nhìn (mặt phẳng XZ).</summary>
    protected Vector3 FacingDir()
    {
        Vector3 d = stats != null ? stats.facingDirection : transform.forward;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    /// <summary>Vị trí mục tiêu (nếu có) hoặc điểm cách trước mặt 'forwardDist'.</summary>
    protected Vector3 TargetPosOrForward(float forwardDist)
    {
        Stats t = CurrentTargetStats();
        if (t != null) return t.transform.position;
        return transform.position + FacingDir() * forwardDist;
    }

    /// <summary>AllyStats của Player (PlayerStats kế thừa AllyStats). Null nếu chưa tìm thấy.</summary>
    protected AllyStats PlayerAlly()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        return player != null ? player.GetComponent<AllyStats>() : null;
    }

    /// <summary>Buff tạm: apply ngay, revert sau dur giây.</summary>
    protected void TimedBuff(System.Action apply, System.Action revert, float dur)
        => StartCoroutine(TimedBuffRoutine(apply, revert, dur));
    private System.Collections.IEnumerator TimedBuffRoutine(System.Action apply, System.Action revert, float dur)
    {
        apply?.Invoke();
        try
        {
            yield return new WaitForSeconds(dur);
        }
        finally
        {
            // finally: Companion chet/bi disable giua chung thi buff VAN duoc go.
            // Truoc day revert nam sau yield nen coroutine bi dung la buff ket vinh vien
            // (nang nhat la Aegis: giu luon token super armor cua player).
            revert?.Invoke();
        }
    }

    protected int EnemyMask()
    {
        // dùng cùng dangerLayer của player nếu có; fallback all layers.
        var pc = player != null ? player.GetComponent<PlayerController>() : null;
        return pc != null ? pc.dangerLayer.value : ~0;
    }

    /// <summary>Gây sát thương VẬT LÝ = mult * physicalAtk của Companion.</summary>
    protected void DealPhysical(Stats target, float mult, int impact = 0, bool stun = false, float stunTime = 0f, bool knockback = false, float kbForce = 0f)
    {
        if (target == null || target.currentHp <= 0) return;
        DamageHelper.ApplyStandardDamage(stats, target, transform, mult, null, null, impact, stun, stunTime, knockback, kbForce, sourceType: DamageSourceType.Ranged);
    }

    /// <summary>Gây sát thương PHÉP = mult * magicAtk của Companion (dùng MagicProxy).</summary>
    protected void DealMagic(Stats target, float mult, int impact = 0, bool stun = false, float stunTime = 0f, bool knockback = false, float kbForce = 0f)
    {
        if (target == null || target.currentHp <= 0) return;
        DamageHelper.ApplyStandardDamage(stats, target, transform, mult, null, CompanionCombat.MagicProxy, impact, stun, stunTime, knockback, kbForce, sourceType: DamageSourceType.Ranged);
    }

    /// <summary>Sát thương theo atkType của Aegis (Physical/Magic).</summary>
    protected void DealByAegisType(Stats target, float mult, int impact = 0, bool stun = false, float stunTime = 0f, bool knockback = false, float kbForce = 0f)
    {
        bool magic = equip != null && equip.Protocol != null && equip.Protocol.atkType == CompanionAtkType.Magic;
        if (magic) DealMagic(target, mult, impact, stun, stunTime, knockback, kbForce);
        else DealPhysical(target, mult, impact, stun, stunTime, knockback, kbForce);
    }

    /// <summary>Sát thương chuẩn (True) trực tiếp 1 lượng cụ thể.</summary>
    protected void DealTrue(Stats target, float amount, bool stun = false, float stunTime = 0f)
    {
        if (target == null || target.currentHp <= 0 || amount <= 0f) return;
        var info = new DamageInfo { trueDamage = amount, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Other };
        if (stun && stunTime > 0f)
            info.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, stunTime) { sourcePosition = transform.position });
        target.TakeDamage(info);
    }

    /// <summary>Lấy mọi Stats địch trong bán kính (dedupe).</summary>
    protected List<Stats> EnemiesInRadius(Vector3 center, float radius)
    {
        var list = new List<Stats>();
        var seen = new HashSet<Stats>();
        foreach (var h in Physics.OverlapSphere(center, radius, EnemyMask()))
        {
            Stats e = CompanionCombat.GetEnemy(h);
            if (e != null && seen.Add(e)) list.Add(e);
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

    /// <summary>Làm chậm địch theo % trong dur giây — qua effect system (strongest-wins, expiry-safe).</summary>
    protected void SlowEnemy(Stats target, float percent, float dur)
    {
        if (target == null || percent <= 0f) return;
        target.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, dur) { magnitude = percent }, stats);
    }

    /// <summary>Giảm Armor địch trong dur giây (tự khôi phục).</summary>
    protected void ReduceArmor(Stats target, float flatOrPctAmount, float dur)
    {
        if (target == null || flatOrPctAmount <= 0f) return;
        StartCoroutine(ReduceStatRoutine(target, flatOrPctAmount, 0f, dur));
    }
    /// <summary>Giảm Armor & MR địch trong dur giây.</summary>
    protected void ReduceArmorMR(Stats target, float armorAmount, float mrAmount, float dur)
    {
        if (target == null) return;
        StartCoroutine(ReduceStatRoutine(target, armorAmount, mrAmount, dur));
    }
    private System.Collections.IEnumerator ReduceStatRoutine(Stats target, float armorAmt, float mrAmt, float dur)
    {
        if (target != null) { target.armor -= armorAmt; target.magicResist -= mrAmt; }
        yield return new WaitForSeconds(dur);
        if (target != null) { target.armor += armorAmt; target.magicResist += mrAmt; }
    }

    /// <summary>Đạn bay theo hướng dir, nổ khi chạm địch (hitRadius) hoặc đi hết maxDist → gọi onExplode(vị trí nổ).</summary>
    protected void SpawnTravelingAoE(Vector3 dir, float speed, float maxDist, float hitRadius, System.Action<Vector3> onExplode, Color color)
    {
        StartCoroutine(TravelRoutine(dir, speed, maxDist, hitRadius, onExplode, color));
    }
    private System.Collections.IEnumerator TravelRoutine(Vector3 dir, float speed, float maxDist, float hitRadius, System.Action<Vector3> onExplode, Color color)
    {
        dir.y = 0f; dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        Vector3 pos = transform.position + Vector3.up * 0.3f;
        float traveled = 0f;
        int mask = EnemyMask();
        while (traveled < maxDist)
        {
            float step = speed * Time.deltaTime;
            pos += dir * step; traveled += step;
            VisualDebugHelper.DrawSphere(pos, 0.2f, color, 0.05f);
            bool hit = false;
            foreach (var h in Physics.OverlapSphere(pos, hitRadius, mask))
                if (CompanionCombat.GetEnemy(h) != null) { hit = true; break; }
            if (hit) break;
            yield return null;
        }
        onExplode?.Invoke(pos);
    }

    /// <summary>Vùng mặt đất tồn tại duration, mỗi tickInterval gọi onTick lên từng địch trong bán kính.</summary>
    protected void SpawnGroundZone(Vector3 center, float radius, float duration, float tickInterval, System.Action<Stats> onTick, Color color)
    {
        StartCoroutine(ZoneRoutine(center, radius, duration, tickInterval, onTick, color));
    }
    private System.Collections.IEnumerator ZoneRoutine(Vector3 center, float radius, float duration, float tickInterval, System.Action<Stats> onTick, Color color)
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

    /// <summary>Choáng địch dur giây (đòn 0 sát thương, qua ApplyCombatEffects).</summary>
    protected void StunEnemy(Stats e, float dur, int impact = 0)
    {
        if (e == null || e.currentHp <= 0) return;
        var info = new DamageInfo { attacker = stats, sourcePosition = transform.position, impactLevel = impact };
        info.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, dur) { impactLevel = impact, sourcePosition = transform.position });
        e.TakeDamage(info);
    }

    /// <summary>Trói chân địch trong dur giây — vẫn cho tấn công. Qua effect system (endTime, không
    /// đụng baseMoveSpeed/agent.isStopped trực tiếp → tránh xóa nhầm trạng thái nguồn khác).</summary>
    protected void RootEnemy(Stats e, float dur)
    {
        if (e == null) return;
        e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Root, dur), stats);
    }

    /// <summary>Kéo địch về 'center' (hút). Dùng NavMeshAgent.Warp nếu có để mượt.</summary>
    protected void PullToward(Stats e, Vector3 center, float speed)
    {
        if (e == null) return;
        Vector3 next = Vector3.MoveTowards(e.transform.position, center, speed * Time.deltaTime);
        var agent = e.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(next);
        else e.transform.position = next;
    }

    /// <summary>Gây Bleed/Burn: mỗi giây 'dmgPerSec' trong 'duration' giây.</summary>
    protected void DoT(Stats target, float dmgPerSec, float duration, bool magic)
    {
        if (target == null) return;
        StartCoroutine(DoTRoutine(target, dmgPerSec, duration, magic));
    }
    private System.Collections.IEnumerator DoTRoutine(Stats target, float dps, float dur, bool magic)
    {
        float t = 0f;
        while (t < dur && target != null && target.currentHp > 0)
        {
            target.TakeDamage(new DamageInfo
            {
                physDamage = magic ? 0f : dps,
                magicDamage = magic ? dps : 0f,
                attacker = stats,
                sourceType = DamageSourceType.DoT
            });
            t += 1f;
            yield return new WaitForSeconds(1f);
        }
    }
}
