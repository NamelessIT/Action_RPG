using UnityEngine;
using System.Collections;

public class LeoSignature : SkillBehavior
{
    [Header("Flashbang Settings")]
    public float radius = 5.0f;           // Bán kính vụ nổ
    public float stunDuration = 3.0f;     // Thời gian choáng 3s

    [Header("VFX")]
    public GameObject flashVfxPrefab;     // Hiệu ứng nổ sáng chói mắt

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
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
        // 1. Khóa di chuyển để thực hiện động tác ném bom
        player.isAttacking = true;

        // [Tùy chọn] Bật animation ném xuống đất
        // if (animator != null) animator.SetTrigger("ThrowItem");

        // Khựng lại một nhịp nhỏ (0.2s) để khớp với lúc tay ném chạm đất
        yield return new WaitForSeconds(0.2f);

        Debug.Log("<color=yellow>Leo: FLASHBANG!</color>");

        // Vị trí nổ: Ngay phía trước mặt nhân vật một chút
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        Vector3 explosionPos = transform.position + forward * 0.5f;

        // 2. Kích nổ VFX
        //if (flashVfxPrefab != null)
        //{
        //    Instantiate(flashVfxPrefab, explosionPos, Quaternion.identity);
        //}

        // 3. Quét kẻ địch xung quanh và làm choáng
        Collider[] hits = Physics.OverlapSphere(explosionPos, radius, player.dangerLayer);
        int hitCount = 0;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // Chỉ truyền hiệu ứng khống chế, không truyền sát thương
                DamageInfo info = new DamageInfo();
                info.sourcePosition = explosionPos;
                info.physDamage = 0f;
                info.isStun = true;
                info.stunDuration = stunDuration;
                info.isKnockback = false;
                info.attacker = stats;

                enemyStats.TakeDamage(info);
                hitCount++;
            }
        }

        Debug.Log($"<color=white>Leo đã làm choáng {hitCount} kẻ địch.</color>");

        // Mở khóa Player sau khi ném xong
        yield return new WaitForSeconds(0.2f);
        player.isAttacking = false;
    }

    // Vẽ vùng nổ màu vàng trong Scene để dễ dàng cân chỉnh bán kính
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 forward = (Application.isPlaying && stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        Vector3 pos = transform.position + forward * 0.5f;
        Gizmos.DrawWireSphere(pos, radius);
    }
}