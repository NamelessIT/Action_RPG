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

        combat.Setup(stats, playerTarget,animator);

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
        HandleAnimation(stats.facingDirection);
    }

    void Update()
    {
        if (playerTarget == null) return;

        // [MỚI] QUAN TRỌNG: Nếu đang đánh thì đứng yên tuyệt đối, không tính toán AI
        if (combat.isAttacking)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero; // Dừng trượt
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

            // [YÊU CẦU 2] OutCombat = true khi quay về
            stats.outCombat = true;
        }

        // --- 2. XỬ LÝ STATE ---

        if (isReturningHome)
        {
            // Trong lúc đang đi về, đảm bảo vẫn là Out Combat (trừ khi bị đánh lại)
            if (stats.outCombat == false && currentState == "Returning")
            {
                // Dòng này tùy chọn: Nếu bạn muốn nó hồi máu nhanh khi đang đi bộ về
                // stats.outCombat = true; 
            }
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
            if (gotHit || blockedByPlayer)
            {
                stats.outCombat = false;
                shouldFightBack = true;
            } 
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
                stats.outCombat= false;
            }
            else if (blockedByPlayer && distToSpawn > 3.0f)
            {
                shouldFightBack = true;
                stats.outCombat = false;
            }
        }

        // Thực hiện hành động nếu quyết định đánh lại
        if (shouldFightBack)
        {
            Debug.Log("Đang về thì bị khiêu khích -> Chiến tiếp!");
            isReturningHome = false;
            stats.outCombat = false;

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
            stats.outCombat = true;
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
        if (Mathf.Abs(dir.z) > Mathf.Abs(dir.x)) dirIndex = dir.z > 0 ? 4 : 0;
        else dirIndex = 2;
        animator.SetFloat("Direction", (float)dirIndex);
        if (dir.x > 0) spriteRenderer.flipX = true;
        else if (dir.x < 0) spriteRenderer.flipX = false;
    }

    // [HÀM MỚI] Kiểm tra phát hiện Player
    bool CheckDetection()
    {
        if (playerTarget == null) return false;

        // 1. Lấy chỉ số lén lút của Player
        float playerStealth = 1.0f;
        Stats playerStats = playerTarget.GetComponent<Stats>();
        if (playerStats != null)
        {
            playerStealth = playerStats.stealthFactor;
        }

        // 2. Tính khoảng cách thực tế
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // --- A. PHÁT HIỆN BẰNG PHẠM VI (RANGE / SOUND) ---
        // Phạm vi này bị giảm bởi chỉ số Stealth
        // Ví dụ: Radius gốc 5m, Stealth 0.8 -> Còn 4m
        float effectiveRadius = stats.detectionRadius * playerStealth;

        if ((stats.detectionMethod & DetectionMethod.Range) != 0)
        {
            if (distToPlayer <= effectiveRadius) return true;
        }

        // --- B. PHÁT HIỆN BẰNG TẦM NHÌN (SIGHT) ---
        if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
        {
            // Tầm nhìn xa cũng bị giảm
            float effectiveViewDist = stats.viewDistance * playerStealth;
            // Góc nhìn cũng bị hẹp lại (Option nâng cao, tùy bạn muốn dùng hay không)
            // float effectiveAngle = stats.viewAngle * playerStealth; 
            float effectiveAngle = stats.viewAngle; // Tạm thời giữ nguyên góc

            if (distToPlayer <= effectiveViewDist)
            {
                Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;

                // Tính góc giữa hướng mặt Enemy và hướng tới Player
                // Lưu ý: facingDirection là Vector3.back/forward... trong game 2.5D của bạn
                // Nếu dùng 3D thuần thì dùng transform.forward
                Vector3 facingDir = stats.facingDirection;
                if (facingDir == Vector3.zero) facingDir = transform.forward; // Fallback

                float angleToPlayer = Vector3.Angle(facingDir, dirToPlayer);

                // Kiểm tra góc (chia đôi vì viewAngle là tổng góc mở)
                if (angleToPlayer < effectiveAngle / 2f)
                {
                    // [QUAN TRỌNG] Kiểm tra vật cản (Raycast)
                    // Bắn tia từ mắt Enemy tới Player
                    if (!Physics.Raycast(transform.position, dirToPlayer, distToPlayer, stats.obstacleMask))
                    {
                        return true; // Nhìn thấy và không bị che
                    }
                }
            }
        }

        return false;
    }

    // [CẬP NHẬT] Vẽ Gizmos để debug dễ hơn
    void OnDrawGizmosSelected()
    {
        if (stats != null)
        {
            // 1. Vẽ phạm vi tròn (Range)
            Gizmos.color = new Color(1, 1, 0, 0.3f); // Vàng mờ
            Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);

            // 2. Vẽ hình quạt (Sight)
            if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f); // Đỏ
                Vector3 forward = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;

                // Vẽ 2 cạnh của hình quạt
                Quaternion leftRayRotation = Quaternion.AngleAxis(-stats.viewAngle / 2, Vector3.up);
                Quaternion rightRayRotation = Quaternion.AngleAxis(stats.viewAngle / 2, Vector3.up);

                Vector3 leftRay = leftRayRotation * forward;
                Vector3 rightRay = rightRayRotation * forward;

                Gizmos.DrawRay(transform.position, leftRay * stats.viewDistance);
                Gizmos.DrawRay(transform.position, rightRay * stats.viewDistance);
            }

            // Vẽ Aggro Radius
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, stats.aggroRadius);
        }
    }
}