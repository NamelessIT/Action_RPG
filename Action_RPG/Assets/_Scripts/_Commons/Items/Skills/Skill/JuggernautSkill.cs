using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// JuggernautSkill — "Blood Counter".
/// Giơ vũ khí thủ thế (Stance) tối đa 3s, +150 Defense Value, tỏa khí đỏ.
/// Trong lúc thủ thế: KHÔNG đánh thường / dùng signature / di chuyển.
/// Nếu bị đánh trúng → kết thúc thủ thế, phản đòn quét ngang một lần:
/// 150% physicalAtk + 150% Armor + 150% MR (một lần damage duy nhất), làm choáng + đẩy lùi.
/// Máu càng thấp → tầm quét rộng & đẩy lùi mạnh hơn. Hồi máu theo sát thương gây ra (≤30%).
/// Sau đó +20% tốc độ di chuyển trong 3s.
/// </summary>
public class JuggernautSkill : SkillBehavior
{
    [Header("Thủ thế (Stance)")]
    public float stanceDuration = 3.0f;
    public float defenseValueBonus = 150f;

    [Header("Phản đòn — sát thương")]
    public float baseCounterMultiplier = 1.5f; // 150% physicalAtk
    public float armorScaling = 1.5f;          // +150% Armor
    public float msScaling = 1.5f;             // +150% Magic Resist
    public float stunDuration = 2f;
    public int counterImpactLevel = 2;         // phá siêu giáp để choáng chắc

    [Header("Phản đòn — tầm & đẩy lùi (scale theo % máu mất)")]
    public float baseRadius = 3.0f;
    public float maxBonusRadius = 2.0f;
    public float baseKnockback = 5f;
    public float maxBonusKnockback = 5f;

    [Header("Hồi máu & buff")]
    public float healRatio = 1.0f;        // 100% sát thương gây ra
    public float healCapPercent = 0.3f;   // tối đa 30% maxHp
    public float bonusSpeed = 0.2f;       // +20% move speed
    public float speedBuffDuration = 3.0f;

    [Header("VFX (tuỳ chọn)")]
    public GameObject stanceAuraVfxPrefab;

    private bool isStanceActive = false;
    private Coroutine stanceCoroutine;
    private GameObject currentAuraInstance;
    private GameObject debugAura;
    private EquipmentManager equipmentManager;

    // Giá trị hiệu lực (chốt khi vào thủ thế, đã tính node nâng cấp)
    private float _effDefBonus;
    private float _effArmorScaling;
    private float _effMSScaling;
    private float _effBonusSpeed;
    private float _effCounterMult;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        if (isStanceActive) EndStance(false);
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartStance();
        return true;
    }

    private void StartStance()
    {
        // Vanguard U1: +20% Defense Value | Vanguard U3: +20% scale Armor/MR
        // BloodReaver U1: +20% move speed | BloodReaver U3: +20% hệ số sát thương phản đòn
        float vU1  = stats != null ? stats.vanguardSkillU1    : 0f;
        float vU3  = stats != null ? stats.vanguardSkillU3    : 0f;
        float brU1 = stats != null ? stats.bloodReaverSkillU1 : 0f;
        float brU3 = stats != null ? stats.bloodReaverSkillU3 : 0f;

        _effDefBonus     = defenseValueBonus    * (1f + vU1);
        _effArmorScaling = armorScaling         * (1f + vU3);
        _effMSScaling    = msScaling            * (1f + vU3);
        _effBonusSpeed   = bonusSpeed           * (1f + brU1);
        _effCounterMult  = baseCounterMultiplier * (1f + brU3);

        isStanceActive = true;
        player.isStance = true;
        player.isUsingSpecialSkill = true; // KHÓA: không đánh thường / signature / di chuyển khi thủ thế

        stats.defenseValue += _effDefBonus;
        Debug.Log($"<color=red>JUGGERNAUT: THỦ THẾ!</color> Def +{_effDefBonus}");

        // VFX (tạm): khí đỏ bám theo người.
        debugAura = MageVfxHelper.AttachSphere(player.transform, 1.3f, new Color(1f, 0.1f, 0.1f, 0.28f));
        debugAura.transform.localPosition = Vector3.up * 1f;
        if (stanceAuraVfxPrefab != null) currentAuraInstance = Instantiate(stanceAuraVfxPrefab, player.transform);

        stats.OnDamageReceived += OnHitTrigger;
        stanceCoroutine = StartCoroutine(StanceTimer());
    }

    private IEnumerator StanceTimer()
    {
        yield return new WaitForSeconds(stanceDuration);
        EndStance(false);
        Debug.Log("<color=gray>Juggernaut: Hết thủ thế, không phản đòn.</color>");
    }

    private void OnHitTrigger(float damageTaken, Stats target)
    {
        if (!isStanceActive) return;
        Debug.Log("<color=yellow>JUGGERNAUT: PHẢN ĐÒN!</color>");
        EndStance(true);
    }

    private void EndStance(bool triggerCounter)
    {
        if (!isStanceActive) return;

        isStanceActive = false;
        player.isStance = false;
        stats.defenseValue -= _effDefBonus;
        stats.OnDamageReceived -= OnHitTrigger;

        if (stanceCoroutine != null) { StopCoroutine(stanceCoroutine); stanceCoroutine = null; }
        if (currentAuraInstance != null) Destroy(currentAuraInstance);
        if (debugAura != null) Destroy(debugAura);

        if (triggerCounter)
        {
            StartCoroutine(CounterAttackRoutine()); // giữ khóa input tới hết phản đòn
        }
        else
        {
            player.isUsingSpecialSkill = false; // hết thủ thế không phản đòn → mở khóa ngay
        }
    }

    private IEnumerator CounterAttackRoutine()
    {
        player.isAttacking = true;

        float hpPercent = stats.maxHp > 0 ? stats.currentHp / stats.maxHp : 1f;
        float missingHpRatio = Mathf.Clamp01(1.0f - hpPercent);

        float finalRadius    = baseRadius    + missingHpRatio * maxBonusRadius;
        float finalKnockback = baseKnockback + missingHpRatio * maxBonusKnockback;
        Debug.Log($"Counter: HP {hpPercent * 100:F0}% → Radius {finalRadius:F1}, Knockback {finalKnockback:F1}");

        // VFX (tạm): vùng quét ngang.
        VisualDebugHelper.DrawSphere(transform.position, finalRadius, new Color(1f, 0.3f, 0.1f, 0.3f), 0.3f);

        stats.EnterCombat();
        Collider[] hits = Physics.OverlapSphere(transform.position, finalRadius, player.dangerLayer);
        HashSet<Stats> done = new HashSet<Stats>();
        float totalDamageDealt = 0f;

        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !done.Add(e)) continue;
            totalDamageDealt += ApplyCounterDamage(e, finalKnockback);
        }

        if (totalDamageDealt > 0f)
        {
            float healAmount = Mathf.Min(totalDamageDealt * healRatio, stats.maxHp * healCapPercent);
            stats.Heal(healAmount, true, false, HealSource.Skill);
            Debug.Log($"<color=green>Juggernaut hồi: {healAmount:F0} HP</color>");
        }

        StartCoroutine(SpeedBuffRoutine());

        yield return new WaitForSeconds(0.4f);
        player.isAttacking = false;
        player.isUsingSpecialSkill = false; // mở khóa sau khi phản đòn xong
    }

    // MỘT lần damage duy nhất: base 150% physAtk (qua CombatMath → giáp + crit) GỘP phần Armor/MR (bỏ giáp).
    private float ApplyCounterDamage(Stats e, float knockbackForce)
    {
        float t = CombatMath.CalculateDirectionFactor(transform, e);
        bool crit = CombatMath.CheckIsCrit(stats.critChance);
        var dmg = CombatMath.CalculateFullDamage(stats, e, t, crit, null, null, _effCounterMult); // weapon=null → vật lý
        float bonus = stats.armor * _effArmorScaling + stats.magicResist * _effMSScaling;
        float total = dmg.phys + dmg.magic + bonus;

        float hpBefore = e.currentHp;
        e.TakeDamage(new DamageInfo
        {
            physDamage = total,
            attacker = stats,
            sourcePosition = transform.position,
            isCrit = crit,
            impactLevel = counterImpactLevel,
            isStun = true,
            stunDuration = stunDuration,
            isKnockback = true,
            knockbackForce = knockbackForce
        });
        return Mathf.Max(0f, hpBefore - e.currentHp);
    }

    private IEnumerator SpeedBuffRoutine()
    {
        stats.bonusMoveSpeed += _effBonusSpeed;
        stats.CalculateMoveSpeedOnly();
        yield return new WaitForSeconds(speedBuffDuration);
        stats.bonusMoveSpeed -= _effBonusSpeed;
        stats.CalculateMoveSpeedOnly();
    }
}
