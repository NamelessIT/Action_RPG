using System;
using System.Collections.Generic;
using UnityEngine;

public enum RuntimeStatKey
{
    BonusMoveSpeed,
    BaseMoveSpeedMultiplier,
    BonusAttackSpeed,
    BaseAttackSpeedMultiplier,
    Armor,
    MagicResist,
    DefenseValue,
    DamageOutputMultiplier,
    DamageTakenMultiplier,
    HealingReceivedMultiplier
}

public enum RuntimeModifierOperation
{
    Flat,
    AdditivePercent,
    Multiplicative
}

[Serializable]
public struct RuntimeStatModifierSpec
{
    public RuntimeStatKey stat;
    public RuntimeModifierOperation operation;
    public float value;

    public RuntimeStatModifierSpec(RuntimeStatKey stat, RuntimeModifierOperation operation, float value)
    {
        this.stat = stat;
        this.operation = operation;
        this.value = value;
    }
}

public struct ModifierHandle : IEquatable<ModifierHandle>
{
    private readonly int id;
    public int Id => id;
    public bool IsValid => id > 0;

    public ModifierHandle(int id) { this.id = id; }
    public bool Equals(ModifierHandle other) => id == other.id;
    public override bool Equals(object obj) => obj is ModifierHandle other && Equals(other);
    public override int GetHashCode() => id;
    public static readonly ModifierHandle Invalid = new ModifierHandle(0);
}

/// <summary>
/// Ledger modifier có ownership. Phase A chưa thay công thức stat cũ; component này là source-of-truth
/// mới để Phase B/C có thể chuyển từng consumer mà không còn pattern += / -= thủ công.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeStatModifierLedger : MonoBehaviour
{
    private sealed class Entry
    {
        public ModifierHandle handle;
        public EffectHandle owner;
        public RuntimeStatModifierSpec modifier;
    }

    private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
    private int _nextModifierId = 1;

    public ModifierHandle Add(EffectHandle owner, RuntimeStatModifierSpec modifier)
    {
        ModifierHandle handle = new ModifierHandle(_nextModifierId++);
        _entries[handle.Id] = new Entry { handle = handle, owner = owner, modifier = modifier };
        return handle;
    }

    public void Remove(ModifierHandle handle)
    {
        if (handle.IsValid) _entries.Remove(handle.Id);
    }

    public void RemoveAllOwnedBy(EffectHandle owner)
    {
        if (!owner.IsValid) return;
        List<int> remove = null;
        foreach (var pair in _entries)
        {
            if (!pair.Value.owner.Equals(owner)) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) _entries.Remove(remove[i]);
    }

    public float GetFlat(RuntimeStatKey stat)
    {
        float total = 0f;
        foreach (var entry in _entries.Values)
            if (entry.modifier.stat == stat && entry.modifier.operation == RuntimeModifierOperation.Flat)
                total += entry.modifier.value;
        return total;
    }

    public float GetAdditivePercent(RuntimeStatKey stat)
    {
        float total = 0f;
        foreach (var entry in _entries.Values)
            if (entry.modifier.stat == stat && entry.modifier.operation == RuntimeModifierOperation.AdditivePercent)
                total += entry.modifier.value;
        return total;
    }

    public float GetMultiplicativeFactor(RuntimeStatKey stat)
    {
        float factor = 1f;
        foreach (var entry in _entries.Values)
            if (entry.modifier.stat == stat && entry.modifier.operation == RuntimeModifierOperation.Multiplicative)
                factor *= entry.modifier.value;
        return factor;
    }

    public void ClearAll() => _entries.Clear();
}
