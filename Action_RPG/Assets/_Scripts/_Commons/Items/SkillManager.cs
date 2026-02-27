using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{

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
                        RemoveSkillEffect(currentSignature); // <--- QUAN TRỌNG
                    }

                    Debug.Log($"Player trang bị Signature: {newSkill.skillName}");
                    currentSignature = newSkill;
                    ApplySkillEffect(newSkill);
                }
                break;

            case SkillData.SkillType.Enemy:
                Debug.LogWarning("Player không thể học skill của Enemy!");
                break;
        }
    }

    //private void UnequipPlayerSkill(SkillData skillToRemove)
    //{
    //    // Kiểm tra xem skill muốn tháo đang nằm ở slot nào
    //    if (currentDefaultPassive == skillToRemove)
    //    {
    //        Debug.Log($"Tháo Default Passive: {skillToRemove.skillName}");
    //        currentDefaultPassive = null;
    //        // TODO: Nếu Passive có cộng chỉ số, nhớ trừ ra
    //    }
    //    else if (currentPassive == skillToRemove)
    //    {
    //        Debug.Log($"Tháo Passive: {skillToRemove.skillName}");
    //        currentPassive = null;
    //        // TODO: Nếu Passive có cộng chỉ số, nhớ trừ ra
    //    }
    //    else if (currentSkill == skillToRemove)
    //    {
    //        Debug.Log($"Tháo Skill: {skillToRemove.skillName}");
    //        currentSkill = null;
    //    }
    //    else if (currentSignature == skillToRemove)
    //    {
    //        Debug.Log($"Tháo Signature: {skillToRemove.skillName}");
    //        currentSignature = null;
    //    }
    //}


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

        // 1. Cộng chỉ số tĩnh (List Stats trong Data)
        if (skill.passiveStats != null)
        {
            foreach (var mod in skill.passiveStats)
            {
                ApplyModifier(mod, false);
            }
        }

        // 2. Gắn Script Logic (Dựa trên Factory)
        if (skill.passiveEffectCode != SkillData.PassiveEffectCode.None)
        {
            // Hỏi Factory: "Skill này dùng script nào?" (VD: BattleHardened -> ChrisPassive)
            System.Type componentType = SkillFactory.GetPassiveComponentType(skill.passiveEffectCode);

            if (componentType != null)
            {
                if (!activeSkills.ContainsKey(skill))
                {
                    Debug.Log($">> Đang gắn script: {componentType.Name} vào Player");

                    // AddComponent: Gắn script đó vào GameObject Player
                    SkillBehavior behavior = (SkillBehavior)gameObject.AddComponent(componentType);

                    // Khởi tạo (Truyền stats và data vào cho script con dùng)
                    behavior.Initialize(allyStats, skill, playerController);

                    // Lưu vào danh sách để quản lý
                    activeSkills.Add(skill, behavior);
                }
            }
        }

        if (allyStats != null) allyStats.RecalculateStats();
    }
    private void ApplySkillEffect (SkillData skill)
    {
        if (skill == null) return;
        // 2. Gắn Script Logic (Dựa trên Factory)
        if (skill.skillEffectCode != SkillData.SkillEffectCode.None)
        {
            // Hỏi Factory: "Skill này dùng script nào?" (VD: BattleHardened -> ChrisPassive)
            System.Type componentType = SkillFactory.GetSkillComponentType(skill.skillEffectCode);

            if (componentType != null)
            {
                if (!activeSkills.ContainsKey(skill))
                {
                    Debug.Log($">> Đang gắn script: {componentType.Name} vào Player");

                    // AddComponent: Gắn script đó vào GameObject Player
                    SkillBehavior behavior = (SkillBehavior)gameObject.AddComponent(componentType);

                    // Khởi tạo (Truyền stats và data vào cho script con dùng)
                    behavior.Initialize(allyStats, skill, playerController);

                    // Lưu vào danh sách để quản lý
                    activeSkills.Add(skill, behavior);
                }
            }
        }
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
    void ApplyModifier(StatModifier mod, bool isReversing)
    {
        float value = mod.GetFinalValue();
        if (isReversing) value = -value; // Nếu tháo đồ thì trừ đi

        switch (mod.stat)
        {
            case StatModifier.StatType.STR: allyStats.STR += value; break;
            case StatModifier.StatType.DEX: allyStats.DEX += value; break;
            case StatModifier.StatType.INT: allyStats.INT += value; break;
            case StatModifier.StatType.VIT: allyStats.VIT += value; break;
            case StatModifier.StatType.AGI: allyStats.AGI += value; break;
            case StatModifier.StatType.BonusSTR: allyStats.STR += value; break;
            case StatModifier.StatType.BonusDEX: allyStats.DEX += value; break;
            case StatModifier.StatType.BonusINT: allyStats.INT += value; break;
            case StatModifier.StatType.BonusVIT: allyStats.VIT += value; break;
            case StatModifier.StatType.BonusAGI: allyStats.AGI += value; break;

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
                script.Use();
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

    // Hàm này để Enemy AI gọi ngẫu nhiên 1 skill để đánh
    public SkillData GetRandomEnemySkill()
    {
        if (!isPlayer && enemySkills.Count > 0)
        {
            int index = Random.Range(0, enemySkills.Count);
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