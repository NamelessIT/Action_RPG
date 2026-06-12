using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "StatDatabase",
    menuName = "Database/Stat Database"
)]
public class StatDatabase : ScriptableObject
{
    public List<StatDB> stats = new List<StatDB>();

    private Dictionary<string, StatDB> cache;

    /// <summary>
    /// [OBSOLETE - Not currently used in DAO layer]
    /// Reserved for future expansion when stat lookups by name are needed.
    /// Currently all stat definitions are handled via PlayerDatabase, WeaponDatabase, AccessoryDatabase.
    /// </summary>
    [System.Obsolete("Not used in current architecture. Reserved for future use.", false)]
    public StatDB GetStat(string statName)
    {
        if (cache == null)
        {
            cache = new Dictionary<string, StatDB>();
            foreach (var s in stats)
            {
                if (!cache.ContainsKey(s.statName))
                    cache.Add(s.statName, s);
            }
        }

        return cache.TryGetValue(statName, out var stat) ? stat : null;
    }
}
