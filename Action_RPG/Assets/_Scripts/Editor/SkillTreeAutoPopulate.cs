using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class SkillTreeAutoPopulate
{
    [MenuItem("Tools/Skill Tree/Auto-Populate All Class Trees")]
    public static void PopulateAllClassTrees()
    {
        int total = 0;
        total += Populate("VanguardSkillTree",    BuildVanguard());
        total += Populate("WarriorSkillTree",     BuildWarrior());
        total += Populate("BattleMageSkillTree",  BuildBattleMage());
        total += Populate("BloodReaverSkillTree", BuildBloodReaver());
        total += Populate("RogueSkillTree",       BuildRogue());
        total += Populate("DuelistSkillTree",     BuildDuelist());
        total += Populate("MageSkillTree",        BuildMage());
        total += Populate("CatalystSkillTree",    BuildCatalyst());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SkillTreeAutoPopulate] Hoàn thành! Đã populate {total} node trên 8 class trees.");
        EditorUtility.DisplayDialog("Skill Tree Auto-Populate", $"Đã populate {total} node thành công!\nNhớ gán SkillData cho các Skill node trong Inspector.", "OK");
    }

    // ── Core helpers ─────────────────────────────────────────────────────────

    static int Populate(string assetName, List<SkillNodeData> nodes)
    {
        string path = $"Assets/Resources/Datas/SkillTree/{assetName}.asset";
        var tree = AssetDatabase.LoadAssetAtPath<ClassSkillTreeData>(path);
        if (tree == null)
        {
            Debug.LogError($"[SkillTreeAutoPopulate] Không tìm thấy asset: {path}");
            return 0;
        }
        tree.skillNodes = nodes;
        EditorUtility.SetDirty(tree);
        Debug.Log($"[SkillTreeAutoPopulate] {assetName}: {nodes.Count} nodes OK");
        return nodes.Count;
    }

    static SkillNodeData Stat(string id, string name, string desc, int lvl,
                              List<string> pre, List<StatGainEntry> gains, int idx)
        => new SkillNodeData
        {
            nodeId               = id,
            nodeName             = name,
            nodeDescription      = desc,
            nodeType             = SkillNodeType.Stat,
            skillData            = null,
            statGains            = gains,
            classGrantType       = ClassGrantType.None,
            classGrantName       = "",
            unlockCost           = 1,
            requiredLevel        = lvl,
            prerequisiteNodeIds  = pre ?? new List<string>(),
            uiPosition           = new Vector2(0, -idx * 150f)
        };

    static SkillNodeData Skill(string id, string name, string desc, int lvl,
                               List<string> pre, int idx,
                               ClassGrantType grant = ClassGrantType.None, string grantName = "")
        => new SkillNodeData
        {
            nodeId               = id,
            nodeName             = name,
            nodeDescription      = desc,
            nodeType             = SkillNodeType.Skill,
            skillData            = null,
            statGains            = new List<StatGainEntry>(),
            classGrantType       = grant,
            classGrantName       = grantName,
            unlockCost           = 1,
            requiredLevel        = lvl,
            prerequisiteNodeIds  = pre ?? new List<string>(),
            uiPosition           = new Vector2(0, -idx * 150f)
        };

    static SkillNodeData Core(string id, string name, string desc, int lvl,
                              List<string> pre, int idx)
        => new SkillNodeData
        {
            nodeId               = id,
            nodeName             = name,
            nodeDescription      = desc,
            nodeType             = SkillNodeType.CoreMechanic,
            skillData            = null,
            statGains            = new List<StatGainEntry>(),
            classGrantType       = ClassGrantType.None,
            classGrantName       = "",
            unlockCost           = 1,
            requiredLevel        = lvl,
            prerequisiteNodeIds  = pre ?? new List<string>(),
            uiPosition           = new Vector2(0, -idx * 150f)
        };

    static StatGainEntry G(StatFieldType t, float v) => new StatGainEntry { statType = t, value = v };
    static List<string>       P(params string[] ids) => new List<string>(ids);
    static List<StatGainEntry> Gs(params StatGainEntry[] gs) => new List<StatGainEntry>(gs);

    // ═════════════════════════════════════════════════════════════════════════
    //  CHRIS — VANGUARD
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildVanguard()
    {
        const string C  = "Vanguard";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Chris_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","  +2 STR","  +2 baseSTR",T2,P(t1),          Gs(G(StatFieldType.BaseSTR,2)),0));
        n.Add(Stat (C+"_T2_N2","  +4 VIT","  +4 baseVIT",T2,P(C+"_T2_N1"),  Gs(G(StatFieldType.BaseVIT,4)),1));
        n.Add(Stat (C+"_T2_N3","  +4 STR","  +4 baseSTR",T2,P(C+"_T2_N2"),  Gs(G(StatFieldType.BaseSTR,4)),2));
        n.Add(Stat (C+"_T2_N4","  +6 VIT","  +6 baseVIT",T2,P(C+"_T2_N3"),  Gs(G(StatFieldType.BaseVIT,6)),3));
        n.Add(Skill(C+"_T2_N5","VanguardLiteSignature","Học VanguardLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","VanguardPassive","Học VanguardPassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"Vanguard"));
        n.Add(Stat (C+"_T3_N2","+4 STR / +8 Armor / +4 MR","+4 baseSTR, +8 Armor, +4 MagicResist",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseSTR,4),G(StatFieldType.Armor,8),G(StatFieldType.MagicResist,4)),6));
        n.Add(Skill(C+"_T3_N3","VanguardSkill","Học VanguardSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+4 DefenseValue","+4 DefenseValue",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.DefenseValue,4)),8));
        n.Add(Stat (C+"_T3_N5","+10 VIT / +3% Resistance / +2% HpGain","+10 baseVIT, +3% resistanceEffect, +2% bonusHpGain",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseVIT,10),G(StatFieldType.ResistanceEffect,0.03f),G(StatFieldType.BonusHpGain,0.02f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","Block: -10% Stamina","Core: Giảm 10% stamina tiêu hao khi Block",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+6 VIT / +3 STR","+6 baseVIT, +3 baseSTR",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseVIT,6),G(StatFieldType.BaseSTR,3)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","DefValue Buff +20%","Core: Kỹ năng tăng 20% DefenseValue khi kích hoạt",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+12 Armor / +6 MR","+12 Armor, +6 MagicResist",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.Armor,12),G(StatFieldType.MagicResist,6)),14));
        n.Add(Core (C+"_T4_N6","Block Speed +20%","Core: Tăng 20% tốc độ giơ khiên/Block",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+6 VIT / +3 STR","+6 baseVIT, +3 baseSTR",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseVIT,6),G(StatFieldType.BaseSTR,3)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+6 DefenseValue","+6 DefenseValue",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.DefenseValue,6)),19));
        n.Add(Core (C+"_T4_N11","Block: No MS Penalty","Core: Không bị giảm 50% tốc độ di chuyển khi Block",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+8 VIT / +4 STR","+8 baseVIT, +4 baseSTR",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseVIT,8),G(StatFieldType.BaseSTR,4)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Armor/MR Scale +20%","Core: Kỹ năng scale thêm 20% Armor và MagicResist",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+4.5% Resistance / +3% HpGain","+4.5% resistanceEffect, +3% bonusHpGain",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.ResistanceEffect,0.045f),G(StatFieldType.BonusHpGain,0.03f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","VanguardSignature","Học VanguardSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+20 VIT / +10 STR","+20 baseVIT, +10 baseSTR",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseVIT,20),G(StatFieldType.BaseSTR,10)),26));
        n.Add(Stat (C+"_T5_N3","+20 Armor / +10 MR / +10 DefVal","+20 Armor, +10 MagicResist, +10 DefenseValue",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.Armor,20),G(StatFieldType.MagicResist,10),G(StatFieldType.DefenseValue,10)),27));
        n.Add(Stat (C+"_T5_N4","+20 VIT / +10 STR","+20 baseVIT, +10 baseSTR",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseVIT,20),G(StatFieldType.BaseSTR,10)),28));
        n.Add(Stat (C+"_T5_N5","+7.5% Resistance / +5% HpGain","+7.5% resistanceEffect, +5% bonusHpGain",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.ResistanceEffect,0.075f),G(StatFieldType.BonusHpGain,0.05f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CHRIS — WARRIOR
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildWarrior()
    {
        const string C  = "Warrior";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Chris_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","+2 VIT","+2 baseVIT",T2,P(t1),         Gs(G(StatFieldType.BaseVIT,2)),0));
        n.Add(Stat (C+"_T2_N2","+4 STR","+4 baseSTR",T2,P(C+"_T2_N1"), Gs(G(StatFieldType.BaseSTR,4)),1));
        n.Add(Stat (C+"_T2_N3","+4 VIT","+4 baseVIT",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseVIT,4)),2));
        n.Add(Stat (C+"_T2_N4","+6 STR","+6 baseSTR",T2,P(C+"_T2_N3"), Gs(G(StatFieldType.BaseSTR,6)),3));
        n.Add(Skill(C+"_T2_N5","WarriorLiteSignature","Học WarriorLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","WarriorPassive","Học WarriorPassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"Warrior"));
        n.Add(Stat (C+"_T3_N2","+4 VIT / +4% StaminaRed","+4 baseVIT, +4% StaminaReduction",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseVIT,4),G(StatFieldType.StaminaReduction,0.04f)),6));
        n.Add(Skill(C+"_T3_N3","WarriorSkill","Học WarriorSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+4% CritChance","+4% bonusCritChance",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.BonusCritChance,0.04f)),8));
        n.Add(Stat (C+"_T3_N5","+10 STR / +4% DefValIgnore","+10 baseSTR, +4% defenseValueIgnore",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseSTR,10),G(StatFieldType.DefenseValueIgnore,0.04f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","Heavy Attack -20% Time","Core: Giảm 20% thời gian đánh nặng",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+6 STR / +3 VIT","+6 baseSTR, +3 baseVIT",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseSTR,6),G(StatFieldType.BaseVIT,3)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","Stun +20% Duration","Core: Kỹ năng tăng 20% thời gian choáng",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+6% StaminaReduction","+6% StaminaReduction",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.StaminaReduction,0.06f)),14));
        n.Add(Core (C+"_T4_N6","Heavy Attack Harder Interrupt","Core: Đánh nặng khó bị ngắt hơn",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+6 STR / +3 VIT","+6 baseSTR, +3 baseVIT",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseSTR,6),G(StatFieldType.BaseVIT,3)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+6% CritChance","+6% bonusCritChance",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.BonusCritChance,0.06f)),19));
        n.Add(Core (C+"_T4_N11","Heavy Attack +20% Damage","Core: Đánh nặng tăng 20% sát thương",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+8 STR / +4 VIT","+8 baseSTR, +4 baseVIT",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseSTR,8),G(StatFieldType.BaseVIT,4)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Damage Scale +20%","Core: Kỹ năng scale thêm 20% sát thương",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+6% DefValueIgnore","+6% defenseValueIgnore",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.DefenseValueIgnore,0.06f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","WarriorSignature","Học WarriorSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+20 STR / +10 VIT","+20 baseSTR, +10 baseVIT",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseSTR,20),G(StatFieldType.BaseVIT,10)),26));
        n.Add(Stat (C+"_T5_N3","+10% StaminaRed / +10% CritChance","+10% StaminaReduction, +10% bonusCritChance",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.StaminaReduction,0.10f),G(StatFieldType.BonusCritChance,0.10f)),27));
        n.Add(Stat (C+"_T5_N4","+20 STR / +10 VIT","+20 baseSTR, +10 baseVIT",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseSTR,20),G(StatFieldType.BaseVIT,10)),28));
        n.Add(Stat (C+"_T5_N5","+10% DefValueIgnore","+10% defenseValueIgnore",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.DefenseValueIgnore,0.10f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CHRIS — BATTLEMAGE
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildBattleMage()
    {
        const string C  = "BattleMage";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Chris_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","+3 VIT","+3 baseVIT",T2,P(t1),         Gs(G(StatFieldType.BaseVIT,3)),0));
        n.Add(Stat (C+"_T2_N2","+3 INT","+3 baseINT",T2,P(C+"_T2_N1"), Gs(G(StatFieldType.BaseINT,3)),1));
        n.Add(Stat (C+"_T2_N3","+5 VIT","+5 baseVIT",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseVIT,5)),2));
        n.Add(Stat (C+"_T2_N4","+5 INT","+5 baseINT",T2,P(C+"_T2_N3"), Gs(G(StatFieldType.BaseINT,5)),3));
        n.Add(Skill(C+"_T2_N5","BattleMageLiteSignature","Học BattleMageLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","BattleMagePassive","Học BattleMagePassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"BattleMage"));
        n.Add(Stat (C+"_T3_N2","+7 VIT / +4 Armor / +8 MR","+7 baseVIT, +4 Armor, +8 MagicResist",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseVIT,7),G(StatFieldType.Armor,4),G(StatFieldType.MagicResist,8)),6));
        n.Add(Skill(C+"_T3_N3","BattleMageSkill","Học BattleMageSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+1% MagicLifeSteal","+1% MagicLifeSteal",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.MagicLifeSteal,0.01f)),8));
        n.Add(Stat (C+"_T3_N5","+7 INT / +0.4 FlatSinGain","+7 baseINT, +0.4 flatSinGain",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseINT,7),G(StatFieldType.FlatSinGain,0.4f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","Heal 10 Sin on HP Threshold","Core: Hồi 10 Sin khi HP xuống dưới ngưỡng nhất định",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+5 INT / +5 VIT","+5 baseINT, +5 baseVIT",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseINT,5),G(StatFieldType.BaseVIT,5)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","Skill Range +20%","Core: Kỹ năng tăng 20% tầm đánh / tầm hồi",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+6 Armor / +12 MR","+6 Armor, +12 MagicResist",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.Armor,6),G(StatFieldType.MagicResist,12)),14));
        n.Add(Core (C+"_T4_N6","Excess Heal 30% → Shield","Core: 30% lượng hồi máu vượt mức chuyển thành shield",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+5 INT / +5 VIT","+5 baseINT, +5 baseVIT",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseINT,5),G(StatFieldType.BaseVIT,5)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+1.5% MagicLifeSteal","+1.5% MagicLifeSteal",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.MagicLifeSteal,0.015f)),19));
        n.Add(Core (C+"_T4_N11","Companion Heals 5% of BM Heal","Core: Companion hồi 5% lượng máu mà BattleMage hồi",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+5 INT / +5 VIT","+5 baseINT, +5 baseVIT",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseINT,5),G(StatFieldType.BaseVIT,5)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Heal Scale +20%","Core: Kỹ năng scale thêm 20% lượng hồi máu",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+0.6 FlatSinGain","+0.6 flatSinGain",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.FlatSinGain,0.6f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","BattleMageSignature","Học BattleMageSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+15 INT / +15 VIT","+15 baseINT, +15 baseVIT",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseINT,15),G(StatFieldType.BaseVIT,15)),26));
        n.Add(Stat (C+"_T5_N3","+10 Armor / +20 MR / +2.5% MagicLS","+10 Armor, +20 MagicResist, +2.5% MagicLifeSteal",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.Armor,10),G(StatFieldType.MagicResist,20),G(StatFieldType.MagicLifeSteal,0.025f)),27));
        n.Add(Stat (C+"_T5_N4","+15 INT / +15 VIT","+15 baseINT, +15 baseVIT",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseINT,15),G(StatFieldType.BaseVIT,15)),28));
        n.Add(Stat (C+"_T5_N5","+1 FlatSinGain","+1 flatSinGain",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.FlatSinGain,1f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CHRIS — BLOODREAVER
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildBloodReaver()
    {
        const string C  = "BloodReaver";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Chris_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","+3 VIT","+3 baseVIT",T2,P(t1),         Gs(G(StatFieldType.BaseVIT,3)),0));
        n.Add(Stat (C+"_T2_N2","+3 STR","+3 baseSTR",T2,P(C+"_T2_N1"), Gs(G(StatFieldType.BaseSTR,3)),1));
        n.Add(Stat (C+"_T2_N3","+5 VIT","+5 baseVIT",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseVIT,5)),2));
        n.Add(Stat (C+"_T2_N4","+5 STR","+5 baseSTR",T2,P(C+"_T2_N3"), Gs(G(StatFieldType.BaseSTR,5)),3));
        n.Add(Skill(C+"_T2_N5","BloodReaverLiteSignature","Học BloodReaverLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","BloodReaverPassive","Học BloodReaverPassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"BloodReaver"));
        n.Add(Stat (C+"_T3_N2","+7 VIT / +2% BonusHp","+7 baseVIT, +2% bonusHp",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseVIT,7),G(StatFieldType.BonusHp,0.02f)),6));
        n.Add(Skill(C+"_T3_N3","BloodReaverSkill","Học BloodReaverSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+6% CritMultiplier","+6% bonusCritMultiplier",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.BonusCritMultiplier,0.06f)),8));
        n.Add(Stat (C+"_T3_N5","+7 STR / +4% AttackSpeed","+7 baseSTR, +4% bonusAttackSpeed",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseSTR,7),G(StatFieldType.BonusAttackSpeed,0.04f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","<50% HP: -20% Stamina Cost","Core: Khi HP dưới 50%, tiêu hao stamina giảm 20%",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+5 STR / +5 VIT","+5 baseSTR, +5 baseVIT",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseSTR,5),G(StatFieldType.BaseVIT,5)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","MoveSpeed Buff +20%","Core: Kỹ năng tăng 20% bonusMoveSpeed",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+3% BonusHp","+3% bonusHp",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.BonusHp,0.03f)),14));
        n.Add(Core (C+"_T4_N6","<30% HP: Immune to Slow","Core: Khi HP dưới 30%, miễn nhiễm với hiệu ứng chậm",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+5 STR / +5 VIT","+5 baseSTR, +5 baseVIT",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseSTR,5),G(StatFieldType.BaseVIT,5)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+9% CritMultiplier","+9% bonusCritMultiplier",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.BonusCritMultiplier,0.09f)),19));
        n.Add(Core (C+"_T4_N11","Self-Damage → MoveSpeed+CritDmg Stack","Core: Nhận damage từ bản thân chồng buff MoveSpeed và CritDmg",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+5 STR / +5 VIT","+5 baseSTR, +5 baseVIT",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseSTR,5),G(StatFieldType.BaseVIT,5)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Damage Scale +20%","Core: Kỹ năng scale thêm 20% sát thương",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+6% AttackSpeed","+6% bonusAttackSpeed",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.BonusAttackSpeed,0.06f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","BloodReaverSignature","Học BloodReaverSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+15 STR / +15 VIT","+15 baseSTR, +15 baseVIT",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseSTR,15),G(StatFieldType.BaseVIT,15)),26));
        n.Add(Stat (C+"_T5_N3","+5% BonusHp / +15% CritMult","+5% bonusHp, +15% bonusCritMultiplier",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.BonusHp,0.05f),G(StatFieldType.BonusCritMultiplier,0.15f)),27));
        n.Add(Stat (C+"_T5_N4","+15 STR / +15 VIT","+15 baseSTR, +15 baseVIT",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseSTR,15),G(StatFieldType.BaseVIT,15)),28));
        n.Add(Stat (C+"_T5_N5","+10% AttackSpeed","+10% bonusAttackSpeed",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.BonusAttackSpeed,0.10f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEO — ROGUE
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildRogue()
    {
        const string C  = "Rogue";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Leo_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","+2 AGI","+2 baseAGI",T2,P(t1),         Gs(G(StatFieldType.BaseAGI,2)),0));
        n.Add(Stat (C+"_T2_N2","+4 DEX","+4 baseDEX",T2,P(C+"_T2_N1"), Gs(G(StatFieldType.BaseDEX,4)),1));
        n.Add(Stat (C+"_T2_N3","+4 AGI","+4 baseAGI",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseAGI,4)),2));
        n.Add(Stat (C+"_T2_N4","+6 DEX","+6 baseDEX",T2,P(C+"_T2_N3"), Gs(G(StatFieldType.BaseDEX,6)),3));
        n.Add(Skill(C+"_T2_N5","RogueLiteSignature","Học RogueLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","RoguePassive","Học RoguePassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"Rogue"));
        n.Add(Stat (C+"_T3_N2","+4 AGI / +4% StealthRed","+4 baseAGI, +4% stealthReduction",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseAGI,4),G(StatFieldType.StealthReduction,0.04f)),6));
        n.Add(Skill(C+"_T3_N3","RogueSkill","Học RogueSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+3% DirBackstab","+3% directionBonusBackstab",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.DirectionBonusBackstab,0.03f)),8));
        n.Add(Stat (C+"_T3_N5","+10 DEX / +4% DashSpeed","+10 baseDEX, +4% dashSpeed",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseDEX,10),G(StatFieldType.DashSpeed,0.04f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","Dash Through Enemies","Core: Dash xuyên qua kẻ địch",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+6 DEX / +3 AGI","+6 baseDEX, +3 baseAGI",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseDEX,6),G(StatFieldType.BaseAGI,3)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","Skill Duration +20%","Core: Kỹ năng tăng 20% thời gian hiệu lực",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+6% StealthReduction","+6% stealthReduction",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.StealthReduction,0.06f)),14));
        n.Add(Core (C+"_T4_N6","First Attack After Dash +10% Dmg","Core: Đòn đánh đầu tiên sau dash tăng 10% sát thương",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+6 DEX / +3 AGI","+6 baseDEX, +3 baseAGI",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseDEX,6),G(StatFieldType.BaseAGI,3)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+4.5% DirBackstab","+4.5% directionBonusBackstab",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.DirectionBonusBackstab,0.045f)),19));
        n.Add(Core (C+"_T4_N11","Backstab: -30% Armor+MS 3s","Core: Backstab giảm 30% Armor và MoveSpeed kẻ địch trong 3 giây",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+8 DEX / +4 AGI","+8 baseDEX, +4 baseAGI",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseDEX,8),G(StatFieldType.BaseAGI,4)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Damage Scale +20%","Core: Kỹ năng scale thêm 20% sát thương",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+6% DashSpeed","+6% dashSpeed",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.DashSpeed,0.06f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","RogueSignature","Học RogueSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+20 DEX / +10 AGI","+20 baseDEX, +10 baseAGI",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseDEX,20),G(StatFieldType.BaseAGI,10)),26));
        n.Add(Stat (C+"_T5_N3","+10% StealthRed / +7.5% DirBackstab","+10% stealthReduction, +7.5% directionBonusBackstab",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.StealthReduction,0.10f),G(StatFieldType.DirectionBonusBackstab,0.075f)),27));
        n.Add(Stat (C+"_T5_N4","+20 DEX / +10 AGI","+20 baseDEX, +10 baseAGI",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseDEX,20),G(StatFieldType.BaseAGI,10)),28));
        n.Add(Stat (C+"_T5_N5","+6% DashSpeed","+6% dashSpeed",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.DashSpeed,0.06f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEO — DUELIST
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildDuelist()
    {
        const string C  = "Duelist";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Leo_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","+3 STR","+3 baseSTR",T2,P(t1),         Gs(G(StatFieldType.BaseSTR,3)),0));
        n.Add(Stat (C+"_T2_N2","+3 DEX","+3 baseDEX",T2,P(C+"_T2_N1"), Gs(G(StatFieldType.BaseDEX,3)),1));
        n.Add(Stat (C+"_T2_N3","+5 STR","+5 baseSTR",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseSTR,5)),2));
        n.Add(Stat (C+"_T2_N4","+5 DEX","+5 baseDEX",T2,P(C+"_T2_N3"), Gs(G(StatFieldType.BaseDEX,5)),3));
        n.Add(Skill(C+"_T2_N5","DuelistLiteSignature","Học DuelistLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","DuelistPassive","Học DuelistPassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"Duelist"));
        n.Add(Stat (C+"_T3_N2","+7 STR / +0.04 Perfect Parry Window","+7 baseSTR, +0.04 Perfect Parry Window",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseSTR,7),G(StatFieldType.ParryWindow,0.04f)),6));
        n.Add(Skill(C+"_T3_N3","DuelistSkill","Học DuelistSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+5% SignatureDmgBonus","+5% signatureDamageBonus",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.SignatureDamageBonus,0.05f)),8));
        n.Add(Stat (C+"_T3_N5","+7 DEX / +6% CritMult","+7 baseDEX, +6% bonusCritMultiplier",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseDEX,7),G(StatFieldType.BonusCritMultiplier,0.06f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","+10 Stamina on Parry","Core: Hồi 10 Stamina khi Parry thành công",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+5 DEX / +5 STR","+5 baseDEX, +5 baseSTR",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseDEX,5),G(StatFieldType.BaseSTR,5)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","Stun +20% Duration","Core: Kỹ năng tăng 20% thời gian choáng",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+0.06 Perfect Parry Window","+0.06 Perfect Parry Window",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.ParryWindow,0.06f)),14));
        n.Add(Core (C+"_T4_N6","Parried Enemy -20 DefVal 3s","Core: Kẻ địch bị Parry mất 20 DefenseValue trong 3 giây",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+5 DEX / +5 STR","+5 baseDEX, +5 baseSTR",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseDEX,5),G(StatFieldType.BaseSTR,5)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+7.5% SignatureDmgBonus","+7.5% signatureDamageBonus",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.SignatureDamageBonus,0.075f)),19));
        n.Add(Core (C+"_T4_N11","Auto-Parry 1 Missed (CD 10s)","Core: Tự động Parry 1 lần khi bỏ lỡ, cooldown 10 giây",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+5 DEX / +5 STR","+5 baseDEX, +5 baseSTR",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseDEX,5),G(StatFieldType.BaseSTR,5)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Damage Scale +20%","Core: Kỹ năng scale thêm 20% sát thương",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+9% CritMultiplier","+9% bonusCritMultiplier",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.BonusCritMultiplier,0.09f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","DuelistSignature","Học DuelistSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+15 DEX / +15 STR","+15 baseDEX, +15 baseSTR",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseDEX,15),G(StatFieldType.BaseSTR,15)),26));
        n.Add(Stat (C+"_T5_N3","+0.1 Perfect Parry Window / +12.5% SigDmg","+0.1 Perfect Parry Window, +12.5% signatureDamageBonus",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.ParryWindow,0.10f),G(StatFieldType.SignatureDamageBonus,0.125f)),27));
        n.Add(Stat (C+"_T5_N4","+15 DEX / +15 STR","+15 baseDEX, +15 baseSTR",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseDEX,15),G(StatFieldType.BaseSTR,15)),28));
        n.Add(Stat (C+"_T5_N5","+15% CritMultiplier","+15% bonusCritMultiplier",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.BonusCritMultiplier,0.15f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEO — MAGE
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildMage()
    {
        const string C  = "Mage";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Leo_T1_N5";
        var n = new List<SkillNodeData>();

        // T2
        n.Add(Stat (C+"_T2_N1","+2 AGI","+2 baseAGI",T2,P(t1),         Gs(G(StatFieldType.BaseAGI,2)),0));
        n.Add(Stat (C+"_T2_N2","+4 INT","+4 baseINT",T2,P(C+"_T2_N1"), Gs(G(StatFieldType.BaseINT,4)),1));
        n.Add(Stat (C+"_T2_N3","+4 AGI","+4 baseAGI",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseAGI,4)),2));
        n.Add(Stat (C+"_T2_N4","+6 INT","+6 baseINT",T2,P(C+"_T2_N3"), Gs(G(StatFieldType.BaseINT,6)),3));
        n.Add(Skill(C+"_T2_N5","MageLiteSignature","Học MageLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","MagePassive","Học MagePassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"Mage"));
        n.Add(Stat (C+"_T3_N2","+4 AGI / +3% BonusSinGain","+4 baseAGI, +3% bonusSinGain",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseAGI,4),G(StatFieldType.BonusSinGain,0.03f)),6));
        n.Add(Skill(C+"_T3_N3","MageSkill","Học MageSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+2% BonusCdr","+2% bonusCdr",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.BonusCdr,0.02f)),8));
        n.Add(Stat (C+"_T3_N5","+10 INT / +6% ProjectileSpeed","+10 baseINT, +6% projectileSpeedBonus",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseINT,10),G(StatFieldType.ProjectileSpeedBonus,0.06f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","+10 Stamina on Element Switch","Core: Hồi 10 Stamina khi chuyển nguyên tố",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+6 INT / +3 AGI","+6 baseINT, +3 baseAGI",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseINT,6),G(StatFieldType.BaseAGI,3)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","Skill Duration +20%","Core: Kỹ năng tăng 20% thời gian hiệu lực",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+4.5% BonusSinGain","+4.5% bonusSinGain",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.BonusSinGain,0.045f)),14));
        n.Add(Core (C+"_T4_N6","Opposite Element 200% Damage","Core: Nguyên tố đối lập gây 200% sát thương",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+6 INT / +3 AGI","+6 baseINT, +3 baseAGI",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseINT,6),G(StatFieldType.BaseAGI,3)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+3% BonusCdr","+3% bonusCdr",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.BonusCdr,0.03f)),19));
        n.Add(Core (C+"_T4_N11","Projectiles Pierce Walls","Core: Đạn phép xuyên qua tường",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+8 INT / +4 AGI","+8 baseINT, +4 baseAGI",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseINT,8),G(StatFieldType.BaseAGI,4)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Damage Scale +20%","Core: Kỹ năng scale thêm 20% sát thương",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+9% ProjectileSpeed","+9% projectileSpeedBonus",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.ProjectileSpeedBonus,0.09f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","MageSignature","Học MageSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+20 INT / +10 AGI","+20 baseINT, +10 baseAGI",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseINT,20),G(StatFieldType.BaseAGI,10)),26));
        n.Add(Stat (C+"_T5_N3","+7.5% BonusSinGain / +5% BonusCdr","+7.5% bonusSinGain, +5% bonusCdr",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.BonusSinGain,0.075f),G(StatFieldType.BonusCdr,0.05f)),27));
        n.Add(Stat (C+"_T5_N4","+20 INT / +10 AGI","+20 baseINT, +10 baseAGI",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseINT,20),G(StatFieldType.BaseAGI,10)),28));
        n.Add(Stat (C+"_T5_N5","+15% ProjectileSpeed","+15% projectileSpeedBonus",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.ProjectileSpeedBonus,0.15f)),29));

        return n;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEO — CATALYST
    // ═════════════════════════════════════════════════════════════════════════
    static List<SkillNodeData> BuildCatalyst()
    {
        const string C  = "Catalyst";
        const int T2=6, T3=16, T4=26, T5=41;
        const string t1 = "Leo_T1_N5";
        var n = new List<SkillNodeData>();

        // T2 (main INT+STR, secondary AGI)
        n.Add(Stat (C+"_T2_N1","+3 AGI","+3 baseAGI",T2,P(t1),         Gs(G(StatFieldType.BaseAGI,3)),0));
        n.Add(Stat (C+"_T2_N2","+1.5 INT / +1.5 STR","+1.5 baseINT, +1.5 baseSTR",T2,P(C+"_T2_N1"),Gs(G(StatFieldType.BaseINT,1.5f),G(StatFieldType.BaseSTR,1.5f)),1));
        n.Add(Stat (C+"_T2_N3","+5 AGI","+5 baseAGI",T2,P(C+"_T2_N2"), Gs(G(StatFieldType.BaseAGI,5)),2));
        n.Add(Stat (C+"_T2_N4","+2.5 INT / +2.5 STR","+2.5 baseINT, +2.5 baseSTR",T2,P(C+"_T2_N3"),Gs(G(StatFieldType.BaseINT,2.5f),G(StatFieldType.BaseSTR,2.5f)),3));
        n.Add(Skill(C+"_T2_N5","CatalystLiteSignature","Học CatalystLiteSignature Skill",T2,P(C+"_T2_N4"),4));

        // T3
        n.Add(Skill(C+"_T3_N1","CatalystPassive","Học CatalystPassive Skill",T3,P(C+"_T2_N5"),5,ClassGrantType.GrantClass,"Catalyst"));
        n.Add(Stat (C+"_T3_N2","+7 AGI / +6% CompanionDmgMult","+7 baseAGI, +6% companionDamageOutputMult",T3,P(C+"_T3_N1"),Gs(G(StatFieldType.BaseAGI,7),G(StatFieldType.CompanionDamageOutputMult,0.06f)),6));
        n.Add(Skill(C+"_T3_N3","CatalystSkill","Học CatalystSkill Skill",T3,P(C+"_T3_N2"),7,ClassGrantType.CombineClasses));
        n.Add(Stat (C+"_T3_N4","+6% CompanionBonusHp","+6% companionBonusHp",T3,P(C+"_T3_N3"),Gs(G(StatFieldType.CompanionBonusHp,0.06f)),8));
        n.Add(Stat (C+"_T3_N5","+3.5 INT / +3.5 STR / +4% CompanionCdr","+3.5 baseINT, +3.5 baseSTR, +4% companionBonusCdr",T3,P(C+"_T3_N4"),Gs(G(StatFieldType.BaseINT,3.5f),G(StatFieldType.BaseSTR,3.5f),G(StatFieldType.CompanionBonusCdr,0.04f)),9));

        // T4
        n.Add(Core (C+"_T4_N1","Co-Kill MoveSpeed Buff","Core: Khi hạ kẻ địch cùng Companion, nhận buff MoveSpeed",T4,P(C+"_T3_N5"),10));
        n.Add(Stat (C+"_T4_N2","+2.5 INT / +2.5 STR / +5 AGI","+2.5 baseINT, +2.5 baseSTR, +5 baseAGI",T4,P(C+"_T4_N1"),Gs(G(StatFieldType.BaseINT,2.5f),G(StatFieldType.BaseSTR,2.5f),G(StatFieldType.BaseAGI,5)),11));
        n.Add(Stat (C+"_T4_N3","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N2"),Gs(G(StatFieldType.AttributePointRemaining,10)),12));
        n.Add(Core (C+"_T4_N4","Debuff Duration +20%","Core: Kỹ năng tăng 20% thời gian debuff",T4,P(C+"_T4_N3"),13));
        n.Add(Stat (C+"_T4_N5","+9% CompanionDmgMult","+9% companionDamageOutputMult",T4,P(C+"_T4_N4"),Gs(G(StatFieldType.CompanionDamageOutputMult,0.09f)),14));
        n.Add(Core (C+"_T4_N6","Marked Enemy +50% Companion Dmg","Core: Kẻ địch bị đánh dấu nhận thêm 50% sát thương từ Companion",T4,P(C+"_T4_N5"),15));
        n.Add(Stat (C+"_T4_N7","+2.5 INT / +2.5 STR / +5 AGI","+2.5 baseINT, +2.5 baseSTR, +5 baseAGI",T4,P(C+"_T4_N6"),Gs(G(StatFieldType.BaseINT,2.5f),G(StatFieldType.BaseSTR,2.5f),G(StatFieldType.BaseAGI,5)),16));
        n.Add(Stat (C+"_T4_N8","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N7"),Gs(G(StatFieldType.AttributePointRemaining,10)),17));
        n.Add(Core (C+"_T4_N9","Cooldown -1s","Core: Giảm 1 giây cooldown kỹ năng",T4,P(C+"_T4_N8"),18));
        n.Add(Stat (C+"_T4_N10","+9% CompanionBonusHp","+9% companionBonusHp",T4,P(C+"_T4_N9"),Gs(G(StatFieldType.CompanionBonusHp,0.09f)),19));
        n.Add(Core (C+"_T4_N11","Healing Applies to Companion","Core: Hồi máu bản thân cũng hồi cho Companion",T4,P(C+"_T4_N10"),20));
        n.Add(Stat (C+"_T4_N12","+2.5 INT / +2.5 STR / +5 AGI","+2.5 baseINT, +2.5 baseSTR, +5 baseAGI",T4,P(C+"_T4_N11"),Gs(G(StatFieldType.BaseINT,2.5f),G(StatFieldType.BaseSTR,2.5f),G(StatFieldType.BaseAGI,5)),21));
        n.Add(Stat (C+"_T4_N13","+10 Attribute Points","+10 điểm thuộc tính tự chọn",T4,P(C+"_T4_N12"),Gs(G(StatFieldType.AttributePointRemaining,10)),22));
        n.Add(Core (C+"_T4_N14","Damage Scale +20%","Core: Kỹ năng scale thêm 20% sát thương",T4,P(C+"_T4_N13"),23));
        n.Add(Stat (C+"_T4_N15","+6% CompanionBonusCdr","+6% companionBonusCdr",T4,P(C+"_T4_N14"),Gs(G(StatFieldType.CompanionBonusCdr,0.06f)),24));

        // T5
        n.Add(Skill(C+"_T5_N1","CatalystSignature","Học CatalystSignature Skill",T5,P(C+"_T4_N15"),25));
        n.Add(Stat (C+"_T5_N2","+7.5 INT / +7.5 STR / +15 AGI","+7.5 baseINT, +7.5 baseSTR, +15 baseAGI",T5,P(C+"_T5_N1"),Gs(G(StatFieldType.BaseINT,7.5f),G(StatFieldType.BaseSTR,7.5f),G(StatFieldType.BaseAGI,15)),26));
        n.Add(Stat (C+"_T5_N3","+15% CompanionDmgMult / +15% CompanionHp","+15% companionDamageOutputMult, +15% companionBonusHp",T5,P(C+"_T5_N2"),Gs(G(StatFieldType.CompanionDamageOutputMult,0.15f),G(StatFieldType.CompanionBonusHp,0.15f)),27));
        n.Add(Stat (C+"_T5_N4","+7.5 INT / +7.5 STR / +15 AGI","+7.5 baseINT, +7.5 baseSTR, +15 baseAGI",T5,P(C+"_T5_N3"),Gs(G(StatFieldType.BaseINT,7.5f),G(StatFieldType.BaseSTR,7.5f),G(StatFieldType.BaseAGI,15)),28));
        n.Add(Stat (C+"_T5_N5","+10% CompanionBonusCdr","+10% companionBonusCdr",T5,P(C+"_T5_N4"),Gs(G(StatFieldType.CompanionBonusCdr,0.10f)),29));

        return n;
    }
}
