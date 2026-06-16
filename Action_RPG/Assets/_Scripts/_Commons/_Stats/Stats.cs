using System;
using System.Collections; // Để dùng Coroutine
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;

/// <summary>Nguồn hồi máu — để effect phân biệt máu hồi từ đâu (skill, potion, hút máu, regen...).</summary>
public enum HealSource { Other, Skill, Potion, Lifesteal, Regen, Drain }

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
    public float damageTakenMultiplier = 1.0f;  // % sát thương NHẬN vào (1 = 100%; >1 dễ tổn thương; <1 giảm nhận)

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

    // [COMPANION MTX_DEF_T3_01] Miễn nhiễm mọi khống chế (stun/knockback) khi true.
    public bool ccImmune = false;

    // [COMPANION PRT_SUP_T3_02] Bị Trầm Mặc (cấm dùng kĩ năng). Giữ public để code cũ set trực tiếp
    // không vỡ; nhưng KHUYẾN NGHỊ dùng ApplyCombatEffects(Silence) / IsSilenced để an toàn (counter).
    public bool isSilenced = false;

    // ─────────────────────────────────────────────────────────────
    //  [MỚI] ACTION-LOCK STATE — quản lý qua endTime/counter, an toàn khi chồng nhiều nguồn.
    //  Player/Enemy/Companion nên dùng các query IsXxx thay vì tự check rải rác.
    // ─────────────────────────────────────────────────────────────
    private bool  _isAirborne = false;   // đang bị hất tung (khóa MỌI hành động)
    private float _airborneEndTime = 0f; // thời điểm hết Airborne (cho phép nhiều nguồn chồng → lấy mốc xa nhất)
    private float _rootEndTime = 0f;     // bị trói chân tới thời điểm này
    private int   _silenceTokens = 0;    // số nguồn Silence đang giữ (counter)
    private float _silenceEndTime = 0f;  // thời điểm hết Silence (cho nguồn theo duration)

    /// <summary>Đang bị hất tung — khóa mọi hành động, không cleanse được.</summary>
    public bool IsAirborne => _isAirborne;
    /// <summary>Đang bị trói chân — khóa di chuyển/dash, KHÔNG khóa skill.</summary>
    public bool IsRooted => Time.time < _rootEndTime;
    /// <summary>Đang bị câm lặng — khóa Skill/Signature. True nếu có token hoặc cờ legacy hoặc còn endTime.</summary>
    public bool IsSilenced => isSilenced || _silenceTokens > 0 || Time.time < _silenceEndTime;

    /// <summary>Khóa MỌI hành động (Airborne, hoặc Stun làm khóa attack/move). Dùng cho move+attack+item.</summary>
    public bool IsActionLocked => isDead || _isAirborne || isStunned;
    /// <summary>Khóa di chuyển/dash (Airborne, Stun, Root).</summary>
    public bool IsMovementLocked => isDead || _isAirborne || isStunned || IsRooted;
    /// <summary>Khóa Skill/Signature (Airborne, Stun, Silence). Root KHÔNG khóa skill.</summary>
    public bool IsSkillLocked => isDead || _isAirborne || isStunned || IsSilenced;
    /// <summary>Khóa dùng vật phẩm/cleanse (Airborne, Stun).</summary>
    public bool IsItemLocked => isDead || _isAirborne || isStunned;

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

    // [CC INTERRUPT] Bắn khi bị STUN hoặc KNOCKBACK bắt đầu → người nghe (PlayerController / EnemyCombat)
    // tự hủy đòn đánh / skill đang thực hiện. Stun/knockback "ngắt hành động" — chuẩn ARPG.
    public event Action OnInterrupted;
    /// <summary>Gọi để báo các hệ điều khiển hủy hành động hiện tại (do CC).</summary>
    protected void RaiseInterrupted() => OnInterrupted?.Invoke();
    public event Action<float, float> OnHealReceived; // <lượng hồi, lượng dư>
    public event Action<float, float, HealSource> OnHealDetailed; // <lượng hồi, dư, nguồn>

    // [ACCESSORY] Bắn KHI VÀ CHỈ KHI sát thương chạm máu thật (Global Rule 3) — kèm DamageInfo
    // để effect biết hướng đánh (info.attacker/sourcePosition) và loại damage (phys/magic/true).
    public event Action<DamageInfo, float> OnDamageTakenHp; // <info, lượng máu thật bị trừ>
    // [ACCESSORY] Bắn khi lớp giáp ảo (Shield) vừa bị đòn đánh phá vỡ về 0.
    public event Action OnShieldBroken;

    private NavMeshAgent agent;
    private Rigidbody rb;
    protected Animator animator;

    // [HURT ANIM] Chống spam: chỉ cho phép trigger Hurt mỗi hurtAnimCooldown giây.
    [Header("--- Hurt Reaction ---")]
    [Tooltip("Khoảng cách tối thiểu giữa 2 lần chạy animation Hurt (giây). Tránh giật liên tục khi bị đánh dồn.")]
    public float hurtAnimCooldown = 0.4f;
    private float _lastHurtTime = -999f;
    private EnemyCombat _enemyCombatCache; // null nếu không phải enemy → check rẻ

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

            // Gây sát thương (Bleed = DoT vật lý)
            TakeDamage(new DamageInfo { physDamage = currentBleedDamage, sourceType = DamageSourceType.DoT });
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

            // Gây sát thương (Burn = DoT phép)
            TakeDamage(new DamageInfo { magicDamage = currentBurnDamage, sourceType = DamageSourceType.DoT });
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
    public bool TryConsumeStamina(float amount, bool isDash = false, bool isMovement = false)
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

        // SHD_CS_T5_04: tiêu Sin thay Stamina.
        //  • Dash (isDash): tốn cố định 10% maxSin.
        //  • Hành động khác (chạy nhanh/HeavyAttack...): tốn Sin BẰNG ĐÚNG lượng Stamina (1:1)
        //    → giữ tốc độ tiêu hao khi sprint giống hệt khi dùng Stamina (không cạn Sin tức thì).
        if (shieldId == "SHD_CS_T5_04")
        {
            float sinCost = isDash ? maxSin * 0.1f : amount;
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

        // [ACCESSORY] Hook tiêu hao Stamina.
        AllyStats accAlly = this as AllyStats;
        // ACC_CH_T4_04: dash/chạy nhanh KHÔNG cập nhật mốc tiêu Stamina → không gián đoạn hồi tự nhiên.
        bool noStaminaInterrupt = false;
        if (accAlly != null)
        {
            // ACC_CH_T5_04: HP > 80% → Stamina không bao giờ giảm.
            if (accAlly.accStaminaFreeWhileHighHp && currentHp > maxHp * 0.80f) return true;
            // ACC_RM_T5_02: giảm % Stamina tiêu hao.
            amount *= accAlly.accStaminaConsumeMult;
            noStaminaInterrupt = accAlly.accDashSprintNoRegenInterrupt && (isDash || isMovement);
        }

        // Logic mặc định
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            if (!noStaminaInterrupt) lastStaminaConsumeTime = Time.time;
            // ACC_CH_T5_05: tiêu Stamina → nhận Sin = % lượng Stamina tiêu hao.
            if (accAlly != null && accAlly.accStaminaToSinGain > 0f)
                currentSin = Mathf.Min(maxSin, currentSin + amount * accAlly.accStaminaToSinGain);
            return true;
        }

        // ACC_CH_T5_02: thiếu Stamina → trả phần thiếu bằng Máu (giữ tối thiểu 1 HP).
        if (accAlly != null && accAlly.accHpPerStaminaOverride > 0f)
        {
            float missing = amount - currentStamina;
            float hpCost = missing * accAlly.accHpPerStaminaOverride;
            if (currentHp - hpCost >= 1f)
            {
                currentHp -= hpCost;
                currentStamina = 0f;
                if (!noStaminaInterrupt) lastStaminaConsumeTime = Time.time;
                return true;
            }
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
        float damageToTake = info.TotalRawDamage * damageTakenMultiplier; // dễ tổn thương / giảm nhận (companion debuff/buff)

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

        // [HURT ANIM] Phản ứng giật mình khi MẤT MÁU THẬT và chưa chết — safe-set + có điều kiện.
        // Player animator không có "Hurt" nên tự bỏ qua; SlimeAnimator (enemy) có thì chạy.
        if (animator != null && damageToTake > 0f && currentHp > 0f && HasParameter(animator, "Hurt"))
            TryPlayHurt();

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
    public virtual void Heal(float amount, bool showPopup = true, bool isLifesteal = false, HealSource source = HealSource.Other)
    {
        if (isDead) return;
        if (isLifesteal) source = HealSource.Lifesteal;
        // Healing-block: chặn mọi hồi máu TRỪ hút máu.
        if (isHealingBlocked && !isLifesteal) return;

        // [ACC_MS_T5_02] Chỉ nhận hồi máu từ Hút máu; và Hút máu x3 khi HP<50%.
        AllyStats accHealAlly = this as AllyStats;
        if (accHealAlly != null)
        {
            if (accHealAlly.accOnlyLifestealHeal && source != HealSource.Lifesteal) return;
            if (accHealAlly.accLifestealTripleLowHp && source == HealSource.Lifesteal && currentHp < maxHp * 0.5f)
                amount *= 3f;
        }

        float excess = (currentHp + amount) - maxHp;
        if (excess < 0) excess = 0;

        currentHp += amount;
        if (currentHp > maxHp) currentHp = maxHp;

        OnHealReceived?.Invoke(amount, excess);
        OnHealDetailed?.Invoke(amount, excess, source);
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
    // Giữ tên cũ làm wrapper để mọi call-site cũ không vỡ; nội bộ chuyển sang ApplyCombatEffects.
    void ApplyCrowdControl(DamageInfo info) => ApplyCombatEffects(info);

    /// <summary>
    /// Entry point THỐNG NHẤT xử lý mọi hiệu ứng CC của 1 đòn đánh.
    /// - Bridge legacy isStun/isKnockback → effects list (không double-apply).
    /// - ccImmune + super armor chặn như cũ.
    /// - PHASE 1: giữ NGUYÊN math resistance hiện tại (stun + knockback đều dùng resistanceKnockBack)
    ///   để không đổi gameplay. Việc tách stun sang resistanceEffect và thêm Airborne/Root/Silence
    ///   handler là phase sau.
    /// </summary>
    public void ApplyCombatEffects(DamageInfo info)
    {
        if (info == null) return;

        // [COMPANION MTX_DEF_T3_01] Miễn nhiễm khống chế.
        if (ccImmune) return;

        if (isSuperArmor && info.impactLevel <= superArmorLevel)
        {
            //Debug.Log("Super Armor Blocked CC!");
            return;
        }

        // Gộp legacy bool vào effects list (idempotent — không thêm nếu nguồn đã AddEffect).
        info.BridgeLegacyEffects();
        if (info.effects == null) return;

        foreach (var eff in info.effects)
        {
            if (eff == null) continue;
            switch (eff.type)
            {
                case CombatEffectType.Knockback:
                    ApplyKnockbackEffect(eff);
                    break;
                case CombatEffectType.Stun:
                    ApplyStunEffect(eff);
                    break;
                case CombatEffectType.Airborne:
                    // Airborne KHÔNG bị resistanceEffect giảm (luôn full duration).
                    Airborne(eff.duration, eff.height);
                    break;
                case CombatEffectType.Root:
                    ApplyRootEffect(eff);
                    break;
                case CombatEffectType.Silence:
                    ApplySilenceEffect(eff);
                    break;
                // Slow: chưa nối (cần move/attack-speed modifier) — để phase sau.
            }
        }
    }

    /// <summary>
    /// Áp 1 hiệu ứng CC THUẦN (không kèm damage) lên mục tiêu — đi qua đúng pipeline
    /// ApplyCombatEffects (ccImmune / super armor / resistance). Dùng cho các nguồn CC trực tiếp
    /// (Companion skill/protocol) thay vì gọi Airborne()/isSilenced= trực tiếp.
    /// </summary>
    public void ApplyEffect(CombatEffectInfo effect, Stats source = null)
    {
        if (effect == null) return;
        var info = new DamageInfo { attacker = source, sourcePosition = source != null ? source.transform.position : transform.position };
        info.AddEffect(effect);
        ApplyCombatEffects(info);
    }

    /// <summary>Resistance áp cho duration nếu effect cho phép. PHASE NÀY dùng resistanceEffect cho
    /// Root/Silence (stun vẫn giữ resistanceKnockBack riêng để không đổi balance enemy hiện tại).</summary>
    private float ResistedDuration(CombatEffectInfo eff)
        => eff.respectEffectResistance ? eff.duration * (1f - resistanceEffect) : eff.duration;

    private void ApplyRootEffect(CombatEffectInfo eff)
    {
        float dur = ResistedDuration(eff);
        if (dur < 0.1f) return;
        // endTime-based: nhiều nguồn root chồng → lấy mốc xa nhất, không restore bừa.
        _rootEndTime = Mathf.Max(_rootEndTime, Time.time + dur);
        if (eff.interruptCurrentAction) RaiseInterrupted();
        if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
    }

    private void ApplySilenceEffect(CombatEffectInfo eff)
    {
        float dur = ResistedDuration(eff);
        if (dur < 0.1f) return;
        // endTime-based: nhiều nguồn silence chồng → lấy mốc xa nhất. IsSilenced tự true tới đó.
        _silenceEndTime = Mathf.Max(_silenceEndTime, Time.time + dur);
        // Silence ngắt skill đang cast nếu effect yêu cầu (player/enemy đang windup → bị mất + cooldown
        // xử lý ở tầng SkillManager/EnemyCombat qua RaiseInterrupted + check IsSilenced).
        if (eff.interruptCurrentAction) RaiseInterrupted();
    }

    private void ApplyKnockbackEffect(CombatEffectInfo eff)
    {
        if (eff.force <= 0f) return;
        // Knockback force dùng kháng riêng resistanceKnockBack (giữ nguyên).
        float finalForce = eff.force * (1.0f - resistanceKnockBack);
        if (finalForce > 0f)
        {
            Vector3 knockbackDir = (transform.position - eff.sourcePosition).normalized;
            knockbackDir.y = 0;
            StartCoroutine(KnockbackRoutine(knockbackDir, finalForce));
        }
    }

    private void ApplyStunEffect(CombatEffectInfo eff)
    {
        // PHASE 1: giữ nguyên — stun vẫn giảm theo resistanceKnockBack (chưa đổi sang resistanceEffect).
        float finalDuration = eff.duration * (1.0f - resistanceKnockBack);
        if (finalDuration < 0.1f) return; // < 0.1s coi như kháng hoàn toàn

        float proposedEndTime = Time.time + finalDuration;
        if (proposedEndTime > stunEndTime)
        {
            stunEndTime = proposedEndTime;
            if (currentStunCoroutine != null) StopCoroutine(currentStunCoroutine);
            currentStunCoroutine = StartCoroutine(StunRoutine(finalDuration));
        }
    }

    public IEnumerator KnockbackRoutine(Vector3 dir, float force)
    {
        isStunned = true;
        RaiseInterrupted(); // Knockback ngắt đòn đánh / skill đang thực hiện

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
        RaiseInterrupted(); // Stun ngắt đòn đánh / skill đang thực hiện
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

    /// <summary>
    /// Chạy animation Hurt CÓ ĐIỀU KIỆN:
    ///  1. Cooldown: không spam khi bị đánh dồn (mỗi hurtAnimCooldown giây 1 lần).
    ///  2. KHÔNG cắt ngang đòn đánh: nếu enemy đang attack/telegraph thì bỏ qua hurt
    ///     → đòn đánh của enemy "lì", player vẫn gây damage + nháy đỏ nhưng không khóa cứng được nó.
    /// (Player không có EnemyCombat nên _enemyCombatCache=null → chỉ áp cooldown.)
    /// </summary>
    private void TryPlayHurt()
    {
        // Cooldown chống spam
        if (Time.time < _lastHurtTime + hurtAnimCooldown) return;

        // Đang đánh/gồng → không cho hurt cắt ngang
        if (_enemyCombatCache == null) _enemyCombatCache = GetComponent<EnemyCombat>();
        if (_enemyCombatCache != null && (_enemyCombatCache.isAttacking || _enemyCombatCache.isTelegraphing))
            return;

        _lastHurtTime = Time.time;
        animator.SetTrigger("Hurt");
    }

    /// <summary>True nếu Animator có parameter tên paramName — tránh spam warning khi set vào param không tồn tại.</summary>
    protected static bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in anim.parameters)
            if (p.name == paramName) return true;
        return false;
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

        // 4. Play Animation Die — safe-set: chỉ set nếu Animator CÓ parameter tương ứng
        // (player animator không có Die/IsDead nên sẽ tự bỏ qua, không spam warning).
        if (animator != null)
        {
            if (HasParameter(animator, "Die"))    animator.SetTrigger("Die");
            // IsDead = true để giữ ở state chết, không chuyển sang state khác
            if (HasParameter(animator, "IsDead")) animator.SetBool("IsDead", true);
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

    // [MỚI] HẤT TUNG (Airborne): nâng mục tiêu lên cao + không hành động được trong 'duration' giây.
    // Re-entrant: nếu đang bay mà bị hất tung tiếp → chỉ KÉO DÀI mốc kết thúc (không tạo routine chồng
    // làm nhân vật lơ lửng / clear cờ lẫn nhau).
    public void Airborne(float duration, float height = 1.2f)
    {
        if (isDead || duration <= 0f) return;
        bool alreadyAirborne = _isAirborne && Time.time < _airborneEndTime;
        _airborneEndTime = Mathf.Max(_airborneEndTime, Time.time + duration);
        if (!alreadyAirborne) StartCoroutine(AirborneRoutine(height));
        // đang bay rồi → routine hiện tại tự chạy tới _airborneEndTime mới, không start thêm.
    }
    private System.Collections.IEnumerator AirborneRoutine(float height)
    {
        isStunned = true;
        _isAirborne = true; // khóa mọi hành động + chặn cleanse trong lúc bay
        RaiseInterrupted(); // hất tung ngắt đòn đánh / skill đang thực hiện
        bool agentWas = agent != null && agent.enabled;
        if (agentWas) agent.enabled = false;
        Vector3 start = transform.position;
        float startTime = Time.time;
        while (Time.time < _airborneEndTime)
        {
            float total = Mathf.Max(0.01f, _airborneEndTime - startTime); // có thể tăng nếu bị hất tung chồng
            float k = Mathf.Clamp01((Time.time - startTime) / total);
            float y = Mathf.Sin(k * Mathf.PI) * height; // bay lên rồi rơi xuống
            transform.position = new Vector3(start.x, start.y + y, start.z);
            yield return null;
        }
        transform.position = start;
        if (agentWas && agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(start);
        }
        _isAirborne = false;
        // [IDEMPOTENT] Chỉ gỡ stun nếu KHÔNG còn stun timer đang chạy — tránh xóa nhầm stun từ nguồn khác
        // overlap (stun đó tự có StunRoutine quản tới stunEndTime).
        if (Time.time >= stunEndTime) isStunned = false;
    }

    // [MỚI] Hàm giải phóng nhân vật khỏi mọi trạng thái khống chế hiện tại
    public void BreakCrowdControl()
    {
        // RULE: KHÔNG cleanse được trong lúc bị hất tung (Airborne).
        if (_isAirborne) return;

        // Cleanse Silence (theo duration). Counter-based silence do nguồn tự quản, không xóa ở đây.
        _silenceEndTime = 0f;
        // Cleanse Root.
        _rootEndTime = 0f;

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