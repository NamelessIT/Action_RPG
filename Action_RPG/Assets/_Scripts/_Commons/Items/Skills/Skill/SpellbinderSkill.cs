using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

/// <summary>
/// SpellbinderSkill — "Arcane Conduit".
/// Leonard truyền năng lượng quá tải vào Companion: Companion nhận lá chắn (500% INT) trong 4s,
/// khiêu khích địch và tự lao vào nơi đông kẻ địch nhất. Khi tới nơi / hết 4s / vỡ giáp →
/// lá chắn phát nổ gây 300% magicAtk + choáng AOE. Sau khi nổ, địch trong vùng quay sang nhắm Player.
/// </summary>
public class SpellbinderSkill : SkillBehavior
{
    [Header("Phase 1: Quá tải & Lá chắn")]
    [Tooltip("Lá chắn = INT × hệ số (5.0 → 100 INT = 500 shield)")]
    public float shieldIntMultiplier = 5.0f;
    public float overloadDuration = 4.0f;

    [Header("Phase 2: Khiêu khích & Di chuyển")]
    public float tauntRadius = 15.0f;
    public float searchMobRadius = 20.0f;
    public float clusterRadius = 4.0f;
    public float companionSpeedMult = 1.5f;

    [Header("Phase 3: Phát nổ")]
    public float explosionRadius = 5.0f;
    public float explosionDamageMult = 3.0f; // 300% magicAtk
    public float stunDuration = 1.5f;

    [Header("VFX (tuỳ chọn)")]
    public GameObject castBeamVfx;
    public GameObject overloadAuraVfx;
    public GameObject explosionVfx;

    private EquipmentManager equipmentManager;

    private float _effOverloadDur;
    private float _effStunDur;
    private float _effExplosionMult;

    // Vũ khí ảo loại Magic để ép sát thương PHÉP bất kể vũ khí đang cầm.
    private static WeaponData _magicProxy;
    private static WeaponData MagicProxy
    {
        get
        {
            if (_magicProxy == null)
            {
                _magicProxy = ScriptableObject.CreateInstance<WeaponData>();
                _magicProxy.weaponAtkType = WeaponData.WeaponAtkType.Magic;
            }
            return _magicProxy;
        }
    }

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        CompanionAI companion = FindFirstObjectByType<CompanionAI>();
        if (companion == null)
        {
            Debug.Log("<color=red>SPELLBINDER: Không có Companion để cường hóa!</color>");
            return false;
        }

        if (!base.Use()) return false;

        // Mage U1: +20% thời gian lá chắn | Mage U3 + Catalyst U3: +20% sát thương nổ | Catalyst U1: +20% choáng
        float mU1 = stats != null ? stats.mageSkillU1     : 0f;
        float mU3 = stats != null ? stats.mageSkillU3     : 0f;
        float cU1 = stats != null ? stats.catalystSkillU1 : 0f;
        float cU3 = stats != null ? stats.catalystSkillU3 : 0f;

        _effOverloadDur   = overloadDuration   * (1f + mU1);
        _effStunDur       = stunDuration       * (1f + cU1);
        _effExplosionMult = explosionDamageMult * (1f + mU3 + cU3);

        StartCoroutine(SpellbinderRoutine(companion));
        return true;
    }

    private IEnumerator SpellbinderRoutine(CompanionAI companion)
    {
        player.isAttacking = true;

        Stats compStats = companion.GetComponent<Stats>();
        NavMeshAgent compAgent = companion.GetComponent<NavMeshAgent>();
        bool wasAIEnabled = companion.enabled;
        GameObject shieldAura = null;

        try
        {
            // ---------- 1. LÁ CHẮN (scale theo INT) ----------
            if (castBeamVfx) Instantiate(castBeamVfx, transform.position, Quaternion.LookRotation((companion.transform.position - transform.position).normalized));
            float appliedShield = stats.INT * shieldIntMultiplier;
            compStats.AddShield(appliedShield, _effOverloadDur);
            Debug.Log($"<color=cyan>SPELLBINDER:</color> +{appliedShield:F0} shield cho Companion ({_effOverloadDur:F1}s)");

            shieldAura = overloadAuraVfx ? Instantiate(overloadAuraVfx, companion.transform)
                                         : MageVfxHelper.AttachSphere(companion.transform, 1.4f, new Color(0.3f, 0.6f, 1f, 0.3f));

            // ---------- 2. KHIÊU KHÍCH ĐỊCH VÀO COMPANION ----------
            TauntEnemiesToCompanion(companion);

            // ---------- 3. TÌM CỤM ĐÔNG KẺ ĐỊCH NHẤT ----------
            Vector3 targetPosition = FindDensestCluster(companion.transform.position);

            // ---------- 4. CHIẾM QUYỀN & LÙA COMPANION VÀO CỤM ----------
            companion.enabled = false;
            if (compAgent && compAgent.isOnNavMesh)
            {
                compAgent.isStopped = false;
                compAgent.speed *= companionSpeedMult;
                compAgent.SetDestination(targetPosition);
            }

            yield return new WaitForSeconds(0.2f);
            player.isAttacking = false;

            // ---------- 5. CHỜ TỚI CỤM / HẾT GIỜ / VỠ GIÁP ----------
            float timer = 0f;
            while (timer < _effOverloadDur)
            {
                if (compAgent && compAgent.isOnNavMesh && !compAgent.pathPending && compAgent.remainingDistance <= 1.5f) break;
                if (compStats.currentShield <= 0) break;
                timer += Time.deltaTime;
                yield return null;
            }

            // ---------- 6. KÍCH NỔ ----------
            ExecuteExplosion(companion.transform.position);
            if (compStats.currentShield > 0) compStats.currentShield = 0;
        }
        finally
        {
            if (compAgent && compAgent.isOnNavMesh)
            {
                compAgent.speed /= companionSpeedMult;
                compAgent.isStopped = true;
                compAgent.ResetPath();
            }
            companion.enabled = wasAIEnabled;
            if (shieldAura != null) Destroy(shieldAura);
            player.isAttacking = false;
        }
    }

    private void TauntEnemiesToCompanion(CompanionAI companion)
    {
        Collider[] hits = Physics.OverlapSphere(companion.transform.position, tauntRadius, player.dangerLayer);
        HashSet<EnemyAI> seen = new HashSet<EnemyAI>();
        foreach (var hit in hits)
        {
            EnemyAI ai = hit.GetComponentInParent<EnemyAI>();
            if (ai == null || !seen.Add(ai)) continue;
            ai.OnDamageTaken(companion.transform);     // đánh thức + aggro sang Companion
            ai.nearestTarget = companion.transform;
        }
    }

    private Vector3 FindDensestCluster(Vector3 fallback)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, searchMobRadius, player.dangerLayer);
        List<Stats> enemies = new List<Stats>();
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e != null && e.currentHp > 0 && seen.Add(e)) enemies.Add(e);
        }
        if (enemies.Count == 0) return fallback;

        int bestCount = -1;
        Vector3 best = fallback;
        foreach (var a in enemies)
        {
            int count = 0;
            foreach (var b in enemies)
                if (Vector3.Distance(a.transform.position, b.transform.position) <= clusterRadius) count++;
            if (count > bestCount) { bestCount = count; best = a.transform.position; }
        }
        Debug.Log($"<color=orange>SMART BOMB:</color> Cụm đông nhất {bestCount} mục tiêu — lùa Companion tới!");
        return best;
    }

    private void ExecuteExplosion(Vector3 center)
    {
        Debug.Log("<color=red>SPELLBINDER: QUÁ TẢI PHÁT NỔ!</color>");
        if (explosionVfx) Instantiate(explosionVfx, center, Quaternion.identity);
        VisualDebugHelper.DrawSphere(center, explosionRadius, new Color(0.6f, 0.2f, 1f, 0.4f), 0.4f);

        stats.EnterCombat();
        Collider[] hits = Physics.OverlapSphere(center, explosionRadius, player.dangerLayer);
        HashSet<Stats> done = new HashSet<Stats>();
        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !done.Add(e)) continue;

            // 300% magicAtk (ép phép qua MagicProxy) + choáng + phá siêu giáp.
            DamageHelper.ApplyStandardDamage(stats, e, transform, _effExplosionMult, null, MagicProxy, 2, true, _effStunDur);

            // Sau khi hết choáng → kéo target về Player.
            EnemyAI ai = e.GetComponent<EnemyAI>() ?? e.GetComponentInParent<EnemyAI>();
            if (ai != null) StartCoroutine(AggroPlayerAfterStunRoutine(ai, _effStunDur));
        }
    }

    private IEnumerator AggroPlayerAfterStunRoutine(EnemyAI ai, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ai == null) yield break;

        Stats es = ai.GetComponent<Stats>() ?? ai.GetComponentInParent<Stats>();
        if (es == null || es.isDead) yield break;

        ai.OnDamageTaken(player.transform);     // đánh thức + aggro về Player
        ai.nearestTarget = player.transform;
        Debug.Log($"<color=orange>[Spellbinder]</color> {es.name} tỉnh choáng → quay sang Player!");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, searchMobRadius);
    }
}
