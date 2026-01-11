using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    public float idleDelay = 0.25f;

    private CharacterStats stats;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private CinemachineImpulseSource impulseSource;

    [Header("Combat Settings")]
    public float attackRange = 1.5f;
    public LayerMask enemyLayer;

    // State variables
    private int lastDirection = 0;
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
    public Transform testEnemyTarget;
    public bool testIsCrit = false;

    void Start()
    {
        stats = GetComponent<CharacterStats>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        impulseSource = GetComponent<CinemachineImpulseSource>();

        if (stats == null) Debug.LogError("Thiếu CharacterStats!");
        if (animator == null) Debug.LogError("Thiếu Animator!");
        if (spriteRenderer == null) Debug.LogError("Thiếu SpriteRenderer!");

        currentVisualDir = Vector3.back;
    }

    void Update()
    {
        if (stats == null) return;

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

        // Test keys
        if (Input.GetKeyDown(KeyCode.K)) TakeDamage(10);
        if (Input.GetKeyDown(KeyCode.T) && testEnemyTarget != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, testEnemyTarget);
            Debug.Log($"Hệ số hướng t={t}");
        }
    }

    void PerformDash()
    {
        if (Time.time < stats.lastDashTime + stats.dashRecovery)
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

        StartCoroutine(DashCoroutine());
    }

    IEnumerator DashCoroutine()
    {
        isDashing = true;
        isSprinting = false;
        stats.lastDashTime = Time.time;
        stats.isInvincible = true;

        Vector3 dashDir = movementInput.magnitude > 0.1f ? movementInput : currentVisualDir;
        dashDir.y = 0;
        dashDir.Normalize();

        // Cập nhật hướng nhìn ngay lập tức theo hướng dash để Sprite quay đúng hướng
        currentVisualDir = dashDir;
        UpdateAnimationDirection(currentVisualDir); // Cập nhật ngay để Animator nhận IsWalking=true

        float duration = stats.dashDuration;
        float dashSpeed = stats.dashDistance / duration;

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

    //void UpdateAnimationDirection(Vector3 facingDir)
    //{
    //    if (Mathf.Abs(facingDir.z) > Mathf.Abs(facingDir.x))
    //    {
    //        lastDirection = facingDir.z > 0 ? 1 : 0;
    //    }
    //    else
    //    {
    //        lastDirection = 2;
    //    }

    //    if (facingDir.x > 0.1f) spriteRenderer.flipX = true;
    //    else if (facingDir.x < -0.1f) spriteRenderer.flipX = false;

    //    if (animator != null)
    //    {
    //        // [CẬP NHẬT QUAN TRỌNG] Thêm || isDashing
    //        // Nếu đang Dash -> IsWalking = true -> Animator chuyển sang Walk_Tree (Logic animation chạy/lướt)
    //        animator.SetBool("IsWalking", isWalking || isTurning || isDashing);
    //        animator.SetFloat("Direction", (float)lastDirection);
    //    }
    //    // -------------------------------





    //    /* =========================================================

    //       [8-DIRECTION & 5-SPRITES SETUP] 

    //       KHI NÀO CÓ ĐỦ 5 ANIMATION (Dưới, Trên, Trái, Dưới-Trái, Trên-Trái):

    //       1. Vào Animator tạo Parameter "DirectionInt" (Type: Int).

    //       2. Bỏ comment đoạn code dưới đây.

    //       3. Comment lại đoạn code 4 hướng ở trên.

    //       ========================================================= */



    //    /*

    //    // 1. Tính góc 360 độ (0 là hướng Nam/Dưới, tăng dần theo chiều kim đồng hồ)

    //    float angle = Vector3.SignedAngle(Vector3.back, facingDir, Vector3.up);

    //    if (angle < 0) angle += 360;



    //    // 2. Chia thành 8 hướng (mỗi hướng 45 độ)

    //    // Cộng 22.5 để xoay trục cho khớp

    //    int directionIndex = Mathf.FloorToInt((angle + 22.5f) / 45f);

    //    if (directionIndex >= 8) directionIndex = 0; 



    //    // 3. Logic FlipX cho 5 Sprites (Lật các hướng bên Phải thành bên Trái)

    //    // Index: 0=S, 1=SW, 2=W, 3=NW, 4=N, 5=NE, 6=E, 7=SE



    //    bool shouldFlip = false;

    //    int animationToPlay = directionIndex;



    //    if (directionIndex > 4) // Các hướng bên phải (5, 6, 7)

    //    {

    //        shouldFlip = true;

    //        // Map ngược lại về sprite bên trái

    //        // 5 (NE) -> 3 (NW)

    //        // 6 (E)  -> 2 (W)

    //        // 7 (SE) -> 1 (SW)

    //        animationToPlay = 8 - directionIndex; 

    //    }



    //    // 4. Gửi vào Animator & SpriteRenderer

    //    if (animator != null)

    //    {

    //        animator.SetBool("IsWalking", isWalking || isTurning);

    //        animator.SetInteger("DirectionInt", animationToPlay); // Gửi index 0,1,2,3,4

    //    }



    //    // Luôn set FlipX theo logic đã tính toán

    //    spriteRenderer.flipX = shouldFlip;

    //    */
    //}
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
            float currentSpeed = stats.moveSpeed * (isSprinting ? stats.runSpeedMultiplier : 1f);
            Vector3 targetPosition = rb.position + movementInput * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }

    void PerformAttack()
    {
        if (stats == null) return;

        bool hitAnything = false;
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        foreach (Collider enemy in hitEnemies)
        {
            CharacterStats enemyStats = enemy.GetComponent<CharacterStats>();
            if (enemyStats != null)
            {
                hitAnything = true;
                stats.EnterCombat();
                float t = CombatMath.CalculateDirectionFactor(transform, enemy.transform);
                float damage = CombatMath.CalculateFullDamage(stats, enemyStats, t, testIsCrit);
                enemyStats.TakeDamage(damage);
                if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.1f);
            }
        }
        if (hitAnything) Debug.Log("Tấn công TRÚNG ĐỊCH -> Vào Combat");
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