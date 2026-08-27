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

        /// <summary>Khoang cach tu day panel len nut Reset.</summary>
        public const float BottomMargin = 24f;

        /// <summary>Nhan cua nut hoan diem ky nang.</summary>
        public const string RefundLabel = "Reset Skill Point";

        /// <summary>
        /// Nhan THANG hai doi tuong can sap, khong tra cuu theo ten.
        ///
        /// Ban dau ham nay nhan panelRoot roi goi Find("DetailPanel") / Find("BTN_Refund").
        /// Sai: SkillTreeController KHONG nam tren SkillTreePanel ma nam tren
        /// Canvas/GameUI_MainLayout, nen Find tra ve null va toan bo ham IM LANG khong lam gi.
        /// Truyen thang tham chieu da serialize thi khong con phu thuoc vao cay phan cap.
        /// </summary>
        public static void Apply(GameObject detailPanel, Component refundButton)
        {
            if (detailPanel != null)
                RepairDetailPanel((RectTransform)detailPanel.transform);

            if (refundButton != null)
            {
                var rt = (RectTransform)refundButton.transform;
                // GIUA DAY panel, co dinh: neo (0.5, 0) va pivot (0.5, 0) nen no bam day
                // va tu can giua o moi do phan giai, khong can tinh lai toa do.
                AnchorToCorner(rt, new Vector2(0.5f, 0f), new Vector2(0f, BottomMargin));
                SetLabel(rt, RefundLabel);
            }
        }

        /// <summary>
        /// Dat nhan cho nut. Khac ban truoc: dat VO DIEU KIEN chu khong chi khi nhan dang la
        /// "Button" — vi nut nay luon la nut hoan diem, khong co truong hop nao no mang chu khac.
        /// Chinh cai dieu kien do lam nhan van la "Button" khi ham chay sau mot lan da doi.
        /// </summary>
        private static void SetLabel(Transform target, string label)
        {
            var tmp = target.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) return;

            tmp.text = label;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UIPalette.TextBright;
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
