using UnityEngine;
using System.Collections;

public class CatalystSignature : SkillBehavior
{
    [Header("Transfer Settings")]
    public float sinTransferAmount = 120f;  // Lượng Sin bơm cho Companion

    [Header("Companion Buff Settings")]
    public float duration = 10.0f;          // Duy trì 10 giây
    public float compDamageBuff = 1.0f;     // +100% Sát thương (Cộng vào damageOutputMultiplier)
    public float compAttackSpeedBuff = 1.0f;// +100% Tốc đánh

    [Header("Player Distance Bonus Settings")]
    public float maxBonusDistance = 2.0f;   // Đứng cách <= 2m sẽ nhận Max Bonus
    public float minBonusDistance = 10.0f;  // Đứng cách >= 10m sẽ nhận Min Bonus
    public float maxHpPercentBonus = 0.02f; // Tối đa 2% Max HP
    public float minHpPercentBonus = 0.005f;// Tối thiểu 0.5% Max HP

    [Header("VFX")]
    public GameObject companionAuraVfxPrefab; // Luồng khí đen dày đặc gắn vào Companion

    private Stats compStats;
    private Coroutine buffCoroutine;
    private GameObject currentAuraVfx;
    private bool isBuffActive = false;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        RemoveBuff();
    }

    public override bool Use()
    {
        // 1. Kiểm tra xem có Companion trên sân không
        CompanionAI companion = FindFirstObjectByType<CompanionAI>();
        if (companion == null)
        {
            Debug.Log("<color=red>CATALYST: Không tìm thấy Companion để truyền năng lượng!</color>");
            return false;
        }

        if (!base.Use()) return false; // (Hệ thống sẽ tự trừ Sin của Player ở đây nếu cấu hình trong SkillData)

        compStats = companion.GetComponent<Stats>();

        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(SignatureRoutine(companion));

        return true;
    }

    private IEnumerator SignatureRoutine(CompanionAI companion)
    {
        // --- 1. TRUYỀN SIN CHARGE CHO COMPANION ---
        if (compStats != null)
        {
            compStats.currentSin += sinTransferAmount;
            if (compStats.currentSin > compStats.maxSin) compStats.currentSin = compStats.maxSin;
            Debug.Log($"<color=purple>CATALYST: Đã truyền {sinTransferAmount} Sin cho Companion!</color>");
        }

        // --- 2. BẬT TRẠNG THÁI BẤT TỬ & CƯỜNG HÓA COMPANION ---
        if (!isBuffActive)
        {
            isBuffActive = true;

            compStats.isInvincible = true;
            compStats.damageOutputMultiplier += compDamageBuff;

            // Xử lý Tốc đánh (Kiểm tra xem thú cưng dùng AllyStats hay Stats thường)
            if (compStats is AllyStats compAlly)
            {
                compAlly.bonusAttackSpeed += compAttackSpeedBuff;
                compAlly.CalculateCombatStatsOnly();
            }
            else
            {
                // Nếu dùng Stats thường, giảm thời gian vung đòn (Cooldown) đi 3 lần để mô phỏng +200% tốc đánh
                compStats.baseAttackSpeed /= (1f + compAttackSpeedBuff);
            }

            // Đăng ký lắng nghe sự kiện đánh trúng của Player
            player.OnHitEnemy += HandlePlayerHitEnemy;

            // Bật Visual Effect
            //if (currentAuraVfx != null) Destroy(currentAuraVfx);
            //if (companionAuraVfxPrefab != null)
            //    currentAuraVfx = Instantiate(companionAuraVfxPrefab, compStats.transform);

            Debug.Log("<color=magenta>COMPANION TIẾN HÓA: Bất tử, x2 Sát thương, x2 Tốc đánh!</color>");
        }

        // --- 3. ĐỢI HẾT 10 GIÂY ---
        yield return new WaitForSeconds(duration);

        // --- 4. GỠ BUFF ---
        RemoveBuff();
    }

    private void RemoveBuff()
    {
        if (isBuffActive)
        {
            isBuffActive = false;

            if (compStats != null)
            {
                compStats.isInvincible = false;
                compStats.damageOutputMultiplier -= compDamageBuff;

                if (compStats is AllyStats compAlly)
                {
                    compAlly.bonusAttackSpeed -= compAttackSpeedBuff;
                    compAlly.CalculateCombatStatsOnly();
                }
                else
                {
                    compStats.baseAttackSpeed *= (1f + compAttackSpeedBuff);
                }
            }

            // Gỡ lắng nghe sự kiện
            player.OnHitEnemy -= HandlePlayerHitEnemy;

            if (currentAuraVfx != null) Destroy(currentAuraVfx);

            Debug.Log("<color=gray>Catalyst: Companion đã trở lại bình thường.</color>");
        }
    }

    // ==========================================================
    // LOGIC GÂY THÊM SÁT THƯƠNG THEO KHOẢNG CÁCH CỦA PLAYER
    // ==========================================================
    private void HandlePlayerHitEnemy(Stats enemy, int stepIndex, bool isHeavy, bool isCrit)
    {
        if (enemy == null || enemy.currentHp <= 0 || compStats == null) return;

        // 1. Tính khoảng cách giữa Bạn (Player) và Thú cưng (Companion)
        float dist = Vector3.Distance(transform.position, compStats.transform.position);

        // 2. Nội suy Tỷ lệ % Máu dựa trên khoảng cách
        // Nếu dist <= 2m -> t = 1.0
        // Nếu dist >= 10m -> t = 0.0
        float t = Mathf.InverseLerp(minBonusDistance, maxBonusDistance, dist);

        // Chuyển đổi t thành % máu (t=1.0 -> 2%, t=0.0 -> 0.5%)
        float percentBonus = Mathf.Lerp(minHpPercentBonus, maxHpPercentBonus, t);

        // Tính lượng sát thương chuẩn (Dựa trên Max HP của địch)
        float bonusMagicDamage = enemy.maxHp * percentBonus;

        // 3. Gửi sát thương thêm vào kẻ địch
        DamageInfo bonusInfo = new DamageInfo();
        bonusInfo.sourcePosition = transform.position;
        bonusInfo.attacker = stats;
        bonusInfo.magicDamage = bonusMagicDamage;
        bonusInfo.isCrit = isCrit; // Cho phép sát thương thêm có thể Crit theo đòn gốc

        enemy.TakeDamage(bonusInfo);

        Debug.Log($"<color=cyan>Cộng Hưởng (Cách {dist:F1}m):</color> Gây thêm {bonusMagicDamage:F1} sát thương phép ({percentBonus * 100:F1}% Max HP).");
    }
}