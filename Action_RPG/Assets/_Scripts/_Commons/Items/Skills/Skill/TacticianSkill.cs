using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TacticianSkill : SkillBehavior
{
    [Header("Phase 1: Smoke & Taunt")]
    public float tauntRadius = 15.0f;       // Bán kính khiêu khích
    public float trapDuration = 5.0f;       // Thời gian giăng bẫy (chờ địch đánh)

    [Header("Phase 2: Companion Counter")]
    public float counterDamageMult = 4.0f;  // Sát thương phản đòn cực lớn (x4)
    public float counterStunDuration = 1.5f;// Choáng 1.5s

    [Header("VFX Prefabs")]
    public GameObject smokeVfxPrefab;       // Khói mù (tung tại vị trí thú cưng)
    public GameObject tauntAuraVfxPrefab;   // Vòng sáng khiêu khích quanh người Player
    public GameObject companionCounterVfx;  // Hiệu ứng thú cưng hiện ra phản đòn

    private CompanionAI companion;
    private Coroutine trapCoroutine;
    private GameObject currentTauntAura;
    private bool isTrapTriggered = false;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        CleanUpTrap();
    }

    public override bool Use()
    {
        // Yêu cầu phải có Companion trên sân
        companion = FindFirstObjectByType<CompanionAI>();
        if (companion == null)
        {
            Debug.Log("<color=red>TACTICIAN: Không tìm thấy Companion để tung hoả mù!</color>");
            return false;
        }

        if (!base.Use()) return false;

        if (trapCoroutine != null) StopCoroutine(trapCoroutine);
        trapCoroutine = StartCoroutine(TacticianRoutine());

        return true;
    }

    private IEnumerator TacticianRoutine()
    {
        isTrapTriggered = false;

        // ==========================================
        // 1. COMPANION TUNG HỎA MÙ
        // ==========================================
        //if (smokeVfxPrefab) Instantiate(smokeVfxPrefab, companion.transform.position, Quaternion.identity);
        Debug.Log("<color=cyan>TACTICIAN: TUNG HỎA MÙ!</color>");

        // ==========================================
        // 2. PLAYER KHIÊU KHÍCH (TAUNT)
        // ==========================================
        //if (tauntAuraVfxPrefab) currentTauntAura = Instantiate(tauntAuraVfxPrefab, transform);

        Collider[] hits = Physics.OverlapSphere(transform.position, tauntRadius, player.dangerLayer);
        foreach (var hit in hits)
        {
            EnemyAI enemyAI = hit.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                // Ép quái vật khóa mục tiêu vào Player ngay lập tức bằng hàm OnDamageTaken
                enemyAI.OnDamageTaken(transform);
            }
        }
        Debug.Log("<color=orange>TACTICIAN: TẤT CẢ QUAY SANG ĐÂY!</color>");

        // ==========================================
        // 3. GIĂNG BẪY (INTERCEPTOR)
        // ==========================================
        // Đăng ký cổng chặn sát thương vào Stats của Player
        stats.damageInterceptor = HandleIntercept;

        float timer = 0f;
        // Chờ đến khi bẫy sập HOẶC hết thời gian
        while (timer < trapDuration && !isTrapTriggered)
        {
            yield return null;
            timer += Time.deltaTime;
        }

        // ==========================================
        // 4. KẾT THÚC BẪY
        // ==========================================
        CleanUpTrap();
    }

    // ==========================================================
    // CỔNG CHẶN SÁT THƯƠNG (INTERCEPTOR LOGIC)
    // ==========================================================
    private bool HandleIntercept(DamageInfo info)
    {
        // Nếu kẻ tấn công hợp lệ (Là Enemy)
        if (info.attacker != null && info.attacker.CompareTag("Enemy"))
        {
            Debug.Log($"<color=magenta>TACTICIAN COUNTER:</color> Bắt bài {info.attacker.name}!");

            isTrapTriggered = true; // Đánh dấu bẫy đã sập để kết thúc Coroutine

            // Gọi Companion ra trừng phạt
            ExecuteCompanionCounter(info.attacker);

            // Trả về TRUE -> Báo cho Stats.cs biết là HỦY LUÔN sát thương này!
            return true;
        }

        // Trả về FALSE -> Không phải Enemy đánh (vd: rớt vực, dính độc), cứ trừ máu bình thường
        return false;
    }

    // ==========================================================
    // ĐÒN TRỪNG PHẠT TỪ THÚ CƯNG
    // ==========================================================
    private void ExecuteCompanionCounter(Stats enemy)
    {
        if (companion == null || enemy == null) return;

        Stats compStats = companion.GetComponent<Stats>();
        UnityEngine.AI.NavMeshAgent compAgent = companion.GetComponent<UnityEngine.AI.NavMeshAgent>();

        // 1. DỊCH CHUYỂN COMPANION RA TRƯỚC MẶT KẺ ĐỊCH (Để chặn đòn)
        Vector3 enemyForward = enemy.facingDirection;
        if (enemyForward == Vector3.zero) enemyForward = enemy.transform.forward;
        enemyForward.y = 0; enemyForward.Normalize();

        // Đo đạc Hitbox để đứng cho chuẩn
        Collider enemyCol = enemy.GetComponent<Collider>();
        float eRadius = enemyCol != null ? Mathf.Max(enemyCol.bounds.extents.x, enemyCol.bounds.extents.z) : 1.0f;
        Vector3 blockPos = enemy.transform.position + enemyForward * (eRadius + 0.5f);

        // Dịch chuyển an toàn với NavMesh
        if (compAgent) compAgent.enabled = false;
        companion.transform.position = blockPos;

        // Thú cưng quay mặt nhìn thẳng vào mặt kẻ địch
        companion.transform.rotation = Quaternion.LookRotation(-enemyForward);

        if (compAgent) { compAgent.enabled = true; compAgent.Warp(blockPos); }

        // 2. HIỆU ỨNG VÀ SÁT THƯƠNG
        //if (companionCounterVfx) Instantiate(companionCounterVfx, enemy.transform.position, Quaternion.identity);

        // Tính sát thương dựa trên chỉ số của thú cưng
        var compDmg = CombatMath.CalculateFullDamage(compStats, enemy, 1.0f, true, null, null, counterDamageMult);

        DamageInfo counterInfo = new DamageInfo
        {
            sourcePosition = companion.transform.position,
            attacker = compStats,
            physDamage = compDmg.phys,
            magicDamage = compDmg.magic,
            trueDamage = compDmg.trueDmg,
            isCrit = true,                  // Đòn phản công luôn chí mạng
            isStun = true,                  // Khóa chết mục tiêu
            stunDuration = counterStunDuration,
            impactLevel = 2                 // Phá Siêu Giáp
        };

        enemy.TakeDamage(counterInfo);

        // 3. HUỶ ĐÒN ĐÁNH CỦA ĐỊCH
        EnemyCombat enemyCombat = enemy.GetComponent<EnemyCombat>();
        if (enemyCombat != null) enemyCombat.CancelAttack();

        // Ép thú cưng đứng im tạo dáng
        companion.ForceWait(1.0f);
    }

    private void CleanUpTrap()
    {
        if (currentTauntAura) Destroy(currentTauntAura);

        // Rút thẻ Interceptor ra khỏi Player để nhận sát thương lại bình thường
        if (stats.damageInterceptor == HandleIntercept)
        {
            stats.damageInterceptor = null;
            Debug.Log("<color=gray>Tactician: Đã thu hồi bẫy chiến thuật.</color>");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, tauntRadius);
    }
}