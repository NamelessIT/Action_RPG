using UnityEngine;

[System.Serializable]
public class DamageInfo
{
    // [MỚI] Chia làm 3 biến thành phần
    public float physDamage;
    public float magicDamage;
    public float trueDamage;

    // [MỚI] Hàm tiện ích (Getter) để lấy tổng sát thương bất cứ lúc nào cần
    public float TotalRawDamage => physDamage + magicDamage + trueDamage;
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
