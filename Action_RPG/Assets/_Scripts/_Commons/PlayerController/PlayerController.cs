using UnityEngine;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    // --- KHÔNG CẦN KHAI BÁO BIẾN MOVEMENT Ở ĐÂY NỮA ---
    // Chúng ta sẽ lấy trực tiếp từ CharacterStats

    [Header("Settings")]
    public float idleDelay = 0.25f;

    // Khai báo các component
    private CharacterStats stats; // <--- THAM CHIẾU QUAN TRỌNG
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

    // Movement internal variables
    private Vector3 movementInput;
    private Vector3 currentVisualDir;

    // Testing
    public Transform testEnemyTarget;

    // Biến tạm để test Crit (Sau này có thể làm random)
    public bool testIsCrit = false;

    void Start()
    {
        // 1. Lấy Stats (Dữ liệu)
        stats = GetComponent<CharacterStats>();

        // 2. Lấy các Component khác
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
        impulseSource = GetComponent<CinemachineImpulseSource>();

        // 3. Kiểm tra lỗi
        if (stats == null) Debug.LogError("Thiếu script CharacterStats trên Player! Hãy gắn vào ngay.");
        if (animator == null) Debug.LogError("Thiếu Animator!");
        if (spriteRenderer == null) Debug.LogError("Thiếu SpriteRenderer!");

        currentVisualDir = Vector3.back;
    }

    void Update()
    {
        // Kiểm tra an toàn: Nếu không có stats thì không làm gì cả để tránh crash game
        if (stats == null) return;

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

            // --- THAY ĐỔI: Lấy turnDuration từ stats ---
            float rotSpeed = Mathf.PI / stats.turnDuration;

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

            // --- THAY ĐỔI: Lấy moveThresholdAngle từ stats ---
            // Kiểm tra góc lệch để quyết định đi hay dừng
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
        // --- CODE HIỆN TẠI (4 HƯỚNG) ---
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

        // [CODE 8 HƯỚNG BẠN ĐỂ COMMENT Ở ĐÂY - GIỮ NGUYÊN NHƯ CŨ]
        /* ... */
    }

    void FixedUpdate()
    {
        if (stats == null) return;

        if (!isTurning && isWalking)
        {
            // --- THAY ĐỔI: Lấy moveSpeed từ stats ---
            Vector3 targetPosition = rb.position + movementInput * stats.moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }

    void PerformAttack()
    {
        // --- TỐI ƯU: Dùng biến stats đã cache, không cần GetComponent lại ---
        if (stats == null) return;

        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
        foreach (Collider enemy in hitEnemies)
        {
            // Lấy Stat của kẻ địch (Target)
            CharacterStats enemyStats = enemy.GetComponent<CharacterStats>();

            if (enemyStats != null)
            {
                // 1. Tính hướng (t)
                float t = CombatMath.CalculateDirectionFactor(transform, enemy.transform);

                // 2. Tính Damage
                // Truyền stats (của mình) và enemyStats (của địch)
                float damage = CombatMath.CalculateFullDamage(stats, enemyStats, t, testIsCrit);

                // 3. Gây sát thương
                enemyStats.TakeDamage(damage);

                // 4. Rung màn hình
                if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.1f);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        // Hàm này chỉ để test hiệu ứng rung, việc trừ máu thực tế nên gọi vào CharacterStats.TakeDamage
        Debug.Log($"Odo bị đánh trúng (Test Effect)!");
        if (impulseSource != null) impulseSource.GenerateImpulseWithForce(0.2f);

        // Nếu muốn đồng bộ trừ máu thật:
        // if(stats != null) stats.TakeDamage(damage);
    }

    // Hàm này cập nhật vào Stats thay vì biến local
    public void SetTurnSmoothTime(float time)
    {
        if (stats != null) stats.turnDuration = time;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}