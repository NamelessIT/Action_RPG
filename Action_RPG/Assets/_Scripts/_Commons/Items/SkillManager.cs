using UnityEngine;
using System;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    public Action OnSkillCast;
    [Header("Settings")]
    public bool isPlayer = false; // Tick vào nếu là Player, bỏ tick nếu là Enemy

    // Biến tạm để test nhặt skill (giống EquipmentManager)
    public SkillData pickUpSkill;
    [Header("Player Slots (Only if isPlayer = true)")]
    public SkillData currentDefaultPassive; //Passive default
    public SkillData currentPassive1;    // Slot Passive
    public SkillData currentPassive2;
    public SkillData currentSkill;      // Slot Kỹ năng thường (E/Q)
    public SkillData currentSignature;  // Slot Chiêu cuối (R)
    [Header("Enemy List (Only if isPlayer = false)")]
    public List<SkillData> enemySkills = new List<SkillData>();
    private AllyStats allyStats;
    private PlayerController playerController;

    // Dictionary để quản lý các script skill đang chạy
    // Key: SkillData (File data) ---> Value: SkillBehavior (Script đang chạy trên người)
    private Dictionary<SkillData, SkillBehavior> activeSkills = new Dictionary<SkillData, SkillBehavior>();

    void Start()
    {
        if (isPlayer)
        {
            allyStats = GetComponent<AllyStats>();
            playerController = GetComponent<PlayerController>();

            // Guard: cảnh báo nếu thiếu component sau khi isPlayer = true
            if (allyStats == null)
                Debug.LogError("[SkillManager] isPlayer=true nhưng không tìm thấy AllyStats/PlayerStats trên " +
                               $"'{gameObject.name}'. Skill equip sẽ bị lỗi NullRef.");
            if (playerController == null)
                Debug.LogError("[SkillManager] isPlayer=true nhưng không tìm thấy PlayerController trên " +
                               $"'{gameObject.name}'. Skill Initialize sẽ bị lỗi NullRef.");
        }
    }
    // --- HÀM TRANG BỊ SKILL (Dùng chung) ---
    public void EquipSkill(SkillData newSkill)
    {
        Debug.Log(newSkill.name);
        if (newSkill == null)
        {
            Debug.LogWarning("Skill không hợp lệ (null)");
            return;
        }

        if (isPlayer)
        {
            EquipPlayerSkill(newSkill);
        }
        else
        {
            EquipEnemySkill(newSkill);
        }
    }

    // --- HÀM THÁO SKILL (Dùng chung) ---
    //public void UnequipSkill(SkillData skillToRemove)
    //{
    //    if (skillToRemove == null) return;

    //    if (isPlayer)
    //    {
    //        UnequipPlayerSkill(skillToRemove);
    //    }
    //    else
    //    {
    //        UnequipEnemySkill(skillToRemove);
    //    }
    //}

    // =========================================================
    // LOGIC CHO PLAYER (3 SLOTS CỐ ĐỊNH)
    // =========================================================
    private void EquipPlayerSkill(SkillData newSkill)
    {
        // Kiểm tra loại skill để gắn vào đúng slot
        switch (newSkill.skillType)
        {     
            // --- 1. DEFAULT PASSIVE (Chỉ có 1 slot) ---
            case SkillData.SkillType.DefaultPassive:
                // Tháo cái cũ ra trước (nếu có)
                if (currentDefaultPassive != null)
                {
                    RemovePassiveEffect(currentDefaultPassive);
                }
                Debug.Log($"Trang bị Default Passive: {newSkill.skillName}");
                currentDefaultPassive = newSkill;
                // [QUAN TRỌNG] Kích hoạt hiệu ứng (Gắn script ChrisPassive vào)
                ApplyPassiveEffect(newSkill);
                break;

            // --- 2. CLASS PASSIVE (Có 2 slot) ---
            case SkillData.SkillType.Passive:
                // Bước 1: Kiểm tra xem đã đeo skill này chưa (tránh đeo 2 slot giống nhau)
                if (currentPassive1 == newSkill || currentPassive2 == newSkill)
                {
                    Debug.Log("Đã trang bị Passive này rồi!");
                    return;
                }
                // Bước 2: Tìm slot trống
                if (currentPassive1 == null)
                {
                    Debug.Log($"Trang bị Passive vào Slot 1: {newSkill.skillName}");
                    currentPassive1 = newSkill;
                    ApplyPassiveEffect(newSkill);
                }
                else if (currentPassive2 == null)
                {
                    Debug.Log($"Trang bị Passive vào Slot 2: {newSkill.skillName}");
                    currentPassive2 = newSkill;
                    ApplyPassiveEffect(newSkill);
                }
                else
                {
                    // Bước 3: Cả 2 slot đều đầy -> Báo lỗi
                    Debug.Log("Đã có đủ 2 passive");
                    break;
                }
                break;

            case SkillData.SkillType.Skill:
                if (currentSkill != newSkill)
                {
                    // A. Tháo skill cũ ra trước (nếu có)
                    if (currentSkill != null)
                    {
                        RemoveSkillEffect(currentSkill); // <--- QUAN TRỌNG: Dọn dẹp skill cũ
                    }

                    Debug.Log($"Player trang bị Skill: {newSkill.skillName}");
                    currentSkill = newSkill;
                    ApplySkillEffect(newSkill);
                }
                break;

            case SkillData.SkillType.Signature:
                if (currentSignature != newSkill)
                {
                    // A. Tháo skill cũ ra trước
                    if (currentSignature != null)
                    {
                        RemoveSignatureEffect(currentSignature); // <--- QUAN TRỌNG
                    }

                    Debug.Log($"Player trang bị Signature: {newSkill.skillName}");
                    currentSignature = newSkill;
                    ApplySignatureEffect(newSkill);
                }
                break;

            case SkillData.SkillType.Enemy:
                Debug.LogWarning("Player không thể học skill của Enemy!");
                break;
        }
    }

    // =========================================================
    // THÁO SKILL THEO TỪNG SLOT (PUBLIC — DevTool gọi)
    // =========================================================

    /// <summary>Tháo Default Passive đang được trang bị. Không làm gì nếu slot trống.</summary>
    public void UnequipDefaultPassive()
    {
        if (currentDefaultPassive == null)
        {
            Debug.Log("[SkillManager] UnequipDefaultPassive — slot đang trống.");
            return;
        }
        Debug.Log($"[SkillManager] Tháo Default Passive: {currentDefaultPassive.skillName}");
        RemovePassiveEffect(currentDefaultPassive);
        currentDefaultPassive = null;
    }

    /// <summary>Tháo Passive Slot 1 đang được trang bị. Không làm gì nếu slot trống.</summary>
    public void UnequipPassive1()
    {
        if (currentPassive1 == null)
        {
            Debug.Log("[SkillManager] UnequipPassive1 — slot đang trống.");
            return;
        }
        Debug.Log($"[SkillManager] Tháo Passive 1: {currentPassive1.skillName}");
        RemovePassiveEffect(currentPassive1);
        currentPassive1 = null;
    }

    /// <summary>Tháo Passive Slot 2 đang được trang bị. Không làm gì nếu slot trống.</summary>
    public void UnequipPassive2()
    {
        if (currentPassive2 == null)
        {
            Debug.Log("[SkillManager] UnequipPassive2 — slot đang trống.");
            return;
        }
        Debug.Log($"[SkillManager] Tháo Passive 2: {currentPassive2.skillName}");
        RemovePassiveEffect(currentPassive2);
        currentPassive2 = null;
    }

    /// <summary>Tháo Skill (slot E/Q) đang được trang bị. Không làm gì nếu slot trống.</summary>
    public void UnequipSkillSlot()
    {
        if (currentSkill == null)
        {
            Debug.Log("[SkillManager] UnequipSkillSlot — slot đang trống.");
            return;
        }
        Debug.Log($"[SkillManager] Tháo Skill: {currentSkill.skillName}");
        RemoveSkillEffect(currentSkill);
        currentSkill = null;
    }

    /// <summary>Tháo Signature (slot R) đang được trang bị. Không làm gì nếu slot trống.</summary>
    public void UnequipSignatureSlot()
    {
        if (currentSignature == null)
        {
            Debug.Log("[SkillManager] UnequipSignatureSlot — slot đang trống.");
            return;
        }
        Debug.Log($"[SkillManager] Tháo Signature: {currentSignature.skillName}");
        RemoveSignatureEffect(currentSignature);
        currentSignature = null;
    }


    // =========================================================
    // LOGIC CHO ENEMY (LIST KHÔNG GIỚI HẠN)
    // =========================================================
    private void EquipEnemySkill(SkillData newSkill)
    {
        // Enemy không quan tâm type, cứ nhét vào list
        if (!enemySkills.Contains(newSkill))
        {
            enemySkills.Add(newSkill);
            Debug.Log($"Enemy đã học thêm skill: {newSkill.skillName}");
        }
    }

    private void UnequipEnemySkill(SkillData skillToRemove)
    {
        if (enemySkills.Contains(skillToRemove))
        {
            enemySkills.Remove(skillToRemove);
            Debug.Log($"Enemy đã quên skill: {skillToRemove.skillName}");
        }
    }
    // KHU VỰC THAY ĐỔI: Dùng Factory Pattern
    // ------------------------------------------------------------------

    private void ApplyPassiveEffect(SkillData skill)
    {
        if (skill == null) return;

        // 1. Cộng chỉ số tĩnh
        if (skill.passiveStats != null)
        {
            foreach (var mod in skill.passiveStats)
                ApplyModifier(mod, false);
        }

        // 2. Gắn Script Logic
        if (skill.passiveEffectCode != SkillData.PassiveEffectCode.None)
        {
            System.Type componentType = SkillFactory.GetPassiveComponentType(skill.passiveEffectCode);
            if (componentType != null && !activeSkills.ContainsKey(skill))
            {
                if (!AssertPlayerRefs("ApplyPassiveEffect", skill.skillName)) return;

                Debug.Log($">> Đang gắn script: {componentType.Name} vào Player");
                SkillBehavior behavior = (SkillBehavior)gameObject.AddComponent(componentType);
                behavior.Initialize(allyStats, skill, playerController);
                activeSkills.Add(skill, behavior);
            }
        }

        if (allyStats != null) allyStats.RecalculateStats();
    }

    private void ApplySkillEffect(SkillData skill)
    {
        if (skill == null) return;

        if (skill.skillEffectCode != SkillData.SkillEffectCode.None)
        {
            System.Type componentType = SkillFactory.GetSkillComponentType(skill.skillEffectCode);
            if (componentType != null && !activeSkills.ContainsKey(skill))
            {
                if (!AssertPlayerRefs("ApplySkillEffect", skill.skillName)) return;

                Debug.Log($">> Đang gắn script: {componentType.Name} vào Player");
                SkillBehavior behavior = (SkillBehavior)gameObject.AddComponent(componentType);
                behavior.Initialize(allyStats, skill, playerController);
                activeSkills.Add(skill, behavior);
            }
        }
    }

    private void ApplySignatureEffect(SkillData skill)
    {
        if (skill == null) return;

        if (skill.signatureEffectCode != SkillData.SignatureEffectCode.None)
        {
            System.Type componentType = SkillFactory.GetSignatureComponentType(skill.signatureEffectCode);
            if (componentType != null && !activeSkills.ContainsKey(skill))
            {
                if (!AssertPlayerRefs("ApplySignatureEffect", skill.skillName)) return;

                Debug.Log($">> Đang gắn script: {componentType.Name} vào Player");
                SkillBehavior behavior = (SkillBehavior)gameObject.AddComponent(componentType);
                behavior.Initialize(allyStats, skill, playerController);
                activeSkills.Add(skill, behavior);
            }
        }
    }

    /// <summary>
    /// Guard helper: kiểm tra allyStats và playerController trước khi gọi Initialize.
    /// Trả về false (và log lỗi rõ ràng) nếu thiếu — thay vì throw NullReferenceException.
    /// </summary>
    private bool AssertPlayerRefs(string context, string skillName)
    {
        if (allyStats == null || playerController == null)
        {
            Debug.LogError($"[SkillManager] {context} '{skillName}' thất bại: " +
                           $"allyStats={allyStats != null} playerController={playerController != null}. " +
                           "→ Hãy tick 'Is Player = true' trên SkillManager của Player GameObject.");
            return false;
        }
        return true;
    }

    private void RemovePassiveEffect(SkillData skill)
    {
        if (skill == null) return;

        // 1. Trừ chỉ số tĩnh
        if (skill.passiveStats != null)
        {
            foreach (var mod in skill.passiveStats)
            {
                ApplyModifier(mod, true);
            }
        }

        // 2. Hủy Script Logic
        if (activeSkills.ContainsKey(skill))
        {
            SkillBehavior behaviorToRemove = activeSkills[skill];

            // Gọi hàm Terminate để script tự dọn dẹp và tự hủy
            if (behaviorToRemove != null) behaviorToRemove.Terminate();

            // Xóa khỏi danh sách quản lý
            activeSkills.Remove(skill);
        }

        if (allyStats != null) allyStats.RecalculateStats();
    }
    private void RemoveSkillEffect(SkillData skill)
    {
        if (skill == null) return;
        // 2. Hủy Script Logic
        if (activeSkills.ContainsKey(skill))
        {
            SkillBehavior behaviorToRemove = activeSkills[skill];

            // Gọi hàm Terminate để script tự dọn dẹp và tự hủy
            if (behaviorToRemove != null) behaviorToRemove.Terminate();

            // Xóa khỏi danh sách quản lý
            activeSkills.Remove(skill);
        }
    }
    private void RemoveSignatureEffect(SkillData skill)
    {
        if (skill == null) return;
        // 2. Hủy Script Logic
        if (activeSkills.ContainsKey(skill))
        {
            SkillBehavior behaviorToRemove = activeSkills[skill];

            // Gọi hàm Terminate để script tự dọn dẹp và tự hủy
            if (behaviorToRemove != null) behaviorToRemove.Terminate();

            // Xóa khỏi danh sách quản lý
            activeSkills.Remove(skill);
        }
    }
    void ApplyModifier(StatModifier mod, bool isReversing)
    {
        if (allyStats == null)
        {
            Debug.LogError("[SkillManager] ApplyModifier: allyStats là null. " +
                           "Hãy tick 'Is Player = true' trên SkillManager của Player.");
            return;
        }

        float value = mod.GetFinalValue();
        if (isReversing) value = -value;

        switch (mod.stat)
        {
            case StatModifier.StatType.STR: allyStats.flatSTR += value; break;
            case StatModifier.StatType.DEX: allyStats.flatDEX += value; break;
            case StatModifier.StatType.INT: allyStats.flatINT += value; break;
            case StatModifier.StatType.VIT: allyStats.flatVIT += value; break;
            case StatModifier.StatType.AGI: allyStats.flatAGI += value; break;
            case StatModifier.StatType.BonusSTR: allyStats.bonusSTR += value; break;
            case StatModifier.StatType.BonusDEX: allyStats.bonusDEX += value; break;
            case StatModifier.StatType.BonusINT: allyStats.bonusINT += value; break;
            case StatModifier.StatType.BonusVIT: allyStats.bonusVIT += value; break;
            case StatModifier.StatType.BonusAGI: allyStats.bonusAGI += value; break;

            case StatModifier.StatType.FlatHP: allyStats.flatHp += value; break;
            case StatModifier.StatType.BonusHP: allyStats.bonusHp += value; break;

            case StatModifier.StatType.FlatPhysicalAtk: allyStats.flatPhysicalAtk += value; break;
            case StatModifier.StatType.FlatMagicAtk: allyStats.flatMagicAtk += value; break;
            case StatModifier.StatType.BonusPhysicalAtk: allyStats.bonusPhysicalAtk += value; break;
            case StatModifier.StatType.BonusMagicAtk: allyStats.bonusMagicAtk += value; break;
            case StatModifier.StatType.CritChance: allyStats.bonusCritChance += value; break;
            case StatModifier.StatType.CritMultiplier: allyStats.bonusCritMultiplier += value; break;
            case StatModifier.StatType.Armor: allyStats.armor += value; break;
            case StatModifier.StatType.MagicResist: allyStats.magicResist += value; break;
            case StatModifier.StatType.BonusMoveSpeed: allyStats.bonusMoveSpeed += value; break;
            case StatModifier.StatType.BonusCDR: allyStats.bonusCdr += value; break;
            case StatModifier.StatType.DefenseValue: allyStats.defenseValue += value; break;
            case StatModifier.StatType.PhysicalLifeSteal: allyStats.physicalLifeSteal += value; break;
            case StatModifier.StatType.MagicLifeSteal: allyStats.magicLifeSteal += value; break;
            case StatModifier.StatType.KnockBackRes: allyStats.resistanceKnockBack += value; break;
            case StatModifier.StatType.EffectRes: allyStats.resistanceEffect += value; break;

                // ... Thêm các case cho các chỉ số khác
        }
    }

    // Active Skill 
    // --- [MỚI] HÀM DÙNG SKILL (Được gọi từ PlayerController) ---
    public void CastSkill(SkillData skillData)
    {
        if (skillData == null) return;

        // Tìm xem skill này có script nào đang chạy không
        if (activeSkills.ContainsKey(skillData))
        {
            SkillBehavior script = activeSkills[skillData];

            // Gọi hàm Use() của script đó
            if (script != null)
            {
                // [ĐÃ SỬA] Hứng kết quả true/false từ hàm Use()
                bool isSuccess = script.Use();
                // Nếu dùng chiêu thành công (Đủ điều kiện, trừ Sin xong)
                if (isSuccess)
                {
                    // Thông báo dùng skill thành công
                    Debug.Log($"<color=cyan>>> Kích hoạt {skillData.skillName}</color>");

                    // Gọi trực tiếp sang EffectManager
                    EquipmentEffectManager effectManager = GetComponentInChildren<EquipmentEffectManager>();
                    if (effectManager != null)
                    {
                        // [BÍ QUYẾT Ở ĐÂY]: Truyền thẳng cái Type (E hay Q) sang cho EffectManager
                        effectManager.TriggerSkillCastEffects(skillData.skillType);
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"Skill {skillData.skillName} có trong slot nhưng chưa được gắn Script!");
        }
    }
    // =========================================================
    // TIỆN ÍCH (HELPER)
    // =========================================================

    // [MỚI] Giảm thời gian hồi chiêu cho toàn bộ kỹ năng đang trang bị
    public void ReduceAllCooldowns(float amount)
    {
        if (!isPlayer) return; // Chỉ áp dụng cho Player

        // Duyệt qua tất cả các script kỹ năng đang chạy (Cả chiêu E và chiêu Q)
        foreach (var kvp in activeSkills)
        {
            SkillBehavior behavior = kvp.Value;
            if (behavior != null)
            {
                // Yêu cầu từng skill tự giảm thời gian đếm ngược của nó
                behavior.ReduceCooldown(amount);
            }
        }
    }

    // Hàm này để Enemy AI gọi ngẫu nhiên 1 skill để đánh
    public SkillData GetRandomEnemySkill()
    {
        if (!isPlayer && enemySkills.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, enemySkills.Count);
            return enemySkills[index];
        }
        return null;
    }

    // ... (Cuối file SkillManager.cs) ...

    // [MỚI] Hàm để UI lấy script đang chạy
    public SkillBehavior GetActiveSkillBehavior(SkillData data)
    {
        if (data == null) return null;
        if (activeSkills.ContainsKey(data))
        {
            return activeSkills[data];
        }
        return null;
    }
}