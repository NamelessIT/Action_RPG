using System.Collections;
using UnityEngine;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;

public class InfiltratorSkill : SkillBehavior
{
    [Header("Phase 1: Invisibility")]
    public float invisibilityDuration = 4.0f;
    public float engageSearchRadius = 5.0f; // Khoảng cách tối đa để lướt tới mục tiêu

    [Header("Phase 2: Engage & Mark (Debuff)")]
    public float armorShredPercent = 0.3f;   // Giảm 30% Giáp
    public float magicResistShredPercent = 0.3f; // Giảm 30% Kháng phép
    public float debuffDuration = 3.0f;

    [Header("Phase 3: Companion Strike")]
    public float companionDamageMult = 2.0f; // Companion chém x2 sát thương
    public float stunDuration = 1.5f;        // Choáng 1.5s

    [Header("Phase 4: Backstab Execution")]
    //public float backstabDamageMult = 3.5f;  // Player đâm lén x3.5 sát thương

    [Header("VFX Prefabs")]
    public GameObject invisAuraVfx;          // Khói tàng hình
    public GameObject markVfx;               // Ấn ký trên đầu địch
    public GameObject companionStrikeVfx;    // Hiệu ứng chém của thú cưng
    public GameObject backstabVfx;           // Hiệu ứng đâm lén chí mạng

    // --- Trạng thái hệ thống ---
    private SpriteRenderer playerSprite;
    private EquipmentManager equipmentManager;
    private Coroutine invisCoroutine;
    private GameObject currentInvisVfx;

    private bool isPhantomStrikeReady = false; // Cờ chờ đòn đánh tiếp theo

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        playerSprite = myPlayer.GetComponentInChildren<SpriteRenderer>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip()
    {
        BreakInvisibility(true);
    }

    void Update()
    {
        // Lắng nghe thao tác Đánh Thường (Bấm chuột trái) khi đang có Buff Tàng Hình
        if (isPhantomStrikeReady && Input.GetMouseButtonDown(0))
        {
            isPhantomStrikeReady = false;
            BreakInvisibility(false);

            Stats target = FindNearestEnemy();
            if (target != null)
            {
                // Bắt đầu chuỗi Combo kết liễu
                StartCoroutine(ExecutionComboRoutine(target));
            }
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // Bật tàng hình và chờ lệnh
        if (invisCoroutine != null) StopCoroutine(invisCoroutine);
        invisCoroutine = StartCoroutine(InvisibilityRoutine());

        return true;
    }

    // ==========================================================
    // LOGIC TÀNG HÌNH
    // ==========================================================
    private IEnumerator InvisibilityRoutine()
    {
        stats.isInvisible = true;
        isPhantomStrikeReady = true;

        if (playerSprite) playerSprite.enabled = false;
        if (invisAuraVfx) currentInvisVfx = Instantiate(invisAuraVfx, transform);

        Debug.Log("<color=cyan>INFILTRATOR: Bước Chân Bóng Tối (Tàng hình 4s)!</color>");

        yield return new WaitForSeconds(invisibilityDuration);

        BreakInvisibility(false);
    }

    private void BreakInvisibility(bool isForceCleanup)
    {
        if (!stats.isInvisible && !isForceCleanup) return;

        stats.isInvisible = false;
        isPhantomStrikeReady = false;

        if (playerSprite) playerSprite.enabled = true;
        if (currentInvisVfx) Destroy(currentInvisVfx);

        if (!isForceCleanup) Debug.Log("<color=gray>Infiltrator: Đã hiện hình.</color>");
    }

    private Stats FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, engageSearchRadius, player.dangerLayer);
        Stats bestTarget = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Stats enemy = hit.GetComponent<Stats>();
            if (enemy != null && enemy.currentHp > 0)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestTarget = enemy;
                }
            }
        }
        return bestTarget;
    }

    // ==========================================================
    // CHUỖI COMBO ĐIỆN ẢNH (PLAYER -> COMPANION -> PLAYER)
    // ==========================================================
    private IEnumerator ExecutionComboRoutine(Stats target)
    {
        // 0. KHÓA NGƯỜI CHƠI KHÔNG CHO THAO TÁC KHÁC
        player.isUsingSpecialSkill = true;
        GameObject currentMarkVfx = null;

        try
        {
            // ----------------------------------------------------
            // NHỊP 1: PLAYER LƯỚT TỚI CHÉM VÀ ĐÁNH DẤU
            // ----------------------------------------------------
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
            dirToTarget.y = 0;

            // Đo kích thước Hitbox để đứng cho chuẩn
            Collider targetCol = target.GetComponent<Collider>();
            float tRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;

            // Lướt ra TRƯỚC MẶT kẻ địch
            Vector3 frontPos = target.transform.position - dirToTarget * (tRadius + 0.5f);

            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(frontPos, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                player.transform.position = navHit.position;
            else
                player.transform.position = frontPos;

            player.ForceFaceDirection(dirToTarget);

            Debug.Log($"<color=orange>NHỊP 1:</color> Lướt tới {target.name}, Đánh Dấu & Trừ Giáp!");

            // Áp dụng Debuff & Đánh dấu
            target.IsMarked = true;
            if (markVfx) currentMarkVfx = Instantiate(markVfx, target.transform.position + Vector3.up * 2f, Quaternion.identity, target.transform);

            StartCoroutine(DebuffRoutine(target));

            // Đợi 0.4s để Player chém xong đòn chém thường (PlayerController đang tự chạy Animation)
            yield return new WaitForSeconds(0.4f);

            // ----------------------------------------------------
            // NHỊP 2: COMPANION LAO VÀO STUN
            // ----------------------------------------------------
            CompanionAI companion = FindFirstObjectByType<CompanionAI>();
            if (companion != null)
            {
                Stats compStats = companion.GetComponent<Stats>();
                UnityEngine.AI.NavMeshAgent compAgent = companion.GetComponent<UnityEngine.AI.NavMeshAgent>();

                // Dời Companion ra BÊN HÔNG kẻ địch
                Vector3 rightSideDir = Quaternion.Euler(0, 90, 0) * dirToTarget;
                Vector3 sidePos = target.transform.position + rightSideDir * (tRadius + 0.5f);

                if (compAgent) compAgent.enabled = false;
                companion.transform.position = sidePos;

                // Quay mặt vào địch
                Vector3 lookDir = (target.transform.position - companion.transform.position).normalized;
                lookDir.y = 0;
                companion.transform.rotation = Quaternion.LookRotation(lookDir);

                if (compAgent) { compAgent.enabled = true; compAgent.Warp(sidePos); }

                Debug.Log($"<color=orange>NHỊP 2:</color> Companion lao tới làm choáng!");

                if (companionStrikeVfx) Instantiate(companionStrikeVfx, target.transform.position, Quaternion.identity);

                // Gây sát thương của thú cưng
                var compDmg = CombatMath.CalculateFullDamage(compStats, target, 0.5f, false, null, null, companionDamageMult);
                DamageInfo compInfo = new DamageInfo
                {
                    sourcePosition = companion.transform.position,
                    attacker = compStats,
                    physDamage = compDmg.phys,
                    magicDamage = compDmg.magic,
                    trueDamage = compDmg.trueDmg,
                    isStun = true,
                    stunDuration = stunDuration,
                    impactLevel = 2 // Đảm bảo phá siêu giáp
                };
                target.TakeDamage(compInfo);

                // Ép thú cưng đứng im nhìn cho ngầu
                companion.ForceWait(1.5f);
            }

            yield return new WaitForSeconds(0.3f);

            // ----------------------------------------------------
            // NHỊP 3: PLAYER ĐÂM LÉN SAU LƯNG TẦT SÁT
            // ----------------------------------------------------
            Vector3 enemyForward = target.facingDirection;
            if (enemyForward == Vector3.zero) enemyForward = target.transform.forward;
            enemyForward.y = 0; enemyForward.Normalize();

            // Lướt ra SAU LƯNG
            Vector3 backPos = target.transform.position - enemyForward * (tRadius + 0.5f);

            if (UnityEngine.AI.NavMesh.SamplePosition(backPos, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                player.transform.position = navHit.position;
            else
                player.transform.position = backPos;

            player.ForceFaceDirection(enemyForward);

            Debug.Log($"<color=red>NHỊP 3 (TẤT SÁT):</color> Backstab chí mạng!");

            if (backstabVfx) Instantiate(backstabVfx, target.transform.position, Quaternion.LookRotation(enemyForward));

            WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
            float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
            bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

            // Ép t = 1.0f (Sát thương đâm lén)
            var bsDamage = CombatMath.CalculateFullDamage(stats, target, 1.0f, isCrit, data, currentWpn, data.skillPhysicalMultiplier);

            DamageInfo bsInfo = new DamageInfo
            {
                sourcePosition = transform.position,
                attacker = stats,
                physDamage = bsDamage.phys,
                magicDamage = bsDamage.magic,
                trueDamage = bsDamage.trueDmg,
                isCrit = isCrit,
                impactLevel = 1
            };
            target.TakeDamage(bsInfo);

            // Nhịp nghỉ tạo dáng
            yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            // Dọn dẹp trạng thái
            if (target != null) target.IsMarked = false;
            if (currentMarkVfx != null) Destroy(currentMarkVfx);
            player.isUsingSpecialSkill = false;
        }
    }

    private IEnumerator DebuffRoutine(Stats enemy)
    {
        if (enemy != null && enemy.currentHp > 0)
        {
            float armorToReduce = enemy.armor * armorShredPercent;
            float magicResistToReduce = enemy.magicResist * magicResistShredPercent;

            enemy.armor -= armorToReduce;
            enemy.magicResist -= magicResistToReduce;

            yield return new WaitForSeconds(debuffDuration);

            if (enemy != null)
            {
                enemy.armor += armorToReduce;
                enemy.magicResist += magicResistToReduce;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, engageSearchRadius);
    }
}