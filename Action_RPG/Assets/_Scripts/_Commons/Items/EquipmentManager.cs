using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    private AllyStats allyStats;
    //Weapon
    [Header("Weapon")]
    private WeaponData baseWeapon; // Vũ khí mặc định (Hand)
    public WeaponData pickUpWeapon;
    public WeaponData currentWeapon;
    //Core Shield
    [Header("Core Shield")]
    public CoreShieldData pickUpCoreShield;
    public CoreShieldData currentCoreShield;

    //Accessory
    [Header("Accessories Slots")]
    public AccessoryData pickUpAccessory;
    [Header("Core Shard")]
    //public AccessoryData pickUpCoreShard;
    public AccessoryData currentCoreShard;
    [Header("Mark of Sin")]
    //public AccessoryData pickUpMarkOfSin;
    public AccessoryData currentMarkOfSin;
    [Header("Relic of Memory")]
    //public AccessoryData pickUpRelicOfMemory;
    public AccessoryData currentRelicOfMemory;
    [Header("Parasite")]
    //public AccessoryData pickUpParasite;
    public AccessoryData currentParasite;
    [Header("Chain")]
    //public AccessoryData pickUpChain;
    public AccessoryData currentChain;

    void Start()
    {
        allyStats = GetComponent<AllyStats>();
        InitializeBaseWeapon(); // Khởi tạo vũ khí mặc định
    }

    // ---------------Weapon---------------
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

    // ---------------Core Shield---------------
    // 1. Trang bị Core Shield mới
    public void EquipCoreShield(CoreShieldData newShield)
    {
        if (newShield == null)
        {
            Debug.Log("Core Shield không hợp lệ (null)");
            return;
        }

        if (currentCoreShield == newShield)
        {
            Debug.Log("Đã trang bị Core Shield này rồi");
            return;
        }

        // Nếu đang đeo khiên cũ -> Tháo ra trước
        if (currentCoreShield != null)
        {
            UnequipCoreShieldInternal(currentCoreShield);
        }

        // Đeo khiên mới vào
        EquipCoreShieldInternal(newShield);
    }

    // 2. Tháo Core Shield (Về trạng thái trống)
    public void UnequipCoreShield()
    {
        if (currentCoreShield != null)
        {
            UnequipCoreShieldInternal(currentCoreShield);
            // KHÁC BIỆT: Không gọi EquipInternal(baseShield) vì Shield tháo ra là hết
        }
        else
        {
            Debug.Log("Không có Core Shield nào để tháo");
        }
    }
    private void EquipCoreShieldInternal(CoreShieldData newShield)
    {
        Debug.Log($"Trang bị Shield: {newShield.coreShieldName}");
        currentCoreShield = newShield;

        // 1. Cộng Substats (Dùng lại hàm ApplyModifier có sẵn)
        if (newShield.substats != null)
        {
            foreach (StatModifier mod in newShield.substats)
            {
                ApplyModifier(mod, false); // false = cộng vào
            }
        }

        // 2. Cộng Base Stats (Riêng của Shield)
        allyStats.armor += newShield.armor;
        allyStats.magicResist += newShield.magicResist;

        // 3. Tính lại chỉ số tổng
        allyStats.RecalculateStats();
    }

    private void UnequipCoreShieldInternal(CoreShieldData shieldToRemove)
    {
        Debug.Log($"Tháo Shield: {shieldToRemove.coreShieldName}");

        // 1. Trừ Substats
        if (shieldToRemove.substats != null)
        {
            foreach (StatModifier mod in shieldToRemove.substats)
            {
                ApplyModifier(mod, true); // true = trừ ra
            }
        }

        // 2. Trừ Base Stats
        allyStats.armor -= shieldToRemove.armor;
        allyStats.magicResist -= shieldToRemove.magicResist;

        // 3. Reset biến và tính lại
        currentCoreShield = null; // Quan trọng: Đưa về null để biểu thị không đeo gì
        allyStats.RecalculateStats();
    }

    // --------------- ACCESSORY (5 SLOTS) ---------------
    // 1. Trang bị Accessory (Tự động nhận diện slot dựa trên Type)
    public void EquipAccessory(AccessoryData newAccessory)
    {
        if (newAccessory == null)
        {
            Debug.Log("Accessory không hợp lệ (null)");
            return;
        }

        // Lấy ra món đồ đang đeo ở slot tương ứng (nếu có) để so sánh
        AccessoryData currentInSlot = GetCurrentAccessoryByType(newAccessory.accessoryType);

        if (currentInSlot == newAccessory)
        {
            Debug.Log($"Đã trang bị {newAccessory.AccessoryName} ở slot {newAccessory.accessoryType} rồi.");
            return;
        }

        // Nếu slot đó đang có đồ cũ -> Tháo ra trước
        if (currentInSlot != null)
        {
            UnequipAccessoryInternal(currentInSlot);
        }

        // Đeo đồ mới vào
        EquipAccessoryInternal(newAccessory);
    }

    // 2. Tháo một Accessory cụ thể
    public void UnequipAccessory(AccessoryData accToRemove)
    {
        if (accToRemove == null) return;

        // Kiểm tra xem món đồ này có thực sự đang được đeo không
        AccessoryData currentInSlot = GetCurrentAccessoryByType(accToRemove.accessoryType);

        if (currentInSlot == accToRemove)
        {
            UnequipAccessoryInternal(accToRemove);
        }
        else
        {
            Debug.Log("Không thể tháo: Món đồ này hiện không được trang bị.");
        }
    }

    // --- HÀM TRỢ GIÚP ---

    // Hàm này giúp lấy biến slot tương ứng dựa trên Enum Type
    private AccessoryData GetCurrentAccessoryByType(AccessoryData.AccessoryType type)
    {
        switch (type)
        {
            case AccessoryData.AccessoryType.CoreShard: return currentCoreShard;
            case AccessoryData.AccessoryType.MarkOfSin: return currentMarkOfSin;
            case AccessoryData.AccessoryType.RelicOfMemory: return currentRelicOfMemory;
            case AccessoryData.AccessoryType.Parasite: return currentParasite;
            case AccessoryData.AccessoryType.Chain: return currentChain;
            default: return null;
        }
    }

    // Hàm gán biến slot thành null hoặc giá trị mới
    private void SetAccessorySlot(AccessoryData.AccessoryType type, AccessoryData data)
    {
        switch (type)
        {
            case AccessoryData.AccessoryType.CoreShard: currentCoreShard = data; break;
            case AccessoryData.AccessoryType.MarkOfSin: currentMarkOfSin = data; break;
            case AccessoryData.AccessoryType.RelicOfMemory: currentRelicOfMemory = data; break;
            case AccessoryData.AccessoryType.Parasite: currentParasite = data; break;
            case AccessoryData.AccessoryType.Chain: currentChain = data; break;
        }
    }

    // --- LOGIC CỘNG TRỪ CHỈ SỐ (INTERNAL) ---

    private void EquipAccessoryInternal(AccessoryData newAcc)
    {
        Debug.Log($"Trang bị Accessory [{newAcc.accessoryType}]: {newAcc.AccessoryName}");

        // 1. Gán vào slot
        SetAccessorySlot(newAcc.accessoryType, newAcc);

        // 2. Cộng Substats
        if (newAcc.substats != null)
        {
            foreach (StatModifier mod in newAcc.substats)
            {
                ApplyModifier(mod, false); // false = cộng vào
            }
        }

        // Accessory thường không có Base Stats cố định như Armor/Atk riêng lẻ 
        // mà phụ thuộc hoàn toàn vào Substats, nên ta chỉ cần tính lại Stats.
        // Nếu sau này Accessory có base stat, bạn cộng ở đây giống Shield.

        allyStats.RecalculateStats();
    }

    private void UnequipAccessoryInternal(AccessoryData accToRemove)
    {
        Debug.Log($"Tháo Accessory [{accToRemove.accessoryType}]: {accToRemove.AccessoryName}");

        // 1. Trừ Substats
        if (accToRemove.substats != null)
        {
            foreach (StatModifier mod in accToRemove.substats)
            {
                ApplyModifier(mod, true); // true = trừ ra
            }
        }

        // 2. Gán slot về null
        SetAccessorySlot(accToRemove.accessoryType, null);

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