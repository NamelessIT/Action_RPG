using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AttackEffect
{
    public bool causesKnockback;
    public float knockbackForce = 10f;
    public bool causesStun;
    public float stunDuration = 1.0f;
}

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
        Both,
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

    // --- [MỚI] THÊM THÔNG SỐ TẦM ĐÁNH ---
    [Header("Combat Reach")]
    [Tooltip("Tầm đánh thực tế trong game (Tính bằng mét)")]
    public float attackRange;
    [Tooltip("Vũ khí đánh xa (Tạo đạn/Projectile) hay Cận chiến?")]
    public bool isRanged;
    // [MỚI] Chứa Prefab của viên đạn (Mũi tên, Cầu phép...)
    public GameObject projectilePrefab;

    [Header("Substats")]
    public List<StatModifier> substats = new List<StatModifier>();

    [Header("Requirements")]
    public float reqStr;
    public float reqDex;
    public float reqInt;
    public float reqVit;
    public float reqAgi;

    public bool playerOnly;

    [Header("Combo Effects")]
    [Tooltip("Index 0 = Đòn 1, Index 1 = Đòn 2...")]
    public List<AttackEffect> comboEffects;

    private void OnValidate()
    {
        // 1. Tự động set Substats powerMod
        if (substats != null)
        {
            foreach (var sub in substats)
            {
                if (sub.powerMod == 0f)
                {
                    sub.powerMod = 1.0f;
                }
            }
        }

        // 2. [MỚI] TỰ ĐỘNG GÁN TẦM ĐÁNH THEO LOẠI VŨ KHÍ
        // Chỉ tự động gán nếu attackRange đang bằng 0 (chưa thiết lập)
        // Nhờ vậy, nếu bạn cố tình chỉnh tay một cây kiếm dài 3m, code sẽ không đè lại thành 2m.
        if (attackRange == 0f)
        {
            switch (weaponType)
            {
                case WeaponType.Hand: attackRange = 1.0f; isRanged = false; break;
                case WeaponType.Dagger: attackRange = 1.2f; isRanged = false; break;
                case WeaponType.Sword: attackRange = 2.0f; isRanged = false; break;
                case WeaponType.GreatSword: attackRange = 2.5f; isRanged = false; break;
                case WeaponType.Spear: attackRange = 3.5f; isRanged = false; break;
                case WeaponType.Staff: attackRange = 3.0f; isRanged = true; break; // Đánh xa vừa
                case WeaponType.Grimoire: attackRange = 8.0f; isRanged = true; break; // Đánh xa
                case WeaponType.Bow: attackRange = 15.0f; isRanged = true; break; // Xa nhất
            }
        }
    }
}