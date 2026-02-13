using UnityEngine;

public class DuelistPassive : MonoBehaviour
{
    [Header("Config")]
    public bool isPlayer = true; // [MỚI] Check xem ai đang dùng
    public bool isLearned = false;

    [Header("Parry Settings")]
    public float parryWindowDuration = 0.3f; // Thời gian cửa sổ đỡ đòn (0.3s)
    public float perfectParryWindow = 0.15f; // Thời gian vàng để Perfect (0.15s đầu)
    public float parryCooldown = 0.5f;       // Hồi chiêu để tránh spam chuột

    [Header("Counter Attack Settings")]
    public float counterDuration = 2.0f;     // Thời gian buff Crit sau khi Perfect Parry

    // --- State ---
    private float currentParryTimer = 0f;    // Đếm ngược thời gian đỡ
    private float currentCooldown = 0f;
    private float currentCounterBuffTimer = 0f; // Đếm ngược buff Crit

    // --- References ---
    private Animator animator;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {

        // Logic giảm Timer
        if (currentParryTimer > 0) currentParryTimer -= Time.deltaTime;
        if (currentCooldown > 0) currentCooldown -= Time.deltaTime;
        if (currentCounterBuffTimer > 0) currentCounterBuffTimer -= Time.deltaTime;

        // 2. INPUT: Chuột phải để Parry
        if (isLearned && isPlayer && Input.GetMouseButtonDown(1))
        {
            PerformParry();
        }
    }

    void PerformParry()
    {
        // Đang hồi chiêu hoặc đang đỡ thì không spam được
        if (currentCooldown > 0 || currentParryTimer > 0) return;

        // Kích hoạt cửa sổ đỡ đòn
        currentParryTimer = parryWindowDuration;
        currentCooldown = parryCooldown;

        // Animation (Nếu có)
        if (animator != null) animator.SetTrigger("Parry");

        // Debug.Log("Duelist: Parry Stance Active!");
    }

    // --- HÀM GỌI TỪ PLAYER STATS KHI BỊ ĐÁNH ---
    // Trả về: Lượng Damage thực tế sẽ nhận sau khi tính toán Parry
    public float HandleIncomingDamage(float incomingDamage, Stats attacker)
    {
        // Nếu không học hoặc không trong thời gian đỡ -> Nhận đủ dam
        if (!isLearned || currentParryTimer <= 0)
        {
            return incomingDamage;
        }

        // Tính thời gian đã trôi qua kể từ lúc bấm nút
        // (parryWindowDuration - currentParryTimer) là thời gian đã trôi qua
        float timeSinceParryStart = parryWindowDuration - currentParryTimer;

        // --- KIỂM TRA PERFECT PARRY ---
        // Nếu bị đánh trúng ngay khi vừa bấm nút (trong khoảng perfectParryWindow)
        if (timeSinceParryStart <= perfectParryWindow)
        {
            Debug.Log("<color=yellow>PERFECT PARRY!</color>");

            // 1. Giảm 100% Damage
            // 2. Stun quái 0.5s
            DamageInfo stunInfo = new DamageInfo();
            stunInfo.isStun = true;
            stunInfo.stunDuration = 0.5f;
            stunInfo.damageAmount = 0; // Stun không gây dam
            attacker.TakeDamage(stunInfo); // Gọi hàm TakeDamage của quái để gây Stun

            // 3. Kích hoạt Buff Counter Attack (Chí mạng)
            currentCounterBuffTimer = counterDuration;

            return 0f; // Không nhận damage
        }

        // --- NORMAL PARRY ---
        else
        {
            Debug.Log("<color=white>Normal Parry.</color>");
            // Giảm 80% Damage -> Nhận 20%
            return incomingDamage * 0.2f;
        }
    }

    // --- HÀM KIỂM TRA BUFF COUNTER ---
    // Gọi hàm này khi Player tấn công để xem có được buff không
    public bool TryUseCounterAttack()
    {
        if (currentCounterBuffTimer > 0)
        {
            // Dùng xong thì tắt buff ngay (chỉ áp dụng cho 1 đòn)
            currentCounterBuffTimer = 0;
            return true;
        }
        return false;
    }
}