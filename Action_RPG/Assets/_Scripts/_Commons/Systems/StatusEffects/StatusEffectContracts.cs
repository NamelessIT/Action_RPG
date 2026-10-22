using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cách một effect chồng/refresh chính nó. Phase A chỉ khai báo contract; runtime đầy đủ sẽ được
/// triển khai dần khi migrate Slow/Debuff ở các phase sau.
/// </summary>
public enum EffectStackPolicy
{
    RefreshSameSource,
    Strongest,
    AdditiveStacks,
    IndependentPerSource,
    ReplaceSameSource
}

public enum PullAnchorMode
{
    SourceTransform,
    FixedPoint,
    TargetTransform
}

public enum EffectApplyStatus
{
    Accepted,
    Invalid,
    CrowdControlImmune,
    SlowImmune,
    BlockedBySuperArmor,
    RejectedByDisplacementPriority,
    Unsupported
}

public enum EffectRemoveReason
{
    Expired,
    Cleansed,
    ImmunityGranted,
    Overridden,
    SourceInvalid,
    TargetDied,
    Manual
}

/// <summary>Clock dùng cho lifecycle Debuff. Scaled là mặc định; Unscaled giữ semantics realtime của producer legacy.</summary>
public enum DebuffTimeMode
{
    Scaled,
    Unscaled
}

/// <summary>Identity runtime của một effect đã được hệ thống chấp nhận.</summary>
[Serializable]
public struct EffectHandle : IEquatable<EffectHandle>
{
    [SerializeField] private int instanceId;
    public int InstanceId => instanceId;
    public bool IsValid => instanceId > 0;

    public EffectHandle(int id) { instanceId = id; }
    public bool Equals(EffectHandle other) => instanceId == other.instanceId;
    public override bool Equals(object obj) => obj is EffectHandle other && Equals(other);
    public override int GetHashCode() => instanceId;
    public override string ToString() => IsValid ? $"Effect#{instanceId}" : "Effect#Invalid";

    public static readonly EffectHandle Invalid = new EffectHandle(0);
}

/// <summary>Context runtime tách khỏi data definition/request.</summary>
public struct StatusEffectContext
{
    public Stats source;
    public Stats target;
    public string sourceKey;
    public float appliedAt;

    public StatusEffectContext(Stats source, Stats target, string sourceKey = null)
    {
        this.source = source;
        this.target = target;
        this.sourceKey = sourceKey;
        this.appliedAt = Time.time;
    }
}

/// <summary>Kết quả apply effect; caller mới có thể log/debug mà không phải suy luận từ state.</summary>
public struct EffectApplyResult
{
    public EffectApplyStatus status;
    public EffectHandle handle;
    public float resolvedDuration;

    public bool Accepted => status == EffectApplyStatus.Accepted;

    public EffectApplyResult(EffectApplyStatus status, EffectHandle handle, float resolvedDuration = 0f)
    {
        this.status = status;
        this.handle = handle;
        this.resolvedDuration = resolvedDuration;
    }

    public static EffectApplyResult Reject(EffectApplyStatus status)
        => new EffectApplyResult(status, EffectHandle.Invalid, 0f);
}

/// <summary>Payload DoT generic thuộc một Debuff instance. Damage mỗi tick có thể gồm nhiều damage type.</summary>
[Serializable]
public struct DebuffDotSpec
{
    public bool enabled;
    public float tickInterval;
    public bool tickImmediately;
    public float physDamage;
    public float magicDamage;
    public float trueDamage;

    public bool HasDamage => enabled && tickInterval > 0f
        && (physDamage > 0f || magicDamage > 0f || trueDamage > 0f);
}

/// <summary>Snapshot immutable-ish dùng cho debug/event/hook Debuff lifecycle.</summary>
public struct DebuffRuntimeSnapshot
{
    public EffectHandle handle;
    public string effectId;
    public Stats source;
    public Stats target;
    public string sourceIdentity;
    public int stackCount;
    public int maxStacks;
    public bool cleanseable;
    public float remainingDuration;
    public float potency;

    public DebuffRuntimeSnapshot(EffectHandle handle, string effectId, Stats source, Stats target,
                                 string sourceIdentity, int stackCount, int maxStacks,
                                 bool cleanseable, float remainingDuration, float potency)
    {
        this.handle = handle;
        this.effectId = effectId;
        this.source = source;
        this.target = target;
        this.sourceIdentity = sourceIdentity;
        this.stackCount = stackCount;
        this.maxStacks = maxStacks;
        this.cleanseable = cleanseable;
        this.remainingDuration = remainingDuration;
        this.potency = potency;
    }
}

/// <summary>
/// Request Debuff generic. Không dùng giant enum theo tên content: effectId định danh instance/package,
/// còn hành vi tái sử dụng được mô tả bằng statModifiers + dot + lifecycle hooks runtime.
/// </summary>
[Serializable]
public class DebuffRequest
{
    public string effectId;
    public float duration = 1f;
    public bool cleanseable = true;
    public DebuffTimeMode timeMode = DebuffTimeMode.Scaled;
    public EffectStackPolicy stackPolicy = EffectStackPolicy.RefreshSameSource;
    public int maxStacks = 1;

    [Tooltip("Dùng khi stackPolicy=Strongest để chọn instance dominant giữa nhiều source.")]
    public float potency = 1f;

    public List<RuntimeStatModifierSpec> statModifiers = new List<RuntimeStatModifierSpec>();
    public DebuffDotSpec dot;
    public string note;

    // Runtime hooks: không serialize vào asset. Registry vẫn sở hữu timing/remove; producer chỉ cung cấp side-effect đặc thù.
    [NonSerialized] public Action<DebuffRuntimeSnapshot> onApply;
    [NonSerialized] public Action<DebuffRuntimeSnapshot> onRefreshOrStack;
    [NonSerialized] public Action<DebuffRuntimeSnapshot> onTick;
    [NonSerialized] public Action<DebuffRuntimeSnapshot, EffectRemoveReason> onRemove;
}
