using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// WardenSkill — "Seismic Slam".
/// Nhảy lên cao rồi cắm vũ khí xuống đất theo hướng xéo trước mặt (BẤT TỬ khi ở trên không,
/// không thể bị ngăn cản, không tự cancel). Trong lúc lao xuống đẩy lùi kẻ địch về phía trước.
/// Vừa tiếp đất là tung 2 đòn chém VỀ PHÍA TRƯỚC:
///  • Đòn 1: sát thương vật lý 100% physicalAtk + scale theo Armor/MR của bản thân (1 lần damage).
///  • Đòn 2: sát thương CHUẨN + choáng, scale theo Armor/MR/VIT của bản thân và maxHp của mục tiêu.
/// Sau đó nhận 150 Defense Value trong 3s.
/// </summary>
public class WardenSkill : SkillBehavior
{
    [Header("Phase 1: Nhảy & Cắm xuống")]
    public float jumpDistance = 2.0f;     // tầm nhảy tối đa (tự rút ngắn nếu có địch chắn trước)
    public float jumpHeight = 2.5f;
    public float jumpDuration = 0.5f;

    [Header("Lướt theo & ủi địch (sau khi tiếp đất)")]
    public float dashSpeed = 6.0f;        // tốc độ lướt theo tới gần
    public float dashDuration = 0.35f;    // thời gian lướt theo
    public float pushHitRadius = 2.2f;    // bán kính bắt địch để ủi
    public float pushStandoff = 1.2f;     // giữ địch cách trước mặt khoảng này khi ủi

    [Header("Vùng chém (về phía trước)")]
    public float slashWidth = 2.0f;       // bề rộng
    public float slashRange = 3.5f;       // chiều dài về trước (đủ phủ kẻ địch vừa bị đẩy)

    [Header("Đòn 1 — Vật lý (scale Armor/MR)")]
    public float slash1BaseMultiplier = 1.0f; // 100% physicalAtk
    public float armorToDamageScale = 1.5f;   // +150% Armor
    public float mrToDamageScale = 1.5f;      // +150% MR

    [Header("Đòn 2 — Sát thương chuẩn + choáng")]
    public float flatTrueDamageBase = 50f;
    public float vitToTrueDamageScale = 2.0f;   // +2 / VIT
    public float armorToTrueDamageScale = 0.5f; // +50% Armor
    public float mrToTrueDamageScale = 0.5f;    // +50% MR
    public float targetMaxHpPercent = 0.05f;    // +5% maxHp của mục tiêu
    public float stunDuration = 2.0f;

    [Header("Phase cuối: Tự buff")]
    public float defenseValueBuff = 150f;
    public float buffDuration = 3.0f;

    [Header("VFX Prefabs (tuỳ chọn)")]
    public GameObject slash1VfxPrefab;
    public GameObject slash2VfxPrefab;
    public GameObject buffVfxPrefab;

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    // Cờ giữ nguồn super armor — để nhả được cả khi coroutine bị cắt ngang.
    private bool _superArmorHeld;

    private void HoldSuperArmor()
    {
        if (_superArmorHeld) return;
        stats.PushSuperArmor(99);
        _superArmorHeld = true;
    }

    private void ReleaseSuperArmor()
    {
        if (!_superArmorHeld) return;
        stats.PopSuperArmor(99);
        _superArmorHeld = false;
    }

    protected override void OnUnequip()
    {
        // Tháo skill giữa lúc chiêu đang chạy: finally của coroutine KHÔNG chắc chạy,
        // nên nhả ở đây. Có cờ nên gọi hai lần cũng không trừ dư.
        ReleaseSuperArmor();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(WardenSkillRoutine());
        return true;
    }

    private IEnumerator WardenSkillRoutine()
    {
        // Vanguard U1: +20% Defense Value | Vanguard U3: +20% scale Armor/MR
        // Warrior U1:  +20% stun          | Warrior U3:  +20% sát thương cơ bản
        float vU1 = stats != null ? stats.vanguardSkillU1 : 0f;
        float vU3 = stats != null ? stats.vanguardSkillU3 : 0f;
        float wU1 = stats != null ? stats.warriorSkillU1  : 0f;
        float wU3 = stats != null ? stats.warriorSkillU3  : 0f;

        float effDefBuff       = defenseValueBuff      * (1f + vU1);
        float effArmorToDmg    = armorToDamageScale    * (1f + vU3);
        float effMRToDmg       = mrToDamageScale       * (1f + vU3);
        float effArmorToTrue   = armorToTrueDamageScale * (1f + vU3);
        float effMRToTrue      = mrToTrueDamageScale    * (1f + vU3);
        float effStun          = stunDuration           * (1f + wU1);
        float effSlash1Mult    = slash1BaseMultiplier   * (1f + wU3);
        float effTrueDmgBase   = flatTrueDamageBase      * (1f + wU3);

        player.isUsingSpecialSkill = true; // khóa input — không tự cancel

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = player.transform.forward;
        forward.y = 0; forward.Normalize();
        Quaternion rot = Quaternion.LookRotation(forward);

        bool originalUseGravity = rb.useGravity;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

        // Không thể bị ngăn cản suốt skill. Push/Pop thay cho gán-rồi-khôi-phục để không đạp nguồn khác.
        HoldSuperArmor();

        try
        {
            // ---------- PHASE 1: NHẢY + CẮM XUỐNG ----------
            stats.isInvincible = true;
            Debug.Log("<color=cyan>WARDEN: NHẢY LÊN CAO!</color>");

            Vector3 startPos = transform.position;

            // Không nhảy QUA đầu kẻ địch: rút ngắn tầm nhảy tới kẻ địch gần nhất phía trước.
            float landDist = Mathf.Min(jumpDistance, NearestFrontEnemyDistance(startPos, forward, jumpDistance));
            Vector3 targetPos = startPos + forward * landDist;
            if (NavMesh.SamplePosition(targetPos, out var navHit, 2.0f, NavMesh.AllAreas))
                targetPos = navHit.position;

            float timer = 0f;
            while (timer < jumpDuration)
            {
                float t = timer / jumpDuration;
                Vector3 cur = Vector3.Lerp(startPos, targetPos, t);
                cur.y += Mathf.Sin(t * Mathf.PI) * jumpHeight;
                rb.MovePosition(cur);

                timer += Time.fixedDeltaTime;
                yield return waitFixed;
            }
            rb.MovePosition(targetPos);
            stats.isInvincible = false; // tiếp đất → hết bất tử

            // ---------- PHASE 2: LƯỚT THEO + ỦI ĐỊCH ----------
            // Lướt về phía trước bám theo, giữ kẻ địch ngay trước mặt (không bị bỏ lại xa) rồi mới chém.
            Debug.Log("<color=orange>WARDEN: LƯỚT THEO ỦI ĐỊCH!</color>");
            float dashTimer = 0f;
            while (dashTimer < dashDuration)
            {
                Vector3 nextPos = rb.position + forward * dashSpeed * Time.fixedDeltaTime;
                rb.MovePosition(nextPos);
                DragEnemiesForward(nextPos, forward);
                dashTimer += Time.fixedDeltaTime;
                yield return waitFixed;
            }
            rb.linearVelocity = Vector3.zero;

            if (slash1VfxPrefab) Instantiate(slash1VfxPrefab, transform.position + forward * (slashRange * 0.5f), rot);
            yield return new WaitForSeconds(0.1f);

            // ---------- ĐÒN 1: VẬT LÝ (1 lần damage, scale Armor/MR) ----------
            Debug.Log("<color=yellow>WARDEN: CHÉM ĐÒN 1!</color>");
            Vector3 c1 = transform.position + forward * (slashRange * 0.5f);
            VisualDebugHelper.DrawBox(c1, new Vector3(slashWidth, 2f, slashRange), rot, new Color(1f, 0.9f, 0.2f, 0.4f), 0.2f);

            stats.EnterCombat();
            float bonusDef = stats.armor * effArmorToDmg + stats.magicResist * effMRToDmg;
            foreach (Stats e in ForwardEnemies(c1, rot))
            {
                // Base 100% physicalAtk (qua CombatMath → có giáp + crit) GỘP với phần Armor/MR
                // (bỏ qua giáp địch) thành MỘT lần TakeDamage duy nhất.
                float t = CombatMath.CalculateDirectionFactor(transform, e);
                bool crit = CombatMath.CheckIsCrit(stats.critChance);
                var dmg = CombatMath.CalculateFullDamage(stats, e, t, crit, null, null, effSlash1Mult);
                float total = dmg.phys + dmg.magic + bonusDef;

                e.TakeDamage(new DamageInfo
                {
                    physDamage = total,
                    attacker = stats,
                    sourcePosition = transform.position,
                    isCrit = crit,
                    impactLevel = 1
                });
            }

            // ---------- ĐÒN 2: SÁT THƯƠNG CHUẨN + CHOÁNG ----------
            yield return new WaitForSeconds(0.3f);
            Debug.Log("<color=red>WARDEN: CHÉM ĐÒN 2 (SÁT THƯƠNG CHUẨN)!</color>");
            Vector3 c2 = transform.position + forward * (slashRange * 0.5f);
            VisualDebugHelper.DrawBox(c2, new Vector3(slashWidth, 2f, slashRange), rot, new Color(1f, 0.15f, 0.15f, 0.5f), 0.25f);
            if (slash2VfxPrefab) Instantiate(slash2VfxPrefab, c2, rot);

            float selfVit = stats.VIT;
            foreach (Stats e in ForwardEnemies(c2, rot))
            {
                float trueDamage = effTrueDmgBase
                                 + stats.armor * effArmorToTrue
                                 + stats.magicResist * effMRToTrue
                                 + selfVit * vitToTrueDamageScale
                                 + e.maxHp * targetMaxHpPercent;

                var wardenInfo = new DamageInfo
                {
                    sourcePosition = transform.position,
                    attacker = stats,
                    trueDamage = trueDamage, // bỏ qua giáp/kháng phép
                    impactLevel = 2 // phá siêu giáp
                };
                // [CC] Stun qua effect system, impact 2 trên chính CombatEffectInfo.
                wardenInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, effStun)
                { impactLevel = 2, sourcePosition = transform.position });
                e.TakeDamage(wardenInfo);
                Debug.Log($"<color=red>True Damage:</color> {trueDamage:F0} lên {e.name}");
            }

            // ---------- TỰ BUFF DEFENSE VALUE ----------
            StartCoroutine(DefenseBuffRoutine(effDefBuff));
            yield return new WaitForSeconds(0.2f);
        }
        finally
        {
            rb.useGravity = originalUseGravity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            stats.isInvincible = false;
            ReleaseSuperArmor();

            player.isUsingSpecialSkill = false;
        }
    }

    // Khoảng cách tới kẻ địch gần nhất PHÍA TRƯỚC (trong tầm), để không nhảy qua đầu.
    private float NearestFrontEnemyDistance(Vector3 origin, Vector3 forward, float maxDist)
    {
        Collider[] hits = Physics.OverlapSphere(origin, maxDist, player.dangerLayer);
        float min = maxDist;
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var col in hits)
        {
            Stats e = col.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;

            Vector3 to = e.transform.position - origin; to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) { min = 0f; continue; }     // trùng vị trí → không nhảy
            if (Vector3.Angle(forward, to) > 60f) continue;            // chỉ tính phía trước
            float d = to.magnitude;
            if (d < min) min = d;
        }
        return min;
    }

    // Ủi kẻ địch ĐI THEO trước mặt: giữ địch cách player ~pushStandoff về phía trước,
    // để player lướt tới đâu địch bị đẩy tới đó (không bị bỏ lại xa).
    private void DragEnemiesForward(Vector3 playerPos, Vector3 forward)
    {
        Collider[] hits = Physics.OverlapSphere(playerPos + Vector3.up * 0.5f, pushHitRadius, player.dangerLayer);
        HashSet<Stats> done = new HashSet<Stats>();
        foreach (var col in hits)
        {
            Stats e = col.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !done.Add(e)) continue;

            // Chỉ ủi kẻ địch ở phía trước (không kéo kẻ ở sau lưng).
            Vector3 to = e.transform.position - playerPos; to.y = 0f;
            if (Vector3.Dot(to, forward) < -0.2f) continue;

            Vector3 ideal = playerPos + forward * pushStandoff;
            ideal.y = e.transform.position.y;

            NavMeshAgent agent = e.GetComponentInParent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.Warp(ideal);
            }
            else
            {
                e.transform.position = ideal;
            }
        }
    }

    // Kẻ địch trong hộp VỀ PHÍA TRƯỚC (center đã đẩy ra trước nửa chiều dài), dedupe.
    private List<Stats> ForwardEnemies(Vector3 center, Quaternion rot)
    {
        Collider[] hits = Physics.OverlapBox(center, new Vector3(slashWidth * 0.5f, 1f, slashRange * 0.5f), rot, player.dangerLayer);
        List<Stats> result = new List<Stats>();
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var col in hits)
        {
            Stats e = col.GetComponentInParent<Stats>();
            if (e != null && e.currentHp > 0 && seen.Add(e)) result.Add(e);
        }
        return result;
    }

    private IEnumerator DefenseBuffRoutine(float buffAmount)
    {
        stats.defenseValue += buffAmount;
        Debug.Log($"<color=green>WARDEN BUFF:</color> +{buffAmount} Defense Value (tổng {stats.defenseValue}).");

        // VFX (tạm): aura phòng thủ NHỎ, BÁM THEO người.
        GameObject aura = MageVfxHelper.AttachSphere(transform, 0.6f, new Color(0.2f, 0.5f, 1f, 0.3f));
        aura.transform.localPosition = Vector3.up * 1f;
        GameObject buffVfx = buffVfxPrefab != null ? Instantiate(buffVfxPrefab, transform) : null;

        yield return new WaitForSeconds(buffDuration);

        stats.defenseValue -= buffAmount;
        if (aura != null) Destroy(aura);
        if (buffVfx != null) Destroy(buffVfx);
        Debug.Log($"<color=gray>Warden Buff hết hạn:</color> Defense Value còn {stats.defenseValue}.");
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = (Application.isPlaying && stats != null && stats.facingDirection != Vector3.zero)
            ? stats.facingDirection : transform.forward;
        forward.y = 0f;
        if (forward == Vector3.zero) return;
        forward.Normalize();
        Quaternion rot = Quaternion.LookRotation(forward);

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + forward * (slashRange * 0.5f), rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(slashWidth, 2f, slashRange));
        Gizmos.matrix = old;
    }
}
