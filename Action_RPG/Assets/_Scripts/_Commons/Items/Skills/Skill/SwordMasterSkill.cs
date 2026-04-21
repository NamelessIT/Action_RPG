using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SwordMasterSkill : SkillBehavior
{
    [Header("Swiftness Buff Settings")]
    public float buffDuration = 4.0f;       // Tồn tại 4 giây

    [Header("Counter Attack Settings")]
    //public float counterDamageMultiplier = 4.0f; // Sát thương cực lớn (x4)
    public float stunDuration = 2.0f;            // Ảo ảnh gây choáng 2s
    public float counterSearchRange = 10.0f;     // Tìm kẻ địch trong 10m để phản đòn

    [Header("VFX")]
    public GameObject swiftnessAuraVfx;     // Hào quang gió/kiếm khí quanh người
    public GameObject illusionVfxPrefab;    // Hiệu ứng bóng mờ để lại tại chỗ
    public GameObject backstabVfxPrefab;    // Hiệu ứng chém chí mạng sau lưng

    private AllyStats allyStats;
    private EquipmentManager equipmentManager;

    private Coroutine swiftnessCoroutine;
    private float originalDashCost;
    private bool isSwiftnessActive = false;
    private GameObject currentAuraVfx;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        allyStats = myStats as AllyStats; // Ép kiểu để lấy dashCost và cờ PerfectDodge
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        RemoveBuff();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // 1. THANH TẨY MỌI HIỆU ỨNG BẤT LỢI (CLEANSE)
        stats.BreakCrowdControl(); // Giải phóng khỏi Stun, Knockback
        //stats.isBleeding = false;  // Cầm máu

        Debug.Log("<color=green>SWORD MASTER: Giải trừ khống chế!</color>");

        // 2. KÍCH HOẠT TRẠNG THÁI SWIFTNESS
        if (swiftnessCoroutine != null) StopCoroutine(swiftnessCoroutine);
        swiftnessCoroutine = StartCoroutine(SwiftnessRoutine());

        return true;
    }

    private IEnumerator SwiftnessRoutine()
    {
        if (!isSwiftnessActive && allyStats != null)
        {
            isSwiftnessActive = true;

            // Xóa cost của Dash
            originalDashCost = allyStats.dashCost;
            allyStats.dashCost = 0f;

            //if (currentAuraVfx != null) Destroy(currentAuraVfx);
            //if (swiftnessAuraVfx != null) currentAuraVfx = Instantiate(swiftnessAuraVfx, transform);

            Debug.Log("<color=cyan>SWORD MASTER: SWIFTNESS! (Dash không tốn thể lực trong 4s)</color>");

            float timer = 0f;
            while (timer < buffDuration)
            {
                // LIÊN TỤC LẮNG NGHE PHẢN XẠ (FRAME PERFECT)
                // Hệ thống sẽ bật cờ này khi bạn lướt đúng lúc quái đang đánh
                if (allyStats.isPerfectDodgeSuccess)
                {
                    allyStats.isPerfectDodgeSuccess = false; // Tiêu thụ ngay lập tức để không lặp lại
                    ExecutePerfectCounter();
                }

                timer += Time.deltaTime;
                yield return null; // Quét mỗi frame
            }

            // HẾT THỜI GIAN -> GỠ BUFF
            RemoveBuff();
        }
    }

    private void RemoveBuff()
    {
        if (isSwiftnessActive && allyStats != null)
        {
            isSwiftnessActive = false;
            allyStats.dashCost = originalDashCost; // Trả lại cost lướt như cũ

            //if (currentAuraVfx != null) Destroy(currentAuraVfx);
            Debug.Log("<color=gray>Sword Master: Hết trạng thái Swiftness.</color>");
        }
    }

    // ==========================================================
    // LOGIC PHẢN KÍCH (ẢO ẢNH + DỊCH CHUYỂN BACKSTAB)
    // ==========================================================
    private void ExecutePerfectCounter()
    {
        Debug.Log("<color=magenta>SWORD MASTER: ĐỘT KÍCH ẢO ẢNH!</color>");

        // 1. TÌM KẺ ĐỊCH GẦN NHẤT ĐỂ TRẢ ĐŨA
        Collider[] hits = Physics.OverlapSphere(transform.position, counterSearchRange, player.dangerLayer);
        Stats targetEnemy = null;
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
                    targetEnemy = enemy;
                }
            }
        }

        if (targetEnemy == null) return; // Nếu không tìm thấy ai thì bỏ qua

        // 2. ĐỂ LẠI ẢO ẢNH TẠI VỊ TRÍ CŨ VÀ GÂY CHOÁNG
        if (illusionVfxPrefab != null)
        {
            // Sinh ra ảo ảnh đứng lại vị trí bạn vừa đứng
            GameObject illusion = Instantiate(illusionVfxPrefab, transform.position, transform.rotation);
            Destroy(illusion, stunDuration);
        }

        DamageInfo stunInfo = new DamageInfo
        {
            sourcePosition = transform.position,
            attacker = stats,
            physDamage = 0,
            isStun = true,
            stunDuration = stunDuration,
            impactLevel = 2 // Phá siêu giáp, chắc chắn choáng
        };
        targetEnemy.TakeDamage(stunInfo);

        // 3. DỊCH CHUYỂN RA SAU LƯNG ĐỊCH
        Vector3 enemyForward = targetEnemy.facingDirection;
        if (enemyForward == Vector3.zero) enemyForward = targetEnemy.transform.forward;
        enemyForward.y = 0; enemyForward.Normalize();

        Collider targetCol = targetEnemy.GetComponent<Collider>();
        Collider playerCol = player.GetComponent<Collider>();

        float targetRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;
        float playerRadius = playerCol != null ? Mathf.Max(playerCol.bounds.extents.x, playerCol.bounds.extents.z) : 0.5f;

        float safeDistance = targetRadius + playerRadius + 0.2f;
        Vector3 backPosition = targetEnemy.transform.position - enemyForward * safeDistance;

        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(backPosition, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            player.transform.position = navHit.position;
        else
            player.transform.position = backPosition;

        player.ForceFaceDirection(enemyForward);

        // 4. TUNG ĐÒN BACKSTAB CHÍ MẠNG
        //if (backstabVfxPrefab) Instantiate(backstabVfxPrefab, targetEnemy.transform.position, Quaternion.LookRotation(enemyForward));

        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        // Ép t = 1.0f để hệ thống luôn tính sát thương này là đâm lén (Backstab Bonus)
        var dmgTuple = CombatMath.CalculateFullDamage(
            stats, targetEnemy, 1.0f, isCrit, data, currentWpn, data.skillPhysicalMultiplier
        );

        DamageInfo damageInfo = new DamageInfo
        {
            sourcePosition = transform.position,
            attacker = stats,
            physDamage = dmgTuple.phys,
            magicDamage = dmgTuple.magic,
            trueDamage = dmgTuple.trueDmg,
            isCrit = isCrit
        };

        targetEnemy.TakeDamage(damageInfo);
    }
}