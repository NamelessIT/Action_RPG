using System;
using UnityEngine;

public class PlayerStats : AllyStats
{
    private static PlayerStats _current;

    /// <summary>
    /// PlayerStats duy nhất trong scene. Thay cho FindFirstObjectByType&lt;PlayerStats&gt;() rải rác:
    /// lần đầu quét scene, các lần sau lấy cache. Player bị huỷ thì cache tự rỗng nên quét lại.
    /// </summary>
    public static PlayerStats Current
    {
        get
        {
            if (_current == null) _current = FindFirstObjectByType<PlayerStats>();
            return _current;
        }
    }

    [Header("--- Skill Point---")]
    public int skillPointRemain;

    public Action OnPerfectParryTriggered;
    public override void Start()
    {
        // Đọc nhân vật đã chọn từ CharacterSelect (nếu có), ghi đè trước khi base.Start() gọi InitializeClassStats
        string savedChar = PlayerPrefs.GetString("SelectedCharacter", "");
        if (!string.IsNullOrEmpty(savedChar))
            characterId = savedChar;

        base.Start();
        if (skillPointRemain == 0) skillPointRemain = level;
        this.tag = "Player";
    }
    // [MỚI] Ghi đè hàm thăng cấp của AllyStats
    protected override void LevelUp()
    {
        base.LevelUp(); // Sẽ gọi hàm LevelUp của AllyStats -> Nhận 5 Attribute + Hồi máu

        skillPointRemain += 1; // Cấp 1 điểm kỹ năng
        // Lưu SP per-character để SkillTreeRuntime khôi phục đúng khi load
        PlayerPrefs.SetInt($"SP_{characterId}", skillPointRemain);
        PlayerPrefs.Save();
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
                    info.physDamage = 0;
                    info.magicDamage = 0;
                    info.trueDamage = 0;
                    if (duelist != null) duelist.OnParrySuccess(true, info.attacker);
                    OnPerfectParryTriggered?.Invoke();
                }
                else
                {
                    Debug.Log("<color=white>>> Normal Parry (Giảm 80%)</color>");
                    info.physDamage *= 0.2f;
                    info.magicDamage *= 0.2f;
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
    protected override void InitializeClassStats()
    {
        if (characterId == "Chris")
        {
            characterName  = "Chris Don";
            isSuperArmor   = true;
            superArmorLevel = 0;
        }
        else if (characterId == "Leo")
        {
            characterName  = "Leonard II";
            isSuperArmor   = false;
        }
    }
}