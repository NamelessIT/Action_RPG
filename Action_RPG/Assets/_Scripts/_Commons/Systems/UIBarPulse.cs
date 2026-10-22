using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulse/flash nhẹ một thanh (HP/Stamina/Sin) khi giá trị thay đổi — game feel cho HUD.
/// Component ĐỘC LẬP: gắn lên GameObject có Slider (hoặc kéo Slider/targetGraphic vào Inspector),
/// không cần sửa UIStats. Tự theo dõi Slider.value mỗi frame.
///
/// - Khi GIẢM (mất máu/tốn tài nguyên): nháy màu damage + phóng to nhẹ.
/// - Khi TĂNG (hồi phục): nháy màu heal (tùy chọn).
/// Dùng unscaled time để vẫn pulse khi hit-stop.
/// </summary>
[RequireComponent(typeof(Slider))]
public class UIBarPulse : MonoBehaviour
{
    [Header("Target (để trống = tự lấy)")]
    [Tooltip("Graphic sẽ đổi màu khi pulse. Để trống sẽ tự tìm fillRect Image của Slider.")]
    [SerializeField] private Graphic _targetGraphic;
    [Tooltip("Transform sẽ scale khi pulse. Để trống = transform của chính Slider.")]
    [SerializeField] private RectTransform _scaleTarget;

    [Header("Pulse on DECREASE (mất máu/tốn tài nguyên)")]
    [SerializeField] private bool  _pulseOnDecrease = true;
    [SerializeField] private Color _decreaseColor   = new Color(0.937f, 0.325f, 0.325f, 1f); // UIPalette.StateBad

    [Header("Pulse on INCREASE (hồi phục)")]
    [SerializeField] private bool  _pulseOnIncrease = false;
    [SerializeField] private Color _increaseColor   = new Color(0.298f, 0.831f, 0.365f, 1f); // UIPalette.StateGood

    [Header("Animation")]
    [SerializeField] private float _pulseDuration = 0.25f;
    [SerializeField] private float _scalePunch    = 0.08f;   // +8% scale lúc đỉnh
    [Tooltip("Bỏ qua thay đổi nhỏ hơn ngưỡng này (theo tỉ lệ value, tránh pulse do Lerp).")]
    [SerializeField] private float _threshold      = 0.001f;

    private Slider  _slider;
    private Color   _baseColor;
    private Vector3 _baseScale;
    private float   _lastValue;
    private float   _pulseTimer = -1f;
    private Color   _activePulseColor;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        if (_targetGraphic == null && _slider.fillRect != null)
            _targetGraphic = _slider.fillRect.GetComponent<Graphic>();
        if (_scaleTarget == null)
            _scaleTarget = transform as RectTransform;

        if (_targetGraphic != null) _baseColor = _targetGraphic.color;
        if (_scaleTarget   != null) _baseScale = _scaleTarget.localScale;
        _lastValue = _slider.value;
    }

    private void Update()
    {
        // Phát hiện thay đổi value
        float v = _slider.value;
        float maxV = Mathf.Max(0.0001f, _slider.maxValue);
        float deltaRatio = (v - _lastValue) / maxV;

        if (deltaRatio < -_threshold && _pulseOnDecrease) StartPulse(_decreaseColor);
        else if (deltaRatio > _threshold && _pulseOnIncrease) StartPulse(_increaseColor);
        _lastValue = v;

        // Chạy pulse (unscaled để vẫn mượt khi hit-stop)
        if (_pulseTimer >= 0f)
        {
            _pulseTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_pulseTimer / _pulseDuration);
            float curve = Mathf.Sin(t * Mathf.PI); // 0→1→0

            if (_targetGraphic != null)
                _targetGraphic.color = Color.Lerp(_baseColor, _activePulseColor, curve);
            if (_scaleTarget != null)
                _scaleTarget.localScale = _baseScale * (1f + _scalePunch * curve);

            if (t >= 1f)
            {
                _pulseTimer = -1f;
                if (_targetGraphic != null) _targetGraphic.color = _baseColor;
                if (_scaleTarget   != null) _scaleTarget.localScale = _baseScale;
            }
        }
    }

    private void StartPulse(Color c)
    {
        _activePulseColor = c;
        _pulseTimer = 0f;
    }
}
