using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Database tổng chứa tất cả Class Skill Trees.
/// Gắn 1 instance duy nhất trên scene (hoặc làm ScriptableObject singleton).
/// SkillTreeController dùng file này để tra cứu skill tree theo className.
/// </summary>
[CreateAssetMenu(fileName = "SkillTreeDatabase", menuName = "SkillTree/Skill Tree Database")]
public class SkillTreeDatabase : ScriptableObject
{
    [Header("--- All Class Skill Trees ---")]
    [Tooltip("Kéo tất cả ClassSkillTreeData vào đây (1 class = 1 entry)")]
    public List<ClassSkillTreeData> allClassTrees = new List<ClassSkillTreeData>();

    [Header("--- Fusion Skills (Dual-Class) ---")]
    [Tooltip("Danh sách fusion skill khi player có 2 class. Mở rộng sau.")]
    public List<FusionSkillEntry> fusionSkills = new List<FusionSkillEntry>();

    /// <summary>Tìm skill tree theo tên class.</summary>
    public ClassSkillTreeData GetTreeByClassName(string className)
    {
        if (string.IsNullOrEmpty(className)) return null;

        foreach (var tree in allClassTrees)
        {
            if (tree != null && tree.className == className)
                return tree;
        }

        Debug.LogWarning($"[SkillTreeDatabase] Không tìm thấy skill tree cho class '{className}'");
        return null;
    }
}

/// <summary>
/// Entry cho Fusion Skill (khi player có 2 class).
/// VD: Warrior + Mage = Spellblade fusion skill.
/// </summary>
[System.Serializable]
public class FusionSkillEntry
{
    [Tooltip("Tên class thứ nhất")]
    public string classA;

    [Tooltip("Tên class thứ hai")]
    public string classB;

    [Tooltip("Danh sách skill fusion mở khóa khi có cả 2 class")]
    public List<SkillNodeData> fusionNodes = new List<SkillNodeData>();
}
