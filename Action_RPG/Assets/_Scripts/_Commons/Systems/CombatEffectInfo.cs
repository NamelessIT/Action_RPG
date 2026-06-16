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
    Slow        // làm chậm (giảm move/attack speed) — chưa nối, để sẵn cho phase sau
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
