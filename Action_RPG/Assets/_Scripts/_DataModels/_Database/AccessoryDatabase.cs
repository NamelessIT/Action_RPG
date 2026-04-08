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

/// <summary>
/// [DAO Layer - Database Tier 1] Accessory stat modifiers.
/// This is a ScriptableObject asset containing accessory stat bonuses.
/// 
/// ARCHITECTURE:
/// - Called ONLY by InAppPlayerStateDAO.LoadAccessoryStats()
/// - Part of the read-only database layer (no modifications here)
/// - Uses lazy-loaded Dictionary cache for O(1) lookups (per accessory ID)
/// 
/// Usage: Create in Editor via Assets/Create/Database/Accessory Database
/// </summary>
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