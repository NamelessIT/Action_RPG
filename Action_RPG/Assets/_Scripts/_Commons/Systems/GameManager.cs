// _Scripts/_Commons/System/GameManager.cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Databases")]
    public PlayerDatabase playerDB;
    public WeaponDatabase weaponDB;
    public CheckpointDatabase checkpointDB;
    public AccessoryDatabase accessoryDB;

    private PlayerStateManager stateManager;
    private PlayerRuntimeState currentPlayerState;

    void Awake()
    {
        // Khởi tạo hệ thống quản lý state
        stateManager = new PlayerStateManager(playerDB, weaponDB, checkpointDB, accessoryDB);

        // Tải player ID = 1
        currentPlayerState = stateManager.RebuildRuntimeState(playerId: 1);

        Debug.Log("[GameManager] ✅ Đã tải xong Player Runtime State!");

        // Gán dữ liệu từ runtime state vào CharacterStats của Player
        AssignStatsToPlayer();
    }

    void AssignStatsToPlayer()
    {
        // Tìm GameObject Player trong scene
        GameObject playerObj = GameObject.Find("Player"); // ← tên object trong Hierarchy
        if (playerObj == null)
        {
            Debug.LogError("[GameManager] ❌ Không tìm thấy GameObject 'Player' trong scene!");
            return;
        }

        CharacterStats stats = playerObj.GetComponent<CharacterStats>();
        if (stats == null)
        {
            Debug.LogError("[GameManager] ❌ Player không có script CharacterStats!");
            return;
        }

        // Gán dữ liệu từ PlayerRuntimeState vào CharacterStats
        stats.STR = currentPlayerState.TotalSTR;
        stats.DEX = currentPlayerState.TotalDEX;
        stats.INT = currentPlayerState.TotalINT;
        stats.VIT = currentPlayerState.TotalVIT;
        stats.AGI = currentPlayerState.TotalAGI;

        stats.maxHp = currentPlayerState.MaxHp;
        stats.currentHp = currentPlayerState.currentHp; // ← có thể cần set lại nếu HP bị âm
        stats.baseHp = currentPlayerState.baseHp;

        stats.physicalAtk = currentPlayerState.PhysicalAtk;
        stats.magicAtk = currentPlayerState.MagicAtk;

        stats.moveSpeed = currentPlayerState.MoveSpeed;
        stats.dashDistance = currentPlayerState.DashDistance;
        stats.dashRecovery = currentPlayerState.DashRecovery;
        stats.dashCost = currentPlayerState.dashCost;

        stats.armorBackstabReduce = currentPlayerState.armorBackstabReduce;

        // Nếu CharacterStats có thêm field như armor, magicResist... thì gán tiếp
        // stats.armor = currentPlayerState.Armor;
        // stats.magicResist = currentPlayerState.MagicResist;

        Debug.Log($"[GameManager] ✅ Đã gán stats cho Player:");
        Debug.Log($"  - HP: {stats.currentHp}/{stats.maxHp}");
        Debug.Log($"  - ATK: {stats.physicalAtk:0.0} | Magic: {stats.magicAtk:0.0}");
        Debug.Log($"  - Speed: {stats.moveSpeed:0.0} | Crit: {currentPlayerState.CritChance:P1}");
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveGame();
            Debug.Log("🎮 Đã lưu game (F5)");
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            // Tải lại runtime state
            currentPlayerState = stateManager.RebuildRuntimeState(1);
            AssignStatsToPlayer();
            Debug.Log("🎮 Đã tải lại game (F9)");
        }
    }
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveGame();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveGame();
    }

    void OnDestroy()
    {
        SaveGame();
    }

    private void SaveGame()
    {
        if (currentPlayerState != null)
        {
            stateManager.SaveGameState(currentPlayerState);
            Debug.Log("[GameManager] 💾 Đã lưu game khi thoát.");
        }
    }
}