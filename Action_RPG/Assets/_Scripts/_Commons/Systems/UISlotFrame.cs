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

            // Khung là trang trí — không được nuốt click của ô bên dưới.
            _image.raycastTarget = false;

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
        /// Dựng một khung phủ kín <paramref name="parent"/> và đặt xuống DƯỚI CÙNG thứ tự vẽ,
        /// nên nó nằm sau icon. Trả về component vừa tạo, hoặc cái đã có nếu gọi lại.
        /// </summary>
        public static UISlotFrame Create(RectTransform parent, string name = "Frame_Auto")
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

            rt.SetAsFirstSibling(); // vẽ trước => nằm sau icon

            return go.AddComponent<UISlotFrame>();
        }
    }
}
