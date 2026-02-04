// _Scripts/_DataModels/_Players/StatDB.cs
using System;

[Serializable]
public class StatDB
{
    public string statName; // "STR", "max_hp", "crit_chance", ...
    public float value;     // giá trị flat
}