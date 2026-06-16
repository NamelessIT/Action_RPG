using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SLOT 3 — SYNC CORE (Lõi Đồng Điệu): hoạt động tương đồng AccessoryData của Player,
/// nhưng Companion chỉ có 1 slot. Cung cấp Sub-stats + hiệu ứng "Kết hợp" (Rarity 3+).
/// </summary>
[CreateAssetMenu(fileName = "CompanionSyncCore", menuName = "Companion/Sync Core Data")]
public class CompanionSyncCoreData : CompanionModuleData
{
    [Header("Tên hiển thị")]
    public string syncCoreName;

    [Header("Chỉ số Phụ (Sub-stats)")]
    public List<StatModifier> statModifiers = new List<StatModifier>();
}
