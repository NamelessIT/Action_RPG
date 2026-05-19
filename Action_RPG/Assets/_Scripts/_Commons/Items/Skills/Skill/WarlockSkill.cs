using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WarlockSkill : SkillBehavior
{
    [Header("Mark Settings")]
    public float castRadius = 5.0f;
    public int maxStacks = 5;
    public float markDuration = 10.0f;

    [Header("Damage Settings")]
    [Tooltip("Sát thương phép thêm = % Máu đã mất (0.1 = 10%)")]
    public float missingHpDamagePercent = 0.10f;

    [Header("Buff Settings")]
    public float msBuffPercent = 0.20f;
    public float msBuffDuration = 3.0f;
    public float healPercent = 0.03f;

    [Header("VFX")]
    public GameObject markCastVfxPrefab;
    public GameObject markHitVfxPrefab;

    private Rigidbody rb;
    private AllyStats allyStats;

    // Effective values cached at Use() time — needed for correct cleanup
    private float _effectiveCastRadius;
    private float _effectiveHealPercent;
    private float _effectiveMsBuffPercent;
    private float _effectiveMissingHpDmgPct;

    private class MarkData
    {
        public int stacks;
        public Coroutine expireRoutine;
    }

    private Dictionary<Stats, MarkData> activeMarks = new Dictionary<Stats, MarkData>();
    private Coroutine msBuffCoroutine;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        allyStats = myStats;
    }

    protected override void OnEquip()
    {
        stats.OnHitEnemy += HandleOnHitEnemy;
        // Compute effective values on equip so they are ready
        RefreshEffectiveValues();
    }

    protected override void OnUnequip()
    {
        stats.OnHitEnemy -= HandleOnHitEnemy;

        foreach (var kvp in activeMarks)
        {
            if (kvp.Value.expireRoutine != null) StopCoroutine(kvp.Value.expireRoutine);
        }
        activeMarks.Clear();

        if (msBuffCoroutine != null)
        {
            StopCoroutine(msBuffCoroutine);
            stats.bonusMoveSpeed -= _effectiveMsBuffPercent;
            stats.CalculateMoveSpeedOnly();
            msBuffCoroutine = null;
        }
    }

    private void RefreshEffectiveValues()
    {
        // BattleMage U1: +20% range (castRadius) | BattleMage U3: +20% heal
        // BloodReaver U1: +20% msBuffPercent      | BloodReaver U3: +20% missingHpDamagePercent
        float bmU1 = allyStats != null ? allyStats.battleMageSkillU1  : 0f;
        float bmU3 = allyStats != null ? allyStats.battleMageSkillU3  : 0f;
        float brU1 = allyStats != null ? allyStats.bloodReaverSkillU1 : 0f;
        float brU3 = allyStats != null ? allyStats.bloodReaverSkillU3 : 0f;

        _effectiveCastRadius       = castRadius            * (1f + bmU1);
        _effectiveHealPercent      = healPercent           * (1f + bmU3);
        _effectiveMsBuffPercent    = msBuffPercent         * (1f + brU1);
        _effectiveMissingHpDmgPct  = missingHpDamagePercent * (1f + brU3);
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        RefreshEffectiveValues();
        StartCoroutine(CastRoutine());
        return true;
    }

    private IEnumerator CastRoutine()
    {
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(transform.position, _effectiveCastRadius, player.dangerLayer);
        int markCount = 0;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                ApplyMark(enemyStats);
                markCount++;
            }
        }

        Debug.Log($"<color=magenta>Warlock:</color> Marked {markCount} enemies (radius {_effectiveCastRadius:F1})");

        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    private void ApplyMark(Stats target)
    {
        if (activeMarks.ContainsKey(target))
        {
            MarkData markData = activeMarks[target];
            markData.stacks = maxStacks;
            if (markData.expireRoutine != null) StopCoroutine(markData.expireRoutine);
            markData.expireRoutine = StartCoroutine(MarkTimer(target));
        }
        else
        {
            MarkData markData = new MarkData();
            markData.stacks = maxStacks;
            markData.expireRoutine = StartCoroutine(MarkTimer(target));
            activeMarks.Add(target, markData);
        }
    }

    private IEnumerator MarkTimer(Stats target)
    {
        yield return new WaitForSeconds(markDuration);

        if (target != null && activeMarks.ContainsKey(target))
            activeMarks.Remove(target);
    }

    private void HandleOnHitEnemy(Stats target, float t, bool isCrit)
    {
        if (target != null && activeMarks.ContainsKey(target))
        {
            MarkData mark = activeMarks[target];

            if (mark.stacks > 0)
            {
                mark.stacks--;

                if (target.currentHp > 0)
                {
                    float missingHp = target.maxHp - target.currentHp;
                    // BloodReaver U3 scales missingHpDamagePercent
                    float extraDamage = missingHp * _effectiveMissingHpDmgPct;
                    if (data != null) extraDamage *= data.skillMagicMultiplier;

                    DamageInfo extraInfo = new DamageInfo();
                    extraInfo.magicDamage = extraDamage;
                    extraInfo.sourcePosition = transform.position;
                    extraInfo.isCrit = isCrit;
                    extraInfo.isKnockback = false;
                    extraInfo.isStun = false;

                    target.TakeDamage(extraInfo);
                }

                TriggerBuffAndHeal();

                if (mark.stacks <= 0)
                {
                    if (mark.expireRoutine != null) StopCoroutine(mark.expireRoutine);
                    activeMarks.Remove(target);
                }
            }
        }
    }

    private void TriggerBuffAndHeal()
    {
        // BattleMage U3 scales healPercent
        float healAmount = stats.maxHp * _effectiveHealPercent;
        stats.Heal(healAmount);

        // BloodReaver U1 scales msBuffPercent
        if (msBuffCoroutine != null)
        {
            StopCoroutine(msBuffCoroutine);
            msBuffCoroutine = StartCoroutine(MsBuffTimer());
        }
        else
        {
            stats.bonusMoveSpeed += _effectiveMsBuffPercent;
            stats.CalculateMoveSpeedOnly();
            msBuffCoroutine = StartCoroutine(MsBuffTimer());
        }
    }

    private IEnumerator MsBuffTimer()
    {
        yield return new WaitForSeconds(msBuffDuration);
        stats.bonusMoveSpeed -= _effectiveMsBuffPercent;
        stats.CalculateMoveSpeedOnly();
        msBuffCoroutine = null;
    }
}
