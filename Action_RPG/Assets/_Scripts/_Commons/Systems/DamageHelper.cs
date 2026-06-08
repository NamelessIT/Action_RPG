using UnityEngine;

/// <summary>
/// Lớp Helper tĩnh quản lý toàn bộ việc gây sát thương trong game.
/// Thay vì gọi thẳng enemy.TakeDamage(new DamageInfo{...}), HÃY GỌI HÀM NÀY.
/// </summary>
public static class DamageHelper
{
    /// <summary>
    /// Hàm xử lý sát thương chuẩn cho MỌI nguồn gốc (Vũ khí, Kỹ năng, Đạn, Bẫy, Effect).
    /// </summary>
    /// <param name="attacker">Kẻ gây sát thương (Player, Enemy, Companion...). Không được null.</param>
    /// <param name="target">Kẻ nhận sát thương. Không được null.</param>
    /// <param name="sourceTransform">Vị trí phát ra sát thương (VD: Vị trí viên đạn, tâm vụ nổ, hoặc transform của Player). Dùng để tính hướng đâm lén (Backstab).</param>
    /// <param name="multiplier">Hệ số nhân sát thương gốc. VD: 1.5f = 150% damage.</param>
    /// <param name="skill">Truyền SkillData vào nếu nguồn là Kỹ năng. Nếu là đánh thường thì để null.</param>
    /// <param name="weapon">Truyền WeaponData vào nếu nguồn là Vũ khí. Nếu là Kỹ năng thì để null.</param>
    /// <param name="impactLvl">Độ giật lùi (0: Không giật, 1: Giật nhẹ, 2: Giật mạnh vỡ siêu giáp).</param>
    /// <param name="applyStun">Có kèm hiệu ứng Choáng (Stun) không?</param>
    /// <param name="stunTime">Thời gian Choáng (nếu applyStun = true).</param>
    /// <param name="applyKnockback">Có kèm hiệu ứng đẩy lùi (Knockback) không?</param>
    /// <param name="knockbackForce">Lực đẩy lùi (nếu applyKnockback = true).</param>
    /// <param name="isGuaranteedCrit">Bật cờ này = true nếu muốn đòn này CHẮC CHẮN chí mạng (bỏ qua xác suất).</param>
    public static void ApplyStandardDamage(
        AllyStats attacker,
        Stats target,
        Transform sourceTransform,
        float multiplier = 1.0f,
        SkillData skill = null,
        WeaponData weapon = null,
        int impactLvl = 0,
        bool applyStun = false,
        float stunTime = 0f,
        bool applyKnockback = false,
        float knockbackForce = 0f,
        bool isGuaranteedCrit = false)
    {
        // 1. Chốt chặn an toàn (Sanity Checks)
        if (target == null || target.currentHp <= 0 || attacker == null) return;

        // 2. Tính toán Hướng đòn đánh (Dựa trên CombatMath)
        // Lưu ý: Dùng sourceTransform (viên đạn/vụ nổ) để tính hướng so với target
        float dirFactor = 0f;
        if (sourceTransform != null)
        {
            dirFactor = CombatMath.CalculateDirectionFactor(sourceTransform, target);
        }
        else
        {
            dirFactor = CombatMath.CalculateDirectionFactor(attacker.transform, target);
        }

        // 3. Tính toán Chí Mạng (Crit)
        bool isCrit = isGuaranteedCrit; // Nếu ép chí mạng thì luôn true
        if (!isCrit)
        {
            // Cộng dồn tỉ lệ crit từ Stats gốc và Vũ khí đang cầm
            float totalCritChance = attacker.critChance;
            isCrit = CombatMath.CheckIsCrit(totalCritChance);
        }

        // 4. Lò tính toán Sát thương (CombatMath xử lý Giáp, Kháng phép, Buff/Debuff)
        var dmgTuple = CombatMath.CalculateFullDamage(
            attacker,
            target,
            dirFactor,
            isCrit,
            skill,
            weapon,
            multiplier
        );

        // 5. Đóng gói Sát thương cuối cùng
        DamageInfo info = new DamageInfo
        {
            sourcePosition = sourceTransform != null ? sourceTransform.position : attacker.transform.position,
            attacker = attacker,
            physDamage = dmgTuple.phys,
            magicDamage = dmgTuple.magic,
            trueDamage = dmgTuple.trueDmg,
            isCrit = isCrit,
            impactLevel = impactLvl,
            isStun = applyStun,
            stunDuration = stunTime
        };

        // 6. Giáng đòn thực tế
        target.TakeDamage(info);
    }

    /// <summary>
    /// Hàm quá tải (Overload) rút gọn dành cho các Effect/Vũ khí chỉ cần gây Damage thuần tùy biến, không cần truy xuất Skill/Weapon Data.
    /// </summary>
    public static void ApplyQuickProcDamage(AllyStats attacker, Stats target, float physMultiplier, float magicMultiplier, Transform sourceTransform = null)
    {
        if (target == null || attacker == null) return;

        bool isCrit = CombatMath.CheckIsCrit(attacker.critChance);
        float dirFactor = sourceTransform != null ? CombatMath.CalculateDirectionFactor(sourceTransform, target) : 0f;

        // Gọi CombatMath để scale chỉ số theo hướng và Crit
        var dmgTuple = CombatMath.CalculateFullDamage(attacker, target, dirFactor, isCrit, null, null, 1f);

        DamageInfo info = new DamageInfo
        {
            attacker = attacker,
            sourcePosition = sourceTransform != null ? sourceTransform.position : attacker.transform.position,
            isCrit = isCrit,
            physDamage = attacker.physicalAtk * physMultiplier, // Base raw
            magicDamage = attacker.magicAtk * magicMultiplier   // Base raw
        };

        target.TakeDamage(info);
    }
}