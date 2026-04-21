using UnityEngine;
using System.Collections;

public class CatalystSkill : SkillBehavior
{
    [Header("Beam Settings")]
    public float range = 15f;
    public float beamRadius = 1.0f;

    [Header("Companion Buff")]
    public float focusDuration = 5.0f;
    public float companionAtkSpeedBuff = 0.20f;

    [Header("VFX")]
    public GameObject beamVfxPrefab;
    public GameObject markVfxPrefab;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>(); // Khởi tạo
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(FireBeamRoutine());
        return true;
    }

    private IEnumerator FireBeamRoutine()
    {
        // 1. Khóa di chuyển một chút để thực hiện action bắn
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        // Xác định hướng bắn
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 startPos = transform.position + Vector3.up * 0.5f;

        // 2. Hiển thị VFX tia sáng
        //if (beamVfxPrefab != null)
        //{
        //    GameObject beam = Instantiate(beamVfxPrefab, startPos, Quaternion.LookRotation(forward));
        //    Destroy(beam, 0.5f);
        //}

        // 3. Dùng SphereCast để tìm kẻ địch đầu tiên trúng tia sáng
        RaycastHit[] hits = Physics.SphereCastAll(startPos, beamRadius, forward, range, player.dangerLayer);

        Stats bestTarget = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.collider.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                float dist = Vector3.Distance(startPos, hit.point);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = enemyStats;
                }
            }
        }

        // 4. XỬ LÝ KHI TRÚNG ĐÍCH (Thêm Sát Thương ở đây)
        if (bestTarget != null)
        {
            Debug.Log($"<color=yellow>Catalyst:</color> Đã bắn trúng và đánh dấu {bestTarget.name}!");

            // --- GÂY SÁT THƯƠNG PHÉP ---
            stats.EnterCombat();
            WeaponData currentWpn = equipmentManager.currentWeapon;
            float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
            bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
            float t = CombatMath.CalculateDirectionFactor(transform, bestTarget);


            var dmgTuple = CombatMath.CalculateFullDamage(
                stats, bestTarget, t, isCrit, data, currentWpn, data.skillMagicMultiplier
            );

            DamageInfo info = new DamageInfo();
            info.sourcePosition = transform.position;
            info.isCrit = isCrit;
            info.physDamage = dmgTuple.phys;
            info.magicDamage = dmgTuple.magic;
            info.trueDamage = dmgTuple.trueDmg;
            info.isStun = true;
            info.stunDuration = 1f; // Vì là tia sáng nên stun 1 giây

            bestTarget.TakeDamage(info);
            // ---------------------------

            // Hiện VFX trên đầu mục tiêu
            //if (markVfxPrefab != null)
            //{
            //    GameObject mark = Instantiate(markVfxPrefab, bestTarget.transform);
            //    mark.transform.localPosition = Vector3.up * 2.0f;
            //    Destroy(mark, focusDuration);
            //}

            // Gọi Companion ra lệnh Focus
            CompanionAI companion = FindFirstObjectByType<CompanionAI>();
            if (companion != null)
            {
                companion.ForceFocusTarget(bestTarget.transform, companionAtkSpeedBuff, focusDuration);
            }
        }

        // Khựng lại 1 nhịp ngắn chờ action xong
        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        Vector3 startPos = transform.position + Vector3.up * 0.5f;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(startPos, forward * range);
        Gizmos.DrawWireSphere(startPos + forward * range, beamRadius);
    }
}