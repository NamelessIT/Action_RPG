using System.Collections;
using UnityEngine;

/// <summary>
/// WPN_SP_T5_01: đánh thường của giáo thành đường thẳng xuyên (tầm rất xa),
/// có thể BẺ GÓC tối đa 2 lần để tìm mục tiêu. Mỗi lần bẻ góc giảm 25% sát thương đoạn sau đó.
/// • Đoạn 1 (0 lần bẻ): 100% — dùng ctx.ApplyDamage (tích hợp đầy đủ: crit, on-hit, lifesteal...).
/// • Đoạn 2 (1 lần bẻ): 75%, Đoạn 3 (2 lần bẻ): 50% — dùng DamageHelper (có hệ số nhân).
/// Mỗi đoạn xuyên trúng TẤT CẢ địch trên đường.
/// Bẻ góc: nếu đoạn có trúng → bẻ tại địch xa nhất; nếu KHÔNG trúng ai → bẻ sau khi đi 2.5f để tìm hướng khác.
/// Chỉ KHÔNG bẻ khi xung quanh không còn kẻ địch nào.
/// </summary>
public class SpearChainAttackHandler : IWeaponAttackHandler
{
    private const float SEGMENT_LENGTH = 50f;   // "vươn xa vô hạn"
    private const float THRUST_RADIUS  = 0.4f;
    private const float NO_HIT_TRAVEL  = 2.5f;   // đoạn trượt: đi 2.5f rồi bẻ
    private const float BEND_SEARCH    = 30f;    // tầm tìm mục tiêu để bẻ góc ("xung quanh")
    private static readonly float[] SEGMENT_MULT = { 1f, 0.75f, 0.5f };

    public bool IsChanneled => false;

    public IEnumerator ExecuteSwing(WeaponAttackContext ctx)
    {
        Vector3 segStart = ctx.PlayerPos + Vector3.up * 0.15f;
        Vector3 segDir   = ctx.FacingDir;
        Color[] segColors =
        {
            new Color(0f, 0.9f, 1f, 0.5f),   // đoạn 1: cyan
            new Color(1f, 0.6f, 0.1f, 0.5f), // đoạn 2: cam
            new Color(1f, 0.9f, 0.2f, 0.5f), // đoạn 3: vàng
        };

        for (int seg = 0; seg < SEGMENT_MULT.Length; seg++)
        {
            Vector3 segEnd = segStart + segDir * SEGMENT_LENGTH;

            // Xuyên trúng tất cả địch trên đoạn; tìm địch XA NHẤT làm điểm bẻ.
            Stats furthest = null;
            float maxDist = -1f;
            Collider[] hits = Physics.OverlapCapsule(segStart, segEnd, THRUST_RADIUS, ctx.DangerLayer);
            foreach (var hit in hits)
            {
                Stats enemy = ctx.GetEnemyStats(hit);
                if (enemy == null) continue;
                if (!ctx.TryAddHitTarget(enemy.transform)) continue; // dedupe xuyên cả các đoạn

                if (seg == 0)
                    ctx.ApplyDamage(enemy, ctx.IsHeavy, ctx.ComboStep);
                else
                    DamageHelper.ApplyStandardDamage(ctx.Stats, enemy, ctx.Player.transform,
                        SEGMENT_MULT[seg], null, ctx.Weapon, 0, sourceType: DamageSourceType.Melee);

                float d = Vector3.Distance(segStart, enemy.transform.position);
                if (d > maxDist) { maxDist = d; furthest = enemy; }
            }

            // Điểm bẻ: có trúng → tại địch xa nhất; trượt → sau khi đi NO_HIT_TRAVEL.
            Vector3 bendOrigin = (furthest != null)
                ? furthest.transform.position
                : segStart + segDir * NO_HIT_TRAVEL;

            // Visual tạm: vẽ đoạn từ segStart -> bendOrigin.
            float drawLen = Mathf.Max(0.5f, Vector3.Distance(segStart, bendOrigin));
            VisualDebugHelper.DrawBox((segStart + bendOrigin) * 0.5f,
                new Vector3(THRUST_RADIUS * 2f, 0.3f, drawLen),
                Quaternion.LookRotation(segDir), segColors[seg], 0.6f);

            if (seg == SEGMENT_MULT.Length - 1) break; // đã hết lượt bẻ

            // Bẻ sang địch gần nhất CHƯA trúng quanh điểm bẻ.
            Stats next = FindNearestUnhit(ctx, bendOrigin);
            if (next == null) break; // KHÔNG còn địch nào quanh đây → ngừng (trường hợp duy nhất không bẻ)
            segStart = bendOrigin;
            segDir = (next.transform.position - bendOrigin); segDir.y = 0;
            if (segDir.sqrMagnitude < 0.0001f) break;
            segDir = segDir.normalized;
        }

        if (ctx.HitTargets.Count > 0 && ctx.Stats != null)
            ctx.Stats.GainSinFromAttack(ctx.HitTargets.Count);

        ctx.OnHitEvent?.Invoke(ctx.ComboStep, ctx.IsHeavy);
        yield return new WaitForSeconds(ctx.SwingDuration);
    }

    private Stats FindNearestUnhit(WeaponAttackContext ctx, Vector3 from)
    {
        Collider[] hits = Physics.OverlapSphere(from, BEND_SEARCH, ctx.DangerLayer);
        Stats best = null;
        float min = float.MaxValue;
        foreach (var hit in hits)
        {
            Stats enemy = ctx.GetEnemyStats(hit);
            if (enemy == null || enemy.currentHp <= 0) continue;
            if (ctx.HitTargets.Contains(enemy.transform)) continue;
            float d = Vector3.Distance(from, enemy.transform.position);
            if (d < min) { min = d; best = enemy; }
        }
        return best;
    }

    public Coroutine StartChanneled(WeaponAttackContext ctx, MonoBehaviour owner) => null;
    public void StopChanneled() { }
}
