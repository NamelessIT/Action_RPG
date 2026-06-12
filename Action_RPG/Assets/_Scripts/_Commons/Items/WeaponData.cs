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

    /// <summary>
    /// Chế độ đánh thường.
    /// Default: arc sweep (Sword/Greatsword/Dagger).
    /// Thrust: đâm thẳng (Spear).
    /// Directional: đạn bay theo hướng nhìn (Bow/Grimoire mặc định).
    /// TargetSeek: tự tìm mục tiêu gần nhất trong góc hẹp (Staff).
    /// </summary>
    public enum AttackMode
    {
        Default,        // Arc sweep — Sword, Greatsword, Dagger
        Thrust,         // Đâm thẳng — Spear
        Directional,    // Đạn theo hướng nhìn — Bow, Grimoire (mặc định)
        TargetSeek,     // Tự tìm mục tiêu — Staff
    }

    /// <summary>
    /// Loại Heavy Attack đặc biệt theo vũ khí.
    /// Default = heavy thường (x2 dmg + knockback).
    /// </summary>
    public enum HeavyAttackType
    {
        Default          = 0,   // Heavy thường — Hand (200% dmg + knockback, dùng DefaultMeleeAttackHandler)
        DaggerSpin       = 1,   // Xoay dao 360° không knockback — Dagger
        GreatswordSlam   = 2,   // Bổ xuống thẳng — Greatsword
        BowPierce        = 3,   // Mũi tên xuyên thấu — Bow
        GrimoireStrike   = 4,   // Tia sét AoE từ trên trời — Grimoire
        StaffChannelSpin = 5,   // Xoay trượng liên tục (channeled) — Staff
        SpearThrust      = 6,   // Đâm thẳng lướt tới + xuyên thấu + stun nhẹ — Spear
        SwordDoubleSlash = 7,   // 2 nhát liên tiếp 1.5x dmg, knockback ở đòn 2 — Sword
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
    [Tooltip("Prefab đạn thường (mũi tên, cầu phép...)")]
    public GameObject projectilePrefab;

    // ─────────────────────────────────────────────────────────────
    //  ATTACK BEHAVIOR
    // ─────────────────────────────────────────────────────────────
    [Header("Attack Behavior")]
    [Tooltip("Chế độ đánh thường. Default = tự chọn theo weaponType.")]
    public AttackMode attackMode = AttackMode.Default;

    // ─────────────────────────────────────────────────────────────
    //  HEAVY ATTACK CONFIG
    // ─────────────────────────────────────────────────────────────
    [Header("Heavy Attack Config")]
    [Tooltip("Thời gian tối thiểu giữ chuột (giây) để kích hoạt Heavy Attack.")]
    public float heavyChargeTime = 1.0f;

    [Tooltip("Loại Heavy Attack đặc biệt theo vũ khí.")]
    public HeavyAttackType heavyAttackType = HeavyAttackType.Default;

    [Tooltip("Hệ số sát thương Heavy Attack (mặc định 2.0 = 200%).")]
    public float heavyDamageMultiplier = 2.0f;

    [Tooltip("Prefab đạn Heavy (Bow heavy xuyên thấu). Null = dùng projectilePrefab thường.")]
    public GameObject heavyProjectilePrefab;

    [Header("Substats")]
    public List<StatModifier> substats = new List<StatModifier>();

    [Header("Requirements")]
    public float reqStr;
    public float reqDex;
    public float reqInt;
    public float reqVit;
    public float reqAgi;

    public bool playerOnly;

    [Header("--- Unique Effect VFX ---")]
    [Tooltip("VFX văng ra khi hiệu ứng đặc biệt kích hoạt")]
    public GameObject triggerVfxPrefab;

    [Tooltip("VFX kéo dài đi theo người (Aura) nếu có")]
    public GameObject auraVfxPrefab;

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
                case WeaponType.Hand:      attackRange = 1.0f;  isRanged = false; break;
                case WeaponType.Dagger:    attackRange = 1.2f;  isRanged = false; break;
                case WeaponType.Sword:     attackRange = 2.0f;  isRanged = false; break;
                case WeaponType.GreatSword:attackRange = 2.5f;  isRanged = false; break;
                case WeaponType.Spear:     attackRange = 3.5f;  isRanged = false; break;
                case WeaponType.Staff:     attackRange = 3.0f;  isRanged = false; break; // TargetSeek melee
                case WeaponType.Grimoire:  attackRange = 8.0f;  isRanged = true;  break;
                case WeaponType.Bow:       attackRange = 15.0f; isRanged = true;  break;
            }
        }

        // attackMode KHÔNG tự động gán — để Default có nghĩa là "tự chọn theo weaponType tại runtime"
        // Dispatcher sẽ resolve Default → mode cụ thể khi SelectHandlers() được gọi.
        // Developer có thể override bất kỳ lúc nào bằng cách chỉnh attackMode trong Inspector.

        if (heavyAttackType == HeavyAttackType.Default)
        {
            switch (weaponType)
            {
                case WeaponType.Sword:
                    heavyAttackType = HeavyAttackType.SwordDoubleSlash;
                    heavyChargeTime = 0.8f;
                    // Sword: 2 nhát x1.5 mỗi nhát — chỉ set nếu multiplier còn mặc định
                    if (heavyDamageMultiplier <= 0f || heavyDamageMultiplier == 2.0f)
                        heavyDamageMultiplier = 1.5f;
                    break;
                case WeaponType.Spear:     heavyAttackType = HeavyAttackType.SpearThrust;      heavyChargeTime = 1.0f; break;
                case WeaponType.Dagger:    heavyAttackType = HeavyAttackType.DaggerSpin;       heavyChargeTime = 0.5f; break;
                case WeaponType.GreatSword:heavyAttackType = HeavyAttackType.GreatswordSlam;   heavyChargeTime = 1.5f; break;
                case WeaponType.Bow:       heavyAttackType = HeavyAttackType.BowPierce;        heavyChargeTime = 1.0f; break;
                case WeaponType.Grimoire:  heavyAttackType = HeavyAttackType.GrimoireStrike;   heavyChargeTime = 1.0f; break;
                case WeaponType.Staff:     heavyAttackType = HeavyAttackType.StaffChannelSpin; heavyChargeTime = 0.5f; break;
                default: /* Hand giữ Default (200% dmg + knockback) */                                                 break;
            }
        }
        // Nếu vũ khí Sword/Spear đã được serialize với giá trị enum cũ (trước khi thêm SpearThrust/SwordDoubleSlash),
        // OnValidate sẽ không tự sửa — cần mở từng asset trong Inspector và bấm Save để cập nhật.

        if (heavyDamageMultiplier == 0f) heavyDamageMultiplier = 2.0f;
    }
}