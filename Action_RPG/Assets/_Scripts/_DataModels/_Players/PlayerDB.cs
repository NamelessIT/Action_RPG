// _Scripts/_DataModels/_Players/PlayerDB.cs
using System;

[Serializable]
public class PlayerDB
{
    public int id;
    public string name;
    public float STR, DEX, INT, VIT, AGI;

    // === CÁC FIELD MỚI TỪ EXCEL ===
    public float base_hp;               // base HP cố định
    public int max_stamina;             // max = 100
    public int dash_cost;               // default = 20 (%)
    public float armor_backstab_reduce; // default = 0.5f
    public int? weapon_holding;
    public int? core_shield_holding;
}