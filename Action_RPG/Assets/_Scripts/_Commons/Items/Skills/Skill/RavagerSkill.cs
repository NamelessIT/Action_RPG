using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RavagerSkill : SkillBehavior
{
    [Header("Berserk Settings")]
    public float duration = 5.0f;
    public float scanRange = 10.0f;
    public float stopDistance = 0.8f;

    [Header("Buffs")]
    public float shieldPercent = 0.5f;
    public float atkSpeedBonus = 2.0f;
    public float physAtkPercent = 2.0f;
    public float critMultBonus = 0.2f;
    public float moveSpeedBonus = 0.2f;

    [Header("Finisher")]
    public float healOnEndPercent = 0.3f;
    public float stunRadius = 2.0f;
    public float stunDuration = 1.5f;

    [Header("VFX")]
    public GameObject berserkerVfxPrefab;
    public GameObject finisherVfxPrefab;

    private EquipmentManager equipmentManager;
    private BloodReaverPassive bloodPassive;
    private float originalHpCost = 0f;
    private AllyStats allyStats;

    private float _appliedPhysAtkPercent;
    private float _appliedAtkSpeedBonus;
    private float _appliedMoveSpeedBonus;
    private float _appliedStunDur;

    private Coroutine berserkCoroutine;
    private bool _berserkActive = false;
    private bool _healingBlockPushed = false;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        bloodPassive = myPlayer.GetComponent<BloodReaverPassive>();
        allyStats = myStats;
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        if (berserkCoroutine != null)
        {
            StopCoroutine(berserkCoroutine);
            berserkCoroutine = null;
        }

        EndBerserk(false);
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (berserkCoroutine != null)
        {
            StopCoroutine(berserkCoroutine);
            berserkCoroutine = null;
        }

        if (_berserkActive) EndBerserk(false);

        berserkCoroutine = StartCoroutine(BerserkRoutine());
        return true;
    }

    private IEnumerator BerserkRoutine()
    {
        float wU1 = allyStats != null ? allyStats.warriorSkillU1 : 0f;
        float wU3 = allyStats != null ? allyStats.warriorSkillU3 : 0f;
        float brU1 = allyStats != null ? allyStats.bloodReaverSkillU1 : 0f;
        float brU3 = allyStats != null ? allyStats.bloodReaverSkillU3 : 0f;

        _appliedStunDur = stunDuration * (1f + wU1);
        _appliedPhysAtkPercent = physAtkPercent * (1f + wU3 + brU3);
        _appliedMoveSpeedBonus = moveSpeedBonus * (1f + brU1);
        _appliedAtkSpeedBonus = atkSpeedBonus;

        _berserkActive = true;
        player.isBerserk = true;
        stats.PushHealingBlock();
        _healingBlockPushed = true;

        float shieldAmount = stats.maxHp * shieldPercent;
        stats.AddShield(shieldAmount, duration);

        ApplyBuffs();

        if (bloodPassive != null)
        {
            originalHpCost = bloodPassive.hpCostPercent;
            bloodPassive.hpCostPercent = 0f;
        }

        Debug.Log("<color=red>RAVAGER: BERSERK!</color>");

        float timer = 0f;
        while (timer < duration)
        {
            Stats target = FindNearestEnemy();

            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist <= player.attackRange + 0.2f)
                {
                    Vector3 dir = (target.transform.position - transform.position).normalized;
                    stats.facingDirection = dir;
                    player.AI_Attack();
                }
                else
                {
                    player.AI_MoveTo(target.transform.position);
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        berserkCoroutine = null;
        EndBerserk(true);
    }

    private void EndBerserk(bool triggerFinisher)
    {
        if (!_berserkActive) return;
        _berserkActive = false;

        player.isBerserk = false;

        if (_healingBlockPushed)
        {
            stats.PopHealingBlock();
            _healingBlockPushed = false;
        }

        stats.currentShield = 0f;

        RemoveBuffs();

        if (bloodPassive != null)
            bloodPassive.hpCostPercent = originalHpCost;

        if (triggerFinisher)
        {
            float healAmt = stats.maxHp * healOnEndPercent;
            stats.Heal(healAmt, true, false, HealSource.Skill);
            Debug.Log($"<color=green>Ravager End: Heal {healAmt} HP</color>");

            Collider[] hits = Physics.OverlapSphere(transform.position, stunRadius, player.dangerLayer);
            foreach (var hit in hits)
            {
                Stats enemy = hit.GetComponent<Stats>();
                if (enemy != null && enemy.currentHp > 0)
                {
                    DamageInfo info = new DamageInfo();
                    info.physDamage = 0;
                    info.attacker = stats;
                    info.sourcePosition = transform.position;
                    // [CC] Stun finisher qua effect system (giữ duration đã upgrade _appliedStunDur).
                    info.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, _appliedStunDur)
                    { sourcePosition = transform.position });
                    enemy.TakeDamage(info);
                }
            }
        }
    }

    private void ApplyBuffs()
    {
        stats.bonusPhysicalAtk += _appliedPhysAtkPercent;
        stats.bonusAttackSpeed += _appliedAtkSpeedBonus;
        stats.bonusCritMultiplier += critMultBonus;
        stats.bonusMoveSpeed += _appliedMoveSpeedBonus;

        stats.CalculateCombatStatsOnly();
        stats.CalculateMoveSpeedOnly();
    }

    private void RemoveBuffs()
    {
        stats.bonusPhysicalAtk -= _appliedPhysAtkPercent;
        stats.bonusAttackSpeed -= _appliedAtkSpeedBonus;
        stats.bonusCritMultiplier -= critMultBonus;
        stats.bonusMoveSpeed -= _appliedMoveSpeedBonus;

        stats.CalculateCombatStatsOnly();
        stats.CalculateMoveSpeedOnly();
    }

    private Stats FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, scanRange, player.dangerLayer);
        Stats nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            Stats s = hit.GetComponent<Stats>();
            if (s != null && s.currentHp > 0)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = s;
                }
            }
        }

        return nearest;
    }
}
