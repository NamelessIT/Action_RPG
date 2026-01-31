using UnityEngine;

// Lớp cha trừu tượng (Abstract): Không dùng trực tiếp, chỉ để kế thừa
public abstract class SkillBehavior : MonoBehaviour
{
    protected AllyStats stats;
    protected SkillData data;
    protected PlayerController player; // Nếu cần điều khiển nhân vật

    // Hàm khởi tạo: Chạy ngay khi Skill được trang bị
    public virtual void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        this.stats = myStats;
        this.data = myData;
        this.player = myPlayer;

        OnEquip();
    }

    // Hàm hủy: Chạy khi tháo Skill
    public virtual void Terminate()
    {
        OnUnequip();
        Destroy(this); // Tự hủy component này khỏi GameObject
    }

    // Các hàm con sẽ ghi đè (override) lại logic riêng
    protected abstract void OnEquip();
    protected abstract void OnUnequip();
}