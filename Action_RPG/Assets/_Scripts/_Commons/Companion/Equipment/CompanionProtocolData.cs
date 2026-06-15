using UnityEngine;

/// <summary>
/// SLOT 1 — PROTOCOL (Giao Thức): quyết định AI tấn công + chỉ số sát thương.
/// Lưu ý: Không có Sub-stats. baseAtk → flatPhysicalAtk hoặc flatMagicAtk theo atkType.
/// </summary>
[CreateAssetMenu(fileName = "CompanionProtocol", menuName = "Companion/Protocol Data")]
public class CompanionProtocolData : CompanionModuleData
{
    [Header("Tên hiển thị")]
    public string protocolName;

    [Header("Logic AI")]
    public CompanionProtocolType protocolType = CompanionProtocolType.Carnage;
    public CompanionAtkType atkType = CompanionAtkType.Physical;

    [Header("Chỉ số Combat")]
    public float baseAtk = 5f;          // → flatPhysicalAtk hoặc flatMagicAtk theo atkType
    public float defenseValue = 0f;
    public float bonusCritChance = 0f;
    public float baseAttackSpeed = 1f;
    public float attackRange = 2f;
    public bool isRange = false;        // true = tạo Projectile (Artillery/Suppression)
}
