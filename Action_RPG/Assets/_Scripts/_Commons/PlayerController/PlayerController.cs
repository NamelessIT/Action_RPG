using UnityEngine;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float idleDelay = 0.25f;

    // Khai báo các component
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private CinemachineImpulseSource impulseSource;

    [Header("Rotation Mechanics")]
    [Tooltip("Thời gian để xoay 180 độ (Giây). Ví dụ: 0.5s")]
    public float turnDuration = 0.1f ;

    [Tooltip("Góc lệch tối đa cho phép di chuyển. > 45 độ thì đứng lại xoay.")]
    public float moveThresholdAngle = 45f;

    [Header("Combat Settings")]
    public float attackRange = 1.5f;
    public LayerMask enemyLayer;
    public float baseDamage = 10f;

    // State variables
    private int lastDirection = 0;
    private bool isWalking = false;
    private float lastMoveTime = 0f;
    private bool isTurning = false;

    // Movement internal variables
    private Vector3 movementInput;
    private Vector3 currentVisualDir;

    // Testing
    public Transform testEnemyTarget;

    // Biến tạm để test Crit (Sau này có thể làm random)
    public bool testIsCrit = false;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        impulseSource = GetComponent<CinemachineImpulseSource>();

        if (animator == null) Debug.LogError("Thiếu Animator!");
        if (spriteRenderer == null) Debug.LogError("Thiếu SpriteRenderer!");

        currentVisualDir = Vector3.back;
    }

    void Update()
    {
        // 1. TẤN CÔNG
        if (Input.GetMouseButtonDown(0)) PerformAttack();

        // 2. DI CHUYỂN & XOAY
        HandleMovementStopToTurn();

        // 3. TEST
        if (Input.GetKeyDown(KeyCode.K)) TakeDamage(10);
        if (Input.GetKeyDown(KeyCode.T) && testEnemyTarget != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, testEnemyTarget);
            Debug.Log($"Hệ số hướng t={t}");
        }
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
            float rotSpeed = Mathf.PI / turnDuration;

            // Xử lý quay đầu 180 độ
            if (angleDifference > 175f)
            {
                currentVisualDir = Quaternion.Euler(0, 1f, 0) * currentVisualDir;
            }

            // Xoay từ từ
            currentVisualDir = Vector3.RotateTowards(
                currentVisualDir,
                movementInput,
                rotSpeed * Time.deltaTime,
                0.0f
            );

            // Kiểm tra góc lệch để quyết định đi hay dừng
            if (angleDifference > moveThresholdAngle)
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
        // --- CODE HIỆN TẠI (4 HƯỚNG) ---
        // Khi chưa gỡ comment 8 hướng, đoạn này vẫn chạy để game không lỗi
        if (Mathf.Abs(facingDir.z) > Mathf.Abs(facingDir.x))
        {
            lastDirection = facingDir.z > 0 ? 1 : 0;
        }
        else
        {
            lastDirection = 2;
        }

        if (facingDir.x > 0.1f) spriteRenderer.flipX = true;
        else if (facingDir.x < -0.1f) spriteRenderer.flipX = false;

        if (animator != null)
        {
            animator.SetBool("IsWalking", isWalking || isTurning);
            animator.SetFloat("Direction", (float)lastDirection);
        }
        // -------------------------------


        /* =========================================================
           [8-DIRECTION & 5-SPRITES SETUP] 
           KHI NÀO CÓ ĐỦ 5 ANIMATION (Dưới, Trên, Trái, Dưới-Trái, Trên-Trái):
           1. Vào Animator tạo Parameter "DirectionInt" (Type: Int).
           2. Bỏ comment đoạn code dưới đây.
           3. Comment lại đoạn code 4 hướng ở trên.
           ========================================================= */

        /*
        // 1. Tính góc 360 độ (0 là hướng Nam/Dưới, tăng dần theo chiều kim đồng hồ)
        float angle = Vector3.SignedAngle(Vector3.back, facingDir, Vector3.up);
        if (angle < 0) angle += 360;

        // 2. Chia thành 8 hướng (mỗi hướng 45 độ)
        // Cộng 22.5 để xoay trục cho khớp
        int directionIndex = Mathf.FloorToInt((angle + 22.5f) / 45f);
        if (directionIndex >= 8) directionIndex = 0; 

        // 3. Logic FlipX cho 5 Sprites (Lật các hướng bên Phải thành bên Trái)
        // Index: 0=S, 1=SW, 2=W, 3=NW, 4=N, 5=NE, 6=E, 7=SE
        
        bool shouldFlip = false;
        int animationToPlay = directionIndex;

        if (directionIndex > 4) // Các hướng bên phải (5, 6, 7)
        {
            shouldFlip = true;
            // Map ngược lại về sprite bên trái
            // 5 (NE) -> 3 (NW)
            // 6 (E)  -> 2 (W)
            // 7 (SE) -> 1 (SW)
            animationToPlay = 8 - directionIndex; 
        }

        // 4. Gửi vào Animator & SpriteRenderer
        if (animator != null)
        {
            animator.SetBool("IsWalking", isWalking || isTurning);
            animator.SetInteger("DirectionInt", animationToPlay); // Gửi index 0,1,2,3,4
        }
        
        // Luôn set FlipX theo logic đã tính toán
        spriteRenderer.flipX = shouldFlip;
        */
    }

    void FixedUpdate()
    {
        if (!isTurning && isWalking)
        {
            Vector3 targetPosition = rb.position + movementInput * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }

    void PerformAttack()
    {
        // Debug.Log("Player vung kiếm tấn công!"); (Bỏ bớt log thừa)

        // Lấy Stat của bản thân (Attacker)
        CharacterStats myStats = GetComponent<CharacterStats>();
        if (myStats == null)
        {
            Debug.LogError("Chưa gắn script CharacterStats vào Player!");
            return;
        }

        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        foreach (Collider enemy in hitEnemies)
        {
            // Lấy Stat của kẻ địch (Target)
            CharacterStats enemyStats = enemy.GetComponent<CharacterStats>();

            if (enemyStats != null)
            {
                // 1. Tính hướng (t)
                float t = CombatMath.CalculateDirectionFactor(transform, enemy.transform);

                // 2. Tính Damage (Gọi CombatMath)
                // Truyền stat của mình, stat của địch, t, và có crit hay không
                float damage = CombatMath.CalculateFullDamage(myStats, enemyStats, t, testIsCrit);

                // 3. Gây sát thương
                enemyStats.TakeDamage(damage);

                // 4. (Optional) Rung màn hình nhẹ khi đánh trúng
                if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.1f);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        Debug.Log($"Odo bị đánh! -{damage} HP");
        if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.2f);
    }

    public void SetTurnSmoothTime(float time)
    {
        turnDuration = time;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}