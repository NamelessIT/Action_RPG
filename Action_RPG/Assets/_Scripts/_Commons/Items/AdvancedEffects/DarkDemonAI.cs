using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// WPN_GR_T5_01: Ác Quỷ Bóng Tối — minion tạm thời triệu hồi khi hạ kẻ địch.
/// • Stat: maxHp = 20% maxHp player, magicAtk = 20% magicAtk player, attackSpeed = 1, moveSpeed = 5, tầm đánh 5f.
/// • Đánh xa (targetSeek) bằng đạn khoá mục tiêu (tái dùng CompanionLockProjectile, sát thương phép).
/// • Tag "Ally" → EnemyStats.IsHostileTo coi là mục tiêu hợp lệ ⇒ kẻ địch tự nhắm & đánh; hết máu → biến mất.
/// • Không bị player target/đánh (player chỉ đánh tag "Enemy") và không nhận buff (không nằm trong lookup của skill).
/// Visual: placeholder runtime (quả cầu tối qua MageVfxHelper).
/// </summary>
[RequireComponent(typeof(AllyStats))]
public class DarkDemonAI : MonoBehaviour
{
    private const float ATTACK_RANGE  = 5f;
    private const float SCAN_RADIUS   = 18f;
    private const float SCAN_INTERVAL = 0.4f;

    private AllyStats stats;
    private NavMeshAgent agent;
    private Stats target;
    private float lastAttackTime = -10f;
    private float nextScan = 0f;

    /// <summary>Tạo 1 Ác Quỷ tại vị trí cho trước, scale theo chỉ số của owner (player). Trả về null nếu thất bại.</summary>
    public static DarkDemonAI Spawn(Vector3 position, AllyStats owner)
    {
        if (owner == null) return null;

        Vector3 pos = position;
        if (NavMesh.SamplePosition(position, out NavMeshHit nh, 5f, NavMesh.AllAreas)) pos = nh.position;

        GameObject go = new GameObject("DarkDemon_GR_T5_01");
        go.SetActive(false);
        go.transform.position = pos;
        go.tag = "Ally"; // để kẻ địch coi là mục tiêu (EnemyStats.IsHostileTo)

        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.4f; col.height = 1.6f; col.center = Vector3.up * 0.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        var nav = go.AddComponent<NavMeshAgent>();
        nav.speed = 5f; nav.radius = 0.4f; nav.height = 1.6f;
        nav.updateRotation = false; nav.updateUpAxis = false;
        nav.avoidancePriority = 40;

        // Cấu hình AllyStats sao cho RecalculateStats() ra đúng số mong muốn (mọi attribute = 0).
        var st = go.AddComponent<AllyStats>();
        st.level = 0;
        st.flatHp = owner.maxHp * 0.2f; st.hpPerVIT = 0f; st.bonusHp = 0f; st.flatBonusMaxHp = 0f;
        st.flatMagicAtk = owner.magicAtk * 0.2f; st.flatPhysicalAtk = 0f; st.bonusMagicAtk = 0f;
        st.bonusAttackSpeed = 0f; st.bonusMoveSpeed = 0f; st.moveFlexibility = 0f;
        // [P2-DATA-01B] base stats qua API (maxStamina giữ mặc định — DarkDemon không dùng stamina).
        st.ApplyBaseRuntimeStats(new Stats.BaseStatSnapshot { initialBaseHp = 0f, maxStamina = st.maxStamina }, resetCurrentVitals: false);
        st.SetBaseAttackSpeed(1f);
        st.SetBaseMoveSpeed(5f);
        st.flatSTR = st.flatDEX = st.flatINT = st.flatVIT = st.flatAGI = 0f;

        go.AddComponent<DarkDemonAI>();
        go.SetActive(true);

        MageVfxHelper.AttachSphere(go.transform, 0.6f, new Color(0.45f, 0.1f, 0.7f, 1f));
        return go.GetComponent<DarkDemonAI>();
    }

    void Start()
    {
        stats = GetComponent<AllyStats>();
        agent = GetComponent<NavMeshAgent>();
        stats.RecalculateStats();
        stats.currentHp = stats.maxHp; // RecalculateStats kẹp currentHp về 0 → đổ đầy lại
    }

    void Update()
    {
        if (stats == null || stats.isDead) { Destroy(gameObject); return; }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (stats.isStunned) { agent.isStopped = true; return; }

        if (Time.time >= nextScan)
        {
            target = FindNearestEnemy();
            nextScan = Time.time + SCAN_INTERVAL;
        }

        if (target == null || target.currentHp <= 0)
        {
            agent.isStopped = true;
            return;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist <= ATTACK_RANGE)
        {
            agent.isStopped = true;
            stats.facingDirection = (target.transform.position - transform.position).normalized;

            float spd = stats.attackSpeed > 0f ? stats.attackSpeed : 1f;
            if (Time.time >= lastAttackTime + 1f / spd)
            {
                lastAttackTime = Time.time;
                Attack();
            }
        }
        else
        {
            agent.isStopped = false;
            agent.stoppingDistance = ATTACK_RANGE * 0.8f;
            agent.SetDestination(target.transform.position);
        }
    }

    private void Attack()
    {
        if (target == null) return;
        var go = new GameObject("DarkDemon_Bolt");
        go.transform.position = transform.position + Vector3.up * 0.4f;
        go.AddComponent<CompanionLockProjectile>()
          .Init(stats, target, true, 16f, new Color(0.5f, 0.05f, 0.7f, 1f)); // isMagic = true
    }

    private Stats FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, SCAN_RADIUS);
        Stats best = null;
        float min = float.MaxValue;
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !e.CompareTag("Enemy")) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < min) { min = d; best = e; }
        }
        return best;
    }
}
