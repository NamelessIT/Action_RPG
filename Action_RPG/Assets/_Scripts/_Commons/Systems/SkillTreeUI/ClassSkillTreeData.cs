using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New ClassSkillTree", menuName = "SkillTree/Class Skill Tree Data")]
public class ClassSkillTreeData : ScriptableObject
{
    [Header("--- Class Info ---")]
    public string className;
    public Sprite classIcon;

    [Header("--- Skill Nodes ---")]
    public List<SkillNodeData> skillNodes = new List<SkillNodeData>();
}

// ──────────────────────────────────────────────────────────
//  ENUMS
// ──────────────────────────────────────────────────────────

public enum SkillNodeType
{
    Stat,           // Tăng chỉ số thuần (baseVIT, armor, ...)
    Skill,          // Học/upgrade một SkillData
    CoreMechanic    // Mở khóa cơ chế mới (không có stat gain, không có SkillData)
}

/// <summary>
/// Xác định node này có cập nhật danh sách class của player không.
/// T3 N1 (Passive): GrantClass → thêm class vào AllyStats.classes
/// T3 N3 (Skill):   CombineClasses → khi cả 2 T3N3 đều unlock thì gộp class
/// </summary>
public enum ClassGrantType
{
    None,           // Node bình thường, không ảnh hưởng className
    GrantClass,     // Unlock node này → thêm classGrantName vào classes (T3 N1)
    CombineClasses  // Unlock node này + 2 class đã có → gộp thành "ClassA+ClassB" (T3 N3)
}

// Mỗi entry trong statGains ánh xạ sang 1 field của AllyStats.
// Thêm entry mới vào enum này khi cần dùng stat khác.
public enum StatFieldType
{
    None,
    BaseHp,
    BaseVIT,
    BaseSTR,
    BaseINT,
    BaseDEX,
    BaseAGI,
    BonusCritChance,            // %
    BonusCritMultiplier,        // %
    Armor,
    MagicResist,
    DefenseValue,
    ResistanceEffect,           // %
    BonusHpGain,                // %
    BonusHp,                    // %
    BonusAttackSpeed,           // %
    StaminaReduction,           // % giảm stamina tiêu hao
    DefenseValueIgnore,         // % bỏ qua Defense Value của địch
    StealthReduction,           // % giảm tầm bị phát hiện
    SignatureDamageBonus,       // % tăng sát thương Signature
    ProjectileSpeedBonus,       // % tăng tốc độ bay đạn
    MagicLifeSteal,             // %
    FlatSinGain,                // +flat sin per attack
    BonusSinGain,               // %
    BonusCdr,                   // %
    AttributePointRemaining,    // Điểm tự chọn
    ParryWindow,                // giây (extend parry window)
    DirectionBonusBackstab,     // % tăng backstab direction bonus
    DashSpeed,                  // %
    CompanionDamageOutputMult,  // %
    CompanionBonusHp,           // %
    CompanionBonusCdr,          // %
}

// ──────────────────────────────────────────────────────────
//  DATA STRUCTS
// ──────────────────────────────────────────────────────────

[System.Serializable]
public class StatGainEntry
{
    [Tooltip("Loại chỉ số cần tăng")]
    public StatFieldType statType = StatFieldType.None;

    [Tooltip("Giá trị tăng (flat hoặc %). Xem comment StatFieldType để biết đơn vị.")]
    public float value;
}

[System.Serializable]
public class SkillNodeData
{
    [Header("--- Node Identity ---")]
    [Tooltip("ID duy nhất của node (VD: 'Warrior_T2_N1'). Dùng để track unlock và prerequisite.")]
    public string nodeId;

    [Tooltip("Tên hiển thị (VD: '+4 baseSTR', 'Học WarriorSkill', 'Core: Heavy Armor')")]
    public string nodeName;

    [TextArea(2, 5)]
    [Tooltip("Mô tả chi tiết hiển thị trong Detail Panel khi hover node.")]
    public string nodeDescription;

    public SkillNodeType nodeType = SkillNodeType.Stat;

    // ── Skill Node ───────────────────────────────────────
    [Header("--- Skill Reference (nodeType = Skill) ---")]
    [Tooltip("SkillData tương ứng. Bắt buộc khi nodeType = Skill.")]
    public SkillData skillData;

    // ── Stat Node ────────────────────────────────────────
    [Header("--- Stat Gains (nodeType = Stat) ---")]
    [Tooltip("Danh sách chỉ số tăng khi unlock node này.")]
    public List<StatGainEntry> statGains = new List<StatGainEntry>();

    // ── Class Grant ──────────────────────────────────────
    [Header("--- Class Grant ---")]
    [Tooltip("None = bình thường.\nGrantClass = T3 N1 (Passive) → thêm classGrantName vào classes.\nCombineClasses = T3 N3 (Skill) → khi cả 2 T3N3 đều unlock sẽ gộp class.")]
    public ClassGrantType classGrantType = ClassGrantType.None;
    [Tooltip("Tên class cấp cho player khi classGrantType = GrantClass. VD: 'Warrior', 'Rogue'...")]
    public string classGrantName = "";

    // ── Unlock Requirements ──────────────────────────────
    [Header("--- Unlock Requirements ---")]
    [Tooltip("Skill Point cần để unlock (thường = 1)")]
    public int unlockCost = 1;

    [Tooltip("Level tối thiểu để unlock node này")]
    public int requiredLevel = 1;

    [Tooltip("Danh sách nodeId phải unlock trước. Để trống nếu node đầu tầng.")]
    public List<string> prerequisiteNodeIds = new List<string>();

    // ── UI Position ──────────────────────────────────────
    [Header("--- UI ---")]
    [Tooltip("Vị trí node trên canvas (anchoredPosition từ NodeContainer)")]
    public Vector2 uiPosition;
}
