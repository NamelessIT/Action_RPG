using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dữ liệu context truyền vào IWeaponAttackHandler khi thực hiện một đòn đánh.
/// Handler chỉ đọc context trong scope coroutine — không giữ reference lâu dài.
/// </summary>
public class WeaponAttackContext
{
    // ── References ────────────────────────────────────────────────
    public PlayerController Player;
    public AllyStats         Stats;
    public Animator          Animator;
    public WeaponData        Weapon;

    // ── Physics ───────────────────────────────────────────────────
    public LayerMask          DangerLayer;
    public List<Transform>    HitTargets;   // Shared list — handler thêm vào để tránh double-hit

    // ── Timing ────────────────────────────────────────────────────
    public float SwingDuration;            // Thời gian handler được phép gây damage (giây)

    // ── Attack State ──────────────────────────────────────────────
    public int  ComboStep;
    public bool IsHeavy;

    // ── Callbacks ─────────────────────────────────────────────────
    /// <summary>Gọi ApplyDamageToTarget trên PlayerController.</summary>
    public System.Action<Stats, bool, int> ApplyDamage;

    /// <summary>Gọi OnAttackPerformed event (stepIndex, isHeavy).</summary>
    public System.Action<int, bool> OnHitEvent;

    // ── Helpers ───────────────────────────────────────────────────
    public Vector3 FacingDir =>
        (Stats != null && Stats.facingDirection != Vector3.zero)
            ? Stats.facingDirection
            : Player.transform.forward;

    public Vector3 PlayerPos => Player.transform.position;

    /// <summary>
    /// Đăng ký thêm 1 Transform đã trúng đòn.
    /// Trả về true nếu đây là lần đầu (chưa có trong danh sách).
    /// </summary>
    public bool TryAddHitTarget(Transform t)
    {
        if (HitTargets.Contains(t)) return false;
        HitTargets.Add(t);
        return true;
    }

    // ── Enemy Detection Helpers ───────────────────────────────────
    /// <summary>
    /// Lấy Stats từ Collider, hỗ trợ Collider nằm trên child object.
    /// Sprite enemy thường có cấu trúc: Parent(tagged "Enemy", has Stats) → Child(has Collider).
    /// Trả về null nếu không phải enemy hoặc không có Stats.
    /// </summary>
    public Stats GetEnemyStats(UnityEngine.Collider col)
    {
        // Case 1: Collider nằm ngay trên object có tag + Stats
        if (col.CompareTag("Enemy"))
        {
            Stats s = col.GetComponent<Stats>();
            if (s != null) return s;
        }
        // Case 2: Collider nằm trên child — tìm Stats + tag ở parent
        Stats parentStats = col.GetComponentInParent<Stats>();
        if (parentStats != null && parentStats.gameObject.CompareTag("Enemy"))
            return parentStats;
        return null;
    }

    /// <summary>Overload cho RaycastHit (SphereCastAll, SphereCast...).</summary>
    public Stats GetEnemyStats(UnityEngine.RaycastHit hit) => GetEnemyStats(hit.collider);
}
