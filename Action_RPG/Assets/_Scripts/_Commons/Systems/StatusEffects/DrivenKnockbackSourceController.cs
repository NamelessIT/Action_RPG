using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Source-side runtime cho Knockback Driven (Warden / Chris / Vanguard charge).
///
/// Locked rule:
/// - Target đã được CC gateway accept sẽ bám anchor phía trước source; target KHÔNG tự có speed riêng.
/// - Tốc độ source = baseSpeed * (1 - max knockbackResistance của mọi target đang thật sự bị ủi).
/// - Resistance được đọc lại liên tục; enemy chết / bị displacement khác override / source release => bỏ khỏi tập ngay.
/// - Nhiều target => chỉ resistance CAO NHẤT quyết định tốc độ source.
/// - Khi không còn target hợp lệ => speed multiplier trở lại 1 ngay trong tick tiếp theo.
/// </summary>
[DisallowMultipleComponent]
public sealed class DrivenKnockbackSourceController : MonoBehaviour
{
    private sealed class CarriedTarget
    {
        public Stats target;
        public EffectHandle handle;
        public float offset;
    }

    private readonly Dictionary<int, CarriedTarget> _targets = new Dictionary<int, CarriedTarget>();
    private readonly List<int> _scratchRemove = new List<int>();

    private Stats _source;
    private Transform _anchor;
    private bool _sessionActive;
    private string _sessionId;
    private Vector3 _direction = Vector3.forward;
    private float _offset = 1f;
    private float _safetyTimeout = 2f;

    public bool SessionActive => _sessionActive;
    public int ActiveTargetCount { get { PruneInvalidTargets(); return _targets.Count; } }

    public float CurrentMaxResistance
    {
        get
        {
            PruneInvalidTargets();
            float maxResistance = 0f;
            foreach (CarriedTarget carried in _targets.Values)
            {
                if (carried.target == null || carried.target.isDead) continue;
                maxResistance = Mathf.Max(maxResistance, Mathf.Clamp01(carried.target.knockbackResistance));
            }
            return Mathf.Clamp01(maxResistance);
        }
    }

    /// <summary>Linear weight rule: 0% resist => 100% speed; 50% => 50%; 100% => source đứng lại.</summary>
    public float CurrentSpeedMultiplier => 1f - CurrentMaxResistance;

    public void BeginSession(Stats source, string sessionId, Vector3 direction, float offset, float safetyTimeout)
    {
        EndSession();

        _source = source;
        _sessionId = string.IsNullOrWhiteSpace(sessionId) ? "DRIVEN_KNOCKBACK" : sessionId;
        _offset = Mathf.Max(0f, offset);
        _safetyTimeout = Mathf.Max(0.1f, safetyTimeout);
        _direction = FlattenDirection(direction);
        EnsureAnchor();
        _anchor.gameObject.SetActive(true);
        _anchor.position = source != null ? source.transform.position : transform.position;
        _sessionActive = true;
    }

    /// <summary>
    /// Update predicted source position TRƯỚC khi physics tick hoàn tất để carried targets bám khít,
    /// không trễ 1 frame phía sau player.
    /// </summary>
    public void SetPredictedAnchor(Vector3 predictedSourcePosition, Vector3 direction)
    {
        if (!_sessionActive) return;
        EnsureAnchor();
        _anchor.position = predictedSourcePosition;
        _direction = FlattenDirection(direction);

        PruneInvalidTargets();
        foreach (CarriedTarget carried in _targets.Values)
        {
            DisplacementController displacement = carried.target != null && carried.target.StatusEffects != null
                ? carried.target.StatusEffects.Displacement
                : null;
            if (displacement != null)
                displacement.UpdateDrivenParameters(carried.handle, _anchor, _direction, carried.offset);
        }
    }

    /// <summary>
    /// Thử đưa target vào Driven Knockback qua đúng CC Immunity / Super Armor / displacement priority.
    /// False = target không bị ủi (bị block/reject/invalid).
    /// </summary>
    public bool TryCapture(Stats target, int impactLevel)
    {
        if (!_sessionActive || _source == null || target == null || target == _source || target.isDead)
            return false;

        PruneInvalidTargets();
        int key = target.GetInstanceID();
        if (_targets.TryGetValue(key, out CarriedTarget existing))
        {
            if (IsStillOwned(existing)) return true;
            _targets.Remove(key);
        }

        EnsureAnchor();
        // Giữ đúng khoảng cách tại thời điểm contact để target KHÔNG bị snap/teleport ra trước.
        // Điều này đặc biệt quan trọng với 100% KB resist: source đứng lại và target cũng đứng nguyên nơi va chạm.
        float projectedContactOffset = Vector3.Dot(target.transform.position - _anchor.position, _direction);
        float captureOffset = projectedContactOffset > 0.05f ? projectedContactOffset : Mathf.Max(0.05f, _offset);

        var effect = new CombatEffectInfo(CombatEffectType.Knockback, _safetyTimeout)
        {
            effectId = _sessionId + "_DRIVEN_KB",
            impactLevel = Mathf.Max(0, impactLevel),
            knockbackMode = KnockbackMotionMode.Driven,
            drivenAnchor = _anchor,
            drivenDirection = _direction,
            drivenOffset = captureOffset,
            sourcePosition = _source.transform.position,
            interruptCurrentAction = true,
            respectEffectResistance = false,
            note = "Driven knockback charge"
        };

        StatusEffectController effects = target.StatusEffects;
        if (effects == null) return false;

        EffectApplyResult result = effects.ApplyCrowdControl(
            effect,
            new StatusEffectContext(_source, target, _sessionId));

        if (result.status != EffectApplyStatus.Accepted || !result.handle.IsValid)
            return false;

        DisplacementController displacement = effects.Displacement;
        if (displacement == null || !displacement.Owns(result.handle))
            return false;

        _targets[key] = new CarriedTarget { target = target, handle = result.handle, offset = captureOffset };
        return true;
    }

    public void PruneInvalidTargets()
    {
        if (_targets.Count == 0) return;
        _scratchRemove.Clear();

        foreach (KeyValuePair<int, CarriedTarget> pair in _targets)
        {
            if (!IsStillOwned(pair.Value))
                _scratchRemove.Add(pair.Key);
        }

        for (int i = 0; i < _scratchRemove.Count; i++)
            _targets.Remove(_scratchRemove[i]);
    }

    public void EndSession()
    {
        if (_targets.Count > 0)
        {
            foreach (CarriedTarget carried in _targets.Values)
            {
                if (carried.target == null || carried.target.StatusEffects == null) continue;
                carried.target.StatusEffects.RemoveEffect(carried.handle, EffectRemoveReason.Manual);
            }
            _targets.Clear();
        }

        _sessionActive = false;
        _source = null;
        _sessionId = null;
        if (_anchor != null) _anchor.gameObject.SetActive(false);
    }

    private bool IsStillOwned(CarriedTarget carried)
    {
        if (carried == null || carried.target == null || carried.target.isDead || !carried.handle.IsValid)
            return false;
        StatusEffectController effects = carried.target.StatusEffects;
        return effects != null && effects.Displacement != null && effects.Displacement.Owns(carried.handle);
    }

    private void EnsureAnchor()
    {
        if (_anchor != null) return;
        GameObject go = new GameObject("[Runtime] Driven Knockback Anchor");
        go.transform.SetParent(transform, worldPositionStays: true);
        _anchor = go.transform;
    }

    private static Vector3 FlattenDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) direction = Vector3.forward;
        return direction.normalized;
    }

    private void OnDisable() => EndSession();

    private void OnDestroy()
    {
        EndSession();
        if (_anchor != null) Destroy(_anchor.gameObject);
    }
}
