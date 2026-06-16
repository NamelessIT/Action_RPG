using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phân loại nguồn sát thương (cho các effect cần phân biệt, vd phản đòn cận chiến).
/// Melee: đòn cận chiến trực tiếp. Ranged: đạn bay + skill AoE tầm xa.
/// DoT: thiêu đốt/chảy máu/độc theo thời gian. Other: môi trường/phản đòn/khác.
/// </summary>
public enum DamageSourceType { Melee, Ranged, DoT, Other }

[System.Serializable]
public class DamageInfo
{
    // Nguồn sát thương — mặc định Melee (đòn trực tiếp thường gặp nhất).
    public DamageSourceType sourceType = DamageSourceType.Melee;

    // [MỚI] Chia làm 3 biến thành phần
    public float physDamage;
    public float magicDamage;
    public float trueDamage;

    // [MỚI] Hàm tiện ích (Getter) để lấy tổng sát thương bất cứ lúc nào cần
    public float TotalRawDamage => physDamage + magicDamage + trueDamage;
    public bool isCrit;

    // --- Hiệu ứng khống chế (CC) ---
    public bool isStun;
    public float stunDuration;

    public bool isKnockback;
    public float knockbackForce;
    public Vector3 sourcePosition; // Vị trí kẻ đánh (để tính hướng đẩy lùi)

    public Stats attacker;

    // [MỚI] Cấp độ va chạm (Dùng cho Super Armor)
    // 0: Normal (Quái nhỏ)
    // 1: Heavy (Quái to / Trọng kích)
    // 2: Massive (Boss / Skill đặc biệt)
    public int impactLevel;

    // ─────────────────────────────────────────────────────────────
    //  [MỚI] HỆ COMBAT EFFECT (CC) — model thống nhất
    //  Song song với legacy isStun/isKnockback. Stats.ApplyCombatEffects() sẽ:
    //   1. Bridge legacy → effects (nếu chưa có), KHÔNG double-apply.
    //   2. Xử lý toàn bộ qua đường này.
    //  Các nguồn mới nên dùng AddEffect() thay vì set isStun/isKnockback.
    // ─────────────────────────────────────────────────────────────
    public List<CombatEffectInfo> effects;

    /// <summary>Thêm 1 hiệu ứng vào đòn đánh (tự khởi tạo list nếu cần).</summary>
    public DamageInfo AddEffect(CombatEffectInfo effect)
    {
        if (effect == null) return this;
        effects ??= new List<CombatEffectInfo>();
        effects.Add(effect);
        return this;
    }

    /// <summary>Có hiệu ứng nào thuộc loại type trong list effects không (không tính legacy).</summary>
    public bool HasEffect(CombatEffectType type)
    {
        if (effects == null) return false;
        for (int i = 0; i < effects.Count; i++)
            if (effects[i] != null && effects[i].type == type) return true;
        return false;
    }

    /// <summary>
    /// Bridge legacy isStun/isKnockback → effects list, CHỈ thêm nếu list chưa có loại đó
    /// (tránh double-apply khi nguồn đã dùng AddEffect). Gọi bởi Stats trước khi áp effects.
    /// </summary>
    public void BridgeLegacyEffects()
    {
        if (isStun && stunDuration > 0f && !HasEffect(CombatEffectType.Stun))
        {
            AddEffect(new CombatEffectInfo(CombatEffectType.Stun, stunDuration)
            {
                impactLevel = impactLevel,
                sourcePosition = sourcePosition,
                respectEffectResistance = true,
            });
        }
        if (isKnockback && knockbackForce > 0f && !HasEffect(CombatEffectType.Knockback))
        {
            AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f)
            {
                force = knockbackForce,
                impactLevel = impactLevel,
                sourcePosition = sourcePosition,
                respectEffectResistance = false, // knockback force dùng resistanceKnockBack riêng
            });
        }
    }
}
