using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nguồn chân lý DUY NHẤT cho việc pause game khi mở UI chặn (Inventory / SkillTree / DevTool...).
///
/// Vấn đề cũ: mỗi panel tự set Time.timeScale = 0/1 độc lập. Nếu mở 2 panel rồi đóng 1,
/// panel vừa đóng sẽ set timeScale = 1 trong khi panel kia vẫn mở → game chạy lại ngoài ý muốn.
///
/// Giải pháp: mỗi panel "đăng ký" 1 khóa (lock) theo tên khi mở và gỡ khi đóng.
/// Game chỉ chạy lại (timeScale = 1) khi KHÔNG còn khóa nào. Dùng HashSet nên idempotent:
/// gọi SetLock trùng lặp không gây lệch số đếm.
/// </summary>
public static class UIPauseManager
{
    private static readonly HashSet<string> _locks = new HashSet<string>();

    /// <summary>Giá trị fixedDeltaTime mặc định của Unity (60 physics step/giây).</summary>
    public const float DefaultFixedDeltaTime = 0.02f;

    /// <summary>
    /// Tốc độ game khi KHÔNG bị UI pause (mặc định 1). DevTool đổi qua đây (0.5x / 1x / 2x).
    /// Khi có UI lock → effective timeScale = 0; hết lock → quay về giá trị này.
    /// </summary>
    public static float GameplayTimeScale { get; private set; } = 1f;

    /// <summary>Có ít nhất 1 UI chặn đang mở (game đang pause).</summary>
    public static bool IsPaused => _locks.Count > 0;

    /// <summary>
    /// Giá trị timeScale "bình thường" nên trả về: 0 nếu UI đang pause, ngược lại GameplayTimeScale.
    /// Slow-motion (perfect dodge / Duelist) gọi hàm này khi kết thúc thay vì cứng nhắc set 1,
    /// để không (a) unpause khi UI đang mở, (b) ép về 1 khi dev đang chỉnh 2x/0.5x.
    /// </summary>
    public static float ResumeTimeScale => IsPaused ? 0f : GameplayTimeScale;

    /// <summary>
    /// fixedDeltaTime nên dùng khi kết thúc slow-motion: scale theo GameplayTimeScale để vật lý
    /// khớp tốc độ game (vd 2x → physics nhanh tương ứng). Slow-mo dùng thay cho hard-code 0.02f.
    /// </summary>
    public static float ResumeFixedDeltaTime => DefaultFixedDeltaTime * GameplayTimeScale;

    /// <summary>Đổi tốc độ gameplay (DevTool). Áp ngay nếu không có UI lock; nếu đang pause thì
    /// giữ pause, đổi sẽ có hiệu lực khi đóng UI.</summary>
    public static void SetGameplayTimeScale(float scale)
    {
        GameplayTimeScale = Mathf.Clamp(scale, 0.05f, 8f);
        Apply();
        Debug.Log($"[UIPauseManager] GameplayTimeScale = {GameplayTimeScale:0.##}x" +
                  (IsPaused ? " (đang pause bởi UI, sẽ áp khi đóng)" : ""));
    }

    /// <summary>Bật/tắt khóa pause cho 1 panel. key nên là hằng định danh ("Inventory", "SkillTree", "DevTool"...).</summary>
    public static void SetLock(string key, bool locked)
    {
        if (string.IsNullOrEmpty(key)) return;
        bool changed = locked ? _locks.Add(key) : _locks.Remove(key);
        if (changed) Apply();
    }

    /// <summary>Gỡ sạch mọi khóa và trả game về bình thường. Dùng khi reload scene / reset.</summary>
    public static void ClearAll()
    {
        if (_locks.Count == 0)
        {
            // Vẫn ép trạng thái chuẩn phòng khi timeScale bị lệch từ nơi khác.
            Apply();
            return;
        }
        _locks.Clear();
        Apply();
    }

    private static void Apply()
    {
        bool paused = _locks.Count > 0;
        Time.timeScale     = paused ? 0f : GameplayTimeScale; // hết lock → tốc độ dev đã chọn (mặc định 1)
        // Pause (timeScale=0) thì giữ fixedDeltaTime mặc định; khi chạy thì scale theo tốc độ game.
        Time.fixedDeltaTime = paused ? DefaultFixedDeltaTime : ResumeFixedDeltaTime;
        Cursor.visible     = paused;
        Cursor.lockState   = paused ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
