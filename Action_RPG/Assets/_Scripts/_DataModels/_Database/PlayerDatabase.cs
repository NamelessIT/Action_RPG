// _Scripts/_DataModels/_Database/PlayerDatabase.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// [DAO Layer - Database Tier 1] Player base statistics configuration.
/// This is a ScriptableObject asset containing player templates.
/// 
/// ARCHITECTURE: 
/// - Called ONLY by InAppPlayerStateDAO.LoadPlayerFromDB()
/// - Part of the read-only database layer (no modifications here)
/// - Uses lazy-loaded Dictionary cache for O(1) lookups
/// 
/// Usage: Create in Editor via Assets/Create/Database/Player Database
/// </summary>
[CreateAssetMenu(fileName = "PlayerDatabase", menuName = "Database/Player Database")]
public class PlayerDatabase : ScriptableObject
{
    [System.Serializable]
    public class PlayerEntry
    {
        public int id;
        public string name;
        public float STR, DEX, INT, VIT, AGI;
        public float base_hp;
        public int max_stamina;
        public int dash_cost;
        public float armor_backstab_reduce;
        public int? weapon_holding;
        public int? core_shield_holding;
        public List<int> defaultAccessories = new List<int>();
    }

    public List<PlayerEntry> players = new List<PlayerEntry>();

    private Dictionary<int, PlayerEntry> cache;

    public PlayerDB GetPlayer(int id)
    {
        if (cache == null)
        {
            cache = new Dictionary<int, PlayerEntry>();
            foreach (var p in players)
                cache[p.id] = p;
        }

        if (cache.TryGetValue(id, out var entry))
        {
            return new PlayerDB
            {
                id = entry.id,
                name = entry.name,
                STR = entry.STR,
                DEX = entry.DEX,
                INT = entry.INT,
                VIT = entry.VIT,
                AGI = entry.AGI,
                base_hp = entry.base_hp,
                max_stamina = entry.max_stamina,
                dash_cost = entry.dash_cost,
                armor_backstab_reduce = entry.armor_backstab_reduce,
                weapon_holding = entry.weapon_holding,
                core_shield_holding = entry.core_shield_holding
            };
        }
        return null;
    }

    public List<int> GetEquippedAccessories(int playerId)
    {
        if (cache != null && cache.TryGetValue(playerId, out var p))
            return new List<int>(p.defaultAccessories);
        return new List<int>();
    }
}