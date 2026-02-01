using UnityEngine;

public class VanguardPassive : SkillBehavior
{
    [Header("Settings")]
    public KeyCode shieldKey = KeyCode.F;
    public float shieldSetupTime = 1.0f;
    public float staminaCostPerSec = 20f;
    public float moveSpeedSlowPercent = 0.5f;
    public float damageReductionPercent = 0.5f;

    private bool isHoldingShield = false;
    private float holdTimer = 0f;
    private bool isShieldActive = false;

    protected override void OnEquip()
    {
        // blockThreshold nằm ở Stats cha, gọi OK
        stats.blockThreshold = 0.75f;

        // Các event và biến kia nằm ở AllyStats, SkillBehavior đã khai báo "stats" là AllyStats nên gọi OK
        stats.OnBlockSuccess += HandleBlockSuccess;

        Debug.Log($"[Skill System] Đã kích hoạt logic: {data.skillName}");
    }

    protected override void OnUnequip()
    {
        stats.blockThreshold = 0.5f;
        stats.OnBlockSuccess -= HandleBlockSuccess;
        ResetShieldState();
        Debug.Log($"[Skill System] Đã gỡ bỏ logic: {data.skillName}");
    }

    void Update()
    {
        HandleShieldInput();
    }

    private void HandleShieldInput()
    {
        if (Input.GetKeyDown(shieldKey))
        {
            isHoldingShield = true;
            holdTimer = 0f;
        }

        if (Input.GetKey(shieldKey) && isHoldingShield)
        {
            bool hasStamina = stats.TryConsumeStamina(staminaCostPerSec * Time.deltaTime);
            if (!hasStamina)
            {
                ResetShieldState();
                return;
            }

            if (!isShieldActive)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= shieldSetupTime) ActivateShield();
            }
        }

        if (Input.GetKeyUp(shieldKey))
        {
            ResetShieldState();
        }
    }

    private void ActivateShield()
    {
        isShieldActive = true;

        // Trừ tốc chạy (Cộng âm)
        stats.bonusMoveSpeed -= moveSpeedSlowPercent;

        // Bật cờ Manual Guard trong AllyStats
        stats.isManualGuarding = true;
        stats.manualGuardReduction = damageReductionPercent;

        stats.RecalculateStats();
        Debug.Log("SHIELD ON");
    }

    private void ResetShieldState()
    {
        if (!isHoldingShield) return;
        isHoldingShield = false;
        holdTimer = 0f;

        if (isShieldActive)
        {
            isShieldActive = false;

            // Hoàn trả tốc chạy
            stats.bonusMoveSpeed += moveSpeedSlowPercent;

            // Tắt cờ Manual Guard
            stats.isManualGuarding = false;
            stats.manualGuardReduction = 0f;

            stats.RecalculateStats();
            Debug.Log("SHIELD OFF");
        }
    }

    private void HandleBlockSuccess()
    {
        // Chỉ hồi khi đang dựng khiên active
        if (isShieldActive)
        {
            float restore = stats.maxStamina * 0.05f;
            if (stats.currentStamina < stats.maxStamina)
            {
                stats.currentStamina += restore;
                if (stats.currentStamina > stats.maxStamina) stats.currentStamina = stats.maxStamina;
                Debug.Log($"Vanguard Block: Hồi {restore} Stamina");
            }
        }
    }
}