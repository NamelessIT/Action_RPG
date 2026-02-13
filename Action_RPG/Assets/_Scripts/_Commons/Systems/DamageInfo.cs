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

    public Stats attacker;

    // [MỚI] Cấp độ va chạm (Dùng cho Super Armor)
    // 0: Normal (Quái nhỏ)
    // 1: Heavy (Quái to / Trọng kích)
    // 2: Massive (Boss / Skill đặc biệt)
    public int impactLevel;
}
