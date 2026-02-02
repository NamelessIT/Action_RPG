using UnityEngine;

[System.Serializable]
public struct DamageInfo
{
    public float damageAmount;
    public bool isCrit;

    // --- Hiệu ứng khống chế (CC) ---
    public bool isStun;
    public float stunDuration;

    public bool isKnockback;
    public float knockbackForce;
    public Vector3 sourcePosition; // Vị trí kẻ đánh (để tính hướng đẩy lùi)
}
