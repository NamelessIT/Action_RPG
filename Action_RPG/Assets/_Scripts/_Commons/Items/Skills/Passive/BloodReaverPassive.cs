using UnityEngine;

public class BloodReaverPassive : SkillBehavior
{
    [Header("Settings")]
    public float hpCostPercent = 0.005f; // Tiêu hao 0.5% Máu tối đa
    public float hpThresholdNoCost = 0.25f; // Dưới 25% không mất máu

    [Header("Bleed Settings")]
    public float bleedDuration = 5f; // 5 giây
    [Tooltip("Sát thương chảy máu mỗi giây = % Physical Attack của người chơi")]
    public float bleedDamagePercent = 0.15f; // Ví dụ: Gây 15% sát thương vật lý mỗi giây

    [Header("Passive Bonuses")]
    public float physDmgPer1PercentLost = 0.005f; // Mất 1% máu -> Tăng 0.5% Damage
    public float lowHpThreshold = 0.3f; // Dưới 30% máu kích hoạt Crit
    public float bonusCritChance = 0.3f; // +30% Tỷ lệ Crit
    public float bonusCritMultiplier = 0.25f; // +25% Sát thương Crit

    // Biến lưu giá trị cộng dồn để trừ ra mỗi frame
    private float addedPhysDmg = 0f;
    private float addedCritChance = 0f;
    private float addedCritMult = 0f;

    protected override void OnEquip()
    {
        // Đăng ký sự kiện đánh trúng địch
        stats.OnHitEnemy += HandleOnHitEnemy;
        Debug.Log("[BloodReaver] Huyết Thệ Kích Hoạt");
    }

    protected override void OnUnequip()
    {
        stats.OnHitEnemy -= HandleOnHitEnemy;

        // Dọn dẹp chỉ số ảo
        CleanUpStats();
    }

    void Update()
    {
        HandleDynamicStats();
    }

    // --- LOGIC 1: CẬP NHẬT CHỈ SỐ THEO MÁU ---
    private void HandleDynamicStats()
    {
        // 1. Hoàn trả lượng đã cộng ở frame trước
        CleanUpStats();

        // 2. Tính toán Bonus 1: Mất máu tăng Damage
        // Máu hiện tại / Máu tối đa = % Máu đang còn (0.8 = 80%)
        // 1 - % Máu còn = % Máu mất (0.2 = 20%)
        float lostHealthPercent = 1.0f - (stats.currentHp / stats.maxHp);

        // Chuyển về số nguyên (20) để nhân với hệ số
        // Ví dụ: Mất 20% -> Tăng 20 * 0.5% = 10% Damage (0.1)
        float damageBonus = (lostHealthPercent * 100f) * physDmgPer1PercentLost;

        addedPhysDmg = damageBonus;
        stats.bonusPhysicalAtk += addedPhysDmg;

        // 3. Tính toán Bonus 2: Máu thấp tăng Crit
        if (stats.currentHp < stats.maxHp * lowHpThreshold)
        {
            addedCritChance = bonusCritChance;
            addedCritMult = bonusCritMultiplier;

            stats.bonusCritChance += addedCritChance;
            stats.bonusCritMultiplier += addedCritMult;

            // Debug.Log(">> CRISIS MODE: ACTIVATED!");
        }
        // [MỚI] Gọi hàm tính lại chỉ số ngay lập tức
        if (stats is AllyStats ally)
        {
            ally.CalculateCombatStatsOnly();
        }
    }

    private void CleanUpStats()
    {
        stats.bonusPhysicalAtk -= addedPhysDmg;
        stats.bonusCritChance -= addedCritChance;
        stats.bonusCritMultiplier -= addedCritMult;

        addedPhysDmg = 0f;
        addedCritChance = 0f;
        addedCritMult = 0f;
    }

    // --- LOGIC 2: ĐÁNH MẤT MÁU & GÂY BLEED ---
    private void HandleOnHitEnemy(Stats target, float t, bool isCrit)
    {
        // A. Xử lý Trừ máu bản thân
        // Điều kiện: Máu phải trên 25%
        if (stats.currentHp > stats.maxHp * hpThresholdNoCost)
        {
            float selfDamage = stats.maxHp * hpCostPercent;
            stats.currentHp -= selfDamage;

            // Hiển thị effect máu văng ra nếu muốn (Optional)
            Debug.Log($"[BloodReaver] Hiến tế {selfDamage} máu.");
        }

        // B. Gây hiệu ứng Chảy Máu lên địch
        if (target != null)
        {
            // Tính damage bleed (Ví dụ: 20% của PhysAtk hiện tại)
            float bleedDmg = stats.physicalAtk * bleedDamagePercent;

            // Gọi hàm chảy máu bên Stats của địch
            target.ApplyBleed(bleedDmg, (int)bleedDuration);
        }
    }
}