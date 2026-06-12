using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WarriorSignature : SkillBehavior
{
    [Header("Jump Settings")]
    public float jumpHeight = 3.0f;       // Độ cao của cú nhảy
    public float jumpDuration = 1.0f;     // Tổng thời gian bay (0.5s lên, 0.5s xuống)

    [Header("Impact Settings")]
    public float slamRadius = 3.0f;       // Bán kính vùng nổ
    //public float damageMultiplier = 3.5f; // Sát thương diện rộng x3.5

    [Header("Execution Settings")]
    public float executeThreshold = 0.20f; // Dưới 20% máu là chết luôn

    [Header("VFX")]
    public GameObject takeoffVfxPrefab;   // VFX lúc dậm nhảy (Bụi bay)
    public GameObject slamVfxPrefab;      // VFX lúc cắm kiếm xuống đất (Mặt đất nứt nẻ, sóng xung kích)
    public GameObject executeVfxPrefab;   // VFX khi một mục tiêu bị kết liễu (Cột sáng máu/Chữ Execute)

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
        StartCoroutine(SkyfallRoutine());
        return true;
    }

    private IEnumerator SkyfallRoutine()
    {
        // 1. SETUP TRẠNG THÁI (Không thể bị cản)
        player.isAttacking = true;  // Khóa Input
        stats.isInvincible = true;  // Bất tử

        bool originalKinematic = rb.isKinematic;
        rb.isKinematic = true;      // Tắt vật lý để bay lên không bị kẹt

        Vector3 startPos = transform.position;
        Vector3 peakPos = startPos + (Vector3.up * jumpHeight);

        // [Optional] Bật animation nhảy
        // if (animator != null) animator.SetTrigger("SkyfallJump");

        // Bật VFX lúc cất cánh
        //if (takeoffVfxPrefab != null) Instantiate(takeoffVfxPrefab, startPos, Quaternion.identity);

        Debug.Log("<color=orange>Warrior: CẤT CÁNH!</color>");

        // 2. PHASE 1: BAY LÊN (Sử dụng Lerp & Sine wave để tạo cảm giác chậm dần ở đỉnh)
        float timer = 0f;
        float riseTime = jumpDuration * 0.4f; // Bay lên chiếm 40% thời gian (nhanh)

        while (timer < riseTime)
        {
            timer += Time.deltaTime;
            float percent = timer / riseTime;
            // Ease-out (Chậm dần khi tới đỉnh)
            float t = Mathf.Sin(percent * Mathf.PI * 0.5f);

            rb.MovePosition(Vector3.Lerp(startPos, peakPos, t));
            yield return null;
        }

        // Khựng lại 1 xíu trên không trung cho ngầu
        yield return new WaitForSeconds(0.1f);

        // 3. PHASE 2: LAO XUỐNG (Sử dụng Lerp & Cosine wave để tạo cảm giác rơi tự do gia tốc)
        timer = 0f;
        float fallTime = jumpDuration * 0.6f; // Rơi xuống chiếm 60% thời gian (chậm rồi cực nhanh)

        while (timer < fallTime)
        {
            timer += Time.deltaTime;
            float percent = timer / fallTime;
            // Ease-in (Tăng tốc khi rơi)
            float t = 1f - Mathf.Cos(percent * Mathf.PI * 0.5f);

            rb.MovePosition(Vector3.Lerp(peakPos, startPos, t));
            yield return null;
        }

        // Đảm bảo đáp đúng vị trí mặt đất
        rb.MovePosition(startPos);

        // 4. PHASE 3: VA CHẠM (IMPACT)
        Debug.Log("<color=red>Warrior: GIÁNG LÂM!</color>");

        // Rung màn hình (Nếu bạn có CinemachineImpulseSource trên Player)
        // impulseSource.GenerateImpulseWithForce(2.0f);

        //if (slamVfxPrefab != null) Instantiate(slamVfxPrefab, startPos, Quaternion.identity);

        // Quét kẻ địch
        Collider[] hits = Physics.OverlapSphere(startPos, slamRadius, player.dangerLayer);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                ApplySkyfallDamage(enemyStats);
            }
        }

        // Chờ animation rút vũ khí lên (nếu có)
        yield return new WaitForSeconds(0.5f);

        // 5. TRẢ LẠI TRẠNG THÁI
        rb.isKinematic = originalKinematic;
        stats.isInvincible = false;
        player.isAttacking = false;
    }

    private void ApplySkyfallDamage(Stats enemyStats)
    {
        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;

        // --- LOGIC KẾT LIỄU (EXECUTE) ---
        // Tính mốc máu kết liễu
        float executeHpTarget = enemyStats.maxHp * executeThreshold;

        if (enemyStats.currentHp <= executeHpTarget)
        {
            // MỤC TIÊU DƯỚI 20% MÁU -> CHẾT NGAY LẬP TỨC
            Debug.Log($"<color=red>EXECUTE THÀNH CÔNG: {enemyStats.name}!</color>");

            DamageInfo executeInfo = new DamageInfo();
            executeInfo.sourcePosition = transform.position;
            executeInfo.trueDamage = 999999f; // Lượng sát thương ảo khổng lồ để chắc chắn chết
            executeInfo.isCrit = true; // Hiện đỏ cho ngầu
            executeInfo.attacker = stats;

            enemyStats.TakeDamage(executeInfo);

            //if (executeVfxPrefab != null)
            //{
            //    Instantiate(executeVfxPrefab, enemyStats.transform.position + Vector3.up, Quaternion.identity);
            //}
        }
        else
        {
            // MỤC TIÊU TRÊN 20% MÁU -> NHẬN SÁT THƯƠNG DIỆN RỘNG NHƯ BÌNH THƯỜNG
            DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, data.skillPhysicalMultiplier, data, currentWpn, 0, true, 1.5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}