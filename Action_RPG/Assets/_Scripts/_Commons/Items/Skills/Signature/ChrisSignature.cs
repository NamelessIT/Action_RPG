using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChrisSignature : SkillBehavior
{
    [Header("Shockwave Settings")]
    public float radius = 2f;           // Bán kính sóng xung kích
    public float knockbackForce = 5.0f;  // Lực đẩy lùi nhẹ

    [Header("Buff Settings")]
    public float physAtkBuffPercent = 0.15f; // Tăng 15% Physical Attack
    public float buffDuration = 8.0f;        // Tồn tại 8 giây

    [Header("VFX")]
    public GameObject shockwaveVfxPrefab; // Hiệu ứng sóng âm tỏa ra
    public GameObject buffAuraVfxPrefab;  // Hiệu ứng aura đỏ/cam quanh người khi đang buff

    private Coroutine buffCoroutine;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Gỡ skill thì mất buff luôn
        if (buffCoroutine != null)
        {
            StopCoroutine(buffCoroutine);
            RemoveBuff();
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(SignatureRoutine());
        return true;
    }

    private IEnumerator SignatureRoutine()
    {
        // 1. Khóa di chuyển/đánh để gồng
        player.isAttacking = true;

        // [Optional] Play Animation gồng/hét
        // if (animator != null) animator.SetTrigger("WarCry");

        // Delay 1 chút xíu để khớp với animation hét (tầm 0.2s)
        yield return new WaitForSeconds(0.2f);

        Debug.Log("<color=orange>Chris: HRAAAAAAAGH!</color>");

        // 2. Play VFX Sóng xung kích
        //if (shockwaveVfxPrefab != null)
        //{
        //    Instantiate(shockwaveVfxPrefab, transform.position, Quaternion.identity);
        //}

        // 3. ĐẨY LÙI KẺ ĐỊCH (Không sát thương)
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, player.dangerLayer);
        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                DamageInfo info = new DamageInfo();
                info.sourcePosition = transform.position; // Lấy tâm từ Player đẩy ra
                info.damageAmount = 0f;                   // Không gây sát thương
                info.isKnockback = true;
                info.knockbackForce = knockbackForce;     // Lực đẩy lùi
                info.isStun = false;
                info.attacker = stats;

                // Hàm TakeDamage sẽ bỏ qua vụ trừ máu (do dmg = 0) nhưng vẫn xử lý Knockback
                enemyStats.TakeDamage(info);
            }
        }

        // 4. ÁP DỤNG BUFF
        // Nếu đang có buff rồi thì reset lại thời gian
        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(BuffRoutine());

        // Mở khóa sau khi hét xong
        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    private IEnumerator BuffRoutine()
    {
        // Cộng chỉ số
        stats.bonusPhysicalAtk += physAtkBuffPercent;
        stats.CalculateCombatStatsOnly(); // Cập nhật ngay lập tức

        // Hiệu ứng Aura
        //GameObject currentAura = null;
        //if (buffAuraVfxPrefab != null)
        //{
        //    currentAura = Instantiate(buffAuraVfxPrefab, transform);
        //}

        // Chờ 8 giây
        yield return new WaitForSeconds(buffDuration);

        // Hết giờ thì gỡ Buff
        RemoveBuff();
        //if (currentAura != null) Destroy(currentAura);
    }

    private void RemoveBuff()
    {
        stats.bonusPhysicalAtk -= physAtkBuffPercent;
        stats.CalculateCombatStatsOnly();
        Debug.Log("<color=white>Chris: Buff Physical Attack đã kết thúc.</color>");

        buffCoroutine = null;
    }

    // Vẽ vòng tròn vàng trong Inspector để dễ chỉnh Radius
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}