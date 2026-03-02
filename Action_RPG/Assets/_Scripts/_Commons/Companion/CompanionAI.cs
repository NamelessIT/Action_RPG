using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AllyStats))] // Thú cưng thuộc phe Ally
public class CompanionAI : MonoBehaviour
{
    [Header("--- References ---")]
    public Transform player;
    private NavMeshAgent agent;
    private AllyStats stats;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    [Header("--- Follow Settings ---")]
    [Tooltip("Khoảng cách bắt đầu chạy theo Player")]
    public float distanceFollow = 8f;
    [Tooltip("Khoảng cách quá xa sẽ tự động dịch chuyển lại gần")]
    public float teleportDistance = 15f;
    [Tooltip("Bán kính đi lảng vảng quanh Player khi ở gần")]
    public float wanderRadius = 5f;
    public float wanderWaitTime = 3f;

    private float wanderTimer = 0f;
    private bool isWandering = false;

    [Header("--- Combat Settings ---")]
    public float attackRange = 2.0f;
    public float attackCooldown = 1.5f;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;

    [Header("--- Target Memory ---")]
    private HashSet<Transform> markedTargets = new HashSet<Transform>();
    public Transform currentTarget;

    [Header("Scanning")]
    public float scanInterval = 0.5f;
    private float nextScanTime;
    public float scanRadius = 15f;

    private Vector3 currentVisualDir;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<AllyStats>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.baseMoveSpeed > 0 ? stats.baseMoveSpeed : 5f;

        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        currentVisualDir = Vector3.back;
    }

    void Update()
    {
        if (stats.isDead || stats.isStunned || player == null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        CleanDeadTargets();

        if (Time.time >= nextScanTime)
        {
            ScanForThreats();
            nextScanTime = Time.time + scanInterval;
        }

        if (isAttacking)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        UpdateCurrentTarget();

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // -- KIỂM TRA DỊCH CHUYỂN --
        if (distToPlayer > teleportDistance)
        {
            TeleportToPlayer();
            return;
        }

        // -- CHIẾN ĐẤU --
        if (currentTarget != null)
        {
            HandleCombat();
        }
        // -- ĐI THEO / ĐI DẠO --
        else
        {
            if (distToPlayer > distanceFollow)
            {
                // Xa quá -> Chạy theo Player
                agent.isStopped = false;
                isWandering = false;

                // [FIX 1] Đặt stoppingDistance để Companion biết "phanh" lại khi còn cách Player 3 mét
                agent.stoppingDistance = 3f;

                // Giúp NavMeshAgent không bị khựng do tính toán quá nhiều
                if (!agent.hasPath || Vector3.Distance(agent.destination, player.position) > 1f)
                {
                    agent.SetDestination(player.position);
                }
            }
            else
            {
                // [FIX 2] NGĂN TÔNG VÀO PLAYER
                // Nếu vừa mới chạy vào vùng an toàn (distanceFollow) và mục tiêu vẫn đang là Player
                if (!isWandering && agent.hasPath)
                {
                    if (Vector3.Distance(agent.destination, player.position) < 2.5f)
                    {
                        // Hủy đường đi ngay lập tức và dừng lại
                        agent.ResetPath();
                        agent.velocity = Vector3.zero;
                    }
                }

                // Ở gần -> Đi lảng vảng (Wander)
                HandleWander();
            }
        }

        HandleVisuals();
    }

    void HandleWander()
    {
        // Khi đi dạo thì phải đi tới đích, không dùng phanh 3m của hàm Follow nữa
        agent.stoppingDistance = 0f;

        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!isWandering)
            {
                isWandering = true;
                wanderTimer = wanderWaitTime;
            }

            wanderTimer -= Time.deltaTime;

            if (wanderTimer <= 0)
            {
                // [FIX 2] TÌM ĐIỂM WANDER AN TOÀN (Không dẫm lên Player)
                float minSafeDistance = 2.5f; // Luôn cách Player ít nhất 2.5m

                // Lấy hướng ngẫu nhiên (tạo thành 1 vòng tròn viền ngoài)
                Vector2 randomCircle = Random.insideUnitCircle.normalized;

                // Lấy độ dài ngẫu nhiên từ 2.5m đến 5m
                float randomDist = Random.Range(minSafeDistance, wanderRadius);

                Vector3 randomDir = new Vector3(randomCircle.x, 0, randomCircle.y) * randomDist;
                Vector3 randomPoint = player.position + randomDir;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, 2.0f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    isWandering = false;
                }
                else
                {
                    // Random kẹt vào tường thì random lại ngay lập tức
                    wanderTimer = 0.5f;
                }
            }
        }
    }

    void TeleportToPlayer()
    {
        Vector3 randomDir = Random.insideUnitSphere * 3f;
        randomDir.y = 0;
        Vector3 targetPos = player.position + randomDir;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 5.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log("[Companion] Bỏ chạy cùng Player. Đã xóa toàn bộ Aggro!");

            agent.ResetPath();

            // [FIX 3] XÓA BỎ MỌI MỤC TIÊU VÀ TRẠNG THÁI TẤN CÔNG
            markedTargets.Clear();
            currentTarget = null;
            isAttacking = false;
            stats.outCombat = true; // Trở về trạng thái bình yên

            // Dừng hoạt ảnh đánh nếu có
            //if (animator != null) animator.ResetTrigger("Attack");
        }
    }

    public void AddMarkedTarget(Transform enemy)
    {
        if (enemy == null) return;
        Stats enemyStats = enemy.GetComponent<Stats>();
        if (enemyStats != null && !enemyStats.isDead)
        {
            markedTargets.Add(enemy);
            Debug.Log($"[Companion] Đã đánh dấu mục tiêu: {enemy.name}");
        }
    }

    void ScanForThreats()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, scanRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyAI eAI = hit.GetComponent<EnemyAI>();
                if (eAI != null && eAI.nearestTarget != null)
                {
                    if (eAI.nearestTarget == player || eAI.nearestTarget == transform)
                    {
                        AddMarkedTarget(hit.transform);
                    }
                }
            }
        }
    }

    void CleanDeadTargets()
    {
        List<Transform> toRemove = new List<Transform>();

        foreach (Transform target in markedTargets)
        {
            if (target == null)
            {
                toRemove.Add(target);
                continue;
            }

            Stats tStats = target.GetComponent<Stats>();
            if (tStats == null || tStats.isDead)
            {
                toRemove.Add(target);
            }
        }

        foreach (Transform t in toRemove)
        {
            markedTargets.Remove(t);
        }

        if (currentTarget != null)
        {
            Stats cStats = currentTarget.GetComponent<Stats>();
            if (cStats == null || cStats.isDead) currentTarget = null;
        }
    }

    void UpdateCurrentTarget()
    {
        if (currentTarget != null) return;

        float minDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (Transform target in markedTargets)
        {
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                bestTarget = target;
            }
        }

        currentTarget = bestTarget;
    }

    void HandleCombat()
    {
        float distToTarget = Vector3.Distance(transform.position, currentTarget.position);

        if (distToTarget <= attackRange)
        {
            agent.isStopped = true;
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(AttackRoutine());
            }

            Vector3 dir = (currentTarget.position - transform.position).normalized;
            dir.y = 0;
            currentVisualDir = dir;
        }
        else
        {
            agent.isStopped = false;
            // Dừng ở mép viền tầm đánh, không cần rúc hẳn vào bụng quái
            agent.stoppingDistance = attackRange * 0.8f;

            if (!agent.hasPath || Vector3.Distance(agent.destination, currentTarget.position) > 1f)
            {
                agent.SetDestination(currentTarget.position);
            }
        }
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        stats.EnterCombat();

        if (animator != null) animator.SetTrigger("Attack");

        yield return new WaitForSeconds(0.3f);

        if (currentTarget != null)
        {
            Stats targetStats = currentTarget.GetComponent<Stats>();
            if (targetStats != null && !targetStats.isDead)
            {
                DamageInfo info = new DamageInfo();
                info.sourcePosition = transform.position;
                info.attacker = stats;
                info.damageAmount = stats.physicalAtk;

                targetStats.TakeDamage(info);
                Debug.Log($"[Companion] Đã cắn {currentTarget.name}!");
            }
        }

        yield return new WaitForSeconds(0.5f);

        isAttacking = false;
    }

    void HandleVisuals()
    {
        if (agent.velocity.magnitude > 0.1f)
        {
            currentVisualDir = agent.velocity.normalized;
        }
        else if (currentTarget != null && !isAttacking)
        {
            currentVisualDir = (currentTarget.position - transform.position).normalized;
        }

        if (currentVisualDir != Vector3.zero)
        {
            stats.facingDirection = currentVisualDir;
            UpdateAnimationDirection(currentVisualDir);
        }
    }

    void UpdateAnimationDirection(Vector3 dir)
    {
        if (animator == null) return;

        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("IsWalking", isMoving);

        int dirIndex = 0;
        if (Mathf.Abs(dir.z) > Mathf.Abs(dir.x)) dirIndex = dir.z > 0 ? 4 : 0;
        else dirIndex = 2;

        animator.SetFloat("Direction", (float)dirIndex);

        if (spriteRenderer != null)
        {
            if (dir.x > 0) spriteRenderer.flipX = true;
            else if (dir.x < 0) spriteRenderer.flipX = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, distanceFollow);

        Gizmos.color = Color.yellow;
        if (player != null) Gizmos.DrawWireSphere(player.position, wanderRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, teleportDistance);
    }
}