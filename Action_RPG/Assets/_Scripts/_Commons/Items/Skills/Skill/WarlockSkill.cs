using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// WarlockSkill — "Crimson Tether".
/// Gắn 5 stack dấu ấn "Máu tươi" lên mọi kẻ địch quanh người (tồn tại 10s).
/// Khi đánh trúng kẻ địch có dấu ấn → tiêu hao 1 stack, gây thêm sát thương phép theo
/// lượng máu ĐÃ MẤT của mục tiêu, hồi 3% máu tối đa và +20% tốc chạy 3s (không cộng dồn).
/// Số stack tối đa luôn ≤ 5 dù tái kích hoạt sớm.
/// </summary>
public class WarlockSkill : SkillBehavior
{
    [Header("Dấu ấn")]
    public float castRadius = 5.0f;
    public int maxStacks = 5;
    public float markDuration = 10.0f;

    [Header("Sát thương")]
    [Tooltip("Sát thương phép thêm = % máu ĐÃ MẤT của mục tiêu (0.1 = 10%)")]
    public float missingHpDamagePercent = 0.10f;

    [Header("Buff / Hồi máu")]
    public float msBuffPercent = 0.20f;
    public float msBuffDuration = 3.0f;
    public float healPercent = 0.03f;

    [Header("VFX (tuỳ chọn)")]
    public GameObject markCastVfxPrefab;
    public GameObject markHitVfxPrefab;

    private Rigidbody rb;

    private float _effCastRadius;
    private float _effHealPercent;
    private float _effMsBuffPercent;
    private float _effMissingHpDmgPct;

    private class MarkData
    {
        public int stacks;
        public Coroutine expireRoutine;
        public GameObject auraVfx;
    }

    private Dictionary<Stats, MarkData> activeMarks = new Dictionary<Stats, MarkData>();
    private Coroutine msBuffCoroutine;
    private bool msBuffActive = false;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        rb = myPlayer.GetComponent<Rigidbody>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip()
    {
        stats.OnHitEnemy += HandleOnHitEnemy;
        RefreshEffectiveValues();
    }

    protected override void OnUnequip()
    {
        stats.OnHitEnemy -= HandleOnHitEnemy;

        foreach (var kvp in activeMarks)
        {
            if (kvp.Value.expireRoutine != null) StopCoroutine(kvp.Value.expireRoutine);
            if (kvp.Value.auraVfx != null) Destroy(kvp.Value.auraVfx);
        }
        activeMarks.Clear();

        if (msBuffActive)
        {
            if (msBuffCoroutine != null) StopCoroutine(msBuffCoroutine);
            stats.bonusMoveSpeed -= _effMsBuffPercent;
            stats.CalculateMoveSpeedOnly();
            msBuffActive = false;
            msBuffCoroutine = null;
        }
    }

    private void RefreshEffectiveValues()
    {
        // BattleMage U1: +20% tầm | BattleMage U3: +20% hồi máu
        // BloodReaver U1: +20% buff tốc chạy | BloodReaver U3: +20% sát thương theo máu mất
        float bmU1 = stats != null ? stats.battleMageSkillU1  : 0f;
        float bmU3 = stats != null ? stats.battleMageSkillU3  : 0f;
        float brU1 = stats != null ? stats.bloodReaverSkillU1 : 0f;
        float brU3 = stats != null ? stats.bloodReaverSkillU3 : 0f;

        _effCastRadius      = castRadius            * (1f + bmU1);
        _effHealPercent     = healPercent           * (1f + bmU3);
        _effMsBuffPercent   = msBuffPercent         * (1f + brU1);
        _effMissingHpDmgPct = missingHpDamagePercent * (1f + brU3);
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

        // VFX (tạm): vùng gắn dấu ấn.
        VisualDebugHelper.DrawSphere(transform.position, _effCastRadius, new Color(0.8f, 0f, 0.2f, 0.15f), 0.5f);
        if (markCastVfxPrefab) Instantiate(markCastVfxPrefab, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, _effCastRadius, player.dangerLayer);
        HashSet<Stats> done = new HashSet<Stats>();
        int markCount = 0;
        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !done.Add(e)) continue;
            ApplyMark(e);
            markCount++;
        }

        Debug.Log($"<color=magenta>WARLOCK:</color> Gắn dấu ấn {markCount} kẻ địch (tầm {_effCastRadius:F1}).");

        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    private void ApplyMark(Stats target)
    {
        if (activeMarks.TryGetValue(target, out MarkData mark))
        {
            // Tái kích hoạt: ĐẶT về tối đa (không cộng dồn vượt 5), làm mới thời gian.
            mark.stacks = maxStacks;
            if (mark.expireRoutine != null) StopCoroutine(mark.expireRoutine);
            mark.expireRoutine = StartCoroutine(MarkTimer(target));
        }
        else
        {
            MarkData newMark = new MarkData { stacks = maxStacks };
            newMark.auraVfx = MageVfxHelper.AttachSphere(target.transform, 0.7f, new Color(0.7f, 0f, 0.1f, 0.4f));
            newMark.auraVfx.transform.localPosition = Vector3.up * 1f;
            newMark.expireRoutine = StartCoroutine(MarkTimer(target));
            activeMarks.Add(target, newMark);
        }
    }

    private IEnumerator MarkTimer(Stats target)
    {
        yield return new WaitForSeconds(markDuration);
        RemoveMark(target);
    }

    private void RemoveMark(Stats target)
    {
        if (target == null) { return; }
        if (activeMarks.TryGetValue(target, out MarkData mark))
        {
            if (mark.expireRoutine != null) StopCoroutine(mark.expireRoutine);
            if (mark.auraVfx != null) Destroy(mark.auraVfx);
            activeMarks.Remove(target);
        }
    }

    private void HandleOnHitEnemy(Stats target, float t, bool isCrit)
    {
        if (target == null || !activeMarks.TryGetValue(target, out MarkData mark)) return;
        if (mark.stacks <= 0) return;

        mark.stacks--;

        // Sát thương phép thêm = % máu ĐÃ MẤT của mục tiêu (KHÔNG nhân skillMagicMultiplier).
        if (target.currentHp > 0)
        {
            float missingHp = target.maxHp - target.currentHp;
            float extraDamage = missingHp * _effMissingHpDmgPct;
            if (extraDamage > 0f)
            {
                target.TakeDamage(new DamageInfo
                {
                    magicDamage = extraDamage,
                    attacker = stats,
                    sourcePosition = transform.position,
                    isCrit = isCrit,
                    sourceType = DamageSourceType.Other
                });
            }
            if (markHitVfxPrefab) Instantiate(markHitVfxPrefab, target.transform.position, Quaternion.identity);
            VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up, 0.4f, new Color(1f, 0.1f, 0.2f, 0.5f), 0.15f);
        }

        TriggerBuffAndHeal();

        if (mark.stacks <= 0) RemoveMark(target);
    }

    private void TriggerBuffAndHeal()
    {
        // Hồi 3% máu tối đa mỗi lần tiêu stack.
        stats.Heal(stats.maxHp * _effHealPercent, true, false, HealSource.Skill);

        // +20% tốc chạy 3s — KHÔNG cộng dồn, chỉ làm mới thời gian.
        if (msBuffActive)
        {
            if (msBuffCoroutine != null) StopCoroutine(msBuffCoroutine);
            msBuffCoroutine = StartCoroutine(MsBuffTimer());
        }
        else
        {
            stats.bonusMoveSpeed += _effMsBuffPercent;
            stats.CalculateMoveSpeedOnly();
            msBuffActive = true;
            msBuffCoroutine = StartCoroutine(MsBuffTimer());
        }
    }

    private IEnumerator MsBuffTimer()
    {
        yield return new WaitForSeconds(msBuffDuration);
        stats.bonusMoveSpeed -= _effMsBuffPercent;
        stats.CalculateMoveSpeedOnly();
        msBuffActive = false;
        msBuffCoroutine = null;
    }
}
