using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Gateway + runtime owner của Status Effect.
/// Phase F1: CC/displacement giữ ownership hiện tại; Debuff registry source-aware sở hữu duration/stack/tick/cleanse
/// và RuntimeStatModifierLedger ownership. Bleed/Burn compatibility wrappers đi qua registry này.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Stats))]
public sealed class StatusEffectController : MonoBehaviour
{
    private sealed class ActiveCrowdControl
    {
        public EffectHandle handle;
        public CombatEffectType type;
        public string effectId;
        public string sourceIdentity;
        public Stats source;
        public Transform forcedControlAnchor;
        public bool hasExplicitForcedControlAnchor;
        public EffectStackPolicy stackPolicy;
        public int maxStacks;
        public int stackCount;
        public float magnitudePerStack;
        public float expiresAt;
        public int impactLevel;
        public bool interruptCurrentAction;
        public bool putInterruptedSkillOnCooldown;
        public int applyOrder;
        public string note;

        public float EffectiveMagnitude
        {
            get
            {
                int count = stackPolicy == EffectStackPolicy.AdditiveStacks ? Mathf.Max(1, stackCount) : 1;
                return Mathf.Clamp01(magnitudePerStack * count);
            }
        }
    }

    private sealed class ActiveDebuff
    {
        public EffectHandle handle;
        public string effectId;
        public string sourceIdentity;
        public Stats source;
        public bool cleanseable;
        public EffectStackPolicy stackPolicy;
        public int maxStacks;
        public int stackCount;
        public float potency;
        public DebuffTimeMode timeMode;
        public float expiresAt;
        public float nextTickAt;
        public int applyOrder;
        public string note;
        public List<RuntimeStatModifierSpec> statModifiers;
        public DebuffDotSpec dot;
        public Action<DebuffRuntimeSnapshot> onApply;
        public Action<DebuffRuntimeSnapshot> onRefreshOrStack;
        public Action<DebuffRuntimeSnapshot> onTick;
        public Action<DebuffRuntimeSnapshot, EffectRemoveReason> onRemove;
    }

    private Stats _stats;
    private RuntimeStatModifierLedger _modifierLedger;
    private DisplacementController _displacement;
    private StatusControlResolver _controlResolver;
    private int _nextEffectId = 1;
    private int _nextApplyOrder = 1;

    // Source-safe immunity ledger. Legacy Stats counters vẫn được đọc song song trong migration.
    private readonly HashSet<object> _ccImmunitySources = new HashSet<object>();
    private readonly HashSet<object> _slowImmunitySources = new HashSet<object>();

    // Managed CC runtime.
    private readonly Dictionary<int, ActiveCrowdControl> _managedCc = new Dictionary<int, ActiveCrowdControl>();
    // Phase F managed Debuff runtime.
    private readonly Dictionary<int, ActiveDebuff> _managedDebuffs = new Dictionary<int, ActiveDebuff>();
    private readonly List<int> _scratchRemove = new List<int>();
    private readonly List<ActiveDebuff> _scratchDebuffIteration = new List<ActiveDebuff>();

    public bool HasCrowdControlImmunity => _ccImmunitySources.Count > 0 || (_stats != null && _stats.LegacyCrowdControlImmune);
    public bool HasSlowImmunity => _slowImmunitySources.Count > 0 || (_stats != null && _stats.LegacySlowImmune);

    public RuntimeStatModifierLedger ModifierLedger => _modifierLedger;
    public DisplacementController Displacement => _displacement;
    public StatusControlSnapshot ControlState => _controlResolver != null
        ? _controlResolver.RebuildFromCurrentState()
        : StatusControlSnapshot.Normal;

    public bool IsStunned { get { PruneExpiredManaged(); return HasManagedTypeNoPrune(CombatEffectType.Stun); } }
    public bool IsRooted { get { PruneExpiredManaged(); return HasManagedTypeNoPrune(CombatEffectType.Root); } }
    public bool IsSilenced { get { PruneExpiredManaged(); return HasManagedTypeNoPrune(CombatEffectType.Silence); } }
    public bool IsTaunted { get { PruneExpiredManaged(); return HasManagedTypeNoPrune(CombatEffectType.Taunt); } }
    public bool IsFeared { get { PruneExpiredManaged(); return HasManagedTypeNoPrune(CombatEffectType.Fear); } }

    /// <summary>
    /// Forced-control priority: Fear > Taunt. Trong cùng type: impact cao hơn thắng; bằng nhau latest wins.
    /// Effect thấp hơn vẫn tồn tại để có thể resume sau khi effect cao hơn kết thúc.
    /// </summary>
    public bool TryGetDominantForcedControl(out CombatEffectType type, out Stats source,
                                             out EffectHandle handle, out int impactLevel)
    {
        return TryGetDominantForcedControl(out type, out source, out _, out handle, out impactLevel);
    }

    public bool TryGetDominantForcedControl(out CombatEffectType type, out Stats source,
                                             out Transform anchor, out EffectHandle handle, out int impactLevel)
    {
        PruneExpiredManaged();

        ActiveCrowdControl best = FindDominantForcedNoPrune();

        if (best == null)
        {
            type = CombatEffectType.Unknown;
            source = null;
            anchor = null;
            handle = EffectHandle.Invalid;
            impactLevel = 0;
            return false;
        }

        type = best.type;
        source = best.source;
        anchor = ResolveForcedControlAnchor(best);
        handle = best.handle;
        impactLevel = best.impactLevel;
        return anchor != null;
    }

    public bool IsEffectActive(EffectHandle handle)
    {
        if (!handle.IsValid) return false;
        PruneExpiredManaged();
        PruneExpiredManagedDebuffs();
        if (_managedCc.ContainsKey(handle.InstanceId)) return true;
        if (_managedDebuffs.ContainsKey(handle.InstanceId)) return true;
        return _displacement != null && _displacement.IsActive && _displacement.Active.owner.Equals(handle);
    }

    /// <summary>
    /// Slow toàn cục: Slow thường cạnh tranh strongest-wins. Slow author AdditiveStacks được cộng theo
    /// cùng effectId + source trước, rồi tổng stack đó mới cạnh tranh với các Slow khác.
    /// Cách này hỗ trợ cả shared-expiry stack (1 managed entry, stackCount > 1) và per-stack expiry
    /// (nhiều managed entry sourceKey khác nhau nhưng cùng effectId + source).
    /// </summary>
    public float CurrentSlowAmount
    {
        get
        {
            PruneExpiredManaged();
            float strongest = 0f;
            Dictionary<string, float> additiveGroups = null;

            foreach (ActiveCrowdControl entry in _managedCc.Values)
            {
                if (entry.type != CombatEffectType.Slow) continue;

                if (entry.stackPolicy == EffectStackPolicy.AdditiveStacks)
                {
                    if (additiveGroups == null) additiveGroups = new Dictionary<string, float>();
                    string groupKey = BuildAdditiveSlowGroupKey(entry);
                    additiveGroups.TryGetValue(groupKey, out float total);
                    additiveGroups[groupKey] = total + entry.EffectiveMagnitude;
                    continue;
                }

                strongest = Mathf.Max(strongest, entry.EffectiveMagnitude);
            }

            if (additiveGroups != null)
            {
                foreach (float total in additiveGroups.Values)
                    strongest = Mathf.Max(strongest, Mathf.Clamp01(total));
            }

            return Mathf.Clamp01(strongest);
        }
    }

    private static string BuildAdditiveSlowGroupKey(ActiveCrowdControl entry)
    {
        string effectId = string.IsNullOrWhiteSpace(entry.effectId) ? CombatEffectType.Slow.ToString() : entry.effectId;
        string sourceId = entry.source != null
            ? entry.source.GetInstanceID().ToString()
            : "null:" + (entry.sourceIdentity ?? string.Empty);
        return effectId + "|" + sourceId;
    }

    public int ManagedEffectCount
    {
        get { PruneExpiredManaged(); return _managedCc.Count; }
    }

    public int ManagedDebuffCount
    {
        get { PruneExpiredManagedDebuffs(); return _managedDebuffs.Count; }
    }

    public event Action<CombatEffectInfo, EffectApplyResult> OnCrowdControlResolved;
    public event Action<DebuffRequest, EffectApplyResult> OnDebuffResolved;
    public event Action<DebuffRuntimeSnapshot> OnDebuffApplied;
    public event Action<DebuffRuntimeSnapshot> OnDebuffRefreshedOrStacked;
    public event Action<DebuffRuntimeSnapshot> OnDebuffTicked;
    public event Action<DebuffRuntimeSnapshot, EffectRemoveReason> OnDebuffRemoved;

    private void Awake()
    {
        _stats = GetComponent<Stats>();
        _modifierLedger = GetComponent<RuntimeStatModifierLedger>();
        _displacement = GetComponent<DisplacementController>();
        _controlResolver = GetComponent<StatusControlResolver>();
    }

    private void Update()
    {
        if (_stats == null) _stats = GetComponent<Stats>();

        if (_stats != null && _stats.isDead)
        {
            if (_managedCc.Count > 0)
                RemoveAllManaged(EffectRemoveReason.TargetDied);
            if (_managedDebuffs.Count > 0)
                RemoveAllManagedDebuffs(EffectRemoveReason.TargetDied, false);
            return;
        }

        PruneExpiredManaged();
        ProcessManagedDebuffTicks();
        PruneExpiredManagedDebuffs();
    }

    public EffectApplyResult ApplyCrowdControl(CombatEffectInfo request, StatusEffectContext context)
    {
        if (_stats == null) _stats = GetComponent<Stats>();
        if (_stats == null || request == null || _stats.isDead)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));

        if (HasCrowdControlImmunity)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.CrowdControlImmune));

        if (request.type == CombatEffectType.Slow && HasSlowImmunity)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.SlowImmune));

        // Locked rule: armorLevel=0 chặn nothing; equality xuyên.
        if (_stats.IsSuperArmor)
        {
            int armorLevel = _stats.EffectiveSuperArmorLevel;
            if (armorLevel > 0 && request.impactLevel < armorLevel)
                return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.BlockedBySuperArmor));
        }

        if (!IsSupportedInCurrentPhase(request.type))
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Unsupported));

        if (IsForcedControl(request.type) && !IsRequestedForcedControlSourceValid(request, context.source))
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));

        CombatEffectInfo resolved = request.CreateRuntimeCopy();
        resolved.duration = ResolveCcDuration(resolved.type, resolved.duration);

        if (IsManagedCrowdControl(resolved.type) && resolved.duration < 0.1f)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));

        // Airborne cần duration thật. Pull và Driven Knockback dùng duration như safety timeout.
        // Standard Knockback KHÔNG dùng authored duration để giữ control; legacy duration=0 vẫn hợp lệ.
        if (resolved.type == CombatEffectType.Airborne && resolved.duration <= 0f)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));
        if (resolved.type == CombatEffectType.Pull && resolved.duration <= 0f)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));
        if (resolved.type == CombatEffectType.Knockback
            && resolved.knockbackMode == KnockbackMotionMode.Driven
            && resolved.duration <= 0f)
            return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));

        if (IsManagedCrowdControl(resolved.type))
            return Resolve(request, ApplyManagedCrowdControl(resolved, context));

        if (IsDisplacement(resolved.type))
            return Resolve(request, ApplyDisplacementCrowdControl(resolved, context));

        return Resolve(request, EffectApplyResult.Reject(EffectApplyStatus.Unsupported));
    }

    public EffectApplyResult ApplyDebuff(DebuffRequest request, StatusEffectContext context)
    {
        if (_stats == null) _stats = GetComponent<Stats>();
        if (_modifierLedger == null) _modifierLedger = GetComponent<RuntimeStatModifierLedger>();

        if (_stats == null || request == null || _stats.isDead
            || string.IsNullOrWhiteSpace(request.effectId) || request.duration <= 0f)
            return ResolveDebuff(request, EffectApplyResult.Reject(EffectApplyStatus.Invalid));

        PruneExpiredManagedDebuffs();

        string effectId = request.effectId.Trim();
        string explicitSourceKey = string.IsNullOrWhiteSpace(context.sourceKey) ? null : context.sourceKey.Trim();
        string sourceIdentity = BuildSourceIdentity(context, explicitSourceKey);
        ActiveDebuff existing = FindDebuffMergeCandidate(effectId, sourceIdentity);

        if (existing != null && request.stackPolicy == EffectStackPolicy.ReplaceSameSource)
        {
            RemoveDebuffEntry(existing, EffectRemoveReason.Overridden, true);
            existing = null;
        }

        float now = GetDebuffNow(request.timeMode);
        float expiresAt = now + request.duration;
        EffectHandle handle;
        bool created = existing == null;

        if (created)
        {
            handle = new EffectHandle(_nextEffectId++);
            existing = new ActiveDebuff
            {
                handle = handle,
                effectId = effectId,
                sourceIdentity = sourceIdentity,
                source = context.source,
                cleanseable = request.cleanseable,
                stackPolicy = request.stackPolicy,
                maxStacks = Mathf.Max(1, request.maxStacks),
                stackCount = 1,
                potency = request.potency,
                timeMode = request.timeMode,
                expiresAt = expiresAt,
                nextTickAt = ResolveInitialDebuffTickTime(request.dot, now),
                applyOrder = _nextApplyOrder++,
                note = request.note,
                statModifiers = CopyModifiers(request.statModifiers),
                dot = request.dot,
                onApply = request.onApply,
                onRefreshOrStack = request.onRefreshOrStack,
                onTick = request.onTick,
                onRemove = request.onRemove
            };
            _managedDebuffs[handle.InstanceId] = existing;
        }
        else
        {
            handle = existing.handle;
            bool hadTick = existing.dot.HasDamage;
            existing.source = context.source;
            existing.cleanseable = request.cleanseable;
            existing.stackPolicy = request.stackPolicy;
            existing.maxStacks = Mathf.Max(1, request.maxStacks);
            existing.potency = request.potency;

            // Nếu một producer đổi clock mode giữa các refresh, bảo toàn remaining duration rồi
            // chuyển sang clock mới thay vì so sánh hai timestamp thuộc hai time domain khác nhau.
            if (existing.timeMode != request.timeMode)
            {
                float previousRemaining = GetDebuffRemaining(existing);
                existing.timeMode = request.timeMode;
                existing.expiresAt = now + Mathf.Max(previousRemaining, request.duration);
                if (existing.dot.HasDamage)
                    existing.nextTickAt = ResolveInitialDebuffTickTime(request.dot, now);
            }
            else
            {
                existing.expiresAt = Mathf.Max(existing.expiresAt, expiresAt);
            }
            existing.applyOrder = _nextApplyOrder++;
            existing.note = request.note;
            existing.statModifiers = CopyModifiers(request.statModifiers);
            existing.dot = request.dot;
            existing.onApply = request.onApply;
            existing.onRefreshOrStack = request.onRefreshOrStack;
            existing.onTick = request.onTick;
            existing.onRemove = request.onRemove;

            if (request.stackPolicy == EffectStackPolicy.AdditiveStacks)
                existing.stackCount = Mathf.Min(existing.maxStacks, existing.stackCount + 1);
            else
                existing.stackCount = 1;

            if (!hadTick && existing.dot.HasDamage)
                existing.nextTickAt = ResolveInitialDebuffTickTime(existing.dot, now);
        }

        RefreshDebuffModifierContributions(effectId);
        NotifyManagedDebuffStateChanged();

        DebuffRuntimeSnapshot snapshot = BuildDebuffSnapshot(existing);
        if (created)
        {
            existing.onApply?.Invoke(snapshot);
            OnDebuffApplied?.Invoke(snapshot);
        }
        else
        {
            existing.onRefreshOrStack?.Invoke(snapshot);
            OnDebuffRefreshedOrStacked?.Invoke(snapshot);
        }

        return ResolveDebuff(request, new EffectApplyResult(EffectApplyStatus.Accepted, handle, request.duration));
    }

    /// <summary>Cleanse mọi CC cleanseable hiện được controller quản; không đụng Airborne/Knockback/Pull.</summary>
    public void CleanseCrowdControl()
    {
        RemoveAllManaged(EffectRemoveReason.Cleansed);
        _stats?.ExecuteLegacyCrowdControlCleanse();
    }

    public void CleanseSlow()
    {
        RemoveManagedType(CombatEffectType.Slow, EffectRemoveReason.Cleansed);
        _stats?.ExecuteLegacySlowCleanse();
    }

    public void CleanseDebuffs()
    {
        PruneExpiredManagedDebuffs();
        if (_managedDebuffs.Count > 0)
        {
            _scratchRemove.Clear();
            foreach (KeyValuePair<int, ActiveDebuff> pair in _managedDebuffs)
                if (pair.Value.cleanseable) _scratchRemove.Add(pair.Key);

            for (int i = 0; i < _scratchRemove.Count; i++)
                if (_managedDebuffs.TryGetValue(_scratchRemove[i], out ActiveDebuff entry))
                    RemoveDebuffEntry(entry, EffectRemoveReason.Cleansed, false);

            RefreshAllStrongestDebuffGroups();
            NotifyManagedDebuffStateChanged();
        }

        // Compatibility bridge trong migration: những legacy Debuff chưa migrate vẫn được cleanup tại Stats.
        _stats?.ExecuteLegacyDebuffCleanse();
    }

    public void GrantCrowdControlImmunity(object sourceKey)
    {
        object key = sourceKey ?? this;
        bool wasImmune = HasCrowdControlImmunity;
        _ccImmunitySources.Add(key);
        if (!wasImmune) CleanseCrowdControl();
    }

    public void RevokeCrowdControlImmunity(object sourceKey)
    {
        _ccImmunitySources.Remove(sourceKey ?? this);
    }

    public void GrantSlowImmunity(object sourceKey)
    {
        object key = sourceKey ?? this;
        bool wasImmune = HasSlowImmunity;
        _slowImmunitySources.Add(key);
        if (!wasImmune) CleanseSlow();
    }

    public void RevokeSlowImmunity(object sourceKey)
    {
        _slowImmunitySources.Remove(sourceKey ?? this);
    }

    public bool RemoveEffect(EffectHandle handle, EffectRemoveReason reason = EffectRemoveReason.Manual)
    {
        if (!handle.IsValid) return false;

        if (_managedCc.Remove(handle.InstanceId))
        {
            NotifyManagedStateChanged();
            return true;
        }

        if (_managedDebuffs.TryGetValue(handle.InstanceId, out ActiveDebuff debuff))
        {
            RemoveDebuffEntry(debuff, reason, true);
            return true;
        }

        // Generic manual/owner removal may end a displacement, but normal CleanseCrowdControl never calls this.
        return _displacement != null && _displacement.Release(handle, reason);
    }

    public int GetActiveCount(CombatEffectType type)
    {
        PruneExpiredManaged();
        int count = 0;
        foreach (ActiveCrowdControl entry in _managedCc.Values)
            if (entry.type == type) count++;

        if (_displacement != null && _displacement.IsActive && MapDisplacementType(_displacement.ActiveKind) == type)
            count++;
        return count;
    }

    /// <summary>DEV helper: tóm tắt managed CC active để F9 panel kiểm tra source/stack.</summary>
    public string GetManagedDebugSummary()
    {
        PruneExpiredManaged();
        if (_managedCc.Count == 0) return "none";

        StringBuilder sb = new StringBuilder();
        foreach (ActiveCrowdControl entry in _managedCc.Values)
        {
            if (sb.Length > 0) sb.Append(" | ");
            float remain = Mathf.Max(0f, entry.expiresAt - Time.time);
            sb.Append(entry.type).Append('#').Append(entry.handle.InstanceId);
            if (!string.IsNullOrEmpty(entry.effectId)) sb.Append('(').Append(entry.effectId).Append(')');
            if (entry.type == CombatEffectType.Slow)
                sb.Append('=').Append(entry.EffectiveMagnitude.ToString("P0"));
            if (entry.stackCount > 1)
                sb.Append(" x").Append(entry.stackCount);
            if (entry.type == CombatEffectType.Taunt || entry.type == CombatEffectType.Fear)
            {
                Transform anchor = ResolveForcedControlAnchor(entry);
                if (anchor != null) sb.Append(" anchor=").Append(anchor.name);
                if (entry.source != null && (anchor == null || entry.source.transform != anchor))
                    sb.Append(" caster=").Append(entry.source.name);
            }
            sb.Append(' ').Append(remain.ToString("0.0")).Append('s');
        }
        return sb.ToString();
    }

    public bool HasDebuff(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return false;
        PruneExpiredManagedDebuffs();
        string wanted = effectId.Trim();
        foreach (ActiveDebuff entry in _managedDebuffs.Values)
            if (string.Equals(entry.effectId, wanted, StringComparison.Ordinal)) return true;
        return false;
    }

    public int GetActiveDebuffCount(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return 0;
        PruneExpiredManagedDebuffs();
        string wanted = effectId.Trim();
        int count = 0;
        foreach (ActiveDebuff entry in _managedDebuffs.Values)
            if (string.Equals(entry.effectId, wanted, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>Remove mọi managed Debuff có cùng effectId. Dùng cho mechanic consume mark (vd Challenge).</summary>
    public int RemoveDebuffsByEffectId(string effectId, EffectRemoveReason reason = EffectRemoveReason.Manual, Stats source = null)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return 0;
        PruneExpiredManagedDebuffs();
        string wanted = effectId.Trim();

        _scratchRemove.Clear();
        foreach (KeyValuePair<int, ActiveDebuff> pair in _managedDebuffs)
        {
            if (!string.Equals(pair.Value.effectId, wanted, StringComparison.Ordinal)) continue;
            if (source != null && pair.Value.source != source) continue;
            _scratchRemove.Add(pair.Key);
        }

        int removed = 0;
        for (int i = 0; i < _scratchRemove.Count; i++)
        {
            if (_managedDebuffs.TryGetValue(_scratchRemove[i], out ActiveDebuff entry))
            {
                RemoveDebuffEntry(entry, reason, false);
                removed++;
            }
        }

        if (removed > 0)
        {
            RefreshAllStrongestDebuffGroups();
            NotifyManagedDebuffStateChanged();
        }
        return removed;
    }

    public string GetManagedDebuffDebugSummary()
    {
        PruneExpiredManagedDebuffs();
        if (_managedDebuffs.Count == 0) return "none";

        StringBuilder sb = new StringBuilder();
        foreach (ActiveDebuff entry in _managedDebuffs.Values)
        {
            if (sb.Length > 0) sb.Append(" | ");
            DebuffRuntimeSnapshot snapshot = BuildDebuffSnapshot(entry);
            sb.Append(entry.effectId).Append('#').Append(entry.handle.InstanceId);
            if (entry.stackCount > 1) sb.Append(" x").Append(entry.stackCount);
            if (!entry.cleanseable) sb.Append(" [NC]");
            if (entry.timeMode == DebuffTimeMode.Unscaled) sb.Append(" [RT]");
            if (entry.stackPolicy == EffectStackPolicy.Strongest && !IsDominantStrongestDebuff(entry))
                sb.Append(" [suspended]");
            sb.Append(' ').Append(snapshot.remainingDuration.ToString("0.0")).Append('s');
        }
        return sb.ToString();
    }

    public string GetDisplacementDebugSummary()
    {
        return _displacement != null ? _displacement.GetDebugSummary() : "none";
    }

    private EffectApplyResult ApplyDisplacementCrowdControl(CombatEffectInfo resolved, StatusEffectContext context)
    {
        if (_displacement == null) _displacement = GetComponent<DisplacementController>();
        if (_displacement == null) return EffectApplyResult.Reject(EffectApplyStatus.Unsupported);

        if (resolved.type == CombatEffectType.Airborne && resolved.height < 0f)
            return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

        if (resolved.type == CombatEffectType.Knockback)
        {
            if (resolved.knockbackMode == KnockbackMotionMode.Standard)
            {
                if (resolved.force <= 0f) return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
                // 100% KB resistance = không có forced movement => không vào Control=Displaced dù chỉ 1 frame.
                if (resolved.force * (1f - Mathf.Clamp01(_stats.knockbackResistance)) <= 0.0001f)
                    return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
            }
            else
            {
                if (resolved.drivenAnchor == null || !resolved.drivenAnchor.gameObject.activeInHierarchy)
                    return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
                if (resolved.drivenDirection.sqrMagnitude <= 0.0001f)
                    return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
            }
        }

        if (resolved.type == CombatEffectType.Pull)
        {
            if (resolved.pullSpeed <= 0f) return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
            if (resolved.pullAnchorMode == PullAnchorMode.SourceTransform && context.source == null)
                return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
            if (resolved.pullAnchorMode == PullAnchorMode.TargetTransform && resolved.pullTransform == null)
                return EffectApplyResult.Reject(EffectApplyStatus.Invalid);

            Vector3 anchor;
            if (!TryResolveRequestedPullAnchor(resolved, context, out anchor))
                return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
            // Đã ở đúng điểm đích => không tạo displacement ownership rồi chờ timeout.
            if (Vector3.Distance(_stats.transform.position, anchor) <= DisplacementController.PullArrivalTolerance)
                return EffectApplyResult.Reject(EffectApplyStatus.Invalid);
        }

        string explicitEffectId = string.IsNullOrWhiteSpace(resolved.effectId) ? null : resolved.effectId.Trim();
        string explicitSourceKey = string.IsNullOrWhiteSpace(context.sourceKey) ? null : context.sourceKey.Trim();
        string sourceIdentity = BuildSourceIdentity(context, explicitSourceKey);

        DisplacementRuntimeRequest displacementRequest = new DisplacementRuntimeRequest
        {
            kind = MapDisplacementKind(resolved.type),
            owner = new EffectHandle(_nextEffectId++),
            source = context.source,
            effectId = explicitEffectId,
            sourceIdentity = sourceIdentity,
            allowIdentityRefresh = resolved.type == CombatEffectType.Pull && explicitEffectId != null,
            impactLevel = resolved.impactLevel,
            duration = resolved.duration,
            height = resolved.height,
            knockbackMode = resolved.knockbackMode,
            knockbackForce = resolved.force,
            sourcePosition = resolved.sourcePosition,
            drivenAnchor = resolved.drivenAnchor,
            drivenDirection = resolved.drivenDirection,
            drivenOffset = resolved.drivenOffset,
            pullAnchorMode = resolved.pullAnchorMode,
            pullPoint = resolved.pullPoint,
            pullTransform = resolved.pullTransform,
            pullSpeed = resolved.pullSpeed
        };

        DisplacementApplyResult displacementResult = _displacement.TryApply(displacementRequest);
        if (!displacementResult.Accepted)
            return EffectApplyResult.Reject(EffectApplyStatus.RejectedByDisplacementPriority);

        // A refresh is the same active displacement ownership; do not repeatedly interrupt every zone tick.
        if (displacementResult.StartsNewPhysicalOwnership && resolved.interruptCurrentAction)
            _stats.NotifyManagedCrowdControlInterrupt(resolved, context.source);

        return new EffectApplyResult(EffectApplyStatus.Accepted, displacementResult.handle, resolved.duration);
    }

    private bool TryResolveRequestedPullAnchor(CombatEffectInfo resolved, StatusEffectContext context, out Vector3 anchor)
    {
        anchor = _stats != null ? _stats.transform.position : Vector3.zero;
        switch (resolved.pullAnchorMode)
        {
            case PullAnchorMode.FixedPoint:
                anchor = resolved.pullPoint;
                return true;
            case PullAnchorMode.TargetTransform:
                if (resolved.pullTransform == null || !resolved.pullTransform.gameObject.activeInHierarchy) return false;
                anchor = resolved.pullTransform.position;
                return true;
            case PullAnchorMode.SourceTransform:
            default:
                if (context.source == null || context.source.isDead) return false;
                anchor = context.source.transform.position;
                return true;
        }
    }

    private EffectApplyResult ApplyManagedCrowdControl(CombatEffectInfo resolved, StatusEffectContext context)
    {
        PruneExpiredManaged();

        ActiveCrowdControl previousForcedDominant = IsForcedControl(resolved.type) ? FindDominantForcedNoPrune() : null;
        EffectHandle previousForcedHandle = previousForcedDominant != null ? previousForcedDominant.handle : EffectHandle.Invalid;

        string explicitEffectId = string.IsNullOrWhiteSpace(resolved.effectId) ? null : resolved.effectId.Trim();
        string explicitSourceKey = string.IsNullOrWhiteSpace(context.sourceKey) ? null : context.sourceKey.Trim();
        bool hasMergeIdentity = explicitEffectId != null || explicitSourceKey != null;
        string effectIdentity = explicitEffectId ?? resolved.type.ToString();
        string sourceIdentity = BuildSourceIdentity(context, explicitSourceKey);

        ActiveCrowdControl existing = null;
        if (hasMergeIdentity && resolved.stackPolicy != EffectStackPolicy.IndependentPerSource)
            existing = FindMergeCandidate(resolved.type, effectIdentity, sourceIdentity);

        float expiresAt = Time.time + resolved.duration;
        EffectHandle handle;

        if (existing != null && resolved.stackPolicy == EffectStackPolicy.ReplaceSameSource)
        {
            _managedCc.Remove(existing.handle.InstanceId);
            existing = null;
        }

        if (existing == null)
        {
            handle = new EffectHandle(_nextEffectId++);
            ActiveCrowdControl entry = new ActiveCrowdControl
            {
                handle = handle,
                type = resolved.type,
                effectId = effectIdentity,
                sourceIdentity = sourceIdentity,
                source = context.source,
                forcedControlAnchor = IsForcedControl(resolved.type)
                    ? (resolved.forcedControlAnchor != null ? resolved.forcedControlAnchor : context.source?.transform)
                    : null,
                hasExplicitForcedControlAnchor = IsForcedControl(resolved.type) && resolved.forcedControlAnchor != null,
                stackPolicy = resolved.stackPolicy,
                maxStacks = Mathf.Max(1, resolved.maxStacks),
                stackCount = 1,
                magnitudePerStack = Mathf.Clamp01(resolved.magnitude),
                expiresAt = expiresAt,
                impactLevel = resolved.impactLevel,
                interruptCurrentAction = resolved.interruptCurrentAction,
                putInterruptedSkillOnCooldown = resolved.putInterruptedSkillOnCooldown,
                applyOrder = _nextApplyOrder++,
                note = resolved.note
            };
            _managedCc[handle.InstanceId] = entry;
        }
        else
        {
            handle = existing.handle;
            existing.source = context.source;
            if (IsForcedControl(resolved.type))
            {
                existing.forcedControlAnchor = resolved.forcedControlAnchor != null
                    ? resolved.forcedControlAnchor
                    : context.source?.transform;
                existing.hasExplicitForcedControlAnchor = resolved.forcedControlAnchor != null;
            }
            existing.impactLevel = resolved.impactLevel;
            existing.interruptCurrentAction = resolved.interruptCurrentAction;
            existing.putInterruptedSkillOnCooldown = resolved.putInterruptedSkillOnCooldown;
            existing.applyOrder = _nextApplyOrder++;
            existing.note = resolved.note;
            existing.maxStacks = Mathf.Max(1, resolved.maxStacks);

            switch (resolved.stackPolicy)
            {
                case EffectStackPolicy.AdditiveStacks:
                    existing.stackCount = Mathf.Min(existing.maxStacks, existing.stackCount + 1);
                    existing.magnitudePerStack = Mathf.Clamp01(resolved.magnitude);
                    existing.expiresAt = Mathf.Max(existing.expiresAt, expiresAt);
                    break;

                case EffectStackPolicy.Strongest:
                    existing.magnitudePerStack = Mathf.Max(existing.magnitudePerStack, Mathf.Clamp01(resolved.magnitude));
                    existing.expiresAt = Mathf.Max(existing.expiresAt, expiresAt);
                    break;

                case EffectStackPolicy.RefreshSameSource:
                default:
                    existing.stackCount = 1;
                    existing.magnitudePerStack = Mathf.Clamp01(resolved.magnitude);
                    // Refresh không được vô tình rút ngắn một instance đang còn lâu hơn.
                    existing.expiresAt = Mathf.Max(existing.expiresAt, expiresAt);
                    break;
            }
        }

        // Với forced-control: chỉ application THỰC SỰ trở thành dominant mới được interrupt.
        // Lower-impact Taunt/Fear vẫn có thể tồn tại suspended nhưng không được cắt ngang action hiện tại.
        bool shouldInterrupt = resolved.interruptCurrentAction && resolved.type != CombatEffectType.Slow;
        if (shouldInterrupt && IsForcedControl(resolved.type))
        {
            ActiveCrowdControl newDominant = FindDominantForcedNoPrune();
            shouldInterrupt = newDominant != null
                           && newDominant.handle.Equals(handle)
                           && !newDominant.handle.Equals(previousForcedHandle);
        }

        if (shouldInterrupt)
            _stats.NotifyManagedCrowdControlInterrupt(resolved, context.source);

        NotifyManagedStateChanged();
        return new EffectApplyResult(EffectApplyStatus.Accepted, handle, resolved.duration);
    }

    private ActiveCrowdControl FindMergeCandidate(CombatEffectType type, string effectId, string sourceIdentity)
    {
        foreach (ActiveCrowdControl entry in _managedCc.Values)
        {
            if (entry.type != type) continue;
            if (!string.Equals(entry.effectId, effectId, StringComparison.Ordinal)) continue;
            if (!string.Equals(entry.sourceIdentity, sourceIdentity, StringComparison.Ordinal)) continue;
            return entry;
        }
        return null;
    }

    private ActiveDebuff FindDebuffMergeCandidate(string effectId, string sourceIdentity)
    {
        foreach (ActiveDebuff entry in _managedDebuffs.Values)
        {
            if (!string.Equals(entry.effectId, effectId, StringComparison.Ordinal)) continue;
            if (!string.Equals(entry.sourceIdentity, sourceIdentity, StringComparison.Ordinal)) continue;
            return entry;
        }
        return null;
    }

    private static List<RuntimeStatModifierSpec> CopyModifiers(List<RuntimeStatModifierSpec> source)
    {
        return source == null
            ? new List<RuntimeStatModifierSpec>()
            : new List<RuntimeStatModifierSpec>(source);
    }

    private static float ResolveInitialDebuffTickTime(DebuffDotSpec dot, float now)
    {
        if (!dot.HasDamage) return float.PositiveInfinity;
        return dot.tickImmediately ? now : now + Mathf.Max(0.01f, dot.tickInterval);
    }

    private void ProcessManagedDebuffTicks()
    {
        if (_managedDebuffs.Count == 0 || _stats == null || _stats.isDead) return;

        _scratchDebuffIteration.Clear();
        foreach (ActiveDebuff entry in _managedDebuffs.Values) _scratchDebuffIteration.Add(entry);

        for (int entryIndex = 0; entryIndex < _scratchDebuffIteration.Count; entryIndex++)
        {
            ActiveDebuff entry = _scratchDebuffIteration[entryIndex];
            if (entry == null || !_managedDebuffs.ContainsKey(entry.handle.InstanceId)) continue;
            if (!entry.dot.HasDamage || !ShouldDebuffContribute(entry)) continue;

            float now = GetDebuffNow(entry.timeMode);
            float interval = Mathf.Max(0.01f, entry.dot.tickInterval);
            int safety = 0;
            while (entry.nextTickAt <= now + 0.0001f
                   && entry.nextTickAt <= entry.expiresAt + 0.0001f
                   && safety++ < 64)
            {
                int stackMultiplier = entry.stackPolicy == EffectStackPolicy.AdditiveStacks
                    ? Mathf.Max(1, entry.stackCount)
                    : 1;

                DamageInfo tick = new DamageInfo
                {
                    attacker = entry.source,
                    sourceType = DamageSourceType.DoT,
                    sourcePosition = entry.source != null ? entry.source.transform.position : _stats.transform.position,
                    physDamage = Mathf.Max(0f, entry.dot.physDamage) * stackMultiplier,
                    magicDamage = Mathf.Max(0f, entry.dot.magicDamage) * stackMultiplier,
                    trueDamage = Mathf.Max(0f, entry.dot.trueDamage) * stackMultiplier
                };
                _stats.TakeDamage(tick);

                DebuffRuntimeSnapshot snapshot = BuildDebuffSnapshot(entry);
                entry.onTick?.Invoke(snapshot);
                OnDebuffTicked?.Invoke(snapshot);

                entry.nextTickAt += interval;
                if (_stats == null || _stats.isDead || !_managedDebuffs.ContainsKey(entry.handle.InstanceId)) break;
            }

            // Chống catch-up loop vô hạn nếu clock của Debuff nhảy quá xa.
            if (safety >= 64 && entry.nextTickAt <= now)
                entry.nextTickAt = now + interval;

            if (_stats == null || _stats.isDead) break;
        }
    }

    private void PruneExpiredManagedDebuffs()
    {
        if (_managedDebuffs.Count == 0) return;

        _scratchRemove.Clear();
        foreach (KeyValuePair<int, ActiveDebuff> pair in _managedDebuffs)
            if (GetDebuffNow(pair.Value.timeMode) >= pair.Value.expiresAt) _scratchRemove.Add(pair.Key);

        if (_scratchRemove.Count == 0) return;
        for (int i = 0; i < _scratchRemove.Count; i++)
            if (_managedDebuffs.TryGetValue(_scratchRemove[i], out ActiveDebuff entry))
                RemoveDebuffEntry(entry, EffectRemoveReason.Expired, false);

        RefreshAllStrongestDebuffGroups();
        NotifyManagedDebuffStateChanged();
    }

    private void RemoveAllManagedDebuffs(EffectRemoveReason reason, bool refreshAfterEach)
    {
        if (_managedDebuffs.Count == 0) return;
        _scratchRemove.Clear();
        foreach (int id in _managedDebuffs.Keys) _scratchRemove.Add(id);
        for (int i = 0; i < _scratchRemove.Count; i++)
            if (_managedDebuffs.TryGetValue(_scratchRemove[i], out ActiveDebuff entry))
                RemoveDebuffEntry(entry, reason, refreshAfterEach);
        RefreshAllStrongestDebuffGroups();
        NotifyManagedDebuffStateChanged();
    }

    private void RemoveDebuffEntry(ActiveDebuff entry, EffectRemoveReason reason, bool refreshGroupImmediately)
    {
        if (entry == null || !_managedDebuffs.Remove(entry.handle.InstanceId)) return;
        _modifierLedger?.RemoveAllOwnedBy(entry.handle);

        DebuffRuntimeSnapshot snapshot = BuildDebuffSnapshot(entry);
        entry.onRemove?.Invoke(snapshot, reason);
        OnDebuffRemoved?.Invoke(snapshot, reason);

        if (refreshGroupImmediately && entry.stackPolicy == EffectStackPolicy.Strongest)
            RefreshDebuffModifierContributions(entry.effectId);
        NotifyManagedDebuffStateChanged();
    }

    private DebuffRuntimeSnapshot BuildDebuffSnapshot(ActiveDebuff entry)
    {
        return new DebuffRuntimeSnapshot(
            entry.handle, entry.effectId, entry.source, _stats, entry.sourceIdentity,
            entry.stackCount, entry.maxStacks, entry.cleanseable,
            GetDebuffRemaining(entry), entry.potency);
    }

    private static float GetDebuffNow(DebuffTimeMode timeMode)
    {
        return timeMode == DebuffTimeMode.Unscaled ? Time.unscaledTime : Time.time;
    }

    private static float GetDebuffRemaining(ActiveDebuff entry)
    {
        if (entry == null) return 0f;
        return Mathf.Max(0f, entry.expiresAt - GetDebuffNow(entry.timeMode));
    }

    private bool ShouldDebuffContribute(ActiveDebuff entry)
    {
        return entry != null
            && (entry.stackPolicy != EffectStackPolicy.Strongest || IsDominantStrongestDebuff(entry));
    }

    private bool IsDominantStrongestDebuff(ActiveDebuff entry)
    {
        if (entry == null || entry.stackPolicy != EffectStackPolicy.Strongest) return true;
        ActiveDebuff best = null;
        foreach (ActiveDebuff candidate in _managedDebuffs.Values)
        {
            if (candidate.stackPolicy != EffectStackPolicy.Strongest) continue;
            if (!string.Equals(candidate.effectId, entry.effectId, StringComparison.Ordinal)) continue;
            if (best == null
                || candidate.potency > best.potency
                || (Mathf.Approximately(candidate.potency, best.potency) && candidate.applyOrder > best.applyOrder))
                best = candidate;
        }
        return best != null && best.handle.Equals(entry.handle);
    }

    private void RefreshDebuffModifierContributions(string effectId)
    {
        if (_modifierLedger == null) _modifierLedger = GetComponent<RuntimeStatModifierLedger>();
        if (_modifierLedger == null || string.IsNullOrEmpty(effectId)) return;

        foreach (ActiveDebuff entry in _managedDebuffs.Values)
        {
            if (!string.Equals(entry.effectId, effectId, StringComparison.Ordinal)) continue;
            _modifierLedger.RemoveAllOwnedBy(entry.handle);
            if (!ShouldDebuffContribute(entry)) continue;

            int copies = entry.stackPolicy == EffectStackPolicy.AdditiveStacks
                ? Mathf.Max(1, entry.stackCount)
                : 1;
            if (entry.statModifiers == null) continue;
            for (int stack = 0; stack < copies; stack++)
                for (int i = 0; i < entry.statModifiers.Count; i++)
                    _modifierLedger.Add(entry.handle, entry.statModifiers[i]);
        }
    }

    private void RefreshAllStrongestDebuffGroups()
    {
        if (_managedDebuffs.Count == 0) return;
        HashSet<string> groups = new HashSet<string>();
        foreach (ActiveDebuff entry in _managedDebuffs.Values)
            if (entry.stackPolicy == EffectStackPolicy.Strongest) groups.Add(entry.effectId);
        foreach (string effectId in groups) RefreshDebuffModifierContributions(effectId);
    }

    private void NotifyManagedDebuffStateChanged()
    {
        _stats?.SynchronizeManagedDebuffState(
            HasDebuffNoPrune("Bleed"),
            HasDebuffNoPrune("Burn"),
            HasDebuffNoPrune(Stats.ResonanceMarkDebuffId),
            HasDebuffNoPrune(Stats.ChallengeMarkDebuffId));
    }

    private bool HasDebuffNoPrune(string effectId)
    {
        foreach (ActiveDebuff entry in _managedDebuffs.Values)
            if (string.Equals(entry.effectId, effectId, StringComparison.Ordinal)) return true;
        return false;
    }

    private string BuildSourceIdentity(StatusEffectContext context, string explicitSourceKey)
    {
        if (explicitSourceKey != null) return "key:" + explicitSourceKey;
        if (context.source != null) return "stats:" + context.source.GetInstanceID();
        return "source:null";
    }

    private void PruneExpiredManaged()
    {
        if (_managedCc.Count == 0) return;

        _scratchRemove.Clear();
        float now = Time.time;
        foreach (KeyValuePair<int, ActiveCrowdControl> pair in _managedCc)
        {
            ActiveCrowdControl entry = pair.Value;
            if (now >= entry.expiresAt
                || (IsForcedControl(entry.type) && !IsValidForcedControlEntry(entry)))
                _scratchRemove.Add(pair.Key);
        }

        if (_scratchRemove.Count == 0) return;
        for (int i = 0; i < _scratchRemove.Count; i++)
            _managedCc.Remove(_scratchRemove[i]);

        NotifyManagedStateChanged();
    }

    private void RemoveManagedType(CombatEffectType type, EffectRemoveReason reason)
    {
        if (_managedCc.Count == 0) return;
        _scratchRemove.Clear();
        foreach (KeyValuePair<int, ActiveCrowdControl> pair in _managedCc)
            if (pair.Value.type == type) _scratchRemove.Add(pair.Key);

        if (_scratchRemove.Count == 0) return;
        for (int i = 0; i < _scratchRemove.Count; i++)
            _managedCc.Remove(_scratchRemove[i]);

        NotifyManagedStateChanged();
    }

    private void RemoveAllManaged(EffectRemoveReason reason)
    {
        if (_managedCc.Count == 0) return;
        _managedCc.Clear();
        NotifyManagedStateChanged();
    }

    private bool HasManagedTypeNoPrune(CombatEffectType type)
    {
        foreach (ActiveCrowdControl entry in _managedCc.Values)
            if (entry.type == type) return true;
        return false;
    }

    private void NotifyManagedStateChanged()
    {
        _stats?.SynchronizeManagedCrowdControlState();
    }

    private float ResolveCcDuration(CombatEffectType type, float baseDuration)
    {
        if (type == CombatEffectType.Airborne || type == CombatEffectType.Knockback || type == CombatEffectType.Pull)
            return baseDuration;
        return baseDuration * (1f - Mathf.Clamp01(_stats.resistanceEffect));
    }

    private static bool IsManagedCrowdControl(CombatEffectType type)
    {
        return type == CombatEffectType.Slow
            || type == CombatEffectType.Stun
            || type == CombatEffectType.Root
            || type == CombatEffectType.Silence
            || type == CombatEffectType.Taunt
            || type == CombatEffectType.Fear;
    }

    private static bool IsForcedControl(CombatEffectType type)
        => type == CombatEffectType.Taunt || type == CombatEffectType.Fear;

    private static bool IsRequestedForcedControlSourceValid(CombatEffectInfo request, Stats source)
    {
        if (request != null && request.forcedControlAnchor != null)
            return request.forcedControlAnchor.gameObject.activeInHierarchy;

        // Self-CC không có hidden bypass: source==target vẫn được resolver bình thường xử lý.
        return source != null && !source.isDead
            && source.gameObject != null && source.gameObject.activeInHierarchy;
    }

    private bool IsValidForcedControlEntry(ActiveCrowdControl entry)
    {
        if (entry == null) return false;
        if (entry.hasExplicitForcedControlAnchor)
            return entry.forcedControlAnchor != null
                && entry.forcedControlAnchor.gameObject.activeInHierarchy;

        return entry.source != null && !entry.source.isDead
            && entry.source.gameObject != null && entry.source.gameObject.activeInHierarchy;
    }

    private Transform ResolveForcedControlAnchor(ActiveCrowdControl entry)
    {
        if (entry == null) return null;
        if (entry.hasExplicitForcedControlAnchor)
            return entry.forcedControlAnchor != null && entry.forcedControlAnchor.gameObject.activeInHierarchy
                ? entry.forcedControlAnchor
                : null;
        return entry.source != null && !entry.source.isDead && entry.source.gameObject.activeInHierarchy
            ? entry.source.transform
            : null;
    }

    private ActiveCrowdControl FindDominantForcedNoPrune()
    {
        ActiveCrowdControl bestFear = FindDominantManaged(CombatEffectType.Fear);
        if (bestFear != null) return bestFear;
        return FindDominantManaged(CombatEffectType.Taunt);
    }

    private ActiveCrowdControl FindDominantManaged(CombatEffectType type)
    {
        ActiveCrowdControl best = null;
        foreach (ActiveCrowdControl entry in _managedCc.Values)
        {
            if (entry.type != type) continue;
            if (!IsValidForcedControlEntry(entry)) continue;

            if (best == null
                || entry.impactLevel > best.impactLevel
                || (entry.impactLevel == best.impactLevel && entry.applyOrder > best.applyOrder))
                best = entry;
        }
        return best;
    }

    private static bool IsDisplacement(CombatEffectType type)
    {
        return type == CombatEffectType.Airborne
            || type == CombatEffectType.Knockback
            || type == CombatEffectType.Pull;
    }

    private static DisplacementKind MapDisplacementKind(CombatEffectType type)
    {
        switch (type)
        {
            case CombatEffectType.Airborne: return DisplacementKind.Airborne;
            case CombatEffectType.Knockback: return DisplacementKind.Knockback;
            case CombatEffectType.Pull: return DisplacementKind.Pull;
            default: return DisplacementKind.None;
        }
    }

    private static CombatEffectType MapDisplacementType(DisplacementKind kind)
    {
        switch (kind)
        {
            case DisplacementKind.Airborne: return CombatEffectType.Airborne;
            case DisplacementKind.Knockback: return CombatEffectType.Knockback;
            case DisplacementKind.Pull: return CombatEffectType.Pull;
            default: return CombatEffectType.Unknown;
        }
    }

    private static bool IsSupportedInCurrentPhase(CombatEffectType type)
    {
        return IsManagedCrowdControl(type) || IsDisplacement(type);
    }

    private EffectApplyResult ResolveDebuff(DebuffRequest request, EffectApplyResult result)
    {
        OnDebuffResolved?.Invoke(request, result);
        return result;
    }

    private EffectApplyResult Resolve(CombatEffectInfo request, EffectApplyResult result)
    {
        OnCrowdControlResolved?.Invoke(request, result);
        return result;
    }
}
