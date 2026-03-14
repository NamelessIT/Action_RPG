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
    public float aggroRadius = 5f;

    [Header("--- Target Focus Settings ---")]
    [Tooltip("Thời gian kiên nhẫn: Nếu mục tiêu hiện tại không gây sát thương trong X giây, Enemy sẽ đổi mục tiêu nếu bị kẻ khác đánh.")]
    public float targetPatienceTime = 15f;

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

    // [MỚI] Khả năng đỡ đòn (Có khiên không?)
    public bool canParry = false;

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
        // 1. Cập nhật Aggro và gọi AI (Giữ nguyên)
        lastDamageReceivedTime = Time.time;
        AddAggro(25f);

        if (enemyAI != null && info.attacker != null)
        {
            enemyAI.OnDamageTaken(info.attacker.transform);
        }

        Debug.Log("-----EnemyStats TakeDamage-----");
        // --- 2. LOGIC PARRY CỦA ENEMY ---
        if (isParrying)
        {
            // Tính toán hướng kẻ địch
            Vector3 dirToAttacker = (info.sourcePosition - transform.position).normalized;
            Vector3 myFacingDir = facingDirection != Vector3.zero ? facingDirection : transform.forward;
            float angle = Vector3.Angle(myFacingDir, dirToAttacker);

            // Kiểm tra góc đỡ (Enemy đỡ thành công)
            if (angle <= parryAngle / 2f)
            {
                Debug.Log($"<color=white>>> {gameObject.name} PARRY thành công! (Giảm 80% sát thương)</color>");

                // Normal Parry: Giảm 80% sát thương
                info.damageAmount *= 0.2f;

                DuelistPassive duelist = GetComponent<DuelistPassive>();
                if (duelist != null) duelist.OnParrySuccess(false, info.attacker);

                // --- BẬT SUPER ARMOR BẢO VỆ ---
                bool oldSuperArmor = this.isSuperArmor;
                int oldArmorLevel = this.superArmorLevel;

                this.isSuperArmor = true;
                this.superArmorLevel = 99; // Chống mọi Stun/Knockback (trừ skill có Impact >= 100)

                // [QUAN TRỌNG NHẤT] Gọi TakeDamage GỐC ngay lúc Super Armor đang level 99
                base.TakeDamage(info);

                // Xử lý xong mới trả lại chỉ số giáp cũ
                this.isSuperArmor = oldSuperArmor;
                this.superArmorLevel = oldArmorLevel;

                return; // Đã xử lý xong, KHÔNG chạy xuống dưới nữa
            }
            else
            {
                // Parry thất bại do bị đánh sau lưng / tạt sườn
                Debug.Log($"<color=red>>> {gameObject.name} Parry thất bại! (Bị đánh ngoài góc {parryAngle} độ)</color>");
            }
        }

        // --- 3. KHÔNG PARRY / PARRY TRƯỢT ---
        // Ăn trọn sát thương và hiệu ứng (Stun/Knockback)
        base.TakeDamage(info);
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

    protected override void Die()
    {
        base.Die();
        //effect gạch đá vỡ vụn
    }
}