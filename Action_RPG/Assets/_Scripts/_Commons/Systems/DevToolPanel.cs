#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Dev Tool Panel — chỉ khả dụng trong Editor và Development Build.
/// Toggle bằng phím V. Gắn vào một Canvas GameObject (luôn-on-screen).
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
    //  SKILLS TAB — 5 SLOTS (1 DefaultPassive · 2 Passive · 1 Skill · 1 Signature)
    // ============================================================
    [Header("Skills Tab — 5 Slots")]
    [SerializeField] private TMP_Dropdown _ddDefaultPassive;
    [SerializeField] private TMP_Dropdown _ddPassive1;
    [SerializeField] private TMP_Dropdown _ddPassive2;
    [SerializeField] private TMP_Dropdown _ddSkill;
    [SerializeField] private TMP_Dropdown _ddSignature;

    // ============================================================
    //  EQUIPMENT TAB — WEAPON + SHIELD
    // ============================================================
    [Header("Equipment Tab — Weapon & Shield")]
    [SerializeField] private TMP_Dropdown _dropdownWeapon;
    [SerializeField] private TMP_Dropdown _dropdownShield;

    // ============================================================
    //  EQUIPMENT TAB — ACCESSORIES (5 loại riêng)
    // ============================================================
    [Header("Equipment Tab — Accessories by Type")]
    [SerializeField] private TMP_Dropdown _ddCoreShard;
    [SerializeField] private TMP_Dropdown _ddMarkOfSin;
    [SerializeField] private TMP_Dropdown _ddRelicOfMemory;
    [SerializeField] private TMP_Dropdown _ddParasite;
    [SerializeField] private TMP_Dropdown _ddChain;

    // ============================================================
    //  PLAYER TAB — SPAWN LOOT TEST
    // ============================================================
    [Header("Player Tab — Spawn Loot Test")]
    [Tooltip("Kéo Enemy Prefab (có LootDropper) vào đây để test spawn loot")]
    [SerializeField] private GameObject _spawnEnemyPrefab;
    [Tooltip("Khoảng cách spawn enemy trước mặt player")]
    [SerializeField] private float _spawnOffset = 2f;

    // ============================================================
    //  RUNTIME REFS (auto-found)
    // ============================================================
    private PlayerStats _playerStats;
    private EquipmentManager _equipmentManager;
    private SkillManager _skillManager;

    // ============================================================
    //  DATA LISTS — raw (loaded from Resources)
    // ============================================================
    private List<WeaponData>      _weapons     = new List<WeaponData>();
    private List<CoreShieldData>  _shields     = new List<CoreShieldData>();
    private List<AccessoryData>   _accessories = new List<AccessoryData>();
    private List<SkillData>       _skills      = new List<SkillData>();

    // ============================================================
    //  DATA LISTS — filtered skills by SkillType
    // ============================================================
    private List<SkillData> _defaultPassiveSkills = new List<SkillData>();
    private List<SkillData> _passiveSkills        = new List<SkillData>(); // dùng chung Passive1 & Passive2
    private List<SkillData> _skillSkills          = new List<SkillData>();
    private List<SkillData> _signatureSkills      = new List<SkillData>();

    // ============================================================
    //  DATA LISTS — filtered accessories by AccessoryType
    // ============================================================
    private List<AccessoryData> _coreShardList  = new List<AccessoryData>();
    private List<AccessoryData> _markOfSinList  = new List<AccessoryData>();
    private List<AccessoryData> _relicList      = new List<AccessoryData>();
    private List<AccessoryData> _parasiteList   = new List<AccessoryData>();
    private List<AccessoryData> _chainList      = new List<AccessoryData>();

    // ============================================================
    //  STATE
    // ============================================================
    private bool _isVisible   = false;
    private int  _activeTab   = 0; // 0=Combat, 1=Skills, 2=Equipment, 3=Player
    private bool _devGodMode  = false;

    // ============================================================
    //  UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        _playerStats      = FindFirstObjectByType<PlayerStats>();
        _equipmentManager = FindFirstObjectByType<EquipmentManager>();

        // Không dùng FindFirstObjectByType<SkillManager>() vì sẽ tìm thấy Enemy SkillManager trước.
        // Tìm đúng SkillManager có isPlayer = true (của Player, không phải Enemy).
        _skillManager = FindPlayerSkillManager();

        if (_playerStats      == null) Debug.LogWarning("[DevTool] PlayerStats not found in scene.");
        if (_equipmentManager == null) Debug.LogWarning("[DevTool] EquipmentManager not found in scene.");
        if (_skillManager     == null) Debug.LogWarning("[DevTool] Không tìm thấy SkillManager có isPlayer=true trong scene.");
    }

    /// <summary>
    /// Tìm SkillManager thuộc về Player (isPlayer = true).
    /// Tránh nhầm với SkillManager của Enemy.
    /// </summary>
    private SkillManager FindPlayerSkillManager()
    {
        // Ưu tiên tìm qua tag "Player"
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            SkillManager sm = playerObj.GetComponent<SkillManager>();
            if (sm != null) return sm;
        }

        // Fallback: duyệt tất cả SkillManager và lấy cái có isPlayer = true
        foreach (SkillManager sm in FindObjectsByType<SkillManager>(FindObjectsSortMode.None))
        {
            if (sm.isPlayer) return sm;
        }

        return null;
    }

    private void Start()
    {
        _weapons     = new List<WeaponData>(Resources.LoadAll<WeaponData>("Datas/Weapons"));
        _shields     = new List<CoreShieldData>(Resources.LoadAll<CoreShieldData>("Datas/Core Shields"));
        _accessories = new List<AccessoryData>(Resources.LoadAll<AccessoryData>("Datas/Accessories"));
        _skills      = new List<SkillData>(Resources.LoadAll<SkillData>("Datas/Skills"));

        Debug.Log($"[DevTool] Loaded — Weapons:{_weapons.Count} Shields:{_shields.Count} " +
                  $"Accessories:{_accessories.Count} Skills:{_skills.Count}");

        BuildFilteredLists();
        PopulateDropdowns();
        ShowTab(0);
    }

    // ============================================================
    //  BUILD FILTERED LISTS
    // ============================================================

    private void BuildFilteredLists()
    {
        _defaultPassiveSkills.Clear();
        _passiveSkills.Clear();
        _skillSkills.Clear();
        _signatureSkills.Clear();

        foreach (var sk in _skills)
        {
            switch (sk.skillType)
            {
                case SkillData.SkillType.DefaultPassive: _defaultPassiveSkills.Add(sk); break;
                case SkillData.SkillType.Passive:        _passiveSkills.Add(sk);        break;
                case SkillData.SkillType.Skill:          _skillSkills.Add(sk);          break;
                case SkillData.SkillType.Signature:      _signatureSkills.Add(sk);      break;
            }
        }

        _coreShardList.Clear();
        _markOfSinList.Clear();
        _relicList.Clear();
        _parasiteList.Clear();
        _chainList.Clear();

        foreach (var acc in _accessories)
        {
            switch (acc.accessoryType)
            {
                case AccessoryData.AccessoryType.CoreShard:      _coreShardList.Add(acc);  break;
                case AccessoryData.AccessoryType.MarkOfSin:      _markOfSinList.Add(acc);  break;
                case AccessoryData.AccessoryType.RelicOfMemory:  _relicList.Add(acc);      break;
                case AccessoryData.AccessoryType.Parasite:       _parasiteList.Add(acc);   break;
                case AccessoryData.AccessoryType.Chain:          _chainList.Add(acc);      break;
            }
        }

        Debug.Log($"[DevTool] Skills — DefaultPassive:{_defaultPassiveSkills.Count} " +
                  $"Passive:{_passiveSkills.Count} Skill:{_skillSkills.Count} Signature:{_signatureSkills.Count}");
        Debug.Log($"[DevTool] Accessories — CoreShard:{_coreShardList.Count} MarkOfSin:{_markOfSinList.Count} " +
                  $"Relic:{_relicList.Count} Parasite:{_parasiteList.Count} Chain:{_chainList.Count}");
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
        if (_panelCombat    != null) _panelCombat.SetActive(index == 0);
        if (_panelSkills    != null) _panelSkills.SetActive(index == 1);
        if (_panelEquipment != null) _panelEquipment.SetActive(index == 2);
        if (_panelPlayer    != null) _panelPlayer.SetActive(index == 3);
        _activeTab = index;
    }

    // ============================================================
    //  POPULATE DROPDOWNS
    // ============================================================

    private void PopulateDropdowns()
    {
        // Weapon
        FillDropdown(_dropdownWeapon, _weapons, w => string.IsNullOrEmpty(w.weaponName) ? w.name : w.weaponName);

        // Shield
        FillDropdown(_dropdownShield, _shields, s => string.IsNullOrEmpty(s.coreShieldName) ? s.name : s.coreShieldName);

        // Skills — 5 riêng biệt
        FillDropdown(_ddDefaultPassive, _defaultPassiveSkills, sk => string.IsNullOrEmpty(sk.skillName) ? sk.name : sk.skillName);
        FillDropdown(_ddPassive1,       _passiveSkills,        sk => string.IsNullOrEmpty(sk.skillName) ? sk.name : sk.skillName);
        FillDropdown(_ddPassive2,       _passiveSkills,        sk => string.IsNullOrEmpty(sk.skillName) ? sk.name : sk.skillName);
        FillDropdown(_ddSkill,          _skillSkills,          sk => string.IsNullOrEmpty(sk.skillName) ? sk.name : sk.skillName);
        FillDropdown(_ddSignature,      _signatureSkills,      sk => string.IsNullOrEmpty(sk.skillName) ? sk.name : sk.skillName);

        // Accessories — 5 riêng biệt theo type
        FillDropdown(_ddCoreShard,      _coreShardList, a => string.IsNullOrEmpty(a.AccessoryName) ? a.name : a.AccessoryName);
        FillDropdown(_ddMarkOfSin,      _markOfSinList, a => string.IsNullOrEmpty(a.AccessoryName) ? a.name : a.AccessoryName);
        FillDropdown(_ddRelicOfMemory,  _relicList,     a => string.IsNullOrEmpty(a.AccessoryName) ? a.name : a.AccessoryName);
        FillDropdown(_ddParasite,       _parasiteList,  a => string.IsNullOrEmpty(a.AccessoryName) ? a.name : a.AccessoryName);
        FillDropdown(_ddChain,          _chainList,     a => string.IsNullOrEmpty(a.AccessoryName) ? a.name : a.AccessoryName);
    }

    /// <summary>Generic helper: clear + fill a dropdown from a list using a name selector.</summary>
    private void FillDropdown<T>(TMP_Dropdown dropdown, List<T> list, System.Func<T, string> nameSelector)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        var options = new List<string>();
        foreach (var item in list)
            options.Add(nameSelector(item));
        dropdown.AddOptions(options);
    }

    // ============================================================
    //  TAB 0 — COMBAT
    // ============================================================

    /// <summary>Deal 10 raw damage to the player.</summary>
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
        _playerStats.Heal(_playerStats.maxHp - _playerStats.currentHp);
        Debug.Log($"[DevTool] HealFull → HP: {_playerStats.currentHp}/{_playerStats.maxHp}");
    }

    /// <summary>Add 100 EXP to the player.</summary>
    public void CMD_AddExp100()
    {
        if (!AssertStats()) return;
        _playerStats.AddExp(100f);
        Debug.Log($"[DevTool] AddExp 100 → EXP: {_playerStats.exp}/{_playerStats.nextLevelExp}");
    }

    /// <summary>Force level-up by giving exactly enough EXP.</summary>
    public void CMD_ForceLevelUp()
    {
        if (!AssertStats()) return;
        if (_playerStats.level >= _playerStats.maxLevel)
        {
            Debug.LogWarning("[DevTool] ForceLevelUp — player is already at max level.");
            return;
        }
        float expNeeded = _playerStats.nextLevelExp - _playerStats.exp;
        _playerStats.AddExp(Mathf.Max(expNeeded, 1f));
        Debug.Log($"[DevTool] ForceLevelUp → Level: {_playerStats.level}");
    }

    /// <summary>Toggle God Mode (Stats.isInvincible).</summary>
    public void CMD_ToggleGodMode()
    {
        if (!AssertStats()) return;
        _devGodMode = !_devGodMode;
        _playerStats.isInvincible = _devGodMode;
        Debug.Log($"[DevTool] God Mode: {(_devGodMode ? "<color=green>ON</color>" : "<color=red>OFF</color>")}");
    }

    /// <summary>Tạo Shield bằng 20% max HP, tồn tại 3 giây.</summary>
    public void CMD_AddShield20()
    {
        if (!AssertStats()) return;
        float amount = _playerStats.maxHp * 0.2f;
        _playerStats.AddShield(amount, 3f);
        Debug.Log($"[DevTool] AddShield 20% ({amount:F0}) — tồn tại 3s");
    }

    /// <summary>Tạo Shield bằng 50% max HP, tồn tại 3 giây.</summary>
    public void CMD_AddShield50()
    {
        if (!AssertStats()) return;
        float amount = _playerStats.maxHp * 0.5f;
        _playerStats.AddShield(amount, 3f);
        Debug.Log($"[DevTool] AddShield 50% ({amount:F0}) — tồn tại 3s");
    }

    // ============================================================
    //  TAB 1 — SKILLS
    // ============================================================

    // --- DEFAULT PASSIVE ---

    /// <summary>Trang bị Default Passive từ dropdown.</summary>
    public void CMD_EquipDefaultPassive()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_defaultPassiveSkills, _ddDefaultPassive, "DefaultPassive")) return;
        _skillManager.EquipSkill(_defaultPassiveSkills[_ddDefaultPassive.value]);
        Debug.Log($"[DevTool] EquipDefaultPassive: {_defaultPassiveSkills[_ddDefaultPassive.value].skillName}");
    }

    /// <summary>Tháo Default Passive đang trang bị. Không làm gì nếu slot trống.</summary>
    public void CMD_UnequipDefaultPassive()
    {
        if (!AssertSkillManager()) return;
        _skillManager.UnequipDefaultPassive();
    }

    // --- PASSIVE 1 ---

    /// <summary>Trang bị Passive vào Slot 1 từ dropdown.</summary>
    public void CMD_EquipPassive1()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_passiveSkills, _ddPassive1, "Passive1")) return;
        _skillManager.EquipSkill(_passiveSkills[_ddPassive1.value]);
        Debug.Log($"[DevTool] EquipPassive1: {_passiveSkills[_ddPassive1.value].skillName}");
    }

    /// <summary>Tháo Passive Slot 1. Không làm gì nếu slot trống.</summary>
    public void CMD_UnequipPassive1()
    {
        if (!AssertSkillManager()) return;
        _skillManager.UnequipPassive1();
    }

    // --- PASSIVE 2 ---

    /// <summary>Trang bị Passive vào Slot 2 từ dropdown.</summary>
    public void CMD_EquipPassive2()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_passiveSkills, _ddPassive2, "Passive2")) return;
        _skillManager.EquipSkill(_passiveSkills[_ddPassive2.value]);
        Debug.Log($"[DevTool] EquipPassive2: {_passiveSkills[_ddPassive2.value].skillName}");
    }

    /// <summary>Tháo Passive Slot 2. Không làm gì nếu slot trống.</summary>
    public void CMD_UnequipPassive2()
    {
        if (!AssertSkillManager()) return;
        _skillManager.UnequipPassive2();
    }

    // --- SKILL (E/Q) ---

    /// <summary>Trang bị Skill (E/Q) từ dropdown.</summary>
    public void CMD_EquipSkill()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_skillSkills, _ddSkill, "Skill")) return;
        _skillManager.EquipSkill(_skillSkills[_ddSkill.value]);
        Debug.Log($"[DevTool] EquipSkill: {_skillSkills[_ddSkill.value].skillName}");
    }

    /// <summary>Tháo Skill (E/Q). Không làm gì nếu slot trống.</summary>
    public void CMD_UnequipSkill()
    {
        if (!AssertSkillManager()) return;
        _skillManager.UnequipSkillSlot();
    }

    // --- SIGNATURE (R) ---

    /// <summary>Trang bị Signature (R) từ dropdown.</summary>
    public void CMD_EquipSignature()
    {
        if (!AssertSkillManager()) return;
        if (!AssertListIndex(_signatureSkills, _ddSignature, "Signature")) return;
        _skillManager.EquipSkill(_signatureSkills[_ddSignature.value]);
        Debug.Log($"[DevTool] EquipSignature: {_signatureSkills[_ddSignature.value].skillName}");
    }

    /// <summary>Tháo Signature (R). Không làm gì nếu slot trống.</summary>
    public void CMD_UnequipSignature()
    {
        if (!AssertSkillManager()) return;
        _skillManager.UnequipSignatureSlot();
    }

    // --- CAST (giữ lại để test) ---

    /// <summary>Cast Skill đang trang bị (currentSkill).</summary>
    public void CMD_CastSkill()
    {
        if (!AssertSkillManager()) return;
        if (_skillManager.currentSkill == null)
        {
            Debug.LogWarning("[DevTool] CastSkill — chưa trang bị Skill.");
            return;
        }
        _skillManager.CastSkill(_skillManager.currentSkill);
        Debug.Log($"[DevTool] CastSkill: {_skillManager.currentSkill.skillName}");
    }

    /// <summary>Cast Signature đang trang bị (currentSignature).</summary>
    public void CMD_CastSignature()
    {
        if (!AssertSkillManager()) return;
        if (_skillManager.currentSignature == null)
        {
            Debug.LogWarning("[DevTool] CastSignature — chưa trang bị Signature.");
            return;
        }
        _skillManager.CastSkill(_skillManager.currentSignature);
        Debug.Log($"[DevTool] CastSignature: {_skillManager.currentSignature.skillName}");
    }

    // ============================================================
    //  TAB 2 — EQUIPMENT
    // ============================================================

    // --- WEAPON ---

    public void CMD_EquipWeapon()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_weapons, _dropdownWeapon, "Weapons")) return;
        _equipmentManager.EquipWeapon(_weapons[_dropdownWeapon.value]);
        Debug.Log($"[DevTool] EquipWeapon: {_weapons[_dropdownWeapon.value].weaponName}");
    }

    public void CMD_UnequipWeapon()
    {
        if (!AssertEquipmentManager()) return;
        _equipmentManager.ResetToBaseWeapon();
        Debug.Log("[DevTool] UnequipWeapon → reset to base weapon");
    }

    // --- SHIELD ---

    public void CMD_EquipShield()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_shields, _dropdownShield, "Shields")) return;
        _equipmentManager.EquipCoreShield(_shields[_dropdownShield.value]);
        Debug.Log($"[DevTool] EquipShield: {_shields[_dropdownShield.value].coreShieldName}");
    }

    public void CMD_UnequipShield()
    {
        if (!AssertEquipmentManager()) return;
        _equipmentManager.UnequipCoreShield();
        Debug.Log("[DevTool] UnequipShield");
    }

    // --- ACCESSORIES (5 loại) ---

    public void CMD_EquipCoreShard()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_coreShardList, _ddCoreShard, "CoreShard")) return;
        _equipmentManager.EquipAccessory(_coreShardList[_ddCoreShard.value]);
        Debug.Log($"[DevTool] EquipCoreShard: {_coreShardList[_ddCoreShard.value].AccessoryName}");
    }

    public void CMD_UnequipCoreShard()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_coreShardList, _ddCoreShard, "CoreShard")) return;
        _equipmentManager.UnequipAccessory(_coreShardList[_ddCoreShard.value]);
        Debug.Log($"[DevTool] UnequipCoreShard: {_coreShardList[_ddCoreShard.value].AccessoryName}");
    }

    public void CMD_EquipMarkOfSin()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_markOfSinList, _ddMarkOfSin, "MarkOfSin")) return;
        _equipmentManager.EquipAccessory(_markOfSinList[_ddMarkOfSin.value]);
        Debug.Log($"[DevTool] EquipMarkOfSin: {_markOfSinList[_ddMarkOfSin.value].AccessoryName}");
    }

    public void CMD_UnequipMarkOfSin()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_markOfSinList, _ddMarkOfSin, "MarkOfSin")) return;
        _equipmentManager.UnequipAccessory(_markOfSinList[_ddMarkOfSin.value]);
        Debug.Log($"[DevTool] UnequipMarkOfSin: {_markOfSinList[_ddMarkOfSin.value].AccessoryName}");
    }

    public void CMD_EquipRelicOfMemory()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_relicList, _ddRelicOfMemory, "RelicOfMemory")) return;
        _equipmentManager.EquipAccessory(_relicList[_ddRelicOfMemory.value]);
        Debug.Log($"[DevTool] EquipRelicOfMemory: {_relicList[_ddRelicOfMemory.value].AccessoryName}");
    }

    public void CMD_UnequipRelicOfMemory()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_relicList, _ddRelicOfMemory, "RelicOfMemory")) return;
        _equipmentManager.UnequipAccessory(_relicList[_ddRelicOfMemory.value]);
        Debug.Log($"[DevTool] UnequipRelicOfMemory: {_relicList[_ddRelicOfMemory.value].AccessoryName}");
    }

    public void CMD_EquipParasite()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_parasiteList, _ddParasite, "Parasite")) return;
        _equipmentManager.EquipAccessory(_parasiteList[_ddParasite.value]);
        Debug.Log($"[DevTool] EquipParasite: {_parasiteList[_ddParasite.value].AccessoryName}");
    }

    public void CMD_UnequipParasite()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_parasiteList, _ddParasite, "Parasite")) return;
        _equipmentManager.UnequipAccessory(_parasiteList[_ddParasite.value]);
        Debug.Log($"[DevTool] UnequipParasite: {_parasiteList[_ddParasite.value].AccessoryName}");
    }

    public void CMD_EquipChain()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_chainList, _ddChain, "Chain")) return;
        _equipmentManager.EquipAccessory(_chainList[_ddChain.value]);
        Debug.Log($"[DevTool] EquipChain: {_chainList[_ddChain.value].AccessoryName}");
    }

    public void CMD_UnequipChain()
    {
        if (!AssertEquipmentManager()) return;
        if (!AssertListIndex(_chainList, _ddChain, "Chain")) return;
        _equipmentManager.UnequipAccessory(_chainList[_ddChain.value]);
        Debug.Log($"[DevTool] UnequipChain: {_chainList[_ddChain.value].AccessoryName}");
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

    /// <summary>
    /// Spawn enemy prefab (_spawnEnemyPrefab) phía trước player.
    /// Enemy sẽ ở lại trong scene — player tự tay giết để test EXP + loot drop thật.
    /// Gán _spawnEnemyPrefab qua Inspector (kéo Enemy Prefab có LootDropper vào).
    /// </summary>
    public void CMD_SpawnLootTest()
    {
        if (_spawnEnemyPrefab == null)
        {
            Debug.LogWarning("[DevTool] SpawnLootTest — _spawnEnemyPrefab chưa được gán. " +
                             "Kéo Enemy Prefab vào field trong Inspector.");
            return;
        }

        // Tìm vị trí player để spawn enemy phía trước
        GameObject playerObj = GameObject.FindWithTag("Player");
        Vector3 spawnPos;
        if (playerObj != null)
        {
            spawnPos = playerObj.transform.position + playerObj.transform.forward * _spawnOffset;
            spawnPos.y = playerObj.transform.position.y;
        }
        else
        {
            spawnPos = new Vector3(0f, 0f, _spawnOffset);
            Debug.LogWarning("[DevTool] SpawnLootTest — không tìm thấy tag 'Player'. Spawn tại gốc tọa độ.");
        }

        GameObject spawnedEnemy = Instantiate(_spawnEnemyPrefab, spawnPos, Quaternion.identity);
        Debug.Log($"[DevTool] ✅ Spawned '{spawnedEnemy.name}' tại {spawnPos}. " +
                  $"Hãy giết enemy để test EXP + loot drop!");

        // Kiểm tra enemy có đầy đủ component không
        LootDropper dropper = spawnedEnemy.GetComponent<LootDropper>()
                           ?? spawnedEnemy.GetComponentInChildren<LootDropper>();
        if (dropper == null)
            Debug.LogWarning($"[DevTool] ⚠ '{_spawnEnemyPrefab.name}' chưa có LootDropper — " +
                             "enemy sẽ không rớt đồ khi chết.");

        EnemyStats es = spawnedEnemy.GetComponent<EnemyStats>();
        if (es != null)
            Debug.Log($"[DevTool]   └ EXP reward: {es.expReward} | HP: {es.baseHp}");
    }

    /// <summary>Chuyển sang nhân vật Chris và reload scene.</summary>
    public void CMD_SwitchToChris()
    {
        PlayerPrefs.SetString("SelectedCharacter", "Chris");
        PlayerPrefs.Save();
        Debug.Log("[DevTool] Chuyển sang Chris — reloading scene...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Chuyển sang nhân vật Leo và reload scene.</summary>
    public void CMD_SwitchToLeo()
    {
        PlayerPrefs.SetString("SelectedCharacter", "Leo");
        PlayerPrefs.Save();
        Debug.Log("[DevTool] Chuyển sang Leo — reloading scene...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Reset TOÀN BỘ game về trạng thái mặc định:
    /// 1. Chặn GameManager.OnDestroy() ghi đè save (fix bug static Dictionary)
    /// 2. Xóa static memory cache (InAppPlayerStateDAO.savedStates)
    /// 3. Xóa save file trên disk
    /// 4. Reload scene → player load lại từ DB default
    /// </summary>
    public void CMD_ResetGame()
    {
        // 1. Chặn GameManager.OnDestroy() ghi lại save khi scene unload
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.suppressNextSave = true;
            Debug.Log("[DevTool] ResetGame — suppressNextSave = true trên GameManager.");
        }

        // 2. Xóa static memory cache (BUG ROOT CAUSE: static Dictionary tồn tại qua scene reload)
        InAppPlayerStateDAO.ClearMemoryCache();

        // 3. Xóa tất cả save file per-character trên disk
        string[] charIds = { "Chris", "Leo" };
        foreach (string cid in charIds)
        {
            string charSavePath = Path.Combine(Application.persistentDataPath, $"save_{cid}.json");
            if (File.Exists(charSavePath))
            {
                File.Delete(charSavePath);
                Debug.Log($"[DevTool] ResetGame — đã xóa save file: {charSavePath}");
            }
        }
        // Xóa cả file legacy (save_player_1.json) nếu còn tồn tại
        string legacySavePath = Path.Combine(Application.persistentDataPath, "save_player_1.json");
        if (File.Exists(legacySavePath))
        {
            File.Delete(legacySavePath);
            Debug.Log($"[DevTool] ResetGame — đã xóa legacy save file: {legacySavePath}");
        }

        // 3b. Xóa tiến trình Skill Tree + SP per-character khỏi PlayerPrefs
        SkillTreeRuntime.ClearAllSavedProgress("Chris", "Leo");
        Debug.Log("[DevTool] ResetGame — đã xóa toàn bộ tiến trình Skill Tree.");

        // 4. Reload scene
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Debug.Log("[DevTool] ResetGame — reloading scene...");
    }

    /// <summary>
    /// Reset player stats về DB default mà KHÔNG xóa save hay reload scene.
    /// Dùng khi chỉ muốn reset chỉ số trong session đang chạy.
    /// </summary>
    public void CMD_ResetPlayerStats()
    {
        if (!AssertStats()) return;

        GameManager gm2 = FindFirstObjectByType<GameManager>();
        if (gm2 == null || gm2.playerDB == null)
        {
            Debug.LogWarning("[DevTool] ResetPlayerStats — GameManager hoặc playerDB null.");
            return;
        }

        PlayerDB dbEntry = gm2.playerDB.GetPlayer(1);
        if (dbEntry == null)
        {
            Debug.LogWarning("[DevTool] ResetPlayerStats — không tìm thấy PlayerDB entry (id=1).");
            return;
        }

        _playerStats.level               = 1;
        _playerStats.exp                 = 0f;
        _playerStats.attributePointRemain = 5;
        _playerStats.skillPointRemain    = 1;
        _playerStats.baseSTR             = dbEntry.STR;
        _playerStats.baseDEX             = dbEntry.DEX;
        _playerStats.baseINT             = dbEntry.INT;
        _playerStats.baseVIT             = dbEntry.VIT;
        _playerStats.baseAGI             = dbEntry.AGI;
        _playerStats.initialBaseHp       = dbEntry.base_hp;
        _playerStats.maxStamina          = dbEntry.max_stamina;

        _playerStats.RefreshExpRequirements();
        _playerStats.RecalculateStats();
        _playerStats.currentHp           = _playerStats.maxHp;
        _playerStats.currentStamina      = _playerStats.maxStamina;

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
