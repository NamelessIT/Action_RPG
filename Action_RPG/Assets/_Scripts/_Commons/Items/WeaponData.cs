using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Inventory/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public enum WeaponType
    {
        Hand,
        Sword,
        Dagger,
        Bow,
        Spear,
        Staff,
        GreatSword,
        Grimoire,
    }
    public enum WeaponAtkType
    { 
        Physical,
        Magic,
    }
    public enum Rarity
    {
        Residual_1,
        Stained_2,
        Corrupted_3,
        Condemned_4,
        Anomalous_5
    }
    public string id;
    public WeaponType weaponType;
    public WeaponAtkType weaponAtkType;

    public string weaponName;
    [TextArea] public string lore;
    [TextArea] public string description;
    public Sprite icon;
    public Rarity rarity;

    [Header("Base Stats")]
    public float baseAtk;
    public float baseAttackSpeed;
    public float moveFlexibility;
    public float defenseValue;
    public float bonusCritChance;

    [Header("Substats")]
    // Đổi từ string sang List để Inspector hiển thị đẹp và code đọc được luôn
    public List<StatModifier> substats = new List<StatModifier>();

    [Header("Requirements")]
    public float reqStr;
    public float reqDex;
    public float reqInt;
    public float reqVit;
    public float reqAgi;

    public bool playerOnly;
    // public string effectConfig; // Để sau

    // Hàm này chạy ngay lập tức khi bạn chỉnh sửa trong Inspector
    private void OnValidate()
    {
        if (substats != null)
        {
            foreach (var sub in substats)
            {
                // Nếu thấy powerMod bằng 0 (do Unity tạo mặc định), tự sửa thành 1
                // Lưu ý: Dùng 0f để so sánh float chuẩn xác
                if (sub.powerMod == 0f)
                {
                    sub.powerMod = 1.0f;
                }
            }
        }
    }
}