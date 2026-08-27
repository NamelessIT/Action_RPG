using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Systems
{
    /// <summary>
    /// Khoác lại toàn bộ control uGUI mặc định dưới một panel theo tông arcane của
    /// <see cref="UIPalette"/>. Chạy LÚC RUNTIME, đi qua cây con và chỉnh màu/material —
    /// không sửa prefab, không xoá gì, không thêm/bớt phần tử bố cục.
    ///
    /// Sinh ra vì toàn bộ UI hiện là control mặc định của Unity (nút trắng chữ đen).
    /// Sửa tay từng nút trong Inspector vừa lâu vừa chắc chắn lệch nhau; làm ở đây thì
    /// mọi panel dùng chung một bộ quy tắc.
    ///
    /// Có thể gắn tay vào panel trong Editor, hoặc gọi <see cref="Apply"/> từ code.
    /// Idempotent — gọi lại nhiều lần không nhân đôi gì.
    /// </summary>
    public class ArcaneUISkin : MonoBehaviour
    {
        [Header("Khoác cho loại nào")]
        public bool skinButtons   = true;
        public bool skinDropdowns = true;
        public bool skinToggles   = true;
        public bool skinScrollbars = true;
        public bool skinTexts     = true;

        [Tooltip("Bọc panel gốc bằng khung bo tròn arcane.")]
        public bool framePanelRoot = true;

        [Tooltip("Bo góc cho nút. 0 = vuông.")]
        [Range(0f, 0.5f)] public float buttonRadius = 0.22f;

        [Tooltip("Áp lại mỗi lần panel được bật. Tắt nếu panel sinh nút động và tự gọi Apply.")]
        public bool applyOnEnable = true;

        private bool _applied;

        private void Start() => Apply();

        private void OnEnable()
        {
            if (applyOnEnable && _applied) Apply();
        }

        /// <summary>Khoác lại mọi control dưới GameObject này.</summary>
        public void Apply()
        {
            Apply(gameObject, this);
            _applied = true;
        }

        /// <summary>
        /// Khoác lại mọi control dưới <paramref name="root"/>. Gọi được từ bất kỳ đâu mà
        /// không cần gắn component (vd DevToolPanel tự gọi trong Awake).
        /// </summary>
        public static void Apply(GameObject root, ArcaneUISkin opt = null)
        {
            if (root == null) return;

            bool doButtons    = opt == null || opt.skinButtons;
            bool doDropdowns  = opt == null || opt.skinDropdowns;
            bool doToggles    = opt == null || opt.skinToggles;
            bool doScrollbars = opt == null || opt.skinScrollbars;
            bool doTexts      = opt == null || opt.skinTexts;
            bool doFrame      = opt == null || opt.framePanelRoot;
            float radius      = opt != null ? opt.buttonRadius : 0.22f;

            if (doFrame)
            {
                var rt = root.transform as RectTransform;
                if (rt != null)
                {
                    // behind:false — panel thường ĐÃ CÓ nền riêng, đặt khung ra sau nền thì
                    // nó vô hình. Vẽ đè lên trên, và để fill TRONG SUỐT để chỉ lấy viền +
                    // quầng sáng chứ không che nội dung.
                    var frame = UISlotFrame.Create(rt, "Frame_PanelAuto", false);
                    if (frame != null)
                    {
                        frame.radius    = 0.035f;   // panel to nên bo nhẹ thôi
                        frame.border    = 0.004f;
                        frame.glowWidth = 0.012f;
                        frame.SetColors(UIPalette.With(UIPalette.VoidPanel, 0f),
                                        UIPalette.RuneEdge,
                                        UIPalette.With(UIPalette.RuneGlow, 0.55f));
                    }
                }
            }

            // THỨ TỰ QUAN TRỌNG: tô chữ TRƯỚC, rồi để các handler chuyên trách ghi đè nhãn
            // của riêng chúng.
            //
            // Bản đầu làm ngược lại (chữ sau cùng) nên phải đánh dấu "text này đã xử lý rồi"
            // bằng một marker component gắn lên TỪNG text — đo được 360 component chỉ để làm
            // cờ. Đảo thứ tự cho ra đúng kết quả đó mà không cần marker nào.
            if (doTexts)
                foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true)) SkinLooseText(t);

            if (doButtons)
                foreach (var b in root.GetComponentsInChildren<Button>(true)) SkinButton(b, radius);

            if (doDropdowns)
                foreach (var d in root.GetComponentsInChildren<TMP_Dropdown>(true)) SkinDropdown(d, radius);

            if (doToggles)
                foreach (var t in root.GetComponentsInChildren<Toggle>(true)) SkinToggle(t);

            if (doScrollbars)
                foreach (var s in root.GetComponentsInChildren<Scrollbar>(true)) SkinScrollbar(s);
        }

        // ── Nút ──────────────────────────────────────────────────────────────
        private static void SkinButton(Button btn, float radius)
        {
            if (btn == null) return;

            var img = btn.targetGraphic as Image;
            if (img != null)
            {
                ApplyFrameMaterial(img, radius, UIPalette.VoidRaised, UIPalette.RuneEdge, UIPalette.ArcaneCore);
                img.color = Color.white; // shader lo màu; vertex color chỉ dùng cho state bên dưới
            }

            // Shader nhân với vertex color, nên state được diễn tả bằng độ sáng.
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor      = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor     = new Color(0.58f, 0.58f, 0.58f, 1f);
            cb.selectedColor    = new Color(0.94f, 0.94f, 0.94f, 1f);
            cb.disabledColor    = new Color(0.40f, 0.40f, 0.40f, 0.55f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;

            foreach (var label in btn.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.color = UIPalette.TextBright;
                FitLabel(label);
            }
        }

        /// <summary>
        /// Nhãn nút không được xuống dòng. Tab DevTool rộng 82px mà chữ "Companion Equipment"
        /// 16pt thì TMP tự ngắt thành "Companio / n Equipmen / t" — đọc rất khó chịu.
        /// Cách xử lý: cấm ngắt dòng rồi bật auto-size để chữ TỰ CO cho vừa bề ngang.
        /// Nhãn ngắn giữ nguyên cỡ, chỉ nhãn dài mới nhỏ lại.
        /// </summary>
        private static void FitLabel(TextMeshProUGUI label)
        {
            if (label == null) return;

            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode     = TextOverflowModes.Ellipsis;

            // Chốt cỡ hiện tại làm TRẦN trước khi bật auto-size — sau khi bật,
            // fontSize trở thành giá trị do TMP tính nên đọc sau sẽ không còn đúng.
            if (!label.enableAutoSizing)
            {
                label.fontSizeMax     = label.fontSize;
                label.fontSizeMin     = Mathf.Max(8f, label.fontSize * 0.55f);
                label.enableAutoSizing = true;
            }
        }

        // ── Dropdown ─────────────────────────────────────────────────────────
        private static void SkinDropdown(TMP_Dropdown dd, float radius)
        {
            if (dd == null) return;

            var img = dd.targetGraphic as Image;
            if (img != null)
            {
                ApplyFrameMaterial(img, radius, UIPalette.VoidSunk, UIPalette.RuneEdge, UIPalette.ArcaneCore);
                img.color = Color.white;
            }

            if (dd.captionText != null) dd.captionText.color = UIPalette.TextBright;
            if (dd.itemText    != null) dd.itemText.color    = UIPalette.TextBright;

            // Nền danh sách xổ xuống (template chỉ tồn tại khi dropdown mở, nên chỉnh cả template gốc).
            if (dd.template != null)
            {
                var tplImg = dd.template.GetComponent<Image>();
                if (tplImg != null)
                {
                    tplImg.material = null;      // danh sách dài -> để phẳng cho dễ đọc
                    tplImg.color    = UIPalette.VoidRaised;
                }
            }
        }

        // ── Toggle ───────────────────────────────────────────────────────────
        private static void SkinToggle(Toggle tg)
        {
            if (tg == null) return;

            if (tg.targetGraphic is Image box)
            {
                box.material = null;
                box.color    = UIPalette.VoidSunk;
            }
            if (tg.graphic is Image check)
            {
                check.material = null;
                check.color    = UIPalette.ArcaneCore;
            }
            foreach (var label in tg.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.color = UIPalette.TextMuted;
            }
        }

        // ── Scrollbar ────────────────────────────────────────────────────────
        private static void SkinScrollbar(Scrollbar sb)
        {
            if (sb == null) return;

            var track = sb.GetComponent<Image>();
            if (track != null) { track.material = null; track.color = UIPalette.BarTrack; }

            if (sb.targetGraphic is Image handle)
            {
                handle.material = null;
                handle.color    = UIPalette.RuneEdge;
            }
        }

        // ── Chữ chưa được hàm nào phụ trách ──────────────────────────────────
        private static void SkinLooseText(TextMeshProUGUI t)
        {
            if (t == null) return;

            // Phân vai theo cỡ chữ. CHỈ hai bậc, và bậc thấp nhất vẫn phải đọc được.
            //
            // Bản đầu có bậc thứ ba dùng TextDim cho chữ < 18pt — sai. TextDim đo được
            // tương phản 2.73 trên nền panel, dưới xa ngưỡng 4.5, nên mọi nhãn nhỏ
            // (STR/DEX/INT..., tên chỉ số) thành tím mờ trên nền đen, không đọc nổi.
            // TextDim chỉ dành cho chữ trang trí mà người chơi không cần đọc, và
            // skinner quét đại trà thì không thể biết chữ nào là loại đó.
            float size = t.fontSize;
            t.color = size >= 28f ? UIPalette.TextBright   // tương phản 15.5
                                  : UIPalette.TextMuted;   // tương phản 6.19
        }

        // ── Hạ tầng ──────────────────────────────────────────────────────────

        // Material DÙNG CHUNG theo hình dạng. Một panel dev có tới 55 nút + 16 dropdown;
        // nếu mỗi Image một material riêng thì sinh ~71 material, mỗi cái phá một batch.
        // Các nút cùng kích thước thì hình dạng y hệt nhau, nên gộp được.
        // Khoá gồm cả bộ màu vì nút và dropdown dùng nền khác nhau.
        private static readonly System.Collections.Generic.Dictionary<string, Material> _frameMats
            = new System.Collections.Generic.Dictionary<string, Material>();

        private static void ApplyFrameMaterial(Image img, float radius, Color fill, Color edge, Color glow)
        {
            if (img == null) return;

            Shader sh = Shader.Find("UI/RoundedFrame");
            if (sh == null) return;

            var rt = img.rectTransform;
            float aspect = (rt != null && rt.rect.height > 0f) ? rt.rect.width / rt.rect.height : 1f;

            // Làm tròn aspect về 2 chữ số: chênh lệch nhỏ hơn thế mắt không thấy, mà gộp
            // được rất nhiều. 71 material rút xuống còn một nhúm.
            float aspectKey = Mathf.Round(aspect * 100f) / 100f;

            string key = $"{aspectKey}|{radius}|{ColorUtility.ToHtmlStringRGBA(fill)}"
                       + $"|{ColorUtility.ToHtmlStringRGBA(edge)}|{ColorUtility.ToHtmlStringRGBA(glow)}";

            if (!_frameMats.TryGetValue(key, out var mat) || mat == null)
            {
                mat = new Material(sh) { hideFlags = HideFlags.DontSave };
                mat.SetFloat("_Radius", radius);
                mat.SetFloat("_Border", 0.03f);
                mat.SetFloat("_Aspect", aspectKey);
                mat.SetFloat("_GlowWidth", 0.02f);
                mat.SetFloat("_GlowPower", 1.4f);
                mat.SetColor("_FillColor", fill);
                mat.SetColor("_BorderColor", edge);
                mat.SetColor("_GlowColor", glow);
                _frameMats[key] = mat;
            }

            img.material = mat;
        }

        /// <summary>Số material khung đang dùng chung — để soi lúc gỡ lỗi hiệu năng.</summary>
        public static int SharedFrameMaterialCount => _frameMats.Count;

    }
}
