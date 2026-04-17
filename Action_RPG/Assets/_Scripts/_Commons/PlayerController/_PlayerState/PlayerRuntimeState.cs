//_Scripts/_Commons/PlayerController/PlayerRuntimeState.cs
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class PlayerRuntimeState
{
    // =============== PHẦN LƯU ĐƯỢC (SAVE/LOAD) ===============
    public float currentHp;
    public float currentEnergy;
    public float currentStamina;
    public float currentSin;
    public int checkpointId;
    public Vector2 position;
    public int maxStamina;
    public float baseHp;
    public int dashCost;
    public float armorBackstabReduce;
    public int level = 1;
    public float exp;
    public float nextLevelExp;
    public float maxExpForCurrentLevel;
    public int attributePointRemain;
    public int skillPointRemain;
    public List<PlayerStateSaveData.SavedInventoryItem> inventoryItems = new List<PlayerStateSaveData.SavedInventoryItem>();

    // =============== CƠ SỞ DỮ LIỆU (không thay đổi trong combat) ===============
    public int playerId;
    public string playerName;
    public int currentClassId;
    public int weaponId;
    public int coreShieldId;
    public List<int> equippedAccessoryIds = new List<int>();
    public string weaponAssetId;
    public string coreShieldAssetId;
    public List<string> equippedAccessoryAssetIds = new List<string>();

    // =============== BASE STATS (từ bảng `player`) ===============
    public float baseSTR;
    public float baseDEX;
    public float baseINT;
    public float baseVIT;
    public float baseAGI;

    // =============== WEAPON STATS ===============
    public float weaponBaseAtk;
    public float weaponAttackSpeed;
    public float weaponMoveFlexibility;
    public float weaponDefenseValue;

    // =============== TỔNG HỢP MODIFIERS ===============
    // Flat bonuses (từ accessory, weapon, core_shield)
    private Dictionary<string, float> flatStats = new Dictionary<string, float>();

    // Percent multipliers (từ buff, passive, relic)
    private Dictionary<string, float> percentMultipliers = new Dictionary<string, float>();

    // =============== HẰNG SỐ CÔNG THỨC ===============
    public const float HP_PER_VIT = 20f;
    public const float ATK_PER_STR = 1.5f;
    public const float MAGIC_ATK_PER_INT = 1.2f;
    public const float MOVE_SPEED_PER_AGI = 0.1f;
    public const float MOVE_SPEED_REDUCE_PER_VIT = 0.02f;
    public const float BASE_MOVE_SPEED = 5f;
    public const float BASE_DASH_DISTANCE = 3f;
    public const float BASE_TURN_RATE = 2f;
    public const float BASE_DASH_RECOVERY = 0.15f;
    public const float CRIT_PER_DEX = 0.01f; // 1% crit per 1 DEX
    public const float BASE_CRIT_CHANCE = 0.05f; // 5% base
    public const float COOLDOWN_PER_AGI = 0.001f; // 0.1% per AGI, max 10% → AGI=100
    public const float MAX_COOLDOWN_REDUCTION_RELC = 0.3f; // 30% từ relic
    public const float ATTACK_SPEED_SOFT_CAP_CONSTANT = 100f; // A

    // =============== HELPER: SET MODIFIERS ===============
    public void SetFlatStat(string stat, float value)
    {
        flatStats[stat] = value;
    }

    public void AddFlatStat(string stat, float value)
    {
        if (!flatStats.ContainsKey(stat)) flatStats[stat] = 0;
        flatStats[stat] += value;
    }

    public float GetFlatStat(string stat)
    {
        return flatStats.TryGetValue(stat, out float v) ? v : 0f;
    }

    public void SetPercentMultiplier(string stat, float multiplier) // multiplier = 0.1 → +10%
    {
        percentMultipliers[stat] = multiplier;
    }

    public float GetPercentMultiplier(string stat)
    {
        return percentMultipliers.TryGetValue(stat, out float v) ? v : 0f;
    }

    // =============== DERIVED STATS ===============
    public float TotalVIT => baseVIT + GetFlatStat("VIT");
    public float TotalSTR => baseSTR + GetFlatStat("STR");
    public float TotalDEX => baseDEX + GetFlatStat("DEX");
    public float TotalINT => baseINT + GetFlatStat("INT");
    public float TotalAGI => baseAGI + GetFlatStat("AGI");

    public float MaxHp
    {
        get
        {
            float flatHp = GetFlatStat("max_hp");
            float totalBaseHp = baseHp + flatHp + TotalVIT * HP_PER_VIT;
            float percent = GetPercentMultiplier("max_hp");
            return totalBaseHp * (1f + percent);
        }
    }


    public float PhysicalAtk
    {
        get
        {
            float flatAtk = weaponBaseAtk + GetFlatStat("physical_atk");
            float baseAtk = flatAtk + TotalSTR * ATK_PER_STR;
            float percent = GetPercentMultiplier("physical_atk");
            return baseAtk * (1f + percent);
        }
    }

    public float MagicAtk
    {
        get
        {
            float flat = GetFlatStat("magic_atk");
            float baseAtk = flat + TotalINT * MAGIC_ATK_PER_INT;
            float percent = GetPercentMultiplier("magic_atk");
            return baseAtk * (1f + percent);
        }
    }

    public float Armor => GetFlatStat("armor");
    public float MagicResist => GetFlatStat("magic_resist");

    public float BonusAttackSpeed
    {
        get
        {
            float maxBuff = 0.5f; // max +50% attack speed từ buff
            return maxBuff * TotalAGI / (TotalAGI + ATTACK_SPEED_SOFT_CAP_CONSTANT);
        }
    }

    public float AttackSpeed => weaponAttackSpeed * (1f + BonusAttackSpeed);

    public float MoveSpeed => BASE_MOVE_SPEED
                            + TotalAGI * MOVE_SPEED_PER_AGI
                            - TotalVIT * MOVE_SPEED_REDUCE_PER_VIT
                            - weaponMoveFlexibility;

    public float CritChance => BASE_CRIT_CHANCE + TotalDEX * CRIT_PER_DEX;

    public float CooldownReduction
    {
        get
        {
            float baseReduction = Mathf.Min(COOLDOWN_PER_AGI * TotalAGI, 0.1f); // max 10%
            float relicBonus = GetFlatStat("cooldown_reduction_relic"); // nếu có
            return Mathf.Min(baseReduction + relicBonus, 0.4f); // max 40%
        }
    }

    public float DashDistance => BASE_DASH_DISTANCE * (1f - weaponMoveFlexibility);
    public float TurnRate => BASE_TURN_RATE * (1f - weaponMoveFlexibility);
    public float DashRecovery => BASE_DASH_RECOVERY * (1f + weaponMoveFlexibility);

    // =============== DAMAGE SYSTEM ===============
    public float CalculateFinalDamage(
        float skillMultiplier,
        bool isCrit,
        float targetArmor,
        float targetDefenseValue,
        float attackDirectionT // 0 → 1
    )
    {
        float critMult = isCrit ? 1.5f : 1f;
        float rawDamage = PhysicalAtk * skillMultiplier * critMult;

        float armorDir = targetArmor * (1f - armorBackstabReduce * attackDirectionT);

        float damageReduction = 0.25f * (armorDir / (armorDir + 100f)); // C = 100

        float defenseValueMultiplier = (attackDirectionT > 0.5f)
           ? 1f
           : 1f / (1f + targetDefenseValue * 0.005f);

        float directionBonus = 1f + attackDirectionT * 0.25f;

        float final = rawDamage
                * (1f - damageReduction)
                * defenseValueMultiplier
                * directionBonus;

        return Mathf.Max(final, rawDamage * 0.6f);
    }

    // =============== SAVE/LOAD ===============
    public PlayerStateSaveData GetSaveData()
    {
        return new PlayerStateSaveData
        {
            playerId = playerId,
            currentHp = currentHp,
            currentEnergy = currentEnergy,
            currentStamina = currentStamina,
            currentSin = currentSin,
            checkpointId = checkpointId,
            level = level,
            exp = exp,
            nextLevelExp = nextLevelExp,
            maxExpForCurrentLevel = maxExpForCurrentLevel,
            attributePointRemain = attributePointRemain,
            skillPointRemain = skillPointRemain,
            baseSTR = baseSTR,
            baseDEX = baseDEX,
            baseINT = baseINT,
            baseVIT = baseVIT,
            baseAGI = baseAGI,
            positionX = position.x,
            positionY = position.y,
            currentClassId = currentClassId,
            weaponId = weaponId,
            coreShieldId = coreShieldId,
            accessoryIds = new List<int>(equippedAccessoryIds),
            weaponAssetId = weaponAssetId,
            coreShieldAssetId = coreShieldAssetId,
            accessoryAssetIds = new List<string>(equippedAccessoryAssetIds),
            // FIX: inventoryItems bị thiếu → không được ghi vào JSON → mất sau reload
            inventoryItems = inventoryItems != null
                ? new List<PlayerStateSaveData.SavedInventoryItem>(inventoryItems)
                : new List<PlayerStateSaveData.SavedInventoryItem>()
        };
    }

    public void LoadFromSave(PlayerStateSaveData data)
    {
        playerId = data.playerId;
        currentHp = data.currentHp;
        currentEnergy = data.currentEnergy;
        currentStamina = data.currentStamina > 0f ? data.currentStamina : data.currentEnergy;
        currentSin = data.currentSin;
        checkpointId = data.checkpointId;
        level = data.level > 0 ? data.level : 1;
        exp = data.exp;
        nextLevelExp = data.nextLevelExp;
        maxExpForCurrentLevel = data.maxExpForCurrentLevel;
        attributePointRemain = data.attributePointRemain;
        skillPointRemain = data.skillPointRemain;
        baseSTR = data.baseSTR;
        baseDEX = data.baseDEX;
        baseINT = data.baseINT;
        baseVIT = data.baseVIT;
        baseAGI = data.baseAGI;
        position = new Vector2(data.positionX, data.positionY);
        currentClassId = data.currentClassId;
        weaponId = data.weaponId;
        coreShieldId = data.coreShieldId;
        equippedAccessoryIds = data.accessoryIds != null ? new List<int>(data.accessoryIds) : new List<int>();
        weaponAssetId = data.weaponAssetId;
        coreShieldAssetId = data.coreShieldAssetId;
        equippedAccessoryAssetIds = data.accessoryAssetIds != null
            ? new List<string>(data.accessoryAssetIds)
            : new List<string>();
        // FIX: load inventoryItems từ save
        inventoryItems = data.inventoryItems != null
            ? new List<PlayerStateSaveData.SavedInventoryItem>(data.inventoryItems)
            : new List<PlayerStateSaveData.SavedInventoryItem>();
    }
}