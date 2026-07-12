using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [EAM] Kiểu đòn tấn công của Enemy. Quyết định runtime style (P1-EAM-02 implement phần chạy thực tế).
/// </summary>
public enum EnemyAttackStyle
{
    MeleeSingle,           // 1 đòn cận chiến đơn (tương đương fallback melee hiện tại)
    MeleeSweep,            // quét cung theo sweepStartAngle → sweepEndAngle
    MeleeThrust,           // đâm thẳng (hộp dài về phía trước)
    MeleeCircleAOE,        // nổ vòng quanh enemy (aoeRadius)
    DashStrike,            // lướt tới rồi đánh
    ProjectileDirectional, // bắn đạn theo hướng mặt
    ProjectileTargeted,    // bắn đạn khóa mục tiêu (aim/homing)
    GroundTargetAOE,       // đặt vùng AoE dưới chân mục tiêu (telegraph)
    ConeBreath,            // hơi thở hình nón (P1-EAM-02 chưa implement → warning)
    SelfBuff,              // tự buff (P1-EAM-02 chưa implement → warning)
    Summon                 // triệu hồi (P1-EAM-02 chưa implement → warning)
}

/// <summary>
/// [EAM] Dữ liệu 1 đòn tấn công của Enemy (basic hoặc skill). EnemyCombat dispatch theo <see cref="style"/>.
/// Module null → EnemyCombat dùng melee fallback hiện tại. Runtime KHÔNG mutate asset — clone effect per target.
/// </summary>
[CreateAssetMenu(fileName = "EAM_New", menuName = "Action_RPG/Enemy Attack Module", order = 0)]
public class EnemyAttackModuleData : ScriptableObject
{
    [Header("--- Identity ---")]
    public string id;
    public string displayName;
    public EnemyAttackStyle style = EnemyAttackStyle.MeleeSingle;

    [Header("--- Core ---")]
    [Tooltip("Tầm hiệu lực của đòn (m). EnemyAI dùng cho stopping/chase + cast range.")]
    public float range = 2f;
    [Tooltip("Hồi chiêu giữa 2 lần dùng (giây). 0 = theo nhịp tốc đánh (đòn thường).")]
    public float cooldown = 0f;
    [Tooltip("Hệ số nhân sát thương (× physicalAtk/magicAtk theo nguồn).")]
    public float damageMultiplier = 1f;

    [Header("--- Timing (giây) ---")]
    [Tooltip("Windup / telegraph — báo đòn để player né.")]
    public float windupDuration = 0.5f;
    [Tooltip("Cửa sổ gây damage (active).")]
    public float activeDuration = 0.2f;
    [Tooltip("Recovery — hở sườn sau đòn.")]
    public float recoveryDuration = 0.3f;

    [Header("--- Hình học (Melee / AOE) ---")]
    [Tooltip("Góc tổng của đòn (độ) — MeleeSingle / hitbox cận chiến.")]
    public float attackAngle = 90f;
    [Tooltip("Góc bắt đầu quét — MeleeSweep.")]
    public float sweepStartAngle = -45f;
    [Tooltip("Góc kết thúc quét — MeleeSweep.")]
    public float sweepEndAngle = 45f;
    [Tooltip("Bán kính AoE — MeleeCircleAOE / GroundTargetAOE / ConeBreath.")]
    public float aoeRadius = 1.5f;

    [Header("--- Projectile (nếu style là Projectile*) ---")]
    public float projectileSpeed = 10f;
    public float projectileLifetime = 4f;
    public GameObject projectilePrefab;
    [Tooltip("Telegraph prefab tùy chọn (GroundTargetAOE / báo đòn AoE).")]
    public GameObject telegraphPrefab;

    [Header("--- Flags ---")]
    [Tooltip("Khóa hướng mặt về target khi bắt đầu đòn (commit từ pha telegraph).")]
    public bool faceTargetOnStart = true;
    [Tooltip("Có chạy telegraph (windup báo đòn) không.")]
    public bool useTelegraph = true;
    [Tooltip("Cấp va chạm CỘNG THÊM cho effect của đòn (phá Super Armor).")]
    public int impactBonus = 0;

    [Header("--- Combat Effects (CC) ---")]
    [Tooltip("Hiệu ứng CC áp lên mục tiêu khi trúng. Runtime CLONE per-target (không mutate asset).")]
    public List<CombatEffectInfo> effects = new List<CombatEffectInfo>();
}
