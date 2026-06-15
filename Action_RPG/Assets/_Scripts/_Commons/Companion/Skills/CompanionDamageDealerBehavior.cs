using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NGUYÊN MẪU 5 — THE DAMAGE DEALER. Xả sát thương.
/// Passive [Tích Tụ Động Năng], Skill [Pháo Kích Hệ Thống] (CD8s), Signature [Án Tử] (120 Sin, CD16s).
/// Một số chiêu xấp xỉ (homing/meteor/đòn-thứ-3 bỏ giáp) — xem ghi chú.
/// </summary>
public class CompanionDamageDealerBehavior : CompanionSkillBehavior
{
    public override CompanionArchetype Archetype => CompanionArchetype.DamageDealer;
    public override float SkillCooldown => 8f;
    public override float SignatureCooldown => 16f;
    public override float SignatureSinCost => 120f;

    // ── Passive [Tích Tụ Động Năng]: đòn thứ 3 +50% dmg + bỏ qua 50% Armor/MR ──
    // Xấp xỉ: cứ đòn thứ 3 trúng địch → cộng thêm 1 phát = 50% physAtk dạng TRUE (mô phỏng "bỏ giáp").
    private int _hitCount = 0;
    protected override void OnPassiveEnable()  { if (stats != null) stats.OnHitEnemy += OnCompHit; }
    protected override void OnPassiveDisable() { if (stats != null) stats.OnHitEnemy -= OnCompHit; }
    private void OnCompHit(Stats v, float t, bool crit)
    {
        if (v == null) return;
        _hitCount++;
        if (_hitCount % 3 == 0) DealTrue(v, stats.physicalAtk * 0.5f);
    }

    // ───────────────────────── SKILL [Pháo Kích Hệ Thống] ─────────────────────────
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

    // Cơ bản: luồng năng lượng từ trời, nổ r=1f tại target (hoặc 4f trước mặt), 150% physAtk.
    private void Skill_Basic()
    {
        Vector3 pos = TargetPosOrForward(4f);
        foreach (var e in EnemiesInRadius(pos, 1f)) DealPhysical(e, 1.5f);
        VisualDebugHelper.DrawSphere(pos, 1f, new Color(1f, 0.4f, 0.1f, 0.45f), 0.4f);
    }

    // Artillery: đạn đơn 300% physAtk + mục tiêu chịu +20% sát thương 5s.
    private void Skill_Artillery()
    {
        Stats tgt = CurrentTargetStats() ?? NearestEnemy(transform.position + FacingDir() * 7f, 15f);
        if (tgt == null) return;
        DealPhysical(tgt, 3.0f);
        StartCoroutine(VulnRoutine(tgt, 0.20f, 5f));
    }

    // Carnage: xoay vũ khí r=2f 3s, 50% physAtk mỗi 0.5s; Companion hồi 20% sát thương gây ra (xấp xỉ).
    private void Skill_Carnage() => StartCoroutine(SpinRoutine(2f, 3f, 0.5f, 0.5f));
    private IEnumerator SpinRoutine(float radius, float dur, float tick, float mult)
    {
        float t = 0f;
        while (t < dur)
        {
            foreach (var e in EnemiesInRadius(transform.position, radius))
            {
                DealPhysical(e, mult);
                stats.Heal(stats.physicalAtk * mult * 0.20f, false, false, HealSource.Drain); // 20% lượng dmg (xấp xỉ)
            }
            VisualDebugHelper.DrawSphere(transform.position, radius, new Color(1f, 0.2f, 0.2f, 0.35f), tick);
            t += tick;
            yield return new WaitForSeconds(tick);
        }
    }

    // Suppression: 5 cầu homing chia đều địch trong 8f (ưu tiên thấp máu nếu >5), mỗi cầu 50% magicAtk + chậm 20% 3s; trúng >1 cầu thì +10% dmg cầu sau (xấp xỉ: bắn tức thì).
    private void Skill_Suppression()
    {
        var enemies = EnemiesInRadius(transform.position, 8f);
        if (enemies.Count == 0) return;
        enemies.Sort((a, b) => a.currentHp.CompareTo(b.currentHp)); // thấp máu trước
        var orbCount = new Dictionary<Stats, int>();
        for (int i = 0; i < 5; i++)
        {
            Stats e = enemies[i % enemies.Count];
            if (e == null || e.currentHp <= 0) continue;
            orbCount.TryGetValue(e, out int prior);
            DealMagic(e, 0.5f * (1f + 0.10f * prior)); // +10% mỗi cầu trước đó lên cùng địch
            SlowEnemy(e, 0.20f, 3f);
            orbCount[e] = prior + 1;
        }
    }

    // Aegis: đập đất, hất tung r=0.5f 1s, gây (atkType Aegis) = 20% Max HP của Companion.
    private void Skill_Aegis()
    {
        bool magic = equip != null && equip.Protocol != null && equip.Protocol.atkType == CompanionAtkType.Magic;
        float dmg = stats.maxHp * 0.20f;
        foreach (var e in EnemiesInRadius(transform.position, 0.5f))
        {
            e.TakeDamage(new DamageInfo
            {
                physDamage = magic ? 0f : dmg, magicDamage = magic ? dmg : 0f,
                attacker = stats, sourcePosition = transform.position, impactLevel = 1
            });
            e.Airborne(1f); // hất tung 1s
        }
        VisualDebugHelper.DrawSphere(transform.position, 0.5f, Color.yellow, 0.4f);
    }

    // ───────────────────────── SIGNATURE [Án Tử] ─────────────────────────
    public override void ExecuteSignature(CompanionProtocolType? p)
    {
        switch (p)
        {
            case CompanionProtocolType.Artillery:   StartCoroutine(Sig_Artillery()); break;
            case CompanionProtocolType.Carnage:     StartCoroutine(Sig_Carnage(5f)); break;
            case CompanionProtocolType.Suppression: StartCoroutine(Sig_Suppression()); break;
            case CompanionProtocolType.Aegis:       StartCoroutine(Sig_Aegis(5f)); break;
            default:                                Sig_Basic(); break;
        }
    }

    // Cơ bản: sóng xung kích r=4f, 300% physAtk.
    private void Sig_Basic()
    {
        foreach (var e in EnemiesInRadius(transform.position, 4f)) DealPhysical(e, 3.0f);
        VisualDebugHelper.DrawSphere(transform.position, 4f, new Color(1f, 0.3f, 0.1f, 0.4f), 0.5f);
    }

    // Artillery: bắn lên trời, 2s sau rớt vào địch nhiều MÁU NHẤT (theo số máu) trong 15f (hoặc random 2f nếu không có); Shatter + 450% physAtk đơn.
    private IEnumerator Sig_Artillery()
    {
        yield return new WaitForSeconds(2f);
        Stats tgt = HighestHpEnemy(transform.position, 15f);
        if (tgt != null)
        {
            tgt.currentShield = 0f;
            DealPhysical(tgt, 4.5f);
            VisualDebugHelper.DrawSphere(tgt.transform.position, 0.5f, Color.red, 0.4f);
        }
        else
        {
            Vector2 r = Random.insideUnitCircle * 2f;
            VisualDebugHelper.DrawSphere(transform.position + new Vector3(r.x, 0, r.y), 0.5f, Color.red, 0.4f);
        }
    }

    // Carnage: +300% bonusAttackSpeed 5s; mỗi đòn lên cùng 1 địch +2% sát thương so với đòn trước (cộng dồn). Hết buff → xóa bộ đếm.
    private IEnumerator Sig_Carnage(float dur)
    {
        stats.bonusAttackSpeed += 3.0f; stats.CalculateCombatStatsOnly();
        var hitCounter = new Dictionary<Stats, int>();
        System.Action<Stats, float, bool> onHit = (v, t, c) =>
        {
            if (v == null) return;
            hitCounter.TryGetValue(v, out int n);
            if (n > 0) DealPhysical(v, 0.02f * n); // cộng dồn 2% mỗi đòn trước đó
            hitCounter[v] = n + 1;
        };
        stats.OnHitEnemy += onHit;
        yield return new WaitForSeconds(dur);
        stats.OnHitEnemy -= onHit;
        stats.bonusAttackSpeed -= 3.0f; stats.CalculateCombatStatsOnly();
    }

    // Suppression: Mưa Thiên Thạch r=5f 5s, 25 viên (1 viên/0.2s), mỗi viên nổ r=0.3f = 50% magicAtk + chậm 20% 3s; viên thứ 4 trúng 1 địch → choáng 2.5s. (Phân bổ vị trí xấp xỉ.)
    private IEnumerator Sig_Suppression()
    {
        var meteorCount = new Dictionary<Stats, int>();
        for (int i = 0; i < 25; i++)
        {
            var enemies = EnemiesInRadius(transform.position, 5f);
            Vector3 pos;
            if (enemies.Count > 0 && Random.value < 0.7f)
                pos = enemies[Random.Range(0, enemies.Count)].transform.position; // ưu tiên rơi vào đầu địch
            else
            {
                Vector2 r = Random.insideUnitCircle * 5f;
                pos = transform.position + new Vector3(r.x, 0, r.y);
            }
            foreach (var e in EnemiesInRadius(pos, 0.3f))
            {
                DealMagic(e, 0.5f);
                SlowEnemy(e, 0.20f, 3f);
                meteorCount.TryGetValue(e, out int n);
                meteorCount[e] = n + 1;
                if (meteorCount[e] == 4) StunEnemy(e, 2.5f);
            }
            VisualDebugHelper.DrawSphere(pos, 0.3f, new Color(0.7f, 0.3f, 1f, 0.5f), 0.2f);
            yield return new WaitForSeconds(0.2f);
        }
    }

    // Aegis: khiêu khích địch trong 5f, -50% dmg nhận 5s; sau 5s gây True cho từng địch đã đánh trúng = 1% maxHp Companion × số lần (DoT: 2 tick = 1 lần).
    private IEnumerator Sig_Aegis(float dur)
    {
        stats.damageTakenMultiplier -= 0.5f;
        var hitsBy = new Dictionary<Stats, float>(); // dùng float để DoT cộng 0.5
        System.Action<DamageInfo> onTaken = info =>
        {
            if (info == null || info.attacker == null) return;
            Stats atk = info.attacker as Stats;
            if (atk == null || !atk.CompareTag("Enemy")) return;
            hitsBy.TryGetValue(atk, out float n);
            hitsBy[atk] = n + (info.sourceType == DamageSourceType.DoT ? 0.5f : 1f);
        };
        stats.OnBeforeTakeDamage += onTaken;
        // (Khiêu chọc: dồn aggro địch về Companion — game chưa có API ép target, tạm bỏ qua, chỉ giữ giảm sát thương + phản đòn.)
        yield return new WaitForSeconds(dur);
        stats.OnBeforeTakeDamage -= onTaken;
        stats.damageTakenMultiplier += 0.5f;

        foreach (var kv in hitsBy)
            if (kv.Key != null && kv.Key.currentHp > 0)
                DealTrue(kv.Key, stats.maxHp * 0.01f * Mathf.Floor(kv.Value));
    }

    // ── helper ──
    private Stats HighestHpEnemy(Vector3 center, float radius)
    {
        Stats best = null; float max = -1f;
        foreach (var e in EnemiesInRadius(center, radius))
            if (e.currentHp > max) { max = e.currentHp; best = e; }
        return best;
    }

    private IEnumerator VulnRoutine(Stats e, float pct, float dur)
    {
        if (e == null) yield break;
        e.damageTakenMultiplier += pct;
        yield return new WaitForSeconds(dur);
        if (e != null) e.damageTakenMultiplier -= pct;
    }
}
