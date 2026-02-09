using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChrisSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float dashSpeed = 5f; 
    public float dashDuration = 2f; 
    public float hitRadius = 0.1f;
    public float knockbackForce = 0.5f;

    [Tooltip("Góc để xác định là húc thẳng (độ)")]
    public float headOnAngle = 30f;

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
        StartCoroutine(DashAndBashRoutine());
        return true;
    }

    private IEnumerator DashAndBashRoutine()
    {
        // 1. Xác định hướng lướt
        Vector3 dashDir = stats.facingDirection;
        if (dashDir == Vector3.zero) dashDir = player.transform.forward;
        dashDir.y = 0;
        dashDir.Normalize();

        float timer = 0f;
        List<GameObject> alreadyDamagedList = new List<GameObject>();

        // 2. Setup Physics: Bật Kinematic để Player xuyên qua địch
        bool originalKinematic = rb.isKinematic;
        rb.isKinematic = true;

        while (timer < dashDuration)
        {
            // Di chuyển Player (Kinematic Move)
            rb.MovePosition(rb.position + dashDir * dashSpeed * Time.deltaTime);

            // 3. Quét va chạm
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, hitRadius, player.dangerLayer);

            foreach (var hit in hits)
            {
                // Chỉ xử lý nếu chưa gây damage cho đối tượng này trong lần lướt này
                if (alreadyDamagedList.Contains(hit.gameObject)) continue;

                Stats enemyStats = hit.GetComponent<Stats>();
                if (enemyStats != null && enemyStats.currentHp > 0)
                {
                    alreadyDamagedList.Add(hit.gameObject);

                    // GỌI HÀM XỬ LÝ DAMAGE & KNOCKBACK (Giống PlayerController)
                    ApplyDamage(enemyStats, dashDir);
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 4. Reset Physics
        rb.isKinematic = originalKinematic;
        if (!originalKinematic) rb.linearVelocity = Vector3.zero;
    }

    private void ApplyDamage(Stats enemyStats, Vector3 dashDir)
    {
        // --- CHUẨN BỊ DỮ LIỆU SÁT THƯƠNG ---
        bool wasAlive = enemyStats.currentHp > 0;
        stats.EnterCombat();

        // 1. Tính toán hướng và góc va chạm
        Vector3 impactDir = (enemyStats.transform.position - transform.position);
        impactDir.y = 0;
        impactDir.Normalize();

        float angle = Vector3.Angle(dashDir, impactDir);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        // 2. Tính Crit
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

        // 3. TẠO DAMAGE INFO (Giống PlayerController)
        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position; // Để hệ thống tự tính hướng đẩy lùi (Center to Center)
        info.isCrit = isCrit;

        // --- LOGIC KNOCKBACK ---
        info.isKnockback = true;

        if (angle <= headOnAngle)
        {
            // Case Húc Thẳng: Lực mạnh hơn + Gây choáng (Impact Level cao)
            info.knockbackForce = knockbackForce;
            info.impactLevel = 2; // Giả sử mức 2 là mạnh
            info.isStun = true;   // Khuyến mãi thêm stun nếu húc trực diện
            info.stunDuration = 0.5f;
            Debug.Log("Húc trực diện, bị đẩy lùi");
        }
        else
        {
            // Case Húc Sượt: Lực thường
            info.knockbackForce = knockbackForce;
            info.impactLevel = 1;
            info.isStun = true;
            info.stunDuration = 0.1f;
            Debug.Log("Húc bên rìa, bị văng ra ngoài");
        }

        // 4. Tính toán lượng Damage
        // (Có thể nhân thêm hệ số skill damage ở tham số cuối cùng, ví dụ 1.2f)
        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, 1f
        );
        info.damageAmount = damage;

        // 5. GỬI ĐI
        enemyStats.TakeDamage(info);
    }
}