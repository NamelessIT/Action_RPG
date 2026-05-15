using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Runtime state: theo dõi skill nào đã unlock, upgrade bao nhiêu lần.
/// Gắn trên Player GameObject (cùng chỗ với SkillManager, PlayerStats).
/// Lưu/Load được.
/// </summary>
public class SkillTreeRuntime : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  EVENTS
    // ─────────────────────────────────────────────────────────────
    public event Action OnSkillTreeChanged;

    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────

    // Key = SkillData.id, Value = upgrade level đã đạt (0 = chưa unlock, 1 = unlocked, 2+ = upgraded)
    private Dictionary<string, int> _unlockedSkills = new Dictionary<string, int>();

    // Skill đang được EQUIP (tối đa 1 Skill + 1 Signature)
    // Lưu ý: SkillManager đã quản lý equip, đây chỉ mirror state cho UI
    private SkillData _equippedSkill;
    private SkillData _equippedSignature;

    // References
    private PlayerStats _playerStats;
    private SkillManager _skillManager;

    // ─────────────────────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _playerStats = GetComponent<PlayerStats>();
        _skillManager = GetComponent<SkillManager>();

        if (_playerStats == null)
            Debug.LogError("[SkillTreeRuntime] Không tìm thấy PlayerStats trên " + gameObject.name);
        if (_skillManager == null)
            Debug.LogError("[SkillTreeRuntime] Không tìm thấy SkillManager trên " + gameObject.name);
    }

    // ─────────────────────────────────────────────────────────────
    //  QUERIES
    // ─────────────────────────────────────────────────────────────

    /// <summary>Lấy upgrade level hiện tại (0 = chưa unlock).</summary>
    public int GetUpgradeLevel(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return 0;
        return _unlockedSkills.TryGetValue(skillId, out int level) ? level : 0;
    }

    /// <summary>Skill đã được unlock chưa?</summary>
    public bool IsUnlocked(string skillId)
    {
        return GetUpgradeLevel(skillId) > 0;
    }

    /// <summary>Kiểm tra có đủ điều kiện unlock/upgrade node này không.</summary>
    public bool CanUnlockOrUpgrade(SkillNodeData node)
    {
        if (node == null || node.skillData == null) return false;
        if (_playerStats == null) return false;

        string id = node.skillData.id;
        int currentLevel = GetUpgradeLevel(id);

        // Đã max upgrade rồi
        if (currentLevel >= node.maxUpgradeLevel) return false;

        // Check level requirement
        if (_playerStats.level < node.requiredLevel) return false;

        // Check prerequisites
        if (node.prerequisites != null)
        {
            foreach (var prereq in node.prerequisites)
            {
                if (prereq != null && !IsUnlocked(prereq.id))
                    return false;
            }
        }

        // Check skill point
        int cost = (currentLevel == 0) ? node.unlockCost : node.upgradeCost;
        if (_playerStats.skillPointRemain < cost) return false;

        return true;
    }

    /// <summary>Lấy chi phí tiếp theo (unlock hoặc upgrade).</summary>
    public int GetNextCost(SkillNodeData node)
    {
        if (node == null || node.skillData == null) return 0;
        int currentLevel = GetUpgradeLevel(node.skillData.id);
        return (currentLevel == 0) ? node.unlockCost : node.upgradeCost;
    }

    // ─────────────────────────────────────────────────────────────
    //  ACTIONS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Unlock hoặc Upgrade 1 skill node.
    /// Trả về true nếu thành công.
    /// </summary>
    public bool UnlockOrUpgrade(SkillNodeData node)
    {
        if (!CanUnlockOrUpgrade(node)) return false;

        string id = node.skillData.id;
        int currentLevel = GetUpgradeLevel(id);
        int cost = (currentLevel == 0) ? node.unlockCost : node.upgradeCost;

        // Trừ skill point
        _playerStats.skillPointRemain -= cost;

        // Tăng level
        _unlockedSkills[id] = currentLevel + 1;

        Debug.Log($"[SkillTreeRuntime] {node.skillData.skillName} " +
                  $"{(currentLevel == 0 ? "UNLOCKED" : $"UPGRADED lv{currentLevel + 1}")} " +
                  $"(Cost: {cost} SP, Remain: {_playerStats.skillPointRemain})");

        OnSkillTreeChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Equip 1 skill vào slot tương ứng (Skill hoặc Signature).
    /// Chỉ equip được skill đã unlock.
    /// Trả về true nếu thành công.
    /// </summary>
    public bool EquipSkillFromTree(SkillData skill)
    {
        if (skill == null) return false;
        if (!IsUnlocked(skill.id))
        {
            Debug.LogWarning($"[SkillTreeRuntime] Không thể equip '{skill.skillName}' — chưa unlock!");
            return false;
        }

        // Gọi SkillManager để equip (nó tự xử lý unequip cái cũ)
        if (_skillManager != null)
        {
            _skillManager.EquipSkill(skill);

            // Cập nhật mirror state
            if (skill.skillType == SkillData.SkillType.Skill)
                _equippedSkill = skill;
            else if (skill.skillType == SkillData.SkillType.Signature)
                _equippedSignature = skill;

            OnSkillTreeChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unequip skill khỏi slot.
    /// </summary>
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

    /// <summary>Skill này có đang được equip không?</summary>
    public bool IsEquipped(SkillData skill)
    {
        if (skill == null) return false;
        return skill == _equippedSkill || skill == _equippedSignature;
    }

    // ─────────────────────────────────────────────────────────────
    //  CLASS CHANGE — REFUND
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Refund tất cả skill point khi đổi class.
    /// Unequip tất cả skill, reset unlock state, trả lại SP.
    /// </summary>
    public void RefundAllSkillPoints()
    {
        if (_playerStats == null) return;

        int totalRefund = 0;

        // Tính tổng SP đã chi
        // Cần biết node data để tính chính xác → duyệt qua database
        // Cách đơn giản: mỗi unlock = unlockCost, mỗi upgrade = upgradeCost
        // Nhưng ta không lưu node data → dùng cách đếm tổng level * 1 (giả sử mỗi lần = 1 SP)
        // => Cần tham chiếu database để tính chính xác

        foreach (var kvp in _unlockedSkills)
        {
            // Mỗi level = 1 skill point (cách đơn giản)
            totalRefund += kvp.Value;
        }

        // Unequip skill đang trang bị
        if (_equippedSkill != null)
        {
            _skillManager?.UnequipSkillSlot();
            _equippedSkill = null;
        }
        if (_equippedSignature != null)
        {
            _skillManager?.UnequipSignatureSlot();
            _equippedSignature = null;
        }

        // Clear toàn bộ unlock state
        _unlockedSkills.Clear();

        // Trả lại SP
        _playerStats.skillPointRemain += totalRefund;

        Debug.Log($"[SkillTreeRuntime] REFUND: Trả lại {totalRefund} Skill Points " +
                  $"(Total: {_playerStats.skillPointRemain})");

        OnSkillTreeChanged?.Invoke();
    }

    /// <summary>
    /// Refund chính xác hơn bằng cách dùng database (dùng khi có ClassSkillTreeData).
    /// </summary>
    public void RefundAllWithDatabase(SkillTreeDatabase database)
    {
        if (_playerStats == null || database == null) return;

        int totalRefund = 0;

        foreach (var tree in database.allClassTrees)
        {
            foreach (var node in tree.skillNodes)
            {
                if (node.skillData == null) continue;
                int level = GetUpgradeLevel(node.skillData.id);
                if (level <= 0) continue;

                // Tính cost: lần đầu = unlockCost, các lần sau = upgradeCost
                totalRefund += node.unlockCost;
                totalRefund += (level - 1) * node.upgradeCost;
            }
        }

        // Unequip
        if (_equippedSkill != null)
        {
            _skillManager?.UnequipSkillSlot();
            _equippedSkill = null;
        }
        if (_equippedSignature != null)
        {
            _skillManager?.UnequipSignatureSlot();
            _equippedSignature = null;
        }

        _unlockedSkills.Clear();
        _playerStats.skillPointRemain += totalRefund;

        Debug.Log($"[SkillTreeRuntime] REFUND (Database): Trả lại {totalRefund} SP " +
                  $"(Total: {_playerStats.skillPointRemain})");

        OnSkillTreeChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  SYNC WITH SKILLMANAGER (Mirror state khi load game)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Đồng bộ equip state từ SkillManager (gọi sau khi load save).</summary>
    public void SyncFromSkillManager()
    {
        if (_skillManager == null) return;
        _equippedSkill = _skillManager.currentSkill;
        _equippedSignature = _skillManager.currentSignature;
    }

    // ─────────────────────────────────────────────────────────────
    //  PROPERTIES
    // ─────────────────────────────────────────────────────────────

    public SkillData EquippedSkill => _equippedSkill;
    public SkillData EquippedSignature => _equippedSignature;
    public int SkillPointRemain => _playerStats != null ? _playerStats.skillPointRemain : 0;
    public string CurrentClassName => _playerStats is AllyStats ally ? ally.className : "";
}
