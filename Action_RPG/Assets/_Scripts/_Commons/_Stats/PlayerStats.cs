using UnityEngine;

public class PlayerStats : AllyStats
{
    public override void Start()
    {
        base.Start();
        this.tag = "Player";

        // Gọi base.Start() của AllyStats 
        // -> Nó sẽ chạy RecalculateStats() và InitializeClassStats()
        base.Start();

        // Debug để kiểm tra xem HP đã được tính chưa
    }

    // Không cần override Update nếu chỉ để gọi base.Update() (Tối ưu hiệu năng)
    public override void Update()
    {
        base.Update();
    }

    public override void TakeDamage(DamageInfo info)
    {
        // --- LOGIC DUELIST PARRY ---
        // Chỉ xử lý nếu đang bật thế thủ (isParrying)
        if (isParrying)
        {
            // Lưu lại SuperArmor hiện tại để restore sau
            bool oldSuperArmor = this.isSuperArmor;
            int oldArmorLevel = this.superArmorLevel;

            // Bật SuperArmor cấp cao nhất để không bị ngắt động tác khi đang Parry
            this.isSuperArmor = true;
            this.superArmorLevel = 99;

            // Tìm component Duelist để báo cáo kết quả
            DuelistPassive duelist = GetComponent<DuelistPassive>();

            if (isPerfectParryWindow)
            {
                // --- PERFECT PARRY ---
                Debug.Log("<color=yellow>>> PERFECT PARRY! (0 Damage)</color>");

                info.damageAmount = 0; // Không nhận damage

                // Báo cho Skill biết để Stun địch và Buff Crit
                if (duelist != null) duelist.OnParrySuccess(true, info.attacker);

                // Trả lại trạng thái cũ và thoát luôn
                this.isSuperArmor = oldSuperArmor;
                this.superArmorLevel = oldArmorLevel;
                return;
            }
            else
            {
                // --- NORMAL PARRY ---
                Debug.Log("<color=white>>> Normal Parry (Giảm 80%)</color>");

                info.damageAmount *= 0.2f; // Chỉ nhận 20% damage

                // Báo normal parry (không stun)
                if (duelist != null) duelist.OnParrySuccess(false, info.attacker);
            }

            // Trả lại trạng thái cũ
            this.isSuperArmor = oldSuperArmor;
            this.superArmorLevel = oldArmorLevel;
        }

        // --- GỌI LOGIC TRỪ MÁU GỐC ---
        // (Bao gồm trừ máu, check chết, rung chuông OnTakeDamage...)
        base.TakeDamage(info);
    }
}