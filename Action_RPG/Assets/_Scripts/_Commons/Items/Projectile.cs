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

    // ── WPN_BW_T5_02: Chim Ánh Trăng (homing) ───────────────────────
    private bool homing = false;
    private LayerMask homingLayer;
    private float homingLife = 0f;
    private const float HOMING_TURN_SPEED = 720f;   // độ/giây — bẻ cong mọi góc
    private const float HOMING_SEARCH_RADIUS = 15f;  // tầm dò mục tiêu
    private const float HOMING_MAX_LIFE = 5f;        // tự hủy sau 5s nếu không trúng

    /// <summary>Bật chế độ tự bẻ cong đuổi kẻ địch gần nhất (WPN_BW_T5_02).</summary>
    public void EnableHoming(LayerMask enemyLayer)
    {
        homing = true;
        homingLayer = enemyLayer;
    }

    // WPN_GR_T4_04: đạn Phi Dao — đòn chính tính vật lý + bonus phép (xử lý trong ApplyDamageToTarget).
    public bool grimoirePhiDao = false;

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
        // WPN_BW_T5_02: bẻ cong hướng bay về kẻ địch gần nhất trước khi di chuyển.
        if (homing)
        {
            Transform tgt = FindNearestEnemy();
            if (tgt != null)
            {
                Vector3 desired = (tgt.position - transform.position).normalized;
                moveDirection = Vector3.RotateTowards(
                    moveDirection, desired,
                    HOMING_TURN_SPEED * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }
        }

        // Tự di chuyển viên đạn mỗi frame
        transform.position += moveDirection * speed * Time.deltaTime;

        // Homing: không giới hạn tầm, chỉ tự hủy sau HOMING_MAX_LIFE.
        if (homing)
        {
            homingLife += Time.deltaTime;
            if (homingLife >= HOMING_MAX_LIFE) Destroy(gameObject);
            return;
        }

        // Tự hủy nếu bay quá tầm đánh (Max Range)
        if (Vector3.Distance(startPos, transform.position) >= maxRange)
        {
            // Debug.Log("[Projectile] Đạn đã bay hết tầm tối đa, tự hủy.");
            Destroy(gameObject);
        }
    }

    private Transform FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, HOMING_SEARCH_RADIUS, homingLayer);
        Transform best = null;
        float min = float.MaxValue;
        foreach (var h in hits)
        {
            Stats s = h.GetComponentInParent<Stats>();
            if (s == null || s.currentHp <= 0 || !s.gameObject.CompareTag("Enemy")) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < min) { min = d; best = s.transform; }
        }
        return best;
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

                owner.ApplyDamageToTarget(enemyStats, isHeavy, stepIndex, false, grimoirePhiDao);

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