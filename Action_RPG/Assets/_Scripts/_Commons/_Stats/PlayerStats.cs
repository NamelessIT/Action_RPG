using UnityEngine;

public class PlayerStats : AllyStats
{
    [Header("--- Skill Point---")]
    public int skillPointRemain;
    public override void Start()
    {
        base.Start();
        // [MỚI] Khởi tạo điểm Skill ban đầu
        if (skillPointRemain == 0) skillPointRemain = level;
        this.tag = "Player";

        // Debug để kiểm tra xem HP đã được tính chưa
    }
    // [MỚI] Ghi đè hàm thăng cấp của AllyStats
    protected override void LevelUp()
    {
        base.LevelUp(); // Sẽ gọi hàm LevelUp của AllyStats -> Nhận 5 Attribute + Hồi máu

        skillPointRemain += 1; // Cấp 1 điểm kỹ năng
        Debug.Log($"<color=green>[PlayerStats]</color> {gameObject.name} nhận 1 Skill Point (Tổng: {skillPointRemain})");
    }

    // Không cần override Update nếu chỉ để gọi base.Update() (Tối ưu hiệu năng)
    public override void Update()
    {
        base.Update();
    }

    public override void TakeDamage(DamageInfo info)
    {
        // --- LOGIC DUELIST PARRY ---
        if (isParrying)
        {
            // [MỚI] CHECK GÓC PARRY (ANGLE CHECK)
            // 1. Xác định hướng đòn đánh tới (Vector từ mình tới kẻ địch)
            // Lưu ý: info.sourcePosition là vị trí kẻ địch
            Vector3 dirToAttacker = (info.sourcePosition - transform.position).normalized;

            // 2. Xác định hướng mặt của mình
            Vector3 myFacingDir = facingDirection != Vector3.zero ? facingDirection : transform.forward;

            // 3. Tính góc lệch
            float angle = Vector3.Angle(myFacingDir, dirToAttacker);

            // 4. Nếu góc lệch nằm trong phạm vi cho phép (Parry thành công)
            if (angle <= parryAngle / 2f)
            {
                // ... (Logic Parry cũ của bạn: Perfect/Normal, SuperArmor...) ...

                bool oldSuperArmor = this.isSuperArmor;
                int oldArmorLevel = this.superArmorLevel;
                this.isSuperArmor = true;
                this.superArmorLevel = 99;

                DuelistPassive duelist = GetComponent<DuelistPassive>();

                if (isPerfectParryWindow)
                {
                    Debug.Log("<color=yellow>>> PERFECT PARRY! (0 Damage)</color>");
                    info.damageAmount = 0;
                    if (duelist != null) duelist.OnParrySuccess(true, info.attacker);
                }
                else
                {
                    Debug.Log("<color=white>>> Normal Parry (Giảm 80%)</color>");
                    info.damageAmount *= 0.2f;
                    if (duelist != null) duelist.OnParrySuccess(false, info.attacker);
                }
                // [FIX LỖI] Gọi base.TakeDamage Ở ĐÂY khi Super Armor đang bật
                base.TakeDamage(info);  
                this.isSuperArmor = oldSuperArmor;
                this.superArmorLevel = oldArmorLevel;
                return; // Thoát luôn
            }
            else
            {
                // [MỚI] PARRY THẤT BẠI DO SAI HƯỚNG
                Debug.Log($"<color=red>>> Parry Failed! Wrong Direction (Angle: {angle})</color>");
                // Có thể thêm hình phạt: Nhận thêm damage (Backstab) hoặc chỉ nhận damage thường
                // Ở đây ta cứ để nó chạy xuống base.TakeDamage để nhận damage thường
            }
        }

        // --- GỌI LOGIC TRỪ MÁU GỐC ---
        base.TakeDamage(info);
    }
}