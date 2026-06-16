using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NGUYÊN MẪU 1 — DEBUFFER. Chuyên phá giáp / lộ điểm yếu.
/// Passive [Điểm Yếu Chí Tử], Skill [Lựu Đạn Ăn Mòn] (CD10s), Signature [Khởi Động Hủy Diệt] (120 Sin, CD16s).
/// </summary>
public class CompanionDebufferBehavior : CompanionSkillBehavior
{
    public override CompanionArchetype Archetype => CompanionArchetype.Debuffer;
    public override float SkillCooldown => 10f;
    public override float SignatureCooldown => 16f;
    public override float SignatureSinCost => 120f;

    // ── Passive: quét điểm yếu kẻ địch gần nhất ──
    private float _nextScan = 0f;
    private const float SCAN_INTERVAL = 0.5f;
    private const float SCAN_RADIUS = 12f;

    private void Update()
    {
        if (stats == null || stats.isDead) return;
        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + SCAN_INTERVAL;
            Stats e = NearestEnemy(transform.position, SCAN_RADIUS);
            if (e != null && CompanionWeaknessSystem.CanScan(e))
            {
                CompanionWeaknessSystem.RevealRandom(e, 5f);
                VisualDebugHelper.DrawSphere(e.transform.position + Vector3.up * 2f, 0.3f, Color.yellow, 0.4f);
            }
        }
    }

    // ───────────────────────── SKILL [Lựu Đạn Ăn Mòn] ─────────────────────────
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

    // Cơ bản: ném bom nổ AoE 1f = 150% physAtk, -15% Armor & MR 5s.
    private void Skill_Basic()
    {
        Stats tgt = CurrentTargetStats();
        Vector3 dir = tgt != null ? (tgt.transform.position - transform.position) : FacingDir();
        float maxDist = tgt != null ? Vector3.Distance(transform.position, tgt.transform.position) + 0.5f : 4f;
        SpawnTravelingAoE(dir, 12f, maxDist, 0.5f, pos =>
        {
            foreach (var e in EnemiesInRadius(pos, 1f))
            {
                DealPhysical(e, 1.5f);
                ReduceArmorMR(e, e.armor * 0.15f, e.magicResist * 0.15f, 5f);
            }
            VisualDebugHelper.DrawSphere(pos, 1f, new Color(0.6f, 1f, 0.2f, 0.4f), 0.4f);
        }, new Color(0.6f, 1f, 0.2f, 1f));
    }

    // Artillery: kim tiêm đơn mục tiêu 200% physAtk, -30% Armor 8s, Bleed 8s (10% physAtk/s).
    private void Skill_Artillery()
    {
        Stats tgt = CurrentTargetStats() ?? NearestEnemy(transform.position + FacingDir() * 7f, 8f);
        if (tgt == null) return;
        DealPhysical(tgt, 2.0f);
        ReduceArmor(tgt, tgt.armor * 0.30f, 8f);
        DoT(tgt, stats.physicalAtk * 0.10f, 8f, false);
    }

    // Carnage: xoay cưa AoE 2f = 150% physAtk + "Rỉ Sét" (-20% sát thương & -20% Armor 5s).
    private void Skill_Carnage()
    {
        foreach (var e in EnemiesInRadius(transform.position, 2f))
        {
            DealPhysical(e, 1.5f);
            ApplyRust(e, 5f);
        }
        VisualDebugHelper.DrawSphere(transform.position, 2f, new Color(1f, 0.5f, 0.1f, 0.4f), 0.4f);
    }

    // Suppression: bãi axit r=2.5f, 4s. Mỗi giây: 50% magicAtk + chậm 30%; -40% MR khi đứng trong vùng.
    private void Skill_Suppression()
    {
        Vector3 center = TargetPosOrForward(4f);
        HashSet<Stats> mrReduced = new HashSet<Stats>();
        SpawnGroundZone(center, 2.5f, 4f, 1f, e =>
        {
            DealMagic(e, 0.5f);
            SlowEnemy(e, 0.30f, 1.1f);
            if (mrReduced.Add(e)) ReduceArmorMR(e, 0f, e.magicResist * 0.40f, 4f);
        }, new Color(0.3f, 1f, 0.2f, 0.35f));
    }

    // Aegis: đập khiên đơn mục tiêu 150% (theo atkType Aegis), Shatter shield, lộ điểm yếu cả 8 hướng 3s.
    private void Skill_Aegis()
    {
        Stats tgt = CurrentTargetStats() ?? NearestEnemy(transform.position + FacingDir() * 2f, 4f);
        if (tgt == null) return;
        tgt.currentShield = 0f; // Shatter
        DealByAegisType(tgt, 1.5f);
        CompanionWeaknessSystem.RevealAll(tgt, 3f);
    }

    // ───────────────────────── SIGNATURE [Khởi Động Hủy Diệt] ─────────────────────────
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

    // Cơ bản: xóa toàn bộ buff có lợi của địch quanh 5f. (Game chưa có hệ buff cho enemy → no-op, để sẵn móc.)
    private void Sig_Basic()
    {
        VisualDebugHelper.DrawSphere(transform.position, 5f, new Color(0.7f, 0.7f, 1f, 0.3f), 0.5f);
        // TODO: khi enemy có hệ buff, gọi clear buff ở đây cho EnemiesInRadius(transform.position, 5f).
        Debug.Log("[Companion-Debuffer] Signature Cơ bản: xóa buff địch (chưa có hệ buff enemy → no-op).");
    }

    // Artillery: laser đơn mục tiêu 250% physAtk TRUE + -100% Armor 5s.
    private void Sig_Artillery()
    {
        Stats tgt = CurrentTargetStats() ?? NearestEnemy(transform.position + FacingDir() * 7f, 15f);
        if (tgt == null) return;
        DealTrue(tgt, stats.physicalAtk * 2.5f);
        ReduceArmor(tgt, tgt.armor, 5f); // -100% armor
    }

    // Carnage: chém địch nhiều máu nhất trong 3f, 10 nhát/3s, mỗi nhát 20% physAtk + giảm vĩnh viễn 0.5% maxHp.
    private void Sig_Carnage()
    {
        Stats tgt = HighestHpEnemy(transform.position, 3f);
        if (tgt == null) return;
        StartCoroutine(CarnageSlashRoutine(tgt));
    }
    private IEnumerator CarnageSlashRoutine(Stats tgt)
    {
        for (int i = 0; i < 10 && tgt != null && tgt.currentHp > 0; i++)
        {
            DealPhysical(tgt, 0.20f);
            // Giảm VĨNH VIỄN 0.5% maxHp (maxHp là field nền của Stats; EnemyStats không recalc đè nên giữ nguyên).
            tgt.maxHp -= tgt.maxHp * 0.005f;
            if (tgt.currentHp > tgt.maxHp) tgt.currentHp = tgt.maxHp;
            yield return new WaitForSeconds(0.3f);
        }
    }

    // Suppression: ga độc r=4f 6s, địch trong vùng chịu +20% sát thương.
    private void Sig_Suppression()
    {
        HashSet<Stats> marked = new HashSet<Stats>();
        SpawnGroundZone(transform.position, 4f, 6f, 1f, e =>
        {
            if (marked.Add(e)) StartCoroutine(VulnRoutine(e, 0.20f, 6f));
        }, new Color(0.4f, 0.8f, 0.2f, 0.3f));
    }

    // Aegis: giương khiên 5s (đứng yên, -50% dmg nhận); vùng nắng r=5f: mỗi giây True = 50%Armor+50%MR của Companion, -50% Armor/MR địch.
    private void Sig_Aegis()
    {
        StartCoroutine(AegisSunRoutine());
    }
    private IEnumerator AegisSunRoutine()
    {
        stats.damageTakenMultiplier -= 0.5f; // -50% sát thương nhận vào trong 5s
        HashSet<Stats> reduced = new HashSet<Stats>();
        float t = 0f;
        while (t < 5f)
        {
            foreach (var e in EnemiesInRadius(transform.position, 5f))
            {
                DealTrue(e, stats.armor * 0.5f + stats.magicResist * 0.5f);
                if (reduced.Add(e)) ReduceArmorMR(e, e.armor * 0.5f, e.magicResist * 0.5f, 5f);
            }
            VisualDebugHelper.DrawSphere(transform.position, 5f, new Color(1f, 0.95f, 0.4f, 0.3f), 1f);
            t += 1f;
            yield return new WaitForSeconds(1f);
        }
        stats.damageTakenMultiplier += 0.5f;
    }

    // ───────────────────────── helpers riêng ─────────────────────────
    private Stats HighestHpEnemy(Vector3 center, float radius)
    {
        Stats best = null; float max = -1f;
        foreach (var e in EnemiesInRadius(center, radius))
            if (e.currentHp > max) { max = e.currentHp; best = e; }
        return best;
    }

    // "Rỉ Sét": -20% sát thương địch gây ra (damageOutputMultiplier) + -20% Armor.
    private void ApplyRust(Stats e, float dur) => StartCoroutine(RustRoutine(e, dur));
    private IEnumerator RustRoutine(Stats e, float dur)
    {
        if (e == null) yield break;
        float armorCut = e.armor * 0.20f;
        e.damageOutputMultiplier -= 0.20f; e.armor -= armorCut;
        yield return new WaitForSeconds(dur);
        if (e != null) { e.damageOutputMultiplier += 0.20f; e.armor += armorCut; }
    }

    private IEnumerator VulnRoutine(Stats e, float pct, float dur)
    {
        if (e == null) yield break;
        e.damageTakenMultiplier += pct;
        yield return new WaitForSeconds(dur);
        if (e != null) e.damageTakenMultiplier -= pct;
    }
}
