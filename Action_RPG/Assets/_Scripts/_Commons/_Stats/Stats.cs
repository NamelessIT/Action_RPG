using System;
using System.Collections; // Để dùng Coroutine
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
    public float baseDashDistance = 2f;
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

    [Header("--- Status ---")]
    public bool isDead = false; // [MỚI] Kiểm tra đã chết chưa


    private float stunEndTime = 0f;

    private Coroutine currentStunCoroutine;

    // [MỚI] Trạng thái bị khống chế
    public bool isStunned = false;

    // [MỚI] Super Armor (Siêu Giáp) - Không bị ngắt chiêu
    [Header("--- Super Armor ---")]
    public bool isSuperArmor = false;
    public int superArmorLevel = 0; // Cấp độ giáp (0: Chống quái nhỏ, 1: Chống Elite...)

    [Header("--- Tăng thời gian nhận Buff ---")]
    public float buffDurationBonus = 0f;
    // [MỚI] Biến lưu hướng mặt thực tế (Dùng cho CombatMath)
    [HideInInspector] public Vector3 facingDirection = Vector3.back;

    [Header("--- Stealth ---")]
    public float stealthFactor = 1.0f; // 1 = Bình thường, 0.5 = Giảm 50% tầm địch

    // --- HIỆU ỨNG CHẢY MÁU (BLEED) ---
    [Header("--- Bleed ---")]
    public bool isBleeding = false;
    private Coroutine bleedCoroutine;
    private float bleedTimer = 0f; // Bộ đếm thời gian còn lại của Bleed
    private float currentBleedDamage = 0f; // Lưu damage để nếu đánh tiếp thì cập nhật damage mới
    [Header ("--- Mark ---")]
    [Tooltip("Bị đánh dấu")]
    public bool IsMarked=false;

    [Header("--- Combat State ---")]
    public bool isParrying = false;       // Đang trong thế thủ
    public bool isPerfectParryWindow = false; // Đang trong "khung giờ vàng"


    private NavMeshAgent agent;
    private Rigidbody rb;
    protected Animator animator;

    public virtual void Start()
    {
        maxHp = baseHp;
        currentHp = maxHp;
        currentStamina = maxStamina;
        currentSin= maxSin;

        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
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

    public void ApplyBleed(float damagePerTick, float duration)
    {
        // 1. Cập nhật thông số mới nhất
        currentBleedDamage = damagePerTick; // Có thể làm logic: Lấy damage cao nhất hoặc cộng dồn

        // 2. Gia hạn thời gian (Reset lại đồng hồ đếm ngược)
        bleedTimer = duration;

        // Đánh dấu kẻ địch đang bị Bleed
        isBleeding = true;
        // 3. Chỉ Start Coroutine nếu nó chưa chạy
        if (bleedCoroutine == null)
        {
            bleedCoroutine = StartCoroutine(BleedRoutine());
        }
    }

    private IEnumerator BleedRoutine()
    {
        // Vòng lặp chạy chừng nào còn thời gian
        while (bleedTimer > 0)
        {
            // Chờ 1 giây
            yield return new WaitForSeconds(1.0f);

            // Trừ thời gian
            bleedTimer -= 1.0f;

            // Gây sát thương
            TakeDamage(currentBleedDamage);
            Debug.Log($"<color=red>{gameObject.name} đang chảy máu: -{currentBleedDamage} HP (Còn {bleedTimer}s)</color>");
        }

        // Hết giờ -> Xóa Coroutine để lần sau Start lại được
        isBleeding = false;
        bleedCoroutine = null;
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
        if (isInvincible || isDead) return;

        EnterCombat();
        currentHp -= info.damageAmount;
        if (info.isCrit) Debug.Log($"<color=red>CRIT!</color> {gameObject.name} nhận {info.damageAmount}");
        else Debug.Log($"{gameObject.name} nhận {info.damageAmount}");

        // --- XỬ LÝ HIỆU ỨNG (CC) ---
        ApplyCrowdControl(info);

        if (currentHp <= 0)
        {
            currentHp = 0;
            Die();
        }
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
        if (isSuperArmor && info.impactLevel <= superArmorLevel)
        {
            // Có thể thêm hiệu ứng visual (ví dụ: người lóe sáng trắng chịu đòn)
             //Debug.Log("Super Armor Blocked CC!");
            return;
        }

        // 1. Xử lý KNOCKBACK
        if (info.isKnockback)
        {
            // Tính lực đẩy lùi thực tế sau khi trừ Kháng
            // Ví dụ: Force 10, Res 0.2 -> Thực nhận 8
            float finalForce = info.knockbackForce * (1.0f - resistanceKnockBack);
            Debug.Log("finalForce: " + finalForce + " info.knockbackForce: "+ info.knockbackForce + " resistanceKnockBack: "+ resistanceKnockBack);

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
            float finalDuration = info.stunDuration * (1.0f - resistanceKnockBack);
            Debug.Log($"Stun: {finalDuration}");
            // [SỬA] Bỏ hàm Mathf.Max(0.1f). Nếu thời gian < 0.1s coi như kháng hoàn toàn
            if (finalDuration >= 0.1f)
            {
                float proposedEndTime = Time.time + finalDuration;
                if (proposedEndTime > stunEndTime)
                {
                    stunEndTime = proposedEndTime;
                    if (currentStunCoroutine != null) StopCoroutine(currentStunCoroutine);
                    currentStunCoroutine = StartCoroutine(StunRoutine(finalDuration));
                }
            }
        }
    }

    public IEnumerator KnockbackRoutine(Vector3 dir, float force)
    {
        isStunned = true;

        bool wasKinematic = false;
        bool hasAgent = (agent != null);

        // [MỚI] Biến lưu trạng thái Root Motion
        bool wasRootMotion = false;

        // 1. TẮT HẲN NAVMESH AGENT (Biện pháp mạnh)
        // isStopped đôi khi không đủ với Humanoid, tắt luôn Component cho chắc
        if (hasAgent)
        {
            //Debug.Log("hasAgent: lấy thành công");
            agent.velocity = Vector3.zero;
            agent.enabled = false; // <--- TẮT HẲN
        }

        // 2. TẠM DỪNG ROOT MOTION
        if (animator != null)
        {
            //Debug.Log("animator: Lấy thành công " );
            wasRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = false; // Tắt Root Motion để Physics hoạt động
        }

        // 3. XỬ LÝ RIGIDBODY (Đẩy Lùi)
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = false; // Bật Physics

            //Debug.Log("rb.isKinematic: " + rb.isKinematic);

            // Reset vận tốc cũ
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Debug xem lực có được add không
            // Debug.Log($"Add Force: {force} theo hướng {dir}");

            // Thêm lực đẩy (Dùng Impulse cho dứt khoát)
            rb.AddForce(dir * force, ForceMode.Impulse);
        }

        // 4. CHỜ THỜI GIAN BAY
        yield return new WaitForSeconds(0.2f);

        // 5. KHÔI PHỤC TRẠNG THÁI
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = wasKinematic;
        }

        // Khôi phục Root Motion
        if (animator != null)
        {
            animator.applyRootMotion = wasRootMotion;
        }

        // Khôi phục Agent
        if (hasAgent)
        {
            agent.enabled = true; // <--- BẬT LẠI
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true; // Vẫn giữ stop vì đang Stun
                agent.ResetPath();      // Xóa đường đi cũ cho sạch
            }
        }

        // 6. CHECK STUN TIẾP
        yield return new WaitForSeconds(0.1f);

        if (Time.time >= stunEndTime)
        {
            isStunned = false;
        }
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        Debug.Log($"{gameObject.name} bị STUN!");

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

    protected virtual void Die()
    {
        if (isDead) return; // Chết rồi không chết lại
        isDead = true;

        Debug.Log($"{gameObject.name} đã chết!");

        // 1. Tắt Collider để không còn là mục tiêu (Raycast/OverlapSphere không thấy nữa)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 2. Tắt Physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true; // Để xác không bị trôi
        }

        // 3. Tắt AI Agent (Nếu là Enemy)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // 4. Play Animation Die
        if (animator != null)
        {
            //animator.SetTrigger("Die");
            // Đảm bảo animator không chuyển sang state khác
            //animator.SetBool("IsDead", true);
        }

        // 5. Vô hiệu hóa Script điều khiển
        // Nếu là Player
        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null) playerCtrl.enabled = false;

        // Nếu là Enemy
        var enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null) enemyAI.enabled = false;

        var enemyCombat = GetComponent<EnemyCombat>();
        if (enemyCombat != null) enemyCombat.enabled = false;

        // 6. Hủy Object sau 3 giây
        Destroy(gameObject, 3.0f);
    }
}