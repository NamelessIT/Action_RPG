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
    private IWeaponAttackHandler _activeChanneledHandler;
    private bool _forcedBasicChannelActive;

    // ─────────────────────────────────────────────────────────────
    //  PROPERTIES
    // ─────────────────────────────────────────────────────────────

    /// <summary>True khi Staff spin đang chạy.</summary>
    public bool IsChanneledActive => _isChanneledActive;
    public bool IsForcedBasicChanneledActive => _forcedBasicChannelActive;
    public WeaponData CurrentWeapon => _weapon;

    /// <summary>
    /// True nếu vũ khí hiện tại có heavy channeled (Staff).
    /// PlayerController dùng để phân nhánh HandleAttackInput.
    /// </summary>
    public bool CurrentHeavyIsChanneled =>
        _heavyHandler != null && _heavyHandler.IsChanneled;

    /// <summary>True nếu basic/normal attack hiện tại là channeled (hiện dùng cho WPN_SW_T5_02).</summary>
    public bool CurrentNormalIsChanneled =>
        _normalHandler != null && _normalHandler.IsChanneled;

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
        // ── KHIỂM TRA VŨ KHÍ SW_T5_02 ĐẶC BIỆT ──
        if (w.id.Trim() == "WPN_SW_T5_02")
        {
            _normalHandler = new ChainsawAttackHandler();
            _heavyHandler = new ChainsawAttackHandler(); // Khóa Heavy thành Chainsaw luôn
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

        // ── Override đặc biệt theo id (chỉ đổi đánh thường, heavy giữ nguyên) ──
        // WPN_SP_T5_01: đánh thường thành đường xuyên bẻ góc tối đa 2 lần.
        if (w.id.Trim() == "WPN_SP_T5_01") _normalHandler = new SpearChainAttackHandler();
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
        return StartChanneledInternal(_heavyHandler, ctx, forcedBasic: false);
    }

    /// <summary>
    /// Phase E2.1: bắt đầu NORMAL/basic channeled do Taunt cưỡng chế.
    /// Hiện WPN_SW_T5_02 dùng ChainsawAttackHandler cho normal attack.
    /// </summary>
    public bool TryStartForcedBasicChanneled(WeaponAttackContext ctx)
    {
        if (!CurrentNormalIsChanneled || _isChanneledActive) return false;
        return StartChanneledInternal(_normalHandler, ctx, forcedBasic: true);
    }

    private bool StartChanneledInternal(IWeaponAttackHandler handler, WeaponAttackContext ctx, bool forcedBasic)
    {
        if (handler == null || !handler.IsChanneled || _isChanneledActive) return false;
        _activeChanneledHandler = handler;
        _forcedBasicChannelActive = forcedBasic;
        _isChanneledActive = true;
        _channeledCoroutine = handler.StartChanneled(ctx, this);
        if (_channeledCoroutine != null) return true;

        // Defensive rollback nếu handler khai IsChanneled nhưng không trả coroutine hợp lệ.
        _activeChanneledHandler = null;
        _forcedBasicChannelActive = false;
        _isChanneledActive = false;
        return false;
    }

    public void StopForcedBasicChanneled()
    {
        if (_forcedBasicChannelActive) StopChanneled();
    }

    /// <summary>Dừng channeled attack.</summary>
    public void StopChanneled()
    {
        if (!_isChanneledActive) return;

        _activeChanneledHandler?.StopChanneled();

        // Force-kill coroutine. Sau khi StopCoroutine, cleanup cuối routine không chạy nên reset tại đây.
        if (_channeledCoroutine != null) StopCoroutine(_channeledCoroutine);
        _channeledCoroutine = null;
        _activeChanneledHandler = null;
        _forcedBasicChannelActive = false;
        _isChanneledActive  = false;

        if (_pc != null)
        {
            _pc.isAttacking     = false;
            _pc.isStaffSpinning = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  FORCED BASIC TARGETING (TAUNT)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Target đã nằm trong reach để bắt đầu basic attack hay chưa. Đây chỉ là CHASE GATE;
    /// không guarantee hit. Hit thật vẫn do handler geometry/projectile/collision quyết định.
    /// </summary>
    public bool CanForcedBasicAttackTarget(Transform target)
    {
        if (_pc == null || target == null || !target.gameObject.activeInHierarchy) return false;

        Stats targetStats = target.GetComponent<Stats>() ?? target.GetComponentInParent<Stats>();
        if (targetStats != null && targetStats.isDead) return false;

        string id = _weapon != null && !string.IsNullOrEmpty(_weapon.id) ? _weapon.id.Trim() : string.Empty;

        // WPN_SP_T5_01: basic attack là đường xuyên 50m/bẻ góc; gameplay spec coi reach là vô hạn
        // nên Taunt KHÔNG chase. Handler sẽ aim đoạn đầu thẳng vào Taunter rồi mới bẻ góc như bình thường.
        if (id == "WPN_SP_T5_01") return true;

        float reach = Mathf.Max(0.1f, _pc.attackRange);
        // WPN_SW_T5_02 chainsaw hitbox dài đúng 2m phía trước. Asset hiện cũng attackRange=2,
        // giữ explicit để behavior forced không phụ thuộc một future inspector edit vô tình lệch hitbox.
        if (id == "WPN_SW_T5_02") reach = 2f;

        Collider col = target.GetComponentInChildren<Collider>();
        Vector3 targetPoint = col != null ? col.ClosestPoint(_pc.transform.position) : target.position;
        Vector3 a = _pc.transform.position;
        a.y = targetPoint.y;
        return Vector3.Distance(a, targetPoint) <= reach;
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
        System.Action<int, bool> onHitEvent,
        Transform forcedPrimaryTarget = null)
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
            ForcedPrimaryTarget = forcedPrimaryTarget,
            ForcedPrimaryTargetStats = forcedPrimaryTarget != null
                ? (forcedPrimaryTarget.GetComponent<Stats>() ?? forcedPrimaryTarget.GetComponentInParent<Stats>())
                : null,
            ApplyDamage   = applyDamage,
            OnHitEvent    = onHitEvent,
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  GIZMOS — Chỉ vẽ hitbox ĐẶC BIỆT, không duplicate sphere/ray
    //  của PlayerController (vốn đã vẽ sphere xanh + ray vàng).
    //  Select Player trong Scene để thấy.
    // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_pc == null || _weapon == null) return;

        float   range     = _pc.attackRange;
        Vector3 pos       = transform.position;
        Vector3 facingDir = _stats?.facingDirection ?? transform.forward;
        if (facingDir == Vector3.zero) facingDir = transform.forward;

        // ── Greatsword Slam: sphere thu hẹp góc 60° ─────────────────────────
        if (_weapon.weaponType == WeaponData.WeaponType.GreatSword)
        {
            const float SLAM_ANGLE = 60f;
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f); // Cam
            Gizmos.DrawSphere(pos + Vector3.up * 0.5f, range);
            // Ray giới hạn góc 60° (hẹp hơn normal attack để thấy sự khác biệt)
            Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
            Gizmos.DrawRay(pos + Vector3.up * 0.5f,
                Quaternion.AngleAxis(-SLAM_ANGLE / 2f, Vector3.up) * facingDir * range);
            Gizmos.DrawRay(pos + Vector3.up * 0.5f,
                Quaternion.AngleAxis( SLAM_ANGLE / 2f, Vector3.up) * facingDir * range);
        }

        // ── Spear: capsule đâm thẳng ─────────────────────────────────────────
        if (_weapon.weaponType == WeaponData.WeaponType.Spear)
        {
            const float THRUST_R = 0.4f;
            Vector3 origin = pos + Vector3.up * 0.5f;
            Vector3 tip    = origin + facingDir * range;
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.4f); // Cyan đậm
            Gizmos.DrawSphere(origin, THRUST_R);
            Gizmos.DrawSphere(tip,    THRUST_R);
            Gizmos.DrawLine(origin + Vector3.right * THRUST_R, tip + Vector3.right * THRUST_R);
            Gizmos.DrawLine(origin - Vector3.right * THRUST_R, tip - Vector3.right * THRUST_R);
            Gizmos.DrawLine(origin + Vector3.up    * THRUST_R, tip + Vector3.up    * THRUST_R);
            Gizmos.DrawLine(origin - Vector3.up    * THRUST_R, tip - Vector3.up    * THRUST_R);
        }

        // ── Staff Spin: sphere cố định 1.5f ─────────────────────────────────
        if (_weapon.weaponType == WeaponData.WeaponType.Staff)
        {
            Gizmos.color = new Color(0.6f, 0f, 1f, 0.3f); // Tím
            Gizmos.DrawSphere(pos + Vector3.up * 0.5f, 1.5f);
        }
    }
#endif
}
