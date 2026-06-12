using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  COMPANION ATTACK BEHAVIOR — strategy theo role.
//
//  Mức nền (giai đoạn này): mỗi behavior cung cấp QUYẾT ĐỊNH cơ bản cho CompanionAI:
//    - DesiredRange: khoảng cách muốn giữ với mục tiêu (kite/áp sát).
//    - ShouldFlee:   có nên lùi khi địch lại quá gần / máu thấp không.
//    - PickTarget:   chọn mục tiêu theo role (gần nhất / xa nhất / máu thấp / cụm đông).
//
//  CompanionAI gọi các hàm này nếu có Attack Module; nếu không có module thì dùng
//  logic fallback cũ (giữ nguyên hành vi hiện tại).
//
//  TODO[T4/T5]: các effect nâng cao (execute, fear, black hole, projectile block...)
//  sẽ móc thêm vào đây khi có hệ thống nền tương ứng.
// ─────────────────────────────────────────────────────────────────────────────

public abstract class CompanionAttackBehavior
{
    protected readonly CompanionAttackModuleData data;
    protected CompanionAttackBehavior(CompanionAttackModuleData d) { data = d; }

    /// <summary>Khoảng cách muốn giữ với mục tiêu.</summary>
    public virtual float DesiredRange => data != null ? data.preferredRange : 2f;

    /// <summary>Có nên lùi/kite khi mục tiêu ở khoảng cách distToTarget, máu hiện hpPercent (0-1).</summary>
    public virtual bool ShouldFlee(float distToTarget, float hpPercent) => false;

    /// <summary>Chọn mục tiêu trong danh sách (đã lọc sống). null nếu rỗng.</summary>
    public virtual Transform PickTarget(Vector3 self, IReadOnlyList<Transform> candidates)
        => Nearest(self, candidates);

    // ── Factory: tạo behavior theo role ─────────────────────────────────────
    public static CompanionAttackBehavior Create(CompanionAttackModuleData d)
    {
        if (d == null) return null;
        switch (d.role)
        {
            case CompanionRole.Sniper:    return new SniperBehavior(d);
            case CompanionRole.Berserker: return new BerserkerBehavior(d);
            case CompanionRole.Control:   return new ControlBehavior(d);
            case CompanionRole.Vanguard:  return new VanguardBehavior(d);
            default:                      return new BerserkerBehavior(d);
        }
    }

    // ── Helpers chọn mục tiêu ───────────────────────────────────────────────
    protected static Transform Nearest(Vector3 self, IReadOnlyList<Transform> c)
    {
        Transform best = null; float min = Mathf.Infinity;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            float d = Vector3.Distance(self, c[i].position);
            if (d < min) { min = d; best = c[i]; }
        }
        return best;
    }

    protected static Transform Farthest(Vector3 self, IReadOnlyList<Transform> c)
    {
        Transform best = null; float max = -1f;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            float d = Vector3.Distance(self, c[i].position);
            if (d > max) { max = d; best = c[i]; }
        }
        return best;
    }

    protected static Transform LowestHp(IReadOnlyList<Transform> c)
    {
        Transform best = null; float min = Mathf.Infinity;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            var s = c[i].GetComponent<Stats>();
            if (s == null || s.isDead) continue;
            if (s.currentHp < min) { min = s.currentHp; best = c[i]; }
        }
        return best;
    }

    /// <summary>Mục tiêu có NHIỀU đồng bọn xung quanh nhất (cho Control AoE).</summary>
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

// ── SNIPER: giữ khoảng cách xa, kite, target xa/máu thấp ────────────────────
public class SniperBehavior : CompanionAttackBehavior
{
    public SniperBehavior(CompanionAttackModuleData d) : base(d) { }
    public override float DesiredRange => Mathf.Max(10f, data.preferredRange);
    // Lùi khi địch lại gần hơn 70% desired range — ưu tiên sống sót.
    public override bool ShouldFlee(float distToTarget, float hpPercent) => distToTarget < DesiredRange * 0.7f;
    // Ưu tiên máu thấp để dứt điểm, không có thì xa nhất.
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c)
        => LowestHp(c) ?? Farthest(self, c);
    // TODO[T4/T5]: pierce terrain; execute enemy < 15% HP.
}

// ── BERSERKER: lao vào gần nhất, không bỏ chạy ──────────────────────────────
public class BerserkerBehavior : CompanionAttackBehavior
{
    public BerserkerBehavior(CompanionAttackModuleData d) : base(d) { }
    public override float DesiredRange => Mathf.Min(2f, data.preferredRange);
    public override bool ShouldFlee(float distToTarget, float hpPercent) => false; // không bao giờ chạy
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c) => Nearest(self, c);
    // TODO[T4/T5]: máu thấp đánh nhanh hơn; hit heal 5% missing HP; kill gây fear.
}

// ── CONTROL: giữ khoảng cách trung bình, nhắm cụm đông ──────────────────────
public class ControlBehavior : CompanionAttackBehavior
{
    public ControlBehavior(CompanionAttackModuleData d) : base(d) { }
    public override float DesiredRange => Mathf.Clamp(data.preferredRange, 5f, 7f);
    public override bool ShouldFlee(float distToTarget, float hpPercent) => distToTarget < DesiredRange * 0.6f;
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c)
        => MostClustered(c, 4f) ?? Nearest(self, c);
    // TODO[T4/T5]: paralysis stack 3 → stun/freeze 2s; black hole mỗi 15s.
}

// ── VANGUARD: chắn giữa player & enemy lớn, tank ────────────────────────────
public class VanguardBehavior : CompanionAttackBehavior
{
    public VanguardBehavior(CompanionAttackModuleData d) : base(d) { }
    public override float DesiredRange => Mathf.Min(2.5f, Mathf.Max(1.5f, data.preferredRange));
    public override bool ShouldFlee(float distToTarget, float hpPercent) => false; // tank đứng giữ
    // Nhắm mục tiêu khoẻ nhất (máu cao nhất ~ enemy to/boss).
    public override Transform PickTarget(Vector3 self, IReadOnlyList<Transform> c)
    {
        Transform best = null; float max = -1f;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i] == null) continue;
            var s = c[i].GetComponent<Stats>();
            if (s == null || s.isDead) continue;
            if (s.maxHp > max) { max = s.maxHp; best = c[i]; }
        }
        return best ?? Nearest(self, c);
    }
    // TODO[T4/T5]: intercept projectile về player + heal; damage cap mỗi hit <= 10% maxHP.
}
