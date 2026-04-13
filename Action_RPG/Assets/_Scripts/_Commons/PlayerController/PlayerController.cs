using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Rendering;
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
    private float chargeTimer = 0f;
    private float currentDamageMultiplier = 1.0f; // Hệ số sát thương của đòn hiện tại


    // State variables
    //private int lastDirection = 0;
    private bool isWalking = false;
    private float lastMoveTime = 0f;
    private bool isTurning = false;

    // Dash & Sprint State
    private bool isDashing = false;
    private bool isSprinting = false;

    // Movement variables
    private Vector3 movementInput;
    private Vector3 currentVisualDir;

    // Testing
    public bool isTestCrit = false;

    private EquipmentManager equipmentManager;
    private SkillManager skillManager;

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
    // ==================================================================


    private DuelistPassive duelistSkill;
    private bool isDuelistCounterActive = false; // Cache trạng thái counter cho cả vòng lặp quét
    private InventoryUIManager _inventoryUIManager;
    private ItemPickupManager _pickupManager;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private DevToolPanel _devToolPanel;
#endif
    void Start()
    {
        stats = GetComponent<AllyStats>();

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
        _inventoryUIManager = FindFirstObjectByType<InventoryUIManager>();
        _pickupManager = GetComponent<ItemPickupManager>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _devToolPanel = FindFirstObjectByType<DevToolPanel>(FindObjectsInactive.Include);
#endif

        // [002-E] Initialize player vision manager
        InitializeVisionManager();
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

        if (InventoryUIManager.IsInventoryOpen) return;

        // [SỬA Ở ĐÂY] Khóa di chuyển vật lý nếu đang dùng skill, lướt, parry, hoặc ĐANG GỒNG
        if (isUsingSpecialSkill || isDashing || stats.isParrying || isCharging) return;

        // Logic di chuyển thường
        if (!isTurning && isWalking)
        {
            float currentSpeed = stats.moveSpeed * (isSprinting ? stats.runSpeedMultiplier : 1f);
            Vector3 targetPosition = rb.position + movementInput * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }

    void Update()
    {
        if (stats == null) return;

        // B key được xử lý TRUỜC guard IsInventoryOpen để đóng và mở đều hoạt động
        if (Input.GetKeyDown(KeyCode.B))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_devToolPanel != null && _devToolPanel.gameObject.activeSelf)
                return; // DevTool đang mở → không toggle inventory
#endif
            if (_inventoryUIManager == null)
                _inventoryUIManager = FindFirstObjectByType<InventoryUIManager>();
            _inventoryUIManager?.ToggleInventory();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F) && !InventoryUIManager.IsInventoryOpen)
        {
            _pickupManager?.TryPickupNearest();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (_devToolPanel != null && !InventoryUIManager.IsInventoryOpen)
            {
                bool willShow = !_devToolPanel.gameObject.activeSelf;
                _devToolPanel.gameObject.SetActive(willShow);

                // Nếu mở DevTool → show cursor, pause game
                if (willShow)
                {
                    Time.timeScale = 0f;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                else
                {
                    Time.timeScale = 1f;
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
            return; // Prevent V from doing anything else
        }
#endif

        if (InventoryUIManager.IsInventoryOpen) return;

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
        if (Input.GetKey(KeyCode.LeftShift) && movementInput.magnitude > 0.1f)
        {
            if (stats.TryConsumeStamina(stats.runCost * Time.deltaTime))
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
        // [MỚI] Nếu đang Parry thì HỦY LUÔN việc gồng (Ưu tiên đỡ đòn)
        if (stats.isParrying && isCharging)
        {
            isCharging = false;
        }

        // 1. Bắt đầu nhấn chuột -> Bắt đầu tính giờ
        if (Input.GetMouseButtonDown(0) && !isAttacking && !isStance && !stats.isParrying)
        {
            isCharging = true;
            chargeTimer = 0f;
        }

        // 2. Đang giữ chuột -> Tăng thời gian
        if (isCharging)
        {
            if (Input.GetMouseButton(0))
            {
                chargeTimer += Time.deltaTime;

                // --- [FIX QUAN TRỌNG] TỰ ĐỘNG TUNG TRỌNG KÍCH QUÁ GIỜ ---
                // Nếu giữ quá (thời gian chuẩn + 3 giây) thì tự đánh luôn
                if (chargeTimer >= stats.heavyAttackChargeTime + 2.0f)
                {
                    isCharging = false;
                    PerformAttack(true); // Tự động đánh mạnh
                    return; // Thoát sớm để không chạy xuống phần nhả chuột nữa
                }
            }

            // 3. Nhả chuột -> Quyết định đánh thường hay đánh mạnh
            if (Input.GetMouseButtonUp(0) && isCharging)
            {
                isCharging = false;
                if (chargeTimer >= stats.heavyAttackChargeTime) PerformAttack(true);
                else PerformAttack(false);
            }
        }

        // Queue Attack
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
        if (Time.time < stats.lastDashTime + stats.baseDashRecovery)
        {
            Debug.Log("Dash Cooldown!");
            return;
        }

        // Dash tiêu tốn một cục thể lực lớn ngay lập tức
        if (!stats.TryConsumeStamina(stats.dashCost))
        {
            Debug.Log("Không đủ Stamina để Dash!");
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
        if (isAttacking)
        {
            // 1. Dừng ngay lập tức Coroutine đánh đang chạy
            if (currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
            }

            // 2. Reset các trạng thái tấn công
            isAttacking = false;
            nextAttackQueued = false; // Xóa luôn lệnh đánh tiếp theo nếu có

            // 3. Reset Animator để nó không bị kẹt ở pose đánh
            if (animator != null)
            {
                animator.ResetTrigger("Attack");
                animator.SetFloat("ComboStep", 0);
                // Có thể cần play ngay animation khác hoặc để Blend Tree tự xử lý
            }

            Debug.Log(">> Đã Cancel Attack để Dash!");
        }
        // ---------------------------------

        StartCoroutine(DashCoroutine());
    }


    IEnumerator DashCoroutine()
    {
        isDashing = true;
        isSprinting = false;
        stats.lastDashTime = Time.time;
        stats.isInvincible = true;

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

        yield return new WaitForSeconds(0.1f);
        stats.isInvincible = false;

        if (duration > 0.1f)
        {
            yield return new WaitForSeconds(duration - 0.1f);
        }

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

        // [MỚI] Nếu đang đánh thì KHÔNG nhận input di chuyển nữa
        if (isAttacking || stats.isParrying || isCharging)
        {
            movementInput = Vector3.zero; // Xóa vector di chuyển
            return; // Thoát hàm ngay, không tính toán xoay hay đi nữa
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

        // Setup Multiplier
        if (isHeavy) currentDamageMultiplier = stats.heavyAttackCharge;
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

        // Góc chém: Giả sử chém từ Phải (Góc dương) sang Trái (Góc âm)
        // (Nếu muốn chuẩn theo từng animation chém trái/phải thì cần logic phức tạp hơn)
        float startAngle = attackAngle / 2f;
        float endAngle = -attackAngle / 2f;

        // 4. Chờ vung tay (Wind-up)
        yield return new WaitForSeconds(startDamageTime);

        // 5. VÒNG LẶP QUÉT (SWEEPING)
        // --- [SỬA Ở ĐÂY] CHIA NHÁNH ĐÁNH GẦN VÀ BẮN XA ---
        if (isRangedAttack && projectilePrefab != null)
        {
            // BẮN ĐẠN
            Vector3 spawnPos = transform.position + Vector3.up * projectileSpawnOffsetY; // Sinh đạn ở ngang ngực (cao 1m)
            Vector3 fireDir = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;

            GameObject projObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            Projectile projScript = projObj.GetComponent<Projectile>();

            if (projScript != null)
            {
                // Truyền dữ liệu cho viên đạn tự xử lý
                projScript.Setup(this, fireDir, attackRange, isHeavy, currentStep);
            }
        }
        else
        {
            // ĐÁNH CẬN CHIẾN (Quét hình quạt như cũ)
            float currentSweepTime = 0f;
            bool hitAnyInSweep = false;

            while (currentSweepTime < swingDuration)
            {
                currentSweepTime += Time.deltaTime;
                float t = currentSweepTime / swingDuration;
                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

                if (PerformPlayerSweep(currentAngle, isHeavy, currentStep))
                {
                    hitAnyInSweep = true;
                }
                yield return null;
            }

            if (hitAnyInSweep)
            {
                if (stats != null && hitTargets.Count > 0)
                {
                    stats.GainSinFromAttack(hitTargets.Count); // Tích Sin cho đánh gần
                }
            }
        }

        // 6. Recovery (Chờ nốt animation)
        // [FIX] Tính lại thời gian còn lại chính xác
        yield return new WaitForSeconds(realDuration - endDamageTime);

        isAttacking = false;
        currentDamageMultiplier = 1.0f;

        // 7. Input Buffer
        if (nextAttackQueued)
        {
            yield return null;
            currentAttackCoroutine = StartCoroutine(AttackRoutine(false));
        }
    }

    // Hàm quét tại 1 góc (Trả về true nếu trúng ai đó mới)
    bool PerformPlayerSweep(float angle, bool isHeavy, int stepIndex)
    {
        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 dirOfSword = rotation * forward;

        // Vị trí quét: Cách người 1 đoạn, bán kính 1m
        Vector3 checkPos = transform.position + dirOfSword * (attackRange * 0.8f);
        float checkRadius = attackRange * 0.5f; // Độ rộng đường kiếm

        Collider[] hits = Physics.OverlapSphere(checkPos, checkRadius, dangerLayer);
        bool hitNew = false;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                if (!hitTargets.Contains(hit.transform))
                {

                    // --- [FIX QUAN TRỌNG] THÊM LẠI CHECK GÓC Ở ĐÂY ---
                    // Dù Sphere có chạm, ta vẫn phải check xem Enemy có nằm trong góc AttackAngle không
                    // Để tránh trường hợp Sphere quá to lấn ra sau lưng

                    Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;

                    // Tính góc giữa "Mặt Player" và "Kẻ địch"
                    float angleToEnemy = Vector3.Angle(forward, dirToEnemy);

                    // Nếu góc lệch lớn hơn một nửa góc đánh -> Nằm ngoài hình quạt -> Bỏ qua
                    // (Ví dụ attackAngle 90 thì mỗi bên 45. Nếu địch ở góc 50 -> Skip)
                    if (angleToEnemy > attackAngle / 2f)
                    {
                        continue;
                    }
                    // ----------------------------------------------------

                    hitTargets.Add(hit.transform); // Mark as hit

                    Stats enemyStats = hit.GetComponent<Stats>();
                    if (enemyStats != null)
                    {
                        // Gọi hàm tính damage riêng biệt
                        ApplyDamageToTarget(enemyStats, isHeavy, stepIndex);

                        // Callback sự kiện (Chỉ gọi 1 lần mỗi lần trúng địch)
                        OnAttackPerformed?.Invoke(stepIndex, isHeavy);
                        hitNew = true;
                    }
                }
            }
        }
        return hitNew;
    }

    // --- HÀM TÍNH TOÁN DAMAGE (Tách ra từ HandleDamageLogic cũ) ---
    public void ApplyDamageToTarget(Stats enemyStats, bool isHeavy, int stepIndex)
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
        if (isHeavy)
        {
            info.isKnockback = true;
            info.knockbackForce = 15f;
            info.impactLevel = 1;
        }
        else
        {
            info.impactLevel = 0;
            if (currentWpn != null && currentWpn.comboEffects != null && stepIndex < currentWpn.comboEffects.Count)
            {
                var effect = currentWpn.comboEffects[stepIndex];
                info.isKnockback = effect.causesKnockback;
                info.knockbackForce = effect.knockbackForce;
                info.isStun = effect.causesStun;
                info.stunDuration = effect.stunDuration;
            }
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

        // 4. Crit & Counter Logic (Dùng biến cache isDuelistCounterActive)
        float totalCritChance = stats.critChance;
        if (currentWpn != null) totalCritChance += currentWpn.bonusCritChance;

        // Gộp cả 2 loại buff (Perfect Parry Counter VÀ Challenge đều auto Crit và xuyên giáp)
        bool forceCritOrTrueDamage = isDuelistCounterActive || isDuelistEmpoweredAttackActive;

        bool isCrit = forceCritOrTrueDamage || CombatMath.CheckIsCrit(totalCritChance);
        bool ignoreReduction = forceCritOrTrueDamage;

        if (isTestCrit) isCrit = true;
        info.isCrit = isCrit;
        if (isCrit) Debug.Log("<color=red>CRITICAL HIT!</color>");

        // 5. Calculate Final Damage
        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, null, currentWpn, attackMultiplier, ignoreReduction
        );
        info.damageAmount = damage;

        // --- 6. Send ---
        enemyStats.TakeDamage(info);

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

        // Kill Check
        if (wasAlive && enemyStats.currentHp <= 0)
        {
            bool isBackstab = (t == 1f);
            if (stats != null) stats.NotifyKillEnemy(enemyStats, isBackstab);
            Debug.Log(">> KẾT LIỄU ĐỊCH!");
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