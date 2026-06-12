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
    private Dictionary<Stats, float> activeSlows = new Dictionary<Stats, float>();

    // Class lưu trữ lượng giáp đã trừ để trả lại cho đúng
    private class SunderData
    {
        public float timer;
        public float armorShredded;
        public float mrShredded;
    }
    private Dictionary<Stats, SunderData> activeSunders = new Dictionary<Stats, SunderData>();

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

    // ==========================================
    // VÒNG LẶP KIỂM TRA & GỠ DEBUFF MỖI FRAME
    // ==========================================
    void Update()
    {
        // 1. Quản lý Làm chậm (Slow)
        if (activeSlows.Count > 0)
        {
            List<Stats> expiredSlows = new List<Stats>();
            foreach (var enemy in activeSlows.Keys.ToList())
            {
                activeSlows[enemy] -= Time.deltaTime;
                if (activeSlows[enemy] <= 0) expiredSlows.Add(enemy);
            }
            foreach (var enemy in expiredSlows)
            {
                if (enemy != null) enemy.baseMoveSpeed /= (1-slowPercent); // Trả lại tốc độ
                activeSlows.Remove(enemy);
            }
        }

        // 2. Quản lý Giảm thủ (Sunder)
        if (activeSunders.Count > 0)
        {
            List<Stats> expiredSunders = new List<Stats>();
            foreach (var enemy in activeSunders.Keys.ToList())
            {
                activeSunders[enemy].timer -= Time.deltaTime;
                if (activeSunders[enemy].timer <= 0) expiredSunders.Add(enemy);
            }
            foreach (var enemy in expiredSunders)
            {
                if (enemy != null)
                {
                    enemy.armor += activeSunders[enemy].armorShredded;
                    enemy.magicResist += activeSunders[enemy].mrShredded;
                }
                activeSunders.Remove(enemy);
            }
        }
    }

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
                DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, data.skillMagicMultiplier, data, currentWpn, 0, true, 0.2f);

                // B. REFRESH DEBUFF (LỬA)
                float burnDps = stats.magicAtk * burnDamagePercent;
                enemyStats.ApplyBurn(burnDps, burnDuration);

                // C. REFRESH DEBUFF (BĂNG - Làm chậm)
                if (!activeSlows.ContainsKey(enemyStats))
                {
                    enemyStats.baseMoveSpeed *= (1-slowPercent); // Trừ lần đầu
                }
                activeSlows[enemyStats] = chillDuration; // Cập nhật/Làm mới thời gian

                // D. REFRESH DEBUFF (ĐẤT - Giảm giáp)
                if (!activeSunders.ContainsKey(enemyStats))
                {
                    float armorToReduce = enemyStats.armor * defReductionPercent;
                    float mrToReduce = enemyStats.magicResist * defReductionPercent;

                    enemyStats.armor -= armorToReduce;
                    enemyStats.magicResist -= mrToReduce;

                    // Lưu lại lượng đã trừ để trả cho đúng
                    activeSunders[enemyStats] = new SunderData
                    {
                        timer = sunderDuration,
                        armorShredded = armorToReduce,
                        mrShredded = mrToReduce
                    };
                }
                else
                {
                    activeSunders[enemyStats].timer = sunderDuration; // Làm mới thời gian
                }
            }
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
        foreach (var enemy in activeSlows.Keys)
        {
            if (enemy != null) enemy.baseMoveSpeed /= (1 - slowPercent);
        }
        activeSlows.Clear();

        foreach (var kvp in activeSunders)
        {
            if (kvp.Key != null)
            {
                kvp.Key.armor += kvp.Value.armorShredded;
                kvp.Key.magicResist += kvp.Value.mrShredded;
            }
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