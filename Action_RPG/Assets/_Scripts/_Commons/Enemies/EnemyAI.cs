using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyCombat))]
public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTarget;
    private NavMeshAgent agent;
    private EnemyStats stats;
    private EnemyCombat combat;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    [Header("State Debug")]
    public string currentState = "Idle";
    public bool isReturningHome = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        combat = GetComponent<EnemyCombat>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTarget = p.transform;
        }

        combat.Setup(stats, playerTarget);

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.moveSpeed;

        if (stats.enemyType == EnemyType.Hostile && playerTarget != null)
        {
            Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;
            stats.facingDirection = dirToPlayer;
        }
        else
        {
            stats.facingDirection = Vector3.back;
        }
        HandleAnimation(stats.facingDirection);
    }

    void Update()
    {
        if (playerTarget == null) return;

        bool canSensePlayer = CheckDetection();
        float distToSpawn = Vector3.Distance(transform.position, stats.spawnPosition);
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // --- 1. KIỂM TRA ĐIỀU KIỆN QUAY VỀ ---

        bool shouldReturn = !isReturningHome
                            && distToSpawn > (stats.aggroRadius * 1.5f)
                            && stats.enemyType != EnemyType.Friendly;

        if (shouldReturn)
        {
            Debug.Log("Quá xa nhà -> Bắt đầu quay về!");
            isReturningHome = true;
            stats.currentAggro = 0; // Xóa hận thù
            // Không cần set stats.outCombat = true ở đây nữa, ta dùng logic Aggro để check
        }

        // --- 2. XỬ LÝ STATE ---

        if (isReturningHome)
        {
            HandleReturningState(distToSpawn, distToPlayer);
        }
        else
        {
            HandleNormalBehavior(canSensePlayer, distToPlayer);
        }

        HandleVisuals();
        combat.HandleCombatUpdate();
    }

    // [CẬP NHẬT QUAN TRỌNG] Sửa logic bị kẹt
    void HandleReturningState(float distToSpawn, float distToPlayer)
    {
        // Điều kiện hủy quay về:
        // 1. Có Hận thù (Nghĩa là vừa bị đánh -> TakeDamage -> AddAggro > 0)
        // 2. HOẶC Player đứng ngay trong tầm đánh (chặn đường)
        bool shouldFightBack = stats.currentAggro > 0 || distToPlayer <= combat.basicAttackRange;

        if (shouldFightBack && stats.enemyType != EnemyType.Friendly)
        {
            Debug.Log("Đang về thì bị đánh/gặp địch -> Chiến tiếp!");
            isReturningHome = false;

            // Nếu chưa có Aggro (trường hợp gặp địch chặn đường), buff Aggro lên
            if (stats.currentAggro <= 0) stats.AddAggro(50f);

            stats.EnterCombat();
            return;
        }

        // Logic di chuyển về
        if (distToSpawn < 1.0f)
        {
            isReturningHome = false; // Đã về đến nơi
            stats.currentAggro = 0;
            State_Idle();
        }
        else
        {
            State_MoveTo(stats.spawnPosition, "Returning");
        }
    }

    void HandleNormalBehavior(bool canSensePlayer, float distToPlayer)
    {
        if (stats.currentAggro > 0)
        {
            if (stats.enemyType == EnemyType.Friendly)
            {
                State_Flee();
            }
            else
            {
                if (distToPlayer <= combat.basicAttackRange)
                {
                    State_Attack();
                }
                else
                {
                    State_Chase();
                }
            }
        }
        else
        {
            // Logic tự kích hoạt Aggro cho Hostile khi thấy Player (kể cả khi vừa về nhà xong)
            if (stats.enemyType == EnemyType.Hostile && canSensePlayer)
            {
                // Chỉ kích hoạt nếu Player nằm trong vùng hoạt động (Aggro Radius)
                // Để tránh việc vừa về đến nhà, thấy Player ở xa tít (ngoài vùng) lại chạy ra đuổi tiếp
                float distToSpawn = Vector3.Distance(transform.position, stats.spawnPosition);
                if (distToSpawn <= stats.aggroRadius)
                {
                    stats.AddAggro(stats.maxAggro);
                }
                else
                {
                    State_Idle(); // Ở nhà nhưng Player ở ngoài vùng -> Kệ
                }
            }
            else
            {
                // Logic lang thang về (cho Neutral/Friendly)
                float distToSpawn = Vector3.Distance(transform.position, stats.spawnPosition);
                if (distToSpawn > 1.0f) State_MoveTo(stats.spawnPosition, "Returning Idle");
                else State_Idle();
            }
        }
    }

    // --- CÁC HÀM HÀNH ĐỘNG ---

    void State_MoveTo(Vector3 targetPos, string debugState)
    {
        if (!agent.isOnNavMesh) return;
        currentState = debugState;
        agent.isStopped = false;
        agent.SetDestination(targetPos);
    }

    void State_Idle()
    {
        currentState = "Idle";
        if (agent.isOnNavMesh) agent.isStopped = true;
    }

    void State_Chase()
    {
        State_MoveTo(playerTarget.position, "Chasing");
    }

    void State_Attack()
    {
        currentState = "Attacking";
        if (agent.isOnNavMesh) agent.isStopped = true;
        combat.PerformBasicAttack();
    }

    void State_Flee()
    {
        currentState = "Fleeing";
        Vector3 dirToPlayer = transform.position - playerTarget.position;
        Vector3 fleePos = transform.position + dirToPlayer.normalized * 5f;

        float distFleeToSpawn = Vector3.Distance(fleePos, stats.spawnPosition);
        if (distFleeToSpawn > stats.aggroRadius)
        {
            Vector3 dirFromSpawn = (fleePos - stats.spawnPosition).normalized;
            fleePos = stats.spawnPosition + dirFromSpawn * stats.aggroRadius;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(fleePos, out hit, 5.0f, NavMesh.AllAreas))
        {
            State_MoveTo(hit.position, "Fleeing");
        }
        else
        {
            State_MoveTo(stats.spawnPosition, "Fleeing Home");
        }
    }

    void HandleVisuals()
    {
        Vector3 currentDir = Vector3.zero;
        if (agent.velocity.magnitude > 0.1f) currentDir = agent.velocity.normalized;
        else if (currentState == "Attacking" || currentState == "Chasing")
            currentDir = (playerTarget.position - transform.position).normalized;
        else currentDir = stats.facingDirection;

        if (currentDir != Vector3.zero)
        {
            stats.facingDirection = currentDir;
            HandleAnimation(currentDir);
        }
    }

    void HandleAnimation(Vector3 dir)
    {
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("IsWalking", isMoving);
        int dirIndex = 0;
        if (Mathf.Abs(dir.z) > Mathf.Abs(dir.x)) dirIndex = dir.z > 0 ? 1 : 0;
        else dirIndex = 2;
        animator.SetFloat("Direction", (float)dirIndex);
        if (dir.x > 0) spriteRenderer.flipX = true;
        else if (dir.x < 0) spriteRenderer.flipX = false;
    }

    bool CheckDetection()
    {
        float dist = Vector3.Distance(transform.position, playerTarget.position);
        return dist <= stats.detectionRange;
    }

    void OnDrawGizmosSelected()
    {
        if (stats != null)
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, stats.detectionRange);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, stats.aggroRadius);
            Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, stats.aggroRadius * 1.5f);
        }
    }
}