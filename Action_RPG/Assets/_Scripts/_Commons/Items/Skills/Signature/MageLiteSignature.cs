using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MageLiteSignature : SkillBehavior
{
    [Header("Rectangle AOE Settings (Hình chữ nhật dài)")]
    public float boxWidth = 1.5f;          // Chiều rộng hình chữ nhật
    public float boxLength = 6.0f;        // Chiều dài hình chữ nhật
    //public float damageMultiplier = 3.0f;   // Sát thương hỗn hợp khổng lồ (x3)

    [Header("--- Fire Effect ---")]
    public float burnDuration = 5.0f;       // Thiêu đốt 5s
    [Tooltip("Hệ số sát thương thiêu đốt tính theo % Magic Attack")]
    public float burnDamagePercent = 0.15f; // Sát thương Burn = 15% MagicAtk/tick (1s)

    [Header("--- Ice Effect ---")]
    public float chillDuration = 5.0f;      // Tê tái (Chậm) 5s
    public float slowPercent = 0.4f;        // Làm chậm 40%

    [Header("--- Wind Effect (Self Buff) ---")]
    public float hazeDuration = 5.0f;       // Tăng tốc 5s
    public float selfSpeedBuff = 0.3f;      // Tăng 30% Tốc chạy & Tốc đánh

    [Header("--- Earth Effect ---")]
    public float sunderDuration = 5.0f;     // Giảm thủ 5s
    public float defReductionPercent = 0.25f; // Giảm 25% Armor & MagicResist

    [Header("VFX Prefabs (Gắn hiệu ứng nổ vào đây)")]
    public GameObject elementalBeamVfxPrefab; // Hiệu ứng 4 chùm tia bắn ra

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

        StartCoroutine(SignatureRoutine());

        return true;
    }

    private IEnumerator SignatureRoutine()
    {
        // --- 1. SETUP HƯỚNG BẮN ---
        player.isAttacking = true;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();
        Quaternion rotation = Quaternion.LookRotation(forward);

        // Kích hoạt hiệu ứng hình ảnh 4 chùm tia bắn ra
        //if (elementalBeamVfxPrefab != null) Instantiate(elementalBeamVfxPrefab, transform.position, rotation);

        Debug.Log("<color=magenta>MAGE LITE: ELEMENTAL CONVERGENCE!</color>");

        // --- 2. QUÉT VÙNG HÌNH CHỮ NHẬT ĐỂ TÍNH SÁT THƯƠNG ---
        // Vị trí tâm hình chữ nhật là ở giữa chiều dài (Length / 2)
        Vector3 centerPos = transform.position + forward * (boxLength * 0.5f);

        // Kích thước Box: Length (Dài), Width (Rộng), Height (Cao cố định)
        Vector3 halfExtents = new Vector3(boxWidth * 0.5f, 1.0f, boxLength * 0.5f);

        // Quét kẻ địch trong vùng hình chữ nhật (Box Overlap)
        Collider[] hits = Physics.OverlapBox(centerPos, halfExtents, rotation, player.dangerLayer);

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        // --- 3. ÁP DỤNG SÁT THƯƠNG VÀ HIỆU ỨNG LÊN TỪNG KẺ ĐỊCH ---
        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // Tính sát thương (x3, hỗn hợp)
                DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, data.skillMagicMultiplier, data, currentWpn, 0);

                // --- Gây 4 loại Debuff Nguyên Tố ---
                // A. Lửa (Thiêu Đốt - Sử dụng hàm thiêu đốt chuẩn)
                float burnDps = stats.magicAtk * burnDamagePercent;
                enemyStats.ApplyBurn(burnDps, burnDuration);

                // B. Băng (Làm Chậm)
                StartCoroutine(TempSlowRoutine(enemyStats, slowPercent, chillDuration));

                // C. Đất (Giảm song thủ: Armor & MagicResist)
                StartCoroutine(TempSunderRoutine(enemyStats, defReductionPercent, sunderDuration));
            }
        }

        // --- 4. Gió (Self Buff: Tăng tốc bản thân) ---
        StartCoroutine(SelfHasteRoutine(selfSpeedBuff, hazeDuration));

        // --- 5. CLEANUP ---
        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    // ==========================================================
    // CÁC COROUTINE HIỆU ỨNG TẠM THỜI
    // ==========================================================

    //
    private IEnumerator TempSlowRoutine(Stats enemy, float percent, float duration)
    {
        if (enemy != null && enemy.currentHp > 0)
        {
            enemy.baseMoveSpeed *= (1-percent);
            // enemyStats is a parent variable, you may need a component variable in the new context if it is not defined
            yield return new WaitForSeconds(duration);
            if (enemy != null) enemy.baseMoveSpeed /= (1-percent);
        }
    }

    private IEnumerator TempSunderRoutine(Stats enemy, float percent, float duration)
    {
        if (enemy != null && enemy.currentHp > 0)
        {
            float armorToReduce = enemy.armor * percent;
            float mrToReduce = enemy.magicResist * percent;

            enemy.armor -= armorToReduce;
            enemy.magicResist -= mrToReduce;

            yield return new WaitForSeconds(duration);

            if (enemy != null && enemy.currentHp > 0)
            {
                enemy.armor += armorToReduce;
                enemy.magicResist += mrToReduce;
            }
        }
    }

    private IEnumerator SelfHasteRoutine(float percent, float duration)
    {
        stats.bonusMoveSpeed += percent;
        stats.bonusAttackSpeed += percent;

        // Gọi hàm cập nhật lại chỉ số di chuyển
        if (stats is AllyStats ally) ally.CalculateMoveSpeedOnly();

        yield return new WaitForSeconds(duration);

        stats.bonusMoveSpeed -= percent;
        stats.bonusAttackSpeed -= percent;

        if (stats is AllyStats allyRevert) allyRevert.CalculateMoveSpeedOnly();
    }

    // ==========================================================
    // VẼ KHUNG AOE TRONG EDITOR ĐỂ CÂN CHỈNH
    // ==========================================================
    void OnDrawGizmosSelected()
    {
        Vector3 forward = (Application.isPlaying && stats != null) ? stats.facingDirection : transform.forward;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        Vector3 centerPos = transform.position + forward * (boxLength * 0.5f);
        Vector3 boxSize = new Vector3(boxWidth, 2.0f, boxLength);
        Quaternion rotation = Quaternion.LookRotation(forward);

        Gizmos.color = Color.magenta;
        Gizmos.matrix = Matrix4x4.TRS(centerPos, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }
}