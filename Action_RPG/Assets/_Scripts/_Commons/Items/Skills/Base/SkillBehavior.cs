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
        float finalCooldown = Mathf.Max(0f, data.cooldown * (1f - stats.cooldownReduction) - flatReduction);

        if (Time.time < lastUseTime + finalCooldown)
        {
            Debug.Log($"Skill đang hồi! ({finalCooldown - (Time.time - lastUseTime):F1}s)");
            return false;
        }

        // 2. Kiểm tra Sin Charge (Năng lượng)
        if (stats.currentSin < data.sinChargeReq)
        {
            Debug.Log("Không đủ Sin Charge!");
            return false;
        }

        // 3. Trừ Sin Charge
        stats.currentSin -= data.sinChargeReq;

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
}