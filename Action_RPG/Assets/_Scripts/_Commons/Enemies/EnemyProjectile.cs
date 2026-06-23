using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [EAM-03] Đạn của Enemy — dùng cho các style ProjectileDirectional / ProjectileTargeted (P1-EAM-02B).
/// Hỗ trợ bay theo HƯỚNG cố định hoặc KHOÁ MỤC TIÊU (homing). Chỉ gây sát thương Player/Ally
/// (không enemy friendly-fire, không trúng owner). Damage tính qua CombatMath với EnemyStats attacker +
/// clone CombatEffectInfo từ module (per-hit, +impactBonus, không mutate ScriptableObject).
/// Hit dedupe + safe destroy theo lifetime/va chạm; mất target vẫn bay tiếp theo hướng cũ (không null-ref).
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    private EnemyStats attacker;
    private Transform homingTarget;   // optional — chỉ ProjectileTargeted
    private Vector3 dir;              // hướng bay hiện tại (ngang mặt đất)
    private float speed;
    private float damageMultiplier;
    private int impactBonus;
    private List<CombatEffectInfo> effects; // tham chiếu list của module — CLONE khi gây hit
    private float hitRadius;
    private bool pierce;              // true = xuyên (dedupe), false = nổ ở mục tiêu đầu

    private LayerMask obstacleMask;
    private readonly HashSet<Stats> _hit = new HashSet<Stats>();
    private bool _inited;

    /// <summary>Khởi tạo đạn. direction dùng khi bắn thẳng; target (optional) để khoá mục tiêu (homing).</summary>
    public void Init(EnemyStats atk, Vector3 direction, Transform target, float spd, float lifetime,
                     float dmgMult, int impBonus, List<CombatEffectInfo> moduleEffects,
                     float radius = 0.4f, bool piercing = false)
    {
        attacker = atk;
        homingTarget = target;
        dir = direction; dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        speed = spd;
        damageMultiplier = dmgMult > 0f ? dmgMult : 1f;
        impactBonus = impBonus;
        effects = moduleEffects;
        hitRadius = Mathf.Max(0.1f, radius);
        pierce = piercing;
        obstacleMask = LayerMask.GetMask("Obstacle"); // 0 nếu chưa có layer → không chặn
        _inited = true;
        Destroy(gameObject, Mathf.Max(0.1f, lifetime)); // an toàn: tự huỷ theo lifetime
    }

    private void Update()
    {
        if (!_inited) return;
        if (attacker == null) { Destroy(gameObject); return; } // mất owner → huỷ an toàn

        // [HOMING] còn target sống → cập nhật hướng; mất target → GIỮ hướng cũ (bay tiếp, không null-ref).
        if (homingTarget != null)
        {
            Stats ts = homingTarget.GetComponent<Stats>();
            if (ts == null || ts.currentHp <= 0) homingTarget = null;
            else
            {
                Vector3 d = homingTarget.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
            }
        }

        float step = speed * Time.deltaTime;

        // Bị tường/vật cản chặn → mất đạn.
        if (obstacleMask.value != 0 && Physics.Raycast(transform.position, dir, step + 0.1f, obstacleMask))
        { Destroy(gameObject); return; }

        transform.position += dir * step;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);

        // Va chạm — capsule dọc để bắt mọi độ cao của mục tiêu.
        Vector3 top = transform.position + Vector3.up * 1.5f;
        Vector3 bot = transform.position - Vector3.up * 1.5f;
        foreach (var col in Physics.OverlapCapsule(top, bot, hitRadius))
        {
            Stats victim = GetVictim(col);
            if (victim == null) continue;
            if (!_hit.Add(victim)) continue; // dedupe
            DealHit(victim);
            if (!pierce) { Destroy(gameObject); return; }
        }
    }

    /// <summary>Chỉ Player/Ally còn sống, KHÔNG phải owner. null nếu không hợp lệ.</summary>
    private Stats GetVictim(Collider col)
    {
        if (col == null) return null;
        Stats s = col.GetComponentInParent<Stats>();
        if (s == null || s.currentHp <= 0) return null;
        if (s == (Stats)attacker) return null; // không trúng owner
        if (!s.CompareTag("Player") && !s.CompareTag("Ally")) return null; // không enemy friendly-fire
        return s;
    }

    private void DealHit(Stats victim)
    {
        float t = CombatMath.CalculateDirectionFactor(transform, victim);

        DamageInfo info = new DamageInfo
        {
            sourcePosition = transform.position,
            attacker = attacker,
            impactLevel = attacker.monsterRank,
            sourceType = DamageSourceType.Ranged,
        };

        // CLONE module effects per-hit (sourcePosition riêng, +impactBonus) — không mutate ScriptableObject.
        if (effects != null)
        {
            foreach (var src in effects)
            {
                if (src == null) continue;
                info.AddEffect(new CombatEffectInfo(src.type, src.duration)
                {
                    force = src.force,
                    height = src.height,
                    magnitude = src.magnitude,
                    impactLevel = src.impactLevel + impactBonus,
                    sourcePosition = transform.position,
                    respectEffectResistance = src.respectEffectResistance,
                    interruptCurrentAction = src.interruptCurrentAction,
                    putInterruptedSkillOnCooldown = src.putInterruptedSkillOnCooldown,
                    note = src.note,
                });
            }
        }

        bool isCrit = CombatMath.CheckIsCrit(attacker.baseCritChance);
        info.isCrit = isCrit;
        var dmg = CombatMath.CalculateFullDamage(attacker, victim, t, isCrit, null, null, damageMultiplier, false);
        info.physDamage = dmg.phys;
        info.magicDamage = dmg.magic;
        info.trueDamage = dmg.trueDmg;

        victim.TakeDamage(info);
    }
}
