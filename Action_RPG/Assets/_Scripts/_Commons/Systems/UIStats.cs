using UnityEngine;
using UnityEngine.UI;
using TMPro; // Nhớ import TextMeshPro nếu dùng text xịn, hoặc dùng UnityEngine.UI cho Text thường

namespace Systems
{
    public class UIStats : MonoBehaviour
    {
        [Header("--- References ---")]
        [Tooltip("Kéo PlayerStats vào đây (hoặc để trống tự tìm)")]
        public AllyStats playerStats;
        [Tooltip("Kéo SkillManager vào đây")]
        public SkillManager skillManager;

        [Header("--- Bars Setup ---")]
        public Slider hpSlider;
        public TextMeshProUGUI hpText; // Tùy chọn: Hiển thị số máu (100/100)

        public Slider staminaSlider;
        public Slider sinSlider; // Thanh Sin (Nghiệp/Mana)

        [Header("--- Skill E (Normal Skill - Uses Stamina) ---")]
        public GameObject skillE_Container;      // Parent object để tắt mở nếu chưa học
        public Image skillE_Icon;
        public Image skillE_CooldownFill;        // Ảnh mờ đè lên (Type: Filled)
        public TextMeshProUGUI skillE_CooldownText;
        public Color notEnoughStaminaColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Màu tối

        [Header("--- Skill Q (Signature/Ult - Uses Sin) ---")]
        public GameObject skillQ_Container;
        public Image skillQ_Icon;
        public Image skillQ_CooldownFill;
        public TextMeshProUGUI skillQ_CooldownText;
        public Color notEnoughSinColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        void Start()
        {
            // Tự động tìm player nếu quên kéo
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();

            if (skillManager == null && playerStats != null)
                skillManager = playerStats.GetComponent<SkillManager>();

            // Setup Slider Max Values lúc đầu
            if (playerStats != null)
            {
                if (hpSlider) hpSlider.maxValue = playerStats.maxHp;
                if (staminaSlider) staminaSlider.maxValue = playerStats.maxStamina;
                if (sinSlider) sinSlider.maxValue = playerStats.maxSin;
            }
        }

        void Update()
        {
            if (playerStats == null) return;

            UpdateBars();
            UpdateSkills();
        }

        // 1. Cập nhật các thanh chỉ số
        void UpdateBars()
        {
            // --- HP ---
            if (hpSlider)
            {
                hpSlider.maxValue = playerStats.maxHp; // Cập nhật max đề phòng buff máu
                hpSlider.value = Mathf.Lerp(hpSlider.value, playerStats.currentHp, Time.deltaTime * 10f); // Hiệu ứng mượt
            }
            if (hpText) hpText.text = $"{Mathf.Ceil(playerStats.currentHp)} / {playerStats.maxHp}";

            // --- STAMINA ---
            if (staminaSlider)
            {
                staminaSlider.maxValue = playerStats.maxStamina;
                staminaSlider.value = playerStats.currentStamina;
            }

            // --- SIN ---
            if (sinSlider)
            {
                sinSlider.maxValue = playerStats.maxSin;
                sinSlider.value = playerStats.currentSin;
            }
        }

        // 2. Cập nhật trạng thái Skill
        void UpdateSkills()
        {
            if (skillManager == null) return;

            // --- UPDATE SKILL E (Stamina) ---
            UpdateSingleSkillSlot(
                skillManager.currentSkill,      // Skill đang trang bị
                skillE_Container,
                skillE_Icon,
                skillE_CooldownFill,
                skillE_CooldownText,
                playerStats.currentStamina,     // Tài nguyên hiện có
                notEnoughStaminaColor
            );

            // --- UPDATE SKILL Q (Sin) ---
            UpdateSingleSkillSlot(
                skillManager.currentSignature,  // Skill Ultimate
                skillQ_Container,
                skillQ_Icon,
                skillQ_CooldownFill,
                skillQ_CooldownText,
                playerStats.currentSin,         // Tài nguyên hiện có
                notEnoughSinColor
            );
        }

        // Hàm chung để xử lý logic hiển thị 1 ô skill
        void UpdateSingleSkillSlot(
            SkillData activeSkill,
            GameObject container,
            Image icon,
            Image cooldownOverlay,
            TextMeshProUGUI cooldownText,
            float currentResource,
            Color dimColor)
        {
            // A. Kiểm tra có skill không
            if (activeSkill == null)
            {
                if (container.activeSelf) container.SetActive(false);
                return;
            }

            if (!container.activeSelf) container.SetActive(true);

            // B. Hiển thị Icon
            if (icon.sprite != activeSkill.icon)
                icon.sprite = activeSkill.icon;

            // C. Xử lý Cooldown
            float cooldownTimer = 0; // Giả sử trong SkillBehavior bạn để biến này là public
            float maxCooldown = activeSkill.cooldown;

            if (cooldownTimer > 0)
            {
                // Đang hồi chiêu
                cooldownOverlay.fillAmount = cooldownTimer / maxCooldown;
                cooldownText.text = cooldownTimer.ToString("F1"); // Hiển thị 1 số thập phân (ví dụ: 2.5)
                cooldownText.gameObject.SetActive(true);

                // Khi đang hồi chiêu thì icon tối lại luôn cho dễ nhìn
                icon.color = Color.gray;
            }
            else
            {
                // Đã hồi xong -> Check Tài nguyên (Stamina/Sin)
                cooldownOverlay.fillAmount = 0;
                cooldownText.gameObject.SetActive(false);

                // D. Check Resource (Đủ mana/stamina không?)
                float cost = activeSkill.sinChargeReq; // Giả sử SkillData có biến này

                if (currentResource < cost)
                {
                    // Không đủ tiền -> Tối màu
                    icon.color = dimColor;
                }
                else
                {
                    // Đủ tiền -> Sáng màu
                    icon.color = Color.white;
                }
            }
        }
    }
}