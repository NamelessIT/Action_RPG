using System.Collections;
using UnityEngine;

/// <summary>
/// Hiệu Ứng Slot 2 (Matrix) — Rarity 3+. Theo "Tổng hợp Effect Matrix.docx".
/// Thiên về PHÒNG THỦ / SINH TỒN của Companion (regen, tàng hình, phản đòn, miễn khống chế...).
///
/// GHI CHÚ XẤP XỈ:
///  • MTX_REG_T3_01: dùng cờ stats.outCombat sẵn có làm mốc "thoát giao tranh" (ngưỡng outCombatTime
///    của game thay cho đúng 3s).
///  • MTX_REG_T4_02 (cleanse): game chưa có "registry debuff" → xóa các trạng thái phổ biến
///    (stun/bleed/đang bị giảm giáp-MR qua reset multiplier) — mang tính xấp xỉ.
///  • MTX_DEF_T3_02 phản đòn "cận chiến": không phân biệt được melee/ranged → phản 15% mọi đòn.
///  • MTX_PHA_T4_02: chưa có prefab "tàn ảnh" → taunt địch quanh Companion 2s rồi nổ tại chỗ.
/// </summary>
[DisallowMultipleComponent]
public class MatrixEffectManager : CompanionEffectManagerBase
{
    private CompanionMatrixData _module;

    // trạng thái passive cần revert khi đổi module
    private bool _ccImmuneApplied;
    private bool _moveSpeedApplied;

    // timers / cooldown (chỉ 1 module hoạt động cùng lúc)
    private float _cd1, _cd2;
    private float _lastDamageTime;
    private bool[] _regT5Passed = new bool[3]; // 75/50/25%

    protected override void Awake() { base.Awake(); }

    private void OnEnable()
    {
        if (stats == null) return;
        stats.OnHealReceived     += OnCompanionHealRaw;
        stats.OnHealDetailed     += OnCompanionHealDetailed;
        stats.OnDamageReceived   += OnCompanionDamaged;
        stats.OnDamageTakenHp    += OnCompanionDamagedHp;
        stats.OnBeforeTakeDamage += OnCompanionBeforeDamage;
        if (playerStats != null)
        {
            playerStats.OnHealReceived        += OnPlayerHeal;
            playerStats.OnPerfectDodgeTriggered += OnPlayerPerfectDodge;
            playerStats.OnBeforeTakeDamage    += OnPlayerBeforeDamage;
        }
    }
    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnHealReceived     -= OnCompanionHealRaw;
            stats.OnHealDetailed     -= OnCompanionHealDetailed;
            stats.OnDamageReceived   -= OnCompanionDamaged;
            stats.OnDamageTakenHp    -= OnCompanionDamagedHp;
            stats.OnBeforeTakeDamage -= OnCompanionBeforeDamage;
        }
        if (playerStats != null)
        {
            playerStats.OnHealReceived        -= OnPlayerHeal;
            playerStats.OnPerfectDodgeTriggered -= OnPlayerPerfectDodge;
            playerStats.OnBeforeTakeDamage    -= OnPlayerBeforeDamage;
        }
    }

    public void SetModule(CompanionMatrixData m)
    {
        // revert passive cũ
        if (_ccImmuneApplied) { stats.ccImmune = false; _ccImmuneApplied = false; }
        if (_moveSpeedApplied) { stats.bonusMoveSpeed -= 0.15f; stats.CalculateCombatStatsOnly(); _moveSpeedApplied = false; }

        _module = m;
        _cd1 = _cd2 = 0f;
        _lastDamageTime = Time.time;
        _regT5Passed = new bool[3];

        if (!Active) return;

        // bật passive tức thời
        if (_module.id == "MTX_DEF_T3_01") { stats.ccImmune = true; _ccImmuneApplied = true; }
        if (_module.id == "MTX_PHA_T3_01") { stats.bonusMoveSpeed += 0.15f; stats.CalculateCombatStatsOnly(); _moveSpeedApplied = true; }
    }

    private bool Active => _module != null && _module.HasEffect;

    private void Update()
    {
        if (!Active) return;

        // MTX_REG_T3_01: thoát giao tranh → hồi 5% maxHp/s
        if (_module.id == "MTX_REG_T3_01" && stats.outCombat && stats.currentHp < stats.maxHp)
            stats.Heal(stats.maxHp * 0.05f * Time.deltaTime, false, false, HealSource.Regen);

        // MTX_PHA_T4_02: mỗi 5s không chịu sát thương → tàn ảnh khiêu khích 2s rồi nổ
        if (_module.id == "MTX_PHA_T4_02" && Time.time - _lastDamageTime >= 5f)
        {
            _lastDamageTime = Time.time;
            StartCoroutine(DecoyRoutine());
        }
    }

    // ───────────────────────── COMPANION HEAL ─────────────────────────
    private void OnCompanionHealRaw(float amount, float overheal)
    {
        if (!Active) return;
        // MTX_REG_T3_02: 20% lượng hồi vượt mức → Shield 5s, cap 50% maxHp
        if (_module.id == "MTX_REG_T3_02" && overheal > 0f)
        {
            float cap = stats.maxHp * 0.50f;
            float add = Mathf.Min(overheal * 0.20f, Mathf.Max(0f, cap - stats.currentShield));
            if (add > 0f) stats.AddShield(add, 5f);
        }
    }

    private void OnCompanionHealDetailed(float amount, float overheal, HealSource source)
    {
        if (!Active) return;
        // MTX_REG_T4_02: Companion được hồi máu → xóa debuff (CD5s). Tránh nguồn Regen của chính effect.
        if (_module.id == "MTX_REG_T4_02" && source != HealSource.Regen && Time.time >= _cd1)
        {
            _cd1 = Time.time + 5f;
            Cleanse();
        }
    }

    private void Cleanse()
    {
        stats.isStunned = false;
        stats.isBleeding = false;
        stats.damageTakenMultiplier = 1f; // gỡ "dễ tổn thương"
        VisualDebugHelper.DrawSphere(transform.position, 1f, Color.blue, 0.3f);
    }

    // ───────────────────────── COMPANION DAMAGE ─────────────────────────
    private void OnCompanionDamaged(float amount, Stats self)
    {
        if (!Active) return;
        _lastDamageTime = Time.time;

        switch (_module.id)
        {
            case "MTX_PHA_T3_01": // +30% moveSpeed 3s sau khi chịu sát thương
                StartCoroutine(MoveSpeedSurge());
                break;

            case "MTX_PHA_T3_02": // blink 3f + tàng hình 1s, CD5s
                if (Time.time >= _cd1)
                {
                    _cd1 = Time.time + 5f;
                    BlinkAway(3f);
                    StartCoroutine(InvisRoutine(1f));
                }
                break;
        }
    }

    private void OnCompanionDamagedHp(DamageInfo info, float hpLost)
    {
        if (!Active) return;

        switch (_module.id)
        {
            case "MTX_PHA_T4_01": // máu <30% → tàng hình 2s + hồi 20% trong 4s, CD20s
                if (stats.currentHp < stats.maxHp * 0.30f && Time.time >= _cd1)
                {
                    _cd1 = Time.time + 20f;
                    StartCoroutine(InvisRoutine(2f));
                    StartCoroutine(HealOverTime(stats.maxHp * 0.05f, 4));
                }
                break;

            case "MTX_DEF_T4_02": // đòn >5% maxHp → 50% tạo Shield 20% maxHp 3s
                if (hpLost > stats.maxHp * 0.05f && Random.value < 0.5f)
                    stats.AddShield(stats.maxHp * 0.20f, 3f);
                break;

            case "MTX_REG_T5_01": // ngưỡng 75/50/25% → Vùng Thánh Địa r3 5s (CD15s chung)
                CheckSanctuary();
                break;
        }
    }

    // OnBeforeTakeDamage: can thiệp TRƯỚC khi trừ máu (mutate info)
    private void OnCompanionBeforeDamage(DamageInfo info)
    {
        if (!Active || info == null) return;

        // MTX_DEF_T5_01 (Passive): sát thương 1 đòn không vượt 15% maxHp
        if (_module.id == "MTX_DEF_T5_01")
        {
            float cap = stats.maxHp * 0.15f;
            float total = info.TotalRawDamage;
            if (total > cap && total > 0f)
            {
                float scale = cap / total;
                info.physDamage *= scale;
                info.magicDamage *= scale;
                info.trueDamage *= scale;
            }
        }

        // MTX_DEF_T3_02 (Trigger): phản 15% sát thương nhận vào (dạng vật lý)
        if (_module.id == "MTX_DEF_T3_02" && info.attacker != null && info.attacker.CompareTag("Enemy"))
        {
            float reflect = info.TotalRawDamage * 0.15f;
            if (reflect > 0f)
                info.attacker.TakeDamage(new DamageInfo { physDamage = reflect, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Other });
        }
    }

    // ───────────────────────── PLAYER EVENTS ─────────────────────────
    private void OnPlayerHeal(float amount, float overheal)
    {
        if (!Active) return;
        // MTX_REG_T4_01: Player được hồi → Companion hồi lượng tương đương
        if (_module.id == "MTX_REG_T4_01" && amount > 0f)
            stats.Heal(amount, false, false, HealSource.Other);
    }

    private void OnPlayerPerfectDodge()
    {
        if (!Active) return;
        // MTX_PHA_T5_01: Player Perfect Dodge → Companion bất tử 2s
        if (_module.id == "MTX_PHA_T5_01")
            StartCoroutine(InvincibleRoutine(2f));
    }

    private void OnPlayerBeforeDamage(DamageInfo info)
    {
        if (!Active || info == null || playerStats == null) return;
        // MTX_DEF_T4_01: Player máu <30% → Companion gánh 30% sát thương thay
        if (_module.id == "MTX_DEF_T4_01" && playerStats.currentHp < playerStats.maxHp * 0.30f)
        {
            float redirect = info.TotalRawDamage * 0.30f;
            if (redirect <= 0f) return;
            info.physDamage *= 0.70f;
            info.magicDamage *= 0.70f;
            info.trueDamage *= 0.70f;
            stats.TakeDamage(new DamageInfo { trueDamage = redirect, attacker = info.attacker, sourcePosition = transform.position, sourceType = DamageSourceType.Other });
        }
    }

    // ───────────────────────── ROUTINES / HELPERS ─────────────────────────
    private IEnumerator MoveSpeedSurge()
    {
        // tăng từ +15% lên +30% (thêm 15%) trong 3s
        stats.bonusMoveSpeed += 0.15f; stats.CalculateCombatStatsOnly();
        yield return new WaitForSeconds(3f);
        if (stats != null) { stats.bonusMoveSpeed -= 0.15f; stats.CalculateCombatStatsOnly(); }
    }

    private void BlinkAway(float dist)
    {
        Stats e = NearestEnemy(transform.position, 15f);
        Vector3 dir = e != null ? (transform.position - e.transform.position) : -transform.forward;
        dir.y = 0f; dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;
        Vector3 dest = transform.position + dir * dist;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(dest);
        else transform.position = dest;
        VisualDebugHelper.DrawSphere(dest, 0.5f, Color.blue, 0.3f);
    }

    private IEnumerator InvisRoutine(float dur)
    {
        stats.isInvisible = true;
        yield return new WaitForSeconds(dur);
        if (stats != null) stats.isInvisible = false;
    }

    private IEnumerator InvincibleRoutine(float dur)
    {
        stats.isInvincible = true;
        yield return new WaitForSeconds(dur);
        if (stats != null) stats.isInvincible = false;
    }

    private IEnumerator HealOverTime(float perSecond, int seconds)
    {
        for (int i = 0; i < seconds; i++)
        {
            if (stats == null || stats.isDead) yield break;
            stats.Heal(perSecond, false, false, HealSource.Regen);
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator DecoyRoutine()
    {
        // taunt địch quanh Companion 2s
        foreach (var e in EnemiesInRadius(transform.position, 5f)) TauntToCompanion(e);
        Vector3 pos = transform.position;
        VisualDebugHelper.DrawSphere(pos, 1f, new Color(0.6f, 0.3f, 1f, 0.4f), 2f);
        yield return new WaitForSeconds(2f);
        foreach (var e in EnemiesInRadius(pos, 2f)) DealMagic(e, 0.50f);
        VisualDebugHelper.DrawSphere(pos, 2f, Color.magenta, 0.4f);
    }

    private void CheckSanctuary()
    {
        float ratio = stats.currentHp / Mathf.Max(1f, stats.maxHp);
        float[] th = { 0.75f, 0.50f, 0.25f };
        bool crossed = false;
        for (int i = 0; i < 3; i++)
            if (!_regT5Passed[i] && ratio <= th[i]) { _regT5Passed[i] = true; crossed = true; }
        if (!crossed || Time.time < _cd2) return;
        _cd2 = Time.time + 15f;
        SpawnSanctuary(transform.position, 3f, 5f);
    }

    private void SpawnSanctuary(Vector3 center, float radius, float dur)
    {
        StartCoroutine(SanctuaryRoutine(center, radius, dur));
    }
    private IEnumerator SanctuaryRoutine(Vector3 center, float radius, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            // hồi cho Companion + Player nếu đứng trong vùng
            if (Vector3.Distance(transform.position, center) <= radius)
                stats.Heal(stats.maxHp * 0.05f, false, false, HealSource.Regen);
            if (playerStats != null && playerTf != null && Vector3.Distance(playerTf.position, center) <= radius)
                playerStats.Heal(playerStats.maxHp * 0.05f, false, false, HealSource.Regen);
            VisualDebugHelper.DrawSphere(center, radius, new Color(0.3f, 0.8f, 1f, 0.35f), 1f);
            t += 1f;
            yield return new WaitForSeconds(1f);
        }
    }
}
