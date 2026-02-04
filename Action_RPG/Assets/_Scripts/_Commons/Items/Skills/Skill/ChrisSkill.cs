using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI; // Cần thêm thư viện này để xử lý NavMeshAgent nếu có

public class ChrisSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float dashSpeed = 5f;
    public float dashDuration = 2f;
    public float hitRadius = 0.1f; // Tăng radius lên xíu để dễ quét trúng
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
        Vector3 dashDir = stats.facingDirection;
        if (dashDir == Vector3.zero) dashDir = player.transform.forward;
        dashDir.y = 0;
        dashDir.Normalize();

        float timer = 0f;
        List<GameObject> alreadyDamagedList = new List<GameObject>();

        // [QUAN TRỌNG 1] Bật chế độ "Bóng ma" (Kinematic) cho Player
        // Để không bị chặn lại bởi địch hay vật cản
        bool originalKinematic = rb.isKinematic;
        rb.isKinematic = true;

        // Tắt va chạm giữa Player và EnemyLayer tạm thời (Optional, nhưng Kinematic là đủ rồi)
        //Physics.IgnoreLayerCollision(...) 

        while (timer < dashDuration)
        {
            // [QUAN TRỌNG 2] Di chuyển Player bằng MovePosition (Dành cho Kinematic)
            // Di chuyển vị trí hiện tại + vận tốc * thời gian
            rb.MovePosition(rb.position + dashDir * dashSpeed * Time.deltaTime);

            // B. Quét va chạm
            // Dịch tâm quét lên 1 chút để quét chính xác hơn
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up + dashDir * 0.5f, hitRadius, player.enemyLayer);

            foreach (var hit in hits)
            {
                Stats enemyStats = hit.GetComponent<Stats>();
                Rigidbody enemyRb = hit.GetComponent<Rigidbody>();

                if (enemyStats != null && enemyStats.currentHp > 0 && enemyRb != null)
                {
                    // --- PHẦN 1: XỬ LÝ VẬT LÝ ---
                    Vector3 impactDir = (hit.transform.position - transform.position);
                    impactDir.y = 0;
                    impactDir.Normalize();

                    float angle = Vector3.Angle(dashDir, impactDir);

                    // Xử lý NavMeshAgent (Nếu địch là Kinematic thường có NavMeshAgent)
                    NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled)
                    {
                        agent.velocity = Vector3.zero; // Reset vận tốc NavMesh
                        // Nếu muốn đẩy mượt, đôi khi cần tắt agent.updatePosition hoặc tắt luôn agent
                        // Ở đây ta dùng cách đẩy transform cưỡng bức
                    }

                    if (angle <= headOnAngle)
                    {
                        // === CASE 1: HÚC THẲNG ===
                        // Đẩy nhanh hơn tốc độ lướt của mình để tạo cảm giác "ủn"
                        Vector3 pushVelocity = dashDir * (dashSpeed * 1.2f);

                        if (!enemyRb.isKinematic)
                        {
                            enemyRb.linearVelocity = pushVelocity;
                        }
                        else
                        {
                            // Kinematic: Dịch chuyển transform
                            enemyRb.transform.position += pushVelocity * Time.deltaTime;
                        }
                    }
                    else
                    {
                        // === CASE 2: HÚC DẠT (SANG BÊN) ===
                        // [QUAN TRỌNG 3] Tính vector dạt chuẩn hơn
                        // Ta lấy vector vuông góc với hướng lướt để tạo lực đẩy ngang thuần túy
                        // Hoặc đơn giản là dùng impactDir nhưng nhân lực mạnh hơn

                        if (!enemyRb.isKinematic)
                        {
                            if (enemyRb.linearVelocity.magnitude < knockbackForce)
                            {
                                enemyRb.AddForce(impactDir * knockbackForce, ForceMode.Impulse);
                            }
                        }
                        else
                        {
                            // Kinematic: Dịch chuyển ngang
                            // Dùng impactDir là chuẩn vì nó hướng từ tâm Player ra tâm Enemy
                            enemyRb.transform.position += impactDir * (knockbackForce * 1.5f * Time.deltaTime);
                        }
                    }

                    // --- PHẦN 2: GÂY SÁT THƯƠNG ---
                    if (!alreadyDamagedList.Contains(hit.gameObject))
                    {
                        alreadyDamagedList.Add(hit.gameObject);
                        ApplyDamage(enemyStats, impactDir);

                        // Effect va chạm
                        // Debug.Log($"Húc trúng: {hit.name}");
                    }
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // [QUAN TRỌNG 4] Trả lại trạng thái vật lý ban đầu
        rb.isKinematic = originalKinematic;

        // Reset vận tốc về 0 để dừng lại dứt khoát
        if (!originalKinematic) rb.linearVelocity = Vector3.zero;
    }

    private void ApplyDamage(Stats enemyStats, Vector3 impactDir)
    {
        bool wasAlive = enemyStats.currentHp > 0;
        stats.EnterCombat();

        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.isKnockback = false; // Tắt knockback hệ thống vì đã xử lý tay

        float damage = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, 1f
        );
        info.damageAmount = damage;
        enemyStats.TakeDamage(info);
    }
}