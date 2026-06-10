using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RogueSignature : SkillBehavior
{
    [Header("Omnislash Settings")]
    public float scanRadius = 5.0f;           // Vùng quét 5m xung quanh
    public float timeBetweenSlashes = 0.15f;  // Thời gian giữa mỗi nhát chém (0.15s cho 12 nhát = 1.8s)
    //public float damageMultiplierPerSlash = 0.8f; // Sát thương mỗi nhát (80% sức mạnh)

    [Header("VFX")]
    public GameObject vanishVfxPrefab;        // Khói đen lúc bắt đầu/kết thúc
    public GameObject slashVfxPrefab;         // Vết chém chớp nhoáng trên người địch

    private EquipmentManager equipmentManager;
    private SpriteRenderer spriteRenderer;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        spriteRenderer = myPlayer.GetComponentInChildren<SpriteRenderer>();
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
        // 1. LƯU VỊ TRÍ GỐC VÀ QUÉT ĐỊCH
        Vector3 originalPosition = transform.position;
        Collider[] hits = Physics.OverlapSphere(originalPosition, scanRadius, player.dangerLayer);

        List<Stats> validTargets = new List<Stats>();
        foreach (var hit in hits)
        {
            Stats enemy = hit.GetComponent<Stats>();
            if (enemy != null && enemy.currentHp > 0 && !validTargets.Contains(enemy))
            {
                validTargets.Add(enemy);
            }
        }

        if (validTargets.Count == 0)
        {
            Debug.Log("<color=gray>Rogue: Không có ai xung quanh để ám sát.</color>");
            yield break; // Không xài chiêu nếu không có địch
        }

        // 2. SETUP TRẠNG THÁI: TÀNG HÌNH & BẤT TỬ
        player.isAttacking = true;
        stats.isInvincible = true;
        if (spriteRenderer != null) spriteRenderer.enabled = false; // Tắt hình ảnh nhân vật (Biến thành bóng)

        //if (vanishVfxPrefab) Instantiate(vanishVfxPrefab, transform.position, Quaternion.identity);
        Debug.Log("<color=purple>Rogue: SHADOW DANCE!</color>");

        // Tính số lượng nhát chém
        int totalSlashes = (validTargets.Count == 1) ? 6 : 12;
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        // 3. VÒNG LẶP CHÉM (OMNISLASH)
        for (int i = 0; i < totalSlashes; i++)
        {
            // Lọc lại danh sách: Loại bỏ những con đã chết ở nhát chém trước
            validTargets.RemoveAll(e => e == null || e.currentHp <= 0);

            // Nếu đang chém dở mà chết sạch địch -> Dừng chém sớm
            if (validTargets.Count == 0) break;

            // Chọn mục tiêu luân phiên (Round-robin) để chia đều sát thương
            Stats currentTarget = validTargets[i % validTargets.Count];

            // Tìm hướng lưng của địch
            Vector3 enemyForward = currentTarget.facingDirection;
            if (enemyForward == Vector3.zero) enemyForward = currentTarget.transform.forward;
            enemyForward.y = 0;
            enemyForward.Normalize();

            // Dịch chuyển chớp nhoáng ra sau lưng kẻ địch đó
            player.transform.position = currentTarget.transform.position - enemyForward * 0.5f;
            player.ForceFaceDirection(enemyForward);

            // Hiệu ứng chém
            //if (slashVfxPrefab) Instantiate(slashVfxPrefab, currentTarget.transform.position, Quaternion.LookRotation(enemyForward));

            // Gây sát thương (Player đứng sau lưng địch, direction tự nhiên = backstab)
            DamageHelper.ApplyStandardDamage(stats, currentTarget, transform, data.skillPhysicalMultiplier, data, currentWpn, 0, true, 0.2f);

            // Chờ một khoảng siêu ngắn trước khi nhảy sang mục tiêu tiếp theo
            yield return new WaitForSeconds(timeBetweenSlashes);
        }

        // 4. KẾT THÚC VÀ ĐÁP ĐẤT
        // Lọc lại danh sách lần cuối cùng xem còn ai sống sót không
        validTargets.RemoveAll(e => e == null || e.currentHp <= 0);

        if (validTargets.Count > 0)
        {
            // Tìm kẻ địch còn nhiều HP nhất
            Stats highestHpTarget = null;
            float maxHp = -1f;
            foreach (var t in validTargets)
            {
                if (t.currentHp > maxHp)
                {
                    maxHp = t.currentHp;
                    highestHpTarget = t;
                }
            }

            // Đo đạc Hitbox để đáp đất an toàn sau lưng nó (Giống RogueLite)
            Vector3 enemyForward = highestHpTarget.facingDirection;
            if (enemyForward == Vector3.zero) enemyForward = highestHpTarget.transform.forward;
            enemyForward.y = 0; enemyForward.Normalize();

            Collider targetCol = highestHpTarget.GetComponent<Collider>();
            Collider playerCol = player.GetComponent<Collider>();

            float targetRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;
            float playerRadius = playerCol != null ? Mathf.Max(playerCol.bounds.extents.x, playerCol.bounds.extents.z) : 0.5f;

            float safeDistance = targetRadius + playerRadius + 0.2f;
            Vector3 backPosition = highestHpTarget.transform.position - enemyForward * safeDistance;

            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(backPosition, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                player.transform.position = navHit.position;
            else
                player.transform.position = backPosition;

            player.ForceFaceDirection(enemyForward);
        }
        else
        {
            // Chết sạch rồi thì về lại chỗ cũ đứng tạo dáng
            player.transform.position = originalPosition;
            Debug.Log("<color=gray>Rogue: Toàn diệt.</color>");
        }

        // Hiện hình lại và dỡ bỏ bất tử
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        //if (vanishVfxPrefab) Instantiate(vanishVfxPrefab, transform.position, Quaternion.identity);

        stats.isInvincible = false;
        player.isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}