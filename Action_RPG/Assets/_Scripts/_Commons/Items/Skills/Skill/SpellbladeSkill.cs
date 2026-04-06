using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SpellbladeSkill : SkillBehavior
{
    [Header("Wave Settings (Kiếm Khí)")]
    public float waveDistance = 5.0f;       // Tầm bay của kiếm khí
    [Range(0, 360)] public float waveAngle = 90f; // Góc hình nón (90 độ trước mặt)

    [Header("Phase 1: Initial Damage & Piercing")]
    public float baseDamageMultiplier = 1.5f; // Sát thương gốc ban đầu
    public float damageFalloffPerHit = 0.2f;  // Giảm 20% sát thương mỗi lần xuyên thấu
    public float minDamageMultiplier = 0.5f;  // Giảm tối đa còn 50% sát thương

    [Header("Phase 2: Prediction Counter (Phản Đòn Tầm Xa)")]
    public float markDuration = 2.0f;         // Dấu ấn phản đòn tồn tại 2s
    public float counterDamageMult = 2.0f;    // Sát thương khi phản đòn (x2)
    public float counterStunDuration = 1.5f;  // Choáng 1.5s

    [Header("VFX Prefabs")]
    public GameObject waveVfxPrefab;          // Hiệu ứng kiếm khí chém ra
    public GameObject markVfxPrefab;          // Hiệu ứng ấn ký trên đầu kẻ địch
    public GameObject counterBlastVfxPrefab;  // Hiệu ứng nổ khi phản đòn thành công

    private EquipmentManager equipmentManager;

    // --- LỚP LƯU TRỮ DỮ LIỆU ẤN KÝ ---
    private class MarkedEnemy
    {
        public float timer;
        public GameObject vfxInstance;
        public EnemyCombat combatComponent;
    }

    // Từ điển theo dõi kẻ địch bị đánh dấu
    private Dictionary<Stats, MarkedEnemy> markedEnemies = new Dictionary<Stats, MarkedEnemy>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        CleanUpAllMarks();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        StartCoroutine(WaveRoutine());

        return true;
    }

    // ==========================================================
    // THEO DÕI VÀ KÍCH HOẠT PHẢN ĐÒN MỖI FRAME
    // ==========================================================
    void Update()
    {
        if (markedEnemies.Count == 0) return;

        List<Stats> expiredMarks = new List<Stats>();

        foreach (var kvp in markedEnemies)
        {
            Stats enemy = kvp.Key;
            MarkedEnemy data = kvp.Value;

            // 1. Kiểm tra mục tiêu còn hợp lệ không
            if (enemy == null || enemy.currentHp <= 0)
            {
                expiredMarks.Add(enemy);
                continue;
            }

            // 2. Trừ thời gian tồn tại của ấn ký
            data.timer -= Time.deltaTime;
            if (data.timer <= 0)
            {
                expiredMarks.Add(enemy);
                continue;
            }

            // 3. LOGIC KÍCH HOẠT PHẢN ĐÒN: 
            // Nếu kẻ địch ĐANG BẮT ĐẦU ĐÁNH -> Cắn trả ngay lập tức!
            if (data.combatComponent != null && data.combatComponent.isAttacking)
            {
                TriggerCounterAttack(enemy, data.combatComponent);
                expiredMarks.Add(enemy); // Xóa ấn ký sau khi nổ
            }
        }

        // Xóa các ấn ký đã hết hạn hoặc đã nổ
        foreach (var enemy in expiredMarks)
        {
            //if (markedEnemies[enemy].vfxInstance != null) Destroy(markedEnemies[enemy].vfxInstance);
            markedEnemies.Remove(enemy);
        }
    }

    // ==========================================================
    // PHÓNG KIẾM KHÍ & ĐÁNH DẤU
    // ==========================================================
    private IEnumerator WaveRoutine()
    {
        player.isAttacking = true;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        //if (waveVfxPrefab) Instantiate(waveVfxPrefab, transform.position + Vector3.up, Quaternion.LookRotation(forward));

        Debug.Log("<color=cyan>SPELLBLADE: ARCANE CRESCENT!</color>");

        // 1. QUÉT HÌNH NÓN (CONE OVERLAP)
        Collider[] hits = Physics.OverlapSphere(transform.position, waveDistance, player.dangerLayer);
        List<Stats> hitEnemies = new List<Stats>();

        foreach (var hit in hits)
        {
            Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(forward, dirToEnemy);

            // Kiểm tra xem quái có nằm trong góc vung kiếm không (Cone filter)
            if (angle <= waveAngle / 2f)
            {
                Stats enemyStats = hit.GetComponent<Stats>();
                if (enemyStats != null && enemyStats.currentHp > 0 && !hitEnemies.Contains(enemyStats))
                {
                    hitEnemies.Add(enemyStats);
                }
            }
        }

        // 2. SẮP XẾP QUÁI THEO KHOẢNG CÁCH TỪ GẦN ĐẾN XA (Để tính Xuyên Thấu giảm sát thương)
        hitEnemies.Sort((a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position))
        );

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);

        // 3. GÂY SÁT THƯƠNG VÀ ĐÁNH DẤU
        float currentDamageMult = baseDamageMultiplier;

        for (int i = 0; i < hitEnemies.Count; i++)
        {
            Stats enemy = hitEnemies[i];

            // A. Gây sát thương kiếm khí (Có suy giảm theo lần xuyên)
            float t = CombatMath.CalculateDirectionFactor(transform, enemy);
            bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

            float damage = CombatMath.CalculateFullDamage(stats, enemy, t, isCrit, data, currentWpn, currentDamageMult);

            DamageInfo info = new DamageInfo
            {
                sourcePosition = transform.position,
                attacker = stats,
                damageAmount = damage,
                isCrit = isCrit,
                impactLevel = 0
            };
            enemy.TakeDamage(info);

            // Giảm hệ sát thương cho mục tiêu tiếp theo phía sau
            currentDamageMult -= damageFalloffPerHit;
            if (currentDamageMult < minDamageMultiplier) currentDamageMult = minDamageMultiplier;

            // B. Gắn Ấn Ký Phản Đòn (Lời nguyền)
            if (!markedEnemies.ContainsKey(enemy))
            {
                EnemyCombat enemyCombat = enemy.GetComponent<EnemyCombat>();
                //GameObject vfx = null;
                //if (markVfxPrefab) vfx = Instantiate(markVfxPrefab, enemy.transform.position + Vector3.up * 2f, Quaternion.identity, enemy.transform);

                markedEnemies[enemy] = new MarkedEnemy
                {
                    timer = markDuration,
                    //vfxInstance = vfx,
                    combatComponent = enemyCombat
                };
            }
            else
            {
                markedEnemies[enemy].timer = markDuration; // Làm mới nếu đã có
            }
        }

        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    // ==========================================================
    // THỰC THI PHẢN ĐÒN KHI KẺ ĐỊCH VUNG VŨ KHÍ
    // ==========================================================
    private void TriggerCounterAttack(Stats enemy, EnemyCombat enemyCombat)
    {
        Debug.Log($"<color=magenta>SPELLBLADE COUNTER:</color> Ngắt chiêu {enemy.name}!");

        //if (counterBlastVfxPrefab) Instantiate(counterBlastVfxPrefab, enemy.transform.position, Quaternion.identity);

        // 1. [QUAN TRỌNG NHẤT] GỌI HÀM HỦY ĐÒN ĐÁNH CỦA ĐỊCH!
        if (enemyCombat != null)
        {
            enemyCombat.CancelAttack();
        }

        // 2. TÍNH TOÁN SÁT THƯƠNG GỐC CỰC MẠNH (Không bị giảm do xuyên thấu)
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemy);

        float damage = CombatMath.CalculateFullDamage(stats, enemy, t, isCrit, data, currentWpn, counterDamageMult);

        DamageInfo info = new DamageInfo
        {
            sourcePosition = transform.position,
            attacker = stats,
            damageAmount = damage,
            isCrit = isCrit,
            isStun = true,                  // Choáng cứng
            stunDuration = counterStunDuration,
            impactLevel = 2                 // Đảm bảo phá Siêu Giáp
        };

        enemy.TakeDamage(info);
    }

    private void CleanUpAllMarks()
    {
        foreach (var kvp in markedEnemies)
        {
            if (kvp.Value.vfxInstance != null) Destroy(kvp.Value.vfxInstance);
        }
        markedEnemies.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (Application.isPlaying && stats != null) ? stats.facingDirection : transform.forward;
        if (forward == Vector3.zero) forward = transform.forward;

        // Vẽ hình nón thể hiện tầm bay của kiếm khí
        Gizmos.color = Color.cyan;
        Vector3 leftRay = Quaternion.AngleAxis(-waveAngle / 2, Vector3.up) * forward;
        Vector3 rightRay = Quaternion.AngleAxis(waveAngle / 2, Vector3.up) * forward;

        Gizmos.DrawRay(transform.position, leftRay * waveDistance);
        Gizmos.DrawRay(transform.position, rightRay * waveDistance);
        Gizmos.DrawWireSphere(transform.position, waveDistance);
    }
}