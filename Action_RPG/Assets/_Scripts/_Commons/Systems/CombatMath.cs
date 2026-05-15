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
    /// Kiểm tra xem đòn đánh có chí mạng không dựa trên % cơ hội (0-100)
    /// </summary>
    public static bool CheckIsCrit(float critChance)
    {
        // Random từ 0 đến 100. Ví dụ chance = 25.
        // Nếu ra 10 -> 10 < 25 -> True (Crit)
        // Nếu ra 80 -> 80 < 25 -> False (Không Crit)
        float critCheck = Random.Range(0f, 1f);
        //Debug.Log($"Crit Check: {critCheck}");
        return critCheck < critChance;
    }

    /// <summary>
    /// Hàm tính damage chính thức
    /// <param name="skill">Skill sử dụng (null nếu là đánh thường)</param>
    /// <param name="weapon">Vũ khí đang cầm (để check type khi đánh thường)</param>
    /// <param name="externalMult">Hệ số phụ (Combo, Charge... mặc định là 1)</param>
    /// Hàm tính damage chính thức (Đã thêm ignoreReduction)
    /// </summary>
    public static (float phys, float magic, float trueDmg) CalculateFullDamage(Stats attacker, Stats target, float t, bool isCrit, SkillData skill, WeaponData weapon, float externalMult = 1.0f, bool ignoreReduction = false)
    {
        // --- 1. Tính Raw Damage ---
        float critMult = isCrit ? attacker.baseCritMultiplier : 1.0f; //Sửa lại thành critMultiplier 

        // XÁC ĐỊNH MULTIPLIER (SKILL vs BASIC ATTACK) ---
        float physMult = 0f;
        float magicMult = 0f;
        if (skill != null)
        {
            // A. Dùng Skill: Lấy hệ số từ Skill
            physMult = skill.skillPhysicalMultiplier;
            magicMult = skill.skillMagicMultiplier;
        }
        else
        {
            // B. Đánh thường: Dựa vào loại vũ khí
            // Nếu weapon null (tay không) -> mặc định là Physical
            WeaponData.WeaponAtkType atkType = weapon != null
                ? weapon.weaponAtkType
                : WeaponData.WeaponAtkType.Physical;

            switch (atkType)
            {
                case WeaponData.WeaponAtkType.Magic:
                    physMult  = 0f;
                    magicMult = 1.0f;
                    break;
                case WeaponData.WeaponAtkType.Both:
                    // Chia đều 50/50 — hiện cả màu cam lẫn tím
                    physMult  = 0.5f;
                    magicMult = 0.5f;
                    break;
                default: // Physical (Hand, Sword, Spear, Bow...)
                    physMult  = 1.0f;
                    magicMult = 0f;
                    break;
            }
        }
        // Nhân thêm hệ số phụ (Combo step / Charge attack)
        physMult *= externalMult;
        magicMult *= externalMult;
        // Raw Phys + Raw Magic
        float rawPhysical = attacker.physicalAtk * physMult * critMult * attacker.damageOutputMultiplier; //buff hay debuff giảm damage apply vào đây
        float rawMagic = attacker.magicAtk * magicMult * critMult * attacker.damageOutputMultiplier;


        // XỬ LÝ SÁT THƯƠNG CHUẨN (Bỏ qua Giáp)
        if (ignoreReduction)
        {
            float trueDamage = rawPhysical + rawMagic;
            float bonus = 1.0f + (0.25f * t);

            // Trả toàn bộ thành True Damage
            return (0f, 0f, trueDamage * bonus);
        }


        // --- 2. Tính Armor/MagicResist thực tế theo hướng đánh (Armor Direction) ---
        float armorDir = target.armor * (1 - (attacker.armorBackstabReduce * t));
        float magicResistDir = target.magicResist * (1 - (attacker.magicResistBackstabReduce * t));


        // --- 3. Tính % Giảm Sát Thương (Damage Reduction) ---
        // DR Vật lý = 25% * (armor / (armor + C))
        float physDR = 0.25f * (armorDir / (armorDir + C_CONST));

        // DR Phép
        float magicDR = 0.25f * (magicResistDir / (magicResistDir + C_CONST));


        // --- 4. TÍNH DEFENSE & MANUAL GUARD (SỬA LẠI) ---
        float defenseValMult = 1.0f;
        float skillReductionMult = 1.0f; // Mặc định là 1 (không giảm)

        // Kiểm tra góc Block (blockThreshold nằm ở Stats cha nên gọi trực tiếp được)
        if (t <= target.blockThreshold)
        {
            // A. Auto Block bằng chỉ số Defense của vũ khí
            defenseValMult = 1f / (1f + target.defenseValue * K_CONST);

            // B. Manual Guard (Phải ép kiểu sang AllyStats mới check được)
            if (target is AllyStats allyTarget)
            {
                if (allyTarget.isManualGuarding)
                {
                    // Giảm damage theo chỉ số (VD: 1 - 0.5 = 0.5)
                    skillReductionMult = 1.0f - allyTarget.manualGuardReduction;
                    allyTarget.NotifyBlockSuccess(); // C. Báo hiệu Block thành công (Logic hồi Stamina)
                }
            }
            else // Nếu target là Enemy thường
            {               
                if (target.defenseValue > 0)
                {
                    // Enemy có thể cần logic notify riêng nếu muốn, tạm thời bỏ qua
                }
            }
        }
        // Cứ là AllyStats và đang vung kiếm (MomentumActive) là được giảm
        if (target is AllyStats warrior && warrior.isMomentumActive)
        {
            // Nhân dồn giảm sát thương (Multiplicative stacking)
            // Ví dụ: Giảm 30% -> Nhân với 0.7
            skillReductionMult *= (1.0f - warrior.momentumReduction);
        }

        // --- 5. Tính Direction Bonus Multiplier (Thưởng sát thương theo hướng) ---
        // Từ 1.0 (t=0) đến 1.25 (t=1). Công thức tuyến tính: 1 + 0.25 * t
        float dirBonusMult = 1.0f + (0.25f * t);


        // --- 6. TÍNH FINAL DAMAGE ---
        float finalPhys = rawPhysical * (1 - physDR) * defenseValMult * dirBonusMult * skillReductionMult;
        float finalMagic = rawMagic * (1 - magicDR) * defenseValMult * dirBonusMult * skillReductionMult;

        float totalDamage = finalPhys + finalMagic;
        // Debug kiểm tra xem skillReductionMult có hoạt động không
        if (skillReductionMult < 1.0f)
        {
            Debug.Log($"<color=green>SHIELD BLOCK!</color> Giảm {(1 - skillReductionMult) * 100}% sát thương. Damage còn: {totalDamage}");
        }
        // Debug chi tiết — bỏ comment khi cần tuning damage, tắt khi ship
        // Debug.Log($"LOG DAMAGE >> RawPhys:{rawPhysical:F1} RawMagic:{rawMagic:F1} | " +
        //           $"Armor:{armorDir:F1} DR:{physDR:P0} | T:{t} DirBonus:{dirBonusMult:F2} | " +
        //           $"Final Phys:{finalPhys:F1} Magic:{finalMagic:F1} Total:{totalDamage:F1}");


        // Tổng Damage: Trả về 3 cục riêng biệt (True = 0 ở đòn đánh thường)
        return (finalPhys, finalMagic, 0f);
    }
}