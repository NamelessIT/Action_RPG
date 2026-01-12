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
            stats.currentAggro = 0;
            stats.outCombat = true; // Ép buộc thoát combat để tránh lỗi lặp
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

    // [QUAN TRỌNG] HÀM ĐÃ ĐƯỢC SỬA LỖI LOGIC NEUTRAL TỰ ĐÁNH
    void HandleReturningState(float distToSpawn, float distToPlayer)
    {
        // 1. Kiểm tra các điều kiện để HỦY quay về và ĐÁNH LẠI

        bool gotHit = stats.currentAggro > 0;
        bool blockedByPlayer = distToPlayer <= combat.basicAttackRange;

        bool shouldFightBack = false;

        if (stats.enemyType == EnemyType.Hostile)
        {
            // Hostile: Hung hăng mọi lúc mọi nơi
            // Bị đánh HOẶC Bị chặn đường -> Chiến luôn
            if (gotHit || blockedByPlayer) shouldFightBack = true;
        }
        else if (stats.enemyType == EnemyType.Neutral)
        {
            // Neutral: Hiền lành nhưng không nhu nhược
            // 1. Nếu BỊ ĐÁNH (gotHit) -> Chắc chắn đánh lại.
            // 2. Nếu BỊ CHẶN ĐƯỜNG (blockedByPlayer):
            //    - Chỉ đánh lại khi CÒN Ở XA NHÀ (distToSpawn > 3.0f).
            //    - Nếu đã về gần nhà (<= 3.0f) -> Bỏ qua việc bị chặn, cố đi nốt về chỗ ngủ để reset.

            if (gotHit)
            {
                shouldFightBack = true;
            }
            else if (blockedByPlayer && distToSpawn > 3.0f)
            {
                shouldFightBack = true;
            }
        }

        // Thực hiện hành động nếu quyết định đánh lại
        if (shouldFightBack)
        {
            Debug.Log("Đang về thì bị khiêu khích -> Chiến tiếp!");
            isReturningHome = false;

            // Nếu chưa có Aggro (trường hợp bị chặn đường), buff lên để đánh
            if (stats.currentAggro <= 0) stats.AddAggro(50f);

            stats.EnterCombat();
            return;
        }

        // 2. Logic di chuyển về (Nếu không đánh nhau)
        if (distToSpawn < 1.0f)
        {
            isReturningHome = false;
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
            if (stats.enemyType == EnemyType.Hostile && canSensePlayer)
            {
                // Kiểm tra lại: Hostile chỉ tự đánh khi Player ở trong vùng hoạt động
                // Để tránh việc vừa về nhà xong, thấy Player ở tít xa (ngoài vùng) lại chạy ra
                float distToSpawn = Vector3.Distance(transform.position, stats.spawnPosition);
                if (distToSpawn <= stats.aggroRadius)
                {
                    stats.AddAggro(stats.maxAggro);
                }
                else
                {
                    State_Idle(); // Ở nhà, thấy Player nhưng Player ở ngoài vùng -> Kệ
                }
            }
            else
            {
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