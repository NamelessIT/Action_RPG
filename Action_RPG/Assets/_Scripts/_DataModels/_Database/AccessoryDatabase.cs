// _Scripts/_DataModels/_Database/AccessoryDatabase.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AccessoryStatEntry
{
    public int accessoryId;
    public string statName;
    public float value;
}

[CreateAssetMenu(fileName = "AccessoryDatabase", menuName = "Database/Accessory Database")]
public class AccessoryDatabase : ScriptableObject
{
    public List<AccessoryStatEntry> stats = new List<AccessoryStatEntry>();

    private Dictionary<int, List<StatDB>> cache;

    public List<StatDB> GetStats(int accessoryId)
    {
        if (cache == null)
        {
            cache = new Dictionary<int, List<StatDB>>();
            foreach (var entry in stats)
            {
                if (!cache.ContainsKey(entry.accessoryId))
                    cache[entry.accessoryId] = new List<StatDB>();
                cache[entry.accessoryId].Add(new StatDB
                {
                    statName = entry.statName,
                    value = entry.value
                });
            }
        }

        if (cache.TryGetValue(accessoryId, out var list))
            return list;
        return new List<StatDB>();
    }
}