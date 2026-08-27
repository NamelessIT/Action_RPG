using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Systems
{
    /// <summary>
    /// Sửa các lỗi bố cục CỤ THỂ của panel Inventory. Khác <see cref="UILayoutTidy"/> ở chỗ
    /// đây không phải quy tắc chung — đây là những chỗ đã đo được là sai và sửa đích danh.
    ///
    /// KHÔNG đụng tới paper-doll: 7 ô TDV được đặt tay quanh Avatar (mũ trên, 3 trái, 3 phải)
    /// và đó là thiết kế CỐ Ý. Nhét layout group vào TopEquipDiv sẽ phá nó.
    /// </summary>
    public static class InventoryLayoutRepair
    {
        /// <summary>Chiều cao hàng chỉ số. Bản gốc 15px, chật tới mức phải dùng font 8pt.</summary>
        public const float RowHeight = 30f;

        /// <summary>Cỡ chữ hàng chỉ số. Bản gốc 8pt — đọc không nổi.</summary>
        public const float RowFontSize = 16f;

        /// <summary>Cạnh nút "+". Bản gốc 15px, nhỏ hơn cả vùng bấm tối thiểu nên khó click.</summary>
        public const float ButtonSize = 26f;

        /// <summary>Khe giữa hai thanh HP và EXP.</summary>
        public const float SliderGap = 8f;

        public static void Apply(GameObject panelRoot)
        {
            if (panelRoot == null) return;
            RepairStatRows(panelRoot);
            RepairPaperDollSliders(panelRoot);
        }

        /// <summary>
        /// Hàng chỉ số gốc: row 360x15, <c>Attribute</c> minWidth 50 / <c>Value</c> minWidth 200 /
        /// <c>Button</c> 15x15, cả ba căn GIỮA, chữ 8pt.
        ///
        /// Hai vấn đề: (1) 15px ép chữ xuống 8pt nên không đọc được — đây mới là thủ phạm
        /// chính, không phải màu; (2) Value chiếm 200px trong khi nhãn chỉ 50px nên con số bị
        /// đẩy trôi giữa hàng, mắt không nối được nhãn với giá trị.
        ///
        /// Sửa: nhãn giãn nở và căn TRÁI, giá trị hẹp lại và căn PHẢI — nhãn với số bám hai
        /// mép, ở giữa là khoảng trống, mắt lần theo hàng dễ hơn hẳn.
        /// </summary>
        private static void RepairStatRows(GameObject root)
        {
            foreach (var row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
            {
                if (!row.name.StartsWith("StatRow_")) continue;

                // Hàng nằm trong VerticalLayoutGroup nên chiều cao do group cha quyết định;
                // đặt sizeDelta ở đây vô nghĩa, phải qua LayoutElement.
                var rowLE = GetOrAdd<LayoutElement>(row.gameObject);
                rowLE.minHeight = rowLE.preferredHeight = RowHeight;

                row.childAlignment = TextAnchor.MiddleLeft;
                row.childForceExpandWidth = false;

                foreach (Transform child in row.transform)
                {
                    var le  = GetOrAdd<LayoutElement>(child.gameObject);
                    var tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);

                    switch (child.name)
                    {
                        case "Attribute":
                            le.minWidth = 70f;
                            le.flexibleWidth = 1f;          // giãn, đẩy Value về mép phải
                            le.minHeight = RowHeight;
                            if (tmp != null)
                            {
                                tmp.fontSize  = RowFontSize;
                                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                            }
                            break;

                        case "Value":
                            le.minWidth = 90f;
                            le.flexibleWidth = 0f;          // giữ hẹp, không trôi
                            le.minHeight = RowHeight;
                            if (tmp != null)
                            {
                                tmp.fontSize  = RowFontSize;
                                tmp.alignment = TextAlignmentOptions.MidlineRight;
                            }
                            break;

                        case "Button":
                            le.minWidth  = le.preferredWidth  = ButtonSize;
                            le.minHeight = le.preferredHeight = ButtonSize;
                            le.flexibleWidth = 0f;
                            if (tmp != null)
                            {
                                tmp.fontSize  = RowFontSize;
                                tmp.alignment = TextAlignmentOptions.Center;
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Trong <c>TopEquipDiv</c>, Slider_HP ở y=-250 và Slider_Experience ở y=-270, cả hai
        /// cao 25px với pivot 0.5 — đo ra CHỒNG NHAU đúng 5px. Xếp lại cho EXP nằm hẳn dưới HP.
        ///
        /// Idempotent: vị trí EXP luôn tính TỪ HP, mà HP không bị dịch, nên chạy bao nhiêu lần
        /// cũng ra cùng một kết quả.
        /// </summary>
        private static void RepairPaperDollSliders(GameObject root)
        {
            Transform top = FindDescendant(root.transform, "TopEquipDiv");
            if (top == null) return;

            RectTransform hp = null, ex = null;
            foreach (Transform c in top)
            {
                if (c.name == "Slider_HP")         hp = c as RectTransform;
                if (c.name == "Slider_Experience") ex = c as RectTransform;
            }
            if (hp == null || ex == null) return;

            float hpBottom = hp.anchoredPosition.y - hp.rect.height * hp.pivot.y;
            float exCenter = hpBottom - SliderGap - ex.rect.height * (1f - ex.pivot.y);

            ex.anchoredPosition = new Vector2(hp.anchoredPosition.x, exCenter);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var found = FindDescendant(c, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
