using UnityEngine;

public enum ControlMode
{
    Normal,
    Taunted,
    Feared,
    Stunned,
    Displaced
}

/// <summary>Policy điều khiển việc một Skill có được phép cast dưới CC hay không.</summary>
public enum SkillControlPolicy
{
    Normal = 0,
    Cleanse = 1
}

public struct StatusControlSnapshot
{
    public ControlMode mode;

    // Manual / normal-control capabilities.
    public bool canMove;
    public bool canDash;
    public bool canBasicAttack;
    public bool canCastNormalSkill;
    public bool canCastCleanseSkill;

    // Forced-control capabilities. Tách khỏi manual flags để Taunt có thể auto-attack dù user/AI
    // không được quyền bấm basic attack bình thường.
    public bool canForcedMove;
    public bool canForcedBasicAttack;

    public Stats forcedTarget;
    public Transform forcedTargetTransform;
    public Stats controlSource;
    public float forcedMoveSpeedMultiplier;

    public static StatusControlSnapshot Normal => new StatusControlSnapshot
    {
        mode = ControlMode.Normal,
        canMove = true,
        canDash = true,
        canBasicAttack = true,
        canCastNormalSkill = true,
        canCastCleanseSkill = true,
        canForcedMove = false,
        canForcedBasicAttack = false,
        forcedMoveSpeedMultiplier = 1f
    };
}

/// <summary>
/// Resolver capability thống nhất cho Player / Enemy / Companion.
/// Priority: Displacement > Stun > Fear > Taunt > normal.
/// Root/Silence vẫn tồn tại bên dưới forced control; Root chặn phần forced movement nhưng Taunt vẫn
/// được basic-attack nguồn nếu đã ở trong tầm. Cleanse Skill vẫn cast được dưới Fear/Taunt.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Stats))]
public sealed class StatusControlResolver : MonoBehaviour
{
    private Stats _stats;
    private StatusEffectController _statusEffects;
    public StatusControlSnapshot Current { get; private set; }

    private void Awake()
    {
        _stats = GetComponent<Stats>();
        _statusEffects = GetComponent<StatusEffectController>();
        Current = StatusControlSnapshot.Normal;
    }

    public StatusControlSnapshot RebuildFromCurrentState()
    {
        if (_stats == null) _stats = GetComponent<Stats>();
        if (_statusEffects == null) _statusEffects = GetComponent<StatusEffectController>();

        StatusControlSnapshot s = StatusControlSnapshot.Normal;

        if (_stats == null || _stats.isDead)
        {
            s.canMove = false;
            s.canDash = false;
            s.canBasicAttack = false;
            s.canCastNormalSkill = false;
            s.canCastCleanseSkill = false;
            Current = s;
            return s;
        }

        // 1) Hard displacement: khóa cả Cleanse.
        if (_stats.IsDisplaced)
        {
            s.mode = ControlMode.Displaced;
            s.canMove = false;
            s.canDash = false;
            s.canBasicAttack = false;
            s.canCastNormalSkill = false;
            s.canCastCleanseSkill = false;
            Current = s;
            return s;
        }

        // 2) Stun: khóa action thường nhưng Cleanse Skill được phép.
        if (_stats.IsStunned)
        {
            s.mode = ControlMode.Stunned;
            s.canMove = false;
            s.canDash = false;
            s.canBasicAttack = false;
            s.canCastNormalSkill = false;
            s.canCastCleanseSkill = true;
            Current = s;
            return s;
        }

        // 3) Forced control: Fear > Taunt; lower-priority instances vẫn nằm trong StatusEffectController.
        if (_statusEffects != null
            && _statusEffects.TryGetDominantForcedControl(out CombatEffectType forcedType,
                                                          out Stats source,
                                                          out Transform anchor,
                                                          out _,
                                                          out _))
        {
            bool rooted = _stats.IsRooted;
            s.canMove = false;
            s.canDash = false;
            s.canBasicAttack = false;
            s.canCastNormalSkill = false;
            s.canCastCleanseSkill = true;
            s.controlSource = source;

            if (forcedType == CombatEffectType.Fear)
            {
                s.mode = ControlMode.Feared;
                s.forcedTarget = null;
                s.forcedTargetTransform = anchor;
                s.forcedMoveSpeedMultiplier = 0.5f;
                s.canForcedMove = !rooted;
                s.canForcedBasicAttack = false;
            }
            else
            {
                s.mode = ControlMode.Taunted;
                s.forcedTargetTransform = anchor;
                s.forcedTarget = anchor != null ? anchor.GetComponentInParent<Stats>() : null;
                s.forcedMoveSpeedMultiplier = 1f;
                s.canForcedMove = !rooted;
                s.canForcedBasicAttack = true;
            }

            Current = s;
            return s;
        }

        // 4) Normal control with lower-level restrictions.
        if (_stats.IsRooted)
        {
            s.canMove = false;
            s.canDash = false;
        }
        if (_stats.IsSilenced)
            s.canCastNormalSkill = false;

        Current = s;
        return s;
    }

    public bool CanCastSkill(SkillControlPolicy policy)
    {
        StatusControlSnapshot state = RebuildFromCurrentState();
        return policy == SkillControlPolicy.Cleanse
            ? state.canCastCleanseSkill
            : state.canCastNormalSkill;
    }
}
