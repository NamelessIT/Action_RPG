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

    [Header("--- Ambient Behavior (Patrol & Look) ---")]
    [Tooltip("Cho phép đi tuần tra xung quanh điểm Spawn")]
    public bool enablePatrol = false;
    [Tooltip("Bán kính tuần tra")]
    public float patrolRadius = 5.0f;
    [Tooltip("Thời gian đứng nghỉ giữa các lần đi tuần")]
    public float patrolWaitTime = 3.0f;

    [Space(10)]
    [Tooltip("Bật tắt hành vi nhìn quanh khi đứng yên (Lắc đầu)")]
    public bool enableLookAround = true;
    [Tooltip("Góc quét (Độ). VD: 45 độ trái phải")]
    public float lookAngle = 45f;
    [Tooltip("Tốc độ quay đầu")]
    public float lookSpeed = 2.0f;

    [Space(10)]
    [Header("--- Ambient Behavior (Random Turn) ---")]
    [Tooltip("Cho phép tự đổi hướng đứng khi đang Idle (Dành cho lính gác)")]
    public bool enableRandomTurn = true;
    [Tooltip("Thời gian tối thiểu giữa các lần đổi hướng (nên > lookSpeed)")]
    public float randomTurnMinTime = 10.0f;
    [Tooltip("Thời gian tối đa giữa các lần đổi hướng")]
    public float randomTurnMaxTime = 20.0f;

    // Biến nội bộ
    private Vector3 baseIdleDirection;
    private float lookTimer;
    private float currentPatrolTimer;
    private bool isPatrolWaiting = false;

    // [MỚI] Timer cho Random Turn
    private float nextTurnTimer;

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

        combat.Setup(stats, playerTarget, animator);

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.baseMoveSpeed;

        if (stats.enemyType == EnemyType.Hostile && playerTarget != null)
        {
            Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;
            stats.facingDirection = dirToPlayer;
        }
        else
        {
            stats.facingDirection = Vector3.back;
        }

        baseIdleDirection = stats.facingDirection;

        // [MỚI] Khởi tạo timer quay ngẫu nhiên
        nextTurnTimer = Random.Range(randomTurnMinTime, randomTurnMaxTime);

        HandleAnimation(stats.facingDirection);
    }

    void Update()
    {
        if (playerTarget == null) return;

        if (combat.isAttacking)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            stats.EnterCombat();
            return;
        }

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
            stats.outCombat = true;
            agent.isStopped = false;
        }

        // --- 2. XỬ LÝ STATE ---
        if (isReturningHome)
        {
            if (stats.outCombat == false && currentState == "Returning")
            {
                stats.outCombat = true;
            }
            HandleReturningState(distToSpawn, distToPlayer);
        }
        else
        {
            // Nếu có Aggro -> Chiến đấu / Đuổi
            if (stats.currentAggro > 0)
            {
                HandleCombatBehavior(distToPlayer);
            }
            // Nếu không có Aggro -> Idle hoặc Tuần tra
            else
            {
                if (stats.enemyType == EnemyType.Hostile && canSensePlayer)
                {
                    if (distToSpawn <= stats.aggroRadius)
                    {
                        stats.AddAggro(stats.maxAggro);
                    }
                    else
                    {
                        HandleIdleOrPatrol();
                    }
                }
                else
                {
                    if (distToSpawn > patrolRadius * 1.5f)
                    {
                        State_MoveTo(stats.spawnPosition, "Returning Idle");
                    }
                    else
                    {
                        HandleIdleOrPatrol();
                    }
                }
            }
        }

        HandleVisuals();
        combat.HandleCombatUpdate();
    }

    // [MỚI] Hàm xử lý quay ngẫu nhiên hướng đứng
    void HandleRandomTurn()
    {
        nextTurnTimer -= Time.deltaTime;

        if (nextTurnTimer <= 0)
        {
            // Chọn một góc ngẫu nhiên (360 độ)
            float randomAngle = Random.Range(0f, 360f);

            // Tính vector hướng mới từ góc đó
            // (Giả sử trục Y là trục đứng, xoay quanh Y)
            Quaternion rot = Quaternion.Euler(0, randomAngle, 0);
            Vector3 newDir = rot * Vector3.forward; // Hoặc Vector3.right tùy trục gốc

            // Cập nhật hướng gốc (trục xoay)
            baseIdleDirection = newDir.normalized;

            // Nếu KHÔNG bật LookAround, ta phải cập nhật facingDirection ngay lập tức
            // Nếu bật LookAround, hàm HandleLookAround sẽ tự lo việc xoay theo baseIdleDirection
            if (!enableLookAround)
            {
                stats.facingDirection = baseIdleDirection;
            }
            else
            {
                // Nếu đang lắc đầu, reset LookTimer để nó bắt đầu lắc từ trung tâm hướng mới
                // giúp chuyển động mượt hơn, không bị giật cục
                lookTimer = 0f;
            }

            // Reset timer cho lần quay tiếp theo
            nextTurnTimer = Random.Range(randomTurnMinTime, randomTurnMaxTime);
            // Debug.Log("Enemy đổi hướng đứng!");
        }
    }

    // --- CÁC HÀM XỬ LÝ LOGIC ---

    void HandleCombatBehavior(float distToPlayer)
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

    void HandleIdleOrPatrol()
    {
        if (enablePatrol)
        {
            if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
            {
                currentState = "Patrolling (Waiting)";
                if (agent.isOnNavMesh) agent.isStopped = true;

                if (!isPatrolWaiting)
                {
                    isPatrolWaiting = true;
                    currentPatrolTimer = patrolWaitTime;
                    lookTimer = 0f;

                    if (stats.facingDirection != Vector3.zero)
                        baseIdleDirection = stats.facingDirection;
                }

                currentPatrolTimer -= Time.deltaTime;

                // Khi đứng chờ ở điểm tuần tra, cũng có thể áp dụng LookAround
                if (enableLookAround) HandleLookAround();

                // Tùy chọn: Bạn có muốn nó tự quay hướng ngẫu nhiên trong lúc chờ Patrol không?
                // Nếu muốn thì bỏ comment dòng dưới:
                // if (enableRandomTurn) HandleRandomTurn(); 

                if (currentPatrolTimer <= 0)
                {
                    Vector3 nextPos = GetRandomPatrolPoint();
                    State_MoveTo(nextPos, "Patrolling (Moving)");
                    isPatrolWaiting = false;
                }
            }
            else
            {
                currentState = "Patrolling (Moving)";
                isPatrolWaiting = false;

                if (agent.velocity.sqrMagnitude > 0.1f)
                    baseIdleDirection = agent.velocity.normalized;
            }
        }
        else
        {
            // Logic cho Enemy đứng yên (không đi tuần)
            State_Idle();
        }
    }

    void State_Idle()
    {
        currentState = "Idle";
        if (agent.isOnNavMesh) agent.isStopped = true;

        if (stats.facingDirection != Vector3.zero && baseIdleDirection == Vector3.zero)
            baseIdleDirection = stats.facingDirection;

        // [MỚI] Gọi hàm xử lý quay ngẫu nhiên
        if (enableRandomTurn)
        {
            HandleRandomTurn();
        }

        // Gọi hàm nhìn quanh (lắc lư)
        if (enableLookAround)
        {
            HandleLookAround();
        }
    }

    // Các hàm khác giữ nguyên (HandleLookAround, GetRandomPatrolPoint, HandleReturningState...)
    // Copy y nguyên phần dưới của file cũ

    Vector3 GetRandomPatrolPoint()
    {
        Vector3 randomPoint = stats.spawnPosition + Random.insideUnitSphere * patrolRadius;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, 2.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return stats.spawnPosition;
    }

    void HandleReturningState(float distToSpawn, float distToPlayer)
    {
        bool gotHit = stats.currentAggro > 0;
        bool blockedByPlayer = distToPlayer <= combat.basicAttackRange;
        bool shouldFightBack = false;

        if (stats.enemyType == EnemyType.Hostile)
        {
            if (gotHit || blockedByPlayer)
            {
                stats.outCombat = false;
                shouldFightBack = true;
            }
        }
        else if (stats.enemyType == EnemyType.Neutral)
        {
            if (gotHit)
            {
                shouldFightBack = true;
                stats.outCombat = false;
            }
            else if (blockedByPlayer && distToSpawn > 3.0f)
            {
                shouldFightBack = true;
                stats.outCombat = false;
            }
        }

        if (shouldFightBack)
        {
            Debug.Log("Đang về thì bị khiêu khích -> Chiến tiếp!");
            isReturningHome = false;
            stats.outCombat = false;
            if (stats.currentAggro <= 0) stats.AddAggro(50f);
            stats.EnterCombat();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            isReturningHome = false;
            stats.currentAggro = 0;
            stats.outCombat = true;
            baseIdleDirection = stats.facingDirection;
            ResetPatrolState();
        }
        else
        {
            State_MoveTo(stats.spawnPosition, "Returning");
        }
    }

    void ResetPatrolState()
    {
        isPatrolWaiting = false;
        currentPatrolTimer = 0f;
        if (agent.isOnNavMesh) agent.ResetPath();
        State_Idle();
    }

    void State_MoveTo(Vector3 targetPos, string debugState)
    {
        if (!agent.isOnNavMesh) return;
        currentState = debugState;
        agent.isStopped = false;
        agent.SetDestination(targetPos);
    }

    void HandleLookAround()
    {
        lookTimer += Time.deltaTime * lookSpeed;
        float currentAngle = Mathf.Sin(lookTimer) * lookAngle;
        Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.up);
        Vector3 newDir = rotation * baseIdleDirection;
        if (newDir != Vector3.zero)
            stats.facingDirection = newDir.normalized;
    }

    void State_Chase()
    {
        State_MoveTo(playerTarget.position, "Chasing");
        stats.EnterCombat();
    }

    void State_Attack()
    {
        currentState = "Attacking";
        if (agent.isOnNavMesh) agent.isStopped = true;
        stats.EnterCombat();
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
        if (agent.velocity.magnitude > 0.1f)
        {
            currentDir = agent.velocity.normalized;
            lookTimer = 0f;
        }
        else if (currentState == "Attacking" || currentState == "Chasing")
        {
            currentDir = (playerTarget.position - transform.position).normalized;
        }
        else
        {
            currentDir = stats.facingDirection;
        }

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
        if (Mathf.Abs(dir.z) > Mathf.Abs(dir.x)) dirIndex = dir.z > 0 ? 4 : 0;
        else dirIndex = 2;
        animator.SetFloat("Direction", (float)dirIndex);
        if (dir.x > 0) spriteRenderer.flipX = true;
        else if (dir.x < 0) spriteRenderer.flipX = false;
    }

    bool CheckDetection()
    {
        if (playerTarget == null) return false;
        float playerStealth = 1.0f;
        Stats playerStats = playerTarget.GetComponent<Stats>();
        if (playerStats != null) playerStealth = playerStats.stealthFactor;
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float effectiveRadius = stats.detectionRadius * playerStealth;
        if ((stats.detectionMethod & DetectionMethod.Range) != 0)
        {
            if (distToPlayer <= effectiveRadius) return true;
        }
        if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
        {
            float effectiveViewDist = stats.viewDistance * playerStealth;
            float effectiveAngle = stats.viewAngle;
            if (distToPlayer <= effectiveViewDist)
            {
                Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;
                Vector3 facingDir = stats.facingDirection;
                if (facingDir == Vector3.zero) facingDir = transform.forward;
                float angleToPlayer = Vector3.Angle(facingDir, dirToPlayer);
                if (angleToPlayer < effectiveAngle / 2f)
                {
                    if (!Physics.Raycast(transform.position, dirToPlayer, distToPlayer, stats.obstacleMask)) return true;
                }
            }
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (stats != null)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);
            if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Vector3 forward = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
                Quaternion leftRayRotation = Quaternion.AngleAxis(-stats.viewAngle / 2, Vector3.up);
                Quaternion rightRayRotation = Quaternion.AngleAxis(stats.viewAngle / 2, Vector3.up);
                Vector3 leftRay = leftRayRotation * forward;
                Vector3 rightRay = rightRayRotation * forward;
                Gizmos.DrawRay(transform.position, leftRay * stats.viewDistance);
                Gizmos.DrawRay(transform.position, rightRay * stats.viewDistance);
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, forward * stats.viewDistance);
            }
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, stats.aggroRadius);
            if (enablePatrol)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(stats.spawnPosition, patrolRadius);
            }
        }
    }
}