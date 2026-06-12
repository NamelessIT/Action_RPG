using System.Collections;
using UnityEngine;

/// <summary>
/// Contract cho tất cả attack handler theo loại vũ khí.
///
/// Có 2 loại handler:
/// • Standard: ExecuteSwing() — coroutine chạy trong swing window của AttackRoutine.
/// • Channeled: IsChanneled=true — bắt đầu khi chargeTimer đủ, dừng khi thả chuột.
///   PlayerController gọi StartChanneled() / StopChanneled() trực tiếp.
/// </summary>
public interface IWeaponAttackHandler
{
    // ── Standard Attack ───────────────────────────────────────────

    /// <summary>
    /// Thực hiện logic gây damage trong cửa sổ swing (SwingDuration).
    /// Được gọi từ PlayerController.AttackRoutine() sau wind-up delay.
    /// Coroutine này KHÔNG cần tự chờ recovery — AttackRoutine lo phần đó.
    /// </summary>
    IEnumerator ExecuteSwing(WeaponAttackContext ctx);

    // ── Channeled Heavy ───────────────────────────────────────────

    /// <summary>True nếu heavy attack là loại giữ liên tục (channeled).</summary>
    bool IsChanneled { get; }

    /// <summary>
    /// Bắt đầu channeled attack. Chỉ gọi khi IsChanneled = true.
    /// Coroutine tự vòng lặp cho đến khi StopChanneled() được gọi.
    /// </summary>
    Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner);

    /// <summary>Dừng channeled attack (thường gọi khi thả chuột).</summary>
    void StopChanneled();
}
