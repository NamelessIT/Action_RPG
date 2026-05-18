using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
public class AllyStats : Stats
{
    [Header("--- Attribute Point---")]
    public int attributePointRemain;

    [Header("--- Sub-Health ---")]
    public float H = 200f;
    public float maxHpGainBonus = 10f;
    public float hpPerVIT = 15f;
    public float flatHpGain;
    public float hpGain;
    public float bonusHpGain;
    public float bonusHp;
    public float flatHp;

    [Header("--- Sub-Sin ---")]
    public float flatSinGain;
    public float sinGain;
    public float bonusSinGain;
    public float S = 100f;
    public float maxSinBonus=0.7f;

    [Header("--- Damage ---")]
    public float physicalAtkPerSTR = 2.5f;
    public float magicAtkPerINT = 3f;
    public float maxAttackSpeedBuff = 0.8f;
    public float critPerDEX = 0.0015f;
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

    [Header("--- Special Stats (Skill Tree) ---")]
    [Tooltip("% bỏ qua Defense Value của quái (0.1 = 10%)")]
    public float defenseValueIgnore = 0f;
    [Tooltip("% giảm tầm phát hiện của enemy (0.1 = giảm 10%)")]
    public float stealthReduction = 0f;
    [Tooltip("% tăng sát thương Signature (0.1 = +10%)")]
    public float signatureDamageBonus = 0f;
    [Tooltip("% tăng tốc độ bay projectile (0.1 = +10%)")]
    public float projectileSpeedBonus = 0f;
    [Tooltip("% giảm Stamina tiêu hao (0.2 = giảm 20%)")]
    public float staminaReduction = 0f;
    [Tooltip("Giây cộng thêm vào parry window của DuelistPassive")]
    public float parryWindow = 0f;
    [Tooltip("% tăng thêm cho Direction Bonus khi backstab (0.15 = tăng thêm 15%, 1.25→1.4)")]
    public float directionBonusBackstab = 0f;
    [Tooltip("% tăng tốc độ dash (0.2 = nhanh hơn 20%)")]
    public float dashSpeed = 0f;
    [Tooltip("% tăng DamageOutputMultiplier của Companion (0.3 = +30%)")]
    public float companionDamageOutputMult = 0f;
    [Tooltip("% tăng bonusHp của Companion (0.3 = +30%)")]
    public float companionBonusHp = 0f;
    [Tooltip("% tăng bonusCdr của Companion (0.2 = +20%)")]
    public float companionBonusCdr = 0f;

    [Header("--- Class System ---")]
    [Tooltip("Danh sách class đã học (0–2 phần tử). Tự động cập nhật khi unlock node Skill Tree.")]
    public List<string> classes = new List<string>();

    [Tooltip("'Chris' hoặc 'Leo' — xác định nhân vật, không thay đổi trong game.")]
    public string characterId = "Chris";

    /// <summary>Class đầu tiên học được (T3 N1 đầu tiên).</summary>
    public string PrimaryClass   => classes.Count > 0 ? classes[0] : "";
    /// <summary>Class thứ hai (T3 N1 thứ hai). Trống nếu chưa học.</summary>
    public string SecondaryClass => classes.Count > 1 ? classes[1] : "";
    /// <summary>Tên hiển thị: "ClassA+ClassB" sau khi kết hợp, hoặc "ClassA" nếu còn đơn.</summary>
    public string DisplayClassName => classes.Count > 0 ? string.Join("+", classes) : "—";

    [Header("--- Combat Status ---")]
    public bool isPerfectDodgeSuccess = false;
    public Action OnPerfectDodgeTriggered;

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

    // ---------------- Rogue ----------------
    // Action gửi đi: 
    // 1. Stats victim (Kẻ vừa bị giết)
    // 2. bool isBackstab (Có phải giết từ sau lưng không)
    public Action<Stats, bool> OnKillEnemy;

    public override void Start()
    {
        base.Start();
        // [MỚI] Khởi tạo điểm ban đầu. 
        // Nếu bắt đầu game ở level 1 -> 5 điểm. Nếu setup level 10 -> 50 điểm.
        if (attributePointRemain == 0) attributePointRemain = level * 5;
        // Gọi tính toán lần đầu
        RecalculateStats();
        InitializeClassStats();
        currentHp = maxHp;
        this.tag = "Ally";
    }

    // [MỚI] Ghi đè hàm thăng cấp của Stats cha
    protected override void LevelUp()
    {
        base.LevelUp(); // Cập nhật số level ở file cha

        attributePointRemain += 5; // Cấp 5 điểm tiềm năng

        // Gọi tính toán lại chỉ số (Giúp baseHp tăng lên nhờ công thức: 100 + 20 * level)
        RecalculateStats();

        // Đặc ân khi lên cấp: Hồi đầy máu
        currentHp = maxHp;

        Debug.Log($"<color=cyan>[AllyStats]</color> {gameObject.name} nhận 5 Attribute Points (Tổng: {attributePointRemain})");
        Debug.Log($"<color=gray>Lưu ý UI sau này: Chỉ số tối đa được cộng là {(level * 3) + 10}</color>");
    }
    /// <summary>Override trong PlayerStats để khởi tạo chỉ số theo nhân vật cụ thể.</summary>
    protected virtual void InitializeClassStats() { }

    // Hàm này phải được gọi mỗi khi: Lên cấp, Đổi đồ, Nhận Buff, Chịu Debuff
    public void RecalculateStats()
    {
        // ==========================================
        // TÍNH TOÁN CHỈ SỐ GỐC 
        // ==========================================
        // 0. Tính Attribute Stat 
        // Công thức: Tổng = (Base * % Tăng thêm) + Điểm cộng thẳng từ đồ
        STR = (baseSTR * (1 + bonusSTR)) + flatSTR;
        INT = (baseINT * (1 + bonusINT)) + flatINT;
        DEX = (baseDEX * (1 + bonusDEX)) + flatDEX;
        AGI = (baseAGI * (1 + bonusAGI)) + flatAGI;
        VIT = (baseVIT * (1 + bonusVIT)) + flatVIT;

        // 1. Tính HP
        baseHp = initialBaseHp + 20 * level;
        hpGain = (baseHpGain * (1 + maxHpGainBonus * VIT / (VIT + H)) + flatHpGain) * (1 + bonusHpGain);
        maxHp = (flatHp + baseHp + hpPerVIT * VIT) * (1 + bonusHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        // 2. Tính Sin
        sinGain = (baseSinGain * (1 + maxSinBonus * INT / (INT + S)) + flatSinGain) * (1 + bonusSinGain);

        SkillManager skillManager = GetComponent<SkillManager>();
        if (skillManager != null && skillManager.currentSignature != null)
        {
            maxSin = skillManager.currentSignature.sinChargeReq;
        }
        else
        {
            maxSin = 40f;
        }
        currentSin = Mathf.Clamp(currentSin, 0, maxSin);

        // 3. Tính Damage
        physicalAtk = (flatPhysicalAtk + STR * physicalAtkPerSTR) * (1 + bonusPhysicalAtk);
        magicAtk = (flatMagicAtk + INT * magicAtkPerINT) * (1 + bonusMagicAtk);

        // 4. Tính Attack Speed 
        float agiAttackSpeedBonus = maxAttackSpeedBuff * AGI / (AGI + A);
        attackSpeed = baseAttackSpeed * (1 + agiAttackSpeedBonus + bonusAttackSpeed);

        // 5. Tính Crit
        critChance = baseCritChance + critPerDEX * DEX + bonusCritChance;
        critMultiplier = baseCritMultiplier + bonusCritMultiplier;

        // 6. Tính Movement & Flexibility
        trueMoveFlexibility = 1 - ((1 - moveFlexibility) * (1 - maxReduceBySTR * STR / (STR + M)));
        combatTurnDuration = 0.6f - (0.5f * trueMoveFlexibility);

        moveSpeed = baseMoveSpeed * (1 + ((0.05f * Mathf.Sqrt(AGI) - VIT * moveSpeedReducePerVIT) * (1 + trueMoveFlexibility) / 2)) * (1 + bonusMoveSpeed);

        if (moveSpeed < minSpeed) moveSpeed = minSpeed;
        if (moveSpeed > maxSpeed) moveSpeed = maxSpeed;

        // 7. Tính Dash
        dashDistance = baseDashDistance * (1f - trueMoveFlexibility);
        dashRecovery = (baseDashRecovery + (1f - trueMoveFlexibility)) * (1f - maxDashReduction * DEX / (DEX + R));

        if (AGI >= AGI_ThreshHold) dashCost = 15;
        else dashCost = 20;

        // 8. Cooldown Reduction
        cooldownReduction = baseCdr + (cdrPerAGI * AGI) + bonusCdr;

        // 9. Stealth Factor (dùng bởi EnemyAI.CanSeeTarget)
        stealthFactor = 1.0f - Mathf.Clamp01(stealthReduction);
    }
    // Ghi đè hàm TakeDamage của cha (Stats) (Cho Chris)
    // Override phiên bản đầy đủ (DamageInfo)
    public override void TakeDamage(DamageInfo info)
    {
        // 1. Gọi logic cơ bản của cha (Trừ máu, xử lý Stun/Knockback ở Stats.cs)
        base.TakeDamage(info);

        // 2. Rung chuông báo động cho các Passive (chỉ cần gửi số damge)
        OnTakeDamage?.Invoke(info.TotalRawDamage);
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
    // Hàm gọi sự kiện (Sẽ được PlayerController gọi) (Cho Rogue)
    public void NotifyKillEnemy(Stats victim, bool isBackstab)
    {
        OnKillEnemy?.Invoke(victim, isBackstab);
    }
    // [MỚI] Hàm này chỉ tính lại Damage và Crit (Nhẹ, chạy được trong Update) (Dùng cho BloodReaver)
    public void CalculateCombatStatsOnly()
    {
        // 1. Tính Damage (Công thức copy từ RecalculateStats xuống)
        physicalAtk = (flatPhysicalAtk + STR * physicalAtkPerSTR) * (1 + bonusPhysicalAtk);
        magicAtk = (flatMagicAtk + INT * magicAtkPerINT) * (1 + bonusMagicAtk);

        // 2. Tính Crit (Công thức copy từ RecalculateStats xuống)
        critChance = baseCritChance + critPerDEX * DEX + bonusCritChance;
        critMultiplier = baseCritMultiplier + bonusCritMultiplier;

        // 3. Tính AttackSpeed
        // [ĐÃ SỬA] Tính lại tương tự như trên RecalculateStats
        float agiAttackSpeedBonus = maxAttackSpeedBuff * AGI / (AGI + A);
        attackSpeed = baseAttackSpeed * (1 + agiAttackSpeedBonus + bonusAttackSpeed);
    }
    public void CalculateMoveSpeedOnly()
    {
        moveSpeed = baseMoveSpeed * (1 + ((0.05f * Mathf.Sqrt(AGI) - VIT * moveSpeedReducePerVIT) * (1 + trueMoveFlexibility) / 2)) * (1 + bonusMoveSpeed);
        if (moveSpeed < minSpeed) moveSpeed = minSpeed; // Min speed
        if (moveSpeed > maxSpeed) moveSpeed = maxSpeed;
    }
    public override void Update()
    {
        base.Update();
        // Có thể gọi RecalculateStats ở đây để test (nhưng sẽ nặng máy), nên gọi khi cần thiết thôi.
        // HỒI MÁU TỰ NHIÊN (HpGain)
        if (currentHp > 0 && currentHp < maxHp && !isHealingBlocked)
        {
            // outCombat = true thì hồi x2, false thì x1
            float multiplier = outCombat ? 2f : 1f;
            float amount = hpGain * multiplier * Time.deltaTime;
            if (amount <= 0f) return;

            // showPopup=false: không hiện số xanh lá cho hồi máu tự nhiên
            Heal(amount, false);
        }
    }


    // Hàm gọi khi né thành công (Có thể thêm slow motion ở đây sau này)
    public void TriggerPerfectDodge()
    {
        isPerfectDodgeSuccess = true;
        OnPerfectDodgeTriggered?.Invoke(); // [MỚI] Rung chuông báo hiệu né thành công
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

    // --- [MỚI] HÀM TÍNH TOÁN VÀ CỘNG SIN ---
    public void GainSinFromAttack(int enemiesHitCount)
    {
        if (enemiesHitCount <= 0) return;

        // Công thức: y = 2 * sinGain - sinGain * (0.5)^(x - 1)
        // Lưu ý dùng Mathf.Pow cho số mũ
        float totalSinEarned = (2f * sinGain) - (sinGain * Mathf.Pow(0.5f, enemiesHitCount - 1));

        // Cộng vào currentSin và giới hạn ở mức maxSin
        currentSin += totalSinEarned;
        if (currentSin > maxSin) currentSin = maxSin;

        Debug.Log($"<color=purple>Đánh trúng {enemiesHitCount} địch -> Hồi {totalSinEarned:F2} Sin (Current: {currentSin:F1}/{maxSin})</color>");
    }
    // [MỚI] Phe Ally chết thì không bị Destroy để còn Hồi sinh
    protected override void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log($"{gameObject.name} đã gục ngã (Chờ hồi sinh)!");
        // [QUAN TRỌNG NHẤT] Hủy mọi Coroutine (Knockback/Stun/Buff) đang chạy ngầm trên Stats
        StopAllCoroutines();
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody myRb = GetComponent<Rigidbody>();
        if (myRb != null)
        {
            myRb.linearVelocity = Vector3.zero;
            //myRb.angularVelocity = Vector3.zero;
            myRb.isKinematic = true; // Đóng băng vật lý ngay lập tức
        }

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Tắt các Script điều khiển
        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null) playerCtrl.enabled = false;

        var companionAI = GetComponent<CompanionAI>();
        if (companionAI != null) companionAI.enabled = false;

        // ĐẶC BIỆT: KHÔNG GỌI Destroy(gameObject) Ở ĐÂY
    }
}
