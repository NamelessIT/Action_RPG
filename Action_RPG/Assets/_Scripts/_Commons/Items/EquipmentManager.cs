using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    // Tham chiếu đến Stats của nhân vật
    private AllyStats allyStats;

    // Các ô trang bị hiện tại
    public WeaponData currentWeapon;
    public WeaponData pickUpWeapon;
    public AccessoryData currentAccessory;
    // public CoreShieldData currentShield; ...

    void Start()
    {
        allyStats = GetComponent<AllyStats>();
    }

    // Hàm mặc vũ khí
    public void EquipWeapon(WeaponData newWeapon)
    {
        // 1. Nếu đang cầm vũ khí cũ -> Tháo ra (Trừ chỉ số)
        //if (currentWeapon != null)
        //{
            //Debug.Log("Tháo vũ khí ra");
            //UnequipWeapon();
        //}

        // 2. Gán vũ khí mới
        currentWeapon = newWeapon;

        // 3. Cộng chỉ số từ vũ khí mới vào AllyStats
        // Duyệt qua danh sách StatModifier trong WeaponData
        if (newWeapon.substats != null)
        {
            foreach (StatModifier mod in newWeapon.substats)
            {
                ApplyModifier(mod, false); // false = cộng vào
            }
        }

        // Cập nhật các chỉ số cơ bản riêng của vũ khí (Base Atk, Flex...)
        if (newWeapon.weaponAtkType == WeaponData.WeaponAtkType.Physical) 
        {
            allyStats.flatPhysicalAtk += newWeapon.baseAtk; // Ví dụ cộng vào flat
            Debug.Log("cộng thêm chỉ số vào flatPhysicalAtk: " + allyStats.flatPhysicalAtk + " với baseAtk vũ khí là : "+ newWeapon.baseAtk);
        }
        else
        {
            allyStats.flatMagicAtk += newWeapon.baseAtk; // Ví dụ cộng vào flat
            Debug.Log("cộng thêm chỉ số vào flatMagicAtk: " + allyStats.flatMagicAtk + " với baseAtk vũ khí là : " + newWeapon.baseAtk);
        }
        allyStats.baseAttackSpeed = newWeapon.baseAttackSpeed;
        allyStats.moveFlexibility = newWeapon.moveFlexibility;
        allyStats.defenseValue += newWeapon.defenseValue;
        allyStats.bonusCritChance += newWeapon.bonusCritChance;

        // 4. Tính toán lại toàn bộ chỉ số
        allyStats.RecalculateStats();

        Debug.Log($"Đã trang bị: {newWeapon.weaponName}");
    }

    // Hàm tháo vũ khí
    public void UnequipWeapon()
    {
        // Sau này sửa các chỗ null thành id của Hand
        if (currentWeapon == null) return;

        // Trừ chỉ số Substats
        if (currentWeapon.substats != null)
        {
            foreach (StatModifier mod in currentWeapon.substats)
            {
                ApplyModifier(mod, true); // true = trừ ra (đảo ngược)
            }
        }

        // Trừ chỉ số Base
        if (currentWeapon.weaponAtkType == WeaponData.WeaponAtkType.Physical)
        {
            allyStats.flatPhysicalAtk -= currentWeapon.baseAtk;
            Debug.Log("Đã trừ flatPhysicalAtk: " + allyStats.flatPhysicalAtk + " với baseAtk vũ khí là : " + currentWeapon.baseAtk);
        }
        else
        {
            allyStats.flatMagicAtk -= currentWeapon.baseAtk; 
            Debug.Log("cộng thêm chỉ số vào flatMagicAtk: " + allyStats.flatMagicAtk + " với baseAtk vũ khí là : " + currentWeapon.baseAtk);
        }
        allyStats.moveFlexibility = 0; // Reset về 0 hoặc giá trị mặc định
        allyStats.defenseValue -= currentWeapon.defenseValue;
        allyStats.bonusCritChance -= currentWeapon.bonusCritChance;
        currentWeapon = null; //Sau này sửa null thành id của hand
        //allyStats.baseAttackSpeed = null.baseAttackSpeed; sửa attackSpeed thành attackSpeed của hand

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