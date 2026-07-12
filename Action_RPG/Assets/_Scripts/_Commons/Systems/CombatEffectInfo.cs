using UnityEngine;

/// <summary>
/// Loại hiệu ứng khống chế (CC) / debuff chiến đấu dùng chung cho Player / Enemy / Companion.
/// Đây là model thống nhất thay cho các bool rời rạc (isStun/isKnockback) — bước đầu của
/// hệ Combat Effect. Legacy isStun/isKnockback vẫn được giữ và bridge sang đây (xem DamageInfo).
/// </summary>
public enum CombatEffectType
{
    Stun,       // choáng: khóa move + attack thường; có thể bị cleanse tùy thiết kế
    Knockback,  // đẩy lùi theo lực 'force'
    Airborne,   // hất tung: khóa MỌI hành động, không cleanse được trong lúc bay
    Root,       // trói chân: khóa move/dash, KHÔNG khóa skill (nếu không kèm Silence/Stun/Airborne)
    Silence,    // câm lặng: khóa Skill/Signature; basic attack vẫn dùng được
    Slow,       // làm chậm (giảm move/attack speed) qua EffectiveSlowMultiplier
    Unknown     // [INT-01A] sentinel: interrupt KHÔNG gắn với 1 effect cụ thể (legacy/direct routine). Thêm CUỐI để an toàn serialize.
}

/// <summary>
/// [INT-01A] Ngữ cảnh khi 1 hành động (đòn đánh/skill windup/charge) bị NGẮT bởi CC. Consumer
/// (SkillManager / EnemyCombat) đọc flag để quyết cooldown/cost. Legacy event OnInterrupted (không
/// tham số) vẫn được fire song song để call-site cũ không vỡ.
/// </summary>
public struct InterruptContext
{
    public CombatEffectType type;
    public bool interruptCurrentAction;
    public bool putInterruptedSkillOnCooldown;
    public Stats source;   // kẻ gây CC (có thể null nếu nguồn không xác định)
    public string note;

    public InterruptContext(CombatEffectType type, bool interruptCurrentAction = true,
                            bool putInterruptedSkillOnCooldown = false, Stats source = null, string note = null)
    {
        this.type = type;
        this.interruptCurrentAction = interruptCurrentAction;
        this.putInterruptedSkillOnCooldown = putInterruptedSkillOnCooldown;
        this.source = source;
        this.note = note;
    }

    /// <summary>Dựng context từ 1 CombatEffectInfo (giữ đúng type/flags/note) + nguồn gây CC.</summary>
    public static InterruptContext FromEffect(CombatEffectInfo eff, Stats source)
        => new InterruptContext(eff.type, eff.interruptCurrentAction, eff.putInterruptedSkillOnCooldown, source, eff.note);
}

/// <summary>
/// Một hiệu ứng chiến đấu đơn lẻ kèm thông số. Serializable để gán list trong Inspector
/// (vd enemy skill effects) hoặc tạo runtime trong code.
/// </summary>
[System.Serializable]
public class CombatEffectInfo
{
    public CombatEffectType type = CombatEffectType.Stun;

    [Tooltip("Thời lượng hiệu ứng (giây). Với Knockback chỉ dùng force.")]
    public float duration = 1f;

    [Tooltip("Lực đẩy (chỉ Knockback).")]
    public float force = 0f;

    [Tooltip("Độ cao hất tung (chỉ Airborne).")]
    public float height = 1.2f;

    [Tooltip("Cấp va chạm để vượt Super Armor (0 thường, 1 heavy, 2 massive).")]
    public int impactLevel = 0;

    [Tooltip("Độ mạnh hiệu ứng — CHỈ dùng cho Slow: tỉ lệ giảm tốc 0..1 (vd 0.3 = giảm 30% move/attack speed).")]
    [Range(0f, 1f)]
    public float magnitude = 0f;

    [Tooltip("Vị trí nguồn (để tính hướng đẩy lùi). Thường set runtime = vị trí kẻ đánh.")]
    public Vector3 sourcePosition;

    [Tooltip("True = thời lượng bị giảm bởi resistanceEffect (effectRes). Airborne nên để FALSE.")]
    public bool respectEffectResistance = true;

    [Tooltip("True = ngắt hành động đang thực hiện (đòn đánh/skill) của mục tiêu.")]
    public bool interruptCurrentAction = true;

    [Tooltip("True = nếu ngắt 1 skill đang cast thì đưa skill đó vào cooldown (cho Silence/CC).")]
    public bool putInterruptedSkillOnCooldown = false;

    [Tooltip("Ghi chú tùy chọn (debug/tooltip).")]
    public string note;

    public CombatEffectInfo() { }

    public CombatEffectInfo(CombatEffectType t, float dur)
    {
        type = t;
        duration = dur;
    }
}
