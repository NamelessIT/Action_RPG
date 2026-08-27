using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Features.Companion; // [003-E] For CompanionVisionManager

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AllyStats))] // Thú cưng thuộc phe Ally
public class CompanionAI : MonoBehaviour
{
    // ---------------Truy cap chung---------------
    private static CompanionAI _current;

    /// <summary>
    /// Companion đang có trong scene. Thay cho FindFirstObjectByType&lt;CompanionAI&gt;() rải khắp code:
    /// lần đầu vẫn quét scene, các lần sau lấy từ cache.
    /// Companion bị huỷ thì cache tự rỗng (Unity coi object đã destroy là null) nên lần gọi kế tiếp quét lại.
    /// </summary>
    public static CompanionAI Current
    {
        get
        {
            if (_current == null) _current = FindFirstObjectByType<CompanionAI>();
            return _current;
        }
    }

    [Header("--- References ---")]
    public Transform player;
    private NavMeshAgent agent;
    private AllyStats stats;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    
    // [003-E] Vision system integration
    private CompanionVisionManager _visionManager;

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
    // Tầm đánh do Protocol (Slot 1) quyết định; FALLBACK dùng khi chưa trang bị Protocol.
    private const float FALLBACK_RANGE = 2.0f;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;

    [Header("--- Matrix Dodge ---")]
    // [GIỮ LẠI] Chế độ né: hiện CHỈ né bằng DASH (đã ổn). Mở lại nếu muốn thêm chế độ chạy né.
    // public bool dodgeByDash = true;
    public float dodgeDistance = 3f;       // quãng đường né
    public float dashDodgeTime = 0.16f;    // thời lượng dash
    private bool isDodging = false;

    // [COMPANION EQUIPMENT] Hệ trang bị riêng. Protocol → behavior tấn công; Matrix → dodge/aggro.
    private CompanionEquipmentManager _equipment;
    private CompanionAttackBehavior Behavior => _equipment != null ? _equipment.CurrentBehavior : null;
    private int _aegisHitCount = 0;     // đếm đòn cho Aegis (đòn thứ 3 hất tung)
    private LayerMask obstacleMask;     // layer "Obstacle" — chặn đường ngắm/đạn của đòn xa

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
        _equipment = GetComponent<CompanionEquipmentManager>(); // optional

        // [COMPANION SKILL] Tự gắn bộ điều phối kỹ năng (Passive/Skill/Signature theo nguyên mẫu).
        if (GetComponent<CompanionSkillController>() == null)
            gameObject.AddComponent<CompanionSkillController>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.baseMoveSpeed > 0 ? stats.baseMoveSpeed : 5f;

        if (player == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) player = pObj.transform;
        }

        // Chống bị ĐẨY: NavMeshAgent đã tự lo di chuyển → để Rigidbody Kinematic cho khỏi bị
        // Player/Enemy húc văng. Đồng thời ưu tiên tránh né cao để agent khác nhường đường.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        agent.avoidancePriority = 30;
        obstacleMask = LayerMask.GetMask("Obstacle"); // 0 nếu chưa có layer này (không chặn)

        // Matrix dodge: né đòn định hướng của địch (không đụng damageInterceptor để khỏi xung đột skill).
        stats.OnBeforeTakeDamage += HandleMatrixDodge;

        currentVisualDir = Vector3.back;
        
        // [003-E] Initialize companion vision manager
        _visionManager = GetComponent<CompanionVisionManager>();
        if (_visionManager == null)
        {
            // [003-E] Auto-create if not present
            _visionManager = gameObject.AddComponent<CompanionVisionManager>();
            Debug.Log("[003-E] CompanionVisionManager was auto-created and attached to Companion.");
        }
        else
        {
            Debug.Log("[003-E] CompanionVisionManager already attached to Companion.");
        }
    }

    void Update()
    {
        if (stats.isDead || stats.isStunned || player == null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // [SLOW] Companion di chuyển theo baseMoveSpeed × EffectiveSlowMultiplier (cập nhật mỗi frame; 1 nếu không Slow).
        // Giữ fallback 5f như lúc init (tránh agent.speed=0 nếu baseMoveSpeed chưa set).
        if (agent.enabled && agent.isOnNavMesh)
            agent.speed = (stats.baseMoveSpeed > 0 ? stats.baseMoveSpeed : 5f) * stats.EffectiveSlowMultiplier;

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

        // [MATRIX DODGE] Đang dash né → để pha né chạy, không chen AI khác.
        if (isDodging) return;

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
            HandleCombat(); // HandleCombat tự xử lý Root: đứng yên, chỉ đánh nếu địch đã trong tầm.
        }
        // [ROOT] Không có mục tiêu mà đang bị trói chân → đứng yên hoàn toàn, KHÔNG follow/wander/pathfind.
        else if (stats.IsRooted)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; agent.ResetPath(); }
            isWandering = false;
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
        if (markedTargets.Count == 0) { currentTarget = null; return; }

        // [COMPANION EQUIPMENT] Nếu có Attack Module → chọn mục tiêu theo role
        // (Sniper: máu thấp/xa, Control: cụm đông, Vanguard: enemy to...). Không có → gần nhất (fallback).
        var behavior = Behavior;
        if (behavior != null)
        {
            behavior.Player = player; // Aegis cần biết "ai đang đánh player"
            _targetBuffer.Clear();
            foreach (Transform t in markedTargets) if (t != null) _targetBuffer.Add(t);
            currentTarget = behavior.PickTarget(transform.position, _targetBuffer);
            return;
        }

        float minDistance = Mathf.Infinity;
        Transform bestTarget = null;
        foreach (Transform target in markedTargets)
        {
            if (target == null) continue;
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist < minDistance) { minDistance = dist; bestTarget = target; }
        }
        currentTarget = bestTarget;
    }

    // Buffer tái sử dụng cho PickTarget (tránh alloc mỗi frame)
    private readonly List<Transform> _targetBuffer = new List<Transform>();

    void HandleCombat()
    {
        float distToTarget = Vector3.Distance(transform.position, currentTarget.position);

        // [ROOT] Bị trói chân → KHÔNG di chuyển/kite; nhưng nếu mục tiêu đã trong tầm thì vẫn đánh.
        if (stats.IsRooted)
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            float effRangeR = Behavior != null ? Behavior.AttackRange : FALLBACK_RANGE;
            if (distToTarget <= effRangeR)
            {
                float spd = stats.attackSpeed > 0 ? stats.attackSpeed : 1.0f;
                spd = Mathf.Max(0.01f, spd * stats.EffectiveSlowMultiplier); // [SLOW] giảm cadence đánh
                if (Time.time >= lastAttackTime + (1.0f / spd)) StartCoroutine(AttackRoutine(spd));
                Vector3 d = (currentTarget.position - transform.position).normalized; d.y = 0; currentVisualDir = d;
            }
            return;
        }

        // [COMPANION EQUIPMENT] Behavior theo role có thể yêu cầu KITE (lùi ra) khi địch quá gần.
        var behavior = Behavior;
        if (behavior != null)
        {
            float hpPercent = stats.maxHp > 0 ? stats.currentHp / stats.maxHp : 1f;
            if (behavior.ShouldFlee(distToTarget, hpPercent))
            {
                // Lùi ra xa mục tiêu để giữ DesiredRange (Sniper/Control)
                agent.isStopped = false;
                agent.stoppingDistance = 0f;
                Vector3 away = (transform.position - currentTarget.position).normalized;
                away.y = 0;
                Vector3 dest = transform.position + away * behavior.DesiredRange;
                if (NavMesh.SamplePosition(dest, out NavMeshHit kh, 3f, NavMesh.AllAreas))
                    agent.SetDestination(kh.position);
                currentVisualDir = (currentTarget.position - transform.position).normalized; // vẫn nhìn địch
                return;
            }
        }

        // Tầm đánh hiệu lực: theo Protocol behavior nếu có.
        float effRange = behavior != null ? behavior.AttackRange : FALLBACK_RANGE;

        // Đòn XA chỉ bắn khi KHÔNG bị tường/vật cản chắn (LoS). Bị chắn → di chuyển để lấy góc.
        bool hasLoS = true;
        if (behavior != null && behavior.IsRanged && obstacleMask.value != 0)
        {
            Vector3 fromP = transform.position + Vector3.up * 0.3f;
            Vector3 toP = currentTarget.position; toP.y = fromP.y;
            if (Physics.Linecast(fromP, toP, obstacleMask)) hasLoS = false;
        }

        if (distToTarget <= effRange && hasLoS)
        {
            agent.isStopped = true;
            // Cooldown theo tốc độ đánh (baseAttackSpeed đã được Protocol cộng vào stats).
            float speed = stats.attackSpeed > 0 ? stats.attackSpeed : 1.0f;
            speed = Mathf.Max(0.01f, speed * stats.EffectiveSlowMultiplier); // [SLOW] giảm cadence đánh
            if (Time.time >= lastAttackTime + (1.0f / speed))
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
            agent.stoppingDistance = effRange * 0.8f;

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

        ExecuteProtocolAttack();

        // Đợi nốt phần animation còn lại thu tay về
        yield return new WaitForSeconds(timeToRecover);

        isAttacking = false;
    }

    // Thực thi đòn đánh theo Protocol (Slot 1): Artillery/Suppression bắn đạn, Carnage/Aegis quét AoE.
    private void ExecuteProtocolAttack()
    {
        if (currentTarget == null) return;

        var behavior = Behavior;
        var protocol = _equipment != null ? _equipment.Protocol : null;
        bool isMagic = protocol != null && protocol.atkType == CompanionAtkType.Magic;
        // Bắn NGANG MẶT ĐẤT (thấp) để trúng cả kẻ địch lùn.
        Vector3 spawn = transform.position + Vector3.up * 0.3f;

        // ── ĐÁNH XA (Artillery / Suppression) ──
        if (behavior != null && behavior.IsRanged)
        {
            if (behavior.ProtocolType == CompanionProtocolType.Artillery)
            {
                Stats tgt = currentTarget.GetComponentInParent<Stats>();
                if (tgt == null || tgt.isDead) return;
                var go = new GameObject("Companion_Artillery");
                go.transform.position = spawn;
                go.AddComponent<CompanionLockProjectile>()
                  .Init(stats, tgt, isMagic, 18f, new Color(1f, 0.8f, 0.2f, 1f));
            }
            else // Suppression — đạn KHOÁ MỤC TIÊU (homing) nhưng bị địch khác/tường chặn, nổ AoE
            {
                Stats tgt = currentTarget.GetComponentInParent<Stats>();
                if (tgt == null || tgt.isDead) return;
                var go = new GameObject("Companion_Suppression");
                go.transform.position = spawn;
                go.AddComponent<CompanionSeekProjectile>()
                  .Init(stats, tgt, isMagic, 14f, behavior.DesiredRange + 3f,
                        behavior.AoeRadius, 0.6f, new Color(0.7f, 0.3f, 1f, 1f));
            }
            return;
        }

        // ── CẬN CHIẾN QUÉT AoE (Carnage / Aegis) — nhận diện địch theo tag ──
        bool knockup = behavior != null && behavior.DoesKnockup && (++_aegisHitCount % 5 == 0);
        float radius = behavior != null ? Mathf.Max(behavior.AoeRadius, 1f) : FALLBACK_RANGE;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = CompanionCombat.GetEnemy(h);
            if (e == null || !seen.Add(e)) continue;
            // Aegis đòn thứ 5: gây sát thương + HẤT TUNG 0.5s (Airborne thật).
            CompanionCombat.DealHit(stats, e, transform, isMagic, knockup ? 1 : 0);
            if (knockup) e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Airborne, 0.5f) { respectEffectResistance = false }, stats);
        }
        if (knockup) Debug.Log("[Companion-Aegis] Đòn thứ 5 — HẤT TUNG 0.5s!");
    }

    // Matrix dodge: né đòn ĐỊNH HƯỚNG của kẻ địch (zero damage trong DamageInfo, không đụng interceptor).
    private void HandleMatrixDodge(DamageInfo info)
    {
        if (info == null || stats.isDead) return;
        if (info.attacker == null || !info.attacker.CompareTag("Enemy")) return; // chỉ né đòn của địch
        if (isDodging) return; // đang né rồi

        float chance = _equipment != null ? _equipment.DodgeChance : 0.25f;
        if (chance <= 0f) return; // Deflector: không né
        if (Random.value > chance) return;

        // Né thành công → vô hiệu đòn này.
        info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f;
        info.ClearCombatEffects(); // né thành công → đòn không còn CC (effects list + legacy)

        // Hướng né: ra xa kẻ tấn công.
        Vector3 away = transform.position - info.sourcePosition; away.y = 0;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();
        Vector3 dest = transform.position + away * dodgeDistance;
        if (NavMesh.SamplePosition(dest, out NavMeshHit nh, 2f, NavMesh.AllAreas)) dest = nh.position;

        StartCoroutine(DashDodgeRoutine(dest)); // luôn né bằng DASH (lerp mượt, không dịch chuyển tức thời)
        Debug.Log("<color=cyan>[Companion] NÉ ĐÒN!</color>");
    }

    // DASH né: lerp mượt ra điểm né trong dashDodgeTime (không teleport).
    private IEnumerator DashDodgeRoutine(Vector3 dest)
    {
        isDodging = true;
        bool agentWas = agent != null && agent.enabled;
        if (agentWas) { agent.isStopped = true; agent.enabled = false; }

        Vector3 start = transform.position;
        Vector3 faceDir = (dest - start); faceDir.y = 0;
        if (faceDir != Vector3.zero) { currentVisualDir = faceDir.normalized; UpdateAnimationDirection(currentVisualDir); }

        float t = 0f;
        float dur = Mathf.Max(0.05f, dashDodgeTime);
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, dest, Mathf.Clamp01(t / dur));
            yield return null;
        }
        transform.position = dest;

        if (agentWas)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(dest);
            agent.isStopped = false;
        }
        isDodging = false;
    }

    private void OnDestroy()
    {
        if (_current == this) _current = null;

        if (stats != null) stats.OnBeforeTakeDamage -= HandleMatrixDodge;
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
            // [ROOT] Đang trói chân → KHÔNG bật agent chạy tới target (chỉ đổi target để đánh nếu đã trong tầm).
            agent.isStopped = stats.IsRooted;
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