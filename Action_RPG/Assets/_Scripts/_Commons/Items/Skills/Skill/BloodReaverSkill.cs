using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BloodReaverSkill : SkillBehavior
{
    [Header("BOW MODE Settings")]
    public float arrowSpeed = 8f;
    public float arrowRange = 30f;
    public float arrowRadius = 0.5f; // Độ to của mũi tên
    public float damageReductionPerHit = 0.15f; // Giảm 15% mỗi lần xuyên
    public float maxDamageReduction = 0.45f;    // Tối đa giảm 45%

    [Header("MELEE MODE Settings")]
    public float sweepRadius = 2.5f;
    public float sweepAngle = 180f; // Quét 180 độ trước mặt
    public float recoilDistance = 2.0f; // Khoảng cách giật lùi
    public float recoilSpeed = 10f;

    [Header("Heal & Bonus Settings")]
    public float healPerEnemyPercent = 0.05f; // Hồi 5% máu mỗi địch
    public float maxHealPercent = 0.20f;      // Tối đa 20%
    public float bleedBonusDamage = 1.5f;     // x1.5 sát thương nếu địch đang chảy máu

    private EquipmentManager equipmentManager;
    private Rigidbody rb;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        rb = myPlayer.GetComponent<Rigidbody>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;

        WeaponData currentWpn = equipmentManager.currentWeapon;

        // Kiểm tra loại vũ khí
        if (currentWpn != null && currentWpn.weaponType == WeaponData.WeaponType.Bow)
        {
            // --- CASE 1: CUNG (BOW) ---
            StartCoroutine(BowRoutine());
        }
        else
        {
            // --- CASE 2: KIẾM/THƯƠNG (SWORD/SPEAR/OTHER) ---
            // Mặc định các vũ khí cận chiến sẽ dùng logic này
            StartCoroutine(MeleeRoutine());
        }

        return true;
    }

    // =========================================================
    // LOGIC 1: BOW (XUYÊN THẤU & GIẢM DAMAGE)
    // =========================================================
    private IEnumerator BowRoutine()
    {
        // 1. Tạo mũi tên ảo (Primitive Sphere để test, sau này bạn thay bằng Prefab)
        GameObject arrowVis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        arrowVis.transform.localScale = Vector3.one * 0.3f;
        arrowVis.GetComponent<Collider>().enabled = false; // Tắt collider của visual đi

        // Setup vị trí bắn
        Vector3 startPos = transform.position; //+ Vector3.up; // Bắn từ ngực
        arrowVis.transform.position = startPos;

        // Xác định hướng bắn
        Vector3 fireDir = stats.facingDirection;
        if (fireDir == Vector3.zero) fireDir = transform.forward;
        fireDir.y = 0; fireDir.Normalize();

        float traveledDist = 0f;
        List<GameObject> hitList = new List<GameObject>(); // Danh sách địch đã xuyên qua
        int hitCount = 0;

        // Vòng lặp bay
        while (traveledDist < arrowRange)
        {
            float step = arrowSpeed * Time.deltaTime;
            Vector3 nextPos = arrowVis.transform.position + fireDir * step;

            // Di chuyển visual
            arrowVis.transform.position = nextPos;
            traveledDist += step;

            // Quét va chạm trên đường bay (Raycast hoặc OverlapSphere)
            Collider[] hits = Physics.OverlapSphere(arrowVis.transform.position, arrowRadius, LayerMask.GetMask("Enemy"));

            int validHits = 0; // Đếm số địch trúng chiêu để tính hồi máu

            foreach (var hit in hits)
            {
                if (!hitList.Contains(hit.gameObject))
                {
                    Stats enemyStats = hit.GetComponent<Stats>();
                    if (enemyStats != null && enemyStats.currentHp > 0)
                    {
                        hitList.Add(hit.gameObject);

                        // Tính toán giảm damage dựa trên số lượng kẻ địch ĐÃ xuyên qua (hitCount)
                        float reduction = Mathf.Min(hitCount * damageReductionPerHit, maxDamageReduction);
                        float bonusMult = enemyStats.isBleeding ? bleedBonusDamage : 1.0f;
                        float damageMult = (1.0f - reduction) * bonusMult;

                        if (enemyStats.isBleeding)
                            Debug.Log($"<color=red>EXECUTE!</color> Địch đang chảy máu -> X{bonusMult} Damage");

                        // Gây sát thương
                        DealDamage(enemyStats, damageMult);

                        hitCount++; // Tăng đếm sau khi đã tính damage
                    }
                }
            }
            // 3. Hồi máu (Tối đa 20%)
            if (validHits > 0)
            {
                // Công thức: Min(Số địch * 5%, 20%)
                float healPercent = Mathf.Min(validHits * healPerEnemyPercent, maxHealPercent);
                float healAmount = stats.maxHp * healPercent;

                stats.currentHp += healAmount;
                if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;

                Debug.Log($"<color=green>Huyết Tế:</color> Trúng {validHits} địch -> Hồi {healAmount} HP");
            }

            yield return null;
        }

        Destroy(arrowVis); // Xóa mũi tên khi bay hết tầm
    }

    // =========================================================
    // LOGIC 2: MELEE (QUÉT VÙNG & HỒI MÁU & GIẬT LÙI)
    // =========================================================
    private IEnumerator MeleeRoutine()
    {
        // 1. Xác định hướng
        Vector3 forwardDir = stats.facingDirection;
        if (forwardDir == Vector3.zero) forwardDir = transform.forward;

        // 2. Quét vùng (OverlapSphere + Góc)
        Collider[] hits = Physics.OverlapSphere(transform.position, sweepRadius, LayerMask.GetMask("Enemy"));

        int validHits = 0; // Đếm số địch trúng chiêu để tính hồi máu

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;

                // Kiểm tra góc (180 độ tức là góc kẹp giữa hướng nhìn và địch < 90)
                if (Vector3.Angle(forwardDir, dirToEnemy) <= sweepAngle / 2)
                {
                    // Trúng chiêu!
                    validHits++;

                    // Kiểm tra Bleed để bonus damage (1.5x)
                    float bonusMult = enemyStats.isBleeding ? bleedBonusDamage : 1.0f;

                    if (enemyStats.isBleeding)
                        Debug.Log($"<color=red>EXECUTE!</color> Địch đang chảy máu -> X{bonusMult} Damage");

                    DealDamage(enemyStats, bonusMult);
                }
            }
        }

        // 3. Hồi máu (Tối đa 20%)
        if (validHits > 0)
        {
            // Công thức: Min(Số địch * 5%, 20%)
            float healPercent = Mathf.Min(validHits * healPerEnemyPercent, maxHealPercent);
            float healAmount = stats.maxHp * healPercent;

            stats.currentHp += healAmount;
            if (stats.currentHp > stats.maxHp) stats.currentHp = stats.maxHp;

            Debug.Log($"<color=green>Huyết Tế:</color> Trúng {validHits} địch -> Hồi {healAmount} HP");
        }

        // 4. Giật lùi (Recoil)
        // Dùng MovePosition hoặc Velocity trong thời gian ngắn
        stats.isInvincible = true; // Bất tử ngắn khi giật lùi (Optional)

        Vector3 recoilDir = -forwardDir; // Ngược hướng nhìn
        float recoilTimer = 0f;
        float recoilDuration = recoilDistance / recoilSpeed;

        while (recoilTimer < recoilDuration)
        {
            rb.linearVelocity = recoilDir * recoilSpeed;
            recoilTimer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector3.zero;
        stats.isInvincible = false;
    }

    // Hàm gây sát thương chung
    private void DealDamage(Stats enemyStats, float multiplier)
    {
        bool wasAlive = enemyStats.currentHp > 0;
        stats.EnterCombat();

        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        // Tính damage
        // multiplier ở đây là:
        // - Với Bow: Hệ số giảm dần (1.0 -> 0.85 -> 0.7...) và Hệ số bonus bleed (1.0 hoặc 1.5)
        // - Với Melee: Hệ số bonus bleed (1.0 hoặc 1.5)
        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, multiplier
        );

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.damageAmount = damage;
        // Bow thì có thể knockback nhẹ, Melee thì đẩy mạnh (tùy chỉnh sau)
        info.isKnockback = true;
        info.knockbackForce = 0.5f;

        enemyStats.TakeDamage(info);
    }
}