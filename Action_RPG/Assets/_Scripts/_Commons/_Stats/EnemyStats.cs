using UnityEngine;
using System.Collections.Generic;

public enum EnemyType { Hostile, Neutral, Friendly }
public enum DetectionMethod { Sight, Sound, Range }

public class EnemyStats : CharacterStats
{
    // ... (Phần Header giữ nguyên) ...
    [Header("--- Enemy Identity ---")]
    public string enemyID;
    public EnemyType enemyType = EnemyType.Hostile;
    public DetectionMethod detectionMethod = DetectionMethod.Sight;

    [Header("--- Aggro System ---")]
    public float currentAggro = 0f;
    public float maxAggro = 100f;
    public float aggroDecayRate = 5f;
    public float aggroRadius = 20f;

    [Header("--- Perception ---")]
    public float detectionRange = 10f;
    [Range(0, 360)] public float viewAngle = 110f;

    [Header("--- Spawn Info ---")]
    [HideInInspector] public Vector3 spawnPosition;

    void Start()
    {
        base.maxHp = base.baseHp;
        base.currentHp = base.maxHp;
        spawnPosition = transform.position;

        if (enemyType == EnemyType.Hostile) currentAggro = maxAggro;
        else currentAggro = 0;
    }

    public override void Update()
    {
        base.Update();
        HandleAggroDecay();
    }

    void HandleAggroDecay()
    {
        // [CẬP NHẬT] Nếu là Hostile thì KHÔNG BAO GIỜ tự giảm Aggro
        if (enemyType == EnemyType.Hostile) return;

        // Chỉ giảm Aggro khi ĐÃ THOÁT COMBAT (cho Neutral/Friendly)
        if (outCombat && currentAggro > 0)
        {
            currentAggro -= aggroDecayRate * Time.deltaTime;
        }
        if (currentAggro < 0) currentAggro = 0;
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        AddAggro(50f);
    }

    public void AddAggro(float amount)
    {
        currentAggro += amount;
        if (currentAggro > maxAggro) currentAggro = maxAggro;
    }
}