using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  COMPANION EQUIPMENT — DATA MODELS (ScriptableObject)
//
//  Companion KHÔNG dùng Weapon/CoreShield/Accessory của player. Có hệ riêng 3 slot:
//    1. Attack Module  — quyết định AI tấn công / di chuyển / hành vi (role).
//    2. Defense Core   — lõi sinh tồn (HP/giáp...).
//    3. Bond           — giống accessory nhưng CHỈ 1 slot.
//
//  Giai đoạn này CHỈ dựng nền sạch: data + stat modifier cơ bản + role.
//  Effect T4/T5 phức tạp để TODO trong behavior, KHÔNG implement sâu ở đây.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Vai trò AI của Companion, do Attack Module quyết định.</summary>
public enum CompanionRole
{
    Sniper,     // giữ khoảng cách xa, bắn, ưu tiên sống sót
    Berserker,  // lao vào đánh cận chiến, không bỏ chạy
    Control,    // giữ khoảng cách trung bình, AoE/khống chế đám đông
    Vanguard    // chắn giữa player và enemy lớn, tank/aggro
}

/// <summary>Một stat modifier đơn giản cho companion (cộng phẳng vào AllyStats khi equip).</summary>
[System.Serializable]
public class CompanionStatMod
{
    public enum Field
    {
        BonusHp, Armor, MagicResist, BonusPhysicalAtk, BonusMagicAtk,
        BonusAttackSpeed, BonusMoveSpeed, BonusCritChance
    }
    public Field field;
    public float value;
}

public abstract class CompanionModuleData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string moduleName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stat Modifiers (cộng vào AllyStats khi equip)")]
    public List<CompanionStatMod> statMods = new List<CompanionStatMod>();
}

/// <summary>Slot 1 — quyết định role + thông số chiến đấu cơ bản của Companion.</summary>
[CreateAssetMenu(fileName = "CompanionAttackModule", menuName = "Companion/Attack Module")]
public class CompanionAttackModuleData : CompanionModuleData
{
    [Header("Role & Combat")]
    public CompanionRole role = CompanionRole.Berserker;

    [Tooltip("Khoảng cách ưa thích tới mục tiêu (Sniper lớn, Berserker nhỏ).")]
    public float preferredRange = 2f;
    [Tooltip("Tầm đánh hiệu lực.")]
    public float attackRange = 2f;
    [Tooltip("Cooldown giữa 2 đòn (giây). 0 = dùng theo attackSpeed của stats.")]
    public float attackCooldown = 0f;
    [Tooltip("Hệ số nhân sát thương cơ bản của đòn đánh module.")]
    public float damageMultiplier = 1f;

    // TODO[T4/T5]: pierce terrain, execute <15% HP (Sniper); low-HP rage, kill fear (Berserker);
    //              paralysis stack→stun, black hole (Control); projectile-block heal, damage cap (Vanguard).
}

/// <summary>Slot 2 — lõi sinh tồn của Companion.</summary>
[CreateAssetMenu(fileName = "CompanionDefenseCore", menuName = "Companion/Defense Core")]
public class CompanionDefenseCoreData : CompanionModuleData
{
    [Header("Survival")]
    public float bonusMaxHpPercent = 0f;   // +% máu tối đa
    public float damageReduction = 0f;     // 0.1 = giảm 10% sát thương nhận (TODO: hook vào TakeDamage)

    // TODO[T4/T5]: shield regen, revive once, thorns...
}

/// <summary>Slot 3 — Bond (1 slot, giống accessory nhưng riêng cho companion).</summary>
[CreateAssetMenu(fileName = "CompanionBond", menuName = "Companion/Bond")]
public class CompanionBondData : CompanionModuleData
{
    [Header("Bond Synergy")]
    [Tooltip("% buff truyền cho Player khi Bond active (TODO: hook vào player stats).")]
    public float playerSynergyPercent = 0f;

    // TODO[T4/T5]: shared lifesteal, link damage, resurrection bond...
}
