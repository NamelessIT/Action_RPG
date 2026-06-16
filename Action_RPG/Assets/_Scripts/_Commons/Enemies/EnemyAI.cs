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

    [Header("--- Post Attack Behavior ---")]
    [Tooltip("Khoảng cách lùi sau khi đánh xong")]
    public float backOffDistance = 2.0f;
    [Tooltip("Xác suất lùi lại sau đòn (0=không bao giờ, 1=luôn luôn)")]
    [Range(0f, 1f)] public float backOffChance = 0.5f;
    [Tooltip("Xác suất xoay vòng quanh player thay vì lùi")]
    [Range(0f, 1f)] public float circleChance = 0.3f;
    // Thời điểm kết thúc đòn đánh cuối để tính cooldown
    private float lastAttackEndTime = -100f;

    // Post-attack behavior state — chọn một lần, giữ đến hết cooldown
    private bool _postAttackBehaviorChosen = false;
    private bool _postAttackCircleRight    = false;

    [Header("--- Boss Dodge System ---")]
    [Tooltip("Chỉ hoạt động với monsterRank >= 1")]
    public float bossDodgeCooldown = 4f;
    [Range(0f, 1f)]
    public float bossDodgeChance = 0.65f;
    private bool isBossDodging = false;
    private float lastBossDodgeTime = -100f;

    [Header("--- AI Parry System ---")]
    [Tooltip("Tỷ lệ phản xạ đỡ đòn (0.8 = 80%)")]
    public float parryChance = 0.8f;

    [Header("--- AI Defense System ---")]
    public float projectileDetectionRadius = 8.0f; // Tầm phát hiện đạn
    public LayerMask projectileLayer; // Layer của mũi tên/đạn player

    [Tooltip("Thời gian nghỉ giữa các lần suy nghĩ có nên đỡ hay không")]
    public float parryReactionCooldown = 2.0f;
    private float nextParryCheckTime = 0f;

    // [MỚI] Biến đếm thời gian chờ phản công
    [Tooltip("Thời gian chờ không nhận sát thương mới được phản công")]
    public float cooldownTimerReceiveDame = 5.0f;
    public float receiveDamageTimer = 0f;

    // Đặt chung chỗ với các biến đếm timer khác (vd: dưới receiveDamageTimer)
    private float lastTimeCurrentTargetDamagedMe = -100f;


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
        agent.acceleration = 20f;   // Dừng nhanh nhưng không instant → không khựng
        agent.angularSpeed = 0f;    // Tắt rotation của NavMesh, dùng facingDirection thủ công

        if (stats.facingDirection == Vector3.zero) stats.facingDirection = Vector3.back;
        baseIdleDirection = stats.facingDirection;
        nextTurnTimer = Random.Range(randomTurnMinTime, randomTurnMaxTime);

        HandleAnimation(stats.facingDirection);

        parrySkill = GetComponent<DuelistPassive>();

        if (parrySkill == null && stats.canParry)
        {
            parrySkill = gameObject.AddComponent<DuelistPassive>();
        }

        if (parrySkill != null)
        {
            parrySkill.isPlayer = false;
            parrySkill.isLearned = true;
            stats.canParry = true;
            // [MỚI] Ép cứng 5 giây cho Enemy ngay từ lúc bắt đầu
            parrySkill.maxParryDuration = 15f;
        }
    }

    void Update()
    {
        if (stats.isDead || stats.isStunned)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            return;
        }

        // Boss dodge đang chạy — coroutine tự quản lý movement
        if (isBossDodging) return;

        // [MỚI] Giảm thời gian chờ phản công liên tục
        if (receiveDamageTimer > 0) receiveDamageTimer -= Time.deltaTime;

        // [MỚI] NẾU ĐANG PARRY -> KHÓA DI CHUYỂN VÀ TẤN CÔNG
        // Để đảm bảo Enemy đứng yên "gồng" đỡ đòn trong 0.5s
        if (stats.isParrying)
        {
            // Nếu đang cầm khiên MÀ đã hết 2 giây không ai đánh -> HẠ KHIÊN ĐỂ PHẢN CÔNG NGAY!
            if (receiveDamageTimer <= 0f)
            {
                Debug.Log("<color=green>[AI] Đã hết 2s an toàn, tự động hạ khiên phản công!</color>");
                if (parrySkill != null) parrySkill.AI_StopParry();
                // Không "return" ở đây để code tiếp tục chạy xuống dưới và lao vào đánh
            }
            else
            {
                // Nếu vẫn đang trong 2 giây chờ -> Đứng im cầm khiên
                if (agent.isOnNavMesh) agent.isStopped = true;
                currentState = "Parrying";
                HandleAnimation(stats.facingDirection);
                return; // Thoát Update để không chạy logic di chuyển/tấn công bên dưới
            }
        }

        // 1. QUÉT TÌM MỤC TIÊU
        if (Time.time >= nextScanTime)
        {
            ScanForTarget();
            nextScanTime = Time.time + scanInterval;
        }

        // Cập nhật target cho Combat
        combat.SetTarget(nearestTarget);

        // 2. NẾU ĐANG ĐÁNH hoặc TELEGRAPH -> KHÓA DI CHUYỂN
        if (combat.isAttacking || combat.isTelegraphing)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            if (animator != null) animator.SetBool("IsWalking", false); // tránh kẹt animation đi bộ khi đang gồng/đánh
            stats.EnterCombat();
            if (combat.isAttacking) lastAttackEndTime = Time.time;
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

        // Điều kiện quay về (không phải Friendly):
        //  - Bản thân đi quá xa nhà, HOẶC
        //  - Mục tiêu đã ra NGOÀI vùng hoạt động (bị nhử ra rìa). → cam kết chạy về, đừng đứng rìa.
        float homeZone = stats.aggroRadius * 1.5f;
        bool targetOutOfZone = nearestTarget != null
                               && Vector3.Distance(nearestTarget.position, stats.spawnPosition) > homeZone;
        bool shouldReturn = !isReturningHome
                            && stats.enemyType != EnemyType.Friendly
                            && (distToSpawn > homeZone || targetOutOfZone);

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
                        StartCoroutine(ParryRoutine(parrySkill.maxParryDuration));
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
                            StartCoroutine(ParryRoutine(parrySkill.maxParryDuration));

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
    IEnumerator ParryRoutine(float delay)
    {
        // 1. Giữ trạng thái Parry trong 0.5s (trong lúc này AI bất động nhờ logic Update)
        yield return new WaitForSeconds(delay);

        if (parrySkill != null) parrySkill.AI_StopParry();
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
                Debug.Log($"[AI] Đã mất dấu {nearestTarget.name} (Chết, tàng hình hoặc chạy quá xa).");
                nearestTarget = null;
            }
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, stats.detectionRadius);
        Transform bestTarget = null;
        Transform bestPredator = null;
        float closeDist = Mathf.Infinity;

        // [COMPANION AGGRO] Ứng viên Player & Companion (để cân aggro theo Matrix khi CHỌN mục tiêu mới).
        Transform aggroPlayerCand = null;
        Transform aggroCompanionCand = null;

        foreach (var hit in hits)
        {
            Transform t = hit.transform;
            if (t == transform) continue;

            // Check kẻ thù để bỏ chạy (Cho Friendly)
            if (stats.IsScaredOf(t) && IsValidTarget(t)) // [MỚI] Thêm check IsValidTarget (tàng hình)
            {
                bestPredator = t;
            }

            // Check mục tiêu để tấn công
            if (stats.IsHostileTo(t))
            {
                if (CanSeeTarget(t))
                {
                    if (t.CompareTag("Player")) aggroPlayerCand = t;
                    else if (t.GetComponentInParent<CompanionAI>() != null) aggroCompanionCand = t;

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
        // --- [MỚI] GÁN MỤC TIÊU VÀ RESET ĐỒNG HỒ ---
        if (nearestTarget == null && bestTarget != null)
        {
            // [COMPANION AGGRO] Thấy CẢ Player lẫn Companion → cân theo AggroWeight của Matrix
            // (Regen 50/50, Phantoms 20/80, Deflector 80/20 = Companion/Player). Chỉ roll khi CHỌN mới.
            Transform chosen = bestTarget;
            if (aggroPlayerCand != null && aggroCompanionCand != null)
            {
                var eq = aggroCompanionCand.GetComponentInParent<CompanionEquipmentManager>();
                float w = eq != null ? eq.AggroWeight : 0.5f;
                chosen = (Random.value < w) ? aggroCompanionCand : aggroPlayerCand;
            }
            nearestTarget = chosen;
            // Vừa tự phát hiện ra mục tiêu mới -> Reset đồng hồ 15s cho nó
            lastTimeCurrentTargetDamagedMe = Time.time;
        }

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
        if (tStats == null || tStats.isDead || tStats.isInvisible) return false;
        return stats.IsHostileTo(t);
    }

    bool CanSeeTarget(Transform target)
    {
        float dist = Vector3.Distance(transform.position, target.position);
        float stealthFactor = 1.0f;
        Stats tStats = target.GetComponent<Stats>();
        if (tStats != null) stealthFactor = tStats.stealthFactor;

        // Enemy không bị ảnh hưởng stealthReduction → bỏ qua stealth của player
        if (stats is EnemyStats enemyStats && !enemyStats.isAffectedByStealthReduction)
            stealthFactor = 1.0f;

        float effectiveRadius    = stats.detectionRadius * stealthFactor;
        float effectiveViewDist  = stats.viewDistance * stealthFactor;
        float effectiveHalfAngle = (stats.viewAngle / 2f) * stealthFactor;

        if ((stats.detectionMethod & DetectionMethod.Range) != 0)
        {
            if (dist <= effectiveRadius) return true;
        }

        if ((stats.detectionMethod & DetectionMethod.Sight) != 0)
        {
            if (dist <= effectiveViewDist)
            {
                Vector3 dirToTarget = (target.position - transform.position).normalized;
                Vector3 facingDir = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;

                float angle = Vector3.Angle(facingDir, dirToTarget);
                if (angle < effectiveHalfAngle)
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
        // ==========================================
        // 0. BOSS DODGE — Chỉ cho rank >= 1, né đòn player rồi phản công
        // ==========================================
        if (stats.monsterRank >= 1 && !isBossDodging && Time.time >= lastBossDodgeTime + bossDodgeCooldown)
        {
            PlayerController pc = nearestTarget?.GetComponent<PlayerController>();
            if (pc != null && pc.isAttacking && distToTarget <= combat.basicAttackRange * 2.5f)
            {
                if (Random.value <= bossDodgeChance)
                {
                    StartCoroutine(BossDodgeRoutine());
                    return;
                }
                // Trượt dodge chance thì reset cooldown ngắn để không liên tục roll fail
                lastBossDodgeTime = Time.time - bossDodgeCooldown * 0.7f;
            }
        }

        // ==========================================
        // 1. POST-ATTACK COOLDOWN BEHAVIOR
        // Enemy vừa đánh xong → hành động thông minh trong thời gian chờ hồi
        // ==========================================
        float attackCooldown = 1.0f / Mathf.Max(stats.baseAttackSpeed, 0.01f);
        bool isOnAttackCooldown = Time.time < lastAttackEndTime + attackCooldown;
        if (isOnAttackCooldown)
        {
            HandlePostAttackBehavior(distToTarget);
            return;
        }

        // Cooldown vừa kết thúc → reset trạng thái post-attack và phục hồi tốc độ
        if (_postAttackBehaviorChosen)
        {
            _postAttackBehaviorChosen = false;
            RestoreNormalSpeed();
        }

        // ==========================================
        // 2. LOGIC QUYẾT ĐỊNH PHÒNG THỦ HAY TẤN CÔNG
        // ==========================================
        bool shouldDefend = false;

        if (stats.canParry && parrySkill != null)
        {
            // Nếu khiên KHÔNG TRONG COOLDOWN (Sẵn sàng dùng)
            if (parrySkill.currentCooldown <= 0)
            {
                // Nếu vừa bị đánh (Timer > 0) -> Đứng thủ chờ thời
                if (receiveDamageTimer > 0f)
                {
                    shouldDefend = true;
                }
                // Nếu đã qua 2 giây an toàn (Timer <= 0) -> Chuyển sang tấn công
                else
                {
                    shouldDefend = false;
                }
            }
            // Nếu khiên ĐANG TRONG COOLDOWN (Vỡ khiên) -> Liều mạng tấn công
            else
            {
                shouldDefend = false;
                // Có thể tắt luôn timer nhận damage để ép nó đánh (như bạn yêu cầu)
                receiveDamageTimer = 0f;
            }
        }

        // ==========================================
        // 2. THỰC THI HÀNH ĐỘNG
        // ==========================================

        if (shouldDefend)
        {
            // --- HÀNH ĐỘNG PHÒNG THỦ (CHỜ ĐỢI) ---
            if (distToTarget <= combat.basicAttackRange * 1.5f) // Cho tầm nhìn rộng hơn chút để nó biết xoay mặt
            {
                currentState = "Defending (Waiting)";
                if (agent.isOnNavMesh) agent.isStopped = true;

                // Liên tục quay mặt nhìn Player đề phòng bị chém lén
                Vector3 dir = (nearestTarget.position - transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero) stats.facingDirection = dir;
            }
            else
            {
                // Dù muốn thủ nhưng xa quá thì vẫn phải chạy lại gần
                State_Chase();
            }
        }
        else
        {
            // --- HÀNH ĐỘNG TẤN CÔNG ---
            if (distToTarget <= combat.basicAttackRange)
            {
                State_Attack();
            }
            else
            {
                //Debug.Log("EnemyAI HandleCombatBehavior do không đủ combat.basicAttackRange");
                State_Chase();
            }
        }
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
        if (agent.isOnNavMesh)
        {
            agent.stoppingDistance = combat.basicAttackRange * 0.9f;
            agent.speed = stats.baseMoveSpeed; // đảm bảo tốc độ về bình thường sau khi circle
        }
        else
        {
            Debug.Log("EnemyAI State_Chase tầm đánh không đủ");
        }
        State_MoveTo(nearestTarget.position, "Chasing");
        stats.EnterCombat();
    }

    void RestoreNormalSpeed()
    {
        if (agent.isOnNavMesh) agent.speed = stats.baseMoveSpeed;
    }

    void State_Attack()
    {
        if (nearestTarget == null) return;
        currentState = "Attacking";
        if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }

        // Ép mặt về phía player trước khi đánh — tránh đánh hụt do facingDirection sai hướng
        Vector3 dirToTarget = (nearestTarget.position - transform.position).normalized;
        dirToTarget.y = 0;
        if (dirToTarget != Vector3.zero) stats.facingDirection = dirToTarget;

        stats.EnterCombat();
        combat.PerformBasicAttack();
    }

    void HandleReturningState(float distToSpawn, float distToTarget)
    {
        // Đã CAM KẾT chạy về nhà. CHỈ dừng lại để đánh nếu có kẻ địch vừa Ở TRONG vùng hoạt động
        // vừa ÁP SÁT trong tầm đánh (chặn đường / cận chiến). Kẻ kite/bắn xa ngoài vùng → phớt lờ.
        // (Điều kiện áp sát tránh dao động khi Companion kite quanh ranh giới vùng.)
        float zone = stats.aggroRadius * 1.5f;
        bool blocker = nearestTarget != null
            && Vector3.Distance(nearestTarget.position, stats.spawnPosition) <= zone
            && distToTarget <= combat.basicAttackRange + 1.0f;

        if (blocker)
        {
            isReturningHome = false;
            stats.outCombat = false;
            if (stats.currentAggro <= 0) stats.AddAggro(50f);
            stats.EnterCombat();
            return;
        }

        // Còn lại: PHỚT LỜ mọi tấn công, xoá aggro/target, LUÔN chạy về nhà (không đứng yên/xoay).
        stats.currentAggro = 0;
        nearestTarget = null;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            isReturningHome = false;
            stats.outCombat = true;
            baseIdleDirection = stats.facingDirection;
            stats.currentHp = stats.maxHp; // Về tới nhà an toàn → HỒI ĐẦY MÁU ngay.
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
            // Khi đang lùi hoặc circle (post-attack), vẫn nhìn về phía player thay vì nhìn hướng di chuyển
            if (currentState.StartsWith("Post-Attack") && nearestTarget != null)
            {
                currentDir = (nearestTarget.position - transform.position).normalized;
                currentDir.y = 0;
            }
            else
            {
                currentDir = agent.velocity.normalized;
            }
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

        // [MỚI] Không khóa mục tiêu nếu kẻ tấn công vô hình
        Stats attackerStats = attacker.GetComponent<Stats>();
        if (attackerStats != null && attackerStats.isInvisible) return;

        // [LEASH] Kẻ tấn công NGOÀI vùng hoạt động (vd Companion nhử ra rìa rồi bắn từ xa)
        // → PHỚT LỜ hoàn toàn: không đánh thức, không khóa mục tiêu, không hủy lệnh về nhà.
        if (Vector3.Distance(attacker.position, stats.spawnPosition) > stats.aggroRadius * 1.5f) return;

        // Đánh thức AI
        isReturningHome = false;
        if (agent.isOnNavMesh) agent.isStopped = false;

        // --- LOGIC KHÓA MỤC TIÊU (STICKY AGGRO) ---
        if (nearestTarget == null)
        {
            // Trường hợp 1: Chưa có mục tiêu -> Kẻ nào đánh trước, kẻ đó làm mục tiêu
            nearestTarget = attacker;
            lastTimeCurrentTargetDamagedMe = Time.time; // Reset bộ đếm 15s
            receiveDamageTimer = cooldownTimerReceiveDame;
            Debug.Log($"[AI] Bị đánh lén! Khóa mục tiêu mới: {attacker.name}");
        }
        else
        {
            // Trường hợp 2: ĐÃ CÓ MỤC TIÊU
            if (attacker == nearestTarget)
            {
                // Người đánh chính là mục tiêu đang truy đuổi -> Cập nhật lại thời điểm bị đánh
                lastTimeCurrentTargetDamagedMe = Time.time;
                receiveDamageTimer = cooldownTimerReceiveDame;
            }
            else
            {
                // Người đánh LÀ MỘT KẺ KHÁC (Ví dụ: Pet cắn)
                // Kiểm tra xem mục tiêu hiện tại (Player) có đang "bỏ trốn/không đánh" quá 15s không?
                if (Time.time > lastTimeCurrentTargetDamagedMe + stats.targetPatienceTime)
                {
                    // Chuyển mục tiêu sang kẻ vừa đánh!
                    Debug.Log($"[AI] {nearestTarget.name} không đánh quá {stats.targetPatienceTime}s. Chuyển mục tiêu sang {attacker.name}!");
                    nearestTarget = attacker;
                    lastTimeCurrentTargetDamagedMe = Time.time;
                    receiveDamageTimer = cooldownTimerReceiveDame;
                }
                else
                {
                    // Mục tiêu hiện tại vẫn đang "nóng" (mới đánh mình gần đây)
                    // -> Mặc kệ kẻ vừa đánh lén, quyết tâm đuổi mục tiêu chính!
                    Debug.Log($"[AI] Bị {attacker.name} cắn, nhưng vẫn quyết tâm đuổi {nearestTarget.name}!");
                }
            }
        }
    }

    // --- POST-ATTACK BEHAVIOR ---
    // Được gọi trong thời gian hồi đòn thường. Tùy tình huống enemy sẽ:
    // lùi ra, xoay vòng, hoặc đứng tại chỗ mặt nhìn player chờ thời.
    void HandlePostAttackBehavior(float distToTarget)
    {
        // Quá xa → áp sát (reset về chase, không giữ behavior cũ)
        if (distToTarget > combat.basicAttackRange * 2.0f)
        {
            _postAttackBehaviorChosen = false;
            RestoreNormalSpeed();
            State_Chase();
            return;
        }

        // Quá gần (player áp sát) → lùi ra (override behavior hiện tại)
        if (distToTarget < combat.basicAttackRange * 0.6f)
        {
            _postAttackBehaviorChosen = false;
            RestoreNormalSpeed();
            State_BackOff();
            return;
        }

        // Chưa chọn hành động → chọn ngay bây giờ (chỉ chọn 1 lần cho cả cooldown)
        if (!_postAttackBehaviorChosen)
        {
            _postAttackBehaviorChosen = true;
            _postAttackCircleRight = Random.value < 0.5f; // lưu chiều circle
            float roll = Random.value;
            if (roll < backOffChance)
                State_BackOff();
            else if (roll < backOffChance + circleChance)
                State_CircleTarget();
            else
                State_HoldAndFace();
            return;
        }

        // Hành động đã chọn → nếu agent đến đích rồi thì đứng chờ, không roll lại
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            State_HoldAndFace();
    }

    // Lùi ra xa player theo hướng ngược lại
    void State_BackOff()
    {
        if (nearestTarget == null || !agent.isOnNavMesh) return;
        currentState = "Post-Attack BackOff";
        Vector3 dirAway = (transform.position - nearestTarget.position).normalized;
        dirAway.y = 0;
        Vector3 dest = transform.position + dirAway * backOffDistance;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(dest, out hit, backOffDistance + 1f, NavMesh.AllAreas))
        {
            agent.speed = stats.baseMoveSpeed * 0.8f; // lùi ra xa player với 80% tốc độ
            State_MoveTo(hit.position, "Post-Attack BackOff");
        }
        else
            State_HoldAndFace();
    }

    // Xoay vòng quanh player (flanking nhẹ) — 50% tốc độ, chiều đã chọn 1 lần
    void State_CircleTarget()
    {
        if (nearestTarget == null || !agent.isOnNavMesh) return;
        currentState = "Post-Attack Circle";
        Vector3 toTarget = (nearestTarget.position - transform.position).normalized;
        toTarget.y = 0;
        Vector3 circleDir = Vector3.Cross(toTarget, Vector3.up).normalized;
        if (!_postAttackCircleRight) circleDir = -circleDir; // chiều đã cố định từ lúc chọn
        float circleDist = combat.basicAttackRange * 0.8f;
        Vector3 dest = transform.position + circleDir * circleDist;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(dest, out hit, circleDist + 1f, NavMesh.AllAreas))
        {
            agent.speed = stats.baseMoveSpeed * 0.5f;
            State_MoveTo(hit.position, "Post-Attack Circle");
        }
        else
            State_BackOff();
    }

    // Đứng tại chỗ, nhìn player chờ
    void State_HoldAndFace()
    {
        if (!agent.isOnNavMesh) return;
        currentState = "Post-Attack Hold";
        agent.isStopped = true;
        if (nearestTarget != null)
        {
            Vector3 dir = (nearestTarget.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) stats.facingDirection = dir;
        }
    }

    // --- BOSS DODGE ---
    // Dành cho enemy rank >= 1: lướt né đòn player theo kiểu Genshin rồi phản công ngay
    IEnumerator BossDodgeRoutine()
    {
        if (nearestTarget == null) yield break;

        isBossDodging = true;
        lastBossDodgeTime = Time.time;
        stats.isInvincible = true;
        currentState = "Boss Dodging";

        // Dodge vuông góc với hướng tấn công của player
        Vector3 toEnemy = (transform.position - nearestTarget.position).normalized;
        toEnemy.y = 0;
        Vector3 dodgeDir = Vector3.Cross(toEnemy, Vector3.up).normalized;
        if (Random.value < 0.5f) dodgeDir = -dodgeDir;

        // Tìm điểm đích hợp lệ trên NavMesh
        float dodgeDist = 2.5f;
        Vector3 dodgeDest = transform.position + dodgeDir * dodgeDist;
        NavMeshHit nmHit;
        if (NavMesh.SamplePosition(dodgeDest, out nmHit, dodgeDist + 1f, NavMesh.AllAreas))
            dodgeDest = nmHit.position;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = stats.baseMoveSpeed * 3.5f;
            agent.SetDestination(dodgeDest);
        }

        // Giữ mặt nhìn player trong lúc lướt
        Vector3 faceDir = (nearestTarget.position - transform.position).normalized;
        faceDir.y = 0;
        if (faceDir != Vector3.zero)
        {
            stats.facingDirection = faceDir;
            HandleAnimation(faceDir);
        }

        // I-frames trong suốt dodge (giống player dash)
        yield return new WaitForSeconds(0.3f);

        stats.isInvincible = false;
        isBossDodging = false;

        if (agent.isOnNavMesh)
        {
            agent.speed = stats.baseMoveSpeed;
            agent.isStopped = true;
        }

        // Ngay sau khi dodge xong → phản công nếu player vẫn trong tầm
        yield return new WaitForSeconds(0.1f);

        if (nearestTarget != null && !stats.isDead && !stats.isStunned)
        {
            float dist = Vector3.Distance(transform.position, nearestTarget.position);
            if (dist <= combat.basicAttackRange + 1.5f) State_Attack();
            else State_Chase();
        }
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