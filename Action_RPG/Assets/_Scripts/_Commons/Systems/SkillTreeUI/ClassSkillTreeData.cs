using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject chứa danh sách skill của 1 class (VD: Warrior, Rogue, Mage...).
/// Mỗi class có 1 file ClassSkillTreeData riêng.
/// Designer tạo trong Unity: Right-click > Create > SkillTree/Class Skill Tree Data.
/// </summary>
[CreateAssetMenu(fileName = "New ClassSkillTree", menuName = "SkillTree/Class Skill Tree Data")]
public class ClassSkillTreeData : ScriptableObject
{
    [Header("--- Class Info ---")]
    [Tooltip("Tên class hiển thị (VD: Warrior, Rogue, Mage...)")]
    public string className;

    [Tooltip("Icon đại diện cho class (hiển thị trên tab)")]
    public Sprite classIcon;

    [Header("--- Skill Nodes ---")]
    [Tooltip("Danh sách tất cả skill node trong cây kỹ năng của class này")]
    public List<SkillNodeData> skillNodes = new List<SkillNodeData>();
}

/// <summary>
/// Dữ liệu 1 node skill trong Skill Tree.
/// Mỗi node tương ứng 1 skill có thể unlock/upgrade.
/// </summary>
[System.Serializable]
public class SkillNodeData
{
    [Header("--- Skill Reference ---")]
    [Tooltip("SkillData ScriptableObject tương ứng (Từ hệ thống SkillManager)")]
    public SkillData skillData;

    [Header("--- Unlock Requirements ---")]
    [Tooltip("Skill Point cần để UNLOCK skill này lần đầu")]
    public int unlockCost = 1;

    [Tooltip("Level tối thiểu để unlock")]
    public int requiredLevel = 1;

    [Tooltip("Các skill phải unlock trước (prerequisite). Để trống nếu không cần.")]
    public List<SkillData> prerequisites = new List<SkillData>();

    [Header("--- Upgrade ---")]
    [Tooltip("Số lần upgrade tối đa (VD: 3 lần, mỗi lần tăng stat)")]
    public int maxUpgradeLevel = 3;

    [Tooltip("Skill Point cần cho mỗi lần upgrade (sau lần unlock đầu tiên)")]
    public int upgradeCost = 1;

    [Header("--- UI Position ---")]
    [Tooltip("Vị trí node trên giao diện Skill Tree (Anchor-based, designer kéo thả)")]
    public Vector2 uiPosition;
}
