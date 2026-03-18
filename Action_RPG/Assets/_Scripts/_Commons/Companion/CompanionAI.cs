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
    //public float attackCooldown = 1.5f;
    private float lastAttackTime = 0f; 
    private bool isAttacking = false;

    [Header("--- Target Memory ---")]
    private HashSet<Transform> markedTargets = new HashSet<Transform>();
    public Transform currentTarget;

    [Header("Scanning")]
    public float scanInterval = 0.5f;
    private float nextScanTime;
    public float scanRadius = 15f;

    [Header("Catalyst Buff")]
    private float focusBuffTimer = 0f;
    private float currentFocusBuffAmount = 0f;

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

        // [MỚI THÊM] NẾU ĐANG BỊ ÉP ĐỨNG YÊN THÌ KHÔNG LÀM GÌ CẢ
        if (forceWaitTimer > 0)
        {
            forceWaitTimer -= Time.deltaTime;

            // Xoay mặt nhìn theo hướng của Player cho ngầu (Cùng nhìn về phía trước)
            Stats playerStats = player.GetComponent<Stats>();
            if (playerStats != null && currentVisualDir != playerStats.facingDirection)
            {
                currentVisualDir = playerStats.facingDirection;
                UpdateAnimationDirection(currentVisualDir);
            }
            return; // Thoát hàm Update, không chạy AI đuổi/đánh nữa
        }

        // [MỚI] XỬ LÝ HẾT HẠN BUFF TỐC ĐÁNH TỪ CATALYST
        if (focusBuffTimer > 0)
        {
            focusBuffTimer -= Time.deltaTime;
            if (focusBuffTimer <= 0)
            {
                stats.bonusAttackSpeed -= currentFocusBuffAmount;
                stats.CalculateCombatStatsOnly();
                currentFocusBuffAmount = 0f;
                Debug.Log("<color=gray>[Companion] Đã hết thời gian Buff Tốc Đánh từ Catalyst.</color>");
            }
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
            // [MỚI] Tính toán Cooldown dựa trên attackSpeed
            float speed = stats.attackSpeed > 0 ? stats.attackSpeed : 1.0f;
            float currentAttackCooldown = 1.0f / speed;
            if (Time.time >= lastAttackTime + currentAttackCooldown)
            {
                StartCoroutine(AttackRoutine(speed));
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

    IEnumerator AttackRoutine(float currentAttackSpeed)
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        stats.EnterCombat();

        if (animator != null)
        {
            // Tăng tốc độ chạy animation (nếu Animator của Companion có param này)
            animator.SetFloat("AttackSpeedMultiplier", currentAttackSpeed);
            animator.SetTrigger("Attack");
        }

        // [MỚI] Chia tỉ lệ thời gian chờ gây sát thương dựa trên tốc độ đánh
        float totalAnimTime = 1.0f / currentAttackSpeed;

        // Cắn/chém ở mốc ~37.5% thời gian animation (tương đương 0.3s trong 0.8s cũ của bạn)
        float timeToHit = totalAnimTime * 0.375f;
        float timeToRecover = totalAnimTime - timeToHit;

        yield return new WaitForSeconds(timeToHit);

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

        // Đợi nốt phần animation còn lại thu tay về
        yield return new WaitForSeconds(timeToRecover);

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
    // ==========================================================
    // [MỚI] CHỨC NĂNG DÀNH CHO CATALYST SKILL
    // ==========================================================

    // Hàm ép buộc đổi mục tiêu và nhận Buff
    public void ForceFocusTarget(Transform newTarget, float attackSpeedBuff, float buffDuration)
    {
        if (newTarget == null || stats.isDead) return;

        // 1. Nhận mục tiêu mới
        markedTargets.Clear();
        markedTargets.Add(newTarget);
        currentTarget = newTarget;

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
        }

        // 2. Cấp Buff an toàn (Chống cộng dồn)
        if (focusBuffTimer <= 0)
        {
            // Nếu chưa có Buff -> Cộng Buff mới
            currentFocusBuffAmount = attackSpeedBuff;
            stats.bonusAttackSpeed += currentFocusBuffAmount;
            stats.CalculateCombatStatsOnly();
            Debug.Log($"<color=cyan>[Companion]</color> Đổi mục tiêu: {newTarget.name} và NHẬN Buff Tốc Đánh!");
        }
        else
        {
            // Nếu đang có Buff rồi -> Chỉ Làm Mới thời gian, KHÔNG cộng thêm
            Debug.Log($"<color=cyan>[Companion]</color> Đổi mục tiêu: {newTarget.name} và LÀM MỚI thời gian Buff!");
        }

        // 3. Reset đồng hồ đếm ngược
        focusBuffTimer = buffDuration;
    }
    // ==========================================================
    // [MỚI] CHỨC NĂNG ÉP ĐỨNG YÊN (CHO VANGUARD SKILL)
    // ==========================================================
    private float forceWaitTimer = 0f;

    public void ForceWait(float duration)
    {
        forceWaitTimer = duration;

        // Dừng NavMeshAgent
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        // Xóa trạng thái đang đánh và ép về dáng đứng im
        isAttacking = false;
        if (animator != null) animator.SetBool("IsWalking", false);

        Debug.Log($"<color=cyan>[Companion]</color> Đứng im nấp sau khiên trong {duration}s!");
    }
}