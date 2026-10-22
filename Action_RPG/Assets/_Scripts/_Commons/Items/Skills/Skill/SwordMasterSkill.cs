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

    // Cached effective values (computed at swiftness activation)
    private float _effectiveBuffDuration;
    private float _effectiveStunDuration;
    private float _effectiveBackstabMult;

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
        stats.ClearDebuffs();      // Xóa debuff DoT (Bleed, Burn)

        Debug.Log("<color=green>SWORD MASTER: Giải trừ khống chế & debuff!</color>");

        // 2. KÍCH HOẠT TRẠNG THÁI SWIFTNESS
        if (swiftnessCoroutine != null) StopCoroutine(swiftnessCoroutine);
        swiftnessCoroutine = StartCoroutine(SwiftnessRoutine());

        return true;
    }

    private IEnumerator SwiftnessRoutine()
    {
        if (!isSwiftnessActive && allyStats != null)
        {
            // Rogue U1:  +20% buffDuration    | Rogue U3:  +20% backstab damage mult
            // Duelist U1: +20% stun duration  | Duelist U3: +20% backstab damage mult
            float rU1 = allyStats.rogueSkillU1;
            float rU3 = allyStats.rogueSkillU3;
            float dU1 = allyStats.duelistSkillU1;
            float dU3 = allyStats.duelistSkillU3;

            _effectiveBuffDuration  = buffDuration  * (1f + rU1);
            _effectiveStunDuration  = stunDuration  * (1f + dU1);
            _effectiveBackstabMult  = data.skillPhysicalMultiplier * (1f + rU3 + dU3);

            isSwiftnessActive = true;

            originalDashCost = allyStats.dashCost;
            allyStats.dashCost = 0f;

            Debug.Log($"<color=cyan>SWORD MASTER: SWIFTNESS! ({_effectiveBuffDuration:F1}s)</color>");

            float timer = 0f;
            while (timer < _effectiveBuffDuration)
            {
                if (allyStats.isPerfectDodgeSuccess)
                {
                    allyStats.isPerfectDodgeSuccess = false;
                    ExecutePerfectCounter();
                    break; // Chỉ kích hoạt 1 lần, phản đòn xong là thoát Swiftness ngay
                }

                timer += Time.deltaTime;
                yield return null;
            }

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
            impactLevel = 2
        };
        // [CC] Stun qua effect system, impact 2 (phá siêu giáp) trên chính CombatEffectInfo.
        stunInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, _effectiveStunDuration) // Duelist U1
        { impactLevel = 2, sourcePosition = transform.position });
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

        // Player đã đứng sau lưng địch, direction tự nhiên = backstab
        // Rogue U3 * Duelist U3 both boost backstab damage
        DamageHelper.ApplyStandardDamage(stats, targetEnemy, transform, _effectiveBackstabMult, data, equipmentManager != null ? equipmentManager.currentWeapon : null, 0);
    }
}