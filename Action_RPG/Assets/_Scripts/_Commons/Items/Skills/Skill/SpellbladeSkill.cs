using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SpellbladeSkill — "Arcane Crescent".
/// Chém ra một đường kiếm khí hình nón trước mặt, xuyên thấu mọi kẻ địch (giảm 20% sát thương
/// mỗi lần xuyên, tối đa giảm 50%), gây 150% magicAtk và đánh dấu chúng trong 2s.
/// Nếu kẻ địch ĐANG ra đòn trong lúc bị đánh dấu → kích nổ: hủy đòn của nó, gây thêm 200% magicAtk
/// và làm choáng 1.5s.
/// </summary>
public class SpellbladeSkill : SkillBehavior
{
    [Header("Kiếm khí (hình nón)")]
    public float waveDistance = 5.0f;
    [Range(0, 360)] public float waveAngle = 90f;

    [Header("Sát thương & xuyên thấu")]
    public float baseDamageMultiplier = 1.5f; // 150% magicAtk
    public float damageFalloffPerHit = 0.2f;  // -20% mỗi lần xuyên
    public float maxFalloffPercent = 0.5f;    // giảm tối đa 50%

    [Header("Dấu ấn & kích nổ")]
    public float markDuration = 2.0f;
    public float counterDamageMult = 2.0f;    // 200% magicAtk
    public float counterStunDuration = 1.5f;

    [Header("VFX (tuỳ chọn)")]
    public GameObject waveVfxPrefab;
    public GameObject markVfxPrefab;
    public GameObject counterBlastVfxPrefab;

    private EquipmentManager equipmentManager;

    private float _effMarkDuration;
    private float _effBaseDmgMult;
    private float _effCounterStunDur;
    private float _effCounterDmgMult;

    // Vũ khí ảo loại Magic để ép sát thương PHÉP.
    private static WeaponData _magicProxy;
    private static WeaponData MagicProxy
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

    private class MarkedEnemy
    {
        public float timer;
        public GameObject vfxInstance;
        public EnemyCombat combatComponent;
    }

    private Dictionary<Stats, MarkedEnemy> markedEnemies = new Dictionary<Stats, MarkedEnemy>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    protected override void OnUnequip() { CleanUpAllMarks(); }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(WaveRoutine());
        return true;
    }

    // ==========================================================
    // THEO DÕI DẤU ẤN & KÍCH NỔ
    // ==========================================================
    void Update()
    {
        if (markedEnemies.Count == 0) return;

        List<Stats> expired = new List<Stats>();
        foreach (var kvp in markedEnemies)
        {
            Stats enemy = kvp.Key;
            MarkedEnemy data = kvp.Value;

            if (enemy == null || enemy.currentHp <= 0) { expired.Add(enemy); continue; }

            data.timer -= Time.deltaTime;
            if (data.timer <= 0) { expired.Add(enemy); continue; }

            // Kẻ địch đang ra đòn trong lúc bị đánh dấu → kích nổ.
            if (data.combatComponent != null && data.combatComponent.isAttacking)
            {
                TriggerCounterAttack(enemy, data.combatComponent);
                expired.Add(enemy);
            }
        }

        foreach (var enemy in expired)
        {
            if (enemy != null && markedEnemies.TryGetValue(enemy, out var d) && d.vfxInstance != null)
                Destroy(d.vfxInstance);
            markedEnemies.Remove(enemy);
        }
    }

    // ==========================================================
    // PHÓNG KIẾM KHÍ & ĐÁNH DẤU
    // ==========================================================
    private IEnumerator WaveRoutine()
    {
        // Mage U1: +20% thời gian dấu ấn | Duelist U1: +20% choáng | Mage U3 + Duelist U3: +20% sát thương
        float dU1 = stats != null ? stats.duelistSkillU1 : 0f;
        float dU3 = stats != null ? stats.duelistSkillU3 : 0f;
        float mU1 = stats != null ? stats.mageSkillU1    : 0f;
        float mU3 = stats != null ? stats.mageSkillU3    : 0f;
        float dmgScale = 1f + mU3 + dU3;

        _effMarkDuration   = markDuration        * (1f + mU1);
        _effBaseDmgMult    = baseDamageMultiplier * dmgScale;
        _effCounterStunDur = counterStunDuration  * (1f + dU1);
        _effCounterDmgMult = counterDamageMult    * dmgScale;

        player.isAttacking = true;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        if (waveVfxPrefab) Instantiate(waveVfxPrefab, transform.position + Vector3.up, Quaternion.LookRotation(forward));
        VisualDebugHelper.DrawSphere(transform.position + forward * (waveDistance * 0.5f), waveDistance * 0.5f, new Color(0.4f, 0.7f, 1f, 0.25f), 0.3f);
        Debug.Log("<color=cyan>SPELLBLADE: ARCANE CRESCENT!</color>");

        // 1. Quét hình nón trước mặt (xuyên thấu).
        Collider[] hits = Physics.OverlapSphere(transform.position, waveDistance, player.dangerLayer);
        List<Stats> hitEnemies = new List<Stats>();
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var hit in hits)
        {
            Stats e = hit.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;

            Vector3 dirToEnemy = (e.transform.position - transform.position).normalized;
            if (Vector3.Angle(forward, dirToEnemy) <= waveAngle / 2f) hitEnemies.Add(e);
        }

        // 2. Sắp theo khoảng cách (gần → xa) để tính giảm sát thương theo lần xuyên.
        hitEnemies.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - transform.position)
            .CompareTo(Vector3.SqrMagnitude(b.transform.position - transform.position)));

        stats.EnterCombat();

        // 3. Gây sát thương PHÉP (giảm dần) + gắn dấu ấn.
        for (int i = 0; i < hitEnemies.Count; i++)
        {
            Stats enemy = hitEnemies[i];

            float reduction = Mathf.Min(damageFalloffPerHit * i, maxFalloffPercent);
            float mult = _effBaseDmgMult * (1f - reduction);
            DamageHelper.ApplyStandardDamage(stats, enemy, transform, mult, null, MagicProxy, 0);

            if (markedEnemies.TryGetValue(enemy, out var existing))
            {
                existing.timer = _effMarkDuration; // refresh
            }
            else
            {
                GameObject aura = markVfxPrefab
                    ? Instantiate(markVfxPrefab, enemy.transform.position + Vector3.up * 2f, Quaternion.identity, enemy.transform)
                    : MageVfxHelper.AttachSphere(enemy.transform, 0.6f, new Color(0.4f, 0.7f, 1f, 0.5f));
                markedEnemies[enemy] = new MarkedEnemy
                {
                    timer = _effMarkDuration,
                    combatComponent = enemy.GetComponent<EnemyCombat>() ?? enemy.GetComponentInParent<EnemyCombat>(),
                    vfxInstance = aura
                };
            }
        }

        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    // ==========================================================
    // KÍCH NỔ KHI KẺ ĐỊCH RA ĐÒN
    // ==========================================================
    private void TriggerCounterAttack(Stats enemy, EnemyCombat enemyCombat)
    {
        Debug.Log($"<color=magenta>SPELLBLADE COUNTER:</color> Kích nổ dấu ấn {enemy.name}!");

        if (counterBlastVfxPrefab) Instantiate(counterBlastVfxPrefab, enemy.transform.position, Quaternion.identity);
        VisualDebugHelper.DrawSphere(enemy.transform.position + Vector3.up, 0.6f, new Color(0.6f, 0.2f, 1f, 0.5f), 0.2f);

        if (enemyCombat != null) enemyCombat.CancelAttack();

        // 200% magicAtk + choáng + phá siêu giáp (ép phép qua MagicProxy).
        DamageHelper.ApplyStandardDamage(stats, enemy, transform, _effCounterDmgMult, null, MagicProxy, 2, true, _effCounterStunDur);
    }

    private void CleanUpAllMarks()
    {
        foreach (var kvp in markedEnemies)
            if (kvp.Value.vfxInstance != null) Destroy(kvp.Value.vfxInstance);
        markedEnemies.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (Application.isPlaying && stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0f; forward.Normalize();

        Gizmos.color = Color.cyan;
        Vector3 leftRay = Quaternion.AngleAxis(-waveAngle / 2, Vector3.up) * forward;
        Vector3 rightRay = Quaternion.AngleAxis(waveAngle / 2, Vector3.up) * forward;
        Gizmos.DrawRay(transform.position, leftRay * waveDistance);
        Gizmos.DrawRay(transform.position, rightRay * waveDistance);
        Gizmos.DrawWireSphere(transform.position, waveDistance);
    }
}
