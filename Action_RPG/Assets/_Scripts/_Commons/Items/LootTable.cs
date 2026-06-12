using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class LootEntry
{
    public WeaponData weaponData;
    public AccessoryData accessoryData;
    [Range(0f, 1f)] public float dropChance = 0.3f;
    public int quantity = 1;
    public string displayName;
    public Sprite icon;
}

[CreateAssetMenu(menuName = "Game/Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    public List<LootEntry> entries = new List<LootEntry>();

    public List<LootEntry> RollLoot()
    {
        var result = new List<LootEntry>();
        foreach (var entry in entries)
        {
            if (UnityEngine.Random.value < entry.dropChance)
                result.Add(entry);
        }
        return result;
    }
}
