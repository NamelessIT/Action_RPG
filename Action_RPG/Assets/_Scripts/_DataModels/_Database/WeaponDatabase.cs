// _Scripts/_DataModels/_Database/WeaponDatabase.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class WeaponStatEntry
{
    public int weaponId;
    public string statName;
    public float value;
}

[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Database/Weapon Database")]
public class WeaponDatabase : ScriptableObject
{
    public List<WeaponDB> weapons = new List<WeaponDB>();
    public List<WeaponStatEntry> extraStats = new List<WeaponStatEntry>(); // ← THÊM DÒNG NÀY

    private Dictionary<int, WeaponDB> weaponCache;
    private Dictionary<int, List<StatDB>> statCache;

    public WeaponDB GetWeapon(int id)
    {
        if (weaponCache == null)
        {
            weaponCache = new Dictionary<int, WeaponDB>();
            foreach (var weapon in weapons)
                weaponCache[weapon.id] = weapon;
        }
        return weaponCache.TryGetValue(id, out var result) ? result : null;
    }

    public List<StatDB> GetExtraStats(int weaponId)
    {
        if (statCache == null)
        {
            statCache = new Dictionary<int, List<StatDB>>();
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

        if (statCache.TryGetValue(weaponId, out var list))
            return list;
        return new List<StatDB>();
    }
}