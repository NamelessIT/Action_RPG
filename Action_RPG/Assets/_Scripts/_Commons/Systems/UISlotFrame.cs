using UnityEngine;
using UnityEngine.UI;

namespace Systems
{
    /// <summary>
    /// Khung bo tròn phát sáng cho một ô UI, vẽ bằng shader <c>UI/RoundedFrame</c>.
    ///
    /// Sinh ra vì các ô skill hiện KHÔNG có phần tử viền nào — khung bo tròn mà ta thấy
    /// trong game là do art của từng icon tự vẽ. Skill nào art không có khung thì nhìn trần,
    /// và không có cách nào bắt chúng đồng bộ. Component này dựng khung THẬT, nên mọi ô
    /// trông giống nhau bất kể icon là ảnh gì.
    ///
    /// Gắn tay trong Editor cũng được, hoặc gọi <see cref="Create"/> để dựng lúc chạy
    /// (không phải sửa prefab).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UISlotFrame : MonoBehaviour
    {
        [Header("Hình dạng")]
        [Range(0f, 0.5f)] public float radius = 0.18f;
        [Range(0f, 0.25f)] public float border = 0.045f;

        [Header("Phát sáng")]
        [Range(0f, 0.3f)] public float glowWidth = 0.06f;
        [Range(0f, 4f)]   public float glowPower = 1.6f;

        [Tooltip("Bật thì viền thở theo nhịp — dùng cho ô đang sẵn sàng / được chọn.")]
        public bool pulsing = false;

        [Tooltip("Lấy chính sprite của Image làm nền, tức BO TRÒN luôn cái ảnh đó.\n" +
                 "Bật khi gắn thẳng lên icon: icon lấp kín ô nên khung dựng SAU LƯNG nó sẽ bị " +
                 "che sạch, phải để chính icon mang khung.")]
        public bool fillFromSprite = false;

        [Tooltip("Để trống thì tự Shader.Find. Khi BUILD nhớ thêm UI/RoundedFrame vào " +
                 "Project Settings > Graphics > Always Included Shaders.")]
        public Shader frameShader;

        private Image _image;
        private Material _mat;
        private RectTransform _rt;
        private float _lastAspect = -1f;

        private Color _fill   = UIPalette.VoidSunk;
        private Color _edge   = UIPalette.RuneEdge;
        private Color _glow   = UIPalette.RuneGlow;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rt    = (RectTransform)transform;

            // KHÔNG đụng raycastTarget ở đây. Awake chạy ngay bên trong AddComponent, tức là
            // TRƯỚC khi bên gọi kịp gán field, nên mọi cờ đọc ở đây đều là giá trị mặc định.
            // Ai tạo Image thì người đó tự quyết raycast (xem Create).

            Shader sh = frameShader != null ? frameShader : Shader.Find("UI/RoundedFrame");
            if (sh == null)
            {
                Debug.LogWarning("[UISlotFrame] Không tìm thấy shader UI/RoundedFrame — khung sẽ không hiện.");
                enabled = false;
                _image.enabled = false;
                return;
            }

            _mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            _image.material = _mat;
            ApplyStaticProperties();
        }

        private void OnEnable()  => UpdateAspect(true);
        private void OnRectTransformDimensionsChange() => UpdateAspect(false);

        private void Update()
        {
            UpdateAspect(false);

            if (pulsing && _mat != null)
                _mat.SetColor("_GlowColor", UIPalette.Glow(_glow, Time.unscaledTime));
        }

        private void OnDestroy()
        {
            // new Material() không tự thu hồi — huỷ tay để không rò mỗi lần load scene.
            if (_mat != null) Destroy(_mat);
        }

        /// <summary>Áp lại mọi thuộc tính lên material. Gọi sau khi đổi field từ code —
        /// bắt buộc với đường AddComponent, vì Awake đã chạy trước lúc gán field.</summary>
        public void Refresh() => ApplyStaticProperties();

        /// <summary>Đổi bộ màu của khung (vd theo bậc rarity, hoặc theo trạng thái ô).</summary>
        public void SetColors(Color fill, Color edge, Color glow)
        {
            _fill = fill; _edge = edge; _glow = glow;
            ApplyStaticProperties();
        }

        private void ApplyStaticProperties()
        {
            if (_mat == null) return;
            _mat.SetFloat("_Radius", radius);
            _mat.SetFloat("_Border", border);
            _mat.SetFloat("_GlowWidth", glowWidth);
            _mat.SetFloat("_GlowPower", glowPower);
            _mat.SetColor("_FillColor", _fill);
            _mat.SetColor("_BorderColor", _edge);
            _mat.SetColor("_GlowColor", _glow);
            _mat.SetFloat("_TextureFill", fillFromSprite ? 1f : 0f);
        }

        /// <summary>Góc bo phải tính theo tỉ lệ ô, nếu không ô chữ nhật sẽ có góc méo.</summary>
        private void UpdateAspect(bool force)
        {
            if (_mat == null || _rt == null) return;

            Rect r = _rt.rect;
            if (r.height <= 0f) return;

            float aspect = r.width / r.height;
            if (!force && Mathf.Approximately(aspect, _lastAspect)) return;

            _lastAspect = aspect;
            _mat.SetFloat("_Aspect", aspect);
        }

        /// <summary>
        /// Gắn khung THẲNG lên một Image có sẵn (icon, ảnh đại diện...) thay vì dựng
        /// GameObject riêng. Dùng khi Image đó lấp kín ô — khung dựng sau lưng nó sẽ bị che.
        /// Ảnh sẽ được bo tròn theo khung. Idempotent.
        /// </summary>
        public static UISlotFrame AttachTo(Image target, float radius = 0.18f, float border = 0.045f)
        {
            if (target == null) return null;

            var existing = target.GetComponent<UISlotFrame>();
            if (existing != null) return existing;

            var f = target.gameObject.AddComponent<UISlotFrame>();
            f.fillFromSprite = true;
            f.radius = radius;
            f.border = border;
            f.Refresh();   // Awake đã chạy với field mặc định — phải áp lại
            return f;
        }

        /// <summary>
        /// Dựng một khung phủ kín <paramref name="parent"/>. Trả về component vừa tạo, hoặc
        /// cái đã có nếu gọi lại.
        ///
        /// <paramref name="behind"/> = true: vẽ TRƯỚC mọi thứ, nên nằm sau icon — dùng cho ô
        /// trống cần nền. false: vẽ SAU CÙNG, nằm trên — dùng cho panel đã có nền riêng, khi
        /// đó nhớ để fill trong suốt (chỉ lấy viền + quầng sáng), nếu không sẽ che mất nội dung.
        /// </summary>
        public static UISlotFrame Create(RectTransform parent, string name = "Frame_Auto", bool behind = true)
        {
            if (parent == null) return null;

            // Gọi hai lần (vd sau khi load lại scene) thì trả về cái cũ, không nhân bản.
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<UISlotFrame>();

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            // Phủ kín ô cha
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (behind) rt.SetAsFirstSibling(); // vẽ trước => nằm sau icon
            else        rt.SetAsLastSibling();  // vẽ sau  => viền nổi trên nền sẵn có

            // Image này do chính hàm này tạo và chỉ để trang trí -> không được nuốt click.
            go.GetComponent<Image>().raycastTarget = false;

            var f = go.AddComponent<UISlotFrame>();
            f.Refresh();
            return f;
        }
    }
}
