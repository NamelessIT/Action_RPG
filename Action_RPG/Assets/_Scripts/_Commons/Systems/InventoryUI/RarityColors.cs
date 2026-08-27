using UnityEngine;

/// <summary>
/// Bảng màu rarity — NGUỒN DUY NHẤT cho mọi nơi hiển thị bậc item:
/// viền slot inventory, tooltip, item rơi ngoài thế giới, module companion...
/// Không tự viết lại bảng màu ở chỗ khác; luôn gọi vào đây.
///
/// 5 bậc khớp enum <see cref="Rarity"/> dùng chung
/// (Weapon / CoreShield / Accessory / CompanionModule):
///   0 Residual · 1 Stained · 2 Corrupted · 3 Condemned · 4 Anomalous
/// </summary>
public static class RarityColors
{
    // Xám → Lục → Lam → Tím → Cam (chuẩn action-RPG)
    private static readonly Color[] _colors =
    {
        new Color(0.62f, 0.62f, 0.62f), // 0 Residual  — xám
        new Color(0.30f, 0.80f, 0.30f), // 1 Stained   — lục
        new Color(0.30f, 0.60f, 1.00f), // 2 Corrupted — lam
        new Color(0.70f, 0.35f, 1.00f), // 3 Condemned — tím
        new Color(1.00f, 0.55f, 0.10f), // 4 Anomalous — cam
    };

    /// <summary>Màu theo bậc rarity (0-4). tier ngoài khoảng → màu trống (clear).</summary>
    public static Color Get(int tier)
    {
        if (tier < 0 || tier >= _colors.Length) return Color.clear;
        return _colors[tier];
    }

    /// <summary>Màu theo enum — bản an toàn kiểu, khỏi phải tự ép (int).</summary>
    public static Color Get(Rarity rarity)
    {
        return Get((int)rarity);
    }

    /// <summary>Mã hex (không có #) cho rich-text TMP, vd $"&lt;color=#{RarityColors.Hex(t)}&gt;".</summary>
    public static string Hex(int tier)
    {
        Color c = Get(tier);
        return ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Mã hex theo enum — xem <see cref="Hex(int)"/>.</summary>
    public static string Hex(Rarity rarity)
    {
        return Hex((int)rarity);
    }
}
