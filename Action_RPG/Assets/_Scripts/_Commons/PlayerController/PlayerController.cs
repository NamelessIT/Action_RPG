using UnityEngine;
using Unity.Cinemachine;
using System.Collections;


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

    // Biến tính toán runtime
    //private float currentAttackCooldown = 0f;

    [Header("Combat Settings")]
    public float attackRange = 0.5f;
    public LayerMask enemyLayer;
    private bool nextAttackQueued = false; // Đã bấm chuột cho đòn tiếp theo chưa?

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
    public bool testIsCrit = false;

    private EquipmentManager equipmentManager;

    // [MỚI] Biến để lưu tiến trình đánh đang chạy
    private Coroutine currentAttackCoroutine;

    void Start()
    {
        stats = GetComponent<AllyStats>();

        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        enemyLayer = LayerMask.GetMask("Enemy");

        if (stats == null) Debug.LogError("Thiếu CharacterStats!");
        if (animator == null) Debug.LogError("Thiếu Animator!");
        if (spriteRenderer == null) Debug.LogError("Thiếu SpriteRenderer!");

        currentVisualDir = Vector3.back;

        equipmentManager = GetComponent<EquipmentManager>();
    }

    void Update()
    {
        if (stats == null) return;

        // Cập nhật tốc độ đánh cho Animator (để múa kiếm nhanh chậm theo stats)
        if (animator != null)
        {
            animator.SetFloat("AttackSpeedMultiplier", stats.attackSpeed);
        }

        // --- 1. DASH (Ưu tiên cao nhất) ---
        if (isDashing) return; // Đang lướt thì không nhận input khác

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            PerformDash();
            return;
        }

        // --- 2. SPRINT (Chạy nhanh) ---
        // Điều kiện: Giữ Shift + Đang di chuyển
        if (Input.GetKey(KeyCode.LeftShift) && movementInput.magnitude > 0.1f)
        {
            // [MỚI] Trừ thể lực theo thời gian (Run Cost * Time.deltaTime)
            // Nếu đủ thể lực thì cho phép chạy nhanh
            if (stats.TryConsumeStamina(stats.runCost * Time.deltaTime))
            {
                isSprinting = true;
            }
            else
            {
                // Hết thể lực -> Tắt chạy nhanh
                isSprinting = false;
                // Debug.Log("Hết hơi!"); 
            }
        }
        else
        {
            isSprinting = false;
        }

        // --- 3. TẤN CÔNG ---
        if (Input.GetMouseButtonDown(0)) PerformAttack();

        // --- 4. DI CHUYỂN ---
        HandleMovementStopToTurn();

        // [MỚI] Cập nhật hướng nhìn vào Stats để Enemy biết đường mà đánh lén
        if (stats != null) stats.facingDirection = currentVisualDir;

        // Test keys
        if (Input.GetKeyDown(KeyCode.K)) TakeDamage(10);
        if (Input.GetKeyDown(KeyCode.T) && stats != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, stats);
            Debug.Log($"Hệ số hướng t={t}");
        }
        if (Input.GetKeyDown(KeyCode.E)) EquipWeapon();
        if (Input.GetKeyDown(KeyCode.X)) DropWeapon();
        if (Input.GetKeyDown(KeyCode.C)) EquipCoreShield();
        if (Input.GetKeyDown(KeyCode.U)) DropCoreShield();
        if (Input.GetKeyDown(KeyCode.N)) EquipPickedAccessory();
        if (Input.GetKeyDown(KeyCode.M)) DropPickedAccessory();
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

    void HandleMovementStopToTurn()
    {

        // [MỚI] Nếu đang đánh thì KHÔNG nhận input di chuyển nữa
        if (isAttacking)
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
    void FixedUpdate()
    {
        if (stats == null) return;

        if (!isTurning && isWalking && !isDashing)
        {
            float currentSpeed = stats.baseMoveSpeed * (isSprinting ? stats.runSpeedMultiplier : 1f);
            Vector3 targetPosition = rb.position + movementInput * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }

    void PerformAttack()
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

        currentAttackCoroutine=StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        // 1. Setup
        isAttacking = true;
        nextAttackQueued = false;
        lastAttackTime = Time.time;

        // --- DEBUG ---
        // Debug.Log("Thực hiện đánh Combo số: " + comboCount);

        // 2. Gửi Animator
        if (animator != null)
        {
            // [FIX 1] Reset Trigger cũ để tránh bị dính lệnh thừa từ lần bấm trước
            animator.ResetTrigger("Attack");

            animator.SetFloat("ComboStep", (float)comboCount);
            animator.SetTrigger("Attack");
        }

        // 3. Tăng Combo
        comboCount++;
        if (comboCount >= maxCombo) comboCount = 0;

        // 4. Deal Damage
        yield return new WaitForSeconds(0.1f);
        HandleDamageLogic();

        // 5. Tính thời gian chờ (Animation Duration)
        float baseAnimDuration = 0.5f;
        float realDuration = baseAnimDuration / stats.attackSpeed;

        // [FIX 2] Thay vì trừ số cứng, hãy chờ khoảng 90% thời lượng animation
        // Điều này giúp animation chạy gần hết mới chuyển, tránh bị cắt quá sớm nhìn bị giật
        float waitTime = realDuration * 0.9f;

        yield return new WaitForSeconds(waitTime);

        // 6. Mở khóa
        isAttacking = false;

        // 7. Check hàng chờ (Input Buffer)
        if (nextAttackQueued)
        {
            // [FIX 3] QUAN TRỌNG NHẤT: Chờ 1 Frame để Animator kịp thở và xử lý xong Transition cũ
            yield return null;

            currentAttackCoroutine = StartCoroutine(AttackRoutine());
        }
    }

    // Tách phần gây damage ra cho gọn
    void HandleDamageLogic()
    {
        bool hitAnything = false;
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        foreach (Collider enemy in hitEnemies)
        {
            Stats enemyStats = enemy.GetComponent<Stats>();
            if (enemyStats != null)
            {
                hitAnything = true;
                stats.EnterCombat();
                float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

                // Có thể thêm logic: Đòn cuối combo (comboCount == maxCombo) thì damage to hơn
                float comboMultiplier = (comboCount == 0) ? 1.5f : 1.0f; // Vì comboCount đã ++ ở trên nên lúc này 0 nghĩa là vừa đánh xong đòn cuối (reset)

                float damage = CombatMath.CalculateFullDamage(stats, enemyStats, t, testIsCrit);
                enemyStats.TakeDamage(damage * comboMultiplier);
            }
        }
        if (hitAnything) Debug.Log("Tấn công TRÚNG ĐỊCH -> Vào Combat");
    }


    // Hàm trang bị hoặc đổi vũ khí (xài chung)
    void EquipWeapon()
    {
        Debug.Log("Đang xài vũ khí:" + equipmentManager.currentWeapon);
        equipmentManager.EquipWeapon(equipmentManager.pickUpWeapon);
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


    public void TakeDamage(int damage)
    {
        if (stats != null && stats.isInvincible) return;
        Debug.Log($"Odo bị đánh trúng (Test Effect)!");
        if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.2f);
        if (stats != null) stats.TakeDamage(damage);
    }

    public void SetTurnSmoothTime(float time) { if (stats != null) stats.turnDuration = time; }
    void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange); }
}