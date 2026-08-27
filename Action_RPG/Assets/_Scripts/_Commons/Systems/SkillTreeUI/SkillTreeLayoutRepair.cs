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

        /// <summary>Nhan cua nut hoan diem ky nang.</summary>
        public const string RefundLabel = "Refund";

        /// <summary>Chieu cao dai duoi cung danh RIENG cho nut Refund.
        /// Vung cuon se bi cat bot dung bang nay de nut khong de len node skill.</summary>
        public const float BottomStripHeight = 56f;

        /// <summary>Le hai ben va le tren cua vung cuon so voi panel.</summary>
        public const float ScrollInset = 25f;

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
            RectTransform panelRoot = null;

            if (detailPanel != null)
            {
                panelRoot = detailPanel.transform.parent as RectTransform;
                RepairDetailPanel((RectTransform)detailPanel.transform);
            }

            // O _refundButton trong Inspector hay bi bo TRONG (da gap that: no null nen ca
            // khoi duoi day bi bo qua, nut giu nguyen anchor goc (0.5,0.5) tuc giua panel —
            // ma panel stretch full man hinh nen nut "troi giua man hinh").
            // Tu tim lay de khong phu thuoc vao viec ai do co keo dung o hay khong.
            RectTransform rt = refundButton != null
                ? (RectTransform)refundButton.transform
                : FindRefundButton(panelRoot);

            if (rt != null)
            {
                ReserveBottomStrip(panelRoot ?? rt.parent as RectTransform);

                // Dat nut vao GIUA dai duoi cung. Neo (0.5, 0) + pivot (0.5, 0.5) nen no bam
                // day panel va tu can giua o moi do phan giai, khong phai tinh lai toa do.
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, BottomStripHeight * 0.5f);

                SetLabel(rt, RefundLabel);
            }
        }

        /// <summary>
        /// Tim nut Refund khi tham chieu serialize bi bo trong.
        ///
        /// Chi xet CON TRUC TIEP cua panel root, va bo qua nhanh Scroll View — moi Button ben
        /// trong Scroll View la nut cua tung node skill (Unlock/Equip), khong phai nut Refund.
        /// </summary>
        private static RectTransform FindRefundButton(RectTransform panelRoot)
        {
            if (panelRoot == null) return null;

            foreach (Transform child in panelRoot)
            {
                if (child.GetComponent<ScrollRect>() != null) continue;   // vung cuon -> bo qua
                if (child.GetComponent<Button>() != null) return child as RectTransform;
            }
            return null;
        }

        /// <summary>
        /// Cat bot day vung cuon de chua cho cho nut Refund.
        ///
        /// Vi sao phai lam: panel cao 450, Scroll View cao 400 nam giua -> chi con 25px duoi
        /// day scroll, ma nut cao 30. Khong du cho. Neu chi neo nut vao day panel thi no se
        /// DE LEN node skill cuoi cung. Phai chua han mot dai rieng.
        ///
        /// Tim Scroll View bang KIEU (ScrollRect) chu khong bang TEN — tim theo ten da mot lan
        /// lam ca ham nay im lang khong chay.
        ///
        /// Idempotent: offset dat bang HANG SO tuyet doi, khong cong don theo trang thai hien
        /// tai, nen chay bao nhieu lan cung ra cung ket qua.
        /// </summary>
        private static void ReserveBottomStrip(RectTransform panelRoot)
        {
            if (panelRoot == null) return;

            var scroll = panelRoot.GetComponentInChildren<ScrollRect>(true);
            if (scroll == null) return;

            var srt = (RectTransform)scroll.transform;

            // Gian kin panel roi thut vao: hai ben + tren giu nguyen le cu, rieng day chua
            // dung BottomStripHeight cho nut.
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.pivot     = new Vector2(0.5f, 0.5f);
            srt.offsetMin = new Vector2(ScrollInset, BottomStripHeight);
            srt.offsetMax = new Vector2(-ScrollInset, -ScrollInset);
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
