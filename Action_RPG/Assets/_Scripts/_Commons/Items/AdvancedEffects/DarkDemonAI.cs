using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// WPN_GR_T5_01: Ác Quỷ Bóng Tối — minion tạm thời triệu hồi khi hạ kẻ địch.
/// • Tối đa 5 demon hoạt động cho mỗi owner.
/// • Stat snapshot khi spawn: maxHp = 20% maxHp owner, magicAtk = 20% magicAtk owner,
///   attackSpeed = 1, moveSpeed = 5, tầm đánh = 5.
/// • Đánh xa bằng đạn target-lock (CompanionLockProjectile), sát thương phép.
/// • Tag "Ally" → EnemyStats.IsHostileTo coi là mục tiêu hợp lệ.
/// • Dùng hệ CC mới: Stun/Displacement khóa cứng; Root chặn di chuyển; Taunt cưỡng chế chase + basic attack
///   Taunter; Fear cưỡng chế chạy khỏi source với 50% EffectiveMoveSpeed.
/// • Không tự đổi target nền khi bị Taunt/Fear; khi CC hết sẽ quay lại scan enemy bình thường.
/// Visual hiện vẫn là placeholder runtime qua MageVfxHelper.
/// </summary>
[RequireComponent(typeof(AllyStats))]
public class DarkDemonAI : MonoBehaviour
{
    public const int MaxActivePerOwner = 5;

    private const float ATTACK_RANGE  = 5f;
    private const float SCAN_RADIUS   = 18f;
    private const float SCAN_INTERVAL = 0.4f;

    private static readonly List<DarkDemonAI> ActiveDemons = new List<DarkDemonAI>();

    private AllyStats owner;
    private AllyStats stats;
    private NavMeshAgent agent;
    private ForcedControlRunner forcedControlRunner;
    private Stats target;
    private float lastAttackTime = -10f;
    private float nextScan = 0f;
    private bool wasForcedControlled;

    public AllyStats Owner => owner;
    public AllyStats DemonStats => stats != null ? stats : GetComponent<AllyStats>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveDemons.Clear();
    }

    /// <summary>Tạo 1 Ác Quỷ tại vị trí cho trước, scale theo owner. Trả null nếu owner không hợp lệ hoặc đã đủ 5 con.</summary>
    public static DarkDemonAI Spawn(Vector3 position, AllyStats owner)
    {
        if (owner == null || owner.isDead) return null;

        CleanupRegistry();
        if (GetActiveCount(owner) >= MaxActivePerOwner) return null;

        Vector3 pos = position;
        if (NavMesh.SamplePosition(position, out NavMeshHit nh, 5f, NavMesh.AllAreas))
            pos = nh.position;

        GameObject go = new GameObject("DarkDemon_GR_T5_01");
        go.SetActive(false);
        go.transform.position = pos;
        go.tag = "Ally";

        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.4f;
        col.height = 1.6f;
        col.center = Vector3.up * 0.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var nav = go.AddComponent<NavMeshAgent>();
        nav.speed = 5f;
        nav.radius = 0.4f;
        nav.height = 1.6f;
        nav.updateRotation = false;
        nav.updateUpAxis = false;
        nav.avoidancePriority = 40;

        // Cấu hình AllyStats sao cho RecalculateStats() ra đúng snapshot mong muốn.
        var st = go.AddComponent<AllyStats>();
        st.level = 0;
        st.flatHp = owner.maxHp * 0.2f;
        st.hpPerVIT = 0f;
        st.bonusHp = 0f;
        st.flatBonusMaxHp = 0f;
        st.flatMagicAtk = owner.magicAtk * 0.2f;
        st.flatPhysicalAtk = 0f;
        st.bonusMagicAtk = 0f;
        st.bonusAttackSpeed = 0f;
        st.bonusMoveSpeed = 0f;
        st.moveFlexibility = 0f;
        st.ApplyBaseRuntimeStats(new Stats.BaseStatSnapshot
        {
            initialBaseHp = 0f,
            maxStamina = st.maxStamina
        }, resetCurrentVitals: false);
        st.SetBaseAttackSpeed(1f);
        st.SetBaseMoveSpeed(5f);
        st.flatSTR = st.flatDEX = st.flatINT = st.flatVIT = st.flatAGI = 0f;

        DarkDemonAI demon = go.AddComponent<DarkDemonAI>();
        demon.owner = owner;

        go.SetActive(true);
        Register(demon);

        MageVfxHelper.AttachSphere(go.transform, 0.6f, new Color(0.45f, 0.1f, 0.7f, 1f));
        return demon;
    }

    /// <summary>DEV helper: spawn nhiều demon quanh owner, vẫn tuân max 5/owner.</summary>
    public static int SpawnDebugPack(AllyStats owner, int count)
    {
        if (owner == null || owner.isDead || count <= 0) return 0;

        int spawned = 0;
        int requested = Mathf.Max(0, count);
        for (int i = 0; i < requested; i++)
        {
            float angle = (Mathf.PI * 2f * i) / Mathf.Max(1, requested);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.8f;
            if (Spawn(owner.transform.position + offset, owner) != null)
                spawned++;
        }
        return spawned;
    }

    /// <summary>DEV/helper cleanup: destroy toàn bộ demon của owner. owner=null => destroy tất cả.</summary>
    public static int DestroyAllForOwner(AllyStats owner)
    {
        CleanupRegistry();
        int removed = 0;
        for (int i = ActiveDemons.Count - 1; i >= 0; i--)
        {
            DarkDemonAI demon = ActiveDemons[i];
            if (demon == null) { ActiveDemons.RemoveAt(i); continue; }
            if (owner != null && demon.owner != owner) continue;

            ActiveDemons.RemoveAt(i);
            removed++;
            UnityEngine.Object.Destroy(demon.gameObject);
        }
        return removed;
    }

    public static int GetActiveCount(AllyStats owner = null)
    {
        CleanupRegistry();
        int count = 0;
        foreach (DarkDemonAI demon in ActiveDemons)
        {
            if (demon == null || !demon.gameObject.activeInHierarchy) continue;
            if (owner == null || demon.owner == owner) count++;
        }
        return count;
    }

    public static DarkDemonAI FindNearest(Vector3 origin, AllyStats owner = null)
    {
        CleanupRegistry();
        DarkDemonAI best = null;
        float bestSq = float.PositiveInfinity;
        foreach (DarkDemonAI demon in ActiveDemons)
        {
            if (demon == null || !demon.gameObject.activeInHierarchy) continue;
            if (owner != null && demon.owner != owner) continue;
            AllyStats demonStats = demon.DemonStats;
            if (demonStats == null || demonStats.isDead) continue;

            float sq = (demon.transform.position - origin).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = demon;
            }
        }
        return best;
    }

    private static void Register(DarkDemonAI demon)
    {
        if (demon == null) return;
        CleanupRegistry();
        if (!ActiveDemons.Contains(demon)) ActiveDemons.Add(demon);
    }

    private static void CleanupRegistry()
    {
        ActiveDemons.RemoveAll(d => d == null);
    }

    private void Start()
    {
        stats = GetComponent<AllyStats>();
        agent = GetComponent<NavMeshAgent>();
        forcedControlRunner = stats != null ? stats.ForcedControl : null;

        if (stats != null)
        {
            stats.RecalculateStats();
            stats.currentHp = stats.maxHp;
        }
        Register(this);
    }

    private void OnDestroy()
    {
        ActiveDemons.Remove(this);
    }

    private void Update()
    {
        if (stats == null || stats.isDead)
        {
            Destroy(gameObject);
            return;
        }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        StatusControlSnapshot control = stats.ControlState;

        // Hard-control priority đã được resolver quyết định trước forced control.
        if (control.mode == ControlMode.Stunned || control.mode == ControlMode.Displaced)
        {
            StopMovement();
            return;
        }

        if (ForcedControlRunner.IsForcedMode(control.mode))
        {
            HandleForcedControl();
            return;
        }

        if (wasForcedControlled)
            ExitForcedControl();

        agent.speed = stats.EffectiveMoveSpeed;

        if (Time.time >= nextScan)
        {
            target = FindNearestEnemy();
            nextScan = Time.time + SCAN_INTERVAL;
        }

        if (target == null || target.currentHp <= 0 || !target.gameObject.activeInHierarchy)
        {
            StopMovement();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist <= ATTACK_RANGE)
        {
            StopMovement();
            SetFacing(toTarget);

            float spd = Mathf.Max(0.01f, stats.EffectiveAttackSpeed);
            if (control.canBasicAttack && Time.time >= lastAttackTime + 1f / spd)
            {
                lastAttackTime = Time.time;
                Attack(target);
            }
        }
        else
        {
            if (!control.canMove)
            {
                StopMovement();
                SetFacing(toTarget);
                return;
            }

            agent.isStopped = false;
            agent.stoppingDistance = ATTACK_RANGE * 0.8f;
            agent.SetDestination(target.transform.position);
            SetFacing(toTarget);
        }
    }

    private void HandleForcedControl()
    {
        wasForcedControlled = true;
        if (forcedControlRunner == null) forcedControlRunner = stats.ForcedControl;

        if (forcedControlRunner == null || !forcedControlRunner.TryBuildIntent(out ForcedControlIntent intent))
        {
            StopMovement();
            return;
        }

        Transform anchor = intent.targetTransform;
        if (anchor == null)
        {
            StopMovement();
            return;
        }

        if (intent.mode == ControlMode.Feared)
        {
            if (!intent.canMove || intent.direction.sqrMagnitude <= 0.0001f)
            {
                StopMovement();
                SetFacing(intent.direction);
                return;
            }

            agent.speed = stats.EffectiveMoveSpeed * Mathf.Max(0f, intent.speedMultiplier);
            agent.stoppingDistance = 0f;

            Vector3 desired = transform.position + intent.direction * 5f;
            if (NavMesh.SamplePosition(desired, out NavMeshHit fearHit, 5f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(fearHit.position);
            }
            else
            {
                StopMovement();
            }
            SetFacing(intent.direction);
            return;
        }

        // Taunt: Taunter là primary target cưỡng chế. Không ghi đè target nền.
        Vector3 toAnchor = anchor.position - transform.position;
        toAnchor.y = 0f;
        float distance = toAnchor.magnitude;
        Stats forcedTarget = intent.target;

        if (distance <= ATTACK_RANGE && intent.canBasicAttack && forcedTarget != null
            && !forcedTarget.isDead && forcedTarget.gameObject.activeInHierarchy)
        {
            StopMovement();
            SetFacing(toAnchor);
            float spd = Mathf.Max(0.01f, stats.EffectiveAttackSpeed);
            if (Time.time >= lastAttackTime + 1f / spd)
            {
                lastAttackTime = Time.time;
                Attack(forcedTarget);
            }
            stats.EnterCombat();
            return;
        }

        if (!intent.canMove)
        {
            StopMovement();
            SetFacing(toAnchor);
            return;
        }

        agent.speed = stats.EffectiveMoveSpeed * Mathf.Max(0f, intent.speedMultiplier);
        agent.stoppingDistance = ATTACK_RANGE * 0.8f;
        agent.isStopped = false;
        agent.SetDestination(anchor.position);
        SetFacing(toAnchor);
        stats.EnterCombat();
    }

    private void ExitForcedControl()
    {
        wasForcedControlled = false;
        nextScan = 0f; // reacquire ngay để không đứng chờ hết scan interval cũ.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = stats != null ? stats.EffectiveMoveSpeed : 5f;
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
    }

    private void StopMovement()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (agent.hasPath) agent.ResetPath();
    }

    private void SetFacing(Vector3 dir)
    {
        if (stats == null) return;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            stats.facingDirection = dir.normalized;
    }

    private void Attack(Stats attackTarget)
    {
        if (attackTarget == null || attackTarget.currentHp <= 0) return;

        var go = new GameObject("DarkDemon_Bolt");
        go.transform.position = transform.position + Vector3.up * 0.4f;
        go.AddComponent<CompanionLockProjectile>()
          .Init(stats, attackTarget, true, 16f, new Color(0.5f, 0.05f, 0.7f, 1f));
    }

    private Stats FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, SCAN_RADIUS);
        Stats best = null;
        float min = float.MaxValue;
        foreach (Collider h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !e.CompareTag("Enemy")) continue;
            float d = (transform.position - e.transform.position).sqrMagnitude;
            if (d < min)
            {
                min = d;
                best = e;
            }
        }
        return best;
    }
}
