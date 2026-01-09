using UnityEngine;
using UnityEngine.Rendering;

public class CharacterStats : MonoBehaviour
{
    [Header("--- Health ---")]
    public float maxHp = 1000;
    public float currentHp;
    public float baseHp=1000;

    [Header("--- Stamina ---")]
    public float maxStamina = 100f;
    public float staminaBaseRecovery = 0.5f;
    public float currentStamina;

    [Header("--- Energy ---")]
    public float maxEnergy = 50f;
    public float currentEnergy;
    public float energyBaseCollection = 0.5f;

    [Header("--- Base Stat ---")]
    [Tooltip("STR:Chỉ số sức mạnh (Strength); DEX:Chỉ số khéo léo (Dexterity); INT:Chỉ số trí tuệ (Intelligence); VIT:Chỉ số sinh lực (Vitality); AGI:Chỉ số nhanh nhẹn (Agility)")]
    public float STR;
    public float DEX;
    public float INT;
    public float VIT;
    public float AGI;


    [Header("--- Attack Stats ---")]
    public float physicalAtk = 120;
    public float magicAtk = 60;

    [Header("--- Multipliers ---")]
    public float skillPhysicalMultiplier = 1.0f;       // Hệ số skill vật lý
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

    [Header("--- Movement Setting ---")]
    [Tooltip("Thời gian để xoay 180 độ (Giây). Ví dụ: 0.5s")]
    public float moveSpeed = 5f;
    public float dashDistance = 5f;
    public float turnDuration = 0.1f;
    public float dashRecovery = 1f;
    public float dashCost = 20f;
    public float moveThresholdAngle = 45f;


    void Start()
    {
        // Khởi tạo máu đầy khi bắt đầu game
        maxHp=baseHp*1;
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