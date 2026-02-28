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
    public float maxBonusRadius = 2.0f; // Máu thấp nhất thì cộng thêm 2m radius
    public float baseKnockback = 5f;
    public float maxBonusKnockback = 5f; // Máu thấp nhất thì đẩy mạnh thêm 10

    [Header("Buffs")]
    public float healCapPercent = 0.3f; // Hồi tối đa 30% HP
    public float bonusSpeed = 0.2f;
    public float speedBuffDuration = 3.0f;

    private bool isStanceActive = false;
    private Coroutine stanceCoroutine;
    private GameObject currentAuraInstance;
    private EquipmentManager equipmentManager; // Để lấy vũ khí tính damage nếu cần

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Dọn dẹp nếu đang dùng mà tháo skill
        if (isStanceActive)
        {
            EndStance(false);
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false; // Check cooldown & mana

        // Bắt đầu thế thủ
        StartStance();
        return true;
    }

    private void StartStance()
    {
        isStanceActive = true;

        // 1. Khóa tấn công (nhưng vẫn cho di chuyển)
        player.isStance = true;

        // 2. Tăng Defense Value
        stats.defenseValue += defenseValueBonus;
        Debug.Log($"<color=red>Juggernaut Stance!</color> Def +{defenseValueBonus}");

        // 3. Hiệu ứng Khí đỏ 

        // 4. Lắng nghe sự kiện bị đánh
        stats.OnDamageReceived += OnHitTrigger;

        // 5. Đếm ngược 3 giây
        stanceCoroutine = StartCoroutine(StanceTimer());
    }

    private IEnumerator StanceTimer()
    {
        yield return new WaitForSeconds(stanceDuration);

        // Hết giờ mà không bị đánh -> Kết thúc buồn tẻ
        EndStance(false);
        Debug.Log("Juggernaut: Hết thời gian, không phản đòn.");
    }

    // Hàm này được gọi TỰ ĐỘNG khi Stats bị trừ máu
    private void OnHitTrigger(float damageTaken, Stats target)
    {
        if (!isStanceActive) return;

        Debug.Log("<color=yellow>Juggernaut: KÍCH HOẠT PHẢN ĐÒN!</color>");

        // Dừng Stance ngay lập tức và chuyển sang Counter
        EndStance(true);
    }

    private void EndStance(bool triggerCounter)
    {
        if (!isStanceActive) return;

        isStanceActive = false;

        // 1. Trả lại trạng thái cũ
        player.isStance = false;
        stats.defenseValue -= defenseValueBonus;
        stats.OnDamageReceived -= OnHitTrigger; // Ngừng lắng nghe

        if (stanceCoroutine != null) StopCoroutine(stanceCoroutine);
        if (currentAuraInstance != null) Destroy(currentAuraInstance);

        // 2. Nếu kích hoạt phản đòn
        if (triggerCounter)
        {
            StartCoroutine(CounterAttackRoutine());
        }
    }

    private IEnumerator CounterAttackRoutine()
    {
        // 1. Khóa di chuyển để thực hiện đòn quét (Animation)
        player.isAttacking = true;

        // animator.SetTrigger("JuggernautSpin");

        // 2. Tính toán tầm quét và lực đẩy dựa trên Máu
        float hpPercent = stats.currentHp / stats.maxHp; // 0.1 (máu thấp) -> 1.0 (full máu)
        float missingHpRatio = 1.0f - hpPercent; // 0.9 (máu thấp) -> 0.0 (full máu)

        float finalRadius = baseRadius + (missingHpRatio * maxBonusRadius);
        float finalKnockback = baseKnockback + (missingHpRatio * maxBonusKnockback);

        Debug.Log($"Counter: HP {hpPercent * 100:F0}% -> Radius: {finalRadius:F1}, Knockback: {finalKnockback:F1}");

        // 3. Tạo VFX Quét

        // 4. Gây sát thương
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

        // 5. Hồi máu (Lifesteal)
        if (totalDamageDealt > 0)
        {
            float healAmount = totalDamageDealt; // Hồi dựa trên damage
            float maxHeal = stats.maxHp * healCapPercent;

            // Kẹp giá trị hồi máu (Min)
            healAmount = Mathf.Min(healAmount, maxHeal);

            stats.currentHp += healAmount;
            if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;

            Debug.Log($"<color=green>Hồi phục: {healAmount} HP</color>");
        }

        // 6. Buff Tốc chạy
        StartCoroutine(SpeedBuffRoutine());

        // Chờ animation xong
        yield return new WaitForSeconds(0.4f);

        player.isAttacking = false;
    }

    private float ApplyCounterDamage(Stats enemyStats, float knockbackForce)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;

        // 1. Tính lượng sát thương cộng thêm từ chỉ số (Scaling)
        // (Armor * 1.5 + MagicResist * 1.5)
        float scalingDmg = (stats.armor * armorScaling) + (stats.magicResist * msScaling);

        // 2. Tính Damage gốc từ Skill (Dựa trên Physical Attack và Skill Multiplier)
        // Ta cần tính trước cái này để làm mẫu số quy đổi
        float basePhyAtk = stats.physicalAtk;
        if (basePhyAtk <= 0) basePhyAtk = 1f; // Tránh chia cho 0 nếu stats lỗi

        // Damage chuẩn của skill theo data (Ví dụ Atk = 100, SkillMult = 1.5 -> Raw = 150)
        float standardSkillDmg = basePhyAtk * data.skillPhysicalMultiplier;

        // 3. Quy đổi Scaling Damage thành Multiplier
        // Ta muốn: Tổng Damage = StandardSkillDmg + ScalingDmg
        // Mà công thức CombatMath là: Atk * (SkillMult * FinalMult)
        // => Ta cần tìm FinalMult sao cho kết quả bằng tổng trên.

        float totalRawDamage = standardSkillDmg + scalingDmg;

        // Hệ số nhân cuối cùng sẽ được đưa vào hàm
        float finalMultiplier = 1.0f;

        if (standardSkillDmg > 0)
        {
            // Tỷ lệ tăng thêm = Tổng / Gốc
            finalMultiplier = totalRawDamage / standardSkillDmg;
        }

        // 4. Tính toán các thông số phụ
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        // Tính hệ số hướng (Phản đòn quét tròn nên t có thể tính hoặc mặc định 1)
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        // 5. GỌI HÀM CHUẨN CỦA HỆ THỐNG
        // Hàm này sẽ tự lấy (Atk * SkillData.Mult) * finalMultiplier
        // Kết quả sẽ tương đương (Standard + Scaling) rồi mới trừ Giáp
        float damage = CombatMath.CalculateFullDamage(
            stats,
            enemyStats,
            t,
            isCrit,
            data,
            currentWpn,
            finalMultiplier // <--- Bí thuật nằm ở đây
        );

        // 6. Tạo DamageInfo & Gửi đi
        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.damageAmount = damage; // Damage cuối cùng (đã trừ giáp)

        info.isStun = true;
        info.stunDuration = stunDuration;
        info.isKnockback = true;
        info.knockbackForce = knockbackForce;
        info.attacker = stats;

        enemyStats.TakeDamage(info);

        // Debug để kiểm tra
        // Debug.Log($"Counter Dmg: Base({standardSkillDmg}) + Scale({scalingDmg}) = Raw({totalRawDamage}) -> Final({damage})");

        return damage; // Trả về damage thực tế gây ra để hồi máu
    }

    private IEnumerator SpeedBuffRoutine()
    {
        // Tăng tốc chạy thủ công
        stats.bonusMoveSpeed += bonusSpeed;
        stats.CalculateMoveSpeedOnly();
        yield return new WaitForSeconds(speedBuffDuration);

        stats.bonusMoveSpeed -= bonusSpeed;
        stats.CalculateMoveSpeedOnly();
    }
}