using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleMageSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float radius = 3.0f;           // Bán kính vòng tròn năng lượng
    //public float damageMultiplier = 1.8f; // Hệ số sát thương phép
    public float stunDuration = 1.5f;     // Thời gian choáng

    [Header("Healing Settings")]
    public int minEnemiesToHeal = 2;      // Cần trúng ít nhất 2 địch để kích hoạt hồi máu
    public float healAmountPerEnemy = 15f; // Hồi 15 HP trên mỗi kẻ địch trúng đòn
    public float maxHealLimit = 90f;      // Giới hạn hồi máu tối đa mỗi lần dậm

    [Header("VFX")]
    public GameObject impactVfxPrefab;    // Hiệu ứng vòng tròn năng lượng tỏa ra

    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(StampRoutine());
        return true;
    }

    private IEnumerator StampRoutine()
    {
        // 1. Setup trạng thái
        player.isAttacking = true;

        // Dừng di chuyển một chút để thực hiện động tác dậm chân
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // [Tùy chọn] Chơi animation dậm chân
        // if (animator != null) animator.SetTrigger("Stamp");

        yield return new WaitForSeconds(0.2f); // Chờ nhịp dậm chân chạm đất

        // 2. Kích hoạt hiệu ứng hình ảnh
        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        }

        // 3. Quét kẻ địch xung quanh
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, player.dangerLayer);

        int hitCount = 0;
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // Tính toán sát thương phép
                float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);
                bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

                // Sử dụng data.skillMagicMultiplier của bạn
                float damage = CombatMath.CalculateFullDamage(
                    stats, enemyStats, t, isCrit, data, currentWpn, data.skillMagicMultiplier
                );

                DamageInfo info = new DamageInfo();
                info.sourcePosition = transform.position;
                info.attacker = stats;
                info.damageAmount = damage;
                info.isCrit = isCrit;
                info.isStun = true;
                info.stunDuration = stunDuration;
                info.impactLevel = 1;

                enemyStats.TakeDamage(info);
                hitCount++;
            }
        }

        // 4. Xử lý Hồi máu (Sustain)
        if (hitCount >= minEnemiesToHeal)
        {
            float totalHeal = Mathf.Min(hitCount * healAmountPerEnemy, maxHealLimit);
            stats.Heal(totalHeal);
            Debug.Log($"<color=green>BattleMage:</color> Trúng {hitCount} địch, hồi {totalHeal} HP!");

            // Có thể thêm một VFX hồi máu nhỏ ở đây
        }

        // 5. Kết thúc
        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}