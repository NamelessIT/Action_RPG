using UnityEngine;
using UnityEngine.AI;

public enum DisplacementKind
{
    None,
    Airborne,
    Knockback,
    Pull
}

public enum DisplacementApplyKind
{
    Rejected,
    Started,
    Refreshed,
    Overridden
}

public struct ActiveDisplacementState
{
    public DisplacementKind kind;
    public KnockbackMotionMode knockbackMode;
    public int impactLevel;
    public EffectHandle owner;
    public Stats source;
    public string effectId;
    public float startedAt;
    public float endsAt;

    public bool IsActive => kind != DisplacementKind.None && owner.IsValid;
    /// <summary>Airborne = real motion duration. Pull/Driven = safety timeout. Standard KB = motor timeout.</summary>
    public float Remaining => IsActive ? Mathf.Max(0f, endsAt - Time.time) : 0f;
}

public struct DisplacementApplyResult
{
    public DisplacementApplyKind kind;
    public EffectHandle handle;

    public bool Accepted => kind != DisplacementApplyKind.Rejected;
    public bool StartsNewPhysicalOwnership => kind == DisplacementApplyKind.Started || kind == DisplacementApplyKind.Overridden;

    public DisplacementApplyResult(DisplacementApplyKind kind, EffectHandle handle)
    {
        this.kind = kind;
        this.handle = handle;
    }

    public static DisplacementApplyResult Reject()
        => new DisplacementApplyResult(DisplacementApplyKind.Rejected, EffectHandle.Invalid);
}

/// <summary>
/// Runtime request đã qua CC immunity / Super Armor / resistance policy ở StatusEffectController.
/// Component này là owner DUY NHẤT của movement/physics cho Airborne / Knockback / Pull.
/// </summary>
public struct DisplacementRuntimeRequest
{
    public DisplacementKind kind;
    public EffectHandle owner;
    public Stats source;
    public string effectId;
    public string sourceIdentity;
    public bool allowIdentityRefresh;

    public int impactLevel;
    /// <summary>
    /// Airborne: motion duration. Pull/Driven KB: safety timeout only.
    /// Standard KB ignores authored duration and ends when motion settles (hard-capped by motor timeout).
    /// </summary>
    public float duration;
    public float height;

    // Knockback
    public KnockbackMotionMode knockbackMode;
    public float knockbackForce;
    public Vector3 sourcePosition;
    public Transform drivenAnchor;
    public Vector3 drivenDirection;
    public float drivenOffset;

    // Pull
    public PullAnchorMode pullAnchorMode;
    public Vector3 pullPoint;
    public Transform pullTransform;
    public float pullSpeed;
}

/// <summary>
/// Phase D authoritative displacement motor.
///
/// Lifecycle rule:
/// - Displaced CHỈ tồn tại trong lúc target đang thực sự bị forced movement.
/// - Airborne: kết thúc khi arc/landing kết thúc.
/// - Standard Knockback: kết thúc ngay khi impulse motion settle; không có post-motion action-lock.
/// - Pull: kết thúc ngay khi target tới anchor; duration chỉ là safety timeout.
/// - Driven Knockback: target bị source/anchor ủi liên tục và chỉ hết khi source release / invalid / timeout.
///
/// Priority rule:
/// - Chỉ một displacement sở hữu target tại một thời điểm.
/// - Effect khác chỉ override khi incoming impactLevel > active impactLevel. Equal/lower reject.
/// - Pull cùng logical identity có thể refresh để zone cập nhật anchor/speed mà không spam interrupt.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Stats))]
public sealed class DisplacementController : MonoBehaviour
{
    // Standard Knockback giữ motor legacy (impulse), nhưng bỏ 0.10s action-lock dư sau khi movement đã settle.
    public const float DefaultKnockbackMotionTimeout = 0.20f;
    public const float StandardKnockbackMinObserveTime = 0.04f;
    public const float StandardKnockbackStopSpeed = 0.08f;
    public const int StandardKnockbackStableTicks = 2;

    // Pull tới gần anchor hơn tolerance này => coi là đã tới nơi và release control ngay.
    public const float PullArrivalTolerance = 0.08f;

    private sealed class RuntimeState
    {
        public DisplacementRuntimeRequest request;
        public float startedAt;
        public float endsAt;
        public Vector3 airborneBasePosition;
        public int stationaryFixedTicks;
    }

    private Stats _stats;
    private NavMeshAgent _agent;
    private Rigidbody _rb;
    private Animator _animator;
    private RuntimeState _active;

    // Original actor state, captured ONCE khi một displacement chain bắt đầu và restore ONCE khi chain kết thúc.
    private bool _originalStateCaptured;
    private bool _agentWasEnabled;
    private bool _agentWasStopped;
    private bool _rbWasKinematic;
    private bool _rbUsedGravity;
    private bool _animatorUsedRootMotion;

    public bool IsActive => _active != null;
    public DisplacementKind ActiveKind => _active != null ? _active.request.kind : DisplacementKind.None;
    public int ActiveImpactLevel => _active != null ? _active.request.impactLevel : -1;

    public ActiveDisplacementState Active
    {
        get
        {
            if (_active == null) return default(ActiveDisplacementState);
            return new ActiveDisplacementState
            {
                kind = _active.request.kind,
                knockbackMode = _active.request.knockbackMode,
                impactLevel = _active.request.impactLevel,
                owner = _active.request.owner,
                source = _active.request.source,
                effectId = _active.request.effectId,
                startedAt = _active.startedAt,
                endsAt = _active.endsAt
            };
        }
    }

    private void Awake() => ResolveComponents();

    private void ResolveComponents()
    {
        if (_stats == null) _stats = GetComponent<Stats>();
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_active == null) return;
        ResolveComponents();

        if (_stats == null || _stats.isDead)
        {
            FinishActive(EffectRemoveReason.TargetDied, restoreOriginalState: false);
            return;
        }

        // Airborne duration is actual trajectory duration. Pull/Driven duration is safety only.
        if (Time.time >= _active.endsAt)
        {
            FinishActive(EffectRemoveReason.Expired, restoreOriginalState: true);
            return;
        }

        switch (_active.request.kind)
        {
            case DisplacementKind.Airborne:
                UpdateAirborne();
                break;

            case DisplacementKind.Pull:
                UpdatePull();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (_active == null || _active.request.kind != DisplacementKind.Knockback) return;
        ResolveComponents();

        if (_stats == null || _stats.isDead)
        {
            FinishActive(EffectRemoveReason.TargetDied, restoreOriginalState: false);
            return;
        }

        if (Time.time >= _active.endsAt)
        {
            FinishActive(EffectRemoveReason.Expired, restoreOriginalState: true);
            return;
        }

        if (_active.request.knockbackMode == KnockbackMotionMode.Driven)
            UpdateDrivenKnockback();
        else
            UpdateStandardKnockback();
    }

    /// <summary>True nếu handle này vẫn đang sở hữu physical displacement của target.</summary>
    public bool Owns(EffectHandle owner)
        => _active != null && owner.IsValid && _active.request.owner.Equals(owner);

    /// <summary>Source của Driven Knockback cập nhật anchor/direction/offset mỗi physics tick.</summary>
    public bool UpdateDrivenParameters(EffectHandle owner, Transform anchor, Vector3 direction, float offset)
    {
        if (!Owns(owner) || _active.request.kind != DisplacementKind.Knockback
            || _active.request.knockbackMode != KnockbackMotionMode.Driven)
            return false;

        _active.request.drivenAnchor = anchor;
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.y = 0f;
            _active.request.drivenDirection = direction.normalized;
        }
        _active.request.drivenOffset = Mathf.Max(0f, offset);
        return true;
    }

    /// <summary>
    /// Start/refresh/override một displacement đã qua global CC validation.
    /// Equal impact không override effect khác. Refresh chỉ dành cho Pull cùng identity.
    /// </summary>
    public DisplacementApplyResult TryApply(DisplacementRuntimeRequest incoming)
    {
        ResolveComponents();
        if (_stats == null || _stats.isDead || incoming.kind == DisplacementKind.None || !incoming.owner.IsValid)
            return DisplacementApplyResult.Reject();

        if (incoming.kind == DisplacementKind.Airborne && incoming.duration <= 0f)
            return DisplacementApplyResult.Reject();
        if (incoming.kind == DisplacementKind.Pull && incoming.duration <= 0f)
            return DisplacementApplyResult.Reject();
        if (incoming.kind == DisplacementKind.Knockback
            && incoming.knockbackMode == KnockbackMotionMode.Driven
            && incoming.duration <= 0f)
            return DisplacementApplyResult.Reject();

        if (_active != null && IsSameLogicalEffect(incoming))
        {
            RefreshActive(incoming);
            return new DisplacementApplyResult(DisplacementApplyKind.Refreshed, _active.request.owner);
        }

        if (_active != null && incoming.impactLevel <= _active.request.impactLevel)
            return DisplacementApplyResult.Reject();

        bool overriding = _active != null;
        if (!_originalStateCaptured)
            CaptureOriginalActorState();

        if (overriding)
            PrepareForOverride();

        float timeout = ResolveMotorTimeout(incoming);
        _active = new RuntimeState
        {
            request = incoming,
            startedAt = Time.time,
            endsAt = Time.time + timeout,
            airborneBasePosition = transform.position,
            stationaryFixedTicks = 0
        };

        ConfigurePhysicalMode(incoming, isRefresh: false);
        _stats.SynchronizeManagedCrowdControlState();

        return new DisplacementApplyResult(
            overriding ? DisplacementApplyKind.Overridden : DisplacementApplyKind.Started,
            incoming.owner);
    }

    private static float ResolveMotorTimeout(DisplacementRuntimeRequest incoming)
    {
        if (incoming.kind == DisplacementKind.Knockback && incoming.knockbackMode == KnockbackMotionMode.Standard)
            return DefaultKnockbackMotionTimeout;
        return Mathf.Max(0.01f, incoming.duration);
    }

    public bool Release(EffectHandle owner, EffectRemoveReason reason = EffectRemoveReason.Manual)
    {
        if (_active == null || !owner.IsValid || !_active.request.owner.Equals(owner)) return false;
        FinishActive(reason, restoreOriginalState: _stats == null || !_stats.isDead);
        return true;
    }

    public void ForceRelease(EffectRemoveReason reason = EffectRemoveReason.Manual, bool restoreOriginalState = true)
    {
        if (_active == null) return;
        FinishActive(reason, restoreOriginalState);
    }

    public string GetDebugSummary()
    {
        if (_active == null) return "none";
        string id = string.IsNullOrEmpty(_active.request.effectId) ? "anonymous" : _active.request.effectId;
        string mode = _active.request.kind == DisplacementKind.Knockback ? $"/{_active.request.knockbackMode}" : string.Empty;
        return $"{_active.request.kind}{mode}#{_active.request.owner.InstanceId} ({id}) impact={_active.request.impactLevel} remaining={Mathf.Max(0f, _active.endsAt - Time.time):0.00}s";
    }

    private bool IsSameLogicalEffect(DisplacementRuntimeRequest incoming)
    {
        if (_active == null || !incoming.allowIdentityRefresh || !_active.request.allowIdentityRefresh) return false;
        if (_active.request.kind != incoming.kind) return false;
        if (!string.Equals(_active.request.effectId, incoming.effectId, System.StringComparison.Ordinal)) return false;
        return string.Equals(_active.request.sourceIdentity, incoming.sourceIdentity, System.StringComparison.Ordinal);
    }

    private void RefreshActive(DisplacementRuntimeRequest incoming)
    {
        DisplacementRuntimeRequest previous = _active.request;
        EffectHandle existingOwner = previous.owner;
        incoming.owner = existingOwner;
        incoming.impactLevel = Mathf.Max(previous.impactLevel, incoming.impactLevel);
        _active.request = incoming;
        _active.endsAt = Mathf.Max(_active.endsAt, Time.time + Mathf.Max(0.01f, incoming.duration));
        _stats.SynchronizeManagedCrowdControlState();
    }

    private void CaptureOriginalActorState()
    {
        ResolveComponents();
        _originalStateCaptured = true;

        if (_agent != null)
        {
            _agentWasEnabled = _agent.enabled;
            if (_agent.enabled && _agent.isOnNavMesh)
                _agentWasStopped = _agent.isStopped;
        }

        if (_rb != null)
        {
            _rbWasKinematic = _rb.isKinematic;
            _rbUsedGravity = _rb.useGravity;
        }

        if (_animator != null)
            _animatorUsedRootMotion = _animator.applyRootMotion;
    }

    private void ConfigurePhysicalMode(DisplacementRuntimeRequest incoming, bool isRefresh)
    {
        ResolveComponents();

        if (_animator != null)
            _animator.applyRootMotion = false;

        switch (incoming.kind)
        {
            case DisplacementKind.Airborne:
                PrepareAgentDisabled();
                PrepareRigidBodyKinematic();
                if (!isRefresh)
                    _active.airborneBasePosition = transform.position;
                break;

            case DisplacementKind.Knockback:
                PrepareAgentDisabled();
                if (incoming.knockbackMode == KnockbackMotionMode.Driven)
                {
                    PrepareRigidBodyKinematic();
                    UpdateDrivenKnockback(); // snap vào vị trí carry ngay frame apply
                }
                else
                {
                    PrepareRigidBodyForStandardKnockback();
                    if (!isRefresh) ApplyStandardKnockbackImpulse();
                }
                break;

            case DisplacementKind.Pull:
                // Pull phải sở hữu cả vertical handoff để Airborne -> Pull không bị treo giữa không trung.
                PrepareAgentDisabled();
                PrepareRigidBodyKinematic();
                break;
        }
    }

    private void PrepareForOverride()
    {
        if (_active == null) return;

        // QUAN TRỌNG: không snap Airborne về baseY khi handoff. Incoming displacement tiếp quản
        // chính current world position để trajectory không bị teleport / treo rồi rơi.
        ZeroRigidBodyVelocity();
    }

    private void PrepareAgentDisabled()
    {
        if (_agent == null) return;
        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
        }
        if (_agent.enabled) _agent.enabled = false;
    }

    private void PrepareRigidBodyKinematic()
    {
        if (_rb == null) return;
        ZeroRigidBodyVelocity();
        _rb.useGravity = false;
        _rb.isKinematic = true;
    }

    private void PrepareRigidBodyForStandardKnockback()
    {
        if (_rb == null) return;
        ZeroRigidBodyVelocity();
        _rb.isKinematic = false;
        _rb.useGravity = _rbUsedGravity;
    }

    private void UpdateStandardKnockback()
    {
        if (_active == null) return;

        float elapsed = Time.time - _active.startedAt;
        if (elapsed >= DefaultKnockbackMotionTimeout)
        {
            FinishActive(EffectRemoveReason.Expired, restoreOriginalState: true);
            return;
        }

        if (elapsed < StandardKnockbackMinObserveTime || _rb == null) return;

        Vector3 horizontalVelocity = _rb.linearVelocity;
        horizontalVelocity.y = 0f;
        if (horizontalVelocity.magnitude <= StandardKnockbackStopSpeed)
            _active.stationaryFixedTicks++;
        else
            _active.stationaryFixedTicks = 0;

        // Collision / damping đã làm target đứng lại => trả control ngay, không chờ hết timeout.
        if (_active.stationaryFixedTicks >= StandardKnockbackStableTicks)
            FinishActive(EffectRemoveReason.Expired, restoreOriginalState: true);
    }

    private void ApplyStandardKnockbackImpulse()
    {
        if (_active == null || _rb == null) return;

        Vector3 dir = transform.position - _active.request.sourcePosition;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f && _active.request.source != null)
        {
            dir = transform.position - _active.request.source.transform.position;
            dir.y = 0f;
        }
        if (dir.sqrMagnitude <= 0.0001f) dir = transform.forward;
        dir.Normalize();

        float resistance = _stats != null ? Mathf.Clamp01(_stats.knockbackResistance) : 0f;
        float finalForce = Mathf.Max(0f, _active.request.knockbackForce) * (1f - resistance);
        if (finalForce > 0f)
            _rb.AddForce(dir * finalForce, ForceMode.Impulse);
    }

    private void UpdateDrivenKnockback()
    {
        if (_active == null) return;

        Transform anchor = _active.request.drivenAnchor;
        if (!IsValidAnchorTransform(anchor))
        {
            FinishActive(EffectRemoveReason.SourceInvalid, restoreOriginalState: true);
            return;
        }

        Vector3 dir = _active.request.drivenDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f && _active.request.source != null)
        {
            dir = _active.request.source.transform.forward;
            dir.y = 0f;
        }
        if (dir.sqrMagnitude <= 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        Vector3 desired = anchor.position + dir * Mathf.Max(0f, _active.request.drivenOffset);
        // Driven KB = source thật sự đang ủi target trên mặt phẳng của source. Nếu takeover Airborne,
        // target đi liền mạch từ current position xuống vị trí carry thay vì đứng treo chờ duration.
        SetPositionDirect(desired);
    }

    private void UpdateAirborne()
    {
        if (_active == null) return;
        float duration = Mathf.Max(0.01f, _active.endsAt - _active.startedAt);
        float t = Mathf.Clamp01((Time.time - _active.startedAt) / duration);
        float y = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, _active.request.height);
        Vector3 next = _active.airborneBasePosition;
        next.y += y;
        SetPositionDirect(next);
    }

    private void UpdatePull()
    {
        if (_active == null) return;

        Vector3 anchor;
        if (!TryResolvePullAnchor(out anchor))
        {
            FinishActive(EffectRemoveReason.SourceInvalid, restoreOriginalState: true);
            return;
        }

        float speed = Mathf.Max(0f, _active.request.pullSpeed);
        if (speed <= 0f)
        {
            FinishActive(EffectRemoveReason.Manual, restoreOriginalState: true);
            return;
        }

        Vector3 current = transform.position;
        if (Vector3.Distance(current, anchor) <= PullArrivalTolerance)
        {
            SetPositionDirect(anchor);
            FinishActive(EffectRemoveReason.Expired, restoreOriginalState: true);
            return;
        }

        // Full 3D MoveTowards để Airborne -> higher-impact Pull handoff đi chéo xuống anchor tự nhiên.
        Vector3 next = Vector3.MoveTowards(current, anchor, speed * Time.deltaTime);
        SetPositionDirect(next);

        if (Vector3.Distance(next, anchor) <= PullArrivalTolerance)
        {
            SetPositionDirect(anchor);
            FinishActive(EffectRemoveReason.Expired, restoreOriginalState: true);
        }
    }

    private bool TryResolvePullAnchor(out Vector3 anchor)
    {
        anchor = transform.position;
        if (_active == null) return false;

        switch (_active.request.pullAnchorMode)
        {
            case PullAnchorMode.FixedPoint:
                anchor = _active.request.pullPoint;
                return true;

            case PullAnchorMode.TargetTransform:
                if (!IsValidAnchorTransform(_active.request.pullTransform)) return false;
                anchor = _active.request.pullTransform.position;
                return true;

            case PullAnchorMode.SourceTransform:
            default:
                if (_active.request.source == null || _active.request.source.isDead) return false;
                anchor = _active.request.source.transform.position;
                return true;
        }
    }

    private static bool IsValidAnchorTransform(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        Stats owner = t.GetComponentInParent<Stats>();
        return owner == null || !owner.isDead;
    }

    private void SetPositionDirect(Vector3 position)
    {
        if (_rb != null && _rb.isKinematic)
            _rb.position = position;
        else
            transform.position = position;
    }

    private void ZeroRigidBodyVelocity()
    {
        if (_rb == null) return;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void FinishActive(EffectRemoveReason reason, bool restoreOriginalState)
    {
        if (_active == null) return;

        // Airborne natural finish lands at its base. Override path never calls FinishActive before handoff.
        if (_active.request.kind == DisplacementKind.Airborne)
        {
            Vector3 p = transform.position;
            p.y = _active.airborneBasePosition.y;
            SetPositionDirect(p);
        }

        ZeroRigidBodyVelocity();
        _active = null;

        if (restoreOriginalState)
        {
            RestoreOriginalActorState();
        }
        else
        {
            if (_animator != null && _originalStateCaptured)
                _animator.applyRootMotion = _animatorUsedRootMotion;
            _originalStateCaptured = false;
        }

        if (_stats != null)
            _stats.SynchronizeManagedCrowdControlState();
    }

    private void RestoreOriginalActorState()
    {
        if (!_originalStateCaptured) return;
        ResolveComponents();

        if (_rb != null)
        {
            ZeroRigidBodyVelocity();
            _rb.isKinematic = _rbWasKinematic;
            _rb.useGravity = _rbUsedGravity;
        }

        if (_animator != null)
            _animator.applyRootMotion = _animatorUsedRootMotion;

        if (_agent != null)
        {
            if (_agentWasEnabled)
            {
                if (!_agent.enabled) _agent.enabled = true;

                if (!_agent.isOnNavMesh)
                {
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(transform.position, out hit, 2f, _agent.areaMask))
                        _agent.Warp(hit.position);
                }

                if (_agent.enabled && _agent.isOnNavMesh)
                {
                    bool managedLock = _stats != null && (_stats.IsStunned || _stats.IsRooted);
                    _agent.isStopped = _agentWasStopped || managedLock;
                    _agent.velocity = Vector3.zero;
                }
            }
            else if (_agent.enabled)
            {
                _agent.enabled = false;
            }
        }

        _originalStateCaptured = false;
    }
}
