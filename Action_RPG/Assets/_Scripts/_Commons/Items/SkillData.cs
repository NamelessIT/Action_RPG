using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Skill", menuName = "Inventory/Skill Data")]
public class SkillData : ScriptableObject
{
    public enum SkillType
    {
        DefaultPassive, Passive, Skill, Signature, Enemy
    }
    // Định danh logic đặc biệt (Để SkillManager biết phải code gì cho skill này)
    public enum PassiveEffectCode
    {
        None, //Skill hoặc signature
        Chris, // Cơ bản ChrisDon
        Leo, // Cơ bản Leo
        Vanguard,  // Vanguard
        Warrior,  // Warrior
        BattleMage,  // Battle-Mage
        BloodReaver,  // Blood Reaver
        Rouge,
        Duelist,
        Mage,
        Hermit,
    }
    public enum SkillEffectCode
    {
        None, //Passive hoặc Signature
        ChrisSkill,
        LeoSkill,
        VanguardSkill,
        WarriorSkill,
        BattleMageSkill,
        BloodReaverSkill,
        RougeSkill,
        DuelistSkill,
        MageSkill,
    }
    public string id;
    public string skillName;
    public Sprite icon;
    [TextArea] public string skilDesc;
    public SkillType skillType;
    public float skillPhysicalMultiplier;
    public float skillMagicMultiplier;
    public float cooldown;
    public float sinChargeReq;
    [Header("Special Logic ID")]
    public PassiveEffectCode passiveEffectCode; // <-- Quan trọng: Chọn logic ở đây
    public SkillEffectCode skillEffectCode;

    [Header("Passive Stat Modifiers")]
    // Dùng để cộng chỉ số tĩnh (VD: +10% HP, +Crit) mà không cần viết code
    public List<StatModifier> passiveStats = new List<StatModifier>();

    // Hàm này chạy ngay lập tức khi bạn chỉnh sửa trong Inspector
    private void OnValidate()
    {
        if (passiveStats != null)
        {
            foreach (var sub in passiveStats)
            {
                // Nếu thấy powerMod bằng 0 (do Unity tạo mặc định), tự sửa thành 1
                // Lưu ý: Dùng 0f để so sánh float chuẩn xác
                if (sub.powerMod == 0f)
                {
                    sub.powerMod = 1.0f;
                }
            }
        }
    }
    //public string skillEffectConfig; để sau

}
