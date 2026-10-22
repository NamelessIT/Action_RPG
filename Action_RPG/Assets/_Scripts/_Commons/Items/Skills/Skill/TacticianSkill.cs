using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

/// <summary>
/// TacticianSkill — "Intervention".
/// Gắn mồi nhử lên Companion: khiêu khích mọi kẻ địch trong 15f quay sang tấn công Companion
/// trong 5s. Khi MỘT kẻ địch ra đòn vào Companion, PLAYER lập tức xuất hiện chắn ngay trước mặt
/// kẻ đó, HỦY đòn của nó, làm choáng 1.5s và gây 400% physicalAtk, rồi kéo target kẻ đó sang PLAYER.
/// Nếu hết 5s mà không có kẻ địch nào ra đòn → trả target của chúng về PLAYER.
/// </summary>
public class TacticianSkill : SkillBehavior
{
    [Header("Phase 1: Mồi nhử & Khiêu khích")]
    public float tauntRadius = 15.0f;
    public float trapDuration = 5.0f;
    public float tauntRefreshInterval = 0.3f; // chu kỳ scan để bắt enemy MỚI bước vào vùng; không refresh Taunt đã apply
    public float triggerRange = 2.0f;         // địch tới gần Companion cỡ này coi như "ra đòn"

    [Header("Phase 2: Player phản đòn")]
    public float counterDamageMult = 4.0f;     // 400% physicalAtk của Player
    public float counterStunDuration = 1.5f;

    [Header("VFX (tuỳ chọn)")]
    public GameObject baitVfxPrefab;
    public GameObject counterVfxPrefab;

    private CompanionAI companion;
    private Stats companionStats;
    private Rigidbody rb;
    private Coroutine trapCoroutine;
    private GameObject debugBaitAura;

    private bool isTrapTriggered = false;
    private Stats triggeredAttacker;
    private readonly List<EnemyAI> tauntedEnemies = new List<EnemyAI>(); // accepted true-Taunt only
    private readonly HashSet<EnemyAI> tauntAttemptedEnemies = new HashSet<EnemyAI>();
    private readonly Dictionary<EnemyAI, EffectHandle> tauntHandles = new Dictionary<EnemyAI, EffectHandle>();

    private float _effStunDur;
    private float _effDmgMult;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        rb = myPlayer.GetComponent<Rigidbody>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    protected override void OnUnequip()
    {
        if (trapCoroutine != null)
        {
            StopCoroutine(trapCoroutine);
            trapCoroutine = null;
        }
        CleanUpTrap(true);
    }

    public override bool Use()
    {
        companion = CompanionAI.Current;
        if (companion == null)
        {
            Debug.Log("<color=red>TACTICIAN: Không có Companion để gắn mồi nhử!</color>");
            return false;
        }

        if (!base.Use()) return false;

        // Duelist U1 + Catalyst U1: +20% choáng | Duelist U3 + Catalyst U3: +20% sát thương
        float dU1 = stats != null ? stats.duelistSkillU1  : 0f;
        float dU3 = stats != null ? stats.duelistSkillU3  : 0f;
        float cU1 = stats != null ? stats.catalystSkillU1 : 0f;
        float cU3 = stats != null ? stats.catalystSkillU3 : 0f;

        _effStunDur = counterStunDuration * (1f + dU1 + cU1);
        _effDmgMult = counterDamageMult   * (1f + dU3 + cU3);

        companionStats = companion.GetComponent<Stats>();

        // Defensive lifecycle: nếu content/runtime gọi Use lại khi trap cũ còn sống,
        // phải gỡ interceptor + đúng EffectHandle của Taunt cũ trước khi tạo trap mới.
        if (trapCoroutine != null)
        {
            StopCoroutine(trapCoroutine);
            trapCoroutine = null;
            CleanUpTrap(true);
        }
        trapCoroutine = StartCoroutine(TacticianRoutine());
        return true;
    }

    private IEnumerator TacticianRoutine()
    {
        isTrapTriggered = false;
        triggeredAttacker = null;
        tauntedEnemies.Clear();
        tauntAttemptedEnemies.Clear();
        tauntHandles.Clear();

        // ---------- 1. GẮN MỒI NHỬ LÊN COMPANION ----------
        Debug.Log("<color=cyan>TACTICIAN: GẮN MỒI NHỬ LÊN COMPANION!</color>");
        if (baitVfxPrefab) debugBaitAura = Instantiate(baitVfxPrefab, companion.transform);
        else { debugBaitAura = MageVfxHelper.AttachSphere(companion.transform, 1.2f, new Color(1f, 0.85f, 0.1f, 0.35f)); debugBaitAura.transform.localPosition = Vector3.up; }

        // ---------- 2. ĐẶT CỔNG CHẶN SÁT THƯƠNG TRÊN COMPANION ----------
        if (companionStats != null) companionStats.damageInterceptor = HandleCompanionIntercept;

        // ---------- 3. KHIÊU KHÍCH + GIỮ TARGET TRÊN COMPANION ----------
        float timer = 0f;
        float refresh = 0f;
        ForceTauntToCompanion(trapDuration);
        while (timer < trapDuration && !isTrapTriggered)
        {
            timer += Time.deltaTime;
            refresh += Time.deltaTime;
            if (refresh >= tauntRefreshInterval)
            {
                refresh = 0f;
                ForceTauntToCompanion(Mathf.Max(0.1f, trapDuration - timer));
            }
            CheckProximityTrigger(); // địch áp sát Companion → kích hoạt phản đòn
            yield return null;
        }

        // ---------- 4. KẾT QUẢ ----------
        if (isTrapTriggered && triggeredAttacker != null)
            ExecutePlayerCounter(triggeredAttacker);

        // Gỡ đúng Taunt của Intervention trước. Sau đó mới làm legacy target redirect nếu target
        // thực sự đã trở về normal control; không được stomp một Fear/Taunt khác đang còn hiệu lực.
        CleanUpTrap(false);

        if (isTrapTriggered && triggeredAttacker != null)
        {
            EnemyAI triggeredAI = triggeredAttacker.GetComponent<EnemyAI>() ?? triggeredAttacker.GetComponentInParent<EnemyAI>();
            RedirectToPlayerIfUnforced(triggeredAI);
        }
        else
        {
            RevertTauntToPlayer();
            Debug.Log("<color=gray>Tactician: Hết khiêu khích, địch quay về Player.</color>");
        }

        trapCoroutine = null;
    }

    // Phase E: scan lặp chỉ để bắt enemy MỚI bước vào vùng. Mỗi enemy chỉ apply Taunt 1 lần,
    // tránh refresh liên tục làm vô hiệu resistanceEffect.
    private void ForceTauntToCompanion(float remainingTrapTime)
    {
        if (companionStats == null || companionStats.isDead) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, tauntRadius, player.dangerLayer);
        HashSet<EnemyAI> seen = new HashSet<EnemyAI>();
        foreach (var hit in hits)
        {
            EnemyAI ai = hit.GetComponentInParent<EnemyAI>();
            if (ai == null || !seen.Add(ai) || tauntAttemptedEnemies.Contains(ai)) continue;
            tauntAttemptedEnemies.Add(ai);

            Stats enemyStats = ai.GetComponent<Stats>() ?? ai.GetComponentInParent<Stats>();
            if (enemyStats == null || enemyStats.isDead) continue;

            EffectApplyResult result = enemyStats.ApplyEffectWithResult(
                new CombatEffectInfo(CombatEffectType.Taunt, Mathf.Max(0.1f, remainingTrapTime))
                {
                    effectId = "Tactician.Intervention.Taunt",
                    impactLevel = 0,
                    stackPolicy = EffectStackPolicy.RefreshSameSource,
                    interruptCurrentAction = true,
                    note = "Tactician Intervention"
                },
                companionStats);

            if (!result.Accepted) continue;
            tauntedEnemies.Add(ai);
            tauntHandles[ai] = result.handle;
        }
    }

    private void RevertTauntToPlayer()
    {
        foreach (var ai in tauntedEnemies)
            RedirectToPlayerIfUnforced(ai);
    }

    private void RedirectToPlayerIfUnforced(EnemyAI ai)
    {
        if (ai == null || player == null) return;
        Stats es = ai.GetComponent<Stats>() ?? ai.GetComponentInParent<Stats>();
        if (es == null || es.isDead) return;
        if (ForcedControlRunner.IsForcedMode(es.ControlState.mode)) return;

        ai.OnDamageTaken(player.transform);
        ai.nearestTarget = player.transform;
    }

    // Nếu có kẻ địch bị khiêu khích áp sát Companion → coi như nó "ra đòn", kích hoạt phản đòn.
    private void CheckProximityTrigger()
    {
        if (companion == null) return;
        foreach (var ai in tauntedEnemies)
        {
            if (ai == null) continue;
            Stats es = ai.GetComponent<Stats>() ?? ai.GetComponentInParent<Stats>();
            if (es == null || es.currentHp <= 0) continue;
            if (!tauntHandles.TryGetValue(ai, out EffectHandle handle)
                || es.StatusEffects == null
                || !es.StatusEffects.IsEffectActive(handle)) continue;

            if (Vector3.Distance(ai.transform.position, companion.transform.position) <= triggerRange)
            {
                triggeredAttacker = es;
                isTrapTriggered = true;
                return;
            }
        }
    }

    // ==========================================================
    // CỔNG CHẶN SÁT THƯƠNG TRÊN COMPANION
    // ==========================================================
    private bool HandleCompanionIntercept(DamageInfo info)
    {
        if (info.attacker != null && info.attacker.CompareTag("Enemy"))
        {
            // Bắt bài kẻ tấn công Companion → kích hoạt phản đòn ở coroutine (tránh reentrancy).
            triggeredAttacker = info.attacker;
            isTrapTriggered = true;
            Debug.Log($"<color=magenta>TACTICIAN:</color> {info.attacker.name} ra đòn vào Companion — PLAYER CHẶN & PHẢN!");
            return true; // HỦY đòn của địch
        }
        return false;
    }

    // ==========================================================
    // PLAYER PHẢN ĐÒN (xuất hiện trước mặt kẻ địch)
    // ==========================================================
    private void ExecutePlayerCounter(Stats enemy)
    {
        if (enemy == null || enemy.currentHp <= 0) return;

        // Player xuất hiện chắn ngay TRƯỚC MẶT kẻ địch.
        Vector3 enemyForward = enemy.facingDirection;
        if (enemyForward == Vector3.zero) enemyForward = enemy.transform.forward;
        enemyForward.y = 0; enemyForward.Normalize();

        Collider enemyCol = enemy.GetComponentInChildren<Collider>();
        float eRadius = enemyCol != null ? Mathf.Max(enemyCol.bounds.extents.x, enemyCol.bounds.extents.z) : 1.0f;
        Vector3 blockPos = enemy.transform.position + enemyForward * (eRadius + 0.5f);
        if (NavMesh.SamplePosition(blockPos, out var navHit, 2.0f, NavMesh.AllAreas)) blockPos = navHit.position;

        // Dùng rb.position để teleport (giữ Rigidbody đồng bộ → không kẹt di chuyển sau đó).
        if (rb != null) { rb.position = blockPos; rb.linearVelocity = Vector3.zero; }
        else player.transform.position = blockPos;
        player.ForceFaceDirection(-enemyForward); // quay mặt vào kẻ địch

        if (counterVfxPrefab) Instantiate(counterVfxPrefab, enemy.transform.position, Quaternion.identity);
        VisualDebugHelper.DrawSphere(enemy.transform.position + Vector3.up, 0.6f, new Color(1f, 0.2f, 0.8f, 0.5f), 0.25f);

        // 400% physicalAtk của Player + choáng (weapon=null → ép vật lý).
        stats.EnterCombat();
        DamageHelper.ApplyStandardDamage(stats, enemy, player.transform, _effDmgMult, null, null, 2, true, _effStunDur);

        // Hủy đòn đang ra của địch.
        EnemyCombat enemyCombat = enemy.GetComponent<EnemyCombat>() ?? enemy.GetComponentInParent<EnemyCombat>();
        if (enemyCombat != null) enemyCombat.CancelAttack();

        // Target redirect sang Player được thực hiện SAU khi gỡ Taunt của Intervention,
        // để không ghi đè một forced-control khác đang chiếm quyền.
        Debug.Log("<color=red>TACTICIAN: Player phản đòn 400% + choáng!</color>");
    }

    private void CleanUpTrap(bool revertOnForce)
    {
        if (debugBaitAura)
        {
            Destroy(debugBaitAura);
            debugBaitAura = null;
        }

        if (companionStats != null && companionStats.damageInterceptor == HandleCompanionIntercept)
            companionStats.damageInterceptor = null;

        ReleaseAllTaunts();
        if (revertOnForce) RevertTauntToPlayer();
    }

    private void ReleaseAllTaunts()
    {
        foreach (var pair in tauntHandles)
        {
            EnemyAI ai = pair.Key;
            if (ai == null) continue;
            Stats es = ai.GetComponent<Stats>() ?? ai.GetComponentInParent<Stats>();
            if (es != null && es.StatusEffects != null)
                es.StatusEffects.RemoveEffect(pair.Value, EffectRemoveReason.Manual);
        }
        tauntHandles.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, tauntRadius);
    }
}
