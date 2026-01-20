using UnityEngine;

public class AllyStats : Stats
{
    [Header("--- Sub-Health ---")]
    public float H = 200f;
    public float maxHpGainBonus = 10f;
    public float hpPerVIT = 15f;
    public float hpGain;
    public float bonusHp;
    public float flatHp;

    [Header("--- Sub-Sin ---")]
    public float sinGain;
    public float S = 100f;
    public float maxSinBonus=0.7f;

    [Header("--- Damage ---")]
    public float physicalAtkPerSTR = 2.5f;
    public float magicAtkPerINT = 3f;
    public float maxAttackSpeedBuff = 0.8f;
    public float critPerDEX = 0.015f;
    public float A = 80f;
    public float C = 100f;
    public float K = 0.005f;
    public float bonusPhysicalAtk;
    public float bonusMagicAtk;
    public float attackSpeed;
    public float bonusAttackSpeed;
    public float flatPhysicalAtk=0f;
    public float flatMagicAtk=0f;
    public float critChance;
    public float bonusCritChance;
    public float critMultiplier;
    public float bonusCritMultiplier;


    [Header("--- Movement ---")]
    public float moveSpeed;
    public float moveSpeedReducePerVIT=0.005f;
    public float minSpeed=3f;
    public float maxReduceBySTR=0.6f;
    public float M = 150f;
    public float bonusMoveSpeed;
    public float trueMoveFlexibility;

    [Header("--- Dash ---")]
    public float dashDistance;
    public float dashRecovery;
    public float maxDashReduction=0.35f;
    public float AGI_ThreshHold=75;
    public float R = 80;

    [Header("--- Cooldown ---")]
    public float baseCdr = 0;
    public float cooldownReduction;
    public float cdrPerAGI=0.0015f;
    public float bonusCdr;



    void Start()
    {
        // Gọi tính toán lần đầu
        RecalculateStats();
    }

    // Hàm này phải được gọi mỗi khi: Lên cấp, Đổi đồ, Nhận Buff, Chịu Debuff
    public void RecalculateStats()
    {
        // 1. Tính HP
        // Công thức: hpGain = base * (1 + maxBonus * VIT / (VIT + H))
        hpGain = baseHpGain * (1 + maxHpGainBonus * VIT / (VIT + H));

        // Công thức: MaxHP = (Flat + VIT * 15) * (1 + Bonus%)
        maxHp = (flatHp + baseHp + hpPerVIT) * (1 + bonusHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp); // Đảm bảo máu không vượt quá Max

        // 2. Tính Sin
        sinGain = baseSinGain * (1 + maxSinBonus * INT / (INT + S));

        // 3. Tính Damage
        physicalAtk = (flatPhysicalAtk + STR * physicalAtkPerSTR) * (1 + bonusPhysicalAtk);
        magicAtk = (flatMagicAtk + INT * magicAtkPerINT) * (1 + bonusMagicAtk);

        // 4. Tính Attack Speed (Giới hạn buff tối đa bởi AGI)
        bonusAttackSpeed = maxAttackSpeedBuff * AGI / (AGI + A);
        attackSpeed = baseAttackSpeed * (1 + bonusAttackSpeed); 
        // (Lưu ý: baseAttackSpeed nên lấy từ Weapon đang cầm)

        // 5. Tính Crit
        // baseCritChance lấy từ Stats.cs
        critChance = baseCritChance + critPerDEX * DEX + bonusCritChance;
        critMultiplier = baseCritMultiplier + bonusCritMultiplier;

        // 6. Tính Movement & Flexibility
        // Công thức flexibility: 1 - ((1 - weaponFlex) * (1 - maxReduceBySTR))
        trueMoveFlexibility = 1f - ((1f - moveFlexibility) * (1f - maxReduceBySTR)); //moveFlexibility lấy từ vũ khí

        // Công thức Move Speed (Sửa lỗi cú pháp ^ thành Mathf.Pow)
        // moveSpeed = baseMoveSpeed + (0.2f * Mathf.Pow(AGI, 0.5f) - VIT * 0.005f) ...
        moveSpeed = baseMoveSpeed * (0.2f * Mathf.Pow(AGI, 0.5f) - VIT * moveSpeedReducePerVIT) * ((1 + trueMoveFlexibility) / 2) * bonusMoveSpeed;
        if (moveSpeed < minSpeed) moveSpeed = minSpeed; // Min speed

        // 7. Tính Dash
        dashDistance = baseDashDistance * (1f - trueMoveFlexibility); // Nặng thì lướt ngắn
        dashRecovery = (baseDashRecovery + (1f - trueMoveFlexibility)) * (1f - 0.35f * DEX / (DEX + R));

        // Cost Dash (AGI Threshold)
        if (AGI >= AGI_ThreshHold) dashCost = 15;
        else dashCost = 20;

        // 8. Cooldown Reduction
        cooldownReduction = baseCdr + (cdrPerAGI * AGI) + bonusCdr;
    }

    public override void Update()
    {
        base.Update();
        // Có thể gọi RecalculateStats ở đây để test (nhưng sẽ nặng máy), nên gọi khi cần thiết thôi.
    }
}
