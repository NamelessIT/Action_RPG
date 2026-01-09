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
