using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;

public class Stats : MonoBehaviour
{
    [Header("--- Health ---")]
    public float maxHp;
    public float currentHp;
    public float baseHp;
    public float baseHpGain = 2f;
    public float level = 1f;

    public bool isInvincible = false;

    [Header("--- Stamina  ---")]
    public float maxStamina = 100f;
    public float currentStamina;

    [Tooltip("Hồi phục mỗi giây khi TRONG combat")]
    public float staminaBaseRecovery = 0.5f;

    [Tooltip("Hồi phục mỗi giây khi NGOÀI combat")]
    public float staminaOutCombatRecovery = 15f;

    // [MỚI] Thời gian chờ hồi phục sau khi dùng thể lực (Dash/Run)
    public float staminaRegenDelay = 1.0f;
    private float lastStaminaConsumeTime = -10f; // Biến ghi lại thời điểm cuối cùng dùng thể lực

    [Header("--- Combat State ---")]
    public bool outCombat = true;
    public float outCombatTime = 10.0f;
    private float combatTimer = 0f;

    [Header("--- Dash & Run Settings ---")]
    public float baseDashDistance = 1.5f;
    public float baseDashRecovery = 1.25f;
    public float dashCost = 15f;
    public float baseDashDuration = 0.2f;

    // [MỚI] Thể lực tiêu hao mỗi giây khi chạy nhanh
    public float runCost = 8.0f;

    [HideInInspector] public float lastDashTime = -10f;

    [Header("--- Sins ---")]
    public float maxSin ;
    public float currentSin;
    public float baseSinGain = 5f;

    [Header("--- Base Stats ---")]
    public float STR; public float DEX; public float INT; public float VIT; public float AGI;

    [Header("--- Attack Stats ---")]
    public float physicalAtk ;
    public float magicAtk ;

    [Header("--- LifeSteal ---")]
    public float physicalLifeSteal;
    public float magicLifeSteal;

    // [SỬA] Tách comment ra khỏi khai báo biến
    [Tooltip("Thời gian giữa các đòn đánh (Cooldown)")]
    public float baseAttackSpeed;

    // Biến hỗ trợ Combo (Logic này sẽ nằm ở PlayerController, nhưng Stats chứa thông số)
    public float comboResetTime = 1.0f; // Thời gian chờ để reset combo về đòn 1
    public float heavyAttackChargeTime = 1.0f; // Thời gian giữ chuột để max dame
    public int heavyAttackCharge = 2;

    [Header("--- Crit ---")]
    public float baseCritChance;
    public float baseCritMultiplier = 1.5f;


    [Header("--- Penetration (Player Only) ---")]
    public float armorBackstabReduce = 0.5f;
    public float magicResistBackstabReduce = 0.5f;

    [Header("--- Defense Stats (Enemy) ---")]
    public float armor = 100;
    public float magicResist = 100;
    public float defenseValue = 20;
    [Header("--- Defense Logic ---")]
    // Góc block hiệu quả. Mặc định 0.5 (180 độ). Vanguard sẽ sửa thành 0.75 (270 độ).
    public float blockThreshold = 0.5f;

    [Header("--- Movement Setting ---")]
    public float baseMoveSpeed = 5f;
    public float runSpeedMultiplier = 1.5f;
    public float moveThresholdAngle = 45f;
    public float moveFlexibility=1f;

    [Header("--- Rotation Dynamic ---")]
    public float turnDuration = 0.1f;
    private float idleTurnDuration = 0.1f;
    public float combatTurnDuration;

    [Header("--- Knockback & Effect Res ---")]
    public float resistanceKnockBack = 0.1f; 
    public float resistanceEffect = 0f; //giảm thời gian debuff

    private float stunEndTime = 0f;

    private Coroutine currentStunCoroutine;

    // [MỚI] Trạng thái bị khống chế
    public bool isStunned = false;

    [Header("--- Tăng thời gian nhận Buff ---")]
    public float buffDurationBonus = 0f;
    // [MỚI] Biến lưu hướng mặt thực tế (Dùng cho CombatMath)
    [HideInInspector] public Vector3 facingDirection = Vector3.back;

    [Header("--- Stealth ---")]
    public float stealthFactor = 1.0f; // 1 = Bình thường, 0.5 = Giảm 50% tầm địch

    [Header ("--- Mark ---")]
    [Tooltip("Bị đánh dấu")]
    public bool IsMarked=false;

    private NavMeshAgent agent;
    private Rigidbody rb;

    void Start()
    {
        maxHp = baseHp;
        currentHp = maxHp;
        currentStamina = maxStamina;
        currentSin= maxSin;

        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
    }

    public virtual void Update()
    {
        HandleCombatState();
        HandleStaminaRegen();
        UpdateTurnSpeed();
    }

    void UpdateTurnSpeed()
    {
        if (outCombat) turnDuration = idleTurnDuration;
        else turnDuration = combatTurnDuration;
    }

    void HandleCombatState()
    {
        if (!outCombat)
        {
            combatTimer += Time.deltaTime;
            if (combatTimer >= outCombatTime)
            {
                outCombat = true;
            }
        }
    }

    void HandleStaminaRegen()
    {
        // [MỚI] Kiểm tra Delay: Nếu chưa qua 1 giây kể từ lần cuối dùng thể lực -> Không hồi
        if (Time.time < lastStaminaConsumeTime + staminaRegenDelay)
        {
            return;
        }

        float recoveryRate = outCombat ? staminaOutCombatRecovery : staminaBaseRecovery;

        if (currentStamina < maxStamina)
        {
            currentStamina += recoveryRate * Time.deltaTime;
            if (currentStamina > maxStamina) currentStamina = maxStamina;
        }
    }

    public void EnterCombat()
    {
        if (outCombat) Debug.Log(">> Enter Combat! (Hồi thể lực chậm, Xoay chậm)");
        outCombat = false;
        combatTimer = 0f;
    }

    // [CẬP NHẬT] Hàm tiêu hao thể lực dùng chung cho Dash và Run
    public bool TryConsumeStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;

            // [MỚI] Ghi lại thời gian tiêu hao để tính Delay hồi phục
            lastStaminaConsumeTime = Time.time;

            return true;
        }
        return false;
    }

    public virtual void TakeDamage(DamageInfo info)
    {
        if (isInvincible)
        {
            Debug.Log($"{gameObject.name} DODGED damage (Invincible)!");
            return;
        }

        EnterCombat();
        currentHp -= info.damageAmount;
        if (info.isCrit) Debug.Log($"<color=red>CRIT!</color> {gameObject.name} nhận {info.damageAmount}");
        else Debug.Log($"{gameObject.name} nhận {info.damageAmount}");

        // --- XỬ LÝ HIỆU ỨNG (CC) ---
        ApplyCrowdControl(info);

        if (currentHp <= 0) Die();
    }

    //// Hàm cũ (Overload) để tương thích code cũ chưa kịp sửa
    public virtual void TakeDamage(float damage)
    {
        DamageInfo info = new DamageInfo
        {
            damageAmount = damage,
            isCrit = false,
            isStun = false,
            isKnockback = false
        };
        TakeDamage(info);
    }

    // --- LOGIC STUN & KNOCKBACK ---
    void ApplyCrowdControl(DamageInfo info)
    {
        // 1. Xử lý KNOCKBACK
        if (info.isKnockback)
        {
            // Tính lực đẩy lùi thực tế sau khi trừ Kháng
            // Ví dụ: Force 10, Res 0.2 -> Thực nhận 8
            float finalForce = info.knockbackForce * (1.0f - resistanceKnockBack);

            // Nếu lực vẫn > 0 thì đẩy
            if (finalForce > 0)
            {
                Vector3 knockbackDir = (transform.position - info.sourcePosition).normalized;
                knockbackDir.y = 0; // Giữ thăng bằng mặt đất
                StartCoroutine(KnockbackRoutine(knockbackDir, finalForce));
            }
        }

        // 2. Xử lý STUN (Nâng cấp)
        if (info.isStun)
        {
            float finalDuration = Mathf.Max(0.1f, info.stunDuration * (1.0f - resistanceEffect));
            float proposedEndTime = Time.time + finalDuration;

            // Chỉ áp dụng nếu stun mới kéo dài lâu hơn thời gian stun còn lại
            if (proposedEndTime > stunEndTime)
            {
                stunEndTime = proposedEndTime;

                // Reset coroutine cũ để chạy cái mới chính xác hơn
                if (currentStunCoroutine != null) StopCoroutine(currentStunCoroutine);
                currentStunCoroutine = StartCoroutine(StunRoutine(finalDuration));
            }
        }
    }

    IEnumerator KnockbackRoutine(Vector3 dir, float force)
    {
        isStunned = true;

        // Biến lưu trạng thái gốc để khôi phục sau khi đẩy xong
        bool wasKinematic = false;
        bool hasAgent = (agent != null);

        // 1. TẠM DỪNG NAVMESH AGENT (Nếu là Enemy)
        // Phải tắt update position để Agent không "giằng co" vị trí với Rigidbody
        if (hasAgent)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        // 2. XỬ LÝ RIGIDBODY (Cho cả Player và Enemy)
        if (rb != null)
        {
            wasKinematic = rb.isKinematic; // Lưu lại: Enemy là true, Player là false

            // BẮT BUỘC: Phải tắt Kinematic thì mới AddForce hoặc set velocity được
            rb.isKinematic = false;

            // Reset vận tốc cũ để lực đẩy dứt khoát hơn
            rb.linearVelocity = Vector3.zero;

            // Thêm lực đẩy
            rb.AddForce(dir * force, ForceMode.Impulse);
        }

        // 3. CHỜ THỜI GIAN BỊ ĐẨY
        yield return new WaitForSeconds(0.2f);

        // 4. KHÔI PHỤC TRẠNG THÁI
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; // Phanh lại
            rb.isKinematic = wasKinematic; // Trả lại trạng thái gốc (Enemy -> Kinematic, Player -> Dynamic)
        }

        if (hasAgent)
        {
            // Đồng bộ vị trí Agent theo vị trí vật lý mới của Enemy
            agent.Warp(transform.position);
            agent.updatePosition = true;
            agent.updateRotation = true;
            // Vẫn giữ isStopped = true vì đang bị Stun (sẽ mở lại ở StunRoutine hoặc Logic AI)
        }

        // 5. CHECK STUN TIẾP THEO
        yield return new WaitForSeconds(0.1f);

        // Logic kiểm tra xem có ai đang gia hạn stun không (Stun chồng Stun)
        // Nếu không có StunRoutine nào khác đang chạy đè lên thì mới tắt
        // (Tuy nhiên ở đây bạn dùng biến bool đơn giản nên ta cứ set false nếu hết giờ)
        if (isStunned) isStunned = false;
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        // Debug.Log($"{gameObject.name} bị STUN!");

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Thay vì WaitForSeconds cố định, ta chờ đến đúng thời điểm stunEndTime
        // Điều này giúp việc "ghi đè" thời gian trở nên mượt mà (chỉ cần update stunEndTime)
        while (Time.time < stunEndTime)
        {
            yield return null;
        }

        isStunned = false;
        // Debug.Log($"{gameObject.name} hết STUN");
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} đã bị hạ gục!");
    }
}