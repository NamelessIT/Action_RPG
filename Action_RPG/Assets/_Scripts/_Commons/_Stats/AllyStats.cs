using UnityEngine;
using System;
using System.Collections;
public class AllyStats : Stats
{
    public float exp;
    [Header("--- Sub-Health ---")]
    public float H = 200f;
    public float maxHpGainBonus = 10f;
    public float hpPerVIT = 15f;
    public float hpGain;
    public float bonusHp;
    public float flatHp;

    [Header("--- Sub-Sin ---")]
    public float sinGain;
    public float S = 100f;
    public float maxSinBonus=0.7f;

    [Header("--- Damage ---")]
    public float physicalAtkPerSTR = 2.5f;
    public float magicAtkPerINT = 3f;
    public float maxAttackSpeedBuff = 0.8f;
    public float critPerDEX = 0.015f;
    public float A = 80f;
    public float C = 100f;
    public float K = 0.005f;
    public float bonusPhysicalAtk;
    public float bonusMagicAtk;
    public float attackSpeed;
    public float bonusAttackSpeed;
    public float flatPhysicalAtk=0f;
    public float flatMagicAtk=0f;
    public float critChance;
    public float bonusCritChance;
    public float critMultiplier;
    public float bonusCritMultiplier;


    [Header("--- Movement ---")]
    public float moveSpeed;
    public float moveSpeedReducePerVIT=0.005f;
    public float minSpeed = 3f;
    public float maxSpeed = 9f;
    public float maxReduceBySTR=0.6f;
    public float M = 150f;
    public float bonusMoveSpeed;
    public float trueMoveFlexibility;

    [Header("--- Dash ---")]
    public float dashDistance;
    public float dashRecovery;
    public float maxDashReduction=0.35f;
    public float AGI_ThreshHold=75;
    public float R = 80;

    [Header("--- Cooldown ---")]
    public float baseCdr = 0;
    public float cooldownReduction;
    public float cdrPerAGI=0.0015f;
    public float bonusCdr;

    [Header("--- Class Info ---")]
    public string className = "Warrior"; // Hoặc dùng Enum ClassType

    [Header("--- Combat Status ---")]
    public bool isPerfectDodgeSuccess = false;

    [Tooltip("Độ chậm thời gian (0.1 = rất chậm, 1.0 = bình thường)")]
    public float slowMotionFactor = 0.2f;

    [Tooltip("Thời gian duy trì hiệu ứng (tính theo giây thực tế)")]
    public float slowMotionDuration = 0.5f;

    // ---------------- Chris Passive ----------------
    // Đây là cái "chuông" để báo cho Passive Chris biết khi nào bị đánh
    public Action<float> OnTakeDamage;

    // ---------------- Leo Passive ----------------
    // Đây là cái chuông để báo cho Passive Leo biết khi nào đánh backstab hoặc crit
    // Action gửi đi 2 tham số: 
    // 1. Stats (Để xác định mục tiêu bị đánh) (Dùng để áp dụng hiệu ứng Bleed) (BloodReaver)
    // 1. float t (Hệ số hướng, t=1 là lưng)
    // 2. bool isCrit (Có chí mạng không)
    public Action<Stats, float, bool> OnHitEnemy;

    // ---------------- Vanguard Passive ----------------
    // Sự kiện khi Block thành công (Để Vanguard hồi Stamina)
    public Action OnBlockSuccess;
    // [MỚI] Biến xác định đang dựng khiên chủ động
    public bool isManualGuarding = false;
    // [MỚI] Chỉ số giảm sát thương khi dựng khiên (0.5 = 50%)
    public float manualGuardReduction = 0f;

    // ---------------- Warrior Passive ----------------
    // Biến xác định đang trong trạng thái vung kiếm
    public bool isMomentumActive = false;
    // Chỉ số giảm thương (0.3 = 30%)
    public float momentumReduction = 0.3f;

    // ---------------- Rouge ----------------
    // Action gửi đi: 
    // 1. Stats victim (Kẻ vừa bị giết)
    // 2. bool isBackstab (Có phải giết từ sau lưng không)
    public Action<Stats, bool> OnKillEnemy;

    public override void Start()
    {
        base.Start();
        // Gọi tính toán lần đầu
        RecalculateStats();
        InitializeClassStats();
    }

    // Hàm này phải được gọi mỗi khi: Lên cấp, Đổi đồ, Nhận Buff, Chịu Debuff
    public void RecalculateStats()
    {
        // 1. Tính HP
        // Công thức: baseHp = 100 + 20*level
        baseHp = 100 + 20 * level;
        // Công thức: hpGain = base * (1 + maxBonus * VIT / (VIT + H))
        hpGain = baseHpGain * (1 + maxHpGainBonus * VIT / (VIT + H));
        // Công thức: MaxHP = (Flat + VIT * 15) * (1 + Bonus%)
        maxHp = (flatHp + baseHp + hpPerVIT * VIT) * (1 + bonusHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp); // Đảm bảo máu không vượt quá Max

        // 2. Tính Sin
        sinGain = baseSinGain * (1 + maxSinBonus * INT / (INT + S));

        // 3. Tính Damage
        physicalAtk = (flatPhysicalAtk + STR * physicalAtkPerSTR) * (1 + bonusPhysicalAtk);
        magicAtk = (flatMagicAtk + INT * magicAtkPerINT) * (1 + bonusMagicAtk);

        // 4. Tính Attack Speed (Giới hạn buff tối đa bởi AGI)
        bonusAttackSpeed = maxAttackSpeedBuff * AGI / (AGI + A);
        attackSpeed = baseAttackSpeed * (1 + bonusAttackSpeed); 
        // (Lưu ý: baseAttackSpeed nên lấy từ Weapon đang cầm)

        // 5. Tính Crit
        // baseCritChance lấy từ Stats.cs
        critChance = baseCritChance + critPerDEX * DEX + bonusCritChance;
        critMultiplier = baseCritMultiplier + bonusCritMultiplier;

        // 6. Tính Movement & Flexibility
        // Công thức flexibility: 1 - ((1 - weaponFlex) * (1 - maxReduceBySTR))
        trueMoveFlexibility =  1 - ((1 - moveFlexibility) * (1 - maxReduceBySTR * STR / (STR + M))); //moveFlexibility lấy từ vũ khí
        combatTurnDuration = 0.6f - (0.5f * trueMoveFlexibility);

        // Công thức Move Speed (Sửa lỗi cú pháp ^ thành Mathf.Pow)
        // moveSpeed = baseMoveSpeed + (0.2f * Mathf.Pow(AGI, 0.5f) - VIT * 0.005f) ...
        moveSpeed = baseMoveSpeed * (1 + ((0.05f * Mathf.Sqrt(AGI) - VIT * moveSpeedReducePerVIT) * (1 + trueMoveFlexibility) / 2)) * (1 + bonusMoveSpeed);
        if (moveSpeed < minSpeed) moveSpeed = minSpeed; // Min speed
        if (moveSpeed > maxSpeed) moveSpeed = maxSpeed;

        // 7. Tính Dash
        dashDistance = baseDashDistance * (1f - trueMoveFlexibility); // Nặng thì lướt ngắn
        dashRecovery = (baseDashRecovery + (1f - trueMoveFlexibility)) * (1f - maxDashReduction * DEX / (DEX + R));

        // Cost Dash (AGI Threshold)
        if (AGI >= AGI_ThreshHold) dashCost = 15;
        else dashCost = 20;

        // 8. Cooldown Reduction
        cooldownReduction = baseCdr + (cdrPerAGI * AGI) + bonusCdr;
    }
    public void InitializeClassStats()
    {
        if (className == "Warrior")
        {
            // WARRIOR: "Mình đồng da sắt"
            // Luôn luôn có Super Armor cấp 0 -> Miễn nhiễm với quái nhỏ (Rank 0)
            isSuperArmor = true;
            superArmorLevel = 0;

            // Nếu bạn muốn Warrior cấp cao cứng hơn nữa thì tăng level lên 1
        }
        else if (className == "Assassin")
        {
            // ASSASSIN: Mỏng manh, dễ bị đẩy
            isSuperArmor = false;
        }
    }

    // Ghi đè hàm TakeDamage của cha (Stats) (Cho Chris)
    // Override phiên bản đầy đủ (DamageInfo)
    public override void TakeDamage(DamageInfo info)
    {
        // 1. Gọi logic cơ bản của cha (Trừ máu, xử lý Stun/Knockback ở Stats.cs)
        base.TakeDamage(info);

        // 2. Rung chuông báo động cho các Passive (chỉ cần gửi số damge)
        OnTakeDamage?.Invoke(info.damageAmount);
    }

    // Override phiên bản rút gọn (để tương thích ngược)
    //public override void TakeDamage(float damage)
    //{
    //    // Tự tạo một DamageInfo đơn giản (không crit, không hiệu ứng)
    //    DamageInfo info = new DamageInfo
    //    {
    //        damageAmount = damage,
    //        sourcePosition = transform.position // Tạm lấy vị trí bản thân (ko đẩy lùi)
    //    };
    //    TakeDamage(info); // Gọi hàm trên
    //}
    // Cho Leo Passive
    public void NotifyOnHitEnemy(Stats target, float t, bool isCrit)
    {
        OnHitEnemy?.Invoke(target, t, isCrit);
    }
    // Hàm để CombatMath gọi khi tính toán thấy Block thành công (Cho Vanguard)
    public void NotifyBlockSuccess()
    {
        OnBlockSuccess?.Invoke();
    }
    // Hàm gọi sự kiện (Sẽ được PlayerController gọi) (Cho Rouge)
    public void NotifyKillEnemy(Stats victim, bool isBackstab)
    {
        OnKillEnemy?.Invoke(victim, isBackstab);
    }
    // [MỚI] Hàm này chỉ tính lại Damage và Crit (Nhẹ, chạy được trong Update) (Dùng cho BloodReaver)
    public void CalculateCombatStatsOnly()
    {
        // 1. Tính Damage (Công thức copy từ RecalculateStats xuống)
        physicalAtk = (flatPhysicalAtk + STR * physicalAtkPerSTR) * (1 + bonusPhysicalAtk);
        //magicAtk = (flatMagicAtk + INT * magicAtkPerINT) * (1 + bonusMagicAtk);



        // 2. Tính Crit (Công thức copy từ RecalculateStats xuống)
        critChance = baseCritChance + critPerDEX * DEX + bonusCritChance;
        critMultiplier = baseCritMultiplier + bonusCritMultiplier;
        
    }

    public void CalculateMoveSpeed()
    {
        moveSpeed = baseMoveSpeed * (1 + ((0.05f * Mathf.Sqrt(AGI) - VIT * moveSpeedReducePerVIT) * (1 + trueMoveFlexibility) / 2)) * (1 + bonusMoveSpeed);
    }
    public override void Update()
    {
        base.Update();
        // Có thể gọi RecalculateStats ở đây để test (nhưng sẽ nặng máy), nên gọi khi cần thiết thôi.
    }


    // Hàm gọi khi né thành công (Có thể thêm slow motion ở đây sau này)
    public void TriggerPerfectDodge()
    {
        isPerfectDodgeSuccess = true;
        Debug.Log("<color=cyan>✨ PERFECT DODGE! ✨</color>");

        // Ví dụ: Hồi 10 Stamina ngay lập tức
        currentStamina = Mathf.Min(currentStamina + 10f, maxStamina);

        // [MỚI] Kích hoạt Slow Motion
        StartCoroutine(SlowMotionRoutine());

        // Reset cờ sau 1 khoảng ngắn (để đòn đánh sau đó biết mà kích hoạt hiệu ứng)
        StartCoroutine(ResetPerfectDodgeFlag());
    }

    // Coroutine xử lý Slow Motion
    IEnumerator SlowMotionRoutine()
    {
        // 1. Làm chậm thời gian
        Time.timeScale = slowMotionFactor;

        // [QUAN TRỌNG] Phải chỉnh cả FixedDeltaTime để vật lý không bị giật
        // Mặc định là 0.02f
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 2. Chờ thời gian thực (Vì Time.time đang bị chậm nên không dùng WaitForSeconds thường được)
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        // 3. Trả lại thời gian bình thường
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }

    IEnumerator ResetPerfectDodgeFlag()
    {
        // Giữ trạng thái "Vừa né xong" trong 1-2 giây để người chơi kịp bấm đánh phản công
        // Lưu ý: WaitForSeconds ở đây sẽ bị ảnh hưởng bởi timeScale, 
        // nhưng vì ta muốn người chơi có thời gian phản xạ trong lúc slow motion nên vẫn ổn.
        yield return new WaitForSecondsRealtime(5.0f);
        isPerfectDodgeSuccess = false;
    }
}
