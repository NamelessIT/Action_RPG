// _Scripts/_Commons/System/GameManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Databases")]
    public PlayerDatabase playerDB;
    public WeaponDatabase weaponDB;
    public CheckpointDatabase checkpointDB;
    public AccessoryDatabase accessoryDB;

    private PlayerStateManager stateManager;
    private PlayerRuntimeState currentPlayerState;

    /// <summary>
    /// Đặt thành true trước khi reload scene để ngăn OnDestroy ghi đè save đã xóa.
    /// DevToolPanel.CMD_ResetGame() dùng flag này.
    /// </summary>
    [HideInInspector] public bool suppressNextSave = false;
    private GameObject playerObject;
    private PlayerStats playerStats;
    private EquipmentManager equipmentManager;
    private InventoryRuntime inventoryRuntime;
    private Dictionary<string, WeaponData> weaponLookup;
    private Dictionary<string, CoreShieldData> coreShieldLookup;
    private Dictionary<string, AccessoryData> accessoryLookup;

    void Awake()
    {
        // Khởi tạo hệ thống quản lý state
        stateManager = new PlayerStateManager(playerDB, weaponDB, checkpointDB, accessoryDB);

        // Tải player ID = 1
        currentPlayerState = stateManager.RebuildRuntimeState(playerId: 1);
        inventoryRuntime = FindFirstObjectByType<InventoryRuntime>();

        Debug.Log("[GameManager] ✅ Đã tải xong Player Runtime State!");
    }

    void Start()
    {
        // Delay 1 frame để đảm bảo tất cả MonoBehaviour.Start() (PlayerStats, AllyStats, Stats)
        // đã chạy xong trước khi gán giá trị từ save lên.
        StartCoroutine(AssignStatsNextFrame());

        // Subscribe real-time inventory sync:
        // Mỗi khi inventory thay đổi (nhặt/bỏ item), lập tức cập nhật currentPlayerState.inventoryItems.
        // Điều này đảm bảo SaveGame() luôn có dữ liệu mới nhất, kể cả khi
        // inventoryRuntime bị destroy trước GameManager.OnDestroy().
        if (inventoryRuntime != null)
        {
            inventoryRuntime.OnInventoryChanged += SyncInventoryToState;
        }
    }

    private IEnumerator AssignStatsNextFrame()
    {
        yield return null;
        AssignStatsToPlayer();
    }

    void AssignStatsToPlayer()
    {
        if (!ResolvePlayerReferences())
        {
            return;
        }

        if (currentPlayerState == null)
        {
            Debug.LogError("[GameManager] ❌ Không có runtime state để áp vào Player.");
            return;
        }

        playerStats.level = Mathf.Max(1, currentPlayerState.level);
        // characterName đã được set theo characterId trong InitializeClassStats() (AllyStats.Start).
        // Không ghi đè bằng playerName từ DB (DB chỉ có entry cho player 1 = "Chris Don").
        if (string.IsNullOrEmpty(playerStats.characterName))
            playerStats.characterName = currentPlayerState.playerName;
        playerStats.initialBaseHp = currentPlayerState.baseHp > 0f ? currentPlayerState.baseHp : 100f;
        playerStats.baseSTR = currentPlayerState.baseSTR;
        playerStats.baseDEX = currentPlayerState.baseDEX;
        playerStats.baseINT = currentPlayerState.baseINT;
        playerStats.baseVIT = currentPlayerState.baseVIT;
        playerStats.baseAGI = currentPlayerState.baseAGI;
        playerStats.maxStamina = currentPlayerState.maxStamina > 0 ? currentPlayerState.maxStamina : playerStats.maxStamina;
        playerStats.dashCost = currentPlayerState.dashCost > 0 ? currentPlayerState.dashCost : playerStats.dashCost;
        playerStats.armorBackstabReduce = currentPlayerState.armorBackstabReduce;
        playerStats.attributePointRemain = currentPlayerState.attributePointRemain;
        playerStats.skillPointRemain = currentPlayerState.skillPointRemain;

        playerStats.RefreshExpRequirements();
        playerStats.exp = Mathf.Max(0f, currentPlayerState.exp);
        if (currentPlayerState.nextLevelExp > 0f)
        {
            playerStats.nextLevelExp = currentPlayerState.nextLevelExp;
        }

        if (currentPlayerState.maxExpForCurrentLevel > 0f)
        {
            playerStats.maxExpForCurrentLevel = currentPlayerState.maxExpForCurrentLevel;
        }

        playerStats.RecalculateStats();
        RestoreEquipmentState();

        if (inventoryRuntime != null && currentPlayerState.inventoryItems != null
            && currentPlayerState.inventoryItems.Count > 0)
        {
            inventoryRuntime.LoadInventoryFromSave(
                currentPlayerState.inventoryItems,
                weaponDB,
                accessoryDB);
        }

        playerStats.currentHp = Mathf.Clamp(currentPlayerState.currentHp > 0f ? currentPlayerState.currentHp : playerStats.maxHp, 0f, playerStats.maxHp);
        playerStats.currentStamina = Mathf.Clamp(
            currentPlayerState.currentStamina > 0f ? currentPlayerState.currentStamina : currentPlayerState.currentEnergy,
            0f,
            playerStats.maxStamina);
        playerStats.currentSin = Mathf.Clamp(currentPlayerState.currentSin, 0f, playerStats.maxSin);

        Vector3 currentPosition = playerObject.transform.position;
        playerObject.transform.position = new Vector3(currentPlayerState.position.x, currentPlayerState.position.y, currentPosition.z);

        Debug.Log($"[GameManager] ✅ Đã gán stats cho Player:");
        Debug.Log($"  - HP: {playerStats.currentHp}/{playerStats.maxHp}");
        Debug.Log($"  - Level: {playerStats.level} | EXP: {playerStats.exp}/{playerStats.maxExpForCurrentLevel}");
        Debug.Log($"  - STR/DEX/INT/VIT/AGI: {playerStats.baseSTR}/{playerStats.baseDEX}/{playerStats.baseINT}/{playerStats.baseVIT}/{playerStats.baseAGI}");
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
        // Unsubscribe trước khi destroy
        if (inventoryRuntime != null)
        {
            inventoryRuntime.OnInventoryChanged -= SyncInventoryToState;
        }
        SaveGame();
    }

    /// <summary>
    /// Callback: được gọi mỗi khi inventory thay đổi (AddItem/RemoveItem).
    /// Sync ngay vào currentPlayerState để SaveGame() luôn có dữ liệu mới nhất.
    /// </summary>
    private void SyncInventoryToState()
    {
        if (currentPlayerState == null || inventoryRuntime == null) return;
        currentPlayerState.inventoryItems = inventoryRuntime.GetInventorySaveData();
        Debug.Log($"[GameManager] 🔄 Inventory synced → {currentPlayerState.inventoryItems.Count} items saved to state.");
    }

    private void SaveGame()
    {
        if (suppressNextSave)
        {
            Debug.Log("[GameManager] 🔕 SaveGame bị chặn (suppressNextSave = true) — bỏ qua lưu.");
            return;
        }
        if (currentPlayerState == null) return;

        // Khi thoát Play Mode hoặc scene teardown, Player có thể đã bị hủy
        // → chỉ sync nếu Player còn sống, nếu không thì lưu state hiện có
        if (playerObject != null)
        {
            SyncRuntimeStateFromScene();
        }

        stateManager.SaveGameState(currentPlayerState);
        Debug.Log("[GameManager] 💾 Đã lưu game khi thoát.");
    }

    private bool ResolvePlayerReferences()
    {
        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        if (playerObject == null)
        {
            Debug.LogError("[GameManager] ❌ Không tìm thấy GameObject 'Player' trong scene!");
            return false;
        }

        if (playerStats == null)
        {
            playerStats = playerObject.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            Debug.LogError("[GameManager] ❌ Player không có script PlayerStats!");
            return false;
        }

        if (equipmentManager == null)
        {
            equipmentManager = playerObject.GetComponent<EquipmentManager>();
        }

        return true;
    }

    private void SyncRuntimeStateFromScene()
    {
        if (!ResolvePlayerReferences())
        {
            return;
        }

        currentPlayerState.playerId = currentPlayerState.playerId > 0 ? currentPlayerState.playerId : 1;
        currentPlayerState.currentHp = playerStats.currentHp;
        currentPlayerState.currentEnergy = playerStats.currentStamina;
        currentPlayerState.currentStamina = playerStats.currentStamina;
        currentPlayerState.currentSin = playerStats.currentSin;
        currentPlayerState.checkpointId = currentPlayerState.checkpointId <= 0 ? 1 : currentPlayerState.checkpointId;
        currentPlayerState.position = new Vector2(playerObject.transform.position.x, playerObject.transform.position.y);
        currentPlayerState.maxStamina = Mathf.RoundToInt(playerStats.maxStamina);
        currentPlayerState.baseHp = playerStats.baseHp;
        currentPlayerState.dashCost = Mathf.RoundToInt(playerStats.dashCost);
        currentPlayerState.armorBackstabReduce = playerStats.armorBackstabReduce;
        currentPlayerState.level = playerStats.level;
        currentPlayerState.exp = playerStats.exp;
        currentPlayerState.nextLevelExp = playerStats.nextLevelExp;
        currentPlayerState.maxExpForCurrentLevel = playerStats.maxExpForCurrentLevel;
        currentPlayerState.attributePointRemain = playerStats.attributePointRemain;
        currentPlayerState.skillPointRemain = playerStats.skillPointRemain;
        currentPlayerState.baseSTR = playerStats.baseSTR;
        currentPlayerState.baseDEX = playerStats.baseDEX;
        currentPlayerState.baseINT = playerStats.baseINT;
        currentPlayerState.baseVIT = playerStats.baseVIT;
        currentPlayerState.baseAGI = playerStats.baseAGI;

        if (equipmentManager == null)
        {
            return;
        }

        WeaponData equippedWeapon = equipmentManager.GetVisibleEquippedWeapon();
        currentPlayerState.weaponAssetId = equippedWeapon != null ? equippedWeapon.id : string.Empty;
        currentPlayerState.coreShieldAssetId = equipmentManager.currentCoreShield != null ? equipmentManager.currentCoreShield.id : string.Empty;
        currentPlayerState.weaponId = ParseLegacyId(currentPlayerState.weaponAssetId);
        currentPlayerState.coreShieldId = ParseLegacyId(currentPlayerState.coreShieldAssetId);

        currentPlayerState.equippedAccessoryAssetIds = new List<string>();
        currentPlayerState.equippedAccessoryIds = new List<int>();

        AppendAccessoryState(equipmentManager.currentCoreShard);
        AppendAccessoryState(equipmentManager.currentMarkOfSin);
        AppendAccessoryState(equipmentManager.currentRelicOfMemory);
        AppendAccessoryState(equipmentManager.currentParasite);
        AppendAccessoryState(equipmentManager.currentChain);

        if (inventoryRuntime != null)
        {
            currentPlayerState.inventoryItems = inventoryRuntime.GetInventorySaveData();
        }
    }

    private void AppendAccessoryState(AccessoryData accessory)
    {
        if (accessory == null)
        {
            return;
        }

        currentPlayerState.equippedAccessoryAssetIds.Add(accessory.id);

        int parsedId = ParseLegacyId(accessory.id);
        if (parsedId > 0)
        {
            currentPlayerState.equippedAccessoryIds.Add(parsedId);
        }
    }

    private void RestoreEquipmentState()
    {
        if (equipmentManager == null)
        {
            return;
        }

        WeaponData weapon = ResolveWeaponData(currentPlayerState.weaponAssetId, currentPlayerState.weaponId);
        if (weapon != null)
        {
            equipmentManager.EquipWeapon(weapon);
        }
        else
        {
            equipmentManager.ResetToBaseWeapon();
        }

        CoreShieldData shield = ResolveCoreShieldData(currentPlayerState.coreShieldAssetId, currentPlayerState.coreShieldId);
        if (shield != null)
        {
            equipmentManager.EquipCoreShield(shield);
        }
        else
        {
            equipmentManager.UnequipCoreShield();
        }

        ClearAllAccessories();

        for (int i = 0; i < currentPlayerState.equippedAccessoryAssetIds.Count; i++)
        {
            AccessoryData accessory = ResolveAccessoryData(currentPlayerState.equippedAccessoryAssetIds[i]);
            if (accessory != null)
            {
                equipmentManager.EquipAccessory(accessory);
            }
        }
    }

    private void ClearAllAccessories()
    {
        AccessoryData[] equippedAccessories =
        {
            equipmentManager.currentCoreShard,
            equipmentManager.currentMarkOfSin,
            equipmentManager.currentRelicOfMemory,
            equipmentManager.currentParasite,
            equipmentManager.currentChain,
        };

        for (int i = 0; i < equippedAccessories.Length; i++)
        {
            if (equippedAccessories[i] != null)
            {
                equipmentManager.UnequipAccessory(equippedAccessories[i]);
            }
        }
    }

    private WeaponData ResolveWeaponData(string assetId, int legacyId)
    {
        EnsureItemLookups();

        if (!string.IsNullOrEmpty(assetId) && weaponLookup.TryGetValue(assetId, out WeaponData weapon))
        {
            return weapon;
        }

        if (legacyId <= 0)
        {
            return null;
        }

        string legacyKey = legacyId.ToString();
        return weaponLookup.TryGetValue(legacyKey, out weapon) ? weapon : null;
    }

    private CoreShieldData ResolveCoreShieldData(string assetId, int legacyId)
    {
        EnsureItemLookups();

        if (!string.IsNullOrEmpty(assetId) && coreShieldLookup.TryGetValue(assetId, out CoreShieldData shield))
        {
            return shield;
        }

        if (legacyId <= 0)
        {
            return null;
        }

        string legacyKey = legacyId.ToString();
        return coreShieldLookup.TryGetValue(legacyKey, out shield) ? shield : null;
    }

    private AccessoryData ResolveAccessoryData(string assetId)
    {
        EnsureItemLookups();

        if (string.IsNullOrEmpty(assetId))
        {
            return null;
        }

        return accessoryLookup.TryGetValue(assetId, out AccessoryData accessory) ? accessory : null;
    }

    private void EnsureItemLookups()
    {
        if (weaponLookup != null && coreShieldLookup != null && accessoryLookup != null)
        {
            return;
        }

        weaponLookup = new Dictionary<string, WeaponData>();
        coreShieldLookup = new Dictionary<string, CoreShieldData>();
        accessoryLookup = new Dictionary<string, AccessoryData>();

        RegisterWeapons(Resources.LoadAll<WeaponData>("Datas/Weapons"));
        RegisterCoreShields(Resources.LoadAll<CoreShieldData>("Datas/Core Shields"));
        RegisterAccessories(Resources.LoadAll<AccessoryData>("Datas/Accessories"));
    }

    private void RegisterWeapons(WeaponData[] weapons)
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponData weapon = weapons[i];
            if (weapon != null && !string.IsNullOrEmpty(weapon.id) && !weaponLookup.ContainsKey(weapon.id))
            {
                weaponLookup.Add(weapon.id, weapon);
            }
        }
    }

    private void RegisterCoreShields(CoreShieldData[] shields)
    {
        for (int i = 0; i < shields.Length; i++)
        {
            CoreShieldData shield = shields[i];
            if (shield != null && !string.IsNullOrEmpty(shield.id) && !coreShieldLookup.ContainsKey(shield.id))
            {
                coreShieldLookup.Add(shield.id, shield);
            }
        }
    }

    private void RegisterAccessories(AccessoryData[] accessories)
    {
        for (int i = 0; i < accessories.Length; i++)
        {
            AccessoryData accessory = accessories[i];
            if (accessory != null && !string.IsNullOrEmpty(accessory.id) && !accessoryLookup.ContainsKey(accessory.id))
            {
                accessoryLookup.Add(accessory.id, accessory);
            }
        }
    }

    private int ParseLegacyId(string rawId)
    {
        if (string.IsNullOrEmpty(rawId))
        {
            return 0;
        }

        return int.TryParse(rawId, out int parsedId) ? parsedId : 0;
    }
}