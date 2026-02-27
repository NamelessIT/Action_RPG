using UnityEngine;

public class DuelistPassive : SkillBehavior
{
    [Header("Config")]
    public bool isPlayer = true;
    public bool isLearned = false;

    [Header("Parry Settings (Hold Mechanism)")]
    [Tooltip("Tổng thời gian tối đa được phép giữ Parry (0.5s)")]
    public float maxParryDuration = 0.5f;

    [Tooltip("Thời điểm bắt đầu tính là Perfect (0.4s). Tức là từ 0.4s đến 0.5s là Perfect.")]
    public float perfectParryStartTime = 0.1f;

    [Tooltip("Thời gian hồi chiêu sau khi buông nút hoặc hết giờ")]
    public float parryCooldown = 1.0f; // Tăng lên xíu vì skill này mạnh

    [Header("Counter Attack Settings")]
    [Tooltip("Thời gian tồn tại của Buff Phản công (5s)")]
    public float counterDuration = 5.0f;

    // --- State ---
    private float currentHoldTimer = 0f;    // Đếm thời gian đã giữ nút
    private float currentCooldown = 0f;
    private float currentCounterBuffTimer = 0f; // Đếm ngược buff Crit

    private bool isHoldingButton = false;   // Trạng thái đang giữ nút

    private Animator animator;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        animator = myPlayer.GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (stats == null) stats = GetComponent<AllyStats>();
    }

    protected override void OnEquip()
    {
        isLearned = true;
        Debug.Log("[Duelist] Hold Right Click to Parry!");
    }

    protected override void OnUnequip()
    {
        isLearned = false;
        StopParry(); // Reset trạng thái ngay
    }

    void Update()
    {
        // 1. Giảm Cooldown
        if (currentCooldown > 0) currentCooldown -= Time.deltaTime;

        // 2. Giảm Timer Buff Counter (Buff 5 giây)
        if (currentCounterBuffTimer > 0) currentCounterBuffTimer -= Time.deltaTime;

        // 3. LOGIC INPUT (CHỈ PLAYER)
        if (isLearned && isPlayer)
        {
            HandleInput();
        }



        // (Nếu muốn Enemy dùng được thì cần viết hàm AI gọi StartHoldParry và StopHoldParry riêng)
        // 4. Logic Timer Parry (Xử lý việc giữ nút/thời gian hiệu lực)
        if (isHoldingButton || currentHoldTimer > 0)
        {
            UpdateParryState(); // Tách logic đếm giờ ra hàm riêng hoặc để trong Update
        }
    }

    // [MỚI] Hàm dành riêng cho AI gọi (Bắt đầu đỡ)
    public void AI_StartParry()
    {
        if (currentCooldown <= 0 && !isHoldingButton)
        {
            StartParry();
        }
    }

    // [MỚI] Hàm dành riêng cho AI gọi (Ngừng đỡ)
    public void AI_StopParry()
    {
        StopParry();
    }

    void HandleInput()
    {
        // --- A. BẮT ĐẦU GIỮ NÚT ---
        if (Input.GetMouseButtonDown(1))
        {
            if (currentCooldown <= 0)
            {
                StartParry();
            }
        }

        // --- B. ĐANG GIỮ NÚT ---
        if (Input.GetMouseButton(1) && isHoldingButton)
        {
            UpdateParryState();
        }

        // --- C. THẢ NÚT (HOẶC HẾT GIỜ) ---
        if (Input.GetMouseButtonUp(1) && isHoldingButton)
        {
            StopParry();
        }
    }

    void StartParry()
    {
        isHoldingButton = true;
        currentHoldTimer = 0f; // Reset đếm giờ

        // Báo cho Stats biết là bắt đầu đỡ
        if (stats != null)
        {
            stats.isParrying = true;
            stats.isPerfectParryWindow = false; // Mới bấm chưa phải perfect
        }

        // Trigger Animation (Vào thế thủ)
        // if (animator != null) animator.SetBool("IsParrying", true); 
        // Lưu ý: Nên dùng Bool cho việc giữ nút thay vì Trigger

        Debug.Log("Duelist: Start Holding Parry...");
    }

    void UpdateParryState()
    {
        // Tăng thời gian giữ
        currentHoldTimer += Time.deltaTime;

        // Logic chia giai đoạn
        if (stats != null)
        {
            // Giai đoạn 1: Perfect Parry (0.0s -> 0.1s)
            if (currentHoldTimer <= perfectParryStartTime )
            {
                if (!stats.isPerfectParryWindow) Debug.Log("<color=cyan>Duelist: Entering Perfect Window!</color>");
                stats.isPerfectParryWindow = true;
            }
            // Giai đoạn 2: Normal Parry (0.1s -> 0.5s)
            else if (currentHoldTimer > perfectParryStartTime && currentHoldTimer <= maxParryDuration)
            {
                // Chỉ log 1 lần khi vừa bước vào giai đoạn Perfect
                stats.isPerfectParryWindow = false;
            }
            // Giai đoạn 3: Quá giờ ( > 0.5s) -> Tự ngắt
            else
            {
                Debug.Log("Duelist: Parry Overloaded (Too long)!");
                StopParry();
            }
        }
    }

    void StopParry()
    {
        if (!isHoldingButton) return; // Đã dừng rồi thì thôi

        isHoldingButton = false;
        currentHoldTimer = 0f;

        // Vào hồi chiêu
        currentCooldown = parryCooldown;

        // Reset Stats
        if (stats != null)
        {
            stats.isParrying = false;
            stats.isPerfectParryWindow = false;
        }

        // Tắt Animation
        // if (animator != null) animator.SetBool("IsParrying", false);

        // Debug.Log("Duelist: End Parry.");
    }

    // --- HÀM XỬ LÝ KHI PARRY THÀNH CÔNG (GỌI TỪ STATS) ---
    public void OnParrySuccess(bool isPerfect, Stats attacker)
    {
        if (isPerfect)
        {
            Debug.Log("<color=yellow>PERFECT PARRY SUCCESS!</color>");

            // 1. Kích hoạt Buff Counter (Kéo dài 5s)
            currentCounterBuffTimer = counterDuration;

            // 2. Stun kẻ địch
            if (attacker != null)
            {
                DamageInfo stunInfo = new DamageInfo();
                stunInfo.isStun = true;
                stunInfo.stunDuration = 0.5f;
                stunInfo.damageAmount = 0;
                stunInfo.attacker = stats;
                attacker.TakeDamage(stunInfo);
            }

            // [Tùy chọn] Thành công Perfect thì ngắt thế thủ luôn để phản công?
            StopParry();
        }
        else
        {
            Debug.Log("<color=white>Normal Parry Hit.</color>");
            // Normal Parry thì vẫn giữ thế thủ được đến khi hết giờ hoặc thả tay
        }
    }

    // --- HÀM CHECK BUFF COUNTER ---
    public bool TryUseCounterAttack()
    {
        if (currentCounterBuffTimer > 0)
        {
            currentCounterBuffTimer = 0; // Dùng xong mất luôn
            return true;
        }
        return false;
    }
}