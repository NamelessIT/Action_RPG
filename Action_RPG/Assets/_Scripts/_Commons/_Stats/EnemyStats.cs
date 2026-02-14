using UnityEngine;
using System.Collections.Generic;

public enum EnemyType { Hostile, Neutral, Friendly }

[System.Flags]
public enum DetectionMethod
{
    Sight = 1,
    Range = 2,
    Both = Sight | Range
}


public class EnemyStats : Stats
{
    [Header("--- Enemy Identity ---")]
    public string enemyID;
    public EnemyType enemyType = EnemyType.Hostile;
    public DetectionMethod detectionMethod = DetectionMethod.Sight;

    [Header("--- Aggro System ---")]
    public float currentAggro = 0f;
    public float maxAggro = 100f;
    public float aggroDecayRate = 5f;
    public float aggroRadius = 20f;

    [Header("--- Perception (Detection) ---")]
    public float detectionRadius = 10f;
    public float viewDistance = 10f;
    [Range(0, 360)] public float viewAngle = 110f;
    public LayerMask obstacleMask;

    [Header("--- Spawn Info ---")]
    [HideInInspector] public Vector3 spawnPosition;

    [Header("--- Enemy Rank ---")]
    public int monsterRank = 0;

    public float aggroRetentionTime = 5.0f;
    private float lastDamageReceivedTime = -100f;

    // [MỚI] Tham chiếu tới AI để báo tin
    private EnemyAI enemyAI;

    public override void Start()
    {
        base.Start();
        if (maxHp == 0) maxHp = baseHp;
        currentHp = maxHp;
        base.baseAttackSpeed = 0.5f;
        spawnPosition = transform.position;

        currentAggro = 0;
        obstacleMask = LayerMask.GetMask("Obstacle");

        SetupResistances();

        // Đảm bảo tag đúng để hệ thống nhận diện
        if (this.gameObject.tag == "Untagged") this.gameObject.tag = "Enemy";

        enemyAI = GetComponent<EnemyAI>();
    }

    void SetupResistances()
    {
        if (monsterRank == 0) { isSuperArmor = false; resistanceKnockBack = 0.1f; }
        else if (monsterRank == 1) { isSuperArmor = true; superArmorLevel = 0; resistanceKnockBack = 0.5f; }
        else if (monsterRank == 2) { isSuperArmor = true; superArmorLevel = 10; resistanceKnockBack = 1.0f; resistanceEffect = 1.0f; }
    }

    public override void Update()
    {
        base.Update();
        HandleAggroDecay();
    }

    void HandleAggroDecay()
    {
        if (enemyType == EnemyType.Hostile) return;

        if (Time.time > lastDamageReceivedTime + aggroRetentionTime)
        {
            if (currentAggro > 0) currentAggro -= aggroDecayRate * Time.deltaTime;
        }
        if (currentAggro < 0) currentAggro = 0;
    }

    public override void TakeDamage(DamageInfo info)
    {
        base.TakeDamage(info);
        lastDamageReceivedTime = Time.time;
        AddAggro(25f);

        // [MỚI] BÁO CHO AI BIẾT KẺ TẤN CÔNG LÀ AI
        if (enemyAI != null && info.attacker != null)
        {
            // info.attacker là Stats, ta cần Transform
            enemyAI.OnDamageTaken(info.attacker.transform);
        }
    }

    public void AddAggro(float amount)
    {
        currentAggro += amount;
        if (currentAggro > maxAggro) currentAggro = maxAggro;
        lastDamageReceivedTime = Time.time;
    }

    // --- KIỂM TRA ĐỊCH (Tấn công) ---
    public bool IsHostileTo(Transform target)
    {
        if (target == null || target == transform) return false;

        bool isPlayerFaction = target.CompareTag("Player") || target.CompareTag("Ally");

        if (isPlayerFaction)
        {
            if (enemyType == EnemyType.Hostile) return true;
            if (enemyType == EnemyType.Neutral && currentAggro > 0) return true;
        }
        return false;
    }

    // --- [FIX LỖI] KIỂM TRA SỢ HÃI (Bỏ chạy) ---
    public bool IsScaredOf(Transform target)
    {
        if (target == null) return false;

        // Chỉ Enemy Friendly (như Cừu) mới biết sợ
        if (enemyType == EnemyType.Friendly)
        {
            // Sợ Player hoặc Ally
            if (target.CompareTag("Player") || target.CompareTag("Ally")) return true;

            // Sợ Enemy Hostile
            EnemyStats otherStats = target.GetComponent<EnemyStats>();
            if (otherStats != null && otherStats.enemyType == EnemyType.Hostile) return true;
        }

        return false;
    }
}