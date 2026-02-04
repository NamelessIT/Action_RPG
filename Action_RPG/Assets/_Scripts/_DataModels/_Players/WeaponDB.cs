// _Scripts/_DataModels/_Players/WeaponDB.cs
using System;

[Serializable]
public class WeaponDB
{
    public string id;
    public string weaponType;
    public string weaponAtkType; //Physical; Magic
    public string weaponName;
    public string lore;
    public string description;
    public string icon;
    public string rarity;
    public float baseAtk;
    public float baseAttackSpeed;
    public float moveFlexibility;
    public float baseDefenseValue;
    public float bonusCritChance;
    public string substats;
    public string effectConfig;
    public float reqStr;
    public float reqDex;
    public float reqInt;
    public float reqVit;
    public float reqAgi;
    public bool playerOnly;
}