using UnityEngine;

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
    public float baseDashDistance = 3f;
    public float baseDashRecovery = 1f;
    public float dashCost = 20f;
    public float baseDashDuration = 0.2f;

    // [MỚI] Thể lực tiêu hao mỗi giây khi chạy nhanh
    public float runCost = 12.0f;

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

    [Header("--- Tăng thời gian nhận Buff ---")]
    public float buffDurationBonus = 0f;
    // [MỚI] Biến lưu hướng mặt thực tế (Dùng cho CombatMath)
    [HideInInspector] public Vector3 facingDirection = Vector3.back;

    void Start()
    {
        maxHp = baseHp;
        currentHp = maxHp;
        currentStamina = maxStamina;
        currentSin= maxSin;
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
                Debug.Log(">> Out Combat! (Hồi thể lực nhanh, Xoay nhanh)");
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

    public virtual void TakeDamage(float damage)
    {
        if (isInvincible)
        {
            Debug.Log($"{gameObject.name} DODGED damage (Invincible)!");
            return;
        }

        EnterCombat();
        currentHp -= damage;
        Debug.Log($"{gameObject.name} nhận {damage} sát thương! HP còn: {currentHp}/{maxHp}");

        if (currentHp <= 0) Die();
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} đã bị hạ gục!");
    }
}