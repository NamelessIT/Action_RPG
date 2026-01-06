using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float idleDelay = 0.25f;
    private Vector2 movement;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private int lastDirection = 0; // Lưu hướng cuối cùng

    private bool isWalking = false;
    private float lastMoveTime = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");


        if (movement.magnitude > 0.1f)
        {
            isWalking = true;
            lastMoveTime = Time.time;

            //Update last direction
            if (Mathf.Abs(movement.y) > Mathf.Abs(movement.x))
            {
                lastDirection = movement.y > 0 ? 1 : 0;
            }
            else
            {
                lastDirection = 2;
            }
        }

        else
        {
            if (Time.time - lastMoveTime > idleDelay)
            {
                isWalking = false;
            }

        }

        // Gửi trạng thái đến Animator
        animator.SetBool("IsWalking", isWalking);
        animator.SetInteger("Direction", lastDirection);
        Debug.Log($"Sending → IsWalking: {isWalking}, Direction: {lastDirection}");
        // Xử lý flipX
        if (movement.x > 0)
        {
            spriteRenderer.flipX = true;
        }
        else if (movement.x < 0)
        {
            spriteRenderer.flipX = false;
        }
    }
    void FixedUpdate()
    {
        GetComponent<Rigidbody2D>().MovePosition(
            (Vector2)transform.position + movement * moveSpeed * Time.fixedDeltaTime
        );
    }
}