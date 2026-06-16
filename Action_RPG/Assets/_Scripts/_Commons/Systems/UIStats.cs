using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Systems
{
    public class UIStats : MonoBehaviour
    {
        [Header("--- References ---")]
        [Tooltip("Kéo PlayerStats vào đây (hoặc để trống tự tìm)")]
        public PlayerStats playerStats;
        [Tooltip("Kéo SkillManager vào đây")]
        public SkillManager skillManager;

        [Header("--- Bars Setup ---")]
        public Slider hpSlider;
        public TextMeshProUGUI hpText;

        public Slider staminaSlider;
        public TextMeshProUGUI staminaText;
        public Slider sinSlider;
        public TextMeshProUGUI sinText;

        [Header("--- Shield Bar ---")]
        [Tooltip("Image fill màu bạc — lấp đầy phần HP còn trống (fill từ trái sang phải)")]
        public Image shieldFillImage;
        [Tooltip("Image fill màu bạc mờ — đè lên HP bar khi shield vượt quá max HP (fill từ phải sang trái)")]
        public Image shieldOverlayImage;

        [Header("--- Skill E (Normal Skill - No Cost) ---")]
        public GameObject skillE_Container;
        public Image skillE_Icon;
        public Image skillE_CooldownFill;
        public TextMeshProUGUI skillE_CooldownText;
        public Color notEnoughStaminaColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("--- Skill Q (Signature/Ult - Uses Sin) ---")]
        public GameObject skillQ_Container;
        public Image skillQ_Icon;
        public Image skillQ_CooldownFill;
        public TextMeshProUGUI skillQ_CooldownText;
        public Color notEnoughSinColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        void Start()
        {
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();

            if (skillManager == null && playerStats != null)
                skillManager = playerStats.GetComponent<SkillManager>();

            // Khởi tạo Max Value để thanh không bị rỗng lúc đầu
            if (playerStats != null)
            {
                if (hpSlider)
                {
                    hpSlider.maxValue = playerStats.maxHp;
                    hpSlider.value = playerStats.currentHp;
                }
                if (staminaSlider)
                {
                    staminaSlider.maxValue = playerStats.maxStamina;
                    staminaSlider.value = playerStats.currentStamina;
                }
                if (sinSlider)
                {
                    sinSlider.maxValue = playerStats.maxSin;
                    sinSlider.value = playerStats.currentSin;
                }
            }
        }

        void Update()
        {
            if (playerStats == null) return;

            UpdateBars();
            UpdateSkills();
        }

        void UpdateBars()
        {
            if (hpSlider)
            {
                hpSlider.maxValue = playerStats.maxHp;
                hpSlider.value = Mathf.Lerp(hpSlider.value, playerStats.currentHp, Time.deltaTime * 10f);
            }
            if (hpText) hpText.text = $"{Mathf.Ceil(playerStats.currentHp)} / {playerStats.maxHp}";

            UpdateShieldBar();

            if (staminaSlider)
            {
                staminaSlider.maxValue = playerStats.maxStamina;
                staminaSlider.value = Mathf.Lerp(staminaSlider.value, playerStats.currentStamina, Time.deltaTime * 10f);
            }
            if (staminaText) staminaText.text = $"{Mathf.Ceil(playerStats.currentStamina)} / {playerStats.maxStamina}";
            if (sinSlider)
            {
                sinSlider.maxValue = playerStats.maxSin;
                sinSlider.value = Mathf.Lerp(sinSlider.value, playerStats.currentSin, Time.deltaTime * 10f);
            }
            if (sinText) sinText.text = $"{Mathf.Ceil(playerStats.currentSin)} / {playerStats.maxSin}";
        }

        // Shield bar dùng 2 Image chồng lên hp slider fill area:
        // shieldFillImage    — fillOrigin=Left,  solid silver:   điền phần HP trống
        // shieldOverlayImage — fillOrigin=Right, semi-transparent: đè lên phần HP đầy
        // Hierarchy Unity: shieldFillImage DƯỚI hpSlider, shieldOverlayImage TRÊN hpSlider
        void UpdateShieldBar()
        {
            float shield = playerStats.currentShield;
            float curHp  = playerStats.currentHp;
            float maxHp  = playerStats.maxHp;

            bool hasShield = shield > 0f && maxHp > 0f;
            if (shieldFillImage)    shieldFillImage.gameObject.SetActive(hasShield);
            if (shieldOverlayImage) shieldOverlayImage.gameObject.SetActive(hasShield);
            if (!hasShield) return;

            float emptySpace  = Mathf.Max(0f, maxHp - curHp);
            float fillPart    = Mathf.Min(shield, emptySpace);
            float overlayPart = Mathf.Max(0f, shield - emptySpace);

            // Fill từ trái đến (curHp + fillPart)/maxHp; phần dưới hpSlider bị che, chỉ lộ shield
            if (shieldFillImage)
                shieldFillImage.fillAmount = (curHp + fillPart) / maxHp;

            // Fill từ phải sang trái, đè lên HP bar (bạc mờ, thấy máu đỏ bên dưới)
            if (shieldOverlayImage)
                shieldOverlayImage.fillAmount = overlayPart / maxHp;
        }

        void UpdateSkills()
        {
            if (skillManager == null) return;

            // --- UPDATE SKILL E ---
            // Gửi thêm chữ "E" vào tham số cuối
            UpdateSingleSkillSlot(
                skillManager.currentSkill,
                skillE_Container,
                skillE_Icon,
                skillE_CooldownFill,
                skillE_CooldownText,
                0,
                Color.white,
                "E"
            );

            // --- UPDATE SKILL Q ---
            // Gửi thêm chữ "Q" vào tham số cuối
            UpdateSingleSkillSlot(
                skillManager.currentSignature,
                skillQ_Container,
                skillQ_Icon,
                skillQ_CooldownFill,
                skillQ_CooldownText,
                playerStats.currentSin,
                notEnoughSinColor,
                "Q"
            );
        }

        // Đã thêm biến "string keyName" vào cuối để biết đang update nút nào
        void UpdateSingleSkillSlot(
            SkillData activeSkillData,
            GameObject container,
            Image icon,
            Image cooldownOverlay,
            TextMeshProUGUI centerText,
            float currentResource,
            Color dimColor,
            string keyName)
        {
            // Luôn bật container (không tắt đi nữa để luôn thấy ô trống)
            if (!container.activeSelf) container.SetActive(true);

            // ============================================
            // TRƯỜNG HỢP 1: CHƯA CÓ SKILL
            // ============================================
            if (activeSkillData == null)
            {
                icon.enabled = false; // Tắt Image Icon để tránh bị ô vuông trắng

                // Ép Cooldown Overlay đầy 100% làm lớp màng xám che kín nút trống
                cooldownOverlay.fillAmount = 1f;
                cooldownOverlay.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);

                // KHÔNG hiện chữ gì (chỉ icon + số cooldown)
                if (centerText) centerText.gameObject.SetActive(false);
                return;
            }

            // ============================================
            // TRƯỜNG HỢP 2: ĐÃ CÓ SKILL
            // ============================================
            icon.enabled = true; // Bật Image trở lại
            cooldownOverlay.color = new Color(0.4f, 0.4f, 0.4f, 0.7f); // lớp phủ XÁM khi đang hồi chiêu

            // Xử lý Icon
            if (activeSkillData.icon != null)
            {
                icon.sprite = activeSkillData.icon;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Không có icon thì để màu nền tối
            }

            // Xử lý Cooldown
            float cooldownTimer = 0f;
            SkillBehavior behavior = skillManager.GetActiveSkillBehavior(activeSkillData);

            AllyStats allyStats = playerStats != null ? playerStats.GetComponent<AllyStats>() : null;
            float flatRed = allyStats != null ? allyStats.flatSkillCooldownReduction : 0f;
            float finalCooldown = Mathf.Max(0f, activeSkillData.cooldown * (1f - playerStats.cooldownReduction) - flatRed);

            if (behavior != null)
            {
                float timeSinceLastUse = Time.time - behavior.lastUseTime;
                if (timeSinceLastUse < finalCooldown)
                {
                    cooldownTimer = finalCooldown - timeSinceLastUse;
                }
            }

            // Tránh lỗi chia cho 0 nếu skill vô tình set cooldown = 0
            float maxCooldown = finalCooldown > 0 ? finalCooldown : 1f;

            if (cooldownTimer > 0)
            {
                // -- ĐANG HỒI CHIÊU --
                cooldownOverlay.fillAmount = cooldownTimer / maxCooldown;
                centerText.text = cooldownTimer.ToString("F1");
                centerText.gameObject.SetActive(true);

                icon.color = Color.gray; // Icon tối lại
            }
            else
            {
                // -- ĐÃ HỒI XONG, SẴN SÀNG DÙNG --
                cooldownOverlay.fillAmount = 0;

                // KHÔNG hiện chữ E/Q — chỉ icon.
                if (centerText) centerText.gameObject.SetActive(false);

                // Check coi có đủ Năng lượng (Sin) để dùng không?
                float cost = activeSkillData.sinChargeReq;

                if (currentResource < cost)
                {
                    icon.color = dimColor; // Thiếu Năng lượng -> Chuyển màu tối (VD: Đỏ sẫm)
                }
                else
                {
                    if (activeSkillData.icon != null) icon.color = Color.white; // Đủ dùng -> Sáng lên
                }
            }
        }
    }
}