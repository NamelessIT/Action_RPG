using System.Collections;
using UnityEngine;

/// <summary>
/// NGUYÊN MẪU 4 — BUFFER. Buff steroid cho Player.
/// Passive [Cộng Hưởng], Skill [Truyền Tải Dữ Liệu] (CD15s), Signature [Quá Tải Lõi] (120 Sin, CD16s).
/// Signature có các counter cộng dồn (crit/hit/sin/shield-block) trong cửa sổ hiệu lực.
/// </summary>
public class CompanionBufferBehavior : CompanionSkillBehavior
{
    public override CompanionArchetype Archetype => CompanionArchetype.Buffer;
    public override float SkillCooldown => 15f;
    public override float SignatureCooldown => 16f;
    public override float SignatureSinCost => 120f;

    private AllyStats _pl; // cache player stats
    private AllyStats Pl() { if (_pl == null) _pl = PlayerAlly(); return _pl; }

    private SkillManager PlayerSkillMgr()
    {
        if (player == null) return null;
        foreach (var sm in player.GetComponentsInChildren<SkillManager>())
            if (sm.isPlayer) return sm;
        return player.GetComponent<SkillManager>();
    }

    // ── Passive [Cộng Hưởng]: Player Perfect Dodge → +20% atkSpeed 3s; Player crit → +20% moveSpeed 3s (refresh) ──
    private float _atkUntil; private bool _atkActive;
    private float _msUntil;  private bool _msActive;

    protected override void OnPassiveEnable()
    {
        var pl = Pl();
        if (pl != null)
        {
            pl.OnPerfectDodgeTriggered += OnPlayerDodge;
            pl.OnHitEnemy += OnPlayerHit;
        }
    }
    protected override void OnPassiveDisable()
    {
        var pl = Pl();
        if (pl != null)
        {
            pl.OnPerfectDodgeTriggered -= OnPlayerDodge;
            pl.OnHitEnemy -= OnPlayerHit;
        }
    }
    private void OnPlayerDodge()
    {
        _atkUntil = Time.time + 3f;
        if (!_atkActive) StartCoroutine(AtkSpeedBuff());
    }
    private void OnPlayerHit(Stats v, float t, bool crit)
    {
        if (!crit) return;
        _msUntil = Time.time + 3f;
        if (!_msActive) StartCoroutine(MoveSpeedBuff());
    }
    private IEnumerator AtkSpeedBuff()
    {
        var pl = Pl(); if (pl == null) yield break;
        _atkActive = true; pl.bonusAttackSpeed += 0.20f; pl.CalculateCombatStatsOnly();
        while (Time.time < _atkUntil) yield return null;
        pl.bonusAttackSpeed -= 0.20f; pl.CalculateCombatStatsOnly(); _atkActive = false;
    }
    private IEnumerator MoveSpeedBuff()
    {
        var pl = Pl(); if (pl == null) yield break;
        _msActive = true; pl.bonusMoveSpeed += 0.20f; pl.CalculateMoveSpeedOnly();
        while (Time.time < _msUntil) yield return null;
        pl.bonusMoveSpeed -= 0.20f; pl.CalculateMoveSpeedOnly(); _msActive = false;
    }

    // ───────────────────────── SKILL [Truyền Tải Dữ Liệu] ─────────────────────────
    public override void ExecuteSkill(CompanionProtocolType? p)
    {
        var pl = Pl(); if (pl == null) return;
        switch (p)
        {
            case CompanionProtocolType.Artillery:
                TimedBuff(() => { pl.bonusCritChance += 0.15f; pl.bonusCritMultiplier += 0.30f; pl.CalculateCombatStatsOnly(); },
                         () => { pl.bonusCritChance -= 0.15f; pl.bonusCritMultiplier -= 0.30f; pl.CalculateCombatStatsOnly(); }, 5f);
                break;
            case CompanionProtocolType.Carnage:
                TimedBuff(() => { pl.bonusAttackSpeed += 0.20f; pl.physicalLifeSteal += 0.10f; pl.CalculateCombatStatsOnly(); },
                         () => { pl.bonusAttackSpeed -= 0.20f; pl.physicalLifeSteal -= 0.10f; pl.CalculateCombatStatsOnly(); }, 5f);
                break;
            case CompanionProtocolType.Suppression:
                TimedBuff(() => { pl.bonusMagicAtk += 0.20f; pl.bonusCdr += 0.10f; pl.RecalculateStats(); },
                         () => { pl.bonusMagicAtk -= 0.20f; pl.bonusCdr -= 0.10f; pl.RecalculateStats(); }, 5f);
                break;
            case CompanionProtocolType.Aegis:
                TimedBuff(() => { pl.PushSuperArmor(2); pl.armor += pl.armor * 0.30f; pl.magicResist += pl.magicResist * 0.30f; },
                         () => { pl.PopSuperArmor(2); pl.armor /= 1.30f; pl.magicResist /= 1.30f; }, 5f);
                break;
            default: // Cơ bản
                TimedBuff(() => pl.damageOutputMultiplier += 0.10f, () => pl.damageOutputMultiplier -= 0.10f, 5f);
                break;
        }
    }

    // ───────────────────────── SIGNATURE [Quá Tải Lõi] ─────────────────────────
    public override void ExecuteSignature(CompanionProtocolType? p)
    {
        var pl = Pl(); if (pl == null) return;
        switch (p)
        {
            case CompanionProtocolType.Artillery:   StartCoroutine(SigArtillery(pl, 8f)); break;
            case CompanionProtocolType.Carnage:     StartCoroutine(SigCarnage(pl, 8f)); break;
            case CompanionProtocolType.Suppression: StartCoroutine(SigSuppression(pl, 8f)); break;
            case CompanionProtocolType.Aegis:       StartCoroutine(SigAegis(pl, 8f)); break;
            default:                                StartCoroutine(SigBasic(pl, 8f)); break;
        }
    }

    // Cơ bản: +30% damageOutputMultiplier cho Player & Companion 8s.
    private IEnumerator SigBasic(AllyStats pl, float dur)
    {
        pl.damageOutputMultiplier += 0.30f; stats.damageOutputMultiplier += 0.30f;
        yield return new WaitForSeconds(dur);
        pl.damageOutputMultiplier -= 0.30f; stats.damageOutputMultiplier -= 0.30f;
    }

    // Artillery: -100% Stamina tiêu hao + 30% bonusPhysicalAtk + 30% bonusCritMultiplier 8s; mỗi crit +3% critMult (max +45% sau 5 crit).
    private IEnumerator SigArtillery(AllyStats pl, float dur)
    {
        float savedStaminaMult = pl.accStaminaConsumeMult;
        pl.accStaminaConsumeMult = 0f;
        pl.bonusPhysicalAtk += 0.30f; pl.bonusCritMultiplier += 0.30f; pl.CalculateCombatStatsOnly();
        int critStacks = 0;
        System.Action<Stats, float, bool> onHit = (v, t, c) =>
        {
            if (c && critStacks < 5) { critStacks++; pl.bonusCritMultiplier += 0.03f; pl.CalculateCombatStatsOnly(); }
        };
        pl.OnHitEnemy += onHit;
        yield return new WaitForSeconds(dur);
        pl.OnHitEnemy -= onHit;
        pl.accStaminaConsumeMult = savedStaminaMult;
        pl.bonusPhysicalAtk -= 0.30f; pl.bonusCritMultiplier -= (0.30f + 0.03f * critStacks); pl.CalculateCombatStatsOnly();
    }

    // Carnage: +15% bonusPhysicalAtk + 20% bonusAttackSpeed + 10% physicalLifeSteal 8s; mỗi 3 đòn trúng +5% physAtk (max +30% sau 9 đòn).
    private IEnumerator SigCarnage(AllyStats pl, float dur)
    {
        pl.bonusPhysicalAtk += 0.15f; pl.bonusAttackSpeed += 0.20f; pl.physicalLifeSteal += 0.10f; pl.CalculateCombatStatsOnly();
        int hits = 0, physStacks = 0;
        System.Action<Stats, float, bool> onHit = (v, t, c) =>
        {
            hits++;
            if (hits % 3 == 0 && physStacks < 6) { physStacks++; pl.bonusPhysicalAtk += 0.05f; pl.CalculateCombatStatsOnly(); }
        };
        pl.OnHitEnemy += onHit;
        yield return new WaitForSeconds(dur);
        pl.OnHitEnemy -= onHit;
        pl.bonusPhysicalAtk -= (0.15f + 0.05f * physStacks); pl.bonusAttackSpeed -= 0.20f; pl.physicalLifeSteal -= 0.10f; pl.CalculateCombatStatsOnly();
    }

    // Suppression: reset CD skill Player + 100% bonusSinGain + 10% bonusMagicAtk 8s; mỗi 50 Sin nhận được +10% magicAtk (max +30% sau 100 Sin).
    private IEnumerator SigSuppression(AllyStats pl, float dur)
    {
        var psm = PlayerSkillMgr();
        if (psm != null) psm.ResetAllCooldowns();
        pl.bonusSinGain += 1.0f; pl.bonusMagicAtk += 0.10f; pl.RecalculateStats();
        int magicStacks = 0;
        float sinAccum = 0f, lastSin = pl.currentSin;
        float t = 0f;
        while (t < dur)
        {
            float delta = pl.currentSin - lastSin;
            if (delta > 0f) sinAccum += delta;
            lastSin = pl.currentSin;
            while (sinAccum >= 50f && magicStacks < 2) { sinAccum -= 50f; magicStacks++; pl.bonusMagicAtk += 0.10f; pl.RecalculateStats(); }
            t += Time.deltaTime;
            yield return null;
        }
        pl.bonusSinGain -= 1.0f; pl.bonusMagicAtk -= (0.10f + 0.10f * magicStacks); pl.RecalculateStats();
    }

    // Aegis: Shield = 50% maxHp 8s; mỗi 2.5% maxHp chặn được +1 "Hiệu quả"; hết giờ/hết shield → nổ r=3f True = Hiệu quả × (50% Armor + 50% MR Companion).
    private IEnumerator SigAegis(AllyStats pl, float dur)
    {
        float granted = pl.maxHp * 0.50f;
        pl.AddShield(granted, dur);
        float remaining = granted, blocked = 0f;
        System.Action<DamageInfo> onHit = info =>
        {
            if (info == null || remaining <= 0f) return;
            float dmg = info.TotalRawDamage * pl.damageTakenMultiplier;
            float absorb = Mathf.Min(remaining, dmg);
            blocked += absorb; remaining -= absorb;
        };
        pl.OnBeforeTakeDamage += onHit;
        float t = 0f;
        while (t < dur && remaining > 0f) { t += Time.deltaTime; yield return null; }
        pl.OnBeforeTakeDamage -= onHit;

        float unit = pl.maxHp * 0.025f;
        int effectiveness = unit > 0f ? Mathf.FloorToInt(blocked / unit) : 0;
        if (effectiveness > 0)
        {
            float dmgEach = effectiveness * (stats.armor * 0.5f + stats.magicResist * 0.5f);
            foreach (var e in EnemiesInRadius(pl.transform.position, 3f)) DealTrue(e, dmgEach);
            VisualDebugHelper.DrawSphere(pl.transform.position, 3f, new Color(0.3f, 0.6f, 1f, 0.5f), 0.5f);
            Debug.Log($"<color=cyan>[Companion-Buffer]</color> Aegis nổ: Hiệu quả {effectiveness} → {dmgEach:F0} True/địch.");
        }
    }
}
