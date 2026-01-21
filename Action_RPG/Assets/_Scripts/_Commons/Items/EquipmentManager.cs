using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    private AllyStats allyStats;
    private WeaponData baseWeapon; // Vũ khí mặc định (Hand)

    public WeaponData pickUpWeapon;
    public WeaponData currentWeapon;
    public AccessoryData currentAccessory;

    void Start()
    {
        allyStats = GetComponent<AllyStats>();
        InitializeBaseWeapon(); // Khởi tạo vũ khí mặc định
    }

    // 1. Tự động load vũ khí mặc định từ Resources
    void InitializeBaseWeapon()
    {
        // Đường dẫn file trong thư mục Resources (bỏ đuôi .asset)
        // Ví dụ: Assets/Resources/Weapons/WPN_H_T1_01.asset -> Load "Weapons/WPN_H_T1_01"
        // Bạn hãy điều chỉnh pathString cho đúng với project của bạn
        string path = "Datas/Weapons/WPN_H_T1_01";

        baseWeapon = Resources.Load<WeaponData>(path);

        if (baseWeapon == null)
        {
            Debug.LogError($"CRITICAL ERROR: Không tìm thấy Base Weapon tại Resources/{path} !!");
            return;
        }

        // Mặc định ban đầu là tay không
        // Gọi hàm Equip nội bộ để không bị log "Tháo vũ khí"
        EquipInternal(baseWeapon);
    }

    // --- HÀM CHÍNH CHO BÊN NGOÀI GỌI (Public) ---

    // 2. Trang bị vũ khí mới (Logic: Tháo cũ -> Đeo mới)
    public void EquipWeapon(WeaponData newWeapon)
    {
        if (newWeapon == null)
        {
            Debug.Log("Không trang vũ khí");
            return;
        }
        if (currentWeapon == newWeapon)
        {
            Debug.Log("Đã trang bị vũ khí này");
            return;
        }// Đang cầm rồi thì thôi

        // Nếu đang cầm vũ khí (khác null), tháo nó ra trước
        if (currentWeapon != null)
        {
            Debug.Log("Tháo vũ khí hiện tại");
            UnequipWeaponInternal(currentWeapon);
        }

        // Đeo vũ khí mới vào
        
        EquipInternal(newWeapon);
    }

    // 3. Reset về tay không (Gọi khi muốn "Cởi đồ")
    public void ResetToBaseWeapon()
    {
        if (pickUpWeapon != null && currentWeapon != baseWeapon)
        {
            UnequipWeaponInternal(currentWeapon);
        }

        // Chỉ đeo lại tay không nếu chưa đeo
        if (currentWeapon != baseWeapon)
        {
            EquipInternal(baseWeapon);
        }
    }

    // --- CÁC HÀM NỘI BỘ (Private) ĐỂ XỬ LÝ CHỈ SỐ ---

    // Chỉ tháo và trừ chỉ số (Không tự gán vũ khí mới)
    private void UnequipWeaponInternal(WeaponData weaponToRemove)
    {
        Debug.Log($"Tháo vũ khí: {weaponToRemove.weaponName}");

        // Trừ Substats
        if (weaponToRemove.substats != null)
        {
            foreach (StatModifier mod in weaponToRemove.substats)
            {
                ApplyModifier(mod, true); // true = trừ ra
            }
        }

        // Trừ Base Stats
        if (weaponToRemove.weaponAtkType == WeaponData.WeaponAtkType.Physical)
        {
            allyStats.flatPhysicalAtk -= weaponToRemove.baseAtk;
        }
        else
        {
            allyStats.flatMagicAtk -= weaponToRemove.baseAtk;
        }

        allyStats.defenseValue -= weaponToRemove.defenseValue;
        allyStats.bonusCritChance -= weaponToRemove.bonusCritChance;

        // Reset các biến dạng override (Gán đè)
        // Lưu ý: Các biến này sẽ được set lại khi EquipInternal(baseWeapon) chạy ngay sau đó
        allyStats.moveFlexibility = 0;

        // Xóa tham chiếu
        currentWeapon = null;
        allyStats.RecalculateStats();
    }

    // Chỉ cộng chỉ số và gán biến
    private void EquipInternal(WeaponData newWeapon)
    {
        Debug.Log($"Trang bị: {newWeapon.weaponName}");
        currentWeapon = newWeapon;

        // Cộng Substats
        if (newWeapon.substats != null)
        {
            foreach (StatModifier mod in newWeapon.substats)
            {
                ApplyModifier(mod, false); // false = cộng vào
            }
        }

        // Cộng Base Stats
        if (newWeapon.weaponAtkType == WeaponData.WeaponAtkType.Physical)
        {
            allyStats.flatPhysicalAtk += newWeapon.baseAtk;
        }
        else
        {
            allyStats.flatMagicAtk += newWeapon.baseAtk;
        }

        allyStats.baseAttackSpeed = newWeapon.baseAttackSpeed;
        allyStats.moveFlexibility = newWeapon.moveFlexibility;
        allyStats.defenseValue += newWeapon.defenseValue; // Sửa lỗi cú pháp: dùng baseDefenseValue
        allyStats.bonusCritChance += newWeapon.bonusCritChance;

        allyStats.RecalculateStats();
    }

    // Hàm xử lý cộng/trừ StatModifier vào các biến "Bonus" trong AllyStats
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

                // ... Thêm các case cho các chỉ số khác
        }
    }
}