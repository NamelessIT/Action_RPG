using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class AccessoryData : MonoBehaviour
{
    public int id;
    public string accessoryType;
    public string AccessoryName;
    public string lore;
    public string description;
    public string icon;
    public string rarity;
    public int setId;
    public List<string> stats; //JSON
    public string effectConfig; //JSON
    public bool isUnique;
    public bool playerOnly;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
