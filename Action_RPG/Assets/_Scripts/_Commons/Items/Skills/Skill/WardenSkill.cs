using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;

public class WardenSkill : SkillBehavior
{
    [Header("Phase 1: Leap & Slam Settings")]
    public float jumpDistance = 2.0f;       // Khoảng cách nhảy tới
    public float jumpHeight = 2.0f;         // Độ cao của cú nhảy
    public float jumpDuration = 0.5f;       // Thời gian nhảy

    [Header("Phase 2: Dash & Push Settings")]
    public float dashSpeed = 5.0f;         // Tốc độ lướt đẩy địch
    public float dashDuration = 0.4f;       // Thời gian lướt
    public float pushHitRadius = 1.5f;      // Bán kính hút/đẩy quái

    [Header("Phase 3: Slash 1 (Armor/MR Scaling)")]
    public float slash1Radius = 3.0f;
    public float slash1BaseMultiplier = 1.0f; // Hệ số cơ bản
    public float armorToDamageScale = 1.5f;   // 1 Armor = 1.5 Sát thương
    public float mrToDamageScale = 1.5f;      // 1 MR = 1.5 Sát thương

    [Header("Phase 4: Slash 2 (True Damage)")]
    public float slash2Radius = 3.5f;
    public float flatTrueDamageBase = 50f;    // Sát thương chuẩn cơ bản
    public float vitToTrueDamageScale = 2.0f; // 1 VIT = 2 Sát thương chuẩn
    public float armorToTrueDamageScale = 0.5f;
    public float mrToTrueDamageScale = 0.5f;
    public float targetMaxHpPercent = 0.05f;  // 5% Max HP của mục tiêu
    public float stunDuration = 2.0f;         // Choáng 2 giây

    [Header("Phase 5: Self Buff")]
    public float defenseValueBuff = 150f;     // Nhận 150 DV
    public float buffDuration = 3.0f;         // Tồn tại 3s

    [Header("VFX Prefabs")]
    public GameObject slamVfxPrefab;
    public GameObject slash1VfxPrefab;
    public GameObject slash2VfxPrefab;
    public GameObject buffVfxPrefab;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;
    private AllyStats allyStats;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        allyStats = myStats; // Cache lại thành AllyStats để lấy chỉ số VIT
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(WardenSkillRoutine());
        return true;
    }

    private IEnumerator WardenSkillRoutine()
    {
        // ==========================================
        // SETUP TRẠNG THÁI (KHÔNG THỂ CANCEL)
        // ==========================================
        player.isUsingSpecialSkill = true; // Khóa toàn bộ input của người chơi

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = player.transform.forward;
        forward.y = 0; forward.Normalize();

        bool originalUseGravity = rb.useGravity;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        // Lưu Super Armor cũ để trả lại
        bool oldIsSuperArmor = stats.isSuperArmor;
        int oldSuperArmorLevel = stats.superArmorLevel;

        WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

        try
        {
            // ==========================================
            // PHASE 1: JUMP & SLAM (BẤT TỬ)
            // ==========================================
            stats.isInvincible = true;
            Debug.Log("<color=cyan>WARDEN: NHẢY LÊN CAO!</color>");

            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + forward * jumpDistance;

            // Tìm điểm tiếp đất an toàn trên NavMesh (tránh kẹt tường)
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                targetPos = hit.position;

            float timer = 0f;
            while (timer < jumpDuration)
            {
                float t = timer / jumpDuration;
                // Tạo quỹ đạo Parabol (Lerp ngang + Sin dọc)
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * jumpHeight;

                rb.MovePosition(currentPos);

                timer += Time.fixedDeltaTime;
                yield return waitFixed;
            }

            stats.isInvincible = false;
            //if (slamVfxPrefab != null) Instantiate(slamVfxPrefab, transform.position, Quaternion.identity);

            // ==========================================
            // PHASE 2: DASH & PUSH (UNSTOPPABLE)
            // ==========================================
            stats.isSuperArmor = true;
            stats.superArmorLevel = 99; // Miễn nhiễm mọi CC
            Debug.Log("<color=orange>WARDEN: LƯỚT ỦI ĐỊCH!</color>");

            timer = 0f;
            while (timer < dashDuration)
            {
                float dt = Time.fixedDeltaTime;
                rb.linearVelocity = forward * dashSpeed;

                Vector3 centerPoint = transform.position + Vector3.up * 0.5f;
                Collider[] pushHits = Physics.OverlapSphere(centerPoint, pushHitRadius, player.dangerLayer);
                Vector3 nextFramePos = rb.position + (forward * dashSpeed * dt);

                foreach (var col in pushHits)
                {
                    Stats enemy = col.GetComponent<Stats>();
                    if (enemy != null && enemy.currentHp > 0)
                    {
                        // Hàm đẩy quái dính vào người (Drag)
                        HandleContinuousPush(col.gameObject, forward, nextFramePos);
                    }
                }

                timer += dt;
                yield return waitFixed;
            }
            rb.linearVelocity = Vector3.zero; // Dừng lại sau khi lướt

            // ==========================================
            // PHASE 3: CHÉM ĐÒN 1 (SCALE ARMOR & MR)
            // ==========================================
            yield return new WaitForSeconds(0.1f); // Nhịp delay để vung vũ khí
            Debug.Log("<color=yellow>WARDEN: CHÉM ĐÒN 1!</color>");

            //if (slash1VfxPrefab != null) Instantiate(slash1VfxPrefab, transform.position + forward, Quaternion.LookRotation(forward));

            stats.EnterCombat();
            WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
            float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);

            Collider[] slash1Hits = Physics.OverlapSphere(transform.position + forward, slash1Radius, player.dangerLayer);
            foreach (var col in slash1Hits)
            {
                Stats enemyStats = col.GetComponent<Stats>();
                if (enemyStats != null && enemyStats.currentHp > 0)
                {
                    float tFactor = CombatMath.CalculateDirectionFactor(transform, enemyStats);
                    bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

                    // Sát thương chuẩn bị từ hệ thống
                    var baseDamage = CombatMath.CalculateFullDamage(stats, enemyStats, tFactor, isCrit, data, currentWpn, slash1BaseMultiplier);

                    // Cộng thêm sát thương từ Armor và Magic Resist
                    float bonusFromDef = (stats.armor * armorToDamageScale) + (stats.magicResist * mrToDamageScale);
                    DamageInfo info1 = new DamageInfo
                    {
                        sourcePosition = transform.position,
                        attacker = stats,

                        // Bóc tách gói hàng: Cộng sát thương từ Giáp thẳng vào sát thương Vật lý
                        physDamage = baseDamage.phys + bonusFromDef,
                        magicDamage = baseDamage.magic,
                        trueDamage = baseDamage.trueDmg,

                        isCrit = isCrit,
                        impactLevel = 1
                    };
                    enemyStats.TakeDamage(info1);
                }
            }

            // ==========================================
            // PHASE 4: CHÉM ĐÒN 2 (TRUE DAMAGE + STUN 2S)
            // ==========================================
            yield return new WaitForSeconds(0.3f); // Nhịp delay dồn sức
            Debug.Log("<color=red>WARDEN: CHÉM ĐÒN 2 (SÁT THƯƠNG CHUẨN)!</color>");

            //if (slash2VfxPrefab != null) Instantiate(slash2VfxPrefab, transform.position + forward, Quaternion.LookRotation(forward));

            Collider[] slash2Hits = Physics.OverlapSphere(transform.position + forward, slash2Radius, player.dangerLayer);
            foreach (var col in slash2Hits)
            {
                Stats enemyStats = col.GetComponent<Stats>();
                if (enemyStats != null && enemyStats.currentHp > 0)
                {
                    // Lấy chỉ số VIT từ AllyStats
                    float currentVit = allyStats != null ? allyStats.VIT : 0f;

                    // Tính toán Sát thương chuẩn (True Damage)
                    float trueDamage = flatTrueDamageBase
                                     + (stats.armor * armorToTrueDamageScale)
                                     + (stats.magicResist * mrToTrueDamageScale)
                                     + (currentVit * vitToTrueDamageScale)
                                     + (enemyStats.maxHp * targetMaxHpPercent);

                    // Khởi tạo DamageInfo không cần chạy qua CombatMath để bỏ qua chỉ số giảm thương của địch
                    DamageInfo info2 = new DamageInfo
                    {
                        sourcePosition = transform.position,
                        attacker = stats,
                        trueDamage = trueDamage, // Trừ thẳng số máu này
                        isCrit = false, // True damage thường không chí mạng, nếu bạn muốn có thể thêm vào
                        isStun = true,
                        stunDuration = stunDuration,
                        impactLevel = 2 // Phá Siêu giáp
                    };

                    enemyStats.TakeDamage(info2);
                    Debug.Log($"<color=red>True Damage:</color> Gây {trueDamage} sát thương chuẩn lên {enemyStats.name}!");
                }
            }

            // ==========================================
            // PHASE 5: TỰ BUFF DEFENSE VALUE TRONG 3 GIÂY
            // ==========================================
            StartCoroutine(DefenseBuffRoutine());
            // Đợi thêm 0.2s để kết thúc dáng dấp rồi mới cho di chuyển lại
            yield return new WaitForSeconds(0.2f);

        }
        finally
        {
            // ==========================================
            // CLEANUP TRẠNG THÁI AN TOÀN TỐI ĐA
            // ==========================================
            rb.useGravity = originalUseGravity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            stats.isInvincible = false;
            stats.isSuperArmor = oldIsSuperArmor;
            stats.superArmorLevel = oldSuperArmorLevel;

            player.isUsingSpecialSkill = false;
        }
    }

    // Hàm bế quái đi theo (Dựa theo thuật toán của ChrisSkill)
    private void HandleContinuousPush(GameObject enemyObj, Vector3 dashDir, Vector3 playerNextPos)
    {
        Rigidbody enemyRb = enemyObj.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            Vector3 idealPos = playerNextPos + (dashDir * 1.5f); // Đẩy ra trước mặt 1.5m
            idealPos.y = enemyRb.position.y;
            enemyRb.MovePosition(idealPos);

            if (!enemyRb.isKinematic) enemyRb.linearVelocity = Vector3.zero;
        }
    }

    // Coroutine xử lý nhận 150 Defense Value
    private IEnumerator DefenseBuffRoutine()
    {
        stats.defenseValue += defenseValueBuff;
        Debug.Log($"<color=green>WARDEN BUFF:</color> Nhận {defenseValueBuff} Defense Value. Tổng hiện tại: {stats.defenseValue}");

        GameObject buffVfx = null;
        if (buffVfxPrefab != null) buffVfx = Instantiate(buffVfxPrefab, transform);

        yield return new WaitForSeconds(buffDuration);

        stats.defenseValue -= defenseValueBuff;
        if (buffVfx != null) Destroy(buffVfx);
        Debug.Log($"<color=gray>Warden Buff Hết hạn:</color> Defense Value trở về {stats.defenseValue}");
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = Application.isPlaying && stats != null ? stats.facingDirection : transform.forward;
        if (forward == Vector3.zero) forward = transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + forward, slash1Radius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + forward, slash2Radius);
    }
}