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
    public float flatPhysicalAtk;
    public float flatMagicAtk;
    public float critChance;
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



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //HP
        hpGain = base.baseHpGain * (1 + maxHpGainBonus * base.VIT / (base.VIT + H));
        base.maxHp = (flatHp + base.VIT * hpPerVIT) *(1 + bonusHp);

        //Sin
        sinGain = base.baseSinGain * (1 + maxSinBonus * base.INT / (base.INT + S));
        //maxSin

        base.physicalAtk = (flatPhysicalAtk + base.STR * physicalAtkPerSTR) * (1 + bonusPhysicalAtk);
        base.magicAtk = (flatMagicAtk + base.INT * magicAtkPerINT) * (1 + bonusMagicAtk);
        bonusAttackSpeed = maxAttackSpeedBuff * base.AGI / (base.AGI * A);
        //attackSpeed = baseAttackSpeed * (1 + bonusAttackSpeed);
        //trueMoveFlexibility = 1 - ((1 - moveFlexibility) * (1 - maxReduceBySTR * STR / (STR + M)));
        //moveSpeed = baseMoveSpeed + (0,2 * AGI^0,5 - VIT * moveSpeedReducePerVIT) * ((1 + moveFlexibility) / 2) * bonusMoveSpeed;
        
        critChance = base.baseCritChance + base.DEX * critPerDEX;
        critMultiplier = base.baseCritMultiplier + bonusCritMultiplier;

        cooldownReduction = baseCdr + cdrPerAGI * base.AGI + bonusCdr;
        dashDistance = base.baseDashDistance * (1 - trueMoveFlexibility);
        //turnDuration = 0,6 - (0, 5 * trueMoveFlexibility);
        dashRecovery = (base.baseDashRecovery + (1 - trueMoveFlexibility)) * (1 - maxDashReduction * base.DEX / (base.DEX + R));
        if (base.AGI >= AGI_ThreshHold)
        {
            base.dashCost = 15;
        }
        else
        {
            base.dashCost = 20;
        }
            

    }

    // Update is called once per frame
    public override void Update()
    {
        base.Update();
    }

}
