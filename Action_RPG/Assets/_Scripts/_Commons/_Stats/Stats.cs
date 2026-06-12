using System;
using System.Collections; // Để dùng Coroutine
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;

public class Stats : MonoBehaviour
{
    [Header("--- Identity ---")]
    public string characterName;

    [Header("--- Health ---")]
    public float maxHp;
    public float currentHp;
    public float baseHp;
    [Tooltip("Hằng số HP gốc của nhân vật (từ Database hoặc Inspector). Công thức: baseHp = initialBaseHp + 20 * level")]
    public float initialBaseHp = 100f;
    public float baseHpGain = 2f;

    [Header("--- Level---")]
    public int level = 1;
    public int maxLevel = 60; // Giới hạn cấp độ
    public float exp;
    public float nextLevelExp;
    public float maxExpForCurrentLevel;
    public float percentExpReceive = 1f; // Tỷ lệ nhận EXP (Có thể bị tăng hoặc giảm bởi buffs/debuffs)

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
    public float baseSTR; public float baseDEX; public float baseINT; public float baseVIT; public float baseAGI;
    public float flatSTR; public float flatDEX; public float flatINT; public float flatVIT; public float flatAGI;
    public float bonusSTR; public float bonusDEX; public float bonusINT; public float bonusVIT; public float bonusAGI;
    public float STR; public float DEX; public float INT; public float VIT; public float AGI;

    [Header("--- Attack Stats ---")]
    public float physicalAtk ;
    public float magicAtk ;

    [Header("--- Modifiers ---")]
    public float damageOutputMultiplier = 1.0f; // % sát thương gây ra, default là 100%

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

    [Header("--- Shield & State ---")]
    public float currentShield = 0f; // Lớp giáp ảo
    // [HEALING BLOCK] Nhiều nguồn có thể cùng khóa hồi máu (Ravager, BloodReaver, Accessory MS_T4_01...).
    // Dùng COUNTER dùng chung: mỗi nguồn Push khi bắt đầu khóa, Pop khi hết. Hồi máu chỉ được phép
    // khi count == 0. Tránh việc nguồn này nhả cờ làm mất khóa của nguồn khác.
    private int _healingBlockCount = 0;
    /// <summary>True nếu có ÍT NHẤT 1 nguồn đang khóa hồi máu.</summary>
    public bool isHealingBlocked => _healingBlockCount > 0;
    /// <summary>Đăng ký 1 khóa hồi máu (gọi cặp với PopHealingBlock).</summary>
    public void PushHealingBlock() => _healingBlockCount++;
    /// <summary>Nhả 1 khóa hồi máu. Chỉ thật sự cho hồi lại khi mọi nguồn đã nhả (count==0).</summary>
    public void PopHealingBlock()
    {
        if (_healingBlockCount > 0) _healingBlockCount--;
    }

    // [MỚI] Event báo hiệu bị đánh (Dùng cho JuggernautSkill)
    // Tham số: (Lượng damage thực nhận, Bản thân Stats bị đánh)
    public event Action<float, Stats> OnDamageReceived;

    // [MỚI] Cổng cho phép các Kỹ năng can thiệp trước khi nhận sát thương (DuelistSignature)
    public Func<DamageInfo, bool> damageInterceptor;

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

    // [MỚI] Cờ báo hiệu trạng thái Tàng Hình
    public bool isInvisible = false;

    // --- HIỆU ỨNG CHẢY MÁU (BLEED) ---
    [Header("--- Bleed ---")]
    public bool isBleeding = false;
    private Coroutine bleedCoroutine;
    private float bleedTimer = 0f; // Bộ đếm thời gian còn lại của Bleed
    private float currentBleedDamage = 0f; // Lưu damage để nếu đánh tiếp thì cập nhật damage mới

    // --- HIỆU ỨNG THIÊU ĐỐT (BURN) (magic damage)---
    [Header("--- Burn ---")]
    public bool isBurning = false;
    private Coroutine burnCoroutine;
    private float burnTimer = 0f; // Bộ đếm thời gian còn lại của Burn
    private float currentBurnDamage = 0f; // Lưu damage để nếu đánh tiếp thì cập nhật damage mới
    [Header ("--- Mark ---")]
    [Tooltip("Bị đánh dấu")]
    public bool IsMarked=false;

    [Header("Parry Settings")]
    public bool isParrying = false;       // Đang trong thế thủ
    public bool isPerfectParryWindow = false; // Đang trong "khung giờ vàng"
    [Range(0, 360)] public float parryAngle = 120f;

    [Header("--- Duelist Challenge ---")]
    public bool isChallenged = false; // Cờ báo hiệu bị thách đấu
    private Coroutine challengeCoroutine;

    [Header("--- Resonance Mark (Catalyst) ---")]
    public bool isResonated = false;
    private Coroutine resonanceCoroutine;

    public Action<DamageInfo> OnBeforeTakeDamage;
    public event Action<float, float> OnHealReceived; // <lượng hồi, lượng dư>

    // [ACCESSORY] Bắn KHI VÀ CHỈ KHI sát thương chạm máu thật (Global Rule 3) — kèm DamageInfo
    // để effect biết hướng đánh (info.attacker/sourcePosition) và loại damage (phys/magic/true).
    public event Action<DamageInfo, float> OnDamageTakenHp; // <info, lượng máu thật bị trừ>
    // [ACCESSORY] Bắn khi lớp giáp ảo (Shield) vừa bị đòn đánh phá vỡ về 0.
    public event Action OnShieldBroken;

    private NavMeshAgent agent;
    private Rigidbody rb;
    protected Animator animator;

    public virtual void Start()
    {
        maxHp = baseHp;
        currentHp = maxHp;
        currentStamina = maxStamina;
        currentSin= maxSin;
        // [MỚI] Khởi tạo lượng EXP cần thiết cho level hiện tại ngay khi bắt đầu
        RefreshExpRequirements();
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
    public void ApplyBurn(float damagePerTick, float duration)
    {
        // 1. Cập nhật thông số mới nhất
        currentBurnDamage = damagePerTick; // Có thể làm logic: Lấy damage cao nhất hoặc cộng dồn

        // 2. Gia hạn thời gian (Reset lại đồng hồ đếm ngược)
        burnTimer = duration;

        // Đánh dấu kẻ địch đang bị Burn
        isBurning = true;
        // 3. Chỉ Start Coroutine nếu nó chưa chạy
        if (burnCoroutine == null)
        {
            burnCoroutine = StartCoroutine(BurnRoutine());
        }
    }

    private IEnumerator BurnRoutine()
    {
        // Vòng lặp chạy chừng nào còn thời gian
        while (burnTimer > 0)
        {
            // Chờ 1 giây
            yield return new WaitForSeconds(1.0f);

            // Trừ thời gian
            burnTimer -= 1.0f;

            // Gây sát thương
            TakeDamage(new DamageInfo { magicDamage = currentBurnDamage });
            Debug.Log($"<color=red>{gameObject.name} đang bị thiêu đốt: -{currentBurnDamage} HP (Còn {burnTimer}s)</color>");
        }

        // Hết giờ -> Xóa Coroutine để lần sau Start lại được
        isBurning = false;
        burnCoroutine = null;
    }

    // Hàm gắn ấn thách đấu
    public void ApplyChallengeMark(float duration)
    {
        isChallenged = true;

        // Nếu đang có ấn rồi thì đập đi tính lại thời gian mới
        if (challengeCoroutine != null) StopCoroutine(challengeCoroutine);
        challengeCoroutine = StartCoroutine(ChallengeRoutine(duration));
    }

    private IEnumerator ChallengeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isChallenged = false;
        challengeCoroutine = null;
    }

    // [MỚI] Hàm dùng để gắn dấu ấn Cộng Hưởng
    public void ApplyResonanceMark(float duration)
    {
        isResonated = true;

        // Nếu đang có dấu ấn rồi thì reset lại thời gian
        if (resonanceCoroutine != null) StopCoroutine(resonanceCoroutine);
        resonanceCoroutine = StartCoroutine(ResonanceRoutine(duration));
    }

    private IEnumerator ResonanceRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isResonated = false;
        resonanceCoroutine = null;
    }

    // [CẬP NHẬT] Hàm tiêu hao thể lực dùng chung cho Dash và Run
    public bool TryConsumeStamina(float amount, bool isDash = false)
    {
        EquipmentManager eq = GetComponent<EquipmentManager>();
        string shieldId = (eq != null && eq.currentCoreShield != null) ? eq.currentCoreShield.id : "";

        // SHD_CS_T5_02: Tiêu hao HP thay vì Stamina (x5)
        if (shieldId == "SHD_CS_T5_02")
        {
            float hpCost = amount * 5f;
            if (currentHp > hpCost)
            {
                currentHp -= hpCost;
                return true;
            }
            return false;
        }

        // SHD_CS_T5_04: Dash tốn 10% Sin, không tốn Stamina
        if (shieldId == "SHD_CS_T5_04")
        {
            float sinCost = maxSin * 0.1f;
            if (currentSin >= sinCost)
            {
                currentSin -= sinCost;
                return true;
            }
            return false;
        }
        // [MỚI FIX] WPN_SW_T3_02: Giảm 10% lượng Stamina tiêu hao
        WeaponData wep = eq != null ? eq.currentWeapon : null;
        if (wep != null && wep.id.Trim() == "WPN_SW_T3_02")
        {
            amount *= 0.9f; // Giảm 10%
        }

        // Logic mặc định
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            lastStaminaConsumeTime = Time.time;
            return true;
        }
        return false;
    }

    public virtual void TakeDamage(DamageInfo info)
    {
        if (isInvincible || isDead) return;
        // [MỚI] Cho phép các Khiên can thiệp trước khi tính toán sát thương (Giảm/Phản DMG/Cứu tử)
        OnBeforeTakeDamage?.Invoke(info);
        // [MỚI] Cho phép Signature chặn sát thương
        if (damageInterceptor != null && damageInterceptor.Invoke(info))
        {
            return; // Nếu Interceptor trả về true -> Kẻ địch đã sập bẫy, HỦY việc mất máu!
        }

        EnterCombat();
        // 1. TÍNH TOÁN DAMAGE VÀ SHIELD
        float damageToTake = info.TotalRawDamage;

        // [MỚI] KIỂM TRA CỘNG HƯỞNG TỪ COMPANION
        // Giả sử Companion của bạn có tag là "Companion" và khi đánh có truyền info.attacker = stats của nó
        if (isResonated && info.attacker != null && info.attacker.CompareTag("Ally"))
        {
            damageToTake *= 1.30f; // Tăng 30% sát thương
            Debug.Log($"<color=orange>Cộng Hưởng!</color> Sát thương từ Companion tăng lên: {damageToTake}");
        }

        // [MỚI] Trừ vào Shield trước (Nếu có)
        if (currentShield > 0)
        {
            if (damageToTake == 0)
            {
                Debug.Log($"{gameObject.name} chặn toàn bộ sát thương bằng Shield!");
            }
            float damageBlocked = Mathf.Min(damageToTake, currentShield);
            currentShield -= damageBlocked;
            damageToTake -= damageBlocked;

            Debug.Log($"<color=yellow>Shield blocked: {damageBlocked}. Remaining Shield: {currentShield}");

            // [ACCESSORY] Shield vừa vỡ do đòn này (về 0 sau khi chặn) → rung chuông
            if (currentShield <= 0f && damageBlocked > 0f)
                OnShieldBroken?.Invoke();
        }

        // 2. TRỪ MÁU (Nếu damage vẫn còn sau khi phá shield)
        if (damageToTake > 0)
        {
            currentHp -= damageToTake;
            Debug.Log($"{gameObject.name} nhận {damageToTake}");
            OnDamageReceived?.Invoke(damageToTake, this);
            // [ACCESSORY] Global Rule 3: event này CHỈ bắn khi sát thương chạm máu thật
            // (shield gánh hết thì không bắn). Kèm DamageInfo để biết hướng đánh/loại damage.
            OnDamageTakenHp?.Invoke(info, damageToTake);
        }

        // Hiển thị số sát thương nổi lên màn hình (cả bị chặn lẫn xuyên qua Shield)
        DamageNumberManager.Show(info, transform.position);

        // 3. XỬ LÝ HIỆU ỨNG (CC)
        ApplyCrowdControl(info);

        // 4. KIỂM TRA CHẾT
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
            physDamage = damage, // Mặc định nhận sát thương vào mục Vật Lý
            isCrit = false,
            isStun = false,
            isKnockback = false
        };
        TakeDamage(info);
    }
    // Sửa hàm hồi máu (nếu bạn có hàm Heal riêng, hoặc sửa trực tiếp chỗ nào cộng máu)
    // showPopup=false dùng cho heal tự nhiên (HpGain regen) để tránh spam số
    // isLifesteal=true: hồi máu từ HÚT MÁU (PhysicalLifeSteal/MagicLifeSteal) — KHÔNG bị
    //   healing-block chặn (GDD: MS_T5_02 "không nhận hồi máu từ skill/potion, CHỈ hút máu";
    //   MS_T4_01 "vô hiệu mọi hồi máu" áp cho heal thường). Các nguồn skill/potion để mặc định false.
    public virtual void Heal(float amount, bool showPopup = true, bool isLifesteal = false)
    {
        if (isDead) return;
        // Healing-block: chặn mọi hồi máu TRỪ hút máu.
        if (isHealingBlocked && !isLifesteal) return;

        float excess = (currentHp + amount) - maxHp;
        if (excess < 0) excess = 0;

        currentHp += amount;
        if (currentHp > maxHp) currentHp = maxHp;

        OnHealReceived?.Invoke(amount, excess);
        if (showPopup && amount > 0f)
            DamageNumberManager.ShowHeal(amount, transform.position);
    }

    /// <summary>
    /// Thêm giáp ảo. Nếu duration > 0, sẽ tự động xóa đúng lượng đã thêm sau khi hết thời gian.
    /// </summary>
    public void AddShield(float amount, float duration = 0f)
    {
        if (amount <= 0f) return;
        currentShield += amount;
        if (duration > 0f)
            StartCoroutine(RemoveShieldAfterDelay(amount, duration));
    }

    private IEnumerator RemoveShieldAfterDelay(float amount, float duration)
    {
        yield return new WaitForSeconds(duration);
        currentShield = Mathf.Max(0f, currentShield - amount);
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
        // [CHƯA CÓ CLIP] Death animation: code đã sẵn sàng, tạm comment animator.Set, chỉ log.
        // → Khi đã thêm parameter "Die"(Trigger)/"IsDead"(Bool) vào Animator Controller + clip,
        //   bỏ comment 2 dòng animator.Set bên dưới là chạy.
        if (animator != null)
        {
            // animator.SetTrigger("Die");
            Debug.Log($"[Stats] {gameObject.name} → Death anim (comment) animator.SetTrigger(\"Die\")");
            // Đảm bảo animator không chuyển sang state khác
            // animator.SetBool("IsDead", true);
            Debug.Log($"[Stats] {gameObject.name} → Death anim (comment) animator.SetBool(\"IsDead\", true)");
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
    // [ĐÃ SỬA] Hàm Hồi Sinh an toàn vật lý
    public virtual void Revive(float hpPercent)
    {
        if (!isDead) return;
        isDead = false;
        currentHp = maxHp * hpPercent;

        // 1. Reset sạch động lượng (Tránh việc bị lưu lực đẩy từ lúc chết)
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Tự động nhận diện: 
            // Nếu là AI (có NavMeshAgent) -> Khóa vật lý (isKinematic = true)
            // Nếu là Player điều khiển -> Mở vật lý (isKinematic = false)
            rb.isKinematic = (GetComponent<UnityEngine.AI.NavMeshAgent>() != null);
        }

        // 2. Bật lại NavMeshAgent TRƯỚC
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;

        // 3. Bật lại Collider SAU (Để an toàn)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Reset Animation
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        Debug.Log($"<color=green>{gameObject.name} ĐÃ ĐƯỢC HỒI SINH!</color>");
    }
    // [MỚI] Hàm giải phóng nhân vật khỏi mọi trạng thái khống chế hiện tại
    public void BreakCrowdControl()
    {
        isStunned = false;
        stunEndTime = 0f;

        // Ngắt Coroutine Stun nếu đang chạy
        if (currentStunCoroutine != null)
        {
            StopCoroutine(currentStunCoroutine);
            currentStunCoroutine = null;
        }

        // Triệt tiêu động lượng (Lực đẩy lùi Knockback)
        if (rb != null && !isDead)
        {
            rb.linearVelocity = Vector3.zero;
        }

        // Đảm bảo bật lại NavMeshAgent nếu lỡ bị KnockbackRoutine tắt đi
        if (agent != null && !agent.enabled && !isDead)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        Debug.Log($"<color=orange>{gameObject.name} đã THOÁT KHỎI KHỐNG CHẾ!</color>");
    }
    // [MỚI] Hàm xóa các hiệu ứng bất lợi dạng DoT hiện có (Bleed, Burn)
    public void ClearDebuffs()
    {
        if (bleedCoroutine != null) { StopCoroutine(bleedCoroutine); bleedCoroutine = null; }
        bleedTimer = 0f;
        isBleeding = false;

        if (burnCoroutine != null) { StopCoroutine(burnCoroutine); burnCoroutine = null; }
        burnTimer = 0f;
        isBurning = false;
    }
    // ==========================================
    // [MỚI] HỆ THỐNG KINH NGHIỆM VÀ LÊN CẤP
    // ==========================================
    public float GetNextLevelExp()
    {
        // Công thức đường cong EXP: 100 * (level ^ 1.1)
        return Mathf.Floor(100f * Mathf.Pow(level, 1.1f));
    }

    // Hàm dùng chung để tính công thức EXP
    protected float CalculateExpRequirement(int currentLevel)
    {
        return Mathf.Floor(100f * Mathf.Pow(currentLevel, 1.1f));
    }

    public void RefreshExpRequirements()
    {
        float requiredExp = level >= maxLevel ? 0f : CalculateExpRequirement(level);
        nextLevelExp = requiredExp;
        maxExpForCurrentLevel = requiredExp;
    }

    public void AddExp(float amount)
    {
        if (level >= maxLevel) return;

        float finalExp = amount * percentExpReceive;
        exp += finalExp;
        RefreshExpRequirements();

        while (exp >= nextLevelExp && level < maxLevel)
        {
            exp -= nextLevelExp; // Trừ đi lượng exp đã dùng để lên cấp
            LevelUp();
            RefreshExpRequirements();
        }

        if (level >= maxLevel)
        {
            exp = 0;
            nextLevelExp = 0; // Set về 0 hoặc giữ nguyên tùy ý bạn cho UI hiển thị chữ "MAX"
            maxExpForCurrentLevel = 0f;
            Debug.Log($"<color=orange>{gameObject.name} đã đạt Cấp Tối Đa ({maxLevel})!</color>");
        }
    }

    protected virtual void LevelUp()
    {
        level++;
        RefreshExpRequirements();
        Debug.Log($"<color=yellow>LEVEL UP!</color> {gameObject.name} đã đạt cấp {level}!");
    }
}