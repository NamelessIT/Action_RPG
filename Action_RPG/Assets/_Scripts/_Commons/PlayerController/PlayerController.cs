using UnityEngine;
using Unity.Cinemachine;
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float idleDelay = 0.25f;

    private Vector3 movement;

    // Khai báo các component cần lấy
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;


    private int lastDirection = 0;
    private bool isWalking = false;
    private float lastMoveTime = 0f;

    [Header("Effects")]
    private CinemachineImpulseSource impulseSource;

    void Start()
    {
        // QUAN TRỌNG: Dùng GetComponentInChildren để lấy component từ object con (Odo)
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Rigidbody nằm ngay trên object cha (Player) nên dùng GetComponent bình thường
        rb = GetComponent<Rigidbody>();

        // Kiểm tra xem có lấy được component không để tránh lỗi sau này
        if (animator == null) Debug.LogError("Không tìm thấy Animator ở Object con!");
        if (spriteRenderer == null) Debug.LogError("Không tìm thấy SpriteRenderer ở Object con!");

        // Lấy component Impulse Source nằm trên chính Player
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    void Update()
    {
        // Lấy input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // Debug input để kiểm tra bàn phím
        //if (moveX != 0 || moveZ != 0)
        //{
        //    Debug.Log($"Input Detected -> X: {moveX}, Z: {moveZ}");
        //}

        movement = new Vector3(moveX, 0f, moveZ).normalized;

        // Logic check di chuyển
        if (movement.magnitude > 0.1f)
        {
            isWalking = true;
            lastMoveTime = Time.time;

            if (Mathf.Abs(moveZ) > Mathf.Abs(moveX))
            {
                // 0: Front (xuống), 1: Back (lên), 2: Side (ngang)
                lastDirection = moveZ > 0 ? 1 : 0;
            }
            else
            {
                lastDirection = 2; // Side
            }

            // Xử lý FlipX (Lưu ý: spriteRenderer đang nằm ở object con)
            if (moveX > 0) spriteRenderer.flipX = true;
            else if (moveX < 0) spriteRenderer.flipX = false;
        }
        else
        {
            if (Time.time - lastMoveTime > idleDelay)
            {
                isWalking = false;
            }
        }

        // Gửi trạng thái đến Animator
        if (animator != null)
        {
            animator.SetBool("IsWalking", isWalking);
            animator.SetFloat("Direction", (float)lastDirection);

            // Debug xem giá trị gửi vào Animator là gì
             //Debug.Log($"Animator Vars -> IsWalking: {isWalking}, Direction: {lastDirection}");
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            TakeDamage(10);
        }
    }

    void FixedUpdate()
    {
        Vector3 targetPosition = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }


    // Hàm giả lập việc bị đánh (gọi hàm này khi va chạm enemy)
    public void TakeDamage(int damage)
    {
        Debug.Log($"Odo bị đánh! Mất {damage} máu.");

        if (impulseSource != null)
        {
            // CÁCH 1: Gọi đơn giản (Sử dụng đúng thông số Y=-1 bạn đã chỉnh trong Inspector)
            impulseSource.GenerateImpulseWithForce(0.1f);

            // CÁCH 2: Nếu bạn muốn đòn đánh mạnh hơn thì nhân thêm lực
            // impulseSource.GenerateImpulseWithForce(2.0f); // Mạnh gấp đôi
        }
    }
}