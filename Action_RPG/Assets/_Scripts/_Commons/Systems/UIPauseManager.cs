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

    /// <summary>Có ít nhất 1 UI chặn đang mở (game đang pause).</summary>
    public static bool IsPaused => _locks.Count > 0;

    /// <summary>
    /// Giá trị timeScale "bình thường" nên trả về: 0 nếu UI đang pause, 1 nếu không.
    /// Slow-motion (perfect dodge / Duelist) gọi hàm này khi kết thúc thay vì cứng nhắc set 1,
    /// để không vô tình unpause khi người chơi mở UI giữa lúc slow-mo.
    /// </summary>
    public static float ResumeTimeScale => IsPaused ? 0f : 1f;

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
        Time.timeScale   = paused ? 0f : 1f;
        Cursor.visible   = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
