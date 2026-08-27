using UnityEngine;

/// <summary>
/// Bảng màu + token giao diện dùng chung cho TOÀN BỘ UI.
/// Hướng thẩm mỹ: <b>arcane</b> — nền tím-đen sâu, viền phát sáng tím/lam, chữ sáng.
///
/// NGUYÊN TẮC:
///  • Không viết cứng màu trong script UI nữa — luôn gọi vào đây.
///  • Đây là màu GIAO DIỆN. Màu VFX/gameplay (VisualDebugHelper, hiệu ứng skill) KHÔNG thuộc
///    phạm vi file này — chúng là màu hiệu ứng, gom vào đây là sai loại.
///  • Màu theo bậc rarity nằm ở <see cref="RarityColors"/>, không nhân bản sang đây.
///
/// Cách dùng thường gặp:
///   <code>img.color = UIPalette.SlotEmpty;</code>
///   <code>txt.color = UIPalette.DamageMagic;</code>
///   <code>border.color = UIPalette.Glow(UIPalette.RuneGlow, Time.time);</code>
/// </summary>
public static class UIPalette
{
    // ═══════════════════════════════════════════════════════════
    //  NỀN — tím-đen sâu, dùng cho panel/khung
    // ═══════════════════════════════════════════════════════════

    /// <summary>Nền sâu nhất — backdrop sau panel, modal overlay.</summary>
    public static readonly Color VoidDeep   = new Color(0.043f, 0.027f, 0.075f, 1.00f); // #0B0713

    /// <summary>Nền panel chính, bán trong suốt để còn thấy được cảnh phía sau.</summary>
    public static readonly Color VoidPanel  = new Color(0.078f, 0.063f, 0.129f, 0.92f); // #141021

    /// <summary>Nền ô lõm (slot, input, vùng chứa).</summary>
    public static readonly Color VoidSunk   = new Color(0.055f, 0.043f, 0.098f, 0.85f); // #0E0B19

    /// <summary>Nền nổi lên trên panel (tooltip, dropdown, card).</summary>
    public static readonly Color VoidRaised = new Color(0.122f, 0.098f, 0.192f, 0.96f); // #1F1931

    // ═══════════════════════════════════════════════════════════
    //  VIỀN & PHÁT SÁNG — chất "arcane"
    // ═══════════════════════════════════════════════════════════

    /// <summary>Viền tĩnh, không gây chú ý.</summary>
    public static readonly Color RuneEdge   = new Color(0.420f, 0.310f, 0.659f, 1.00f); // #6B4FA8

    /// <summary>Viền phát sáng — hover, focus, phần tử đang hoạt động.</summary>
    public static readonly Color RuneGlow   = new Color(0.702f, 0.533f, 1.000f, 1.00f); // #B388FF

    /// <summary>Accent chính — nút chính, thanh tiến trình, điểm nhấn.</summary>
    public static readonly Color ArcaneCore = new Color(0.545f, 0.361f, 0.965f, 1.00f); // #8B5CF6

    /// <summary>Accent phụ lam sáng — dùng ĐỐI TRỌNG với tím, đừng lạm dụng.</summary>
    public static readonly Color AetherCyan = new Color(0.302f, 0.847f, 0.902f, 1.00f); // #4DD8E6

    // ═══════════════════════════════════════════════════════════
    //  CHỮ
    // ═══════════════════════════════════════════════════════════

    public static readonly Color TextBright = new Color(0.929f, 0.910f, 0.961f, 1.00f); // #EDE8F5
    public static readonly Color TextMuted  = new Color(0.604f, 0.561f, 0.710f, 1.00f); // #9A8FB5
    public static readonly Color TextDim    = new Color(0.369f, 0.337f, 0.467f, 1.00f); // #5E5677

    // ═══════════════════════════════════════════════════════════
    //  TRẠNG THÁI — ngữ nghĩa, TÁCH RIÊNG khỏi accent
    // ═══════════════════════════════════════════════════════════

    /// <summary>Tốt / đã mở khoá / hồi phục.</summary>
    public static readonly Color StateGood   = new Color(0.298f, 0.831f, 0.365f, 1.00f);

    /// <summary>Chú ý / sẵn sàng mở / cảnh báo nhẹ.</summary>
    public static readonly Color StateWarn   = new Color(1.000f, 0.780f, 0.180f, 1.00f);

    /// <summary>Xấu / mất máu / lỗi.</summary>
    public static readonly Color StateBad    = new Color(0.937f, 0.325f, 0.325f, 1.00f);

    /// <summary>Thông tin / đang trang bị.</summary>
    public static readonly Color StateInfo   = new Color(0.302f, 0.635f, 1.000f, 1.00f);

    /// <summary>Khoá / không dùng được — xám NGẢ TÍM, không phải xám trung tính.</summary>
    public static readonly Color StateLocked = new Color(0.290f, 0.267f, 0.353f, 1.00f);

    // ═══════════════════════════════════════════════════════════
    //  Ô / SLOT
    // ═══════════════════════════════════════════════════════════

    /// <summary>Ô trống.</summary>
    public static readonly Color SlotEmpty    = new Color(0.078f, 0.063f, 0.125f, 0.55f);

    /// <summary>Ô có đồ.</summary>
    public static readonly Color SlotFilled   = new Color(0.145f, 0.118f, 0.220f, 0.80f);

    /// <summary>Ô đang được trang bị — nền tím nhạt, thay cho nền lục cũ.</summary>
    public static readonly Color SlotEquipped = new Color(0.353f, 0.239f, 0.612f, 0.50f);

    /// <summary>Icon mờ đi khi ô không dùng được.</summary>
    public static readonly Color IconDimmed   = new Color(0.404f, 0.376f, 0.478f, 1.00f);

    /// <summary>Lớp phủ khi đang hồi chiêu.</summary>
    public static readonly Color CooldownVeil = new Color(0.055f, 0.043f, 0.098f, 0.72f);

    // ═══════════════════════════════════════════════════════════
    //  SỐ SÁT THƯƠNG
    // ═══════════════════════════════════════════════════════════

    /// <summary>Vật lý — cam. Trùng bậc Anomalous của <see cref="RarityColors"/>, cố ý.</summary>
    public static readonly Color DamagePhys  = new Color(1.000f, 0.550f, 0.100f, 1.00f);

    /// <summary>Phép — tím, kéo về đúng tông ArcaneCore.</summary>
    public static readonly Color DamageMagic = new Color(0.702f, 0.400f, 1.000f, 1.00f);

    /// <summary>Sát thương thật — trắng, xuyên mọi kháng.</summary>
    public static readonly Color DamageTrue  = new Color(1.000f, 1.000f, 1.000f, 1.00f);

    /// <summary>Hồi máu — lục.</summary>
    public static readonly Color DamageHeal  = new Color(0.298f, 0.831f, 0.365f, 1.00f);

    // ═══════════════════════════════════════════════════════════
    //  THANH TÀI NGUYÊN
    // ═══════════════════════════════════════════════════════════

    public static readonly Color BarHp      = new Color(0.847f, 0.208f, 0.278f, 1.00f);
    public static readonly Color BarStamina = new Color(0.510f, 0.784f, 0.310f, 1.00f);
    public static readonly Color BarSin     = new Color(0.639f, 0.259f, 0.898f, 1.00f);

    /// <summary>Khiên — lam-bạc phát sáng. KHÔNG dùng trắng: trắng đè lên nền đỏ đọc không ra khiên.</summary>
    public static readonly Color BarShield     = new Color(0.545f, 0.831f, 0.961f, 1.00f);

    /// <summary>Khiên phần vượt quá max HP — cùng tông, mờ hơn.</summary>
    public static readonly Color BarShieldOver = new Color(0.545f, 0.831f, 0.961f, 0.55f);

    /// <summary>Rãnh nền của thanh.</summary>
    public static readonly Color BarTrack   = new Color(0.067f, 0.055f, 0.110f, 0.90f);

    /// <summary>Vệt hiện ra rồi rút dần khi vừa mất máu.</summary>
    public static readonly Color BarLoss    = new Color(0.937f, 0.325f, 0.325f, 0.75f);

    /// <summary>Vệt khi vừa hồi máu.</summary>
    public static readonly Color BarGain    = new Color(0.400f, 1.000f, 0.400f, 0.75f);

    // ═══════════════════════════════════════════════════════════
    //  TIỆN ÍCH
    // ═══════════════════════════════════════════════════════════

    /// <summary>Cùng màu, đổi alpha. Dùng thay cho việc khai báo thêm hằng chỉ để khác độ trong.</summary>
    public static Color With(Color c, float alpha)
    {
        c.a = Mathf.Clamp01(alpha);
        return c;
    }

    /// <summary>Làm mờ một màu về phía nền — cho trạng thái không dùng được.</summary>
    public static Color Dim(Color c, float amount = 0.55f)
        => Color.Lerp(c, VoidSunk, Mathf.Clamp01(amount));

    /// <summary>Đẩy một màu sáng lên — cho hover / nhấn mạnh.</summary>
    public static Color Lift(Color c, float amount = 0.30f)
        => Color.Lerp(c, Color.white, Mathf.Clamp01(amount));

    /// <summary>Nhịp phát sáng theo thời gian. <paramref name="speed"/> = số nhịp mỗi giây.
    /// Dùng cho viền rune, ô sẵn sàng mở khoá, cảnh báo HP thấp.</summary>
    public static Color Glow(Color c, float time, float speed = 1.6f, float depth = 0.35f)
    {
        float t = (Mathf.Sin(time * speed * Mathf.PI * 2f) + 1f) * 0.5f;
        return Color.Lerp(c, Lift(c, 0.55f), t * Mathf.Clamp01(depth));
    }

    /// <summary>Mã hex (không có #) cho rich-text TMP.</summary>
    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    /// <summary>Bọc chuỗi trong tag màu rich-text TMP.</summary>
    public static string Tag(string text, Color c) => "<color=#" + Hex(c) + ">" + text + "</color>";
}
