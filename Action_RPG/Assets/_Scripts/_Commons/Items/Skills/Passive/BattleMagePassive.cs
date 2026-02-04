using UnityEngine;

public class BattleMagePassive : SkillBehavior
{
    [Header("Settings")]
    public float magicLifeStealBonus = 0.1f; // 10% Hút máu phép

    [Tooltip("Tỷ lệ chuyển đổi: 10% Speed (0.1) * 200 = 20 Armor")]
    public float speedToDefenseRatio = 200f;

    [Tooltip("Lượng Sin hồi mỗi giây khi đứng yên")]
    public float sinRegenPerSec = 1f;

    [Header("Interaction Settings")]
    [Tooltip("Số lượng Speed trả lại để tính toán khi đang dựng khiên Vanguard (Phải khớp với số bên Vanguard)")]
    public float shieldSlowCompensation = 0.5f; // Khớp với VanguardPassive
    // Sau này thêm 1 if để kiểm tra xem đang kích hoạt node không giảm speed chưa, chuyển thành 0f

    // Biến lưu giá trị đã cộng để trừ ra khi update frame mới
    private float addedArmor = 0f;
    private float addedMR = 0f;
    private Rigidbody rb;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
    }

    protected override void OnEquip()
    {
        // 1. Cộng Magic Life Steal
        stats.magicLifeSteal += 0.1f;
        Debug.Log($"[BattleMage] +{magicLifeStealBonus * 100}% Magic Life Steal");
    }

    protected override void OnUnequip()
    {
        // 1. Trừ Magic Life Steal
        stats.magicLifeSteal -= magicLifeStealBonus;

        // 2. Dọn dẹp chỉ số ảo
        stats.armor -= addedArmor;
        stats.magicResist -= addedMR;
        addedArmor = 0f;
        addedMR = 0f;

        Debug.Log("[BattleMage] Gỡ bỏ hiệu ứng.");
    }

    void Update()
    {
        HandleSinRegen();
        HandleStatConversion();
    }

    // --- LOGIC 1: HỒI SIN KHI ĐỨNG YÊN ---
    void HandleSinRegen()
    {
        // Kiểm tra tốc độ di chuyển (gần bằng 0)
        // rb.velocity có thể dùng được, hoặc check input từ PlayerController nếu public
        if (rb.linearVelocity.magnitude == 0f) 
        {
            if (stats.currentSin < stats.maxSin)
            {
                stats.currentSin += sinRegenPerSec * Time.deltaTime;

                // Kẹp trần
                if (stats.currentSin > stats.maxSin) stats.currentSin = stats.maxSin;
            }
        }
    }

    // --- LOGIC 2: CHUYỂN MOVE SPEED THÀNH ARMOR/MR ---
    void HandleStatConversion()
    {
        // 1. Hoàn trả lượng đã cộng ở frame trước (để không bị cộng dồn vô hạn)
        stats.armor -= addedArmor;
        stats.magicResist -= addedMR;

        // 2. Tính toán lượng mới dựa trên Bonus Move Speed hiện tại
        // Ví dụ: bonusMoveSpeed là 0.2 (20%) -> Cộng 40 Armor (với ratio 200)
        float speedBonus = stats.bonusMoveSpeed;

        // [MỚI] KIỂM TRA NGOẠI LỆ VANGUARD SHIELD
        // Nếu đang dựng khiên (isManualGuarding = true), nghĩa là Speed đang bị trừ 0.5
        // Ta cộng bù 0.5 vào biến tạm 'speedBonus' để coi như chưa từng bị trừ.
        if (stats.isManualGuarding)
        {
            speedBonus += shieldSlowCompensation;
        }

        // Chỉ tính nếu đang có bonusSpeed
        if (speedBonus > 0f)
        {
            float conversionValue = speedBonus * speedToDefenseRatio;

            addedArmor = conversionValue;
            addedMR = conversionValue;

            // 3. Cộng vào Stats
            stats.armor += addedArmor;
            stats.magicResist += addedMR;
        }
        else
        {
            addedArmor = 0f;
            addedMR = 0f;
        }
    }
}