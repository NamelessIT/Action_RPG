using UnityEngine;

/// <summary>
/// SLOT 2 — MATRIX (Ma Trận): quyết định AI phòng thủ + aggro + độ cứng.
/// Lưu ý: Không có Sub-stats.
/// </summary>
[CreateAssetMenu(fileName = "CompanionMatrix", menuName = "Companion/Matrix Data")]
public class CompanionMatrixData : CompanionModuleData
{
    [Header("Tên hiển thị")]
    public string matrixName;

    [Header("Logic AI")]
    public CompanionMatrixType matrixType = CompanionMatrixType.Regen;

    [Header("Chỉ số Def")]
    public float armor = 0f;
    public float magicResist = 0f;
    public float flatHp = 0f;
    public float bonusHp = 0f;          // % máu tối đa cộng thêm
}
