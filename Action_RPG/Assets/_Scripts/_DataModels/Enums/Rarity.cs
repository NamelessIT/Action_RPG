/// <summary>
/// Bậc hiếm dùng chung cho mọi item trang bị (Weapon / CoreShield / Accessory).
/// Thứ tự quyết định tier 0-4 dùng ở <see cref="RarityColors"/> và InventoryItemRecord.RarityTier.
/// Không đổi thứ tự: giá trị đã được serialize theo int trong các asset ScriptableObject.
/// </summary>
public enum Rarity
{
    Residual_1,
    Stained_2,
    Corrupted_3,
    Condemned_4,
    Anomalous_5
}
