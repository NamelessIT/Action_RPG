using System.Collections;
using UnityEngine;

/// <summary>
/// Gắn vào cùng GameObject với PlayerController.
/// Nhận lệnh tấn công từ PlayerController và chọn đúng handler theo WeaponData.
///
/// Luồng:
///   PlayerController.AttackRoutine()
///     → dispatcher.ExecuteSwing(ctx)       [normal/heavy standard]
///     → dispatcher.TryStartChanneled(ctx)  [Staff spin]
///     → dispatcher.StopChanneled()         [khi thả chuột]
/// </summary>
public class WeaponAttackDispatcher : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────────────────────
    private PlayerController _pc;
    private AllyStats        _stats;
    private Animator         _animator;
    private WeaponData       _weapon;

    private IWeaponAttackHandler _normalHandler;
    private IWeaponAttackHandler _heavyHandler;

    private bool     _isChanneledActive;
    private Coroutine _channeledCoroutine;

    // ─────────────────────────────────────────────────────────────
    //  PROPERTIES
    // ─────────────────────────────────────────────────────────────

    /// <summary>True khi Staff spin đang chạy.</summary>
    public bool IsChanneledActive => _isChanneledActive;

    /// <summary>
    /// True nếu vũ khí hiện tại có heavy channeled (Staff).
    /// PlayerController dùng để phân nhánh HandleAttackInput.
    /// </summary>
    public bool CurrentHeavyIsChanneled =>
        _heavyHandler != null && _heavyHandler.IsChanneled;

    // ─────────────────────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _pc       = GetComponent<PlayerController>();
        _stats    = GetComponent<AllyStats>();
        _animator = GetComponent<Animator>();
    }

    /// <summary>Gọi từ EquipmentManager mỗi khi vũ khí thay đổi.</summary>
    public void OnWeaponChanged(WeaponData weapon)
    {
        // Dừng channeled nếu đang chạy
        if (_isChanneledActive) StopChanneled();

        _weapon = weapon;
        SelectHandlers(weapon);

        Debug.Log($"[WeaponAttackDispatcher] Weapon: {weapon?.weaponName} | " +
                  $"normal={_normalHandler?.GetType().Name} | heavy={_heavyHandler?.GetType().Name}");
    }

    // ─────────────────────────────────────────────────────────────
    //  HANDLER SELECTION
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve AttackMode.Default → mode cụ thể theo weaponType tại runtime.
    /// Nếu developer đã chọn mode khác Default trong Inspector, giữ nguyên giá trị đó.
    /// </summary>
    private WeaponData.AttackMode ResolveAttackMode(WeaponData w)
    {
        if (w.attackMode != WeaponData.AttackMode.Default) return w.attackMode;

        return w.weaponType switch
        {
            WeaponData.WeaponType.Spear    => WeaponData.AttackMode.Thrust,
            WeaponData.WeaponType.Bow      => WeaponData.AttackMode.Directional,
            WeaponData.WeaponType.Grimoire => WeaponData.AttackMode.Directional,
            WeaponData.WeaponType.Staff    => WeaponData.AttackMode.TargetSeek,
            _                              => WeaponData.AttackMode.Default,
        };
    }

    private void SelectHandlers(WeaponData w)
    {
        if (w == null)
        {
            _normalHandler = new DefaultMeleeAttackHandler();
            _heavyHandler  = new DefaultMeleeAttackHandler();
            return;
        }

        // ── Normal handler ─────────────────────────────────────────────────
        WeaponData.AttackMode resolvedMode = ResolveAttackMode(w);
        _normalHandler = resolvedMode switch
        {
            WeaponData.AttackMode.Thrust      => new SpearAttackHandler(),
            WeaponData.AttackMode.Directional => new RangedAttackHandler(WeaponData.AttackMode.Directional),
            WeaponData.AttackMode.TargetSeek  => w.weaponType == WeaponData.WeaponType.Staff
                                                     ? (IWeaponAttackHandler)new StaffNormalAttackHandler()
                                                     : new RangedAttackHandler(WeaponData.AttackMode.TargetSeek),
            _                                 => new DefaultMeleeAttackHandler(),
        };

        // ── Heavy handler ──────────────────────────────────────────────────
        _heavyHandler = w.heavyAttackType switch
        {
            WeaponData.HeavyAttackType.SwordDoubleSlash => new SwordHeavyAttack(),
            WeaponData.HeavyAttackType.SpearThrust      => new SpearHeavyAttack(),
            WeaponData.HeavyAttackType.DaggerSpin       => new DaggerHeavyAttack(),
            WeaponData.HeavyAttackType.GreatswordSlam   => new GreatswordHeavyAttack(),
            WeaponData.HeavyAttackType.BowPierce        => new BowHeavyAttack(),
            WeaponData.HeavyAttackType.GrimoireStrike   => new GrimoireHeavyAttack(),
            WeaponData.HeavyAttackType.StaffChannelSpin => new StaffHeavyAttack(),
            _                                           => new DefaultMeleeAttackHandler(),
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  EXECUTE
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Được gọi từ PlayerController.AttackRoutine trong window swing.
    /// Chọn heavy hoặc normal handler tự động.
    /// </summary>
    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        IWeaponAttackHandler handler = ctx.IsHeavy ? _heavyHandler : _normalHandler;
        if (handler == null) yield break;

        yield return StartCoroutine(handler.ExecuteSwing(ctx));
    }

    // ─────────────────────────────────────────────────────────────
    //  CHANNELED (Staff Spin)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Thử bắt đầu channeled attack.
    /// Trả về true nếu đã start (là channeled weapon + chưa đang chạy).
    /// </summary>
    public bool TryStartChanneled(WeaponAttackContext ctx)
    {
        if (!CurrentHeavyIsChanneled || _isChanneledActive) return false;

        _isChanneledActive  = true;
        _channeledCoroutine = _heavyHandler.StartChanneled(ctx, this);
        return true;
    }

    /// <summary>Dừng channeled attack.</summary>
    public void StopChanneled()
    {
        if (!_isChanneledActive) return;

        _heavyHandler?.StopChanneled();   // Báo handler dừng vòng lặp (set _isSpinning=false)

        // Force-kill coroutine. Sau khi StopCoroutine, phần cleanup cuối SpinRoutine
        // (ctx.Player.isAttacking = false) sẽ KHÔNG chạy → phải reset thủ công ở đây.
        if (_channeledCoroutine != null) StopCoroutine(_channeledCoroutine);
        _channeledCoroutine = null;
        _isChanneledActive  = false;

        // Reset isAttacking để player di chuyển được ngay sau khi thả chuột
        if (_pc != null) _pc.isAttacking = false;
    }

    // ─────────────────────────────────────────────────────────────
    //  CONTEXT BUILDER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo WeaponAttackContext cho một đòn đánh.
    /// Gọi từ PlayerController sau khi setup timing.
    /// </summary>
    public WeaponAttackContext BuildContext(
        bool isHeavy, int comboStep, float swingDuration,
        System.Collections.Generic.List<Transform> hitTargets,
        LayerMask dangerLayer,
        System.Action<Stats, bool, int> applyDamage,
        System.Action<int, bool> onHitEvent)
    {
        return new WeaponAttackContext
        {
            Player        = _pc,
            Stats         = _stats,
            Animator      = _animator,
            Weapon        = _weapon,
            DangerLayer   = dangerLayer,
            HitTargets    = hitTargets,
            SwingDuration = swingDuration,
            ComboStep     = comboStep,
            IsHeavy       = isHeavy,
            ApplyDamage   = applyDamage,
            OnHitEvent    = onHitEvent,
        };
    }
}
