using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class CoreShieldEffectManager : MonoBehaviour
{
    private PlayerController player;
    private AllyStats stats;
    private EquipmentManager eqManager;
    private SkillManager skillManager;
    private Stats companionStats; // Lưu giữ dây cáp

    private Dictionary<string, Action<DamageInfo>> onBeforeTakeDamageEffects = new Dictionary<string, Action<DamageInfo>>();
    private Dictionary<string, Action<float, Stats>> onDamageReceivedEffects = new Dictionary<string, Action<float, Stats>>();
    private Dictionary<string, Action<Stats, int, bool, bool>> onHitEnemyEffects = new Dictionary<string, Action<Stats, int, bool, bool>>();
    private Dictionary<string, Action> onPerfectDodgeEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onSkillCastEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onSignatureCastEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onPerfectParryEffects = new Dictionary<string, Action>();


    void Awake()
    {
        player = GetComponent<PlayerController>();
        stats = GetComponent<AllyStats>();
        eqManager = GetComponent<EquipmentManager>();
        // [SỬA ĐỔI 1] Quét tìm SkillManager ở mọi ngóc ngách của Player
        skillManager = GetComponentInChildren<SkillManager>();
        // Đi tìm Companion và cắm dây cáp lắng nghe sự kiện BỊ ĐÁNH của nó
        CompanionAI comp = FindFirstObjectByType<CompanionAI>();
        if (comp != null)
        {
            companionStats = comp.GetComponent<Stats>();
            // Cắm cáp: Khi Companion sắp bị trừ máu, gọi hàm Gánh Dame
            companionStats.OnBeforeTakeDamage += HandleCompanionDamageShare;
        }
        RegisterAllShieldEffects();
    }

    private void RegisterAllShieldEffects()
    {
        // On Before Damage (Giảm dame, Phản dame)
        onBeforeTakeDamageEffects.Add("SHD_CS_T3_01", Effect_SHD_CS_T3_01);
        onBeforeTakeDamageEffects.Add("SHD_CS_T3_03", Effect_SHD_CS_T3_03);
        onBeforeTakeDamageEffects.Add("SHD_CS_T4_03", Effect_SHD_CS_T4_03);
        onBeforeTakeDamageEffects.Add("SHD_CS_T5_01", Effect_SHD_CS_T5_01); // SHD_CS_T5_01 nằm ở đây là đúng chuẩn vì nó xài DamageInfo
        onBeforeTakeDamageEffects.Add("SHD_CS_T5_03", Effect_SHD_CS_T5_03);
        onBeforeTakeDamageEffects.Add("SHD_CS_T5_05", Effect_SHD_CS_T5_05_MarkAttacker);  // Vì có chứa thông tin attacker.

        // On Damage Received (Kích hoạt khi máu tụt)
        onDamageReceivedEffects.Add("SHD_CS_T3_05", Effect_SHD_CS_T3_05);

        // On Hit Enemy
        onHitEnemyEffects.Add("SHD_CS_T3_05", Effect_SHD_CS_T3_05_LifestealHit);
        onHitEnemyEffects.Add("SHD_CS_T4_04", Effect_SHD_CS_T4_04);
        onHitEnemyEffects.Add("SHD_CS_T4_06", Effect_SHD_CS_T4_06_ApplyHit);
        onHitEnemyEffects.Add("SHD_CS_T4_07", Effect_SHD_CS_T4_07);
        onHitEnemyEffects.Add("SHD_CS_T5_05", Effect_SHD_CS_T5_05_ReduceCDHit);
        onHitEnemyEffects.Add("SHD_CS_T5_07", Effect_SHD_CS_T5_07);

        // On Perfect Dodge/Parry
        onPerfectDodgeEffects.Add("SHD_CS_T3_04", Effect_SHD_CS_T3_04);
        onPerfectDodgeEffects.Add("SHD_CS_T4_05", Effect_SHD_CS_T4_05);
        onPerfectParryEffects.Add("SHD_CS_T4_06", Effect_SHD_CS_T4_06_TriggerBuff);

        // On Skill Cast
        onSkillCastEffects.Add("SHD_CS_T3_02", Effect_SHD_CS_T3_02);
        onSkillCastEffects.Add("SHD_CS_T5_08", Effect_SHD_CS_T5_08);

        // On Signature Cast
        onSignatureCastEffects.Add("SHD_CS_T3_02", Effect_SHD_CS_T3_02);
        onSignatureCastEffects.Add("SHD_CS_T5_06", Effect_SHD_CS_T5_06);
        onSignatureCastEffects.Add("SHD_CS_T5_08", Effect_SHD_CS_T5_08);
    }

    void OnEnable()
    {
        if (player != null) player.OnHitEnemy += HandleOnHitEnemy;
        if (stats != null)
        {
            stats.OnBeforeTakeDamage += HandleOnBeforeTakeDamage;
            stats.OnDamageReceived += HandleOnDamageReceived;
            stats.OnPerfectDodgeTriggered += HandleOnPerfectDodge;
            stats.OnHealReceived += HandleOnHealReceived;

            if (stats is PlayerStats playerStats)
                playerStats.OnPerfectParryTriggered += HandleOnPerfectParry;
            if (eqManager != null)
            {
                eqManager.OnEquipmentChanged += HandleEquipmentChanged;
            }
        }
    }

    void OnDisable()
    {
        if (player != null) player.OnHitEnemy -= HandleOnHitEnemy;
        if (stats != null)
        {
            stats.OnBeforeTakeDamage -= HandleOnBeforeTakeDamage;
            stats.OnDamageReceived -= HandleOnDamageReceived;
            stats.OnPerfectDodgeTriggered -= HandleOnPerfectDodge;
            stats.OnHealReceived -= HandleOnHealReceived;

            if (stats is PlayerStats playerStats)
                playerStats.OnPerfectParryTriggered -= HandleOnPerfectParry;
            if (eqManager != null)
            {
                eqManager.OnEquipmentChanged -= HandleEquipmentChanged;
            }
            if (companionStats != null)
            {
                companionStats.OnBeforeTakeDamage -= HandleCompanionDamageShare;
            }
        }
    }

    // ==========================================
    // CÁC ROUTER ĐIỀU HƯỚNG TỰ ĐỘNG
    // ==========================================
    // [MỚI] Hàm kiểm tra ngay lúc vừa mặc đồ xong
    private void HandleEquipmentChanged()
    {
        if (eqManager == null || eqManager.currentCoreShield == null) return;

        string safeId = eqManager.currentCoreShield.id.Trim();
        if (safeId == "SHD_CS_T3_05")
        {
            Check_SHD_CS_T3_05_Trigger(); // Quét xem máu có đang dưới 30% không
        }
        if (safeId == "SHD_CS_T5_08")
        {
            Update_SHD_CS_T5_08_HpPool(true);
        }
    }
    private void HandleOnBeforeTakeDamage(DamageInfo info)
    {
        if (eqManager.currentCoreShield != null && onBeforeTakeDamageEffects.ContainsKey(eqManager.currentCoreShield.id))
            onBeforeTakeDamageEffects[eqManager.currentCoreShield.id].Invoke(info);
    }
    private void HandleOnDamageReceived(float dmg, Stats myStats)
    {
        if (eqManager.currentCoreShield != null && onDamageReceivedEffects.ContainsKey(eqManager.currentCoreShield.id))
            onDamageReceivedEffects[eqManager.currentCoreShield.id].Invoke(dmg, myStats);
    }
    private void HandleOnHitEnemy(Stats target, int step, bool isHeavy, bool isCrit)
    {
        if (eqManager.currentCoreShield != null && onHitEnemyEffects.ContainsKey(eqManager.currentCoreShield.id))
            onHitEnemyEffects[eqManager.currentCoreShield.id].Invoke(target, step, isHeavy, isCrit);
    }
    private void HandleOnPerfectDodge()
    {
        if (eqManager.currentCoreShield != null && onPerfectDodgeEffects.ContainsKey(eqManager.currentCoreShield.id))
            onPerfectDodgeEffects[eqManager.currentCoreShield.id].Invoke();
    }
    // [ĐÃ ĐỔI THÀNH PUBLIC VÀ NÂNG CẤP DÒ TÌM]
    // [ĐÃ NÂNG CẤP] Nhận biết loại kỹ năng
    public void TriggerSkillCastEffects(SkillData.SkillType skillType)
    {
        if (eqManager == null || eqManager.currentCoreShield == null) return;

        string safeId = eqManager.currentCoreShield.id.Trim();

        // 1. Kích hoạt hiệu ứng của Chiêu E
        if (skillType == SkillData.SkillType.Skill)
        {
            if (onSkillCastEffects.ContainsKey(safeId))
            {
                onSkillCastEffects[safeId].Invoke();
            }
        }
        // 2. Kích hoạt hiệu ứng của Chiêu Q (Signature)
        else if (skillType == SkillData.SkillType.Signature)
        {
            if (onSignatureCastEffects.ContainsKey(safeId))
            {
                onSignatureCastEffects[safeId].Invoke();
            }
        }
    }
    private void HandleOnPerfectParry()
    {
        if (eqManager.currentCoreShield != null && onPerfectParryEffects.ContainsKey(eqManager.currentCoreShield.id))
            onPerfectParryEffects[eqManager.currentCoreShield.id].Invoke();
    }


    // ==========================================
    // LOGIC THỰC TẾ CHO 21 CÁI KHIÊN
    // ==========================================

    private void Effect_SHD_CS_T3_01(DamageInfo info)
    {
        Vector3 myFacing = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
        Vector3 dirToAttacker = (info.sourcePosition - transform.position);
        dirToAttacker.y = 0;

        float angle = Vector3.Angle(myFacing, dirToAttacker.normalized);
        if (angle < 45f)
        {
            info.physDamage *= 0.95f;
            info.magicDamage *= 0.95f;
            Debug.Log($"<color=green>[SHD_CS_T3_01]</color> Giảm 5% DMG trực diện! Angle: {angle}");
        }
    }

    private void Effect_SHD_CS_T3_02()
    {
        StartCoroutine(TemporaryShieldRoutine(200f, 3f));
    }
    private IEnumerator TemporaryShieldRoutine(float amount, float duration)
    {
        stats.currentShield += amount;
        Debug.Log($"<color=cyan>[SHD_CS_T3_02]</color> Nhận {amount} giáp ảo (3s)");

        yield return new WaitForSeconds(duration);

        stats.currentShield -= amount;
        if (stats.currentShield < 0) stats.currentShield = 0;
        Debug.Log("<color=cyan>[SHD_CS_T3_02]</color> Giáp ảo đã hết hạn.");
    }

    private void Effect_SHD_CS_T3_03(DamageInfo info)
    {
        Vector3 myFacing = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
        Vector3 dirToAttacker = (info.sourcePosition - transform.position);
        dirToAttacker.y = 0;

        float angle = Vector3.Angle(myFacing, dirToAttacker.normalized);
        if (angle < 45f)
        {
            float reflectDmg = info.TotalRawDamage * 0.15f;
            Collider[] enemies = Physics.OverlapSphere(transform.position, 1f, player.dangerLayer);
            foreach (var e in enemies)
            {
                Stats eStats = e.GetComponent<Stats>();
                if (eStats != null) eStats.TakeDamage(reflectDmg);
            }
        }
    }
    // SHD_CS_T3_04: BUFF TỐC ĐỘ (CÓ KHÓA CHỐNG SPAM)
    // ==========================================
    private Coroutine SHD_CS_T3_04_Coroutine;
    private bool SHD_CS_T3_04_IsBuffed = false;
    private void Effect_SHD_CS_T3_04()
    {
        // 1. Nếu đang đếm ngược 2 giây rồi thì HỦY cái cũ đi (để reset lại thời gian)
        if (SHD_CS_T3_04_Coroutine != null)
        {
            StopCoroutine(SHD_CS_T3_04_Coroutine);
        }

        // 2. Chạy lại tiến trình mới
        SHD_CS_T3_04_Coroutine = StartCoroutine(T3_04_BuffRoutine());
    }

    private IEnumerator T3_04_BuffRoutine()
    {
        // CHÌA KHÓA: Chỉ cộng thêm 10% nếu nhân vật chưa có Buff này
        if (!SHD_CS_T3_04_IsBuffed)
        {
            stats.bonusMoveSpeed += 0.10f;
            SHD_CS_T3_04_IsBuffed = true;
            stats.RecalculateStats();
            Debug.Log("<color=yellow>[SHD_CS_T3_04]</color> Né hoàn hảo: +10% Tốc chạy (2s)");
        }
        else
        {
            Debug.Log("<color=yellow>[SHD_CS_T3_04]</color> Đã có buff, làm mới lại thời gian 2s!");
        }

        // Bắt đầu đếm thời gian
        yield return new WaitForSeconds(2f);

        // Hết 2 giây -> Thu hồi Buff
        if (SHD_CS_T3_04_IsBuffed)
        {
            stats.bonusMoveSpeed -= 0.10f;
            SHD_CS_T3_04_IsBuffed = false;
            stats.RecalculateStats();
            Debug.Log("<color=yellow>[SHD_CS_T3_04]</color> Hết 2 giây, đã xóa buff tốc chạy.");
        }

        SHD_CS_T3_04_Coroutine = null;
    }

    private bool SHD_CS_T3_05_BloodShieldActive = false;
    private bool SHD_CS_T3_05_HasTriggered = false; // Cờ khóa chống spam bất tử

    // Hàm kiểm tra dùng chung (Cho cả khi bị đánh lẫn khi vừa mặc đồ)
    private void Check_SHD_CS_T3_05_Trigger()
    {
        float hpPercent = stats.currentHp / stats.maxHp;

        // [CƠ CHẾ AAA]: Nếu máu hồi phục an toàn lên trên 30%, mở khóa cò súng
        if (hpPercent > 0.3f && SHD_CS_T3_05_HasTriggered)
        {
            SHD_CS_T3_05_HasTriggered = false;
        }

        // Kích hoạt: Máu dưới 30%, Huyết Thuẫn đang tắt, và Cò súng chưa bị khóa
        if (hpPercent <= 0.3f && !SHD_CS_T3_05_BloodShieldActive && !SHD_CS_T3_05_HasTriggered)
        {
            SHD_CS_T3_05_BloodShieldActive = true;
            SHD_CS_T3_05_HasTriggered = true; // Khóa cò lại cho đến khi máu > 30%
            Debug.Log("<color=red>[SHD_CS_T3_05]</color> HUYẾT THUẪN KÍCH HOẠT! (Tồn tại 5 giây)");

            Invoke(nameof(ResetT3_05), 5f);
        }
    }

    private void Effect_SHD_CS_T3_05(float dmg, Stats myStats)
    {
        // Chỉ việc gọi hàm Check mỗi khi nhận sát thương
        Check_SHD_CS_T3_05_Trigger();
    }

    private void ResetT3_05()
    {
        SHD_CS_T3_05_BloodShieldActive = false;
        Debug.Log("<color=red>[SHD_CS_T3_05]</color> Huyết Thuẫn đã tắt.");
    }

    // Khi đánh trúng địch lúc Huyết Thuẫn đang bật
    private void Effect_SHD_CS_T3_05_LifestealHit(Stats target, int step, bool isH, bool isC)
    {
        if (SHD_CS_T3_05_BloodShieldActive)
        {
            // LƯU Ý CHO BẠN: Vì hàm OnHitEnemy của bạn không có biến truyền Lượng Sát Thương thực tế,
            // nên tui dùng tạm `stats.physicalAtk` của nhân vật để làm Sát Thương Gốc.
            // (Nếu bạn dùng phép, có thể đổi thành stats.magicAtk, hoặc tổng của cả 2)
            float approximateDamageDealt = stats.physicalAtk + stats.magicAtk;

            float totalHeal = approximateDamageDealt * 0.10f; // 10% sát thương gây ra

            // Khởi chạy Coroutine để hồi máu từ từ trong 5 giây
            StartCoroutine(T3_05_HealOverTime(totalHeal, 5f));
        }
    }

    // Coroutine chia nhỏ lượng máu ra hồi trong 5 giây (Hồi từ từ)
    private IEnumerator T3_05_HealOverTime(float totalHealAmount, float duration)
    {
        int ticks = 5; // Chia làm 5 nhịp (mỗi giây hồi 1 lần)
        float healPerTick = totalHealAmount / ticks;
        float timePerTick = duration / ticks;

        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForSeconds(timePerTick);
            stats.Heal(healPerTick);
            Debug.Log($"<color=green>[Huyết Thuẫn]</color> Đang hồi {healPerTick} HP...");
        }
    }


    // SHD_CS_T4_01 & SHD_CS_T4_02 (Update)
    private float SHD_CS_T4_01_StandTimer = 0f;
    private bool SHD_CS_T4_01_HolyGround = false;
    private bool SHD_CS_T4_02_ShieldActive = false;
    private float SHD_CS_T4_02_GrantedShield = 0f; // Lưu trữ chính xác lượng giáp đã cấp

    void Update()
    {
        // 1. Chốt chặn an toàn: Nếu không đeo khiên thì tắt sạch hiệu ứng Update cũ
        if (eqManager == null || eqManager.currentCoreShield == null)
        {
            ResetDynamicShieldEffects();
            return;
        }
        string id = eqManager.currentCoreShield.id.Trim();

        // ==========================================
        // SHD_CS_T4_01: VÙNG ĐẤT THÁNH
        // ==========================================
        if (id == "SHD_CS_T4_01")
        {
            if (!player.isWalking && !player.isDashing)
            {
                SHD_CS_T4_01_StandTimer += Time.deltaTime;
                // BẬT (Chỉ chạy đúng 1 lần nhờ cờ SHD_CS_T4_01_HolyGround)
                if (SHD_CS_T4_01_StandTimer >= 2.0f && !SHD_CS_T4_01_HolyGround)
                {
                    stats.armor += 50;
                    stats.magicResist += 50;
                    stats.isSuperArmor = true;
                    stats.superArmorLevel += 2;
                    SHD_CS_T4_01_HolyGround = true;

                    stats.RecalculateStats(); // Báo UI update
                    Debug.Log("<color=yellow>[T4_01]</color> Vùng Đất Thánh kích hoạt! (+50 Giáp/Kháng)");
                }
            }
            else
            {
                // TẮT (Chỉ trừ khi nó ĐANG BẬT)
                if (SHD_CS_T4_01_HolyGround)
                {
                    ForceTurnOff_T4_01();
                    Debug.Log("<color=yellow>[T4_01]</color> Mất Vùng Đất Thánh do di chuyển.");
                }
                SHD_CS_T4_01_StandTimer = 0f; // Vừa nhích nhẹ là timer reset ngay
            }
        }
        else
        {
            // Chốt chặn: Đang đứng yên mà tháo khiên T4_01 ra thay khiên khác
            if (SHD_CS_T4_01_HolyGround) ForceTurnOff_T4_01();
        }

        // ==========================================
        // SHD_CS_T4_02: GIÁP ẢO TẤN CÔNG
        // ==========================================
        if (id == "SHD_CS_T4_02")
        {
            if (player.isAttacking && !SHD_CS_T4_02_ShieldActive)
            {
                SHD_CS_T4_02_GrantedShield = stats.maxHp * 0.10f;
                stats.currentShield += SHD_CS_T4_02_GrantedShield;
                SHD_CS_T4_02_ShieldActive = true;
                // Debug.Log($"<color=cyan>[T4_02]</color> Nhận {t4_02_GrantedShield} Giáp ảo khi vung vũ khí");
            }
            else if (!player.isAttacking && SHD_CS_T4_02_ShieldActive)
            {
                ForceTurnOff_T4_02();
                // Debug.Log("<color=cyan>[T4_02]</color> Mất giáp ảo tấn công");
            }
        }
        else
        {
            // Chốt chặn: Đang đánh mà đổi khiên
            if (SHD_CS_T4_02_ShieldActive) ForceTurnOff_T4_02();
        }
        // ==========================================
        // SHD_CS_T4_08: BUFF KHI COMPANION CHẾT
        // ==========================================
        if (id == "SHD_CS_T4_08")
        {
            CompanionAI comp = FindFirstObjectByType<CompanionAI>();
            bool isCompDead = (comp != null && comp.GetComponent<Stats>().isDead);

            // BẬT BUFF (Chỉ chạy 1 lần khi Companion vừa nằm xuống)
            if (isCompDead && !SHD_CS_T4_08_DeathBuffActive)
            {
                stats.damageOutputMultiplier += 0.5f;
                SHD_CS_T4_08_DeathBuffActive = true;
                stats.RecalculateStats();
                Debug.Log("<color=red>[T4_08]</color> Companion đã gục ngã! Player phẫn nộ nhận +50% Sát thương.");
            }
            // TẮT BUFF (Chỉ chạy 1 lần khi Companion vừa sống lại)
            else if (!isCompDead && SHD_CS_T4_08_DeathBuffActive)
            {
                ForceTurnOff_T4_08_Buff();
                Debug.Log("<color=red>[T4_08]</color> Companion đã sống lại! Mất buff phẫn nộ.");
            }
        }
        else
        {
            // Chốt chặn: Đang có buff chết mà người chơi tháo khiên ra
            if (SHD_CS_T4_08_DeathBuffActive) ForceTurnOff_T4_08_Buff();
        }

        // SHD_CS_T5_01: ĐẾM GIỜ THÍCH NGHI 3 GIÂY
        if (id == "SHD_CS_T5_01")
        {
            t5_01_Timer += Time.deltaTime;
            if (t5_01_Timer >= 3f)
            {
                EvaluateT5_01_Adaptation();
                t5_01_Timer = 0f; // Reset đếm 3s
            }
        }
        else
        {
            // Chốt chặn: Nếu tháo khiên ra thì tắt buff ngay
            ForceTurnOff_T5_01();
        }
    }

    // Ném hàm tắt này xuống chỗ các hàm ForceTurnOff hôm trước
    private void ForceTurnOff_T4_08_Buff()
    {
        stats.damageOutputMultiplier -= 0.5f;
        SHD_CS_T4_08_DeathBuffActive = false;
        stats.RecalculateStats();
    }

    // ==========================================
    // CÁC HÀM TẮT ĐIỆN KHẨN CẤP (Dùng khi Tháo đồ hoặc Hết điều kiện)
    // ==========================================
    private void ResetDynamicShieldEffects()
    {
        if (SHD_CS_T4_01_HolyGround) ForceTurnOff_T4_01();
        if (SHD_CS_T4_02_ShieldActive) ForceTurnOff_T4_02();
        if (SHD_CS_T4_08_DeathBuffActive) ForceTurnOff_T4_08_Buff();
        ForceTurnOff_T5_01();
        Update_SHD_CS_T5_08_HpPool(false);
    }

    private void ForceTurnOff_T4_01()
    {
        stats.armor -= 50;
        stats.magicResist -= 50;
        stats.isSuperArmor = false;
        stats.superArmorLevel -= 2;
        SHD_CS_T4_01_HolyGround = false;
        SHD_CS_T4_01_StandTimer = 0f;
        stats.RecalculateStats();
    }

    private void ForceTurnOff_T4_02()
    {
        stats.currentShield -= SHD_CS_T4_02_GrantedShield;

        // CHỐT CHẶN CHỐNG ÂM TIỀN (Chống "Gánh nợ giáp" khi bị quái cắn rách giáp)
        if (stats.currentShield < 0) stats.currentShield = 0;

        SHD_CS_T4_02_ShieldActive = false;
        SHD_CS_T4_02_GrantedShield = 0f;
    }

    private void Effect_SHD_CS_T4_03(DamageInfo info)
    {
        float hpPercent = stats.currentHp / stats.maxHp;
        if (hpPercent < 0.5f)
        {
            float reflectPercent = Mathf.Lerp(0.5f, 0.2f, (hpPercent - 0.25f) / 0.25f);
            reflectPercent = Mathf.Clamp(reflectPercent, 0.2f, 0.5f);
            if (info.attacker != null) info.attacker.TakeDamage(info.TotalRawDamage * reflectPercent);
        }
    }

    // ==========================================
    // SHD_CS_T4_04: BUFF TỐC ĐÁNH (CÓ KHÓA CHỐNG SPAM)
    // ==========================================
    private Coroutine SHD_CS_T4_Coroutine;
    private bool SHD_CS_T4_IsBuffed = false;

    private void Effect_SHD_CS_T4_04(Stats target, int step, bool isH, bool isC)
    {
        // KIỂM TRA SÁT THƯƠNG PHÉP
        // (Lưu ý: Vì hàm OnHitEnemy của bạn hiện chưa truyền thông tin Damage Type vào, 
        // nên tui tạm dùng biến stats.magicAtk > 0 để check. Nếu game của bạn phân biệt 
        // sát thương phép bằng biến khác (ví dụ: vũ khí phép, hoặc skill phép) thì bạn 
        // hãy thay thế điều kiện tui viết dưới đây nhé!)

        bool isMagicAttack = (stats.magicAtk > 120); // ĐIỀU KIỆN TẠM THỜI

        if (!isMagicAttack) return; // Nếu không phải đánh phép -> Bỏ qua ngay lập tức

        // --- GIẢI QUYẾT LỖI 2: RESET THỜI GIAN THAY VÌ CỘNG DỒN ---
        // Nếu đang đếm ngược 3 giây rồi mà lại đánh trúng tiếp -> Hủy bộ đếm cũ
        if (SHD_CS_T4_Coroutine != null)
        {
            StopCoroutine(SHD_CS_T4_Coroutine);
        }

        // Khởi động lại bộ đếm thời gian mới
        SHD_CS_T4_Coroutine = StartCoroutine(T4_04_BuffRoutine());
    }

    private IEnumerator T4_04_BuffRoutine()
    {
        // CHỈ CỘNG 10% NẾU CHƯA CÓ BUFF
        if (!SHD_CS_T4_IsBuffed)
        {
            stats.bonusAttackSpeed += 0.10f;
            SHD_CS_T4_IsBuffed = true;

            // --- GIẢI QUYẾT LỖI 3: GỌI RECALCULATE ---
            stats.CalculateCombatStatsOnly();
            Debug.Log("<color=yellow>[SHD_CS_T4_04]</color> Đánh phép: +10% Tốc đánh (3s)");
        }
        else
        {
            // Nếu đã có buff rồi thì chỉ chạy vào đây để reset Coroutine (Kéo dài thêm 3s)
            // Debug.Log("<color=yellow>[SHD_CS_T4_04]</color> Làm mới thời gian 3s!");
        }

        // Bắt đầu đếm 3 giây
        yield return new WaitForSeconds(3f);

        // Sau khi hết 3 giây -> Thu hồi Buff
        if (SHD_CS_T4_IsBuffed)
        {
            stats.bonusAttackSpeed -= 0.10f;
            SHD_CS_T4_IsBuffed = false;

            // --- GỌI RECALCULATE KHI MẤT BUFF ---
            stats.CalculateCombatStatsOnly();
            Debug.Log("<color=yellow>[SHD_CS_T4_04]</color> Mất buff tốc đánh.");
        }

        SHD_CS_T4_Coroutine = null;
    }

    private void Effect_SHD_CS_T4_05()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, 0.5f, player.dangerLayer);
        foreach (var e in enemies)
        {
            Stats eStats = e.GetComponent<Stats>();
            if (eStats != null) eStats.TakeDamage(new DamageInfo { physDamage = 0, isStun = true, stunDuration = 2f });
        }
    }

    private bool SHD_CS_T4_06_NextAttackBuffed = false;
    private void Effect_SHD_CS_T4_06_TriggerBuff() { SHD_CS_T4_06_NextAttackBuffed = true; }
    private void Effect_SHD_CS_T4_06_ApplyHit(Stats target, int step, bool isH, bool isC)
    {
        if (SHD_CS_T4_06_NextAttackBuffed)
        {
            SHD_CS_T4_06_NextAttackBuffed = false;
            float magicDmg = target.maxHp * 0.05f;
            target.TakeDamage(magicDmg);
            stats.Heal(magicDmg * 0.5f);
        }
    }

    // ==========================================
    // BIẾN QUẢN LÝ SHD_CS_T4_07 (GIÁP ĐỔI HƯỚNG)
    // ==========================================
    private Vector3 SHD_CS_T4_07_LastAttackDir = Vector3.zero;
    private Coroutine SHD_CS_T4_07_Coroutine;
    private bool SHD_CS_T4_07_IsActive = false;
    private float SHD_CS_T4_07_ShieldAmount = 100f;
    private void Effect_SHD_CS_T4_07(Stats target, int step, bool isH, bool isC)
    {
        Vector3 currentDir = stats.facingDirection;

        // 1. Đòn đánh đầu tiên: Chưa có hướng trước đó để so sánh -> Chỉ lưu lại hướng rồi bỏ qua
        if (SHD_CS_T4_07_LastAttackDir == Vector3.zero)
        {
            SHD_CS_T4_07_LastAttackDir = currentDir;
            return;
        }

        // 2. Thuật toán quét 8 hướng của bạn (Cực chuẩn!)
        if (Vector3.Angle(SHD_CS_T4_07_LastAttackDir, currentDir) > 30f)
        {
            // Nếu đang đếm ngược 3s rồi mà lại đổi hướng trúng -> Hủy bộ đếm cũ
            if (SHD_CS_T4_07_Coroutine != null)
            {
                StopCoroutine(SHD_CS_T4_07_Coroutine);
            }

            // Khởi động lại vòng lặp 3 giây mới
            SHD_CS_T4_07_Coroutine = StartCoroutine(SHD_CS_T4_07_ShieldRoutine());
        }

        // 3. QUAN TRỌNG: Luôn cập nhật lại hướng cũ bằng hướng vừa đánh xong!
        SHD_CS_T4_07_LastAttackDir = currentDir;
    }

    private IEnumerator SHD_CS_T4_07_ShieldRoutine()
    {
        // KIỂM TRA KHÓA CHỐNG STACK
        if (!SHD_CS_T4_07_IsActive)
        {
            // Nếu chưa có giáp -> Cấp 100 giáp
            stats.currentShield += SHD_CS_T4_07_ShieldAmount;
            SHD_CS_T4_07_IsActive = true;
            Debug.Log("<color=cyan>[T4_07]</color> Kích hoạt 100 Giáp ảo do đổi hướng! (3s)");
        }
        else
        {
            // Nếu CÓ RỒI -> Chạy vào đây.
            // Biến currentShield không bị cộng dồn thêm 100 nữa.
            // Thời gian đã được reset ở hàm trên (Stop/Start Coroutine).
            Debug.Log("<color=cyan>[T4_07]</color> Tiếp tục đổi hướng! Làm mới thời gian 3s.");
        }

        // Đếm ngược 3 giây
        yield return new WaitForSeconds(3f);

        // Hết 3 giây -> Thu hồi số giáp đã cấp
        if (SHD_CS_T4_07_IsActive)
        {
            stats.currentShield -= SHD_CS_T4_07_ShieldAmount;

            // Chốt chặn an toàn: Lỡ quái đánh rách cả giáp này rồi thì không để bị âm nợ
            if (stats.currentShield < 0) stats.currentShield = 0;

            SHD_CS_T4_07_IsActive = false;
            Debug.Log("<color=cyan>[T4_07]</color> Mất 100 Giáp ảo đổi hướng.");
        }

        SHD_CS_T4_07_Coroutine = null;
    }

    // SHD_CS_T4_08: Gánh 50% sát thương cho Companion
    private bool SHD_CS_T4_08_DeathBuffActive = false;
    private void HandleCompanionDamageShare(DamageInfo info)
    {
        if (eqManager == null || eqManager.currentCoreShield == null) return;

        // Chỉ gánh khi đang đeo đúng khiên T4_08 và Companion CHƯA CHẾT
        if (eqManager.currentCoreShield.id.Trim() == "SHD_CS_T4_08" && !companionStats.isDead)
        {
            // Tính toán 50% lượng sát thương
            float sharedPhys = info.physDamage * 0.5f;
            float sharedMagic = info.magicDamage * 0.5f;
            float sharedTrue = info.trueDamage * 0.5f;

            // 1. Trừ bớt 50% damage khỏi đòn đánh đang giáng xuống Companion
            info.physDamage -= sharedPhys;
            info.magicDamage -= sharedMagic;
            info.trueDamage -= sharedTrue;

            // 2. Tạo một đòn đánh ảo giáng xuống đầu Player
            DamageInfo playerDmg = new DamageInfo
            {
                physDamage = sharedPhys,
                magicDamage = sharedMagic,
                trueDamage = sharedTrue, // Vẫn chia sẻ True Damage
                attacker = info.attacker,
                impactLevel = 0 // Tránh việc Player bị giật lùi (knockback) vì gánh dame
            };
            // KHIÊN T5_08: Chuyển 100% sát thương của Companion về cho Player gánh
            if (eqManager.currentCoreShield.id.Trim() == "SHD_CS_T5_08")
            {
                // Bắt Player phải nhận gói sát thương này
                stats.TakeDamage(info);

                // Triệt tiêu sát thương lên Companion (đưa về 0)
                info.physDamage = 0;
                info.magicDamage = 0;
                info.trueDamage = 0;

                Debug.Log("<color=red>[T5_08]</color> Hợp nhất sinh mệnh: Player đã gánh 100% sát thương thay Companion!");
            }

            stats.TakeDamage(playerDmg); // Player tự lãnh thẹo
            Debug.Log($"<color=blue>[T4_08]</color> Player chịu giùm Companion {playerDmg.TotalRawDamage} sát thương!");
        }
        else if (eqManager.currentCoreShield.id.Trim() == "SHD_CS_T5_05")
        {
            ApplyMadnessMark(info.attacker);
        }
    }


    // Core Shield Tier 5
    // ==========================================
    // BIẾN QUẢN LÝ SHD_CS_T5_01 (KHIÊN THÍCH NGHI)
    // ==========================================
    private float t5_01_PhysReceived = 0f;
    private float t5_01_MagicReceived = 0f;
    private float t5_01_Timer = 0f;

    // Lưu lại số liệu đã "vay mượn" để hoàn trả khi hết giờ
    private float t5_01_BonusArmor = 0f;
    private float t5_01_BonusMR = 0f;

    // 1. MÁY QUÉT SÁT THƯƠNG (Chạy khi bị đánh)
    private void Effect_SHD_CS_T5_01(DamageInfo info)
    {
        t5_01_PhysReceived += info.physDamage;
        t5_01_MagicReceived += info.magicDamage;
    }

    // 2. LOGIC TÍNH TOÁN 50% (CỘNG DỒN LIÊN TỤC)
    private void EvaluateT5_01_Adaptation()
    {
        // 1. Nếu 3 giây qua yên bình không bị ai đánh -> Xóa sổ nợ, trả lại chỉ số gốc và kết thúc
        if (t5_01_PhysReceived == 0 && t5_01_MagicReceived == 0)
        {
            ForceTurnOff_T5_01();
            return;
        }

        // 2. Đang trong combat, tính toán lượng xê dịch cho chu kỳ 3 giây NÀY
        float newShiftArmor = 0f;
        float newShiftMR = 0f;

        if (t5_01_PhysReceived > t5_01_MagicReceived)
        {
            // Lấy 50% Kháng Phép HIỆN TẠI (đã bị bòn rút ở các vòng trước)
            float shiftAmount = stats.magicResist * 0.5f;
            newShiftArmor = shiftAmount;
            newShiftMR = -shiftAmount;

            Debug.Log($"<color=orange>[T5_01]</color> Đột biến Vật Lý: +{newShiftArmor} Giáp, {newShiftMR} Kháng Phép");
        }
        else if (t5_01_MagicReceived > t5_01_PhysReceived)
        {
            // Lấy 50% Giáp HIỆN TẠI
            float shiftAmount = stats.armor * 0.5f;
            newShiftMR = shiftAmount;
            newShiftArmor = -shiftAmount;

            Debug.Log($"<color=purple>[T5_01]</color> Đột biến Phép Thuật: +{newShiftMR} Kháng Phép, {newShiftArmor} Giáp");
        }

        // 3. Áp dụng sự thay đổi ngay lập tức
        stats.armor += newShiftArmor;
        stats.magicResist += newShiftMR;
        stats.RecalculateStats();

        // 4. LƯU VÀO SỔ NỢ (Cộng dồn để hàm ForceTurnOff biết đường mà trả lại)
        t5_01_BonusArmor += newShiftArmor;
        t5_01_BonusMR += newShiftMR;

        // 5. Reset máy quét để đo lường cho 3 giây tiếp theo
        t5_01_PhysReceived = 0;
        t5_01_MagicReceived = 0;
    }

    // 3. HÀM DỌN DẸP KHẨN CẤP
    private void ForceTurnOff_T5_01()
    {
        // Nếu không mượn nợ gì thì không cần trả
        if (t5_01_BonusArmor == 0 && t5_01_BonusMR == 0) return;

        // Hoàn trả lại chính xác những gì đã thêm/bớt
        stats.armor -= t5_01_BonusArmor;
        stats.magicResist -= t5_01_BonusMR;

        t5_01_BonusArmor = 0;
        t5_01_BonusMR = 0;

        stats.RecalculateStats();
    }

    private void Effect_SHD_CS_T5_03(DamageInfo info)
    {
        if (stats.currentHp - info.TotalRawDamage <= 0)
        {
            info.physDamage = 0;
            info.magicDamage = 0;
            info.trueDamage = 0;
            stats.currentHp = 1;
            stats.isInvincible = true;
            StartCoroutine(T5_03_Routine());
        }
    }
    private IEnumerator T5_03_Routine()
    {
        for (int i = 0; i < 5; i++)
        {
            Collider[] enemies = Physics.OverlapSphere(transform.position, 5f, player.dangerLayer);
            foreach (var e in enemies)
            {
                Stats eStats = e.GetComponent<Stats>();
                if (eStats != null) { float drain = eStats.maxHp * 0.05f; eStats.TakeDamage(drain); stats.Heal(drain); }
            }
            yield return new WaitForSeconds(1f);
        }
        stats.isInvincible = false;
    }

    // ==========================================
    // SHD_CS_T5_05: ẤN ĐIÊN LOẠN & GIẢM HỒI CHIÊU
    // ==========================================
    // Dùng Dictionary để quản lý từng kẻ địch bị đánh dấu riêng biệt
    private Dictionary<Stats, Coroutine> SHD_CS_T5_05_MarkedEnemies = new Dictionary<Stats, Coroutine>();

    private void ApplyMadnessMark(Stats attacker)
    {
        // Bỏ qua nếu sát thương môi trường (không có attacker), hoặc tự đánh mình
        if (attacker == null || attacker.currentHp <= 0 || attacker == stats) return;

        // Nếu địch đã bị đánh dấu rồi -> Reset thời gian 5s (bằng cách Stop cái cũ)
        if (SHD_CS_T5_05_MarkedEnemies.ContainsKey(attacker))
        {
            if (SHD_CS_T5_05_MarkedEnemies[attacker] != null) StopCoroutine(SHD_CS_T5_05_MarkedEnemies[attacker]);
        }

        // Bắt đầu đếm 5 giây mới
        SHD_CS_T5_05_MarkedEnemies[attacker] = StartCoroutine(MadnessMarkRoutine(attacker));
        // Debug.Log($"<color=purple>[T5_05]</color> {attacker.name} đã bị đánh dấu ĐIÊN LOẠN (5s)!");
    }

    private IEnumerator MadnessMarkRoutine(Stats enemy)
    {
        // Có thể Instantiate một cái VFX tia chớp đỏ trên đầu quái ở đây

        yield return new WaitForSeconds(5f);

        // Hết 5s thì xóa dấu ấn
        if (SHD_CS_T5_05_MarkedEnemies.ContainsKey(enemy))
        {
            SHD_CS_T5_05_MarkedEnemies.Remove(enemy);
        }
    }

    // 1. Khi Player BỊ ĐÁNH -> Kích hoạt gán ấn
    private void Effect_SHD_CS_T5_05_MarkAttacker(DamageInfo info)
    {
        ApplyMadnessMark(info.attacker);
    }

    // 2. Khi Player ĐÁNH TRÚNG ĐỊCH (Chỉ đòn đánh thường mới kích hoạt hàm này)
    private void Effect_SHD_CS_T5_05_ReduceCDHit(Stats target, int step, bool isH, bool isC)
    {
        if (target != null && SHD_CS_T5_05_MarkedEnemies.ContainsKey(target))
        {
            if (skillManager != null)
            {
                // Gọi hàm giảm thời gian hồi chiêu bên SkillManager
                skillManager.ReduceAllCooldowns(0.5f);
                Debug.Log("<color=purple>[SHD_CS_T5_05]</color> Đánh trúng mục tiêu Điên Loạn: Giảm 0.5s CD toàn bộ kỹ năng!");
            }
        }
    }

    // ==========================================
    // SHD_CS_T5_06: TÍCH TỤ HỒI MÁU & NỔ CHUẨN
    // ==========================================
    private bool SHD_CS_T5_06_Accumulating = false;
    private float SHD_CS_T5_06_HealAmount = 0f;
    private Coroutine SHD_CS_T5_06_Coroutine;

    private void Effect_SHD_CS_T5_06()
    {
        // Khóa không cho spam chồng chéo. Đang tích tụ thì kệ nó.
        if (!SHD_CS_T5_06_Accumulating)
        {
            if (SHD_CS_T5_06_Coroutine != null) StopCoroutine(SHD_CS_T5_06_Coroutine);
            SHD_CS_T5_06_Coroutine = StartCoroutine(T5_06_AccumulateRoutine());
        }
    }

    private void HandleOnHealReceived(float amount, float excessAmount)
    {
        // Hễ có cục máu nào bơm vào người là cộng dồn
        if (SHD_CS_T5_06_Accumulating)
        {
            SHD_CS_T5_06_HealAmount += amount;
        }
    }

    private IEnumerator T5_06_AccumulateRoutine()
    {
        SHD_CS_T5_06_Accumulating = true;
        SHD_CS_T5_06_HealAmount = 0f;
        Debug.Log("<color=green>[SHD_CS_T5_06]</color> Dùng chiêu cuối! Bắt đầu tích tụ lượng máu hồi phục trong 5s...");

        // Đợi 5 giây
        yield return new WaitForSeconds(5f);

        // Khóa van lại, không tích nữa
        SHD_CS_T5_06_Accumulating = false;

        // Tính lượng sát thương chuẩn (50% máu đã hồi)
        float finalTrueDamage = SHD_CS_T5_06_HealAmount * 0.5f;

        // CHỐT CHẶN: Chỉ nổ khi có tích tụ máu
        if (finalTrueDamage > 0)
        {
            Debug.Log($"<color=red>[SHD_CS_T5_06]</color> PHÁT NỔ! Gây {finalTrueDamage} Sát Thương Chuẩn.");

            // Bạn có thể Instantiate VFX nổ ở đây
            // Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);

            Collider[] enemies = Physics.OverlapSphere(transform.position, 5f, player.dangerLayer);
            foreach (var e in enemies)
            {
                Stats eStats = e.GetComponent<Stats>();
                if (eStats != null && eStats.currentHp > 0)
                {
                    // TẠO GÓI SÁT THƯƠNG CHUẨN AAA
                    DamageInfo info = new DamageInfo
                    {
                        sourcePosition = transform.position,
                        attacker = stats, // Nguồn sát thương là Player
                        physDamage = 0f,
                        magicDamage = 0f,
                        trueDamage = finalTrueDamage, // ⬅️ BÍ QUYẾT NẰM Ở ĐÂY
                        isCrit = false,
                        impactLevel = 1
                    };

                    eStats.TakeDamage(info);
                }
            }
        }
        else
        {
            Debug.Log("<color=gray>[T5_06]</color> Hết 5s nhưng không hồi được giọt máu nào, xịt ngòi!");
        }

        // Dọn dẹp
        SHD_CS_T5_06_HealAmount = 0f;
        SHD_CS_T5_06_Coroutine = null;
    }

    // ==========================================
    // SHD_CS_T5_07: ĐÁNH THƯỜNG TỐN 1 SIN, X1.2 SÁT THƯƠNG
    // ==========================================
    private void Effect_SHD_CS_T5_07(Stats target, int step, bool isH, bool isC)
    {
        // 1. CHỐT CHẶN: Chỉ áp dụng đòn đánh thường, bỏ qua nếu đang dùng Skill
        if (player != null && player.isUsingSpecialSkill) return;

        // 2. CHỐT CHẶN: Bỏ qua nếu mục tiêu đã chết để tránh lỗi
        if (target == null || target.currentHp <= 0) return;

        // 3. KIỂM TRA VÀ TIÊU HAO TÀI NGUYÊN
        if (stats.currentSin >= 1f)
        {
            // Trừ 1 Sin Charge
            stats.currentSin -= 1f;

            // Tính toán lượng 20% sát thương dôi ra
            WeaponData currentWpn = eqManager != null ? eqManager.currentWeapon : null;
            float t = CombatMath.CalculateDirectionFactor(transform, target);

            // Gọi CombatMath với hệ số phụ (externalMult) = 0.2f để lấy chuẩn 20% sức mạnh gốc
            var extraDmg = CombatMath.CalculateFullDamage(
                stats, target, t, isC, null, currentWpn, 0.2f
            );

            // 4. ĐÓNG GÓI VÀ GIÁNG ĐÒN
            DamageInfo extraInfo = new DamageInfo
            {
                sourcePosition = transform.position,
                attacker = stats,
                physDamage = extraDmg.phys,
                magicDamage = extraDmg.magic,
                trueDamage = extraDmg.trueDmg,
                isCrit = isC, // Vẫn kế thừa tỉ lệ chí mạng của đòn đánh gốc
                impactLevel = 0 // Đòn phụ tàng hình không được gây giật lùi thêm
            };

            target.TakeDamage(extraInfo);

            // Debug để bạn dễ test trong Unity Console
            Debug.Log($"<color=orange>[SHD_CS_T5_07]</color> Tiêu hao 1 Sin Charge! Bồi thêm 20% sát thương ({extraInfo.TotalRawDamage}) vào đòn đánh thường.");
        }
    }

    // ==========================================
    // BIẾN QUẢN LÝ SHD_CS_T5_08 (HỢP NHẤT SINH MỆNH)
    // ==========================================
    private float t5_08_AddedHpToPlayer = 0f;
    private bool t5_08_BuffActive = false;
    private Coroutine t5_08_Coroutine;

    // 2. HÀM CỘNG/TRỪ MAX HP KHI MẶC ĐỒ
    private void Update_SHD_CS_T5_08_HpPool(bool isEquipping)
    {
        if (companionStats == null) return;

        if (isEquipping)
        {
            if (t5_08_AddedHpToPlayer == 0f)
            {
                // Lấy Max HP của Companion cộng vào cho Player
                t5_08_AddedHpToPlayer = companionStats.maxHp;
                stats.flatHp += t5_08_AddedHpToPlayer;
                stats.Heal(t5_08_AddedHpToPlayer); // Hồi đầy phần máu vừa được cộng thêm
                stats.RecalculateStats();
                Debug.Log($"<color=green>[SHD_CS_T5_08]</color> Đã hợp nhất thanh máu! +{t5_08_AddedHpToPlayer} HP từ Companion.");
            }
        }
        else
        {
            if (t5_08_AddedHpToPlayer > 0f)
            {
                // Trả lại Max HP bình thường cho Player
                stats.flatHp -= t5_08_AddedHpToPlayer;
                t5_08_AddedHpToPlayer = 0f;
                stats.RecalculateStats();
                Debug.Log("<color=gray>[T5_08]</color> Đã tách thanh máu.");
            }
        }
    }

    // 3. HÀM BUFF TỐC ĐÁNH KHI DÙNG SKILL
    private void Effect_SHD_CS_T5_08()
    {
        if (t5_08_Coroutine != null) StopCoroutine(t5_08_Coroutine);
        t5_08_Coroutine = StartCoroutine(SHD_CS_T5_08_CompanionBuffRoutine());
    }

    private IEnumerator SHD_CS_T5_08_CompanionBuffRoutine()
    {
        if (companionStats != null && companionStats is AllyStats compAlly)
        {
            if (!t5_08_BuffActive)
            {
                compAlly.bonusAttackSpeed += 0.3f;
                t5_08_BuffActive = true;
                compAlly.RecalculateStats();
                Debug.Log("<color=cyan>[SHD_CS_T5_08]</color> Companion phẫn nộ: +30% Tốc đánh (3s)!");
            }

            yield return new WaitForSeconds(3f);

            if (t5_08_BuffActive)
            {
                compAlly.bonusAttackSpeed -= 0.3f;
                t5_08_BuffActive = false;
                compAlly.RecalculateStats();
                Debug.Log("<color=gray>[SHD_CS_T5_08]</color> Hết thời gian buff tốc đánh Companion.");
            }
        }
        t5_08_Coroutine = null;
    }
    //private IEnumerator TempMoveSpeedBuff(float duration, float amount)
    //{
    //    stats.bonusMoveSpeed += amount;
    //    stats.RecalculateStats();
    //    yield return new WaitForSeconds(duration);
    //    stats.bonusMoveSpeed -= amount;
    //    stats.RecalculateStats();
    //}
}