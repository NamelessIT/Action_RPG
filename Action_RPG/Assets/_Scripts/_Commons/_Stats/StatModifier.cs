using UnityEngine;

[System.Serializable]
public class StatModifier
{
    // Enum để tránh gõ sai chính tả string (STR, INT...)
    public enum StatType
    {
        STR,
        DEX, 
        INT, 
        VIT, 
        AGI,
        BonusSTR,
        BonusDEX,
        BonusINT,
        BonusVIT,
        BonusAGI,
        FlatHP, 
        BonusHP,
        FlatPhysicalAtk,
        FlatMagicAtk,
        BonusPhysicalAtk,
        BonusMagicAtk,
        CritChance, 
        CritMultiplier,
        Armor,
        MagicResist,
        BonusMoveSpeed,
        BonusCDR,
        DefenseValue,
        PhysicalLifeSteal,
        MagicLifeSteal,
        KnockBackRes,
        EffectRes,

        // ... Thêm các stat khác tùy nhu cầu
    }


    public StatType stat;
    public float val;
    public float powerMod=1.0f;

    // Constructor mặc định
    public StatModifier() 
    { 
        powerMod = 1.0f; 
    }

    // Hàm tiện ích để lấy giá trị thực (val * powerMod)
    public float GetFinalValue()
    {
        return val * powerMod;
    }
}