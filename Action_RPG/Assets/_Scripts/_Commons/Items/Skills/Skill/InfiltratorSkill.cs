using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// InfiltratorSkill — "Synchronized Strike".
/// Leonard tàng hình 4s. Đòn đánh KẾ TIẾP sẽ lướt tới kẻ địch gần nhất, chém + giảm
/// Armor/Magic Resist + đánh dấu. Companion lập tức dash tới làm choáng + gây sát thương.
/// Leonard lướt ra sau lưng bồi 1 đòn backstab chí mạng.
/// Nếu KHÔNG có mục tiêu trong tầm khi bấm → KHÔNG tiêu hao buff (vẫn giữ tàng hình).
/// </summary>
public class InfiltratorSkill : SkillBehavior
{
    [Header("Phase 1: Tàng hình")]
    public float invisibilityDuration = 4.0f;
    public float engageSearchRadius = 5.0f;

    [Header("Tốc độ lướt (thời gian lướt — càng lớn càng chậm)")]
    public float playerDashDuration = 0.28f;
    public float companionDashDuration = 0.28f;

    [Header("Phase 2: Lướt chém + Đánh dấu (debuff)")]
    public float engageDamageMult = 1.0f;        // đòn chém khi lướt tới (đánh thường)
    public float armorShredPercent = 0.3f;       // -30% Armor
    public float magicResistShredPercent = 0.3f; // -30% Magic Resist
    public float debuffDuration = 3.0f;

    [Header("Phase 3: Companion đánh")]
    public float companionDamageMult = 2.0f;     // 200% physicalAtk của Companion
    public float stunDuration = 1.5f;

    [Header("Phase 4: Backstab")]
    public float backstabMultiplier = 3.5f;      // 350% physicalAtk của Leonard

    [Header("VFX (tuỳ chọn)")]
    public GameObject invisAuraVfx;
    public GameObject markVfx;
    public GameObject companionStrikeVfx;
    public GameObject backstabVfx;

    private SpriteRenderer playerSprite;
    private EquipmentManager equipmentManager;
    private Rigidbody rb;
    private Coroutine invisCoroutine;
    private GameObject currentInvisVfx;
    private bool isPhantomStrikeReady = false;

    private float _effInvisDur;
    private float _effStunDur;
    private float _effEngageMult;
    private float _effBackstabMult;
    private float _effCompanionDmgMult;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        playerSprite = myPlayer.GetComponentInChildren<SpriteRenderer>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        rb = myPlayer.GetComponent<Rigidbody>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { BreakInvisibility(true); }

    void Update()
    {
        if (!isPhantomStrikeReady || !Input.GetMouseButtonDown(0)) return;

        Stats target = FindNearestEnemy();
        if (target == null)
        {
            // Không có mục tiêu → KHÔNG tiêu hao buff, vẫn giữ tàng hình để chờ.
            Debug.Log("<color=gray>Infiltrator: Không có mục tiêu trong tầm — giữ nguyên buff.</color>");
            return;
        }

        isPhantomStrikeReady = false;
        player.isUsingSpecialSkill = true; // khóa ngay để click này không kích đòn đánh thường
        BreakInvisibility(false);
        StartCoroutine(ExecutionComboRoutine(target));
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // Rogue U1: +20% tàng hình | Rogue U3: +20% sát thương
        // Catalyst U1: +20% choáng | Catalyst U3: +20% sát thương
        float rU1 = stats != null ? stats.rogueSkillU1    : 0f;
        float rU3 = stats != null ? stats.rogueSkillU3    : 0f;
        float cU1 = stats != null ? stats.catalystSkillU1 : 0f;
        float cU3 = stats != null ? stats.catalystSkillU3 : 0f;
        float dmgScale = 1f + rU3 + cU3;

        _effInvisDur         = invisibilityDuration * (1f + rU1);
        _effStunDur          = stunDuration         * (1f + cU1);
        _effEngageMult       = engageDamageMult     * dmgScale;
        _effBackstabMult     = backstabMultiplier   * dmgScale;
        _effCompanionDmgMult = companionDamageMult  * dmgScale;

        if (invisCoroutine != null) StopCoroutine(invisCoroutine);
        invisCoroutine = StartCoroutine(InvisibilityRoutine());
        return true;
    }

    // ==========================================================
    // TÀNG HÌNH
    // ==========================================================
    private IEnumerator InvisibilityRoutine()
    {
        stats.isInvisible = true;
        isPhantomStrikeReady = true;

        if (playerSprite) playerSprite.enabled = false;
        if (invisAuraVfx) currentInvisVfx = Instantiate(invisAuraVfx, transform);
        else currentInvisVfx = MageVfxHelper.AttachSphere(transform, 1.1f, new Color(0.4f, 0.2f, 0.6f, 0.3f));

        Debug.Log($"<color=cyan>INFILTRATOR: Tàng hình {_effInvisDur:F1}s!</color>");
        yield return new WaitForSeconds(_effInvisDur);
        BreakInvisibility(false);
    }

    private void BreakInvisibility(bool isForceCleanup)
    {
        if (!stats.isInvisible && !isForceCleanup) return;

        stats.isInvisible = false;
        isPhantomStrikeReady = false;
        if (playerSprite) playerSprite.enabled = true;
        if (currentInvisVfx) Destroy(currentInvisVfx);

        if (!isForceCleanup) Debug.Log("<color=gray>Infiltrator: Hiện hình.</color>");
    }

    private Stats FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, engageSearchRadius, player.dangerLayer);
        Stats best = null;
        float minDist = float.MaxValue;
        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0) continue;
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < minDist) { minDist = dist; best = e; }
        }
        return best;
    }

    // ==========================================================
    // CHUỖI COMBO: LEONARD → COMPANION → LEONARD
    // ==========================================================
    private IEnumerator ExecutionComboRoutine(Stats target)
    {
        player.isUsingSpecialSkill = true;
        GameObject currentMarkVfx = null;
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        try
        {
            // ---------- NHỊP 1: LƯỚT TỚI CHÉM + GIẢM GIÁP + ĐÁNH DẤU ----------
            Vector3 dirToTarget = (target.transform.position - transform.position);
            dirToTarget.y = 0; dirToTarget.Normalize();

            Collider targetCol = target.GetComponentInChildren<Collider>();
            float tRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;

            Vector3 frontPos = target.transform.position - dirToTarget * (tRadius + 0.5f);
            if (NavMesh.SamplePosition(frontPos, out var navHit, 2.0f, NavMesh.AllAreas)) frontPos = navHit.position;

            player.ForceFaceDirection(dirToTarget);
            yield return DashPlayerTo(frontPos, playerDashDuration);

            Debug.Log($"<color=orange>NHỊP 1:</color> Lướt tới {target.name}, chém + giảm giáp + đánh dấu!");

            target.IsMarked = true;
            currentMarkVfx = markVfx
                ? Instantiate(markVfx, target.transform.position + Vector3.up * 2f, Quaternion.identity, target.transform)
                : MageVfxHelper.AttachSphere(target.transform, 0.6f, new Color(0.7f, 0.2f, 1f, 0.5f));

            StartCoroutine(DebuffRoutine(target));

            stats.EnterCombat();
            DamageHelper.ApplyStandardDamage(stats, target, transform, _effEngageMult, null, currentWpn, 1);

            yield return new WaitForSeconds(0.25f);

            // ---------- NHỊP 2: COMPANION LAO VÀO STUN + GÂY SÁT THƯƠNG ----------
            CompanionAI companion = CompanionAI.Current;
            if (companion != null && target != null && target.currentHp > 0)
            {
                NavMeshAgent compAgent = companion.GetComponent<NavMeshAgent>();
                Vector3 rightSideDir = Quaternion.Euler(0, 90, 0) * dirToTarget;
                Vector3 sidePos = target.transform.position + rightSideDir * (tRadius + 0.5f);
                if (NavMesh.SamplePosition(sidePos, out var compHit, 2.0f, NavMesh.AllAreas)) sidePos = compHit.position;

                yield return DashCompanionTo(companion, compAgent, sidePos, companionDashDuration);

                Vector3 lookDir = (target.transform.position - companion.transform.position); lookDir.y = 0;
                if (lookDir != Vector3.zero) companion.transform.rotation = Quaternion.LookRotation(lookDir.normalized);

                if (companionStrikeVfx) Instantiate(companionStrikeVfx, target.transform.position, Quaternion.identity);
                VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up, 0.6f, new Color(0.2f, 0.8f, 1f, 0.5f), 0.2f);

                AllyStats compStats = companion.GetComponent<AllyStats>();
                if (compStats != null)
                    DamageHelper.ApplyStandardDamage(compStats, target, companion.transform, _effCompanionDmgMult, null, null, 2, true, _effStunDur);

                companion.ForceWait(1.5f);
                Debug.Log("<color=orange>NHỊP 2:</color> Companion lao tới làm choáng + gây sát thương!");
            }

            yield return new WaitForSeconds(0.2f);

            // ---------- NHỊP 3: LEONARD BACKSTAB ----------
            if (target != null && target.currentHp > 0)
            {
                Vector3 enemyForward = target.facingDirection;
                if (enemyForward == Vector3.zero) enemyForward = target.transform.forward;
                enemyForward.y = 0; enemyForward.Normalize();

                Vector3 backPos = target.transform.position - enemyForward * (tRadius + 0.5f);
                if (NavMesh.SamplePosition(backPos, out navHit, 2.0f, NavMesh.AllAreas)) backPos = navHit.position;

                player.ForceFaceDirection(enemyForward);
                yield return DashPlayerTo(backPos, playerDashDuration);

                if (backstabVfx) Instantiate(backstabVfx, target.transform.position, Quaternion.LookRotation(enemyForward));
                VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up, 0.5f, new Color(1f, 0.1f, 0.1f, 0.5f), 0.2f);

                bool isCrit = CombatMath.CheckIsCrit(stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0f));
                DamageHelper.ApplyStandardDamage(stats, target, transform, _effBackstabMult, null, currentWpn, 1, false, 0f, isCrit);
                Debug.Log("<color=red>NHỊP 3 (TẤT SÁT):</color> Backstab chí mạng!");
            }

            yield return new WaitForSeconds(0.2f);
        }
        finally
        {
            if (target != null) target.IsMarked = false;
            if (currentMarkVfx != null) Destroy(currentMarkVfx);
            if (rb != null) rb.linearVelocity = Vector3.zero;
            player.isUsingSpecialSkill = false;
        }
    }

    // Lướt player MƯỢT bằng rb.MovePosition (giữ Rigidbody đồng bộ → không bị kẹt sau skill).
    private IEnumerator DashPlayerTo(Vector3 dest, float duration)
    {
        if (rb == null) { player.transform.position = dest; yield break; }
        Vector3 start = rb.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.fixedDeltaTime;
            rb.MovePosition(Vector3.Lerp(start, dest, Mathf.Clamp01(t / duration)));
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(dest);
        yield return new WaitForFixedUpdate();
    }

    // Lướt companion MƯỢT (tắt agent trong lúc lướt rồi bật lại + Warp để khớp NavMesh).
    private IEnumerator DashCompanionTo(CompanionAI comp, NavMeshAgent agent, Vector3 dest, float duration)
    {
        bool wasEnabled = agent != null && agent.enabled;
        if (wasEnabled) agent.enabled = false;

        Vector3 start = comp.transform.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            comp.transform.position = Vector3.Lerp(start, dest, Mathf.Clamp01(t / duration));
            yield return null;
        }
        comp.transform.position = dest;

        if (wasEnabled)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(dest);
        }
    }

    private IEnumerator DebuffRoutine(Stats enemy)
    {
        if (enemy == null || enemy.currentHp <= 0) yield break;

        float armorCut = enemy.armor * armorShredPercent;
        float mrCut = enemy.magicResist * magicResistShredPercent;
        enemy.armor -= armorCut;
        enemy.magicResist -= mrCut;

        yield return new WaitForSeconds(debuffDuration);

        if (enemy != null)
        {
            enemy.armor += armorCut;
            enemy.magicResist += mrCut;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, engageSearchRadius);
    }
}
