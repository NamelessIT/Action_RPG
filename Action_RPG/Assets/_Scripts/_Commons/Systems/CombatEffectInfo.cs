using UnityEngine;

/// <summary>
/// Loại hiệu ứng khống chế (CC) / debuff chiến đấu dùng chung cho Player / Enemy / Companion.
/// Đây là model thống nhất thay cho các bool rời rạc (isStun/isKnockback) — bước đầu của
/// hệ Combat Effect. Legacy isStun/isKnockback vẫn được giữ và bridge sang đây (xem DamageInfo).
/// </summary>
public enum KnockbackMotionMode
{
    Standard,
    Driven
}

public enum CombatEffectType
{
    // GIỮ GIÁ TRỊ SỐ CŨ để không phá dữ liệu enum đã serialize trong prefab/asset.
    Stun = 0,
    Knockback = 1,
    Airborne = 2,
    Root = 3,
    Silence = 4,
    Slow = 5,
    Unknown = 6,

    // Phase A: type mới append ở cuối.
    Taunt = 7,
    Fear = 8,
    Pull = 9
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

    [Tooltip("Định danh content/effect ổn định để runtime source-aware/stack-aware. Có thể để trống với legacy.")]
    public string effectId;

    [Tooltip("Thời lượng hiệu ứng (giây). Airborne dùng làm motion duration. Pull/Driven Knockback dùng như safety timeout; Standard Knockback không dùng duration để giữ control.")]
    public float duration = 1f;

    [Tooltip("Lực đẩy (Standard Knockback). Giữ field legacy để toàn bộ content hiện tại không phải refactor trong Phase D.")]
    public float force = 0f;

    [Header("--- Knockback Motion ---")]
    [Tooltip("Standard = impulse knockback hiện tại. Driven = target bị source đang lao tới ủi đi liên tục.")]
    public KnockbackMotionMode knockbackMode = KnockbackMotionMode.Standard;

    [Tooltip("Anchor do source của Driven Knockback cập nhật liên tục. Chỉ dùng khi knockbackMode=Driven.")]
    public Transform drivenAnchor;

    [Tooltip("Hướng ủi world-space. Chỉ dùng khi knockbackMode=Driven.")]
    public Vector3 drivenDirection;

    [Tooltip("Khoảng cách target được giữ ở phía trước anchor/source khi Driven Knockback.")]
    public float drivenOffset = 1f;

    [Tooltip("Độ cao hất tung (chỉ Airborne).")]
    public float height = 1.2f;

    [Tooltip("Cấp va chạm để vượt Super Armor (0 thường, 1 heavy, 2 massive).")]
    public int impactLevel = 0;

    [Tooltip("Độ mạnh hiệu ứng — dùng cho Slow: tỉ lệ giảm TỐC ĐỘ DI CHUYỂN 0..1 (vd 0.3 = giảm 30%).")]
    [Range(0f, 1f)]
    public float magnitude = 0f;

    [Tooltip("Vị trí nguồn (để tính hướng đẩy lùi). Thường set runtime = vị trí kẻ đánh.")]
    public Vector3 sourcePosition;

    [Header("--- Forced Control Anchor ---")]
    [Tooltip("Anchor mà target phải chase/flee đối với Taunt/Fear. Nếu null, runtime dùng source.transform. " +
             "Dùng cho decoy/illusion không có Stats.")]
    public Transform forcedControlAnchor;

    [Tooltip("[LEGACY] Phase A giữ field để không phá asset. Global CC rule giờ tự quyết định resistance theo type; content mới không dùng field này.")]
    public bool respectEffectResistance = true;

    [Tooltip("True = ngắt hành động đang thực hiện (đòn đánh/skill) của mục tiêu.")]
    public bool interruptCurrentAction = true;

    [Tooltip("True = nếu ngắt 1 skill đang cast thì đưa skill đó vào cooldown (cho Silence/CC).")]
    public bool putInterruptedSkillOnCooldown = false;

    [Header("--- Status Effect Runtime Contract ---")]
    [Tooltip("Cách instance cùng effect/source stack hoặc refresh. Phase A mới dựng contract.")]
    public EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshSameSource;

    [Min(1)] public int maxStacks = 1;

    [Tooltip("Pull anchor mode. Chỉ có ý nghĩa với CombatEffectType.Pull.")]
    public PullAnchorMode pullAnchorMode = PullAnchorMode.SourceTransform;
    public Vector3 pullPoint;
    public Transform pullTransform;
    [Tooltip("Tốc độ kéo theo world units/giây (chỉ Pull).")]
    public float pullSpeed = 0f;

    [Tooltip("Ghi chú tùy chọn (debug/tooltip).")]
    public string note;

    public CombatEffectInfo() { }

    public CombatEffectInfo(CombatEffectType t, float dur)
    {
        type = t;
        duration = dur;
    }

    /// <summary>Clone request trước khi resolver chỉnh duration/runtime payload, tránh mutate asset/shared object.</summary>
    public CombatEffectInfo CreateRuntimeCopy()
    {
        return new CombatEffectInfo
        {
            type = type,
            effectId = effectId,
            duration = duration,
            force = force,
            knockbackMode = knockbackMode,
            drivenAnchor = drivenAnchor,
            drivenDirection = drivenDirection,
            drivenOffset = drivenOffset,
            height = height,
            impactLevel = impactLevel,
            magnitude = magnitude,
            sourcePosition = sourcePosition,
            forcedControlAnchor = forcedControlAnchor,
            respectEffectResistance = respectEffectResistance,
            interruptCurrentAction = interruptCurrentAction,
            putInterruptedSkillOnCooldown = putInterruptedSkillOnCooldown,
            stackPolicy = stackPolicy,
            maxStacks = maxStacks,
            pullAnchorMode = pullAnchorMode,
            pullPoint = pullPoint,
            pullTransform = pullTransform,
            pullSpeed = pullSpeed,
            note = note
        };
    }
}
