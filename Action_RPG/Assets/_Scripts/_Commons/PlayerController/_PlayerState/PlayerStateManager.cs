// _Scripts/_Commons/PlayerController/_PlayerState/PlayerStateManager.cs
using UnityEngine;
using System.Collections.Generic;
public class PlayerStateManager
{
    private InAppPlayerStateDAO dao;

    public PlayerStateManager(
        PlayerDatabase playerDB,
        WeaponDatabase weaponDB,
        CheckpointDatabase checkpointDB,
        AccessoryDatabase accessoryDB)
    {
        dao = new InAppPlayerStateDAO(playerDB, weaponDB, checkpointDB, accessoryDB);
    }

    public PlayerRuntimeState RebuildRuntimeState(int playerId)
    {
        Debug.Log($"[PlayerStateManager] Đang tải player ID: {playerId}");
        var runtime = new PlayerRuntimeState();

        // 1. Load base player
        var player = dao.LoadPlayerFromDB(playerId);
        if (player == null)
        {
            Debug.LogError($"[PlayerStateManager] ❌ Không tìm thấy player ID {playerId} trong PlayerDatabase!");
            throw new System.Exception($"Player {playerId} not found!");
        }
        Debug.Log($"[PlayerStateManager] ✅ Tải base player: {player.name} | STR={player.STR}, VIT={player.VIT}");

        // Gán base stats...
        runtime.playerName = player.name;
        runtime.baseSTR = player.STR;
        runtime.baseDEX = player.DEX;
        runtime.baseINT = player.INT;
        runtime.baseVIT = player.VIT;
        runtime.baseAGI = player.AGI;
        runtime.baseHp = player.base_hp;
        runtime.maxStamina = player.max_stamina;
        runtime.dashCost = player.dash_cost;
        runtime.armorBackstabReduce = player.armor_backstab_reduce;
        runtime.playerId = playerId;
        runtime.level = 1;

        // 2. Load saved state
        var saveData = dao.LoadSaveData(playerId);
        bool hasSaveData = saveData != null && saveData.currentHp > 0f;
        if (hasSaveData)
        {
            // Lưu base stats từ DB trước khi LoadFromSave ghi đè
            float dbSTR = runtime.baseSTR;
            float dbDEX = runtime.baseDEX;
            float dbINT = runtime.baseINT;
            float dbVIT = runtime.baseVIT;
            float dbAGI = runtime.baseAGI;

            runtime.LoadFromSave(saveData);

            // Nếu save file chưa có base stats (legacy save) → dùng lại giá trị DB
            runtime.baseSTR = saveData.baseSTR > 0f ? saveData.baseSTR : dbSTR;
            runtime.baseDEX = saveData.baseDEX > 0f ? saveData.baseDEX : dbDEX;
            runtime.baseINT = saveData.baseINT > 0f ? saveData.baseINT : dbINT;
            runtime.baseVIT = saveData.baseVIT > 0f ? saveData.baseVIT : dbVIT;
            runtime.baseAGI = saveData.baseAGI > 0f ? saveData.baseAGI : dbAGI;

            if (saveData.level <= 0)
            {
                runtime.level = 1;
                runtime.attributePointRemain = runtime.level * 5;
                runtime.skillPointRemain = runtime.level;
            }

            if (saveData.nextLevelExp <= 0f || saveData.maxExpForCurrentLevel <= 0f)
            {
                float requiredExp = Mathf.Floor(100f * Mathf.Pow(runtime.level, 1.1f));
                runtime.nextLevelExp = requiredExp;
                runtime.maxExpForCurrentLevel = requiredExp;
            }

            Debug.Log($"[PlayerStateManager] ✅ Tải save game: HP={saveData.currentHp}, Energy={saveData.currentEnergy}, Checkpoint={saveData.checkpointId}");
        }
        else
        {
            Debug.Log($"[PlayerStateManager] ⚠ Không có save game. Dùng trạng thái mặc định.");
            var defaultCheckpoint = dao.LoadCheckpoint(1);
            if (defaultCheckpoint != null)
            {
                runtime.position = new Vector2(defaultCheckpoint.positionX, defaultCheckpoint.positionY);
                runtime.checkpointId = 1;
                Debug.Log($"[PlayerStateManager] ✅ Dùng checkpoint mặc định: ({defaultCheckpoint.positionX}, {defaultCheckpoint.positionY})");
            }
            runtime.currentEnergy = 100;
            runtime.currentStamina = runtime.maxStamina;
            runtime.attributePointRemain = runtime.level * 5;
            runtime.skillPointRemain = runtime.level;
        }

        // 3. Load weapon
        string effectiveWeaponDbId = null;
        if (runtime.weaponId > 0)
        {
            effectiveWeaponDbId = runtime.weaponId.ToString();
        }
        else if (player.weapon_holding.HasValue)
        {
            effectiveWeaponDbId = player.weapon_holding.Value.ToString();
        }

        if (!string.IsNullOrEmpty(effectiveWeaponDbId))
        {
            var weapon = dao.LoadWeaponFromDB(effectiveWeaponDbId); 

            if (weapon != null)
            {
                runtime.weaponBaseAtk = weapon.baseAtk; 
                runtime.weaponAttackSpeed = weapon.baseAttackSpeed; 
                runtime.weaponMoveFlexibility = weapon.moveFlexibility;
                runtime.weaponDefenseValue = weapon.baseDefenseValue; 

                if (int.TryParse(weapon.id, out int parsedId))
                {
                    runtime.weaponId = parsedId;
                }
                else
                {
                    Debug.LogError($"[PlayerStateManager] ❌ Weapon ID không hợp lệ: '{weapon.id}'. Dùng ID=0 làm mặc định.");
                    runtime.weaponId = 0; 
                }
                if (weapon.bonusCritChance > 0)
                    runtime.AddFlatStat("crit_chance", weapon.bonusCritChance);


                var weaponStats = dao.LoadWeaponStats(weapon.id); 
                foreach (var stat in weaponStats)
                    runtime.AddFlatStat(stat.statName, stat.value);

                Debug.Log($"[PlayerStateManager] ✅ Tải vũ khí ID {weapon.id}: ATK={weapon.baseAtk}, Speed={weapon.baseAttackSpeed}");
                if (weaponStats.Count > 0)
                    Debug.Log($"[PlayerStateManager]   └─ Stat phụ: {string.Join(", ", weaponStats.ConvertAll(s => $"{s.statName}={s.value}"))}");
            }
            else
            {
                Debug.LogWarning($"[PlayerStateManager] ⚠ Vũ khí được chỉ định (ID={effectiveWeaponDbId}) không tồn tại!");
            }
        }
        else
        {
            Debug.Log("[PlayerStateManager] ℹ Player không cầm vũ khí.");
        }

        // 4. Load accessories
        var equippedAccessories = runtime.equippedAccessoryIds.Count > 0
            ? new List<int>(runtime.equippedAccessoryIds)
            : dao.LoadPlayerAccessories(playerId);

        runtime.equippedAccessoryIds = new List<int>(equippedAccessories);
        foreach (var accId in equippedAccessories)
        {
            var stats = dao.LoadAccessoryStats(accId);
            foreach (var stat in stats)
                runtime.AddFlatStat(stat.statName, stat.value);

            if (stats.Count > 0)
                Debug.Log($"[PlayerStateManager] ✅ Tải phụ kiện ID {accId}: {string.Join(", ", stats.ConvertAll(s => $"{s.statName}={s.value}"))}");
            else
                Debug.Log($"[PlayerStateManager] ✅ Tải phụ kiện ID {accId} (không có stat)");
        }

        if (equippedAccessories.Count == 0)
            Debug.Log("[PlayerStateManager] ℹ Không có phụ kiện nào được trang bị.");

        // 5. Finalize HP
        if (runtime.currentHp <= 0)
        {
            runtime.currentHp = runtime.MaxHp;
            Debug.Log($"[PlayerStateManager] 💖 Đặt HP đầy: {runtime.MaxHp:0.0}");
        }

        if (runtime.currentStamina <= 0f)
        {
            runtime.currentStamina = runtime.maxStamina;
        }

        // Log tổng kết
        Debug.Log($"[PlayerStateManager] 🎮 Player runtime state hoàn tất!");
        Debug.Log($"  - HP: {runtime.currentHp:0.0} / {runtime.MaxHp:0.0}");
        Debug.Log($"  - ATK: {runtime.PhysicalAtk:0.0} | Magic: {runtime.MagicAtk:0.0}");
        Debug.Log($"  - Move Speed: {runtime.MoveSpeed:0.0} | Crit: {runtime.CritChance:P1}");
        Debug.Log($"  - Weapon Flex: {runtime.weaponMoveFlexibility:0.2}");

        return runtime;
    }

    public void SaveGameState(PlayerRuntimeState runtime)
    {
        dao.SaveGameState(runtime.playerId, runtime.GetSaveData());
    }
}