using UnityEngine;

// Vẫn cần Rigidbody để OnTriggerEnter hoạt động đúng với quái
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    public float speed = 15f;

    private float maxRange;
    private Vector3 startPos;
    private Vector3 moveDirection; // [MỚI] Lưu hướng bay

    private PlayerController owner;
    private bool isHeavy;
    private int stepIndex;
    private bool hasHit = false;

    /// <summary>
    /// Nếu true: đạn chỉ là visual, KHÔNG gây damage khi trigger.
    /// Dùng cho BowHeavyAttack — damage đã được tính qua SphereCastAll.
    /// </summary>
    public bool visualOnly = false;

    public void Setup(PlayerController _owner, Vector3 dir, float _maxRange, bool _isHeavy, int _stepIndex)
    {
        owner = _owner;
        maxRange = _maxRange;
        isHeavy = _isHeavy;
        stepIndex = _stepIndex;
        startPos = transform.position;

        moveDirection = dir.normalized; // Lưu lại hướng bay

        // Quay đầu viên đạn về hướng bay
        transform.rotation = Quaternion.LookRotation(moveDirection);

        // Đảm bảo Rigidbody là Kinematic để không rớt, và KHÔNG gán velocity nữa
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        // Tự di chuyển viên đạn mỗi frame
        transform.position += moveDirection * speed * Time.deltaTime;

        // Tự hủy nếu bay quá tầm đánh (Max Range)
        if (Vector3.Distance(startPos, transform.position) >= maxRange)
        {
            // Debug.Log("[Projectile] Đạn đã bay hết tầm tối đa, tự hủy.");
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (visualOnly) return; // Chỉ visual, không gây damage

        // --- DEBUG TEST ---
        // In ra mọi thứ mà đạn chạm vào để bạn dễ theo dõi
        Debug.Log($"[Projectile Test] Đạn vừa chạm vào: <color=yellow>{other.gameObject.name}</color> | Tag: <color=cyan>{other.tag}</color> | Layer: <color=green>{LayerMask.LayerToName(other.gameObject.layer)}</color>");

        // 1. Nếu trúng quái
        if (other.CompareTag("Enemy"))
        {
            Stats enemyStats = other.GetComponent<Stats>();
            if (enemyStats != null && owner != null)
            {
                hasHit = true;
                Debug.Log($"<color=red>[Projectile] Trúng kẻ địch {other.name}! Gây damage.</color>");

                owner.ApplyDamageToTarget(enemyStats, isHeavy, stepIndex);

                AllyStats allyStats = owner.GetComponent<AllyStats>();
                if (allyStats != null) allyStats.GainSinFromAttack(1);

                Destroy(gameObject);
            }
        }
        // 2. Nếu trúng mặt đất (Layer Ground)
        else if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Debug.Log("[Projectile] Cắm xuống đất (Ground). Hủy đạn.");
            Destroy(gameObject);
        }
        // 3. Nếu trúng chướng ngại vật / Tường 
        // Dùng .tag thay vì CompareTag để tránh lỗi crash nếu chưa khai báo tag trong Inspector
        else if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle") || other.gameObject.tag == "Obstacle")
        {
            // CM3 Mage: đạn xuyên tường
            AllyStats ownerAlly = owner != null ? owner.GetComponent<AllyStats>() : null;
            if (ownerAlly != null && ownerAlly.mageCM3_ProjectilePhaseWalls) return;

            Debug.Log("[Projectile] Đập vào chướng ngại vật (Obstacle). Hủy đạn.");
            Destroy(gameObject);
        }
    }
}