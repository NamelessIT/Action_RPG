using UnityEngine;
using UnityEngine.UI;

namespace Systems
{
    /// <summary>
    /// Nới khoảng cách cho các layout group đang bị dí sát nhau.
    ///
    /// Khảo sát scene cho thấy GẦN NHƯ MỌI layout group trong project đều để
    /// <c>spacing = 0</c>: hàng stat là <c>Attribute | Value | Button</c> dính liền,
    /// các div xếp chồng không có khe. Đó là nguyên nhân chính khiến giao diện trông
    /// bí và rẻ tiền, chứ không phải màu.
    ///
    /// HAI RÀNG BUỘC AN TOÀN — cố ý, đừng gỡ:
    ///  1. CHỈ điền vào chỗ đang bằng 0. Giá trị bạn đã cố ý đặt không bị đè.
    ///  2. KHÔNG đụng tới kích thước, vị trí, anchor, pivot hay cellSize. Các phần tử
    ///     đặt tay vẫn nằm nguyên chỗ cũ; chỉ khe hở giữa chúng rộng ra.
    /// Nhờ vậy áp nhầm cũng không phá được bố cục — cùng lắm là chưa đẹp.
    /// </summary>
    public static class UILayoutTidy
    {
        /// <summary>Khe hở chuẩn giữa các phần tử cùng nhóm.</summary>
        public const int Gap = 8;

        /// <summary>Khe hở hẹp — cho hàng dày đặc như hàng chỉ số.</summary>
        public const int GapTight = 6;

        /// <summary>Lề trong của panel.</summary>
        public const int Pad = 16;

        /// <summary>
        /// Nới mọi layout group dưới <paramref name="root"/>.
        /// </summary>
        /// <param name="gap">Khe hở điền vào các group đang để 0.</param>
        /// <param name="padRoot">Lề trong cho group nằm NGAY TRÊN root (nếu đang là 0).
        /// Group con không đụng tới, vì lề lồng nhau sẽ cộng dồn thành thừa.</param>
        public static void Apply(GameObject root, int gap = Gap, int padRoot = Pad)
        {
            if (root == null) return;

            foreach (var g in root.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
            {
                if (Mathf.Approximately(g.spacing, 0f))
                    g.spacing = gap;

                if (g.transform == root.transform && IsZeroPadding(g.padding))
                    SetPadding(g.padding, padRoot);
            }

            foreach (var g in root.GetComponentsInChildren<GridLayoutGroup>(true))
            {
                if (g.spacing.x <= 0.01f && g.spacing.y <= 0.01f)
                    g.spacing = new Vector2(gap, gap);

                if (g.transform == root.transform && IsZeroPadding(g.padding))
                    SetPadding(g.padding, padRoot);
            }
        }

        /// <summary>
        /// Nới riêng một nhóm hàng dày đặc (vd các <c>StatRow_*</c>): khe hẹp hơn,
        /// và căn giữa theo chiều dọc để nhãn / số / nút thẳng hàng nhau.
        /// </summary>
        public static void ApplyRows(GameObject root, int gap = GapTight)
        {
            if (root == null) return;

            foreach (var g in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
            {
                if (Mathf.Approximately(g.spacing, 0f))
                    g.spacing = gap;

                // Nhãn, số và nút trong cùng một hàng cao thấp khác nhau; căn giữa
                // để chúng nằm trên cùng một đường.
                if (g.childAlignment == TextAnchor.UpperLeft)
                    g.childAlignment = TextAnchor.MiddleLeft;
            }
        }

        private static bool IsZeroPadding(RectOffset p)
            => p != null && p.left == 0 && p.right == 0 && p.top == 0 && p.bottom == 0;

        private static void SetPadding(RectOffset p, int v)
        {
            if (p == null) return;
            p.left = p.right = p.top = p.bottom = v;
        }
    }
}
