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
            case SkillData.PassiveEffectCode.Rogue:
                return typeof(RoguePassive);
            case SkillData.PassiveEffectCode.Duelist:
                return typeof(DuelistPassive);
            case SkillData.PassiveEffectCode.Mage:
                return typeof(MagePassive);
            case SkillData.PassiveEffectCode.Catalyst:
                return typeof(CatalystPassive);
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
            case SkillData.SkillEffectCode.VanguardSkill:
                return typeof(VanguardSkill);
            case SkillData.SkillEffectCode.WarriorSkill:
                return typeof(WarriorSkill);
            case SkillData.SkillEffectCode.BattleMageSkill:
                return typeof(BattleMageSkill);
            case SkillData.SkillEffectCode.BloodReaverSkill:
                return typeof(BloodReaverSkill);
            case SkillData.SkillEffectCode.RogueSkill:
                return typeof(RogueSkill);
            case SkillData.SkillEffectCode.DuelistSkill:
                return typeof(DuelistSkill);
            case SkillData.SkillEffectCode.MageSkill:
                return typeof(MageSkill);
            case SkillData.SkillEffectCode.CatalystSkill:
                return typeof(CatalystSkill);
            case SkillData.SkillEffectCode.WardenSkill:
                return typeof(WardenSkill);
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
            case SkillData.SkillEffectCode.SwordMasterSkill:
                return typeof(SwordMasterSkill);
            case SkillData.SkillEffectCode.TricksterSkill:
                return typeof(TricksterSkill);
            case SkillData.SkillEffectCode.InfiltratorSkill:
                return typeof(InfiltratorSkill);
            case SkillData.SkillEffectCode.SpellbladeSkill:
                return typeof(SpellbladeSkill);
            case SkillData.SkillEffectCode.TacticianSkill:
                return typeof(TacticianSkill);
            case SkillData.SkillEffectCode.SpellbinderSkill:
                return typeof(SpellbinderSkill);
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
            case SkillData.SignatureEffectCode.LeoSignature:
                return typeof(LeoSignature);
            case SkillData.SignatureEffectCode.VanguardLiteSignature:
                return typeof(VanguardLiteSignature);
            case SkillData.SignatureEffectCode.VanguardSignature:
                return typeof(VanguardSignature);
            case SkillData.SignatureEffectCode.WarriorLiteSignature:
                return typeof(WarriorLiteSignature);
            case SkillData.SignatureEffectCode.WarriorSignature:
                return typeof(WarriorSignature);
            case SkillData.SignatureEffectCode.BattleMageLiteSignature:
                return typeof(BattleMageLiteSignature);
            case SkillData.SignatureEffectCode.BattleMageSignature:
                return typeof(BattleMageSignature);
            case SkillData.SignatureEffectCode.BloodReaverLiteSignature:
                return typeof(BloodReaverLiteSignature);
            case SkillData.SignatureEffectCode.BloodReaverSignature:
                return typeof(BloodReaverSignature);
            case SkillData.SignatureEffectCode.RogueLiteSignature:
                return typeof(RogueLiteSignature);
            case SkillData.SignatureEffectCode.RogueSignature:
                return typeof(RogueSignature);
            case SkillData.SignatureEffectCode.DuelistLiteSignature:
                return typeof(DuelistLiteSignature);
            case SkillData.SignatureEffectCode.DuelistSignature:
                return typeof(DuelistSignature);
            case SkillData.SignatureEffectCode.MageLiteSignature:
                return typeof(MageLiteSignature);
            case SkillData.SignatureEffectCode.MageSignature:
                return typeof(MageSignature);
            case SkillData.SignatureEffectCode.CatalystLiteSignature:
                return typeof(CatalystLiteSignature);
            case SkillData.SignatureEffectCode.CatalystSignature:
                return typeof(CatalystSignature);

            default:
                return null;
        }
    }
}