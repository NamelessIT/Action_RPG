using UnityEngine;
using System.Collections;

public class WarriorLiteSignature : SkillBehavior
{
    [Header("Rage Settings")]
    public float duration = 5.0f;
    public float moveSpeedBuff = 0.5f; // +50% Tốc chạy
    public float physAtkBuff = 0.3f;   // +30% Sát thương vật lý
    public float attackRangeMultiplier = 1.5f; // Tầm đánh x1.5 (Tăng 50%)

    [Header("VFX")]
    public GameObject rageVfxPrefab; // Hiệu ứng Aura bốc lửa/nổi giận

    private Coroutine rageCoroutine;

    // Lưu lại các chỉ số gốc để hoàn trả sau 5s
    private float originalAttackRange;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Gỡ an toàn nếu tháo skill giữa chừng
        if (rageCoroutine != null)
        {
            StopCoroutine(rageCoroutine);
            RemoveRageBuffs();
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // Nếu người chơi dùng chiêu liên tục (Reset cooldown), ta làm mới thời gian.
        // PHẢI gỡ buff cũ trước khi chạy lượt mới: trước đây chỉ StopCoroutine rồi Start lại,
        // nên Push 2 lần mà chỉ Pop 1 lần (kẹt super armor), và moveSpeed/physAtk cộng dồn đôi.
        if (rageCoroutine != null)
        {
            StopCoroutine(rageCoroutine);
            RemoveRageBuffs();
        }

        rageCoroutine = StartCoroutine(RageRoutine());
        return true;
    }

    private IEnumerator RageRoutine()
    {
        // 1. THOÁT KHỎI KHỐNG CHẾ (Giải CC)
        stats.BreakCrowdControl();

        // 2. Kích hoạt VFX
        GameObject currentVfx = null;
        if (rageVfxPrefab != null) currentVfx = Instantiate(rageVfxPrefab, transform);

        Debug.Log("<color=red>WARRIOR: RAGE MODE KÍCH HOẠT!</color>");

        // 3. LƯU CHỈ SỐ GỐC VÀ CỘNG BUFF
        originalAttackRange = player.attackRange;

        // Cấp Buff: Tốc chạy, Sát thương, Tầm đánh
        player.attackRange = originalAttackRange * attackRangeMultiplier;
        stats.bonusMoveSpeed += moveSpeedBuff;
        stats.bonusPhysicalAtk += physAtkBuff;

        // Cấp Miễn nhiễm khống chế trong 5s. Push/Pop nên KHÔNG đạp super armor của nguồn khác
        // (trước đây gán thẳng 999 rồi khôi phục từ biến lưu → nguồn nào bật xen giữa sẽ bị mất).
        stats.PushSuperArmor(999);

        // [QUAN TRỌNG] Gọi hàm tính toán lại chỉ số để áp dụng buff ngay lập tức
        if (stats is AllyStats ally)
        {
            ally.CalculateCombatStatsOnly();
            ally.CalculateMoveSpeedOnly();
        }

        // 4. Chờ hết thời gian tác dụng
        yield return new WaitForSeconds(duration);

        // 5. Kết thúc hiệu ứng
        RemoveRageBuffs();
        if (currentVfx != null) Destroy(currentVfx);
    }

    private void RemoveRageBuffs()
    {
        // Trả lại các chỉ số Buff
        player.attackRange = originalAttackRange;
        stats.bonusMoveSpeed -= moveSpeedBuff;
        stats.bonusPhysicalAtk -= physAtkBuff;

        // Nhả nguồn super armor của Rage Mode (nguồn khác vẫn giữ nguyên).
        stats.PopSuperArmor(999);

        // Cập nhật lại logic hệ thống một lần nữa
        if (stats is AllyStats ally)
        {
            ally.CalculateCombatStatsOnly();
            ally.CalculateMoveSpeedOnly();
        }

        rageCoroutine = null;
        Debug.Log("<color=white>WARRIOR: Đã hết trạng thái Rage.</color>");
    }
}