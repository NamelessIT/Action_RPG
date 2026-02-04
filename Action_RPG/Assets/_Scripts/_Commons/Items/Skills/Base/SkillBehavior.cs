using UnityEngine;

public abstract class SkillBehavior : MonoBehaviour
{
    protected AllyStats stats;
    protected SkillData data;
    protected PlayerController player;

    // Biến đếm Cooldown (Hồi chiêu) nội bộ của script
    protected float lastUseTime = -100f;

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
        if (Time.time < lastUseTime + data.cooldown)
        {
            Debug.Log($"Skill {data.skillName} đang hồi! ({data.cooldown - (Time.time - lastUseTime):F1}s)");
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
}