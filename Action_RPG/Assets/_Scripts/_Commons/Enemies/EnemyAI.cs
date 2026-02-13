using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyCombat))]
public class EnemyAI : MonoBehaviour
{
    [Header("Targeting System")]
    public Transform nearestTarget; // Mục tiêu tấn công
    private Transform fleeTarget;   // Mục tiêu cần né (cho Friendly)

    [Header("Scanning")]
    public float scanInterval = 0.5f;
    private float nextScanTime;

    // References
    private NavMeshAgent agent;
    private EnemyStats stats;
    private EnemyCombat combat;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    [Header("State Debug")]
    public string currentState = "Idle";
    public bool isReturningHome = false;

    [Header("--- Ambient Behavior ---")]
    public bool enablePatrol = false;
    public float patrolRadius = 5.0f;
    public float patrolWaitTime = 3.0f;

    [Space(5)]
    public bool enableLookAround = true;
    public float lookAngle = 45f;
    public float lookSpeed = 2.0f;

    [Space(5)]
    public bool enableRandomTurn = true;
    public float randomTurnMinTime = 10.0f;
    public float randomTurnMaxTime = 20.0f;

    // Internal Vars
    private Vector3 baseIdleDirection;
    private float lookTimer;
    private float currentPatrolTimer;
    private bool isPatrolWaiting = false;
    private float nextTurnTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        combat = GetComponent<EnemyCombat>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        combat.Setup(stats, null, animator);

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.baseMoveSpeed;

        if (stats.facingDirection == Vector3.zero) stats.facingDirection = Vector3.back;
        baseIdleDirection = stats.facingDirection;
        nextTurnTimer = Random.Range(randomTurnMinTime, randomTurnMaxTime);

        HandleAnimation(stats.facingDirection);
    }

    void Update()
    {
        if (stats.isDead || stats.isStunned)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            return;
        }

        // 1. QUÉT TÌM MỤC TIÊU
        if (Time.time >= nextScanTime)
        {
            ScanForTarget();
            nextScanTime = Time.time + scanInterval;
        }

        // Cập nhật target cho Combat
        combat.SetTarget(nearestTarget);

        // 2. NẾU ĐANG ĐÁNH -> KHÓA DI CHUYỂN
        if (combat.isAttacking)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            stats.EnterCombat();
            return;
        }

        // 3. LOGIC TRẠNG THÁI (STATE MACHINE)
        float distToSpawn = Vector3.Distance(transform.position, stats.spawnPosition);
        float distToTarget = (nearestTarget != null) ? Vector3.Distance(transform.position, nearestTarget.position) : 9999f;

        // Điều kiện quay về: Đi quá xa và không phải là Friendly
        bool shouldReturn = !isReturningHome
                            && distToSpawn > (stats.aggroRadius * 1.5f)
                            && stats.enemyType != EnemyType.Friendly;

        if (shouldReturn)
        {
            isReturningHome = true;
            stats.currentAggro = 0;
            stats.outCombat = true;
            agent.isStopped = false;
            nearestTarget = null;
        }

        if (isReturningHome)
        {
            if (stats.outCombat == false && currentState == "Returning") stats.outCombat = true;
            HandleReturningState(distToSpawn, distToTarget);
        }
        else
        {
            // Ưu tiên 1: Bỏ chạy (Flee)
            if (fleeTarget != null)
            {
                HandleFleeBehavior();
            }
            // Ưu tiên 2: Chiến đấu / Truy đuổi
            else if (nearestTarget != null)
            {
                HandleCombatBehavior(distToTarget);
            }
            // Ưu tiên 3: Idle / Patrol
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

        HandleVisuals();
        combat.HandleCombatUpdate();
    }

    void ScanForTarget()
    {
        // Giữ target cũ nếu còn hợp lệ
        if (nearestTarget != null)
        {
            if (IsValidTarget(nearestTarget))
            {
                float d = Vector3.Distance(transform.position, nearestTarget.position);
                if (d <= stats.aggroRadius * 1.2f) return;
            }
            else
            {
                nearestTarget = null;
            }
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, stats.detectionRadius);
        Transform bestTarget = null;
        Transform bestPredator = null;
        float closeDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            Transform t = hit.transform;
            if (t == transform) continue;

            // Check kẻ thù để bỏ chạy (Cho Friendly)
            if (stats.IsScaredOf(t))
            {
                bestPredator = t;
            }

            // Check mục tiêu để tấn công
            if (stats.IsHostileTo(t))
            {
                if (CanSeeTarget(t))
                {
                    float d = Vector3.Distance(transform.position, t.position);
                    if (d < closeDist)
                    {
                        closeDist = d;
                        bestTarget = t;
                    }
                }
            }
        }

        fleeTarget = bestPredator;
        nearestTarget = bestTarget;

        // Hostile tự tăng Aggro khi thấy mồi ở gần nhà
        if (nearestTarget != null && stats.enemyType == EnemyType.Hostile)
        {
            float distToSpawn = Vector3.Distance(transform.position, stats.spawnPosition);
            if (distToSpawn <= stats.aggroRadius)
            {
                stats.AddAggro(stats.maxAggro);
            }
        }
    }

    bool IsValidTarget(Transform t)
    {
        if (t == null) return false;
        Stats tStats = t.GetComponent<Stats>();
        if (tStats == null || tStats.isDead) return false;
        return stats.IsHostileTo(t);
    }

    bool CanSeeTarget(Transform target)
    {
        float dist = Vector3.Distance(transform.position, target.position);
        float stealthFactor = 1.0f;
        Stats tStats = target.GetComponent<Stats>();
        if (tStats != null) stealthFactor = tStats.stealthFactor;

        float effectiveRadius = stats.detectionRadius * stealthFactor;

        if ((stats.detectionMethod & DetectionMethod.Range) != 0)
        {
            if (dist <= effectiveRadius) return true;
        }

        if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
        {
            float effectiveViewDist = stats.viewDistance * stealthFactor;
            if (dist <= effectiveViewDist)
            {
                Vector3 dirToTarget = (target.position - transform.position).normalized;
                Vector3 facingDir = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;

                float angle = Vector3.Angle(facingDir, dirToTarget);
                if (angle < stats.viewAngle / 2f)
                {
                    if (!Physics.Raycast(transform.position, dirToTarget, dist, stats.obstacleMask))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // --- BEHAVIOR ---

    void HandleCombatBehavior(float distToTarget)
    {
        if (distToTarget <= combat.basicAttackRange) State_Attack();
        else State_Chase();
    }

    void HandleFleeBehavior()
    {
        currentState = "Fleeing";
        Vector3 dirAway = (transform.position - fleeTarget.position).normalized;
        Vector3 runTo = transform.position + dirAway * 5.0f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(runTo, out hit, 5.0f, NavMesh.AllAreas)) State_MoveTo(hit.position, "Fleeing");
        else State_MoveTo(stats.spawnPosition, "Fleeing Home");
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
                    if (stats.facingDirection != Vector3.zero) baseIdleDirection = stats.facingDirection;
                }

                currentPatrolTimer -= Time.deltaTime;
                if (enableLookAround) HandleLookAround();
                if (enableRandomTurn) HandleRandomTurn();

                if (currentPatrolTimer <= 0)
                {
                    State_MoveTo(GetRandomPatrolPoint(), "Patrolling (Moving)");
                    isPatrolWaiting = false;
                }
            }
            else
            {
                currentState = "Patrolling (Moving)";
                isPatrolWaiting = false;
                if (agent.velocity.sqrMagnitude > 0.1f) baseIdleDirection = agent.velocity.normalized;
            }
        }
        else
        {
            State_Idle();
        }
    }

    void State_Idle()
    {
        currentState = "Idle";
        if (agent.isOnNavMesh) agent.isStopped = true;
        if (stats.facingDirection != Vector3.zero && baseIdleDirection == Vector3.zero)
            baseIdleDirection = stats.facingDirection;
        if (enableRandomTurn) HandleRandomTurn();
        if (enableLookAround) HandleLookAround();
    }

    void State_Chase()
    {
        if (nearestTarget == null) return;
        State_MoveTo(nearestTarget.position, "Chasing");
        stats.EnterCombat();
    }

    void State_Attack()
    {
        if (nearestTarget == null) return;
        currentState = "Attacking";
        if (agent.isOnNavMesh) agent.isStopped = true;
        stats.EnterCombat();
        combat.PerformBasicAttack();
    }

    void HandleReturningState(float distToSpawn, float distToTarget)
    {
        bool gotHit = stats.currentAggro > 0;
        bool blockedByTarget = distToTarget <= combat.basicAttackRange;
        if (gotHit || (blockedByTarget && nearestTarget != null))
        {
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

    // --- HELPERS ---
    void HandleRandomTurn()
    {
        nextTurnTimer -= Time.deltaTime;
        if (nextTurnTimer <= 0)
        {
            float randomAngle = Random.Range(0f, 360f);
            baseIdleDirection = (Quaternion.Euler(0, randomAngle, 0) * Vector3.forward).normalized;
            if (!enableLookAround) stats.facingDirection = baseIdleDirection;
            else lookTimer = 0f;
            nextTurnTimer = Random.Range(randomTurnMinTime, randomTurnMaxTime);
        }
    }

    void HandleLookAround()
    {
        lookTimer += Time.deltaTime * lookSpeed;
        float currentAngle = Mathf.Sin(lookTimer) * lookAngle;
        Vector3 newDir = Quaternion.AngleAxis(currentAngle, Vector3.up) * baseIdleDirection;
        if (newDir != Vector3.zero) stats.facingDirection = newDir.normalized;
    }

    Vector3 GetRandomPatrolPoint()
    {
        Vector3 randomPoint = stats.spawnPosition + Random.insideUnitSphere * patrolRadius;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, 2.0f, NavMesh.AllAreas)) return hit.position;
        return stats.spawnPosition;
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
            if (nearestTarget != null) currentDir = (nearestTarget.position - transform.position).normalized;
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

    // [MỚI] Hàm phản xạ khi bị đánh (Gọi từ EnemyStats)
    public void OnDamageTaken(Transform attacker)
    {
        if (attacker == null) return;

        // 1. Nếu kẻ tấn công thuộc phe mình (Ally đánh Ally) thì bỏ qua (tùy logic game bạn)
        // Nhưng ở đây ta cứ set target đã, logic IsHostileTo sẽ lọc sau nếu cần.

        // 2. Ép buộc nhận mục tiêu ngay lập tức
        nearestTarget = attacker;

        // 3. Hủy bỏ trạng thái đang làm (Về nhà/Đi tuần) để chiến đấu ngay
        isReturningHome = false;
        if (agent.isOnNavMesh) agent.isStopped = false; // Đảm bảo AI có thể di chuyển/xoay

        // 4. Nếu là Neutral -> Chuyển sang thù địch (Logic này Stats đã lo qua Aggro, nhưng AI cần biết để Update)
        // Đảm bảo AI quay mặt về phía kẻ tấn công ngay lập tức (Tùy chọn)
        /*
        Vector3 dir = (attacker.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) stats.facingDirection = dir;
        */

        Debug.Log($"[AI] Bị đánh lén bởi {attacker.name}! -> Quay lại trả đũa ngay.");
    }

    // --- GIZMOS CẢI TIẾN (Vẽ hình rẻ quạt) ---
    void OnDrawGizmosSelected()
    {
        if (stats != null)
        {
            // 1. Detection Radius (Vàng)
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);

            // 2. View Cone (Nón Tầm Nhìn)
            if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Vector3 forward = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;

                // Vẽ 2 cạnh biên
                Vector3 leftRay = Quaternion.AngleAxis(-stats.viewAngle / 2, Vector3.up) * forward;
                Vector3 rightRay = Quaternion.AngleAxis(stats.viewAngle / 2, Vector3.up) * forward;
                Gizmos.DrawRay(transform.position, leftRay * stats.viewDistance);
                Gizmos.DrawRay(transform.position, rightRay * stats.viewDistance);

                // Vẽ thêm các tia ở giữa để tạo hình rẻ quạt
                int segments = 10;
                for (int i = 1; i < segments; i++)
                {
                    float angle = Mathf.Lerp(-stats.viewAngle / 2, stats.viewAngle / 2, i / (float)segments);
                    Vector3 midRay = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                    Gizmos.DrawRay(transform.position, midRay * stats.viewDistance);
                }

                // Vẽ trục giữa (Xanh)
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, forward * stats.viewDistance);
            }

            // 3. Aggro Radius (Tím)
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, stats.aggroRadius);

            // 4. Patrol Area (Cyan)
            if (enablePatrol)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(stats.spawnPosition, patrolRadius);
            }
        }
    }
}