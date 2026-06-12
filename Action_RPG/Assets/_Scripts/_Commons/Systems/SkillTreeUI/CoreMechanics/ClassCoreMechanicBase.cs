using UnityEngine;

/// <summary>
/// Base abstract cho tất cả Core Mechanic handler của từng Class.
/// Gắn lên player GameObject cùng với Passive script tương ứng.
///
/// Mỗi handler xử lý 3 Core Mechanic (CM1, CM2, CM3) và 3 Upgrade Skill node (U1, U2, U3)
/// của một class cụ thể trong Tầng 4 Skill Tree.
/// </summary>
public abstract class ClassCoreMechanicBase : MonoBehaviour
{
    /// <summary>Tên class khớp với ClassSkillTreeData.className (case-insensitive).</summary>
    public abstract string ClassName { get; }

    protected AllyStats stats;
    protected PlayerController player;

    protected virtual void Awake()
    {
        stats  = GetComponent<AllyStats>();
        player = GetComponent<PlayerController>();
    }

    /// <summary>Kích hoạt Core Mechanic. index = 1, 2 hoặc 3.</summary>
    public abstract void Apply(int index);

    /// <summary>Hoàn trả Core Mechanic (khi reset skill tree). index = 1, 2 hoặc 3.</summary>
    public abstract void Revert(int index);

    /// <summary>Kích hoạt Upgrade Skill node. index = 1, 2 hoặc 3 (2 = −1s CD universal).</summary>
    public virtual void ApplyUpgrade(int index)
    {
        if (stats == null) return;
        if (index == 2) stats.flatSkillCooldownReduction += 1f;
        // index 1 và 3: mỗi subclass override để set field per-class tương ứng
    }

    /// <summary>Hoàn trả Upgrade Skill node.</summary>
    public virtual void RevertUpgrade(int index)
    {
        if (stats == null) return;
        if (index == 2) stats.flatSkillCooldownReduction = Mathf.Max(0f, stats.flatSkillCooldownReduction - 1f);
    }
}
