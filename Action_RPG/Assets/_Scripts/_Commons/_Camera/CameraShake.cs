using UnityEngine;

/// <summary>
/// Camera shake cộng thêm offset SAU khi CameraFollow đã đặt vị trí.
/// Gắn CÙNG GameObject với CameraFollow (Main Camera).
///
/// Cách hoạt động: mỗi LateUpdate, gỡ offset frame trước rồi cộng offset shake mới.
/// Vì script này chạy SAU CameraFollow (đảm bảo bằng việc gỡ-rồi-cộng, idempotent),
/// CameraFollow.Lerp vẫn hội tụ về target đúng, shake chỉ là rung quanh đó.
///
/// Dùng unscaled time để shake vẫn mượt trong lúc hit-stop (timeScale=0).
/// </summary>
[DefaultExecutionOrder(100)] // chạy sau CameraFollow (execution order mặc định 0)
public class CameraShake : MonoBehaviour
{
    private Vector3 _appliedOffset = Vector3.zero;
    private float   _amplitude;
    private float   _duration;
    private float   _elapsed;

    /// <summary>Kích hoạt rung: biên độ (world units) trong duration giây (thực).</summary>
    public void Shake(float amplitude, float duration)
    {
        // Lấy mạnh nhất nếu đang rung dở (không cộng dồn vô hạn)
        _amplitude = Mathf.Max(_amplitude, amplitude);
        _duration  = Mathf.Max(_duration - _elapsed, duration);
        _elapsed   = 0f;
    }

    private void LateUpdate()
    {
        // 1. Gỡ offset đã cộng frame trước (để CameraFollow tính trên vị trí sạch)
        transform.position -= _appliedOffset;
        _appliedOffset = Vector3.zero;

        if (_duration <= 0f) return;

        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= _duration)
        {
            _amplitude = 0f;
            _duration  = 0f;
            _elapsed   = 0f;
            return;
        }

        // 2. Tính offset shake: giảm dần theo thời gian, nhiễu ngẫu nhiên 2 trục X/Y
        float damper = 1f - Mathf.Clamp01(_elapsed / _duration);
        float mag = _amplitude * damper;
        _appliedOffset = new Vector3(
            Random.Range(-1f, 1f) * mag,
            Random.Range(-1f, 1f) * mag,
            0f);

        // 3. Cộng vào vị trí (sau CameraFollow nhờ DefaultExecutionOrder)
        transform.position += _appliedOffset;
    }
}
