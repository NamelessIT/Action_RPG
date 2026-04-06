using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpellbinderSkill : SkillBehavior
{
    [Header("Phase 1: Overload & Shield")]
    [Tooltip("Hệ số nhân Giáp Ảo dựa trên INT của Player (Ví dụ: 5.0 -> 100 INT = 500 Giáp)")]
    public float shieldIntMultiplier = 5.0f; 
    public float overloadDuration = 4.0f;

    [Header("Phase 2: Taunt & Relocate")]
    public float tauntRadius = 15.0f;
    public float searchMobRadius = 20.0f;
    public float clusterRadius = 4.0f;

    [Header("Phase 3: Detonation")]
    public float explosionRadius = 5.0f;
    //public float explosionDamageMult = 3.0f;
    public float stunDuration = 1.5f;

    [Header("VFX Prefabs")]
    public GameObject castBeamVfx;
    public GameObject overloadAuraVfx;
    public GameObject explosionVfx;

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
        CompanionAI companion = FindFirstObjectByType<CompanionAI>();
        if (companion == null)
        {
            Debug.Log("<color=red>SPELLBINDER: Không tìm thấy thú cưng để cường hóa!</color>");
            return false;
        }

        if (!base.Use()) return false;

        StartCoroutine(SpellbinderRoutine(companion));
        return true;
    }

    private IEnumerator SpellbinderRoutine(CompanionAI companion)
    {
        player.isAttacking = true;

        Stats compStats = companion.GetComponent<Stats>();
        UnityEngine.AI.NavMeshAgent compAgent = companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
        GameObject auraInstance = null;

        bool wasAIEnabled = companion.enabled;

        try
        {
            // ==========================================
            // 1. TRUYỀN NĂNG LƯỢNG VÀ TẠO GIÁP ẢO (SCALE THEO INT)
            // ==========================================
            //if (castBeamVfx)
            //{
            //    Instantiate(castBeamVfx, transform.position, Quaternion.LookRotation(companion.transform.position - transform.position));
            //}
            //if (overloadAuraVfx) auraInstance = Instantiate(overloadAuraVfx, companion.transform);

            // [ĐÃ SỬA] Tính toán Giáp Ảo dựa trên INT hiện tại của Player
            float appliedShield = stats.INT * shieldIntMultiplier;
            compStats.currentShield += appliedShield;
            Debug.Log($"<color=cyan>SPELLBINDER:</color> Bơm {appliedShield} Giáp Ảo (Dựa trên {stats.INT} INT) cho Companion!");

            // ==========================================
            // 2. KHIÊU KHÍCH ĐỊCH (TAUNT VÀO COMPANION)
            // ==========================================
            Collider[] hitsToTaunt = Physics.OverlapSphere(companion.transform.position, tauntRadius, player.dangerLayer);
            foreach (var hit in hitsToTaunt)
            {
                EnemyAI enemyAI = hit.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    enemyAI.OnDamageTaken(companion.transform);
                }
            }

            // ==========================================
            // 3. TÌM VỊ TRÍ ĐÔNG KẺ ĐỊCH NHẤT
            // ==========================================
            Vector3 targetPosition = companion.transform.position;
            Collider[] allEnemies = Physics.OverlapSphere(transform.position, searchMobRadius, player.dangerLayer);

            if (allEnemies.Length > 0)
            {
                int maxClusterSize = -1;
                Transform bestTarget = null;

                foreach (var enemy in allEnemies)
                {
                    Collider[] neighbors = Physics.OverlapSphere(enemy.transform.position, clusterRadius, player.dangerLayer);
                    if (neighbors.Length > maxClusterSize)
                    {
                        maxClusterSize = neighbors.Length;
                        bestTarget = enemy.transform;
                    }
                }

                if (bestTarget != null)
                {
                    targetPosition = bestTarget.position;
                    Debug.Log($"<color=orange>SMART BOMB:</color> Phát hiện cụm {maxClusterSize} mục tiêu. Đang áp sát!");
                }
            }

            // ==========================================
            // 4. CHIẾM QUYỀN ĐIỀU KHIỂN & LÙA COMPANION VÀO BÃI
            // ==========================================
            companion.enabled = false;
            if (compAgent)
            {
                compAgent.isStopped = false;
                compAgent.speed *= 1.5f;
                compAgent.SetDestination(targetPosition);
            }

            yield return new WaitForSeconds(0.2f);
            player.isAttacking = false;

            // ==========================================
            // 5. CHỜ ĐẾN VỊ TRÍ HOẶC HẾT 4 GIÂY HOẶC VỠ GIÁP
            // ==========================================
            float timer = 0f;
            while (timer < overloadDuration)
            {
                if (compAgent && compAgent.remainingDistance <= 1.5f && !compAgent.pathPending) break;
                if (compStats.currentShield <= 0) break;

                timer += Time.deltaTime;
                yield return null;
            }

            // ==========================================
            // 6. KÍCH NỔ (DETONATION)
            // ==========================================
            //if (explosionVfx) Instantiate(explosionVfx, companion.transform.position, Quaternion.identity);
            //if (auraInstance) Destroy(auraInstance);

            ExecuteExplosion(companion.transform.position);

            if (compStats.currentShield > 0) compStats.currentShield = 0;
        }
        finally
        {
            // ==========================================
            // 7. TRẢ LẠI AI CHO COMPANION
            // ==========================================
            if (compAgent)
            {
                compAgent.speed /= 1.5f;
                compAgent.isStopped = true;
                compAgent.ResetPath();
            }
            companion.enabled = wasAIEnabled;
            player.isAttacking = false;
        }
    }

    private void ExecuteExplosion(Vector3 explosionCenter)
    {
        Debug.Log("<color=red>SPELLBINDER: QUÁ TẢI PHÁT NỔ!</color>");

        Collider[] hits = Physics.OverlapSphere(explosionCenter, explosionRadius, player.dangerLayer);

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);
                bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

                float damage = CombatMath.CalculateFullDamage(
                    stats, enemyStats, t, isCrit, data, currentWpn, data.skillMagicMultiplier
                );

                DamageInfo info = new DamageInfo
                {
                    sourcePosition = explosionCenter,
                    attacker = stats,
                    damageAmount = damage,
                    isCrit = isCrit,
                    isStun = true,
                    stunDuration = stunDuration,
                    impactLevel = 2
                };

                enemyStats.TakeDamage(info);

                // Đợi hết choáng thì ép quái vật chuyển mục tiêu sang Player
                EnemyAI enemyAI = enemyStats.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    StartCoroutine(AggroPlayerAfterStunRoutine(enemyAI, stunDuration));
                }
            }
        }
    }

    // ==========================================================
    // CHUYỂN AGGRO SANG PLAYER SAU KHI HẾT CHOÁNG
    // ==========================================================
    private IEnumerator AggroPlayerAfterStunRoutine(EnemyAI enemyAI, float delay)
    {
        // Đợi bằng đúng thời gian bị Stun
        yield return new WaitForSeconds(delay);

        // Kẻ địch vẫn còn sống thì mới chuyển Aggro
        if (enemyAI != null)
        {
            Stats enemyStats = enemyAI.GetComponent<Stats>();
            if (enemyStats != null && !enemyStats.isDead)
            {
                // Gọi hàm OnDamageTaken, truyền Transform của Player vào để khóa mục tiêu
                enemyAI.OnDamageTaken(transform);
                Debug.Log($"<color=orange>[Spellbinder]</color> {enemyStats.name} đã tỉnh choáng và quay sang trả thù Player!");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, searchMobRadius);
    }
}