using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Inventory/Skill Data")]
public class SkillData : ScriptableObject
{
    public enum SkillType
    {
        DefaultPassive, Passive, Skill, Signature, Enemy
    }
    public string id;
    public string skillName;
    public Sprite icon;
    public SkillType skillType;
    public float skillPhysicalMultiplier;
    public float skillMagicMultiplier;
    public float cooldown;
    public float sinChargeReq;
    [TextArea] public string skilDesc;
    //public string skillEffectConfig; để sau

}
