using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleMageLiteSignature : SkillBehavior
{
    [Header("Explosion Settings")]
    public float chargeTime = 0.5f;       // Thời gian tụ năng lượng
    public float explosionRadius = 3.0f;  // Bán kính nổ

    [Header("Damage Scaling")]
    //public float baseDamageMultiplier = 1.0f; // Hệ số sát thương gốc
    public float bonusDamagePerEnemy = 0.2f;  // Thêm 0.2 hệ số cho MỖI kẻ địch trúng chiêu
    public float maxDamage = 3.0f; // Sát thương tối đa không quá 300%

    [Header("Heal Scaling")]
    public float baseHealPercent = 0.02f;     // Hồi gốc 2% Max HP
    public float bonusHealPerEnemy = 0.015f;  // Thêm 1.5% Max HP cho MỖI kẻ địch
    public float maxHealPercent = 0.30f;      // Giới hạn hồi tối đa 30% để tránh bất tử

    [Header("VFX")]
    public GameObject chargeVfxPrefab;    // Hiệu ứng hút/tụ năng lượng
    public GameObject explosionVfxPrefab; // Hiệu ứng nổ tung

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
        StartCoroutine(SignatureRoutine());
        return true;
    }

    private IEnumerator SignatureRoutine()
    {
        // 1. TỤ NĂNG LƯỢNG (CHARGE)
        player.isAttacking = true; // Khóa di chuyển/đánh thường
        rb.linearVelocity = Vector3.zero;

        // Bật VFX Tụ năng lượng
        //GameObject chargeVfx = null;
        //if (chargeVfxPrefab != null)
        //{
        //    chargeVfx = Instantiate(chargeVfxPrefab, transform.position, Quaternion.identity, transform);
        //}

        // Chờ thời gian tụ
        yield return new WaitForSeconds(chargeTime);
        //if (chargeVfx != null) Destroy(chargeVfx);

        // 2. PHÁT NỔ (EXPLOSION)
        //if (explosionVfxPrefab != null)
        //{
        //    Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
        //}

        // Lấy danh sách toàn bộ kẻ địch trong tầm nổ
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, player.dangerLayer);
        List<Stats> validEnemies = new List<Stats>();

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                validEnemies.Add(enemyStats);
            }
        }

        int enemyCount = validEnemies.Count;

        if (enemyCount > 0)
        {
            Debug.Log($"<color=purple>Arcane Burst:</color> Trúng {enemyCount} kẻ địch!");

            // Chỉ tính cộng thêm từ kẻ địch THỨ 2 trở đi
            int bonusEnemies = Mathf.Max(0, enemyCount - 1);

            // 3. TÍNH TOÁN CỘNG DỒN SÁT THƯƠNG
            // Công thức: Multiplier = Base + (Số địch từ thứ 2 trở đi * 0.2)
            float finalDamageMultiplier = data.skillMagicMultiplier + (bonusEnemies * bonusDamagePerEnemy);
            finalDamageMultiplier = Mathf.Min(finalDamageMultiplier, maxDamage); // Giới hạn không quá 300%
            // Gây sát thương lên tất cả kẻ địch trong danh sách
            foreach (Stats enemy in validEnemies)
            {
                ApplyDamage(enemy, finalDamageMultiplier);
            }

            // 4. TÍNH TOÁN HỒI MÁU
            // Công thức: Heal = Base (2%) + (Số địch từ thứ 2 trở đi * 1.5%)
            float totalHealPercent = baseHealPercent + (bonusEnemies * bonusHealPerEnemy);
            totalHealPercent = Mathf.Min(totalHealPercent, maxHealPercent); // Giới hạn hồi máu

            float healAmount = stats.maxHp * totalHealPercent;
            stats.Heal(healAmount); // Sử dụng hàm Heal có sẵn trong Stats.cs chặn hồi máu nếu dính debuff

            Debug.Log($"<color=green>Hồi phục:</color> {healAmount} HP ({totalHealPercent * 100}%).");
        }
        else
        {
            Debug.Log("<color=gray>Arcane Burst: Nổ hụt (Không trúng ai).</color>");
        }

        // Mở khóa sau khi nổ xong một nhịp ngắn
        yield return new WaitForSeconds(0.2f);
        player.isAttacking = false;
    }

    private void ApplyDamage(Stats enemyStats, float multiplier)
    {
        stats.EnterCombat();
        DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, multiplier, data, equipmentManager.currentWeapon, 0);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}