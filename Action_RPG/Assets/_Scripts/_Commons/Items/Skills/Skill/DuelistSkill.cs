using UnityEngine;
using System.Collections;

public class DuelistSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float dashDistance = 1.0f;     // Lướt ngắn
    public float dashSpeed = 5.0f;       // Tốc độ lướt 
    public float sweepRadius = 1f;      // Tầm chém sau lướt
    public float sweepAngle = 360f;       // Chém quét 360 độ
    //public float damageMultiplier = 1.0f; // Sát thương bình thường (100%)

    [Header("Challenge Mark")]
    public float challengeDuration = 5.0f; // Ấn tồn tại 5s

    [Header("VFX")]
    public GameObject dashVfxPrefab;
    public GameObject challengeMarkVfxPrefab; // [Tùy chọn] Prefab một cái icon 2 cây kiếm chéo nhau hiện trên đầu địch

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(ChallengeRoutine());
        return true;
    }

    private IEnumerator ChallengeRoutine()
    {
        // 1. Khóa Player & Lướt
        player.isAttacking = true;
        //stats.isInvincible = true; // Bất tử nhẹ lúc lướt
        bool originalKinematic = rb.isKinematic;
        rb.isKinematic = true;

        Vector3 startPos = transform.position;
        Vector3 dashDir = stats.facingDirection;
        if (dashDir == Vector3.zero) dashDir = transform.forward;
        dashDir.y = 0; dashDir.Normalize();

        Vector3 targetPos = startPos + (dashDir * dashDistance);

        if (dashVfxPrefab != null) Instantiate(dashVfxPrefab, transform.position, Quaternion.identity);

        float distanceTotal = Vector3.Distance(startPos, targetPos);
        float duration = Mathf.Max(distanceTotal / dashSpeed, 0.1f);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float percent = timer / duration;
            rb.MovePosition(Vector3.Lerp(startPos, targetPos, percent));
            yield return null;
        }

        rb.isKinematic = originalKinematic;
        //stats.isInvincible = false;

        // 2. Chém Quét (Sweep) và Gắn Dấu Ấn
        Collider[] hits = Physics.OverlapSphere(transform.position, sweepRadius, player.dangerLayer);
        int hitCount = 0;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // Kiểm tra góc chém
                Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;
                if (Vector3.Angle(dashDir, dirToEnemy) <= sweepAngle / 2)
                {
                    ApplyDamageAndMark(enemyStats);
                    hitCount++;
                }
            }
        }

        Debug.Log($"<color=orange>Duelist:</color> Đã thách đấu {hitCount} kẻ địch!");

        // 3. Khựng lại thu kiếm
        yield return new WaitForSeconds(0.2f);
        player.isAttacking = false;
    }

    private void ApplyDamageAndMark(Stats enemyStats)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        // Gây sát thương cơ bản
        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, data.skillPhysicalMultiplier
        );

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.damageAmount = damage;
        info.isKnockback = true;
        info.knockbackForce = 0.5f; // Đẩy rất nhẹ

        enemyStats.TakeDamage(info);

        // [QUAN TRỌNG] Gắn ấn Thách Đấu
        enemyStats.ApplyChallengeMark(challengeDuration);

        // Sinh ra VFX cái ấn trên đầu quái
        //if (challengeMarkVfxPrefab != null)
        //{
        //    GameObject mark = Instantiate(challengeMarkVfxPrefab, enemyStats.transform);
        //    mark.transform.localPosition = Vector3.up * 2.0f; // Nhô cao 2m so với tâm quái
        //    Destroy(mark, challengeDuration);
        //}
    }
}