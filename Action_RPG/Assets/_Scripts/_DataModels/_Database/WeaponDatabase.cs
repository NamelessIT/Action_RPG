using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class WeaponStatEntry
{
    
    public string weaponId;
    public string statName;
    public float value;
}

[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Database/Weapon Database")]
public class WeaponDatabase : ScriptableObject
{
    public List<WeaponDB> weapons = new List<WeaponDB>();
    public List<WeaponStatEntry> extraStats = new List<WeaponStatEntry>();


    private Dictionary<string, WeaponDB> weaponCache;
    private Dictionary<string, List<StatDB>> statCache;


    public WeaponDB GetWeapon(string id)
    {
        if (weaponCache == null)
        {
            weaponCache = new Dictionary<string, WeaponDB>();
            foreach (var weapon in weapons)
                weaponCache[weapon.id] = weapon; 
        }
        return weaponCache.TryGetValue(id, out var result) ? result : null;
    }


    public List<StatDB> GetExtraStats(string weaponId)
    {
        if (statCache == null)
        {
            statCache = new Dictionary<string, List<StatDB>>();
            foreach (var entry in extraStats)
            {
                if (!statCache.ContainsKey(entry.weaponId)) 
                    statCache[entry.weaponId] = new List<StatDB>();
                statCache[entry.weaponId].Add(new StatDB
                {
                    statName = entry.statName,
                    value = entry.value
                });
            }
        }
        return statCache.TryGetValue(weaponId, out var list) ? list : new List<StatDB>();
    }
}