using System.Collections;
using UnityEngine;

/// <summary>
/// NGUYÊN MẪU 3 — CONTROLLER. Khống chế đám đông.
/// Passive [Từ Trường], Skill [Bom Trọng Lực] (CD12s), Signature [Phá Vỡ Không Gian] (150 Sin, CD18s).
/// </summary>
public class CompanionControllerBehavior : CompanionSkillBehavior
{
    public override CompanionArchetype Archetype => CompanionArchetype.Controller;
    public override float SkillCooldown => 12f;
    public override float SignatureCooldown => 18f;
    public override float SignatureSinCost => 150f;

    // ── Passive [Từ Trường]: Companion gây sát thương → địch bị làm chậm 15% trong 2s ──
    protected override void OnPassiveEnable()
    {
        if (stats != null) stats.OnHitEnemy += OnCompHit;
    }
    protected override void OnPassiveDisable()
    {
        if (stats != null) stats.OnHitEnemy -= OnCompHit;
    }
    private void OnCompHit(Stats victim, float t, bool crit)
    {
        if (victim != null) SlowEnemy(victim, 0.15f, 2f);
    }

    // ───────────────────────── SKILL [Bom Trọng Lực] ─────────────────────────
    public override void ExecuteSkill(CompanionProtocolType? p)
    {
        switch (p)
        {
            case CompanionProtocolType.Artillery:   Skill_Artillery(); break;
            case CompanionProtocolType.Carnage:     Skill_Carnage(); break;
            case CompanionProtocolType.Suppression: Skill_Suppression(); break;
            case CompanionProtocolType.Aegis:       Skill_Aegis(); break;
            default:                                Skill_Basic(); break;
        }
    }

    // Cơ bản: bom nổ AoE 1f = 150% magicAtk + choáng 2s.
    private void Skill_Basic()
    {
        Stats tgt = CurrentTargetStats();
        Vector3 dir = tgt != null ? (tgt.transform.position - transform.position) : FacingDir();
        float maxDist = tgt != null ? Vector3.Distance(transform.position, tgt.transform.position) + 0.5f : 4f;
        SpawnTravelingAoE(dir, 12f, maxDist, 0.5f, pos =>
        {
            foreach (var e in EnemiesInRadius(pos, 1f)) { DealMagic(e, 1.5f); StunEnemy(e, 2f); }
            VisualDebugHelper.DrawSphere(pos, 1f, new Color(0.6f, 0.2f, 1f, 0.4f), 0.4f);
        }, new Color(0.6f, 0.2f, 1f, 1f));
    }

    // Artillery: đạn đơn 200% physAtk + trói chân 4s.
    private void Skill_Artillery()
    {
        Stats tgt = CurrentTargetStats() ?? NearestEnemy(transform.position + FacingDir() * 7f, 15f);
        if (tgt == null) return;
        DealPhysical(tgt, 2.0f);
        RootEnemy(tgt, 4f);
    }

    // Carnage: 5s, đòn đánh của Companion làm choáng địch 0.25s.
    private void Skill_Carnage() => StartCoroutine(CarnageStunWindow(5f));
    private IEnumerator CarnageStunWindow(float dur)
    {
        System.Action<Stats, float, bool> h = (v, t, c) => { if (v != null) StunEnemy(v, 0.25f); };
        stats.OnHitEnemy += h;
        yield return new WaitForSeconds(dur);
        stats.OnHitEnemy -= h;
    }

    // Suppression: ném tiểu Hố Đen (4s), hút địch trong 2f về tâm.
    private void Skill_Suppression()
    {
        Vector3 center = TargetPosOrForward(3f);
        StartCoroutine(BlackHoleRoutine(center, 2f, 4f, 3f));
    }
    private IEnumerator BlackHoleRoutine(Vector3 center, float radius, float dur, float pullSpeed)
    {
        float t = 0f;
        while (t < dur)
        {
            foreach (var e in EnemiesInRadius(center, radius)) PullToward(e, center, pullSpeed);
            VisualDebugHelper.DrawSphere(center, radius, new Color(0.3f, 0f, 0.5f, 0.4f), 0.1f);
            t += Time.deltaTime;
            yield return null;
        }
    }

    // Aegis: đập đất 100% (atkType Aegis) + Hất tung 1s rồi choáng 1.5s, bán kính 1f.
    private void Skill_Aegis()
    {
        foreach (var e in EnemiesInRadius(transform.position, 1f))
        {
            DealByAegisType(e, 1.0f, impact: 1);
            StartCoroutine(AirborneThenStun(e, 1f, 1.5f));
        }
        VisualDebugHelper.DrawSphere(transform.position, 1f, Color.yellow, 0.4f);
    }
    private IEnumerator AirborneThenStun(Stats e, float airTime, float stunTime)
    {
        if (e == null) yield break;
        e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Airborne, airTime) { respectEffectResistance = false }, stats);
        yield return new WaitForSeconds(airTime);
        if (e != null && e.currentHp > 0) StunEnemy(e, stunTime);
    }

    // ───────────────────────── SIGNATURE [Phá Vỡ Không Gian] ─────────────────────────
    public override void ExecuteSignature(CompanionProtocolType? p)
    {
        switch (p)
        {
            case CompanionProtocolType.Artillery:   Sig_Artillery(); break;
            case CompanionProtocolType.Carnage:     Sig_Carnage(); break;
            case CompanionProtocolType.Suppression: Sig_Suppression(); break;
            case CompanionProtocolType.Aegis:       Sig_Aegis(); break;
            default:                                Sig_Basic(); break;
        }
    }

    // Cơ bản: 100% magicAtk + Choáng 3s mọi địch trong 4f.
    private void Sig_Basic()
    {
        foreach (var e in EnemiesInRadius(transform.position, 4f)) { DealMagic(e, 1.0f); StunEnemy(e, 3f); }
        VisualDebugHelper.DrawSphere(transform.position, 4f, new Color(0.6f, 0.2f, 1f, 0.35f), 0.5f);
    }

    // Artillery: đạn đơn 300% physAtk + choáng 5s.
    private void Sig_Artillery()
    {
        Stats tgt = CurrentTargetStats() ?? NearestEnemy(transform.position + FacingDir() * 7f, 15f);
        if (tgt == null) return;
        DealPhysical(tgt, 3.0f);
        StunEnemy(tgt, 5f);
    }

    // Carnage: dậm đất r=3f, 300% physAtk + choáng 2s; hết choáng → chậm 30% 3s.
    private void Sig_Carnage()
    {
        foreach (var e in EnemiesInRadius(transform.position, 3f))
        {
            DealPhysical(e, 3.0f);
            StunEnemy(e, 2f);
            StartCoroutine(SlowAfter(e, 2f, 0.30f, 3f));
        }
        VisualDebugHelper.DrawSphere(transform.position, 3f, Color.yellow, 0.5f);
    }
    private IEnumerator SlowAfter(Stats e, float delay, float pct, float dur)
    {
        yield return new WaitForSeconds(delay);
        if (e != null && e.currentHp > 0) SlowEnemy(e, pct, dur);
    }

    // Suppression: Tâm Bão Trọng Lực 4s tại target (hoặc 4f trước mặt), hút r=1.5f; target chết → dời sang target mới; không có → đứng yên.
    private void Sig_Suppression() => StartCoroutine(GravityStormRoutine(4f, 1.5f, 3.5f));
    private IEnumerator GravityStormRoutine(float dur, float radius, float pullSpeed)
    {
        Stats focus = CurrentTargetStats();
        Vector3 center = focus != null ? focus.transform.position : transform.position + FacingDir() * 4f;
        float t = 0f;
        while (t < dur)
        {
            if (focus != null && focus.currentHp > 0) center = focus.transform.position;
            else { focus = CurrentTargetStats(); if (focus != null) center = focus.transform.position; }

            foreach (var e in EnemiesInRadius(center, radius)) PullToward(e, center, pullSpeed);
            VisualDebugHelper.DrawSphere(center, radius, new Color(0.4f, 0f, 0.7f, 0.4f), 0.1f);
            t += Time.deltaTime;
            yield return null;
        }
    }

    // Aegis: Trường Ngưng Đọng r=3f theo Companion 5s — địch bên trong bị trói chân.
    private void Sig_Aegis() => StartCoroutine(StasisFieldRoutine(3f, 5f));
    private IEnumerator StasisFieldRoutine(float radius, float dur)
    {
        var rooted = new System.Collections.Generic.HashSet<Stats>();
        float t = 0f;
        while (t < dur)
        {
            foreach (var e in EnemiesInRadius(transform.position, radius))
                if (rooted.Add(e)) RootEnemy(e, dur - t + 0.2f); // trói tới khi trường tan
            VisualDebugHelper.DrawSphere(transform.position, radius, new Color(0.2f, 0.5f, 1f, 0.3f), 0.25f);
            t += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }
    }
}
