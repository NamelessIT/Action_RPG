using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Systems
{
    /// <summary>
    /// Sửa bố cục panel Skill Tree.
    ///
    /// Vấn đề đo được: trong <c>DetailPanel</c>, cả bốn phần tử <c>Icon</c>, <c>NameText</c>,
    /// <c>DescText</c>, <c>TypeText</c> đều nằm ở <c>anchoredPosition (0,0)</c> — tức CHỒNG
    /// ĐÚNG LÊN NHAU ở giữa. <c>BTN_Refund</c> cũng ở (0,0), đó chính là chữ "Button" trôi
    /// giữa màn hình. Ba text còn nguyên chuỗi mặc định "New Text" của Unity.
    /// Và <c>DetailPanel</c> rộng 800x450 nên phủ kín luôn cả cây skill phía sau.
    ///
    /// An toàn: <c>SkillTreeController</c> chỉ set sprite/text/active cho các phần tử này,
    /// KHÔNG bao giờ đặt vị trí chúng — nên sắp lại bố cục ở đây không đá nhau với nó.
    /// </summary>
    public static class SkillTreeLayoutRepair
    {
        /// <summary>Bề rộng thẻ chi tiết khi neo vào cạnh phải.</summary>
        public const float CardWidth = 300f;
        public const float CardHeight = 380f;
        public const float CardMargin = 20f;

        public static void Apply(GameObject panelRoot)
        {
            if (panelRoot == null) return;

            Transform detail = panelRoot.transform.Find("DetailPanel");
            if (detail != null) RepairDetailPanel((RectTransform)detail);

            Transform refund = panelRoot.transform.Find("BTN_Refund");
            if (refund != null)
            {
                AnchorToCorner((RectTransform)refund, new Vector2(1f, 0f), new Vector2(-CardMargin, CardMargin));
                RenameDefaultLabel(refund, "Hoàn điểm");
            }
        }

        /// <summary>
        /// Đổi nhãn còn nguyên chuỗi mặc định "Button" của Unity thành chữ có nghĩa.
        /// CHỈ đổi khi nhãn đúng bằng "Button" — nếu ai đó đã đặt tên thật thì không đụng.
        /// </summary>
        private static void RenameDefaultLabel(Transform target, string label)
        {
            var tmp = target.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && tmp.text == "Button") tmp.text = label;
        }

        /// <summary>
        /// Biến DetailPanel từ tấm phủ toàn màn thành THẺ neo cạnh phải, và cho các phần tử
        /// bên trong xếp dọc thay vì chồng lên nhau.
        /// </summary>
        private static void RepairDetailPanel(RectTransform detail)
        {
            // Thẻ bên phải, canh giữa theo chiều dọc — không che cây skill nữa.
            detail.anchorMin = detail.anchorMax = new Vector2(1f, 0.5f);
            detail.pivot     = new Vector2(1f, 0.5f);
            detail.sizeDelta = new Vector2(CardWidth, CardHeight);
            detail.anchoredPosition = new Vector2(-CardMargin, 0f);

            // Nền thẻ
            var bg = detail.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = null;
                bg.color  = Color.white;   // shader lo màu
                var frame = UISlotFrame.AttachTo(bg, 0.06f, 0.012f);
                if (frame != null)
                {
                    frame.fillFromSprite = false;   // nền phẳng, không lấy sprite
                    frame.glowWidth = 0.02f;
                    frame.SetColors(UIPalette.VoidRaised, UIPalette.RuneEdge,
                                    UIPalette.With(UIPalette.RuneGlow, 0.6f));
                }
            }

            // Xếp dọc thay vì chồng lên nhau ở (0,0).
            var vlg = detail.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 18, 18);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth  = true;
            vlg.childControlHeight = true;

            // Thứ tự đọc: ảnh -> tên -> loại -> mô tả.
            SetupChild(detail, "Icon",     96f,  0f, TextAlignmentOptions.Center,  0f);
            SetupChild(detail, "NameText", 34f, 22f, TextAlignmentOptions.Center,  0f);
            SetupChild(detail, "TypeText", 22f, 14f, TextAlignmentOptions.Center,  0f);
            SetupChild(detail, "DescText", 60f, 14f, TextAlignmentOptions.TopLeft, 1f);

            OrderChildren(detail, "Icon", "NameText", "TypeText", "DescText");
        }

        private static void SetupChild(RectTransform parent, string name, float minHeight,
                                       float fontSize, TextAlignmentOptions align, float flexHeight)
        {
            Transform t = parent.Find(name);
            if (t == null) return;

            var le = t.GetComponent<LayoutElement>();
            if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight      = minHeight;
            le.flexibleHeight = flexHeight;

            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize  = fontSize;
                tmp.alignment = align;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.color = name == "NameText" ? UIPalette.TextBright : UIPalette.TextMuted;

                // "New Text" là chuỗi mặc định Unity sinh ra khi tạo phần tử, chưa ai thay.
                // Xoá đi cho sạch — controller sẽ ghi đè nội dung thật khi chọn node.
                if (tmp.text == "New Text") tmp.text = string.Empty;
            }
            else
            {
                // Icon: giữ vuông
                le.minWidth = le.preferredWidth = minHeight;
                le.preferredHeight = minHeight;
            }
        }

        private static void OrderChildren(Transform parent, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = parent.Find(names[i]);
                if (t != null) t.SetSiblingIndex(i);
            }
        }

        /// <summary>Neo một phần tử vào góc, thoát khỏi cảnh nằm chết ở (0,0) giữa panel.</summary>
        private static void AnchorToCorner(RectTransform rt, Vector2 corner, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot     = corner;
            rt.anchoredPosition = offset;
        }
    }
}
