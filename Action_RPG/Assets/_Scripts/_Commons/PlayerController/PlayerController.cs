using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.AI; // [FIX] tắt NavMeshObstacle gây enemy/companion né-đẩy player
using System;
using System.Collections.Generic; // [MỚI]
using Game.Features.Player;

// nhiệm vụ: làm thêm attack cooldown dựa trên attack speed AllyStats.attackSpeed (có kết hợp animator) và hoàn thiện animator
public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    public float idleDelay = 0.25f;

    private AllyStats stats;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private CinemachineImpulseSource impulseSource;

    [Header("--- Combo System ---")]
    public int comboCount = 0;          // Đòn đánh thứ mấy (0, 1, 2...)
    public int maxCombo = 2;            // Combo tối đa 3 đòn
    public float lastAttackTime = 0f;   // Thời điểm đánh đòn cuối
    public float comboWindow = 2.0f;    // Thời gian cho phép nối combo (nếu quá thì reset về 0)
    public bool isAttacking = false;    // Đang trong animation đánh
    public bool isStance = false;       // Juggernaut
    public bool isBerserk = false;    // Đang điên loạn (Ravager)
    public bool isSkillBlocked = false; // [MỚI] Cờ chặn dùng skill (Blood Reaver Signature)

    // Biến tính toán runtime
    //private float currentAttackCooldown = 0f;

    [Header("Combat Settings")]
    public float attackRange = 0.5f;
    private bool nextAttackQueued = false; // Đã bấm chuột cho đòn tiếp theo chưa?
    [Range(0, 360)] public float attackAngle = 90f;

    // --- [MỚI] Cờ đánh xa (Do EquipmentManager gán vào) ---
    [HideInInspector] public bool isRangedAttack = false;

    [HideInInspector] public GameObject projectilePrefab; // [MỚI]

    // --- [MỚI] BIẾN CHỈNH ĐỘ CAO ĐẠN ---
    [Tooltip("Độ cao tính từ chân nhân vật để sinh ra đạn")]
    public float projectileSpawnOffsetY = 0f;

    // [MỚI] Danh sách kẻ địch đã trúng đòn trong nhịp chém hiện tại
    private List<Transform> hitTargets = new List<Transform>();

    // [MỚI] Biến hỗ trợ Charge Attack
    private bool isCharging = false;
    [HideInInspector] public bool isSkillChanneling = false; // ACC_CH_T4_03: đang gồng skill (khóa di chuyển/dash/đánh)
    private float chargeTimer = 0f;
    private float currentDamageMultiplier = 1.0f; // Hệ số sát thương của đòn hiện tại


    // State variables
    //private int lastDirection = 0;
    public bool isWalking = false;
    private float lastMoveTime = 0f;
    private bool isTurning = false;

    // Dash & Sprint State
    public bool isDashing = false;
    private bool isSprinting = false;

    // Movement variables
    private Vector3 movementInput;
    private Vector3 currentVisualDir;

    // Testing
    public bool isTestCrit = false;

    private EquipmentManager equipmentManager;
    private SkillManager skillManager;
    private WeaponEffectManager _weaponFx; // hệ effect vũ khí (hook nhân dmg đòn đánh thường / xuyên giáp)
    private AccessoryEffectManager _accessoryFx; // hệ effect trang sức (hook chỉnh dmg đòn đánh thường)
    private CompanionSkillController _companionSkillCtrl; // cache controller skill của Companion (phím R/T)
    private int _heavyArmorApplied = 0; // +armor tạm khi heavy swing; FIELD để hoàn trả được nếu bị ngắt giữa chừng

    /// <summary>Tìm/cache CompanionSkillController của Companion trong scene (companion có thể spawn sau).</summary>
    private CompanionSkillController ResolveCompanionSkillCtrl()
    {
        if (_companionSkillCtrl != null) return _companionSkillCtrl;
        CompanionAI c = FindFirstObjectByType<CompanionAI>();
        if (c != null) _companionSkillCtrl = c.GetComponent<CompanionSkillController>();
        return _companionSkillCtrl;
    }

    // [MỚI] Biến để lưu tiến trình đánh đang chạy
    private Coroutine currentAttackCoroutine;

    [Header("Perfect Dodge Settings")]
    public float perfectDodgeRadius = 2.0f; // Bán kính nguy hiểm để tính Perfect
    public LayerMask dangerLayer;

    // [MỚI] Biến trạng thái dùng Skill đặc biệt
    public bool isUsingSpecialSkill = false;
    // [MỚI] Biến kiểm tra đòn đánh cường hóa DuelistSkill
    private bool isDuelistEmpoweredAttackActive = false; // Cache trạng thái Thách đấu

    // ============ CLASS-NEUTRAL EVENTS (Minimal Integration) ============
    public event System.Action<Vector2> OnMovementInputChanged;   // Gọi khi input di chuyển thay đổi
    public event System.Action<int, bool> OnAttackPerformed;      // Gọi khi đánh trúng địch (stepIndex, isHeavy)
#pragma warning disable 0067
    public event System.Action<WeaponData> OnWeaponEquipped;      // Gọi khi trang bị vũ khí mới
#pragma warning restore 0067
    public event System.Action<Stats, int, bool, bool> OnHitEnemy;
    // [MỚI] Kỹ năng (qua DamageHelper.ApplyStandardDamage) trúng kẻ địch: (target, isMagic, isCrit).
    // Dùng cho các effect "khi kỹ năng trúng đích" (vd WPN_ST_T4_01, WPN_ST_T4_03).
    public event System.Action<Stats, bool, bool> OnSkillHitEnemy;
    public void NotifySkillHitEnemy(Stats target, bool isMagic, bool isCrit) => OnSkillHitEnemy?.Invoke(target, isMagic, isCrit);
    // [MỚI] Kỹ năng (có SkillData) HẠ GỤC kẻ địch — để attribute kill theo loại skill (ACC_RM_T3_06, ACC_CH_T5_01).
    public event System.Action<SkillData> OnSkillKillEnemy;
    public void NotifySkillKill(SkillData skill) => OnSkillKillEnemy?.Invoke(skill);
    // [MỚI] THÊM DÒNG NÀY
    public event System.Action<Stats, bool> OnKillEnemy;
    // ==================================================================
    public event System.Action OnDashPerformed;
    // [ACCESSORY] Bắn kèm DamageInfo đầy đủ mỗi lần gây damage lên 1 mục tiêu
    // (để effect biết phân tách phys/magic/true, crit... mà OnHitEnemy không chở được)
    public event System.Action<Stats, DamageInfo> OnDamageDealt;

    private DuelistPassive duelistSkill;
    private bool isDuelistCounterActive = false; // Cache trạng thái counter cho cả vòng lặp quét

    // ─────────────────────────────────────────────────────────────
    //  WEAPON ATTACK DISPATCHER (hệ thống đánh theo vũ khí)
    // ─────────────────────────────────────────────────────────────
    [HideInInspector] public WeaponAttackDispatcher attackDispatcher;

    /// <summary>Expose hitTargets cho dispatcher/handlers.</summary>
    public System.Collections.Generic.List<Transform> HitTargets => hitTargets;

    // Cờ đặc biệt cho Bow heavy charge (giảm tốc độ di chuyển)
    [HideInInspector] public bool isBowCharging = false;
    // Cờ cho Staff heavy spin — cho phép di chuyển 50% tốc độ trong khi xoay
    [HideInInspector] public bool isStaffSpinning = false;

    // ── Per-hit override flags (set bởi WeaponAttackHandlers trước khi gọi ApplyDamage) ──
    /// <summary>True → đòn tiếp theo không gây knockback dù isHeavy=true (Dagger spin, Spear thrust...)</summary>
    [HideInInspector] public bool  suppressNextKnockback = false;
    /// <summary>True → đòn tiếp theo gây stun với thời lượng nextHitStunDuration (Spear, Grimoire heavy...)</summary>
    [HideInInspector] public bool  nextHitStun           = false;
    [HideInInspector] public float nextHitStunDuration   = 0f;
    /// <summary>≥0 → override knockbackForce cho heavy hit tiếp theo (Staff spin giảm knockback). -1 = không override.</summary>
    [HideInInspector] public float nextHitKnockbackForce = -1f;
    private InventoryUIManager _inventoryUIManager;
    private SkillTreeController _skillTreeController;
    private ItemPickupManager _pickupManager;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private DevToolPanel _devToolPanel;
#endif
    void Start()
    {
        stats = GetComponent<AllyStats>();
        if (stats != null) stats.OnInterrupted += HandleInterrupted; // CC ngắt đòn đánh

        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        dangerLayer = LayerMask.GetMask("Enemy");

        if (stats == null) Debug.LogError("Thiếu CharacterStats!");
        if (animator == null) Debug.LogError("Thiếu Animator!");
        if (spriteRenderer == null) Debug.LogError("Thiếu SpriteRenderer!");

        currentVisualDir = Vector3.back;

        equipmentManager = GetComponent<EquipmentManager>();
        skillManager = GetComponent<SkillManager>();
        _weaponFx = GetComponent<WeaponEffectManager>(); // hook hệ effect vũ khí (nhân dmg/xuyên giáp)
        _accessoryFx = GetComponent<AccessoryEffectManager>(); // hook hệ effect trang sức

        // ── Dispatcher: GetOrAdd (cũng có thể drag-drop trong Inspector) ──
        attackDispatcher = GetComponent<WeaponAttackDispatcher>();
        if (attackDispatcher == null)
            attackDispatcher = gameObject.AddComponent<WeaponAttackDispatcher>();

        // Notify dispatcher về vũ khí đang trang bị (nếu đã equip trước đó)
        if (equipmentManager != null && equipmentManager.currentWeapon != null)
            attackDispatcher.OnWeaponChanged(equipmentManager.currentWeapon);
        _inventoryUIManager = FindFirstObjectByType<InventoryUIManager>();
        _skillTreeController = FindFirstObjectByType<SkillTreeController>();
        _pickupManager = GetComponent<ItemPickupManager>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _devToolPanel = FindFirstObjectByType<DevToolPanel>(FindObjectsInactive.Include);
#endif

        // [002-E] Initialize player vision manager
        InitializeVisionManager();

        // [FIX BUG] Player có NavMeshObstacle (Carve=OFF) → trở thành vật cản RVO động,
        // khiến mọi NavMeshAgent (enemy & companion) tự né/đẩy theo hướng vuông góc khi
        // player dí sát từ bên hông (bất đối xứng trái/phải). Tắt obstacle để chỉ còn va
        // chạm vật lý (Rigidbody) chặn lại — không né, không đẩy bất thường.
        NavMeshObstacle navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle != null && navObstacle.enabled)
        {
            navObstacle.enabled = false;
            Debug.Log("[PlayerController] Đã tắt NavMeshObstacle trên Player (fix enemy/companion né-đẩy khi dí sát bên hông).");
        }
    }

    /// <summary>
    /// Initialize player vision manager. Called in Start().
    /// </summary>
    private void InitializeVisionManager()
    {
        // [002-E] Get or create PlayerVisionManager component
        PlayerVisionManager visionManager = GetComponent<PlayerVisionManager>();
        if (visionManager == null)
        {
            visionManager = gameObject.AddComponent<PlayerVisionManager>();
            Debug.Log("[002-E] PlayerVisionManager component added automatically.");
        }
        else
        {
            Debug.Log("[002-E] PlayerVisionManager already exists on Player GameObject.");
        }
    }

    void FixedUpdate()
    {
        if (stats == null) return;

        if (InventoryUIManager.IsInventoryOpen || SkillTreeController.IsSkillTreeOpen || IsDevToolOpen()) return;

        // [ACTION-LOCK] Stun / Airborne / Root đều khóa di chuyển (qua query thống nhất).
        if (stats.IsMovementLocked) return;

        // [SỬA Ở ĐÂY] Khóa di chuyển vật lý nếu đang dùng skill, lướt, parry, hoặc ĐANG GỒNG
        // Bow heavy charge: cho phép di chuyển nhưng cản thường (isCharging block bị tắt)
        bool blockedByCharge = isCharging && !isBowCharging;
        if (isUsingSpecialSkill || isDashing || stats.isParrying || blockedByCharge) return;

        // Logic di chuyển thường
        if (!isTurning && isWalking)
        {
            float currentSpeed = stats.moveSpeed * (isSprinting ? stats.runSpeedMultiplier : 1f);

            // Bow heavy charge: giảm tốc 50% — trừ khi WPN_BW_T3_01 (gồng bắn không giảm tốc).
            bool bowNoSlow = (stats as AllyStats)?.bowNoChargeSlow ?? false;
            if (isBowCharging && !bowNoSlow) currentSpeed *= 0.5f;

            // Staff normal attack: giảm tốc 50%
            if (isStaffSpinning) currentSpeed *= 0.5f;

            Vector3 targetPosition = rb.position + movementInput * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }

    // [MỚI] True khi người chơi đang gõ vào 1 ô input (InputField / TMP_InputField).
    // Dùng để KHÔNG nuốt phím tắt (B/C/F/V...) thành lệnh game khi user đang gõ tìm kiếm.
    private bool IsTypingInInputField()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        GameObject sel = es.currentSelectedGameObject;
        if (sel == null) return false;
        // InputField cũ (uGUI) hoặc TMP_InputField đều bắt được qua GetComponent
        if (sel.GetComponent<UnityEngine.UI.InputField>() != null) return true;
        if (sel.GetComponent<TMPro.TMP_InputField>() != null) return true;
        return false;
    }

    // [MỚI] DevTool chỉ tồn tại trong Editor/Dev build — helper an toàn cho mọi build
    private bool IsDevToolOpen()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return _devToolPanel != null && _devToolPanel.gameObject.activeSelf;
#else
        return false;
#endif
    }

    void Update()
    {
        if (stats == null) return;

        // Đang gõ vào ô tìm kiếm/input → bỏ qua mọi phím tắt (B/C/F/V) để không bị đóng UI ngoài ý muốn
        if (IsTypingInInputField()) return;

        // B key được xử lý TRUỜC guard IsInventoryOpen để đóng và mở đều hoạt động
        if (Input.GetKeyDown(KeyCode.B))
        {
            // Không MỞ inventory khi đang có panel chặn khác (DevTool / Skill Tree); vẫn cho ĐÓNG nếu đang mở
            if (!InventoryUIManager.IsInventoryOpen && (IsDevToolOpen() || SkillTreeController.IsSkillTreeOpen))
                return;

            if (_inventoryUIManager == null)
                _inventoryUIManager = FindFirstObjectByType<InventoryUIManager>();
            _inventoryUIManager?.ToggleInventory();
            return;
        }

        // C key — Skill Tree (xử lý TRƯỚC guard để đóng/mở đều hoạt động)
        if (Input.GetKeyDown(KeyCode.C))
        {
            // Cho phép đóng nếu đang mở; chỉ MỞ khi không có panel chặn nào khác (Inventory / DevTool)
            if (SkillTreeController.IsSkillTreeOpen ||
                (!InventoryUIManager.IsInventoryOpen && !IsDevToolOpen()))
            {
                if (_skillTreeController == null)
                    _skillTreeController = FindFirstObjectByType<SkillTreeController>();
                _skillTreeController?.ToggleSkillTree();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.F) && !InventoryUIManager.IsInventoryOpen)
        {
            _pickupManager?.TryPickupNearest();
        }

        // [COMPANION] R = ra lệnh Companion dùng Skill, T = dùng Signature.
        if (Input.GetKeyDown(KeyCode.R)) { ResolveCompanionSkillCtrl()?.CommandSkill(); }
        if (Input.GetKeyDown(KeyCode.T)) { ResolveCompanionSkillCtrl()?.CommandSignature(); }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (_devToolPanel != null && !InventoryUIManager.IsInventoryOpen && !SkillTreeController.IsSkillTreeOpen)
            {
                bool willShow = !_devToolPanel.gameObject.activeSelf;
                _devToolPanel.gameObject.SetActive(willShow);
                // Pause/cursor do UIPauseManager quản lý (DevToolPanel.OnEnable/OnDisable tự set lock)
            }
            return; // Prevent V from doing anything else
        }
#endif

        if (InventoryUIManager.IsInventoryOpen || SkillTreeController.IsSkillTreeOpen || IsDevToolOpen()) return;

        // --- 0. CÁC LOGIC NỀN (Luôn chạy bất kể trạng thái) ---
        UpdateTimersAndCombo(); // Tách logic reset combo ra hàm riêng cho gọn
        UpdateAnimatorParameters(); // Tách logic update animator parameter

        // --- 1. CHẶN INPUT (Priority High) ---
        // Nếu đang dùng Skill đặc biệt -> Chặn toàn bộ Input điều khiển
        if (isUsingSpecialSkill) return;

        // [MỚI] Nếu đang Berserk (Say máu) -> Bỏ qua toàn bộ Input người chơi
        if (isBerserk) return;

        // Nếu đang Dash -> Chặn toàn bộ Input điều khiển (trừ khi bạn muốn cho phép cancel dash?)
        if (isDashing) return;

        // --- 2. XỬ LÝ INPUT (Priority Medium) ---
        HandleDashInput();      // Xử lý Shift (Dash)
        HandleSprintInput();    // Xử lý Shift (Sprint)
        HandleAttackInput();    // Xử lý Chuột (Attack / Charge)
        HandleSkillInput();     // Xử lý Skill (1, 2, Q, E...)

        // --- 3. XỬ LÝ DI CHUYỂN (Priority Low) ---
        HandleMovementStopToTurn(); // Tính toán vector di chuyển & hướng nhìn

        // Cập nhật hướng nhìn vào Stats (để Skill lấy hướng mà dùng)
        if (stats != null) stats.facingDirection = currentVisualDir;


    }

    void UpdateTimersAndCombo()
    {
        // Logic Reset Combo
        if (Time.time > lastAttackTime + comboWindow && !isAttacking && comboCount > 0)
        {
            comboCount = 0;
        }
    }

    void UpdateAnimatorParameters()
    {
        if (animator != null)
        {
            animator.SetFloat("AttackSpeedMultiplier", stats.attackSpeed);
        }
    }

    void HandleDashInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            PerformDash();
        }
    }

    void HandleSprintInput()
    {
        // [ACC_PA_T5_04] Cấm chạy nhanh khi đeo trang sức tương ứng.
        if ((stats as AllyStats)?.accBlockDashSprint == true) { isSprinting = false; return; }

        if (Input.GetKey(KeyCode.LeftShift) && movementInput.magnitude > 0.1f)
        {
            if (stats.TryConsumeStamina(stats.runCost * Time.deltaTime, false, true))
            {
                isSprinting = true;
            }
            else
            {
                isSprinting = false;
            }
        }
        else
        {
            isSprinting = false;
        }
    }

    void HandleAttackInput()
    {
        // [ACTION-LOCK] Airborne / Stun khóa đánh thường (Root & Silence KHÔNG chặn basic attack).
        if (stats.IsActionLocked) return;

        // [ACC_CH_T4_03] Đang gồng skill (channel) → khóa mọi thao tác đánh.
        if (isSkillChanneling) return;

        // Nếu đang Parry → HỦY LUÔN việc gồng (Ưu tiên đỡ đòn)
        if (stats.isParrying && isCharging)
        {
            isCharging = false;
            isBowCharging = false;
        }

        WeaponData currentWep = equipmentManager?.currentWeapon;

        // ── 1. Bắt đầu nhấn chuột ──────────────────────────────────────────
        if (Input.GetMouseButtonDown(0) && !isAttacking && !isStance && !stats.isParrying)
        {
            isCharging  = true;
            chargeTimer = 0f;
        }

        // ── 2. Đang giữ chuột ──────────────────────────────────────────────
        if (isCharging && Input.GetMouseButton(0))
        {
            chargeTimer += Time.deltaTime;

            // ── BOW: cho phép di chuyển (speed 50%) kể cả tap nhanh lẫn heavy charge ──
            // Chỉ bị khóa khi isAttacking=true (animation bắn thực sự chạy)
            isBowCharging = (currentWep?.weaponType == WeaponData.WeaponType.Bow);

            // ── STAFF CHANNELED: kích hoạt spin ngay khi đủ thời gian ─────
            if (attackDispatcher != null
                && attackDispatcher.CurrentHeavyIsChanneled
                && !attackDispatcher.IsChanneledActive
                && chargeTimer >= (currentWep?.heavyChargeTime ?? 0.5f))
            {
                isCharging = false;
                float swingDur = 0.4f; // Swing window không quan trọng với channeled
                var ctx = attackDispatcher.BuildContext(
                    isHeavy: true, comboCount, swingDur,
                    hitTargets, dangerLayer,
                    (e, h, s) => ApplyDamageToTarget(e, h, s), OnAttackPerformed);

                // Set multiplier cho Staff (1.5f = 150%)
                currentDamageMultiplier = currentWep?.heavyDamageMultiplier ?? 1.5f;
                attackDispatcher.TryStartChanneled(ctx);
                return;
            }

            // ── Tự động tung Heavy nếu giữ quá lâu ───────────────────────
            float baseMaxCharge = currentWep?.heavyChargeTime ?? stats.heavyAttackChargeTime;
            AllyStats allyMaxCharge = stats as AllyStats;
            float maxCharge = baseMaxCharge * (allyMaxCharge != null ? allyMaxCharge.heavyAttackWindupMult : 1f) + 2.0f;
            if (chargeTimer >= maxCharge)
            {
                isCharging    = false;
                isBowCharging = false;
                PerformAttack(true);
                return;
            }
        }

        // ── 3. Nhả chuột ───────────────────────────────────────────────────
        if (Input.GetMouseButtonUp(0))
        {
            isBowCharging = false;

            // Dừng Staff spin nếu đang chạy
            if (attackDispatcher != null && attackDispatcher.IsChanneledActive)
            {
                attackDispatcher.StopChanneled();
                isCharging = false;
                return;
            }

            if (isCharging)
            {
                isCharging = false;
                float baseThreshold = currentWep?.heavyChargeTime ?? stats.heavyAttackChargeTime;
                AllyStats allyThresh = stats as AllyStats;
                float threshold = baseThreshold * (allyThresh != null ? allyThresh.heavyAttackWindupMult : 1f);
                if (chargeTimer >= threshold) PerformAttack(true);
                else                          PerformAttack(false);
            }
        }

        // ── Queue Attack ───────────────────────────────────────────────────
        if (isAttacking && Input.GetMouseButtonDown(0))
        {
            nextAttackQueued = true;
            chargeTimer = 0f;
        }
    }

    void HandleSkillInput()
    {
        // [MỚI] Nếu đang bị khóa skill thì không nhận lệnh
        if (isSkillBlocked) return;

        // Skill 1: cá phím số 1 và E
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.E))
        {
            if (skillManager != null && skillManager.currentSkill != null)
                skillManager.CastSkill(skillManager.currentSkill);
        }

        // Skill 2 (Signature): cá phím số 2 và Q
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Q))
        {
            if (skillManager != null && skillManager.currentSignature != null)
                skillManager.CastSkill(skillManager.currentSignature);
        }
    }



    void PerformDash()
    {
        AllyStats allyDashStats = stats as AllyStats;

        // [ACTION-LOCK] Stun / Airborne / Root khóa Dash.
        if (stats.IsMovementLocked) return;

        // [ACC_CH_T4_03] Đang gồng skill (channel) → không cho Dash.
        if (isSkillChanneling) return;

        // [ACC_PA_T5_04] Cấm Dash khi đeo trang sức tương ứng.
        if (allyDashStats != null && allyDashStats.accBlockDashSprint) return;

        // [SHD_CS_T5_04] Bỏ cooldown Dash nếu cờ được bật.
        bool noCd = allyDashStats != null && allyDashStats.dashNoCooldown;
        if (!noCd && Time.time < stats.lastDashTime + stats.baseDashRecovery)
        {
            Debug.Log("Dash Cooldown!");
            return;
        }

        // Tiêu hao tài nguyên Dash (isDash=true). Stats.TryConsumeStamina tự chuyển sang
        // Sin (T5_04: 10% maxSin) hoặc HP (T5_02: x5) nếu đang đeo khiên tương ứng.
        if (!stats.TryConsumeStamina(stats.dashCost, true))
        {
            Debug.Log("Không đủ tài nguyên để Dash!");
            return;
        }

        // --- [MỚI] KIỂM TRA PERFECT DODGE NGAY KHI BẤM DASH ---
        CheckPerfectDodgeCondition();
        // -----------------------------------------------------

        // --- [FIX MỚI] LOGIC CANCEL CHARGE (HỦY GỒNG TRỌNG KÍCH) ---
        if (isCharging)
        {
            isCharging = false;
            chargeTimer = 0f;
            //Debug.Log(">> Đã Hủy Gồng Trọng Kích để Dash!");
        }

        // --- LOGIC CANCEL ATTACK ---
        CancelCurrentAttack("Dash");
        // ---------------------------------

        StartCoroutine(DashCoroutine());
        OnDashPerformed?.Invoke();
    }

    /// <summary>
    /// Hủy đòn đánh thường đang thực hiện (dùng cho Dash và khi bị CC ngắt).
    /// reason chỉ để log.
    /// </summary>
    public void CancelCurrentAttack(string reason)
    {
        bool channeling = attackDispatcher != null && attackDispatcher.IsChanneledActive;
        // KHÔNG return sớm nếu đang channel (staff spin) — nó cần được dừng dù isAttacking có thể đã lệch.
        if (!isAttacking && !channeling) return;

        // 0. Dừng staff channeled (spin) nếu đang chạy — dispatcher tự reset isAttacking/isStaffSpinning.
        if (channeling) attackDispatcher.StopChanneled();

        // 1. Dừng ngay lập tức Coroutine đánh đang chạy
        if (currentAttackCoroutine != null)
            StopCoroutine(currentAttackCoroutine);

        // 2. Reset các trạng thái tấn công
        isAttacking = false;
        isStaffSpinning = false;
        nextAttackQueued = false; // Xóa luôn lệnh đánh tiếp theo nếu có
        isCharging = false;       // Hủy luôn gồng nếu đang charge
        currentDamageMultiplier = 1.0f;

        // [FIX LEAK] AttackRoutine bị StopCoroutine giữa chừng → phần hoàn trả heavyArmorBonus ở
        // cuối routine KHÔNG chạy. Hoàn trả thủ công ở đây để không leak +armor vĩnh viễn.
        RefundHeavyArmor();

        // 3. Reset Animator để nó không bị kẹt ở pose đánh
        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetFloat("ComboStep", 0);
        }

        Debug.Log($">> Đã Cancel Attack ({reason})!");
    }

    /// <summary>Hoàn trả +armor tạm của heavy swing (idempotent — gọi nhiều lần không trừ dư).</summary>
    private void RefundHeavyArmor()
    {
        if (_heavyArmorApplied > 0 && stats is AllyStats a)
            a.armor -= _heavyArmorApplied;
        _heavyArmorApplied = 0;
    }

    /// <summary>Bị STUN/KNOCKBACK → hủy đòn đánh đang vung. (Skill đặc biệt tự xử lý qua isStunned nếu cần.)</summary>
    private void HandleInterrupted() => CancelCurrentAttack("CC: Stun/Knockback");

    private void OnDisable()
    {
        if (stats != null) stats.OnInterrupted -= HandleInterrupted;
    }


    IEnumerator DashCoroutine()
    {
        isDashing = true;
        isSprinting = false;
        stats.lastDashTime = Time.time;
        stats.isInvincible = true;

        // CM1 Rogue: Dash xuyên qua quái
        AllyStats allyDash = stats as AllyStats;
        bool dashPhaseActive = allyDash != null && allyDash.rogueCM1_DashPhaseEnemies;
        int enemyLayerIdx = LayerMask.NameToLayer("Enemy");
        if (dashPhaseActive && enemyLayerIdx >= 0)
            Physics.IgnoreLayerCollision(gameObject.layer, enemyLayerIdx, true);

        // --- [MỚI] KÍCH HOẠT ANIMATION DASH ---
        //if (animator != null)
        //{
            // Cách 1: Dùng Trigger (Khuyên dùng cho Dash nhanh/ngắn)
            //animator.SetTrigger("Dash");

            // Cách 2: Nếu bạn dùng Bool (Cho Dash dài/giữ nút)
            // animator.SetBool("IsDashing", true); 
        //}
        // --------------------------------------

        Vector3 dashDir = movementInput.magnitude > 0.1f ? movementInput : currentVisualDir;
        dashDir.y = 0;
        dashDir.Normalize();

        // Cập nhật hướng nhìn ngay lập tức theo hướng dash để Sprite quay đúng hướng
        currentVisualDir = dashDir;
        UpdateAnimationDirection(currentVisualDir); // Cập nhật ngay để Animator nhận IsWalking=true

        float duration = stats.baseDashDuration;
        float dashSpeed = stats.baseDashDistance / duration;

        rb.linearVelocity = dashDir * dashSpeed;

        // I-frames kéo dài toàn bộ thời gian dash để dodge thực sự có ý nghĩa
        yield return new WaitForSeconds(duration);
        stats.isInvincible = false;
        // CM1 Rogue: khôi phục collision sau dash
        if (dashPhaseActive && enemyLayerIdx >= 0)
            Physics.IgnoreLayerCollision(gameObject.layer, enemyLayerIdx, false);

        rb.linearVelocity = Vector3.zero;
        isDashing = false;
    }

    // Hàm kiểm tra xem có mối nguy hiểm nào gần đó không
    void CheckPerfectDodgeCondition()
    {
        // Tạo một vùng quét xung quanh Player
        Collider[] hits = Physics.OverlapSphere(transform.position, perfectDodgeRadius, dangerLayer);

        foreach (Collider hit in hits)
        {
            // 1. Kiểm tra nếu là Enemy
            EnemyCombat enemyCombat = hit.GetComponent<EnemyCombat>();
            if (enemyCombat != null)
            {
                // Chỉ tính là Perfect nếu Enemy ĐANG ĐÁNH (isAttacking = true)
                if (enemyCombat.isAttacking)
                {
                    stats.TriggerPerfectDodge();
                    return; // Chỉ cần né được 1 cái là đủ
                }
            }

            // 2. Kiểm tra nếu là Projectile (Đạn)
            // Giả sử bạn có script Projectile, sau này check ở đây
            /*
            Projectile proj = hit.GetComponent<Projectile>();
            if (proj != null) 
            {
                stats.TriggerPerfectDodge();
                return;
            }
            */
        }
    }

    void HandleMovementStopToTurn()
    {

        // Nếu đang đánh thì KHÔNG nhận input di chuyển
        // Ngoại lệ: Bow heavy charge và Staff normal attack cho phép di chuyển chậm
        bool blockMovement = (isAttacking && !isStaffSpinning) || stats.isParrying || (isCharging && !isBowCharging) || isSkillChanneling;
        if (blockMovement)
        {
            movementInput = Vector3.zero;
            return;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        movementInput = new Vector3(moveX, 0f, moveZ).normalized;

        if (movementInput.magnitude > 0.1f)
        {
            lastMoveTime = Time.time;

            float angleDifference = Vector3.Angle(currentVisualDir, movementInput);
            float rotSpeed = Mathf.PI / stats.turnDuration;

            if (angleDifference > 175f)
            {
                currentVisualDir = Quaternion.Euler(0, 1f, 0) * currentVisualDir;
            }

            currentVisualDir = Vector3.RotateTowards(
                currentVisualDir,
                movementInput,
                rotSpeed * Time.deltaTime,
                0.0f
            );

            if (angleDifference > stats.moveThresholdAngle)
            {
                isTurning = true;
                isWalking = false;
            }
            else
            {
                isTurning = false;
                isWalking = true;
            }
        }
        else
        {
            if (Time.time - lastMoveTime > idleDelay)
            {
                isWalking = false;
                isTurning = false;
            }
        }

        UpdateAnimationDirection(currentVisualDir);
        OnMovementInputChanged?.Invoke(new Vector2(movementInput.x, movementInput.z));
    }
    void UpdateAnimationDirection(Vector3 facingDir)
    {
        // Tính góc 360 độ từ hướng South (Vector3.back)
        float angle = Vector3.SignedAngle(Vector3.back, facingDir, Vector3.up);
        if (angle < 0) angle += 360f;

        int directionIndex = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;

        bool shouldFlip = false;
        int animationIndex = directionIndex;

        // Map hướng phải → trái + flip
        switch (directionIndex)
        {
            case 5: // NE → NW
                animationIndex = 3;
                shouldFlip = true;
                break;
            case 6: // E → W
                animationIndex = 2;
                shouldFlip = true;
                break;
            case 7: // SE → SW
                animationIndex = 1;
                shouldFlip = true;
                break;
            default:
                shouldFlip = false;
                break;
        }

        // ✅ GỬI GIÁ TRỊ FLOAT VÀO "Direction" (KHÔNG DÙNG Int!)
        if (animator != null)
        {
            bool isMoving = isWalking || isTurning || isDashing;
            animator.SetBool("IsWalking", isMoving);
            animator.SetFloat("Direction", (float)animationIndex); // ← Đây là chìa khóa!
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = shouldFlip;
        }
    }


    void PerformAttack(bool isHeavy = false)
    {
        if (stats == null) return;

        // Nếu đang đánh: Cho phép "đặt gạch" đòn tiếp theo
        if (isAttacking)
        {
            // Chỉ cho phép queue nếu animation đã chạy được một chút (ví dụ 30% thời lượng)
            // Để tránh spam click quá sớm. Ở đây mình cho phép luôn để test cho dễ.
            nextAttackQueued = true;
            return;
        }

        // Check cooldown bình thường cho đòn đầu tiên
        float cooldownTime = 1.0f / stats.attackSpeed;
        if (Time.time < lastAttackTime + cooldownTime) return;

        // Reset combo nếu quá hạn
        if (Time.time > lastAttackTime + comboWindow)
        {
            comboCount = 0;
        }

        currentAttackCoroutine=StartCoroutine(AttackRoutine(isHeavy));
    }

    IEnumerator AttackRoutine(bool isHeavy)
    {
        // 1. Setup
        isAttacking = true;
        nextAttackQueued = false;
        lastAttackTime = Time.time;
        hitTargets.Clear(); // [QUAN TRỌNG] Reset danh sách trúng đòn
        isDuelistCounterActive = false; // Reset counter flag

        int currentStep = comboCount;

        // [MỚI] CHECK DUELIST COUNTER MỘT LẦN DUY NHẤT CHO CẢ ĐÒN ĐÁNH
        // Để đảm bảo cả đợt quét đều được hưởng buff (hoặc chỉ con đầu tiên tùy logic, ở đây là cả đợt)
        if (duelistSkill == null) duelistSkill = GetComponent<DuelistPassive>();
        if (duelistSkill != null)
        {
            // Check xem có địch xung quanh không để đỡ phí buff?
            isDuelistCounterActive = duelistSkill.TryUseCounterAttack();
            isDuelistEmpoweredAttackActive = duelistSkill.ConsumeEmpoweredAttack(); // Lấy buff Thách Đấu

            if (isDuelistCounterActive) Debug.Log("<color=cyan>>> DUELIST COUNTER READY!</color>");
            if (isDuelistEmpoweredAttackActive) Debug.Log("<color=magenta>>> DUELIST EMPOWERED (CHALLENGE) READY!</color>");
        }

        // Setup Multiplier — dùng heavyDamageMultiplier từ WeaponData nếu có
        AllyStats allyAttack = stats as AllyStats;
        _heavyArmorApplied = 0; // dùng FIELD để CancelCurrentAttack có thể hoàn trả nếu bị ngắt giữa chừng
        if (isHeavy)
        {
            float wepMultiplier = equipmentManager?.currentWeapon?.heavyDamageMultiplier ?? 0f;
            float baseMult = wepMultiplier > 0f ? wepMultiplier : stats.heavyAttackCharge;
            // CM3: Heavy Attack gây thêm 20% sát thương
            float heavyDmgMult = allyAttack != null ? allyAttack.heavyAttackDamageMult : 1f;
            currentDamageMultiplier = baseMult * heavyDmgMult;
            // CM2: Heavy Attack khó bị ngắt hơn (+heavyArmorBonus armor trong lúc swing)
            if (allyAttack != null && allyAttack.heavyArmorBonus > 0)
            {
                _heavyArmorApplied = allyAttack.heavyArmorBonus;
                allyAttack.armor += _heavyArmorApplied;
            }
        }
        else currentDamageMultiplier = 1.0f;

        // 2. Animator
        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetFloat("ComboStep", (float)currentStep);
            animator.SetTrigger("Attack");
        }

        // 3. Tăng Combo
        if (isHeavy) comboCount = 0;
        else { comboCount++; if (comboCount >= maxCombo) comboCount = 0; }

        // --- CẤU HÌNH SWEEP ---
        float baseAnimDuration = 0.5f; // Thời gian animation chuẩn
        float speedMod = isHeavy ? (stats.attackSpeed * 0.7f) : stats.attackSpeed;
        float realDuration = baseAnimDuration / speedMod;

        // Player chém nhanh: Gây damage từ 20% đến 50% thời lượng
        float startDamageTime = realDuration * 0.2f;
        float endDamageTime = realDuration * 0.5f;
        float swingDuration = endDamageTime - startDamageTime;

        // 4. Chờ vung tay (Wind-up)
        yield return new WaitForSeconds(startDamageTime);

        // 5. THỰC HIỆN ĐÒN ĐÁNH — Dispatcher chọn handler theo loại vũ khí ──
        if (attackDispatcher != null)
        {
            var ctx = attackDispatcher.BuildContext(
                isHeavy, currentStep, swingDuration,
                hitTargets, dangerLayer,
                (e, h, s) => ApplyDamageToTarget(e, h, s), OnAttackPerformed);

            yield return StartCoroutine(attackDispatcher.ExecuteSwing(ctx));
        }

        // 6. Recovery (Chờ nốt animation)
        // [FIX] Tính lại thời gian còn lại chính xác
        yield return new WaitForSeconds(realDuration - endDamageTime);

        isAttacking = false;
        currentDamageMultiplier = 1.0f;
        // CM2: hoàn trả heavy armor bonus sau khi swing xong
        RefundHeavyArmor();

        // 7. Input Buffer
        if (nextAttackQueued)
        {
            yield return null;
            currentAttackCoroutine = StartCoroutine(AttackRoutine(false));
        }
    }

    // --- HÀM TÍNH TOÁN DAMAGE (Tách ra từ HandleDamageLogic cũ) ---
    public void ApplyDamageToTarget(Stats enemyStats, bool isHeavy, int stepIndex, bool forceTrueDamage = false, bool grimoirePhiDao = false)
    {
        if (enemyStats == null || enemyStats.currentHp <= 0) return;

        bool wasAlive = enemyStats.currentHp > 0;
        stats.EnterCombat();

        // Tính hướng (Backstab check)
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        // 1. Info
        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.attacker = stats;

        // 2. CC Effects
        WeaponData currentWpn = equipmentManager.currentWeapon;
        // Phân loại nguồn: vũ khí đánh xa (Bow/Grimoire) → Ranged; còn lại → Melee.
        info.sourceType = (currentWpn != null && currentWpn.isRanged) ? DamageSourceType.Ranged : DamageSourceType.Melee;
        if (isHeavy)
        {
            // Handler có thể set suppressNextKnockback=true trước khi gọi ApplyDamage
            // để bỏ qua knockback (Dagger spin, Spear thrust...)
            if (!suppressNextKnockback)
            {
                info.isKnockback    = true;
                info.knockbackForce = (nextHitKnockbackForce >= 0f) ? nextHitKnockbackForce : 15f;
                info.impactLevel    = 1;
            }
            suppressNextKnockback    = false; // auto-reset
            nextHitKnockbackForce    = -1f;   // auto-reset
        }
        else
        {
            info.impactLevel = 0;
            if (currentWpn != null && currentWpn.comboEffects != null && stepIndex < currentWpn.comboEffects.Count)
            {
                var effect = currentWpn.comboEffects[stepIndex];
                info.isKnockback    = effect.causesKnockback;
                info.knockbackForce = effect.knockbackForce;
                info.isStun         = effect.causesStun;
                info.stunDuration   = effect.stunDuration;
            }
        }

        // Override stun từ handler (Spear heavy, Grimoire heavy...)
        // Set nextHitStun=true + nextHitStunDuration trước khi gọi ApplyDamage
        if (nextHitStun)
        {
            info.isStun       = true;
            info.stunDuration = nextHitStunDuration;
            nextHitStun          = false; // auto-reset
            nextHitStunDuration  = 0f;
        }

        // Perfect Dodge Counter (Check từ Stats)
        if (stats.isPerfectDodgeSuccess)
        {
            info.isStun = true;
            info.stunDuration = 1f;
            info.impactLevel = 1;
            stats.isPerfectDodgeSuccess = false;
            Debug.Log(">> DODGE COUNTER!");
        }

        // 3. Multiplier
        float attackMultiplier = isHeavy ? currentDamageMultiplier : ((stepIndex == 0) ? 1.0f : 1.5f);
        // [MỚI] Áp dụng Sức mạnh Thách Đấu
        if (isDuelistEmpoweredAttackActive)
        {
            attackMultiplier *= 2.0f; // x2 Sát thương
            info.isStun = true;
            info.stunDuration = 3.0f; // Choáng 3 giây
            info.impactLevel = 1;     // Phá luôn Super Armor
        }

        // [WEAPON EFFECT] Nhân hệ số sát thương đòn vũ khí theo passive (khoảng cách, shield, Sin, stack...).
        if (_weaponFx != null) attackMultiplier *= _weaponFx.GetBasicAttackDamageMultiplier(enemyStats);

        // 4. Crit & Counter Logic (Dùng biến cache isDuelistCounterActive)
        float totalCritChance = stats.critChance;
        if (currentWpn != null) totalCritChance += currentWpn.bonusCritChance;

        // Perfect Parry Counter auto Crit và xuyên giáp)
        bool forceCritOrTrueDamage = isDuelistCounterActive;

        bool isCrit = forceCritOrTrueDamage || CombatMath.CheckIsCrit(totalCritChance);
        bool ignoreReduction = forceCritOrTrueDamage;

        // [WEAPON EFFECT] Đòn cường hóa xuyên 100% giáp/kháng phép + chắc chắn crit (vd SP_T4_03).
        if (_weaponFx != null && _weaponFx.ConsumeArmorPenCritHit()) { ignoreReduction = true; isCrit = true; }

        // [ACCESSORY EFFECT] Chỉnh đòn đánh thường/heavy: nhân dmg, ép crit, +crit mult, xuyên giáp (RM_T3_05/T4_02/T5_03, MS_T5_05).
        float accArmorPen = 0f;
        float accCritMultBonus = 0f;
        if (_accessoryFx != null)
            _accessoryFx.ModifyBasicAttack(enemyStats, isHeavy, t, ref attackMultiplier, ref isCrit, ref accArmorPen, ref accCritMultBonus);

        // [COMPANION Debuffer Passive] Đánh trúng hướng Điểm Yếu → bỏ qua 50% Armor/MR (+True 3% maxHp xử lý sau khi giáng đòn).
        bool companionWeakHit = CompanionWeaknessSystem.TryConsume(enemyStats, transform.position);
        if (companionWeakHit) accArmorPen = Mathf.Max(accArmorPen, 0.5f);

        if (isTestCrit) isCrit = true;
        info.isCrit = isCrit;
        if (isCrit) Debug.Log("<color=red>CRITICAL HIT!</color>");

        // 5. Calculate Final Damage (tạm cộng crit mult bonus từ accessory cho đòn này rồi khôi phục)
        float _savedBaseCritMult = stats.baseCritMultiplier;
        if (accCritMultBonus != 0f) stats.baseCritMultiplier += accCritMultBonus;
        var dmgTuple = CombatMath.CalculateFullDamage(
                stats, enemyStats, t, isCrit, null, currentWpn, attackMultiplier, ignoreReduction, accArmorPen
            );
        stats.baseCritMultiplier = _savedBaseCritMult;

        // Gán vào 3 biến mới thay vì info.damageAmount
        if (forceTrueDamage)
        {
            // WPN_BW_T5_01 Tia Sáng Mặt Trời: gộp toàn bộ thành sát thương chuẩn (bỏ qua giáp/MR).
            info.physDamage = 0f;
            info.magicDamage = 0f;
            info.trueDamage = dmgTuple.phys + dmgTuple.magic + dmgTuple.trueDmg;
        }
        else if (grimoirePhiDao)
        {
            // WPN_GR_T4_04 Phi Dao: đòn chính theo magicAtk nhưng tính VẬT LÝ (chịu giáp) + bonus phép (100% thường / 150% heavy).
            var conv = CombatMath.CalculateGrimoirePhiDao(stats, enemyStats, t, isCrit, attackMultiplier, isHeavy ? 1.5f : 1.0f);
            info.physDamage = conv.phys;
            info.magicDamage = conv.magic;
            info.trueDamage = 0f;
        }
        else
        {
            info.physDamage = dmgTuple.phys;
            info.magicDamage = dmgTuple.magic;
            info.trueDamage = dmgTuple.trueDmg;
        }

        // --- 6. Send ---
        enemyStats.TakeDamage(info);

        // [COMPANION Debuffer Passive] Kích nổ Điểm Yếu → gây thêm True = 3% maxHp địch.
        if (companionWeakHit && enemyStats.currentHp > 0)
            enemyStats.TakeDamage(new DamageInfo { trueDamage = enemyStats.maxHp * 0.03f, attacker = stats, sourcePosition = transform.position });

        // --- LOGIC HÚT MÁU ---
        float physHeal = info.physDamage * stats.physicalLifeSteal;
        float magicHeal = info.magicDamage * stats.magicLifeSteal;
        float totalHeal = physHeal + magicHeal;

        if (totalHeal > 0)
        {
            // isLifesteal=true: hút máu KHÔNG bị healing-block chặn (GDD MS_T5_02)
            stats.Heal(totalHeal, true, true);
        }

        // --- 7 BÁO CHO THÚ CƯNG BIẾT ĐỂ GHI SỔ ĐEN ---
        CompanionAI myCompanion = FindFirstObjectByType<CompanionAI>();
        if (myCompanion != null)
        {
            myCompanion.AddMarkedTarget(enemyStats.transform);
        }
        // -------------------------------------------------

        // Notify
        if (stats != null) stats.NotifyOnHitEnemy(enemyStats, t, isCrit);
        OnHitEnemy?.Invoke(enemyStats, stepIndex, isHeavy, isCrit);
        OnDamageDealt?.Invoke(enemyStats, info);

        // [COMBAT FEEL] hit-stop/shake theo sức mạnh đòn.
        // Hit flash do HitFlash tự lắng nghe Stats.OnDamageReceived (autoSubscribe) — KHÔNG
        // gọi Flash() ở đây để tránh nháy 2 lần (1 nguồn duy nhất).

        // Kill Check
        if (wasAlive && enemyStats.currentHp <= 0)
        {
            bool isBackstab = (t == 1f);
            if (stats != null) stats.NotifyKillEnemy(enemyStats, isBackstab);
            // [MỚI] RUNG CHUÔNG SỰ KIỆN Ở ĐÂY
            OnKillEnemy?.Invoke(enemyStats, isBackstab);
            Debug.Log(">> KẾT LIỄU ĐỊCH!");
            CombatFeel.OnHit(CombatFeel.HitStrength.Kill, enemyStats.name);
        }
        else
        {
            // Ưu tiên: Crit > Heavy > Normal
            CombatFeel.HitStrength strength =
                isCrit  ? CombatFeel.HitStrength.Crit  :
                isHeavy ? CombatFeel.HitStrength.Heavy :
                          CombatFeel.HitStrength.Normal;
            CombatFeel.OnHit(strength, enemyStats.name);
        }
    }

    // Tách phần gây damage ra cho gọn
    // [CẬP NHẬT] Thêm tham số stepIndex để biết chính xác đòn này là đòn thứ mấy
    // Thay thế toàn bộ hàm HandleDamageLogic cũ bằng hàm này
    // Biến cache skill (Khai báo ở đầu class PlayerController nếu chưa có)
    // private DuelistPassive duelistSkill; 

    // Biến Cache (Nhớ khai báo ở đầu class)

    /*
        // --- HÀM CŨ (AOE BURST) ---
        // Giữ lại để tham khảo hoặc dùng cho Skill AOE đặc biệt sau này
        void HandleDamageLogic_Legacy(bool isHeavy, int stepIndex)
        {
            bool hitAnything = false;
            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, dangerLayer);

            // Logic Duelist cũ (Check ngay tại thời điểm gọi hàm)
            bool isDuelistCounter = false;
            if (duelistSkill == null) duelistSkill = GetComponent<DuelistPassive>();
            if (duelistSkill != null && hitEnemies.Length > 0)
            {
                 isDuelistCounter = duelistSkill.TryUseCounterAttack();
            }

            foreach (Collider enemy in hitEnemies)
            {
                // Check góc (Logic cũ)
                Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized;
                Vector3 facingDir = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
                if (Vector3.Angle(facingDir, dirToEnemy) > attackAngle / 2f) continue;

                Stats enemyStats = enemy.GetComponent<Stats>();
                if (enemyStats != null && enemyStats.currentHp > 0)
                {
                    // Gọi hàm ApplyDamageToTarget ở trên, nhưng phải truyền biến isDuelistCounter vào
                    // (Vì hàm ApplyDamageToTarget hiện tại dùng biến cache, nên logic cũ này cần sửa lại xíu nếu muốn dùng lại)
                    // Tạm thời comment logic bên trong.
                    hitAnything = true;
                }
            }
        }
        */

    /* [DEPRECATED] Moved to DevToolPanel UI — Các hàm dưới đây chỉ delegate thẳng sang Manager,
       không có logic riêng. Đã được thay thế bởi DevToolPanel UI buttons.

    // Hàm trang bị hoặc đổi vũ khí (xài chung)
    void EquipWeapon()
    {
        Debug.Log("Đang xài vũ khí:" + equipmentManager.currentWeapon);
        equipmentManager.EquipWeapon(equipmentManager.pickUpWeapon);
        OnWeaponEquipped?.Invoke(equipmentManager.currentWeapon);
    }
    // Tháo vũ khí (UI sẽ hiện là tháo, code là chuyển sang Hand)
    void DropWeapon()
    {
        Debug.Log("Drop vũ khí");
        equipmentManager.ResetToBaseWeapon();
    }
    // Hàm trang bị hoặc đổi core shield (xài chung)
    void EquipCoreShield()
    {
        Debug.Log("Đang xài core shield:" + equipmentManager.currentCoreShield);
        equipmentManager.EquipCoreShield(equipmentManager.pickUpCoreShield);
    }
    // Tháo core shield (chuyển sang rỗng)
    void DropCoreShield()
    {
        Debug.Log("Tháo Core Shield");
        equipmentManager.UnequipCoreShield();
    }

    // Hàm trang bị hoặc đổi Accessory, xài chung cho cả 5 loại
    void EquipPickedAccessory()
    {
        // Bạn không cần quan tâm nó là loại gì, ném vào Manager tự xử lý
        equipmentManager.EquipAccessory(equipmentManager.pickUpAccessory);
    }

    void DropPickedAccessory()
    {
        // Tháo đúng món đang giữ trong biến pickUpAccessory
        equipmentManager.UnequipAccessory(equipmentManager.pickUpAccessory);
    }
    void LearnSkill()
    {
        skillManager.EquipSkill(skillManager.pickUpSkill);
    }
    void UpdateStat()
    {
        stats.RecalculateStats();
        Debug.Log("Đã tính toán lại stat thủ công");
    }
    */

    public void TakeDamage(int damage)
    {
        if (stats != null && stats.isInvincible) return;
        Debug.Log($"Odo bị đánh trúng (Test Effect)!");
        if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.2f);
        if (stats != null) stats.TakeDamage(damage);
    }
    // ============================================================
    // [MỚI] CÁC HÀM HỖ TRỢ AI (CHO RAVAGER SKILL GỌI)
    // ============================================================

    // Hàm AI tự di chuyển (Bỏ qua Input người chơi)
    public void AI_MoveTo(Vector3 targetPos)
    {
        // [ACTION-LOCK] AI-control (Ravager...) cũng phải tuân luật CC: Root/Stun/Airborne → không tự đi.
        if (stats == null || stats.IsMovementLocked) return;

        // 1. Tính hướng
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;

        // 2. Quay mặt (Cập nhật visual)
        currentVisualDir = dir;
        UpdateAnimationDirection(dir);

        // 3. Di chuyển Rigidbody
        if (!isAttacking && !isDashing)
        {
            // Tính tốc độ (có tính sprint nếu cần, hoặc mặc định)
            float speed = stats.moveSpeed;
            // Nếu muốn Ravager chạy nhanh như Sprint thì nhân thêm:
            // float speed = stats.moveSpeed * stats.runSpeedMultiplier; 

            Vector3 targetPosRb = rb.position + dir * speed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosRb);

            // 4. Bật animation chạy
            if (animator != null) animator.SetBool("IsWalking", true);
        }
    }

    // Hàm AI tự đánh
    public void AI_Attack()
    {
        // [ACTION-LOCK] Stun/Airborne khóa đánh (Root/Silence vẫn cho basic attack — IsActionLocked không gồm 2 cái đó).
        if (stats == null || stats.IsActionLocked) return;

        // 1. Dừng animation chạy để chuyển sang đánh
        if (animator != null) animator.SetBool("IsWalking", false);

        // 2. Gọi hàm đánh thường (Light Attack)
        PerformAttack(false);
    }
    
    // ÉP HƯỚNG NHÌN TỨC THỜI (DÙNG CHO KỸ NĂNG DỊCH CHUYỂN)
    public void ForceFaceDirection(Vector3 dir)
    {
        dir.y = 0;
        if (dir == Vector3.zero) return;

        // Cập nhật biến nội bộ của PlayerController
        currentVisualDir = dir.normalized;

        // Ép Animator và Sprite quay ngay lập tức
        UpdateAnimationDirection(currentVisualDir);

        // Đồng bộ ngược lại cho Stats
        if (stats != null) stats.facingDirection = currentVisualDir;
    }

    public void SetTurnSmoothTime(float time) { if (stats != null) stats.turnDuration = time; }
    void OnDrawGizmosSelected() {
        // Vẽ vòng tròn tầm đánh
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Vẽ hình quạt góc đánh
        Vector3 forward = Vector3.forward;
        if (stats != null && stats.facingDirection != Vector3.zero) forward = stats.facingDirection;
        else forward = transform.forward; // Fallback

        // Cạnh trái
        Vector3 leftRay = Quaternion.AngleAxis(-attackAngle / 2, Vector3.up) * forward;
        // Cạnh phải
        Vector3 rightRay = Quaternion.AngleAxis(attackAngle / 2, Vector3.up) * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, leftRay * attackRange);
        Gizmos.DrawRay(transform.position, rightRay * attackRange);

        // Vẽ hướng mặt hiện tại
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, forward * (attackRange * 0.5f));

        // Vẽ tầm Perfect Dodge (Xanh Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, perfectDodgeRadius);
    }
}