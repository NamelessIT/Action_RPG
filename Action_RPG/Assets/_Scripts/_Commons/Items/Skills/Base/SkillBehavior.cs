using UnityEngine;

public abstract class SkillBehavior : MonoBehaviour
{
    protected AllyStats stats;
    protected SkillData data;
    protected PlayerController player;

    // Đổi chữ 'protected' thành 'public'
    public float lastUseTime = -100f;

    public virtual void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        this.stats = myStats;
        this.data = myData;
        this.player = myPlayer;
        OnEquip();
    }

    public virtual void Terminate()
    {
        OnUnequip();
        Destroy(this);
    }

    protected abstract void OnEquip();
    protected abstract void OnUnequip();

    // --- [MỚI] HÀM KÍCH HOẠT KỸ NĂNG (Dành cho Active Skill) ---
    // Trả về true nếu dùng thành công (để trừ năng lượng/bắt đầu cooldown)
    public virtual bool Use()
    {
        // 1. Kiểm tra Cooldown
        // Giảm trừ thời gian hồi chiêu dựa trên bonusCdr (Giả sử 0.2 = giảm 20%)
        float flatReduction = (stats is AllyStats a) ? a.flatSkillCooldownReduction : 0f;
        // ACC_CH_T4_03: giảm thêm % cooldown skill.
        float finalCooldown = Mathf.Max(0f, data.cooldown * (1f - stats.cooldownReduction - stats.accSkillCdrBonus) - flatReduction);

        if (Time.time < lastUseTime + finalCooldown)
        {
            Debug.Log($"Skill đang hồi! ({finalCooldown - (Time.time - lastUseTime):F1}s)");
            return false;
        }

        // 2. Kiểm tra Sin Charge (Năng lượng). ACC_CH_T4_03 giảm phí mọi skill; weapon/acc khác giảm/tăng phí Signature.
        float sinCost = data.sinChargeReq * stats.accSkillSinCostMult;
        if (data.skillType == SkillData.SkillType.Signature) sinCost *= stats.signatureSinCostMult * stats.accSignatureSinCostMult;

        // 3. Trừ Sin Charge — ACC_CH_T5_02/MS_T4_06: nếu thiếu Sin (chỉ Signature), trả phần thiếu bằng Máu.
        float sinPaid;
        bool bloodPaid = false;
        if (stats.currentSin >= sinCost)
        {
            stats.currentSin -= sinCost;
            sinPaid = sinCost;
        }
        else
        {
            float hpPerSin = (data.skillType == SkillData.SkillType.Signature) ? stats.accHpPerSinOverride : 0f;
            if (hpPerSin <= 0f)
            {
                Debug.Log("Không đủ Sin Charge!");
                return false;
            }
            float missing = sinCost - stats.currentSin;
            float hpCost = missing * hpPerSin;
            if (stats.currentHp - hpCost < 1f) // GDD: tiêu máu không thể giết (giữ tối thiểu 1 HP)
            {
                Debug.Log("Không đủ máu để dùng Signature bằng máu!");
                return false;
            }
            stats.currentHp -= hpCost;
            sinPaid = stats.currentSin;
            stats.currentSin = 0f;
            bloodPaid = true;
            Debug.Log($"<color=red>[ACC]</color> Dùng Signature bằng Máu: -{hpCost:F0} HP (thiếu {missing:F0} Sin).");
        }

        // ACC_MS_T5_03 (+dmg mọi Signature) & ACC_MS_T4_06 (+dmg khi dùng máu): cộng tạm signatureDamageBonus cho đòn này.
        if (data.skillType == SkillData.SkillType.Signature)
        {
            stats.lastSignatureBloodPaid = bloodPaid;
            float sigBonus = stats.accSignatureDmgBonusAlways + (bloodPaid ? stats.accBloodSignatureDmgBonus : 0f);
            if (sigBonus > 0f) StartCoroutine(BloodSignatureDmgRoutine(sigBonus, 3f));
        }

        // Báo sự kiện Sin tiêu hao (cho WPN_ST_T3_02 hồi máu theo Sin...) — dùng lượng Sin thực tế đã tiêu.
        stats.NotifySinConsumed(sinPaid);

        // 4. Cập nhật thời gian dùng
        lastUseTime = Time.time;

        return true;
        // Lớp con (S_Fireball.cs) sẽ override hàm này, gọi base.Use(), nếu true thì bắn cầu lửa
    }

    // [MỚI] HÀM GIẢM THỜI GIAN HỒI CHIÊU
    public virtual void ReduceCooldown(float amount)
    {
        // Kỹ thuật "Đánh lừa thời gian": 
        // Đẩy mốc thời gian dùng chiêu cuối cùng lùi về quá khứ.
        // Điều này sẽ làm cho điều kiện (Time.time < lastUseTime + cooldown) kết thúc sớm hơn.

        lastUseTime -= amount;

        // Debug để bạn dễ dàng theo dõi trên Console
        // Debug.Log($"Đã giảm {amount}s hồi chiêu cho {data.skillName}");
    }

    // [MỚI] Giảm hồi chiêu theo TỈ LỆ cooldown gốc của chính skill này (vd WPN_GR_T3_03: frac=0.5 → giảm 50%).
    public void ReduceCooldownByFraction(float frac)
    {
        if (data != null) lastUseTime -= data.cooldown * frac;
    }

    // [ACC_MS_T4_06] Cộng tạm signatureDamageBonus cho Signature dùng máu (áp ngay trước khi effect chạy, gỡ sau dur).
    private System.Collections.IEnumerator BloodSignatureDmgRoutine(float bonus, float dur)
    {
        stats.signatureDamageBonus += bonus;
        yield return new WaitForSeconds(dur);
        stats.signatureDamageBonus -= bonus;
    }
}