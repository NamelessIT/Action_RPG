// _Scripts/_Commons/PlayerController/_PlayerState/PlayerStateSaveData.cs
using System;
using System.Collections.Generic;

[Serializable]
public class PlayerStateSaveData
{
    // Dữ liệu lưu DB
    public float currentHp;
    public float currentEnergy;
    public int checkpointId;

    // Dữ liệu runtime (dùng khi reload)
    public int playerId;
    public int currentClassId;
    public int weaponId;
    public int coreShieldId;
    public List<int> accessoryIds = new List<int>();
    public float positionX;
    public float positionY;
}