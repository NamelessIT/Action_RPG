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
    public float armorScaling = 0.5f; // +50% Armor vào sát thương
    public float mrScaling = 0.5f;    // +50% Kháng phép vào sát thương

    [Header("Healing")]
    public float healPerEnemyPercent = 0.01f; // Hồi 2% máu mỗi địch/giây
    public float maxHealPerTick = 0.5f;      // Tối đa hồi 10% mỗi giây (tránh bất tử nếu gom quá nhiều quái)

    [Header("VFX")]
    public GameObject sanctuaryVfxPrefab; // Kéo VFX vòng tròn sáng vào đây

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
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
        // 1. KHỞI TẠO TRẠNG THÁI
        // Khóa di chuyển chủ động (WASD) nhưng vẫn chịu lực đẩy (Knockback)
        // Trong PlayerController của bạn, isAttacking = true sẽ chặn input di chuyển
        player.isAttacking = true;

        // Tăng Defense Value
        stats.defenseValue += defenseValueBonus;
        Debug.Log($"<color=yellow>Sanctuary!</color> Def +{defenseValueBonus}");

        // Tạo VFX (Làm con của Player để di chuyển theo)
        //GameObject currentVfx = null;
        //if (sanctuaryVfxPrefab != null)
        //{
        //    currentVfx = Instantiate(sanctuaryVfxPrefab, transform.position, Quaternion.identity, transform);
        //    // Chỉnh scale theo radius (Cylinder mặc định đk=1 => Scale = radius * 2)
        //    // Nếu VFX của bạn chuẩn unity (1 unit = 1m) thì set scale, nếu là particle thì chỉnh size trong inspector prefab
        //    // Ở đây mình ví dụ scale dẹt:
        //    currentVfx.transform.localScale = new Vector3(radius * 2, 0.1f, radius * 2);
        //}
        //else
        //{
        //    // Debug VFX
        //    currentVfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        //    currentVfx.transform.SetParent(transform, false); // Gắn vào player
        //    currentVfx.transform.localPosition = Vector3.zero;
        //    currentVfx.transform.localScale = new Vector3(radius * 2, 0.1f, radius * 2);
        //    currentVfx.GetComponent<Renderer>().material.color = new Color(1, 1, 0, 0.3f); // Vàng
        //    Destroy(currentVfx.GetComponent<Collider>());
        //}

        // 2. VÒNG LẶP GÂY SÁT THƯƠNG & HỒI MÁU (MỖI 0.5 GIÂY)
        float timer = 0f;
        while (timer < duration)
        {
            // Chờ 1 giây
            yield return new WaitForSeconds(0.5f);
            timer += 0.5f;

            // Xử lý Logic mỗi giây
            TickSanctuaryEffect();
        }

        // 3. KẾT THÚC
        // Trả lại chỉ số
        stats.defenseValue -= defenseValueBonus;
        player.isAttacking = false; // Mở khóa di chuyển

        //if (currentVfx != null) Destroy(currentVfx);
        Debug.Log("Sanctuary Ended.");
    }

    private void TickSanctuaryEffect()
    {
        // Quét kẻ địch trong vòng tròn (Vị trí hiện tại của Player vì Vòng tròn đi theo Player)
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, player.dangerLayer);

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

        // Hồi máu dựa trên số lượng địch
        if (enemyCount > 0)
        {
            ApplyHealing(enemyCount);
        }
    }

    private void DealDamage(Stats enemyStats)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;

        // --- CÔNG THỨC DAMAGE ---
        // 1. Base Magic Damage: (MagicAtk * SkillMultiplier)
        float baseMagicDmg = stats.magicAtk * data.skillMagicMultiplier;

        // 2. Scaling Bonus: (Armor * Scale) + (MagicResist * Scale)
        float scalingBonus = (stats.armor * armorScaling) + (stats.magicResist * mrScaling);

        // Tổng damage gốc
        float totalRawDamage = baseMagicDmg + scalingBonus;

        // Quy đổi ra Multiplier để đưa vào hàm CombatMath (giống JuggernautSkill)
        // CombatMath tính: MagicAtk * (SkillMult * FinalMult)
        // Ta muốn kết quả = totalRawDamage.
        // => FinalMult = totalRawDamage / (MagicAtk * SkillMult)

        float finalMultiplier = 1.0f;
        if (baseMagicDmg > 0)
        {
            finalMultiplier = totalRawDamage / baseMagicDmg;
        }
        else
        {
            // Trường hợp MagicAtk = 0 hoặc SkillMult = 0, ta dùng scaling làm damage chính
            // Set 1 lượng base nhỏ ảo để tính ra damage mong muốn
            finalMultiplier = (totalRawDamage > 0) ? totalRawDamage : 0f;
            // Lưu ý: Nếu MagicAtk = 0 thì CombatMath sẽ ra 0. 
            // Nên nếu build thuần thủ (MagicAtk=0), bạn cần hàm tính damage riêng hoặc đảm bảo MagicAtk > 0.
            // Tuy nhiên logic dưới đây vẫn an toàn cho đa số trường hợp.
        }

        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        // Tính toán qua CombatMath (Tự động trừ kháng phép của địch)
        // Lưu ý: Dùng CalculateFullDamage nhưng ta hiểu đây là Magic Damage chủ đạo
        // Bạn có thể cần chỉnh lại CombatMath nếu muốn skill này thuần Magic mà vũ khí lại là Sword (Phys).
        // Tạm thời giả định hệ thống tự phân loại dựa trên chỉ số cao hơn hoặc mặc định.

        var dmgTuple = CombatMath.CalculateFullDamage(
            stats,
            enemyStats,
            0.5f, //AOE mặc địch là 0.5
            isCrit,
            data,
            currentWpn,
            finalMultiplier
        );

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.physDamage = dmgTuple.phys;
        info.magicDamage = dmgTuple.magic;
        info.trueDamage = dmgTuple.trueDmg;
        info.isKnockback = false; // Vòng tròn đốt máu không nên đẩy lùi, để quái đứng trong đó mà chịu trận
        info.attacker = stats;

        enemyStats.TakeDamage(info);
    }

    private void ApplyHealing(int enemyCount)
    {
        // Tính % hồi phục: Min(Số địch * 2%, 10%)
        float healPercent = Mathf.Min(enemyCount * healPerEnemyPercent, maxHealPerTick);
        float healAmount = stats.maxHp * healPercent;

        if (healAmount > 0)
        {
            stats.currentHp += healAmount;
            if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;

            Debug.Log($"<color=green>Sanctuary Heal: {healAmount} ({enemyCount} targets)");
            // Có thể spawn VFX hồi máu xanh lá ở đây
        }
    }

    // Vẽ Gizmos để xem tầm
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}