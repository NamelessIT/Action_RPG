using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Core Shield", menuName = "Inventory/Core Shield Data")]
public class CoreShieldData : ScriptableObject
{
    public enum Rarity
    {
        Residual_1,
        Stained_2,
        Corrupted_3,
        Condemned_4,
        Anomalous_5
    }
    public string id;
    public string coreShieldName;
    public Sprite icon;
    [TextArea] public string lore;
    [TextArea] public string description;
    public Rarity rarity;
    public int setId;

    [Header("Base Stats")]
    public float armor;
    public float magicResist;

    [Header("Substats")]
    // Đổi từ string sang List để Inspector hiển thị đẹp và code đọc được luôn
    public List<StatModifier> stats = new List<StatModifier>();
    //public string effectConfig; để sau

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
