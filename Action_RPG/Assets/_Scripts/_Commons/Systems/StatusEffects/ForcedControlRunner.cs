using UnityEngine;

/// <summary>
/// Actor-neutral forced-control intent. StatusControlResolver quyết quyền/priority; runner chỉ chuyển
/// snapshot thành hướng forced movement/target thống nhất để Player/Enemy/Companion cùng tiêu thụ.
/// </summary>
public struct ForcedControlIntent
{
    public ControlMode mode;
    public Stats source;
    public Stats target;
    public Transform targetTransform;
    public Vector3 direction;
    public float speedMultiplier;
    public bool canMove;
    public bool canBasicAttack;

    public bool IsValid => mode == ControlMode.Taunted || mode == ControlMode.Feared;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Stats))]
public sealed class ForcedControlRunner : MonoBehaviour
{
    private Stats _stats;

    private void Awake()
    {
        _stats = GetComponent<Stats>();
    }

    public static bool IsForcedMode(ControlMode mode)
        => mode == ControlMode.Taunted || mode == ControlMode.Feared;

    public bool TryBuildIntent(out ForcedControlIntent intent)
    {
        if (_stats == null) _stats = GetComponent<Stats>();
        intent = default;
        if (_stats == null || _stats.isDead) return false;

        StatusControlSnapshot control = _stats.ControlState;
        if (!IsForcedMode(control.mode)) return false;

        Stats source = control.controlSource;
        Transform anchor = control.forcedTargetTransform;
        if (anchor == null || !anchor.gameObject.activeInHierarchy) return false;

        Vector3 direction;
        Stats target = control.forcedTarget;
        if (control.mode == ControlMode.Taunted)
        {
            if (target != null && (target.isDead || !target.gameObject.activeInHierarchy)) target = null;
            direction = anchor.position - transform.position;
        }
        else
        {
            direction = transform.position - anchor.position;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
        else direction = Vector3.zero;

        intent = new ForcedControlIntent
        {
            mode = control.mode,
            source = source,
            target = target,
            targetTransform = anchor,
            direction = direction,
            speedMultiplier = Mathf.Max(0f, control.forcedMoveSpeedMultiplier),
            canMove = control.canForcedMove,
            canBasicAttack = control.canForcedBasicAttack
        };
        return true;
    }
}
