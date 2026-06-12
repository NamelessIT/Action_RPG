using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Trung tâm "game feel" khi đánh nhau: hit-stop (đứng hình ngắn) + camera shake.
/// Singleton tự tạo, gọi từ bất cứ đâu qua API tĩnh:
///   CombatFeel.OnHit(HitStrength.Normal);
///
/// CAMERA SHAKE — quan trọng:
///  - Main Camera có CinemachineBrain → nó GHI ĐÈ transform mỗi frame. Vì vậy CameraShake
///    manual (set transform.position) sẽ KHÔNG hiện. Ta ưu tiên CinemachineImpulseSource
///    (Cinemachine-friendly): CinemachineImpulseListener trên CM vcam sẽ nhận và rung.
///  - Nếu không tìm thấy ImpulseSource → fallback CameraShake manual (cho project không Cinemachine).
///
/// Thiết kế an toàn:
///  - Hit-stop dùng unscaled time, có CAP độ dài + COOLDOWN để không spam đứng hình.
///  - Hit-stop tôn trọng UIPauseManager: nếu UI đang mở (timeScale=0) thì KHÔNG can thiệp.
///  - Toàn bộ thông số chỉnh được qua Inspector; tắt enableHitStop/enableShake để vô hiệu.
/// </summary>
public class CombatFeel : MonoBehaviour
{
    public enum HitStrength { Normal, Heavy, Crit, Kill }

    public static CombatFeel Instance { get; private set; }

    [Header("Bật/Tắt")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField] private bool enableShake   = true;
    [Tooltip("Bật để log strength + shake amp + hit-stop dur mỗi lần OnHit (debug combat feel).")]
    [SerializeField] private bool debugLogs     = false;

    [Header("Hit-Stop (giây, unscaled)")]
    [SerializeField] private float hitStopNormal = 0.0f;    // đòn thường: mặc định không đứng hình
    [SerializeField] private float hitStopHeavy  = 0.05f;
    [SerializeField] private float hitStopCrit   = 0.07f;
    [SerializeField] private float hitStopKill   = 0.12f;
    [Tooltip("Độ dài hit-stop tối đa cho 1 lần, tránh kẹt quá lâu.")]
    [SerializeField] private float hitStopMaxDuration = 0.15f;
    [Tooltip("Khoảng cách tối thiểu giữa 2 lần hit-stop (giây thực).")]
    [SerializeField] private float hitStopCooldown = 0.04f;

    [Header("Camera Shake (biên độ)")]
    [Tooltip("Khi dùng Cinemachine Impulse, đây là 'force' truyền vào GenerateImpulseWithForce.\n" +
             "Khi fallback CameraShake manual, đây là biên độ world units.")]
    [SerializeField] private float shakeNormal = 0.15f;
    [SerializeField] private float shakeHeavy  = 0.30f;
    [SerializeField] private float shakeCrit   = 0.40f;
    [SerializeField] private float shakeKill   = 0.55f;
    [SerializeField] private float shakeDuration = 0.18f; // chỉ dùng cho CameraShake fallback
    [Tooltip("Nhân chung cho cường độ rung (giảm nếu thấy quá khó chịu).")]
    [SerializeField] private float shakeIntensityMultiplier = 1f;
    [Tooltip("Kéo CinemachineImpulseSource vào đây (ưu tiên). Để trống sẽ tự tìm trong scene.")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    private CameraShake _cameraShake;
    private float _lastHitStopTime = -999f;
    private bool  _hitStopRunning;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_impulseSource == null) _impulseSource = FindFirstObjectByType<CinemachineImpulseSource>();
        _cameraShake = FindFirstObjectByType<CameraShake>();
    }

    /// <summary>API tĩnh: gọi khi gây 1 cú đánh có sức mạnh tương ứng. targetName chỉ để debug log.</summary>
    public static void OnHit(HitStrength strength, string targetName = null)
    {
        if (Instance == null) return;
        Instance.DoHit(strength, targetName);
    }

    private void DoHit(HitStrength strength, string targetName)
    {
        if (debugLogs)
        {
            float amp = ShakeFor(strength);
            float dur = strength switch { HitStrength.Heavy => hitStopHeavy, HitStrength.Crit => hitStopCrit, HitStrength.Kill => hitStopKill, _ => hitStopNormal };
            string via = _impulseSource != null ? "Impulse" : (_cameraShake != null ? "CameraShake" : "NONE");
            Debug.Log($"[CombatFeel] OnHit type={strength} target={targetName ?? "?"} shake={amp:F2}({via}) hitStop={dur:F2}s");
        }
        if (enableHitStop) TriggerHitStop(strength);
        if (enableShake)   TriggerShake(strength);
    }

    private float ShakeFor(HitStrength s) =>
        (s switch { HitStrength.Heavy => shakeHeavy, HitStrength.Crit => shakeCrit, HitStrength.Kill => shakeKill, _ => shakeNormal })
        * Mathf.Max(0f, shakeIntensityMultiplier);

    // ── HIT STOP ────────────────────────────────────────────────────────────
    private void TriggerHitStop(HitStrength strength)
    {
        float dur = strength switch
        {
            HitStrength.Heavy => hitStopHeavy,
            HitStrength.Crit  => hitStopCrit,
            HitStrength.Kill  => hitStopKill,
            _                 => hitStopNormal,
        };
        if (dur <= 0f) return;
        dur = Mathf.Min(dur, hitStopMaxDuration);

        // Cooldown chống spam
        if (Time.unscaledTime - _lastHitStopTime < hitStopCooldown) return;
        // Không can thiệp khi đang pause bởi UI (Inventory/SkillTree/DevTool)
        if (UIPauseManager.IsPaused) return;
        if (_hitStopRunning) return;

        _lastHitStopTime = Time.unscaledTime;
        StartCoroutine(HitStopRoutine(dur));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        _hitStopRunning = true;
        float prevScale = Time.timeScale;
        // Chỉ đứng hình nếu game đang chạy bình thường (timeScale > 0).
        // Nếu đang slow-motion (vd 0.02) thì prevScale < 1 — ta KHÔI PHỤC ĐÚNG prevScale,
        // không ép 1f (tránh phá slow-mo của Perfect Dodge / DuelistSignature).
        if (prevScale > 0f)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            // Tôn trọng UIPauseManager: nếu UI vừa mở trong lúc đứng hình thì giữ pause (0);
            // ngược lại trả về ĐÚNG giá trị trước đó (1f bình thường, hoặc slow-mo factor).
            Time.timeScale = UIPauseManager.IsPaused ? 0f : prevScale;
        }
        _hitStopRunning = false;
    }

    // ── CAMERA SHAKE ────────────────────────────────────────────────────────
    private void TriggerShake(HitStrength strength)
    {
        float amp = ShakeFor(strength);
        if (amp <= 0f) return;

        // Ưu tiên Cinemachine Impulse (vì CinemachineBrain ghi đè transform → CameraShake manual vô hiệu).
        if (_impulseSource == null) _impulseSource = FindFirstObjectByType<CinemachineImpulseSource>();
        if (_impulseSource != null)
        {
            _impulseSource.GenerateImpulseWithForce(amp);
            return;
        }

        // Fallback: CameraShake manual (project không có Cinemachine).
        if (_cameraShake == null) _cameraShake = FindFirstObjectByType<CameraShake>();
        if (_cameraShake != null) _cameraShake.Shake(amp, shakeDuration);
    }
}
