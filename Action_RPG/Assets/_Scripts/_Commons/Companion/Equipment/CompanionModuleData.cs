using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  COMPANION EQUIPMENT — ENUM + BASE DATA  [theo GDD]
//
//  Companion KHÔNG dùng Weapon/CoreShield/Accessory của Player. Hệ riêng "Lõi Cơ Khí"
//  gồm 3 slot độc lập (mỗi loại data nằm ở file riêng trùng tên class):
//    SLOT 1 — CompanionProtocolData  (Giao Thức): AI TẤN CÔNG + chỉ số sát thương.
//    SLOT 2 — CompanionMatrixData    (Ma Trận):   AI PHÒNG THỦ + aggro + độ cứng.
//    SLOT 3 — CompanionSyncCoreData  (Lõi Đồng Điệu): giống Accessory nhưng CHỈ 1 slot.
//
//  Rarity 1-2: chỉ chỉ số gốc. Rarity 3-4: mở khoá Effect (Passive/Trigger).
//  Rarity 5: Effect đột biến. (Logic Effect xử lý ở 3 EffectManager riêng.)
// ─────────────────────────────────────────────────────────────────────────────

public enum CompanionRarity { Residual_1, Stained_2, Corrupted_3, Condemned_4, Anomalous_5 }

/// <summary>Loại hiệu ứng của module (chỉ Rarity 3+ mới có).</summary>
public enum CompanionEffectType { None, Passive, Trigger }

/// <summary>Slot 1 — kiểu AI tấn công.</summary>
public enum CompanionProtocolType { Artillery, Carnage, Suppression, Aegis }

/// <summary>Loại sát thương của Protocol.</summary>
public enum CompanionAtkType { Physical, Magic }

/// <summary>Slot 2 — kiểu AI phòng thủ / aggro.</summary>
public enum CompanionMatrixType { Regen, Phantoms, Deflector }

/// <summary>Base chung cho cả 3 slot (Định danh + Effect Rarity 3+).</summary>
public abstract class CompanionModuleData : ScriptableObject
{
    [Header("Định danh")]
    public string id;                  // VD: PRT_ART_T3_01
    [TextArea] public string description;
    public Sprite icon;
    public CompanionRarity rarity = CompanionRarity.Residual_1;

    [Header("Hiệu Ứng (Rarity 3+)")]
    public CompanionEffectType effectType = CompanionEffectType.None;
    [TextArea] public string effectCondition;
    [TextArea] public string effectDescription;

    /// <summary>Module Rarity 3 trở lên (mới có Effect).</summary>
    public bool HasEffect => effectType != CompanionEffectType.None
        && (rarity == CompanionRarity.Corrupted_3
            || rarity == CompanionRarity.Condemned_4
            || rarity == CompanionRarity.Anomalous_5);
}
