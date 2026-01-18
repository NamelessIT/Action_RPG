using UnityEngine;

public static class CombatMath
{
    // Cấu hình hằng số (Global Constants) theo tài liệu
    private const float C_CONST = 100f; // Giá trị C trong công thức DR
    private const float K_CONST = 0.005f; // Giá trị K cho Defense Value

    /// <summary>
    /// Tính toán hệ số t (Hướng đánh). Giữ nguyên code cũ đã ngon.
    /// </summary>
    public static float CalculateDirectionFactor(Transform attacker, Stats targetStats)
    {
        // 1. Hướng đòn đánh
        Vector3 attackDir = (targetStats.transform.position - attacker.position).normalized;
        // 2. [QUAN TRỌNG] Lấy hướng mặt từ biến facingDirection thủ công
        // Thay vì dùng targetStats.transform.forward (bị sai trong 2.5D)
        Vector3 targetForward = targetStats.facingDirection;

        // Nếu vector = 0 (lỗi), gán mặc định
        if (targetForward == Vector3.zero) targetForward = Vector3.back;

        float dir = Vector3.Dot(attackDir, targetForward);
        float t = Mathf.Clamp01((dir + 1) / 2);

        // Làm tròn theo bảng quy ước
        if (t >= 0.85f) return 1.0f;   // Sau lưng
        if (t >= 0.65f) return 0.75f;  // Chéo sau
        if (t >= 0.35f) return 0.5f;   // Bên hông
        if (t >= 0.15f) return 0.25f;  // Chéo trước
        return 0.0f;                   // Trước mặt
    }

    /// <summary>
    /// HÀM TÍNH DAMAGE CHÍNH THỨC (Theo file công thức)
    /// </summary>
    public static float CalculateFullDamage(Stats attacker, Stats target, float t, bool isCrit)
    {
        // --- 1. Tính Raw Damage ---
        float critMult = isCrit ? attacker.baseCritMultiplier : 1.0f;

        // [Source: 67] Raw Phys
        float rawPhysical = attacker.physicalAtk * attacker.skillPhysicalMultiplier * critMult;

        // [Source: 68] Raw Magic
        float rawMagic = attacker.magicAtk * attacker.skillMagicMultiplier * critMult;


        // --- 2. Tính Armor/Resist thực tế theo hướng đánh (Armor Direction) ---
        // [Source: 69] armorDir
        float armorDir = target.armor * (1 - (attacker.armorBackstabReduce * t));

        // [Source: 70] magicResistDir
        float magicResistDir = target.magicResist * (1 - (attacker.magicResistBackstabReduce * t));


        // --- 3. Tính % Giảm Sát Thương (Damage Reduction) ---
        // [Source: 71] DR Vật lý = 25% * (armor / (armor + C))
        float physDR = 0.25f * (armorDir / (armorDir + C_CONST));

        // [Source: 72] DR Phép
        float magicDR = 0.25f * (magicResistDir / (magicResistDir + C_CONST));


        // --- 4. Tính Defense Value Multiplier (Chỉ số đỡ đòn của vũ khí) ---
        // [Source: 73, 74] Chỉ tác dụng nếu t <= 0.5 (Đánh trước mặt hoặc bên hông)
        float defenseValMult = 1.0f;
        if (t <= 0.5f)
        {
            defenseValMult = 1f / (1f + target.defenseValue * K_CONST);
        }


        // --- 5. Tính Direction Bonus Multiplier (Thưởng sát thương theo hướng) ---
        // [Source: 75-80] Từ 1.0 (t=0) đến 1.25 (t=1). Công thức tuyến tính: 1 + 0.25 * t
        float dirBonusMult = 1.0f + (0.25f * t);


        // --- 6. TÍNH FINAL DAMAGE ---
        // [Source: 81] Final Phys
        float finalPhys = rawPhysical * (1 - physDR) * defenseValMult * dirBonusMult;

        // [Source: 82] Final Magic
        float finalMagic = rawMagic * (1 - magicDR) * defenseValMult * dirBonusMult;

        // [Source: 83] Tổng
        float totalDamage = finalPhys + finalMagic;

        // Debug chi tiết (có thể comment lại nếu spam console)

        Debug.Log($"LOG DAMGE >> RawPhys: {rawPhysical} | RawMagic: {rawMagic} \n" +
                  $"ArmorDir: {armorDir} (Gốc {target.armor}) | T: {t} \n" +
                  $"DefenseMult: {defenseValMult} | DirBonus: {dirBonusMult} \n" +
                  $"FINAL: {totalDamage} (Phys: {finalPhys} + Magic: {finalMagic})");


        return totalDamage;
    }
}