using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("--- Health ---")]
    public float maxHp = 1000;
    public float currentHp;

    [Header("--- Attack Stats ---")]
    public float physicalAtk = 120;
    public float magicAtk = 60;

    [Header("--- Multipliers ---")]
    public float skillAtkMultiplier = 1.0f;       // Hệ số skill vật lý
    public float skillMagicMultiplier = 0.5f;     // Hệ số skill phép
    public float critMultiplier = 1.5f;           // Hệ số crit

    [Header("--- Penetration (Player Only) ---")]
    [Tooltip("% Giáp bị trừ khi đánh sau lưng (0.5 = 50%)")]
    public float armorBackstabReduce = 0.5f;
    [Tooltip("% Kháng phép bị trừ khi đánh sau lưng")]
    public float magicResistBackstabReduce = 0.5f;

    [Header("--- Defense Stats (Enemy) ---")]
    public float armor = 100;
    public float magicResist = 100;
    [Tooltip("Chỉ số phòng thủ vũ khí (Block), chỉ có tác dụng khi t <= 0.5")]
    public float defenseValue = 20;

    void Start()
    {
        // Khởi tạo máu đầy khi bắt đầu game
        currentHp = maxHp;
    }

    // Hàm trừ máu (Gọi khi bị đánh trúng)
    public void TakeDamage(float damage)
    {
        currentHp -= damage;
        Debug.Log($"{gameObject.name} nhận {damage} sát thương! HP còn: {currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} đã bị hạ gục!");
        // Thêm logic chết (Play animation, Destroy object...) sau này
    }
}