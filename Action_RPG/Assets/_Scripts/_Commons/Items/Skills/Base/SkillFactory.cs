using UnityEngine;
using System;

public static class SkillFactory
{
    // Hàm này trả về KIỂU (Type) của script dựa trên Enum
    public static Type GetSkillComponentType(SkillData.PassiveEffectCode code)
    {
        switch (code)
        {
            case SkillData.PassiveEffectCode.Chris:
                return typeof(ChrisPassive);
            case SkillData.PassiveEffectCode.Leo:
                return typeof(LeoPassive);
            case SkillData.PassiveEffectCode.Vanguard:
                return typeof(VanguardPassive);
            case SkillData.PassiveEffectCode.Warrior:
                return typeof(WarriorPassive);
            case SkillData.PassiveEffectCode.BattleMage:
                return typeof(BattleMagePassive);
            case SkillData.PassiveEffectCode.BloodReaver:
                return typeof(BloodReaverPassive);
            case SkillData.PassiveEffectCode.Rouge:
                return typeof(RougePassive);
            // ... Thêm các case khác vào đây ...

            default:
                return null;
        }
    }
}