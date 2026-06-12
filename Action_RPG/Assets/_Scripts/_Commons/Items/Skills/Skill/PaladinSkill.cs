using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PaladinSkill : SkillBehavior
{
    [Header("Sanctuary Settings")]
    public float duration = 5.0f;     // Tồn tại 5 giây
    public float radius = 2f;       // Bán kính vòng tròn
    public float defenseValueBonus = 500f; // Tăng 500 DefenseValue

    [Header("Damage Scaling")]
    // Sát thương phép cơ bản sẽ nhân với data.SkillMagicMultiplier
    // Cộng thêm % Armor và % MagicResist
    public float armorScaling = 0.2f; // +20% Armor vào sát thương
    public float mrScaling = 0.2f;    // +20% Kháng phép vào sát thương

    [Header("Healing")]
    public float healPerEnemyPercent = 0.01f; // Hồi 1% máu mỗi địch mỗi tick
    public float maxHealPerTick = 0.05f;      // Tối đa hồi 5% mỗi tick

    [Header("VFX")]
    public GameObject sanctuaryVfxPrefab;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;
    private AllyStats allyStats;

    // Cached effective values dùng trong cả TickSanctuaryEffect
    private float _effectiveArmorScaling;
    private float _effectiveMRScaling;
    private float _effectiveRadius;
    private float _effectiveHealPerEnemy;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        allyStats = myStats;
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(SanctuaryRoutine());
        return true;
    }

    private IEnumerator SanctuaryRoutine()
    {
        // Vanguard U1: +20% Defense Value buff
        // Vanguard U3: +20% Armor/MR scale damage
        // BattleMage U1: +20% range (radius)
        // BattleMage U3: +20% heal
        float vU1 = allyStats != null ? allyStats.vanguardSkillU1  : 0f;
        float vU3 = allyStats != null ? allyStats.vanguardSkillU3  : 0f;
        float bmU1 = allyStats != null ? allyStats.battleMageSkillU1 : 0f;
        float bmU3 = allyStats != null ? allyStats.battleMageSkillU3 : 0f;

        float effectiveDefBonus   = defenseValueBonus   * (1f + vU1);
        _effectiveArmorScaling    = armorScaling         * (1f + vU3);
        _effectiveMRScaling       = mrScaling            * (1f + vU3);
        _effectiveRadius          = radius               * (1f + bmU1);
        _effectiveHealPerEnemy    = healPerEnemyPercent  * (1f + bmU3);

        // 1. KHỞI TẠO TRẠNG THÁI
        player.isAttacking = true;

        stats.defenseValue += effectiveDefBonus;
        Debug.Log($"<color=yellow>Sanctuary!</color> Def +{effectiveDefBonus}");

        // 2. VÒNG LẶP GÂY SÁT THƯƠNG & HỒI MÁU (MỖI 0.5 GIÂY)
        float timer = 0f;
        while (timer < duration)
        {
            yield return new WaitForSeconds(0.5f);
            timer += 0.5f;
            TickSanctuaryEffect();
        }

        // 3. KẾT THÚC
        stats.defenseValue -= effectiveDefBonus;
        player.isAttacking = false;
        Debug.Log("Sanctuary Ended.");
    }

    private void TickSanctuaryEffect()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _effectiveRadius, player.dangerLayer);

        int enemyCount = 0;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                enemyCount++;
                DealDamage(enemyStats);
            }
        }

        if (enemyCount > 0)
        {
            ApplyHealing(enemyCount);
        }
    }

    private void DealDamage(Stats enemyStats)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;

        // Base Magic Damage: MagicAtk * SkillMult
        float baseMagicDmg = stats.magicAtk * data.skillMagicMultiplier;

        // Scaling Bonus: Armor/MR * scaling (Vanguard U3 applied)
        float scalingBonus = (stats.armor * _effectiveArmorScaling) + (stats.magicResist * _effectiveMRScaling);

        float totalRawDamage = baseMagicDmg + scalingBonus;

        float finalMultiplier = 1.0f;
        if (baseMagicDmg > 0)
            finalMultiplier = totalRawDamage / baseMagicDmg;
        else
            finalMultiplier = (totalRawDamage > 0) ? totalRawDamage : 0f;

        DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, finalMultiplier, data, currentWpn, 0);
    }

    private void ApplyHealing(int enemyCount)
    {
        // BattleMage U3 scales healPerEnemyPercent
        float healPercent = Mathf.Min(enemyCount * _effectiveHealPerEnemy, maxHealPerTick);
        float healAmount = stats.maxHp * healPercent;

        if (healAmount > 0)
        {
            stats.currentHp += healAmount;
            if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;

            Debug.Log($"<color=green>Sanctuary Heal: {healAmount} ({enemyCount} targets)");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
