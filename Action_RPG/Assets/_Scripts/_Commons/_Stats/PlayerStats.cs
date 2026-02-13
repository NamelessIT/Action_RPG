using UnityEngine;

public class PlayerStats : AllyStats
{
    public bool isPerfectParrySuccess;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void Start()
    {
        this.tag = "Player";
    }

    // Update is called once per frame
    public override void Update()
    {
        base.Update();
    }
}
