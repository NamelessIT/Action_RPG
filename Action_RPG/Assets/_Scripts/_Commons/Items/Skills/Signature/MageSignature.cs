using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Để dùng hàm ToList() cho Dictionary

public class MageSignature : SkillBehavior
{
    [Header("Storm Settings (Siêu bão)")]
    public float stormDuration = 5.0f;      // Bão tồn tại 5s
    public float tickRate = 0.5f;           // Giật sát thương mỗi 0.5s
    public float stormRadius = 5.0f;        // Bán kính cơn bão
    public float spawnOffsetDistance = 3.0f; // Khoảng cách từ người chơi đến tâm bão
    //public float damageMultiplierPerTick = 0.6f; // Sát thương mỗi nhịp (0.6 * 10 nhịp = x6 sát thương tổng)

    [Header("--- Fire Effect ---")]
    public float burnDuration = 3.0f;       // Thiêu đốt duy trì 3s sau khi ra khỏi bão
    public float burnDamagePercent = 0.15f; // Sát thương Burn = 15% MagicAtk mỗi nhịp của Stats.cs

    [Header("--- Ice Effect ---")]
    public float chillDuration = 3.0f;      // Tê tái duy trì 3s
    public float slowPercent = 0.4f;        // Làm chậm 40%

    [Header("--- Wind Effect (Self Buff) ---")]
    public float hazeDuration = 5.0f;       // Tăng tốc bản thân 5s
    public float selfSpeedBuff = 0.3f;      // Tăng 30% Tốc chạy & Tốc đánh

    [Header("--- Earth Effect ---")]
    public float sunderDuration = 3.0f;     // Giảm thủ duy trì 3s
    public float defReductionPercent = 0.25f; // Giảm 35% Armor & MagicResist

    [Header("VFX Prefabs")]
    public GameObject stormVfxPrefab;       // Hiệu ứng siêu bão kéo dài 5s

    private EquipmentManager equipmentManager;
    private Coroutine stormCoroutine;
    private GameObject currentStormVfx;

    // ==========================================
    // BỘ QUẢN LÝ DEBUFF (CHỐNG CỘNG DỒN)
    // ==========================================

    // F2F: giữ snapshot amount + handle để registry sở hữu lifecycle nhưng vẫn bảo toàn
    // semantics cũ: amount chỉ snapshot ở lần apply đầu, các pulse sau chỉ refresh duration.
    private class SunderData
    {
        public float armorShredded;
        public float mrShredded;
        public EffectHandle handle;
    }
    private readonly Dictionary<Stats, SunderData> activeSunders = new Dictionary<Stats, SunderData>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        if (stormCoroutine != null) StopCoroutine(stormCoroutine);
        if (currentStormVfx != null) Destroy(currentStormVfx);
        CleanUpAllDebuffs();
    }

    // F2F: Sunder expiry/cleanse do StatusEffectController quản lý; không còn timer/revert mỗi frame ở producer.

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (stormCoroutine != null) StopCoroutine(stormCoroutine);
        stormCoroutine = StartCoroutine(StormRoutine());

        return true;
    }

    private IEnumerator StormRoutine()
    {
        // --- 1. SETUP VỊ TRÍ TÂM BÃO ---
        player.isAttacking = true;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        Vector3 centerPos = transform.position + forward * spawnOffsetDistance;

        if (currentStormVfx != null) Destroy(currentStormVfx);
        if (stormVfxPrefab != null) currentStormVfx = Instantiate(stormVfxPrefab, centerPos, Quaternion.identity);

        Debug.Log("<color=magenta>MAGE SIGNATURE: TRIỆU HỒI SIÊU BÃO MA THUẬT!</color>");

        // --- 2. SELF BUFF (GIÓ) ---
        StartCoroutine(SelfHasteRoutine(selfSpeedBuff, hazeDuration));

        // Mở khóa cho Player di chuyển sau khi niệm chú xong (0.5s)
        yield return new WaitForSeconds(0.5f);
        player.isAttacking = false;

        // --- 3. VÒNG LẶP SÁT THƯƠNG CỦA CƠN BÃO ---
        float timer = 0f;
        while (timer < stormDuration)
        {
            ProcessStormTick(centerPos);
            yield return new WaitForSeconds(tickRate);
            timer += tickRate;
        }

        // --- 4. KẾT THÚC BÃO ---
        if (currentStormVfx != null) Destroy(currentStormVfx);
        stormCoroutine = null;
    }

    private void ProcessStormTick(Vector3 centerPos)
    {
        Collider[] hits = Physics.OverlapSphere(centerPos, stormRadius, player.dangerLayer);

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // A. GÂY SÁT THƯƠNG HỖN HỢP
                DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, data.skillMagicMultiplier, data, currentWpn, 0, sourceType: DamageSourceType.DoT);

                // B. REFRESH DEBUFF (LỬA)
                float burnDps = stats.magicAtk * burnDamagePercent;
                enemyStats.ApplyBurn(burnDps, burnDuration, stats);

                // C. REFRESH DEBUFF (BĂNG - Làm chậm) — qua effect system (strongest-wins, mỗi pulse refresh).
                enemyStats.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, chillDuration) { magnitude = slowPercent }, stats);

                // D. REFRESH DEBUFF (ĐẤT - Sunder Armor/MR)
                ApplyOrRefreshSunder(enemyStats);
            }
        }
    }

    private void ApplyOrRefreshSunder(Stats enemyStats)
    {
        if (enemyStats == null || enemyStats.currentHp <= 0) return;

        const string effectId = "MageSignature.Storm.Sunder";
        string sourceKey = $"{effectId}:{GetInstanceID()}";

        if (!activeSunders.TryGetValue(enemyStats, out SunderData data))
        {
            // Source hiện tại snapshot raw Armor/MR ở lần apply đầu; refresh không recalc amount.
            data = new SunderData
            {
                armorShredded = Mathf.Max(0f, enemyStats.armor * defReductionPercent),
                mrShredded = Mathf.Max(0f, enemyStats.magicResist * defReductionPercent),
                handle = EffectHandle.Invalid
            };
            activeSunders[enemyStats] = data;
        }

        EffectApplyResult result = enemyStats.ApplyArmorMagicResistDown(
            effectId, data.armorShredded, data.mrShredded, sunderDuration, stats, sourceKey,
            EffectStackPolicy.RefreshSameSource, 1, defReductionPercent,
            "Mage Signature Earth Sunder 25% Armor/MR snapshot + refresh",
            (snapshot, reason) =>
            {
                if (activeSunders.TryGetValue(enemyStats, out SunderData current)
                    && current.handle.Equals(snapshot.handle))
                    activeSunders.Remove(enemyStats);
            });

        if (result.Accepted)
        {
            data.handle = result.handle;
            activeSunders[enemyStats] = data;
        }
    }

    private IEnumerator SelfHasteRoutine(float percent, float duration)
    {
        stats.bonusMoveSpeed += percent;
        stats.bonusAttackSpeed += percent;

        if (stats is AllyStats ally) ally.CalculateMoveSpeedOnly();

        yield return new WaitForSeconds(duration);

        stats.bonusMoveSpeed -= percent;
        stats.bonusAttackSpeed -= percent;

        if (stats is AllyStats allyRevert) allyRevert.CalculateMoveSpeedOnly();
    }

    private void CleanUpAllDebuffs()
    {
        // Slow tự hết hạn qua effect system. Sunder do registry sở hữu, nhưng legacy OnUnequip
        // gỡ Sunder ngay nên giữ behavior bằng cách remove các handle do signature này tạo.
        var snapshot = activeSunders.ToList();
        foreach (var kvp in snapshot)
        {
            if (kvp.Key != null && kvp.Key.StatusEffects != null && kvp.Value.handle.IsValid)
                kvp.Key.StatusEffects.RemoveEffect(kvp.Value.handle, EffectRemoveReason.Manual);
        }
        activeSunders.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (Application.isPlaying && stats != null) ? stats.facingDirection : transform.forward;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        Vector3 centerPos = transform.position + forward * spawnOffsetDistance;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(centerPos, stormRadius);
    }
}