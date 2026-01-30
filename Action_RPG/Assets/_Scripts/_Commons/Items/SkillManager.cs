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
                if (currentDefaultPassive != newSkill)
                {
                    Debug.Log($"Trang bị Default Passive: {newSkill.skillName}");
                    currentDefaultPassive = newSkill;
                    // TODO: Cộng chỉ số Default Passive mới
                }
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
                    // TODO: Cộng chỉ số
                }
                else if (currentPassive2 == null)
                {
                    Debug.Log($"Trang bị Passive vào Slot 2: {newSkill.skillName}");
                    currentPassive2 = newSkill;
                    // TODO: Cộng chỉ số
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
                    Debug.Log($"Player trang bị Skill: {newSkill.skillName}");
                    currentSkill = newSkill;
                }
                break;

            case SkillData.SkillType.Signature:
                if (currentSignature != newSkill)
                {
                    Debug.Log($"Player trang bị Signature: {newSkill.skillName}");
                    currentSignature = newSkill;
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
}