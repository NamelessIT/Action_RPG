using System.Collections;
using UnityEngine;

/// <summary>
/// NGUYÊN MẪU 2 — SUSTAIN. Cỗ máy bơm máu / chống chịu cho Player.
/// Passive [Tiếp Tế Sinh Học], Skill [Giao Thức Chữa Trị] (CD15s), Signature [Nguồn Dự Phòng] (120 Sin, CD16s).
/// Phần lớn hiệu ứng tác động lên PLAYER.
/// </summary>
public class CompanionSustainBehavior : CompanionSkillBehavior
{
    public override CompanionArchetype Archetype => CompanionArchetype.Sustain;
    public override float SkillCooldown => 15f;
    public override float SignatureCooldown => 16f;
    public override float SignatureSinCost => 120f;

    // ── Passive [Tiếp Tế Sinh Học]: mỗi 10s rơi 1 Lõi Hồi Phục (4s) quanh Companion 2f, Player nhặt → +5% maxHp +10 Stamina ──
    private float _nextCore = 0f;
    private void Update()
    {
        if (stats == null || stats.isDead) return;
        if (Time.time >= _nextCore)
        {
            _nextCore = Time.time + 10f;
            Vector2 r = Random.insideUnitCircle * 2f;
            Vector3 pos = transform.position + new Vector3(r.x, 0f, r.y);
            StartCoroutine(HealCoreRoutine(pos));
        }
    }
    private IEnumerator HealCoreRoutine(Vector3 pos)
    {
        float t = 0f;
        AllyStats p = PlayerAlly();
        while (t < 4f)
        {
            VisualDebugHelper.DrawSphere(pos, 0.5f, new Color(0.2f, 1f, 0.5f, 0.5f), 0.15f);
            if (p != null && !p.isDead && Vector3.Distance(p.transform.position, pos) <= 0.8f)
            {
                p.Heal(p.maxHp * 0.05f, true, false, HealSource.Potion);
                p.currentStamina = Mathf.Min(p.maxStamina, p.currentStamina + 10f);
                yield break;
            }
            t += 0.15f;
            yield return new WaitForSeconds(0.15f);
        }
    }

    // ───────────────────────── SKILL [Giao Thức Chữa Trị] ─────────────────────────
    public override void ExecuteSkill(CompanionProtocolType? p)
    {
        AllyStats pl = PlayerAlly();
        if (pl == null) return;
        switch (p)
        {
            case CompanionProtocolType.Artillery:
                pl.Heal(pl.maxHp * 0.10f, true, false, HealSource.Skill);
                TimedBuff(() => { pl.bonusMoveSpeed += 0.20f; pl.CalculateMoveSpeedOnly(); },
                         () => { pl.bonusMoveSpeed -= 0.20f; pl.CalculateMoveSpeedOnly(); }, 3f);
                // "Miễn nhiễm làm chậm" — game chưa có hệ slow lên player → bỏ qua (ghi chú).
                break;
            case CompanionProtocolType.Carnage:
                TimedBuff(() => { pl.bonusAttackSpeed += 0.20f; pl.physicalLifeSteal += 0.10f; pl.CalculateCombatStatsOnly(); },
                         () => { pl.bonusAttackSpeed -= 0.20f; pl.physicalLifeSteal -= 0.10f; pl.CalculateCombatStatsOnly(); }, 4f);
                break;
            case CompanionProtocolType.Suppression:
                StartCoroutine(CoolStreamRoutine(5f)); // Suối Mát (tài liệu không nêu thời lượng → 5s)
                break;
            case CompanionProtocolType.Aegis:
                pl.AddShield(pl.maxHp * 0.30f, 5f);
                break;
            default: // Cơ bản
                pl.Heal(pl.maxHp * 0.10f, true, false, HealSource.Skill);
                break;
        }
    }

    // Suối Mát: Player luôn ở tâm → +20% moveSpeed suốt thời gian; Companion +20% khi trong 3f của Player.
    private IEnumerator CoolStreamRoutine(float dur)
    {
        AllyStats pl = PlayerAlly();
        if (pl == null) yield break;
        pl.bonusMoveSpeed += 0.20f; pl.CalculateMoveSpeedOnly();
        bool compBuffed = false;
        float t = 0f;
        while (t < dur)
        {
            bool inZone = Vector3.Distance(transform.position, pl.transform.position) <= 3f;
            if (inZone && !compBuffed) { stats.bonusMoveSpeed += 0.20f; stats.CalculateMoveSpeedOnly(); compBuffed = true; }
            else if (!inZone && compBuffed) { stats.bonusMoveSpeed -= 0.20f; stats.CalculateMoveSpeedOnly(); compBuffed = false; }
            VisualDebugHelper.DrawSphere(pl.transform.position, 3f, new Color(0.3f, 0.8f, 1f, 0.25f), 0.2f);
            t += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }
        pl.bonusMoveSpeed -= 0.20f; pl.CalculateMoveSpeedOnly();
        if (compBuffed) { stats.bonusMoveSpeed -= 0.20f; stats.CalculateMoveSpeedOnly(); }
    }

    // ───────────────────────── SIGNATURE [Nguồn Dự Phòng] ─────────────────────────
    public override void ExecuteSignature(CompanionProtocolType? p)
    {
        AllyStats pl = PlayerAlly();
        if (pl == null) return;
        switch (p)
        {
            case CompanionProtocolType.Artillery:   StartCoroutine(ReviveStationRoutine(pl, 5f)); break;
            case CompanionProtocolType.Carnage:     StartCoroutine(LifeLinkRoutine(5f)); break;
            case CompanionProtocolType.Suppression: SuppressionSig(pl); break;
            case CompanionProtocolType.Aegis:       StartCoroutine(MartyrRoutine(pl, 8f)); break;
            default:                                StartCoroutine(RegenRoutine(pl, 10f)); break;
        }
    }

    // Cơ bản: 10s, mỗi giây hồi 5% maxHp cho Player.
    private IEnumerator RegenRoutine(AllyStats pl, float dur)
    {
        for (float t = 0; t < dur && pl != null && !pl.isDead; t += 1f)
        {
            pl.Heal(pl.maxHp * 0.05f, false, false, HealSource.Skill);
            yield return new WaitForSeconds(1f);
        }
    }

    // Artillery: 5s, nếu Player nhận sát thương chí mạng → hồi sinh với 100% máu.
    private IEnumerator ReviveStationRoutine(AllyStats pl, float dur)
    {
        bool used = false;
        System.Action<DamageInfo> handler = info =>
        {
            if (used || info == null) return;
            float dmgToHp = info.TotalRawDamage * pl.damageTakenMultiplier - pl.currentShield;
            if (dmgToHp >= pl.currentHp)
            {
                used = true;
                info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f;
                pl.currentHp = pl.maxHp; // Trạm y tế nổ → hồi đầy
                Debug.Log("<color=green>[Companion-Sustain]</color> Trạm y tế cứu mạng! Hồi 100% HP.");
            }
        };
        pl.OnBeforeTakeDamage += handler;
        yield return new WaitForSeconds(dur);
        pl.OnBeforeTakeDamage -= handler;
    }

    // Carnage: 5s, 100% sát thương Companion gây ra → HP cho Player. (Xấp xỉ theo lượng atk chính mỗi đòn — chưa có event damage-amount của companion.)
    private IEnumerator LifeLinkRoutine(float dur)
    {
        AllyStats pl = PlayerAlly();
        System.Action<Stats, float, bool> handler = (victim, t, crit) =>
        {
            if (pl == null) return;
            float est = Mathf.Max(stats.physicalAtk, stats.magicAtk); // ước lượng sát thương 1 đòn của companion
            pl.Heal(est, false, false, HealSource.Skill);
        };
        stats.OnHitEnemy += handler;
        yield return new WaitForSeconds(dur);
        stats.OnHitEnemy -= handler;
    }

    // Suppression: xóa Debuff trên Player + bất tử Player & Companion 3s.
    private void SuppressionSig(AllyStats pl)
    {
        pl.BreakCrowdControl(); // xóa choáng/đẩy (hệ debuff khác chưa có hệ thống → bỏ qua)
        StartCoroutine(InvincibleRoutine(pl, stats, 3f));
    }
    private IEnumerator InvincibleRoutine(AllyStats a, AllyStats b, float dur)
    {
        if (a != null) a.isInvincible = true;
        if (b != null) b.isInvincible = true;
        yield return new WaitForSeconds(dur);
        if (a != null) a.isInvincible = false;
        if (b != null) b.isInvincible = false;
    }

    // Aegis: Companion gánh 100% sát thương Player nhận vào 8s (khi Companion hết máu để gánh → Player chịu).
    private IEnumerator MartyrRoutine(AllyStats pl, float dur)
    {
        System.Action<DamageInfo> handler = info =>
        {
            if (info == null || stats == null || stats.isDead) return;
            float dmg = info.TotalRawDamage;
            if (dmg <= 0f) return;
            // Companion còn đủ máu để gánh?
            if (stats.currentHp + stats.currentShield > 0f)
            {
                DamageInfo redirect = new DamageInfo
                {
                    physDamage = info.physDamage, magicDamage = info.magicDamage, trueDamage = info.trueDamage,
                    attacker = info.attacker, sourcePosition = info.sourcePosition
                };
                info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f; // Player không mất máu
                stats.TakeDamage(redirect);
            }
            // Companion chết/không đủ → để nguyên info, Player chịu.
        };
        pl.OnBeforeTakeDamage += handler;
        yield return new WaitForSeconds(dur);
        pl.OnBeforeTakeDamage -= handler;
    }
}
