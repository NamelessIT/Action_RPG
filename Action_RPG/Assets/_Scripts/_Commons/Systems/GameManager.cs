// _Scripts/_Commons/System/GameManager.cs
using UnityEngine;
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
    private GameObject playerObject;
    private PlayerStats playerStats;
    private EquipmentManager equipmentManager;
    private Dictionary<string, WeaponData> weaponLookup;
    private Dictionary<string, CoreShieldData> coreShieldLookup;
    private Dictionary<string, AccessoryData> accessoryLookup;

    void Awake()
    {
        // Khởi tạo hệ thống quản lý state
        stateManager = new PlayerStateManager(playerDB, weaponDB, checkpointDB, accessoryDB);

        // Tải player ID = 1
        currentPlayerState = stateManager.RebuildRuntimeState(playerId: 1);

        Debug.Log("[GameManager] ✅ Đã tải xong Player Runtime State!");
    }

    void Start()
    {
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
        SaveGame();
    }

    private void SaveGame()
    {
        if (currentPlayerState != null)
        {
            SyncRuntimeStateFromScene();
            stateManager.SaveGameState(currentPlayerState);
            Debug.Log("[GameManager] 💾 Đã lưu game khi thoát.");
        }
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