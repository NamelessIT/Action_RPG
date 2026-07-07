using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  COMPANION ATTACK BEHAVIOR — strategy theo protocolType (Slot 1).
//
//  Cung cấp QUYẾT ĐỊNH cho CompanionAI:
//    - DesiredRange:  khoảng cách muốn giữ với mục tiêu.
//    - ShouldFlee:    có nên kite (lùi) khi địch lại quá gần không.
//    - PickTarget:    chọn mục tiêu theo protocol.
//    - IsRanged / AoeRadius / DoesKnockup: tính chất đòn đánh để AI thực thi.
//
//  Không có Protocol → fallback = Carnage tay không (5 physicalAtk) theo GDD.
// ─────────────────────────────────────────────────────────────────────────────
public abstract class CompanionAttackBehavior
{
    protected readonly CompanionProtocolData data;
    /// <summary>Player transform — AI gán vào để Aegis biết "ai đang đánh player".</summary>
    public Transform Player { get; set; }

    protected CompanionAttackBehavior(CompanionProtocolData d) { data = d; }

    public CompanionProtocolType ProtocolType => data != null ? data.protocolType : CompanionProtocolType.Carnage;
    public bool IsRanged => data != null && data.isRange;
    public float AttackRange => data != null ? data.attackRange : 2f;

    /// <summary>Bán kính nổ AoE của đòn đánh (Suppression 0.5f, Carnage/Aegis = attackRange quét).</summary>
    public virtual float AoeRadius => 0f;
    /// <summary>Aegis: mỗi đòn thứ 3 hất tung mục tiêu.</summary>
    public virtual bool DoesKnockup => false;

    public virtual float DesiredRange => data != null ? data.attackRange : 2f;
    /// <summary>Địch lại gần hơn mức này → kite (chỉ protocol tầm xa).</summary>
    public virtual float FleeTriggerRange => 3f;
    public virtual bool ShouldFlee(float distToTarget, float hpPercent) => false;

    public virtual Transform PickTarget(Vector3 self, IReadOnlyList<Transform> candidates)
        => Nearest(self, candidates);

    // ── Factory theo protocolType ───────────────────────────────────────────
    public static CompanionAttackBehavior Create(CompanionProtocolData d)
    {
        // d == null → tay không kiểu Carnage (GDD: 5 physicalAtk, AI giống Carnage).
        if (d == null) return new CarnageBehavior(null);
        switch (d.protocolType)
        {
            case CompanionProtocolType.Artillery:   return new ArtilleryBehavior(d);
            case CompanionProtocolType.Carnage:     return new CarnageBehavior(d);
            case CompanionProtocolType.Suppression: return new SuppressionBehavior(d);
            case CompanionProtocolType.Aegis:       return new AegisBehavior(d);
            default:                                 return new CarnageBehavior(d);
        }
    }

    // ── Helpers chọn mục tiêu ───────────────────────────────────────────────
    protected static Transform Nearest(Vector3 self, IReadOnlyList<Transform> c)
    {
        Transform best = null; float min = Mathf.Infinity;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            float d = Vector3.SqrMagnitude(self - c[i].position);
            if (d < min) { min = d; best = c[i]; }
        }
        return best;
    }

    protected static Transform LowestHp(IReadOnlyList<Transform> c)
    {
        Transform best = null; float min = Mathf.Infinity;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            var s = c[i].GetComponentInParent<Stats>();
            if (s == null || s.isDead) continue;
            if (s.currentHp < min) { min = s.currentHp; best = c[i]; }
        }
        return best;
    }

    protected static Transform MostClustered(IReadOnlyList<Transform> c, float clusterRadius)
    {
        Transform best = null; int bestCount = -1;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            int count = 0;
            for (int j = 0; j < c.Count; j++)
                if (c[j] != null && Vector3.Distance(c[i].position, c[j].position) <= clusterRadius) count++;
            if (count > bestCount) { bestCount = count; best = c[i]; }
        }
        return best;
    }
}

// ── ARTILLERY (Xạ Kích): bắn xa, kite, ưu tiên máu thấp ─────────────────────
public class ArtilleryBehavior : CompanionAttackBehavior
{
    public ArtilleryBehavior(CompanionProtocolData d) : base(d) { }
    public override float DesiredRange => Mathf.Max(15f, AttackRange);
    public override bool ShouldFlee(float distToTarget, float hpPercent) => distToTarget < FleeTriggerRange;
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c) => LowestHp(c) ?? Nearest(self, c);
}

// ── CARNAGE (Cuồng Huyết): cận chiến AoE quét, lao vào, không bỏ chạy ────────
public class CarnageBehavior : CompanionAttackBehavior
{
    public CarnageBehavior(CompanionProtocolData d) : base(d) { }
    public override float AoeRadius => AttackRange;                 // quét quanh attackRange (mặc định 2f)
    public override float DesiredRange => data != null ? AttackRange : 2f;
    public override bool ShouldFlee(float distToTarget, float hpPercent) => false;
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c) => Nearest(self, c);
}

// ── SUPPRESSION (Áp Chế): bắn phép tầm trung, nhắm cụm đông, kite ───────────
public class SuppressionBehavior : CompanionAttackBehavior
{
    public SuppressionBehavior(CompanionProtocolData d) : base(d) { }
    public override float AoeRadius => 0.5f;                        // nổ AoE 0.5f khi trúng
    public override float DesiredRange => Mathf.Max(8f, AttackRange);
    public override bool ShouldFlee(float distToTarget, float hpPercent) => distToTarget < FleeTriggerRange;
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c) => MostClustered(c, 4f) ?? Nearest(self, c);
}

// ── AEGIS (Vệ Thần): bám player, đánh kẻ đang đánh player, đòn 3 hất tung ────
public class AegisBehavior : CompanionAttackBehavior
{
    public AegisBehavior(CompanionProtocolData d) : base(d) { }
    public override float AoeRadius => AttackRange;                 // quét 2.5f
    public override float DesiredRange => data != null ? AttackRange : 2.5f;
    public override bool ShouldFlee(float distToTarget, float hpPercent) => false;
    public override bool DoesKnockup => true;

    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c)
    {
        // Ưu tiên kẻ địch đang nhắm Player.
        if (Player != null)
        {
            for (int i = 0; i < c.Count; i++)
            {
                if (c[i] == null) continue;
                EnemyAI ai = c[i].GetComponentInParent<EnemyAI>();
                if (ai != null && ai.nearestTarget == Player) return c[i];
            }
        }
        return Nearest(self, c);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  MATRIX PROFILE — dodge% và aggro weight theo matrixType (Slot 2).
//  Không có Matrix → mặc định như Regen (GDD).
// ─────────────────────────────────────────────────────────────────────────────
public static class CompanionMatrixProfile
{
    /// <summary>Tỉ lệ né đòn định hướng của địch.</summary>
    public static float DodgeChance(CompanionMatrixType? type)
    {
        switch (type)
        {
            case CompanionMatrixType.Regen:     return 0.25f;
            case CompanionMatrixType.Phantoms:  return 0.65f;
            case CompanionMatrixType.Deflector: return 0.00f;
            default:                            return 0.25f; // không có Matrix ~ Regen
        }
    }

    /// <summary>Xác suất kẻ địch chọn tấn công Companion (thay vì Player). 0.5 = ngang nhau.</summary>
    public static float AggroWeight(CompanionMatrixType? type)
    {
        switch (type)
        {
            case CompanionMatrixType.Regen:     return 0.50f;
            case CompanionMatrixType.Phantoms:  return 0.20f;
            case CompanionMatrixType.Deflector: return 0.80f;
            default:                            return 0.50f; // không có Matrix ~ Regen
        }
    }
}
