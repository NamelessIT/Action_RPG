using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Helper chung cho đòn đánh Companion: ép loại sát thương (Physical/Magic) + nhận diện địch theo TAG.
/// </summary>
public static class CompanionCombat
{
    private static WeaponData _magicProxy;
    public static WeaponData MagicProxy
    {
        get
        {
            if (_magicProxy == null)
            {
                _magicProxy = ScriptableObject.CreateInstance<WeaponData>();
                _magicProxy.weaponAtkType = WeaponData.WeaponAtkType.Magic;
            }
            return _magicProxy;
        }
    }

    /// <summary>Lấy Stats kẻ ĐỊCH từ 1 collider (Stats ở parent, root tag "Enemy"). null nếu không phải địch.</summary>
    public static Stats GetEnemy(Collider col)
    {
        if (col == null) return null;
        Stats s = col.GetComponentInParent<Stats>();
        if (s == null || s.currentHp <= 0) return null;
        if (!s.CompareTag("Enemy")) return null;
        return s;
    }

    /// <summary>Gây 1 đòn đánh thường của Companion lên 1 mục tiêu (đúng loại sát thương).</summary>
    public static void DealHit(AllyStats attacker, Stats target, Transform source, bool isMagic,
                               int impactLvl = 0, bool stun = false, float stunTime = 0f,
                               bool knockback = false, float knockForce = 0f)
    {
        if (attacker == null || target == null || target.currentHp <= 0) return;
        WeaponData wpn = isMagic ? MagicProxy : null; // null → CombatMath mặc định Physical
        DamageHelper.ApplyStandardDamage(attacker, target, source, 1f, null, wpn, impactLvl, stun, stunTime, knockback, knockForce, sourceType: DamageSourceType.Ranged);
        attacker.NotifyOnHitEnemy(target, 0f, false); // cho ProtocolEffectManager / SyncCoreEffectManager
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ARTILLERY — đạn KHOÁ MỤC TIÊU (Target-Locking).
//  Bay tới đúng mục tiêu, KHÔNG bị kẻ địch khác chặn, nhưng bị TƯỜNG/vật cản chặn.
// ─────────────────────────────────────────────────────────────────────────────
public class CompanionLockProjectile : MonoBehaviour
{
    private AllyStats attacker;
    private Stats target;
    private bool isMagic;
    private float speed;
    private LayerMask obstacleMask;
    private bool done;

    public void Init(AllyStats atk, Stats tgt, bool magic, float spd, Color color)
    {
        attacker = atk; target = tgt; isMagic = magic; speed = spd;
        obstacleMask = LayerMask.GetMask("Obstacle");
        MageVfxHelper.AttachSphere(transform, 0.4f, color);
        Destroy(gameObject, 4f);
    }

    void Update()
    {
        if (done) return;
        if (target == null || target.currentHp <= 0) { Destroy(gameObject); return; }

        // Bắn NGANG MẶT ĐẤT: nhắm vào giữa thân mục tiêu nhưng giữ độ cao của đạn ổn định thấp.
        Vector3 tp = target.transform.position; tp.y = transform.position.y;
        Vector3 dir = tp - transform.position;
        float dist = dir.magnitude;

        if (dist > 0.01f && obstacleMask.value != 0 &&
            Physics.Raycast(transform.position, dir.normalized, speed * Time.deltaTime + 0.1f, obstacleMask))
        {
            Destroy(gameObject); return; // bị tường chặn
        }

        transform.position = Vector3.MoveTowards(transform.position, tp, speed * Time.deltaTime);

        if (dist <= 0.5f)
        {
            done = true;
            CompanionCombat.DealHit(attacker, target, transform, isMagic);
            Destroy(gameObject);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  SUPPRESSION — đạn KHOÁ MỤC TIÊU (Target-Locking) nhưng BỊ CHẶN được.
//  Homing tới mục tiêu đã chọn (cụm đông nhất). CÓ THỂ bị kẻ địch KHÁC trên đường bay
//  hoặc TƯỜNG chặn. Nổ + gây sát thương AoE 0,5f khi chạm kẻ địch ĐẦU TIÊN trúng phải.
// ─────────────────────────────────────────────────────────────────────────────
public class CompanionSeekProjectile : MonoBehaviour
{
    private AllyStats attacker;
    private Stats target;
    private Vector3 lastDir;
    private bool isMagic;
    private float speed, maxDist, aoeRadius, traveled, hitRadius;
    private LayerMask obstacleMask;
    private bool done;

    public void Init(AllyStats atk, Stats tgt, bool magic, float spd, float maxDistance,
                     float aoe, float detectRadius, Color color)
    {
        attacker = atk; target = tgt;
        isMagic = magic; speed = spd; maxDist = maxDistance; aoeRadius = aoe; hitRadius = detectRadius;
        obstacleMask = LayerMask.GetMask("Obstacle");

        Vector3 d0 = (tgt != null ? tgt.transform.position - transform.position : transform.forward);
        d0.y = 0f;
        lastDir = d0.sqrMagnitude > 0.0001f ? d0.normalized : transform.forward;

        MageVfxHelper.AttachSphere(transform, 0.4f, color);
        Destroy(gameObject, 4f);
    }

    void Update()
    {
        if (done) return;

        // KHOÁ MỤC TIÊU: liên tục cập nhật hướng bay về mục tiêu (ngang mặt đất).
        // Mục tiêu chết/biến mất → giữ hướng cũ để vẫn có thể trúng kẻ địch khác trên đường.
        if (target != null && target.currentHp > 0)
        {
            Vector3 d = target.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) lastDir = d.normalized;
        }

        float step = speed * Time.deltaTime;

        // Bị TƯỜNG/vật cản chặn → mất đạn.
        if (obstacleMask.value != 0 && Physics.Raycast(transform.position, lastDir, step + 0.1f, obstacleMask))
        {
            Destroy(gameObject); return;
        }

        transform.position += lastDir * step;
        traveled += step;

        // Trúng kẻ địch ĐẦU TIÊN trên đường (capsule dọc — bắt mọi độ cao) → nổ.
        Vector3 top = transform.position + Vector3.up * 2f;
        Vector3 bot = transform.position - Vector3.up * 2f;
        Collider[] hits = Physics.OverlapCapsule(top, bot, hitRadius);
        foreach (var h in hits)
        {
            if (CompanionCombat.GetEnemy(h) != null) { Explode(); return; }
        }
        if (traveled >= maxDist) Destroy(gameObject);
    }

    private void Explode()
    {
        if (done) return;
        done = true;
        VisualDebugHelper.DrawSphere(transform.position, aoeRadius, new Color(1f, 0.3f, 1f, 0.4f), 0.3f);
        // AoE cũng dùng capsule dọc để không bỏ sót địch lùn quanh điểm nổ.
        Collider[] hits = Physics.OverlapCapsule(
            transform.position + Vector3.up * 2f, transform.position - Vector3.up * 2f, aoeRadius);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = CompanionCombat.GetEnemy(h);
            if (e != null && seen.Add(e)) CompanionCombat.DealHit(attacker, e, transform, isMagic);
        }
        Destroy(gameObject);
    }
}
