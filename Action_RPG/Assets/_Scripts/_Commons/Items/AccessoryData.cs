using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Accessory", menuName = "Inventory/Accessory Data")]
public class AccessoryData : ScriptableObject
{
    public enum AccessoryType
    {
        CoreShard,
        MarkOfSin,
        RelicOfMemory,
        Parasite,
        Chain
    }
    public enum Rarity
    {
        Residual_1,
        Stained_2,
        Corrupted_3,
        Condemned_4,
        Anomalous_5
    }
    public string id;
    public AccessoryType accessoryType;
    public string AccessoryName;
    [TextArea] public string lore;
    [TextArea] string description;
    public Sprite icon;
    public Rarity rarity;
    public int setId;

    [Header("Substats")]
    // Đổi từ string sang List để Inspector hiển thị đẹp và code đọc được luôn
    public List<StatModifier> substats = new List<StatModifier>();

    //public string effectConfig; //để sau
    public bool isUnique;
    public bool playerOnly;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
