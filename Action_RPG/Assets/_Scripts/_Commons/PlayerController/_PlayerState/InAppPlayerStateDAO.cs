// _Scripts/_Commons/PlayerController/_PlayerState/InAppPlayerStateDAO.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [DAO Layer - Data Access Object]
/// 
/// SINGLETON RESPONSIBILITY: Single point of access to all database and save/load operations.
/// This class bridges between read-only Databases (Tier 1) and PlayerStateManager (Tier 3).
/// 
/// ARCHITECTURE LAYERS:
/// └─ Tier 1: Database access (PlayerDatabase, WeaponDatabase, etc.)
///    └─ Tier 2: DAO wrapper (InAppPlayerStateDAO) ← YOU ARE HERE
///       └─ Tier 3: State Manager (PlayerStateManager)
///          └─ Tier 4: Game System (GameManager)
/// 
/// METHODS:
/// - LoadPlayerFromDB(id) → PlayerDB (base stats)
/// - LoadWeaponFromDB(id) → WeaponDB (equipped weapon)
/// - LoadCheckpoint(id) → CheckpointDB (spawn point)
/// - LoadPlayerAccessories(id) → List<int> (equipped accessory IDs)
/// - LoadAccessoryStats(id) → List<StatDB> (stat mods from accessory)
/// - LoadWeaponStats(id) → List<StatDB> (stat mods from weapon)
/// - SaveGameState() → JSON file (persistence)
/// - LoadSaveData() → Memory/File (load runtime data)
/// 
/// CONSTRAINT: Direct database access ONLY occurs here. No other class may call Database.GetXXX()
/// </summary>
public class InAppPlayerStateDAO
{
    private PlayerDatabase playerDB;
    private WeaponDatabase weaponDB;
    private CheckpointDatabase checkpointDB;
    private AccessoryDatabase accessoryDB;

    // Save data sẽ được lưu ở đây (hoặc ra file)
    private static Dictionary<int, PlayerStateSaveData> savedStates = new();

    public InAppPlayerStateDAO(
        PlayerDatabase playerDB,
        WeaponDatabase weaponDB,
        CheckpointDatabase checkpointDB,
        AccessoryDatabase accessoryDB)
    {
        this.playerDB = playerDB;
        this.weaponDB = weaponDB;
        this.checkpointDB = checkpointDB;
        this.accessoryDB = accessoryDB;
    }

    // === Base Data (không thay đổi) ===
    public PlayerDB LoadPlayerFromDB(int playerId) => playerDB.GetPlayer(playerId);
    public WeaponDB LoadWeaponFromDB(string weaponId) => weaponDB.GetWeapon(weaponId);
    public CheckpointDB LoadCheckpoint(int checkpointId) => checkpointDB.GetCheckpoint(checkpointId);
    public List<int> LoadPlayerAccessories(int playerId) => playerDB.GetEquippedAccessories(playerId);
    public List<StatDB> LoadAccessoryStats(int accessoryId) => accessoryDB.GetStats(accessoryId);
    public List<StatDB> LoadWeaponStats(string weaponId) => weaponDB.GetExtraStats(weaponId);

    // === Save/Load State (dùng file JSON) ===
    private string GetSavePath(int playerId)
    {
        return System.IO.Path.Combine(Application.persistentDataPath, $"save_player_{playerId}.json");
    }

    // === Save/Load State (runtime data) ===
    public void SaveGameState(int playerId, PlayerStateSaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        string path = GetSavePath(playerId);
        savedStates[playerId] = data;
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"[InAppPlayerStateDAO] 💾 Đã lưu save game player {playerId} vào: {path}");
    }


    public PlayerStateSaveData LoadSaveData(int playerId)
    {
        // Thử load từ memory trước
        if (savedStates.TryGetValue(playerId, out var memData))
            return memData;

        // Thử load từ file
        string path = Application.persistentDataPath + $"/save_player_{playerId}.json";
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            var fileData = JsonUtility.FromJson<PlayerStateSaveData>(json);
            savedStates[playerId] = fileData;
            return fileData;
        }

        // Trả về mặc định
        return new PlayerStateSaveData
        {
            currentHp = -1,
            currentEnergy = 100,
            checkpointId = 1
        };
    }
}