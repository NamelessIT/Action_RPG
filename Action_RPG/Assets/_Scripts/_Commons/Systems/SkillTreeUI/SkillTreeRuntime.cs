using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Runtime state: theo dõi node nào đã unlock.
/// Key = nodeId (string). Stat node apply ngay khi unlock.
/// </summary>
public class SkillTreeRuntime : MonoBehaviour
{
    public event Action OnSkillTreeChanged;

    // Key = nodeId, Value = 1 (đã unlock). Stat nodes luôn maxLevel=1.
    private Dictionary<string, int> _unlockedNodes = new Dictionary<string, int>();

    private SkillData _equippedSkill;
    private SkillData _equippedSignature;

    private PlayerStats _playerStats;
    private AllyStats   _allyStats;
    private SkillManager _skillManager;
    public SkillTreeDatabase CurrentDatabase { get; private set; }

    private void Awake()
    {
        // SkillTreeRuntime là player-only, LUÔN nằm cùng Player root với PlayerStats/SkillManager.
        // KHÔNG dùng FindFirstObjectByType để "cứu" — nếu bị gắn sai chỗ thì báo lỗi để fix hierarchy,
        // tránh nguy cơ tóm nhầm Player global khi component nằm trên object khác.
        _playerStats  = GetComponent<PlayerStats>();
        _allyStats    = GetComponent<AllyStats>();
        _skillManager = GetComponent<SkillManager>();

        if (_playerStats == null)
            Debug.LogError($"[SkillTreeRuntime] Không tìm thấy PlayerStats trên '{gameObject.name}'. " +
                           "Component này phải nằm cùng Player root.");
        if (_skillManager == null)
            Debug.LogError($"[SkillTreeRuntime] Không tìm thấy SkillManager trên '{gameObject.name}'. " +
                           "Component này phải nằm cùng Player root.");
    }

    // ─────────────────────────────────────────────────────────────
    //  QUERIES
    // ─────────────────────────────────────────────────────────────
    public void SetDatabase(SkillTreeDatabase db)
    {
        CurrentDatabase = db;
    }
    public bool IsUnlocked(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        return _unlockedNodes.ContainsKey(nodeId);
    }

    public bool CanUnlockNode(SkillNodeData node)
    {
        if (node == null || string.IsNullOrEmpty(node.nodeId)) return false;
        if (_playerStats == null) return false;
        if (IsUnlocked(node.nodeId)) return false;               // Đã unlock
        if (_playerStats.level < node.requiredLevel) return false;
        if (_playerStats.skillPointRemain < node.unlockCost) return false;

        // Check node điều kiện đi trước
        if (node.prerequisiteNodeIds != null)
        {
            foreach (var prereqId in node.prerequisiteNodeIds)
            {
                if (!string.IsNullOrEmpty(prereqId) && !IsUnlocked(prereqId))
                    return false;
            }
        }

        // --- VALIDATE THEO LUẬT CHUYÊN SÂU TỪ GAME DESIGN ---
        if (!GetNodeInfo(node, out int classIdx, out int nodeIdxInClass, out int tier))
            return false;

        int currentLv = _playerStats.level;

        // TẦNG 1: Được phép nâng tự do (miễn đủ level 1-5 và có SP)
        if (tier == 1) return true;

        // TẦNG 2: Chọn 2 trên 4 Class. Nâng xong class này mới được sang class kia.
        if (tier == 2)
        {
            if (GetT1UnlockedCount() < 5) return false; // Bắt buộc max Tầng 1

            var selectedT2 = GetSelectedClassesByTier(2);
            if (!selectedT2.Contains(classIdx)) // Chọn class mới
            {
                if (selectedT2.Count >= 2) return false; // Đã chọn đủ 2 class, khóa 2 cột còn lại

                if (selectedT2.Count == 1) // Bắt đầu nâng Class thứ 2
                {
                    if (currentLv < 11) return false; // Phải đạt Lv 11
                    if (GetUnlockedCount(selectedT2[0], 2) < 5) return false; // Phải nâng max 5 node của Class đầu tiên
                }
            }
            return true;
        }

        // TẦNG 3: Nâng 1 trong 2 class đã max T2. Nâng xong mới qua class kia.
        if (tier == 3)
        {
            var selectedT2 = GetSelectedClassesByTier(2);
            if (selectedT2.Count < 2 || GetUnlockedCount(selectedT2[0], 2) < 5 || GetUnlockedCount(selectedT2[1], 2) < 5)
                return false; // Phải max T2 của cả 2 class trước

            if (!selectedT2.Contains(classIdx)) return false; // Chỉ được nâng nhánh của 2 class đã chọn

            var selectedT3 = GetSelectedClassesByTier(3);
            if (!selectedT3.Contains(classIdx))
            {
                if (selectedT3.Count >= 2) return false;

                if (selectedT3.Count == 1) // Bắt đầu nâng T3 của Class thứ 2
                {
                    if (currentLv < 21) return false;
                    if (GetUnlockedCount(selectedT3[0], 3) < 5) return false; // Phải max 5 node T3 của class đầu
                }
            }
            return true;
        }

        // TẦNG 4: Class Chính (Lv 26-40) và Class Phụ (Lv 46-60)
        if (tier == 4)
        {
            var selectedT3 = GetSelectedClassesByTier(3);
            if (selectedT3.Count < 2 || GetUnlockedCount(selectedT3[0], 3) < 5 || GetUnlockedCount(selectedT3[1], 3) < 5)
                return false; // Phải max T3 cả 2 class

            if (!selectedT3.Contains(classIdx)) return false;

            int mainClass = GetMainClassIndex();
            if (mainClass == -1) // Nhát click đầu tiên vào T4 -> Định hình Main Class
            {
                if (currentLv < 26) return false;
                return true;
            }

            if (classIdx == mainClass)
            {
                // Đang nâng Class chính
                return true;
            }
            else
            {
                // Đang nâng Class phụ (Chỉ được nâng ở Lv 46-60 sau khi Main Class đã ăn trọn Tầng 5)
                if (currentLv < 46) return false;
                if (GetUnlockedCount(mainClass, 5) < 5) return false; // Phải max Tầng 5 của Main Class trước
                return true;
            }
        }

        // TẦNG 5: Chỉ duy nhất Main Class được nâng (Lv 41-45)
        if (tier == 5)
        {
            int mainClass = GetMainClassIndex();
            if (mainClass == -1 || classIdx != mainClass) return false; // Chỉ dành cho Main Class

            if (GetUnlockedCount(mainClass, 4) < 15) return false; // Phải max trọn vẹn 15 node Tầng 4 của Main Class
            if (currentLv < 41) return false;

            return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS: DYNAMIC VALIDATION (Không cần file save)
    // ─────────────────────────────────────────────────────────────



    private int GetUnlockedCount(int classIndex, int tier)
    {
        if (CurrentDatabase == null) return 0;
        var tree = CurrentDatabase.allClassTrees[classIndex];

        int startIdx = tier == 2 ? 0 : tier == 3 ? 5 : tier == 4 ? 10 : 25;
        int count = tier == 4 ? 15 : 5;

        int unlockedCount = 0;
        for (int i = startIdx; i < startIdx + count; i++)
        {
            if (i < tree.skillNodes.Count && IsUnlocked(tree.skillNodes[i].nodeId))
                unlockedCount++;
        }
        return unlockedCount;
    }

    private int GetT1UnlockedCount()
    {
        if (CurrentDatabase == null || CurrentDatabase.commonTree == null) return 0;
        int count = 0;
        foreach (var node in CurrentDatabase.commonTree.skillNodes)
            if (IsUnlocked(node.nodeId)) count++;
        return count;
    }

    private List<int> GetSelectedClassesByTier(int tier)
    {
        List<int> list = new List<int>();
        for (int c = 0; c < 4; c++)
        {
            if (GetUnlockedCount(c, tier) > 0) list.Add(c);
        }
        // Sắp xếp giảm dần theo số node đã nâng, để thằng nào được cày trước sẽ nằm ở index [0]
        list.Sort((a, b) => GetUnlockedCount(b, tier).CompareTo(GetUnlockedCount(a, tier)));
        return list;
    }

    private int GetMainClassIndex()
    {
        // Nếu đã nâng T5, thằng đó 100% là Main Class
        for (int c = 0; c < 4; c++) if (GetUnlockedCount(c, 5) > 0) return c;
        // Nếu chưa lên tới T5, check thằng nào vừa bú liếm T4 đầu tiên
        for (int c = 0; c < 4; c++) if (GetUnlockedCount(c, 4) > 0) return c;
        return -1;
    }

    public int GetNextCost(SkillNodeData node)
    {
        return node?.unlockCost ?? 0;
    }
    public string GetFusionClassName(string classA, string classB)
    {
        string pair = $"{classA}+{classB}".Replace(" ", "").Replace("-", "");
        string pairRev = $"{classB}+{classA}".Replace(" ", "").Replace("-", "");

        // Chris Don combos
        if (pair == "Vanguard+Warrior" || pairRev == "Vanguard+Warrior") return "Warden";
        if (pair == "Vanguard+BattleMage" || pairRev == "Vanguard+BattleMage") return "Paladin";
        if (pair == "Vanguard+BloodReaver" || pairRev == "Vanguard+BloodReaver") return "Juggernaut";
        if (pair == "Warrior+BattleMage" || pairRev == "Warrior+BattleMage") return "Dark Inquisitor";
        if (pair == "Warrior+BloodReaver" || pairRev == "Warrior+BloodReaver") return "Ravager";
        if (pair == "BattleMage+BloodReaver" || pairRev == "BattleMage+BloodReaver") return "Warlock";

        // Leonard II combos
        if (pair == "Rogue+Duelist" || pairRev == "Rogue+Duelist") return "Sword Master";
        if (pair == "Rogue+Mage" || pairRev == "Rogue+Mage") return "Trickster";
        if (pair == "Rogue+Catalyst" || pairRev == "Rogue+Catalyst") return "Infiltrator";
        if (pair == "Duelist+Mage" || pairRev == "Duelist+Mage") return "Spellblade";
        if (pair == "Duelist+Catalyst" || pairRev == "Duelist+Catalyst") return "Tactician";
        if (pair == "Mage+Catalyst" || pairRev == "Mage+Catalyst") return "Spellbinder";

        return $"{classA} + {classB}";
    }

    public SkillData GetActualSkillToEquip(SkillNodeData node)
    {
        if (node == null || node.skillData == null) return null;
        SkillData skill = node.skillData;

        if (GetNodeInfo(node, out int classIdx, out int nodeIdx, out int tier))
        {
            if (tier == 3 && nodeIdx == 7)
            {
                var selectedT3 = GetSelectedClassesByTier(3);
                if (selectedT3.Count >= 2)
                {
                    var treeA = CurrentDatabase.allClassTrees[selectedT3[0]];
                    var treeB = CurrentDatabase.allClassTrees[selectedT3[1]];
                    if (IsUnlocked(treeA.skillNodes[7].nodeId) && IsUnlocked(treeB.skillNodes[7].nodeId))
                    {
                        SkillData fusion = GetFusionSkillDataInDB(treeA.className, treeB.className);
                        if (fusion != null) skill = fusion;
                    }
                }
            }
        }
        return skill;
    }

    private SkillData GetFusionSkillDataInDB(string classA, string classB)
    {
        if (CurrentDatabase == null || CurrentDatabase.fusionSkills == null) return null;

        string cleanA = CleanClassName(classA);
        string cleanB = CleanClassName(classB);

        foreach (var f in CurrentDatabase.fusionSkills)
        {
            string fCleanA = CleanClassName(f.classA);
            string fCleanB = CleanClassName(f.classB);

            if ((fCleanA == cleanA && fCleanB == cleanB) || (fCleanA == cleanB && fCleanB == cleanA))
            {
                if (f.fusionNodes.Count > 0 && f.fusionNodes[0].skillData != null)
                    return f.fusionNodes[0].skillData;
            }
        }
        return null;
    }

    private bool GetNodeInfo(SkillNodeData node, out int classIndex, out int nodeIndex, out int tier)
    {
        classIndex = -1; nodeIndex = -1; tier = -1;
        if (CurrentDatabase == null) return false;

        if (CurrentDatabase.commonTree != null)
        {
            int idx = CurrentDatabase.commonTree.skillNodes.IndexOf(node);
            if (idx != -1)
            {
                tier = 1;
                nodeIndex = idx; // SỬA ĐỔI: Thêm gán nodeIndex để sửa lỗi nhận diện Tầng 1
                return true;
            }
        }

        for (int c = 0; c < CurrentDatabase.allClassTrees.Count; c++)
        {
            var tree = CurrentDatabase.allClassTrees[c];
            if (tree == null) continue;
            int idx = tree.skillNodes.IndexOf(node);
            if (idx != -1)
            {
                classIndex = c;
                nodeIndex = idx;
                if (idx < 5) tier = 2;
                else if (idx < 10) tier = 3;
                else if (idx < 25) tier = 4;
                else if (idx < 30) tier = 5;
                return true;
            }
        }
        return false;
    }

    private string CleanClassName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name.Replace(" ", "").Replace("-", "").ToLower();
    }

    public bool IsNodeEquipBanned(SkillNodeData node)
    {
        if (GetNodeInfo(node, out int classIdx, out int nodeIdx, out int tier))
        {
            // SỬA ĐỔI 1: Khóa nút Equip ở Node 3 (index 2) và Node 4 (index 3) của Tầng 1 khi đã chọn Class
            if (tier == 1)
            {
                if (nodeIdx == 2 || nodeIdx == 3)
                {
                    if (GetSelectedClassesByTier(2).Count > 0) return true;
                }
            }

            // SỬA ĐỔI 2: Khi đã mở khóa đủ 2 Kỹ năng Tầng 3 (index 7) -> Khóa chết nút chọn lẻ, ép dùng Combo Skill
            if (tier == 3 && nodeIdx == 7)
            {
                var selectedT3 = GetSelectedClassesByTier(3);
                if (selectedT3.Count >= 2)
                {
                    var treeA = CurrentDatabase.allClassTrees[selectedT3[0]];
                    var treeB = CurrentDatabase.allClassTrees[selectedT3[1]];
                    if (IsUnlocked(treeA.skillNodes[7].nodeId) && IsUnlocked(treeB.skillNodes[7].nodeId))
                    {
                        return true;
                    }
                }
            }

            // SỬA ĐỔI 3: Khóa toàn bộ Signature Lite (Tầng 2 Node 5 - index 4) khi đã học Signature xịn ở Tầng 5
            if (tier == 2 && nodeIdx == 4)
            {
                for (int c = 0; c < 4; c++)
                    if (GetUnlockedCount(c, 5) > 0) return true;
            }
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  UNLOCK
    // ─────────────────────────────────────────────────────────────

    public bool UnlockNode(SkillNodeData node)
    {
        if (!CanUnlockNode(node)) return false;

        _playerStats.skillPointRemain -= node.unlockCost;
        _unlockedNodes[node.nodeId] = 1;

        // Apply effect ngay lập tức
        switch (node.nodeType)
        {
            case SkillNodeType.Stat:
                ApplyStatGains(node);
                break;
            case SkillNodeType.Skill:
                if (node.skillData != null)
                    AutoEquipNode(node);
                break;
            case SkillNodeType.CoreMechanic:
                ApplyCoreMechanicNode(node);
                break;
            case SkillNodeType.UpgradeSkill:
                ApplyUpgradeSkillNode(node);
                break;
        }

        ApplyClassGrant(node);
        SaveUnlockedNodes();

        Debug.Log($"[SkillTreeRuntime] UNLOCK '{node.nodeName}' (Cost: {node.unlockCost} SP, Remain: {_playerStats.skillPointRemain})");
        OnSkillTreeChanged?.Invoke();
        return true;
    }

    /// <summary>Auto-equip kỹ năng vào đúng slot khi unlock.</summary>
    private void AutoEquipNode(SkillNodeData node)
    {
        if (_skillManager == null || node.skillData == null) return;

        GetNodeInfo(node, out int classIdx, out int nodeIdx, out int tier);
        SkillData skillToEquip = GetActualSkillToEquip(node);

        // Nếu học Signature Lite thứ hai ở lv 15 -> Giữ nguyên cái cũ, không tự chèn đè lên slot
        if (tier == 2 && nodeIdx == 4 && _equippedSignature != null) return;

        // Các mốc tự động thay thế bắt buộc (Kỹ năng kết hợp lv 23, Signature xịn lv 41)
        EquipSkillInternal(skillToEquip);
    }

    public void EquipSkillFromNode(SkillNodeData node)
    {
        if (IsNodeEquipBanned(node)) return;
        SkillData actualSkill = GetActualSkillToEquip(node);
        EquipSkillInternal(actualSkill);
    }

    private void EquipSkillInternal(SkillData skill)
    {
        if (skill == null || _skillManager == null) return;

        // SỬA ĐỔI: Đồng bộ hóa gọi trực tiếp hàm xử lý của hệ thống Combat gốc, tránh lỗi trạng thái trống
        // CHỈ ghi nhận equipped khi SkillManager equip THẬT SỰ thành công (gắn được script).
        bool ok = _skillManager.EquipSkill(skill);
        if (!ok)
        {
            Debug.LogWarning($"[SkillTreeRuntime] Equip '{skill.skillName}' thất bại (thiếu effect code) — không ghi vào equipped slot.");
            OnSkillTreeChanged?.Invoke();
            return;
        }

        if (skill.skillType == SkillData.SkillType.Skill) _equippedSkill = skill;
        else if (skill.skillType == SkillData.SkillType.Signature) _equippedSignature = skill;

        Debug.Log($"[SkillTreeRuntime] Gán thành công kỹ năng thực tế: '{skill.skillName}'");
        OnSkillTreeChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  CORE MECHANIC
    // ─────────────────────────────────────────────────────────────

    private void ApplyCoreMechanicNode(SkillNodeData node)
    {
        var (handler, idx) = GetCoreMechanicHandlerAndIndex(node);
        if (handler != null && idx > 0) handler.Apply(idx);
        else Debug.Log($"[SkillTreeRuntime] CoreMechanic '{node.nodeName}': handler chưa gắn hoặc không xác định được index.");
    }

    private void RevertCoreMechanicNode(SkillNodeData node)
    {
        var (handler, idx) = GetCoreMechanicHandlerAndIndex(node);
        if (handler != null && idx > 0) handler.Revert(idx);
    }

    private (ClassCoreMechanicBase handler, int cmIndex) GetCoreMechanicHandlerAndIndex(SkillNodeData node)
    {
        if (!GetNodeInfo(node, out int classIdx, out int nodeIdx, out int tier) || tier != 4)
            return (null, -1);

        int t4Idx = nodeIdx - 10;
        if (t4Idx < 0 || t4Idx >= 15 || t4Idx % 5 != 0) return (null, -1);
        int cmIndex = t4Idx / 5 + 1; // 1, 2 or 3

        string className = CurrentDatabase?.allClassTrees[classIdx]?.className;
        return (FindCoreMechanicHandler(className), cmIndex);
    }

    // ─────────────────────────────────────────────────────────────
    //  UPGRADE SKILL
    // ─────────────────────────────────────────────────────────────

    private void ApplyUpgradeSkillNode(SkillNodeData node)
    {
        var (handler, idx) = GetUpgradeHandlerAndIndex(node);
        if (handler != null && idx > 0) handler.ApplyUpgrade(idx);
        else if (idx == 2) ApplyUniversalCooldownReduction(+1f); // N9 fallback
    }

    private void RevertUpgradeSkillNode(SkillNodeData node)
    {
        var (handler, idx) = GetUpgradeHandlerAndIndex(node);
        if (handler != null && idx > 0) handler.RevertUpgrade(idx);
        else if (idx == 2) ApplyUniversalCooldownReduction(-1f); // N9 fallback
    }

    private (ClassCoreMechanicBase handler, int upgradeIndex) GetUpgradeHandlerAndIndex(SkillNodeData node)
    {
        if (!GetNodeInfo(node, out int classIdx, out int nodeIdx, out int tier) || tier != 4)
            return (null, -1);

        int t4Idx = nodeIdx - 10;
        if (t4Idx < 0 || t4Idx >= 15 || t4Idx % 5 != 3) return (null, -1);
        int upgradeIndex = t4Idx / 5 + 1; // 1, 2 or 3

        string className = CurrentDatabase?.allClassTrees[classIdx]?.className;
        return (FindCoreMechanicHandler(className), upgradeIndex);
    }

    /// <summary>N9: −1s cooldown, chung cho mọi class (fallback nếu handler chưa gắn).</summary>
    private void ApplyUniversalCooldownReduction(float delta)
    {
        if (_allyStats != null)
            _allyStats.flatSkillCooldownReduction = Mathf.Max(0f, _allyStats.flatSkillCooldownReduction + delta);
    }

    private ClassCoreMechanicBase FindCoreMechanicHandler(string className)
    {
        if (string.IsNullOrEmpty(className)) return null;
        var handlers = GetComponents<ClassCoreMechanicBase>();
        foreach (var h in handlers)
            if (string.Equals(h.ClassName, className, System.StringComparison.OrdinalIgnoreCase))
                return h;
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    //  CLASS GRANT
    // ─────────────────────────────────────────────────────────────

    private void ApplyClassGrant(SkillNodeData node)
    {
        if (_allyStats == null || node.classGrantType == ClassGrantType.None) return;

        switch (node.classGrantType)
        {
            // T3 N1 (Passive): thêm class vào danh sách
            case ClassGrantType.GrantClass:
                if (!string.IsNullOrEmpty(node.classGrantName)
                    && !_allyStats.classes.Contains(node.classGrantName))
                {
                    _allyStats.classes.Add(node.classGrantName);
                    Debug.Log($"[SkillTree] Nhân vật thuộc Class mới: <color=cyan>{node.classGrantName}</color> → Classes: [{string.Join(", ", _allyStats.classes)}]");
                }
                break;

            // T3 N3 (Skill): khi cả 2 class đều có rồi → gộp lại
            case ClassGrantType.CombineClasses:
                if (_allyStats.classes.Count >= 2)
                {
                    string classA   = _allyStats.classes[0];
                    string classB   = _allyStats.classes[1];
                    string combined = $"{classA}+{classB}";
                    _allyStats.classes.Clear();
                    _allyStats.classes.Add(combined);
                    Debug.Log($"[SkillTree] Class kết hợp hình thành: <color=yellow>{combined}</color>");
                }
                // Nếu mới unlock T3N3 đầu tiên (classes.Count == 1) → chờ class thứ 2
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  STAT APPLICATION
    // ─────────────────────────────────────────────────────────────

    private void ApplyStatGains(SkillNodeData node)
    {
        if (_allyStats == null || node.statGains == null) return;

        foreach (var gain in node.statGains)
        {
            ApplySingleStat(gain.statType, gain.value);
        }

        _allyStats.RecalculateStats();
    }

    private void ApplySingleStat(StatFieldType type, float val)
    {
        if (_allyStats == null) return;

        // Stats nằm trong base class Stats (accessible qua _allyStats kế thừa)
        switch (type)
        {
            case StatFieldType.BaseHp:                    _allyStats.AddInitialBaseHp(val);                          break;
            case StatFieldType.BaseVIT:                   _allyStats.AddBaseAttribute(Stats.BaseAttribute.VIT, val); break;
            case StatFieldType.BaseSTR:                   _allyStats.AddBaseAttribute(Stats.BaseAttribute.STR, val); break;
            case StatFieldType.BaseINT:                   _allyStats.AddBaseAttribute(Stats.BaseAttribute.INT, val); break;
            case StatFieldType.BaseDEX:                   _allyStats.AddBaseAttribute(Stats.BaseAttribute.DEX, val); break;
            case StatFieldType.BaseAGI:                   _allyStats.AddBaseAttribute(Stats.BaseAttribute.AGI, val); break;
            case StatFieldType.Armor:                     _allyStats.armor                     += val;       break;
            case StatFieldType.MagicResist:               _allyStats.magicResist               += val;       break;
            case StatFieldType.DefenseValue:              _allyStats.defenseValue              += val;       break;
            case StatFieldType.ResistanceEffect:          _allyStats.resistanceEffect          += val;       break;
            case StatFieldType.MagicLifeSteal:            _allyStats.magicLifeSteal            += val;       break;
            // Stats trong AllyStats
            case StatFieldType.BonusCritChance:           _allyStats.bonusCritChance           += val;       break;
            case StatFieldType.BonusCritMultiplier:       _allyStats.bonusCritMultiplier       += val;       break;
            case StatFieldType.BonusHpGain:               _allyStats.bonusHpGain               += val;       break;
            case StatFieldType.BonusHp:                   _allyStats.bonusHp                   += val;       break;
            case StatFieldType.BonusAttackSpeed:          _allyStats.bonusAttackSpeed          += val;       break;
            case StatFieldType.FlatSinGain:               _allyStats.flatSinGain               += val;       break;
            case StatFieldType.BonusSinGain:              _allyStats.bonusSinGain              += val;       break;
            case StatFieldType.BonusCdr:                  _allyStats.bonusCdr                  += val;       break;
            case StatFieldType.AttributePointRemaining:   _allyStats.attributePointRemain      += (int)val;  break;
            // Special Stats (Skill Tree)
            case StatFieldType.DefenseValueIgnore:        _allyStats.defenseValueIgnore        += val;       break;
            case StatFieldType.StealthReduction:          _allyStats.stealthReduction          += val;       break;
            case StatFieldType.SignatureDamageBonus:      _allyStats.signatureDamageBonus      += val;       break;
            case StatFieldType.ProjectileSpeedBonus:      _allyStats.projectileSpeedBonus      += val;       break;
            case StatFieldType.StaminaReduction:          _allyStats.staminaReduction          += val;       break;
            case StatFieldType.ParryWindow:               _allyStats.parryWindow               += val;       break;
            case StatFieldType.DirectionBonusBackstab:    _allyStats.directionBonusBackstab    += val;       break;
            case StatFieldType.DashSpeed:                 _allyStats.dashSpeed                 += val;       break;
            case StatFieldType.CompanionDamageOutputMult: _allyStats.companionDamageOutputMult += val;       break;
            case StatFieldType.CompanionBonusHp:          _allyStats.companionBonusHp          += val;       break;
            case StatFieldType.CompanionBonusCdr:         _allyStats.companionBonusCdr         += val;       break;
            default:
                Debug.LogWarning($"[SkillTreeRuntime] StatFieldType '{type}' chưa map. Thêm case vào ApplySingleStat()."); break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  EQUIP (Skill / Signature nodes)
    // ─────────────────────────────────────────────────────────────

    public bool EquipSkillFromTree(SkillData skill)
    {
        if (skill == null) return false;
        if (!IsUnlocked(skill.id))
        {
            Debug.LogWarning($"[SkillTreeRuntime] Không thể equip '{skill.skillName}' — chưa unlock!");
            return false;
        }

        if (_skillManager != null)
        {
            bool ok = _skillManager.EquipSkill(skill);
            if (!ok)
            {
                Debug.LogWarning($"[SkillTreeRuntime] Equip '{skill.skillName}' thất bại (thiếu effect code) — không ghi equipped.");
                OnSkillTreeChanged?.Invoke();
                return false;
            }

            if (skill.skillType == SkillData.SkillType.Skill)          _equippedSkill = skill;
            else if (skill.skillType == SkillData.SkillType.Signature) _equippedSignature = skill;

            OnSkillTreeChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void UnequipSkillFromTree(SkillData.SkillType slotType)
    {
        if (_skillManager == null) return;

        if (slotType == SkillData.SkillType.Skill)
        {
            _skillManager.UnequipSkillSlot();
            _equippedSkill = null;
        }
        else if (slotType == SkillData.SkillType.Signature)
        {
            _skillManager.UnequipSignatureSlot();
            _equippedSignature = null;
        }

        OnSkillTreeChanged?.Invoke();
    }

    public bool IsEquipped(SkillData skill)
    {
        if (skill == null) return false;
        return skill == _equippedSkill || skill == _equippedSignature;
    }

    // ─────────────────────────────────────────────────────────────
    //  REFUND
    // ─────────────────────────────────────────────────────────────

    public void RefundAllWithDatabase(SkillTreeDatabase database)
    {
        if (_playerStats == null || database == null) return;

        var allNodes = CollectAllNodes(database);
        int totalRefund = 0;

        // Revert từng effect theo thứ tự ngược (CoreMechanic → Skill → Stat)
        foreach (var node in allNodes)
        {
            if (string.IsNullOrEmpty(node.nodeId) || !IsUnlocked(node.nodeId)) continue;
            totalRefund += node.unlockCost;

            switch (node.nodeType)
            {
                case SkillNodeType.Stat:
                    foreach (var gain in node.statGains)
                        ApplySingleStat(gain.statType, -gain.value);
                    break;
                case SkillNodeType.CoreMechanic:
                    RevertCoreMechanicNode(node);
                    break;
                case SkillNodeType.UpgradeSkill:
                    RevertUpgradeSkillNode(node);
                    break;
            }
        }

        _allyStats?.RecalculateStats();

        _skillManager?.UnequipDefaultPassive();
        _skillManager?.UnequipPassive1();
        _skillManager?.UnequipPassive2();
        if (_equippedSkill    != null) { _skillManager?.UnequipSkillSlot();      _equippedSkill    = null; }
        if (_equippedSignature != null) { _skillManager?.UnequipSignatureSlot(); _equippedSignature = null; }

        // Reset class list (class grant nodes đã được refund)
        if (_allyStats != null) _allyStats.classes.Clear();

        _unlockedNodes.Clear();
        _playerStats.skillPointRemain += totalRefund;

        ClearSavedNodes();
        PlayerPrefs.SetInt(SPKey, _playerStats.skillPointRemain);
        PlayerPrefs.Save();

        Debug.Log($"[SkillTreeRuntime] REFUND ALL: Trả lại {totalRefund} SP. Tổng còn: {_playerStats.skillPointRemain}");
        OnSkillTreeChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  SAVE / LOAD
    // ─────────────────────────────────────────────────────────────

    private string SaveKey => $"SkillTree_{(_playerStats != null ? _playerStats.characterId : "Chris")}";
    private string SPKey   => $"SP_{(_playerStats != null ? _playerStats.characterId : "Chris")}";

    private void SaveUnlockedNodes()
    {
        PlayerPrefs.SetString(SaveKey, string.Join(",", _unlockedNodes.Keys));
        if (_playerStats != null) PlayerPrefs.SetInt(SPKey, _playerStats.skillPointRemain);
        PlayerPrefs.Save();
    }

    private void ClearSavedNodes()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        // Không xóa SPKey — sau refund SP tăng lên, SaveSP() sẽ lưu lại giá trị mới
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load danh sách đã unlock từ PlayerPrefs rồi re-apply toàn bộ hiệu ứng.
    /// Gọi từ SkillTreeController.Start() sau khi database sẵn sàng.
    /// </summary>
    public void LoadAndReapply(SkillTreeDatabase database)
    {
        if (database == null || _playerStats == null) return;

        string saved = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            var savedIds = new System.Collections.Generic.HashSet<string>(saved.Split(','));
            var allNodes = CollectAllNodes(database);

            foreach (var node in allNodes)
            {
                if (string.IsNullOrEmpty(node.nodeId) || !savedIds.Contains(node.nodeId)) continue;
                if (_unlockedNodes.ContainsKey(node.nodeId)) continue;

                _unlockedNodes[node.nodeId] = 1;

                switch (node.nodeType)
                {
                    case SkillNodeType.Stat:
                        ApplyStatGains(node);
                        break;
                    case SkillNodeType.Skill:
                        if (node.skillData != null)
                        {
                            SkillData actual = GetActualSkillToEquip(node);
                            if (_skillManager.EquipSkill(actual))
                            {
                                if (actual.skillType == SkillData.SkillType.Skill) _equippedSkill = actual;
                                else if (actual.skillType == SkillData.SkillType.Signature) _equippedSignature = actual;
                            }
                            else
                            {
                                Debug.LogWarning($"[SkillTreeRuntime] LoadAndReapply: equip '{actual.skillName}' thất bại (thiếu effect code).");
                            }
                        }
                        break;
                    case SkillNodeType.CoreMechanic:
                        ApplyCoreMechanicNode(node);
                        break;
                    case SkillNodeType.UpgradeSkill:
                        ApplyUpgradeSkillNode(node);
                        break;
                }
                ApplyClassGrant(node);
            }
        }

        if (PlayerPrefs.HasKey(SPKey)) _playerStats.skillPointRemain = PlayerPrefs.GetInt(SPKey);
        else
        {
            _playerStats.skillPointRemain = _playerStats.level;
            PlayerPrefs.SetInt(SPKey, _playerStats.skillPointRemain);
            PlayerPrefs.Save();
        }

        _allyStats?.RecalculateStats();
        OnSkillTreeChanged?.Invoke();
    }

    private System.Collections.Generic.List<SkillNodeData> CollectAllNodes(SkillTreeDatabase database)
    {
        var result = new System.Collections.Generic.List<SkillNodeData>();
        if (database.commonTree != null) result.AddRange(database.commonTree.skillNodes);
        foreach (var tree in database.allClassTrees) if (tree != null) result.AddRange(tree.skillNodes);
        foreach (var fusion in database.fusionSkills) if (fusion?.fusionNodes != null) result.AddRange(fusion.fusionNodes);
        return result;
    }

    // ─────────────────────────────────────────────────────────────
    //  CLEAR ALL (dùng khi Reset Game)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Xóa toàn bộ tiến trình skill tree + SP khỏi PlayerPrefs cho tất cả character.
    /// Gọi từ DevToolPanel.CMD_ResetGame() trước khi reload scene.
    /// </summary>
    public static void ClearAllSavedProgress(params string[] characterIds)
    {
        foreach (string id in characterIds)
        {
            PlayerPrefs.DeleteKey($"SkillTree_{id}");
            PlayerPrefs.DeleteKey($"SP_{id}");
        }
        PlayerPrefs.Save();
        Debug.Log($"[SkillTreeRuntime] ClearAllSavedProgress: đã xóa tiến trình của {string.Join(", ", characterIds)}.");
    }

    // ─────────────────────────────────────────────────────────────
    //  SYNC
    // ─────────────────────────────────────────────────────────────

    public void SyncFromSkillManager()
    {
        if (_skillManager == null) return;
        _equippedSkill      = _skillManager.currentSkill;
        _equippedSignature  = _skillManager.currentSignature;
    }

    // ─────────────────────────────────────────────────────────────
    //  PROPERTIES
    // ─────────────────────────────────────────────────────────────

    public SkillData EquippedSkill      => _equippedSkill;
    public SkillData EquippedSignature  => _equippedSignature;
    public int SkillPointRemain         => _playerStats != null ? _playerStats.skillPointRemain : 0;
    // Sửa lại property CurrentClassName để hiển thị chuẩn tên Class Kết Hợp:
    public string CurrentClassName
    {
        get
        {
            var selectedT3 = GetSelectedClassesByTier(3);
            if (selectedT3.Count >= 2 && CurrentDatabase != null)
            {
                string classA = CurrentDatabase.allClassTrees[selectedT3[0]].className;
                string classB = CurrentDatabase.allClassTrees[selectedT3[1]].className;
                return GetFusionClassName(classA, classB).ToUpper();
            }
            if (selectedT3.Count == 1 && CurrentDatabase != null)
            {
                return CurrentDatabase.allClassTrees[selectedT3[0]].className.ToUpper();
            }
            return "SKILL TREE";
        }
    }
    public string GetClassByIndex(int i) => (_allyStats != null && _allyStats.classes.Count > i) ? _allyStats.classes[i] : "";
}
