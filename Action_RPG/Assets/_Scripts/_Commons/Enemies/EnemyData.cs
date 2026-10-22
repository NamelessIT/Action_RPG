using UnityEngine;

/// <summary>
/// [P2-DATA-02] Source-of-truth cho THÔNG SỐ GỐC BẤT BIẾN của một enemy archetype.
/// Chỉ chứa config; KHÔNG chứa runtime state (currentHp, timer, target, coroutine, aggro/cooldown runtime).
/// Prefab/scene instance chỉ nên override khi thật sự cần; `EnemyStats.data == null` vẫn fallback inspector cũ.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Action_RPG/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("--- Identity ---")]
    public string enemyID;
    public string enemyName;

    [Tooltip("0 = thường, 1 = elite (super armor lv0), 2 = boss (super armor lv10). Quyết định SetupResistances().")]
    public int monsterRank = 0;

    [Header("--- Base Stats ---")]
    public float baseHp = 100f;
    public float baseMoveSpeed = 5f;

    [Tooltip("Số đòn mỗi giây. 0.667 = 1 đòn mỗi 1.5 giây.")]
    public float baseAttackSpeed = 0.667f;

    [Header("--- Reward ---")]
    public float expReward = 10f;

    [Header("--- Attack Modules (EAM) ---")]
    [Tooltip("Module đòn thường. Null → EnemyCombat dùng melee fallback cũ.")]
    public EnemyAttackModuleData basicAttackModule;

    [Tooltip("Module skill. Null → EnemyCombat dùng enemySkill/skillRange/skillEffects legacy.")]
    public EnemyAttackModuleData skillAttackModule;
}
