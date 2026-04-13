#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Dev Tool Panel — chỉ khả dụng trong Editor và Development Build.
/// Toggle bằng phím F1. Gắn vào một Canvas GameObject (luôn-on-screen).
/// Mỗi public CMD_* method được gán vào Button.onClick qua Inspector.
/// </summary>
public class DevToolPanel : MonoBehaviour
{
    // ============================================================
    //  PANEL TABS
    // ============================================================
    [Header("Panel Tabs — Root GameObjects")]
    [SerializeField] private GameObject _panelCombat;
    [SerializeField] private GameObject _panelSkills;
    [SerializeField] private GameObject _panelEquipment;
    [SerializeField] private GameObject _panelPlayer;

    // ============================================================
    //  SKILLS TAB
    // ============================================================
    [Header("Skills Tab")]
    [SerializeField] private TMP_Dropdown _dropdownSkill1;
    [SerializeField] private TMP_Dropdown _dropdownSkill2;

    // ============================================================
    //  EQUIPMENT TAB
    // ============================================================
    [Header("Equipment Tab")]
    [SerializeField] private TMP_Dropdown _dropdownWeapon;
    [SerializeField] private TMP_Dropdown _dropdownShield;
    [SerializeField] private TMP_Dropdown _dropdownAccessory;

    // ============================================================
    //  RUNTIME REFS (auto-found)
    // ============================================================
    private PlayerStats _playerStats;
    private EquipmentManager _equipmentManager;
    private SkillManager _skillManager;

    // ============================================================
    //  DATA LISTS (loaded from Resources)
    // ============================================================
    private List<WeaponData> _weapons = new List<WeaponData>();
    private List<CoreShieldData> _shields = new List<CoreShieldData>();
    private List<AccessoryData> _accessories = new List<AccessoryData>();
    private List<SkillData> _skills = new List<SkillData>();

    // ============================================================
    //  STATE
    // ============================================================
    private bool _isVisible = false;
    private int _activeTab = 0; // 0=Combat, 1=Skills, 2=Equipment, 3=Player
    private bool _devGodMode = false;

    // ============================================================
    //  UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        _playerStats = FindFirstObjectByType<PlayerStats>();
        _equipmentManager = FindFirstObjectByType<EquipmentManager>();
        _skillManager = FindFirstObjectByType<SkillManager>();

        if (_playerStats == null) Debug.LogWarning("[DevTool] PlayerStats not found in scene.");
        if (_equipmentManager == null) Debug.LogWarning("[DevTool] EquipmentManager not found in scene.");
        if (_skillManager == null) Debug.LogWarning("[DevTool] SkillManager not found in scene.");
    }

    private void Start()
    {
        // Load data assets from Resources
        _weapons = new List<WeaponData>(Resources.LoadAll<WeaponData>("Datas/Weapons"));
        _shields = new List<CoreShieldData>(Resources.LoadAll<CoreShieldData>("Datas/Core Shields"));
        _accessories = new List<AccessoryData>(Resources.LoadAll<AccessoryData>("Datas/Accessories"));
        _skills = new List<SkillData>(Resources.LoadAll<SkillData>("Datas/Skills"));

        Debug.Log($"[DevTool] Loaded — Weapons:{_weapons.Count} Shields:{_shields.Count} Accessories:{_accessories.Count} Skills:{_skills.Count}");

        PopulateDropdowns();
        ShowTab(0);
    }

    private void Update()
    {
        // Toggle moved to PlayerController (V key)
    }

    // ============================================================
    //  PANEL CONTROL
    // ============================================================

    public void TogglePanel()
    {
        _isVisible = !_isVisible;
        gameObject.SetActive(_isVisible);
    }

    public void ShowTab(int index)
    {
        if (_panelCombat != null) _panelCombat.SetActive(index == 0);
        if (_panelSkills != null) _panelSkills.SetActive(index == 1);
        if (_panelEquipment != null) _panelEquipment.SetActive(index == 2);
        if (_panelPlayer != null) _panelPlayer.SetActive(index == 3);
        _activeTab = index;
    }

    private void PopulateDropdowns()
    {
        // Weapon dropdown
        if (_dropdownWeapon != null)
        {
            _dropdownWeapon.ClearOptions();
            var options = new List<string>();
            foreach (var w in _weapons)
                options.Add(string.IsNullOrEmpty(w.weaponName) ? w.name : w.weaponName);
            _dropdownWeapon.AddOptions(options);
        }

        // Shield dropdown
        if (_dropdownShield != null)
        {
            _dropdownShield.ClearOptions();
            var options = new List<string>();
            foreach (var s in _shields)
                options.Add(string.IsNullOrEmpty(s.coreShieldName) ? s.name : s.coreShieldName);
            _dropdownShield.AddOptions(options);
        }

        // Accessory dropdown
        if (_dropdownAccessory != null)
        {
            _dropdownAccessory.ClearOptions();
            var options = new List<string>();
            foreach (var a in _accessories)
                options.Add(string.IsNullOrEmpty(a.AccessoryName) ? a.name : a.AccessoryName);
            _dropdownAccessory.AddOptions(options);
        }

        // Skill dropdowns (both slots share the same list)
        PopulateSkillDropdown(_dropdownSkill1);
        PopulateSkillDropdown(_dropdownSkill2);
    }

    private void PopulateSkillDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        var options = new List<string>();
        foreach (var sk in _skills)
            options.Add(string.IsNullOrEmpty(sk.skillName) ? sk.name : sk.skillName);
        dropdown.AddOptions(options);
    }

    // ============================================================
    //  TAB 0 — COMBAT
    // ============================================================

    /// <summary>Deal 10 raw damage to the player (bypasses armor, hits TakeDamage overload).</summary>
    public void CMD_TakeDamage10()
    {
        if (!AssertStats()) return;
        _playerStats.TakeDamage(10f);
        Debug.Log("[DevTool] TakeDamage 10");
    }

    /// <summary>Deal 50 raw damage to the player.</summary>
    public void CMD_TakeDamage50()
    {
        if (!AssertStats()) return;
        _playerStats.TakeDamage(50f);
        Debug.Log("[DevTool] TakeDamage 50");
    }

    /// <summary>Instantly restore player HP to maximum.</summary>
    public void CMD_HealFull()
    {
        if (!AssertStats()) return;
        _playerStats.currentHp = _playerStats.maxHp;
        Debug.Log($"[DevTool] HealFull → HP: {_playerStats.currentHp}/{_playerStats.maxHp}");
    }

    /// <summary>Add 100 EXP to the player (triggers LevelUp if threshold reached).</summary>
    public void CMD_AddExp100()
    {
        if (!AssertStats()) return;
        _playerStats.AddExp(100f);
        Debug.Log($"[DevTool] AddExp 100 → EXP: {_playerStats.exp}/{_playerStats.nextLevelExp}");
    }

    /// <summary>Force an immediate level-up by giving exactly enough EXP to reach the next level.</summary>
    public void CMD_ForceLevelUp()
    {
        if (!AssertStats()) return;
        if (_playerStats.level >= _playerStats.maxLevel)
        {
            Debug.LogWarning("[DevTool] ForceLevelUp — player is already at max level.");
            return;
        }
        float expNeeded = _playerStats.nextLevelExp - _playerStats.exp;
        // Ensure at least 1 exp so AddExp triggers LevelUp
        _playerStats.AddExp(Mathf.Max(expNeeded, 1f));
        Debug.Log($"[DevTool] ForceLevelUp → Level: {_playerStats.level}");
    }

    /// <summary>
    /// Toggle God Mode — sets Stats.isInvincible.
    /// When active, the character cannot be killed by normal damage flow.
    /// </summary>
    public void CMD_ToggleGodMode()
    {
        if (!AssertStats()) return;
        _devGodMode = !_devGodMode;
        _playerStats.isInvincible = _devGodMode;
        Debug.Log($"[DevTool] God Mode: {(_devGodMode ? "<color=green>ON</color>" : "<color=red>OFF</color>")}");
    }

    // ============================================================
    //  TAB 1 — SKILLS
    // ============================================================

    /// <summary>Equip the skill selected in Dropdown 1 (routes by skillType).</summary>
    public void CMD_ApplySkill1()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_skills, _dropdownSkill1, "Skills")) return;
        _skillManager.EquipSkill(_skills[_dropdownSkill1.value]);
        Debug.Log($"[DevTool] EquipSkill(Slot1): {_skills[_dropdownSkill1.value].skillName}");
    }

    /// <summary>Equip the skill selected in Dropdown 2 as the Signature slot (routes by skillType).</summary>
    public void CMD_ApplySkill2()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_skills, _dropdownSkill2, "Skills")) return;
        _skillManager.EquipSkill(_skills[_dropdownSkill2.value]);
        Debug.Log($"[DevTool] EquipSkill(Slot2): {_skills[_dropdownSkill2.value].skillName}");
    }

    /// <summary>Cast the currently equipped Skill (currentSkill).</summary>
    public void CMD_CastSkill1()
    {
        if (!AssertSkillManager()) return;
        if (_skillManager.currentSkill == null)
        {
            Debug.LogWarning("[DevTool] CastSkill1 — no currentSkill assigned.");
            return;
        }
        _skillManager.CastSkill(_skillManager.currentSkill);
        Debug.Log($"[DevTool] CastSkill: {_skillManager.currentSkill.skillName}");
    }

    /// <summary>Cast the currently equipped Signature (currentSignature).</summary>
    public void CMD_CastSkill2()
    {
        if (!AssertSkillManager()) return;
        if (_skillManager.currentSignature == null)
        {
            Debug.LogWarning("[DevTool] CastSkill2 — no currentSignature assigned.");
            return;
        }
        _skillManager.CastSkill(_skillManager.currentSignature);
        Debug.Log($"[DevTool] CastSignature: {_skillManager.currentSignature.skillName}");
    }

    // ============================================================
    //  TAB 2 — EQUIPMENT
    // ============================================================

    /// <summary>Equip the weapon selected in the dropdown.</summary>
    public void CMD_EquipWeapon()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_weapons, _dropdownWeapon, "Weapons")) return;
        _equipmentManager.EquipWeapon(_weapons[_dropdownWeapon.value]);
        Debug.Log($"[DevTool] EquipWeapon: {_weapons[_dropdownWeapon.value].weaponName}");
    }

    /// <summary>Reset weapon to base (hand) weapon.</summary>
    public void CMD_UnequipWeapon()
    {
        if (!AssertEquipmentManager()) return;
        _equipmentManager.ResetToBaseWeapon();
        Debug.Log("[DevTool] UnequipWeapon → reset to base weapon");
    }

    /// <summary>Equip the Core Shield selected in the dropdown.</summary>
    public void CMD_EquipShield()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_shields, _dropdownShield, "Shields")) return;
        _equipmentManager.EquipCoreShield(_shields[_dropdownShield.value]);
        Debug.Log($"[DevTool] EquipShield: {_shields[_dropdownShield.value].coreShieldName}");
    }

    /// <summary>Remove the currently equipped Core Shield.</summary>
    public void CMD_UnequipShield()
    {
        if (!AssertEquipmentManager()) return;
        _equipmentManager.UnequipCoreShield();
        Debug.Log("[DevTool] UnequipShield");
    }

    /// <summary>Equip the Accessory selected in the dropdown.</summary>
    public void CMD_EquipAccessory()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_accessories, _dropdownAccessory, "Accessories")) return;
        _equipmentManager.EquipAccessory(_accessories[_dropdownAccessory.value]);
        Debug.Log($"[DevTool] EquipAccessory: {_accessories[_dropdownAccessory.value].AccessoryName}");
    }

    /// <summary>Remove the Accessory that is currently selected in the dropdown.</summary>
    public void CMD_UnequipAccessory()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_accessories, _dropdownAccessory, "Accessories")) return;
        _equipmentManager.UnequipAccessory(_accessories[_dropdownAccessory.value]);
        Debug.Log($"[DevTool] UnequipAccessory: {_accessories[_dropdownAccessory.value].AccessoryName}");
    }

    // ============================================================
    //  TAB 3 — PLAYER
    // ============================================================

    /// <summary>Print full player stats snapshot to the Console.</summary>
    public void CMD_LogAllStats()
    {
        if (!AssertStats()) return;
        Debug.Log(
            $"[DevTool] ===== PLAYER STATS =====\n" +
            $"  Level : {_playerStats.level} / {_playerStats.maxLevel}\n" +
            $"  EXP   : {_playerStats.exp} / {_playerStats.nextLevelExp}\n" +
            $"  HP    : {_playerStats.currentHp} / {_playerStats.maxHp}\n" +
            $"  STR   : {_playerStats.STR}  DEX: {_playerStats.DEX}  INT: {_playerStats.INT}\n" +
            $"  VIT   : {_playerStats.VIT}  AGI: {_playerStats.AGI}\n" +
            $"  PhyAtk: {_playerStats.physicalAtk}  MagAtk: {_playerStats.magicAtk}\n" +
            $"  Armor : {_playerStats.armor}  MgRes: {_playerStats.magicResist}\n" +
            $"  Stamina: {_playerStats.currentStamina} / {_playerStats.maxStamina}\n" +
            $"  GodMode: {_devGodMode}"
        );
    }

    /// <summary>Trigger a loot drop test on the first LootDropper found in the scene.</summary>
    public void CMD_SpawnLootTest()
    {
        LootDropper dropper = FindFirstObjectByType<LootDropper>();
        if (dropper == null)
        {
            Debug.LogWarning("[DevTool] SpawnLootTest — no LootDropper found in scene.");
            return;
        }
        dropper.OnEnemyDeath();
        Debug.Log($"[DevTool] SpawnLootTest triggered on {dropper.gameObject.name}");
    }

    /// <summary>
    /// Delete save file and reload scene from scratch (new game).
    /// </summary>
    public void CMD_ResetGame()
    {
        // 1. Xóa save file (player ID = 1)
        string savePath = System.IO.Path.Combine(
            Application.persistentDataPath, "save_player_1.json");
        if (System.IO.File.Exists(savePath))
        {
            System.IO.File.Delete(savePath);
            Debug.Log($"[DevTool] Đã xóa save file: {savePath}");
        }
        else
        {
            Debug.Log("[DevTool] Không tìm thấy save file — game sẽ reset mặc định.");
        }

        // 2. Reload scene
        Time.timeScale = 1f; // Đảm bảo time chạy (inventory có thể đang mở, timeScale = 0)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Debug.Log("[DevTool] ResetGame — reloading scene...");
    }

    /// <summary>
    /// Reset player stats to database defaults without deleting save.
    /// Resets: level=1, exp=0, base attributes to DB values, recalculates.
    /// </summary>
    public void CMD_ResetPlayerStats()
    {
        if (!AssertStats()) return;

        // Tìm GameManager để lấy playerDB
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null || gm.playerDB == null)
        {
            Debug.LogWarning("[DevTool] ResetPlayerStats — GameManager hoặc playerDB null.");
            return;
        }

        // Lấy DB data cho player ID = 1
        PlayerDB dbEntry = gm.playerDB.GetPlayer(1);
        if (dbEntry == null)
        {
            Debug.LogWarning("[DevTool] ResetPlayerStats — không tìm thấy PlayerDB entry (id=1).");
            return;
        }

        // Reset level + exp
        _playerStats.level = 1;
        _playerStats.exp = 0f;
        _playerStats.attributePointRemain = 5;  // Level 1 × 5
        _playerStats.skillPointRemain = 1;      // Level 1

        // Reset base stats từ DB
        _playerStats.baseSTR = dbEntry.STR;
        _playerStats.baseDEX = dbEntry.DEX;
        _playerStats.baseINT = dbEntry.INT;
        _playerStats.baseVIT = dbEntry.VIT;
        _playerStats.baseAGI = dbEntry.AGI;
        _playerStats.initialBaseHp = dbEntry.base_hp;
        _playerStats.maxStamina = dbEntry.max_stamina;

        // Recalculate & refresh
        _playerStats.RefreshExpRequirements();
        _playerStats.RecalculateStats();
        _playerStats.currentHp = _playerStats.maxHp;
        _playerStats.currentStamina = _playerStats.maxStamina;

        Debug.Log("[DevTool] ResetPlayerStats — đã reset về mặc định DB!");
    }

    // ============================================================
    //  PRIVATE GUARD HELPERS
    // ============================================================

    private bool AssertStats()
    {
        if (_playerStats != null) return true;
        Debug.LogWarning("[DevTool] PlayerStats is null — command aborted.");
        return false;
    }

    private bool AssertEquipmentManager()
    {
        if (_equipmentManager != null) return true;
        Debug.LogWarning("[DevTool] EquipmentManager is null — command aborted.");
        return false;
    }

    private bool AssertSkillManager()
    {
        if (_skillManager != null) return true;
        Debug.LogWarning("[DevTool] SkillManager is null — command aborted.");
        return false;
    }

    private bool AssertListIndex<T>(List<T> list, TMP_Dropdown dropdown, string label)
    {
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning($"[DevTool] {label} list is empty — command aborted.");
            return false;
        }
        if (dropdown == null)
        {
            Debug.LogWarning($"[DevTool] Dropdown for {label} is not assigned — command aborted.");
            return false;
        }
        int idx = dropdown.value;
        if (idx < 0 || idx >= list.Count)
        {
            Debug.LogWarning($"[DevTool] {label} dropdown index {idx} out of range [0,{list.Count - 1}].");
            return false;
        }
        return true;
    }
}
#endif
