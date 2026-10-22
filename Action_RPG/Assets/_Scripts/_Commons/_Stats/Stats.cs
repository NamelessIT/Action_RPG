using System;
using System.Collections; // Để dùng Coroutine
using System.Collections.Generic; // List<> cho Slow sources
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization; // FormerlySerializedAs (giữ data khi đổi tên field)
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

    // ─────────────────────────────────────────────────────────────
    //  [P2-DATA-01B] BASE STAT WRITE API — source-of-truth boundary.
    //  Gameplay code KHÔNG ghi thẳng base* / initialBaseHp / maxStamina /
    //  maxSin / baseAttackSpeed / baseMoveSpeed nữa; đi qua các API dưới đây.
    //  Phase này CHƯA đổi serialized shape: storage vẫn là public field ở root
    //  (Unity không migrate được root → nested; xem P2-DATA-01C).
    //  API không tự gọi RecalculateStats() — caller giữ nguyên thứ tự recalc cũ.
    // ─────────────────────────────────────────────────────────────

    /// <summary>Năm chỉ số gốc phân phối được. Tách riêng khỏi StatModifier.StatType (enum đó gồm cả bonus/flat).</summary>
    public enum BaseAttribute { STR, DEX, INT, VIT, AGI }

    /// <summary>Snapshot base stats dùng để load/save. Là DTO thuần, KHÔNG serialize trên Stats.</summary>
    [System.Serializable]
    public struct BaseStatSnapshot
    {
        public float initialBaseHp;
        public float baseHp;
        public float maxStamina;
        public float baseSTR, baseDEX, baseINT, baseVIT, baseAGI;
    }

    public float GetBaseAttribute(BaseAttribute a)
    {
        switch (a)
        {
            case BaseAttribute.STR: return baseSTR;
            case BaseAttribute.DEX: return baseDEX;
            case BaseAttribute.INT: return baseINT;
            case BaseAttribute.VIT: return baseVIT;
            case BaseAttribute.AGI: return baseAGI;
            default: return 0f;
        }
    }

    public void SetBaseAttribute(BaseAttribute a, float value)
    {
        switch (a)
        {
            case BaseAttribute.STR: baseSTR = value; break;
            case BaseAttribute.DEX: baseDEX = value; break;
            case BaseAttribute.INT: baseINT = value; break;
            case BaseAttribute.VIT: baseVIT = value; break;
            case BaseAttribute.AGI: baseAGI = value; break;
        }
    }

    public void AddBaseAttribute(BaseAttribute a, float amount)
        => SetBaseAttribute(a, GetBaseAttribute(a) + amount);

    public float InitialBaseHp => initialBaseHp;
    public void SetInitialBaseHp(float value) => initialBaseHp = value;
    public void AddInitialBaseHp(float amount) => initialBaseHp += amount;

    public void SetMaxStamina(float value) => maxStamina = value;
    public void SetMaxSin(float value) => maxSin = value;

    public void SetBaseAttackSpeed(float value) => baseAttackSpeed = value;
    public void AddBaseAttackSpeed(float amount) => baseAttackSpeed += amount;
    /// <summary>Nhân baseAttackSpeed (buff/debuff tạm). Caller phải gọi lại với nghịch đảo để revert — không tự track.</summary>
    public void MultiplyBaseAttackSpeed(float factor) { if (factor > 0f) baseAttackSpeed *= factor; }

    public void SetBaseMoveSpeed(float value) => baseMoveSpeed = value;

    /// <summary>Nạp base stats từ save/data. Giá trị ≤0 trong snapshot vẫn được ghi — caller tự lo fallback.</summary>
    public void ApplyBaseRuntimeStats(BaseStatSnapshot src, bool resetCurrentVitals)
    {
        initialBaseHp = src.initialBaseHp;
        maxStamina    = src.maxStamina;
        baseSTR = src.baseSTR; baseDEX = src.baseDEX; baseINT = src.baseINT;
        baseVIT = src.baseVIT; baseAGI = src.baseAGI;
        if (resetCurrentVitals) { currentHp = maxHp; currentStamina = maxStamina; }
    }

    /// <summary>Đọc base stats ra DTO để save. `baseHp` là giá trị DẪN XUẤT (initialBaseHp + 20*level).</summary>
    public BaseStatSnapshot CaptureRuntimeStats() => new BaseStatSnapshot
    {
        initialBaseHp = initialBaseHp,
        baseHp        = baseHp,
        maxStamina    = maxStamina,
        baseSTR = baseSTR, baseDEX = baseDEX, baseINT = baseINT,
        baseVIT = baseVIT, baseAGI = baseAGI,
    };

    [Header("--- Rotation Dynamic ---")]
    public float turnDuration = 0.1f;
    private float idleTurnDuration = 0.1f;
    public float combatTurnDuration;

    [Header("--- Knockback & Effect Res ---")]
    [Tooltip("CHỈ giảm LỰC đẩy lùi (Knockback). KHÔNG còn ảnh hưởng Stun/Root/Silence/Slow/Taunt/Fear — dùng resistanceEffect.")]
    [FormerlySerializedAs("resistanceKnockBack")]
    public float knockbackResistance = 0.1f;
    [Tooltip("Giảm THỜI LƯỢNG CC Stun/Root/Silence/Slow/Taunt/Fear. Airborne/Knockback/Pull bỏ qua.")]
    public float resistanceEffect = 0f;

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

    /// <summary>Phase D: mọi Airborne/Knockback/Pull đều do DisplacementController sở hữu.</summary>
    public bool IsDisplaced
    {
        get
        {
            EnsureStatusInfrastructure();
            return _displacementController != null && _displacementController.IsActive;
        }
    }
    public bool IsKnockbackActive
    {
        get
        {
            EnsureStatusInfrastructure();
            return _displacementController != null && _displacementController.ActiveKind == DisplacementKind.Knockback;
        }
    }
    public bool IsPullActive
    {
        get
        {
            EnsureStatusInfrastructure();
            return _displacementController != null && _displacementController.ActiveKind == DisplacementKind.Pull;
        }
    }

    // [COMPANION MTX_DEF_T3_01] Miễn nhiễm mọi khống chế — COUNTER source-safe (nhiều nguồn cùng giữ).
    // Mỗi nguồn Push khi bật, Pop khi tắt; chỉ hết miễn nhiễm khi mọi nguồn đã nhả (count==0).
    private int _ccImmuneCount = 0;
    private int _slowImmuneCount = 0;
    internal bool LegacyCrowdControlImmune => _ccImmuneCount > 0;
    internal bool LegacySlowImmune => _slowImmuneCount > 0;

    /// <summary>True nếu có ít nhất 1 nguồn miễn nhiễm CC (legacy counter hoặc gateway mới).</summary>
    public bool IsCrowdControlImmune => _statusEffects != null ? _statusEffects.HasCrowdControlImmunity : LegacyCrowdControlImmune;
    /// <summary>True nếu có ít nhất 1 nguồn miễn nhiễm Slow.</summary>
    public bool IsSlowImmune => _statusEffects != null ? _statusEffects.HasSlowImmunity : LegacySlowImmune;
    /// <summary>Backward-compat đọc-only.</summary>
    public bool ccImmune => IsCrowdControlImmune;

    /// <summary>Bật CC immunity: lần đầu bật sẽ cleanse CC được phép cleanse trước.</summary>
    public void PushCrowdControlImmunity()
    {
        if (_ccImmuneCount == 0)
        {
            EnsureStatusInfrastructure();
            if (_statusEffects != null) _statusEffects.CleanseCrowdControl();
            else ExecuteLegacyCrowdControlCleanse();
        }
        _ccImmuneCount++;
    }

    public void PopCrowdControlImmunity() { if (_ccImmuneCount > 0) _ccImmuneCount--; }

    /// <summary>Bật Slow immunity: cleanse toàn bộ Slow hiện tại trước rồi chặn Slow mới.</summary>
    public void PushSlowImmunity()
    {
        if (_slowImmuneCount == 0)
        {
            EnsureStatusInfrastructure();
            if (_statusEffects != null) _statusEffects.CleanseSlow();
            else ExecuteLegacySlowCleanse();
        }
        _slowImmuneCount++;
    }

    public void PopSlowImmunity() { if (_slowImmuneCount > 0) _slowImmuneCount--; }

    // [LEGACY COMPAT] Field cũ giữ lại vì asset/code cũ có thể đọc trực tiếp.
    // Phase C runtime Silence chính nằm trong StatusEffectController.
    public bool isSilenced = false;
    private int _silenceTokens = 0; // legacy token bridge; hiện không còn producer trực tiếp.

    // ─────────────────────────────────────────────────────────────
    //  [PHASE D] CONTROL STATE BRIDGE
    //  Slow / Stun / Root / Silence source-of-truth: StatusEffectController.
    //  Airborne / Knockback / Pull source-of-truth: DisplacementController.
    // ─────────────────────────────────────────────────────────────
    public bool IsAirborne
    {
        get
        {
            EnsureStatusInfrastructure();
            return _displacementController != null && _displacementController.ActiveKind == DisplacementKind.Airborne;
        }
    }
    /// <summary>Stun cleanseable do StatusEffectController quản; không đồng nhất với displacement.</summary>
    public bool IsStunned
    {
        get
        {
            EnsureStatusInfrastructure();
            return _statusEffects != null && _statusEffects.IsStunned;
        }
    }
    /// <summary>Root source-aware do StatusEffectController quản.</summary>
    public bool IsRooted
    {
        get
        {
            EnsureStatusInfrastructure();
            return _statusEffects != null && _statusEffects.IsRooted;
        }
    }
    /// <summary>Silence source-aware; legacy flag/token được OR để không phá compatibility.</summary>
    public bool IsSilenced
    {
        get
        {
            EnsureStatusInfrastructure();
            bool managed = _statusEffects != null && _statusEffects.IsSilenced;
            return managed || isSilenced || _silenceTokens > 0;
        }
    }

    /// <summary>Legacy read field được sync để các call-site cũ như AccessoryEffectManager không vỡ.</summary>
    public bool isStunned = false;

    private bool IsForcedControlled
    {
        get
        {
            EnsureStatusInfrastructure();
            return _statusEffects != null && (_statusEffects.IsTaunted || _statusEffects.IsFeared);
        }
    }

    public bool IsActionLocked => isDead || IsDisplaced || IsStunned || IsForcedControlled;
    public bool IsMovementLocked => isDead || IsDisplaced || IsStunned || IsRooted || IsForcedControlled;
    public bool IsSkillLocked => isDead || IsDisplaced || IsStunned || IsSilenced || IsForcedControlled;
    public bool IsItemLocked => isDead || IsDisplaced || IsStunned;

    /// <summary>Legacy API wrapper: direct Slow caller vẫn đi qua gateway chuẩn Phase C.</summary>
    public void ApplySlow(float magnitude, float duration)
    {
        magnitude = Mathf.Clamp01(magnitude);
        if (magnitude <= 0f || duration <= 0f) return;
        ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, duration)
        {
            magnitude = magnitude,
            effectId = "LEGACY_APPLY_SLOW"
        });
    }

    public float CurrentSlowAmount
    {
        get
        {
            EnsureStatusInfrastructure();
            return _statusEffects != null ? _statusEffects.CurrentSlowAmount : 0f;
        }
    }

    public float EffectiveSlowMultiplier => 1f - CurrentSlowAmount;

    /// <summary>
    /// Tốc độ di chuyển runtime thống nhất. Với Stats/Enemy: Slow nhân vào baseMoveSpeed.
    /// AllyStats override để Slow đóng vai trò bonusMoveSpeed âm đúng game-design.
    /// </summary>
    public virtual float EffectiveMoveSpeed
    {
        get
        {
            EnsureStatusInfrastructure();
            float additive = _runtimeModifiers != null
                ? _runtimeModifiers.GetAdditivePercent(RuntimeStatKey.BaseMoveSpeedMultiplier)
                : 0f;
            float multiplicative = _runtimeModifiers != null
                ? _runtimeModifiers.GetMultiplicativeFactor(RuntimeStatKey.BaseMoveSpeedMultiplier)
                : 1f;
            float baseWithRuntime = baseMoveSpeed * Mathf.Max(0f, 1f + additive) * multiplicative;
            return Mathf.Max(0f, baseWithRuntime * EffectiveSlowMultiplier);
        }
    }

    /// <summary>Hệ số move runtime so với baseMoveSpeed, dùng cho dash/movement đặc biệt muốn tôn trọng Slow.</summary>
    public float EffectiveMoveSpeedMultiplier
        => baseMoveSpeed > 0.0001f ? EffectiveMoveSpeed / baseMoveSpeed : 1f;

    /// <summary>
    /// Tốc độ đánh runtime thống nhất. Slow KHÔNG tác động lên giá trị này.
    /// AttackSpeedDown ở phase Debuff sẽ đi qua RuntimeStatModifierLedger.
    /// </summary>
    public virtual float EffectiveAttackSpeed
    {
        get
        {
            EnsureStatusInfrastructure();
            float additive = _runtimeModifiers != null
                ? _runtimeModifiers.GetAdditivePercent(RuntimeStatKey.BaseAttackSpeedMultiplier)
                : 0f;
            float multiplicative = _runtimeModifiers != null
                ? _runtimeModifiers.GetMultiplicativeFactor(RuntimeStatKey.BaseAttackSpeedMultiplier)
                : 1f;
            return Mathf.Max(0.01f, baseAttackSpeed * Mathf.Max(0f, 1f + additive) * multiplicative);
        }
    }

    // Phase F2 consumer path: raw serialized fields remain the base/source values.
    // Managed Debuff modifiers live in RuntimeStatModifierLedger and are read through these
    // effective properties so producer migration never has to destructively mutate base stats.
    private float ResolveRuntimeModifiedStat(float baseValue, RuntimeStatKey stat)
    {
        EnsureStatusInfrastructure();
        if (_runtimeModifiers == null) return baseValue;

        float flat = _runtimeModifiers.GetFlat(stat);
        float additive = _runtimeModifiers.GetAdditivePercent(stat);
        float multiplicative = _runtimeModifiers.GetMultiplicativeFactor(stat);
        return (baseValue + flat) * (1f + additive) * multiplicative;
    }

    public float EffectiveArmor => ResolveRuntimeModifiedStat(armor, RuntimeStatKey.Armor);
    public float EffectiveMagicResist => ResolveRuntimeModifiedStat(magicResist, RuntimeStatKey.MagicResist);
    public float EffectiveDefenseValue => ResolveRuntimeModifiedStat(defenseValue, RuntimeStatKey.DefenseValue);
    public float EffectiveDamageOutputMultiplier
        => ResolveRuntimeModifiedStat(damageOutputMultiplier, RuntimeStatKey.DamageOutputMultiplier);
    public float EffectiveDamageTakenMultiplier
        => ResolveRuntimeModifiedStat(damageTakenMultiplier, RuntimeStatKey.DamageTakenMultiplier);

    /// <summary>
    /// Sync field legacy `isStunned` với managed Stun + authoritative displacement state.
    /// StatusEffectController gọi mỗi khi active managed CC thay đổi.
    /// </summary>
    internal void SynchronizeManagedCrowdControlState()
    {
        bool managedStun = _statusEffects != null && _statusEffects.IsStunned;
        bool displaced = _displacementController != null && _displacementController.IsActive;
        isStunned = managedStun || displaced; // legacy read field: all hard displacement still looks action-locked.

        // Chỉ hard movement lock mới được Stats-level dừng NavMeshAgent.
        // Taunt/Fear KHÔNG phải hard stop: manual control bị khóa nhưng ForcedControl consumer
        // (EnemyAI/CompanionAI/DarkDemonAI) vẫn phải được quyền lái actor chase/flee.
        // Nếu coi forced-control là movementControlLocked ở đây thì BẤT KỲ managed-effect refresh nào
        // (vd Catalyst aura refresh Slow mỗi frame) cũng set isStopped=true sau AI Update, khiến Fear
        // chỉ quay mặt nhưng đứng yên. Root vẫn phải hard-stop forced movement theo canonical rule.
        bool rooted = _statusEffects != null && _statusEffects.IsRooted;
        bool hardMovementLocked = managedStun || displaced || rooted;
        if (hardMovementLocked && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    /// <summary>Phase F compatibility bridge cho code cũ đọc isBleeding/isBurning.</summary>
    internal void SynchronizeManagedDebuffState(bool bleeding, bool burning, bool resonated, bool challenged)
    {
        isBleeding = bleeding;
        isBurning = burning;
        isResonated = resonated;
        isChallenged = challenged;
    }

    internal void NotifyManagedCrowdControlInterrupt(CombatEffectInfo eff, Stats source)
    {
        if (eff == null || !eff.interruptCurrentAction) return;
        RaiseInterrupted(InterruptContext.FromEffect(eff, source));
    }

    // ─────────────────────────────────────────────────────────────
    //  SUPER ARMOR — không bị ngắt chiêu.
    //
    //  Hai tầng TÁCH BẠCH, đừng trộn:
    //   1. NỀN (2 field dưới) — cấu hình cố định của nhân vật: EnemyStats theo monsterRank,
    //      PlayerStats theo characterId. Gán trong Inspector/Init, KHÔNG đụng lúc runtime.
    //   2. TẠM THỜI (counter) — buff/trang bị bật tắt liên tục. Mỗi nguồn PushSuperArmor khi
    //      bật, PopSuperArmor khi tắt, giống hệt _ccImmuneCount.
    //
    //  Trước đây mọi nguồn đều ghi thẳng vào 2 field nền, sinh ra ĐÚNG HAI kiểu hỏng ngược nhau:
    //   • Accessory/Weapon khi tắt chỉ trừ level mà không hạ cờ → super armor kẹt vĩnh viễn.
    //   • CoreShield ForceTurnOff_T4_01 hạ cờ VÔ ĐIỀU KIỆN → cướp super armor của nguồn khác.
    //  Đọc hiệu lực qua IsSuperArmor / EffectiveSuperArmorLevel, đừng đọc 2 field nền.
    // ─────────────────────────────────────────────────────────────
    [Header("--- Super Armor (nền) ---")]
    public bool isSuperArmor = false;
    public int superArmorLevel = 0; // Rule mới: armorLevel=0 KHÔNG chặn CC; CC chỉ bị block khi impactLevel < armorLevel.

    private int _superArmorTokens = 0;      // số nguồn tạm thời đang giữ
    private int _superArmorBonusLevel = 0;  // tổng cấp cộng thêm của các nguồn đó

    /// <summary>Có super armor không — nền HOẶC ít nhất 1 nguồn tạm thời.</summary>
    public bool IsSuperArmor => isSuperArmor || _superArmorTokens > 0;

    /// <summary>Cấp super armor hiệu lực = nền + tổng cấp của mọi nguồn tạm thời.</summary>
    public int EffectiveSuperArmorLevel => superArmorLevel + _superArmorBonusLevel;

    /// <summary>Đăng ký 1 nguồn super armor tạm thời. Gọi cặp với <see cref="PopSuperArmor"/>
    /// và truyền CÙNG bonusLevel cho cả hai.</summary>
    public void PushSuperArmor(int bonusLevel = 0)
    {
        _superArmorTokens++;
        _superArmorBonusLevel += Mathf.Max(0, bonusLevel);
    }

    /// <summary>Nhả 1 nguồn super armor tạm thời. Chặn pop dư (không xuống dưới 0) nên gọi
    /// thừa vẫn an toàn — quan trọng vì nhiều nguồn nhả trong finally/OnDestroy.</summary>
    public void PopSuperArmor(int bonusLevel = 0)
    {
        if (_superArmorTokens > 0) _superArmorTokens--;
        _superArmorBonusLevel = Mathf.Max(0, _superArmorBonusLevel - Mathf.Max(0, bonusLevel));
    }

    [Header("--- Tăng thời gian nhận Buff ---")]
    public float buffDurationBonus = 0f;
    // [MỚI] Biến lưu hướng mặt thực tế (Dùng cho CombatMath)
    [HideInInspector] public Vector3 facingDirection = Vector3.back;

    [Header("--- Stealth ---")]
    public float stealthFactor = 1.0f; // 1 = Bình thường, 0.5 = Giảm 50% tầm địch

    // [MỚI] Cờ báo hiệu trạng thái Tàng Hình
    public bool isInvisible = false;

    // --- DEBUFF COMPATIBILITY FLAGS ---
    // Phase F: lifecycle Bleed/Burn thuộc StatusEffectController. Hai field public này chỉ còn là read-compatible
    // bridge cho content cũ (vd BloodReaverSkill), KHÔNG còn sở hữu timer/coroutine riêng.
    [Header("--- Bleed ---")]
    public bool isBleeding = false;
    [Header("--- Burn ---")]
    public bool isBurning = false;
    [Header ("--- Mark ---")]
    [Tooltip("Bị đánh dấu")]
    public bool IsMarked=false;

    [Header("Parry Settings")]
    public bool isParrying = false;       // Đang trong thế thủ
    public bool isPerfectParryWindow = false; // Đang trong "khung giờ vàng"
    [Range(0, 360)] public float parryAngle = 120f;

    public const string ChallengeMarkDebuffId = "DuelistSkill.ChallengeMark";
    public const string ResonanceMarkDebuffId = "CatalystPassive.Resonance";

    [Header("--- Duelist Challenge ---")]
    [Tooltip("Compatibility read flag. Lifecycle thuộc Debuff Registry từ Phase F3D.")]
    public bool isChallenged = false;

    [Header("--- Resonance Mark (Catalyst) ---")]
    [Tooltip("Compatibility read flag. Lifecycle thuộc Debuff Registry từ Phase F3D.")]
    public bool isResonated = false;

    public Action<DamageInfo> OnBeforeTakeDamage;

    // [CC INTERRUPT] Bắn khi bị STUN hoặc KNOCKBACK bắt đầu → người nghe (PlayerController / EnemyCombat)
    // tự hủy đòn đánh / skill đang thực hiện. Stun/knockback "ngắt hành động" — chuẩn ARPG.
    public event Action OnInterrupted;
    /// <summary>[INT-01A] Event ngắt hành động CÓ ngữ cảnh (type/flags/source). Consumer mới (SkillManager/
    /// EnemyCombat) nên nghe cái này để quyết cooldown/cost; legacy OnInterrupted vẫn fire song song.</summary>
    public event Action<InterruptContext> OnInterruptedContext;
    /// <summary>Báo các hệ điều khiển hủy hành động hiện tại (do CC) — fire CẢ event context + legacy.</summary>
    protected void RaiseInterrupted(InterruptContext ctx)
    {
        OnInterruptedContext?.Invoke(ctx);
        OnInterrupted?.Invoke();
    }
    /// <summary>Overload legacy (không ngữ cảnh) — dùng context Unknown. Giữ cho call-site cũ không vỡ.</summary>
    protected void RaiseInterrupted() => RaiseInterrupted(new InterruptContext(CombatEffectType.Unknown));
    public event Action<float, float> OnHealReceived; // <lượng hồi, lượng dư>
    public event Action<float, float, HealSource> OnHealDetailed; // <lượng hồi, dư, nguồn>

    // [ACCESSORY] Bắn KHI VÀ CHỈ KHI sát thương chạm máu thật (Global Rule 3) — kèm DamageInfo
    // để effect biết hướng đánh (info.attacker/sourcePosition) và loại damage (phys/magic/true).
    public event Action<DamageInfo, float> OnDamageTakenHp; // <info, lượng máu thật bị trừ>
    // [ACCESSORY] Bắn khi lớp giáp ảo (Shield) vừa bị đòn đánh phá vỡ về 0.
    public event Action OnShieldBroken;

    // ─────────────────────────────────────────────────────────────
    //  [STATUS EFFECT PHASE A] Core runtime components.
    //  Stats vẫn là compatibility façade; lifecycle sẽ được migrate dần sang các component này.
    // ─────────────────────────────────────────────────────────────
    private StatusEffectController _statusEffects;
    private RuntimeStatModifierLedger _runtimeModifiers;
    private DisplacementController _displacementController;
    private StatusControlResolver _controlResolver;
    private ForcedControlRunner _forcedControlRunner;

    public StatusEffectController StatusEffects { get { EnsureStatusInfrastructure(); return _statusEffects; } }
    public RuntimeStatModifierLedger RuntimeModifiers { get { EnsureStatusInfrastructure(); return _runtimeModifiers; } }
    public DisplacementController Displacement { get { EnsureStatusInfrastructure(); return _displacementController; } }
    public StatusControlResolver ControlResolver { get { EnsureStatusInfrastructure(); return _controlResolver; } }
    public ForcedControlRunner ForcedControl { get { EnsureStatusInfrastructure(); return _forcedControlRunner; } }

    /// <summary>Snapshot quyền điều khiển thống nhất cho Player / Enemy / Companion.</summary>
    public StatusControlSnapshot ControlState
    {
        get
        {
            EnsureStatusInfrastructure();
            return _controlResolver != null ? _controlResolver.RebuildFromCurrentState() : StatusControlSnapshot.Normal;
        }
    }

    public bool CanCastSkill(SkillControlPolicy policy)
    {
        EnsureStatusInfrastructure();
        return _controlResolver != null && _controlResolver.CanCastSkill(policy);
    }

    private NavMeshAgent agent;
    private Rigidbody rb;
    protected Animator animator;

    // [HURT ANIM] Chống spam: chỉ cho phép trigger Hurt mỗi hurtAnimCooldown giây.
    [Header("--- Hurt Reaction ---")]
    [Tooltip("Khoảng cách tối thiểu giữa 2 lần chạy animation Hurt (giây). Tránh giật liên tục khi bị đánh dồn.")]
    public float hurtAnimCooldown = 0.4f;
    private float _lastHurtTime = -999f;
    private EnemyCombat _enemyCombatCache; // null nếu không phải enemy → check rẻ

    private T GetOrAddStatusComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null) component = gameObject.AddComponent<T>();
        return component;
    }

    private void EnsureStatusInfrastructure()
    {
        if (_runtimeModifiers == null) _runtimeModifiers = GetOrAddStatusComponent<RuntimeStatModifierLedger>();
        if (_displacementController == null) _displacementController = GetOrAddStatusComponent<DisplacementController>();
        if (_controlResolver == null) _controlResolver = GetOrAddStatusComponent<StatusControlResolver>();
        if (_statusEffects == null) _statusEffects = GetOrAddStatusComponent<StatusEffectController>();
        if (_forcedControlRunner == null) _forcedControlRunner = GetOrAddStatusComponent<ForcedControlRunner>();
    }

    public virtual void Start()
    {
        EnsureStatusInfrastructure();
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

    /// <summary>
    /// Compatibility wrapper Phase F: Bleed là registered Debuff source-aware.
    /// Caller mới nên truyền source/sourceKey; caller cũ không truyền vẫn giữ behavior refresh một legacy source chung.
    /// </summary>
    public EffectApplyResult ApplyBleed(float damagePerTick, float duration, Stats source = null, string sourceKey = null)
    {
        EnsureStatusInfrastructure();
        if (_statusEffects == null || damagePerTick <= 0f || duration <= 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = "Bleed",
            duration = duration,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.IndependentPerSource,
            maxStacks = 1,
            potency = damagePerTick,
            dot = new DebuffDotSpec
            {
                enabled = true,
                tickInterval = 1f,
                tickImmediately = false,
                physDamage = damagePerTick
            },
            note = "Stats.ApplyBleed compatibility wrapper"
        };
        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>Compatibility wrapper Phase F: Burn là registered magic DoT Debuff source-aware.</summary>
    public EffectApplyResult ApplyBurn(float damagePerTick, float duration, Stats source = null, string sourceKey = null)
    {
        EnsureStatusInfrastructure();
        if (_statusEffects == null || damagePerTick <= 0f || duration <= 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = "Burn",
            duration = duration,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.IndependentPerSource,
            maxStacks = 1,
            potency = damagePerTick,
            dot = new DebuffDotSpec
            {
                enabled = true,
                tickInterval = 1f,
                tickImmediately = false,
                magicDamage = damagePerTick
            },
            note = "Stats.ApplyBurn compatibility wrapper"
        };
        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>
    /// Phase F2 normalized AttackSpeedDown Debuff.
    /// Player/Companion: negative bonusAttackSpeed contribution.
    /// Enemy/other Stats: percentage reduction on effective base attack speed.
    /// Slow remains movement-only and must not be used to reduce attack cadence.
    /// </summary>
    public EffectApplyResult ApplyAttackSpeedDown(float magnitude, float duration, Stats source = null,
                                                   string sourceKey = null, string note = null)
    {
        EnsureStatusInfrastructure();
        magnitude = Mathf.Clamp01(magnitude);
        if (_statusEffects == null || magnitude <= 0f || duration <= 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        RuntimeStatModifierSpec modifier = this is AllyStats
            ? new RuntimeStatModifierSpec(
                RuntimeStatKey.BonusAttackSpeed,
                RuntimeModifierOperation.AdditivePercent,
                -magnitude)
            : new RuntimeStatModifierSpec(
                RuntimeStatKey.BaseAttackSpeedMultiplier,
                RuntimeModifierOperation.Multiplicative,
                1f - magnitude);

        var request = new DebuffRequest
        {
            effectId = "AttackSpeedDown",
            duration = duration,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.Strongest,
            maxStacks = 1,
            potency = magnitude,
            note = string.IsNullOrEmpty(note) ? "Stats.ApplyAttackSpeedDown" : note
        };
        request.statModifiers.Add(modifier);
        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>
    /// Phase F2 normalized Armor/MagicResist Down Debuff.
    /// Caller truyền lượng flat đã snapshot theo semantics của producer hiện tại; registry chỉ sở hữu lifecycle/cleanse.
    /// </summary>
    public EffectApplyResult ApplyArmorMagicResistDown(string effectId, float armorAmount, float magicResistAmount,
                                                        float duration, Stats source = null, string sourceKey = null,
                                                        EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshSameSource,
                                                        int maxStacks = 1, float potency = 0f, string note = null,
                                                        Action<DebuffRuntimeSnapshot, EffectRemoveReason> onRemove = null)
    {
        EnsureStatusInfrastructure();
        armorAmount = Mathf.Max(0f, armorAmount);
        magicResistAmount = Mathf.Max(0f, magicResistAmount);
        if (_statusEffects == null || string.IsNullOrWhiteSpace(effectId) || duration <= 0f
            || (armorAmount <= 0f && magicResistAmount <= 0f))
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = effectId.Trim(),
            duration = duration,
            cleanseable = true,
            stackPolicy = stackPolicy,
            maxStacks = Mathf.Max(1, maxStacks),
            potency = potency > 0f ? potency : Mathf.Max(armorAmount, magicResistAmount),
            note = string.IsNullOrEmpty(note) ? "Stats.ApplyArmorMagicResistDown" : note
        };

        if (armorAmount > 0f)
            request.statModifiers.Add(new RuntimeStatModifierSpec(
                RuntimeStatKey.Armor, RuntimeModifierOperation.Flat, -armorAmount));
        if (magicResistAmount > 0f)
            request.statModifiers.Add(new RuntimeStatModifierSpec(
                RuntimeStatKey.MagicResist, RuntimeModifierOperation.Flat, -magicResistAmount));
        request.onRemove = onRemove;

        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>
    /// Phase F2 normalized DamageOutputDown / Weaken Debuff.
    /// magnitude là delta âm trên EffectiveDamageOutputMultiplier theo semantics legacy hiện tại.
    /// </summary>
    public EffectApplyResult ApplyDamageOutputDown(string effectId, float magnitude, float duration, Stats source = null,
                                                    string sourceKey = null,
                                                    EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshSameSource,
                                                    int maxStacks = 1, float potency = 0f, string note = null,
                                                    Action<DebuffRuntimeSnapshot, EffectRemoveReason> onRemove = null,
                                                    DebuffTimeMode timeMode = DebuffTimeMode.Scaled)
    {
        EnsureStatusInfrastructure();
        magnitude = Mathf.Max(0f, magnitude);
        if (_statusEffects == null || string.IsNullOrWhiteSpace(effectId) || magnitude <= 0f || duration <= 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = effectId.Trim(),
            duration = duration,
            cleanseable = true,
            timeMode = timeMode,
            stackPolicy = stackPolicy,
            maxStacks = Mathf.Max(1, maxStacks),
            potency = potency > 0f ? potency : magnitude,
            note = string.IsNullOrEmpty(note) ? "Stats.ApplyDamageOutputDown" : note,
            onRemove = onRemove
        };
        request.statModifiers.Add(new RuntimeStatModifierSpec(
            RuntimeStatKey.DamageOutputMultiplier, RuntimeModifierOperation.Flat, -magnitude));
        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>
    /// Phase F2 normalized DefenseValueDown Debuff.
    /// amount là lượng flat bị trừ khỏi EffectiveDefenseValue; raw defenseValue không bị mutate.
    /// </summary>
    public EffectApplyResult ApplyDefenseValueDown(string effectId, float amount, float duration, Stats source = null,
                                                    string sourceKey = null,
                                                    EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshSameSource,
                                                    int maxStacks = 1, float potency = 0f, string note = null,
                                                    Action<DebuffRuntimeSnapshot, EffectRemoveReason> onRemove = null)
    {
        EnsureStatusInfrastructure();
        amount = Mathf.Max(0f, amount);
        if (_statusEffects == null || string.IsNullOrWhiteSpace(effectId) || amount <= 0f || duration <= 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = effectId.Trim(),
            duration = duration,
            cleanseable = true,
            stackPolicy = stackPolicy,
            maxStacks = Mathf.Max(1, maxStacks),
            potency = potency > 0f ? potency : amount,
            note = string.IsNullOrEmpty(note) ? "Stats.ApplyDefenseValueDown" : note,
            onRemove = onRemove
        };
        request.statModifiers.Add(new RuntimeStatModifierSpec(
            RuntimeStatKey.DefenseValue, RuntimeModifierOperation.Flat, -amount));
        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>
    /// Phase F2 normalized DamageTakenUp / Vulnerability Debuff.
    /// magnitude là delta dương trên EffectiveDamageTakenMultiplier; raw damageTakenMultiplier không bị mutate.
    /// </summary>
    public EffectApplyResult ApplyDamageTakenUp(string effectId, float magnitude, float duration, Stats source = null,
                                                string sourceKey = null,
                                                EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshSameSource,
                                                int maxStacks = 1, float potency = 0f, string note = null,
                                                Action<DebuffRuntimeSnapshot, EffectRemoveReason> onRemove = null)
    {
        EnsureStatusInfrastructure();
        magnitude = Mathf.Max(0f, magnitude);
        if (_statusEffects == null || string.IsNullOrWhiteSpace(effectId) || magnitude <= 0f || duration <= 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = effectId.Trim(),
            duration = duration,
            cleanseable = true,
            stackPolicy = stackPolicy,
            maxStacks = Mathf.Max(1, maxStacks),
            potency = potency > 0f ? potency : magnitude,
            note = string.IsNullOrEmpty(note) ? "Stats.ApplyDamageTakenUp" : note,
            onRemove = onRemove
        };
        request.statModifiers.Add(new RuntimeStatModifierSpec(
            RuntimeStatKey.DamageTakenMultiplier, RuntimeModifierOperation.Flat, magnitude));
        return ApplyDebuff(request, source, sourceKey);
    }

    /// <summary>
    /// Phase F3D: Challenge Mark là cleanseable Debuff mark. Re-apply từ cùng Duelist chỉ refresh duration.
    /// Mark bị consume khi Duelist parry đúng target qua ConsumeChallengeMark().
    /// </summary>
    public EffectApplyResult ApplyChallengeMark(float duration, Stats source = null)
    {
        if (duration <= 0f) return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = ChallengeMarkDebuffId,
            duration = duration,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.RefreshSameSource,
            maxStacks = 1,
            potency = 1f,
            note = "Duelist Skill Challenge Mark"
        };
        return ApplyDebuff(request, source);
    }

    public bool HasChallengeMark()
    {
        EnsureStatusInfrastructure();
        return _statusEffects != null && _statusEffects.HasDebuff(ChallengeMarkDebuffId);
    }

    /// <summary>Consume Challenge Mark của đúng Duelist source; source null dùng compatibility fallback consume mọi mark.</summary>
    public bool ConsumeChallengeMark(Stats source = null)
    {
        EnsureStatusInfrastructure();
        return _statusEffects != null
            && _statusEffects.RemoveDebuffsByEffectId(ChallengeMarkDebuffId, EffectRemoveReason.Manual, source) > 0;
    }

    /// <summary>
    /// Phase F3D: Resonance là cleanseable mark. Player hit refresh 5s; Companion damage đọc mark nhưng không consume.
    /// </summary>
    public EffectApplyResult ApplyResonanceMark(float duration, Stats source = null)
    {
        if (duration <= 0f) return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        var request = new DebuffRequest
        {
            effectId = ResonanceMarkDebuffId,
            duration = duration,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.RefreshSameSource,
            maxStacks = 1,
            potency = 1f,
            note = "Catalyst Passive Resonance Mark"
        };
        return ApplyDebuff(request, source);
    }

    public bool HasResonanceMark()
    {
        EnsureStatusInfrastructure();
        return _statusEffects != null && _statusEffects.HasDebuff(ResonanceMarkDebuffId);
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
        if (info == null || isDead) return;
        // [MỚI] Cho phép các Khiên can thiệp trước khi tính toán sát thương (Giảm/Phản DMG/Cứu tử)
        OnBeforeTakeDamage?.Invoke(info);
        // [MỚI] Cho phép Signature chặn sát thương
        if (damageInterceptor != null && damageInterceptor.Invoke(info))
        {
            return; // Interceptor là mechanic riêng: nếu nó consume toàn bộ hit thì giữ behavior legacy.
        }

        // Invincible = không nhận damage (cả Shield lẫn HP), NHƯNG hit vẫn resolve CC/Debuff.
        bool negateDamage = isInvincible;
        EnterCombat();
        // 1. TÍNH TOÁN DAMAGE VÀ SHIELD
        float damageToTake = negateDamage ? 0f : info.TotalRawDamage * EffectiveDamageTakenMultiplier; // invincible giữ hit nhưng damage = 0

        // Keep the popup aligned with the target-side damage actually entering the Shield/HP pipeline.
        // Previously the HP path used EffectiveDamageTakenMultiplier but DamageNumberManager still received
        // the unscaled DamageInfo, making Vulnerability look like it had no gameplay effect.
        float damageBeforeShieldForPopup = damageToTake;

        // [MỚI] KIỂM TRA CỘNG HƯỞNG TỪ COMPANION
        // Giả sử Companion của bạn có tag là "Companion" và khi đánh có truyền info.attacker = stats của nó
        if (HasResonanceMark() && info.attacker != null && info.attacker.CompareTag("Ally"))
        {
            damageToTake *= 1.30f; // Tăng 30% sát thương
            Debug.Log($"<color=orange>Cộng Hưởng!</color> Sát thương từ Companion tăng lên: {damageToTake}");
        }
        damageBeforeShieldForPopup = damageToTake;

        // [MỚI] Trừ vào Shield trước (Nếu có)
        if (!negateDamage && currentShield > 0)
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

        // Hiển thị số sát thương nổi lên màn hình (cả bị chặn lẫn xuyên qua Shield).
        // DamageInfo stores the post-CombatMath components but not target-side Vulnerability/Resonance.
        // Scale a display-only copy so the popup matches the actual incoming damage before Shield.
        if (!negateDamage)
        {
            float rawTotal = info.TotalRawDamage;
            float popupScale = rawTotal > 0f ? damageBeforeShieldForPopup / rawTotal : 1f;
            DamageInfo popupInfo = new DamageInfo
            {
                physDamage = info.physDamage * popupScale,
                magicDamage = info.magicDamage * popupScale,
                trueDamage = info.trueDamage * popupScale,
                isCrit = info.isCrit
            };
            DamageNumberManager.Show(popupInfo, transform.position);
        }

        // [HURT ANIM] Phản ứng giật mình khi MẤT MÁU THẬT và chưa chết — safe-set + có điều kiện.
        // Player animator không có "Hurt" nên tự bỏ qua; SlimeAnimator (enemy) có thì chạy.
        if (animator != null && damageToTake > 0f && currentHp > 0f && HasParameter(animator, "Hurt"))
            TryPlayHurt();

        // 3. XỬ LÝ STATUS ATTACHED: CC + Debuff đều resolve dù Invincible đã zero damage.
        ApplyCrowdControl(info);
        ApplyAttachedDebuffs(info);

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
    /// Entry point compatibility xử lý CC của một hit qua StatusEffectController.
    /// Gateway mới chịu trách nhiệm immunity, Super Armor (impact &lt; armor mới block) và resistance theo type;
    /// Stats chỉ còn bridge legacy payload; runtime CC/displacement ownership nằm ở status components.
    /// </summary>
    public void ApplyCombatEffects(DamageInfo info)
    {
        if (info == null) return;
        EnsureStatusInfrastructure();

        // Gộp legacy bool vào effects list trước khi đi qua gateway mới.
        info.BridgeLegacyEffects();
        if (info.effects == null || _statusEffects == null) return;

        Stats source = info.attacker;
        for (int i = 0; i < info.effects.Count; i++)
        {
            CombatEffectInfo eff = info.effects[i];
            if (eff == null) continue;

            // Clone trước khi bổ sung runtime payload để không mutate asset/shared request.
            CombatEffectInfo request = eff.CreateRuntimeCopy();
            if (request.sourcePosition == Vector3.zero)
                request.sourcePosition = info.sourcePosition != Vector3.zero
                    ? info.sourcePosition
                    : (source != null ? source.transform.position : transform.position);

            _statusEffects.ApplyCrowdControl(request, new StatusEffectContext(source, this));
        }
    }

    private void ApplyAttachedDebuffs(DamageInfo info)
    {
        if (info == null || info.debuffs == null || info.debuffs.Count == 0) return;
        EnsureStatusInfrastructure();
        if (_statusEffects == null) return;

        Stats source = info.attacker;
        for (int i = 0; i < info.debuffs.Count; i++)
        {
            DebuffRequest request = info.debuffs[i];
            if (request == null) continue;
            _statusEffects.ApplyDebuff(request, new StatusEffectContext(source, this));
        }
    }

    /// <summary>
    /// Áp 1 CC thuần qua StatusEffectController. SourcePosition được bổ sung runtime nếu content chưa set.
    /// </summary>
    public void ApplyEffect(CombatEffectInfo effect, Stats source = null)
    {
        ApplyEffectWithResult(effect, source);
    }

    /// <summary>
    /// Phase E+ API cho producer cần giữ EffectHandle (vd Taunt trap phải release sớm khi skill kết thúc).
    /// Legacy caller vẫn dùng ApplyEffect(void) bình thường.
    /// </summary>
    public EffectApplyResult ApplyEffectWithResult(CombatEffectInfo effect, Stats source = null, string sourceKey = null)
    {
        if (effect == null) return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
        EnsureStatusInfrastructure();
        if (_statusEffects == null) return EffectApplyResult.Reject(EffectApplyStatus.Unsupported);

        CombatEffectInfo request = effect.CreateRuntimeCopy();
        if (request.sourcePosition == Vector3.zero)
            request.sourcePosition = source != null ? source.transform.position : transform.position;

        return _statusEffects.ApplyCrowdControl(request, new StatusEffectContext(source, this, sourceKey));
    }

    /// <summary>Phase F gateway cho registered Debuff. Debuff không đi qua Super Armor/resistanceEffect của CC.</summary>
    public EffectApplyResult ApplyDebuff(DebuffRequest request, Stats source = null, string sourceKey = null)
    {
        if (request == null) return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
        EnsureStatusInfrastructure();
        if (_statusEffects == null) return EffectApplyResult.Reject(EffectApplyStatus.Unsupported);
        return _statusEffects.ApplyDebuff(request, new StatusEffectContext(source, this, sourceKey));
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
            // Displacement có thể đang tạm disable Agent; tránh gọi isStopped trên Agent invalid/off-mesh.
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (agent.enabled) agent.enabled = false;
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

    // [PHASE D] Legacy Airborne API wrapper — mọi physical movement đi qua DisplacementController.
    public void Airborne(float duration, float height = 1.2f)
        => ApplyEffect(new CombatEffectInfo(CombatEffectType.Airborne, duration)
        {
            height = height,
            effectId = "LEGACY_AIRBORNE"
        });

    public void Airborne(float duration, float height, InterruptContext interruptCtx)
        => ApplyEffect(new CombatEffectInfo(CombatEffectType.Airborne, duration)
        {
            height = height,
            effectId = "LEGACY_AIRBORNE",
            interruptCurrentAction = interruptCtx.interruptCurrentAction,
            putInterruptedSkillOnCooldown = interruptCtx.putInterruptedSkillOnCooldown,
            note = interruptCtx.note
        }, interruptCtx.source);

    // [MỚI] Hàm giải phóng nhân vật khỏi mọi trạng thái khống chế cleanseable hiện tại.
    public void BreakCrowdControl()
    {
        EnsureStatusInfrastructure();
        if (_statusEffects != null) _statusEffects.CleanseCrowdControl();
        else ExecuteLegacyCrowdControlCleanse();
    }

    internal void ExecuteLegacyCrowdControlCleanse()
    {
        // Managed Stun/Root/Silence/Slow/Taunt/Fear đã được StatusEffectController xóa trước khi vào legacy cleanup.
        // Chỉ dọn legacy raw Silence bridge; KHÔNG động Airborne/Knockback/Pull.
        isSilenced = false;
        _silenceTokens = 0;
        SynchronizeManagedCrowdControlState();

        if (!IsDisplaced && agent != null && !isDead)
        {
            if (!agent.enabled) agent.enabled = true;
            // Không ép isStopped=false ở đây: AI/Companion consumer sẽ quyết định ở frame kế tiếp.
        }

        Debug.Log($"<color=orange>{gameObject.name} đã THOÁT KHỎI CC CLEANSEABLE!</color>");
    }

    internal void ExecuteLegacySlowCleanse()
    {
        // Không còn Slow store legacy từ Phase C. Giữ method để compatibility với code Phase A/B.
    }

    // [MỚI] Hàm xóa các Debuff legacy hiện có (Bleed, Burn). Phase F sẽ chuyển sang generic registry.
    public void ClearDebuffs()
    {
        EnsureStatusInfrastructure();
        if (_statusEffects != null) _statusEffects.CleanseDebuffs();
        else ExecuteLegacyDebuffCleanse();
    }

    internal void ExecuteLegacyDebuffCleanse()
    {
        // Phase F1: Bleed/Burn đã thuộc registry. Giữ hook này để các Debuff legacy khác còn migration
        // có chỗ cleanup; hiện không được phép tự clear compatibility flags nếu registry vẫn còn instance.
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