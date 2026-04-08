using System;
using System.Collections; // -��+� d+�ng Coroutine
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

    [Header("--- Level---")]
    public int level = 1;
    public int maxLevel = 60; // Gi�+�i hߦ�n cߦ�p -��+�
    public float exp;
    public float nextLevelExp;
    public float maxExpForCurrentLevel;
    public float percentExpReceive = 1f; // T�++ l�+� nhߦ�n EXP (C+� th�+� b�+� t-�ng hoߦ+c giߦ�m b�+�i buffs/debuffs)

    public bool isInvincible = false;

    [Header("--- Stamina  ---")]
    public float maxStamina = 100f;
    public float currentStamina;

    [Tooltip("H�+�i ph�+�c m�+�i gi+�y khi TRONG combat")]
    public float staminaBaseRecovery = 0.5f;

    [Tooltip("H�+�i ph�+�c m�+�i gi+�y khi NGO+�I combat")]
    public float staminaOutCombatRecovery = 15f;

    // [M�+�I] Th�+�i gian ch�+� h�+�i ph�+�c sau khi d+�ng th�+� l�+�c (Dash/Run)
    public float staminaRegenDelay = 1.0f;
    private float lastStaminaConsumeTime = -10f; // Biߦ+n ghi lߦ�i th�+�i -�i�+�m cu�+�i c+�ng d+�ng th�+� l�+�c

    [Header("--- Combat State ---")]
    public bool outCombat = true;
    public float outCombatTime = 10.0f;
    private float combatTimer = 0f;

    [Header("--- Dash & Run Settings ---")]
    public float baseDashDistance = 2f;
    public float baseDashRecovery = 1.25f;
    public float dashCost = 15f;
    public float baseDashDuration = 0.2f;

    // [M�+�I] Th�+� l�+�c ti+�u hao m�+�i gi+�y khi chߦ�y nhanh
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
    public float damageOutputMultiplier = 1.0f; // % s+�t th����ng g+�y ra, default l+� 100%

    [Header("--- LifeSteal ---")]
    public float physicalLifeSteal;
    public float magicLifeSteal;

    // [S�+�A] T+�ch comment ra kh�+�i khai b+�o biߦ+n
    [Tooltip("Th�+�i gian gi�+�a c+�c -�+�n -�+�nh (Cooldown)")]
    public float baseAttackSpeed;

    // Biߦ+n h�+� tr�+� Combo (Logic n+�y sߦ+ nߦ�m �+� PlayerController, nh��ng Stats ch�+�a th+�ng s�+�)
    public float comboResetTime = 1.0f; // Th�+�i gian ch�+� -��+� reset combo v�+� -�+�n 1
    public float heavyAttackChargeTime = 1.0f; // Th�+�i gian gi�+� chu�+�t -��+� max dame
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
    // G+�c block hi�+�u quߦ�. Mߦ+c -��+�nh 0.5 (180 -��+�). Vanguard sߦ+ s�+�a th+�nh 0.75 (270 -��+�).
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
    public float resistanceEffect = 0f; //giߦ�m th�+�i gian debuff

    [Header("--- Status ---")]
    public bool isDead = false; // [M�+�I] Ki�+�m tra -�+� chߦ+t ch��a

    [Header("--- Shield & State ---")]
    public float currentShield = 0f; // L�+�p gi+�p ߦ�o
    public bool isHealingBlocked = false; // C�+� chߦ+n h�+�i m+�u c�+�a Ravager

    // [M�+�I] Event b+�o hi�+�u b�+� -�+�nh (D+�ng cho JuggernautSkill)
    // Tham s�+�: (L���+�ng damage th�+�c nhߦ�n, Bߦ�n th+�n Stats b�+� -�+�nh)
    public event Action<float, Stats> OnDamageReceived;

    // [M�+�I] C�+�ng cho ph+�p c+�c K�+� n-�ng can thi�+�p tr���+�c khi nhߦ�n s+�t th����ng (DuelistSignature)
    public Func<DamageInfo, bool> damageInterceptor;

    private float stunEndTime = 0f;

    private Coroutine currentStunCoroutine;

    // [M�+�I] Trߦ�ng th+�i b�+� kh�+�ng chߦ+
    public bool isStunned = false;

    // [M�+�I] Super Armor (Si+�u Gi+�p) - Kh+�ng b�+� ngߦ�t chi+�u
    [Header("--- Super Armor ---")]
    public bool isSuperArmor = false;
    public int superArmorLevel = 0; // Cߦ�p -��+� gi+�p (0: Ch�+�ng qu+�i nh�+�, 1: Ch�+�ng Elite...)

    [Header("--- T-�ng th�+�i gian nhߦ�n Buff ---")]
    public float buffDurationBonus = 0f;
    // [M�+�I] Biߦ+n l��u h���+�ng mߦ+t th�+�c tߦ+ (D+�ng cho CombatMath)
    [HideInInspector] public Vector3 facingDirection = Vector3.back;

    [Header("--- Stealth ---")]
    public float stealthFactor = 1.0f; // 1 = B+�nh th���+�ng, 0.5 = Giߦ�m 50% tߦ�m -��+�ch

    // [M�+�I] C�+� b+�o hi�+�u trߦ�ng th+�i T+�ng H+�nh
    public bool isInvisible = false;

    // --- HI�+�U �+�NG CHߦ�Y M+�U (BLEED) ---
    [Header("--- Bleed ---")]
    public bool isBleeding = false;
    private Coroutine bleedCoroutine;
    private float bleedTimer = 0f; // B�+� -�ߦ+m th�+�i gian c+�n lߦ�i c�+�a Bleed
    private float currentBleedDamage = 0f; // L��u damage -��+� nߦ+u -�+�nh tiߦ+p th+� cߦ�p nhߦ�t damage m�+�i
    [Header ("--- Mark ---")]
    [Tooltip("B�+� -�+�nh dߦ�u")]
    public bool IsMarked=false;

    [Header("Parry Settings")]
    public bool isParrying = false;       // -�ang trong thߦ+ th�+�
    public bool isPerfectParryWindow = false; // -�ang trong "khung gi�+� v+�ng"
    [Range(0, 360)] public float parryAngle = 120f;

    [Header("--- Duelist Challenge ---")]
    public bool isChallenged = false; // C�+� b+�o hi�+�u b�+� th+�ch -�ߦ�u
    private Coroutine challengeCoroutine;

    [Header("--- Resonance Mark (Catalyst) ---")]
    public bool isResonated = false;
    private Coroutine resonanceCoroutine;

    private NavMeshAgent agent;
    private Rigidbody rb;
    protected Animator animator;

    public virtual void Start()
    {
        maxHp = baseHp;
        currentHp = maxHp;
        currentStamina = maxStamina;
        currentSin= maxSin;
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
        // [M�+�I] Ki�+�m tra Delay: Nߦ+u ch��a qua 1 gi+�y k�+� t�+� lߦ�n cu�+�i d+�ng th�+� l�+�c -> Kh+�ng h�+�i
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
        if (outCombat) Debug.Log(">> Enter Combat! (H�+�i th�+� l�+�c chߦ�m, Xoay chߦ�m)");
        outCombat = false;
        combatTimer = 0f;
    }

    public void ApplyBleed(float damagePerTick, float duration)
    {
        // 1. Cߦ�p nhߦ�t th+�ng s�+� m�+�i nhߦ�t
        currentBleedDamage = damagePerTick; // C+� th�+� l+�m logic: Lߦ�y damage cao nhߦ�t hoߦ+c c�+�ng d�+�n

        // 2. Gia hߦ�n th�+�i gian (Reset lߦ�i -��+�ng h�+� -�ߦ+m ng���+�c)
        bleedTimer = duration;

        // -�+�nh dߦ�u kߦ+ -��+�ch -�ang b�+� Bleed
        isBleeding = true;
        // 3. Ch�+� Start Coroutine nߦ+u n+� ch��a chߦ�y
        if (bleedCoroutine == null)
        {
            bleedCoroutine = StartCoroutine(BleedRoutine());
        }
    }

    private IEnumerator BleedRoutine()
    {
        // V+�ng lߦ+p chߦ�y ch�+�ng n+�o c+�n th�+�i gian
        while (bleedTimer > 0)
        {
            // Ch�+� 1 gi+�y
            yield return new WaitForSeconds(1.0f);

            // Tr�+� th�+�i gian
            bleedTimer -= 1.0f;

            // G+�y s+�t th����ng
            TakeDamage(currentBleedDamage);
            Debug.Log($"<color=red>{gameObject.name} -�ang chߦ�y m+�u: -{currentBleedDamage} HP (C+�n {bleedTimer}s)</color>");
        }

        // Hߦ+t gi�+� -> X+�a Coroutine -��+� lߦ�n sau Start lߦ�i -榦�+�c
        isBleeding = false;
        bleedCoroutine = null;
    }

    // H+�m gߦ�n ߦ�n th+�ch -�ߦ�u
    public void ApplyChallengeMark(float duration)
    {
        isChallenged = true;

        // Nߦ+u -�ang c+� ߦ�n r�+�i th+� -�ߦ�p -�i t+�nh lߦ�i th�+�i gian m�+�i
        if (challengeCoroutine != null) StopCoroutine(challengeCoroutine);
        challengeCoroutine = StartCoroutine(ChallengeRoutine(duration));
    }

    private IEnumerator ChallengeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isChallenged = false;
        challengeCoroutine = null;
    }

    // [M�+�I] H+�m d+�ng -��+� gߦ�n dߦ�u ߦ�n C�+�ng H���+�ng
    public void ApplyResonanceMark(float duration)
    {
        isResonated = true;

        // Nߦ+u -�ang c+� dߦ�u ߦ�n r�+�i th+� reset lߦ�i th�+�i gian
        if (resonanceCoroutine != null) StopCoroutine(resonanceCoroutine);
        resonanceCoroutine = StartCoroutine(ResonanceRoutine(duration));
    }

    private IEnumerator ResonanceRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isResonated = false;
        resonanceCoroutine = null;
    }

    // [Cߦ�P NHߦ�T] H+�m ti+�u hao th�+� l�+�c d+�ng chung cho Dash v+� Run
    public bool TryConsumeStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;

            // [M�+�I] Ghi lߦ�i th�+�i gian ti+�u hao -��+� t+�nh Delay h�+�i ph�+�c
            lastStaminaConsumeTime = Time.time;

            return true;
        }
        return false;
    }

    public virtual void TakeDamage(DamageInfo info)
    {
        if (isInvincible || isDead) return;

        // [M�+�I] Cho ph+�p Signature chߦ+n s+�t th����ng
        if (damageInterceptor != null && damageInterceptor.Invoke(info))
        {
            return; // Nߦ+u Interceptor trߦ� v�+� true -> Kߦ+ -��+�ch -�+� sߦ�p bߦ�y, H�+�Y vi�+�c mߦ�t m+�u!
        }

        EnterCombat();
        // 1. T+�NH TO+�N DAMAGE V+� SHIELD
        float damageToTake = info.damageAmount;

        // [M�+�I] KI�+�M TRA C�+�NG H���+PNG T�+� COMPANION
        // Giߦ� s�+� Companion c�+�a bߦ�n c+� tag l+� "Companion" v+� khi -�+�nh c+� truy�+�n info.attacker = stats c�+�a n+�
        if (isResonated && info.attacker != null && info.attacker.CompareTag("Ally"))
        {
            damageToTake *= 1.30f; // T-�ng 30% s+�t th����ng
            Debug.Log($"<color=orange>C�+�ng H���+�ng!</color> S+�t th����ng t�+� Companion t-�ng l+�n: {damageToTake}");
        }

        // [M�+�I] Tr�+� v+�o Shield tr���+�c (Nߦ+u c+�)
        if (currentShield > 0)
        {
            float damageBlocked = Mathf.Min(damageToTake, currentShield);
            currentShield -= damageBlocked;
            damageToTake -= damageBlocked;

            Debug.Log($"<color=yellow>Shield blocked: {damageBlocked}. Remaining Shield: {currentShield}");
        }

        // 2. TR�+� M+�U (Nߦ+u damage vߦ�n c+�n sau khi ph+� shield)
        if (damageToTake > 0)
        {
            currentHp -= damageToTake;

            //if (info.isCrit) Debug.Log($"<color=red>Damage nhߦ�n l�+�n h��n Shield</color> {gameObject.name} nhߦ�n {damageToTake} (Shield chߦ+n: {info.damageAmount - damageToTake})");
            //else Debug.Log($"{gameObject.name} nhߦ�n {damageToTake}");
            Debug.Log($"{gameObject.name} nhߦ�n {damageToTake}");
            // [M�+�I] K+�CH HOߦ�T S�+� KI�+�N "B�+� -�+�NH"
            // B+�o cho JuggernautSkill biߦ+t l+� "Tao b�+� mߦ�t m+�u r�+�i!"
            OnDamageReceived?.Invoke(damageToTake, this);
        }
        else
        {
            Debug.Log($"{gameObject.name} chߦ+n to+�n b�+� s+�t th����ng bߦ�ng Shield!");
        }

        // 3. X�+� L+� HI�+�U �+�NG (CC)
        ApplyCrowdControl(info);

        // 4. KI�+�M TRA CHߦ+T
        if (currentHp <= 0)
        {
            currentHp = 0;
            Die();
        }
    }

    //// H+�m c+� (Overload) -��+� t����ng th+�ch code c+� ch��a k�+�p s�+�a
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
    // S�+�a h+�m h�+�i m+�u (nߦ+u bߦ�n c+� h+�m Heal ri+�ng, hoߦ+c s�+�a tr�+�c tiߦ+p ch�+� n+�o c�+�ng m+�u)
    public void Heal(float amount)
    {
        if (isHealingBlocked)
        {
            Debug.Log("H�+�i m+�u b�+� chߦ+n do Say M+�u!");
            return;
        }

        currentHp += amount;
        if (currentHp > maxHp) currentHp = maxHp;
    }
    // --- LOGIC STUN & KNOCKBACK ---
    void ApplyCrowdControl(DamageInfo info)
    {
        if (isSuperArmor && info.impactLevel <= superArmorLevel)
        {
            // C+� th�+� th+�m hi�+�u �+�ng visual (v+� d�+�: ng���+�i l+�e s+�ng trߦ�ng ch�+�u -�+�n)
             //Debug.Log("Super Armor Blocked CC!");
            return;
        }

        // 1. X�+� l++ KNOCKBACK
        if (info.isKnockback)
        {
            // T+�nh l�+�c -�ߦ�y l+�i th�+�c tߦ+ sau khi tr�+� Kh+�ng
            // V+� d�+�: Force 10, Res 0.2 -> Th�+�c nhߦ�n 8
            float finalForce = info.knockbackForce * (1.0f - resistanceKnockBack);
            Debug.Log("finalForce: " + finalForce + " info.knockbackForce: "+ info.knockbackForce + " resistanceKnockBack: "+ resistanceKnockBack);

            // Nߦ+u l�+�c vߦ�n > 0 th+� -�ߦ�y
            if (finalForce > 0)
            {
                Vector3 knockbackDir = (transform.position - info.sourcePosition).normalized;
                knockbackDir.y = 0; // Gi�+� th-�ng bߦ�ng mߦ+t -�ߦ�t
                StartCoroutine(KnockbackRoutine(knockbackDir, finalForce));
            }
        }

        // 2. X�+� l++ STUN (N+�ng cߦ�p)
        if (info.isStun)
        {
            float finalDuration = info.stunDuration * (1.0f - resistanceKnockBack);
            Debug.Log($"Stun: {finalDuration}");
            // [S�+�A] B�+� h+�m Mathf.Max(0.1f). Nߦ+u th�+�i gian < 0.1s coi nh�� kh+�ng ho+�n to+�n
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

        // [M�+�I] Biߦ+n l��u trߦ�ng th+�i Root Motion
        bool wasRootMotion = false;

        // 1. Tߦ�T Hߦ�N NAVMESH AGENT (Bi�+�n ph+�p mߦ�nh)
        // isStopped -�+�i khi kh+�ng -��+� v�+�i Humanoid, tߦ�t lu+�n Component cho chߦ�c
        if (hasAgent)
        {
            //Debug.Log("hasAgent: lߦ�y th+�nh c+�ng");
            agent.velocity = Vector3.zero;
            agent.enabled = false; // <--- Tߦ�T Hߦ�N
        }

        // 2. Tߦ�M D�+�NG ROOT MOTION
        if (animator != null)
        {
            //Debug.Log("animator: Lߦ�y th+�nh c+�ng " );
            wasRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = false; // Tߦ�t Root Motion -��+� Physics hoߦ�t -��+�ng
        }

        // 3. X�+� L+� RIGIDBODY (-�ߦ�y L+�i)
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = false; // Bߦ�t Physics

            //Debug.Log("rb.isKinematic: " + rb.isKinematic);

            // Reset vߦ�n t�+�c c+�
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Debug xem l�+�c c+� -榦�+�c add kh+�ng
            // Debug.Log($"Add Force: {force} theo h���+�ng {dir}");

            // Th+�m l�+�c -�ߦ�y (D+�ng Impulse cho d�+�t kho+�t)
            rb.AddForce(dir * force, ForceMode.Impulse);
        }

        // 4. CH�+� TH�+�I GIAN BAY
        yield return new WaitForSeconds(0.2f);

        // 5. KH+�I PH�+�C TRߦ�NG TH+�I
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = wasKinematic;
        }

        // Kh+�i ph�+�c Root Motion
        if (animator != null)
        {
            animator.applyRootMotion = wasRootMotion;
        }

        // Kh+�i ph�+�c Agent
        if (hasAgent)
        {
            agent.enabled = true; // <--- Bߦ�T Lߦ�I
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true; // Vߦ�n gi�+� stop v+� -�ang Stun
                agent.ResetPath();      // X+�a -榦�+�ng -�i c+� cho sߦ�ch
            }
        }

        // 6. CHECK STUN TIߦ+P
        yield return new WaitForSeconds(0.1f);

        if (Time.time >= stunEndTime)
        {
            isStunned = false;
        }
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        Debug.Log($"{gameObject.name} b�+� STUN!");

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Thay v+� WaitForSeconds c�+� -��+�nh, ta ch�+� -�ߦ+n -�+�ng th�+�i -�i�+�m stunEndTime
        // -�i�+�u n+�y gi+�p vi�+�c "ghi -�+�" th�+�i gian tr�+� n+�n m���+�t m+� (ch�+� cߦ�n update stunEndTime)
        while (Time.time < stunEndTime)
        {
            yield return null;
        }

        isStunned = false;
        // Debug.Log($"{gameObject.name} hߦ+t STUN");
    }

    protected virtual void Die()
    {
        if (isDead) return; // Chߦ+t r�+�i kh+�ng chߦ+t lߦ�i
        isDead = true;

        Debug.Log($"{gameObject.name} -�+� chߦ+t!");

        // 1. Tߦ�t Collider -��+� kh+�ng c+�n l+� m�+�c ti+�u (Raycast/OverlapSphere kh+�ng thߦ�y n�+�a)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 2. Tߦ�t Physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true; // -��+� x+�c kh+�ng b�+� tr+�i
        }

        // 3. Tߦ�t AI Agent (Nߦ+u l+� Enemy)
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // 4. Play Animation Die
        if (animator != null)
        {
            //animator.SetTrigger("Die");
            // -�ߦ�m bߦ�o animator kh+�ng chuy�+�n sang state kh+�c
            //animator.SetBool("IsDead", true);
        }

        // 5. V+� hi�+�u h+�a Script -�i�+�u khi�+�n
        // Nߦ+u l+� Player
        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null) playerCtrl.enabled = false;

        // Nߦ+u l+� Enemy
        var enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null) enemyAI.enabled = false;

        var enemyCombat = GetComponent<EnemyCombat>();
        if (enemyCombat != null) enemyCombat.enabled = false;

        // 6. H�+�y Object sau 3 gi+�y
        Destroy(gameObject, 3.0f);
    }
    // [-�+� S�+�A] H+�m H�+�i Sinh an to+�n vߦ�t l++
    public virtual void Revive(float hpPercent)
    {
        if (!isDead) return;
        isDead = false;
        currentHp = maxHp * hpPercent;

        // 1. Reset sߦ�ch -��+�ng l���+�ng (Tr+�nh vi�+�c b�+� l��u l�+�c -�ߦ�y t�+� l+�c chߦ+t)
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // T�+� -��+�ng nhߦ�n di�+�n: 
            // Nߦ+u l+� AI (c+� NavMeshAgent) -> Kh+�a vߦ�t l++ (isKinematic = true)
            // Nߦ+u l+� Player -�i�+�u khi�+�n -> M�+� vߦ�t l++ (isKinematic = false)
            rb.isKinematic = (GetComponent<UnityEngine.AI.NavMeshAgent>() != null);
        }

        // 2. Bߦ�t lߦ�i NavMeshAgent TR���+�C
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;

        // 3. Bߦ�t lߦ�i Collider SAU (-��+� an to+�n)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Reset Animation
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        Debug.Log($"<color=green>{gameObject.name} -�+� -ɦ��+�C H�+�I SINH!</color>");
    }
    // [M�+�I] H+�m giߦ�i ph+�ng nh+�n vߦ�t kh�+�i m�+�i trߦ�ng th+�i kh�+�ng chߦ+ hi�+�n tߦ�i
    public void BreakCrowdControl()
    {
        isStunned = false;
        stunEndTime = 0f;

        // Ngߦ�t Coroutine Stun nߦ+u -�ang chߦ�y
        if (currentStunCoroutine != null)
        {
            StopCoroutine(currentStunCoroutine);
            currentStunCoroutine = null;
        }

        // Tri�+�t ti+�u -��+�ng l���+�ng (L�+�c -�ߦ�y l+�i Knockback)
        if (rb != null && !isDead)
        {
            rb.linearVelocity = Vector3.zero;
        }

        // -�ߦ�m bߦ�o bߦ�t lߦ�i NavMeshAgent nߦ+u l�+� b�+� KnockbackRoutine tߦ�t -�i
        if (agent != null && !agent.enabled && !isDead)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        Debug.Log($"<color=orange>{gameObject.name} -�+� THO+�T KH�+�I KH�+�NG CHߦ+!</color>");
    }
    // ==========================================
    // [M�+�I] H�+� TH�+�NG KINH NGHI�+�M V+� L+�N Cߦ�P
    // ==========================================
    public float GetNextLevelExp()
    {
        // C+�ng th�+�c -榦�+�ng cong EXP: 100 * (level ^ 1.1)
        return Mathf.Floor(100f * Mathf.Pow(level, 1.1f));
    }

    // H+�m d+�ng chung -��+� t+�nh c+�ng th�+�c EXP
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
            exp -= nextLevelExp; // Tr�+� -�i l���+�ng exp -�+� d+�ng -��+� l+�n cߦ�p
            LevelUp();
            RefreshExpRequirements();
        }

        if (level >= maxLevel)
        {
            exp = 0;
            nextLevelExp = 0; // Set v�+� 0 hoߦ+c gi�+� nguy+�n t+�y ++ bߦ�n cho UI hi�+�n th�+� ch�+� "MAX"
            maxExpForCurrentLevel = 0f;
            Debug.Log($"<color=orange>{gameObject.name} -�+� -�ߦ�t Cߦ�p T�+�i -�a ({maxLevel})!</color>");
        }
    }

    protected virtual void LevelUp()
    {
        level++;
        RefreshExpRequirements();
        Debug.Log($"<color=yellow>LEVEL UP!</color> {gameObject.name} -�+� -�ߦ�t cߦ�p {level}!");
    }
}
