using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class JuggernautSkill : SkillBehavior
{
    [Header("Stance Phase")]
    public float stanceDuration = 3.0f;
    public float defenseValueBonus = 150f;

    [Header("Counter Attack Settings")]
    public float armorScaling = 1.5f; // Scale theo Armor
    public float msScaling = 1.5f;    // Scale theo Magic Resist
    public float stunDuration = 1.5f;

    [Header("Low HP Scaling")]
    public float baseRadius = 3.0f;
    public float maxBonusRadius = 2.0f;
    public float baseKnockback = 5f;
    public float maxBonusKnockback = 5f;

    [Header("Buffs")]
    public float healCapPercent = 0.3f;
    public float bonusSpeed = 0.2f;
    public float speedBuffDuration = 3.0f;

    private bool isStanceActive = false;
    private Coroutine stanceCoroutine;
    private GameObject currentAuraInstance;
    private EquipmentManager equipmentManager;
    private AllyStats allyStats;

    // Cached effective values set when stance starts
    private float _effectiveDefBonus;
    private float _effectiveArmorScaling;
    private float _effectiveMSScaling;
    private float _effectiveBonusSpeed;
    private float _effectiveBRU3; // BloodReaver U3 applied to finalMultiplier in counter

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        allyStats = myStats;
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        if (isStanceActive)
        {
            EndStance(false);
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartStance();
        return true;
    }

    private void StartStance()
    {
        // Vanguard U1: +20% Defense Value buff
        // Vanguard U3: +20% Armor/MR scale damage (in counter)
        // BloodReaver U1: +20% bonusMoveSpeed buff
        // BloodReaver U3: +20% scale damage (finalMultiplier in counter)
        float vU1  = allyStats != null ? allyStats.vanguardSkillU1    : 0f;
        float vU3  = allyStats != null ? allyStats.vanguardSkillU3    : 0f;
        float brU1 = allyStats != null ? allyStats.bloodReaverSkillU1 : 0f;
        float brU3 = allyStats != null ? allyStats.bloodReaverSkillU3 : 0f;

        _effectiveDefBonus    = defenseValueBonus * (1f + vU1);
        _effectiveArmorScaling = armorScaling      * (1f + vU3);
        _effectiveMSScaling    = msScaling         * (1f + vU3);
        _effectiveBonusSpeed   = bonusSpeed        * (1f + brU1);
        _effectiveBRU3         = brU3;

        isStanceActive = true;
        player.isStance = true;

        stats.defenseValue += _effectiveDefBonus;
        Debug.Log($"<color=red>Juggernaut Stance!</color> Def +{_effectiveDefBonus}");

        stats.OnDamageReceived += OnHitTrigger;

        stanceCoroutine = StartCoroutine(StanceTimer());
    }

    private IEnumerator StanceTimer()
    {
        yield return new WaitForSeconds(stanceDuration);
        EndStance(false);
        Debug.Log("Juggernaut: Hết thời gian, không phản đòn.");
    }

    private void OnHitTrigger(float damageTaken, Stats target)
    {
        if (!isStanceActive) return;
        Debug.Log("<color=yellow>Juggernaut: KÍCH HOẠT PHẢN ĐÒN!</color>");
        EndStance(true);
    }

    private void EndStance(bool triggerCounter)
    {
        if (!isStanceActive) return;

        isStanceActive = false;
        player.isStance = false;
        stats.defenseValue -= _effectiveDefBonus;
        stats.OnDamageReceived -= OnHitTrigger;

        if (stanceCoroutine != null) StopCoroutine(stanceCoroutine);
        if (currentAuraInstance != null) Destroy(currentAuraInstance);

        if (triggerCounter)
        {
            StartCoroutine(CounterAttackRoutine());
        }
    }

    private IEnumerator CounterAttackRoutine()
    {
        player.isAttacking = true;

        float hpPercent = stats.currentHp / stats.maxHp;
        float missingHpRatio = 1.0f - hpPercent;

        float finalRadius    = baseRadius    + (missingHpRatio * maxBonusRadius);
        float finalKnockback = baseKnockback + (missingHpRatio * maxBonusKnockback);

        Debug.Log($"Counter: HP {hpPercent * 100:F0}% -> Radius: {finalRadius:F1}, Knockback: {finalKnockback:F1}");

        Collider[] hits = Physics.OverlapSphere(transform.position, finalRadius, player.dangerLayer);
        float totalDamageDealt = 0f;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                float dmg = ApplyCounterDamage(enemyStats, finalKnockback);
                totalDamageDealt += dmg;
            }
        }

        if (totalDamageDealt > 0)
        {
            float healAmount = Mathf.Min(totalDamageDealt, stats.maxHp * healCapPercent);
            stats.currentHp += healAmount;
            if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;
            Debug.Log($"<color=green>Hồi phục: {healAmount} HP</color>");
        }

        StartCoroutine(SpeedBuffRoutine());

        yield return new WaitForSeconds(0.4f);
        player.isAttacking = false;
    }

    private float ApplyCounterDamage(Stats enemyStats, float knockbackForce)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;

        // Vanguard U3 scales Armor/MR contribution
        float scalingDmg = (stats.armor * _effectiveArmorScaling) + (stats.magicResist * _effectiveMSScaling);

        float basePhyAtk = stats.physicalAtk;
        if (basePhyAtk <= 0) basePhyAtk = 1f;

        float standardSkillDmg = basePhyAtk * data.skillPhysicalMultiplier;
        float totalRawDamage   = standardSkillDmg + scalingDmg;

        float finalMultiplier = 1.0f;
        if (standardSkillDmg > 0)
            finalMultiplier = totalRawDamage / standardSkillDmg;

        // BloodReaver U3: +20% overall damage
        finalMultiplier *= (1f + _effectiveBRU3);

        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        var dmgTuple = CombatMath.CalculateFullDamage(stats, enemyStats, t, isCrit, data, currentWpn, finalMultiplier);

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.physDamage = dmgTuple.phys;
        info.magicDamage = dmgTuple.magic;
        info.isStun = true;
        info.stunDuration = stunDuration;
        info.isKnockback = true;
        info.knockbackForce = knockbackForce;
        info.attacker = stats;

        enemyStats.TakeDamage(info);

        return dmgTuple.phys + dmgTuple.magic + dmgTuple.trueDmg;
    }

    private IEnumerator SpeedBuffRoutine()
    {
        // BloodReaver U1 scales bonusSpeed
        stats.bonusMoveSpeed += _effectiveBonusSpeed;
        stats.CalculateMoveSpeedOnly();
        yield return new WaitForSeconds(speedBuffDuration);
        stats.bonusMoveSpeed -= _effectiveBonusSpeed;
        stats.CalculateMoveSpeedOnly();
    }
}
