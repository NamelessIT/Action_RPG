// _Scripts/_Commons/PlayerController/_PlayerState/InAppPlayerStateDAO.cs
using System.Collections.Generic;
using UnityEngine;

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
    public WeaponDB LoadWeaponFromDB(int weaponId) => weaponDB.GetWeapon(weaponId);
    public CheckpointDB LoadCheckpoint(int checkpointId) => checkpointDB.GetCheckpoint(checkpointId);
    public List<int> LoadPlayerAccessories(int playerId) => playerDB.GetEquippedAccessories(playerId);
    public List<StatDB> LoadAccessoryStats(int accessoryId) => accessoryDB.GetStats(accessoryId);
    public List<StatDB> LoadWeaponStats(int weaponId) => weaponDB.GetExtraStats(weaponId);

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