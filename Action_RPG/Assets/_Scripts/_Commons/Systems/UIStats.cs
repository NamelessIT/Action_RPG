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
        [Tooltip("Image fill — lấp đầy phần HP còn trống (fill từ trái sang phải)")]
        public Image shieldFillImage;
        [Tooltip("Image fill mờ — đè lên HP bar khi shield vượt quá max HP (fill từ phải sang trái)")]
        public Image shieldOverlayImage;
        [Tooltip("Chữ hiện lượng khiên, vd +120. Để trống nếu không muốn hiện.")]
        public TextMeshProUGUI shieldText;
        [Tooltip("Shader UI/ShieldBar. Để trống thì tự Shader.Find lúc chạy — nhưng khi BUILD phải thêm shader này vào Project Settings > Graphics > Always Included Shaders, nếu không Unity sẽ strip mất.")]
        public Shader shieldBarShader;

        // Material riêng cho từng Image (không dùng chung, vì _Fill/_EdgeSide khác nhau).
        private Material _shieldFillMat;
        private Material _shieldOverlayMat;
        private float _lastShield = -1f;   // để bắt lúc lượng khiên đổi
        private float _shieldPulse;        // 1 -> 0, tắt dần sau mỗi lần đổi

        [Header("--- Skill E (Normal Skill - No Cost) ---")]
        public GameObject skillE_Container;
        public Image skillE_Icon;
        public Image skillE_CooldownFill;
        public TextMeshProUGUI skillE_CooldownText;
        public Color notEnoughStaminaColor = new Color(0.404f, 0.376f, 0.478f, 1f); // UIPalette.IconDimmed

        [Header("--- Skill Q (Signature/Ult - Uses Sin) ---")]
        public GameObject skillQ_Container;
        public Image skillQ_Icon;
        public Image skillQ_CooldownFill;
        public TextMeshProUGUI skillQ_CooldownText;
        public Color notEnoughSinColor = new Color(0.404f, 0.376f, 0.478f, 1f); // UIPalette.IconDimmed

        void Start()
        {
            if (playerStats == null)
                playerStats = PlayerStats.Current;

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

            SetupShieldMaterials();
            SetupSkillSlotFrames();
        }

        /// <summary>
        /// Bo tròn + viền cho 2 ô skill lúc chạy.
        ///
        /// Không ô nào có phần tử viền sẵn: khung bo tròn thấy trong game là do art của từng
        /// icon tự vẽ, nên skill nào art không có khung thì nhìn trần. Khung dựng ở đây là
        /// phần tử UI thật nên mọi skill đều giống nhau.
        ///
        /// Gắn THẲNG lên Image của icon, KHÔNG dựng khung riêng đặt sau lưng nó. Bản trước
        /// làm vậy và khung biến mất ngay khi vào Play Mode: icon lấp kín đúng 100x100 của ô
        /// nên nó che sạch khung phía sau, và ô lại trông vuông như cũ.
        /// </summary>
        private void SetupSkillSlotFrames()
        {
            AttachFrameToIcon(skillE_Icon);
            AttachFrameToIcon(skillQ_Icon);
        }

        private void AttachFrameToIcon(Image icon)
        {
            if (icon == null) return;

            var frame = UISlotFrame.AttachTo(icon);
            if (frame != null)
                frame.SetColors(Color.white, UIPalette.RuneEdge, UIPalette.RuneGlow);
        }

        /// <summary>
        /// Dựng material riêng cho 2 Image khiên. Làm lúc chạy nên KHÔNG cần tạo asset material
        /// hay sửa prefab bằng tay — chỉ cần 2 Image đã gán sẵn trong Inspector như trước.
        /// </summary>
        private void SetupShieldMaterials()
        {
            Shader sh = shieldBarShader != null ? shieldBarShader : Shader.Find("UI/ShieldBar");
            if (sh == null)
            {
                Debug.LogWarning("[UIStats] Không tìm thấy shader UI/ShieldBar — thanh khiên sẽ hiển thị phẳng như cũ. Gán shader vào ô shieldBarShader để chắc chắn.");
                return;
            }

            _shieldFillMat    = BuildShieldMaterial(sh, shieldFillImage,    true);
            _shieldOverlayMat = BuildShieldMaterial(sh, shieldOverlayImage, false);
        }

        /// <summary>edgeFromLeft = true cho dải fill từ trái, false cho dải đè fill từ phải.</summary>
        private Material BuildShieldMaterial(Shader sh, Image target, bool edgeFromLeft)
        {
            if (target == null) return null;

            var mat = new Material(sh) { hideFlags = HideFlags.DontSave };

            // Phần đè lên HP mờ hơn để còn thấy máu bên dưới.
            mat.SetColor("_Color", edgeFromLeft ? UIPalette.BarShield : UIPalette.BarShieldOver);
            mat.SetColor("_StripeColor", UIPalette.With(UIPalette.TextBright, 0.30f));
            mat.SetColor("_EdgeColor", UIPalette.Lift(UIPalette.BarShield, 0.45f));
            mat.SetFloat("_EdgeSide", edgeFromLeft ? 1f : -1f);

            target.material = mat;
            return mat;
        }

        private void OnDestroy()
        {
            // Material tạo bằng new Material() không tự thu hồi — phải huỷ tay, nếu không
            // mỗi lần load lại scene sẽ rò thêm 2 material.
            if (_shieldFillMat    != null) Destroy(_shieldFillMat);
            if (_shieldOverlayMat != null) Destroy(_shieldOverlayMat);
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
        // shieldFillImage    — fillOrigin=Left,  đặc: điền phần HP trống
        // shieldOverlayImage — fillOrigin=Right, mờ:  đè lên phần HP đầy
        // Hierarchy Unity: shieldFillImage DƯỚI hpSlider, shieldOverlayImage TRÊN hpSlider
        //
        // Màu KHÔNG đủ để phân biệt khiên với máu (đo được tương phản 2.84 giữa BarShield và
        // BarHp), nên phần nhận diện do shader UI/ShieldBar gánh: gạch chéo chạy + viền sáng
        // ở mép dẫn + nhấp nháy khi lượng khiên đổi.
        void UpdateShieldBar()
        {
            float shield = playerStats.currentShield;
            float curHp  = playerStats.currentHp;
            float maxHp  = playerStats.maxHp;

            // Nhấp nháy mỗi khi lượng khiên đổi (nhận thêm hoặc bị ăn mất).
            if (!Mathf.Approximately(shield, _lastShield))
            {
                if (_lastShield >= 0f) _shieldPulse = 1f;
                _lastShield = shield;
            }
            _shieldPulse = Mathf.MoveTowards(_shieldPulse, 0f, Time.unscaledDeltaTime * 3.2f);

            bool hasShield = shield > 0f && maxHp > 0f;
            if (shieldFillImage)    shieldFillImage.gameObject.SetActive(hasShield);
            if (shieldOverlayImage) shieldOverlayImage.gameObject.SetActive(hasShield);

            if (shieldText)
            {
                shieldText.gameObject.SetActive(hasShield);
                if (hasShield)
                {
                    shieldText.text  = "+" + Mathf.Ceil(shield);
                    shieldText.color = UIPalette.Lift(UIPalette.BarShield, _shieldPulse * 0.6f);
                }
            }

            if (!hasShield) return;

            float emptySpace  = Mathf.Max(0f, maxHp - curHp);
            float fillPart    = Mathf.Min(shield, emptySpace);
            float overlayPart = Mathf.Max(0f, shield - emptySpace);

            // Fill từ trái đến (curHp + fillPart)/maxHp; phần dưới hpSlider bị che, chỉ lộ shield
            float fillAmount = (curHp + fillPart) / maxHp;
            if (shieldFillImage) shieldFillImage.fillAmount = fillAmount;
            PushShieldShaderState(_shieldFillMat, fillAmount);

            // Fill từ phải sang trái, đè lên HP bar (thấy máu đỏ bên dưới)
            float overlayAmount = overlayPart / maxHp;
            if (shieldOverlayImage) shieldOverlayImage.fillAmount = overlayAmount;
            PushShieldShaderState(_shieldOverlayMat, overlayAmount);
        }

        private void PushShieldShaderState(Material mat, float fillAmount)
        {
            if (mat == null) return;
            mat.SetFloat("_Fill", fillAmount);
            mat.SetFloat("_Pulse", _shieldPulse);
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
                cooldownOverlay.color = UIPalette.CooldownVeil;

                // KHÔNG hiện chữ gì (chỉ icon + số cooldown)
                if (centerText) centerText.gameObject.SetActive(false);
                return;
            }

            // ============================================
            // TRƯỜNG HỢP 2: ĐÃ CÓ SKILL
            // ============================================
            icon.enabled = true; // Bật Image trở lại
            cooldownOverlay.color = UIPalette.CooldownVeil; // lớp phủ tối khi đang hồi chiêu

            // Xử lý Icon
            if (activeSkillData.icon != null)
            {
                icon.sprite = activeSkillData.icon;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = UIPalette.VoidSunk; // Không có icon thì để màu nền tối
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

                icon.color = UIPalette.IconDimmed; // Icon tối lại
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