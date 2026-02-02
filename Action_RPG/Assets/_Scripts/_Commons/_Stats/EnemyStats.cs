using UnityEngine;
using System.Collections.Generic;

public enum EnemyType { Hostile, Neutral, Friendly }

// [CẬP NHẬT] DetectionMethod bây giờ có thể chọn cả 2 hoặc từng cái
[System.Flags]
public enum DetectionMethod
{
    Sight = 1,
    Range = 2,
    Both = Sight | Range
}

public class EnemyStats : Stats
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

    // [MỚI] Thời gian chờ trước khi bắt đầu tụt Aggro (sau khi bị đánh lần cuối)
    public float aggroRetentionTime = 5.0f;
    private float lastDamageReceivedTime = -100f; // Mốc thời gian bị đánh cuối cùng

    [Header("--- Perception (Detection) ---")]
    [Tooltip("Phạm vi phát hiện hình cầu (Cảm nhận xung quanh)")]
    public float detectionRadius = 5f;

    [Tooltip("Tầm nhìn xa của hình quạt")]
    public float viewDistance = 10f;

    [Tooltip("Góc nhìn của hình quạt (Độ)")]
    [Range(0, 360)] public float viewAngle = 110f;

    // [MỚI] Layer Mask để chặn tầm nhìn (Tường, Vật cản...)
    public LayerMask obstacleMask;

    [Header("--- Spawn Info ---")]
    [HideInInspector] public Vector3 spawnPosition;

    void Start()
    {
        base.maxHp = base.baseHp;
        base.currentHp = base.maxHp;
        base.baseAttackSpeed = 0.5f;
        spawnPosition = transform.position;

        currentAggro = 0;
        obstacleMask = LayerMask.GetMask("Obstacle");
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
        if (Time.time > lastDamageReceivedTime + aggroRetentionTime)
        {
            if (currentAggro > 0)
            {
                currentAggro -= aggroDecayRate * Time.deltaTime;
                // Debug.Log($"Aggro đang giảm: {currentAggro}");
            }
        }

        if (currentAggro < 0) currentAggro = 0;
    }

    // Override phiên bản đầy đủ (DamageInfo)
    public override void TakeDamage(DamageInfo info)
    {
        base.TakeDamage(info);

        // [MỚI] Cập nhật thời điểm bị đánh
        // (Biến này bạn đã khai báo ở phần Aggro System trước đó)
        // lastDamageReceivedTime = Time.time; 

        // Thêm Aggro (Hận thù)
        AddAggro(25f);
    }

    // Override phiên bản rút gọn
    //public override void TakeDamage(float damage)
    //{
    //    DamageInfo info = new DamageInfo
    //    {
    //        damageAmount = damage,
    //        sourcePosition = transform.position
    //    };
    //    TakeDamage(info);
    //}

    public void AddAggro(float amount)
    {
        currentAggro += amount;
        if (currentAggro > maxAggro) currentAggro = maxAggro;
        lastDamageReceivedTime = Time.time;
    }



}