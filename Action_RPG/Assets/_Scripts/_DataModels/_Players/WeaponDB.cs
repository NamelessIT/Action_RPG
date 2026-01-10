// _Scripts/_DataModels/_Players/WeaponDB.cs
using System;

[Serializable]
public class WeaponDB
{
    public int id;
    public float base_atk;
    public float attack_speed;
    public float move_flexibility;
    public float defense_value;
    public float crit_value; // Ví dụ: +20% crit chance → lưu là 0.2f
}
