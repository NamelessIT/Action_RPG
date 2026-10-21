using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runtime owner for non-CC movement influences such as suction / gravity fields.
///
/// These influences are intentionally NOT Crowd Control:
/// - they do not change ControlState;
/// - CC immunity / resistanceEffect / Super Armor do not reject them;
/// - Cleanse CC does not remove them.
///
/// Producers refresh their influence while a target remains inside a field. Multiple suction vectors
/// add together, while the movement-speed penalty uses the strongest active field only.
/// </summary>
[DisallowMultipleComponent]
public sealed class MovementInfluenceController : MonoBehaviour
{
    private sealed class InfluenceEntry
    {
        public string sourceKey;
        public Vector3 center;
        public float suctionSpeed;
        public float moveSpeedPenalty;
        public int refreshedFrame;
    }

    private readonly Dictionary<string, InfluenceEntry> _entries = new Dictionary<string, InfluenceEntry>();
    private readonly List<string> _staleKeys = new List<string>();

    private Stats _stats;
    private NavMeshAgent _agent;
    private ModifierHandle _movePenaltyHandle = ModifierHandle.Invalid;
    private float _appliedMovePenalty;

    public int ActiveInfluenceCount => _entries.Count;
    public float CurrentStrongestMovePenalty { get; private set; }
    public Vector3 CurrentSuctionVelocity { get; private set; }

    public static MovementInfluenceController GetOrAdd(Stats target)
    {
        if (target == null) return null;
        MovementInfluenceController controller = target.GetComponent<MovementInfluenceController>();
        if (controller == null) controller = target.gameObject.AddComponent<MovementInfluenceController>();
        return controller;
    }

    /// <summary>
    /// Refresh one field contribution for the current frame.
    /// sourceKey must be stable for the lifetime of one field instance.
    /// </summary>
    public void ApplyOrRefresh(string sourceKey, Vector3 center, float suctionSpeed, float moveSpeedPenalty)
    {
        if (string.IsNullOrEmpty(sourceKey)) return;

        if (!_entries.TryGetValue(sourceKey, out InfluenceEntry entry))
        {
            entry = new InfluenceEntry { sourceKey = sourceKey };
            _entries[sourceKey] = entry;
        }

        entry.center = center;
        entry.suctionSpeed = Mathf.Max(0f, suctionSpeed);
        entry.moveSpeedPenalty = Mathf.Clamp01(moveSpeedPenalty);
        entry.refreshedFrame = Time.frameCount;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (_stats == null) _stats = GetComponent<Stats>();
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
    }

    private void LateUpdate()
    {
        CacheReferences();

        if (_stats == null || _stats.isDead || _stats.currentHp <= 0f)
        {
            ClearAll();
            return;
        }

        RemoveEntriesNotRefreshedThisFrame();
        RecalculateAggregateInfluence();
        SyncMoveSpeedPenalty();
        ApplySuctionMovement();
    }

    private void RemoveEntriesNotRefreshedThisFrame()
    {
        if (_entries.Count == 0) return;

        _staleKeys.Clear();
        foreach (KeyValuePair<string, InfluenceEntry> pair in _entries)
        {
            if (pair.Value.refreshedFrame != Time.frameCount)
                _staleKeys.Add(pair.Key);
        }

        for (int i = 0; i < _staleKeys.Count; i++)
            _entries.Remove(_staleKeys[i]);
    }

    private void RecalculateAggregateInfluence()
    {
        CurrentStrongestMovePenalty = 0f;
        CurrentSuctionVelocity = Vector3.zero;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 position = transform.position;

        foreach (InfluenceEntry entry in _entries.Values)
        {
            CurrentStrongestMovePenalty = Mathf.Max(CurrentStrongestMovePenalty, entry.moveSpeedPenalty);

            Vector3 toCenter = entry.center - position;
            toCenter.y = 0f;
            float distance = toCenter.magnitude;
            if (distance <= 0.001f || entry.suctionSpeed <= 0f) continue;

            // Do not let one field overshoot its own center in a single frame.
            float speed = Mathf.Min(entry.suctionSpeed, distance / dt);
            CurrentSuctionVelocity += toCenter / distance * speed;
        }
    }

    private void SyncMoveSpeedPenalty()
    {
        float wanted = Mathf.Clamp01(CurrentStrongestMovePenalty);
        if (Mathf.Approximately(wanted, _appliedMovePenalty)) return;

        RuntimeStatModifierLedger ledger = _stats != null ? _stats.RuntimeModifiers : null;
        if (ledger == null) return;

        if (_movePenaltyHandle.IsValid)
        {
            ledger.Remove(_movePenaltyHandle);
            _movePenaltyHandle = ModifierHandle.Invalid;
        }

        _appliedMovePenalty = wanted;
        if (wanted > 0f)
        {
            // Field penalty is a mechanic, not CombatEffectType.Slow.
            _movePenaltyHandle = ledger.Add(
                EffectHandle.Invalid,
                new RuntimeStatModifierSpec(
                    RuntimeStatKey.BaseMoveSpeedMultiplier,
                    RuntimeModifierOperation.AdditivePercent,
                    -wanted));
        }
    }

    private void ApplySuctionMovement()
    {
        Vector3 delta = CurrentSuctionVelocity * Time.deltaTime;
        delta.y = 0f;
        if (delta.sqrMagnitude <= 0.0000001f) return;

        // Additive movement: normal AI keeps its destination / attacks / skills. We only add a small
        // inward displacement after normal Update, rather than taking ownership like DisplacementController.
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Move(delta);
        }
        else
        {
            transform.position += delta;
        }
    }

    public void ClearAll()
    {
        _entries.Clear();
        CurrentStrongestMovePenalty = 0f;
        CurrentSuctionVelocity = Vector3.zero;

        if (_stats != null && _movePenaltyHandle.IsValid && _stats.RuntimeModifiers != null)
            _stats.RuntimeModifiers.Remove(_movePenaltyHandle);

        _movePenaltyHandle = ModifierHandle.Invalid;
        _appliedMovePenalty = 0f;
    }

    private void OnDisable()
    {
        ClearAll();
    }

    private void OnDestroy()
    {
        ClearAll();
    }
}
