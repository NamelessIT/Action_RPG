using UnityEngine;
using System;

public static class SkillFactory
{
    // Hàm này trả về KIỂU (Type) của script dựa trên Enum
    public static Type GetPassiveComponentType(SkillData.PassiveEffectCode code)
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
            case SkillData.PassiveEffectCode.Duelist:
                return typeof(DuelistPassive);
            case SkillData.PassiveEffectCode.Mage:
                return typeof(MagePassive);
            // ... Thêm các case khác vào đây ...

            default:
                return null;
        }
    }
    public static Type GetSkillComponentType(SkillData.SkillEffectCode code)
    {
        switch (code)
        {
            case SkillData.SkillEffectCode.ChrisSkill:
                return typeof(ChrisSkill);
            case SkillData.SkillEffectCode.LeoSkill:
                return typeof(LeoSkill);
            case SkillData.SkillEffectCode.WarriorSkill:
                return typeof(WarriorSkill);
            case SkillData.SkillEffectCode.BloodReaverSkill:
                return typeof(BloodReaverSkill);
            case SkillData.SkillEffectCode.RougeSkill:
                return typeof(RougeSkill);
            case SkillData.SkillEffectCode.PaladinSkill:
                return typeof(PaladinSkill);
            case SkillData.SkillEffectCode.JuggernautSkill:
                return typeof(JuggernautSkill);
            case SkillData.SkillEffectCode.DarkInquisitorSkill:
                return typeof(DarkInquisitorSkill);
            case SkillData.SkillEffectCode.RavagerSkill:
                return typeof(RavagerSkill);
            case SkillData.SkillEffectCode.WarlockSkill:
                return typeof(WarlockSkill);
            // ... Thêm các case khác vào đây ...


            default:
                return null;
        }
    }
    public static Type GetSignatureComponentType(SkillData.SignatureEffectCode code)
    {
        switch (code)
        {
            case SkillData.SignatureEffectCode.ChrisSignature:
                return typeof(ChrisSignature);
            case SkillData.SignatureEffectCode.BloodReaverLiteSignature:
                return typeof(BloodReaverLiteSignature);
            // ... Thêm các case khác vào đây ...


            default:
                return null;
        }
    }
}