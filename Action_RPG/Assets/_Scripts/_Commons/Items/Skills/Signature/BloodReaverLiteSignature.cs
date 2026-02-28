using UnityEngine;
using System.Collections;

public class BloodReaverLiteSignature : SkillBehavior
{
    [Header("Frenzy Settings")]
    public float duration = 10.0f;           // Thời gian duy trì 10s
    public float attackSpeedBonus = 0.3f;    // Tăng 30% Tốc đánh
    public float moveSpeedBonus = 0.3f;      // Tăng 30% Tốc chạy
    public float hpCostPerSecondPercent = 0.02f; // Mất 2% máu tối đa mỗi giây

    [Header("VFX")]
    public GameObject frenzyCastVfxPrefab; // Hiệu ứng nổ máu khi bật skill
    public GameObject frenzyAuraVfxPrefab; // Hiệu ứng luồng khí đỏ bao quanh người lúc cuồng nộ

    private Coroutine frenzyCoroutine;
    private GameObject currentAuraVfx;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Gỡ skill thì tắt buff ngay lập tức
        if (frenzyCoroutine != null)
        {
            StopCoroutine(frenzyCoroutine);
            RemoveFrenzyBuffs();
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // Nếu người chơi dùng chiêu liên tục (Spam), ta reset lại thời gian
        if (frenzyCoroutine != null)
        {
            StopCoroutine(frenzyCoroutine);
            RemoveFrenzyBuffs();
        }

        frenzyCoroutine = StartCoroutine(FrenzyRoutine());
        return true;
    }

    private IEnumerator FrenzyRoutine()
    {
        // 1. Spawn VFX
        //if (frenzyCastVfxPrefab != null) Instantiate(frenzyCastVfxPrefab, transform.position, Quaternion.identity);
        //if (frenzyAuraVfxPrefab != null) currentAuraVfx = Instantiate(frenzyAuraVfxPrefab, transform);

        // 2. Cộng Buff Tốc Độ
        stats.bonusAttackSpeed += attackSpeedBonus;
        stats.bonusMoveSpeed += moveSpeedBonus;

        // Gọi hàm để update chỉ số ngay lập tức
        if (stats is AllyStats allyStats)
        {
            allyStats.CalculateCombatStatsOnly();
            allyStats.CalculateMoveSpeedOnly();
        }

        Debug.Log("<color=red>BloodReaverLite: HUYẾT NỘ KÍCH HOẠT!</color>");

        // 3. Vòng lặp rút máu (1 giây / lần)
        float timer = 0f;
        while (timer < duration)
        {
            // Chờ 1 giây
            yield return new WaitForSeconds(1.0f);
            timer += 1.0f;

            // Tính lượng máu mất
            float hpLoss = stats.maxHp * hpCostPerSecondPercent;

            // Trừ thẳng vào currentHp để xuyên khiên và không kích hoạt các Passive phòng thủ
            stats.currentHp -= hpLoss;

            // [Tùy chọn an toàn] Không cho phép tự tử bằng chiêu này (Giữ lại 1 máu)
            if (stats.currentHp <= 0)
            {
                stats.currentHp = 1f;
                // Nếu bạn THỰC SỰ muốn người chơi có thể chết vì bật chiêu ngu, 
                // hãy comment dòng stats.currentHp = 1f; và bỏ comment dòng dưới:
                // stats.TakeDamage(9999f); 
            }

            Debug.Log($"<color=darkred>Huyết Nộ rút máu:</color> -{hpLoss} HP (Còn: {stats.currentHp})");
        }

        // 4. Kết thúc
        RemoveFrenzyBuffs();
    }

    private void RemoveFrenzyBuffs()
    {
        // Trừ trả lại chỉ số
        stats.bonusAttackSpeed -= attackSpeedBonus;
        stats.bonusMoveSpeed -= moveSpeedBonus;

        // Cập nhật lại logic hệ thống
        if (stats is AllyStats allyStats)
        {
            allyStats.CalculateCombatStatsOnly();
            allyStats.CalculateMoveSpeedOnly();
        }

        // Xóa Aura
        //if (currentAuraVfx != null) Destroy(currentAuraVfx);

        frenzyCoroutine = null;
        Debug.Log("<color=white>BloodReaverLite: Đã kết thúc Huyết Nộ.</color>");
    }
}