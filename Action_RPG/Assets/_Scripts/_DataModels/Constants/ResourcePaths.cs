/// <summary>
/// Đường dẫn trong Assets/Resources/ dùng chung. Mọi Resources.Load/LoadAll phải đi qua đây.
///
/// Lý do gom: đường dẫn Resources là chuỗi thuần — viết sai KHÔNG lỗi compile,
/// LoadAll chỉ trả về mảng rỗng nên hỏng im lặng lúc chạy. Riêng "Datas/Core Shields"
/// có dấu cách nên càng dễ sai.
///
/// Đổi tên thư mục trong Assets/Resources/ thì sửa đúng một chỗ ở đây.
/// </summary>
public static class ResourcePaths
{
    /// <summary>Thư mục chứa toàn bộ WeaponData (chia theo loại vũ khí / Tier).</summary>
    public const string Weapons = "Datas/Weapons";

    /// <summary>Thư mục chứa CoreShieldData. Chú ý: có dấu cách trong tên thư mục.</summary>
    public const string CoreShields = "Datas/Core Shields";

    /// <summary>Thư mục chứa AccessoryData.</summary>
    public const string Accessories = "Datas/Accessories";

    /// <summary>Thư mục chứa SkillData.</summary>
    public const string Skills = "Datas/Skills";

    /// <summary>Thư mục chứa cả 3 loại module companion (Protocol / Matrix / SyncCore).</summary>
    public const string CompanionModules = "Datas/CompanionModules";

    /// <summary>Vũ khí khởi đầu mà EquipmentManager nạp khi player chưa có gì.</summary>
    public const string DefaultBaseWeapon = "Datas/Weapons/Hand/Tier 1/WPN_H_T1_01";
}
