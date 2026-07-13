#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [EAM] Sinh nhanh bộ Enemy Attack Module Assets mẫu vào
/// Assets/Resources/Datas/EnemyAttackModules/.
///
/// Chạy: menu "Action_RPG ▸ Enemy ▸ Generate Attack Modules".
/// Idempotent — asset đã tồn tại thì CẬP NHẬT tại chỗ (giữ nguyên GUID/tham chiếu), chưa có thì tạo mới.
/// Không đụng tới projectilePrefab/telegraphPrefab đã gán tay (chỉ set khi đang trống).
/// </summary>
public static class EnemyAttackModuleGenerator
{
    private const string Dir = "Assets/Resources/Datas/EnemyAttackModules";

    [MenuItem("Action_RPG/Enemy/Generate Attack Modules")]
    public static void Generate()
    {
        EnsureDir();

        // 1) Kiếm thường
        Make("EAM_Melee_Sword", "Kiếm thường", EnemyAttackStyle.MeleeSingle, m =>
        {
            m.range = 2.2f; m.cooldown = 0f; m.damageMultiplier = 1.0f;
            m.windupDuration = 0.30f; m.activeDuration = 0.20f; m.recoveryDuration = 0.25f;
            m.attackAngle = 90f;
        });

        // 2) Dao găm — nhanh, sát thương thấp
        Make("EAM_Melee_Dagger", "Dao găm", EnemyAttackStyle.MeleeSingle, m =>
        {
            m.range = 1.6f; m.cooldown = 0f; m.damageMultiplier = 0.7f;
            m.windupDuration = 0.15f; m.activeDuration = 0.12f; m.recoveryDuration = 0.15f;
            m.attackAngle = 70f;
        });

        // 3) Đập AoE quanh người — nặng, có hất lùi
        Make("EAM_Melee_Heavy_AOE", "Đập chấn động", EnemyAttackStyle.MeleeCircleAOE, m =>
        {
            m.range = 3.0f; m.cooldown = 6f; m.damageMultiplier = 1.4f;
            m.windupDuration = 0.70f; m.activeDuration = 0.25f; m.recoveryDuration = 0.60f;
            m.aoeRadius = 3.0f; m.impactBonus = 1; m.useTelegraph = true;
            m.effects = new List<CombatEffectInfo>
            {
                new CombatEffectInfo(CombatEffectType.Knockback, 0f) { force = 6f, impactLevel = 1 }
            };
        });

        // 4) Bắn cung theo hướng
        Make("EAM_Ranged_Bow", "Bắn cung", EnemyAttackStyle.ProjectileDirectional, m =>
        {
            m.range = 10f; m.cooldown = 2f; m.damageMultiplier = 1.0f;
            m.windupDuration = 0.40f; m.activeDuration = 0.10f; m.recoveryDuration = 0.30f;
            m.projectileSpeed = 15f; m.projectileLifetime = 4f; m.useTelegraph = true;
        });

        // 5) Đạn khóa mục tiêu (mage)
        Make("EAM_Mage_TargetBolt", "Đạn dẫn đường", EnemyAttackStyle.ProjectileTargeted, m =>
        {
            m.range = 9f; m.cooldown = 3f; m.damageMultiplier = 1.1f;
            m.windupDuration = 0.50f; m.activeDuration = 0.10f; m.recoveryDuration = 0.40f;
            m.projectileSpeed = 10f; m.projectileLifetime = 5f; m.useTelegraph = true;
            m.atkType = WeaponData.WeaponAtkType.Magic; // sát thương phép
        });

        // 6) Vùng phép dưới chân player — kèm làm chậm
        Make("EAM_Mage_GroundAOE", "Vùng nguyền", EnemyAttackStyle.GroundTargetAOE, m =>
        {
            m.range = 8f; m.cooldown = 5f; m.damageMultiplier = 1.2f;
            m.windupDuration = 1.00f; m.activeDuration = 0.50f; m.recoveryDuration = 0.60f;
            m.aoeRadius = 3.0f; m.useTelegraph = true;
            m.atkType = WeaponData.WeaponAtkType.Magic; // sát thương phép
            m.effects = new List<CombatEffectInfo>
            {
                new CombatEffectInfo(CombatEffectType.Slow, 2.5f) { magnitude = 0.4f }
            };
        });

        // 7) Lao tới chém
        Make("EAM_DashStrike", "Lao chém", EnemyAttackStyle.DashStrike, m =>
        {
            m.range = 3f; m.cooldown = 4f; m.damageMultiplier = 1.2f;
            m.windupDuration = 0.35f; m.activeDuration = 0.35f; m.recoveryDuration = 0.40f;
            m.attackAngle = 60f; m.faceTargetOnStart = true; m.useTelegraph = true;
            m.effects = new List<CombatEffectInfo>
            {
                new CombatEffectInfo(CombatEffectType.Knockback, 0f) { force = 5f }
            };
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=cyan>[EAM]</color> Đã tạo/cập nhật 7 Enemy Attack Module tại " + Dir);
    }

    // ── Tạo-hoặc-cập-nhật 1 asset ──
    private static void Make(string id, string displayName, EnemyAttackStyle style, System.Action<EnemyAttackModuleData> configure)
    {
        string path = $"{Dir}/{id}.asset";
        var data = AssetDatabase.LoadAssetAtPath<EnemyAttackModuleData>(path);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<EnemyAttackModuleData>();

        // Reset về mặc định trước khi cấu hình (đảm bảo update sạch, không giữ giá trị cũ lạ).
        data.id = id;
        data.displayName = displayName;
        data.style = style;
        data.range = 2f;
        data.cooldown = 0f;
        data.damageMultiplier = 1f;
        data.atkType = WeaponData.WeaponAtkType.Physical;
        data.windupDuration = 0.5f;
        data.activeDuration = 0.2f;
        data.recoveryDuration = 0.3f;
        data.attackAngle = 90f;
        data.sweepStartAngle = -45f;
        data.sweepEndAngle = 45f;
        data.aoeRadius = 1.5f;
        data.projectileSpeed = 10f;
        data.projectileLifetime = 4f;
        data.faceTargetOnStart = true;
        data.useTelegraph = true;
        data.impactBonus = 0;
        data.effects = new List<CombatEffectInfo>();

        configure?.Invoke(data);

        if (isNew) AssetDatabase.CreateAsset(data, path);
        else EditorUtility.SetDirty(data);
    }

    private static void EnsureDir()
    {
        if (AssetDatabase.IsValidFolder(Dir)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Datas")) AssetDatabase.CreateFolder("Assets/Resources", "Datas");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources/Datas", "EnemyAttackModules");
    }
}
#endif
