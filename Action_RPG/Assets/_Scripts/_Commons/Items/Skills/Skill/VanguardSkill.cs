using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VanguardSkill : SkillBehavior
{
    [Header("Charge Settings")]
    public float dashSpeed = 5f;         // Tốc độ lướt (nên nhanh hơn Chris một chút)
    public float dashDuration = 1.0f;     // Lướt trong 0.8s
    public float hitRadius = 0.3f;        // Bán kính hút/ủi quái
    public float hitOffsetY = 0f;       // Có thể Nâng tâm lên ngang bụng (0.5)
    public float pushOffsetDistance = 1.5f; // Đẩy quái ra trước mặt 1.5m

    [Header("Slam Settings (End of Dash)")]
    public float slamRadius = 2.5f;       // Tầm nổ đập đất
    //public float slamDamageMultiplier = 1.5f; // Sát thương x1.5
    public float stunDuration = 1.5f;     // Choáng 1.5s

    [Header("VFX")]
    public GameObject chargeAuraVfx;      // Hiệu ứng khiên năng lượng lúc đang lướt
    public GameObject slamVfxPrefab;      // Hiệu ứng nứt đất/sóng xung kích lúc đập

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip()
    {
        if (stats != null && stats.damageInterceptor == FrontalBlockInterceptor)
        {
            stats.damageInterceptor = null;
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(ShieldChargeRoutine());
        return true;
    }

    private IEnumerator ShieldChargeRoutine()
    {
        // --- 1. SETUP ---
        player.isUsingSpecialSkill = true; // Khóa Input

        Vector3 dashDir = stats.facingDirection;
        if (dashDir == Vector3.zero) dashDir = player.transform.forward;
        dashDir.y = 0;
        dashDir.Normalize();

        // Bật khiên chặn sát thương phía trước
        stats.damageInterceptor = FrontalBlockInterceptor;
        GameObject aura = null;
        //if (chargeAuraVfx != null) aura = Instantiate(chargeAuraVfx, transform);

        // [Tùy chọn] Animator chạy thế thủ khiên
        // if (animator != null) animator.SetBool("IsCharging", true);

        float timer = 0f;
        bool originalUseGravity = rb.useGravity;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        // Danh sách lưu quái đã bị hút để tiện cấp hiệu ứng
        List<Stats> draggedEnemies = new List<Stats>();

        try
        {
            // --- 2. GIAI ĐOẠN 1: LAO TỚI VÀ KÉO QUÁI ---
            WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

            while (timer < dashDuration)
            {
                float dt = Time.fixedDeltaTime;

                // Ép tốc độ lao tới
                rb.linearVelocity = dashDir * dashSpeed;
                rb.angularVelocity = Vector3.zero;

                Vector3 centerPoint = transform.position + Vector3.up * hitOffsetY;
                Collider[] hits = Physics.OverlapSphere(centerPoint, hitRadius, player.dangerLayer);
                Vector3 nextFramePos = rb.position + (dashDir * dashSpeed * dt);

                foreach (var hit in hits)
                {
                    Stats enemyStats = hit.GetComponent<Stats>();
                    if (enemyStats != null && enemyStats.currentHp > 0)
                    {
                        // Kéo quái theo (Không gây sát thương lúc này)
                        HandleContinuousPush(hit.gameObject, dashDir, nextFramePos);

                        if (!draggedEnemies.Contains(enemyStats))
                            draggedEnemies.Add(enemyStats);
                    }
                }

                timer += dt;
                yield return waitFixed;
            }

            // Dừng xe lại sau khi lướt xong
            rb.linearVelocity = Vector3.zero;

            // --- 3. GIAI ĐOẠN 2: ĐẬP ĐẤT (SLAM) ---
            // Tắt hiệu ứng lướt
            //if (aura != null) Destroy(aura);
            stats.damageInterceptor = null; // Tắt khiên vô địch

            // [Tùy chọn] Animator động tác đập
            // if (animator != null) { animator.SetBool("IsCharging", false); animator.SetTrigger("Slam"); }

            ExecuteSlam();

            // Nếu có tông trúng quái trên đường đi, tích Sin cho Player
            if (draggedEnemies.Count > 0)
            {
                stats.GainSinFromAttack(draggedEnemies.Count);
            }

        }
        finally
        {
            // --- 4. CLEANUP (An toàn vật lý) ---
            rb.useGravity = originalUseGravity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            stats.damageInterceptor = null;
            if (aura != null) Destroy(aura);
        }

        // Chờ thêm 1 nhịp nhẹ để xả đòn đập xong mới cho đi bộ lại
        yield return new WaitForSeconds(0.3f);
        player.isUsingSpecialSkill = false;
    }

    // --- HÀM KÉO QUÁI ĐI THEO ---
    private void HandleContinuousPush(GameObject enemyObj, Vector3 dashDir, Vector3 playerNextPos)
    {
        Rigidbody enemyRb = enemyObj.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            Vector3 idealPos = playerNextPos + (dashDir * pushOffsetDistance);
            idealPos.y = enemyRb.position.y;
            enemyRb.MovePosition(idealPos);

            if (!enemyRb.isKinematic) enemyRb.linearVelocity = Vector3.zero;
        }
    }

    // --- HÀM ĐẬP ĐẤT GÂY SÁT THƯƠNG VÀ CHOÁNG ---
    private void ExecuteSlam()
    {
        Debug.Log("<color=red>VANGUARD: SLAM!</color>");

        //if (slamVfxPrefab != null) Instantiate(slamVfxPrefab, transform.position + transform.forward, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius, player.dangerLayer);

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
                bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
                float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

                var dmgTuple = CombatMath.CalculateFullDamage(
                    stats, enemyStats, t, isCrit, data, currentWpn, data.skillPhysicalMultiplier // Sử dụng multiplier của skill để tính sát thương đập đất
                );

                DamageInfo info = new DamageInfo();
                info.sourcePosition = transform.position;
                info.attacker = stats;
                info.physDamage = dmgTuple.phys;
                info.magicDamage = dmgTuple.magic;
                info.trueDamage = dmgTuple.trueDmg;
                info.isCrit = isCrit;

                // Gây choáng 1.5s
                info.isStun = true;
                info.stunDuration = stunDuration;

                // Đập lùi ra xa một chút cho có lực
                info.isKnockback = true;
                info.knockbackForce = 5f;
                info.impactLevel = 2; // Phá siêu giáp

                enemyStats.TakeDamage(info);
            }
        }
    }

    // --- HÀM CHẶN SÁT THƯƠNG TỪ PHÍA TRƯỚC ---
    private bool FrontalBlockInterceptor(DamageInfo info)
    {
        if (info.attacker == null || info.attacker == stats) return false;

        Vector3 dirToSource = (info.sourcePosition - transform.position).normalized;
        dirToSource.y = 0;

        Vector3 forward = stats.facingDirection;
        forward.y = 0;

        // Nếu nguồn gốc sát thương nằm trong góc 180 độ phía trước mặt
        if (Vector3.Angle(forward, dirToSource) <= 90f)
        {
            Debug.Log("<color=cyan>Shield Charge:</color> Chặn 100% sát thương từ phía trước!");
            return true; // Trả về true để Stats.cs HỦY sát thương
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * hitOffsetY, hitRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}