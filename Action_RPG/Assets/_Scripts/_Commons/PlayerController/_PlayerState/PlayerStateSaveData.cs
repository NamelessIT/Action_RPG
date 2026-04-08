// _Scripts/_Commons/PlayerController/_PlayerState/PlayerStateSaveData.cs
using System;
using System.Collections.Generic;

[Serializable]
public class PlayerStateSaveData
{
    // Dữ liệu lưu DB
    public float currentHp;
    public float currentEnergy;
    public float currentStamina;
    public float currentSin;
    public int checkpointId;
    public int level;
    public float exp;
    public float nextLevelExp;
    public float maxExpForCurrentLevel;
    public int attributePointRemain;
    public int skillPointRemain;
    public float baseSTR;
    public float baseDEX;
    public float baseINT;
    public float baseVIT;
    public float baseAGI;

    // Dữ liệu runtime (dùng khi reload)
    public int playerId;
    public int currentClassId;
    public int weaponId;
    public int coreShieldId;
    public List<int> accessoryIds = new List<int>();
    public string weaponAssetId;
    public string coreShieldAssetId;
    public List<string> accessoryAssetIds = new List<string>();
    public float positionX;
    public float positionY;
}