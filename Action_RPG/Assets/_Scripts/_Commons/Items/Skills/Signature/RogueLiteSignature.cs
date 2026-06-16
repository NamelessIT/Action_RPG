using UnityEngine;
using System.Collections;

public class RogueLiteSignature : SkillBehavior
{
    [Header("Teleport Settings")]
    [Tooltip("Tầm nhìn của Player")]
    public float visionRange = 20.0f;
    public float vanishDuration = 0.2f;

    [Header("Strike Settings")]
    //public float damageMultiplier = 2.0f; // Sát thương x2
    public float bleedDuration = 10.0f;   // Chảy máu 10s
    public float bleedPercentPerSec = 0.02f; // Mất 2% máu/giây

    [Header("VFX")]
    public GameObject vanishVfxPrefab;
    public GameObject reappearVfxPrefab;
    public GameObject strikeVfxPrefab;
    public GameObject bleedVfxPrefab;

    private EquipmentManager equipmentManager;
    private SpriteRenderer spriteRenderer;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        spriteRenderer = myPlayer.GetComponentInChildren<SpriteRenderer>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(SignatureRoutine());
        return true;
    }

    private IEnumerator SignatureRoutine()
    {
        // 1. TÌM KẺ ĐỊCH XA NHẤT TRONG TẦM NHÌN
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRange, player.dangerLayer);
        Stats furthestEnemy = null;
        float maxDist = -1f;

        foreach (var hit in hits)
        {
            Stats enemy = hit.GetComponent<Stats>();
            if (enemy != null && enemy.currentHp > 0)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    furthestEnemy = enemy;
                }
            }
        }

        if (furthestEnemy == null)
        {
            Debug.Log("<color=gray>Rogue: Không có mục tiêu trong tầm nhìn!</color>");
            yield break; // Thoát nếu không có quái
        }

        // 2. BIẾN MẤT VÀ VÔ ĐỊCH
        player.isAttacking = true;
        stats.isInvincible = true;
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        //if (vanishVfxPrefab) Instantiate(vanishVfxPrefab, transform.position, Quaternion.identity);

        yield return new WaitForSeconds(vanishDuration);

        // 3. TÍNH TỌA ĐỘ SAU LƯNG ĐỊCH
        Vector3 enemyForward = furthestEnemy.facingDirection;
        if (enemyForward == Vector3.zero) enemyForward = furthestEnemy.transform.forward;
        enemyForward.y = 0;
        enemyForward.Normalize();

        Collider targetCol = furthestEnemy.GetComponent<Collider>();
        Collider playerCol = player.GetComponent<Collider>();

        float targetRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;
        float playerRadius = playerCol != null ? Mathf.Max(playerCol.bounds.extents.x, playerCol.bounds.extents.z) : 0.5f;

        float safeDistance = targetRadius + playerRadius + 0.2f;
        Vector3 backPosition = furthestEnemy.transform.position - enemyForward * safeDistance;

        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(backPosition, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            player.transform.position = navHit.position;
        }
        else
        {
            player.transform.position = backPosition;
        }

        player.ForceFaceDirection(enemyForward);

        if (spriteRenderer != null) spriteRenderer.enabled = true;
        //if (reappearVfxPrefab) Instantiate(reappearVfxPrefab, transform.position, Quaternion.identity);

        // 4. ĐÂM LÉN CHÍ MẠNG
        //if (strikeVfxPrefab) Instantiate(strikeVfxPrefab, furthestEnemy.transform.position, Quaternion.LookRotation(enemyForward));

        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        // Player đã đứng sau lưng địch; isGuaranteedCrit = true (đâm lén chí mạng)
        DamageHelper.ApplyStandardDamage(stats, furthestEnemy, transform, data.skillPhysicalMultiplier, data, currentWpn, 0, false, 0f, true);

        // 5. GÂY CHẢY MÁU ĐỘC LẬP (Không dùng hàm của Stats.cs)
        StartCoroutine(IndependentBleedRoutine(furthestEnemy, targetRadius));

        Debug.Log($"<color=red>Rogue: Đâm lén chí mạng {furthestEnemy.name}!</color>");

        // 6. KẾT THÚC
        yield return new WaitForSeconds(0.3f);
        stats.isInvincible = false;
        player.isAttacking = false;
    }

    // --- COROUTINE RÚT MÁU ĐỘC LẬP ---
    private IEnumerator IndependentBleedRoutine(Stats target, float targetRadius)
    {
        float timer = 0f;

        // Tính toán sát thương chuẩn: 2% Max HP
        float tickDamage = target.maxHp * bleedPercentPerSec;

        // Sinh VFX chảy máu
        //GameObject bleedVfx = null;
        //if (bleedVfxPrefab)
        //{
        //    bleedVfx = Instantiate(bleedVfxPrefab, target.transform);
        //    bleedVfx.transform.localPosition = Vector3.up * targetRadius;
        //}

        // Vòng lặp rút máu
        while (timer < bleedDuration && target != null && target.currentHp > 0)
        {
            yield return new WaitForSeconds(1.0f);
            timer += 1.0f;

            // Truyền trực tiếp sát thương vào hệ thống
            DamageInfo bleedInfo = new DamageInfo();
            bleedInfo.sourcePosition = target.transform.position;
            bleedInfo.physDamage = tickDamage;
            bleedInfo.attacker = stats;
            bleedInfo.sourceType = DamageSourceType.DoT;

            target.TakeDamage(bleedInfo);
            Debug.Log($"<color=red>Ám sát (Rút máu):</color> {target.name} mất {tickDamage} HP.");
        }

        //if (bleedVfx != null) Destroy(bleedVfx);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}