using Mono.Cecil.Cil;
using System.Collections;
using UnityEditor;
using UnityEditor.PackageManager.UI;
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

    [Header("--- AI Parry System ---")]
    [Tooltip("Tỷ lệ phản xạ đỡ đòn (0.8 = 80%)")]
    public float parryChance = 0.8f;

    [Header("--- AI Defense System ---")]
    public float projectileDetectionRadius = 8.0f; // Tầm phát hiện đạn
    public LayerMask projectileLayer; // Layer của mũi tên/đạn player

    [Tooltip("Thời gian nghỉ giữa các lần suy nghĩ có nên đỡ hay không")]
    public float parryReactionCooldown = 2.0f;
    private float nextParryCheckTime = 0f;


    // Tham chiếu skill
    private DuelistPassive parrySkill;

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

        // Lấy skill Parry (nếu có)
        parrySkill = GetComponent<DuelistPassive>();

        // Tự động bật canParry nếu có skill
        if (parrySkill != null)
        {
            parrySkill.isPlayer = false; // Báo đây là AI
            parrySkill.isLearned = true;
            stats.canParry = true;
        }
    }

    void Update()
    {
        if (stats.isDead || stats.isStunned || stats.isParrying)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            return;
        }

        // [MỚI] NẾU ĐANG PARRY -> KHÓA DI CHUYỂN VÀ TẤN CÔNG
        // Để đảm bảo Enemy đứng yên "gồng" đỡ đòn trong 0.5s
        if (stats.isParrying)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            currentState = "Parrying";
            HandleAnimation(stats.facingDirection); // Vẫn update anim direction nếu cần
            return; // Thoát Update để không chạy logic di chuyển/tấn công bên dưới
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

        // LOGIC AI PARRY CẢI TIẾN
        if (nearestTarget != null && stats.canParry && !stats.isStunned) // [QUAN TRỌNG] Chỉ chạy khi có Target
        {
            // 1. Melee Parry (Cận chiến)
            HandleParryReaction();

            // 2. Projectile Parry (Đỡ đạn) - Chỉ quét khi rảnh tay
            if (!combat.isAttacking && !stats.isParrying)
            {
                HandleProjectileDefense();
            }
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

    void HandleParryReaction()
    {
        // 1. Nếu đang hồi chiêu phản xạ hoặc đang tấn công/đỡ rồi thì thôi
        if (Time.time < nextParryCheckTime || combat.isAttacking || stats.isParrying) return;

        // 2. Kiểm tra xem mục tiêu (Player) có đang tấn công mình không?
        Animator targetAnim = nearestTarget.GetComponentInChildren<Animator>();
        if (targetAnim != null)
        {
            // Lấy thông tin animation hiện tại của Player
            AnimatorStateInfo stateInfo = targetAnim.GetCurrentAnimatorStateInfo(0);

            // Kiểm tra Tag "Attack" trong Animator của Player (Bạn nhớ set Tag này trong Animator Controller nhé)
            // Hoặc kiểm tra tên state: stateInfo.IsName("Attack1") ...
            //Để dòng code stateInfo.IsTag("Attack") trả về true, bạn bắt buộc phải làm bước này:
            //Mở cửa sổ Animator của Player(Window > Animation > Animator).
            //Chọn các ô trạng thái tấn công(ví dụ: Attack1, Attack2, HeavyAttack, OdoAttack...).
            //Nhìn sang bảng Inspector bên phải.
            //Tìm dòng Tag.
            //Gõ chữ "Attack" vào ô đó (hoặc chọn nếu đã có sẵn). Lưu ý chữ hoa thường phải chính xác 100% như trong code.
            if (stateInfo.IsTag("Attack"))
            {
                // 3. Kiểm tra khoảng cách (Chỉ đỡ khi Player ở gần)
                float dist = Vector3.Distance(transform.position, nearestTarget.position);
                if (dist <= 2.5f) // Tầm gần mới cần đỡ
                {
                    // 4. RANDOM CƠ HỘI (80%)
                    if (Random.value < parryChance)
                    {
                        // THỰC HIỆN PARRY
                        Debug.Log($"<color=orange>[AI] {gameObject.name} phản xạ Parry!</color>");

                        if (parrySkill != null) parrySkill.AI_StartParry();

                        // Giữ trạng thái Parry trong khoảng thời gian (ví dụ 0.5s) rồi thả
                        StartCoroutine(ParryThenCounterAttack(0.5f));
                    }
                    else
                    {
                        Debug.Log("[AI] Phản xạ chậm, không đỡ kịp!");
                    }

                    // Dù đỡ hay không, cũng phải chờ một lúc mới check lại (tránh spam)
                    nextParryCheckTime = Time.time + parryReactionCooldown;
                }
            }
        }
    }


    // [MỚI] Hàm xử lý đỡ đạn từ xa
    void HandleProjectileDefense()
    {
        // Quét xung quanh xem có mũi tên nào đang bay tới không
        // [LƯU Ý] Nếu projectileDetectionRadius quá nhỏ (5.0f), AI có thể ko kịp phản xạ đạn nhanh.
        // Bạn nên tăng lên 8-10f nếu đạn bay nhanh.
        Collider[] threats = Physics.OverlapSphere(transform.position, projectileDetectionRadius, projectileLayer);

        foreach (var threat in threats)
        {
            // Kiểm tra Rigidbody đạn
            Rigidbody projRb = threat.GetComponent<Rigidbody>();
            if (projRb != null && projRb.linearVelocity.sqrMagnitude > 0.1f) // Chỉ check đạn đang bay
            {
                Vector3 dirToMe = (transform.position - threat.transform.position).normalized;
                float dot = Vector3.Dot(projRb.linearVelocity.normalized, dirToMe);

                // Dot > 0.8: Đạn đang bay về phía mình
                if (dot > 0.8f)
                {
                    float dist = Vector3.Distance(transform.position, threat.transform.position);
                    float timeToImpact = dist / projRb.linearVelocity.magnitude;

                    // Nếu sắp trúng (< 0.5s) -> Đỡ ngay!
                    if (timeToImpact < 0.5f)
                    {
                        // Check Cooldown phản xạ (để AI không đỡ liên tục như thần thánh)
                        if (Time.time < nextParryCheckTime) return;

                        if (Random.value < parryChance)
                        {
                            Debug.Log($"<color=cyan>[AI] {name} đỡ đạn!</color>");

                            // Xoay mặt về phía đạn để Angle Check trong Stats hoạt động đúng
                            Vector3 dirToThreat = (threat.transform.position - transform.position).normalized;
                            dirToThreat.y = 0;
                            if (dirToThreat != Vector3.zero) stats.facingDirection = dirToThreat;

                            if (parrySkill != null) parrySkill.AI_StartParry();

                            // Giữ thế thủ 0.5s (hoặc lâu hơn nếu cần)
                            StartCoroutine(ParryThenCounterAttack(0.5f));

                            // Set cooldown phản xạ
                            nextParryCheckTime = Time.time + parryReactionCooldown;

                            return;
                        }
                        else
                        {
                            // AI fail parry -> Set cooldown để ko check lại đạn này ngay lập tức
                            nextParryCheckTime = Time.time + 0.5f;
                        }
                    }
                }
            }
        }
    }



    // [MỚI] Coroutine xử lý Parry xong thì Phản công (Counter Attack)
    IEnumerator ParryThenCounterAttack(float delay)
    {
        // 1. Giữ trạng thái Parry trong 0.5s (trong lúc này AI bất động nhờ logic Update)
        yield return new WaitForSeconds(delay);

        // 2. Tắt Parry
        if (parrySkill != null) parrySkill.AI_StopParry();

        // 3. [QUAN TRỌNG] Đánh trả ngay lập tức (Riposte)
        // Kiểm tra xem mục tiêu còn trong tầm đánh không
        if (nearestTarget != null && !stats.isStunned && !stats.isDead)
        {
            float dist = Vector3.Distance(transform.position, nearestTarget.position);

            // Nếu vẫn còn gần -> Vả luôn!
            if (dist <= combat.basicAttackRange + 0.5f)
            {
                Debug.Log("<color=red>[AI] Parry xong -> Phản công ngay!</color>");
                combat.PerformBasicAttack();
            }
        }
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