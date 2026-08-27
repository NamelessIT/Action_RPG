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
    [Header("--- Archetype Data ---")]
    [Tooltip("[P2-DATA-02] EnemyData là SOURCE-OF-TRUTH cho thông số archetype (ID/rank/hp/move/attack speed/exp/EAM). " +
             "Các trường runtime stats trên inspector chỉ còn là FALLBACK khi data == null, hoặc override tạm thời khi test.")]
    public EnemyData data;

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
    [Tooltip("Có bị ảnh hưởng bởi stealthReduction của player không (false = luôn phát hiện bình thường)")]
    public bool isAffectedByStealthReduction = true;

    [Header("--- Spawn Info ---")]
    [HideInInspector] public Vector3 spawnPosition;

    [Header("--- Enemy Rank ---")]
    public int monsterRank = 0;

    [Header("--- Reward on Death ---")]
    [Tooltip("EXP trao cho Player khi giết enemy này. Mỗi enemy prefab tự config con số riêng.")]
    public float expReward = 10f;

    public float aggroRetentionTime = 5.0f;
    private float lastDamageReceivedTime = -100f;

    // [MỚI] Tham chiếu tới AI để báo tin
    private EnemyAI enemyAI;

    // [MỚI] Khả năng đỡ đòn (Có khiên không?)
    public bool canParry = false;

    public override void Start()
    {
        // [P2-DATA-02] Data phải apply TRƯỚC base.Start() để mọi thứ phía sau (maxHp, SetupResistances,
        // AI/Combat đọc monsterRank) thấy đúng giá trị archetype.
        ApplyEnemyData();

        base.Start();
        if (maxHp == 0) maxHp = baseHp;
        currentHp = maxHp;

        // Fallback attack speed CHỈ khi không có data và inspector chưa set giá trị hợp lệ.
        // (Trước đây dòng này vô điều kiện → ghi đè mọi cấu hình. Prefab enemy hiện tại đều là 0 nên hành vi giữ nguyên.)
        if (data == null && baseAttackSpeed <= 0f) SetBaseAttackSpeed(0.667f); // 1 đòn mỗi 1.5 giây

        spawnPosition = transform.position;

        currentAggro = 0;
        obstacleMask = LayerMask.GetMask("Obstacle");

        // Chạy SAU khi monsterRank đã được copy từ data.
        SetupResistances();

        // Đảm bảo tag đúng để hệ thống nhận diện
        if (this.gameObject.tag == "Untagged") this.gameObject.tag = "Enemy";

        enemyAI = GetComponent<EnemyAI>();
    }

    /// <summary>[P2-DATA-02] Copy archetype config từ `data` vào runtime stats. No-op nếu `data == null`
    /// (enemy prefab cũ giữ nguyên giá trị inspector). Chỉ copy CONFIG, không đụng runtime state.</summary>
    private void ApplyEnemyData()
    {
        if (data == null) return;

        if (!string.IsNullOrEmpty(data.enemyID))   enemyID       = data.enemyID;
        if (!string.IsNullOrEmpty(data.enemyName)) characterName = data.enemyName;

        monsterRank = data.monsterRank;
        expReward   = data.expReward;

        if (data.baseHp > 0f)          baseHp = data.baseHp;
        if (data.baseMoveSpeed > 0f)   SetBaseMoveSpeed(data.baseMoveSpeed);
        if (data.baseAttackSpeed > 0f) SetBaseAttackSpeed(data.baseAttackSpeed);
    }

    void SetupResistances()
    {
        // resistanceEffect giảm THỜI LƯỢNG Stun/Root/Silence/Slow; knockbackResistance chỉ giảm lực đẩy.
        // rank1: resistanceEffect=0.5 để BẢO TOÀN balance Stun cũ (xưa Stun dùng knockbackResistance=0.5).
        // Lưu ý: nay rank1 cũng giảm 50% Root/Silence/Slow (đồng nhất qua resistanceEffect).
        if (monsterRank == 0) { isSuperArmor = false; knockbackResistance = 0.1f; resistanceEffect = 0f; }
        else if (monsterRank == 1) { isSuperArmor = true; superArmorLevel = 0; knockbackResistance = 0.5f; resistanceEffect = 0.5f; }
        else if (monsterRank == 2) { isSuperArmor = true; superArmorLevel = 10; knockbackResistance = 1.0f; resistanceEffect = 1.0f; }
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
                info.physDamage *= 0.2f;
                info.magicDamage *= 0.2f;

                DuelistPassive duelist = GetComponent<DuelistPassive>();
                if (duelist != null) duelist.OnParrySuccess(false, info.attacker);

                // --- BẬT SUPER ARMOR BẢO VỆ ---
                // Chống mọi Stun/Knockback (trừ skill có Impact >= 100) trong đúng cú parry.
                PushSuperArmor(99);

                // [QUAN TRỌNG NHẤT] Gọi TakeDamage GỐC ngay lúc Super Armor đang level 99.
                // try/finally vì lý do như PlayerStats: exception trong event của TakeDamage
                // sẽ làm enemy miễn nhiễm CC vĩnh viễn nếu Pop nằm ngoài finally.
                try
                {
                    base.TakeDamage(info);
                }
                finally
                {
                    PopSuperArmor(99);
                }

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
        base.Die(); // Xử lý chung: tắt collider, AI, destroy sau 3s

        // ── 1. TRAO EXP CHO PLAYER ──────────────────────────────────────
        if (expReward > 0f)
        {
            PlayerStats playerStats = PlayerStats.Current;
            if (playerStats != null)
            {
                float xpGain = expReward;
                MagePassive magePassive = playerStats.GetComponent<MagePassive>();
                if (magePassive != null) xpGain *= magePassive.GetXpBonusMultiplier();
                playerStats.AddExp(xpGain);
                Debug.Log($"[EnemyStats] 🎖️ {gameObject.name} chết → Player nhận {xpGain} EXP");
            }
        }

        // ── 2. TRIGGER LOOT DROP ─────────────────────────────────────────
        // LootDropper phải được gắn trên cùng GameObject (hoặc con trực tiếp)
        LootDropper dropper = GetComponent<LootDropper>()
                           ?? GetComponentInChildren<LootDropper>();
        if (dropper != null)
        {
            dropper.OnEnemyDeath();
        }
        else
        {
            Debug.Log($"[EnemyStats] {gameObject.name} không có LootDropper — bỏ qua drop loot.");
        }
    }
}